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

    private bool _isOn;
    private string _currentProfileName = "Default";

    public WorkerCore()
    {
        ConfigGlobal.Initialize();
        Server.Initialize();

        ProfileSingleton.Create("Default");
        ProfileSingleton.Load("Default");

        KeyboardHook.Enable();

        WorkerNotifier.TurnOffRequested += reason =>
        {
            DebugLogger.Info($"[WorkerCore] Auto turn-off: {reason}");
            _ = HandleTurnOff();
        };

        DebugLogger.LogMessageEmitted += (level, msg) =>
            _ = BroadcastAsync(new LogMessageUpdate(level, msg));

        RefreshToggleHotkey();

        _statePublisher = new StatePublisher(msg => BroadcastAsync(msg));
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
            await BroadcastAsync(new ErrorUpdate("No client connected."));
            return;
        }
        _isOn = true;
        var p = ProfileSingleton.GetCurrent();
        Subject.Notify(new Message(MessageCode.TURN_ON, null));
        p.AutopotHP.Start();
        p.AutopotSP.Start();
        p.SkillTimer.Start();
        p.SkillSpammer.Start();
        p.StatusRecovery.Start();
        p.AutobuffSkill.Start();
        p.AutobuffItem.Start();
        p.DebuffsRecovery.Start();
        p.MacroSwitch.Start();
        p.SongMacro.Start();
        p.TransferHelper.Start();
        _statePublisher.Start();
        await BroadcastAsync(new AppStateUpdate(IsOn: true));
        DebugLogger.Info("[WorkerCore] Turned ON");
    }

    public async Task HandleTurnOff()
    {
        if (!_isOn) return;
        _isOn = false;
        var p = ProfileSingleton.GetCurrent();
        Subject.Notify(new Message(MessageCode.TURN_OFF, null));
        p.AutopotHP.Stop();
        p.AutopotSP.Stop();
        p.SkillTimer.Stop();
        p.SkillSpammer.Stop();
        p.StatusRecovery.Stop();
        p.AutobuffSkill.Stop();
        p.AutobuffItem.Stop();
        p.DebuffsRecovery.Stop();
        p.MacroSwitch.Stop();
        p.SongMacro.Stop();
        p.TransferHelper.Stop();
        _statePublisher.Stop();
        await BroadcastAsync(new AppStateUpdate(IsOn: false));
        DebugLogger.Info("[WorkerCore] Turned OFF");
    }

    // ── Client ────────────────────────────────────────────────────────────────

    public async Task HandleConnectClient(string processName)
    {
        if (_isOn) await HandleTurnOff();
        var client = new Client(processName);
        if (client.Process == null)
        {
            await BroadcastAsync(new ErrorUpdate($"Client not found or unsupported: {processName}"));
            return;
        }
        try
        {
            ClientSingleton.SetClient(client);
            await BroadcastAsync(new ClientStateUpdate(Connected: true, ProcessName: processName));
            DebugLogger.Info($"[WorkerCore] Client connected: {processName}");
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to connect: {ex.Message}"));
        }
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
        var slot = hp.HPSlots.FirstOrDefault(s => s.Id == cmd.Id);
        if (slot == null) return;

        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            slot.Key = key;
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

    public async Task HandleReorderAutopotHP(ReorderAutopotHPCommand cmd)
    {
        var hp = ProfileSingleton.GetCurrent().AutopotHP;
        hp.HPSlots = ReorderAutopotHPSlots(hp.HPSlots, cmd.SlotOrder);
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
        var slot = sp.SPSlots.FirstOrDefault(s => s.Id == cmd.Id);
        if (slot == null) return;

        if (Enum.TryParse<Keys>(cmd.Key, ignoreCase: true, out var key))
            slot.Key = key;
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

    public async Task HandleReorderAutopotSP(ReorderAutopotSPCommand cmd)
    {
        var sp = ProfileSingleton.GetCurrent().AutopotSP;
        sp.SPSlots = ReorderAutopotSPSlots(sp.SPSlots, cmd.SlotOrder);
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

    // ── Full state request ────────────────────────────────────────────────────

    public async Task HandleFullStateRequest()
    {
        var client = ClientSingleton.GetClient();
        await BroadcastAsync(new AppStateUpdate(IsOn: _isOn));
        await BroadcastAsync(new ClientStateUpdate(
            Connected: client != null,
            ProcessName: client != null ? _GetConnectedProcessName() : null));
        await BroadcastAsync(new ProfileListUpdate(Profile.ListAll(), _currentProfileName));
        await BroadcastAsync(new ProcessListUpdate(BuildProcessList()));
        await BroadcastAsync(BuildAutopotHPConfig());
        await BroadcastAsync(BuildAutopotSPConfig());
    }

    public async Task HandleRequestProcessList()
        => await BroadcastAsync(new ProcessListUpdate(BuildProcessList()));

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
    }

    private string? _GetConnectedProcessName()
    {
        try { return ClientSingleton.GetClient()?.Process?.ProcessName; }
        catch { return null; }
    }

    private static List<string> BuildProcessList()
    {
        var processItems = new List<(string ProcessText, int ProcessId)>();

        try
        {
            var knownNames = Server.GetLocalClients()
                .Select(client => client.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string name in knownNames)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(process.MainWindowTitle))
                            continue;

                        string processText = $"{process.ProcessName}.exe - {process.Id}";
                        var client = new Client(processText);
                        if (client.Process != null)
                        {
                            processItems.Add((processText, process.Id));
                        }
                    }
                    catch
                    {
                        // Ignore individual process failures; keep enumerating.
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error(ex, "BuildProcessList failed");
        }

        return processItems
            .OrderBy(item => item.ProcessId)
            .Select(item => item.ProcessText)
            .ToList();
    }

    private static List<AutopotHP.HPSlot> ReorderAutopotHPSlots(
        IReadOnlyList<AutopotHP.HPSlot> currentSlots,
        IEnumerable<int> slotOrder)
    {
        var byId = currentSlots.ToDictionary(slot => slot.Id, slot => slot);
        var ordered = new List<AutopotHP.HPSlot>();
        var seen = new HashSet<int>();

        foreach (var id in slotOrder)
        {
            if (byId.TryGetValue(id, out var slot))
            {
                ordered.Add(slot);
                seen.Add(id);
            }
        }

        foreach (var slot in currentSlots)
        {
            if (!seen.Contains(slot.Id))
                ordered.Add(slot);
        }

        return ordered;
    }

    private static List<AutopotSP.SPSlot> ReorderAutopotSPSlots(
        IReadOnlyList<AutopotSP.SPSlot> currentSlots,
        IEnumerable<int> slotOrder)
    {
        var byId = currentSlots.ToDictionary(slot => slot.Id, slot => slot);
        var ordered = new List<AutopotSP.SPSlot>();
        var seen = new HashSet<int>();

        foreach (var id in slotOrder)
        {
            if (byId.TryGetValue(id, out var slot))
            {
                ordered.Add(slot);
                seen.Add(id);
            }
        }

        foreach (var slot in currentSlots)
        {
            if (!seen.Contains(slot.Id))
                ordered.Add(slot);
        }

        return ordered;
    }
}
