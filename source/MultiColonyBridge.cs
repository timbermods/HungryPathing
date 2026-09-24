using System;
using System.Reflection;
using HungryPathing.Planning;
using Timberborn.BaseComponentSystem;

namespace HungryPathing
{
    // Timber Together gives each colony its own working hours. It patches the per-beaver "are we working"
    // test, which this mod already goes through, but not WorkingHoursManager.EndHours, which this mod reads for
    // "hours left in the shift". So when Timber Together is present, the shift end is asked from it, for the beaver's
    // own colony, through reflection: no reference, no dependency, and if its API ever changes the mod says so once
    // and falls back to the game's value.
    internal static class MultiColonyBridge
    {
        private const string TypeName = "BeaverBuddies.Colonies.ColonyWorkingHours";

        // What the probe found. It depends only on the loaded assemblies, which do not change while the game runs,
        // so it is kept for the whole process.
        private static bool _probed;
        private static bool _available;
        private static string _probeResult = "not probed";
        private static PropertyInfo _instance;
        private static MethodInfo _endHours;
        private static MethodInfo _colonyOf;
        private static readonly object[] _oneComponent = new object[1];
        private static readonly object[] _oneSlot = new object[1];

        // An exception while asking switches the bridge off for the rest of that game only. Which shift end a beaver
        // plans against is simulation state: a player who carried the switch-off into the next game (a rehost reloads
        // in the same process) would use the game's shift end while a co-op partner with a fresh process asks
        // Timber Together. A switch-off is also said in the game (SwitchedOff), since one that only this computer hit
        // splits a co-op game.
        private static readonly Breaker Failure = new Breaker();
        private static string _failureDescription;

        public static string Description => Failure.Tripped ? _failureDescription : _probeResult;

        public static bool Available
        {
            get
            {
                Probe();
                return Failure.IsActive(_available);
            }
        }

        // Only GameLoad.Reset calls this, from the configurator, which runs on every player whenever a game is
        // loaded, joined or rehosted, before any beaver decides. It asks Timber Together again after an exception in the
        // previous game, keeps the probe result and lets go of the previous game's last beaver. Never call it
        // mid-game.
        public static void NewGame()
        {
            _oneComponent[0] = null;
            _oneSlot[0] = null;
            if (Failure.NewGame())
            {
                Log.Info("Timber Together: failure from the previous game cleared; asking it again in this game.");
            }
        }

        // Runs the probe now, so the log can say what was found before the first decision needs it.
        public static void EnsureProbed()
        {
            Probe();
        }

        // True with the beaver's colony's shift end when Timber Together is running separate colonies and knows the
        // beaver's colony. False means: use the game's WorkingHoursManager.
        public static bool TryEndHours(BaseComponent beaver, out float endHours)
        {
            endHours = 0f;
            if (!Available)
            {
                return false;
            }
            try
            {
                _oneComponent[0] = beaver;
                object slot = _colonyOf.Invoke(null, _oneComponent);
                if (slot == null)
                {
                    return false;
                }
                object instance = _instance.GetValue(null);
                if (instance == null)
                {
                    return false;
                }
                _oneSlot[0] = slot;
                endHours = (float)_endHours.Invoke(instance, _oneSlot);
                return true;
            }
            catch (Exception exception)
            {
                if (Failure.Trip())
                {
                    _failureDescription = "present, but asking it failed (" + exception.GetBaseException().Message +
                                          "); using the game's shift end for the rest of this game";
                    Log.Warning("Timber Together: " + _failureDescription);
                    SwitchedOff.Report("the Timber Together shift end (error asking Timber Together)",
                        "beavers here plan against the game's single shift end");
                }
                return false;
            }
        }

        private static void Probe()
        {
            if (_probed)
            {
                return;
            }
            _probed = true;
            try
            {
                Type type = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(TypeName, false);
                    if (type != null)
                    {
                        break;
                    }
                }
                if (type == null)
                {
                    _probeResult = "not present; the game's shift end applies to every beaver";
                    return;
                }
                _instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _endHours = type.GetMethod("EndHours", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int) }, null);
                _colonyOf = type.GetMethod("ColonyOf", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, new[] { typeof(BaseComponent) }, null);
                if (_instance == null || _endHours == null || _colonyOf == null ||
                    _endHours.ReturnType != typeof(float) || _colonyOf.ReturnType != typeof(int?))
                {
                    _probeResult = "present, but its working-hours API is not the one this build knows; " +
                                   "using the game's shift end for every beaver";
                    return;
                }
                _available = true;
                _probeResult = "present; each beaver's shift end comes from its own colony's working hours";
            }
            catch (Exception exception)
            {
                _probeResult = "probe failed (" + exception.GetBaseException().Message + "); using the game's shift end";
            }
        }
    }
}
