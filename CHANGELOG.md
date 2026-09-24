# Changelog

## 0.3.1 (beta), 2026-09-22

The release to install, replacing 0.2.1 as GitHub's Latest. On top of 0.3.0 it does not change decisions, but coming
from 0.2.1 it brings 0.3.0's changes, which do: all co-op players must update together. Neither 0.3 release has been
observed in a live game yet; the notice's text and timing have offline checks, the dialog itself needs the game.

- A switch-off after an error is now said in the game, not only in Player.log. The mod is built so that an error
  throws on every player at the same tick, since the code reads only the simulation, but it cannot promise that. An
  error only one computer hits leaves that computer on the base game's behavior while the other players run the mod,
  and a co-op game drifts apart from there. When the circuit breaker trips, or the Timber Together shift end
  falls back to the game's, that computer now:
  - shows a dialog that names what switched off and what beavers there do now, and asks co-op players to have the
    host save and host that save again, with every player joining it, before playing on. The load turns the mod back
    on for everyone (since 0.3.0 a switch-off lasts one game), so no restart is needed;
  - writes `Day N: switched off on this computer until a game is loaded: ...` on every later in-game day of that
    game. A player whose mod is switched off writes no daily summary line, so two players' logs still compare day by
    day.

  The dialog is shown from the frame loop, never from inside the tick where the error happened, and changes nothing
  in the simulation.

## 0.3.0 (beta, preview), 2026-09-22

Changes decisions: all co-op players must update together. None of it has been observed in a live game yet; each
change has offline checks that fail on 0.2.2 and pass here (TESTING.md).

- Fixed: after an error, the mod switched itself off for the whole game process, not just the game where the error
  happened. A player who hit one and then loaded, joined or rehosted a co-op game ran the base game while the other
  players ran the mod. The switch-off now lasts for the rest of that game, and the next load turns the mod back on and
  says so in the log. The warning now reads "Switched off for the rest of this game".
- Fixed: when asking Timber Together for a colony's shift end threw, the game's single shift end was used
  for the rest of the process instead of the rest of that game. The fallback now ends at the next load, which asks
  Timber Together again and says so in the log.
- Fixed: pre-fuel and the builder job check declined when the best-scoring food was a little past
  `PreFuelNearFoodHours` while a nearer storage existed. They now choose among the storages within that limit, and a
  builder who lets a site go tops off at one of them.
- Fixed: tubeways, zipline cables and stairs make walks cheaper than their straight line, so storages that look far
  but are near along them were never measured. The straight-line early stop is now scaled by the cheapest path cost
  per tile, read from the loaded buildings once per game and named in Player.log (`Cheapest travel here:`). Pre-fuel
  checks cost more path queries (estimated 4.5 to 5 per evaluation in the reference colony, up from about 3, still
  below 0.1.0). Storages outside the nearest `CandidateLimit` by straight line are still not measured.
- A beaver now remembers every storage that failed to start a trip, each for twice `RetryHours`, instead of only the
  last one. After a penalty-state redirect in which every storage the mod tried failed, the game's own critical
  behavior handles that beaver for `RetryHours` instead of the mod measuring again at every ask. The daily log line
  counts failed launches, just before the path queries.
- The critical redirect's Harmony prefix runs last (`Priority.Last`), so its order against other mods' prefixes no
  longer depends on each player's mod load order. A failed hook install removes only this mod's patches.
- Docs: Folktails is tested; Iron Teeth is expected to work but untested. Sample log lines show `<version>`.
- Site: the download buttons follow GitHub's Latest release, and the newest pre-release only when there is no Latest.
- CI: the planner checks and the new site checks run on every pull request.

## 0.2.2 (beta, preview), 2026-09-22

- Timber Together: a colony with its own working hours now gets the right "hours left in the shift".
  Timber Together patches the per-beaver working-hours test but not the game's `WorkingHoursManager.EndHours`, which
  the pre-fuel rule reads, so beavers of a colony with a 20-hour day were planned against the game's 16. The mod
  now asks Timber Together's `ColonyWorkingHours` for the beaver's own colony through reflection when it is present,
  and uses the game's value otherwise. A log line says which. Not yet observed in a game where a colony has set
  its own hours; found by an audit, not by a beaver.

## 0.2.1 (beta), 2026-09-22

- Fixed: the first day with construction queued tripped the circuit breaker in the builder job check. A
  construction site entity carries two `Accessible` components (its own and the finished building's), so the
  component lookup introduced in 0.2.0 was ambiguous and threw. The check now reads the destination from the walk
  the builder has just started, with the site's dedicated accessible as the fallback. The breaker did its job: the
  session continued with the game's own behavior and one warning in the log.

## 0.2.0 (beta), 2026-09-22

First release after a code review and eight days in a live colony (see TESTING.md).

- Fixed: the builder job check read a construction site's position through the game's single-access accessor,
  which throws for any site with more than one access point. On a construction-heavy day this would have tripped
  the circuit breaker and switched the mod off. The nearest of the site's access points is used now.
- Fixed: a storage that could not start a trip (stock reserved meanwhile, or no way in) was retried on the next
  check instead of being skipped. The next best measured storage is now tried in the same decision, and the
  failed one is left alone for twice `RetryHours`.
- Fixed: the variety tolerance was measured from a running best instead of from the closest storage, so a chain
  of slightly farther, slightly better foods could win by more than the tolerance. It is now measured from the
  closest candidate, which also makes the pick independent of list order.
- Fixed: a builder that let a site go kept walking toward it for a tick if no trip started; the walk is stopped.
- Pre-fuel-only checks no longer measure walks to storages that are already too far by straight line, which
  removes most path queries for beavers with hours to spare in a colony with sparse storages.
- A beaver never sleeps more than three hours between checks, so a changed working day is noticed the same shift.
- Disabled needs and a not-yet-known walking speed on the first tick after a load are skipped instead of
  producing infinite or empty answers; a beaver template missing a component the planner needs keeps the game's
  own behavior instead of tripping the breaker.
- Build script refuses to package when the manifest and assembly versions differ.
- Documentation: results from the live colony, before-and-after at the same point in the shift, and the analysis
  scripts under `tools\analysis`.

## 0.1.0 (alpha), 2026-09-21

First release. Game 1.1.2.4.

- Hunger and Thirst get a 3-hour `HoursWarningThreshold` through two partial blueprints, and the planner honors
  it for beavers the way the game does for bots.
- Just-in-time: working beavers leave for the closest stocked storage early enough to arrive with the buffer.
- Pre-fuel: top off when a storage is within half an hour and the beaver would not last the rest of the shift.
- Builder job check: a builder lets a freshly reserved site go, through the game's own unreserve path, when it
  would run out before getting there, working and walking back, and food is near.
- Closest storage while working, for the mod's own trips and for the game's penalty-state trips during working
  hours; a higher-scoring food wins only within a quarter hour of extra walking.
- Settings file, one daily summary line, optional per-trip diagnostics, planner checks that run without the game.
- Circuit breaker: the first exception in the mod's own code is logged with its stack trace and the mod switches
  itself off for the session.
- First live run: three in-game days in a 330-adult colony, twice from the same save, once as a BeaverBuddies host
  with a guest; no exceptions, identical counters and consistency hashes both times. See TESTING.md.
