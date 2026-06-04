namespace ORTools.Shared.Protocol;

/// <summary>
/// String constants used as the "t" discriminator in every IpcEnvelope.
/// Both sides must use these identically.
/// </summary>
public static class MessageTypes
{
    // ── UI → Worker (commands) ────────────────────────────────────────────────
    public const string TurnOn             = "TurnOn";
    public const string TurnOff            = "TurnOff";
    public const string ConnectClient      = "ConnectClient";
    public const string DisconnectClient   = "DisconnectClient";
    public const string SwitchProfile      = "SwitchProfile";
    public const string RequestProcessList = "RequestProcessList";
    public const string RequestFullState   = "RequestFullState";
    public const string Shutdown           = "Shutdown";

    // ── Worker → UI (state updates) ───────────────────────────────────────────
    public const string WorkerReady  = "WorkerReady";
    public const string AppState     = "AppState";
    public const string ClientState  = "ClientState";
    public const string HpSp         = "HpSp";
    public const string Character    = "Character";
    public const string ProcessList  = "ProcessList";
    public const string ProfileList  = "ProfileList";
    public const string LogMessage   = "LogMessage";
    public const string Error        = "Error";
}
