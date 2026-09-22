using System;
using System.Collections.Generic;
using HungryPathing.Planning;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Common;
using Timberborn.ConstructionSites;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSpecs;
using Timberborn.NeedSystem;
using Timberborn.TimeSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace HungryPathing
{
    // Sits in each adult beaver's root behavior list just above WorkerRootBehavior (see Patches), so it is asked at
    // the same moments the game would hand the beaver its next piece of work. It only ever answers with one of the
    // game's own storage need behaviors (walk in, take a unit, eat), or declines. It keeps no saved state: every
    // field here is a cache that is rebuilt from the simulation after a load.
    public class HungryPathingRootBehavior : RootBehavior, IAwakableComponent
    {
        private struct Scored
        {
            public NeedBehavior Behavior;
            public float Value;
            public float HeuristicHours;
            public float FromSiteHours;
            public Vector3 Position;
            public int Order;
        }

        // A beaver with hours to spare sleeps this long at most between evaluations, so a changed working day is
        // noticed the same shift. Evaluations that measure nothing cost a few comparisons.
        private const float MaxSleepHours = 3f;

        // Straight-line order, then insertion order: List.Sort is unstable, so the tie-break keeps it deterministic.
        private static readonly Comparison<Scored> ByHeuristic = (a, b) =>
        {
            int byHours = a.HeuristicHours.CompareTo(b.HeuristicHours);
            return byHours != 0 ? byHours : a.Order.CompareTo(b.Order);
        };

        private static bool _missingComponentLogged;

        private readonly IDayNightCycle _dayNightCycle;
        private readonly WorkingHoursManager _workingHoursManager;
        private readonly TravelCostBound _travelCostBound;

        private Worker _worker;
        private WorkerWorkingHours _workerWorkingHours;
        private WorkRefuser _workRefuser;
        private Citizen _citizen;
        private NeedManager _needManager;
        private Appraiser _appraiser;
        private Walker _walker;
        private WalkerSpeedManager _walkerSpeedManager;
        private bool _ready;

        private readonly List<Scored> _scored = new List<Scored>();
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<NeedBehavior> _candidateBehaviors = new List<NeedBehavior>();
        private readonly List<string> _needOrder = new List<string>();
        private readonly List<float> _needHoursLeft = new List<float>();
        private float _siteToFoodHours;

        private float _nextCheckHours = float.NegativeInfinity;
        private int _lastDay = int.MinValue;
        private string _forcedNeedId;
        private float _forcedUntilHours;
        private float _nextBuilderCheckHours = float.NegativeInfinity;
        private NeedBehavior _backoffBehavior;
        private float _backoffUntilHours;

        // Set by the patch that puts this behavior into the root behavior list.
        internal bool Registered;

        public HungryPathingRootBehavior(IDayNightCycle dayNightCycle, WorkingHoursManager workingHoursManager,
            TravelCostBound travelCostBound)
        {
            _dayNightCycle = dayNightCycle;
            _workingHoursManager = workingHoursManager;
            _travelCostBound = travelCostBound;
        }

        public void Awake()
        {
            _worker = GetComponent<Worker>();
            _workerWorkingHours = GetComponent<WorkerWorkingHours>();
            _workRefuser = GetComponent<WorkRefuser>();
            _citizen = GetComponent<Citizen>();
            _needManager = GetComponent<NeedManager>();
            _appraiser = GetComponent<Appraiser>();
            _walker = GetComponent<Walker>();
            _walkerSpeedManager = GetComponent<WalkerSpeedManager>();
            _ready = _worker && _workerWorkingHours && _workRefuser && _citizen && _needManager && _appraiser &&
                     _walker && _walkerSpeedManager;
            if (!_ready && !_missingComponentLogged)
            {
                // A modded beaver template without one of these parts: the planner stays off for it, and only it.
                _missingComponentLogged = true;
                Log.Warning(Name + " lacks a component the planner needs (worker, needs, walker or district); " +
                            "beavers like it keep the game's own behavior.");
            }
        }

        public override Decision Decide(BehaviorAgent agent)
        {
            if (!Plugin.Settings.Enabled || !_ready)
            {
                return Decision.ReleaseNow();
            }
            try
            {
                return DecideUnguarded(agent);
            }
            catch (Exception exception)
            {
                Safety.Trip(nameof(HungryPathingRootBehavior) + ".Decide", exception);
                return Decision.ReleaseNow();
            }
        }

        private Decision DecideUnguarded(BehaviorAgent agent)
        {
            Config settings = Plugin.Settings;
            Stats.ReportIfNewDay(_dayNightCycle.DayNumber);
            if (!InWorkContext() || !_citizen.HasAssignedDistrict)
            {
                return Decision.ReleaseNow();
            }
            float now = Now();
            if (_lastDay != _dayNightCycle.DayNumber)
            {
                // A new shift: the "would not last the shift" test has a new answer.
                _lastDay = _dayNightCycle.DayNumber;
                _nextCheckHours = float.NegativeInfinity;
            }
            // A builder that just let a site go gets exactly this one decision with its need forced. If another
            // behavior took the decision instead, the ordinary rules apply from here on.
            string forcedNeedId = now < _forcedUntilHours ? _forcedNeedId : null;
            _forcedNeedId = null;
            if (forcedNeedId == null && now < _nextCheckHours)
            {
                return Decision.ReleaseNow();
            }
            if (!_citizen.AssignedDistrict.TryGetComponent(out HungryPathingDistrictIndex index))
            {
                _nextCheckHours = now + settings.RetryHours;
                return Decision.ReleaseNow();
            }
            Stats.Evaluations++;
            LogThresholdsOnce(settings);
            float hoursToShiftEnd = Mathf.Max(0f, ShiftEndHours() - _dayNightCycle.HoursPassedToday);
            float delay = float.MaxValue;
            OrderNeedsByUrgency(settings);
            for (int i = 0; i < _needOrder.Count; i++)
            {
                string needId = _needOrder[i];
                if (_needManager.NeedIsInCriticalState(needId))
                {
                    // The game's own critical behavior, above this one, owns needs already in penalty.
                    continue;
                }
                NeedSpec spec = _needManager.GetNeedSpec(needId);
                float hoursLeft = _needHoursLeft[i];
                float warning = WarningHours(settings, spec);
                bool isForced = forcedNeedId == needId;
                bool justInTime = settings.JustInTime &&
                                  FuelPlanner.InJustInTimeWindow(hoursLeft, warning, settings.JustInTimeLeadHours);
                bool preFuel = settings.PreFuel && FuelPlanner.WouldNotLastShift(hoursLeft, hoursToShiftEnd, warning);
                delay = Mathf.Min(delay, FuelPlanner.NextCheckDelay(hoursLeft, warning, settings.JustInTimeLeadHours,
                    preFuel, settings.RetryHours, MaxSleepHours));
                if (!isForced && !justInTime && !preFuel)
                {
                    continue;
                }
                // Pre-fuel only wants storages within PreFuelNearFoodHours; measuring farther ones cannot change
                // its answer, so they are not measured, and walks that turn out longer are not picked.
                float measureUpTo = isForced || justInTime ? float.MaxValue : settings.PreFuelNearFoodHours;
                if (!MeasureCandidates(index, needId, settings, null, now, measureUpTo))
                {
                    continue;
                }
                TripRules rules = new TripRules(isForced, justInTime, preFuel, hoursLeft, warning,
                    settings.PreFuelNearFoodHours);
                while (true)
                {
                    int best = FuelPlanner.PickTrip(_candidates, settings.VarietyToleranceHours,
                        settings.WorkTimeClosestFood, measureUpTo, rules, out TripReason reason);
                    if (best < 0)
                    {
                        break;
                    }
                    if (reason == TripReason.None)
                    {
                        if (justInTime)
                        {
                            delay = Mathf.Min(delay, FuelPlanner.DelayUntilLeaving(hoursLeft,
                                _candidates[best].TravelHours, warning, settings.RetryHours));
                        }
                        break;
                    }
                    if (TryStart(agent, best, reason, needId, hoursLeft, now, out Decision decision))
                    {
                        return decision;
                    }
                    // That storage could not start a trip: pick again among the others measured in this decision.
                    RemoveCandidate(best);
                }
            }
            _nextCheckHours = now + (delay == float.MaxValue ? settings.RetryHours : delay);
            return Decision.ReleaseNow();
        }

        // Called from the prefix on the game's CriticalNeederRootBehavior. During working hours a beaver whose
        // Hunger or Thirst is in penalty goes to the closest stocked storage for that need instead of the storage
        // holding the highest-scoring food group.
        internal bool TryRedirectCritical(BehaviorAgent agent, out Decision decision)
        {
            decision = default;
            Config settings = Plugin.Settings;
            if (!settings.Enabled || !settings.RedirectCriticalTrips || !_ready)
            {
                return false;
            }
            try
            {
                return TryRedirectCriticalUnguarded(agent, settings, out decision);
            }
            catch (Exception exception)
            {
                Safety.Trip(nameof(HungryPathingRootBehavior) + ".TryRedirectCritical", exception);
                decision = default;
                return false;
            }
        }

        private bool TryRedirectCriticalUnguarded(BehaviorAgent agent, Config settings, out Decision decision)
        {
            decision = default;
            if (!InWorkContext() || !_citizen.HasAssignedDistrict)
            {
                return false;
            }
            if (!_citizen.AssignedDistrict.TryGetComponent(out HungryPathingDistrictIndex index))
            {
                return false;
            }
            string chosen = null;
            float chosenImportance = float.MinValue;
            for (int i = 0; i < settings.Needs.Length; i++)
            {
                string needId = settings.Needs[i];
                if (!_needManager.HasNeed(needId) || !_needManager.NeedIsEnabled(needId) ||
                    !_needManager.NeedIsInCriticalState(needId))
                {
                    continue;
                }
                float importance = _needManager.GetNeedSpec(needId).ImportanceMultiplier;
                if (importance > chosenImportance)
                {
                    chosen = needId;
                    chosenImportance = importance;
                }
            }
            if (chosen == null)
            {
                return false;
            }
            float now = Now();
            if (!MeasureCandidates(index, chosen, settings, null, now, float.MaxValue))
            {
                return false;
            }
            while (true)
            {
                int best = FuelPlanner.PickCandidate(_candidates, settings.VarietyToleranceHours,
                    settings.WorkTimeClosestFood);
                if (best < 0)
                {
                    return false;
                }
                if (TryStart(agent, best, TripReason.CriticalRedirect, chosen, 0f, now, out decision))
                {
                    return true;
                }
                RemoveCandidate(best);
            }
        }

        // Called from the postfix on BuildBehavior.Decide the moment a builder starts walking to a reserved site.
        // True means: let the site go and top off first; the next decision comes back here with the need forced.
        internal bool BuilderShouldTopOffFirst(ConstructionSite site)
        {
            Config settings = Plugin.Settings;
            if (!settings.Enabled || !settings.BuilderJobCheck || !_ready)
            {
                return false;
            }
            try
            {
                return BuilderShouldTopOffFirstUnguarded(site, settings);
            }
            catch (Exception exception)
            {
                Safety.Trip(nameof(HungryPathingRootBehavior) + ".BuilderShouldTopOffFirst", exception);
                return false;
            }
        }

        private bool BuilderShouldTopOffFirstUnguarded(ConstructionSite site, Config settings)
        {
            if (!InWorkContext() || !_citizen.HasAssignedDistrict)
            {
                return false;
            }
            float now = Now();
            if (now < _nextBuilderCheckHours)
            {
                return false;
            }
            _nextBuilderCheckHours = now + settings.RetryHours;
            if (!_citizen.AssignedDistrict.TryGetComponent(out HungryPathingDistrictIndex index))
            {
                return false;
            }
            Stats.BuilderChecks++;
            Vector3 sitePosition = SitePosition(site);
            OrderNeedsByUrgency(settings);
            float travelToSite = float.NaN;
            for (int i = 0; i < _needOrder.Count; i++)
            {
                string needId = _needOrder[i];
                if (_needManager.NeedIsInCriticalState(needId))
                {
                    continue;
                }
                float hoursLeft = _needHoursLeft[i];
                float warning = WarningHours(settings, _needManager.GetNeedSpec(needId));
                if (float.IsNaN(travelToSite))
                {
                    // Deferred until a need is worth measuring; a full colony of well-fed builders costs nothing.
                    if (hoursLeft - warning >= settings.JustInTimeLeadHours + settings.BuilderJobWorkHours + 0.5f)
                    {
                        continue;
                    }
                    travelToSite = _walker.CalculateTravelTimeInHours(Transform.position, sitePosition);
                    Stats.PathQueries++;
                    if (!float.IsFinite(travelToSite))
                    {
                        return false;
                    }
                }
                if (hoursLeft - warning >= travelToSite + settings.BuilderJobWorkHours)
                {
                    continue;
                }
                // Only storages near the builder right now matter: farther ones are not measured, and the pick is
                // made among the near ones, so a better food just past the limit cannot hide a near storage.
                if (!MeasureCandidates(index, needId, settings, sitePosition, now, settings.PreFuelNearFoodHours))
                {
                    continue;
                }
                int best = FuelPlanner.PickBuilderTopOff(_candidates, settings.VarietyToleranceHours,
                    settings.WorkTimeClosestFood, hoursLeft, warning, travelToSite, settings.BuilderJobWorkHours,
                    _siteToFoodHours, settings.PreFuelNearFoodHours);
                if (best < 0)
                {
                    continue;
                }
                _forcedNeedId = needId;
                _forcedUntilHours = now + 3f * _dayNightCycle.FixedDeltaTimeInHours;
                _nextCheckHours = now;
                if (settings.Diagnostics)
                {
                    Log.Info($"{Name}: lets a site {travelToSite:0.00}h away go to top off {needId} first " +
                             $"({hoursLeft:0.0}h left, storage {_candidates[best].TravelHours:0.00}h away).");
                }
                return true;
            }
            return false;
        }

        // Starts a trip to the measured candidate at that index, or backs that storage off and returns false.
        private bool TryStart(BehaviorAgent agent, int candidate, TripReason reason, string needId, float hoursLeft,
            float now, out Decision decision)
        {
            NeedBehavior behavior = _candidateBehaviors[candidate];
            Decision inner = behavior.Decide(agent);
            if (inner.ShouldReleaseNow || (inner.Executor == null && !inner.ShouldReturnToBehavior))
            {
                // Nothing to take there after all (stock reserved meanwhile, or no way in). Leave that storage alone
                // for a while; the caller tries the next one.
                _backoffBehavior = behavior;
                _backoffUntilHours = now + 2f * Plugin.Settings.RetryHours;
                decision = default;
                return false;
            }
            _nextCheckHours = now;
            Stats.Count(reason);
            if (Plugin.Settings.Diagnostics)
            {
                Log.Info($"{Name}: {reason} trip for {needId} ({hoursLeft:0.0}h left) to {behavior.Name}, " +
                         $"{_candidates[candidate].TravelHours:0.00}h away.");
            }
            decision = Decision.TransferNow(behavior, in inner);
            return true;
        }

        // Ranks the district's stocked storages for one need: straight-line distance first for everything, then
        // real walking time for the nearest CandidateLimit that could be within measureUpToHours of walking.
        // Storages the beaver cannot take a full unit from score zero with the game's own appraiser and drop out
        // before any path query. The caller picks from the measured candidates with FuelPlanner, and they stay
        // available for a re-pick if the chosen one cannot start a trip. False when nothing could be measured.
        // The straight line is scaled by one factor for the whole game, which does not change this order: a storage
        // that tubeways or ziplines make quick to reach but that is not among the nearest CandidateLimit in a
        // straight line is still not measured.
        private bool MeasureCandidates(HungryPathingDistrictIndex index, string needId, Config settings, Vector3? site,
            float now, float measureUpToHours)
        {
            _scored.Clear();
            _candidates.Clear();
            _candidateBehaviors.Clear();
            _siteToFoodHours = float.MaxValue;
            float speed = _walkerSpeedManager.GetWalkerBaseSpeed();
            if (!(speed > 0f))
            {
                // Not known yet on the very first tick after a load; nothing is measured against a guess.
                return false;
            }
            Vector3 here = Transform.position;
            IReadOnlyList<HungryPathingDistrictIndex.Group> groups = index.GroupsFor(needId);
            int order = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                HungryPathingDistrictIndex.Group group = groups[g];
                if (group.Behaviors.Count == 0)
                {
                    continue;
                }
                float value = _appraiser.AppraiseEffects(group.Effects, NeedFilter.AnyNeed());
                if (value <= 0f)
                {
                    continue;
                }
                for (int b = 0; b < group.Behaviors.Count; b++)
                {
                    NeedBehavior behavior = group.Behaviors[b];
                    if (behavior == _backoffBehavior && FuelPlanner.StillBackedOff(now, _backoffUntilHours))
                    {
                        continue;
                    }
                    Vector3? position = behavior.ActionPosition(_needManager);
                    if (!position.HasValue)
                    {
                        continue;
                    }
                    int existing = IndexOfScored(behavior);
                    if (existing >= 0)
                    {
                        // One storage with several foods: it counts once, at its best value.
                        if (value > _scored[existing].Value)
                        {
                            Scored updated = _scored[existing];
                            updated.Value = value;
                            _scored[existing] = updated;
                        }
                        continue;
                    }
                    _scored.Add(new Scored
                    {
                        Behavior = behavior,
                        Value = value,
                        HeuristicHours = HeuristicHours(here, position.Value, speed),
                        FromSiteHours = site.HasValue ? HeuristicHours(site.Value, position.Value, speed) : 0f,
                        Position = position.Value,
                        Order = order++
                    });
                }
            }
            if (_scored.Count == 0)
            {
                return false;
            }
            _scored.Sort(ByHeuristic);
            for (int i = 0; i < _scored.Count; i++)
            {
                _siteToFoodHours = Mathf.Min(_siteToFoodHours, _scored[i].FromSiteHours);
            }
            int limit = Mathf.Min(settings.CandidateLimit, _scored.Count);
            float minCostPerUnit = _travelCostBound.MinCostPerUnit;
            for (int i = 0; i < limit; i++)
            {
                if (FuelPlanner.StraightLineRulesOut(_scored[i].HeuristicHours, measureUpToHours, minCostPerUnit))
                {
                    // Scaled by the cheapest cost per tile, the straight line is a lower bound on the walk, short of
                    // free single steps (FuelPlanner.CheapestCostPerUnit), so nothing after this can be near enough.
                    break;
                }
                float travel = _walker.CalculateTravelTimeInHours(here, _scored[i].Position);
                Stats.PathQueries++;
                if (!float.IsFinite(travel))
                {
                    continue;
                }
                _candidates.Add(new Candidate(travel, _scored[i].Value));
                _candidateBehaviors.Add(_scored[i].Behavior);
            }
            return _candidates.Count > 0;
        }

        private void RemoveCandidate(int index)
        {
            _candidates.RemoveAt(index);
            _candidateBehaviors.RemoveAt(index);
        }

        private int IndexOfScored(NeedBehavior behavior)
        {
            for (int i = 0; i < _scored.Count; i++)
            {
                if (_scored[i].Behavior == behavior)
                {
                    return i;
                }
            }
            return -1;
        }

        private float HeuristicHours(Vector3 from, Vector3 to, float speed)
        {
            return _dayNightCycle.SecondsToHours(Vector3.Distance(from, to) / speed);
        }

        // Tracked needs the beaver actually has and that are switched on, least hours left first, with those hours
        // cached alongside.
        private void OrderNeedsByUrgency(Config settings)
        {
            _needOrder.Clear();
            _needHoursLeft.Clear();
            for (int i = 0; i < settings.Needs.Length; i++)
            {
                string needId = settings.Needs[i];
                if (!_needManager.HasNeed(needId) || !_needManager.NeedIsEnabled(needId))
                {
                    continue;
                }
                NeedSpec spec = _needManager.GetNeedSpec(needId);
                float hoursLeft = FuelPlanner.HoursUntilZero(_needManager.GetNeedPoints(needId), Mathf.Abs(spec.DailyDelta) / 24f);
                int at = _needOrder.Count;
                while (at > 0 && _needHoursLeft[at - 1] > hoursLeft)
                {
                    at--;
                }
                _needOrder.Insert(at, needId);
                _needHoursLeft.Insert(at, hoursLeft);
            }
        }

        private static float WarningHours(Config settings, NeedSpec spec)
        {
            return settings.WarningHours > 0f ? settings.WarningHours : Mathf.Max(0f, spec.HoursWarningThreshold);
        }

        private bool InWorkContext()
        {
            return _worker.Employed && _workerWorkingHours.AreWorkingHours && !_workRefuser.RefusesWork;
        }

        // When this beaver's working day ends, in hours of the day. The game keeps one value for everyone;
        // BeaverBuddies MultiColony keeps one per colony and is asked first when it is present.
        private float ShiftEndHours()
        {
            return MultiColonyBridge.TryEndHours(this, out float endHours) ? endHours : _workingHoursManager.EndHours;
        }

        private float Now()
        {
            return _dayNightCycle.DayNumber * 24f + _dayNightCycle.HoursPassedToday;
        }

        // Where the builder is about to walk. The walk to the site was launched a moment before this is called, so
        // the walker's path ends exactly there. A site entity carries two Accessible components (the site's own and
        // the finished building's, disabled until then), so a plain component lookup is ambiguous and throws; the
        // fallbacks go through the site's dedicated accessible instead, then the entity position.
        private Vector3 SitePosition(ConstructionSite site)
        {
            ReadOnlyList<PathCorner> corners = _walker.PathCorners;
            if (corners.Count > 0)
            {
                return corners[corners.Count - 1].Position;
            }
            if (site.TryGetComponent(out ConstructionSiteAccessible siteAccessible) && siteAccessible.Accessible != null)
            {
                ReadOnlyList<Vector3> accesses = siteAccessible.Accessible.Accesses;
                if (accesses.Count > 0)
                {
                    Vector3 here = Transform.position;
                    Vector3 best = accesses[0];
                    float bestDistance = Vector3.Distance(here, best);
                    for (int i = 1; i < accesses.Count; i++)
                    {
                        float distance = Vector3.Distance(here, accesses[i]);
                        if (distance < bestDistance)
                        {
                            best = accesses[i];
                            bestDistance = distance;
                        }
                    }
                    return best;
                }
            }
            return site.Transform.position;
        }

        private void LogThresholdsOnce(Config settings)
        {
            if (Stats.ThresholdsLogged)
            {
                return;
            }
            Stats.ThresholdsLogged = true;
            MultiColonyBridge.EnsureProbed();
            List<string> parts = new List<string>();
            for (int i = 0; i < settings.Needs.Length; i++)
            {
                string needId = settings.Needs[i];
                if (!_needManager.HasNeed(needId))
                {
                    parts.Add(needId + " (beavers here do not have this need)");
                    continue;
                }
                NeedSpec spec = _needManager.GetNeedSpec(needId);
                parts.Add($"{needId}: buffer {WarningHours(settings, spec):0.##}h, decays {Mathf.Abs(spec.DailyDelta):0.##}/day " +
                          $"so a full bar lasts {24f / Mathf.Max(0.0001f, Mathf.Abs(spec.DailyDelta)) * spec.MaximumValue:0.#}h");
            }
            Log.Info("Needs in this game: " + string.Join("; ", parts) + ".");
            Log.Info("MultiColony: " + MultiColonyBridge.Description + ".");
        }
    }
}
