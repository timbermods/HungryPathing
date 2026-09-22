# Settings

The file is `version-1.1\HungryPathing.cfg` next to `manifest.json`, plain `key = value` lines, `#` starts a
comment. The game reads it once at startup. Every key except `DailyReport` and `Diagnostics` changes what beavers
decide, so in multiplayer every player needs an identical copy; the `Simulation settings:` line in Player.log is
there to compare.

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. `false` leaves the game's behavior untouched; the mod still loads and logs. |
| `Needs` | `Hunger,Thirst` | Needs the planner tracks. Only needs satisfied by consumable goods in storages make sense. |
| `WarningHours` | `0` | Buffer, in hours before the need hits zero, a working beaver keeps. `0` means use each need's `HoursWarningThreshold` from its blueprint; the mod ships Hunger and Thirst at 3. |
| `JustInTime` | `true` | Leave for the closest stocked storage early enough to arrive with the buffer in hand. |
| `JustInTimeLeadHours` | `4` | How many hours before the buffer would be reached the walk starts being measured. Larger values measure earlier and cost more path queries. |
| `PreFuel` | `true` | Top off when food is near and the beaver would not last the rest of the shift plus the buffer. |
| `PreFuelNearFoodHours` | `0.5` | "Near" for pre-fuel: walking time to the storage, in hours. |
| `BuilderJobCheck` | `true` | A builder that has just reserved a site it would not last at lets it go and tops off first. |
| `BuilderJobWorkHours` | `1.0` | Hours of building assumed at the site when judging that. |
| `WorkTimeClosestFood` | `true` | Rank storages by walking time while working. `false` ranks by need points first, as the game does. |
| `VarietyToleranceHours` | `0.25` | With closest-first ranking, a higher-scoring food still wins when it costs at most this much more walking. |
| `RedirectCriticalTrips` | `true` | Apply the same ranking to the game's own penalty-state trips during working hours. |
| `CandidateLimit` | `8` | At most this many storages, nearest by straight line, get a real path query per decision. |
| `RetryHours` | `0.5` | A beaver inside a trigger window that decided nothing waits this long before looking again. A storage that failed to start a trip is left alone for twice this long, and after a penalty-state redirect in which every measured storage failed, the game's own behavior answers for this long. |
| `DailyReport` | `true` | One summary line per in-game day in Player.log. |
| `Diagnostics` | `false` | One line per trip the mod starts, and per site a builder lets go. Verbose. |

## Tuning notes

- The buffer is the main knob. Three hours means a beaver leaves work when the bar shows about 0.1 hunger or 0.09
  thirst, arrives with that still in hand, and eats until another full unit would not fit. Raising it to 6 means
  beavers eat with a third of the bar left, which is more walking during the shift for no penalty benefit unless
  storages are very far apart.
- Pre-fuel at the start of a 16-hour shift triggers for a beaver below about 0.63 hunger or 0.55 thirst (16 hours
  plus the buffer, at the decay rates). Evening eating in the base game usually leaves beavers above that in the
  morning, so the rule mostly catches the ones the evening missed.
- `CandidateLimit` bounds cost, not correctness: the straight-line pre-sort almost always puts the real closest
  storage in the first few.
- To try one rule at a time, keep the others `false`; the daily report says how often each fired.

## Overriding the buffer with data only

The mod ships `Needs\Need.Beaver.Hunger.blueprint.json` and `Needs\Need.Beaver.Thirst.blueprint.json`, each a
partial blueprint that sets only `NeedSpec.HoursWarningThreshold`. Another mod loaded after this one can set a
different value the same way. The value only matters while `WarningHours` is `0`.
