namespace HungryPathing
{
    // Every piece of static state that must start fresh with each game, reset in one place. The configurator calls
    // this once per load on every player, whenever a game is loaded, joined or rehosted, before any beaver decides.
    // Nothing else may call it, and never mid-game. The offline checks call it too, so they run the same reset the
    // game does: the circuit breaker and the MultiColony bridge are the two switch-offs that feed decisions, and a
    // load that left either one as it was would carry a switch-off from one game into the next on that player only.
    // What the in-game notice reports about them (SwitchedOff) is cleared with them.
    internal static class GameLoad
    {
        public static void Reset()
        {
            Stats.Reset();
            Safety.NewGame();
            MultiColonyBridge.NewGame();
            SwitchedOff.NewGame();
        }
    }
}
