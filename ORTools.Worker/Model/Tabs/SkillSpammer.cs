
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ORTools.Worker
{
    public class KeyConfig
    {
        public Keys Key { get; set; }
        public bool ClickActive { get; set; }
        public bool IsIndeterminate { get; set; }

        public KeyConfig() { }

        public KeyConfig(Keys key, bool clickActive, bool isIndeterminate = false)
        {
            Key = key;
            ClickActive = clickActive;
            IsIndeterminate = isIndeterminate;
        }
    }

    public class SkillSpammer : IAction
    {
        private ConcurrentDictionary<Keys, bool> toggledKeys = new ConcurrentDictionary<Keys, bool>();

        /// <summary>
        /// Clears which keys are actively firing in toggle mode, without disabling toggle mode itself.
        /// Call this when the app state turns OFF so the next ON requires a fresh key tap.
        /// </summary>
        public void ResetToggleState()
        {
            toggledKeys.Clear();
        }
        private ConcurrentDictionary<Keys, bool> keyPressedLastFrame = new ConcurrentDictionary<Keys, bool>();

        public event EventHandler<bool> ToggleModeChanged;

        public Keys ToggleModeKey { get; set; } = Keys.None;

        public static bool IsGameWindowActive()
        {
            try
            {
                Client currentClient = ClientSingleton.GetClient();
                if (currentClient == null || !currentClient.IsProcessRunning())
                {
                    return false;
                }

                return currentClient.MainWindowHandle != IntPtr.Zero && ClientInput.IsForeground(currentClient.MainWindowHandle);
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error checking if game window is active: {ex.Message}");
                return false;
            }
        }

        private const string ACTION_NAME = "SkillSpammer";
        private ThreadRunner thread;
        private volatile bool _running = false;
        public ConcurrentDictionary<string, KeyConfig> SpammerEntries { get; set; } = new ConcurrentDictionary<string, KeyConfig>();

        private int _delay = AppConfig.SkillSpammerDefaultDelay;

        public int SpammerDelay
        {
            get => _delay < 0 ? AppConfig.SkillSpammerDefaultDelay : _delay;
            set => _delay = value;
        }

        public bool MouseFlick { get; set; } = false;

        public bool NoShift { get; set; } = false;

        public bool ToggleMode { get; set; } = false;

        public SkillSpammer() { }

        public void Start()
        {
            Client roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                Stop();

                _running = true;
                ResetToggleState();
                keyPressedLastFrame.Clear();

                this.thread = new ThreadRunner(_ => SkillSpammerThread(roClient), "SkillSpammerThread") { IterationDelay = 1 };
                ThreadRunner.Start(this.thread);
            }
        }

        private int SkillSpammerThread(Client roClient)
        {
            if (!_running)
                return 0;

            if (roClient == null || !roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
                return 0;

            IntPtr windowHandle = roClient.MainWindowHandle;
            if (windowHandle == IntPtr.Zero || !ClientInput.IsForeground(windowHandle))
                return 0;

            // Cache settings once per iteration
            bool noShift = this.NoShift;
            bool mouseFlick = this.MouseFlick;

            // Handle toggle mode key press
            if (this.ToggleModeKey != Keys.None)
            {
                bool isAltPressed = ClientInput.IsKeyPressed(Keys.LMenu) || ClientInput.IsKeyPressed(Keys.RMenu);
                bool isToggleKeyPressed = !isAltPressed && ClientInput.IsKeyPressed(this.ToggleModeKey);
                bool wasToggleKeyPressed = keyPressedLastFrame.TryGetValue(this.ToggleModeKey, out bool wasPressed) && wasPressed;

                if (isToggleKeyPressed && !wasToggleKeyPressed)
                {
                    this.ToggleMode = !this.ToggleMode;
                    ToggleModeChanged?.Invoke(this, this.ToggleMode);
                    ProfileSingleton.SetConfiguration(this);

                    if (!this.ToggleMode)
                    {
                        toggledKeys.Clear();
                    }
                }

                keyPressedLastFrame[this.ToggleModeKey] = isToggleKeyPressed;
            }

            foreach (var kvp in SpammerEntries)
            {
                if (!_running || roClient.IsDead() || roClient.IsTextInputActive() || !ClientInput.IsForeground(windowHandle))
                    break;

                var config = kvp.Value;
                if (config != null && (config.ClickActive || config.IsIndeterminate))
                {
                    SkillSpammerSpeedBoost(roClient, config, windowHandle, noShift, mouseFlick);
                }
            }

            return 0;
        }

        private void SkillSpammerSpeedBoost(Client roClient, KeyConfig config, IntPtr windowHandle, bool noShift, bool mouseFlick)
        {
            bool isKeyPressed = ClientInput.IsKeyPressed(config.Key);
            bool wasKeyPressed = keyPressedLastFrame.TryGetValue(config.Key, out bool pressed) && pressed;
            keyPressedLastFrame[config.Key] = isKeyPressed;

            if (this.ToggleMode)
            {
                if (isKeyPressed && !wasKeyPressed)
                {
                    if (!toggledKeys.ContainsKey(config.Key))
                        toggledKeys[config.Key] = false;

                    toggledKeys[config.Key] = !toggledKeys[config.Key];
                }

                if (toggledKeys.TryGetValue(config.Key, out bool isToggled) && isToggled)
                {
                    ExecuteSkillSpam(roClient, config, windowHandle, noShift, mouseFlick);
                }
            }
            else
            {
                if (isKeyPressed)
                {
                    ExecuteSkillSpam(roClient, config, windowHandle, noShift, mouseFlick);
                }
            }
        }

        private void ExecuteSkillSpam(Client roClient, KeyConfig config, IntPtr windowHandle, bool noShift, bool mouseFlick)
        {
            bool shiftHeld = false;
            try
            {
                if (noShift)
                {
                    ClientInput.HoldShift();
                    shiftHeld = true;
                }

                ClientInput.SendKey(windowHandle, config.Key, blockOnAlt: false);

                if (config.ClickActive && !config.IsIndeterminate)
                {
                    Point cursorPos = ClientInput.GetCursorPos();

                    if (mouseFlick)
                    {
                        Point flickPos = new Point(
                            cursorPos.X - Constants.MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK,
                            cursorPos.Y - Constants.MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK
                        );

                        ClientInput.SetCursorPos(flickPos.X, flickPos.Y);
                        ClientInput.SendRawMouseEvent(Constants.MOUSEEVENTF_LEFTDOWN, (uint)flickPos.X, (uint)flickPos.Y);
                        Thread.Sleep(1);
                        ClientInput.SendRawMouseEvent(Constants.MOUSEEVENTF_LEFTUP, (uint)flickPos.X, (uint)flickPos.Y);
                        ClientInput.SetCursorPos(cursorPos.X, cursorPos.Y);
                    }
                    else
                    {
                        ClientInput.SendRawMouseEvent(Constants.MOUSEEVENTF_LEFTDOWN, (uint)cursorPos.X, (uint)cursorPos.Y);
                        Thread.Sleep(1);
                        ClientInput.SendRawMouseEvent(Constants.MOUSEEVENTF_LEFTUP, (uint)cursorPos.X, (uint)cursorPos.Y);
                    }
                }
            }
            finally
            {
                if (shiftHeld)
                {
                    ClientInput.ReleaseShift();
                }
            }

            SleepWithCancel(roClient, this.SpammerDelay);
        }

        private bool SleepWithCancel(Client roClient, int milliseconds)
        {
            if (milliseconds <= 0) return true;

            int elapsed = 0;
            while (elapsed < milliseconds)
            {
                if (!_running) return false;
                if (!roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
                    return false;
                if (!ClientInput.IsForeground(roClient.MainWindowHandle))
                    return false;

                int slice = Math.Min(10, milliseconds - elapsed);
                Thread.Sleep(slice);
                elapsed += slice;
            }
            return true;
        }

        public void AddSkillSpammerEntry(string entryName, KeyConfig value)
        {
            this.SpammerEntries[entryName] = value;
        }

        public void RemoveSkillSpammerEntry(string entryName)
        {
            this.SpammerEntries.TryRemove(entryName, out _);
        }

        public void Stop()
        {
            _running = false;
            ResetToggleState();
            keyPressedLastFrame.Clear();

            if (this.NoShift)
            {
                try { ClientInput.ReleaseShift(); }
                catch { }
            }

            if (this.thread != null)
            {
                ThreadRunner.Stop(this.thread);
                this.thread.Terminate();
                this.thread = null;
            }
        }

        public string GetConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string GetActionName()
        {
            return ACTION_NAME;
        }
    }
}