namespace ORTools.Shared.Protocol;

// ── Worker → UI ───────────────────────────────────────────────────────────────

public sealed record WorkerReadyUpdate(string Version = "1.0") : IIpcMessage
{
    public string Type => MessageTypes.WorkerReady;
}

public sealed record AppStateUpdate(bool IsOn, string ToggleKey, string AppTitle, int ServerMode = 1) : IIpcMessage
{
    public string Type => MessageTypes.AppState;
}

public sealed record ClientStateUpdate(bool Connected, string? ProcessName) : IIpcMessage
{
    public string Type => MessageTypes.ClientState;
}

public sealed record HpSpUpdate(
    uint CurrentHp, uint MaxHp,
    uint CurrentSp, uint MaxSp) : IIpcMessage
{
    public string Type => MessageTypes.HpSp;
}

public sealed record CharacterUpdate(
    string Name, string Map,
    uint Level, uint JobLevel, uint JobId,
    uint Exp, uint ExpToLevel,
    uint WeightCur, uint WeightMax,
    string ActiveStatuses) : IIpcMessage
{
    public string Type => MessageTypes.Character;
}

public sealed record ProcessEntry(string Id, string DisplayName);

public sealed record ProcessListUpdate(List<ProcessEntry> Processes) : IIpcMessage
{
    public string Type => MessageTypes.ProcessList;
}

public sealed record ProfileListUpdate(
    List<string> Profiles,
    string CurrentProfile) : IIpcMessage
{
    public string Type => MessageTypes.ProfileList;
}

public sealed record LogMessageUpdate(string Level, string Message) : IIpcMessage
{
    public string Type => MessageTypes.LogMessage;
}

public sealed record ErrorUpdate(string Message) : IIpcMessage
{
    public string Type => MessageTypes.Error;
}

// ── Autopot config (Worker → UI) ──────────────────────────────────────────────

public sealed record AutopotHPConfigUpdate(
    List<AutopotSlotData> Slots,
    int Delay,
    bool StopOnCriticalInjury) : IIpcMessage
{
    public string Type => MessageTypes.AutopotHPConfig;
}

public sealed record AutopotSPConfigUpdate(
    List<AutopotSlotData> Slots,
    int Delay) : IIpcMessage
{
    public string Type => MessageTypes.AutopotSPConfig;
}

public sealed record StatusRecoveryItemData(string Name, string Key);

public sealed record StatusRecoveryConfigUpdate(
    List<StatusRecoveryItemData> Items,
    int Delay) : IIpcMessage
{
    public string Type => MessageTypes.StatusRecoveryConfig;
}

public sealed record SkillTimerSlotData(
    int Id,
    string Key,
    int Delay,
    int ClickMode,
    bool AltKey,
    bool Enabled);

public sealed record SkillTimerConfigUpdate(
    List<SkillTimerSlotData> Slots) : IIpcMessage
{
    public string Type => MessageTypes.SkillTimerConfig;
}

public sealed record DebuffRecoveryItemData(string Name, string Key, string IconName);

public sealed record DebuffRecoveryConfigUpdate(
    List<DebuffRecoveryItemData> Items,
    int Delay) : IIpcMessage
{
    public string Type => MessageTypes.DebuffRecoveryConfig;
}

public sealed record AutobuffSkillItemData(string Name, string DisplayName, string Key, string IconName);

public sealed record AutobuffSkillGroupData(string GroupName, List<AutobuffSkillItemData> Items);

public sealed record AutobuffSkillConfigUpdate(
    List<AutobuffSkillGroupData> Groups,
    int Delay) : IIpcMessage
{
    public string Type => MessageTypes.AutobuffSkillConfig;
}

public sealed record AutobuffOrderItemData(string Name, string DisplayName, string Key, string ItemType, string IconName);

public sealed record AutobuffOrderConfigUpdate(
    List<AutobuffOrderItemData> Items) : IIpcMessage
{
    public string Type => MessageTypes.AutobuffOrderConfig;
}

public sealed record AutobuffItemItemData(string Name, string DisplayName, string Key, string IconName);

public sealed record AutobuffItemGroupData(string GroupName, List<AutobuffItemItemData> Items);

public sealed record AutobuffItemConfigUpdate(
    List<AutobuffItemGroupData> Groups,
    int Delay) : IIpcMessage
{
    public string Type => MessageTypes.AutobuffItemConfig;
}

