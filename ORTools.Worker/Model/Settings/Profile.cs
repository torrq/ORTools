using System.IO;
using Newtonsoft.Json.Linq;
using ORTools.Worker.Model.Tabs;

namespace ORTools.Worker;

public class Profile
{
    public string         Name            { get; set; } = "";
    public ConfigProfile  UserPreferences { get; set; } = new();
    public SkillSpammer   SkillSpammer    { get; set; } = new();
    public AutopotHP      AutopotHP       { get; set; } = new AutopotHP(AutopotHP.ACTION_NAME_AUTOPOT_HP);
    public AutopotSP      AutopotSP       { get; set; } = new AutopotSP(AutopotSP.ACTION_NAME_AUTOPOT_SP);
    public SkillTimer     SkillTimer      { get; set; } = new();
    public AutoBuffSkill  AutobuffSkill   { get; set; } = new AutoBuffSkill(AutoBuffSkill.ACTION_NAME_AUTOBUFFSKILL);
    public AutoBuffItem   AutobuffItem    { get; set; } = new AutoBuffItem(AutoBuffItem.ACTION_NAME_AUTOBUFFITEM);
    public StatusRecovery StatusRecovery  { get; set; } = new();
    public DebuffRecovery DebuffsRecovery { get; set; } = new DebuffRecovery("DebuffsRecovery");
    public MacroSong      SongMacro       { get; set; } = new();
    public MacroSwitch    MacroSwitch     { get; set; } = new MacroSwitch(MacroSwitch.ACTION_NAME_MACRO_SWITCH, MacroSwitchKey.TOTAL_MACRO_LANES);
    public TransferHelper TransferHelper  { get; set; } = new();
    public AtkDef         ATKDEFMode      { get; set; } = new AtkDef();
    public List<string>   UnifiedAutobuffOrder { get; set; } = new();

    public Profile() { }

    public Profile(string name)
    {
        Name = name;
        UserPreferences         = new ConfigProfile();
        UserPreferences.ConfigVersion = AppConfig.ConfigVersion;
        SkillSpammer            = new SkillSpammer();
        AutopotHP               = new AutopotHP(AutopotHP.ACTION_NAME_AUTOPOT_HP);
        AutopotSP               = new AutopotSP(AutopotSP.ACTION_NAME_AUTOPOT_SP);
        SkillTimer              = new SkillTimer();
        AutobuffSkill           = new AutoBuffSkill(AutoBuffSkill.ACTION_NAME_AUTOBUFFSKILL);
        AutobuffItem            = new AutoBuffItem(AutoBuffItem.ACTION_NAME_AUTOBUFFITEM);
        StatusRecovery          = new StatusRecovery();
        SongMacro               = new MacroSong();
        MacroSwitch             = new MacroSwitch(MacroSwitch.ACTION_NAME_MACRO_SWITCH, MacroSwitchKey.TOTAL_MACRO_LANES);
        ATKDEFMode              = new AtkDef();
        DebuffsRecovery         = new DebuffRecovery("DebuffsRecovery");
        TransferHelper          = new TransferHelper();
    }

    public void StartAll()
    {
        AutopotHP.Start(); AutopotSP.Start();
        SkillTimer.Start(); SkillSpammer.Start();
        StatusRecovery.Start(); AutobuffSkill.Start();
        AutobuffItem.Start(); DebuffsRecovery.Start();
        MacroSwitch.Start(); SongMacro.Start();
        TransferHelper.Start(); ATKDEFMode.Start();
    }

    public void StopAll()
    {
        AutopotHP.Stop(); AutopotSP.Stop();
        SkillTimer.Stop(); SkillSpammer.Stop();
        StatusRecovery.Stop(); AutobuffSkill.Stop();
        AutobuffItem.Stop(); DebuffsRecovery.Stop();
        MacroSwitch.Stop(); SongMacro.Stop();
        TransferHelper.Stop(); ATKDEFMode.Stop();
    }

    public static object GetByAction(dynamic obj, IAction action)
    {
        if (obj != null && obj[action.GetActionName()] != null)
            return obj[action.GetActionName()].ToString();
        return action.GetConfiguration();
    }

