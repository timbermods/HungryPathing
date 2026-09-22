# Changelog

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
- Not yet observed in a live colony; see TESTING.md.
