using System;
using System.Collections.Generic;
using HungryPathing.Planning;

// Checks on the planner arithmetic. They compile the planner source directly and need no game files:
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

        // Storages that failed to launch a trip, remembered per beaver. Keys are compared by reference, like the
        // game's components.
        object storageA = new object(), storageB = new object(), storageC = new object();
        LaunchBackoff<object> backoff = new LaunchBackoff<object>(16);
        backoff.Add(storageA, 0f, 1.0f);
        backoff.Add(storageB, 0f, 1.0f);
        Check(backoff.IsBackedOff(storageA, 0.5f) && backoff.IsBackedOff(storageB, 0.5f),
            "two storages that failed in one pass are both left alone");
        Check(!backoff.IsBackedOff(storageC, 0.5f), "a storage that never failed is not left alone");
        Check(!backoff.IsBackedOff(storageA, 1.0f) && !backoff.IsBackedOff(storageB, 1.0f),
            "both are considered again on the deadline");
        LaunchBackoff<object> repeat = new LaunchBackoff<object>(16);
        repeat.Add(storageA, 0f, 1.0f);
        repeat.Add(storageA, 0.5f, 1.5f);
        Check(repeat.Count == 1 && repeat.IsBackedOff(storageA, 1.2f), "a repeat failure keeps one entry, later deadline");
        repeat.Add(storageA, 0.6f, 1.1f);
        Check(repeat.Count == 1 && repeat.IsBackedOff(storageA, 1.2f), "a repeat failure never shortens the deadline");
        LaunchBackoff<object> full = new LaunchBackoff<object>(2);
        full.Add(storageA, 0f, 1.0f);
        full.Add(storageB, 0.1f, 1.1f);
        full.Add(storageC, 0.2f, 1.2f);
        Check(full.Count == 2 && !full.IsBackedOff(storageA, 0.5f) && full.IsBackedOff(storageB, 0.5f) &&
              full.IsBackedOff(storageC, 0.5f), "a full list forgets the oldest failure first");
        LaunchBackoff<object> roomy = new LaunchBackoff<object>(2);
        roomy.Add(storageA, 0f, 2.0f);
        roomy.Add(storageB, 0.1f, 1.0f);
        roomy.Add(storageC, 1.05f, 2.05f);
        Check(roomy.Count == 2 && roomy.IsBackedOff(storageA, 1.05f) && roomy.IsBackedOff(storageC, 1.05f),
            "an expired failure makes room before a live one is forgotten");
        LaunchBackoff<object> refreshed = new LaunchBackoff<object>(2);
        refreshed.Add(storageA, 0f, 1.0f);
        refreshed.Add(storageB, 0.1f, 1.1f);
        refreshed.Add(storageA, 0.2f, 1.2f);
        refreshed.Add(storageC, 0.3f, 1.3f);
        Check(refreshed.IsBackedOff(storageA, 0.5f) && !refreshed.IsBackedOff(storageB, 0.5f) &&
              refreshed.IsBackedOff(storageC, 0.5f), "a repeat failure counts as the newest");
        LaunchBackoff<object> expiring = new LaunchBackoff<object>(16);
        expiring.Add(storageA, 0f, 1.0f);
        expiring.Add(storageB, 0.5f, 1.5f);
        expiring.Prune(1.0f);
        Check(expiring.Count == 1 && expiring.IsBackedOff(storageB, 1.0f), "pruning drops only what has expired");
        expiring.Add(storageC, 2.0f, 3.0f);
        Check(expiring.Count == 1 && expiring.IsBackedOff(storageC, 2.0f), "a new failure prunes what has expired");
        Check(LaunchBackoff<object>.CapacityFor(8) == 16 && LaunchBackoff<object>.CapacityFor(1) == 16 &&
              LaunchBackoff<object>.CapacityFor(20) == 40, "the list holds two whole decisions, and at least 16");
        LaunchBackoff<object> wide = new LaunchBackoff<object>(LaunchBackoff<object>.CapacityFor(20));
        object[] widePass = new object[20];
        for (int i = 0; i < widePass.Length; i++)
        {
            widePass[i] = new object();
            wide.Add(widePass[i], 0f, 1.0f);
        }
        Check(wide.Count == 20 && wide.IsBackedOff(widePass[0], 0.5f),
            "a pass that failed at every one of 20 candidates forgets none of them");

        // Penalty-state redirects after a pass in which every storage tried failed to start a trip.
        RedirectThrottle quiet = new RedirectThrottle();
        Check(!quiet.IsHeld(0f), "redirects start out not held back");
        quiet.PassEnded(1.0f, 0, false, 0.5f);
        Check(!quiet.IsHeld(1.0f), "a redirect pass that tried no storage holds nothing back");
        RedirectThrottle launched = new RedirectThrottle();
        launched.PassEnded(1.0f, 2, true, 0.5f);
        Check(!launched.IsHeld(1.0f), "a redirect pass that started a trip holds nothing back");
        RedirectThrottle held = new RedirectThrottle();
        held.PassEnded(1.0f, 3, false, 0.5f);
        Check(held.IsHeld(1.0f) && held.IsHeld(1.49f), "a redirect pass where every storage failed holds back the next");
        Check(!held.IsHeld(1.5f), "held-back redirects measure again on the deadline");

        Check(FuelPlanner.StillBackedOff(1.0f, 1.5f), "backed off before the deadline");
        Check(!FuelPlanner.StillBackedOff(1.5f, 1.5f), "a check on the deadline considers the storage again");
        Check(!FuelPlanner.StillBackedOff(2.0f, 1.5f), "not backed off after the deadline");

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
