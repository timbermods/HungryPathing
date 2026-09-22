# Testing

## Planner checks (no game needed)

```
dotnet run --project tests/HungryPathing.Tests.csproj
```

or `.\build.ps1 -Test`. The checks compile `source\Planning\FuelPlanner.cs` on its own and assert the arithmetic
behind each rule: hours left from points and decay, the just-in-time window and leaving time, the shift test and
that it does not change as a shift runs, the builder rule, storage ranking with its tie-breaks in both orders and
within the near limit that pre-fuel, the builder check and the builder's follow-up trip pick under, which storage
each rule goes to and why, the straight-line bound on a walk and the cheapest path cost per tile it is scaled by, the
sleep and wake-up delays, and that eating earlier does not eat more over 30 and 300 days.
They also compile `Safety.cs`, `MultiColonyBridge.cs`, `Stats.cs` and `GameLoad.cs` as shipped against the
stand-ins in `tests\Stubs.cs`, and check that the circuit breaker and the MultiColony bridge stay off for the rest of
one game only and come back through `GameLoad.Reset`, the reset the configurator runs at every load.

Finally, they read the hooks' source under `source\` (the hooks need the game's assemblies to compile, the checks do
not) and check two Harmony rules for lockstep co-op: every prefix that can skip the original (one that returns
`bool`, or sets `ref bool __runOriginal`) carries `[HarmonyPriority(Priority.Last)]`, and nothing calls
`UnpatchAll`, or `Unpatch` by patch type with no owner or with `"*"` (both mean every owner), so a failed install
takes off only this mod's patches. A prefix is found by its name, its attributes, its Harmony parameters, or the
`nameof` passed as the prefix where `Patches.Apply` installs it; a check pins the list of installed prefixes, so a
new prefix makes it fail until the list is updated. The scan ignores comments and strings and is itself checked on a
sample that breaks each rule.

## Site checks (no game needed)

```
node tests/test-site.mjs
```

