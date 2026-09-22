using System;
using System.Collections.Generic;
using HungryPathing.Planning;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
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

        private struct Pick
        {
            public NeedBehavior Behavior;
            public float TravelHours;
            public float Value;
            public float SiteToFoodHours;
        }

        // Straight-line order, then insertion order: List.Sort is unstable, so the tie-break keeps it deterministic.
        private static readonly Comparison<Scored> ByHeuristic = (a, b) =>
        {
            int byHours = a.HeuristicHours.CompareTo(b.HeuristicHours);
            return byHours != 0 ? byHours : a.Order.CompareTo(b.Order);
        };

        private readonly IDayNightCycle _dayNightCycle;
        private readonly WorkingHoursManager _workingHoursManager;

        private Worker _worker;
        private WorkerWorkingHours _workerWorkingHours;
        private WorkRefuser _workRefuser;
        private Citizen _citizen;
        private NeedManager _needManager;
        private Appraiser _appraiser;
        private Walker _walker;
        private WalkerSpeedManager _walkerSpeedManager;

        private readonly List<Scored> _scored = new List<Scored>();
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<NeedBehavior> _candidateBehaviors = new List<NeedBehavior>();
        private readonly List<string> _needOrder = new List<string>();
        private readonly List<float> _needHoursLeft = new List<float>();

        private float _nextCheckHours = float.NegativeInfinity;
        private int _lastDay = int.MinValue;
        private string _forcedNeedId;
        private float _forcedUntilHours;
        private float _nextBuilderCheckHours = float.NegativeInfinity;
        private NeedBehavior _backoffBehavior;
        private float _backoffUntilHours;

        // Set by the patch that puts this behavior into the root behavior list.
        internal bool Registered;

        public HungryPathingRootBehavior(IDayNightCycle dayNightCycle, WorkingHoursManager workingHoursManager)
        {
            _dayNightCycle = dayNightCycle;
            _workingHoursManager = workingHoursManager;
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
        }

        public override Decision Decide(BehaviorAgent agent)
        {
            Config settings = Plugin.Settings;
            if (!settings.Enabled)
            {
                return Decision.ReleaseNow();
            }
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
            bool forced = _forcedNeedId != null && now <= _forcedUntilHours;
            if (!forced && now < _nextCheckHours)
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
            float hoursToShiftEnd = Mathf.Max(0f, _workingHoursManager.EndHours - _dayNightCycle.HoursPassedToday);
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
                bool isForced = forced && _forcedNeedId == needId;
                bool justInTime = settings.JustInTime &&
                                  FuelPlanner.InJustInTimeWindow(hoursLeft, warning, settings.JustInTimeLeadHours);
                bool preFuel = settings.PreFuel && FuelPlanner.WouldNotLastShift(hoursLeft, hoursToShiftEnd, warning);
                delay = Mathf.Min(delay,
                    FuelPlanner.NextCheckDelay(hoursLeft, warning, settings.JustInTimeLeadHours, preFuel, settings.RetryHours));
                if (!isForced && !justInTime && !preFuel)
                {
                    continue;
                }
                if (!TryFindBest(index, needId, settings, null, now, out Pick pick))
                {
                    continue;
                }
                TripReason reason = TripReason.None;
                if (isForced)
                {
                    reason = TripReason.BuilderJob;
                }
                else if (justInTime && FuelPlanner.ShouldLeaveNow(hoursLeft, pick.TravelHours, warning))
                {
                    reason = TripReason.JustInTime;
                }
                else if (preFuel && pick.TravelHours <= settings.PreFuelNearFoodHours)
                {
                    reason = TripReason.PreFuel;
                }
                if (reason == TripReason.None)
                {
                    if (justInTime)
                    {
                        delay = Mathf.Min(delay,
                            FuelPlanner.DelayUntilLeaving(hoursLeft, pick.TravelHours, warning, settings.RetryHours));
                    }
                    continue;
                }
                if (TryStart(agent, pick, reason, needId, hoursLeft, now, out Decision decision))
                {
                    return decision;
                }
            }
            if (forced)
            {
                _forcedNeedId = null;
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
            if (!settings.Enabled || !settings.RedirectCriticalTrips || !InWorkContext() || !_citizen.HasAssignedDistrict)
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
                if (!_needManager.HasNeed(needId) || !_needManager.NeedIsInCriticalState(needId))
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
            if (!TryFindBest(index, chosen, settings, null, now, out Pick pick))
            {
                return false;
            }
            return TryStart(agent, pick, TripReason.CriticalRedirect, chosen, 0f, now, out decision);
        }

        // Called from the postfix on BuildBehavior.Decide the moment a builder starts walking to a reserved site.
        // True means: let the site go and top off first; the next decision comes back here with the need forced.
        internal bool BuilderShouldTopOffFirst(ConstructionSite site)
        {
            Config settings = Plugin.Settings;
            if (!settings.Enabled || !settings.BuilderJobCheck || !InWorkContext() || !_citizen.HasAssignedDistrict)
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
                }
                if (hoursLeft - warning >= travelToSite + settings.BuilderJobWorkHours)
                {
                    continue;
                }
                if (!TryFindBest(index, needId, settings, sitePosition, now, out Pick pick))
                {
                    continue;
                }
                if (!FuelPlanner.BuilderShouldTopOff(hoursLeft, warning, travelToSite, settings.BuilderJobWorkHours,
                        pick.SiteToFoodHours, pick.TravelHours, settings.PreFuelNearFoodHours))
                {
                    continue;
                }
                _forcedNeedId = needId;
                _forcedUntilHours = now + 1f;
                _nextCheckHours = now;
                if (settings.Diagnostics)
                {
                    Log.Info($"{Name}: lets a site {travelToSite:0.00}h away go to top off {needId} first " +
                             $"({hoursLeft:0.0}h left, storage {pick.TravelHours:0.00}h away).");
                }
                return true;
            }
            return false;
        }

        private bool TryStart(BehaviorAgent agent, Pick pick, TripReason reason, string needId, float hoursLeft, float now,
            out Decision decision)
        {
            Decision inner = pick.Behavior.Decide(agent);
            if (inner.ShouldReleaseNow || (inner.Executor == null && !inner.ShouldReturnToBehavior))
            {
                // Nothing to take there after all (stock reserved meanwhile, or no way in). Leave that storage alone
                // for a while and let the game carry on.
                _backoffBehavior = pick.Behavior;
                _backoffUntilHours = now + Plugin.Settings.RetryHours;
                decision = default;
                return false;
            }
            _forcedNeedId = null;
            _nextCheckHours = now;
            Stats.Count(reason);
            if (Plugin.Settings.Diagnostics)
            {
                Log.Info($"{Name}: {reason} trip for {needId} ({hoursLeft:0.0}h left) to {pick.Behavior.Name}, " +
                         $"{pick.TravelHours:0.00}h away.");
            }
            decision = Decision.TransferNow(pick.Behavior, in inner);
            return true;
        }

        // Ranks the district's stocked storages for one need: straight-line distance first for everything, then
        // real walking time for the nearest CandidateLimit, then the planner's pick. Storages the beaver cannot
        // take a full unit from score zero with the game's own appraiser and drop out before any path query.
        private bool TryFindBest(HungryPathingDistrictIndex index, string needId, Config settings, Vector3? site,
            float now, out Pick pick)
        {
            pick = default;
            _scored.Clear();
            Vector3 here = Transform.position;
            float speed = _walkerSpeedManager.GetWalkerBaseSpeed();
            if (speed <= 0f)
            {
                speed = 1f;
            }
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
                    if (behavior == _backoffBehavior && now <= _backoffUntilHours)
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
            float siteToFood = float.MaxValue;
            for (int i = 0; i < _scored.Count; i++)
            {
                siteToFood = Mathf.Min(siteToFood, _scored[i].FromSiteHours);
            }
            int limit = Mathf.Min(settings.CandidateLimit, _scored.Count);
            _candidates.Clear();
            _candidateBehaviors.Clear();
            for (int i = 0; i < limit; i++)
            {
                float travel = _walker.CalculateTravelTimeInHours(here, _scored[i].Position);
                Stats.PathQueries++;
                _candidates.Add(new Candidate(travel, _scored[i].Value));
                _candidateBehaviors.Add(_scored[i].Behavior);
            }
            int best = FuelPlanner.PickCandidate(_candidates, settings.VarietyToleranceHours, settings.WorkTimeClosestFood);
            if (best < 0)
            {
                return false;
            }
            pick = new Pick
            {
                Behavior = _candidateBehaviors[best],
                TravelHours = _candidates[best].TravelHours,
                Value = _candidates[best].Value,
                SiteToFoodHours = siteToFood
            };
            return true;
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

        // Tracked needs the beaver actually has, least hours left first, with those hours cached alongside.
        private void OrderNeedsByUrgency(Config settings)
        {
            _needOrder.Clear();
            _needHoursLeft.Clear();
            for (int i = 0; i < settings.Needs.Length; i++)
            {
                string needId = settings.Needs[i];
                if (!_needManager.HasNeed(needId))
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

        private float Now()
        {
            return _dayNightCycle.DayNumber * 24f + _dayNightCycle.HoursPassedToday;
        }

        private static Vector3 SitePosition(ConstructionSite site)
        {
            if (site.TryGetComponent(out Accessible accessible))
            {
                Vector3? access = accessible.UnblockedSingleAccess;
                if (access.HasValue)
                {
                    return access.Value;
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
        }
    }
}
