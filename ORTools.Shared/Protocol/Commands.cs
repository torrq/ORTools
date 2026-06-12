namespace ORTools.Shared.Protocol;

// ── UI → Worker ───────────────────────────────────────────────────────────────

public sealed record TurnOnCommand : IIpcMessage
{
    public string Type => MessageTypes.TurnOn;
}

public sealed record TurnOffCommand : IIpcMessage
{
    public string Type => MessageTypes.TurnOff;
}

public sealed record ConnectClientCommand(string ProcessName) : IIpcMessage
{
    public string Type => MessageTypes.ConnectClient;
}

public sealed record DisconnectClientCommand : IIpcMessage
{
    public string Type => MessageTypes.DisconnectClient;
}

public record UpdateToggleKeyCommand(string Key) : IIpcMessage
{
    public string Type => MessageTypes.UpdateToggleKey;
}

public sealed record SwitchProfileCommand(string ProfileName) : IIpcMessage
{
    public string Type => MessageTypes.SwitchProfile;
}

public sealed record CreateProfileCommand(string ProfileName) : IIpcMessage
{
    public string Type => MessageTypes.CreateProfile;
}

public sealed record CopyProfileCommand(string SourceProfile, string NewProfileName) : IIpcMessage
{
    public string Type => MessageTypes.CopyProfile;
}

public sealed record RenameProfileCommand(string OldProfileName, string NewProfileName) : IIpcMessage
{
    public string Type => MessageTypes.RenameProfile;
}

public sealed record DeleteProfileCommand(string ProfileName) : IIpcMessage
{
    public string Type => MessageTypes.DeleteProfile;
}

public sealed record RequestProcessListCommand : IIpcMessage
{
    public string Type => MessageTypes.RequestProcessList;
}

public sealed record RequestFullStateCommand : IIpcMessage
{
    public string Type => MessageTypes.RequestFullState;
}

public sealed record ShutdownCommand : IIpcMessage
{
    public string Type => MessageTypes.Shutdown;
}

// ── Autopot HP ────────────────────────────────────────────────────────────────

public sealed record UpdateAutopotHPSlotCommand(
    int Id,
    string Key,
    int Percent,
    bool Enabled) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotHPSlot;
}

public sealed record UpdateAutopotHPSettingsCommand(
    int Delay,
    bool StopOnCriticalInjury) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotHPSettings;
}

// ── Autopot SP ────────────────────────────────────────────────────────────────

public sealed record UpdateAutopotSPSlotCommand(
    int Id,
    string Key,
    int Percent,
    bool Enabled) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotSPSlot;
}

public sealed record UpdateAutopotSPSettingsCommand(int Delay) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotSPSettings;
}

public record UpdateStatusRecoveryItemCommand(string Name, string Key) : IIpcMessage
{
    public string Type => MessageTypes.UpdateStatusRecoveryItem;
}

public record UpdateStatusRecoverySettingsCommand(int Delay) : IIpcMessage
{
    public string Type => MessageTypes.UpdateStatusRecoverySettings;
}

// ── SkillTimer ──────────────────────────────────────────────────────────────

public sealed record UpdateSkillTimerSlotCommand(
    int Id,
    string Key,
    int Delay,
    int ClickMode,
    bool AltKey,
    bool Enabled) : IIpcMessage
{
    public string Type => MessageTypes.UpdateSkillTimerSlot;
}

// ── DebuffRecovery ────────────────────────────────────────────────────────────

public sealed record UpdateDebuffRecoveryItemCommand(string StatusName, string Key) : IIpcMessage
{
    public string Type => MessageTypes.UpdateDebuffRecoveryItem;
}

public sealed record UpdateDebuffRecoverySettingsCommand(int Delay) : IIpcMessage
{
    public string Type => MessageTypes.UpdateDebuffRecoverySettings;
}

// ── AutobuffSkill ─────────────────────────────────────────────────────────────

public sealed record UpdateAutobuffSkillItemCommand(string StatusName, string Key) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutobuffSkillItem;
}

public sealed record UpdateAutobuffSkillSettingsCommand(int Delay) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutobuffSkillSettings;
}

public sealed record UpdateAutobuffOrderCommand(List<string> OrderedStatusNames) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutobuffOrder;
}

// ── AutobuffItem ──────────────────────────────────────────────────────────────

public sealed record UpdateAutobuffItemCommand(string StatusName, string Key) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutobuffItemItem;
}

public sealed record UpdateAutobuffItemSettingsCommand(int Delay) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutobuffItemSettings;
}

public sealed record UpdateSkillSpammerEntryCommand(
    string KeyName,
    bool IsChecked,
    bool IsIndeterminate) : IIpcMessage
{
    public string Type => MessageTypes.UpdateSkillSpammerEntry;
}

public sealed record UpdateSkillSpammerSettingsCommand(
    int Delay,
    bool MouseFlick,
    bool NoShift,
    bool ToggleMode,
    string ToggleModeKey) : IIpcMessage
{
    public string Type => MessageTypes.UpdateSkillSpammerSettings;
}



public sealed record ToggleAutoOffTimerCommand(bool Start) : IIpcMessage
{
    public string Type => MessageTypes.ToggleAutoOffTimer;
}

public sealed record UpdateGlobalConfigCommand(
    int SongRows,
    int MacroSwitchRows,
    string DefaultToggleStateKey,
    bool DebugMode,
    bool DebugModeShowLog,
    bool DisableSystray,
    bool StartAutoOffTimerOnEnable,
    bool ClearAutoOffTimerOnDisable,
    bool PauseWhenChatting,
    bool PauseWhenDead,
    bool ExitWithRo,
    bool AlwaysOnTop) : IIpcMessage
{
    public string Type => MessageTypes.UpdateGlobalConfig;
}

public sealed record UpdateProfileSettingsCommand(
    bool StopBuffsCity,
    bool SoundEnabled) : IIpcMessage
{
    public string Type => MessageTypes.UpdateProfileSettings;
}

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
{
    public string Type => MessageTypes.UpdateAutoOffSettings;
}

public sealed record UpdateTransferHelperCommand(
    string TransferKey) : IIpcMessage
{
    public string Type => MessageTypes.UpdateTransferHelperCommand;
}