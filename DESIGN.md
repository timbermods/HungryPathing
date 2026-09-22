# Design

This is the reasoning behind the mod, with the game code it rests on. Class names refer to Timberborn 1.1.2.4.

## The problem, measured

The motivating colony: 357 adult beavers, one district, Folktails, day 319. The save was taken at hour 15.8 of a
16-hour shift.

| Hunger bar at that moment | Adults |
|---|---|
| In penalty (0 or below) | 11 |
| 0 to 0.1 (under 3 hours left) | 79 |
| 0.1 to 0.3 (3 to 9 hours left) | 170 |
| 0.3 to 0.7 | 28 |
| Above 0.7 (cannot take a full unit) | 69 |

Thirst looked similar: 4 in penalty, 38 under 0.1, 156 between 0.1 and 0.3.

Each beaver keeps its last ten behavior changes with timestamps in `BehaviorManager.TimestampedBehaviorLog`.
Across the colony those logs held 200 `InventoryNeedBehavior` entries, which are eating and drinking trips. 193 of
them started during working hours. The time from the start of a trip to the beaver's next behavior, which is
roughly the walk, had a median of 0.96 in-game hours, a 75th percentile of 1.44 and a maximum of 2.4. 75 of 178
measurable trips took more than an hour.

## How the base game decides

Needs live in `Timberborn.NeedSystem.Need`. The relevant spec fields, from `Needs\Need.Beaver.Hunger.blueprint.json`
and the Thirst equivalent:

| | Hunger | Thirst |
|---|---|---|
| `DailyDelta` | -0.8 | -0.7 |
| `MaximumValue` | 1.0 | 1.0 |
| `MinimumValue` (death) | -3.0 | -3.0 |
| `ImportanceMultiplier` | 3.0 | 3.5 |
| `Wastable` | false | false |
| `HoursWarningThreshold` | 0.0 | 0.0 |
| Penalty at 0 | working speed -50% | movement speed -25% |

`Need` turns `HoursWarningThreshold` into a points threshold (`|DailyDelta|/24 × hours`) and exposes
`IsBelowWarningThreshold`. Only `BotNeedBehaviorPicker` reads it, through `NeedFilter.NeedIsBelowWarningThreshold`;
bots' Energy and Biofuel needs set 3.5 and 4.5 hours. `BeaverNeedBehaviorPicker` never does.

An adult beaver's root behaviors, in the order `BeaverBehaviorInitializer` adds them: character control, dead,
carry, die, contaminate, **critical needer**, stranded, **worker**, **needer**, wander. `BehaviorManager` asks
them in order whenever the running executor finishes, and takes the first that does not release.

- `CriticalNeederRootBehavior` asks the picker for `GetBestNeedBehaviorAffectingNeedsInCriticalState`, which
  returns nothing unless some need is already in its critical state, meaning at or below zero. This is the only
  way a working beaver leaves a job to eat.
- `NeederRootBehavior`, below work, handles free time with `NeedFilter.AnyNeed`.

Choosing where to eat happens in `DistrictNeedBehaviorService.PickBestAction`. Storages register with the service
per *good group*, keyed by the good's `ConsumptionEffects`: a `Berries` group restores Hunger 0.3; a `Bread` group
restores Hunger 0.3 and Bread 0.2. `AppraiseNeedBehaviors` scores each group with `Appraiser.AppraiseEffects`,
which sums `Need.TryAppraise` over the effects: points that fit, times the need's importance, times a small
attractiveness factor. Groups go into a `SortedSet` ordered by score, highest first. `PickShortestAction` then
walks the groups in that order and returns the first group that has a reachable storage, choosing within the group
by `ActionDurationCalculator.DurationWithReturnInHours`, which is the walk from the beaver to the storage plus the
walk from the storage to the beaver's *home*.

Two consequences:

1. Score decides the group before distance is looked at. Sixteen of the seventeen Hunger goods carry a second
   need worth 0.2 to 0.5 points that is `Wastable` and drains 0.05 a day, so it nearly always has room. Any such
   food outscores berries. A hungry builder next to a small warehouse of berries walks to wherever the winning
   food is.
2. The return leg is measured to home, so a storage between the site and home ties with one near the site.

