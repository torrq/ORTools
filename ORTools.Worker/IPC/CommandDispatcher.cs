using ORTools.Shared.Protocol;

namespace ORTools.Worker.IPC;

/// <summary>
/// Routes incoming IPC commands to WorkerCore handlers.
/// All logic lives in WorkerCore — this is just the switch.
/// </summary>
public sealed class CommandDispatcher
{
    private readonly WorkerCore _core;
    public CommandDispatcher(WorkerCore core) => _core = core;

    public async Task HandleAsync(IpcEnvelope env, CancellationToken ct)
    {
        DebugLogger.Debug($"[Dispatcher] ← {env.Type}");

        switch (env.Type)
        {
            case MessageTypes.TurnOn:
                await _core.HandleTurnOn();
                break;

            case MessageTypes.TurnOff:
                await _core.HandleTurnOff();
                break;

            case MessageTypes.ConnectClient:
                var cc = env.As<ConnectClientCommand>();
                if (cc != null) await _core.HandleConnectClient(cc.ProcessName);
                break;

            case MessageTypes.DisconnectClient:
                await _core.HandleDisconnectClient();
                break;

            case MessageTypes.SwitchProfile:
                var sp = env.As<SwitchProfileCommand>();
                if (sp != null) await _core.HandleSwitchProfile(sp.ProfileName);
                break;

            case MessageTypes.RequestProcessList:
                await _core.BroadcastAsync(new ProcessListUpdate(
                    Server.GetLocalClients().Select(c => c.Name).ToList()));
                break;

            case MessageTypes.RequestFullState:
                await _core.HandleFullStateRequest();
                break;

            case MessageTypes.Shutdown:
                DebugLogger.Info("[Dispatcher] Shutdown requested.");
                await _core.HandleTurnOff();
                Environment.Exit(0);
                break;

            default:
                DebugLogger.Warning($"[Dispatcher] Unknown type: {env.Type}");
                break;
        }
    }
}
