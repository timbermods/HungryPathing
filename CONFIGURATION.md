# Settings

Settings are `key = value` lines in `version-1.1\HungryPathing.cfg`, next to `manifest.json`. `#` starts a comment.
The game reads the file at startup, so restart it after editing.

In co-op every player needs an identical copy; only `DailyReport` and `Diagnostics` may differ. Compare the
`Simulation settings:` line each player's Player.log prints at startup.

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. `false` leaves the game's behavior untouched; the mod still loads and logs. |
| `Needs` | `Hunger,Thirst` | Needs the mod plans for. Only needs met by goods in storages make sense. |
| `WarningHours` | `0` | The buffer: hours before a need hits zero that a working beaver keeps in hand. `0` uses the blueprint value: 3 hours for Hunger and Thirst, unless another mod changes it. |
| `JustInTime` | `true` | Leave for the closest stocked storage early enough to arrive with the buffer in hand. |
| `JustInTimeLeadHours` | `4` | How many hours before the buffer the mod starts measuring the walk. Higher values cost more path queries. |
| `PreFuel` | `true` | Top off when food is near and the beaver would not last the rest of the shift plus the buffer. |
| `PreFuelNearFoodHours` | `0.5` | Walking time, in hours, that counts as near for pre-fuel and the builder job check. Both pick only among storages this near. |
| `BuilderJobCheck` | `true` | A builder who has just taken a site it would not last at lets it go and tops off first. |
| `BuilderJobWorkHours` | `1.0` | Hours of building assumed at the site when judging that. |
| `WorkTimeClosestFood` | `true` | Rank storages by walking time while working. `false` ranks by need points first, as the base game does. |
| `VarietyToleranceHours` | `0.25` | A higher-scoring food still wins when it costs at most this many hours more walking. |
| `RedirectCriticalTrips` | `true` | Send the game's own penalty-state trips during working hours to the closest storage too. |
| `CandidateLimit` | `8` | How many storages, nearest in a straight line, get a real path query per need in each decision. |
| `RetryHours` | `0.5` | How long a beaver that decided nothing waits before checking again. A builder's job is checked at most this often. |
| `DailyReport` | `true` | One summary line per in-game day in Player.log. |
| `Diagnostics` | `false` | One line per trip the mod starts, and per site a builder lets go. Verbose. |

Keys ignore case, and on/off keys take `true` or `false`. A value the mod can't read keeps its default. Negative hours
count as `0`, `CandidateLimit` is at least `1` and `RetryHours` at least `0.05`.

## Tuning notes

- **The buffer is the main knob.** At three hours a beaver arrives with about a tenth of the bar left. At six it
  arrives with a fifth left, which means more walking during the shift and helps only when storages are far apart.
- **Pre-fuel** at the start of a 16-hour shift catches beavers below about 0.63 hunger or 0.55 thirst. Evening meals
  usually leave beavers above that, so it mostly catches the ones the evening missed.
- **Tubeways and ziplines** can make a farther storage the quickest to reach. If it isn't among the nearest
  `CandidateLimit`, it is never measured. A higher limit finds it but costs more path queries; the daily line counts
  them.
- **Stairs, ziplines and tubeways** let pre-fuel look farther than half an hour of straight line. The
  `Cheapest travel here:` line in Player.log says how much farther.
- **To try one rule at a time,** set the others to `false`. The daily line says how often each rule fired.

## Setting the buffer from another mod

The buffer comes from two files in `version-1.1\Needs` that set `HoursWarningThreshold` for Hunger and Thirst. A mod
loaded after this one can set a different value the same way. It applies only while `WarningHours` is `0`.
