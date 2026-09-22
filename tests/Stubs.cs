using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;

namespace HungryPathing
{
    // Stand-ins for the parts of the mod that need the game, so that Safety.cs, MultiColonyBridge.cs, SwitchedOff.cs,
    // Stats.cs and GameLoad.cs compile and run here as shipped. They carry only what those files read and write.
    internal sealed class Config
    {
        public bool Enabled = true;
        public bool DailyReport = false;
    }

    internal static class Plugin
    {
        internal static Config Settings = new Config();
        internal static bool HooksInstalled;
    }

    internal static class Log
    {
        public static readonly List<string> Infos = new List<string>();
        public static readonly List<string> Warnings = new List<string>();

        public static void Info(string message)
        {
            Infos.Add(message);
        }

        public static void Warning(string message)
        {
            Warnings.Add(message);
        }
    }

    internal sealed class StubBeaver : BaseComponent
    {
    }
}

namespace Timberborn.BaseComponentSystem
{
    public abstract class BaseComponent
    {
    }
}

namespace BeaverBuddies.Colonies
{
    // The shape of BeaverBuddies MultiColony's ColonyWorkingHours that MultiColonyBridge finds by name: a static
    // Instance, EndHours for a colony slot, and a static ColonyOf that is null for a beaver of no colony.
    public class ColonyWorkingHours
    {
        internal static ColonyWorkingHours Current;
        internal static int? ColonySlot = 1;
        internal static bool ThrowOnce;

        public static ColonyWorkingHours Instance => Current;

        public float EndHours(int slot)
        {
            if (ThrowOnce)
            {
                ThrowOnce = false;
                throw new InvalidOperationException("test");
            }
            return 20f;
        }

        internal static int? ColonyOf(BaseComponent component)
        {
            return ColonySlot;
        }
    }
}
