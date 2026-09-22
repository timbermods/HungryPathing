using System;
using System.Collections.Generic;
using BeaverBuddies.Colonies;
using HungryPathing;
using HungryPathing.Planning;

// Checks on the planner arithmetic, the circuit breaker and the MultiColony bridge, plus rules for the Harmony
// hooks read from their source (HarmonyRules.cs). They compile that source directly and need no game files:
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
        Check(FuelPlanner.PickTrip(new List<Candidate> { new Candidate(0.5f, 3f) }, 0.25f, true, 0.5f, preFuelOnly,
                  out reason) == 0 && reason == TripReason.PreFuel,
            "trip: a storage exactly at the near limit is a pre-fuel trip");
        Check(FuelPlanner.ReasonFor(0.5f, preFuelOnly) == TripReason.PreFuel &&
              FuelPlanner.ReasonFor(0.51f, preFuelOnly) == TripReason.None, "trip: the pre-fuel reason ends at the near limit");
        TripRules forced = new TripRules(true, false, true, 6f, 3f, 0.5f);
        Check(FuelPlanner.PickTrip(nearAndBetter, 0.25f, true, float.MaxValue, forced, out reason) == 0 &&
              reason == TripReason.BuilderJob, "trip: the builder's follow-up trip goes to the near storage");
        Check(FuelPlanner.PickTrip(allFar, 0.25f, true, float.MaxValue, forced, out reason) == 0 &&
              reason == TripReason.BuilderJob, "trip: with nothing near, the builder's follow-up trip takes any storage");
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
        // The builder's follow-up trip keeps the near limit when its first storage cannot start a trip, and takes
        // any storage only once no near one is left.
        List<Candidate> forcedRetry = new List<Candidate> { new Candidate(0.4f, 3f), new Candidate(0.45f, 2f), new Candidate(0.6f, 9f) };
        Check(FuelPlanner.PickTrip(forcedRetry, 0.25f, true, float.MaxValue, forced, out reason) == 0 &&
              reason == TripReason.BuilderJob, "trip: the builder's follow-up trip, better of two near storages first");
        forcedRetry.RemoveAt(0);
        Check(FuelPlanner.PickTrip(forcedRetry, 0.25f, true, float.MaxValue, forced, out reason) == 0 &&
              reason == TripReason.BuilderJob, "trip: the builder's follow-up trip, then the other near storage");
        forcedRetry.RemoveAt(0);
        Check(FuelPlanner.PickTrip(forcedRetry, 0.25f, true, float.MaxValue, forced, out reason) == 0 &&
              reason == TripReason.BuilderJob, "trip: the builder's follow-up trip, then the far one");

        // The builder job check itself picks among the near storages, and the trip that follows goes where it chose,
        // which is nearer than the site it let go.
        Check(FuelPlanner.PickBuilderTopOff(nearAndBetter, 0.25f, true, 6f, 3f, 2f, 1f, 1f, 0.5f) == 0,
            "builder pick: the near storage although a better food lies just past the limit");
        int topOff = FuelPlanner.PickBuilderTopOff(nearAndBetter, 0.25f, true, 5f, 3f, 0.55f, 1f, 1f, 0.5f);
        int followUp = FuelPlanner.PickTrip(nearAndBetter, 0.25f, true, float.MaxValue, forced, out reason);
        Check(topOff == 0 && followUp == topOff && nearAndBetter[followUp].TravelHours < 0.55f,
            "builder pick: the follow-up trip goes to the storage that let a 0.55h site go, not past it");
        Check(FuelPlanner.PickBuilderTopOff(nearAndBetter, 0.25f, true, 8f, 3f, 2f, 1f, 1f, 0.5f) == -1,
            "builder pick: enough buffer for the job means no top-off");
        Check(FuelPlanner.PickBuilderTopOff(nearAndBetter, 0.25f, true, 5f, 3f, 0.3f, 1f, 1f, 0.5f) == -1,
            "builder pick: a site nearer than the food means no top-off");
        Check(FuelPlanner.PickBuilderTopOff(allFar, 0.25f, true, 6f, 3f, 2f, 1f, 1f, 0.5f) == -1,
            "builder pick: nothing near means no top-off");
        Check(FuelPlanner.PickBuilderTopOff(prize, 0.25f, false, 6f, 3f, 2f, 1f, 1f, 0.5f) == 2,
            "builder pick: value-first takes the best food among the near ones");

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

        // MultiColonyBridge.cs as shipped. Whether MultiColony is there is fixed for the process, but an exception from
        // it lasts one game, like the breaker: a player who hit one in an earlier game must not keep the game's shift
        // end in the next while a co-op partner with a fresh process asks MultiColony.
        int infos = Log.Infos.Count;
        int warnings = Log.Warnings.Count;
        StubBeaver beaver = new StubBeaver();
        MultiColonyBridge.NewGame();
        Check(Log.Infos.Count == infos, "multicolony: the first load has nothing to re-arm");
        ColonyWorkingHours.Current = new ColonyWorkingHours();
        Check(MultiColonyBridge.TryEndHours(beaver, out float endHours) && Near(endHours, 20f),
            "multicolony: the shift end comes from the beaver's colony");
        string probed = MultiColonyBridge.Description;
        Check(probed.StartsWith("present; "), "multicolony: the probe finds MultiColony's working hours");
        ColonyWorkingHours.ColonySlot = null;
        Check(!MultiColonyBridge.TryEndHours(beaver, out _), "multicolony: a beaver of no colony keeps the game's shift end");
        ColonyWorkingHours.ColonySlot = 1;
        ColonyWorkingHours.ThrowOnce = true;
        Check(!MultiColonyBridge.TryEndHours(beaver, out _), "multicolony: an exception falls back to the game's shift end");
        Check(!MultiColonyBridge.TryEndHours(beaver, out _), "multicolony: the game's shift end for the rest of the game");
        Check(Log.Warnings.Count == warnings + 1 &&
              Log.Warnings[warnings] == "MultiColony: present, but asking it failed (test); " +
                                        "using the game's shift end for the rest of this game",
            "multicolony: one warning per game, saying how long it lasts");
        MultiColonyBridge.NewGame();
        Check(MultiColonyBridge.TryEndHours(beaver, out endHours) && Near(endHours, 20f),
            "multicolony: asked again after the next load");
        Check(Log.Infos.Count == infos + 1 &&
              Log.Infos[infos] == "MultiColony: failure from the previous game cleared; asking it again in this game.",
            "multicolony: the load that re-arms it says so");
        Check(MultiColonyBridge.Description == probed, "multicolony: the next game's log line gives the probe result again");
        MultiColonyBridge.NewGame();
        Check(Log.Infos.Count == infos + 1, "multicolony: a load with nothing to re-arm says nothing");
        ColonyWorkingHours.ThrowOnce = true;
        Check(!MultiColonyBridge.TryEndHours(beaver, out _) && Log.Warnings.Count == warnings + 2,
            "multicolony: a later game can fail and warn again");

        // GameLoad.Reset is the reset the configurator runs at every load, join and rehost. Both bugs were a load that
        // left a switch-off in place, so one call must re-arm the breaker and the MultiColony bridge together.
        Plugin.HooksInstalled = true;
        Safety.Trip("a check before the load", new InvalidOperationException("test"));
        Stats.Evaluations = 5;
        Check(!Safety.Active && !MultiColonyBridge.TryEndHours(beaver, out _),
            "load: the breaker and the MultiColony bridge are both off before the load");
        infos = Log.Infos.Count;
        GameLoad.Reset();
        Check(Safety.Active, "load: the configurator's reset re-arms the breaker");
        Check(MultiColonyBridge.TryEndHours(beaver, out endHours) && Near(endHours, 20f),
            "load: the configurator's reset re-arms the MultiColony bridge");
        Check(Log.Infos.Count == infos + 2 &&
              Log.Infos[infos] == "Breaker from the previous game cleared; active again for this game." &&
              Log.Infos[infos + 1] == "MultiColony: failure from the previous game cleared; asking it again in this game." &&
              Stats.Evaluations == 0,
            "load: the reset says what it re-armed and starts the day's counters over");

        // Guardrail: a unit of food restores a fixed amount, so eating earlier does not change how much is eaten
        // per day. The early eater is ahead by the units it ate before the late one started, and that lead never grows.
        int lead30 = UnitsEatenOver(30, 0.8f, 0.3f, 0.6f) - UnitsEatenOver(30, 0.8f, 0.3f, 0.0f);
        int lead300 = UnitsEatenOver(300, 0.8f, 0.3f, 0.6f) - UnitsEatenOver(300, 0.8f, 0.3f, 0.0f);
        Check(lead30 >= 0 && lead30 <= 2 && lead300 == lead30,
            $"eating earlier does not eat more (lead after 30 days {lead30}, after 300 days {lead300})");

        // The Harmony hooks need the game to compile, so these read their source. In lockstep co-op the order of
        // prefixes on one method must not depend on each player's mod load order, and a failed install must take off
        // only this mod's patches.
        List<(string Name, string Code)> sources = HarmonyRules.ReadModSources();
        List<string> replacing = HarmonyRules.ReplacingPrefixes(sources, out List<string> notLast);
        Check(replacing.Contains("Patches.cs: CriticalDecidePrefix"),
            "harmony: the source scan finds the critical redirect's prefix (found: " + string.Join(", ", replacing) + ")");
        List<string> installed = HarmonyRules.InstalledPrefixes(sources);
        Check(string.Join(", ", installed) == "AddRootBehaviorPrefix, CriticalDecidePrefix",
            "harmony: the source scan reads which hooks Patches.Apply installs as prefixes (found: " +
            string.Join(", ", installed) + ")");
        Check(notLast.Count == 0,
            "harmony: every prefix that can return false has [HarmonyPriority(Priority.Last)] (missing on: " +
            string.Join(", ", notLast) + ")");
        List<string> unscoped = HarmonyRules.UnscopedUnpatches(sources);
        Check(unscoped.Count == 0,
            "harmony: a failed install takes off only this mod's patches (found: " + string.Join(", ", unscoped) + ")");
        // The scan itself, on code that breaks each rule.
        List<(string Name, string Code)> sample = new List<(string Name, string Code)>
        {
            ("Sample.cs", HarmonyRules.CodeOnly(
                "// [HarmonyPriority(Priority.Last)]\n" +
                "private static bool CommentedPrefix(ref bool __result) { return false; }\n" +
                "[HarmonyPriority(Priority.Last)]\nprivate static bool LastPrefix(object __instance) { return true; }\n" +
                "[HarmonyPriority(Priority.First)]\nprivate static bool FirstPrefix(ref bool __result) { return false; }\n" +
                "[HarmonyPrefix] static bool Guard() { return true; }\n" +
                "private static bool Unnamed(object __instance,\n    ref int __result) { return false; }\n" +
                "private static void VoidPrefix(object __instance) { }\n" +
                "private static void Skip(object __instance, ref bool __runOriginal) { __runOriginal = false; }\n" +
                "private static void Watch(bool __runOriginal) { }\n" +
                "private static bool PassPostfix(bool __result) { return __result; }\n" +
                "private static bool Helper(int x) { return x > 0; }\n" +
                "private static bool SkipDecide() => false;\nprivate static void Observe() { }\n" +
                "private static bool Direct() => true;\nprivate static bool Added() => true;\n" +
                "private static bool After() => true;\nprivate static bool Named() => true;\n" +
                "string url = \"http://x // [HarmonyPriority(Priority.Last)]\"; static bool QuotedPrefix() => true;\n" +
                "private static void Install(Harmony h, Type t, string m, string prefix, string postfix = null) { }\n" +
                "Install(harmony, typeof(T), \"M\", nameof(SkipDecide));\n" +
                "Install(harmony, typeof(T), \"M\", nameof(Observe), nameof(Helper));\n" +
                "harmony.Patch(m, new HarmonyMethod(typeof(S), nameof(Direct)), new HarmonyMethod(typeof(S), nameof(PassPostfix)));\n" +
                "harmony.Patch(m, postfix: new HarmonyMethod(typeof(S), nameof(After)),\n" +
                "    prefix: new HarmonyMethod(AccessTools.Method(typeof(S), nameof(S.Named))));\n" +
                "processor.AddPrefix(new HarmonyMethod(typeof(S), nameof(Added)));\n" +
                "harmony.UnpatchAll(id); /* harmony.UnpatchAll(); */ harmony.Unpatch(m, HarmonyPatchType.All);\n" +
                "harmony.Unpatch(m, HarmonyPatchType.Prefix, id); harmony.Unpatch(m, patch);\n" +
                "Log($\"{s.Trim('\"')} // x\"); harmony.UnpatchAll();\n" +
                "harmony.Unpatch(AccessTools.Method(typeof(T), \"M\", new[] { typeof(int) }), HarmonyPatchType.All);\n" +
                "harmony.Unpatch(m, HarmonyPatchType.All, \"*\"); harmony.Unpatch(m, type: HarmonyPatchType.All);\n" +
                "harmony.Unpatch(original: m, type: HarmonyPatchType.All, harmonyID: id);\n" +
                "harmony.Unpatch(m, HarmonyPatchType.All, harmonyID: \"*\"); Log(\"*\");\n"))
        };
        List<string> sampleReplacing = HarmonyRules.ReplacingPrefixes(sample, out List<string> sampleNotLast);
        List<string> sampleInstalled = HarmonyRules.InstalledPrefixes(sample);
        Check(string.Join(", ", sampleReplacing) ==
              "Sample.cs: CommentedPrefix, Sample.cs: LastPrefix, Sample.cs: FirstPrefix, Sample.cs: Guard, " +
              "Sample.cs: Unnamed, Sample.cs: Skip, Sample.cs: SkipDecide, Sample.cs: Direct, Sample.cs: Added, " +
              "Sample.cs: Named, Sample.cs: QuotedPrefix" &&
              string.Join(", ", sampleNotLast) ==
              "Sample.cs: CommentedPrefix, Sample.cs: FirstPrefix, Sample.cs: Guard, Sample.cs: Unnamed, Sample.cs: Skip, " +
              "Sample.cs: SkipDecide, Sample.cs: Direct, Sample.cs: Added, Sample.cs: Named, Sample.cs: QuotedPrefix" &&
              string.Join(", ", sampleInstalled) == "Added, Direct, Named, Observe, SkipDecide",
            "harmony: the scan finds prefixes that can skip the original (bool prefixes by name, attribute, parameters " +
            "or install call, and any prefix that sets __runOriginal), accepts only Priority.Last, and ignores the " +
            "attribute in comments and strings (prefixes: " + string.Join(", ", sampleReplacing) + "; not last: " +
            string.Join(", ", sampleNotLast) + "; installed: " + string.Join(", ", sampleInstalled) + ")");
        List<string> sampleUnscoped = HarmonyRules.UnscopedUnpatches(sample);
        Check(string.Join(" | ", sampleUnscoped) ==
              "Sample.cs: UnpatchAll | Sample.cs: UnpatchAll | " +
              "Sample.cs: Unpatch(m, HarmonyPatchType.All) with no owner | " +
              "Sample.cs: Unpatch(AccessTools.Method(typeof(T), \"\", new[] { typeof(int) }), HarmonyPatchType.All) with no owner | " +
              "Sample.cs: Unpatch(m, HarmonyPatchType.All, \"*\") for every owner | " +
              "Sample.cs: Unpatch(m, type: HarmonyPatchType.All) with no owner | " +
              "Sample.cs: Unpatch(m, HarmonyPatchType.All, harmonyID: \"*\") for every owner",
            "harmony: the scan finds UnpatchAll, also after strings inside an interpolation, and an Unpatch that takes " +
            "off every owner's patches, with no owner or with \"*\", named arguments included (found: " +
            string.Join(" | ", sampleUnscoped) + ")");

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
