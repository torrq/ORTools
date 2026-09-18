using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Cursor = System.Windows.Forms.Cursor;

namespace ORTools.Worker
{
    public class MacroSwitchKey
    {
        public Keys Key { get; set; }

        public static int TOTAL_MACRO_LANES = ConfigGlobal.GetConfig().MacroSwitchRows;
        public static int TOTAL_MACRO_KEYS = 8;

        private int _delay = AppConfig.MacroDefaultDelay;
        public int Delay
        {
            get => _delay < 0 ? AppConfig.MacroDefaultDelay : _delay;
            set => _delay = value;
        }

        private int _clickMode = 0;
        /// <summary>
        /// Represents the click behavior for the macro switch.
        /// 0: No Click
        /// 1: Click at current mouse position
        /// </summary>
        public int ClickMode
        {
            get => _clickMode > 1 ? 1 : _clickMode;
            set => _clickMode = value > 1 ? 1 : value;
        }

        /// <summary>
        /// Constructor for creating new instances programmatically.
        /// </summary>
        public MacroSwitchKey(Keys key, int delay, int clickMode = 0)
        {
            this.Key = key;
            this.Delay = delay;
            this.ClickMode = clickMode;
        }

        /// <summary>
        /// Constructor used by Newtonsoft.Json for deserialization.
        /// </summary>
        [JsonConstructor]
        public MacroSwitchKey(Keys key, int delay) : this(key, delay, 0)
        {
        }

        public MacroSwitchKey() { }  // Default constructor needed for some deserialization scenarios.
    }

    public class MacroSwitchChainConfig
    {
        public int id;

        /// <summary>
        /// The trigger key that activates this macro chain
        /// </summary>
        public Keys TriggerKey { get; set; } = Keys.None;

        public List<MacroSwitchKey> macroEntries { get; set; } = new List<MacroSwitchKey>();

        public MacroSwitchChainConfig() { }

        public MacroSwitchChainConfig(int id)
        {
            this.id = id;
            this.TriggerKey = Keys.None;
            this.macroEntries = new List<MacroSwitchKey>();
            for (int i = 0; i < MacroSwitchKey.TOTAL_MACRO_KEYS; i++)
            {
                this.macroEntries.Add(new MacroSwitchKey(Keys.None, AppConfig.MacroDefaultDelay));
            }
        }

        public MacroSwitchChainConfig(MacroSwitchChainConfig macro)
        {
            this.id = macro.id;
            this.TriggerKey = macro.TriggerKey;
            this.macroEntries = new List<MacroSwitchKey>(macro.macroEntries);
        }

        public MacroSwitchChainConfig(int id, Keys trigger)
        {
            this.id = id;
            this.TriggerKey = trigger;
            this.macroEntries = new List<MacroSwitchKey>();
            for (int i = 0; i < MacroSwitchKey.TOTAL_MACRO_KEYS; i++)
            {
                this.macroEntries.Add(new MacroSwitchKey(Keys.None, AppConfig.MacroDefaultDelay));
            }
        }
    }

    public class MacroSwitch : IAction
    {
        public static string ACTION_NAME_MACRO_SWITCH = "MacroSwitch";

        public string ActionName { get; set; } = ACTION_NAME_MACRO_SWITCH;
        private ThreadRunner thread;
        public List<MacroSwitchChainConfig> ChainConfigs { get; set; } = new List<MacroSwitchChainConfig>();
        private readonly Dictionary<int, bool> _wasTriggerPressed = new();
        private volatile bool _running = false;

        public MacroSwitch()
        {
            EnsureCorrectRowCount(ConfigGlobal.GetConfig().MacroSwitchRows);
        }

        public MacroSwitch(string macroname, int macroLanes)
        {
            this.ActionName = macroname ?? ACTION_NAME_MACRO_SWITCH;
            EnsureCorrectRowCount(macroLanes);
        }

        [JsonConstructor]
        public MacroSwitch(string actionName, List<MacroSwitchChainConfig> chainConfigs)
        {
            this.ActionName = actionName ?? ACTION_NAME_MACRO_SWITCH;
            this.ChainConfigs = chainConfigs ?? new List<MacroSwitchChainConfig>();
            EnsureCorrectRowCount(ConfigGlobal.GetConfig().MacroSwitchRows);
        }