`Need.TryRawAppraise` with `Wastable = false` returns zero when a full unit would not fit, so a beaver above 0.7
hunger cannot eat at all, and one at 0.65 eats exactly one unit. Eating never overshoots, which is what makes
eating early free.

## What the mod does

### A buffer from the game's own field

Two partial blueprints set `HoursWarningThreshold` to 3 for Hunger and Thirst. The planner reads the value from
`NeedSpec` at runtime, so other data mods can change it and the settings file can override it. Three hours at 0.8
a day is 0.1 hunger; the beaver starts its walk with that in hand and arrives with it.

### The planner's place in the beaver

`HungryPathingRootBehavior` is decorated onto `AdultSpec` and inserted into the root behavior list right before
`WorkerRootBehavior` by a prefix on `BehaviorManager.AddRootBehavior`. It runs only when the beaver is employed,
it is working hours and the beaver does not refuse work, which is exactly the gate `WorkerRootBehavior` uses. So it
is consulted at the same moments the beaver would otherwise pick up its next piece of work, and it can only do two
things: decline, or hand back a decision made by one of the game's own `InventoryNeedBehavior` components. Walking
in, reserving the unit, eating and the animation are all vanilla.

Needs already in their critical state are skipped: the game's critical behavior sits above and owns them.

### Storage ranking

`HungryPathingDistrictIndex` is decorated onto `DistrictCenter` and filled by postfixes on
`DistrictNeedBehaviorService.AddNeedBehavior` and `RemoveNeedBehavior`, so it sees the same registrations the game
does, in the same order. For a need, the planner:

1. takes every group that includes the need, scores it with the game's `Appraiser` (zero drops it, which is the
   "full unit must fit" rule), and collects each storage's `ActionPosition` once at its best group score;
2. sorts by straight-line distance, breaking ties by insertion order;
3. asks `Walker.CalculateTravelTimeInHours` for the nearest `CandidateLimit` storages;
4. picks with `FuelPlanner.PickCandidate`: least walking, except a higher score wins within
   `VarietyToleranceHours`.

The return leg is the walk back to where the beaver stands, which during a shift is the job.

### The rules

With `hoursLeft = points / (|DailyDelta| / 24)` and `buffer = HoursWarningThreshold`:

- **Just in time**: inside `hoursLeft <= buffer + JustInTimeLeadHours`, measure the walk to the best storage and
  go when `hoursLeft - walk <= buffer`. Outside the window the beaver sleeps until it could enter it.
- **Pre-fuel**: when `hoursLeft < hoursToShiftEnd + buffer` and the best storage is within `PreFuelNearFoodHours`,
  go now. Both sides fall one hour per hour, so the first test only changes when the beaver eats or a new shift
  starts; it is re-evaluated at each day change and after every trip.
- **Builder job check**: a postfix on `BuildBehavior.Decide` catches the decision that starts the walk to a freshly
  reserved site. With `travelToSite` from the walker, `siteToFood` the straight-line estimate from the site to the
  nearest scoring storage, and `foodNow` the real walk to the best storage from here: if `foodNow` is near, the
  site is farther than the food, and `hoursLeft - buffer < travelToSite + BuilderJobWorkHours + siteToFood`, the
  builder calls `Builder.Unreserve()` and returns `Decision.ReleaseNextTick()`, which is the game's own path for a
  site it cannot reach. The planner is asked next tick with the need forced and starts the trip. The site goes back
  to the pool.
- **Critical redirect**: a prefix on `CriticalNeederRootBehavior.Decide`. In the work context, if Hunger or Thirst
  is critical, the planner picks the storage its own way for the more important of the two and answers instead of
  the game; anything else falls through to vanilla. One cosmetic side effect: the vanilla picker also refreshes the
  saved set of "needs being critically satisfied" that drives floating status icons for `Action`-type critical
  needs, and a redirected trip skips that refresh. Hunger and Thirst are `State`-type needs with no such icon, so
  the only visible effect would be a stale icon from an earlier action-type trip during a redirected walk.

The builder check reads the site's position from the end of the walk the builder has just started, which is where
the game is sending it. Two ways of asking the site itself are traps: the game's single-access accessor, which
storages use, throws because a site has one access per open neighbour column, and a plain lookup of the site's
`Accessible` throws because the entity also carries the finished building's, disabled until construction ends.
The fallback goes through `ConstructionSiteAccessible`, which names the site's own.

