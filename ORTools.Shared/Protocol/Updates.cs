namespace ORTools.Shared.Protocol;

// ── Worker → UI ───────────────────────────────────────────────────────────────

public sealed record WorkerReadyUpdate(string Version = "1.0") : IIpcMessage
{
    public string Type => MessageTypes.WorkerReady;
}

public sealed record AppStateUpdate(bool IsOn, string ToggleKey) : IIpcMessage
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
    uint WeightCur, uint WeightMax) : IIpcMessage
{
    public string Type => MessageTypes.Character;
}

public sealed record ProcessListUpdate(List<string> Processes) : IIpcMessage
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