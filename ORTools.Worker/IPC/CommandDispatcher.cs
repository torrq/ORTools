using ORTools.Shared.Protocol;

namespace ORTools.Worker.IPC;

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
                await _core.HandleTurnOn(); break;

            case MessageTypes.TurnOff:
                await _core.HandleTurnOff(); break;

            case MessageTypes.ConnectClient:
                var cc = env.As<ConnectClientCommand>();
                if (cc != null) await _core.HandleConnectClient(cc.ProcessName);
                break;

            case MessageTypes.DisconnectClient:
                await _core.HandleDisconnectClient(); break;

            case MessageTypes.UpdateToggleKey:
                var utk = env.As<UpdateToggleKeyCommand>();
                if (utk != null) await _core.HandleUpdateToggleKey(utk.Key);
                break;

            case MessageTypes.SwitchProfile:
                var sp = env.As<SwitchProfileCommand>();
                if (sp != null) await _core.HandleSwitchProfile(sp.ProfileName);
                break;

            case MessageTypes.CreateProfile:
                var crp = env.As<CreateProfileCommand>();
                if (crp != null) await _core.HandleCreateProfile(crp.ProfileName);
                break;

            case MessageTypes.CopyProfile:
                var cop = env.As<CopyProfileCommand>();
                if (cop != null) await _core.HandleCopyProfile(cop.SourceProfile, cop.NewProfileName);
                break;

            case MessageTypes.RenameProfile:
                var rep = env.As<RenameProfileCommand>();
                if (rep != null) await _core.HandleRenameProfile(rep.OldProfileName, rep.NewProfileName);
                break;

            case MessageTypes.DeleteProfile:
                var dep = env.As<DeleteProfileCommand>();
                if (dep != null) await _core.HandleDeleteProfile(dep.ProfileName);
                break;

            case MessageTypes.RequestProcessList:
                await _core.HandleRequestProcessList();
                break;

            case MessageTypes.RequestFullState:
                await _core.HandleFullStateRequest(); break;

            case MessageTypes.UpdateAutopotHPSlot:
                var hpSlot = env.As<UpdateAutopotHPSlotCommand>();
                if (hpSlot != null) await _core.HandleUpdateAutopotHPSlot(hpSlot);
                break;

            case MessageTypes.UpdateAutopotHPSettings:
                var hpSet = env.As<UpdateAutopotHPSettingsCommand>();
                if (hpSet != null) await _core.HandleUpdateAutopotHPSettings(hpSet);
                break;

            case MessageTypes.UpdateAutopotSPSlot:
                var spSlot = env.As<UpdateAutopotSPSlotCommand>();
                if (spSlot != null) await _core.HandleUpdateAutopotSPSlot(spSlot);
                break;

            case MessageTypes.UpdateAutopotSPSettings:
                var sps = env.As<UpdateAutopotSPSettingsCommand>();
                if (sps != null) await _core.HandleUpdateAutopotSPSettings(sps);
                break;

            case MessageTypes.UpdateStatusRecoveryItem:
                var sri = env.As<UpdateStatusRecoveryItemCommand>();
                if (sri != null) await _core.HandleUpdateStatusRecoveryItem(sri);
                break;

            case MessageTypes.UpdateStatusRecoverySettings:
                var srs = env.As<UpdateStatusRecoverySettingsCommand>();
                if (srs != null) await _core.HandleUpdateStatusRecoverySettings(srs);
                break;

            case MessageTypes.UpdateSkillTimerSlot:
                var sts = env.As<UpdateSkillTimerSlotCommand>();
                if (sts != null) await _core.HandleUpdateSkillTimerSlot(sts);
                break;

            case MessageTypes.UpdateDebuffRecoveryItem:
                var dri = env.As<UpdateDebuffRecoveryItemCommand>();
                if (dri != null) await _core.HandleUpdateDebuffRecoveryItem(dri);
                break;

            case MessageTypes.UpdateDebuffRecoverySettings:
                var drs = env.As<UpdateDebuffRecoverySettingsCommand>();
                if (drs != null) await _core.HandleUpdateDebuffRecoverySettings(drs);
                break;

            case MessageTypes.UpdateAutobuffSkillItem:
                var absi = env.As<UpdateAutobuffSkillItemCommand>();
                if (absi != null) await _core.HandleUpdateAutobuffSkillItem(absi);
                break;

            case MessageTypes.UpdateAutobuffSkillSettings:
                var abss = env.As<UpdateAutobuffSkillSettingsCommand>();
                if (abss != null) await _core.HandleUpdateAutobuffSkillSettings(abss);
                break;

            case MessageTypes.UpdateAutobuffItemItem:
                var abii = env.As<UpdateAutobuffItemCommand>();
                if (abii != null) await _core.HandleUpdateAutobuffItemItem(abii);
                break;

            case MessageTypes.UpdateAutobuffItemSettings:
                var abis = env.As<UpdateAutobuffItemSettingsCommand>();
                if (abis != null) await _core.HandleUpdateAutobuffItemSettings(abis);
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