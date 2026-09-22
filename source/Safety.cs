using System;

namespace HungryPathing
{
    // Circuit breaker. The first exception anywhere in the mod's own code is logged with its stack trace and the
    // mod switches itself off for the rest of the session, so a bug here costs one log line instead of a beaver
    // that stops deciding or a game hook that throws into vanilla code.
    internal static class Safety
    {
        public static bool Tripped { get; private set; }

        public static void Trip(string where, Exception exception)
        {
            if (Tripped)
            {
                return;
            }
            Tripped = true;
            Plugin.Settings.Enabled = false;
            Log.Warning("Switched off for this session after an error in " + where +
                        ". Beavers behave as in the base game from here on. Please report this with the lines below.\n" +
                        exception);
        }
    }
}
