# Testing

## Planner checks (no game needed)

```
dotnet run --project tests/HungryPathing.Tests.csproj
```

or `.\build.ps1 -Test`. The checks compile `source\Planning\FuelPlanner.cs` on its own and assert the arithmetic
behind each rule: hours left from points and decay, the just-in-time window and leaving time, the shift test and
that it does not change as a shift runs, the builder rule, storage ranking with its tie-breaks in both orders, the
sleep and wake-up delays, and that eating earlier does not eat more over 30 and 300 days.

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
305. Not yet seen: a construction-heavy day for the builder job check, and a before-and-after comparison of eating
trips at the same time of day. The circuit breaker in `Safety.cs` limits the cost of a surprise to one warning.

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
