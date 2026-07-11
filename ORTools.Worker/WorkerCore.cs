using ORTools.Shared.Protocol;
using ORTools.Worker.IPC;

namespace ORTools.Worker;

public sealed class WorkerCore
{
    public event Action<IIpcMessage>? OnBroadcast;

    private readonly CommandDispatcher _dispatcher;
    private readonly StatePublisher _statePublisher;
    private readonly Thread _hookThread;

    private bool _isOn;
    private string _currentProfileName = "Default";
    private readonly AutoOff _autoOff;

    public WorkerCore()
    {
        ConfigGlobal.Initialize();
        Server.Initialize();
        ClientListSingleton.StartCleanupMonitor();

        // Load server configs into ClientListSingleton
        foreach (var dto in Server.GetLocalClients())
        {
            ClientListSingleton.AddClient(new Client(dto));
        }

        ProfileSingleton.Create("Default");
        
        var lastProfile = ConfigGlobal.GetConfig().LastUsedProfile;
        if (string.IsNullOrEmpty(lastProfile)) lastProfile = "Default";
        
        try
        {
            ProfileSingleton.Load(lastProfile);
            _currentProfileName = lastProfile;
        }
        catch
        {
            ProfileSingleton.Load("Default");
            _currentProfileName = "Default";
        }
        
        HookSkillSpammerEvents();

        ClientListSingleton.OnClientRemoved += (removedClient) =>
        {
            if (ClientSingleton.GetClient() == removedClient)
            {
                if (_isOn)
                {
                    DebugLogger.Debug("Client process closed. Automation disabled.");
                }
                _ = HandleDisconnectClient();
            }
        };

        _autoOff = new AutoOff();
        _autoOff.TimerStarted += (s, e) => _ = BroadcastAsync(new AutoOffTimerStateUpdate(e.IsTimerRunning, e.IsPaused, e.SelectedMinutes, e.RemainingSeconds, e.RunningMinutes));
        _autoOff.TimerStopped += (s, e) => _ = BroadcastAsync(new AutoOffTimerStateUpdate(e.IsTimerRunning, e.IsPaused, e.SelectedMinutes, e.RemainingSeconds, e.RunningMinutes));
        _autoOff.TimerTick += (s, e) => _ = BroadcastAsync(new AutoOffTimerStateUpdate(e.IsTimerRunning, e.IsPaused, e.SelectedMinutes, e.RemainingSeconds, e.RunningMinutes));
        _autoOff.TimerPaused += (s, e) => _ = BroadcastAsync(new AutoOffTimerStateUpdate(e.IsTimerRunning, e.IsPaused, e.SelectedMinutes, e.RemainingSeconds, e.RunningMinutes));
        _autoOff.TimerResumed += (s, e) => _ = BroadcastAsync(new AutoOffTimerStateUpdate(e.IsTimerRunning, e.IsPaused, e.SelectedMinutes, e.RemainingSeconds, e.RunningMinutes));
        _autoOff.TimerCompleted += (s, e) => 
        {
            _ = BroadcastAsync(new AutoOffTimerStateUpdate(e.IsTimerRunning, e.IsPaused, e.SelectedMinutes, e.RemainingSeconds, e.RunningMinutes));
            if (_isOn)
            {
                _ = HandleTurnOff();
                WeightLimitMacro.SendOverweightMacro();
            }
        };

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
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"[WorkerCore] Running in-memory IPC  Mode: {AppConfig.GetRateTag()}");
        Win32Interop.timeBeginPeriod(1);
        try
        {
            // Block until cancelled
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (TaskCanceledException) { }
        finally
        {
            Win32Interop.timeEndPeriod(1);
            KeyboardHook.Disable();
            await HandleTurnOff();
        }
    }

    public Task HandleCommandAsync(IIpcMessage command)
    {
        return _dispatcher.HandleAsync(command);
    }

    public Task BroadcastAsync(IIpcMessage msg)
    {
        OnBroadcast?.Invoke(msg);
        return Task.CompletedTask;
    }

    // ── Turn on/off ───────────────────────────────────────────────────────────