Needs only Node, no packages and no network. It parses every page in `docs\` into a small stub DOM, runs the
scripts each page loads against a fetch that answers like GitHub's API, and checks what the download buttons offer:
GitHub's Latest release, the newest pre-release only when there is no Latest, never a draft, and the page as written
(buttons leading to the Latest release page) when GitHub does not answer. Every link with
`data-release-href="download"` and every `btn` link that says "download" counts as a download button. It also applies
the stylesheet rules that hide parts of a page until the script has run, and fails when a label such as the install
guide's `File:` line shows without its value, or when a sample `loading.` log line on the site or in `README.md`
names a version number instead of `<version>`. `docs\assets\release.js` is the release script the timbermods sites
share; keep it byte-identical to theirs.

GitHub Actions runs both sets of checks on every pull request and every push to main
(`.github\workflows\tests.yml`).

## In the game

`tools\run-save.ps1` starts the game straight into a save; the game itself reads `-settlementName` and `-saveName`
from the command line.

```
.\tools\run-save.ps1 -Settlement "Romans missing leg" -Save "2026-09-21 13h12m, Day 17-3.autosave" -Seconds 300
```

With `-Seconds` it closes the game after that long and prints the mod's log lines. Set `Diagnostics = true` in the
installed settings file first to see individual trips. The script goes through `steam.exe -applaunch` because
`Timberborn.exe` started on its own hands over to Steam and loses its arguments; Steam then shows a prompt to
confirm the arguments before the game starts.

Status for 0.1.0: first live run done on 2026-09-21 in a 330-adult, single-district Folktails colony, days 302 to
304, played twice from the same autosave: once alone, once as a BeaverBuddies host with a guest whose mod list
matched. No exceptions. The daily lines from both runs:

```
Day 302: just-in-time 52, pre-fuel 93,  builder job 0 (of 0 job checks), critical redirected 12. 1341 evaluations, 9128 path queries.
Day 303: just-in-time 52, pre-fuel 144, builder job 0 (of 1 job checks), critical redirected 15. 1247 evaluations, 8040 path queries.
Day 304: just-in-time 50, pre-fuel 135, builder job 0 (of 0 job checks), critical redirected 12. 1153 evaluations, 7520 path queries.
```

Identical to the counter in the second run, alongside identical BeaverBuddies consistency hashes for days 303 to
305. Days 305 to 308 followed the same pattern: just-in-time 38 to 48, pre-fuel 117 to 137, penalty-state
redirects 12 to 15 a day.

### Before and after, same point in the shift

A save at 15.1 hours into day 309 with the mod, against the day-319 baseline at 15.8 hours without it. Both are
about an hour before the shift ends; the analysis reads each beaver's need points and its last ten behavior
changes from the save.

| | Without the mod (day 319) | With the mod (day 309) |
|---|---|---|
| Adults | 357 | 355 |
| In the hunger penalty | 11 | 0 |
| In the thirst penalty | 4 | 0 |
| Within 3 hours of the hunger penalty | 79 | 0 |
| Within 3 hours of the thirst penalty | 38 | 0 |
| Mean hunger bar | 0.30 | 0.38 |
| Eating right now | 22 | 11 |
| Working-hour eating trips in the logs | 171 | 153 |
| Median walk per trip | 0.96 h | 0.72 h |
| Trips over an hour | 43% | 39% |
| Trips started in the last four hours of the shift | 49 | 21 |

Of the beavers mid-trip in the mod save, all 11 were walking to the nearest stocked storage of the right kind
(none passed a closer one by more than 20 tiles); in the baseline 2 of 18 were, both after Maple Pastry. The
remaining long walks are distance to any stocked storage at all, not choice: that colony keeps food in 22
warehouses and water in 29 tanks, the median adult works 84 tiles from the nearest stocked food and 85 from water,
and 280 of 355 adults are more than 60 tiles from food. Not yet seen: a construction-heavy day for the builder job
check. The circuit breaker in `Safety.cs` limits the cost of a surprise to one warning.

### The long run on 0.1.0

The session that produced the saves above went on: 33 in-game days, 302 to 334, all as a BeaverBuddies host with
a guest, without an error. Averages per day over those 33 days: 49 just-in-time trips, 133 pre-fuel trips, 15
penalty-state redirects, 1,250 evaluations and 8,300 path queries (6.7 per evaluation). Builder job checks: 0,
because the colony did no building in that stretch.

### 0.2.0 in the same colony

0.2.0 was installed and a save from day 315 loaded, again as a co-op host with a matching mod list. All five hooks
installed, buffer 3h, no exceptions or warnings. The same days had already been played on 0.1.0, which gives a
like-for-like view of what the review fixes changed:

| Day | 0.1.0 just-in-time / pre-fuel / redirects | 0.1.0 path queries | 0.2.0 just-in-time / pre-fuel / redirects | 0.2.0 path queries |
|---|---|---|---|---|
| 316 | 77 / 129 / 10 | 10,072 | 77 / 135 / 9 | 7,016 |
| 317 | 104 / 162 / 10 | 13,488 | 89 / 138 / 20 | 7,839 |
| 318 | 77 / 139 / 11 | 12,544 | 82 / 150 / 9 | 7,816 |

Path queries per evaluation fell from 6.7 to 3.0: a pre-fuel-only check now stops measuring at the first storage
that is already too far by straight line. Evaluations roughly doubled because a beaver never sleeps more than
three hours between checks, but those extra evaluations measure nothing and cost a few comparisons each, so the
net effect is 30 to 40 percent fewer path queries per day. Trip counts differ a little from the 0.1.0 replay, as
expected from the changed tolerance and backoff logic; the two versions are not meant to make identical decisions.

The 9 to 20 penalty-state redirects a day are beavers that reach zero inside a long work task, which the planner
does not interrupt by design; they now walk to the closest storage.

### The builder job check, first contact

On day 321 construction was queued for the first time in this colony, and the first builder job check tripped the
circuit breaker:

```
Switched off for this session after an error in HungryPathingRootBehavior.BuilderShouldTopOffFirst.
System.InvalidOperationException: More than one component of type Timberborn.Navigation.Accessible found in TripleLodge.Folktails(Clone)
```

A construction site entity carries two `Accessible` components, its own and the finished building's, so the
component lookup that 0.2.0 introduced to avoid the single-access accessor was ambiguous. The breaker worked as
intended: the session continued on the game's own behavior with one warning. 0.2.1 reads the destination from the
walk the builder has just started instead. The rule's own decisions, whether a hungry builder actually lets a far
site go and tops off first, are still to be observed on a construction day with 0.2.1.

### MultiColony working hours (0.2.2)

An audit found that the pre-fuel rule read the game's single shift end while MultiColony keeps one per colony.
0.2.2 asks MultiColony for the beaver's colony. The sessions above all ran MultiColony, but no colony had set its
own hours, so both code paths give the same answer there; a game where one colony chose a different working day
is still to be observed. The `MultiColony:` log line after `Needs in this game` says whether the bridge is active.

## Reproducing the analysis

A `.timber` save is a zip; extract `world.json` from it. The scripts in `tools\analysis` read that file with
Python 3 and no extra packages:

```
python tools\analysis\needs_and_trips.py "label" path\to\world.json ["label 2" path\to\other\world.json ...]
python tools\analysis\trip_destinations.py "label" path\to\world.json
python tools\analysis\food_coverage.py path\to\world.json
```

- `needs_and_trips.py`: how many adults are in or near a penalty, and the walking time of recent eating trips
  from each beaver's last ten behavior changes, split into working hours and off duty.
- `trip_destinations.py`: for every beaver currently on an eating trip, how far its chosen storage is against the
  nearest stocked storage of the same kind.
- `food_coverage.py`: how far every adult is from stocked food and water, and which map cells hold the most
  beavers far from both, which is where a new storage would pay off.

Compare saves taken at the same point in the shift; a save's hours-passed value is in `DayNightCycle.DayProgress`
times 24, and the scripts print it.

What to look for in Player.log:

1. `Hooks installed (5/5).` All five hooks found their targets on this game version. If a game update moves one,
   the line becomes a warning and the mod disables itself for the session.
2. `Needs in this game: Hunger: buffer 3h ...`. Printed by the first beaver the planner evaluates. A buffer of 0h
   means the blueprint patches were not applied (wrong folder, or another mod overrides them).
3. `Day N: trips started by rule: ...`. Once per in-game day. Just-in-time and pre-fuel counts in the dozens or
   hundreds for a colony of a few hundred are normal; builder job counts are small because builders that start a
   shift topped off never trigger it.
4. No `HungryPathing` in any exception stack trace.

## Comparing with the base game

The save's behavior logs give a before picture: each beaver keeps its last ten behavior changes with timestamps.
`InventoryNeedBehavior` entries during working hours are eating trips; the gap to the next entry is roughly the
walk. The 357-beaver save that motivated the mod had 193 of 200 recent trips during working hours with a median of
about one in-game hour. After a day with the mod, the same measurement on a new save should show shorter gaps and
more of the trips near the start of the shift.
