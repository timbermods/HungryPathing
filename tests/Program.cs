using System;
using System.Collections.Generic;
using HungryPathing;
using HungryPathing.Planning;

// Checks on the planner arithmetic and the circuit breaker. They compile that source directly and need no game files:
//   dotnet run --project tests/HungryPathing.Tests.csproj
internal static class Program
{
    private static int _failures;

    private static int Main()
    {
        float hungerDecay = 0.8f / 24f;

        Check(Near(FuelPlanner.HoursUntilZero(0.3f, hungerDecay), 9f), "0.3 hunger lasts 9 hours");
        Check(FuelPlanner.HoursUntilZero(0f, hungerDecay) == 0f, "empty need has no hours left");
        Check(FuelPlanner.HoursUntilZero(-1f, hungerDecay) == 0f, "negative points are not negative hours");
        Check(float.IsPositiveInfinity(FuelPlanner.HoursUntilZero(0.5f, 0f)), "a need that does not decay never runs out");

        Check(FuelPlanner.InJustInTimeWindow(7f, 3f, 4f), "at the edge of the window");
        Check(!FuelPlanner.InJustInTimeWindow(7.01f, 3f, 4f), "just outside the window");
        Check(FuelPlanner.ShouldLeaveNow(5f, 2f, 3f), "leave when the walk eats the buffer");
        Check(!FuelPlanner.ShouldLeaveNow(5f, 1.9f, 3f), "stay while the buffer survives the walk");

        Check(FuelPlanner.WouldNotLastShift(18f, 16f, 3f), "18 hours left, 16 to go plus 3 buffer");
        Check(!FuelPlanner.WouldNotLastShift(19f, 16f, 3f), "19 hours left covers 16 plus 3");
        Check(FuelPlanner.WouldNotLastShift(16f, 14f, 3f) == FuelPlanner.WouldNotLastShift(18f, 16f, 3f),
            "the shift test does not change as the shift runs");
        Check(FuelPlanner.ShouldPreFuel(18f, 16f, 3f, 0.4f, 0.5f), "pre-fuel when food is near");
        Check(!FuelPlanner.ShouldPreFuel(18f, 16f, 3f, 0.6f, 0.5f), "no pre-fuel when food is far");

        Check(FuelPlanner.BuilderShouldTopOff(6f, 3f, 2f, 1f, 1f, 0.3f, 0.5f), "builder: 3h of buffer, 4h job");
        Check(!FuelPlanner.BuilderShouldTopOff(8f, 3f, 2f, 1f, 1f, 0.3f, 0.5f), "builder: 5h of buffer, 4h job");
        Check(!FuelPlanner.BuilderShouldTopOff(6f, 3f, 0.2f, 1f, 1f, 0.3f, 0.5f), "builder: site closer than food");
        Check(!FuelPlanner.BuilderShouldTopOff(6f, 3f, 0.3f, 1f, 1f, 0.3f, 0.5f), "builder: site as close as food");
        Check(!FuelPlanner.BuilderShouldTopOff(6f, 3f, 2f, 1f, 1f, 0.6f, 0.5f), "builder: food not near");

        List<Candidate> mixed = new List<Candidate> { new Candidate(1.0f, 5f), new Candidate(0.5f, 3f), new Candidate(0.6f, 9f) };
        Check(FuelPlanner.PickCandidate(mixed, 0.25f, true) == 2, "closest-first takes the better food within tolerance");
        Check(FuelPlanner.PickCandidate(mixed, 0.05f, true) == 1, "closest-first takes the closest outside tolerance");
        Check(FuelPlanner.PickCandidate(mixed, 0.25f, false) == 2, "value-first takes the most valuable");
        List<Candidate> farPrize = new List<Candidate> { new Candidate(0.3f, 1f), new Candidate(1.0f, 100f) };
        Check(FuelPlanner.PickCandidate(farPrize, 0.25f, true) == 0, "a far prize does not win closest-first");
        Check(FuelPlanner.PickCandidate(farPrize, 0.25f, false) == 1, "a far prize wins value-first");
        List<Candidate> ties = new List<Candidate> { new Candidate(0.5f, 3f), new Candidate(0.5f, 3f) };
        Check(FuelPlanner.PickCandidate(ties, 0.25f, true) == 0, "ties fall to the lowest index");
        Check(FuelPlanner.PickCandidate(new List<Candidate>(), 0.25f, true) == -1, "nothing to pick");
        List<Candidate> reversed = new List<Candidate> { mixed[2], mixed[1], mixed[0] };
        Check(Near(reversed[FuelPlanner.PickCandidate(reversed, 0.25f, true)].Value, 9f),
            "the same storage wins whatever order the list came in");
        // The tolerance window is measured from the closest candidate, so a chain of small steps cannot walk the
        // choice out to a far storage.
        List<Candidate> chain = new List<Candidate> { new Candidate(0.5f, 1f), new Candidate(0.7f, 5f), new Candidate(0.9f, 10f) };
        List<Candidate> chainReversed = new List<Candidate> { chain[2], chain[1], chain[0] };
        Check(Near(chain[FuelPlanner.PickCandidate(chain, 0.25f, true)].Value, 5f), "a chain stops at the tolerance window");
        Check(Near(chainReversed[FuelPlanner.PickCandidate(chainReversed, 0.25f, true)].Value, 5f),
            "a chain stops at the same storage in reverse order");
        List<Candidate> broken = new List<Candidate> { new Candidate(float.NaN, 9f), new Candidate(float.PositiveInfinity, 9f), new Candidate(0.8f, 1f) };
        Check(FuelPlanner.PickCandidate(broken, 0.25f, true) == 2, "unmeasured candidates are never chosen");
        Check(FuelPlanner.PickCandidate(broken, 0.25f, false) == 2, "unmeasured candidates are never chosen value-first");
        List<Candidate> allBroken = new List<Candidate> { new Candidate(float.NaN, 9f) };
        Check(FuelPlanner.PickCandidate(allBroken, 0.25f, true) == -1, "only unmeasured candidates means no pick");

        Check(Near(FuelPlanner.NextCheckDelay(20f, 3f, 4f, false, 0.5f, 3f), 3f), "sleep is capped at the maximum");
        Check(Near(FuelPlanner.NextCheckDelay(9f, 3f, 4f, false, 0.5f, 3f), 2f), "sleep until the window could open");
        Check(Near(FuelPlanner.NextCheckDelay(6f, 3f, 4f, false, 0.5f, 3f), 0.5f), "retry inside the window");
        Check(Near(FuelPlanner.NextCheckDelay(20f, 3f, 4f, true, 0.5f, 3f), 0.5f), "retry while pre-fuel is possible");
        Check(Near(FuelPlanner.NextCheckDelay(float.PositiveInfinity, 3f, 4f, false, 0.5f, 3f), 3f),
            "a need that never runs out still gets checked within the cap");
        Check(Near(FuelPlanner.DelayUntilLeaving(6f, 1f, 3f, 0.5f), 0.5f), "wake-up is bounded by the retry interval");
        Check(Near(FuelPlanner.DelayUntilLeaving(3.5f, 1f, 3f, 0.5f), 0.1f), "wake-up never waits past the leaving time");

        Check(FuelPlanner.StillBackedOff(1.0f, 1.5f), "backed off before the deadline");
        Check(!FuelPlanner.StillBackedOff(1.5f, 1.5f), "a check on the deadline considers the storage again");
        Check(!FuelPlanner.StillBackedOff(2.0f, 1.5f), "not backed off after the deadline");

        // The circuit breaker lasts one game. Every player trips it at the same tick and clears it at the same load,
        // so a trip in one game must not leave that machine running the base game in the next.
        Breaker breaker = new Breaker();
        Check(breaker.IsActive(true), "breaker: active before anything threw");
        Check(!breaker.IsActive(false), "breaker: Enabled=false is never active");
        Check(!breaker.NewGame(), "breaker: an untripped breaker has nothing to clear");
        Check(breaker.Trip(), "breaker: the first trip of a game reports itself");
        Check(!breaker.Trip(), "breaker: a second trip in the same game is silent");
        Check(!breaker.IsActive(true), "breaker: inactive for the rest of the game after a trip");
        Check(!breaker.IsActive(false), "breaker: Enabled=false stays inactive after a trip");
        Check(breaker.NewGame(), "breaker: the next load clears the trip and says so");
        Check(breaker.IsActive(true), "breaker: active again after the next load");
        Check(!breaker.IsActive(false), "breaker: Enabled=false stays inactive after the next load");
        Check(!breaker.NewGame(), "breaker: a second load has nothing left to clear");
        Check(breaker.Trip() && !breaker.IsActive(true), "breaker: trips again in the new game");

        // Safety.cs as shipped. A trip must leave the settings alone, since they outlive the game; missing hooks
        // are the one process-wide switch-off.
        Plugin.Settings.Enabled = true;
        Plugin.HooksInstalled = true;
        Check(Safety.Active, "safety: active with Enabled=true and every hook installed");
        Safety.Trip("the first check", new InvalidOperationException("test"));
        Safety.Trip("the second check", new InvalidOperationException("test"));
        Check(!Safety.Active, "safety: a trip switches the mod off");
        Check(Plugin.Settings.Enabled, "safety: a trip leaves Enabled in the settings alone");
        Check(Log.Warnings.Count == 1 &&
              Log.Warnings[0].StartsWith("Switched off for the rest of this game after an error in the first check."),
            "safety: one warning per game, naming the first failure");
        Safety.NewGame();
        Check(Safety.Active, "safety: active again after the next load");
        Check(Log.Infos.Count == 1 &&
              Log.Infos[0] == "Breaker from the previous game cleared; active again for this game.",
            "safety: the load that clears a trip says so");
        Safety.NewGame();
        Check(Log.Infos.Count == 1, "safety: a load with nothing to clear says nothing");
        Safety.Trip("a check in the next game", new InvalidOperationException("test"));
        Check(!Safety.Active && Log.Warnings.Count == 2, "safety: a later game trips and warns again");
        Safety.NewGame();
        Plugin.Settings.Enabled = false;
        Check(!Safety.Active, "safety: Enabled=false keeps it off");
        Plugin.Settings.Enabled = true;
        Plugin.HooksInstalled = false;
        Safety.NewGame();
        Check(!Safety.Active, "safety: a load does not bring back hooks that failed to install");

        // Guardrail: a unit of food restores a fixed amount, so eating earlier does not change how much is eaten
        // per day. The early eater is ahead by the units it ate before the late one started, and that lead never grows.
        int lead30 = UnitsEatenOver(30, 0.8f, 0.3f, 0.6f) - UnitsEatenOver(30, 0.8f, 0.3f, 0.0f);
        int lead300 = UnitsEatenOver(300, 0.8f, 0.3f, 0.6f) - UnitsEatenOver(300, 0.8f, 0.3f, 0.0f);
        Check(lead30 >= 0 && lead30 <= 2 && lead300 == lead30,
            $"eating earlier does not eat more (lead after 30 days {lead30}, after 300 days {lead300})");

        if (_failures == 0)
        {
            Console.WriteLine("All planner checks passed.");
            return 0;
        }
        Console.WriteLine(_failures + " planner check(s) failed.");
        return 1;
    }

    private static int UnitsEatenOver(int days, float dailyDelta, float unit, float eatAtOrBelow)
    {
        float points = 1f;
        int eaten = 0;
        float hourlyDecay = dailyDelta / 24f;
        for (int hour = 0; hour < days * 24; hour++)
        {
            points -= hourlyDecay;
            while (points <= eatAtOrBelow && 1f - points >= unit)
            {
                points += unit;
                eaten++;
            }
        }
        return eaten;
    }

    private static bool Near(float a, float b)
    {
        return Math.Abs(a - b) < 0.001f;
    }

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            _failures++;
            Console.WriteLine("FAILED: " + what);
        }
    }
}
