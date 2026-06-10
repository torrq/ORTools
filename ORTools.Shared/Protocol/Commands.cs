namespace ORTools.Shared.Protocol;

// ── UI → Worker ───────────────────────────────────────────────────────────────
// All command records live here. Add new commands as new records at the bottom.

public sealed record TurnOnCommand : IIpcMessage
{
    public string Type => MessageTypes.TurnOn;
}

public sealed record TurnOffCommand : IIpcMessage
{
    public string Type => MessageTypes.TurnOff;
}

/// <summary>Connect the Worker to a specific RO process by name.</summary>
public sealed record ConnectClientCommand(string ProcessName) : IIpcMessage
{
    public string Type => MessageTypes.ConnectClient;
}

public sealed record DisconnectClientCommand : IIpcMessage
{
    public string Type => MessageTypes.DisconnectClient;
}

/// <summary>Switch the active profile. Worker loads the profile and broadcasts a ProfileList update.</summary>
public sealed record SwitchProfileCommand(string ProfileName) : IIpcMessage
{
    public string Type => MessageTypes.SwitchProfile;
}

/// <summary>Ask the Worker to scan for RO processes and return a ProcessList update.</summary>
public sealed record RequestProcessListCommand : IIpcMessage
{
    public string Type => MessageTypes.RequestProcessList;
}

/// <summary>
/// Sent by the UI immediately after connecting to sync all current state.
/// Worker responds with AppState, ClientState, ProfileList, ProcessList.
/// </summary>
public sealed record RequestFullStateCommand : IIpcMessage
{
    public string Type => MessageTypes.RequestFullState;
}

/// <summary>Graceful shutdown — Worker saves state and exits.</summary>
public sealed record ShutdownCommand : IIpcMessage
{
    public string Type => MessageTypes.Shutdown;
}

// ── Autopot HP ────────────────────────────────────────────────────────────────

/// <summary>User changed one HP slot (key, threshold, or enabled toggle).</summary>
public sealed record UpdateAutopotHPSlotCommand(
    int Id,
    string Key,
    int Percent,
    bool Enabled) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotHPSlot;
}

/// <summary>User changed HP autopot global settings (delay or critical wound flag).</summary>
public sealed record UpdateAutopotHPSettingsCommand(
    int Delay,
    bool StopOnCriticalInjury) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotHPSettings;
}

/// <summary>Persist the current HP slot order after a drag reorder.</summary>
public sealed record ReorderAutopotHPCommand(List<int> SlotOrder) : IIpcMessage
{
    public string Type => MessageTypes.ReorderAutopotHP;
}

// ── Autopot SP ────────────────────────────────────────────────────────────────

/// <summary>User changed one SP slot.</summary>
public sealed record UpdateAutopotSPSlotCommand(
    int Id,
    string Key,
    int Percent,
    bool Enabled) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotSPSlot;
}

/// <summary>User changed SP autopot delay.</summary>
public sealed record UpdateAutopotSPSettingsCommand(int Delay) : IIpcMessage
{
    public string Type => MessageTypes.UpdateAutopotSPSettings;
}

/// <summary>Persist the current SP slot order after a drag reorder.</summary>
public sealed record ReorderAutopotSPCommand(List<int> SlotOrder) : IIpcMessage
{
    public string Type => MessageTypes.ReorderAutopotSP;
}
