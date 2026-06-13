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
    public static string preReleaseTag = "ALPHA";

    // ── File paths ────────────────────────────────────────────────────────────
    public static string ProfileFolder = "Profiles\\";
    public static string ConfigFolder  = "Config\\";
    public static string ConfigFile    = ConfigFolder + "config.json";
    public static string ServersFile   = ConfigFolder + "servers.json";
    public static string CitiesFile    = ConfigFolder + "cities.json";
    public static string DebugLogFile  = "debug.log";

    // ── Feature limits ────────────────────────────────────────────────────────
    public const int ATKDEFLanes = 2;

    // ── String constants ──────────────────────────────────────────────────────
    public const string TEXT_NONE = "None";

    // ── Debug ─────────────────────────────────────────────────────────────────
    public const string INFO    = "I";
    public const string WARNING = "W";
    public const string ERROR   = "E";
    public const string DEBUG   = "D";
    public const string STATUS  = "S";
    public static bool DebugMode => ConfigGlobal.IsLoaded && ConfigGlobal.GetConfig().DebugMode;

    // ── Default delays (ms) ───────────────────────────────────────────────────
    public static int     AutoPotDefaultDelay         = 50;
    public static int     YggDefaultDelay             = 50;
    public static int     SkillSpammerDefaultDelay    = 50;
    public static int     AutoBuffSkillsDefaultDelay  = 50;
    public static int     AutoBuffItemsDefaultDelay   = 50;
    public static int     ATKDEFSpammerDefaultDelay   = 100;
    public static int     ATKDEFSwitchDefaultDelay    = 100;
    public static int     MacroDefaultDelay           = 100;
    public static int     SkillTimerDefaultDelay      = 1000;
    public static decimal DefaultMinimumDelay         = 0;

    // ── Memory addresses ──────────────────────────────────────────────────────
    public static int WeightAddress
    {
        get { switch (ServerMode) { case 0: return 0xE8BB28; case 1: return 0x10D94B0; default: return 0; } }
    }
    public static int MaxWeightAddress
    {
        get { switch (ServerMode) { case 0: return 0xE8BB24; case 1: return 0x10D94AC; default: return 0; } }
    }
    public static int TextInputActiveAddress
    {
        get { switch (ServerMode) { case 0: return 0xCE6B40; case 1: return 0xF33B48; default: return 0; } }
    }

    // ── Server configs ────────────────────────────────────────────────────────
    public static List<dynamic> DefaultServers
    {
        get
        {
            switch (ServerMode)
            {
                case 0:
                    return new List<dynamic> { new {
                        name = "OsRO Midrate", description = "OsRO Midrate",
                        hpAddress = "00E8F434", nameAddress = "00E91C00",
                        mapAddress = "00E8ABD4", jobAddress = "00E8BA54",
                        onlineAddress = "00E884B1"
                    }};
                case 1:
                    return new List<dynamic> { new {
                        name = "OSRO", description = "OsRO Highrate",
                        hpAddress = "010DCE10", nameAddress = "010DF5D8",
                        mapAddress = "010D856C", jobAddress = "010D93D8",
                        onlineAddress = "010A2FB0"
                    }};
                default:
                    throw new InvalidOperationException($"Unsupported ServerMode: {ServerMode}");
            }
        }
    }

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
    public static string GithubLink     = "https://github.com/torrq/4RTools-OSRO/releases";
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
