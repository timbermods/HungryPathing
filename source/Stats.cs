namespace HungryPathing
{
    // Counters for the daily log line. They never feed back into a decision, so they may differ between
    // multiplayer peers without consequence.
    internal static class Stats
    {
        public static int Evaluations;
        public static int PathQueries;
        public static int JustInTime;
        public static int PreFuel;
        public static int BuilderJob;
        public static int BuilderChecks;
        public static int CriticalRedirect;
        public static int FailedLaunches;

        public static bool ThresholdsLogged;

        private static int _reportedDay = -1;

        public static void Reset()
        {
            Evaluations = PathQueries = JustInTime = PreFuel = BuilderJob = BuilderChecks = CriticalRedirect =
                FailedLaunches = 0;
            _reportedDay = -1;
            ThresholdsLogged = false;
        }

        public static void Count(Planning.TripReason reason)
        {
            switch (reason)
            {
                case Planning.TripReason.JustInTime:
                    JustInTime++;
                    break;
                case Planning.TripReason.PreFuel:
                    PreFuel++;
                    break;
                case Planning.TripReason.BuilderJob:
                    BuilderJob++;
                    break;
                case Planning.TripReason.CriticalRedirect:
                    CriticalRedirect++;
                    break;
            }
        }

        public static void ReportIfNewDay(int day)
        {
            if (_reportedDay == -1)
            {
                _reportedDay = day;
                return;
            }
            if (day == _reportedDay)
            {
                return;
            }
            if (Plugin.Settings.DailyReport)
            {
                Log.Info($"Day {_reportedDay}: trips started by rule: just-in-time {JustInTime}, pre-fuel {PreFuel}, " +
                         $"builder job {BuilderJob} (of {BuilderChecks} job checks), critical redirected {CriticalRedirect}. " +
                         $"{Evaluations} evaluations, {FailedLaunches} failed launches, {PathQueries} path queries.");
            }
            Evaluations = PathQueries = JustInTime = PreFuel = BuilderJob = BuilderChecks = CriticalRedirect =
                FailedLaunches = 0;
            _reportedDay = day;
        }
    }
}
