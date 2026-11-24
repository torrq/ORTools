using System.Threading.Channels;
using ORTools.Shared.Protocol;
using ORTools.Worker.IPC;

namespace ORTools.Worker;

public sealed class WorkerCore : IDisposable
{
    public event Action<IIpcMessage>? OnBroadcast;

    private readonly CommandDispatcher _dispatcher;
    private readonly Channel<IIpcMessage> _commandChannel = Channel.CreateUnbounded<IIpcMessage>();
    private readonly StatePublisher _statePublisher;
    private readonly Thread _hookThread;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private volatile bool _isOn;
    private string _currentProfileName = "Default";
    private readonly AutoOff _autoOff;

    // Stored delegates for static event subscriptions so we can unsubscribe in Dispose()
    private readonly Action<Client> _onClientRemovedHandler;
    private readonly Action<string> _onTurnOffRequestedHandler;
    private readonly Action<string, string> _onLogMessageEmittedHandler;

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

        _onClientRemovedHandler = (removedClient) =>
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
        ClientListSingleton.OnClientRemoved += _onClientRemovedHandler;

        WorkerNotifier.IsActive = () => _isOn;

        _autoOff = new AutoOff();
        _autoOff.IsAppOn = () => _isOn;
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
                _ = Task.Run(async () =>
                {
                    if (!_isOn) return;
                    await HandleTurnOff();
                    WeightLimitMacro.SendOverweightMacro();
                });
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

        _onTurnOffRequestedHandler = reason =>
        {
            if (!_isOn) return;
            DebugLogger.Info($"[WorkerCore] Auto turn-off: {reason}");
            _ = HandleTurnOff();
        };
        WorkerNotifier.TurnOffRequested += _onTurnOffRequestedHandler;

        _onLogMessageEmittedHandler = (level, msg) =>
            _ = BroadcastAsync(new LogMessageUpdate(level, msg));
        DebugLogger.LogMessageEmitted += _onLogMessageEmittedHandler;

        RefreshToggleHotkey();

        _statePublisher = new StatePublisher(msg => BroadcastAsync(msg));
        _statePublisher.CharacterChanged += OnCharacterChanged;
        _statePublisher.Start(); // Run continuously to keep UI synced even when OFF

