using System;
using HungryPathing.Planning;

namespace HungryPathing
{
    // Circuit breaker. The first exception anywhere in the mod's own code is logged with its stack trace and the
    // mod switches itself off for the rest of the game, so a bug here costs one log line instead of a beaver that
    // stops deciding or a game hook that throws into vanilla code. The next load turns it back on. The state lives
    // in a Breaker, never in the settings: those are read once per process and outlive the game, and a player who
    // carried a trip into the next game would run the base game while the other players run the mod.
    internal static class Safety
    {
        private static readonly Breaker State = new Breaker();

        // The one gate every entry point checks: the settings file enables the mod, all five hooks went in at
        // startup, and nothing has thrown in this game. All three are the same on every player of one game.
        public static bool Active => State.IsActive(Plugin.Settings.Enabled && Plugin.HooksInstalled);

        public static void Trip(string where, Exception exception)
        {
            if (!State.Trip())
            {
                return;
            }
            Log.Warning("Switched off for the rest of this game after an error in " + where +
                        ". Beavers behave as in the base game until a game is loaded. Please report this with the " +
                        "lines below.\n" + exception);
        }

        // Only the configurator calls this, and it runs on every player whenever a game is loaded, joined or
        // rehosted, before any beaver decides. Never call it mid-game.
        public static void NewGame()
        {
            if (State.NewGame())
            {
                Log.Info("Breaker from the previous game cleared; active again for this game.");
            }
        }
    }
}
