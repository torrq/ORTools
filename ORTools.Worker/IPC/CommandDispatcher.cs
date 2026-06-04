using ORTools.Shared.Protocol;

namespace ORTools.Worker.IPC;

/// <summary>
/// Routes incoming commands from the UI to the appropriate model handler.
///
/// Phase 1: logs every command and returns plausible stub responses so the
///          UI can be built and tested without real model wiring.
///
/// Phase 2: replace each stub with a real call to WorkerCore's model instance.
///          Each handler is a separate method so diffs stay small.
/// </summary>
public sealed class CommandDispatcher
{
    private readonly WorkerCore _core;

    // Phase 2: track state that the models would normally own
    private bool   _isOn;
    private bool   _clientConnected;
    private string _clientProcess  = "";
    private string _currentProfile = "Default";

    public CommandDispatcher(WorkerCore core) => _core = core;

    public async Task HandleAsync(IpcEnvelope env, CancellationToken ct)
    {
        Console.WriteLine($"[Dispatcher] ← {env.Type}");

        switch (env.Type)
        {
            case MessageTypes.TurnOn:
                await HandleTurnOn(ct);
                break;

            case MessageTypes.TurnOff:
                await HandleTurnOff(ct);
                break;

            case MessageTypes.ConnectClient:
                await HandleConnectClient(env.As<ConnectClientCommand>(), ct);
                break;

            case MessageTypes.DisconnectClient:
                await HandleDisconnectClient(ct);
                break;

            case MessageTypes.SwitchProfile:
                await HandleSwitchProfile(env.As<SwitchProfileCommand>(), ct);
                break;

            case MessageTypes.RequestProcessList:
                await HandleRequestProcessList(ct);
                break;

            case MessageTypes.RequestFullState:
                await HandleRequestFullState(ct);
                break;

            case MessageTypes.Shutdown:
                Console.WriteLine("[Dispatcher] Shutdown requested by UI.");
                Environment.Exit(0);
                break;

            default:
                Console.WriteLine($"[Dispatcher] Unknown type: {env.Type}");
                break;
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private async Task HandleTurnOn(CancellationToken ct)
    {
        if (!_clientConnected)
        {
            await _core.BroadcastAsync(new ErrorUpdate("No client connected."));
            return;
        }
        // Phase 2: subject.Notify(new Message(MessageCode.TURN_ON, null));
        _isOn = true;
        await _core.BroadcastAsync(new AppStateUpdate(IsOn: true));
    }

    private async Task HandleTurnOff(CancellationToken ct)
    {
        // Phase 2: subject.Notify(new Message(MessageCode.TURN_OFF, null));
        _isOn = false;
        await _core.BroadcastAsync(new AppStateUpdate(IsOn: false));
    }

    private async Task HandleConnectClient(ConnectClientCommand? cmd, CancellationToken ct)
    {
        if (cmd is null) return;
        // Phase 2: ClientSingleton.SetClient(Server.FindClient(cmd.ProcessName));
        _clientConnected = true;
        _clientProcess   = cmd.ProcessName;
        await _core.BroadcastAsync(new ClientStateUpdate(Connected: true, ProcessName: cmd.ProcessName));
    }

    private async Task HandleDisconnectClient(CancellationToken ct)
    {
        // Phase 2: ClientSingleton.SetClient(null); stop all threads
        _isOn            = false;
        _clientConnected = false;
        _clientProcess   = "";
        await _core.BroadcastAsync(new AppStateUpdate(IsOn: false));
        await _core.BroadcastAsync(new ClientStateUpdate(Connected: false, ProcessName: null));
    }

    private async Task HandleSwitchProfile(SwitchProfileCommand? cmd, CancellationToken ct)
    {
        if (cmd is null) return;
        // Phase 2: ProfileSingleton.LoadProfile(cmd.ProfileName);
        _currentProfile = cmd.ProfileName;
        await _core.BroadcastAsync(new ProfileListUpdate(
            Profiles:       GetProfileList(),
            CurrentProfile: _currentProfile));
    }

    private async Task HandleRequestProcessList(CancellationToken ct)
    {
        // Phase 2: replace with Server.GetLocalClients() scan
        var processes = System.Diagnostics.Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("ragexe",    StringComparison.OrdinalIgnoreCase)
                     || p.ProcessName.Contains("ragnarok",  StringComparison.OrdinalIgnoreCase))
            .Select(p => p.ProcessName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        await _core.BroadcastAsync(new ProcessListUpdate(processes));
    }

    private async Task HandleRequestFullState(CancellationToken ct)
    {
        // Sync everything the UI needs after a reconnect
        await _core.BroadcastAsync(new AppStateUpdate(IsOn: _isOn));
        await _core.BroadcastAsync(new ClientStateUpdate(
            Connected:   _clientConnected,
            ProcessName: _clientConnected ? _clientProcess : null));
        await _core.BroadcastAsync(new ProfileListUpdate(
            Profiles:       GetProfileList(),
            CurrentProfile: _currentProfile));
        await HandleRequestProcessList(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<string> GetProfileList()
    {
        // Phase 2: return Profile.ListAll().ToList();
        return new List<string> { "Default" };
    }
}
