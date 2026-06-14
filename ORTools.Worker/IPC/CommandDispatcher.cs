using ORTools.Shared.Protocol;

namespace ORTools.Worker.IPC;

public sealed class CommandDispatcher
{
    private readonly WorkerCore _core;
    public CommandDispatcher(WorkerCore core) => _core = core;

    public async Task HandleAsync(IIpcMessage env)
    {
        DebugLogger.Debug($"[Dispatcher] ← {env.Type}");

        switch (env.Type)
        {
            case MessageTypes.TurnOn:
                await _core.HandleTurnOn(); break;

            case MessageTypes.TurnOff:
                await _core.HandleTurnOff(); break;

            case MessageTypes.ConnectClient:
                var cc = env as ConnectClientCommand;
                if (cc != null) await _core.HandleConnectClient(cc.ProcessName);
                break;

            case MessageTypes.DisconnectClient:
                await _core.HandleDisconnectClient(); break;

            case MessageTypes.UpdateToggleKey:
                var utk = env as UpdateToggleKeyCommand;
                if (utk != null) await _core.HandleUpdateToggleKey(utk.Key);
                break;

            case MessageTypes.SwitchProfile:
                var sp = env as SwitchProfileCommand;
                if (sp != null) await _core.HandleSwitchProfile(sp.ProfileName);
                break;

            case MessageTypes.CreateProfile:
                var crp = env as CreateProfileCommand;
                if (crp != null) await _core.HandleCreateProfile(crp.ProfileName);
                break;

            case MessageTypes.CopyProfile:
                var cop = env as CopyProfileCommand;
                if (cop != null) await _core.HandleCopyProfile(cop.SourceProfile, cop.NewProfileName);
                break;

            case MessageTypes.RenameProfile:
                var rep = env as RenameProfileCommand;
                if (rep != null) await _core.HandleRenameProfile(rep.OldProfileName, rep.NewProfileName);
                break;

            case MessageTypes.DeleteProfile:
                var dep = env as DeleteProfileCommand;
                if (dep != null) await _core.HandleDeleteProfile(dep.ProfileName);
                break;

            case MessageTypes.RequestProcessList:
                await _core.HandleRequestProcessList();
                break;

            case MessageTypes.RequestFullState:
                await _core.HandleFullStateRequest(); break;

            case MessageTypes.UpdateAutopotHPSlot:
                var hpSlot = env as UpdateAutopotHPSlotCommand;
                if (hpSlot != null) await _core.HandleUpdateAutopotHPSlot(hpSlot);
                break;

            case MessageTypes.UpdateAutopotHPSettings:
                var hpSet = env as UpdateAutopotHPSettingsCommand;
                if (hpSet != null) await _core.HandleUpdateAutopotHPSettings(hpSet);
                break;

            case MessageTypes.UpdateAutopotSPSlot:
                var spSlot = env as UpdateAutopotSPSlotCommand;
                if (spSlot != null) await _core.HandleUpdateAutopotSPSlot(spSlot);
                break;

            case MessageTypes.UpdateAutopotSPSettings:
                var sps = env as UpdateAutopotSPSettingsCommand;
                if (sps != null) await _core.HandleUpdateAutopotSPSettings(sps);
                break;

            case MessageTypes.UpdateStatusRecoveryItem:
                var sri = env as UpdateStatusRecoveryItemCommand;
                if (sri != null) await _core.HandleUpdateStatusRecoveryItem(sri);
                break;

            case MessageTypes.UpdateStatusRecoverySettings:
                var srs = env as UpdateStatusRecoverySettingsCommand;
                if (srs != null) await _core.HandleUpdateStatusRecoverySettings(srs);
                break;

            case MessageTypes.UpdateSkillTimerSlot:
                var sts = env as UpdateSkillTimerSlotCommand;
                if (sts != null) await _core.HandleUpdateSkillTimerSlot(sts);
                break;

            case MessageTypes.UpdateDebuffRecoveryItem:
                var dri = env as UpdateDebuffRecoveryItemCommand;
                if (dri != null) await _core.HandleUpdateDebuffRecoveryItem(dri);
                break;

            case MessageTypes.UpdateDebuffRecoverySettings:
                var drs = env as UpdateDebuffRecoverySettingsCommand;
                if (drs != null) await _core.HandleUpdateDebuffRecoverySettings(drs);
                break;

            case MessageTypes.UpdateAutobuffSkillItem:
                var absi = env as UpdateAutobuffSkillItemCommand;
                if (absi != null) await _core.HandleUpdateAutobuffSkillItem(absi);
                break;

            case MessageTypes.UpdateAutobuffSkillSettings:
                var abss = env as UpdateAutobuffSkillSettingsCommand;
                if (abss != null) await _core.HandleUpdateAutobuffSkillSettings(abss);
                break;

            case MessageTypes.UpdateAutobuffOrder:
                var uabo = env as UpdateAutobuffOrderCommand;
                if (uabo != null) await _core.HandleUpdateAutobuffOrder(uabo);
                break;

            case MessageTypes.UpdateAutobuffItemItem:
                var abii = env as UpdateAutobuffItemCommand;
                if (abii != null) await _core.HandleUpdateAutobuffItemItem(abii);
                break;

            case MessageTypes.UpdateAutobuffItemSettings:
                var abis = env as UpdateAutobuffItemSettingsCommand;
                if (abis != null) await _core.HandleUpdateAutobuffItemSettings(abis);
                break;

            case MessageTypes.UpdateSkillSpammerEntry:
                var usse = env as UpdateSkillSpammerEntryCommand;
                if (usse != null) await _core.HandleUpdateSkillSpammerEntry(usse);
                break;

            case MessageTypes.UpdateSkillSpammerSettings:
                var usss = env as UpdateSkillSpammerSettingsCommand;
                if (usss != null) await _core.HandleUpdateSkillSpammerSettings(usss);
                break;

            case MessageTypes.UpdateProfileSettings:
                var ups = env as UpdateProfileSettingsCommand;
                if (ups != null) await _core.HandleUpdateProfileSettings(ups);
                break;

            case MessageTypes.UpdateAutoOffSettings:
                var uaos = env as UpdateAutoOffSettingsCommand;
                if (uaos != null) await _core.HandleUpdateAutoOffSettings(uaos);
                break;

            case MessageTypes.ToggleAutoOffTimer:
                var taot = env as ToggleAutoOffTimerCommand;
                if (taot != null) await _core.HandleToggleAutoOffTimer(taot);
                break;

            case MessageTypes.UpdateGlobalConfig:
                var ugc = env as UpdateGlobalConfigCommand;
                if (ugc != null) await _core.HandleUpdateGlobalConfig(ugc);
                break;
                
            case MessageTypes.UpdateTransferHelperCommand:
                var utc = env as UpdateTransferHelperCommand;
                if (utc != null) await _core.HandleUpdateTransferHelper(utc);
                break;

            case MessageTypes.UpdateMacroSwitchTrigger:
                var umst = env as UpdateMacroSwitchTriggerCommand;
                if (umst != null) await _core.HandleUpdateMacroSwitchTrigger(umst);
                break;

            case MessageTypes.UpdateMacroSwitchStep:
                var umss = env as UpdateMacroSwitchStepCommand;
                if (umss != null) await _core.HandleUpdateMacroSwitchStep(umss);
                break;

            case MessageTypes.ResetMacroSwitchRow:
                var rmsr = env as ResetMacroSwitchRowCommand;
                if (rmsr != null) await _core.HandleResetMacroSwitchRow(rmsr);
                break;

            case MessageTypes.UpdateMacroSongTrigger:
                var umsonst = env as UpdateMacroSongTriggerCommand;
                if (umsonst != null) await _core.HandleUpdateMacroSongTrigger(umsonst);
                break;

            case MessageTypes.UpdateMacroSongStep:
                var umsonss = env as UpdateMacroSongStepCommand;
                if (umsonss != null) await _core.HandleUpdateMacroSongStep(umsonss);
                break;

            case MessageTypes.UpdateMacroSongAdaptation:
                var umsa = env as UpdateMacroSongAdaptationCommand;
                if (umsa != null) await _core.HandleUpdateMacroSongAdaptation(umsa);
                break;

            case MessageTypes.UpdateMacroSongInstrument:
                var umsi = env as UpdateMacroSongInstrumentCommand;
                if (umsi != null) await _core.HandleUpdateMacroSongInstrument(umsi);
                break;

            case MessageTypes.UpdateMacroSongDelay:
                var umsd = env as UpdateMacroSongDelayCommand;
                if (umsd != null) await _core.HandleUpdateMacroSongDelay(umsd);
                break;

            case MessageTypes.ResetMacroSongRow:
                var rmsongr = env as ResetMacroSongRowCommand;
                if (rmsongr != null) await _core.HandleResetMacroSongRow(rmsongr);
                break;

            case MessageTypes.UpdateAtkDefTrigger:
                var uadt = env as UpdateAtkDefTriggerCommand;
                if (uadt != null) await _core.HandleUpdateAtkDefTrigger(uadt);
                break;

            case MessageTypes.UpdateAtkDefSpammerDelay:
                var uadsd = env as UpdateAtkDefSpammerDelayCommand;
                if (uadsd != null) await _core.HandleUpdateAtkDefSpammerDelay(uadsd);
                break;

            case MessageTypes.UpdateAtkDefSwitchDelay:
                var uadswd = env as UpdateAtkDefSwitchDelayCommand;
                if (uadswd != null) await _core.HandleUpdateAtkDefSwitchDelay(uadswd);
                break;

            case MessageTypes.UpdateAtkDefClick:
                var uadc = env as UpdateAtkDefClickCommand;
                if (uadc != null) await _core.HandleUpdateAtkDefClick(uadc);
                break;

            case MessageTypes.UpdateAtkDefEquip:
                var uade = env as UpdateAtkDefEquipCommand;
                if (uade != null) await _core.HandleUpdateAtkDefEquip(uade);
                break;

            case MessageTypes.ResetAtkDefRow:
                var radr = env as ResetAtkDefRowCommand;
                if (radr != null) await _core.HandleResetAtkDefRow(radr);
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
