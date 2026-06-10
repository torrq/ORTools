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

            // ── Autopot HP ────────────────────────────────────────────────────
            case MessageTypes.UpdateAutopotHPSlot:
                var hpSlot = env.As<UpdateAutopotHPSlotCommand>();
                if (hpSlot != null) await _core.HandleUpdateAutopotHPSlot(hpSlot);
                break;

            case MessageTypes.UpdateAutopotHPSettings:
                var hpSettings = env.As<UpdateAutopotHPSettingsCommand>();
                if (hpSettings != null) await _core.HandleUpdateAutopotHPSettings(hpSettings);
                break;

            case MessageTypes.ReorderAutopotHP:
                var hpOrder = env.As<ReorderAutopotHPCommand>();
                if (hpOrder != null) await _core.HandleReorderAutopotHP(hpOrder);
                break;

            // ── Autopot SP ────────────────────────────────────────────────────
            case MessageTypes.UpdateAutopotSPSlot:
                var spSlot = env.As<UpdateAutopotSPSlotCommand>();
                if (spSlot != null) await _core.HandleUpdateAutopotSPSlot(spSlot);
                break;

            case MessageTypes.UpdateAutopotSPSettings:
                var spSettings = env.As<UpdateAutopotSPSettingsCommand>();
                if (spSettings != null) await _core.HandleUpdateAutopotSPSettings(spSettings);
                break;

            case MessageTypes.ReorderAutopotSP:
                var spOrder = env.As<ReorderAutopotSPCommand>();
                if (spOrder != null) await _core.HandleReorderAutopotSP(spOrder);
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
