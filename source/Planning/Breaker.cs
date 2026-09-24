namespace HungryPathing.Planning
{
    // A switch-off that lasts exactly one game: the circuit breaker's (Safety) and the Timber Together bridge's. It is kept
    // apart from the settings, which outlive the game. It feeds decisions, which makes it simulation state. The code
    // it guards reads only the simulation, so every player should trip it at the same tick (a trip only one player hit
    // is said in the game, see SwitchedOff), and NewGame is called only when a game is loaded, which every player does
    // together (a load, a join or a rehost). Clearing it at any other moment on one player alone would split the
    // players' games.
    public sealed class Breaker
    {
        public bool Tripped { get; private set; }

        // True for the first trip of a game only, so the warning is logged once.
        public bool Trip()
        {
            if (Tripped)
            {
                return false;
            }
            Tripped = true;
            return true;
        }

        // True if a trip from the previous game was cleared.
        public bool NewGame()
        {
            bool wasTripped = Tripped;
            Tripped = false;
            return wasTripped;
        }

        public bool IsActive(bool configuredEnabled)
        {
            return configuredEnabled && !Tripped;
        }
    }
}
