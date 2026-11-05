using Newtonsoft.Json;
using ORTools.Shared.Protocol;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ORTools.Worker.Model.Tabs;

public class AtkDefEquipConfig
{
    [JsonProperty("id")]
    public int Id { get; set; }

    private int _keySpammerDelay = AppConfig.ATKDEFSpammerDefaultDelay;
    [JsonProperty("keySpammerDelay")]
    public int KeySpammerDelay
    {
        get => _keySpammerDelay < 0 ? AppConfig.ATKDEFSpammerDefaultDelay : _keySpammerDelay;
        set => _keySpammerDelay = value;
    }

    private int _switchDelay = AppConfig.ATKDEFSwitchDefaultDelay;
    [JsonProperty("switchDelay")]
    public int SwitchDelay
    {
        get => _switchDelay < 0 ? AppConfig.ATKDEFSwitchDefaultDelay : _switchDelay;
        set => _switchDelay = value;
    }

    [JsonProperty("keySpammer")]
    public string KeySpammer { get; set; } = "None";

    [JsonProperty("keySpammerWithClick")]
    public bool KeySpammerWithClick { get; set; } = true;

    [JsonProperty("defKeys")]
    public ConcurrentDictionary<string, string> DefKeys { get; set; } = new();

    [JsonProperty("atkKeys")]
    public ConcurrentDictionary<string, string> AtkKeys { get; set; } = new();

    public AtkDefEquipConfig() { }

    public AtkDefEquipConfig(int id)
    {
        Id = id;
    }
}

public class AtkDef : IAction
{
    public const string ActionName = "ATKDEFMode";

    private static readonly string[] SlotOrder = ["Head", "Body", "Weapon", "Shield", "Garment", "Shoes"];

    private ThreadRunner? _thread;
    private CancellationTokenSource? _cts;
    private volatile bool _running = false;

    public HoldMultiKeyMode HoldMultiKeyMode => ConfigGlobal.GetConfig().HoldMultiKeyMode;

    private readonly ConcurrentDictionary<Keys, bool> _keyPressedLastFrame = new();
    private readonly ConcurrentDictionary<Keys, long> _keyPressTimestamps = new();
    
