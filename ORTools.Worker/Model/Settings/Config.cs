using System.IO;

namespace ORTools.Worker;

public class Config
{
    public bool   DebugMode                    { get; set; } = false;
    public bool   DebugView                    { get; set; } = false;
    public double DebugViewHeight              { get; set; } = 200;
    public bool   DebugClientLog               { get; set; } = false;
    public bool   DisableSystray               { get; set; } = false;
    public bool   MinimizeToSystray            { get; set; } = false;
    public bool   CloseToSystray               { get; set; } = false;
    public string LastUsedProfile              { get; set; } = "Default";
    public bool   MiniMode                     { get; set; } = false;
    public int    SongRows                     { get; set; } = 4;
    public int    MacroSwitchRows              { get; set; } = 4;
    public int    AtkDefRows                   { get; set; } = 2;
    public string DefaultToggleStateKey        { get; set; } = "None";
    public bool   PauseWhenChatting            { get; set; } = false;
    public bool   PauseWhenDead                { get; set; } = false;
    public bool   ExitWithRo                   { get; set; } = false;
    public bool   AlwaysOnTop                  { get; set; } = false;
    public bool   AllowResizingWindow          { get; set; } = false;
    public bool   ShowExpPerHour               { get; set; } = false;
    public Shared.Protocol.ThemeMode Theme     { get; set; } = Shared.Protocol.ThemeMode.BlueLight;
    
    public StatusLoggerConfig StatusLogger { get; set; } = new();
}

public class StatusLoggerConfig
{
    public bool LogToFile { get; set; } = false;
    public int LogFrequency { get; set; } = 10;
    
    public bool LogName { get; set; } = true;
    public bool LogLevel { get; set; } = true;
    public bool LogJobLevel { get; set; } = true;
    public bool LogExp { get; set; } = true;
    public bool LogHp { get; set; } = true;
    public bool LogMaxHp { get; set; } = true;
    public bool LogSp { get; set; } = true;
    public bool LogMaxSp { get; set; } = true;
    public bool LogWeight { get; set; } = true;
    public bool LogMaxWeight { get; set; } = true;
    public bool LogMap { get; set; } = true;
    public bool LogStatuses { get; set; } = true;
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
        finally
        {
            if (_config == null) _config = new Config();
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
