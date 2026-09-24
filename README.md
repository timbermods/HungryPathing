# Hungry Pathing

**Beta.** A Timberborn mod that times working beavers' food and water to their shift. They eat before the hunger
penalty, at the closest stocked storage, instead of crossing the map for a fancier meal.

- **Game:** Timberborn 1.1.2.4.
- **Requires:** the [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=3284904751) mod, 2.4.1 or newer.
- **Saves:** untouched, so you can add or remove the mod in any colony.
- **Website:** install guide, troubleshooting and FAQ at
  [timbermods.github.io/HungryPathing](https://timbermods.github.io/HungryPathing/).

## Why beavers walk so far

In the base game a working beaver leaves its job only once hunger or thirst hits zero. It then picks the food that
restores the most need points, and only then the closest storage that has it. Most foods also fill a variety need,
so bread at the district center beats berries next to the site. [DESIGN.md](DESIGN.md) has the numbers.

## What it changes

During working hours, for employed adults:

1. **A three-hour buffer.** Hunger and Thirst get three hours of warning, a setting the game already uses for bots.
   Working beavers keep that buffer instead of running the bar to zero.
2. **Just in time.** A beaver measures the walk to the closest stocked storage. It leaves early enough to arrive with
   the buffer still in hand.
3. **Pre-fuel.** If food or water is within half an hour's walk and the beaver won't last the rest of the shift plus
   the buffer, it tops off now.
4. **Builder job check.** A builder who has just taken a site it would not last at lets the site go and tops off
   first.
5. **Closest storage while working.** The mod's trips, and the game's own penalty-state trips, go to the storage with
   the shortest walk. A higher-scoring food still wins if it costs at most a quarter hour more walking.

Off duty, the game's own behavior runs, so evenings keep their variety. Beavers don't eat more; only the timing
moves.

## Install

1. Download `HungryPathing-X.Y.Z.zip` from **Assets** on the
   [latest release](https://github.com/timbermods/HungryPathing/releases/latest), not *Source code*.
2. Close the game. Extract the zip into `Documents\Timberborn\Mods`, so that
   `Mods\HungryPathing\version-1.1\manifest.json` exists.
3. Start the game. In the mod manager, enable **Harmony** and **Hungry Pathing**.

The [install guide](https://timbermods.github.io/HungryPathing/install.html) covers the folder layout, updating and
removing the mod.

## Check that it works

Load a colony, then search `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log` for
`[HungryPathing]`. These lines mean it works:

```
[HungryPathing] <version> loading.
[HungryPathing] Hooks installed (5/5).
[HungryPathing] Needs in this game: Hunger: buffer 3h, ...; Thirst: buffer 3h, ...
```

After each in-game day, a `Day N: trips started by rule: ...` line says how often each rule fired. If a line is
missing, see [Troubleshooting](https://timbermods.github.io/HungryPathing/troubleshooting.html).

## Settings

Settings are `key = value` lines in `version-1.1\HungryPathing.cfg`, next to `manifest.json`. There is no in-game
settings screen. Restart the game after editing. [CONFIGURATION.md](CONFIGURATION.md) explains every key.

## Co-op

Hungry Pathing is built for co-op with BeaverBuddies. Every player needs the same version of the mod, the same game
version and an identical `HungryPathing.cfg`.

Each player's Player.log prints one `Simulation settings:` line at startup. If two players' lines differ, their
games will drift apart.

With [Timber Together](https://timbermods.github.io/TimberTogether/) and separate colonies, each beaver plans by its
own colony's working hours. Beavers only use storages in their own district, so colonies never eat from each other's
storage. Timber Together is optional; the `Timber Together:` line in Player.log says whether it is in use.

## If something goes wrong

- If a game update moves one of the mod's five hooks, the mod stays off and the log says
  `A hook could not be installed`.
- If the mod hits an error, it switches itself off for the rest of that game and shows a dialog. Loading a game
  turns it back on.
- In co-op, the other players may still be running the mod. Before playing on, the host saves and hosts that save
  again, and every player joins it.

Report problems on the [issue tracker](https://github.com/timbermods/HungryPathing/issues) with every
`[HungryPathing]` line from Player.log.

## What it did in a real colony

One single-district Folktails colony of 330 to 355 adults ran it for 36 in-game days, nearly all as a BeaverBuddies
host with a guest. Three days replayed from the same save gave identical counters and co-op hashes. The one error
found there, in the builder job check, is fixed.

Measured an hour before the end of the shift, from the saves:

| | Without the mod | With the mod |
|---|---|---|
| Adults | 357 | 355 |
| In the hunger or thirst penalty | 15 | 0 |
| Within 3 hours of a penalty | 117 | 0 |
| Median walk per working-hour eating trip | 0.96 h | 0.72 h |
| Trips started in the last 4 hours of the shift | 49 | 21 |
| Beavers mid-trip walking past a closer stocked storage | 2 of 18 | 0 of 11 |

The walks that remain are distance to any stocked storage: the median beaver there works 84 tiles from food.
[TESTING.md](TESTING.md) has the full numbers and the method.

**Not played yet:** the current release (offline checks only), the builder rule on a construction-heavy day, a
Timber Together game where a colony sets its own working hours, the switch-off dialog, Iron Teeth, and anything but
Windows. Try it on a copy of your save first.

## Known limits

- Only the builder job check knows where a beaver goes next. Other jobs rely on the shift and the just-in-time rule.
- Tubeways and ziplines can hide the quickest storage from the mod
  ([FAQ](https://timbermods.github.io/HungryPathing/faq.html#factions)).

## For developers

[TESTING.md](TESTING.md) covers building and the checks. [DESIGN.md](DESIGN.md) explains how the mod decides, and
[CHANGELOG.md](CHANGELOG.md) lists every release.

## License

MIT, copyright Timbermods ([LICENSE](LICENSE)). Part of [Timbermods](https://timbermods.github.io/): an unofficial
community mod, not affiliated with or endorsed by Mechanistry.