        public void EnsureCorrectRowCount(int count)
        {
            while (ChainConfigs.Count < count)
            {
                ChainConfigs.Add(new MacroSwitchChainConfig(ChainConfigs.Count + 1, Keys.None));
            }
            foreach (var chain in ChainConfigs)
            {
                while (chain.macroEntries.Count < MacroSwitchKey.TOTAL_MACRO_KEYS)
                {
                    chain.macroEntries.Add(new MacroSwitchKey(Keys.None, AppConfig.MacroDefaultDelay));
                }
            }
        }

        public void ResetMacro(int macroId)
        {
            try
            {
                ChainConfigs[macroId - 1] = new MacroSwitchChainConfig(macroId);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Exception in MacroSwitch.ResetMacro: {ex}");
            }
        }

        public string GetActionName()
        {
            return this.ActionName;
        }

        public string GetConfiguration()
        {
            return JsonConvert.SerializeObject(this);
        }

        private int MacroThread(Client roClient)
        {
            if (!_running) return 0;
            if (!roClient.IsProcessRunning() || roClient.IsDead()) return 0;

            IntPtr hWnd = roClient.MainWindowHandle;
            if (hWnd == IntPtr.Zero || !ClientInput.IsForeground(hWnd))
            {
                _wasTriggerPressed.Clear();
                return 0;
            }

            if (roClient.IsTextInputActive())
            {
                _wasTriggerPressed.Clear();
                return 0;
            }

            int maxRows = ConfigGlobal.GetConfig().MacroSwitchRows;
            for (int i = 0; i < maxRows && i < this.ChainConfigs.Count; i++)
            {
                var chainConfig = this.ChainConfigs[i];
                if (chainConfig.TriggerKey == Keys.None) continue;

                bool isPressed = ClientInput.IsKeyPressed(chainConfig.TriggerKey);
                _wasTriggerPressed.TryGetValue(i, out bool wasPressed);

                if (isPressed && !wasPressed)
                {
                    _wasTriggerPressed[i] = true;
                    ExecuteChainSequence(roClient, hWnd, chainConfig);
                    // Refresh trigger state after sequence completion so holding does not re-trigger
                    _wasTriggerPressed[i] = ClientInput.IsKeyPressed(chainConfig.TriggerKey);
                    break;
                }
                else if (!isPressed && wasPressed)
                {
                    _wasTriggerPressed[i] = false;
                }
            }

            return 0;
        }

        private void ExecuteChainSequence(Client roClient, IntPtr hWnd, MacroSwitchChainConfig chainConfig)
        {
            for (int step = 0; step < chainConfig.macroEntries.Count; step++)
            {
                if (!_running || !roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
                    return;

                var macroKey = chainConfig.macroEntries[step];
                if (macroKey.Key == Keys.None) continue;

                // Send the key
                ClientInput.SendKey(hWnd, macroKey.Key, blockOnAlt: false);

                // Handle click if enabled
                if (macroKey.ClickMode == 1)
                {
                    if (!SleepWithCancel(roClient, 25)) return;
                    ClientInput.ClickAtCurrentPosition(hWnd);
                }

                // Delay after sending key and/or click
                if (macroKey.Delay > 0)
                {
                    if (!SleepWithCancel(roClient, macroKey.Delay)) return;
                }
            }
        }

        private bool SleepWithCancel(Client roClient, int milliseconds)
        {
            int elapsed = 0;
            while (elapsed < milliseconds)
            {
                if (!_running) return false;
                if (!roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
                    return false;

                int slice = Math.Min(25, milliseconds - elapsed);
                Thread.Sleep(slice);
                elapsed += slice;
            }
            return true;
        }

        public void Start()
        {
            Client roClient = ClientSingleton.GetClient();
            if (roClient != null)
            {
                Stop(); // ensure thread and state are cleaned before starting

                _running = true;
                _wasTriggerPressed.Clear();
                this.thread = new ThreadRunner((_) => MacroThread(roClient), "MacroSwitch") { IterationDelay = 1 };
                ThreadRunner.Start(this.thread);
            }
        }

        public void Stop()
        {
            _running = false;
            if (this.thread != null)
            {
                ThreadRunner.Stop(this.thread);
                this.thread.Terminate();
                this.thread = null;
            }
            _wasTriggerPressed.Clear();
        }
    }
}