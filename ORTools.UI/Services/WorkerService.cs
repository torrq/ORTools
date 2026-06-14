using ORTools.Shared.Protocol;
using ORTools.Worker;

namespace ORTools.UI.Services;

/// <summary>
/// Manages the in-memory connection to the WorkerCore.
///
/// Responsibilities:
///   - Start WorkerCore on a background thread
///   - Subscribe to WorkerCore.OnBroadcast and fire typed events
///   - Route Send() commands to WorkerCore.HandleCommandAsync
///
/// All events are fired on the background thread.
/// ViewModels must marshal to the UI thread via the WPF dispatcher.
/// </summary>
public sealed class WorkerService : IDisposable
{
    // ── Connection state ──────────────────────────────────────────────────────
    public enum Status { Disconnected, Connecting, Connected }
    public Status ConnectionStatus { get; private set; } = Status.Disconnected;

    // ── Events (fired on background thread — ViewModels must dispatch) ────────
    public event Action<Status>?             ConnectionChanged;
    public event Action<AppStateUpdate>?     AppStateReceived;
    public event Action<ClientStateUpdate>?  ClientStateReceived;
    public event Action<HpSpUpdate>?         HpSpReceived;
    public event Action<CharacterUpdate>?    CharacterReceived;
    public event Action<ProcessListUpdate>?  ProcessListReceived;
    public event Action<ProfileListUpdate>?  ProfileListReceived;
    public event Action<LogMessageUpdate>?   LogMessageReceived;
    public event Action<ErrorUpdate>?        ErrorReceived;

    public event Action<AutopotHPConfigUpdate>? AutopotHPConfigReceived;
    public event Action<AutopotSPConfigUpdate>? AutopotSPConfigReceived;
    public event Action<StatusRecoveryConfigUpdate>? StatusRecoveryConfigReceived;
    public event Action<SkillTimerConfigUpdate>? SkillTimerConfigReceived;
    public event Action<DebuffRecoveryConfigUpdate>? DebuffRecoveryConfigReceived;
    public event Action<AutobuffSkillConfigUpdate>? AutobuffSkillConfigReceived;
    public event Action<AutobuffOrderConfigUpdate>? AutobuffOrderConfigReceived;
    public event Action<AutobuffItemConfigUpdate>? AutobuffItemConfigReceived;
    public event Action<SkillSpammerConfigUpdate>? SkillSpammerConfigReceived;
    public event Action<GlobalConfigUpdate>? GlobalConfigReceived;
    public event Action<ProfileSettingsUpdate>? ProfileSettingsReceived;
    public event Action<AutoOffConfigUpdate>? AutoOffConfigReceived;
    public event Action<AutoOffTimerStateUpdate>? AutoOffTimerStateReceived;
    public event Action<TransferHelperConfigUpdate>? TransferHelperConfigReceived;
    public event Action<MacroSwitchConfigUpdate>? MacroSwitchConfigReceived;
    public event Action<MacroSongConfigUpdate>? MacroSongConfigReceived;
    public event Action<AtkDefConfigUpdate>? AtkDefConfigReceived;

    // ── Private ───────────────────────────────────────────────────────────────
    private readonly WorkerCore _core;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private Task? _workerTask;

