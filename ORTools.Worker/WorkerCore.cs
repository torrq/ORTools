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
                            var jobSnap = tempClient.ReadJobBlock();
                            uint level = jobSnap?.Level ?? 0;
                            uint jobId = jobSnap?.JobId ?? 0;
                            string jobName = ORTools.Shared.Protocol.JobList.GetNameById((int)jobId);
                            
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                info = $" ({name} / {map} / {jobName} Lv.{level})";
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
            _currentProfileName = profileName;
            Subject.Notify(new Message(MessageCode.PROFILE_CHANGED, profileName));
            ConfigGlobal.GetConfig().LastUsedProfile = profileName;
            ConfigGlobal.SaveConfig();
            RefreshToggleHotkey();
            await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), profileName));
            DebugLogger.Info($"[WorkerCore] Profile: {profileName}");
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to load profile '{profileName}': {ex.Message}"));
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

    private StatusRecoveryConfigUpdate BuildStatusRecoveryConfig()
    {
        var sr = ProfileSingleton.GetCurrent().StatusRecovery;
        return new StatusRecoveryConfigUpdate(
            Items: sr.statusLists.Select(kvp => new StatusRecoveryItemData(kvp.Key, kvp.Value.Key.ToString())).ToList(),
            Delay: sr.Delay);
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
        await BroadcastAsync(BuildAutopotHPConfig());
        await BroadcastAsync(BuildAutopotSPConfig());
        await BroadcastAsync(BuildStatusRecoveryConfig());
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