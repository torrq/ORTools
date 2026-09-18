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
    private volatile bool _running = false;
    
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
            _thread = new ThreadRunner(_ => AtkDefThread(client), "ATKDEF") { IterationDelay = 1 };
            ThreadRunner.Start(_thread);
        }
    }

    public void Stop()
    {
        _running = false;
        if (_thread != null)
        {
            ThreadRunner.Stop(_thread);
            _thread.Terminate();
            _thread = null;
        }
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

    private void SleepWithCancel(Client roClient, int totalMs)
    {
        if (totalMs <= 0) return;
        int elapsed = 0;
        while (_running && elapsed < totalMs)
        {
            int chunk = Math.Min(25, totalMs - elapsed);
            Thread.Sleep(chunk);
            elapsed += chunk;
            if (!roClient.IsProcessRunning() || roClient.IsDead()) break;
        }
    }

    private int AtkDefThread(Client roClient)
    {
        if (!_running) return 0;
        if (!roClient.IsProcessRunning() || roClient.IsDead()) return 0;
        if (roClient.IsTextInputActive()) return 0;

        IntPtr hWnd = roClient.MainWindowHandle;
        if (hWnd == IntPtr.Zero) return 0;

        if (!ClientInput.IsForeground(hWnd)) return 0;

        List<AtkDefEquipConfig> currentConfigs;
        lock (EquipConfigs)
        {
            // Only process the configured number of rows (from ConfigGlobal)
            int rowsToProcess = ConfigGlobal.GetConfig().AtkDefRows;
            currentConfigs = EquipConfigs.Where(c => c.Id <= rowsToProcess).OrderBy(c => c.Id).ToList();
        }

        try
        {
            foreach (var equipConfig in currentConfigs)
            {
                if (!_running || !roClient.IsProcessRunning() || roClient.IsDead() || roClient.IsTextInputActive() || !ClientInput.IsForeground(hWnd))
                {
                    break;
                }

                if (!WorkerNotifier.IsValidKey(equipConfig.KeySpammer))
                    continue;

                if (!Enum.TryParse<Keys>(equipConfig.KeySpammer, out var spammerKey) || spammerKey == Keys.None)
                    continue;

                if (ClientInput.IsKeyPressed(spammerKey)
                    && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                {
                    bool equipAtkItems = false;
                    bool equipDefItems = false;
                    bool ammo = false;

                    while (_running && ClientInput.IsKeyPressed(spammerKey))
                    {
                        if (!ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive() || !roClient.IsProcessRunning())
                        {
                            break;
                        }

                        if (!equipAtkItems)
                        {
                            foreach (string slot in SlotOrder)
                            {
                                if (!_running || !ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive()) break;

                                if (equipConfig.AtkKeys.TryGetValue(slot, out string? keyStr)
                                    && WorkerNotifier.IsValidKey(keyStr)
                                    && Enum.TryParse<Keys>(keyStr, out var key))
                                {
                                    ClientInput.SendKey(hWnd, key, blockOnAlt: false);
                                    SleepWithCancel(roClient, equipConfig.SwitchDelay);
                                }
                            }
                            equipAtkItems = true;
                        }

                        if (!_running || !ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive())
                        {
                            break;
                        }

                        if (equipConfig.KeySpammerWithClick)
                        {
                            ClientInput.SendKey(hWnd, spammerKey, blockOnAlt: false);
                            ClientInput.SendLeftClick(hWnd);
                            AutoSwitchAmmo(roClient, ref ammo, hWnd);
                            SleepWithCancel(roClient, equipConfig.KeySpammerDelay);
                        }
                        else
                        {
                            ClientInput.SendKey(hWnd, spammerKey, blockOnAlt: false);
                            SleepWithCancel(roClient, equipConfig.KeySpammerDelay);
                        }
                    }

                    if (_running && equipAtkItems && !equipDefItems && ClientInput.IsForeground(hWnd) && !roClient.IsDead() && !roClient.IsTextInputActive())
                    {
                        if (equipConfig.KeySpammerWithClick)
                        {
                            ClientInput.SendLeftClick(hWnd);
                        }

                        foreach (string slot in SlotOrder)
                        {
                            if (!_running || !ClientInput.IsForeground(hWnd) || roClient.IsDead() || roClient.IsTextInputActive()) break;

                            if (equipConfig.DefKeys.TryGetValue(slot, out string? keyStr)
                                && WorkerNotifier.IsValidKey(keyStr)
                                && Enum.TryParse<Keys>(keyStr, out var key))
                            {
                                ClientInput.SendKey(hWnd, key, blockOnAlt: false); //Equip DEF Items
                                SleepWithCancel(roClient, equipConfig.SwitchDelay);
                            }
                        }
                        equipDefItems = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[AtkDefThread] Exception: {ex.Message}");
        }

        return 0;
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
                    ClientInput.SendKey(hWnd, ammo1, blockOnAlt: false);
                    ammo = true;
                }
                else
                {
                    ClientInput.SendKey(hWnd, ammo2, blockOnAlt: false);
                    ammo = false;
                }
            }
        }
    }
}