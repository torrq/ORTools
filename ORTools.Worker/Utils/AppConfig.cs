namespace ORTools.Worker;

public static class AppConfig
{
    // ── General ───────────────────────────────────────────────────────────────
    public static string  Name           = "OSRO Tools";
    public static string  Version        => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public static decimal ConfigVersion  = 1;
#if SERVERMODE_HR
    public static int ServerMode = 1;   // 0 = MR, 1 = HR
#else
    public static int ServerMode = 0;   // 0 = MR, 1 = HR
#endif

    public static bool IsMidRate  => ServerMode == 0;
    public static bool IsHighRate => ServerMode == 1;
    public static bool SupportsFishing => IsMidRate;

    public static bool   preRelease    = true;
    public static string preReleaseTag = "BETA-5";

    // ── File paths ────────────────────────────────────────────────────────────
    public static string ProfileFolder = "Profiles\\";
    public static string ConfigFolder  = "Config\\";
    public static string ConfigFile    = ConfigFolder + "config.json";

    public const double MinDebugViewHeight = 10;
    public const double MaxDebugViewHeight = 1200;
    public static string ServersFile   = ConfigFolder + "servers.json";
    public static string CitiesFile    = ConfigFolder + "cities.json";
    public static string DebugLogFile  = "debug.log";
    public static string ExpLogFile    => IsHighRate ? "osro_tools_hr.log" : "osro_tools_mr.log";

    // ── Feature limits ────────────────────────────────────────────────────────
    public const int ATKDEFLanes = 2;
    public const int AutoPotRows = 10;

    // ── String constants ──────────────────────────────────────────────────────
    public const string TEXT_NONE = "None";

    // ── Debug ─────────────────────────────────────────────────────────────────
    public const string INFO    = "I";
    public const string WARNING = "W";
    public const string ERROR   = "E";
    public const string DEBUG   = "D";
    public const string STATUS  = "S";
    public static bool DebugMode => ConfigGlobal.IsLoaded && ConfigGlobal.GetConfig().DebugMode;
    public static bool DebugClientLog => ConfigGlobal.IsLoaded && ConfigGlobal.GetConfig().DebugClientLog;

    // ── Default delays (ms) ───────────────────────────────────────────────────
    public static int     AutoPotDefaultDelay         = 50;
    public static int     YggDefaultDelay             = 50;
    public static int     SkillSpammerDefaultDelay    = 50;
    public static int     AutoBuffSkillsDefaultDelay  = 2000;
    public static int     AutoBuffItemsDefaultDelay   = 5000;
    public static int     ATKDEFSpammerDefaultDelay   = 100;
    public static int     ATKDEFSwitchDefaultDelay    = 100;
    public static int     MacroDefaultDelay           = 100;
    public static int     SkillTimerDefaultDelay      = 1000;
    public static decimal DefaultMinimumDelay         = 0;

    // ── Memory addresses ──────────────────────────────────────────────────────


    // ── Server configs ────────────────────────────────────────────────────────


    // ── Cities ────────────────────────────────────────────────────────────────
    public static List<string> DefaultCities => new List<string>
    {
        "prontera","morocc","geffen","payon","alberta","izlude","aldebaran","xmas",
        "comodo","yuno","amatsu","gonryun","umbala","niflheim","louyang","jawaii",
        "ayothaya","einbroch","lighthalzen","einbech","hugel","rachel","veins",
        "moscovia","mid_camp","munak","splendide","brasilis","dicastes01","mora",
        "dewata","malangdo","malaya","eclage","marketplace","mainhall","quiz_00",
        "e_tower","prt_in2","mall"
    };

    // ── URLs ──────────────────────────────────────────────────────────────────
    public static string GithubLink     = "https://github.com/torrq/ORTools";
    public static string WebsiteMR      = "https://osro.mr";
    public static string WebsiteHR      = "https://osro.gg";
    public static string DiscordLinkMR  = "https://discord.com/invite/osro2";
    public static string DiscordLinkHR  = "https://discord.com/invite/osro";
    public static string WikiLinkMR     = "https://wiki.osro.mr";
    public static string WikiLinkHR     = "https://wiki.osro.gg";

    // ── Helpers ───────────────────────────────────────────────────────────────
    public static string GetRateTag() => ServerMode == 0 ? "MR" : "HR";
    public static string DefaultToggleStateKey = "None";
}