    [JsonProperty("equipConfigs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<AtkDefEquipConfig> EquipConfigs { get; set; } = new();

    public AtkDef()
    {
        EnsureCorrectRowCount(ConfigGlobal.GetConfig().AtkDefRows);
    }

    [JsonConstructor]
    public AtkDef(List<AtkDefEquipConfig>? equipConfigs)
    {
        EquipConfigs = equipConfigs ?? new List<AtkDefEquipConfig>();
        EnsureCorrectRowCount(ConfigGlobal.GetConfig().AtkDefRows);
    }

    public string GetActionName() => ActionName;
    public string GetConfiguration() => JsonConvert.SerializeObject(this);

    public void Start()
    {
        var client = ClientSingleton.GetClient();
        if (client != null)
        {
            Stop();
            _running = true;
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;
            _keyPressedLastFrame.Clear();
            _keyPressTimestamps.Clear();
            _thread = new ThreadRunner(_ => AtkDefThread(client, ct), "ATKDEF")
            {
                IterationDelay = 1,
                Priority = ThreadPriority.AboveNormal
            };
            ThreadRunner.Start(_thread);
        }
    }

    public void Stop()
    {
        _running = false;
        _cts?.Cancel();
        _keyPressedLastFrame.Clear();
        _keyPressTimestamps.Clear();
        if (_thread != null)
        {
            ThreadRunner.Stop(_thread);
            _thread.Terminate();
            _thread.Join(500);
            _thread = null;
        }
        _cts = null;
    }

    public void EnsureCorrectRowCount(int count)
    {
        lock (EquipConfigs)
        {
            // 1. Detect and resolve duplicate IDs caused by legacy JSON appending bugs
            if (EquipConfigs.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            {
                // Prefer entries that have configured data (spammer or keys)
                var configuredEntries = EquipConfigs
                    .Where(x => (x.KeySpammer != "None" && !string.IsNullOrWhiteSpace(x.KeySpammer)) || x.DefKeys.Count > 0 || x.AtkKeys.Count > 0)
                    .ToList();

                var emptyEntries = EquipConfigs
                    .Where(x => (x.KeySpammer == "None" || string.IsNullOrWhiteSpace(x.KeySpammer)) && x.DefKeys.Count == 0 && x.AtkKeys.Count == 0)
                    .ToList();

                var distinctList = new List<AtkDefEquipConfig>();

                foreach (var entry in configuredEntries)
                {
                    if (distinctList.Count >= count) break;
                    distinctList.Add(entry);
                }

                foreach (var entry in emptyEntries)
                {
                    if (distinctList.Count >= count) break;
                    distinctList.Add(entry);
                }

                for (int i = 0; i < distinctList.Count; i++)
                {
                    distinctList[i].Id = i + 1;
                }

                EquipConfigs.Clear();
                EquipConfigs.AddRange(distinctList);
            }

            // 2. Do not delete rows when count shrinks, just add if missing
            for (int i = 1; i <= count; i++)
            {
                if (!EquipConfigs.Any(x => x.Id == i))
                {
                    EquipConfigs.Add(new AtkDefEquipConfig(i));
                }
            }
            EquipConfigs.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
    }

    private void UpdateKeyEdges(Keys key, out bool isPressed, out bool justPressed)
    {
        isPressed = ClientInput.IsKeyPressed(key);
        bool wasPressed = _keyPressedLastFrame.TryGetValue(key, out bool pressed) && pressed;
        _keyPressedLastFrame[key] = isPressed;

        justPressed = isPressed && !wasPressed;
        if (justPressed)
        {
            _keyPressTimestamps[key] = Stopwatch.GetTimestamp();
        }
        else if (!isPressed)
        {
            _keyPressTimestamps.TryRemove(key, out _);
        }
    }

    private bool WaitInterruptibly(int ms, CancellationToken ct, Func<bool> keepGoing)
    {
        if (ms <= 0) return keepGoing();

        long startTick = Stopwatch.GetTimestamp();
        double targetTicks = ms * (Stopwatch.Frequency / 1000.0);

        while (!ct.IsCancellationRequested && _running)
        {
            if (!keepGoing()) return false;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTick;
            double remainingTicks = targetTicks - elapsedTicks;
            if (remainingTicks <= 0) break;

            double remainingMs = (remainingTicks * 1000.0) / Stopwatch.Frequency;
            if (remainingMs <= 1.5)
            {
                Thread.SpinWait(10);
                continue;
            }

            int sleepChunk = (int)Math.Min(10.0, remainingMs - 1.0);
            if (sleepChunk > 0)
            {
                ct.WaitHandle.WaitOne(sleepChunk);
            }
        }

        return !ct.IsCancellationRequested && _running && keepGoing();
    }

    private void EquipAtkItems(Client roClient, IntPtr hWnd, AtkDefEquipConfig config, CancellationToken ct)
    {
        foreach (string slot in SlotOrder)
        {
            if (!_running || ct.IsCancellationRequested || !ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive()) break;

            if (config.AtkKeys.TryGetValue(slot, out string? keyStr)
                && !string.IsNullOrEmpty(keyStr)
                && Enum.TryParse<Keys>(keyStr, out var key)
                && WorkerNotifier.IsValidKey(key))
            {
                ClientInput.SendKeyFast(hWnd, key);
                if (!WaitInterruptibly(Math.Max(1, config.SwitchDelay), ct, () =>
                    _running && !ct.IsCancellationRequested && ClientInput.IsForeground(hWnd) && !roClient.IsDead() && !roClient.IsTextInputActive() && roClient.IsProcessRunning()))
                {
                    break;
                }
            }
        }
    }

    private void EquipDefItems(Client roClient, IntPtr hWnd, AtkDefEquipConfig config, CancellationToken ct)
    {
        foreach (string slot in SlotOrder)
        {
            if (!_running || ct.IsCancellationRequested || !ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive()) break;

            if (config.DefKeys.TryGetValue(slot, out string? keyStr)
                && !string.IsNullOrEmpty(keyStr)
                && Enum.TryParse<Keys>(keyStr, out var key)
                && WorkerNotifier.IsValidKey(key))
            {
                ClientInput.SendKeyFast(hWnd, key);
                if (!WaitInterruptibly(Math.Max(1, config.SwitchDelay), ct, () =>
                    _running && !ct.IsCancellationRequested && ClientInput.IsForeground(hWnd) && !roClient.IsDead() && !roClient.IsTextInputActive() && roClient.IsProcessRunning()))
                {
                    break;
                }
            }
        }
    }

    private int AtkDefThread(Client roClient, CancellationToken ct)
    {
        if (!_running || ct.IsCancellationRequested) return 0;
        if (!roClient.IsProcessRunning() || roClient.IsDead()) return 0;

        IntPtr hWnd = roClient.MainWindowHandle;
        if (hWnd == IntPtr.Zero || !ClientInput.IsForeground(hWnd)) return 0;
        if (roClient.IsTextInputActive()) return 0;

        if (ClientInput.IsKeyPressed(Keys.LMenu) || ClientInput.IsKeyPressed(Keys.RMenu)) return 0;

        List<(AtkDefEquipConfig Config, Keys Key)> configuredRows = new(4);
        lock (EquipConfigs)
        {
            // Only process the configured number of rows (from ConfigGlobal)
            int rowsToProcess = ConfigGlobal.GetConfig().AtkDefRows;
            for (int i = 0; i < EquipConfigs.Count; i++)
            {
                var cfg = EquipConfigs[i];
                if (cfg.Id <= rowsToProcess
                    && !string.IsNullOrEmpty(cfg.KeySpammer)
                    && Enum.TryParse<Keys>(cfg.KeySpammer, out var spammerKey)
                    && WorkerNotifier.IsValidKey(spammerKey))
                {
                    configuredRows.Add((cfg, spammerKey));
                }
            }
        }

        bool anyPressed = false;
        for (int i = 0; i < configuredRows.Count; i++)
        {
            UpdateKeyEdges(configuredRows[i].Key, out bool isPressed, out _);
            if (isPressed)
            {
                anyPressed = true;
            }
        }

        if (!anyPressed)
        {
            return 0;
        }

        try
        {
            RunSpamLoop(roClient, hWnd, configuredRows, ct);
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[AtkDefThread] Exception: {ex.Message}");
        }

        return 0;
    }

    private void RunSpamLoop(Client roClient, IntPtr hWnd, List<(AtkDefEquipConfig Config, Keys Key)> configuredRows, CancellationToken ct)
    {
        AtkDefEquipConfig? activeRow = null;
        AtkDefEquipConfig? lastEquippedAtkRow = null;
        bool ammo = false;
        int roundRobinIndex = 0;
        var heldRows = new List<(AtkDefEquipConfig Config, Keys Key)>(configuredRows.Count);

        long sessionStart = Stopwatch.GetTimestamp();
        int totalCasts = 0;

        while (_running && !ct.IsCancellationRequested)
        {
            if (!ClientInput.IsForeground(hWnd) || !roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive())
            {
                break;
            }

            if (ClientInput.IsKeyPressed(Keys.LMenu) || ClientInput.IsKeyPressed(Keys.RMenu))
            {
                break;
            }

            heldRows.Clear();
            for (int i = 0; i < configuredRows.Count; i++)
            {
                var row = configuredRows[i];
                UpdateKeyEdges(row.Key, out bool isPressed, out _);
                if (isPressed)
                {
                    heldRows.Add(row);
                }
            }

            if (heldRows.Count == 0)
            {
                break;
            }

            AtkDefEquipConfig targetConfig;
            Keys targetKey;

            if (this.HoldMultiKeyMode == HoldMultiKeyMode.LastPressedWins)
            {
                var latest = heldRows[0];
                long bestTs = _keyPressTimestamps.TryGetValue(latest.Key, out long ts0) ? ts0 : 0;
                for (int i = 1; i < heldRows.Count; i++)
                {
                    long tsItem = _keyPressTimestamps.TryGetValue(heldRows[i].Key, out long tsi) ? tsi : 0;
                    if (tsItem > bestTs)
                    {
                        bestTs = tsItem;
                        latest = heldRows[i];
                    }
                }
                targetConfig = latest.Config;
                targetKey = latest.Key;
            }
            else // RoundRobinAll
            {
                if (roundRobinIndex >= heldRows.Count)
                {
                    roundRobinIndex = 0;
                }
                var item = heldRows[roundRobinIndex];
                targetConfig = item.Config;
                targetKey = item.Key;
                roundRobinIndex = (roundRobinIndex + 1) % heldRows.Count;
            }

            if (targetConfig != activeRow)
            {
                EquipAtkItems(roClient, hWnd, targetConfig, ct);
                activeRow = targetConfig;
                lastEquippedAtkRow = targetConfig;
            }

            if (!_running || ct.IsCancellationRequested || !ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive() || !roClient.IsProcessRunning())
            {
                break;
            }

            if (targetConfig.KeySpammerWithClick)
            {
                ClientInput.SendKeyFast(hWnd, targetKey);
                Win32Interop.PostMessage(hWnd, Constants.WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
                AutoSwitchAmmo(roClient, ref ammo, hWnd);
                Thread.Sleep(1);
                Win32Interop.PostMessage(hWnd, Constants.WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                ClientInput.SendKeyFast(hWnd, targetKey);
            }

            totalCasts++;

            int effectiveDelay = Math.Max(1, targetConfig.KeySpammerDelay);
            long currentKeyTimestamp = _keyPressTimestamps.TryGetValue(targetKey, out long ts) ? ts : 0;
            WaitInterruptibly(effectiveDelay, ct, () =>
            {
                if (!_running || ct.IsCancellationRequested || !ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive() || !roClient.IsProcessRunning())
                    return false;

                if (ClientInput.IsKeyPressed(Keys.LMenu) || ClientInput.IsKeyPressed(Keys.RMenu))
                    return false;

                if (!ClientInput.IsKeyPressed(targetKey))
                    return false;

                if (this.HoldMultiKeyMode == HoldMultiKeyMode.LastPressedWins)
                {
                    for (int i = 0; i < configuredRows.Count; i++)
                    {
                        var row = configuredRows[i];
                        if (row.Config.Id != targetConfig.Id && row.Key != targetKey)
                        {
                            if (ClientInput.IsKeyPressed(row.Key)
                                && (!_keyPressTimestamps.TryGetValue(row.Key, out long otherTs) || otherTs > currentKeyTimestamp))
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            });
        }

        if (_running && !ct.IsCancellationRequested && lastEquippedAtkRow != null && ClientInput.IsForeground(hWnd) && !roClient.IsDead() && !roClient.IsTextInputActive() && roClient.IsProcessRunning())
        {
            var defRowToEquip = (activeRow != null && activeRow.DefKeys.Count > 0) ? activeRow : lastEquippedAtkRow;

            if (defRowToEquip.KeySpammerWithClick)
            {
                ClientInput.SendLeftClickFast(hWnd);
            }

            EquipDefItems(roClient, hWnd, defRowToEquip, ct);
        }

        if (totalCasts > 0)
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - sessionStart) * 1000.0 / Stopwatch.Frequency;
            double avgCycleMs = elapsedMs / totalCasts;
            DebugLogger.Debug($"[ATKDEF] Session ended: {totalCasts} casts in {elapsedMs:F1}ms (avg {avgCycleMs:F2}ms/cast)");
        }
    }

    private void AutoSwitchAmmo(Client roClient, ref bool ammo, IntPtr hWnd)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        if (prefs.SwitchAmmo)
        {
            if (WorkerNotifier.IsValidKey(prefs.Ammo1Key) && WorkerNotifier.IsValidKey(prefs.Ammo2Key))
            {
                var ammo1 = prefs.Ammo1Key;
                var ammo2 = prefs.Ammo2Key;
                
                if (!ammo)
                {
                    ClientInput.SendKeyFast(hWnd, ammo1);
                    ammo = true;
                }
                else
                {
                    ClientInput.SendKeyFast(hWnd, ammo2);
                    ammo = false;
                }
            }
        }
    }
}