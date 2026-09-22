using System;
using System.Reflection;
using Timberborn.ModManagerScene;

namespace HungryPathing
{
    public class Plugin : IModStarter
    {
        // Must equal the Id in manifest.json.
        public const string ModId = "kyler.hungrypathing";

        internal static Config Settings = new Config();

        public void StartMod(IModEnvironment modEnvironment)
        {
            try
            {
                string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                Log.Info(version + " loading.");
                Settings = Config.Load(modEnvironment.ModPath, modEnvironment.OriginPath, FolderAboveScripts());
                Log.Info(Settings.ToString());
                if (!Patches.Apply(ModId))
                {
                    Settings.Enabled = false;
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
