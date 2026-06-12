using System.IO;
using System.IO.Pipes;
using ORTools.Shared.Protocol;

namespace ORTools.UI.Services;

/// <summary>
/// Manages the named pipe connection from the UI to the Worker process.
///
/// Responsibilities:
///   - Connect (and auto-reconnect) to ORTools.Worker's named pipe
///   - Launch the Worker via WorkerLauncher if it isn't running
///   - Parse incoming IpcEnvelope messages and fire typed events
///   - Provide SendAsync / Send for outbound commands
///
/// All events are fired on the background thread that reads the pipe.
/// ViewModels must marshal to the UI thread via the WPF dispatcher.
/// </summary>
public sealed class WorkerService : IDisposable
{
    public const string PipeName = "ORTools-Worker";

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

    // ── Private ───────────────────────────────────────────────────────────────
    private NamedPipeClientStream? _pipe;
    private StreamWriter?          _writer;
    private readonly SemaphoreSlim _writeLock       = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Begin the connect → read → reconnect loop. Call once from App startup.
    /// Never throws; all errors are logged.
    /// </summary>
    public async Task StartAsync(CancellationToken externalCt)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            externalCt, _lifetimeCts.Token);

        while (!linked.Token.IsCancellationRequested)
        {
            try   { await RunSessionAsync(linked.Token); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorkerService] Unexpected: {ex.Message}");
                await Task.Delay(2000, linked.Token);
            }
        }
    }

    /// <summary>Send a command. No-op if not connected.</summary>
    public async Task SendAsync<T>(T command) where T : IIpcMessage
    {
        if (_writer is null || ConnectionStatus != Status.Connected) return;
        await _writeLock.WaitAsync(_lifetimeCts.Token);
        try   { await _writer.WriteLineAsync(IpcEnvelope.Wrap(command)); }
        catch { /* disconnection detected by read loop */ }
        finally { _writeLock.Release(); }
    }

    /// <summary>Fire-and-forget convenience wrapper around SendAsync.</summary>
    public void Send<T>(T command) where T : IIpcMessage
        => _ = SendAsync(command);

    public void Dispose() => _lifetimeCts.Cancel();

    // ── Session management ────────────────────────────────────────────────────

    private async Task RunSessionAsync(CancellationToken ct)
    {
        SetStatus(Status.Connecting);

        int attempts = 0;
        while (!ct.IsCancellationRequested)
        {
            if (await TryConnectAsync(ct)) break;

            attempts++;
            if (attempts == 2 && !WorkerLauncher.IsRunning())
                WorkerLauncher.TryLaunch();

            // Back off: 1s for first few retries, then 3s
            int delay = attempts < 5 ? 1000 : 3000;
            await Task.Delay(delay, ct);
        }

        if (!ct.IsCancellationRequested)
            await ReadLoopAsync(ct);
    }

    private async Task<bool> TryConnectAsync(CancellationToken ct)
    {
        try
        {
            var pipe = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            await pipe.ConnectAsync(timeout: 1000, ct);

            _pipe   = pipe;
            _writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

            SetStatus(Status.Connected);
            Console.WriteLine("[WorkerService] Connected.");
            return true;
        }
        catch
        {
            _pipe?.Dispose();
            _pipe   = null;
            _writer = null;
            return false;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(_pipe!, leaveOpen: false);

        // Request a full state dump so the UI is in sync after (re)connect
        Send(new RequestFullStateCommand());

        try
        {
            while (!ct.IsCancellationRequested && (_pipe?.IsConnected ?? false))
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line is null) break;

                var env = IpcEnvelope.Parse(line);
                if (env is not null) Dispatch(env);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Console.WriteLine($"[WorkerService] Read error: {ex.Message}"); }
        finally
        {
            _writer = null;
            SetStatus(Status.Disconnected);
            Console.WriteLine("[WorkerService] Disconnected.");
        }
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private void Dispatch(IpcEnvelope env)
    {
        switch (env.Type)
        {
            case MessageTypes.WorkerReady:
                Console.WriteLine($"[WorkerService] Worker ready (v{env.As<WorkerReadyUpdate>()?.Version}).");
                break;
            case MessageTypes.AppState:
                AppStateReceived?.Invoke(env.As<AppStateUpdate>()!);
                break;
            case MessageTypes.ClientState:
                ClientStateReceived?.Invoke(env.As<ClientStateUpdate>()!);
                break;
            case MessageTypes.HpSp:
                HpSpReceived?.Invoke(env.As<HpSpUpdate>()!);
                break;
            case MessageTypes.Character:
                CharacterReceived?.Invoke(env.As<CharacterUpdate>()!);
                break;
            case MessageTypes.ProcessList:
                ProcessListReceived?.Invoke(env.As<ProcessListUpdate>()!);
                break;
            case MessageTypes.ProfileList:
                ProfileListReceived?.Invoke(env.As<ProfileListUpdate>()!);
                break;
            case MessageTypes.LogMessage:
                LogMessageReceived?.Invoke(env.As<LogMessageUpdate>()!);
                break;
            case MessageTypes.Error:
                ErrorReceived?.Invoke(env.As<ErrorUpdate>()!);
                break;
            case MessageTypes.AutopotHPConfig:
                AutopotHPConfigReceived?.Invoke(env.As<AutopotHPConfigUpdate>()!);
                break;
            case MessageTypes.AutopotSPConfig:
                AutopotSPConfigReceived?.Invoke(env.As<AutopotSPConfigUpdate>()!);
                break;
            case MessageTypes.StatusRecoveryConfig:
                StatusRecoveryConfigReceived?.Invoke(env.As<StatusRecoveryConfigUpdate>()!);
                break;
            case MessageTypes.SkillTimerConfig:
                SkillTimerConfigReceived?.Invoke(env.As<SkillTimerConfigUpdate>()!);
                break;
            case MessageTypes.DebuffRecoveryConfig:
                DebuffRecoveryConfigReceived?.Invoke(env.As<DebuffRecoveryConfigUpdate>()!);
                break;
            case MessageTypes.AutobuffSkillConfig:
                AutobuffSkillConfigReceived?.Invoke(env.As<AutobuffSkillConfigUpdate>()!);
                break;
            case MessageTypes.AutobuffOrderConfig:
                AutobuffOrderConfigReceived?.Invoke(env.As<AutobuffOrderConfigUpdate>()!);
                break;
            case MessageTypes.AutobuffItemConfig:
                AutobuffItemConfigReceived?.Invoke(env.As<AutobuffItemConfigUpdate>()!);
                break;
            case MessageTypes.SkillSpammerConfigUpdate:
                SkillSpammerConfigReceived?.Invoke(env.As<SkillSpammerConfigUpdate>()!);
                break;
            case MessageTypes.GlobalConfigUpdate:
                GlobalConfigReceived?.Invoke(env.As<GlobalConfigUpdate>()!);
                break;
            case MessageTypes.ProfileSettingsUpdate:
                ProfileSettingsReceived?.Invoke(env.As<ProfileSettingsUpdate>()!);
                break;
            case MessageTypes.AutoOffConfigUpdate:
                AutoOffConfigReceived?.Invoke(env.As<AutoOffConfigUpdate>()!);
                break;
            case MessageTypes.AutoOffTimerStateUpdate:
                AutoOffTimerStateReceived?.Invoke(env.As<AutoOffTimerStateUpdate>()!);
                break;
            case MessageTypes.TransferHelperConfigUpdate:
                TransferHelperConfigReceived?.Invoke(env.As<TransferHelperConfigUpdate>()!);
                break;
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
