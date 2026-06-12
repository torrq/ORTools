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

        if (Win32Interop.GetForegroundWindow() != hWnd) return 0;

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
                    if (WorkerNotifier.IsValidKey(equipConfig.KeySpammer) && Win32Interop.IsKeyPressed(spammerKey)
                        && !Win32Interop.IsKeyPressed(Keys.LMenu) && !Win32Interop.IsKeyPressed(Keys.RMenu))
                    {
                        while (Win32Interop.IsKeyPressed(spammerKey))
                        {
                        if (Win32Interop.GetForegroundWindow() != hWnd)
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
                                    Win32Interop.PostMessage(hWnd, Constants.WM_KEYDOWN_MSG_ID, key, Win32Interop.CreateLParam(key, true)); //Equip ATK Items
                                    Win32Interop.PostMessage(hWnd, Constants.WM_KEYUP_MSG_ID, key, Win32Interop.CreateLParam(key, false));
                                    Thread.Sleep(equipConfig.SwitchDelay);
                                }
                            }
                            equipAtkItems = true;
                        }

                        if (equipConfig.KeySpammerWithClick)
                        {
                            Win32Interop.PostMessage(hWnd, Constants.WM_KEYDOWN_MSG_ID, spammerKey, Win32Interop.CreateLParam(spammerKey, true));
                            Win32Interop.PostMessage(hWnd, Constants.WM_KEYUP_MSG_ID, spammerKey, Win32Interop.CreateLParam(spammerKey, false));
                            Win32Interop.PostMessage(hWnd, Constants.WM_LBUTTONDOWN, Keys.None, 0);
                            AutoSwitchAmmo(roClient, ref ammo, hWnd);
                            Thread.Sleep(1);
                            Win32Interop.PostMessage(hWnd, Constants.WM_LBUTTONUP, Keys.None, 0);
                            Thread.Sleep(equipConfig.KeySpammerDelay);
                        }
                        else
                        {
                            Win32Interop.PostMessage(hWnd, Constants.WM_KEYDOWN_MSG_ID, spammerKey, Win32Interop.CreateLParam(spammerKey, true));
                            Win32Interop.PostMessage(hWnd, Constants.WM_KEYUP_MSG_ID, spammerKey, Win32Interop.CreateLParam(spammerKey, false));
                            Thread.Sleep(equipConfig.KeySpammerDelay);
                        }
                    }

                    if (equipConfig.KeySpammerWithClick)
                    {
                        Win32Interop.PostMessage(hWnd, Constants.WM_LBUTTONDOWN, Keys.None, 0);
                        Thread.Sleep(1);
                        Win32Interop.PostMessage(hWnd, Constants.WM_LBUTTONUP, Keys.None, 0);
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
                                Win32Interop.PostMessage(hWnd, Constants.WM_KEYDOWN_MSG_ID, key, Win32Interop.CreateLParam(key, true)); //Equip DEF Items
                                Win32Interop.PostMessage(hWnd, Constants.WM_KEYUP_MSG_ID, key, Win32Interop.CreateLParam(key, false));
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
                    Win32Interop.PostMessage(hWnd, Constants.WM_KEYDOWN_MSG_ID, ammo1, Win32Interop.CreateLParam(ammo1, true));
                    Win32Interop.PostMessage(hWnd, Constants.WM_KEYUP_MSG_ID, ammo1, Win32Interop.CreateLParam(ammo1, false));
                    ammo = true;
                }
                else
                {
                    Win32Interop.PostMessage(hWnd, Constants.WM_KEYDOWN_MSG_ID, ammo2, Win32Interop.CreateLParam(ammo2, true));
                    Win32Interop.PostMessage(hWnd, Constants.WM_KEYUP_MSG_ID, ammo2, Win32Interop.CreateLParam(ammo2, false));
                    ammo = false;
                }
            }
        }
    }
}