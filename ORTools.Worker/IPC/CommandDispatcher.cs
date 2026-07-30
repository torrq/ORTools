using ORTools.Shared.Protocol;

namespace ORTools.Worker.IPC;

public sealed class CommandDispatcher
{
    private readonly WorkerCore _core;
    public CommandDispatcher(WorkerCore core) => _core = core;

    public async Task HandleAsync(IIpcMessage env)
    {
        DebugLogger.Debug($"[Dispatcher] ← {env.GetType().Name}");

        switch (env)
        {
            case TurnOnCommand cmd:
                await _core.HandleTurnOn(); break;

            case TurnOffCommand cmd:
                await _core.HandleTurnOff(); break;

            case ConnectClientCommand cc:
                await _core.HandleConnectClient(cc.ProcessName); break;

            case DisconnectClientCommand cmd:
                await _core.HandleDisconnectClient(); break;

            case UpdateToggleKeyCommand utk:
                await _core.HandleUpdateToggleKey(utk.Key); break;

            case SwitchProfileCommand sp:
                await _core.HandleSwitchProfile(sp.ProfileName); break;

            case CreateProfileCommand crp:
                await _core.HandleCreateProfile(crp.ProfileName); break;

            case CopyProfileCommand cop:
                await _core.HandleCopyProfile(cop.SourceProfile, cop.NewProfileName); break;

            case RenameProfileCommand rep:
                await _core.HandleRenameProfile(rep.OldProfileName, rep.NewProfileName); break;

            case DeleteProfileCommand dep:
                await _core.HandleDeleteProfile(dep.ProfileName); break;

            case RequestProcessListCommand cmd:
                await _core.HandleRequestProcessList();
                break;

            case RequestFullStateCommand cmd:
                await _core.HandleFullStateRequest(); break;

            case UpdateAutopotHPSlotCommand hpSlot:
                await _core.HandleUpdateAutopotHPSlot(hpSlot); break;

            case UpdateAutopotHPSettingsCommand hpSet:
                await _core.HandleUpdateAutopotHPSettings(hpSet); break;

            case UpdateAutopotSPSlotCommand spSlot:
                await _core.HandleUpdateAutopotSPSlot(spSlot); break;

            case UpdateAutopotSPSettingsCommand sps:
                await _core.HandleUpdateAutopotSPSettings(sps); break;

            case UpdateStatusRecoveryItemCommand sri:
                await _core.HandleUpdateStatusRecoveryItem(sri); break;

            case UpdateStatusRecoverySettingsCommand srs:
                await _core.HandleUpdateStatusRecoverySettings(srs); break;

            case UpdateSkillTimerSlotCommand sts:
                await _core.HandleUpdateSkillTimerSlot(sts); break;

            case UpdateDebuffRecoveryItemCommand dri:
                await _core.HandleUpdateDebuffRecoveryItem(dri); break;

            case UpdateDebuffRecoverySettingsCommand drs:
                await _core.HandleUpdateDebuffRecoverySettings(drs); break;

            case UpdateAutobuffSkillItemCommand absi:
                await _core.HandleUpdateAutobuffSkillItem(absi); break;

            case UpdateAutobuffSkillSettingsCommand abss:
                await _core.HandleUpdateAutobuffSkillSettings(abss); break;

            case UpdateAutobuffOrderCommand uabo:
                await _core.HandleUpdateAutobuffOrder(uabo); break;

            case UpdateAutobuffItemCommand abii:
                await _core.HandleUpdateAutobuffItemItem(abii); break;

            case UpdateAutobuffItemSettingsCommand abis:
                await _core.HandleUpdateAutobuffItemSettings(abis); break;

            case UpdateSkillSpammerEntryCommand usse:
                await _core.HandleUpdateSkillSpammerEntry(usse); break;

            case UpdateSkillSpammerSettingsCommand usss:
                await _core.HandleUpdateSkillSpammerSettings(usss); break;

            case UpdateProfileSettingsCommand ups:
                await _core.HandleUpdateProfileSettings(ups); break;

            case UpdateAutoOffSettingsCommand uaos:
                await _core.HandleUpdateAutoOffSettings(uaos); break;

            case ToggleAutoOffTimerCommand taot:
                await _core.HandleToggleAutoOffTimer(taot); break;

            case PauseAutoOffTimerCommand paot:
                await _core.HandlePauseAutoOffTimer(paot); break;

            case UpdateGlobalConfigCommand ugc:
                await _core.HandleUpdateGlobalConfig(ugc); break;
                
            case UpdateStatusLoggerConfigCommand uslcc:
                await _core.HandleUpdateStatusLoggerConfig(uslcc); break;
                
            case UpdateTransferHelperCommand utc:
                await _core.HandleUpdateTransferHelper(utc); break;

            case UpdateMacroSwitchTriggerCommand umst:
                await _core.HandleUpdateMacroSwitchTrigger(umst); break;

            case UpdateMacroSwitchStepCommand umss:
                await _core.HandleUpdateMacroSwitchStep(umss); break;

            case ResetMacroSwitchRowCommand rmsr:
                await _core.HandleResetMacroSwitchRow(rmsr); break;

            case UpdateMacroSongTriggerCommand umsonst:
                await _core.HandleUpdateMacroSongTrigger(umsonst); break;

            case UpdateMacroSongStepCommand umsonss:
                await _core.HandleUpdateMacroSongStep(umsonss); break;

            case UpdateMacroSongAdaptationCommand umsa:
                await _core.HandleUpdateMacroSongAdaptation(umsa); break;

            case UpdateMacroSongInstrumentCommand umsi:
                await _core.HandleUpdateMacroSongInstrument(umsi); break;

            case UpdateMacroSongDelayCommand umsd:
                await _core.HandleUpdateMacroSongDelay(umsd); break;

            case ResetMacroSongRowCommand rmsongr:
                await _core.HandleResetMacroSongRow(rmsongr); break;

            case UpdateAtkDefTriggerCommand uadt:
                await _core.HandleUpdateAtkDefTrigger(uadt); break;

            case UpdateAtkDefSpammerDelayCommand uadsd:
                await _core.HandleUpdateAtkDefSpammerDelay(uadsd); break;

            case UpdateAtkDefSwitchDelayCommand uadswd:
                await _core.HandleUpdateAtkDefSwitchDelay(uadswd); break;

            case UpdateAtkDefClickCommand uadc:
                await _core.HandleUpdateAtkDefClick(uadc); break;

            case UpdateAtkDefEquipCommand uade:
                await _core.HandleUpdateAtkDefEquip(uade); break;

            case ResetAtkDefRowCommand radr:
                await _core.HandleResetAtkDefRow(radr); break;

            case ShutdownCommand cmd:
                DebugLogger.Info("[Dispatcher] Shutdown requested.");
                await _core.HandleTurnOff();
                Environment.Exit(0);
                break;

            default:
                DebugLogger.Warning($"[Dispatcher] Unknown type: {env.GetType().Name}");
                break;
        }
    }
}
