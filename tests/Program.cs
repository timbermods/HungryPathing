using System;
using System.Collections.Generic;
using HungryPathing.Planning;

// Checks on the planner arithmetic, plus rules for the Harmony hooks read from their source (HarmonyRules.cs). They
// compile the planner source directly and need no game files:
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
