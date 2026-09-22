using System;
using System.Collections.Generic;

namespace HungryPathing.Planning
{
    // The arithmetic behind every decision the mod makes. No game types, so it can be checked without the game.
    // Times are in in-game hours. "hoursLeft" is how long until a need reaches zero at its current decay rate;
    // "warningHours" is the buffer a working beaver keeps in hand (the need's HoursWarningThreshold).

    public enum TripReason
    {
        None,
        JustInTime,
        PreFuel,
        BuilderJob,
        CriticalRedirect
    }

    public readonly struct Candidate
    {
        public readonly float TravelHours;
        public readonly float Value;

        public Candidate(float travelHours, float value)
        {
            TravelHours = travelHours;
            Value = value;
        }

        // A candidate whose walk could not be measured (no speed yet, or an overflow) is never chosen.
        public bool Usable => !float.IsNaN(TravelHours) && !float.IsInfinity(TravelHours) && !float.IsNaN(Value);
    }

    public static class FuelPlanner
    {
        // A need that never decays never runs out.
        public static float HoursUntilZero(float points, float hourlyDecay)
        {
            if (hourlyDecay <= 0f)
            {
                return float.PositiveInfinity;
            }
            return Math.Max(0f, points) / hourlyDecay;
        }

        // Close enough to the buffer that walking time is worth measuring.
        public static bool InJustInTimeWindow(float hoursLeft, float warningHours, float leadHours)
        {
            return hoursLeft <= warningHours + leadHours;
        }

        // Leave now so the beaver arrives with the buffer still in hand.
        public static bool ShouldLeaveNow(float hoursLeft, float travelHours, float warningHours)
        {
            return hoursLeft - travelHours <= warningHours;
        }

        // Both sides fall at one hour per hour during a shift, so this only changes when the beaver eats or a
        // new shift starts.
        public static bool WouldNotLastShift(float hoursLeft, float hoursToShiftEnd, float warningHours)
        {
            return hoursLeft < hoursToShiftEnd + warningHours;
        }

        public static bool ShouldPreFuel(float hoursLeft, float hoursToShiftEnd, float warningHours, float travelHours,
            float nearFoodHours)
        {
            return travelHours <= nearFoodHours && WouldNotLastShift(hoursLeft, hoursToShiftEnd, warningHours);
        }

        // A builder about to walk to a site: top off first when food is near now, the site is farther than the
        // food, and the walk there, a stretch of work and the walk from the site to food would use up the buffer.
        public static bool BuilderShouldTopOff(float hoursLeft, float warningHours, float travelToSite,
            float workHours, float siteToFoodHours, float travelToFoodNow, float nearFoodHours)
        {
            if (travelToFoodNow > nearFoodHours || travelToSite <= travelToFoodNow)
            {
                return false;
            }
            return hoursLeft - warningHours < travelToSite + workHours + siteToFoodHours;
        }

        // Index of the storage to use, or -1. closestFirst: the least walking wins, except that any candidate
        // within varietyToleranceHours of the closest one may win on value. The window is measured from the
        // closest candidate, never from a running best, so the answer does not depend on list order. Otherwise
        // value wins and walking breaks ties, which is how the base game orders things. Remaining ties fall to
        // the lowest index, so the result is the same on every machine given the same list.
        public static int PickCandidate(IReadOnlyList<Candidate> candidates, float varietyToleranceHours,
            bool closestFirst)
        {
            float minTravel = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Usable && candidates[i].TravelHours < minTravel)
                {
                    minTravel = candidates[i].TravelHours;
                }
            }
            if (float.IsPositiveInfinity(minTravel))
            {
                return -1;
            }
            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                if (!candidate.Usable)
                {
                    continue;
                }
                if (closestFirst && candidate.TravelHours > minTravel + varietyToleranceHours)
                {
                    continue;
                }
                if (best < 0 || Better(candidate, candidates[best]))
                {
                    best = i;
                }
            }
            return best;
        }

        private static bool Better(Candidate candidate, Candidate best)
        {
            if (candidate.Value != best.Value)
            {
                return candidate.Value > best.Value;
            }
            return candidate.TravelHours < best.TravelHours;
        }

        // How long a beaver that decided nothing can skip evaluating. Inside a window it checks again after
        // retryHours; well above the window it sleeps until it could enter one, since nothing else changes, but
        // never longer than maxSleepHours so a changed working day or an infinite need is noticed within hours.
        public static float NextCheckDelay(float hoursLeft, float warningHours, float leadHours, bool preFuelEligible,
            float retryHours, float maxSleepHours)
        {
            if (preFuelEligible || InJustInTimeWindow(hoursLeft, warningHours, leadHours))
            {
                return retryHours;
            }
            return Math.Max(retryHours, Math.Min(maxSleepHours, hoursLeft - (warningHours + leadHours)));
        }

        // Inside the window with a storage in reach but not yet time to go: wake up when it will be, bounded so
        // a beaver that keeps moving is re-measured now and then.
        public static float DelayUntilLeaving(float hoursLeft, float travelHours, float warningHours, float retryHours)
        {
            float untilLeaving = hoursLeft - travelHours - warningHours;
            return Math.Max(0.1f, Math.Min(retryHours, untilLeaving));
        }

        // A storage that failed to launch a trip stays out of the running until this returns false. Exclusive, so
        // a check that lands exactly on the deadline already considers it again.
        public static bool StillBackedOff(float now, float untilHours)
        {
            return now < untilHours;
        }
    }
}