    public static List<string> ListAll()
    {
        var profiles = new List<string>();
        try
        {
            foreach (string file in Directory.GetFiles(AppConfig.ProfileFolder, "*.json"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    profiles.Add(fileName);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Warning($"Profile.ListAll: {ex.Message}");
        }
        return profiles.OrderBy(p => p == "Default" ? 0 : 1).ThenBy(p => p).ToList();
    }
}

public static class ProfileSingleton
{
    private static volatile Profile _profile = new("Default");
    private static readonly object  _lock    = new();

    private static T TryDeserialize<T>(dynamic rawObject, IAction action, T defaultValue)
    {
        try
        {
            string src = Profile.GetByAction(rawObject, action).ToString();
            return JsonConvert.DeserializeObject<T>(src) ?? defaultValue;
        }
        catch (Exception ex)
        {
            DebugLogger.Error(ex, $"Failed to deserialize {action.GetActionName()}");
            return defaultValue;
        }
    }

    public static void Load(string profileName)
    {
        try
        {
            string filePath = AppConfig.ProfileFolder + profileName + ".json";
            if (!File.Exists(filePath)) { Create(profileName); return; }

            // CRITICAL (Gotcha #8): Ensure we don't bleed state from the previously loaded profile.
            // DO NOT remove or swap the order of this instantiation.
            // If missing sections in JSON fall back to defaults, they must pull from a clean slate
            // instead of inheriting the ghost data of the last loaded profile.
            _profile = new Profile(profileName);

            string  json      = File.ReadAllText(filePath);
            dynamic rawObject = JsonConvert.DeserializeObject(json)!;

            if (rawObject != null)
            {
                _profile.Name            = profileName;
                if (rawObject["UnifiedAutobuffOrder"] != null)
                {
                    _profile.UnifiedAutobuffOrder = rawObject["UnifiedAutobuffOrder"].ToObject<List<string>>();
                }
                _profile.UserPreferences = TryDeserialize(rawObject, _profile.UserPreferences, _profile.UserPreferences);
                _profile.SkillSpammer    = TryDeserialize(rawObject, _profile.SkillSpammer, _profile.SkillSpammer);
                _profile.AutopotHP       = TryDeserialize(rawObject, _profile.AutopotHP, _profile.AutopotHP);
                _profile.AutopotSP       = TryDeserialize(rawObject, _profile.AutopotSP, _profile.AutopotSP);

                try
                {
                    string src = Profile.GetByAction(rawObject, _profile.StatusRecovery).ToString();
                    _profile.StatusRecovery = new StatusRecovery();
                    _profile.StatusRecovery.LoadConfiguration(src);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error(ex, "Failed to load StatusRecovery");
                    _profile.StatusRecovery = new StatusRecovery();
                }

                _profile.SkillTimer     = TryDeserialize(rawObject, _profile.SkillTimer, _profile.SkillTimer);
                _profile.AutobuffSkill  = TryDeserialize(rawObject, _profile.AutobuffSkill, _profile.AutobuffSkill);
                if (_profile.AutobuffSkill.Delay < 0) _profile.AutobuffSkill.Delay = AppConfig.AutoBuffSkillsDefaultDelay;

                try
                {
                    string abiConfig = Profile.GetByAction(rawObject, _profile.AutobuffItem).ToString();
                    if (!string.IsNullOrEmpty(abiConfig)) _profile.AutobuffItem.LoadConfiguration(abiConfig);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error(ex, "Failed to load AutobuffItem");
                }
                if (_profile.AutobuffItem.Delay < 0) _profile.AutobuffItem.Delay = AppConfig.AutoBuffItemsDefaultDelay;

                _profile.SongMacro      = TryDeserialize(rawObject, _profile.SongMacro, _profile.SongMacro);
                _profile.ATKDEFMode     = TryDeserialize(rawObject, _profile.ATKDEFMode, _profile.ATKDEFMode);
                _profile.MacroSwitch    = TryDeserialize(rawObject, _profile.MacroSwitch, _profile.MacroSwitch);
                _profile.TransferHelper = TryDeserialize(rawObject, _profile.TransferHelper, _profile.TransferHelper);
                _profile.DebuffsRecovery = TryDeserialize(rawObject, _profile.DebuffsRecovery, _profile.DebuffsRecovery);

                // Run legacy profile migrations
                ProfileMigrator.Migrate(_profile);

                // Ensure row counts match global config
                var config = ConfigGlobal.GetConfig();
                _profile.ATKDEFMode.EnsureCorrectRowCount(config.AtkDefRows);
                _profile.SongMacro.EnsureCorrectRowCount(config.SongRows);
                _profile.MacroSwitch.EnsureCorrectRowCount(config.MacroSwitchRows);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error(ex, $"Failed to load profile '{profileName}'");
        }
    }

    public static void ClearProfile(string profileName)
    {
        if (profileName != _profile.Name)
            _profile = new Profile(profileName);
    }

    public static void Create(string profileName)
    {
        string jsonFile = AppConfig.ProfileFolder + profileName + ".json";
        if (File.Exists(jsonFile)) return;
        try
        {
            if (!Directory.Exists(AppConfig.ProfileFolder))
                Directory.CreateDirectory(AppConfig.ProfileFolder);
            ClearProfile(profileName);
            
            var config = ConfigGlobal.GetConfig();
            _profile.ATKDEFMode.EnsureCorrectRowCount(config.AtkDefRows);
            _profile.SongMacro.EnsureCorrectRowCount(config.SongRows);
            _profile.MacroSwitch.EnsureCorrectRowCount(config.MacroSwitchRows);

            File.WriteAllText(jsonFile,
                JsonConvert.SerializeObject(_profile, Formatting.Indented));
        }
        catch (Exception ex) { DebugLogger.Error(ex, $"Failed to create profile '{profileName}'"); }
    }

    public static void Delete(string profileName)
    {
        try { if (profileName != "Default") File.Delete(AppConfig.ProfileFolder + profileName + ".json"); }
        catch (Exception ex) { DebugLogger.Error(ex, $"Failed to delete '{profileName}'"); }
    }

    public static void Rename(string oldName, string newName)
    {
        string oldPath = AppConfig.ProfileFolder + oldName + ".json";
        string newPath = AppConfig.ProfileFolder + newName + ".json";
        if (oldName == "Default") throw new Exception("Cannot rename the Default profile.");
        if (!File.Exists(oldPath))  throw new Exception("Profile file does not exist.");
        if (File.Exists(newPath))   throw new Exception("A profile with that name already exists.");
        File.Move(oldPath, newPath);
        if (_profile.Name == oldName) _profile.Name = newName;
    }

    public static void SetConfiguration(IAction action)
    {
        if (_profile == null) return;
        string filePath = AppConfig.ProfileFolder + _profile.Name + ".json";
        lock (_lock)
        {
            try
            {
                if (!File.Exists(filePath)) Create(_profile.Name);
                string   json    = File.ReadAllText(filePath);
                var      jObj    = !string.IsNullOrEmpty(json)
                                    ? JsonConvert.DeserializeObject<JObject>(json)!
                                    : new JObject();
                jObj[action.GetActionName()] = JToken.Parse(action.GetConfiguration());
                File.WriteAllText(filePath, JsonConvert.SerializeObject(jObj, Formatting.Indented));
            }
            catch (Exception ex) { DebugLogger.Error(ex, $"SetConfiguration failed for '{action.GetActionName()}'"); }
        }
    }

    public static void SaveUnifiedAutobuffOrder()
    {
        if (_profile == null) return;
        string filePath = AppConfig.ProfileFolder + _profile.Name + ".json";
        lock (_lock)
        {
            try
            {
                if (!File.Exists(filePath)) Create(_profile.Name);
                string json = File.ReadAllText(filePath);
                var jObj = !string.IsNullOrEmpty(json)
                            ? JsonConvert.DeserializeObject<JObject>(json)!
                            : new JObject();
                jObj["UnifiedAutobuffOrder"] = Newtonsoft.Json.Linq.JArray.FromObject(_profile.UnifiedAutobuffOrder);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(jObj, Formatting.Indented));
            }
            catch (Exception ex) { DebugLogger.Error(ex, "SaveUnifiedAutobuffOrder failed"); }
        }
    }

    public static Profile GetCurrent()
    {
        if (_profile == null) { Create("Default"); Load("Default"); }
        return _profile!;
    }

    public static void Copy(string src, string dst)
    {
        if (string.IsNullOrWhiteSpace(dst)) throw new ArgumentException("Invalid destination name.");
        string srcPath = AppConfig.ProfileFolder + src + ".json";
        string dstPath = AppConfig.ProfileFolder + dst + ".json";
        if (!File.Exists(srcPath)) throw new FileNotFoundException($"Source profile '{src}' not found.");
        if (File.Exists(dstPath))  throw new ArgumentException($"Profile '{dst}' already exists.");
        if (!Directory.Exists(AppConfig.ProfileFolder)) Directory.CreateDirectory(AppConfig.ProfileFolder);
        File.Copy(srcPath, dstPath);
    }
}
