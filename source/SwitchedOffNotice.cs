using System;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;

namespace HungryPathing
{
    // Says in the game that something switched itself off after an error (SwitchedOff): a dialog once per switch-off,
    // and the log line on every later in-game day of that game. It looks from the frame loop, never from inside the
    // tick where the error happened, and only shows a dialog and writes to the log. It is the game's ordinary dialog,
    // so it pauses the game wherever the game's own dialogs do. In co-op BeaverBuddies decides that, as for every
    // other dialog: a guest never pauses for one.
    public class SwitchedOffNotice : IUpdatableSingleton
    {
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly IDayNightCycle _dayNightCycle;
        // One notice per game scene, so it starts at 0 with the scene, as SwitchedOff does with the load.
        private int _shown;
        private bool _failureLogged;

        public SwitchedOffNotice(DialogBoxShower dialogBoxShower, IDayNightCycle dayNightCycle)
        {
            _dialogBoxShower = dialogBoxShower;
            _dayNightCycle = dayNightCycle;
        }

        public void UpdateSingleton()
        {
            if (SwitchedOff.Count == 0)
            {
                return;
            }
            try
            {
                string reminder = SwitchedOff.TakeReminder(_dayNightCycle.DayNumber);
                if (reminder != null)
                {
                    Log.Info(reminder);
                }
                if (SwitchedOff.ShouldShow(ref _shown))
                {
                    _dialogBoxShower.Create().SetMessage(SwitchedOff.NoticeText()).Show();
                }
            }
            catch (Exception exception)
            {
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    Log.Warning("Could not say in the game that the mod switched off: " + exception);
                }
            }
        }
    }
}
