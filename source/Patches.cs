using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Timberborn.BehaviorSystem;
using Timberborn.ConstructionSites;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSpecs;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;

namespace HungryPathing
{
    // Five small hooks. None of them replaces game code: two mirror registrations into the district index, one
    // inserts the mod's root behavior into the beaver's list, one lets the mod answer for a critical Hunger or
    // Thirst during working hours, and one lets a builder release a site it would not last at. If any hook fails
    // to install, all of them are removed and the game runs unmodified.
    internal static class Patches
    {
        public static bool Apply(string harmonyId)
        {
            Harmony harmony = new Harmony(harmonyId);
            try
            {
                Type[] effectsAndBehavior = { typeof(IReadOnlyList<InstantEffectSpec>), typeof(NeedBehavior) };
                Patch(harmony, typeof(BehaviorManager), nameof(BehaviorManager.AddRootBehavior), null,
                    nameof(AddRootBehaviorPrefix), null);
                Patch(harmony, typeof(DistrictNeedBehaviorService), "AddNeedBehavior", effectsAndBehavior,
                    null, nameof(AddNeedBehaviorPostfix));
                Patch(harmony, typeof(DistrictNeedBehaviorService), "RemoveNeedBehavior", effectsAndBehavior,
                    null, nameof(RemoveNeedBehaviorPostfix));
                Patch(harmony, typeof(CriticalNeederRootBehavior), nameof(CriticalNeederRootBehavior.Decide), null,
                    nameof(CriticalDecidePrefix), null);
                Patch(harmony, typeof(BuildBehavior), nameof(BuildBehavior.Decide), null,
                    null, nameof(BuildDecidePostfix));
                Log.Info("Hooks installed (5/5).");
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("A hook could not be installed; the game runs unmodified. " + exception);
                try
                {
                    harmony.UnpatchAll(harmonyId);
                }
                catch (Exception rollback)
                {
                    Log.Warning("Rollback failed: " + rollback.Message);
                }
                return false;
            }
        }

        private static void Patch(Harmony harmony, Type type, string method, Type[] parameters, string prefix, string postfix)
        {
            MethodInfo target = parameters == null
                ? AccessTools.Method(type, method)
                : AccessTools.Method(type, method, parameters);
            if (target == null)
            {
                throw new MissingMethodException(type.Name + "." + method);
            }
            harmony.Patch(target,
                prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix));
        }

        // The game adds root behaviors in a fixed order; ours goes in right before the worker behavior so it is
        // consulted at every point where the beaver would otherwise pick up work.
        private static void AddRootBehaviorPrefix(BehaviorManager __instance, RootBehavior rootBehavior)
        {
            try
            {
                if (!(rootBehavior is WorkerRootBehavior))
                {
                    return;
                }
                if (!rootBehavior.TryGetComponent(out HungryPathingRootBehavior hungry) || hungry.Registered)
                {
                    return;
                }
                hungry.Registered = true;
                __instance.AddRootBehavior(hungry);
            }
            catch (Exception exception)
            {
                Safety.Trip("AddRootBehavior hook", exception);
            }
        }

        private static void AddNeedBehaviorPostfix(DistrictNeedBehaviorService __instance,
            IReadOnlyList<InstantEffectSpec> effects, NeedBehavior needBehavior)
        {
            try
            {
                if (__instance.TryGetComponent(out HungryPathingDistrictIndex index))
                {
                    index.Add(effects, needBehavior);
                }
            }
            catch (Exception exception)
            {
                Safety.Trip("AddNeedBehavior hook", exception);
            }
        }

        private static void RemoveNeedBehaviorPostfix(DistrictNeedBehaviorService __instance,
            IReadOnlyList<InstantEffectSpec> effects, NeedBehavior needBehavior)
        {
            try
            {
                if (__instance.TryGetComponent(out HungryPathingDistrictIndex index))
                {
                    index.Remove(effects, needBehavior);
                }
            }
            catch (Exception exception)
            {
                Safety.Trip("RemoveNeedBehavior hook", exception);
            }
        }

        private static bool CriticalDecidePrefix(CriticalNeederRootBehavior __instance, BehaviorAgent agent,
            ref Decision __result)
        {
            try
            {
                if (!__instance.TryGetComponent(out HungryPathingRootBehavior hungry))
                {
                    return true;
                }
                if (!hungry.TryRedirectCritical(agent, out Decision decision))
                {
                    return true;
                }
                __result = decision;
                return false;
            }
            catch (Exception exception)
            {
                Safety.Trip("CriticalNeederRootBehavior.Decide hook", exception);
                return true;
            }
        }

        // Only the decision that starts the walk to a freshly reserved site. Letting the site go is the game's own
        // "could not get there" path: unreserve, release, decide again next tick.
        private static void BuildDecidePostfix(BuildBehavior __instance, ref Decision __result)
        {
            try
            {
                if (__result.ShouldReleaseNow || !__result.ShouldReturnToBehavior ||
                    !(__result.Executor is WalkToAccessibleExecutor))
                {
                    return;
                }
                if (!__instance.TryGetComponent(out HungryPathingRootBehavior hungry) ||
                    !__instance.TryGetComponent(out Builder builder) || !builder.HasReservedConstructionSite)
                {
                    return;
                }
                if (hungry.BuilderShouldTopOffFirst(builder.ReservedConstructionSite))
                {
                    builder.Unreserve();
                    // The walk toward the site was already launched; without this the beaver would keep walking to a
                    // site it no longer holds if no trip starts next tick. A new walk clears the flag again.
                    if (__instance.TryGetComponent(out Walker walker))
                    {
                        walker.StopNextTick();
                    }
                    __result = Decision.ReleaseNextTick();
                }
            }
            catch (Exception exception)
            {
                Safety.Trip("BuildBehavior.Decide hook", exception);
            }
        }
    }
}
