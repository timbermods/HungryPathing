using System.Collections.Generic;

namespace HungryPathing
{
    // Stand-ins for the parts of the mod that need the game, so that Safety.cs compiles and runs here as shipped.
    // They carry only what Safety.cs reads and writes.
    internal sealed class Config
    {
        public bool Enabled = true;
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
}
