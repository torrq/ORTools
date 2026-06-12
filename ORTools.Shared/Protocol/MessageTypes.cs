namespace ORTools.Shared.Protocol;

public static class MessageTypes
{
    // UI -> Worker (commands)
    public const string TurnOn = "TurnOn";
    public const string TurnOff = "TurnOff";
    public const string ConnectClient    = "ConnectClient";
    public const string DisconnectClient = "DisconnectClient";
    public const string UpdateToggleKey  = "UpdateToggleKey";
    public const string SwitchProfile = "SwitchProfile";
    public const string CreateProfile = "CreateProfile";
    public const string CopyProfile = "CopyProfile";
    public const string RenameProfile = "RenameProfile";
    public const string DeleteProfile = "DeleteProfile";
    public const string RequestProcessList = "RequestProcessList";
    public const string RequestFullState = "RequestFullState";
    public const string Shutdown = "Shutdown";

    // Autopot HP
    public const string UpdateAutopotHPSlot = "UpdateAutopotHPSlot";
    public const string UpdateAutopotHPSettings = "UpdateAutopotHPSettings";

    // Autopot SP
    public const string UpdateAutopotSPSlot = "UpdateAutopotSPSlot";
    public const string UpdateAutopotSPSettings = "UpdateAutopotSPSettings";
    
    public const string UpdateStatusRecoveryItem = "UpdateStatusRecoveryItem";
    public const string UpdateStatusRecoverySettings = "UpdateStatusRecoverySettings";

    public const string UpdateSkillTimerSlot = "UpdateSkillTimerSlot";

    public const string UpdateDebuffRecoveryItem = "UpdateDebuffRecoveryItem";
    public const string UpdateDebuffRecoverySettings = "UpdateDebuffRecoverySettings";

    // Autobuff Skill
    public const string UpdateAutobuffSkillItem = "UpdateAutobuffSkillItem";
    public const string UpdateAutobuffOrder = "UpdateAutobuffOrder";
    public const string UpdateAutobuffSkillSettings = "UpdateAutobuffSkillSettings";
    public const string AutobuffSkillConfigUpdate = "AutobuffSkillConfigUpdate";

    // Autobuff Item
    public const string UpdateAutobuffItemItem = "UpdateAutobuffItemItem";
    public const string UpdateAutobuffItemSettings = "UpdateAutobuffItemSettings";
    public const string AutobuffItemConfig = "AutobuffItemConfig";

    // Skill Spammer
    public const string UpdateSkillSpammerEntry = "UpdateSkillSpammerEntry";
    public const string UpdateSkillSpammerSettings = "UpdateSkillSpammerSettings";

    // Settings
    public const string UpdateGlobalConfig = "UpdateGlobalConfig";
    public const string UpdateProfileSettings = "UpdateProfileSettings";

    // Auto Off
    public const string UpdateAutoOffSettings = "UpdateAutoOffSettings";

    // Worker -> UI (state updates)
    public const string WorkerReady = "WorkerReady";
    public const string AppState = "AppState";
    public const string ClientState = "ClientState";
    public const string HpSp = "HpSp";
    public const string Character = "Character";
    public const string ProcessList = "ProcessList";
    public const string ProfileList = "ProfileList";
    public const string LogMessage = "LogMessage";
    public const string Error = "Error";

    // Autopot config pushes (Worker → UI)
    public const string AutopotHPConfig = "AutopotHPConfig";
    public const string AutopotSPConfig = "AutopotSPConfig";
    // AutoOff Timer
    public const string ToggleAutoOffTimer = "ToggleAutoOffTimer";
    public const string AutoOffTimerStateUpdate = "AutoOffTimerStateUpdate";
    
    // Status Recovery
    public const string StatusRecoveryConfigUpdate = "StatusRecoveryConfigUpdate";
    public const string StatusRecoveryConfig = "StatusRecoveryConfig";
    public const string SkillTimerConfig = "SkillTimerConfig";
    public const string DebuffRecoveryConfig = "DebuffRecoveryConfig";
    public const string AutobuffSkillConfig = "AutobuffSkillConfig";
    public const string AutobuffOrderConfig = "AutobuffOrderConfig";
    public const string SkillSpammerConfigUpdate = "SkillSpammerConfigUpdate";
    public const string GlobalConfigUpdate = "GlobalConfigUpdate";
    public const string ProfileSettingsUpdate = "ProfileSettingsUpdate";
    public const string AutoOffConfigUpdate = "AutoOffConfigUpdate";
    public const string TransferHelperConfigUpdate = "TransferHelperConfigUpdate";
    public const string UpdateTransferHelperCommand = "UpdateTransferHelperCommand";

    // Macro Switch
    public const string UpdateMacroSwitchTrigger = "UpdateMacroSwitchTrigger";
    public const string UpdateMacroSwitchStep = "UpdateMacroSwitchStep";
    public const string ResetMacroSwitchRow = "ResetMacroSwitchRow";
    public const string MacroSwitchConfigUpdate = "MacroSwitchConfigUpdate";

    // Macro Song
    public const string UpdateMacroSongTrigger  = "UpdateMacroSongTrigger";
    public const string UpdateMacroSongStep     = "UpdateMacroSongStep";
    public const string UpdateMacroSongAdaptation = "UpdateMacroSongAdaptation";
    public const string UpdateMacroSongInstrument = "UpdateMacroSongInstrument";
    public const string UpdateMacroSongDelay    = "UpdateMacroSongDelay";
    public const string ResetMacroSongRow       = "ResetMacroSongRow";
    public const string MacroSongConfigUpdate   = "MacroSongConfigUpdate";
}