namespace ORTools.Shared.Protocol;

// ── UI → Worker ───────────────────────────────────────────────────────────────

public sealed record TurnOnCommand : IIpcMessage
;

public sealed record TurnOffCommand : IIpcMessage
;

public sealed record ConnectClientCommand(string ProcessName) : IIpcMessage
;

public sealed record DisconnectClientCommand : IIpcMessage
;

public record UpdateToggleKeyCommand(string Key) : IIpcMessage
;

public sealed record SwitchProfileCommand(string ProfileName) : IIpcMessage
;

public sealed record CreateProfileCommand(string ProfileName) : IIpcMessage
;

public sealed record CopyProfileCommand(string SourceProfile, string NewProfileName) : IIpcMessage
;

public sealed record RenameProfileCommand(string OldProfileName, string NewProfileName) : IIpcMessage
;

public sealed record DeleteProfileCommand(string ProfileName) : IIpcMessage
;

public sealed record RequestProcessListCommand : IIpcMessage
;

public sealed record RequestFullStateCommand : IIpcMessage
;

public sealed record ShutdownCommand : IIpcMessage
;

// ── Autopot HP ────────────────────────────────────────────────────────────────

public sealed record UpdateAutopotHPSlotCommand(
    int Id,
    string Key,
    int Percent,
    bool Enabled) : IIpcMessage
;

public sealed record UpdateAutopotHPSettingsCommand(
    int Delay,
    bool StopOnCriticalInjury) : IIpcMessage
;

// ── Autopot SP ────────────────────────────────────────────────────────────────

public sealed record UpdateAutopotSPSlotCommand(
    int Id,
    string Key,
    int Percent,
    bool Enabled) : IIpcMessage
;

public sealed record UpdateAutopotSPSettingsCommand(int Delay) : IIpcMessage
;

public record UpdateStatusRecoveryItemCommand(string Name, string Key) : IIpcMessage
;

public record UpdateStatusRecoverySettingsCommand(int Delay) : IIpcMessage
;

// ── SkillTimer ──────────────────────────────────────────────────────────────

public sealed record UpdateSkillTimerSlotCommand(
    int Id,
    string Key,
    int Delay,
    int ClickMode,
    bool AltKey,
    bool Enabled) : IIpcMessage
;

// ── DebuffRecovery ────────────────────────────────────────────────────────────

public sealed record UpdateDebuffRecoveryItemCommand(string StatusName, string Key) : IIpcMessage
;

public sealed record UpdateDebuffRecoverySettingsCommand(int Delay) : IIpcMessage
;

// ── AutobuffSkill ─────────────────────────────────────────────────────────────

public sealed record UpdateAutobuffSkillItemCommand(string StatusName, string Key) : IIpcMessage
;

public sealed record UpdateAutobuffSkillSettingsCommand(int Delay) : IIpcMessage
;

public sealed record UpdateAutobuffOrderCommand(List<string> OrderedStatusNames) : IIpcMessage
;

// ── AutobuffItem ──────────────────────────────────────────────────────────────

public sealed record UpdateAutobuffItemCommand(string StatusName, string Key) : IIpcMessage
;

public sealed record UpdateAutobuffItemSettingsCommand(int Delay) : IIpcMessage
;

public sealed record UpdateSkillSpammerEntryCommand(
    string KeyName,
    bool IsChecked,
    bool IsIndeterminate) : IIpcMessage
;

public sealed record UpdateSkillSpammerSettingsCommand(
    int Delay,
    bool MouseFlick,
    bool NoShift,
    bool ToggleMode,
    string ToggleModeKey) : IIpcMessage
;



public sealed record ToggleAutoOffTimerCommand(bool Start) : IIpcMessage
;

public sealed record PauseAutoOffTimerCommand(bool Pause) : IIpcMessage
;

public sealed record UpdateGlobalConfigCommand(
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
    bool CheckForUpdatesOnStartup,
    ThemeMode Theme) : IIpcMessage
;

public sealed record UpdateStatusLoggerConfigCommand(
    bool LogToFile, int LogFrequency,
    bool LogName, bool LogLevel, bool LogJobLevel, bool LogExp,
    bool LogHp, bool LogMaxHp, bool LogSp, bool LogMaxSp,
    bool LogWeight, bool LogMaxWeight, bool LogMap, bool LogStatuses
) : IIpcMessage
;

public sealed record UpdateProfileSettingsCommand(
    bool StopBuffsCity,
    bool SoundEnabled,
    bool StartAutoOffTimerOnEnable,
    bool ClearAutoOffTimerOnDisable,
    bool PauseAutoOffTimerOnDisable,
    bool KeepDeadClientInfo) : IIpcMessage
;

public sealed record UpdateAutoOffSettingsCommand(
    bool AutoOffOverweight,
    int AutoOffOverweightMode,
    string AutoOffKey1,
    string AutoOffKey2,
    bool AutoOffKillClient,
    bool SwitchAmmo,
    string Ammo1Key,
    string Ammo2Key,
    int AutoOffTime) : IIpcMessage
;

public sealed record UpdateTransferHelperCommand(
    string TransferKey) : IIpcMessage
;

// ── Macro Switch ──────────────────────────────────────────────────────────────

public sealed record UpdateMacroSwitchTriggerCommand(int RowId, string TriggerKey) : IIpcMessage
;

public sealed record UpdateMacroSwitchStepCommand(int RowId, int StepId, string Key, int Delay, int ClickMode) : IIpcMessage
;

public sealed record ResetMacroSwitchRowCommand(int RowId) : IIpcMessage
;

// ── Macro Song ────────────────────────────────────────────────────────────────

public sealed record UpdateMacroSongTriggerCommand(int RowId, string TriggerKey) : IIpcMessage
;

public sealed record UpdateMacroSongStepCommand(int RowId, int StepId, string Key) : IIpcMessage
;

public sealed record UpdateMacroSongAdaptationCommand(int RowId, string AdaptationKey) : IIpcMessage
;

public sealed record UpdateMacroSongInstrumentCommand(int RowId, string InstrumentKey) : IIpcMessage
;

public sealed record UpdateMacroSongDelayCommand(int RowId, int Delay) : IIpcMessage
;

public sealed record ResetMacroSongRowCommand(int RowId) : IIpcMessage
;

// ── ATK x DEF ─────────────────────────────────────────────────────────────────

public sealed record UpdateAtkDefTriggerCommand(int RowId, string TriggerKey) : IIpcMessage
;

public sealed record UpdateAtkDefSpammerDelayCommand(int RowId, int Delay) : IIpcMessage
;

public sealed record UpdateAtkDefSwitchDelayCommand(int RowId, int Delay) : IIpcMessage
;

public sealed record UpdateAtkDefClickCommand(int RowId, bool Click) : IIpcMessage
;

public sealed record UpdateAtkDefEquipCommand(int RowId, string Category, string SlotName, string Key) : IIpcMessage
;

public sealed record ResetAtkDefRowCommand(int RowId) : IIpcMessage
;
