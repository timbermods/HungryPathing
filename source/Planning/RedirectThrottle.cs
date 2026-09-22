namespace HungryPathing.Planning
{
    // When a beaver's penalty-state redirect may measure storages again. After a pass that tried storages and saw
    // every one of them fail to start a trip, redirects are held back for a while and the game's own critical
    // behavior answers meanwhile. A pass that had nothing to try, or that started a trip, holds nothing back. It
    // reads only the game time it is given.
    public sealed class RedirectThrottle
    {
        private float _heldUntilHours = float.NegativeInfinity;

        public bool IsHeld(float now)
        {
            return FuelPlanner.StillBackedOff(now, _heldUntilHours);
        }

        // A redirect pass at hour now is over. failedLaunches counts the storages it tried that could not start a
        // trip; launched says whether one did.
        public void PassEnded(float now, int failedLaunches, bool launched, float holdHours)
        {
            if (!launched && failedLaunches > 0)
            {
                _heldUntilHours = now + holdHours;
            }
        }
    }
}
