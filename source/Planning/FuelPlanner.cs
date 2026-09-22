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

    // Which rules apply to one need in one decision, and the numbers they compare. Forced is the builder who has
    // just let a site go to top off this need; JustInTime and PreFuel are true inside those rules' windows.
    public readonly struct TripRules
    {
        public readonly bool Forced;
        public readonly bool JustInTime;
        public readonly bool PreFuel;
        public readonly float HoursLeft;
        public readonly float WarningHours;
        public readonly float NearFoodHours;

        public TripRules(bool forced, bool justInTime, bool preFuel, float hoursLeft, float warningHours,
            float nearFoodHours)
        {
            Forced = forced;
            JustInTime = justInTime;
            PreFuel = preFuel;
            HoursLeft = hoursLeft;
            WarningHours = warningHours;
            NearFoodHours = nearFoodHours;
        }
    }

    public static class FuelPlanner
    {
        // Ground and ordinary paths cost one per tile walked, and a walk is never shorter than its straight line.
        public const float GroundCostPerUnit = 1f;

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

        // Index of the storage to use, or -1. Only candidates within maxTravelHours take part, exactly as if the
        // others had not been measured. closestFirst: the least walking wins, except that any candidate
        // within varietyToleranceHours of the closest one may win on value. The window is measured from the
        // closest candidate, never from a running best, so the answer does not depend on list order. Otherwise
        // value wins and walking breaks ties, which is how the base game orders things. Remaining ties fall to
        // the lowest index, so the result is the same on every machine given the same list.
        public static int PickCandidate(IReadOnlyList<Candidate> candidates, float varietyToleranceHours,
            bool closestFirst, float maxTravelHours = float.PositiveInfinity)
        {
            float minTravel = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (InReach(candidates[i], maxTravelHours) && candidates[i].TravelHours < minTravel)
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
                if (!InReach(candidate, maxTravelHours))
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

        // Why to start a trip now to a storage travelHours away, or None. The builder who let a site go always goes.
        public static TripReason ReasonFor(float travelHours, in TripRules rules)
        {
            if (rules.Forced)
            {
                return TripReason.BuilderJob;
            }
            if (rules.JustInTime && ShouldLeaveNow(rules.HoursLeft, travelHours, rules.WarningHours))
            {
                return TripReason.JustInTime;
            }
            if (rules.PreFuel && travelHours <= rules.NearFoodHours)
            {
                return TripReason.PreFuel;
            }
            return TripReason.None;
        }

        // The storage for one decision and the reason to go there now. It is PickCandidate's choice within
        // maxTravelHours, except when that choice gives no reason to go and pre-fuel applies: then the choice among
        // the storages within NearFoodHours is taken instead, since pre-fuel only asks for food nearby. Otherwise a
        // better food just past the near limit, which wins on value, would stop pre-fuel although a near storage
        // exists. Returns -1 when nothing is in reach; an index with TripReason.None is where the beaver would go
        // once it is time, which the just-in-time wake-up is measured against.
        public static int PickTrip(IReadOnlyList<Candidate> candidates, float varietyToleranceHours, bool closestFirst,
            float maxTravelHours, in TripRules rules, out TripReason reason)
        {
            reason = TripReason.None;
            int best = PickCandidate(candidates, varietyToleranceHours, closestFirst, maxTravelHours);
            if (best < 0)
            {
                return -1;
            }
            reason = ReasonFor(candidates[best].TravelHours, rules);
            if (reason == TripReason.None && rules.PreFuel)
            {
                int near = PickCandidate(candidates, varietyToleranceHours, closestFirst,
                    Math.Min(maxTravelHours, rules.NearFoodHours));
                if (near >= 0)
                {
                    reason = ReasonFor(candidates[near].TravelHours, rules);
                    return near;
                }
            }
            return best;
        }

        private static bool InReach(Candidate candidate, float maxTravelHours)
        {
            return candidate.Usable && candidate.TravelHours <= maxTravelHours;
        }

        private static bool Better(Candidate candidate, Candidate best)
        {
            if (candidate.Value != best.Value)
            {
                return candidate.Value > best.Value;
            }
            return candidate.TravelHours < best.TravelHours;
        }

        // True when a storage straightLineHours away in a straight line cannot be within limitHours of walking, so
        // neither can any storage farther by straight line, and none of them needs a path query. The walker's time
        // is the path's cost over its speed, and a path costs at least minCostPerUnit per unit of straight-line
        // distance (see CheapestCostPerUnit). The straight line alone is only a bound on foot: a tubeway tile costs
        // 0.25, so there a storage 0.6h away in a straight line can be 0.15h away.
        public static bool StraightLineRulesOut(float straightLineHours, float limitHours, float minCostPerUnit)
        {
            return straightLineHours * minCostPerUnit > limitHours;
        }

        // Folds one kind of navigation edge into the cheapest cost per unit of straight-line distance, starting
        // from GroundCostPerUnit. An edge costing edgeCost over edgeLength tiles can bring it down. Edges that cost
        // nothing (a gate, the step from a zipline station onto the cable) are left out: each is one short step a
        // walk takes a few times at most, and counting them would make every storage worth measuring.
        public static float CheapestCostPerUnit(float cheapestSoFar, float edgeCost, float edgeLength)
        {
            if (!(edgeCost > 0f) || !(edgeLength > 0f) || float.IsInfinity(edgeLength))
            {
                return cheapestSoFar;
            }
            return Math.Min(cheapestSoFar, edgeCost / edgeLength);
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
