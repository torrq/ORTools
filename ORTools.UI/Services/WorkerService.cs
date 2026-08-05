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
    public event Action<StatusLoggerConfigUpdate>? StatusLoggerConfigReceived;
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
    private CancellationTokenSource? _linkedCts;
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

        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt, _lifetimeCts.Token);
        var ct = _linkedCts.Token;
        
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
        _ = SendAsync(command).ContinueWith(t => 
        {
            if (t.IsFaulted && t.Exception != null)
            {
                Console.WriteLine($"[WorkerService] Command {command.GetType().Name} failed: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
            }
        });
    }

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _linkedCts?.Dispose();
        _core.OnBroadcast -= Dispatch;
        _core.HandleTurnOff().Wait();
        _core.Dispose();
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private void Dispatch(IIpcMessage env)
    {
        switch (env)
        {
            case WorkerReadyUpdate update:
                Console.WriteLine($"[WorkerService] Worker ready.");
                break;
            case AppStateUpdate update:
                AppStateReceived?.Invoke(update); break;
            case ClientStateUpdate update:
                ClientStateReceived?.Invoke(update); break;
            case HpSpUpdate update:
                HpSpReceived?.Invoke(update); break;
            case CharacterUpdate update:
                CharacterReceived?.Invoke(update); break;
            case ProcessListUpdate update:
                ProcessListReceived?.Invoke(update); break;
            case ProfileListUpdate update:
                ProfileListReceived?.Invoke(update); break;
            case LogMessageUpdate update:
                LogMessageReceived?.Invoke(update); break;
            case ErrorUpdate update:
                ErrorReceived?.Invoke(update); break;

            case AutopotHPConfigUpdate update:
                AutopotHPConfigReceived?.Invoke(update); break;
            case AutopotSPConfigUpdate update:
                AutopotSPConfigReceived?.Invoke(update); break;
            case StatusRecoveryConfigUpdate update:
                StatusRecoveryConfigReceived?.Invoke(update); break;
            case SkillTimerConfigUpdate update:
                SkillTimerConfigReceived?.Invoke(update); break;
            case DebuffRecoveryConfigUpdate update:
                DebuffRecoveryConfigReceived?.Invoke(update); break;
            case AutobuffSkillConfigUpdate update:
                AutobuffSkillConfigReceived?.Invoke(update); break;
            case AutobuffOrderConfigUpdate update:
                AutobuffOrderConfigReceived?.Invoke(update); break;
            case AutobuffItemConfigUpdate update:
                AutobuffItemConfigReceived?.Invoke(update); break;
            
            case SkillSpammerConfigUpdate update:
                SkillSpammerConfigReceived?.Invoke(update); break;
            case GlobalConfigUpdate update:
                GlobalConfigReceived?.Invoke(update); break;
            case StatusLoggerConfigUpdate update:
                StatusLoggerConfigReceived?.Invoke(update); break;
            case ProfileSettingsUpdate update:
                ProfileSettingsReceived?.Invoke(update); break;
            case AutoOffConfigUpdate update:
                AutoOffConfigReceived?.Invoke(update); break;
            case AutoOffTimerStateUpdate update:
                AutoOffTimerStateReceived?.Invoke(update); break;
            case TransferHelperConfigUpdate update:
                TransferHelperConfigReceived?.Invoke(update); break;
            case MacroSwitchConfigUpdate update:
                MacroSwitchConfigReceived?.Invoke(update); break;
            case MacroSongConfigUpdate update:
                MacroSongConfigReceived?.Invoke(update); break;
            case AtkDefConfigUpdate update:
                AtkDefConfigReceived?.Invoke(update); break;

            default:
                Console.WriteLine($"[WorkerService] Unknown update: {env.GetType().Name}");
                break;
        }
    }

    private void SetStatus(Status s)
    {
        ConnectionStatus = s;
        ConnectionChanged?.Invoke(s);
    }
}
