using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ORTools.Shared.Protocol;

namespace ORTools.Worker
{
    public class TolerantHoldMultiKeyModeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            objectType == typeof(HoldMultiKeyMode) || objectType == typeof(HoldMultiKeyMode?);

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value is HoldMultiKeyMode mode)
            {
                writer.WriteValue(mode.ToString());
            }
            else
            {
                writer.WriteValue(HoldMultiKeyMode.RoundRobinAll.ToString());
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                string? str = reader.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(str) && Enum.TryParse<HoldMultiKeyMode>(str, ignoreCase: true, out var result))
                {
                    return result;
                }
            }
            else if (reader.TokenType == JsonToken.Integer)
            {
                try
                {
                    int intVal = Convert.ToInt32(reader.Value);
                    if (Enum.IsDefined(typeof(HoldMultiKeyMode), intVal))
                    {
                        return (HoldMultiKeyMode)intVal;
                    }
                }
                catch { }
            }

            // Return default on invalid string or typo, preventing TryDeserialize from discarding the profile section
            return HoldMultiKeyMode.RoundRobinAll;
        }
    }

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
        public const string ACTION_NAME = "SkillSpammer";

        [JsonConverter(typeof(TolerantHoldMultiKeyModeConverter))]
        public HoldMultiKeyMode HoldMultiKeyMode
        {
            get => ConfigGlobal.IsLoaded ? ConfigGlobal.GetConfig().HoldMultiKeyMode : _holdMultiKeyMode;
            set
            {
                _holdMultiKeyMode = value;
                if (ConfigGlobal.IsLoaded)
                {
                    ConfigGlobal.GetConfig().HoldMultiKeyMode = value;
                }
            }
        }
        private HoldMultiKeyMode _holdMultiKeyMode = HoldMultiKeyMode.RoundRobinAll;

        private ConcurrentDictionary<Keys, bool> toggledKeys = new ConcurrentDictionary<Keys, bool>();
        private ConcurrentDictionary<Keys, bool> keyPressedLastFrame = new ConcurrentDictionary<Keys, bool>();
        private ConcurrentDictionary<Keys, long> _keyPressTimestamps = new ConcurrentDictionary<Keys, long>();

        public event EventHandler<bool>? ToggleModeChanged;

        public Keys ToggleModeKey { get; set; } = Keys.None;

        private ThreadRunner? thread;
        private CancellationTokenSource? _cts;
        private volatile bool _running = false;

        private int _shiftHeld = 0;

        private ConcurrentDictionary<string, KeyConfig> _spammerEntries = new ConcurrentDictionary<string, KeyConfig>();
        private volatile KeyConfig[] _activeConfigsCache = Array.Empty<KeyConfig>();
        private volatile bool _activeConfigsDirty = true;

        public ConcurrentDictionary<string, KeyConfig> SpammerEntries
        {
            get => _spammerEntries;
            set
            {
                _spammerEntries = value ?? new ConcurrentDictionary<string, KeyConfig>();
                _activeConfigsDirty = true;
            }
        }

        private KeyConfig[] GetActiveConfigs()
        {
            if (_activeConfigsDirty)
            {
                _activeConfigsCache = _spammerEntries.Values
                    .Where(c => c != null && (c.ClickActive || c.IsIndeterminate) && c.Key != Keys.None)
                    .ToArray();
                _activeConfigsDirty = false;
            }
            return _activeConfigsCache;
        }

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

        public static bool IsGameWindowActive()
        {
            try
            {
                Client? currentClient = ClientSingleton.GetClient();
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

        public void Start()
        {
            Client? roClient = ClientSingleton.GetClient();
            if (roClient == null) return;

            Stop();

            _running = true;
            _activeConfigsDirty = true;
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;
            ResetToggleState();
            keyPressedLastFrame.Clear();
            _keyPressTimestamps.Clear();

            this.thread = new ThreadRunner(_ => SkillSpammerThread(roClient, ct), "SkillSpammerThread")
            {
                IterationDelay = 1,
                Priority = ThreadPriority.AboveNormal
            };
            ThreadRunner.Start(this.thread);
        }

        public void Stop()
        {
            _running = false;
            _cts?.Cancel();
            ReleaseShiftIfHeld();
            ResetToggleState();
            keyPressedLastFrame.Clear();
            _keyPressTimestamps.Clear();

            if (this.thread != null)
            {
                ThreadRunner.Stop(this.thread);
                this.thread.Terminate();
                this.thread.Join(500);
                this.thread = null;
            }
            _cts = null;
        }

        public void ResetToggleState()
        {
            toggledKeys.Clear();
        }

        private void HoldShiftOnce()
        {
            if (Interlocked.Exchange(ref _shiftHeld, 1) == 0)
            {
                ClientInput.HoldShift();
            }
        }

        private void ReleaseShiftIfHeld()
        {
            if (Interlocked.Exchange(ref _shiftHeld, 0) == 1)
            {
                ClientInput.ReleaseShift();
            }
        }

        private int SkillSpammerThread(Client roClient, CancellationToken ct)
        {
            if (!_running || ct.IsCancellationRequested)
                return 0;

            IntPtr hWnd = roClient.MainWindowHandle;
            if (hWnd == IntPtr.Zero || !ClientInput.IsForeground(hWnd))
                return 0;

            if (!roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
                return 0;

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

            if (this.ToggleMode)
            {
                HandleToggleModeSpam(roClient, hWnd, ct, noShift, mouseFlick);
            }
            else
            {
                HandleHoldModeSpam(roClient, hWnd, ct, noShift, mouseFlick);
            }

            return 0;
        }

        private void UpdateKeyEdges(KeyConfig config, out bool isPressed, out bool justPressed)
        {
            isPressed = ClientInput.IsKeyPressed(config.Key);
            bool wasPressed = keyPressedLastFrame.TryGetValue(config.Key, out bool pressed) && pressed;
            keyPressedLastFrame[config.Key] = isPressed;

            justPressed = isPressed && !wasPressed;
            if (justPressed)
            {
                _keyPressTimestamps[config.Key] = Stopwatch.GetTimestamp();
            }
            else if (!isPressed)
            {
                _keyPressTimestamps.TryRemove(config.Key, out _);
            }
        }

        private void HandleHoldModeSpam(Client roClient, IntPtr hWnd, CancellationToken ct, bool noShift, bool mouseFlick)
        {
            var activeConfigs = GetActiveConfigs();
            if (activeConfigs.Length == 0) return;

            bool anyPressed = false;
            for (int i = 0; i < activeConfigs.Length; i++)
            {
                UpdateKeyEdges(activeConfigs[i], out bool isPressed, out _);
                if (isPressed) anyPressed = true;
            }

            if (!anyPressed) return;
            if (ClientInput.IsKeyPressed(Keys.LMenu) || ClientInput.IsKeyPressed(Keys.RMenu)) return;

            RunSpamLoop(
                roClient,
                hWnd,
                ct,
                noShift,
                mouseFlick,
                isToggle: false,
                toggledConfig: null
            );
        }

        private void HandleToggleModeSpam(Client roClient, IntPtr hWnd, CancellationToken ct, bool noShift, bool mouseFlick)
        {
            var activeConfigs = GetActiveConfigs();
            if (activeConfigs.Length == 0) return;

            for (int i = 0; i < activeConfigs.Length; i++)
            {
                var config = activeConfigs[i];
                UpdateKeyEdges(config, out bool isPressed, out bool justPressed);

                if (justPressed && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                {
                    bool wasToggled = toggledKeys.TryGetValue(config.Key, out bool active) && active;
                    toggledKeys.Clear();

                    if (!wasToggled)
                    {
                        toggledKeys[config.Key] = true;
                        RunSpamLoop(
                            roClient,
                            hWnd,
                            ct,
                            noShift,
                            mouseFlick,
                            isToggle: true,
                            toggledConfig: config
                        );
                    }
                    break;
                }
            }
        }

        private enum ExitReason
        {
            None,
            KeyReleased,
            ToggledOff,
            FocusLost,
            TextInput,
            Dead,
            ProcessExited,
            Cancelled,
            Switched
        }

        private void RunSpamLoop(
            Client roClient,
            IntPtr hWnd,
            CancellationToken ct,
            bool noShift,
            bool mouseFlick,
            bool isToggle,
            KeyConfig? toggledConfig)
        {
            ExitReason exitReason = ExitReason.None;
            int casts = 0;
            long startTimestamp = Stopwatch.GetTimestamp();
            long maxCycleTicks = 0;
            bool anyWithClick = false;
            var heldConfigs = new List<KeyConfig>(16);
            var singleKeyList = new List<KeyConfig>(1) { null! };

            try
            {
                if (noShift)
                {
                    HoldShiftOnce();
                }

                if (isToggle && toggledConfig != null)
                {
                    int releaseTimeout = 0;
                    while (_running && !ct.IsCancellationRequested && ClientInput.IsKeyPressed(toggledConfig.Key) && releaseTimeout < 50)
                    {
                        Thread.Sleep(10);
                        releaseTimeout++;
                    }
                    keyPressedLastFrame[toggledConfig.Key] = false;
                }

                while (_running && !ct.IsCancellationRequested)
                {
                    if (!roClient.IsProcessRunning()) { exitReason = ExitReason.ProcessExited; break; }
                    if (roClient.IsDead()) { exitReason = ExitReason.Dead; break; }
                    if (roClient.IsTextInputActive()) { exitReason = ExitReason.TextInput; break; }
                    if (!ClientInput.IsForeground(hWnd)) { exitReason = ExitReason.FocusLost; break; }

                    var activeConfigs = GetActiveConfigs();
                    List<KeyConfig> keysToCast;

                    if (isToggle)
                    {
                        if (toggledConfig == null || !toggledKeys.TryGetValue(toggledConfig.Key, out bool active) || !active)
                        {
                            exitReason = ExitReason.ToggledOff;
                            break;
                        }

                        // Check if player tapped config.Key again to cancel
                        if (ClientInput.IsKeyPressed(toggledConfig.Key) && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                        {
                            toggledKeys[toggledConfig.Key] = false;
                            while (_running && !ct.IsCancellationRequested && ClientInput.IsKeyPressed(toggledConfig.Key)) Thread.Sleep(10);
                            keyPressedLastFrame[toggledConfig.Key] = false;
                            exitReason = ExitReason.ToggledOff;
                            break;
                        }

                        // Check if player pressed ToggleModeKey to turn off toggle mode
                        if (this.ToggleModeKey != Keys.None && ClientInput.IsKeyPressed(this.ToggleModeKey)
                            && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                        {
                            this.ToggleMode = false;
                            ToggleModeChanged?.Invoke(this, false);
                            ProfileSingleton.SetConfiguration(this);
                            toggledKeys.Clear();
                            while (_running && !ct.IsCancellationRequested && ClientInput.IsKeyPressed(this.ToggleModeKey)) Thread.Sleep(10);
                            exitReason = ExitReason.ToggledOff;
                            break;
                        }

                        // Check if player pressed another configured spammer key to switch to it
                        bool switched = false;
                        for (int i = 0; i < activeConfigs.Length; i++)
                        {
                            var cfg = activeConfigs[i];
                            if (cfg.Key != toggledConfig.Key
                                && ClientInput.IsKeyPressed(cfg.Key)
                                && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                            {
                                toggledKeys[toggledConfig.Key] = false;
                                switched = true;
                                break;
                            }
                        }
                        if (switched)
                        {
                            exitReason = ExitReason.Switched;
                            break;
                        }

                        singleKeyList[0] = toggledConfig;
                        keysToCast = singleKeyList;
                    }
                    else
                    {
                        heldConfigs.Clear();
                        for (int i = 0; i < activeConfigs.Length; i++)
                        {
                            var cfg = activeConfigs[i];
                            UpdateKeyEdges(cfg, out bool isPressed, out _);
                            if (isPressed)
                            {
                                heldConfigs.Add(cfg);
                            }
                        }

                        if (heldConfigs.Count == 0)
                        {
                            exitReason = ExitReason.KeyReleased;
                            break;
                        }

                        if (this.HoldMultiKeyMode == HoldMultiKeyMode.LastPressedWins)
                        {
                            KeyConfig latest = heldConfigs[0];
                            long latestTs = _keyPressTimestamps.TryGetValue(latest.Key, out long t0) ? t0 : 0;
                            for (int i = 1; i < heldConfigs.Count; i++)
                            {
                                var candidate = heldConfigs[i];
                                long candidateTs = _keyPressTimestamps.TryGetValue(candidate.Key, out long tc) ? tc : 0;
                                if (candidateTs > latestTs)
                                {
                                    latest = candidate;
                                    latestTs = candidateTs;
                                }
                            }
                            singleKeyList[0] = latest;
                            keysToCast = singleKeyList;
                        }
                        else
                        {
                            keysToCast = heldConfigs;
                        }
                    }

                    bool brokeEarly = false;
                    for (int kIdx = 0; kIdx < keysToCast.Count; kIdx++)
                    {
                        var cfg = keysToCast[kIdx];
                        if (!_running || ct.IsCancellationRequested) { exitReason = ExitReason.Cancelled; brokeEarly = true; break; }
                        if (!ClientInput.IsForeground(hWnd)) { exitReason = ExitReason.FocusLost; brokeEarly = true; break; }
                        if (roClient.IsDead()) { exitReason = ExitReason.Dead; brokeEarly = true; break; }
                        if (roClient.IsTextInputActive()) { exitReason = ExitReason.TextInput; brokeEarly = true; break; }

                        if (!isToggle && !ClientInput.IsKeyPressed(cfg.Key))
                        {
                            continue;
                        }

                        bool withClick = cfg.ClickActive && !cfg.IsIndeterminate;
                        if (withClick) anyWithClick = true;

                        long cycleStart = Stopwatch.GetTimestamp();

                        ClientInput.SendKeyFast(hWnd, cfg.Key);

                        if (withClick)
                        {
                            DoClick(hWnd, mouseFlick);
                        }

                        long cycleEnd = Stopwatch.GetTimestamp();
                        casts++;
                        long cycleTicks = cycleEnd - cycleStart;
                        if (cycleTicks > maxCycleTicks) maxCycleTicks = cycleTicks;

                        int effectiveDelay = Math.Max(1, this.SpammerDelay);
                        long currentKeyTs = _keyPressTimestamps.TryGetValue(cfg.Key, out long tsVal) ? tsVal : 0;

                        bool keepGoing = WaitInterruptibly(effectiveDelay, ct, () =>
                        {
                            if (!_running || ct.IsCancellationRequested) return false;
                            if (!ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive()) return false;

                            if (isToggle)
                            {
                                if (ClientInput.IsKeyPressed(cfg.Key) && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                                    return false;
                                if (this.ToggleModeKey != Keys.None && ClientInput.IsKeyPressed(this.ToggleModeKey) && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                                    return false;
                                for (int i = 0; i < activeConfigs.Length; i++)
                                {
                                    var other = activeConfigs[i];
                                    if (other.Key != cfg.Key && ClientInput.IsKeyPressed(other.Key) && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                                        return false;
                                }
                                return true;
                            }

                            if (!ClientInput.IsKeyPressed(cfg.Key)) return false;

                            if (this.HoldMultiKeyMode == HoldMultiKeyMode.LastPressedWins)
                            {
                                for (int i = 0; i < activeConfigs.Length; i++)
                                {
                                    var other = activeConfigs[i];
                                    if (other.Key != cfg.Key && ClientInput.IsKeyPressed(other.Key))
                                    {
                                        if (!_keyPressTimestamps.TryGetValue(other.Key, out long otherTs) || otherTs > currentKeyTs)
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }

                            return true;
                        });

                        if (!keepGoing)
                        {
                            if (ct.IsCancellationRequested || !_running) { exitReason = ExitReason.Cancelled; brokeEarly = true; break; }
                            if (!ClientInput.IsForeground(hWnd)) { exitReason = ExitReason.FocusLost; brokeEarly = true; break; }
                            if (roClient.IsDead()) { exitReason = ExitReason.Dead; brokeEarly = true; break; }
                            if (roClient.IsTextInputActive()) { exitReason = ExitReason.TextInput; brokeEarly = true; break; }

                            // Key was released, switched, or toggled off during wait; re-evaluate at top of outer while loop
                            break;
                        }
                    }

                    if (brokeEarly) break;
                }

                if (anyWithClick && ClientInput.IsForeground(hWnd) && !roClient.IsDead() && !roClient.IsTextInputActive())
                {
                    DoClick(hWnd, mouseFlick);
                }
            }
            finally
            {
                if (noShift)
                {
                    ReleaseShiftIfHeld();
                }

                if (casts > 0)
                {
                    double totalMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
                    double avgMs = totalMs / casts;
                    double maxMs = maxCycleTicks * 1000.0 / Stopwatch.Frequency;
                    DebugLogger.Debug($"[SkillSpammer] Session ended: {casts} casts, total {totalMs:F1}ms, avg {avgMs:F1}ms, maxCycle {maxMs:F1}ms, exit: {exitReason}");
                }
            }
        }

        private void DoClick(IntPtr hWnd, bool mouseFlick)
        {
            if (mouseFlick)
            {
                Win32Interop.SendRelativeMouseMove(-Constants.MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK, -Constants.MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK);
                ClientInput.SendLeftClickFast(hWnd);
                Win32Interop.SendRelativeMouseMove(Constants.MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK, Constants.MOUSE_DIAGONAL_MOVIMENTATION_PIXELS_AHK);
            }
            else
            {
                ClientInput.SendLeftClickFast(hWnd);
            }
        }

        private static bool WaitInterruptibly(int ms, CancellationToken ct, Func<bool> keepGoing)
        {
            if (ms <= 0) return !ct.IsCancellationRequested && keepGoing();

            long freq = Stopwatch.Frequency;
            long end = Stopwatch.GetTimestamp() + (long)(ms * (double)freq / 1000.0);

            while (true)
            {
                if (ct.IsCancellationRequested || !keepGoing()) return false;

                long remaining = end - Stopwatch.GetTimestamp();
                if (remaining <= 0) return true;

                double remainingMs = remaining * 1000.0 / freq;
                if (remainingMs > 1.5)
                {
                    int slice = Math.Max(1, Math.Min(10, (int)(remainingMs - 1.0)));
                    if (ct.WaitHandle.WaitOne(slice)) return false;
                }
                else
                {
                    Thread.SpinWait(10);
                }
            }
        }

        public void AddSkillSpammerEntry(string entryName, KeyConfig value)
        {
            this.SpammerEntries[entryName] = value;
            _activeConfigsDirty = true;
        }

        public void RemoveSkillSpammerEntry(string entryName)
        {
            this.SpammerEntries.TryRemove(entryName, out _);
            _activeConfigsDirty = true;
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