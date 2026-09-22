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


        // Pre-fuel and the builder check only ask whether food is near, so they pick among the storages within the
        // near limit. Without the limit, a better food a little past it wins within the variety tolerance and the
        // rule declines although a near storage exists.
        List<Candidate> nearAndBetter = new List<Candidate> { new Candidate(0.4f, 3f), new Candidate(0.6f, 9f) };
        int nearPick = FuelPlanner.PickCandidate(nearAndBetter, 0.25f, true, 0.5f);
        Check(FuelPlanner.PickCandidate(nearAndBetter, 0.25f, true) == 1, "without a limit the better food still wins");
        Check(nearPick == 0, "the near limit leaves out a better food just past it");
        Check(FuelPlanner.ShouldPreFuel(18f, 16f, 3f, nearAndBetter[nearPick].TravelHours, 0.5f),
            "pre-fuel fires when a better food lies just past the near limit");
        Check(FuelPlanner.BuilderShouldTopOff(6f, 3f, 2f, 1f, 1f, nearAndBetter[nearPick].TravelHours, 0.5f),
            "the builder check fires when a better food lies just past the near limit");
        List<Candidate> allFar = new List<Candidate> { new Candidate(0.6f, 9f), new Candidate(0.8f, 1f) };
        Check(FuelPlanner.PickCandidate(allFar, 0.25f, true, 0.5f) == -1, "nothing within the limit means no pick");
        Check(FuelPlanner.PickCandidate(new List<Candidate> { new Candidate(0.5f, 3f) }, 0.25f, true, 0.5f) == 0,
            "a storage exactly at the limit is within it");
        Check(FuelPlanner.PickCandidate(mixed, 0.25f, true, 1.0f) == 2 &&
              Near(reversed[FuelPlanner.PickCandidate(reversed, 0.25f, true, 1.0f)].Value, 9f),
            "a limit that leaves everything in changes nothing, in either order");
        Check(FuelPlanner.PickCandidate(broken, 0.25f, true, 1.0f) == 2, "unmeasured candidates stay out under a limit");
        List<Candidate> window = new List<Candidate> { new Candidate(0.3f, 1f), new Candidate(0.45f, 5f), new Candidate(0.55f, 9f) };
        Check(FuelPlanner.PickCandidate(window, 0.25f, true, 0.5f) == 1, "the tolerance window stops at the limit");
        Check(FuelPlanner.PickCandidate(window, 0.25f, false, 0.5f) == 1, "value-first stops at the limit too");
        Check(FuelPlanner.PickCandidate(ties, 0.25f, true, 0.5f) == 0, "ties under a limit fall to the lowest index");

        // The decision itself: which storage and why, as the root behavior asks it for each need.
        TripReason reason;
        TripRules preFuelOnly = new TripRules(false, false, true, 18f, 3f, 0.5f);
        Check(FuelPlanner.PickTrip(nearAndBetter, 0.25f, true, 0.5f, preFuelOnly, out reason) == 0 &&
              reason == TripReason.PreFuel, "trip: a pre-fuel-only check tops off at the near storage");
        TripRules bothWindows = new TripRules(false, true, true, 6f, 3f, 0.5f);
        Check(FuelPlanner.PickTrip(nearAndBetter, 0.25f, true, float.MaxValue, bothWindows, out reason) == 0 &&
              reason == TripReason.PreFuel, "trip: inside both windows, pre-fuel goes to the near storage now");
        TripRules justInTimeOnly = new TripRules(false, true, false, 6f, 3f, 0.5f);
        Check(FuelPlanner.PickTrip(nearAndBetter, 0.25f, true, float.MaxValue, justInTimeOnly, out reason) == 1 &&
              reason == TripReason.None, "trip: just in time alone waits for the better food");
        TripRules timeToLeave = new TripRules(false, true, true, 3.5f, 3f, 0.5f);
        Check(FuelPlanner.PickTrip(nearAndBetter, 0.25f, true, float.MaxValue, timeToLeave, out reason) == 1 &&
              reason == TripReason.JustInTime, "trip: once it is time to leave, just in time takes the better food");
        TripRules forced = new TripRules(true, false, true, 6f, 3f, 0.5f);
        Check(FuelPlanner.PickTrip(nearAndBetter, 0.25f, true, float.MaxValue, forced, out reason) == 1 &&
              reason == TripReason.BuilderJob, "trip: the builder's follow-up trip is unchanged");
        Check(FuelPlanner.PickTrip(allFar, 0.25f, true, float.MaxValue, bothWindows, out reason) == 0 &&
              reason == TripReason.None, "trip: nothing near and not yet time to leave means no trip");
        Check(FuelPlanner.PickTrip(allFar, 0.25f, true, 0.5f, preFuelOnly, out reason) == -1 &&
              reason == TripReason.None, "trip: a pre-fuel-only check with nothing near picks nothing");
        List<Candidate> prize = new List<Candidate> { new Candidate(1.5f, 20f), new Candidate(0.4f, 3f), new Candidate(0.45f, 4f) };
        Check(FuelPlanner.PickTrip(prize, 0.25f, false, float.MaxValue, bothWindows, out reason) == 2 &&
              reason == TripReason.PreFuel, "trip: value-first pre-fuel takes the best food among the near ones");
        // A near storage that cannot start a trip is dropped and the pick runs again: the near limit still holds.
        List<Candidate> twoNear = new List<Candidate> { new Candidate(0.4f, 3f), new Candidate(0.45f, 2f), new Candidate(0.6f, 9f) };
        Check(FuelPlanner.PickTrip(twoNear, 0.25f, true, float.MaxValue, bothWindows, out reason) == 0 &&
              reason == TripReason.PreFuel, "trip: the better of two near storages first");
        twoNear.RemoveAt(0);
        Check(FuelPlanner.PickTrip(twoNear, 0.25f, true, float.MaxValue, bothWindows, out reason) == 0 &&
              reason == TripReason.PreFuel, "trip: after it fails, the other near storage, not the far one");
        twoNear.RemoveAt(0);
        Check(FuelPlanner.PickTrip(twoNear, 0.25f, true, float.MaxValue, bothWindows, out reason) == 0 &&
              reason == TripReason.None, "trip: with no near storage left, no trip");
        Check(FuelPlanner.PickTrip(new List<Candidate>(), 0.25f, true, float.MaxValue, bothWindows, out reason) == -1 &&
              reason == TripReason.None, "trip: nothing measured means no pick");

        // Measuring stops at the first storage that cannot be near by straight line. On foot a walk costs at least
        // its straight line, but a tubeway tile costs 0.25, so there the straight line alone is no lower bound.
        Check(FuelPlanner.StraightLineRulesOut(0.6f, 0.5f, 1f), "bound: on foot, 0.6h in a straight line is not near");
        Check(!FuelPlanner.StraightLineRulesOut(0.5f, 0.5f, 1f), "bound: on foot, a storage at the limit is measured");
        Check(!FuelPlanner.StraightLineRulesOut(0.6f, 0.5f, 0.25f),
            "bound: 0.6h in a straight line is still measured where a tubeway could make it 0.15h");
        Check(FuelPlanner.StraightLineRulesOut(2.1f, 0.5f, 0.25f), "bound: past four times the limit even tubeways are too slow");
        Check(!FuelPlanner.StraightLineRulesOut(1000f, float.MaxValue, 0.25f), "bound: an unlimited check measures everything");
        bool sameAsBefore = true;
        for (int step = 0; step <= 40; step++)
        {
            float straight = step * 0.025f;
            sameAsBefore &= FuelPlanner.StraightLineRulesOut(straight, 0.5f, 1f) == (straight > 0.5f);
        }
        Check(sameAsBefore, "bound: on foot it is exactly the rule it replaces");

        // The factor is the cheapest edge per tile of straight line among what the game loaded. Numbers from the
        // 1.1.2.4 blueprints: paths 1, stair and slope climbs 0.4 per level, zipline cables 0.4, tubeways 0.25.
        float ground = FuelPlanner.GroundCostPerUnit;
        Check(Near(FuelPlanner.CheapestCostPerUnit(ground, 1f, 1f), 1f), "cost: paths alone leave it at 1");
        float folktails = FuelPlanner.CheapestCostPerUnit(FuelPlanner.CheapestCostPerUnit(ground, 1f, 1f), 0.4f, 1f);
        Check(Near(folktails, 0.4f), "cost: stairs or zipline cables make it 0.4");
        float ironTeeth = FuelPlanner.CheapestCostPerUnit(FuelPlanner.CheapestCostPerUnit(folktails, 0.25f, 1f), 1f, 1f);
        Check(Near(ironTeeth, 0.25f), "cost: tubeways make it 0.25");
        float ironTeethReversed = FuelPlanner.CheapestCostPerUnit(
            FuelPlanner.CheapestCostPerUnit(FuelPlanner.CheapestCostPerUnit(ground, 0.25f, 1f), 1f, 1f), 0.4f, 1f);
        Check(ironTeethReversed == ironTeeth, "cost: the order the buildings come in does not matter");
        Check(FuelPlanner.CheapestCostPerUnit(folktails, 0f, 1f) == folktails &&
              FuelPlanner.CheapestCostPerUnit(folktails, 0f, 3.162f) == folktails,
            "cost: free single steps (gates, onto a zipline) are left out");
        Check(FuelPlanner.CheapestCostPerUnit(folktails, 99999f, 1f) == folktails, "cost: a blocked edge changes nothing");
        Check(Near(FuelPlanner.CheapestCostPerUnit(ground, 1f, 2f), 0.5f), "cost: a long edge counts per tile");
        Check(FuelPlanner.CheapestCostPerUnit(folktails, float.NaN, 1f) == folktails &&
              FuelPlanner.CheapestCostPerUnit(folktails, 0.1f, 0f) == folktails &&
              FuelPlanner.CheapestCostPerUnit(folktails, 0.1f, float.PositiveInfinity) == folktails,
            "cost: unreadable edges are left out");
        Check(!FuelPlanner.StraightLineRulesOut(0.6f, 0.5f, ironTeeth) && FuelPlanner.StraightLineRulesOut(0.6f, 0.5f, ground),
            "bound: with tubeways loaded the 0.6h storage is measured, on foot it is not");

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