### Guardrails

- **Determinism.** Inputs are need points, spec values, positions, the working-hours manager (or MultiColony's
  per-colony hours), the day-night cycle and the game's path queries. Lists are iterated in insertion order and sorts
  have total tie-breaks. No clock, no randomness, no frame timing. The only static state that changes during a game
  is the log's counters and flags, which never feed a decision, and two switch-offs that do: the circuit breaker (see
  failure containment below) and the MultiColony bridge's (see other mods below). Each trips at the same tick on
  every player, because the code it guards reads only the simulation, and each is cleared only in the configurator,
  which every player runs when a game is loaded, joined or rehosted; nothing clears them mid-game. Whether the hooks
  installed and whether MultiColony's API was found are fixed for the process and are the same on every player with
  the same game and mod versions.
- **Performance.** Cheap checks first: a beaver with hours to spare sets a wake-up time (at most three hours away)
  and returns immediately. Appraisal runs before any path query and removes storages the beaver could not eat at.
  Path queries are capped per decision, and a pre-fuel-only check stops measuring at the first storage whose
  straight-line time already exceeds `PreFuelNearFoodHours`, since the straight line is a lower bound on the walk.
  A storage that turns out empty or unreachable when the trip is launched is dropped from that decision's
  candidates, the next best measured one is tried at once, and the failed one is left alone for twice `RetryHours`.
  This matters because the walker's travel-time query never reports "unreachable": it substitutes the straight-line
  time, so an unreachable storage can look like the best candidate until the launch fails.
- **No saved state.** Both components hold caches only. `BehaviorManager` saves the *running* behavior, and a trip
  the mod starts is recorded as the vanilla `InventoryNeedBehavior`, not as the planner, so a save made mid-trip
  loads without the mod.
- **No oscillation, no blocked work.** The planner never returns a decision without a vanilla behavior behind it;
  when it finds nothing it declines and work proceeds. After a trip the beaver is re-evaluated once; a need that is
  now satisfied does not trigger again until the arithmetic says so.
- **Food economy.** The game's own "full unit must fit" gate stays in force, so consumption per day is unchanged.
  The planner checks include a 300-day simulation of this.
- **Other mods that change working hours.** `InWorkContext` uses `WorkerWorkingHours.AreWorkingHours`, which is
  the per-beaver test other mods patch. The shift end is one global value in the game, `WorkingHoursManager.EndHours`,
  and BeaverBuddies MultiColony keeps one per colony without patching that property. `MultiColonyBridge` finds
  MultiColony's `ColonyWorkingHours` by name at runtime, resolves the beaver's colony with its `ColonyOf` and asks
  `EndHours(slot)`. A mismatch in that API leaves the bridge off for the whole process, since the loaded mods do not
  change while the game runs. An exception switches it off with one warning for the rest of that game and the game's
  value is used; the configurator re-arms it at the next load, like the circuit breaker, so a player who hit one in
  an earlier game does not keep the game's value while a co-op partner with a fresh process asks MultiColony. The
  bridge reads MultiColony's own synchronized state, so peers running both mods stay identical.
- **Failure containment.** Every entry point the game can reach (the root behavior, the two answers the hooks ask
  for, and the hook bodies themselves) catches exceptions. The first one is logged with its stack trace and the
  mod disables itself for the rest of the game; the game's own code never sees an exception from this mod. The next
  load turns it back on. The breaker keeps its own flag and never writes `Enabled`: the settings are read once per
  process and outlive the game, so a trip stored there would carry into every later game on that machine, and a
  player who had tripped it once would run the base game while a co-op partner ran the mod. Hooks that fail to
  install stay off for the whole process.

## What was considered and left out

- Carrying rations into the field. It would need a second carry slot or would collide with hauling, plus
  serialization, UI and balance work. The rules above remove the penalty without it.
- Predicting the destination for haulers. Hauling decisions nest several workplace behaviors with their own
  reservations; unwinding them is not a vanilla path the way `Builder.Unreserve()` is. The shift-based pre-fuel
  and just-in-time rules cover haulers without knowing the destination.
- Touching pathfinding. Nothing in the navigation assemblies is patched.
