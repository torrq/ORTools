namespace ORTools.Shared.Protocol;

// ── Worker → UI ───────────────────────────────────────────────────────────────
// All update records live here. Worker pushes these whenever state changes;
// UI never needs to poll.

/// <summary>First message sent after the pipe connection is accepted.</summary>
public sealed record WorkerReadyUpdate(string Version = "1.0") : IIpcMessage
{
    public string Type => MessageTypes.WorkerReady;
}

/// <summary>Application on/off state changed.</summary>
public sealed record AppStateUpdate(bool IsOn) : IIpcMessage
{
    public string Type => MessageTypes.AppState;
}

/// <summary>RO client connected or disconnected.</summary>
public sealed record ClientStateUpdate(bool Connected, string? ProcessName) : IIpcMessage
{
    public string Type => MessageTypes.ClientState;
}

/// <summary>
/// Current HP/SP values. Pushed continuously while a client is connected.
/// Worker should push this at the same rate as the autopot thread reads (every ~50ms).
/// </summary>
public sealed record HpSpUpdate(
    uint CurrentHp,
    uint MaxHp,
    uint CurrentSp,
    uint MaxSp) : IIpcMessage
{
    public string Type => MessageTypes.HpSp;
}

/// <summary>
/// Character metadata. Pushed on login and then roughly every 1–2 seconds
/// (map changes, level-ups, exp updates).
/// </summary>
public sealed record CharacterUpdate(
    string Name,
    string Map,
    uint   Level,
    uint   JobLevel,
    uint   JobId,
    uint   Exp,
    uint   ExpToLevel,
    uint   WeightCur,
    uint   WeightMax) : IIpcMessage
{
    public string Type => MessageTypes.Character;
}

/// <summary>Updated list of RO processes running on the system.</summary>
public sealed record ProcessListUpdate(List<string> Processes) : IIpcMessage
{
    public string Type => MessageTypes.ProcessList;
}

/// <summary>Updated list of profiles and which one is active.</summary>
public sealed record ProfileListUpdate(
    List<string> Profiles,
    string       CurrentProfile) : IIpcMessage
{
    public string Type => MessageTypes.ProfileList;
}

/// <summary>
/// Forwarded DebugLogger entry. UI appends to its debug console.
/// Level: "Debug", "Info", "Warn", "Error"
/// </summary>
public sealed record LogMessageUpdate(string Level, string Message) : IIpcMessage
{
    public string Type => MessageTypes.LogMessage;
}

/// <summary>Non-fatal error the UI should surface to the user.</summary>
public sealed record ErrorUpdate(string Message) : IIpcMessage
{
    public string Type => MessageTypes.Error;
}

// ── Autopot config (Worker → UI) ──────────────────────────────────────────────

/// <summary>Full HP autopot configuration — sent on connect and after any slot change.</summary>
public sealed record AutopotHPConfigUpdate(
    List<AutopotSlotData> Slots,
    int Delay,
    bool StopOnCriticalInjury) : IIpcMessage
{
    public string Type => MessageTypes.AutopotHPConfig;
}

/// <summary>Full SP autopot configuration.</summary>
public sealed record AutopotSPConfigUpdate(
    List<AutopotSlotData> Slots,
    int Delay) : IIpcMessage
{
    public string Type => MessageTypes.AutopotSPConfig;
}