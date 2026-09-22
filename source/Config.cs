using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace HungryPathing
{
    // Plain key=value file next to the manifest. Every value changes what beavers decide, so it is part of the
    // simulation: players in a multiplayer game must use identical files. The fingerprint is logged at startup so
    // two logs can be compared at a glance.
    internal sealed class Config
    {
        public const string FileName = "HungryPathing.cfg";

        public bool Enabled = true;
        public string[] Needs = { "Hunger", "Thirst" };
        public float WarningHours = 0f;
        public bool JustInTime = true;
        public float JustInTimeLeadHours = 4f;
        public bool PreFuel = true;
        public float PreFuelNearFoodHours = 0.5f;
        public bool BuilderJobCheck = true;
        public float BuilderJobWorkHours = 1f;
        public bool WorkTimeClosestFood = true;
        public float VarietyToleranceHours = 0.25f;
        public bool RedirectCriticalTrips = true;
        public int CandidateLimit = 8;
        public float RetryHours = 0.5f;
        public bool DailyReport = true;
        public bool Diagnostics = false;

        public static Config Load(params string[] directories)
        {
            Config config = new Config();
            foreach (string directory in directories)
            {
                if (string.IsNullOrEmpty(directory))
                {
                    continue;
                }
                string path = Path.Combine(directory, FileName);
                if (File.Exists(path))
                {
                    config.Apply(Parse(File.ReadAllLines(path)));
                    Log.Info("Settings read from " + path);
                    return config;
                }
            }
            Log.Info(FileName + " not found; using defaults.");
            return config;
        }

        public static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in lines)
            {
                string line = rawLine;
                int comment = line.IndexOf('#');
                if (comment >= 0)
                {
                    line = line.Substring(0, comment);
                }
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }
                values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            return values;
        }

        public void Apply(Dictionary<string, string> values)
        {
            Enabled = Bool(values, nameof(Enabled), Enabled);
            Needs = List(values, nameof(Needs), Needs);
            WarningHours = Math.Max(0f, Float(values, nameof(WarningHours), WarningHours));
            JustInTime = Bool(values, nameof(JustInTime), JustInTime);
            JustInTimeLeadHours = Math.Max(0f, Float(values, nameof(JustInTimeLeadHours), JustInTimeLeadHours));
            PreFuel = Bool(values, nameof(PreFuel), PreFuel);
            PreFuelNearFoodHours = Math.Max(0f, Float(values, nameof(PreFuelNearFoodHours), PreFuelNearFoodHours));
            BuilderJobCheck = Bool(values, nameof(BuilderJobCheck), BuilderJobCheck);
            BuilderJobWorkHours = Math.Max(0f, Float(values, nameof(BuilderJobWorkHours), BuilderJobWorkHours));
            WorkTimeClosestFood = Bool(values, nameof(WorkTimeClosestFood), WorkTimeClosestFood);
            VarietyToleranceHours = Math.Max(0f, Float(values, nameof(VarietyToleranceHours), VarietyToleranceHours));
            RedirectCriticalTrips = Bool(values, nameof(RedirectCriticalTrips), RedirectCriticalTrips);
            CandidateLimit = Math.Max(1, Int(values, nameof(CandidateLimit), CandidateLimit));
            RetryHours = Math.Max(0.05f, Float(values, nameof(RetryHours), RetryHours));
            DailyReport = Bool(values, nameof(DailyReport), DailyReport);
            Diagnostics = Bool(values, nameof(Diagnostics), Diagnostics);
        }

        // Everything that changes decisions, in a fixed order, so two players can compare one line.
        public string SimulationLine()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return "Simulation settings: " +
                   $"Enabled={Enabled} Needs={string.Join(",", Needs)} WarningHours={WarningHours.ToString(c)} " +
                   $"JustInTime={JustInTime} JustInTimeLeadHours={JustInTimeLeadHours.ToString(c)} " +
                   $"PreFuel={PreFuel} PreFuelNearFoodHours={PreFuelNearFoodHours.ToString(c)} " +
                   $"BuilderJobCheck={BuilderJobCheck} BuilderJobWorkHours={BuilderJobWorkHours.ToString(c)} " +
                   $"WorkTimeClosestFood={WorkTimeClosestFood} VarietyToleranceHours={VarietyToleranceHours.ToString(c)} " +
                   $"RedirectCriticalTrips={RedirectCriticalTrips} CandidateLimit={CandidateLimit} " +
                   $"RetryHours={RetryHours.ToString(c)}. In multiplayer this line must be identical for every player.";
        }

        public override string ToString()
        {
            return SimulationLine() + $" DailyReport={DailyReport} Diagnostics={Diagnostics}";
        }

        private static bool Bool(Dictionary<string, string> values, string key, bool fallback)
        {
            if (values.TryGetValue(key, out string text) && bool.TryParse(text, out bool parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static int Int(Dictionary<string, string> values, string key, int fallback)
        {
            if (values.TryGetValue(key, out string text) &&
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static float Float(Dictionary<string, string> values, string key, float fallback)
        {
            if (values.TryGetValue(key, out string text) &&
                float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static string[] List(Dictionary<string, string> values, string key, string[] fallback)
        {
            if (!values.TryGetValue(key, out string text))
            {
                return fallback;
            }
            List<string> items = new List<string>();
            foreach (string part in text.Split(','))
            {
                string item = part.Trim();
                if (item.Length > 0 && !items.Contains(item))
                {
                    items.Add(item);
                }
            }
            return items.Count > 0 ? items.ToArray() : fallback;
        }
    }
}
