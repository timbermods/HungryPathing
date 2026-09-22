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
    // Five small hooks: two mirror registrations into the district index, one inserts the mod's root behavior into
    // the beaver's list, one lets the mod answer for a critical Hunger or Thirst during working hours, and one lets a
    // builder release a site it would not last at. Only the critical one can skip game code: when the mod answers,
    // the game's own Decide does not run. If any hook fails to install, all of them are removed and the game runs
    // unmodified.
    internal static class Patches
    {
        public static bool Apply(string harmonyId)
        {
            Harmony harmony = new Harmony(harmonyId);
            List<MethodBase> patched = new List<MethodBase>();
            try
            {
                Type[] effectsAndBehavior = { typeof(IReadOnlyList<InstantEffectSpec>), typeof(NeedBehavior) };
                Patch(harmony, patched, typeof(BehaviorManager), nameof(BehaviorManager.AddRootBehavior), null,
                    nameof(AddRootBehaviorPrefix), null);
                Patch(harmony, patched, typeof(DistrictNeedBehaviorService), "AddNeedBehavior", effectsAndBehavior,
                    null, nameof(AddNeedBehaviorPostfix));
                Patch(harmony, patched, typeof(DistrictNeedBehaviorService), "RemoveNeedBehavior", effectsAndBehavior,
                    null, nameof(RemoveNeedBehaviorPostfix));
                Patch(harmony, patched, typeof(CriticalNeederRootBehavior), nameof(CriticalNeederRootBehavior.Decide),
                    null, nameof(CriticalDecidePrefix), null);
                Patch(harmony, patched, typeof(BuildBehavior), nameof(BuildBehavior.Decide), null,
                    null, nameof(BuildDecidePostfix));
                Log.Info("Hooks installed (5/5).");
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("A hook could not be installed; the game runs unmodified. " + exception);
                // This mod's own patches on the methods it patched, and nothing else: another mod's patches on the
                // same methods stay. Unpatch needs the owner, because without one it removes every owner's patches,
                // and the Harmony this mod requires (2.4.1) has no UnpatchSelf.
                foreach (MethodBase target in patched)
                {
                    try
                    {
                        harmony.Unpatch(target, HarmonyPatchType.All, harmonyId);
                    }
                    catch (Exception rollback)
                    {
                        Log.Warning("Rollback failed: " + rollback.Message);
                    }
                }
                return false;
            }
        }

        // new HarmonyMethod(Type, name) reads the hook's Harmony attributes, so a [HarmonyPriority] on it applies.
        private static void Patch(Harmony harmony, List<MethodBase> patched, Type type, string method, Type[] parameters,
            string prefix, string postfix)
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
            // Harmony records a patch only once it is in place, so a target that threw has nothing to take off.
            patched.Add(target);
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

        // Returning false skips the game's Decide, so this prefix runs last: another mod's prefix on Decide goes first
        // on every machine (unless it asks to run last too), instead of in the order each player's mod list happened
        // to load them. When that prefix has already answered, Harmony skips this one and the mod stays out of the way.
        [HarmonyPriority(Priority.Last)]
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
