using System;
using System.Reflection;
using Timberborn.ModManagerScene;

namespace HungryPathing
{
    public class Plugin : IModStarter
    {
        // Must equal the Id in manifest.json.
        public const string ModId = "kyler.hungrypathing";

        // Exactly what the settings file says; nothing writes to it after startup.
        internal static Config Settings = new Config();

        // Set once at startup and never reset: a hook that did not go in stays out for the whole process, which is
        // the same on every player with the same game and mod versions.
        internal static bool HooksInstalled;

        public void StartMod(IModEnvironment modEnvironment)
        {
            try
            {
                string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                Log.Info(version + " loading.");
                Settings = Config.Load(modEnvironment.ModPath, modEnvironment.OriginPath, FolderAboveScripts());
                Log.Info(Settings.ToString());
                HooksInstalled = Patches.Apply(ModId);
                if (!HooksInstalled)
                {
                    Log.Warning("Disabled for this session.");
                    return;
                }
                Log.Info(Settings.Enabled
                    ? "Active. One summary line per in-game day follows while a game runs."
                    : "Loaded with Enabled=false; beavers behave as in the base game.");
            }
            catch (Exception exception)
            {
                Log.Warning("Failed to start; the game runs unmodified. " + exception);
            }
        }

        // The settings file ships next to the manifest, one level above Scripts/. ModPath may or may not be that
        // folder depending on how the game resolved the versioned layout, so it is tried as well.
        private static string FolderAboveScripts()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(location)
                    ? null
                    : System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(location));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
