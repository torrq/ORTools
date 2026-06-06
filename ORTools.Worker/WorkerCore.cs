using ORTools.Shared.Protocol;
using ORTools.Worker.IPC;

namespace ORTools.Worker;

/// <summary>
/// Root of the Worker process. Owns:
///   - Subject/Observer message bus (internal state machine)
///   - All model instances (one per feature)
///   - StatePublisher (live HP/SP + character pushes)
///   - Pipe server and command dispatcher
///
/// Lifecycle:
///   1. Constructor initialises config, profiles, and models
///   2. RunAsync starts the pipe server
///   3. CommandDispatcher calls TurnOn/TurnOff/ConnectClient etc.
///   4. WorkerNotifier.TurnOffRequested fires when a model wants to stop
/// </summary>
public sealed class WorkerCore
{
    public const string PipeName = "ORTools-Worker";

    // ── Internal message bus ──────────────────────────────────────────────────
    public Subject Subject { get; } = new();

    // ── Models ────────────────────────────────────────────────────────────────
    public Profile? ActiveProfile => ProfileSingleton.GetCurrent();

    // ── IPC ───────────────────────────────────────────────────────────────────
    private readonly PipeServer        _server;
    private readonly CommandDispatcher _dispatcher;
    private readonly StatePublisher    _statePublisher;

    // ── App state ─────────────────────────────────────────────────────────────
    private bool   _isOn;
    private string _currentProfileName = "Default";

    public WorkerCore()
    {
        // Initialise config and profile storage
        ConfigGlobal.Initialize();
        Server.Initialize();

        // Ensure default profile exists and load it
        ProfileSingleton.Create("Default");
        ProfileSingleton.Load("Default");

        // Install keyboard hook for toggle hotkey
        KeyboardHook.Enable();

        // Wire WorkerNotifier → automatic turn-off
        WorkerNotifier.TurnOffRequested += reason =>
        {
            DebugLogger.Info($"[WorkerCore] Auto turn-off: {reason}");
            _ = HandleTurnOff();
        };

        // Wire DebugLogger → IPC broadcast
        DebugLogger.LogMessageEmitted += (level, msg) =>
            _ = BroadcastAsync(new LogMessageUpdate(level, msg));

        // Wire KeyboardHook toggle key
        RefreshToggleHotkey();

        // IPC wiring
        _statePublisher = new StatePublisher(msg => BroadcastAsync(msg));
        _dispatcher     = new CommandDispatcher(this);
        _server         = new PipeServer(PipeName, _dispatcher);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine($"[WorkerCore] Pipe: {PipeName}  ServerMode: {AppConfig.GetRateTag()}");
        Win32Interop.timeBeginPeriod(1);
        try   { await _server.RunAsync(ct); }
        finally
        {
            Win32Interop.timeEndPeriod(1);
            KeyboardHook.Disable();
            HandleTurnOff().GetAwaiter().GetResult();
        }
    }

    // ── Turn on/off ───────────────────────────────────────────────────────────

    public async Task HandleTurnOn()
    {
        var client = ClientSingleton.GetClient();
        if (client == null)
        {
            await BroadcastAsync(new ErrorUpdate("No client connected."));
            return;
        }

        _isOn = true;
        var profile = ProfileSingleton.GetCurrent();
        Subject.Notify(new Message(MessageCode.TURN_ON, null));

        profile.AutopotHP.Start();
        profile.AutopotSP.Start();
        profile.SkillTimer.Start();
        profile.SkillSpammer.Start();
        profile.StatusRecovery.Start();
        profile.AutobuffSkill.Start();
        profile.AutobuffItem.Start();
        profile.DebuffsRecovery.Start();
        profile.MacroSwitch.Start();
        profile.SongMacro.Start();
        profile.TransferHelper.Start();

        _statePublisher.Start();

        await BroadcastAsync(new AppStateUpdate(IsOn: true));
        DebugLogger.Info("[WorkerCore] Turned ON");
    }

    public async Task HandleTurnOff()
    {
        if (!_isOn) return;
        _isOn = false;

        var profile = ProfileSingleton.GetCurrent();
        Subject.Notify(new Message(MessageCode.TURN_OFF, null));

        profile.AutopotHP.Stop();
        profile.AutopotSP.Stop();
        profile.SkillTimer.Stop();
        profile.SkillSpammer.Stop();
        profile.StatusRecovery.Stop();
        profile.AutobuffSkill.Stop();
        profile.AutobuffItem.Stop();
        profile.DebuffsRecovery.Stop();
        profile.MacroSwitch.Stop();
        profile.SongMacro.Stop();
        profile.TransferHelper.Stop();

        _statePublisher.Stop();

        await BroadcastAsync(new AppStateUpdate(IsOn: false));
        DebugLogger.Info("[WorkerCore] Turned OFF");
    }

    // ── Client connect/disconnect ─────────────────────────────────────────────

    public async Task HandleConnectClient(string processName)
    {
        if (_isOn) await HandleTurnOff();

        var clients = Server.GetLocalClients();
        var dto     = clients.FirstOrDefault(c =>
            string.Equals(c.Name, processName, StringComparison.OrdinalIgnoreCase));

        if (dto == null)
        {
            await BroadcastAsync(new ErrorUpdate($"Server config not found for: {processName}"));
            return;
        }

        try
        {
            var client = new Client(processName);
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

            await BroadcastAsync(new ProfileListUpdate(
                Profiles:       Profile.ListAll(),
                CurrentProfile: profileName));

            DebugLogger.Info($"[WorkerCore] Profile switched to: {profileName}");
        }
        catch (Exception ex)
        {
            await BroadcastAsync(new ErrorUpdate($"Failed to load profile '{profileName}': {ex.Message}"));
        }
    }

    public async Task HandleFullStateRequest()
    {
        var client = ClientSingleton.GetClient();
        await BroadcastAsync(new AppStateUpdate(IsOn: _isOn));
        await BroadcastAsync(new ClientStateUpdate(
            Connected:   client != null,
            ProcessName: client != null ? _GetConnectedProcessName() : null));
        await BroadcastAsync(new ProfileListUpdate(
            Profiles:       Profile.ListAll(),
            CurrentProfile: _currentProfileName));
        await BroadcastAsync(new ProcessListUpdate(
            Processes: Server.GetLocalClients().Select(c => c.Name).ToList()));
    }

    // ── Broadcast helper ──────────────────────────────────────────────────────

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
                else       _ = HandleTurnOn();
                return true;
            });
        }
    }

    private string? _GetConnectedProcessName()
    {
        try { return ClientSingleton.GetClient()?.Process?.ProcessName; }
        catch { return null; }
    }
}
