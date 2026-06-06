using System.IO;

namespace ORTools.Worker;

public class Config
{
    public bool   DebugMode                    { get; set; } = false;
    public bool   DebugModeShowLog             { get; set; } = true;
    public bool   DisableSystray               { get; set; } = false;
    public bool   ClearAutoOffTimerOnDisable   { get; set; } = false;
    public string LastUsedProfile              { get; set; } = "Default";
    public bool   MiniMode                     { get; set; } = false;
    public int    SongRows                     { get; set; } = 4;
    public int    MacroSwitchRows              { get; set; } = 4;
    public string DefaultToggleStateKey        { get; set; } = "None";
    public bool   PauseWhenChatting            { get; set; } = true;
    public bool   PauseWhenDead                { get; set; } = false;
}

public static class ConfigGlobal
{
    private static readonly string ConfigFile = AppConfig.ConfigFile;
    private static Config? _config;

    /// <summary>True once Initialize() has completed at least once.</summary>
    public static bool IsLoaded => _config != null;

    public static void Initialize()
    {
        try
        {
            EnsureConfigFolderExists();
            EnsureConfigFileExists();
            LoadConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigGlobal] Failed to initialize: {ex.Message}");
        }
    }

    public static void EnsureConfigFolderExists()
    {
        if (!Directory.Exists(AppConfig.ConfigFolder))
            Directory.CreateDirectory(AppConfig.ConfigFolder);
    }

    private static void EnsureConfigFileExists()
    {
        if (!File.Exists(ConfigFile) || string.IsNullOrWhiteSpace(File.ReadAllText(ConfigFile)))
        {
            File.WriteAllText(ConfigFile,
                JsonConvert.SerializeObject(new Config(), Formatting.Indented));
        }
    }

    private static void LoadConfig()
    {
        try
        {
            string json = File.ReadAllText(ConfigFile);
            _config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
        }
        catch
        {
            _config = new Config();
        }
    }

    public static Config GetConfig()
    {
        if (_config == null) Initialize();
        return _config!;
    }

    public static void SaveConfig()
    {
        try
        {
            if (_config != null)
                File.WriteAllText(ConfigFile,
                    JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigGlobal] Save failed: {ex.Message}");
        }
    }
}
