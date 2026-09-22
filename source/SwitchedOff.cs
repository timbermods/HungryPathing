using System.Collections.Generic;
using System.Text;

namespace HungryPathing
{
    // What switched itself off after an error in this game, in the order it happened: the circuit breaker (Safety),
    // which takes the whole mod off, and the MultiColony bridge, which falls back to the game's shift end.
    //
    // Both guard code that reads only the simulation, so an error there should throw on every player at the same tick.
    // That is what the code is built for, not something it can promise: an error that only one computer hits leaves
    // that computer running the base game where the other players run the mod, and a co-op game drifts apart from
    // there. A warning in Player.log is not something anyone reads in time, so it is said three ways: the warning
    // itself, a dialog in the game (SwitchedOffNotice) that asks co-op players to load the host's save before playing
    // on, and a log line on every later in-game day of that game. The next load clears it all (GameLoad.Reset), since
    // that load turns everything back on.
    //
    // Everything here runs on the main thread: the reports come from beaver decisions and game hooks, the notice from
    // the frame loop. Nothing here feeds a decision.
    internal static class SwitchedOff
    {
        private const int NoDay = int.MinValue;

        private static readonly List<string> Names = new List<string>();
        private static readonly List<string> Effects = new List<string>();
        private static int _reminderDay = NoDay;

        // How many things switched off in this game. Only goes up until the next load.
        public static int Count => Names.Count;

        // name says what switched off and after which error, effect what beavers on this computer do now.
        public static void Report(string name, string effect)
        {
            Names.Add(name);
            Effects.Add(effect);
        }

        // Only GameLoad.Reset calls this, at every load, join and rehost: the load turns everything back on.
        public static void NewGame()
        {
            Names.Clear();
            Effects.Clear();
            _reminderDay = NoDay;
        }

        // Whether the notice shows the dialog now, given how many switch-offs this game's notice has already shown:
        // once for each switch-off, not again on the frames after. Showing never repeats a failed attempt, since the
        // count is taken before the dialog is.
        public static bool ShouldShow(ref int shown)
        {
            if (Count <= shown)
            {
                return false;
            }
            shown = Count;
            return true;
        }

        // The log line for the in-game day that has just ended, said on the first frame of each later day of this game
        // while something is switched off; null otherwise. The day of the switch-off has the warning, so the first line
        // comes at the next day change. It is numbered like the daily summary line, which a player whose mod is
        // switched off no longer writes, so two players' logs compare day by day.
        public static string TakeReminder(int day)
        {
            if (Count == 0 || day == _reminderDay)
            {
                return null;
            }
            int ended = _reminderDay;
            _reminderDay = day;
            if (ended == NoDay)
            {
                return null;
            }
            return "Day " + ended + ": switched off on this computer until a game is loaded: " +
                   string.Join("; ", Names) + ". In multiplayer the other players may still run the mod: the host " +
                   "should save and host that save again, and every player join it, before playing on together.";
        }

        // The dialog's text, free of UI so the checks can read it.
        public static string NoticeText()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Hungry Pathing\n\n");
            text.Append("An error on this computer switched this off for the rest of this game:\n");
            for (int i = 0; i < Names.Count; i++)
            {
                text.Append("\n- ").Append(Names[i]).Append(": ").Append(Effects[i]).Append('.');
            }
            text.Append("\n\nPlayer.log has the details; please report it.\n\n");
            text.Append("In multiplayer the other players' computers may still run the mod, and the games can drift " +
                        "apart. Before playing on together, the host should save and host that save again, and every " +
                        "player join it: loading a game turns the mod back on for everyone.\n\n");
            text.Append("In single player nothing else is needed; loading a game turns the mod back on.");
            return text.ToString();
        }
    }
}
