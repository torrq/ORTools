using ORTools.Shared.Protocol;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace ORTools.Worker.Model.Tabs;

public class AtkDefEquipConfig
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    private int _keySpammerDelay = AppConfig.ATKDEFSpammerDefaultDelay;
    [JsonPropertyName("keySpammerDelay")]
    public int KeySpammerDelay
    {
        get => _keySpammerDelay <= 0 ? AppConfig.ATKDEFSpammerDefaultDelay : _keySpammerDelay;
        set => _keySpammerDelay = value;
    }

    private int _switchDelay = AppConfig.ATKDEFSwitchDefaultDelay;
    [JsonPropertyName("switchDelay")]
    public int SwitchDelay
    {
        get => _switchDelay <= 0 ? AppConfig.ATKDEFSwitchDefaultDelay : _switchDelay;
        set => _switchDelay = value;
    }

    [JsonPropertyName("keySpammer")]
    public string KeySpammer { get; set; } = "None";

    [JsonPropertyName("keySpammerWithClick")]
    public bool KeySpammerWithClick { get; set; } = true;

    [JsonPropertyName("defKeys")]
    public Dictionary<string, string> DefKeys { get; set; } = new();

    [JsonPropertyName("atkKeys")]
    public Dictionary<string, string> AtkKeys { get; set; } = new();

    public AtkDefEquipConfig() { }

    public AtkDefEquipConfig(int id)
    {
        Id = id;
    }
}

public class AtkDef : IAction
{
    public const string ActionName = "ATKDEFMode";

    private ThreadRunner? _thread;
    
    [JsonPropertyName("equipConfigs")]
    public List<AtkDefEquipConfig> EquipConfigs { get; set; } = new();

    public string GetActionName() => ActionName;
    public string GetConfiguration() => System.Text.Json.JsonSerializer.Serialize(this);

    public void Start()
    {
        var client = ClientSingleton.GetClient();
        if (client != null)
        {
            Stop();
            _thread = new ThreadRunner(_ => AtkDefThread(client), "ATKDEF") { IterationDelay = 1 };
            ThreadRunner.Start(_thread);
        }
    }

    public void Stop()
    {
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
            // Do not delete rows when count shrinks, just add if missing
            int maxId = EquipConfigs.Count > 0 ? EquipConfigs.Max(x => x.Id) : 0;
            for (int i = 1; i <= count; i++)
            {
                if (!EquipConfigs.Any(x => x.Id == i))
                {
                    EquipConfigs.Add(new AtkDefEquipConfig(i));
                }
            }
        }
    }

    private int AtkDefThread(Client roClient)
    {
        if (roClient.IsTextInputActive() || roClient.IsDead()) return 0;
        if (!roClient.IsProcessRunning()) return 0;

        IntPtr hWnd = roClient.MainWindowHandle;
        if (hWnd == IntPtr.Zero) return 0;

        if (!ClientInput.IsForeground(hWnd)) return 0;

        List<AtkDefEquipConfig> currentConfigs;
        lock (EquipConfigs)
        {
            // Only process the configured number of rows (from ConfigGlobal)
            int rowsToProcess = ConfigGlobal.GetConfig().AtkDefRows;
            currentConfigs = EquipConfigs.Where(c => c.Id <= rowsToProcess).ToList();
        }

        try
        {
            foreach (var equipConfig in currentConfigs)
            {
                bool equipAtkItems = false;
                bool equipDefItems = false;
                bool ammo = false;

                if (Enum.TryParse<Keys>(equipConfig.KeySpammer, out var spammerKey) && spammerKey != Keys.None)
                {
                    if (WorkerNotifier.IsValidKey(equipConfig.KeySpammer) && ClientInput.IsKeyPressed(spammerKey)
                        && !ClientInput.IsKeyPressed(Keys.LMenu) && !ClientInput.IsKeyPressed(Keys.RMenu))
                    {
                        while (ClientInput.IsKeyPressed(spammerKey))
                        {
                        if (!ClientInput.IsForeground(hWnd))
                        {
                            break;
                        }

                        if (!equipAtkItems)
                        {
                            List<string> atkKeys;
                            lock (EquipConfigs)
                            {
                                atkKeys = equipConfig.AtkKeys.Values.Where(k => WorkerNotifier.IsValidKey(k)).ToList();
                            }
                            foreach (string keyStr in atkKeys)
                            {
                                if (Enum.TryParse<Keys>(keyStr, out var key))
                                {
                                    ClientInput.SendKey(hWnd, key, blockOnAlt: false); //Equip ATK Items
                                    Thread.Sleep(equipConfig.SwitchDelay);
                                }
                            }
                            equipAtkItems = true;
                        }

                        if (equipConfig.KeySpammerWithClick)
                        {
                            ClientInput.SendKey(hWnd, spammerKey, blockOnAlt: false);
                            ClientInput.SendLeftClick(hWnd);
                            AutoSwitchAmmo(roClient, ref ammo, hWnd);
                            Thread.Sleep(equipConfig.KeySpammerDelay);
                        }
                        else
                        {
                            ClientInput.SendKey(hWnd, spammerKey, blockOnAlt: false);
                            Thread.Sleep(equipConfig.KeySpammerDelay);
                        }
                    }

                    if (equipConfig.KeySpammerWithClick)
                    {
                        ClientInput.SendLeftClick(hWnd);
                    }

                    if (!equipDefItems)
                    {
                        List<string> defKeys;
                        lock (EquipConfigs)
                        {
                            defKeys = equipConfig.DefKeys.Values.Where(k => WorkerNotifier.IsValidKey(k)).ToList();
                        }
                        foreach (string keyStr in defKeys)
                        {
                            if (Enum.TryParse<Keys>(keyStr, out var key))
                            {
                                ClientInput.SendKey(hWnd, key, blockOnAlt: false); //Equip DEF Items
                                Thread.Sleep(equipConfig.SwitchDelay);
                            }
                        }
                        equipDefItems = true;
                    }
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