public sealed record SkillSpammerKeyData(string KeyName, bool ClickActive, bool IsIndeterminate);

public sealed record SkillSpammerConfigUpdate(
    List<SkillSpammerKeyData> Entries,
    int SpammerDelay,
    bool MouseFlick,
    bool NoShift,
    bool ToggleMode,
    string ToggleModeKey) : IIpcMessage
{
    public string Type => MessageTypes.SkillSpammerConfigUpdate;
}

public sealed record GlobalConfigUpdate(
    int SongRows,
    int MacroSwitchRows,
    int AtkDefRows,
    string DefaultToggleStateKey,
    bool DebugMode,
    bool DebugView,
    double DebugViewHeight,
    bool DebugClientLog,
    bool DisableSystray,
    bool MinimizeToSystray,
    bool CloseToSystray,
    bool PauseWhenChatting,
    bool PauseWhenDead,
    bool ExitWithRo,
    bool AlwaysOnTop,
    bool AllowResizingWindow,
    bool ShowExpPerHour,
    ThemeMode Theme) : IIpcMessage
{
    public string Type => MessageTypes.GlobalConfigUpdate;
}

public sealed record StatusLoggerConfigUpdate(
    bool LogToFile, int LogFrequency, string LogFileName,
    bool LogName, bool LogLevel, bool LogJobLevel, bool LogExp,
    bool LogHp, bool LogMaxHp, bool LogSp, bool LogMaxSp,
    bool LogWeight, bool LogMaxWeight, bool LogMap, bool LogStatuses
) : IIpcMessage
{
    public string Type => MessageTypes.StatusLoggerConfigUpdate;
}

public sealed record ProfileSettingsUpdate(
    bool StopBuffsCity,
    bool SoundEnabled,
    bool StartAutoOffTimerOnEnable,
    bool ClearAutoOffTimerOnDisable,
    bool KeepDeadClientInfo) : IIpcMessage
{
    public string Type => MessageTypes.ProfileSettingsUpdate;
}

public sealed record AutoOffConfigUpdate(
    bool AutoOffOverweight,
    int AutoOffOverweightMode,
    string AutoOffKey1,
    string AutoOffKey2,
    bool AutoOffKillClient,
    bool SwitchAmmo,
    string Ammo1Key,
    string Ammo2Key,
    int AutoOffTime) : IIpcMessage
{
    public string Type => MessageTypes.AutoOffConfigUpdate;
}

public sealed record AutoOffTimerStateUpdate(
    bool IsRunning,
    bool IsPaused,
    int SelectedMinutes,
    int RemainingSeconds) : IIpcMessage
{
    public string Type => MessageTypes.AutoOffTimerStateUpdate;
}

public sealed record TransferHelperConfigUpdate(
    string TransferKey) : IIpcMessage
{
    public string Type => MessageTypes.TransferHelperConfigUpdate;
}

// ── Macro Switch ──────────────────────────────────────────────────────────────

public record MacroSwitchStepData(
    string Key,
    int Delay,
    int ClickMode);

public record MacroSwitchChainData(
    int Id,
    string TriggerKey,
    List<MacroSwitchStepData> Steps);

public sealed record MacroSwitchConfigUpdate(
    List<MacroSwitchChainData> Chains) : IIpcMessage
{
    public string Type => MessageTypes.MacroSwitchConfigUpdate;
}

// ── Macro Song ────────────────────────────────────────────────────────────────

public record MacroSongRowData(
    int Id,
    string TriggerKey,
    string AdaptationKey,
    string InstrumentKey,
    int Delay,
    List<string> Sequence);

public sealed record MacroSongConfigUpdate(
    List<MacroSongRowData> Rows) : IIpcMessage
{
    public string Type => MessageTypes.MacroSongConfigUpdate;
}

// ── ATK x DEF ─────────────────────────────────────────────────────────────────

public record AtkDefRowData(
    int Id,
    string TriggerKey,
    int SpammerDelay,
    int SwitchDelay,
    bool Click,
    Dictionary<string, string> AtkKeys,
    Dictionary<string, string> DefKeys);

public sealed record AtkDefConfigUpdate(
    List<AtkDefRowData> Rows) : IIpcMessage
{
    public string Type => MessageTypes.AtkDefConfigUpdate;
}