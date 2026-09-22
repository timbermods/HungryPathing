# Changelog

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
