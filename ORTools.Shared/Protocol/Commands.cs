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