    public async Task HandleTurnOn()
    {
        var client = ClientSingleton.GetClient();
        if (client == null || client.Process == null || client.Process.HasExited)
        {
            await BroadcastAsync(new ErrorUpdate("Client is disconnected or has exited.")); return;
        }
        _isOn = true;
        var p = ProfileSingleton.GetCurrent();
        await Task.Delay(300);
        p.StartAll();
        
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        if (prefs.StartAutoOffTimerOnEnable && !_autoOff.IsTimerRunning)
        {
            _autoOff.StartTimer();
        }
        
        _autoOff.StartOverweightMonitor();
        
        try
        {
            await BroadcastAsync(new AppStateUpdate(IsOn: true, ToggleKey: p.UserPreferences.ToggleStateKey, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
            DebugLogger.Info("[WorkerCore] Turned ON");
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[WorkerCore] HandleTurnOn broadcast failed: {ex.GetType().Name}: {ex.Message}");
        }

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
        try
        {
            p.StopAll();
            
            var prefs = p.UserPreferences;
            if (prefs.ClearAutoOffTimerOnDisable && _autoOff.IsTimerRunning)
            {
                _autoOff.StopTimer();
            }
            
            _autoOff.StopOverweightMonitor();
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[WorkerCore] Error during TurnOff cleanup: {ex.Message}");
        }
        
        try
        {
            await BroadcastAsync(new AppStateUpdate(IsOn: false, ToggleKey: p.UserPreferences.ToggleStateKey, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
            DebugLogger.Info("[WorkerCore] Turned OFF");
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"[WorkerCore] HandleTurnOff broadcast failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (p.UserPreferences.SoundEnabled)
        {
            try { new System.Media.SoundPlayer(@"Resources\toggle_off.wav").Play(); }
            catch (Exception ex) { DebugLogger.Warning($"Failed to play sound: {ex.Message}"); }
        }
    }


    public async Task HandleUpdateToggleKey(string keyStr)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(keyStr, ignoreCase: true, out var key)) unbindChanged = UnbindKeyGlobally(key);
        prefs.ToggleStateKey = keyStr;
        ProfileSingleton.SetConfiguration(prefs);
        RefreshToggleHotkey();
        
        if (unbindChanged) await PushAllConfigs();
        
        await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: keyStr, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
    }

    public async Task HandleRequestProcessList()
    {
        await BroadcastAsync(new ProcessListUpdate(Utils.ProcessManager.GetActiveProcesses()));
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
            ClientListSingleton.AddClient(client);
            await BroadcastAsync(new ClientStateUpdate(Connected: true, ProcessName: processName));
            DebugLogger.Info($"[WorkerCore] Client connected: {processName}");
        }
        catch (Exception ex) { await BroadcastAsync(new ErrorUpdate($"Connect failed: {ex.Message}")); }
    }

    public async Task HandleDisconnectClient()
    {
        if (_isOn) await HandleTurnOff();
        var client = ClientSingleton.GetClient();
        if (client != null) ClientListSingleton.RemoveClient(client);
        ClientSingleton.SetClient(null);
        _statePublisher.ClearCache();
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
            _autoOff.LoadFromProfile();
            _currentProfileName = profileName;
            ConfigGlobal.GetConfig().LastUsedProfile = profileName;
            ConfigGlobal.SaveConfig();
            RefreshToggleHotkey();
            await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), profileName));
            await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: ProfileSingleton.GetCurrent().UserPreferences.ToggleStateKey, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
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
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            slot.Key = key;
        }
        slot.HPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(hp);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutopotHPConfig());
    }

    public async Task HandleUpdateAutopotHPSettings(UpdateAutopotHPSettingsCommand cmd)
    {
        var hp = ProfileSingleton.GetCurrent().AutopotHP;
        hp.Delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
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
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            slot.Key = key;
        }
        slot.SPPercent = Math.Clamp(cmd.Percent, 0, 100);
        slot.Enabled = cmd.Enabled;
        ProfileSingleton.SetConfiguration(sp);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutopotSPConfig());
    }

    public async Task HandleUpdateAutopotSPSettings(UpdateAutopotSPSettingsCommand cmd)
    {
        var sp = ProfileSingleton.GetCurrent().AutopotSP;
        sp.Delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
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
        sr.Delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
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
        bool unbindChanged = false;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                unbindChanged = UnbindKeyGlobally(key);
                if (key == Keys.None) dr.RemoveKeyFromBuff(statusId);
                else dr.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(dr);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildDebuffRecoveryConfig());
    }

    public async Task HandleUpdateDebuffRecoverySettings(UpdateDebuffRecoverySettingsCommand cmd)
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        dr.Delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
        ProfileSingleton.SetConfiguration(dr);
        await BroadcastAsync(BuildDebuffRecoveryConfig());
    }

    private DebuffRecoveryConfigUpdate BuildDebuffRecoveryConfig()
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        var update = new DebuffRecoveryConfigUpdate(
            Items: BuffService.GetDebuffs().Select(b => new DebuffRecoveryItemData(b.EffectStatusID.ToString(), dr.buffMapping.TryGetValue(b.EffectStatusID, out var key) ? key.ToString() : "None", b.IconName)).ToList(),
            Delay: dr.Delay
        );
        return update;
    }

    // ── Autobuff Skill ────────────────────────────────────────────────────────

    public async Task HandleUpdateAutobuffSkillItem(UpdateAutobuffSkillItemCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        bool unbindChanged = false;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                unbindChanged = UnbindKeyGlobally(key);
                if (key == Keys.None) abs.RemoveKeyFromBuff(statusId);
                else abs.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abs);
        if (unbindChanged) await PushAllConfigs();
        else {
            await BroadcastAsync(BuildAutobuffSkillConfig());
            await BroadcastAsync(BuildAutobuffOrderConfig());
        }
    }

    public async Task HandleUpdateAutobuffSkillSettings(UpdateAutobuffSkillSettingsCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        abs.Delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
        ProfileSingleton.SetConfiguration(abs);
        await BroadcastAsync(BuildAutobuffSkillConfig());
    }

    private AutobuffSkillConfigUpdate BuildAutobuffSkillConfig()
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        var map = abs.buffMapping;
        
        List<AutobuffSkillItemData> Build(List<Buff> buffs) => buffs
            .Select(b => new AutobuffSkillItemData(b.EffectStatusID.ToString(), b.Name, map.TryGetValue(b.EffectStatusID, out var k) ? k.ToString() : "None", b.IconName))
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
        bool unbindChanged = false;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            {
                unbindChanged = UnbindKeyGlobally(key);
                abi.RemoveKeyFromBuff(statusId);
                if (key != Keys.None) abi.AddKeyToBuff(statusId, key);
            }
        }
        ProfileSingleton.SetConfiguration(abi);
        if (unbindChanged) await PushAllConfigs();
        else {
            await BroadcastAsync(BuildAutobuffItemConfig());
            await BroadcastAsync(BuildAutobuffOrderConfig());
        }
    }

    public async Task HandleUpdateAutobuffItemSettings(UpdateAutobuffItemSettingsCommand cmd)
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        abi.Delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
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
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.KeyName, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            if (cmd.IsChecked || cmd.IsIndeterminate)
            {
                spammer.AddSkillSpammerEntry(cmd.KeyName, new KeyConfig(key, cmd.IsChecked, cmd.IsIndeterminate));
            }
            else
            {
                spammer.RemoveSkillSpammerEntry(cmd.KeyName);
            }
            ProfileSingleton.SetConfiguration(spammer);
            if (unbindChanged) await PushAllConfigs();
            else await BroadcastAsync(BuildSkillSpammerConfig());
        }
    }

    public async Task HandleUpdateSkillSpammerSettings(UpdateSkillSpammerSettingsCommand cmd)
    {
        var spammer = ProfileSingleton.GetCurrent().SkillSpammer;
        spammer.SpammerDelay = cmd.Delay;
        spammer.MouseFlick = cmd.MouseFlick;
        spammer.NoShift = cmd.NoShift;
        spammer.ToggleMode = cmd.ToggleMode;
        
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.ToggleModeKey, out var toggleKey))
        {
            unbindChanged = UnbindKeyGlobally(toggleKey);
            spammer.ToggleModeKey = toggleKey;
        }

        ProfileSingleton.SetConfiguration(spammer);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildSkillSpammerConfig());
    }

    // ── Auto Off ──────────────────────────────────────────────────────────────
    
    public async Task HandleUpdateAutoOffSettings(UpdateAutoOffSettingsCommand cmd)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        prefs.AutoOffOverweight = cmd.AutoOffOverweight;
        prefs.AutoOffOverweightMode = (ConfigProfile.OverweightAutoOffMode)cmd.AutoOffOverweightMode;
        
        bool unbindChanged = false;
        
        if (Enum.TryParse<Keys>(cmd.AutoOffKey1, out var key1)) { unbindChanged |= UnbindKeyGlobally(key1); prefs.AutoOffKey1 = key1; }
        if (Enum.TryParse<Keys>(cmd.AutoOffKey2, out var key2)) { unbindChanged |= UnbindKeyGlobally(key2); prefs.AutoOffKey2 = key2; }
        if (Enum.TryParse<Keys>(cmd.Ammo1Key, out var am1)) { unbindChanged |= UnbindKeyGlobally(am1); prefs.Ammo1Key = am1; }
        if (Enum.TryParse<Keys>(cmd.Ammo2Key, out var am2)) { unbindChanged |= UnbindKeyGlobally(am2); prefs.Ammo2Key = am2; }

        prefs.AutoOffKillClient = cmd.AutoOffKillClient;
        prefs.SwitchAmmo = cmd.SwitchAmmo;
        prefs.AutoOffTime = cmd.AutoOffTime;

        _autoOff.SetTime(prefs.AutoOffTime);

        ProfileSingleton.SetConfiguration(prefs);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildAutoOffConfig());
    }

    public async Task HandleUpdateGlobalConfig(UpdateGlobalConfigCommand cmd)
    {
        var config = ConfigGlobal.GetConfig();
        config.SongRows = Math.Max(1, cmd.SongRows);
        config.MacroSwitchRows = Math.Max(1, cmd.MacroSwitchRows);
        config.AtkDefRows = Math.Max(1, cmd.AtkDefRows);
        config.DefaultToggleStateKey = cmd.DefaultToggleStateKey;
        config.DebugMode = cmd.DebugMode;
        config.DebugView = cmd.DebugView;
        config.DebugViewHeight = Math.Clamp(cmd.DebugViewHeight, AppConfig.MinDebugViewHeight, AppConfig.MaxDebugViewHeight);
        config.DebugClientLog = cmd.DebugClientLog;
        config.DisableSystray = cmd.DisableSystray;
        config.MinimizeToSystray = cmd.MinimizeToSystray;
        config.CloseToSystray = cmd.CloseToSystray;
        config.PauseWhenChatting = cmd.PauseWhenChatting;
        config.PauseWhenDead = cmd.PauseWhenDead;
        config.ExitWithRo = cmd.ExitWithRo;
        config.AlwaysOnTop = cmd.AlwaysOnTop;
        config.AllowResizingWindow = cmd.AllowResizingWindow;
        config.ShowExpPerHour = cmd.ShowExpPerHour;
        config.Theme = cmd.Theme;
        ConfigGlobal.SaveConfig();

        var p = ProfileSingleton.GetCurrent();
        p.ATKDEFMode.EnsureCorrectRowCount(config.AtkDefRows);
        ProfileSingleton.SetConfiguration(p.ATKDEFMode);

        await BroadcastAsync(BuildGlobalConfigUpdate());
        await BroadcastAsync(BuildMacroSongConfig());
        await BroadcastAsync(BuildMacroSwitchConfig());
        await BroadcastAsync(BuildAtkDefConfig());
    }

    public Task HandleUpdateStatusLoggerConfig(UpdateStatusLoggerConfigCommand cmd)
    {
        var config = ConfigGlobal.GetConfig().StatusLogger;
        config.LogToFile = cmd.LogToFile;
        config.LogFrequency = cmd.LogFrequency;
        config.LogName = cmd.LogName;
        config.LogLevel = cmd.LogLevel;
        config.LogJobLevel = cmd.LogJobLevel;
        config.LogExp = cmd.LogExp;
        config.LogHp = cmd.LogHp;
        config.LogMaxHp = cmd.LogMaxHp;
        config.LogSp = cmd.LogSp;
        config.LogMaxSp = cmd.LogMaxSp;
        config.LogWeight = cmd.LogWeight;
        config.LogMaxWeight = cmd.LogMaxWeight;
        config.LogMap = cmd.LogMap;
        config.LogStatuses = cmd.LogStatuses;
        ConfigGlobal.SaveConfig();
        _ = BroadcastAsync(BuildStatusLoggerConfigUpdate());
        return Task.CompletedTask;
    }

    public Task HandleUpdateProfileSettings(UpdateProfileSettingsCommand cmd)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        prefs.StopBuffsCity = cmd.StopBuffsCity;
        prefs.SoundEnabled = cmd.SoundEnabled;
        prefs.StartAutoOffTimerOnEnable = cmd.StartAutoOffTimerOnEnable;
        prefs.ClearAutoOffTimerOnDisable = cmd.ClearAutoOffTimerOnDisable;
        prefs.KeepDeadClientInfo = cmd.KeepDeadClientInfo;
        ProfileSingleton.SetConfiguration(prefs);
        return Task.CompletedTask;
    }

    public async Task HandleUpdateTransferHelper(UpdateTransferHelperCommand cmd)
    {
        var th = ProfileSingleton.GetCurrent().TransferHelper;
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.TransferKey, ignoreCase: true, out var key)) unbindChanged = UnbindKeyGlobally(key);
        th.TransferKey = key;
        ProfileSingleton.SetConfiguration(th);
        
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildTransferHelperConfig());
    }

    // ── Macro Switch ──────────────────────────────────────────────────────────

    public async Task HandleUpdateMacroSwitchTrigger(UpdateMacroSwitchTriggerCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        if (cmd.RowId < 1 || cmd.RowId > p.MacroSwitch.ChainConfigs.Count) return;
        
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.TriggerKey, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            p.MacroSwitch.ChainConfigs[cmd.RowId - 1].TriggerKey = key;
        }
        
        ProfileSingleton.SetConfiguration(p.MacroSwitch);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildMacroSwitchConfig());
    }

    public async Task HandleUpdateMacroSwitchStep(UpdateMacroSwitchStepCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        if (cmd.RowId < 1 || cmd.RowId > p.MacroSwitch.ChainConfigs.Count) return;
        var chain = p.MacroSwitch.ChainConfigs[cmd.RowId - 1];
        if (cmd.StepId < 1 || cmd.StepId > chain.macroEntries.Count) return;
        
        var step = chain.macroEntries[cmd.StepId - 1];
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.Key, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            step.Key = key;
        }
        step.Delay = cmd.Delay;
        step.ClickMode = cmd.ClickMode;

        ProfileSingleton.SetConfiguration(p.MacroSwitch);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildMacroSwitchConfig());
    }

    public async Task HandleResetMacroSwitchRow(ResetMacroSwitchRowCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        if (cmd.RowId < 1 || cmd.RowId > p.MacroSwitch.ChainConfigs.Count) return;
        p.MacroSwitch.ResetMacro(cmd.RowId);
        ProfileSingleton.SetConfiguration(p.MacroSwitch);
        await BroadcastAsync(BuildMacroSwitchConfig());
    }

    // ── Macro Song ────────────────────────────────────────────────────────────

    private MacroSongConfigUpdate BuildMacroSongConfig()
    {
        var config = ConfigGlobal.GetConfig();
        var p = ProfileSingleton.GetCurrent();
        var rows = p.SongMacro.SongRows
            .Take(config.SongRows)
            .Select(r => new MacroSongRowData(
            r.Id,
            r.TriggerKey.ToString(),
            r.AdaptationKey.ToString(),
            r.InstrumentKey.ToString(),
            r.Delay,
            r.SongSequence.Select(k => k.ToString()).ToList()
        )).ToList();
        return new MacroSongConfigUpdate(rows);
    }

    public async Task HandleUpdateMacroSongTrigger(UpdateMacroSongTriggerCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.SongMacro.GetSongRow(cmd.RowId);
        if (row == null) return;
        
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.TriggerKey, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            row.TriggerKey = key;
        }
        
        ProfileSingleton.SetConfiguration(p.SongMacro);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildMacroSongConfig());
    }

    public async Task HandleUpdateMacroSongStep(UpdateMacroSongStepCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.SongMacro.GetSongRow(cmd.RowId);
        if (row == null || cmd.StepId < 1 || cmd.StepId > row.SongSequence.Length) return;
        
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.Key, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            row.SongSequence[cmd.StepId - 1] = key;
        }
        
        ProfileSingleton.SetConfiguration(p.SongMacro);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildMacroSongConfig());
    }



    // ── ATK x DEF ─────────────────────────────────────────────────────────────────

    public async Task HandleUpdateAtkDefTrigger(UpdateAtkDefTriggerCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.ATKDEFMode.EquipConfigs.FirstOrDefault(x => x.Id == cmd.RowId);
        if (row != null)
        {
            row.KeySpammer = cmd.TriggerKey;
            ProfileSingleton.SetConfiguration(p.ATKDEFMode);
            await BroadcastAsync(BuildAtkDefConfig());
        }
    }

    public async Task HandleUpdateAtkDefSpammerDelay(UpdateAtkDefSpammerDelayCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.ATKDEFMode.EquipConfigs.FirstOrDefault(x => x.Id == cmd.RowId);
        if (row != null)
        {
            row.KeySpammerDelay = Math.Max(0, cmd.Delay);
            ProfileSingleton.SetConfiguration(p.ATKDEFMode);
            await BroadcastAsync(BuildAtkDefConfig());
        }
    }

    public async Task HandleUpdateAtkDefSwitchDelay(UpdateAtkDefSwitchDelayCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.ATKDEFMode.EquipConfigs.FirstOrDefault(x => x.Id == cmd.RowId);
        if (row != null)
        {
            row.SwitchDelay = Math.Max(0, cmd.Delay);
            ProfileSingleton.SetConfiguration(p.ATKDEFMode);
            await BroadcastAsync(BuildAtkDefConfig());
        }
    }

    public async Task HandleUpdateAtkDefClick(UpdateAtkDefClickCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.ATKDEFMode.EquipConfigs.FirstOrDefault(x => x.Id == cmd.RowId);
        if (row != null)
        {
            row.KeySpammerWithClick = cmd.Click;
            ProfileSingleton.SetConfiguration(p.ATKDEFMode);
            await BroadcastAsync(BuildAtkDefConfig());
        }
    }

    public async Task HandleUpdateAtkDefEquip(UpdateAtkDefEquipCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.ATKDEFMode.EquipConfigs.FirstOrDefault(x => x.Id == cmd.RowId);
        if (row != null)
        {
            var dict = string.Equals(cmd.Category, "DEF", StringComparison.OrdinalIgnoreCase) ? row.DefKeys : row.AtkKeys;
            
            if (cmd.Key == "None" || string.IsNullOrWhiteSpace(cmd.Key))
            {
                dict.TryRemove(cmd.SlotName, out var _);
            }
            else
            {
                dict[cmd.SlotName] = cmd.Key;
            }
            ProfileSingleton.SetConfiguration(p.ATKDEFMode);
            await BroadcastAsync(BuildAtkDefConfig());
        }
    }

    public async Task HandleResetAtkDefRow(ResetAtkDefRowCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.ATKDEFMode.EquipConfigs.FirstOrDefault(x => x.Id == cmd.RowId);
        if (row != null)
        {
            row.KeySpammer = "None";
            row.KeySpammerDelay = AppConfig.ATKDEFSpammerDefaultDelay;
            row.SwitchDelay = AppConfig.ATKDEFSwitchDefaultDelay;
            row.KeySpammerWithClick = true;
            row.AtkKeys.Clear();
            row.DefKeys.Clear();
            ProfileSingleton.SetConfiguration(p.ATKDEFMode);
            await BroadcastAsync(BuildAtkDefConfig());
        }
    }

    private AtkDefConfigUpdate BuildAtkDefConfig()
    {
        var config = ConfigGlobal.GetConfig();
        var p = ProfileSingleton.GetCurrent();
        var rows = p.ATKDEFMode.EquipConfigs
            .Take(config.AtkDefRows)
            .Select(c => new AtkDefRowData(
                c.Id,
                c.KeySpammer,
                c.KeySpammerDelay,
                c.SwitchDelay,
                c.KeySpammerWithClick,
                new Dictionary<string, string>(c.AtkKeys),
                new Dictionary<string, string>(c.DefKeys)
            )).ToList();
        return new AtkDefConfigUpdate(rows);
    }

    public async Task HandleUpdateMacroSongAdaptation(UpdateMacroSongAdaptationCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.SongMacro.GetSongRow(cmd.RowId);
        if (row == null) return;
        
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.AdaptationKey, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            row.AdaptationKey = key;
        }
        
        ProfileSingleton.SetConfiguration(p.SongMacro);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildMacroSongConfig());
    }

    public async Task HandleUpdateMacroSongInstrument(UpdateMacroSongInstrumentCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.SongMacro.GetSongRow(cmd.RowId);
        if (row == null) return;
        
        bool unbindChanged = false;
        if (Enum.TryParse<Keys>(cmd.InstrumentKey, out var key))
        {
            unbindChanged = UnbindKeyGlobally(key);
            row.InstrumentKey = key;
        }
        
        ProfileSingleton.SetConfiguration(p.SongMacro);
        if (unbindChanged) await PushAllConfigs();
        else await BroadcastAsync(BuildMacroSongConfig());
    }

    public async Task HandleUpdateMacroSongDelay(UpdateMacroSongDelayCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        var row = p.SongMacro.GetSongRow(cmd.RowId);
        if (row == null) return;
        
        row.Delay = cmd.Delay;
        ProfileSingleton.SetConfiguration(p.SongMacro);
        await BroadcastAsync(BuildMacroSongConfig());
    }

    public async Task HandleResetMacroSongRow(ResetMacroSongRowCommand cmd)
    {
        var p = ProfileSingleton.GetCurrent();
        p.SongMacro.ResetSongRow(cmd.RowId);
        ProfileSingleton.SetConfiguration(p.SongMacro);
        await BroadcastAsync(BuildMacroSongConfig());
    }

    public Task HandleToggleAutoOffTimer(ToggleAutoOffTimerCommand cmd)
    {
        if (cmd.Start)
        {
            if (_isOn)
            {
                _autoOff.StartTimer();
            }
            else
            {
                _ = BroadcastAsync(new AutoOffTimerStateUpdate(false, false, _autoOff.SelectedMinutes, 0, 0));
            }
        }
        else
        {
            _autoOff.StopTimer();
        }
        return Task.CompletedTask;
    }

    public Task HandlePauseAutoOffTimer(PauseAutoOffTimerCommand cmd)
    {
        if (cmd.Pause)
        {
            _autoOff.PauseTimer();
        }
        else
        {
            if (_isOn)
            {
                _autoOff.ResumeTimer();
            }
            else
            {
                _ = BroadcastAsync(new AutoOffTimerStateUpdate(_autoOff.IsTimerRunning, true, _autoOff.SelectedMinutes, _autoOff.RemainingSeconds, _autoOff.RunningMinutes));
            }
        }
        return Task.CompletedTask;
    }

    private AutobuffItemConfigUpdate BuildAutobuffItemConfig()
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        var map = abi.buffMapping;

        List<AutobuffItemItemData> Build(List<Buff> buffs) => buffs
            .Select(b => new AutobuffItemItemData(b.EffectStatusID.ToString(), b.Name, map.TryGetValue(b.EffectStatusID, out var kList) && kList.Count > 0 ? kList[0].ToString() : "None", b.IconName))
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
    public async Task HandleUpdateAutobuffOrder(UpdateAutobuffOrderCommand cmd)
    {
        var profile = ProfileSingleton.GetCurrent();
        var abs = profile.AutobuffSkill;
        var abi = profile.AutobuffItem;
        
        var newSkillMapping = new Dictionary<EffectStatusIDs, Keys>();
        var newItemMapping = new Dictionary<EffectStatusIDs, List<Keys>>();
        
        foreach (var statusStr in cmd.OrderedStatusNames)
        {
            if (Enum.TryParse<EffectStatusIDs>(statusStr, out var statusId))
            {
                if (abs.buffMapping.TryGetValue(statusId, out var sk))
                    newSkillMapping[statusId] = sk;
                if (abi.buffMapping.TryGetValue(statusId, out var ik))
                    newItemMapping[statusId] = ik;
            }
        }
        
        abs.SetBuffMapping(newSkillMapping);
        abi.buffMapping = new System.Collections.Concurrent.ConcurrentDictionary<EffectStatusIDs, List<Keys>>(newItemMapping);
        profile.UnifiedAutobuffOrder = cmd.OrderedStatusNames;
        
        ProfileSingleton.SetConfiguration(abs);
        ProfileSingleton.SetConfiguration(abi);
        ProfileSingleton.SaveUnifiedAutobuffOrder();
        
        await BroadcastAsync(BuildAutobuffOrderConfig());
    }

    private AutobuffOrderConfigUpdate BuildAutobuffOrderConfig()
    {
        var profile = ProfileSingleton.GetCurrent();
        var abs = profile.AutobuffSkill;
        var abi = profile.AutobuffItem;

        var items = new List<AutobuffOrderItemData>();
        var addedStatuses = new HashSet<EffectStatusIDs>();

        // Migrate legacy AutoBuffOrder if UnifiedAutobuffOrder is empty
        if (profile.UnifiedAutobuffOrder.Count == 0 && profile.UserPreferences.AutoBuffOrder.Count > 0)
        {
            profile.UnifiedAutobuffOrder = profile.UserPreferences.AutoBuffOrder.Select(x => x.ToString()).ToList();
            ProfileSingleton.SetConfiguration(profile.UserPreferences); // Save the profile
        }

        // First, add items based on the saved unified order
        foreach (var statusStr in profile.UnifiedAutobuffOrder)
        {
            if (Enum.TryParse<EffectStatusIDs>(statusStr, out var statusId))
            {
                if (abs.buffMapping.TryGetValue(statusId, out var sk) && !addedStatuses.Contains(statusId))
                {
                    var buff = BuffService.GetBuff(statusId);
                    items.Add(new AutobuffOrderItemData(statusId.ToString(), buff?.Name ?? statusId.ToString(), sk.ToString(), "Skill", buff?.IconName ?? "None"));
                    addedStatuses.Add(statusId);
                }
                
                if (abi.buffMapping.TryGetValue(statusId, out var ik) && !addedStatuses.Contains(statusId))
                {
                    var buff = BuffService.GetBuff(statusId);
                    items.Add(new AutobuffOrderItemData(statusId.ToString(), buff?.Name ?? statusId.ToString(), string.Join(", ", ik), "Item", buff?.IconName ?? "None"));
                    addedStatuses.Add(statusId);
                }
            }
        }

        // Then, append any buffs that were newly added and aren't in the saved order yet
        foreach (var kvp in abs.buffMapping)
        {
            if (!addedStatuses.Contains(kvp.Key))
            {
                var buff = BuffService.GetBuff(kvp.Key);
                items.Add(new AutobuffOrderItemData(kvp.Key.ToString(), buff?.Name ?? kvp.Key.ToString(), kvp.Value.ToString(), "Skill", buff?.IconName ?? "None"));
                profile.UnifiedAutobuffOrder.Add(kvp.Key.ToString());
                addedStatuses.Add(kvp.Key);
            }
        }
        foreach (var kvp in abi.buffMapping)
        {
            if (!addedStatuses.Contains(kvp.Key))
            {
                var buff = BuffService.GetBuff(kvp.Key);
                items.Add(new AutobuffOrderItemData(kvp.Key.ToString(), buff?.Name ?? kvp.Key.ToString(), string.Join(", ", kvp.Value), "Item", buff?.IconName ?? "None"));
                profile.UnifiedAutobuffOrder.Add(kvp.Key.ToString());
                addedStatuses.Add(kvp.Key);
            }
        }

        return new AutobuffOrderConfigUpdate(items);
    }

    // ── Full state ────────────────────────────────────────────────────────────

    public async Task HandleFullStateRequest()
    {
        var client = ClientSingleton.GetClient();
        await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: ProfileSingleton.GetCurrent().UserPreferences.ToggleStateKey, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
        await BroadcastAsync(new ClientStateUpdate(
            Connected: client != null,
            ProcessName: client != null ? _GetConnectedProcessName() : null));
        await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), _currentProfileName));
        await BroadcastAsync(new ProcessListUpdate(ORTools.Worker.Utils.ProcessManager.GetActiveProcesses()));
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
        await BroadcastAsync(BuildAutobuffOrderConfig());
        await BroadcastAsync(BuildSkillSpammerConfig());
        await PushSkillTimerConfig();
        await BroadcastAsync(BuildAutoOffConfig());
        await BroadcastAsync(new AutoOffTimerStateUpdate(_autoOff.IsTimerRunning, _autoOff.IsPaused, _autoOff.SelectedMinutes, _autoOff.RemainingSeconds, _autoOff.RunningMinutes));
        await BroadcastAsync(BuildGlobalConfigUpdate());
        await BroadcastAsync(BuildStatusLoggerConfigUpdate());
        await BroadcastAsync(BuildProfileSettingsUpdate());
        await BroadcastAsync(BuildTransferHelperConfig());
        await BroadcastAsync(BuildMacroSwitchConfig());
        await BroadcastAsync(BuildMacroSongConfig());
        await BroadcastAsync(BuildAtkDefConfig());
    }

    private TransferHelperConfigUpdate BuildTransferHelperConfig()
    {
        return new TransferHelperConfigUpdate(ProfileSingleton.GetCurrent().TransferHelper.TransferKey.ToString());
    }

    private string GetAppTitle()
    {
        string mode = AppConfig.IsHighRate ? "HR" : "MR";
        string title = $"{AppConfig.Name} {AppConfig.Version}/{mode}";
        if (AppConfig.preRelease)
        {
            title += $" ({AppConfig.preReleaseTag})";
        }
        return title;
    }

    private MacroSwitchConfigUpdate BuildMacroSwitchConfig()
    {
        var config = ConfigGlobal.GetConfig();
        var p = ProfileSingleton.GetCurrent();
        var chains = p.MacroSwitch.ChainConfigs
            .Take(config.MacroSwitchRows)
            .Select(chain => new MacroSwitchChainData(
            chain.id,
            chain.TriggerKey.ToString(),
            chain.macroEntries.Select(step => new MacroSwitchStepData(
                step.Key.ToString(),
                step.Delay,
                step.ClickMode
            )).ToList()
        )).ToList();
        return new MacroSwitchConfigUpdate(chains);
    }

    private AutoOffConfigUpdate BuildAutoOffConfig()
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        return new AutoOffConfigUpdate(
            prefs.AutoOffOverweight,
            (int)prefs.AutoOffOverweightMode,
            prefs.AutoOffKey1.ToString(),
            prefs.AutoOffKey2.ToString(),
            prefs.AutoOffKillClient,
            prefs.SwitchAmmo,
            prefs.Ammo1Key.ToString(),
            prefs.Ammo2Key.ToString(),
            prefs.AutoOffTime
        );
    }

    private GlobalConfigUpdate BuildGlobalConfigUpdate()
    {
        var config = ConfigGlobal.GetConfig();
        return new GlobalConfigUpdate(
            config.SongRows,
            config.MacroSwitchRows,
            config.AtkDefRows,
            config.DefaultToggleStateKey,
            config.DebugMode,
            config.DebugView,
            config.DebugViewHeight,
            config.DebugClientLog,
            config.DisableSystray,
            config.MinimizeToSystray,
            config.CloseToSystray,
            config.PauseWhenChatting,
            config.PauseWhenDead,
            config.ExitWithRo,
            config.AlwaysOnTop,
            config.AllowResizingWindow,
            config.ShowExpPerHour,
            config.Theme
        );
    }

    private StatusLoggerConfigUpdate BuildStatusLoggerConfigUpdate()
    {
        var cfg = ConfigGlobal.GetConfig().StatusLogger;
        return new StatusLoggerConfigUpdate(
            cfg.LogToFile, cfg.LogFrequency, AppConfig.ExpLogFile,
            cfg.LogName, cfg.LogLevel, cfg.LogJobLevel, cfg.LogExp,
            cfg.LogHp, cfg.LogMaxHp, cfg.LogSp, cfg.LogMaxSp,
            cfg.LogWeight, cfg.LogMaxWeight, cfg.LogMap, cfg.LogStatuses
        );
    }

    private ProfileSettingsUpdate BuildProfileSettingsUpdate()
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        return new ProfileSettingsUpdate(
            prefs.StopBuffsCity,
            prefs.SoundEnabled,
            prefs.StartAutoOffTimerOnEnable,
            prefs.ClearAutoOffTimerOnDisable,
            prefs.KeepDeadClientInfo
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


    private bool UnbindKeyGlobally(Keys key)
    {
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshToggleHotkey()
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        if (!string.IsNullOrEmpty(prefs?.ToggleStateKey)
            && prefs.ToggleStateKey != AppConfig.TEXT_NONE
            && Enum.TryParse<Keys>(prefs.ToggleStateKey, out Keys toggleKey)
            && toggleKey != Keys.None)
        {
            KeyboardHook.ClearKeyDowns();
            KeyboardHook.KeyDown = null;
            KeyboardHook.AddKeyDown(toggleKey, () =>
            {
                _ = Task.Run(async () =>
                {
                    if (_isOn) await HandleTurnOff();
                    else await HandleTurnOn();
                });
                return true;
            });
            DebugLogger.Debug($"[WorkerCore] Toggle hotkey registered: {prefs.ToggleStateKey} → {toggleKey}");
        }
        else
        {
            KeyboardHook.ClearKeyDowns();
            KeyboardHook.KeyDown = null;
            DebugLogger.Debug($"[WorkerCore] Toggle hotkey cleared (key='{prefs?.ToggleStateKey}')");
        }
    }

    private string? _GetConnectedProcessName()
    {
        try 
        { 
            var p = ClientSingleton.GetClient()?.Process;
            if (p != null) return $"{p.ProcessName}.exe - {p.Id}";
            return null;
        }
        catch { return null; }
    }
}