        _dispatcher = new CommandDispatcher(this);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"[WorkerCore] Running in-memory IPC  Mode: {AppConfig.GetRateTag()}");
        Win32Interop.DisableProcessPowerThrottling();
        Win32Interop.timeBeginPeriod(1);
        try
        {
            await foreach (var cmd in _commandChannel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await _dispatcher.HandleAsync(cmd);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"[WorkerCore] Command {cmd.GetType().Name} failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Win32Interop.timeEndPeriod(1);
            KeyboardHook.Disable();
            await HandleTurnOff();
        }
    }

    public void Dispose()
    {
        WorkerNotifier.IsActive = null;
        ClientListSingleton.OnClientRemoved -= _onClientRemovedHandler;
        WorkerNotifier.TurnOffRequested -= _onTurnOffRequestedHandler;
        DebugLogger.LogMessageEmitted -= _onLogMessageEmittedHandler;
        _autoOff.Dispose();
        _statePublisher.CharacterChanged -= OnCharacterChanged;
        _statePublisher.Stop();
        ClientListSingleton.StopCleanupMonitor();
        _stateLock.Dispose();
    }

    public async Task HandleCommandAsync(IIpcMessage command)
    {
        await _commandChannel.Writer.WriteAsync(command);
    }

    public Task BroadcastAsync(IIpcMessage msg)
    {
        if (msg is ErrorUpdate err)
        {
            DebugLogger.Warning($"[WorkerCore] {err.Message}");
        }
        OnBroadcast?.Invoke(msg);
        return Task.CompletedTask;
    }

    // ── Turn on/off ───────────────────────────────────────────────────────────

    public async Task HandleTurnOn()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_isOn) return;
            var client = ClientSingleton.GetClient();
            if (client == null || client.Process == null || client.Process.HasExited)
            {
                await BroadcastAsync(new ErrorUpdate("Client is disconnected or has exited.")); return;
            }
            _isOn = true;
            var p = ProfileSingleton.GetCurrent();
            await Task.Delay(300);

            // Re-verify after delay that we should still turn on and client is still alive
            if (!_isOn || client.Process == null || client.Process.HasExited)
            {
                _isOn = false;
                p.StopAll();
                _autoOff.StopOverweightMonitor();
                await BroadcastAsync(new AppStateUpdate(IsOn: false, ToggleKey: p.UserPreferences.ToggleStateKey, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
                return;
            }

            p.StartAll();
            
            var prefs = p.UserPreferences;
            if (prefs.StartAutoOffTimerOnEnable && (!_autoOff.IsTimerRunning || _autoOff.IsPaused))
            {
                if (prefs.PauseAutoOffTimerOnDisable && _autoOff.IsPaused)
                    _autoOff.ResumeTimer();
                else if (!_autoOff.IsTimerRunning)
                    _autoOff.StartTimer();
            }
            else if (_autoOff.IsPaused)
            {
                _autoOff.ResumeTimer();
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
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task HandleTurnOff()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (!_isOn) return;
            _isOn = false;
            
            var p = ProfileSingleton.GetCurrent();
            try
            {
                p.StopAll();
                
                var prefs = p.UserPreferences;
                if (_autoOff.IsTimerRunning)
                {
                    if (prefs.ClearAutoOffTimerOnDisable && !prefs.PauseAutoOffTimerOnDisable)
                    {
                        _autoOff.StopTimer();
                    }
                    else
                    {
                        // Pause the timer so it never ticks down or triggers while OFF
                        _autoOff.PauseTimer();
                    }
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
        finally
        {
            _stateLock.Release();
        }
    }


    public async Task HandleUpdateToggleKey(string keyStr)
    {
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        if (!string.IsNullOrWhiteSpace(keyStr) && !string.Equals(keyStr, AppConfig.TEXT_NONE, StringComparison.OrdinalIgnoreCase))
        {
            var cfg = ConfigGlobal.GetConfig();
            if (cfg.ProfileHotkeys != null)
            {
                var conflict = cfg.ProfileHotkeys.FirstOrDefault(kvp =>
                    !kvp.Value.Contains('+') && string.Equals(kvp.Value, keyStr, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(conflict.Key))
                {
                    await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: prefs.ToggleStateKey, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
                    await BroadcastAsync(new ErrorUpdate($"Cannot use '{keyStr}' as the Turn ON/OFF key because it is already used as the switch hotkey for profile '{conflict.Key}'."));
                    return;
                }
            }
        }

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
        ClientSingleton.SetClient(null);
        if (client != null) ClientListSingleton.RemoveClient(client);
        _statePublisher.ClearCache();
        await BroadcastAsync(new ClientStateUpdate(Connected: false, ProcessName: null));
        DebugLogger.Info("[WorkerCore] Client disconnected");
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    private void OnCharacterChanged(string characterName)
    {
        try
        {
            var cfg = ConfigGlobal.GetConfig();
            if (!cfg.AutoSwitchProfileByChar || cfg.CharacterProfileMap == null) return;

            var match = cfg.CharacterProfileMap
                .FirstOrDefault(kvp => string.Equals(kvp.Key, characterName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(match.Value) || string.Equals(match.Value, _currentProfileName, StringComparison.OrdinalIgnoreCase))
                return;

            var available = Profile.ListAll();
            string? target = available.FirstOrDefault(p => string.Equals(p, match.Value, StringComparison.OrdinalIgnoreCase));
            if (target == null) return;

            DebugLogger.Info($"[WorkerCore] Auto-switching profile for '{characterName}' -> '{target}'");
            _ = HandleCommandAsync(new SwitchProfileCommand(target, PreserveActiveState: true));
        }
        catch (Exception ex)
        {
            DebugLogger.Debug($"[WorkerCore] OnCharacterChanged error: {ex.Message}");
        }
    }

    private ProfileListUpdate BuildProfileListUpdate()
    {
        var cfg = ConfigGlobal.GetConfig();
        var profiles = Profile.ListAll();

        var linked = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (cfg.CharacterProfileMap != null)
        {
            foreach (var kvp in cfg.CharacterProfileMap)
            {
                if (!linked.TryGetValue(kvp.Value, out var list))
                {
                    list = new List<string>();
                    linked[kvp.Value] = list;
                }
                list.Add(kvp.Key);
            }
        }

        var hotkeys = cfg.ProfileHotkeys != null
            ? new Dictionary<string, string>(cfg.ProfileHotkeys, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new ProfileListUpdate(profiles, _currentProfileName, linked, hotkeys, cfg.AutoSwitchProfileByChar);
    }

    public async Task HandleSwitchProfile(string profileName, bool preserveActiveState = false)
    {
        bool wasOn = _isOn;
        if (_isOn) await HandleTurnOff();
        try
        {
            ProfileSingleton.Create(profileName);
            ProfileSingleton.Load(profileName);
            string safeName = ProfileSingleton.GetCurrent().Name;
            HookSkillSpammerEvents();
            _autoOff.LoadFromProfile();
            _currentProfileName = safeName;
            ConfigGlobal.GetConfig().LastUsedProfile = safeName;
            ConfigGlobal.SaveConfig();
            RefreshToggleHotkey();
            await BroadcastAsync(BuildProfileListUpdate());
            await BroadcastAsync(new AppStateUpdate(IsOn: _isOn, ToggleKey: ProfileSingleton.GetCurrent().UserPreferences.ToggleStateKey, AppTitle: GetAppTitle(), ServerMode: AppConfig.ServerMode));
            await PushAllConfigs();
            DebugLogger.Info($"[WorkerCore] Profile: {safeName}");

            if (preserveActiveState && wasOn)
            {
                await HandleTurnOn();
            }
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
            await BroadcastAsync(BuildProfileListUpdate());
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
            string safeOld = ProfileSingleton.SanitizeProfileName(oldProfileName);
            string safeNew = ProfileSingleton.SanitizeProfileName(newProfileName);
            ProfileSingleton.Rename(oldProfileName, newProfileName);

            var cfg = ConfigGlobal.GetConfig();
            bool cfgChanged = false;
            if (cfg.CharacterProfileMap != null)
            {
                var keysToUpdate = cfg.CharacterProfileMap
                    .Where(kvp => string.Equals(kvp.Value, safeOld, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key).ToList();
                foreach (var ch in keysToUpdate)
                {
                    cfg.CharacterProfileMap[ch] = safeNew;
                    cfgChanged = true;
                }
            }
            if (cfg.ProfileHotkeys != null)
            {
                var oldHk = cfg.ProfileHotkeys.FirstOrDefault(kvp => string.Equals(kvp.Key, safeOld, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(oldHk.Key))
                {
                    cfg.ProfileHotkeys.Remove(oldHk.Key);
                    cfg.ProfileHotkeys[safeNew] = oldHk.Value;
                    cfgChanged = true;
                }
            }
            if (cfgChanged)
            {
                ConfigGlobal.SaveConfig();
                RefreshToggleHotkey();
            }

            if (string.Equals(_currentProfileName, safeOld, StringComparison.OrdinalIgnoreCase))
            {
                await HandleSwitchProfile(safeNew);
            }
            else
            {
                await BroadcastAsync(BuildProfileListUpdate());
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

            var cfg = ConfigGlobal.GetConfig();
            bool cfgChanged = false;
            if (cfg.CharacterProfileMap != null)
            {
                var keysToRemove = cfg.CharacterProfileMap
                    .Where(kvp => string.Equals(kvp.Value, profileName, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key).ToList();
                foreach (var ch in keysToRemove)
                {
                    cfg.CharacterProfileMap.Remove(ch);
                    cfgChanged = true;
                }
            }
            if (cfg.ProfileHotkeys != null)
            {
                var oldHk = cfg.ProfileHotkeys.FirstOrDefault(kvp => string.Equals(kvp.Key, profileName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(oldHk.Key))
                {
                    cfg.ProfileHotkeys.Remove(oldHk.Key);
                    cfgChanged = true;
                }
            }
            if (cfgChanged)
            {
                ConfigGlobal.SaveConfig();
                RefreshToggleHotkey();
            }

            if (string.Equals(_currentProfileName, profileName, StringComparison.OrdinalIgnoreCase))
            {
                await HandleSwitchProfile("Default");
            }
            else
            {
                await BroadcastAsync(BuildProfileListUpdate());
            }
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to delete profile '{profileName}': {ex.Message}"));
        }
    }

    public async Task HandleLinkCharacterToProfile(string characterName, string profileName)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(profileName)) return;
        var cfg = ConfigGlobal.GetConfig();
        cfg.CharacterProfileMap ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Remove any case-variant duplicate first
        var existing = cfg.CharacterProfileMap.Keys.FirstOrDefault(k => string.Equals(k, characterName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing != null) cfg.CharacterProfileMap.Remove(existing);

        cfg.CharacterProfileMap[characterName.Trim()] = profileName;
        ConfigGlobal.SaveConfig();
        await BroadcastAsync(BuildProfileListUpdate());
    }

    public async Task HandleUnlinkCharacter(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return;
        var cfg = ConfigGlobal.GetConfig();
        if (cfg.CharacterProfileMap != null)
        {
            var existing = cfg.CharacterProfileMap.Keys.FirstOrDefault(k => string.Equals(k, characterName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                cfg.CharacterProfileMap.Remove(existing);
                ConfigGlobal.SaveConfig();
            }
        }
        await BroadcastAsync(BuildProfileListUpdate());
    }

    public async Task HandleSetAutoSwitchProfileByChar(bool enabled)
    {
        var cfg = ConfigGlobal.GetConfig();
        cfg.AutoSwitchProfileByChar = enabled;
        ConfigGlobal.SaveConfig();
        await BroadcastAsync(BuildProfileListUpdate());
    }

    public async Task HandleSetProfileHotkey(string profileName, string hotkey)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;
        var cfg = ConfigGlobal.GetConfig();
        cfg.ProfileHotkeys ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var existingKey = cfg.ProfileHotkeys.Keys.FirstOrDefault(k => string.Equals(k, profileName, StringComparison.OrdinalIgnoreCase));
        if (existingKey != null) cfg.ProfileHotkeys.Remove(existingKey);

        if (!string.IsNullOrWhiteSpace(hotkey) && !string.Equals(hotkey, AppConfig.TEXT_NONE, StringComparison.OrdinalIgnoreCase))
        {
            var curToggle = ProfileSingleton.GetCurrent().UserPreferences?.ToggleStateKey;
            if (!hotkey.Contains('+') && !string.IsNullOrWhiteSpace(curToggle)
                && string.Equals(hotkey, curToggle, StringComparison.OrdinalIgnoreCase))
            {
                await BroadcastAsync(BuildProfileListUpdate());
                await BroadcastAsync(new ErrorUpdate($"Cannot use '{hotkey}' as a profile switch hotkey because it is already set as the Turn ON/OFF key."));
                return;
            }

            // Ensure no other profile holds the exact same hotkey
            var dupes = cfg.ProfileHotkeys
                .Where(kvp => string.Equals(kvp.Value, hotkey, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key).ToList();
            foreach (var d in dupes) cfg.ProfileHotkeys.Remove(d);

            cfg.ProfileHotkeys[profileName] = hotkey;
        }

        ConfigGlobal.SaveConfig();
        RefreshToggleHotkey();
        await BroadcastAsync(BuildProfileListUpdate());
    }

    public async Task HandleImportProfiles(List<string> filePaths)
    {
        if (filePaths == null || filePaths.Count == 0) return;

        string? lastImported = null;
        var errors = new List<string>();

        foreach (var path in filePaths)
        {
            try
            {
                lastImported = ProfileSingleton.Import(path);
                DebugLogger.Info($"[WorkerCore] Imported profile '{lastImported}' from {path}");
            }
            catch (Exception ex)
            {
                errors.Add($"{System.IO.Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (lastImported != null)
        {
            await HandleSwitchProfile(lastImported);
        }

        if (errors.Count > 0)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to import profile(s): {string.Join("; ", errors)}"));
        }
    }

    public async Task HandleExportProfile(string profileName, string destinationPath)
    {
        try
        {
            ProfileSingleton.Export(profileName, destinationPath);
            DebugLogger.Info($"[WorkerCore] Exported profile '{profileName}' to {destinationPath}");
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to export profile '{profileName}': {ex.Message}"));
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
        var profile = ProfileSingleton.GetCurrent();
        int delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
        profile.StatusRecovery.Delay = delay;
        profile.DebuffsRecovery.Delay = delay;
        ProfileSingleton.SetConfiguration(profile.StatusRecovery);
        ProfileSingleton.SetConfiguration(profile.DebuffsRecovery);
        await BroadcastAsync(BuildStatusRecoveryConfig());
        await BroadcastAsync(BuildDebuffRecoveryConfig());
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

    public async Task HandleToggleStatusRecoveryEnabled(ToggleStatusRecoveryEnabledCommand cmd)
    {
        var sr = ProfileSingleton.GetCurrent().StatusRecovery;
        bool hasKey = sr.GetKeyForList(cmd.Name) != Keys.None;
        sr.SetListEnabled(cmd.Name, hasKey ? cmd.IsEnabled : true);
        ProfileSingleton.SetConfiguration(sr);
        await BroadcastAsync(BuildStatusRecoveryConfig());
    }

    private StatusRecoveryConfigUpdate BuildStatusRecoveryConfig()
    {
        var sr = ProfileSingleton.GetCurrent().StatusRecovery;
        return new StatusRecoveryConfigUpdate(
            Items: sr.statusLists.Select(kvp =>
            {
                bool hasKey = kvp.Value.Key != Keys.None;
                return new StatusRecoveryItemData(kvp.Key, hasKey ? kvp.Value.Key.ToString() : "None", !hasKey || sr.IsListEnabled(kvp.Key));
            }).ToList(),
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
        var profile = ProfileSingleton.GetCurrent();
        int delay = Math.Max((int)AppConfig.DefaultMinimumDelay, cmd.Delay);
        profile.StatusRecovery.Delay = delay;
        profile.DebuffsRecovery.Delay = delay;
        ProfileSingleton.SetConfiguration(profile.StatusRecovery);
        ProfileSingleton.SetConfiguration(profile.DebuffsRecovery);
        await BroadcastAsync(BuildStatusRecoveryConfig());
        await BroadcastAsync(BuildDebuffRecoveryConfig());
    }

    public async Task HandleToggleDebuffRecoveryEnabled(ToggleDebuffRecoveryEnabledCommand cmd)
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            bool hasKey = dr.HasMappingForStatus(statusId);
            dr.SetDebuffEnabled(statusId, hasKey ? cmd.IsEnabled : true);
            ProfileSingleton.SetConfiguration(dr);
        }
        await BroadcastAsync(BuildDebuffRecoveryConfig());
    }

    private DebuffRecoveryConfigUpdate BuildDebuffRecoveryConfig()
    {
        var dr = ProfileSingleton.GetCurrent().DebuffsRecovery;
        var update = new DebuffRecoveryConfigUpdate(
            Items: BuffService.GetDebuffs().Select(b =>
            {
                bool hasKey = dr.buffMapping.TryGetValue(b.EffectStatusID, out var key) && key != Keys.None;
                return new DebuffRecoveryItemData(
                    b.EffectStatusID.ToString(),
                    hasKey ? key.ToString() : "None",
                    b.IconName,
                    !hasKey || dr.IsDebuffEnabled(b.EffectStatusID));
            }).ToList(),
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

    public async Task HandleToggleAutobuffSkillEnabled(ToggleAutobuffSkillEnabledCommand cmd)
    {
        var abs = ProfileSingleton.GetCurrent().AutobuffSkill;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            // Unbound buffs remain enabled in appearance and state
            bool effectiveEnabled = abs.HasMappingForStatus(statusId) ? cmd.IsEnabled : true;
            abs.SetBuffEnabled(statusId, effectiveEnabled);
            ProfileSingleton.SetConfiguration(abs);
        }
        await BroadcastAsync(BuildAutobuffSkillConfig());
        await BroadcastAsync(BuildAutobuffOrderConfig());
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
            .Select(b =>
            {
                bool hasKey = map.TryGetValue(b.EffectStatusID, out var k) && k != Keys.None;
                return new AutobuffSkillItemData(
                    b.EffectStatusID.ToString(),
                    b.Name,
                    hasKey ? k.ToString() : "None",
                    b.IconName,
                    !hasKey || abs.IsBuffEnabled(b.EffectStatusID));
            })
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

    public async Task HandleToggleAutobuffItemEnabled(ToggleAutobuffItemEnabledCommand cmd)
    {
        var abi = ProfileSingleton.GetCurrent().AutobuffItem;
        if (Enum.TryParse<EffectStatusIDs>(cmd.StatusName, out var statusId))
        {
            bool effectiveEnabled = abi.HasMappingForStatus(statusId) ? cmd.IsEnabled : true;
            abi.SetBuffEnabled(statusId, effectiveEnabled);
            ProfileSingleton.SetConfiguration(abi);
        }
        await BroadcastAsync(BuildAutobuffItemConfig());
        await BroadcastAsync(BuildAutobuffOrderConfig());
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
        config.DebugViewFontSize = Math.Clamp(cmd.DebugViewFontSize, AppConfig.MinDebugViewFontSize, AppConfig.MaxDebugViewFontSize);
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
        config.CheckForUpdatesOnStartup = cmd.CheckForUpdatesOnStartup;
        config.Theme = cmd.Theme;
        config.CustomThemeIsLight = cmd.CustomThemeIsLight;
        config.CustomThemeColor = cmd.CustomThemeColor;
        config.HoldMultiKeyMode = cmd.HoldMultiKeyMode;
        config.AutopotPriorityMode = cmd.AutopotPriorityMode;
        ConfigGlobal.SaveConfig();

        var p = ProfileSingleton.GetCurrent();
        p.ATKDEFMode.EnsureCorrectRowCount(config.AtkDefRows);
        ProfileSingleton.SetConfiguration(p.ATKDEFMode);

        await BroadcastAsync(BuildGlobalConfigUpdate());
        await BroadcastAsync(BuildMacroSongConfig());
        await BroadcastAsync(BuildMacroSwitchConfig());
        await BroadcastAsync(BuildAtkDefConfig());
    }

    public async Task HandleUpdateCityList(List<string> cities)
    {
        Server.SaveCityList(cities);
        await BroadcastAsync(new CityListUpdate(Server.GetCityListSorted()));
    }

    public async Task HandleResetCityList()
    {
        Server.ResetCityListToDefault();
        await BroadcastAsync(new CityListUpdate(Server.GetCityListSorted()));
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
        prefs.PauseAutoOffTimerOnDisable = cmd.PauseAutoOffTimerOnDisable;
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
            if (row.InstrumentKey == key) row.InstrumentKey = Keys.None;
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
            .Select(b =>
            {
                bool hasKey = map.TryGetValue(b.EffectStatusID, out var kList) && kList.Count > 0 && kList[0] != Keys.None;
                return new AutobuffItemItemData(
                    b.EffectStatusID.ToString(),
                    b.Name,
                    hasKey ? kList![0].ToString() : "None",
                    b.IconName,
                    !hasKey || abi.IsBuffEnabled(b.EffectStatusID));
            })
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
                    items.Add(new AutobuffOrderItemData(statusId.ToString(), buff?.Name ?? statusId.ToString(), sk.ToString(), "Skill", buff?.IconName ?? "None", abs.IsBuffEnabled(statusId)));
                    addedStatuses.Add(statusId);
                }
                
                if (abi.buffMapping.TryGetValue(statusId, out var ik) && !addedStatuses.Contains(statusId))
                {
                    var buff = BuffService.GetBuff(statusId);
                    items.Add(new AutobuffOrderItemData(statusId.ToString(), buff?.Name ?? statusId.ToString(), string.Join(", ", ik), "Item", buff?.IconName ?? "None", abi.IsBuffEnabled(statusId)));
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
                items.Add(new AutobuffOrderItemData(kvp.Key.ToString(), buff?.Name ?? kvp.Key.ToString(), kvp.Value.ToString(), "Skill", buff?.IconName ?? "None", abs.IsBuffEnabled(kvp.Key)));
                profile.UnifiedAutobuffOrder.Add(kvp.Key.ToString());
                addedStatuses.Add(kvp.Key);
            }
        }
        foreach (var kvp in abi.buffMapping)
        {
            if (!addedStatuses.Contains(kvp.Key))
            {
                var buff = BuffService.GetBuff(kvp.Key);
                items.Add(new AutobuffOrderItemData(kvp.Key.ToString(), buff?.Name ?? kvp.Key.ToString(), string.Join(", ", kvp.Value), "Item", buff?.IconName ?? "None", abi.IsBuffEnabled(kvp.Key)));
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
        await BroadcastAsync(BuildProfileListUpdate());
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
        await BroadcastAsync(new CityListUpdate(Server.GetCityListSorted()));
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
            config.DebugViewFontSize,
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
            config.CheckForUpdatesOnStartup,
            config.Theme,
            config.CustomThemeIsLight,
            config.CustomThemeColor,
            config.HoldMultiKeyMode,
            config.AutopotPriorityMode
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
            prefs.PauseAutoOffTimerOnDisable,
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

    private static bool TryParseHotkeyCombo(string raw, out bool ctrl, out bool alt, out bool shift, out Keys key)
    {
        ctrl = alt = shift = false;
        key = Keys.None;
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, AppConfig.TEXT_NONE, StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = raw.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                ctrl = true;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                alt = true;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                shift = true;
            else if (Enum.TryParse<Keys>(part, ignoreCase: true, out var parsed) && parsed != Keys.None)
                key = parsed;
        }
        return key != Keys.None;
    }

    private void RefreshToggleHotkey()
    {
        KeyboardHook.ClearKeyDowns();
        KeyboardHook.KeyDown = null;

        var handlersByKey = new Dictionary<Keys, List<Func<bool>>>();

        void RegisterHandler(Keys key, Func<bool> handler)
        {
            if (!handlersByKey.TryGetValue(key, out var list))
            {
                list = new List<Func<bool>>();
                handlersByKey[key] = list;
            }
            list.Add(handler);
        }

        // 1. Register global ON/OFF toggle key FIRST so the active profile's toggle key
        //    always takes precedence over another profile's modifier-less switch hotkey.
        var prefs = ProfileSingleton.GetCurrent().UserPreferences;
        if (!string.IsNullOrEmpty(prefs?.ToggleStateKey)
            && prefs.ToggleStateKey != AppConfig.TEXT_NONE
            && Enum.TryParse<Keys>(prefs.ToggleStateKey, out Keys toggleKey)
            && toggleKey != Keys.None)
        {
            RegisterHandler(toggleKey, () =>
            {
                if (KeyboardHook.Control || KeyboardHook.Alt || KeyboardHook.Shift)
                    return false;

                if (!_isOn)
                {
                    var client = ClientSingleton.GetClient();
                    if (client == null || client.Process == null || client.Process.HasExited)
                        return false;
                }

                _ = Task.Run(() => HandleCommandAsync(_isOn ? new TurnOffCommand() : new TurnOnCommand()));
                return true;
            });
            DebugLogger.Debug($"[WorkerCore] Toggle hotkey registered: {prefs.ToggleStateKey} → {toggleKey}");
        }
        else
        {
            DebugLogger.Debug($"[WorkerCore] Toggle hotkey cleared (key='{prefs?.ToggleStateKey}')");
        }

        // 2. Register profile switch hotkeys (modifier-checked)
        var cfg = ConfigGlobal.GetConfig();
        if (cfg.ProfileHotkeys != null)
        {
            foreach (var kvp in cfg.ProfileHotkeys)
            {
                string targetProfile = kvp.Key;
                if (!TryParseHotkeyCombo(kvp.Value, out bool needCtrl, out bool needAlt, out bool needShift, out Keys hk))
                    continue;

                RegisterHandler(hk, () =>
                {
                    if (KeyboardHook.Control != needCtrl || KeyboardHook.Alt != needAlt || KeyboardHook.Shift != needShift)
                        return false;

                    // Do not steal plain keys while typing in RO chat
                    var client = ClientSingleton.GetClient();
                    if (!needCtrl && !needAlt && client != null && client.IsProcessRunning() && client.IsTextInputActive())
                        return false;

                    if (string.Equals(_currentProfileName, targetProfile, StringComparison.OrdinalIgnoreCase))
                        return false;

                    _ = HandleCommandAsync(new SwitchProfileCommand(targetProfile, PreserveActiveState: true));
                    return true;
                });
            }
        }

        foreach (var kvp in handlersByKey)
        {
            var handlers = kvp.Value;
            KeyboardHook.AddKeyDown(kvp.Key, () =>
            {
                foreach (var h in handlers)
                {
                    if (h()) break;
                }
                return true;
            });
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
