
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using ORTools.Shared;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ORTools.Worker
{
    public class SkillTimer : IAction
    {
        #region Constants
        public const int MAX_SKILL_TIMERS = AppConstants.MaxSkillTimers;
        #endregion

        private readonly string ACTION_NAME = "SkillTimer";
        public Dictionary<int, SkillTimerKey> skillTimer = new Dictionary<int, SkillTimerKey>();

        private readonly Dictionary<int, ThreadRunner> threads = new Dictionary<int, ThreadRunner>();

        // Map cache — shared across all timer threads; avoids an RPM call every tick.
        // Refreshed at most once per second so city-check stays accurate without hammering memory.
        private string _cachedMap = string.Empty;
        public void Start()
        {
            Client roClient = ClientSingleton.GetClient();
            if (roClient == null) return;

            StopAllThreads();

            // Create and start threads only for enabled skill timers
            for (int i = 1; i <= MAX_SKILL_TIMERS; i++)
            {
                if (skillTimer.TryGetValue(i, out var macro) && macro.Enabled)
                {
                    int skillIndex = i; // Capture loop variable
                    threads[i] = new ThreadRunner((_) => SkillTimerThread(roClient, skillTimer[skillIndex]), $"SkillTimer-{i}") { IterationDelay = 1 };
                    ThreadRunner.Start(threads[i]);
                }
            }
        }

        public void Stop()
        {
            StopAllThreads();
        }

        public void StartTimer(int timerId)
        {
            Client roClient = ClientSingleton.GetClient();
            if (roClient == null) return;

            // Stop existing thread for this timer if it exists
            StopTimer(timerId);

            // Start new thread if the timer exists and is enabled
            if (skillTimer.TryGetValue(timerId, out var macro) && macro.Enabled)
            {
                // Respect the StopBuffsCity setting - if enabled and we're in a city, don't start the timer
                string currentMap = roClient.ReadCurrentMapCached();
                if (ProfileSingleton.GetCurrent().UserPreferences.StopBuffsCity && Server.GetCityList().Contains(currentMap))
                {
                    // Don't start timer if we're in a city and StopBuffsCity is enabled
                    return;
                }

                threads[timerId] = new ThreadRunner((_) => SkillTimerThread(roClient, skillTimer[timerId]), $"SkillTimer-{timerId}") { IterationDelay = 1 };
                ThreadRunner.Start(threads[timerId]);
            }
        }

        public void StopTimer(int timerId)
        {
            if (threads.TryGetValue(timerId, out var thread))
            {
                ThreadRunner.Stop(thread);
                thread.Terminate();
                threads.Remove(timerId);
            }
        }

        private void StopAllThreads()
        {
            foreach (var thread in threads.Values.ToList())
            {
                if (thread != null)
                {
                    ThreadRunner.Stop(thread);
                    thread.Terminate();
                }
            }
            threads.Clear();
        }

        private int SkillTimerThread(Client roClient, SkillTimerKey macro)
        {
            if (!roClient.IsProcessRunning() || roClient.IsTextInputActive() || roClient.IsDead()) return 0;

            string currentMap = roClient.ReadCurrentMapCached();
            if (!ProfileSingleton.GetCurrent().UserPreferences.StopBuffsCity || !Server.GetCityList().Contains(currentMap))
            {
                IntPtr hWnd = roClient.MainWindowHandle;
                if (macro.Key != Keys.None)
                {
                    if (macro.AltKey)
                    {
                        ClientInput.SendAltKeyCombo(hWnd, macro.Key);
                    }
                    else
                    {
                        // Remove the KeyInterop conversion since macro.Key is already Keys enum
                        ClientInput.SendKey(hWnd, macro.Key, blockOnAlt: false);
                    }
                }
                // Handle clicking based on the ClickMode
                switch (macro.ClickMode)
                {
                    case 1: // Click at current cursor position
                        TryClickAtCurrentPosition(hWnd);
                        break;
                    case 2: // Click at the center of the game window
                        TryClickAtCenter(hWnd);
                        break;
                        // case 0: No click, do nothing.
                }
            }
            Thread.Sleep(macro.Delay);
            return 0;
        }

        public string GetConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string GetActionName()
        {
            return ACTION_NAME;
        }


        private static readonly Dictionary<Keys, string> _sendKeysMap = new Dictionary<Keys, string>()
        {
             { Keys.D0, "0" },
             { Keys.D1, "1" },
             { Keys.D2, "2" },
             { Keys.D3, "3" },
             { Keys.D4, "4" },
             { Keys.D5, "5" },
             { Keys.D6, "6" },
             { Keys.D7, "7" },
             { Keys.D8, "8" },
             { Keys.D9, "9" }
        };

        public static string ToSendKeysFormat(Keys key)
        {
            if (_sendKeysMap.TryGetValue(key, out string value))
            {
                return value;
            }
            return key.ToString().ToLower();
        }

        private void TryClickAtCurrentPosition(IntPtr hWnd)
        {
            ClientInput.ClickAtCurrentPosition(hWnd);
        }

        private void TryClickAtCenter(IntPtr hWnd)
        {
            ClientInput.ClickAtWindowCenter(hWnd);
        }

    }
}