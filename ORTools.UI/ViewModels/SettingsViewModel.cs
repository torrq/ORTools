using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly WorkerService _worker;

    [ObservableProperty] private bool _debugMode;
    [ObservableProperty] private bool _debugModeShowLog;
    [ObservableProperty] private bool _disableSystray;
    [ObservableProperty] private bool _minimizeToSystray = true;
    [ObservableProperty] private bool _closeToSystray = true;
    [ObservableProperty] private bool _clearAutoOffTimerOnDisable;
    [ObservableProperty] private bool _pauseWhenChatting;
    [ObservableProperty] private bool _pauseWhenDead;
    [ObservableProperty] private bool _exitWithRo;
    [ObservableProperty] private bool _alwaysOnTop;
    [ObservableProperty] private bool _allowResizingWindow;
    [ObservableProperty] private ThemeMode _theme;

    public ThemeMode[] ThemeModes { get; } = (ThemeMode[])Enum.GetValues(typeof(ThemeMode));

    // Placeholders for Profile Settings
    [ObservableProperty] private bool _stopBuffsCity;
    [ObservableProperty] private bool _soundEnabled;

    // Placeholders for Global Settings
    [ObservableProperty] private int _songRows = 4;
    [ObservableProperty] private int _macroSwitchRows = 4;
    [ObservableProperty] private int _atkDefRows = 2;
    [ObservableProperty] private string _defaultToggleStateKey = "None";
    [ObservableProperty] private bool _startAutoOffTimerOnEnable;

    public AutobuffSkillViewModel AutobuffSkill { get; }

    public SettingsViewModel(WorkerService worker, AutobuffSkillViewModel autobuffSkill)
    {
        _worker = worker;
        AutobuffSkill = autobuffSkill;
        _worker.GlobalConfigReceived += OnGlobalConfigReceived;
        _worker.ProfileSettingsReceived += OnProfileSettingsReceived;
    }

    private void OnGlobalConfigReceived(GlobalConfigUpdate update)
    {
        _suppressUpdates = true;
        SongRows = update.SongRows;
        MacroSwitchRows = update.MacroSwitchRows;
        AtkDefRows = update.AtkDefRows;
        DefaultToggleStateKey = update.DefaultToggleStateKey;
        DebugMode = update.DebugMode;
        DebugModeShowLog = update.DebugModeShowLog;
        DisableSystray = update.DisableSystray;
        MinimizeToSystray = update.MinimizeToSystray;
        CloseToSystray = update.CloseToSystray;
        StartAutoOffTimerOnEnable = update.StartAutoOffTimerOnEnable;
        ClearAutoOffTimerOnDisable = update.ClearAutoOffTimerOnDisable;
        PauseWhenChatting = update.PauseWhenChatting;
        PauseWhenDead = update.PauseWhenDead;
        ExitWithRo = update.ExitWithRo;
        AlwaysOnTop = update.AlwaysOnTop;
        AllowResizingWindow = update.AllowResizingWindow;
        Theme = update.Theme;
        _suppressUpdates = false;

        ThemeService.ApplyTheme(Theme);
    }

    private void OnProfileSettingsReceived(ProfileSettingsUpdate update)
    {
        _suppressUpdates = true;
        StopBuffsCity = update.StopBuffsCity;
        SoundEnabled = update.SoundEnabled;
        _suppressUpdates = false;
    }

    private bool _suppressUpdates = false;

    partial void OnDebugModeChanged(bool value) => SendGlobalUpdate();
    partial void OnDebugModeShowLogChanged(bool value) => SendGlobalUpdate();
    partial void OnDisableSystrayChanged(bool value) => SendGlobalUpdate();
    partial void OnMinimizeToSystrayChanged(bool value) => SendGlobalUpdate();
    partial void OnCloseToSystrayChanged(bool value) => SendGlobalUpdate();
    partial void OnClearAutoOffTimerOnDisableChanged(bool value) => SendGlobalUpdate();
    partial void OnPauseWhenChattingChanged(bool value) => SendGlobalUpdate();
    partial void OnPauseWhenDeadChanged(bool value) => SendGlobalUpdate();
    partial void OnExitWithRoChanged(bool value) => SendGlobalUpdate();
    partial void OnAlwaysOnTopChanged(bool value) => SendGlobalUpdate();
    partial void OnAllowResizingWindowChanged(bool value) => SendGlobalUpdate();
    
    partial void OnThemeChanged(ThemeMode value)
    {
        ThemeService.ApplyTheme(value);
        SendGlobalUpdate();
    }
    
    partial void OnSongRowsChanged(int value)  
    {
        if (value < 1 && !_suppressUpdates) { SongRows = 1; return; }
        SendGlobalUpdate();
    }
    
    partial void OnMacroSwitchRowsChanged(int value) 
    {
        if (value < 1 && !_suppressUpdates) { MacroSwitchRows = 1; return; }
        SendGlobalUpdate();
    }
    
    partial void OnAtkDefRowsChanged(int value)
    {
        if (value < 1 && !_suppressUpdates) { AtkDefRows = 1; return; }
        SendGlobalUpdate();
    }

    partial void OnDefaultToggleStateKeyChanged(string value) => SendGlobalUpdate();
    partial void OnStartAutoOffTimerOnEnableChanged(bool value) => SendGlobalUpdate();

    partial void OnStopBuffsCityChanged(bool value) => SendProfileUpdate();
    partial void OnSoundEnabledChanged(bool value) => SendProfileUpdate();

    private void SendGlobalUpdate()
    {
        if (_worker.ConnectionStatus != WorkerService.Status.Connected || _suppressUpdates) return;

        var cmd = new UpdateGlobalConfigCommand(
            SongRows: SongRows,
            MacroSwitchRows: MacroSwitchRows,
            AtkDefRows: AtkDefRows,
            DefaultToggleStateKey: DefaultToggleStateKey,
            DebugMode: DebugMode,
            DebugModeShowLog: DebugModeShowLog,
            DisableSystray: DisableSystray,
            MinimizeToSystray: MinimizeToSystray,
            CloseToSystray: CloseToSystray,
            StartAutoOffTimerOnEnable: StartAutoOffTimerOnEnable,
            ClearAutoOffTimerOnDisable: ClearAutoOffTimerOnDisable,
            PauseWhenChatting: PauseWhenChatting,
            PauseWhenDead: PauseWhenDead,
            ExitWithRo: ExitWithRo,
            AlwaysOnTop: AlwaysOnTop,
            AllowResizingWindow: AllowResizingWindow,
            Theme: Theme
        );
        _worker.Send(cmd);
    }

    private void SendProfileUpdate()
    {
        if (_worker.ConnectionStatus != WorkerService.Status.Connected || _suppressUpdates) return;

        var cmd = new UpdateProfileSettingsCommand(
            StopBuffsCity: StopBuffsCity,
            SoundEnabled: SoundEnabled
        );
        _worker.Send(cmd);
    }
}
