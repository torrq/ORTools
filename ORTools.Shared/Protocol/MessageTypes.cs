namespace ORTools.Shared.Protocol;

public static class MessageTypes
{
    // ── UI → Worker (commands) ────────────────────────────────────────────────
    public const string TurnOn = "TurnOn";
    public const string TurnOff = "TurnOff";
    public const string ConnectClient    = "ConnectClient";
    public const string DisconnectClient = "DisconnectClient";
    public const string UpdateToggleKey  = "UpdateToggleKey";
    public const string SwitchProfile = "SwitchProfile";
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

    // ── Worker → UI (state updates) ───────────────────────────────────────────
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
    public const string StatusRecoveryConfig = "StatusRecoveryConfig";
}