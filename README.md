# Hungry Pathing

**Beta.** A Timberborn mod that makes working beavers plan food and water around their shift instead of waiting
for the hunger penalty, and makes them eat at the closest stocked storage while on the job instead of walking
across the map for a fancier meal.

Game version 1.1.2.4. Requires the [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=3284904751)
mod. Nothing is written to save files, so the mod can be added to or removed from any colony.

Website with install guide, troubleshooting and FAQ: [timbermods.github.io/HungryPathing](https://timbermods.github.io/HungryPathing/).
Part of [Timbermods](https://timbermods.github.io/).

## What the base game does

Every beaver need is a bar that drains at a fixed rate. Hunger drains 0.8 per day, thirst 0.7, from a full bar of
1.0. A unit of any food restores 0.3 hunger; a unit of water restores 0.33 thirst. The penalty starts the moment
the bar hits zero: half working speed for hunger, a quarter off movement speed for thirst.

Two things decide when a working beaver goes to eat and where:

- **When.** A beaver at work only interrupts the job once a need is already at zero. The game has a per-need
  "hours of warning" field and uses it for bots (they refuel 3.5 to 4.5 hours early) but every beaver need ships
  with it set to zero, and the beaver's own picker never reads it.
- **Where.** The district service scores each *food group* by how many need points it would restore, sorts the
  groups by score, and only then takes the closest storage of the winning group. Sixteen of the seventeen foods
  carry a second, "variety" need (Bread, Grilled Potatoes, Maple Pastry and so on) that is almost never full, so
  plain berries next to the construction site lose to bread at the district center nearly every time. Which food
  wins rotates as variety needs fill up, which is why the long trips look random.

In a 357-beaver save at the end of a 16-hour shift, 90 adults were within three hours of the hunger penalty and 11
were already in it. Of the last 200 eating trips in the beavers' behavior logs, 193 started during working hours
and the median trip took about an in-game hour of walking. See [DESIGN.md](DESIGN.md) for the numbers
and the code paths behind them.

## What this mod changes

All four rules apply only to employed adults during working hours. Off duty, the game's own behavior runs
unchanged, so the variety-seeking that makes evenings pleasant is not lost.

1. **A buffer, from the game's own field.** Two tiny blueprint patches set `HoursWarningThreshold` to 3 hours for
   Hunger and Thirst, and the mod's planner honors it the way the game already does for bots. Another data mod can
   change the value, or `WarningHours` in the settings file overrides it.
2. **Just in time.** While working, a beaver measures the walk to the closest stocked storage and leaves early
   enough to arrive with the buffer still in hand, instead of after the penalty has started.
3. **Pre-fuel.** When food or water is within half an hour's walk and the beaver would not last the rest of the
   shift plus the buffer, it tops off now rather than after walking off to a far job. At the start of a shift this
   is a breakfast rule; mid-shift it catches a hauler passing a stocked warehouse. A builder who has just reserved
   a construction site and would run out before getting there, working a while and walking back lets the site go
   through the game's own "could not get there" path and tops off first.
4. **Closest storage while working.** For every trip the mod starts, and for the game's own penalty-state trips
   during working hours, the storage is chosen by walking time for the need at hand. A higher-scoring food still
   wins when it costs at most a quarter hour more walking.

A unit of food restores a fixed amount and the game already refuses to eat when a full unit would not fit, so
eating earlier does not eat more. Total consumption per day is unchanged; only its timing moves.

## Install

Unzip the release into `Documents\Timberborn\Mods` so that you have `Mods\HungryPathing\version-1.1\manifest.json`,
then enable the mod in the game's mod manager. Harmony must be installed and enabled as well.

## Settings

`version-1.1\HungryPathing.cfg` next to the manifest. Every value changes what beavers decide; the defaults are the
ones described above. [CONFIGURATION.md](CONFIGURATION.md) explains each key.

## Multiplayer

Every rule reads only the simulation and the game's own path queries; nothing depends on the clock, the frame rate
or random numbers, and storages are ranked in a fixed order with fixed tie-breaks. The counters in the daily log
line never feed back into a decision. Two players running the same version with identical settings files make
identical decisions. At startup the log prints one `Simulation settings:` line; if two players' lines differ, their
games will drift apart.

## Performance

A beaver far from any trigger sleeps until it could reach one, and a beaver inside the window re-checks every half
hour of game time. A check costs at most `CandidateLimit` (default 8) path queries, taken from the storages nearest
by straight line, and only after the game's own appraiser confirmed the beaver can take a full unit. The daily log
line reports evaluations and path queries so the cost is visible.

## Saves

The mod adds two components (one per adult beaver, one per district center) that keep only caches rebuilt from the
simulation. Nothing is saved. A save made with the mod loads without it and the other way round.

## What it did in a real colony

36 in-game days in a 355-adult, single-district Folktails colony, nearly all of them as a BeaverBuddies host with
a guest connected: 33 on 0.1.0 and the first days of 0.2.0. No exceptions, all five hooks installed, the buffer
read 3h. Three of the early days were played twice from the same save and produced identical daily counters and
identical co-op consistency hashes both times, which is the determinism the multiplayer section promises.

Measured an hour before the end of the shift, from the saves themselves:

| | Without the mod | With the mod |
|---|---|---|
| Adults | 357 | 355 |
| In the hunger or thirst penalty | 15 | 0 |
| Within 3 hours of a penalty | 117 | 0 |
| Median walk per working-hour eating trip | 0.96 h | 0.72 h |
| Trips started in the last 4 hours of the shift | 49 | 21 |
| Beavers mid-trip walking past a closer stocked storage | 2 of 18 | 0 of 11 |

Over the 33-day run the rules fired on average 49 just-in-time trips, 133 pre-fuel trips and 15 penalty-state
redirects a day. Path queries were about 8,300 a day on 0.1.0 and 7,500 on 0.2.0, which measures fewer walks that
could not change the answer. The walks that remain are distance to any stocked storage at all: in that colony the
median beaver works 84 tiles from the nearest food. [TESTING.md](TESTING.md) has the full numbers and the method.
Not yet seen: a construction-heavy day for the builder job check, because that colony does not build. If anything
in the mod throws, it logs the stack trace once and switches itself off for the session.

## Checking that it works

Player.log (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log`) shows, in order:

```
[HungryPathing] 0.1.0 loading.
[HungryPathing] Simulation settings: ...
[HungryPathing] Hooks installed (5/5).
[HungryPathing] Active. ...
[HungryPathing] Needs in this game: Hunger: buffer 3h, decays 0.8/day so a full bar lasts 30h; Thirst: ...
[HungryPathing] Day 319: trips started by rule: just-in-time 41, pre-fuel 87, builder job 3 (of 60 job checks), critical redirected 9. 2160 evaluations, 3104 path queries.
```

If the `Needs in this game` line shows a buffer of 0h, the blueprint patches did not load. `Diagnostics = true`
logs one line per trip the mod starts. `tools\run-save.ps1` asks Steam to launch the game straight into a save;
Steam shows a prompt to confirm the extra arguments.

## Building

`.\build.ps1` builds against the game folder, lays the mod out under `dist\` and zips it. `-Install` copies it into
`Documents\Timberborn\Mods`; `-Test` also runs the planner checks, which compile the planner on its own and need no
game files. [TESTING.md](TESTING.md) covers the in-game checks.

## Known limits

- The builder job check knows the destination; other jobs are covered by the shift-based pre-fuel and the
  just-in-time rule, which do not know where the beaver is about to go.
- Storages are ranked by walking time from the beaver to the storage. Return legs assume the beaver goes back to
  where it is now, which is right during a shift and is why off-duty trips are left to the game.
- Only needs satisfied by consumable goods in storages make sense in `Needs`; Hunger and Thirst are the defaults.

## License

MIT. See [LICENSE](LICENSE).
