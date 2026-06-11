using ORTools.Shared.Protocol;
using ORTools.Worker.IPC;

namespace ORTools.Worker;

public sealed class WorkerCore
{
    public const string PipeName = "ORTools-Worker";

    public Subject Subject { get; } = new();

    private readonly PipeServer _server;
    private readonly CommandDispatcher _dispatcher;
    private readonly StatePublisher _statePublisher;
    private readonly Thread _hookThread;

    private bool _isOn;
    private string _currentProfileName = "Default";

    public WorkerCore()
    {
        ConfigGlobal.Initialize();
        Server.Initialize();

        // Load server configs into ClientListSingleton
        foreach (var dto in Server.GetLocalClients())
        {
            ClientListSingleton.AddClient(new Client(dto));
        }

        ProfileSingleton.Create("Default");
        ProfileSingleton.Load("Default");
        HookSkillSpammerEvents();

        _hookThread = new Thread(() =>
        {
            KeyboardHook.Enable();
            System.Windows.Forms.Application.Run();
        });
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.IsBackground = true;
        _hookThread.Name = "KeyboardHookMessageLoop";
        _hookThread.Start();

        WorkerNotifier.TurnOffRequested += reason =>
        {
            DebugLogger.Info($"[WorkerCore] Auto turn-off: {reason}");
            _ = HandleTurnOff();
        };

        DebugLogger.LogMessageEmitted += (level, msg) =>
            _ = BroadcastAsync(new LogMessageUpdate(level, msg));

        RefreshToggleHotkey();

        _statePublisher = new StatePublisher(msg => BroadcastAsync(msg));
        _statePublisher.Start(); // Run continuously to keep UI synced even when OFF

        _dispatcher = new CommandDispatcher(this);
        _server = new PipeServer(PipeName, _dispatcher);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"[WorkerCore] Pipe: {PipeName}  Mode: {AppConfig.GetRateTag()}");
        Win32Interop.timeBeginPeriod(1);
        try { await _server.RunAsync(ct); }
        finally
        {
            Win32Interop.timeEndPeriod(1);
            KeyboardHook.Disable();
            await HandleTurnOff();
        }
    }

    // ── Turn on/off ───────────────────────────────────────────────────────────

    public async Task HandleTurnOn()
    {
        if (ClientSingleton.GetClient() == null)
        {
            await BroadcastAsync(new ErrorUpdate("No client connected.")); return;
        }
        _isOn = true;
        var p = ProfileSingleton.GetCurrent();
        Subject.Notify(new Message(MessageCode.TURN_ON, null));
        p.AutopotHP.Start(); p.AutopotSP.Start();
        p.SkillTimer.Start(); p.SkillSpammer.Start();
        p.StatusRecovery.Start(); p.AutobuffSkill.Start();
        p.AutobuffItem.Start(); p.DebuffsRecovery.Start();
        p.MacroSwitch.Start(); p.SongMacro.Start();
        p.TransferHelper.Start();
        await BroadcastAsync(new AppStateUpdate(IsOn: true, ToggleKey: p.UserPreferences.ToggleStateKey));
        DebugLogger.Info("[WorkerCore] Turned ON");

        if (p.UserPreferences.SoundEnabled)
        {
            try { new System.Media.SoundPlayer(@"Resources\toggle_on.wav").Play(); }
            catch (Exception ex) { DebugLogger.Warning($"Failed to play sound: {ex.Message}"); }
        }
    }

    public async Task HandleTurnOff()
    {
        if (!_isOn) return;
        _isOn = false;
        var p = ProfileSingleton.GetCurrent();
        Subject.Notify(new Message(MessageCode.TURN_OFF, null));
        p.AutopotHP.Stop(); p.AutopotSP.Stop();
        p.SkillTimer.Stop(); p.SkillSpammer.Stop();
        p.StatusRecovery.Stop(); p.AutobuffSkill.Stop();
        p.AutobuffItem.Stop(); p.DebuffsRecovery.Stop();
        p.MacroSwitch.Stop(); p.SongMacro.Stop();
        p.TransferHelper.Stop();
        await BroadcastAsync(new AppStateUpdate(IsOn: false, ToggleKey: p.UserPreferences.ToggleStateKey));
        DebugLogger.Info("[WorkerCore] Turned OFF");

        if (p.UserPreferences.SoundEnabled)
        {
            try { new System.Media.SoundPlayer(@"Resources\toggle_off.wav").Play(); }
            catch (Exception ex) { DebugLogger.Warning($"Failed to play sound: {ex.Message}"); }
        }
    }

    public async Task HandleUpdateToggleKey(string key)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        prefs.ToggleStateKey = key;
        ProfileSingleton.SetConfiguration(prefs);
        RefreshToggleHotkey();
        await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: key));
    }

    public async Task HandleRequestProcessList()
    {
        await BroadcastAsync(new ProcessListUpdate(ListLocalProcesses()));
    }

    private List<string> ListLocalProcesses()
    {
        var result = new List<string>();
        foreach (var server in Server.GetLocalClients())
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(server.Name);
                foreach (var p in processes)
                {
                    string info = "";
                    try
                    {
                        using var tempClient = new Client($"{p.ProcessName}.exe - {p.Id}");
                        tempClient.RefreshLoginStatus();
                        if (tempClient.IsLoggedIn)
                        {
                            string name = tempClient.ReadCharacterName();
                            string map = tempClient.ReadCurrentMap();
                            
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                info = $" ({name} @ {map})";
                            }
                        }
                    }
                    catch { }

                    result.Add($"{p.ProcessName}.exe - {p.Id}{info}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"Failed to get processes for {server.Name}: {ex.Message}");
            }
        }
        return result;
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task HandleConnectClient(string processName)
    {
        if (_isOn) await HandleTurnOff();
        
        try
        {
            var client = new Client(processName);
            // Verify if client actually got memory addresses
            if (client.CurrentHPBaseAddress == 0)
            {
                await BroadcastAsync(new ErrorUpdate($"Client is not supported or not fully loaded: {processName}")); 
                return;
            }

            ClientSingleton.SetClient(client);
            await BroadcastAsync(new ClientStateUpdate(Connected: true, ProcessName: processName));
            DebugLogger.Info($"[WorkerCore] Client connected: {processName}");
        }
        catch (Exception ex) { await BroadcastAsync(new ErrorUpdate($"Connect failed: {ex.Message}")); }
    }

    public async Task HandleDisconnectClient()
    {
        if (_isOn) await HandleTurnOff();
        ClientSingleton.SetClient(null);
        await BroadcastAsync(new ClientStateUpdate(Connected: false, ProcessName: null));
        DebugLogger.Info("[WorkerCore] Client disconnected");
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task HandleSwitchProfile(string profileName)
    {
        if (_isOn) await HandleTurnOff();
        try
        {
            ProfileSingleton.Create(profileName);
            ProfileSingleton.Load(profileName);
            HookSkillSpammerEvents();
            _currentProfileName = profileName;
            Subject.Notify(new Message(MessageCode.PROFILE_CHANGED, profileName));
            ConfigGlobal.GetConfig().LastUsedProfile = profileName;
            ConfigGlobal.SaveConfig();
            RefreshToggleHotkey();
            await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), profileName));
            await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: ProfileSingleton.GetCurrent().UserPreferences.ToggleStateKey));
            await PushAllConfigs();
            DebugLogger.Info($"[WorkerCore] Profile: {profileName}");
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to load profile '{profileName}': {ex.Message}"));
        }
    }

    public async Task HandleCreateProfile(string profileName)
    {
        try
        {
            ProfileSingleton.Create(profileName);
            await HandleSwitchProfile(profileName);
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to create profile '{profileName}': {ex.Message}"));
        }
    }

    public async Task HandleCopyProfile(string sourceProfile, string newProfileName)
    {
        try
        {
            ProfileSingleton.Copy(sourceProfile, newProfileName);
            await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), _currentProfileName));
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to copy profile '{sourceProfile}': {ex.Message}"));
        }
    }

    public async Task HandleRenameProfile(string oldProfileName, string newProfileName)
    {
        try
        {
            ProfileSingleton.Rename(oldProfileName, newProfileName);
            if (_currentProfileName == oldProfileName)
            {
                await HandleSwitchProfile(newProfileName);
            }
            else
            {
                await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), _currentProfileName));
            }
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to rename profile '{oldProfileName}': {ex.Message}"));
        }
    }

    public async Task HandleDeleteProfile(string profileName)
    {
        try
        {
            ProfileSingleton.Delete(profileName);
            if (_currentProfileName == profileName)
            {
                await HandleSwitchProfile("Default");
            }
            else
            {
                await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), _currentProfileName));
            }
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to delete profile '{profileName}': {ex.Message}"));
        }
    }

    // ── Autopot HP ────────────────────────────────────────────────────────────

    public async Task HandleUpdateAutopotHPSlot(UpdateAutopotHPSlotCommand cmd)
    {
        var hp = ProfileSingleton.GetCurrent().AutopotHP;
        if (cmd.Id < 1 || cmd.Id > hp.HPSlots.Count) return;
        var slot = hp.HPSlots[cmd.Id - 1];
        if (slot == null) return;
        slot.Id = cmd.Id; // ensure ID is correct
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key)) slot.Key = key;
        slot.HPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(hp);
        await BroadcastAsync(BuildAutopotHPConfig());
    }

    public async Task HandleUpdateAutopotHPSettings(UpdateAutopotHPSettingsCommand cmd)
    {
        var hp = ProfileSingleton.GetCurrent().AutopotHP;
        hp.Delay = Math.Max(1, cmd.Delay);
        hp.StopOnCriticalInjury = cmd.StopOnCriticalInjury;
        ProfileSingleton.SetConfiguration(hp);
        await BroadcastAsync(BuildAutopotHPConfig());
    }

    private AutopotHPConfigUpdate BuildAutopotHPConfig()
    {
        var hp = ProfileSingleton.GetCurrent().AutopotHP;
        return new AutopotHPConfigUpdate(
            Slots: hp.HPSlots.Select(s =>
                new AutopotSlotData(s.Id, s.Key.ToString(), s.HPPercent, s.Enabled)).ToList(),
            Delay: hp.Delay,
            StopOnCriticalInjury: hp.StopOnCriticalInjury);
    }

    // ── Autopot SP ────────────────────────────────────────────────────────────

    public async Task HandleUpdateAutopotSPSlot(UpdateAutopotSPSlotCommand cmd)
    {
        var sp = ProfileSingleton.GetCurrent().AutopotSP;
        if (cmd.Id < 1 || cmd.Id > sp.SPSlots.Count) return;
        var slot = sp.SPSlots[cmd.Id - 1];
        if (slot == null) return;
        slot.Id = cmd.Id;
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key)) slot.Key = key;
        slot.SPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(sp);
        await BroadcastAsync(BuildAutopotSPConfig());
    }

    public async Task HandleUpdateAutopotSPSettings(UpdateAutopotSPSettingsCommand cmd)
    {
        var sp = ProfileSingleton.GetCurrent().AutopotSP;
        sp.Delay = Math.Max(1, cmd.Delay);
        ProfileSingleton.SetConfiguration(sp);
        await BroadcastAsync(BuildAutopotSPConfig());
    }

    private AutopotSPConfigUpdate BuildAutopotSPConfig()
    {
        var sp = ProfileSingleton.GetCurrent().AutopotSP;
        return new AutopotSPConfigUpdate(
            Slots: sp.SPSlots.Select(s =>
                new AutopotSlotData(s.Id, s.Key.ToString(), s.SPPercent, s.Enabled)).ToList(),
            Delay: sp.Delay);
    }

    // ── Status Recovery ───────────────────────────────────────────────────────

    public async Task HandleUpdateStatusRecoveryItem(UpdateStatusRecoveryItemCommand cmd)
    {
        var sr = ProfileSingleton.GetCurrent().StatusRecovery;
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
        {
            sr.SetKeyForList(cmd.Name, key);
        }
        ProfileSingleton.SetConfiguration(sr);
        await BroadcastAsync(BuildStatusRecoveryConfig());
    }

    public async Task HandleUpdateStatusRecoverySettings(UpdateStatusRecoverySettingsCommand cmd)
    {
        var sr = ProfileSingleton.GetCurrent().StatusRecovery;
        sr.Delay = Math.Max(1, cmd.Delay);
        ProfileSingleton.SetConfiguration(sr);
        await BroadcastAsync(BuildStatusRecoveryConfig());
    }

    // ── Skill Timer ───────────────────────────────────────────────────────────

    public async Task HandleUpdateSkillTimerSlot(UpdateSkillTimerSlotCommand cmd)
    {
        var st = ProfileSingleton.GetCurrent().SkillTimer;
        if (!st.skillTimer.TryGetValue(cmd.Id, out var slot))
        {
            slot = new SkillTimerKey(System.Windows.Forms.Keys.None, 1000);
            st.skillTimer[cmd.Id] = slot;
        }

        if (Enum.TryParse<System.Windows.Forms.Keys>(cmd.Key, out var parsed))
            slot.Key = parsed;
        
        slot.Delay = cmd.Delay;
        slot.ClickMode = cmd.ClickMode;
        slot.AltKey = cmd.AltKey;
        slot.Enabled = cmd.Enabled;

        ProfileSingleton.SetConfiguration(st);
        
        // If the worker is ON, toggle the specific timer thread
        if (_isOn)
        {
            if (slot.Enabled) st.StartTimer(cmd.Id);
            else st.StopTimer(cmd.Id);
        }

        await PushSkillTimerConfig();
    }

    private async Task PushSkillTimerConfig()
    {
        var st = ProfileSingleton.GetCurrent().SkillTimer;
        var slots = new List<SkillTimerSlotData>();

        for (int i = 1; i <= SkillTimer.MAX_SKILL_TIMERS; i++)
        {
            if (st.skillTimer.TryGetValue(i, out var macro))
            {
                slots.Add(new SkillTimerSlotData(i, macro.Key.ToString(), macro.Delay, macro.ClickMode, macro.AltKey, macro.Enabled));
            }
            else
            {
                slots.Add(new SkillTimerSlotData(i, "None", 1000, 0, false, false));
            }
        }
        await BroadcastAsync(new SkillTimerConfigUpdate(slots));
    }

    private StatusRecoveryConfigUpdate BuildStatusRecoveryConfig()
    {
        var sr = ProfileSingleton.GetCurrent().StatusRecovery;
        return new StatusRecoveryConfigUpdate(
            Items: sr.statusLists.Select(kvp => new StatusRecoveryItemData(kvp.Key, kvp.Value.Key.ToString())).ToList(),
            Delay: sr.Delay);
    }

    // ── Debuff Recovery ───────────────────────────────────────────────────────

    public async Task HandleUpdateDebuffRecoveryItem(UpdateDebuffRecoveryItemCommand cmd)
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (key == Keys.None) dr.RemoveKeyFromBuff(statusId);
                else dr.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(dr);
        await BroadcastAsync(BuildDebuffRecoveryConfig());
    }

    public async Task HandleUpdateDebuffRecoverySettings(UpdateDebuffRecoverySettingsCommand cmd)
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        dr.Delay = Math.Max(1, cmd.Delay);
        ProfileSingleton.SetConfiguration(dr);
        await BroadcastAsync(BuildDebuffRecoveryConfig());
    }

    private DebuffRecoveryConfigUpdate BuildDebuffRecoveryConfig()
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        return new DebuffRecoveryConfigUpdate(
            Items: dr.buffMapping.Select(kvp => new DebuffRecoveryItemData(kvp.Key.ToString(), kvp.Value.ToString())).ToList(),
            Delay: dr.Delay);
    }

    // ── Autobuff Skill ────────────────────────────────────────────────────────

    public async Task HandleUpdateAutobuffSkillItem(UpdateAutobuffSkillItemCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (key == Keys.None) abs.RemoveKeyFromBuff(statusId);
                else abs.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abs);
        await BroadcastAsync(BuildAutobuffSkillConfig());
    }

    public async Task HandleUpdateAutobuffSkillSettings(UpdateAutobuffSkillSettingsCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        abs.Delay = Math.Max(1, cmd.Delay);
        ProfileSingleton.SetConfiguration(abs);
        await BroadcastAsync(BuildAutobuffSkillConfig());
    }

    private AutobuffSkillConfigUpdate BuildAutobuffSkillConfig()
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        var map = abs.buffMapping;
        
        List<AutobuffSkillItemData> Build(List<Buff> buffs) => buffs
            .Select(b => new AutobuffSkillItemData(b.EffectStatusID.ToString(), b.Name, map.TryGetValue(b.EffectStatusID, out var k) ? k.ToString() : "None"))
            .ToList();

        var groups = new List<AutobuffSkillGroupData>
        {
            new AutobuffSkillGroupData("Archer", Build(BuffService.GetArcherBuffs())),
            new AutobuffSkillGroupData("Swordsman", Build(BuffService.GetSwordmanBuffs())),
            new AutobuffSkillGroupData("Mage", Build(BuffService.GetMageBuffs())),
            new AutobuffSkillGroupData("Merchant", Build(BuffService.GetMerchantBuffs())),
            new AutobuffSkillGroupData("Thief", Build(BuffService.GetThiefBuffs())),
            new AutobuffSkillGroupData("Acolyte", Build(BuffService.GetAcolyteBuffs())),
            new AutobuffSkillGroupData("Taekwon", Build(BuffService.GetTaekwonBuffs())),
            new AutobuffSkillGroupData("Ninja", Build(BuffService.GetNinjaBuffs())),
            new AutobuffSkillGroupData("Gunslinger", Build(BuffService.GetGunslingerBuffs())),
            new AutobuffSkillGroupData("Padawan", Build(BuffService.GetPadawanBuffs()))
        };

        return new AutobuffSkillConfigUpdate(groups, abs.Delay);
    }

    // ── Autobuff Item ─────────────────────────────────────────────────────────

    public async Task HandleUpdateAutobuffItemItem(UpdateAutobuffItemCommand cmd)
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                if (key == Keys.None) abi.RemoveKeyFromBuff(statusId);
                else abi.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abi);
        await BroadcastAsync(BuildAutobuffItemConfig());
    }

    public async Task HandleUpdateAutobuffItemSettings(UpdateAutobuffItemSettingsCommand cmd)
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        abi.Delay = Math.Max(1, cmd.Delay);
        ProfileSingleton.SetConfiguration(abi);
        await BroadcastAsync(BuildAutobuffItemConfig());
    }

    // ── Skill Spammer ─────────────────────────────────────────────────────────

    private void HookSkillSpammerEvents()
    {
        var spammer = ProfileSingleton.GetCurrent().SkillSpammer;
        // Unsubscribe first to avoid double-firing if Hook is called multiple times.
        spammer.ToggleModeChanged -= OnSkillSpammerToggleModeChanged;
        spammer.ToggleModeChanged += OnSkillSpammerToggleModeChanged;
    }

    private void OnSkillSpammerToggleModeChanged(object? sender, bool e)
    {
        _ = BroadcastAsync(BuildSkillSpammerConfig());
    }

    public async Task HandleUpdateSkillSpammerEntry(UpdateSkillSpammerEntryCommand cmd)
    {
        var spammer = ProfileSingleton.GetCurrent().SkillSpammer;
        if (Enum.TryParse<Keys>(cmd.KeyName, out var key))
        {
            if (cmd.IsChecked || cmd.IsIndeterminate)
            {
                spammer.AddSkillSpammerEntry(cmd.KeyName, new KeyConfig(key, cmd.IsChecked, cmd.IsIndeterminate));
            }
            else
            {
                spammer.RemoveSkillSpammerEntry(cmd.KeyName);
            }
            ProfileSingleton.SetConfiguration(spammer);
            await BroadcastAsync(BuildSkillSpammerConfig());
        }
    }

    public async Task HandleUpdateSkillSpammerSettings(UpdateSkillSpammerSettingsCommand cmd)
    {
        var spammer = ProfileSingleton.GetCurrent().SkillSpammer;
        spammer.SpammerDelay = cmd.Delay;
        spammer.MouseFlick = cmd.MouseFlick;
        spammer.NoShift = cmd.NoShift;
        spammer.ToggleMode = cmd.ToggleMode;
        
        if (Enum.TryParse<Keys>(cmd.ToggleModeKey, out var toggleKey))
        {
            spammer.ToggleModeKey = toggleKey;
        }

        ProfileSingleton.SetConfiguration(spammer);
        await BroadcastAsync(BuildSkillSpammerConfig());
    }

    public async Task HandleUpdateGlobalConfig(UpdateGlobalConfigCommand cmd)
    {
        var config = ConfigGlobal.GetConfig();
        config.SongRows = cmd.SongRows;
        config.MacroSwitchRows = cmd.MacroSwitchRows;
        config.DefaultToggleStateKey = cmd.DefaultToggleStateKey;
        config.DebugMode = cmd.DebugMode;
        config.DebugModeShowLog = cmd.DebugModeShowLog;
        config.DisableSystray = cmd.DisableSystray;
        config.StartAutoOffTimerOnEnable = cmd.StartAutoOffTimerOnEnable;
        config.ClearAutoOffTimerOnDisable = cmd.ClearAutoOffTimerOnDisable;
        config.PauseWhenChatting = cmd.PauseWhenChatting;
        config.PauseWhenDead = cmd.PauseWhenDead;
        config.ExitWithRo = cmd.ExitWithRo;
        config.AlwaysOnTop = cmd.AlwaysOnTop;
        ConfigGlobal.SaveConfig();
    }

    public async Task HandleUpdateProfileSettings(UpdateProfileSettingsCommand cmd)
    {
        var profile = ProfileSingleton.GetCurrent();
        profile.UserPreferences.StopBuffsCity = cmd.StopBuffsCity;
        profile.UserPreferences.SoundEnabled = cmd.SoundEnabled;
        ProfileSingleton.SetConfiguration(profile.UserPreferences);
    }

    private AutobuffItemConfigUpdate BuildAutobuffItemConfig()
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        var map = abi.buffMapping;

        List<AutobuffItemItemData> Build(List<Buff> buffs) => buffs
            .Select(b => new AutobuffItemItemData(b.EffectStatusID.ToString(), b.Name, map.TryGetValue(b.EffectStatusID, out var kList) && kList.Count > 0 ? kList[0].ToString() : "None"))
            .ToList();

        var groups = new List<AutobuffItemGroupData>
        {
            new AutobuffItemGroupData("Potions", Build(BuffService.GetPotionBuffs())),
            new AutobuffItemGroupData("Elements", Build(BuffService.GetElementBuffs())),
            new AutobuffItemGroupData("Food", Build(BuffService.GetFoodBuffs())),
            new AutobuffItemGroupData("Boxes", Build(BuffService.GetBoxBuffs())),
            new AutobuffItemGroupData("Scrolls", Build(BuffService.GetScrollBuffs())),
            new AutobuffItemGroupData("Etc", Build(BuffService.GetEtcBuffs()))
        };

        if (AppConfig.SupportsFishing)
        {
            groups.Add(new AutobuffItemGroupData("Fish", Build(BuffService.GetFishBuffs())));
        }

        return new AutobuffItemConfigUpdate(groups, abi.Delay);
    }

    // ── Full state ────────────────────────────────────────────────────────────

    public async Task HandleFullStateRequest()
    {
        var client = ClientSingleton.GetClient();
        await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: ProfileSingleton.GetCurrent().UserPreferences.ToggleStateKey));
        await BroadcastAsync(new ClientStateUpdate(
            Connected: client != null,
            ProcessName: client != null ? _GetConnectedProcessName() : null));
        await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), _currentProfileName));
        await BroadcastAsync(new ProcessListUpdate(ListLocalProcesses()));
        await PushAllConfigs();
    }

    private async Task PushAllConfigs()
    {
        await BroadcastAsync(BuildAutopotHPConfig());
        await BroadcastAsync(BuildAutopotSPConfig());
        await BroadcastAsync(BuildStatusRecoveryConfig());
        await BroadcastAsync(BuildDebuffRecoveryConfig());
        await BroadcastAsync(BuildAutobuffSkillConfig());
        await BroadcastAsync(BuildAutobuffItemConfig());
        await BroadcastAsync(BuildSkillSpammerConfig());
        await BroadcastAsync(BuildGlobalConfigUpdate());
        await BroadcastAsync(BuildProfileSettingsUpdate());
    }

    private GlobalConfigUpdate BuildGlobalConfigUpdate()
    {
        var config = ConfigGlobal.GetConfig();
        return new GlobalConfigUpdate(
            config.SongRows,
            config.MacroSwitchRows,
            config.DefaultToggleStateKey,
            config.DebugMode,
            config.DebugModeShowLog,
            config.DisableSystray,
            config.StartAutoOffTimerOnEnable,
            config.ClearAutoOffTimerOnDisable,
            config.PauseWhenChatting,
            config.PauseWhenDead,
            config.ExitWithRo,
            config.AlwaysOnTop
        );
    }

    private ProfileSettingsUpdate BuildProfileSettingsUpdate()
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        return new ProfileSettingsUpdate(
            prefs.StopBuffsCity,
            prefs.SoundEnabled
        );
    }

    private SkillSpammerConfigUpdate BuildSkillSpammerConfig()
    {
        var spammer = ProfileSingleton.GetCurrent().SkillSpammer;
        var entries = spammer.SpammerEntries.Select(kvp => 
            new SkillSpammerKeyData(kvp.Key, kvp.Value.ClickActive, kvp.Value.IsIndeterminate)).ToList();

        return new SkillSpammerConfigUpdate(
            entries,
            spammer.SpammerDelay,
            spammer.MouseFlick,
            spammer.NoShift,
            spammer.ToggleMode,
            spammer.ToggleModeKey.ToString());
    }

    // ── Broadcast ─────────────────────────────────────────────────────────────

    public Task BroadcastAsync<T>(T update) where T : IIpcMessage
        => _server.BroadcastAsync(update);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshToggleHotkey()
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        if (!string.IsNullOrEmpty(prefs?.ToggleStateKey)
            && prefs.ToggleStateKey != AppConfig.TEXT_NONE
            && Enum.TryParse<Keys>(prefs.ToggleStateKey, out Keys toggleKey)
            && toggleKey != Keys.None)
        {
            KeyboardHook.KeyDown = null;
            KeyboardHook.AddKeyDown(toggleKey, () =>
            {
                if (_isOn) _ = HandleTurnOff();
                else _ = HandleTurnOn();
                return true;
            });
        }
        else
        {
            KeyboardHook.KeyDown = null;
        }
    }

    private string? _GetConnectedProcessName()
    {
        try { return ClientSingleton.GetClient()?.Process?.ProcessName; }
        catch { return null; }
    }
}