    public WorkerService()
    {
        _core = new WorkerCore();
        _core.OnBroadcast += Dispatch;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Start the worker loop in the background.
    /// </summary>
    public Task StartAsync(CancellationToken externalCt)
    {
        SetStatus(Status.Connecting);

        var ct = CancellationTokenSource.CreateLinkedTokenSource(externalCt, _lifetimeCts.Token).Token;
        
        _workerTask = Task.Run(() => _core.RunAsync(ct), ct);

        SetStatus(Status.Connected);
        
        // Request full state immediately so UI populates
        Send(new RequestFullStateCommand());

        return Task.CompletedTask;
    }

    public Task SendAsync<T>(T command) where T : IIpcMessage
    {
        if (ConnectionStatus != Status.Connected) return Task.CompletedTask;
        return _core.HandleCommandAsync(command);
    }

    public void Send<T>(T command) where T : IIpcMessage
    {
        _ = SendAsync(command);
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _core.OnBroadcast -= Dispatch;
        _core.HandleTurnOff().Wait();
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private void Dispatch(IIpcMessage env)
    {
        switch (env.Type)
        {
            case MessageTypes.WorkerReady:
                Console.WriteLine($"[WorkerService] Worker ready.");
                break;
            case MessageTypes.AppState:       AppStateReceived?.Invoke((AppStateUpdate)env); break;
            case MessageTypes.ClientState:    ClientStateReceived?.Invoke((ClientStateUpdate)env); break;
            case MessageTypes.HpSp:           HpSpReceived?.Invoke((HpSpUpdate)env); break;
            case MessageTypes.Character:      CharacterReceived?.Invoke((CharacterUpdate)env); break;
            case MessageTypes.ProcessList:    ProcessListReceived?.Invoke((ProcessListUpdate)env); break;
            case MessageTypes.ProfileList:    ProfileListReceived?.Invoke((ProfileListUpdate)env); break;
            case MessageTypes.LogMessage:     LogMessageReceived?.Invoke((LogMessageUpdate)env); break;
            case MessageTypes.Error:          ErrorReceived?.Invoke((ErrorUpdate)env); break;

            case MessageTypes.AutopotHPConfig: AutopotHPConfigReceived?.Invoke((AutopotHPConfigUpdate)env); break;
            case MessageTypes.AutopotSPConfig: AutopotSPConfigReceived?.Invoke((AutopotSPConfigUpdate)env); break;
            case MessageTypes.StatusRecoveryConfig: StatusRecoveryConfigReceived?.Invoke((StatusRecoveryConfigUpdate)env); break;
            case MessageTypes.SkillTimerConfig: SkillTimerConfigReceived?.Invoke((SkillTimerConfigUpdate)env); break;
            case MessageTypes.DebuffRecoveryConfig: DebuffRecoveryConfigReceived?.Invoke((DebuffRecoveryConfigUpdate)env); break;
            case MessageTypes.AutobuffSkillConfig: AutobuffSkillConfigReceived?.Invoke((AutobuffSkillConfigUpdate)env); break;
            case MessageTypes.AutobuffOrderConfig: AutobuffOrderConfigReceived?.Invoke((AutobuffOrderConfigUpdate)env); break;
            case MessageTypes.AutobuffItemConfig: AutobuffItemConfigReceived?.Invoke((AutobuffItemConfigUpdate)env); break;
            
            case MessageTypes.SkillSpammerConfigUpdate: SkillSpammerConfigReceived?.Invoke((SkillSpammerConfigUpdate)env); break;
            case MessageTypes.GlobalConfigUpdate: GlobalConfigReceived?.Invoke((GlobalConfigUpdate)env); break;
            case MessageTypes.ProfileSettingsUpdate: ProfileSettingsReceived?.Invoke((ProfileSettingsUpdate)env); break;
            case MessageTypes.AutoOffConfigUpdate: AutoOffConfigReceived?.Invoke((AutoOffConfigUpdate)env); break;
            case MessageTypes.AutoOffTimerStateUpdate: AutoOffTimerStateReceived?.Invoke((AutoOffTimerStateUpdate)env); break;
            case MessageTypes.TransferHelperConfigUpdate: TransferHelperConfigReceived?.Invoke((TransferHelperConfigUpdate)env); break;
            case MessageTypes.MacroSwitchConfigUpdate: MacroSwitchConfigReceived?.Invoke((MacroSwitchConfigUpdate)env); break;
            case MessageTypes.MacroSongConfigUpdate: MacroSongConfigReceived?.Invoke((MacroSongConfigUpdate)env); break;
            case MessageTypes.AtkDefConfigUpdate: AtkDefConfigReceived?.Invoke((AtkDefConfigUpdate)env); break;

            default:
                Console.WriteLine($"[WorkerService] Unknown update: {env.Type}");
                break;
        }
    }

    private void SetStatus(Status s)
    {
        ConnectionStatus = s;
        ConnectionChanged?.Invoke(s);
    }
}
