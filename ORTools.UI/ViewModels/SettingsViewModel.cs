using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using ORTools.Worker;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ORTools.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly WorkerService _worker;
    private readonly MainWindowViewModel _mainWindow;

    [ObservableProperty] private bool _debugMode;
    [ObservableProperty] private bool _debugView;
    [ObservableProperty] private double _debugViewHeight = AppConfig.DefaultDebugViewHeight;
    [ObservableProperty] private double _debugViewFontSize = AppConfig.DefaultDebugViewFontSize;
    [ObservableProperty] private bool _debugClientLog;
    [ObservableProperty] private bool _disableSystray;
    [ObservableProperty] private bool _minimizeToSystray = true;
    [ObservableProperty] private bool _closeToSystray = true;
    [ObservableProperty] private bool _pauseWhenChatting;
    [ObservableProperty] private bool _pauseWhenDead;
    [ObservableProperty] private bool _exitWithRo;
    [ObservableProperty] private bool _alwaysOnTop;
    [ObservableProperty] private bool _allowResizingWindow;
    [ObservableProperty] private bool _showExpPerHour;
    [ObservableProperty] private bool _checkForUpdatesOnStartup = true;
    [ObservableProperty] private ThemeMode _theme;
    [ObservableProperty] private bool _customThemeIsLight = AppConfig.DefaultCustomThemeIsLight;
    [ObservableProperty] private string _customThemeColor = AppConfig.DefaultCustomThemeColor;
    [ObservableProperty] private double _customThemeHue = 260;
    [ObservableProperty] private Language _language;

    private bool _suppressColorHueSync;
    private bool _suppressHueUpdate;

    public bool IsCustomTheme => Theme == ThemeMode.Custom;
    public bool CustomThemeIsDark
    {
        get => !CustomThemeIsLight;
        set
        {
            if (value && CustomThemeIsLight)
                CustomThemeIsLight = false;
            else if (!value && !CustomThemeIsLight)
                CustomThemeIsLight = true;
        }
    }

    // ── Update checker ────────────────────────────────────────────────────────
    [ObservableProperty] private int _selectedSettingsTabIndex = 0;
    [ObservableProperty] private string _updateStatusText = "";
    [ObservableProperty] private bool _isCheckingForUpdates;
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private string _downloadUrl = "";
    [ObservableProperty] private string _directZipUrl = "";

    public string CurrentVersionText => $"v{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(2)}";

    public ThemeMode[] ThemeModes => ThemeService.GetAvailableThemes();
    public Language[]  Languages  => new[] { Language.English, Language.Filipino };

    // Placeholders for Profile Settings
    [ObservableProperty] private bool _stopBuffsCity;
    [ObservableProperty] private bool _soundEnabled;
    [ObservableProperty] private bool _clearAutoOffTimerOnDisable;
    [ObservableProperty] private bool _pauseAutoOffTimerOnDisable;
    [ObservableProperty] private bool _startAutoOffTimerOnEnable;
    [ObservableProperty] private bool _keepDeadClientInfo;

    // Placeholders for Global Settings
    [ObservableProperty] private int _songRows = 4;
    [ObservableProperty] private int _macroSwitchRows = 4;
    [ObservableProperty] private int _atkDefRows = 2;
    [ObservableProperty] private string _defaultToggleStateKey = "None";
    [ObservableProperty] private HoldMultiKeyMode _holdMultiKeyMode = HoldMultiKeyMode.RoundRobinAll;
    [ObservableProperty] private AutopotPriorityMode _autopotPriorityMode = AutopotPriorityMode.LowestThresholdFirst;

    public bool IsLowestThresholdPriority
    {
        get => AutopotPriorityMode == AutopotPriorityMode.LowestThresholdFirst;
        set
        {
            if (value && AutopotPriorityMode != AutopotPriorityMode.LowestThresholdFirst)
                AutopotPriorityMode = AutopotPriorityMode.LowestThresholdFirst;
        }
    }

    public bool IsSlotOrderPriority
    {
        get => AutopotPriorityMode == AutopotPriorityMode.SlotOrder;
        set
        {
            if (value && AutopotPriorityMode != AutopotPriorityMode.SlotOrder)
                AutopotPriorityMode = AutopotPriorityMode.SlotOrder;
        }
    }

    public bool IsRoundRobinMode
    {
        get => HoldMultiKeyMode == HoldMultiKeyMode.RoundRobinAll;
        set
        {
            if (value && HoldMultiKeyMode != HoldMultiKeyMode.RoundRobinAll)
            {
                HoldMultiKeyMode = HoldMultiKeyMode.RoundRobinAll;
            }
        }
    }

    public bool IsLastPressedMode
    {
        get => HoldMultiKeyMode == HoldMultiKeyMode.LastPressedWins;
        set
        {
            if (value && HoldMultiKeyMode != HoldMultiKeyMode.LastPressedWins)
            {
                HoldMultiKeyMode = HoldMultiKeyMode.LastPressedWins;
            }
        }
    }

    private UpdateResult? _lastUpdateResult;

    public AutobuffSkillViewModel AutobuffSkill { get; }

    // Town / Safe Maps editor
    public ObservableCollection<string> CityList { get; } = new();
    [ObservableProperty] private string _newCityName = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentMap))]
    private string _currentMapName = "";
    public bool HasCurrentMap => !string.IsNullOrWhiteSpace(CurrentMapName);

    public SettingsViewModel(WorkerService worker, AutobuffSkillViewModel autobuffSkill, MainWindowViewModel mainWindow)
    {
        _worker = worker;
        _mainWindow = mainWindow;
        AutobuffSkill = autobuffSkill;
        _language = LanguageService.Current;
        _worker.GlobalConfigReceived += OnGlobalConfigReceived;
        _worker.CityListReceived += OnCityListReceived;
        _worker.CharacterReceived += OnCharacterReceived;
        _worker.ProfileSettingsReceived += OnProfileSettingsReceived;
        _worker.AppStateReceived += OnAppStateReceived;
        LanguageService.LanguageChanged += OnLanguageChangedEvent;
    }

    private void OnCharacterReceived(CharacterUpdate update)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(update.Map))
                CurrentMapName = update.Map.Trim();
        });
    }

    private void OnCityListReceived(CityListUpdate update)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            CityList.Clear();
            foreach (var city in update.Cities)
                CityList.Add(city);
        });
    }

    private void OnLanguageChangedEvent()
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_lastUpdateResult != null)
            {
                ApplyUpdateResult(_lastUpdateResult);
            }
        });
    }

    private void OnAppStateReceived(AppStateUpdate update)
    {
        // CRITICAL (Gotcha #7): IPC events fire on a background threadpool thread.
        // We MUST marshal any PropertyChanged events or UI-bound updates to the Dispatcher
        // to prevent WPF from throwing an InvalidOperationException and crashing the IPC loop.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => 
        {
            OnPropertyChanged(nameof(ThemeModes));
        });
    }

    private void OnGlobalConfigReceived(GlobalConfigUpdate update)
    {
        // CRITICAL (Gotcha #7): We must marshal to the UI thread because ThemeService touches
        // Application.Current.Resources, which has thread affinity and will throw otherwise.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => 
        {
            _suppressUpdates = true;
            SongRows = update.SongRows;
            MacroSwitchRows = update.MacroSwitchRows;
            AtkDefRows = update.AtkDefRows;
            DefaultToggleStateKey = update.DefaultToggleStateKey;
            DebugMode = update.DebugMode;
            DebugView = update.DebugView;
            DebugViewHeight = update.DebugViewHeight;
            DebugViewFontSize = update.DebugViewFontSize;
            DebugClientLog = update.DebugClientLog;
            DisableSystray = update.DisableSystray;
            MinimizeToSystray = update.MinimizeToSystray;
            CloseToSystray = update.CloseToSystray;
            PauseWhenChatting = update.PauseWhenChatting;
            PauseWhenDead = update.PauseWhenDead;
            ExitWithRo = update.ExitWithRo;
            AlwaysOnTop = update.AlwaysOnTop;
            AllowResizingWindow = update.AllowResizingWindow;
            ShowExpPerHour = update.ShowExpPerHour;
            CheckForUpdatesOnStartup = update.CheckForUpdatesOnStartup;
            Theme = update.Theme;
            CustomThemeIsLight = update.CustomThemeIsLight;
            CustomThemeColor = update.CustomThemeColor;
            HoldMultiKeyMode = update.HoldMultiKeyMode;
            AutopotPriorityMode = update.AutopotPriorityMode;
            OnPropertyChanged(nameof(IsRoundRobinMode));
            OnPropertyChanged(nameof(IsLastPressedMode));
            OnPropertyChanged(nameof(IsLowestThresholdPriority));
            OnPropertyChanged(nameof(IsSlotOrderPriority));
            OnPropertyChanged(nameof(IsCustomTheme));
            OnPropertyChanged(nameof(CustomThemeIsDark));
            _suppressUpdates = false;

            ThemeService.ApplyTheme(Theme, CustomThemeIsLight, CustomThemeColor);
        });
    }

    private void OnProfileSettingsReceived(ProfileSettingsUpdate update)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => 
        {
            _suppressUpdates = true;
            StopBuffsCity = update.StopBuffsCity;
            SoundEnabled = update.SoundEnabled;
            StartAutoOffTimerOnEnable = update.StartAutoOffTimerOnEnable;
            ClearAutoOffTimerOnDisable = update.ClearAutoOffTimerOnDisable;
            PauseAutoOffTimerOnDisable = update.PauseAutoOffTimerOnDisable;
            KeepDeadClientInfo = update.KeepDeadClientInfo;
            _suppressUpdates = false;
        });
    }

    private bool _suppressUpdates = false;

    partial void OnDebugModeChanged(bool value)
    {
        if (!value && DebugView)
        {
            DebugView = false;
        }
        SendGlobalUpdate();
    }
    partial void OnDebugViewChanged(bool value) => SendGlobalUpdate();
    partial void OnDebugViewHeightChanged(double value) => SendGlobalUpdate();
    partial void OnDebugViewFontSizeChanged(double value) => SendGlobalUpdate();
    partial void OnDebugClientLogChanged(bool value) => SendGlobalUpdate();
    partial void OnDisableSystrayChanged(bool value) => SendGlobalUpdate();
    partial void OnMinimizeToSystrayChanged(bool value) => SendGlobalUpdate();
    partial void OnCloseToSystrayChanged(bool value) => SendGlobalUpdate();
    partial void OnPauseWhenChattingChanged(bool value) => SendGlobalUpdate();
    partial void OnPauseWhenDeadChanged(bool value) => SendGlobalUpdate();
    partial void OnExitWithRoChanged(bool value) => SendGlobalUpdate();
    partial void OnAlwaysOnTopChanged(bool value) => SendGlobalUpdate();
    partial void OnAllowResizingWindowChanged(bool value) => SendGlobalUpdate();
    partial void OnShowExpPerHourChanged(bool value) => SendGlobalUpdate();
    partial void OnCheckForUpdatesOnStartupChanged(bool value) => SendGlobalUpdate();
    
    partial void OnThemeChanged(ThemeMode value)
    {
        OnPropertyChanged(nameof(IsCustomTheme));
        ThemeService.ApplyTheme(value, CustomThemeIsLight, CustomThemeColor);
        SendGlobalUpdate();
    }

    partial void OnCustomThemeIsLightChanged(bool value)
    {
        OnPropertyChanged(nameof(CustomThemeIsDark));
        if (Theme == ThemeMode.Custom)
        {
            ThemeService.ApplyTheme(ThemeMode.Custom, value, CustomThemeColor);
        }
        SendGlobalUpdate();
    }

    partial void OnCustomThemeColorChanged(string value)
    {
        if (!_suppressColorHueSync)
        {
            try
            {
                var curColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
                ThemeService.ColorToHsl(curColor, out double h, out _, out _);
                _suppressHueUpdate = true;
                CustomThemeHue = Math.Round(h);
                _suppressHueUpdate = false;
            }
            catch { }
        }

        if (Theme == ThemeMode.Custom)
        {
            ThemeService.ApplyTheme(ThemeMode.Custom, CustomThemeIsLight, value);
        }
        SendGlobalUpdate();
    }

    partial void OnCustomThemeHueChanged(double value)
    {
        if (_suppressHueUpdate) return;
        try
        {
            var curColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CustomThemeColor);
            ThemeService.ColorToHsl(curColor, out _, out double s, out double l);
            if (s < 0.2) s = 0.85;
            if (l < 0.3 || l > 0.7) l = 0.55;
            var newColor = ThemeService.HslToColor(value, s, l);
            _suppressColorHueSync = true;
            CustomThemeColor = $"#{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressColorHueSync = false;
        }
        catch { }
    }

    partial void OnLanguageChanged(Language value)
        => LanguageService.Apply(value);
    
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

    partial void OnHoldMultiKeyModeChanged(HoldMultiKeyMode value)
    {
        OnPropertyChanged(nameof(IsRoundRobinMode));
        OnPropertyChanged(nameof(IsLastPressedMode));
        SendGlobalUpdate();
    }

    partial void OnAutopotPriorityModeChanged(AutopotPriorityMode value)
    {
        OnPropertyChanged(nameof(IsLowestThresholdPriority));
        OnPropertyChanged(nameof(IsSlotOrderPriority));
        SendGlobalUpdate();
    }

    partial void OnStopBuffsCityChanged(bool value) => SendProfileUpdate();
    partial void OnSoundEnabledChanged(bool value) => SendProfileUpdate();
    partial void OnStartAutoOffTimerOnEnableChanged(bool value) => SendProfileUpdate();
    partial void OnClearAutoOffTimerOnDisableChanged(bool value) => SendProfileUpdate();
    partial void OnPauseAutoOffTimerOnDisableChanged(bool value) => SendProfileUpdate();
    partial void OnKeepDeadClientInfoChanged(bool value) => SendProfileUpdate();

    private void SendGlobalUpdate()
    {
        if (_worker.ConnectionStatus != WorkerService.Status.Connected || _suppressUpdates) return;

        var cmd = new UpdateGlobalConfigCommand(
            SongRows: SongRows,
            MacroSwitchRows: MacroSwitchRows,
            AtkDefRows: AtkDefRows,
            DefaultToggleStateKey: DefaultToggleStateKey,
            DebugMode: DebugMode,
            DebugView: DebugView,
            DebugViewHeight: Math.Clamp(DebugViewHeight, AppConfig.MinDebugViewHeight, AppConfig.MaxDebugViewHeight),
            DebugViewFontSize: Math.Clamp(DebugViewFontSize, AppConfig.MinDebugViewFontSize, AppConfig.MaxDebugViewFontSize),
            DebugClientLog: DebugClientLog,
            DisableSystray: DisableSystray,
            MinimizeToSystray: MinimizeToSystray,
            CloseToSystray: CloseToSystray,
            PauseWhenChatting: PauseWhenChatting,
            PauseWhenDead: PauseWhenDead,
            ExitWithRo: ExitWithRo,
            AlwaysOnTop: AlwaysOnTop,
            AllowResizingWindow: AllowResizingWindow,
            ShowExpPerHour: ShowExpPerHour,
            CheckForUpdatesOnStartup: CheckForUpdatesOnStartup,
            Theme: Theme,
            CustomThemeIsLight: CustomThemeIsLight,
            CustomThemeColor: CustomThemeColor,
            HoldMultiKeyMode: HoldMultiKeyMode,
            AutopotPriorityMode: AutopotPriorityMode
        );
        _worker.Send(cmd);
    }

    private void SendProfileUpdate()
    {
        if (_worker.ConnectionStatus != WorkerService.Status.Connected || _suppressUpdates) return;

        var cmd = new UpdateProfileSettingsCommand(
            StopBuffsCity: StopBuffsCity,
            SoundEnabled: SoundEnabled,
            StartAutoOffTimerOnEnable: StartAutoOffTimerOnEnable,
            ClearAutoOffTimerOnDisable: ClearAutoOffTimerOnDisable,
            PauseAutoOffTimerOnDisable: PauseAutoOffTimerOnDisable,
            KeepDeadClientInfo: KeepDeadClientInfo
        );
        _worker.Send(cmd);
    }

    [RelayCommand]
    private void ResetDebugViewHeight()
    {
        DebugViewHeight = AppConfig.DefaultDebugViewHeight;
    }

    [RelayCommand]
    private void ResetDebugViewFontSize()
    {
        DebugViewFontSize = AppConfig.DefaultDebugViewFontSize;
    }

    [RelayCommand]
    private void SelectPresetColor(string hex)
    {
        CustomThemeColor = hex;
    }

    [RelayCommand]
    private void AddCity()
    {
        string cleaned = (NewCityName ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleaned)) return;

        var list = CityList.ToList();
        if (!list.Any(c => string.Equals(c, cleaned, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(cleaned);
            _worker.Send(new UpdateCityListCommand(list));
        }
        NewCityName = "";
    }

    [RelayCommand]
    private void AddCurrentMap()
    {
        if (string.IsNullOrWhiteSpace(CurrentMapName)) return;
        string cleaned = CurrentMapName.Trim().ToLowerInvariant();
        var list = CityList.ToList();
        if (!list.Any(c => string.Equals(c, cleaned, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(cleaned);
            _worker.Send(new UpdateCityListCommand(list));
        }
    }

    [RelayCommand]
    private void RemoveCity(string city)
    {
        if (string.IsNullOrWhiteSpace(city)) return;
        var list = CityList.Where(c => !string.Equals(c, city, StringComparison.OrdinalIgnoreCase)).ToList();
        _worker.Send(new UpdateCityListCommand(list));
    }

    [RelayCommand]
    private async Task ResetCityList()
    {
        var confirm = new ConfirmDeleteDialogViewModel(
            LanguageService.Get("S.Settings.ConfirmResetTownMaps"),
            LanguageService.Get("S.Dialog.ConfirmReset"),
            LanguageService.Get("S.Dialog.Reset"));
        await _mainWindow.ShowDialogAsync(confirm);

        bool confirmed = await confirm.ResultTask;
        _mainWindow.CloseDialog();

        if (confirmed)
        {
            _worker.Send(new ResetCityListCommand());
        }
    }

    // ── Update checker ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusText = LanguageService.Get("S.Settings.UpdateChecking");

        var result = await UpdateCheckerService.CheckAsync(forceRefresh: true);
        ApplyUpdateResult(result);

        IsCheckingForUpdates = false;
    }

    [RelayCommand]
    private void OpenDownloadUrl()
    {
        if (!string.IsNullOrEmpty(DownloadUrl))
            Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenDirectZipUrl()
    {
        if (_lastUpdateResult != null && _lastUpdateResult.IsUpdateAvailable)
        {
            var dialog = new UpdateAvailableDialogViewModel(
                _lastUpdateResult.LatestVersion,
                _lastUpdateResult.ReleaseUrl ?? "",
                _mainWindow,
                _mainWindow,
                _lastUpdateResult.DownloadZipUrl);
            _ = _mainWindow.ShowDialogAsync(dialog);
            return;
        }

        if (!string.IsNullOrEmpty(DirectZipUrl))
            Process.Start(new ProcessStartInfo(DirectZipUrl) { UseShellExecute = true });
    }

    /// <summary>
    /// Apply an update result to the UI. Called from both the manual button and the silent startup check.
    /// </summary>
    public void ApplyUpdateResult(UpdateResult result)
    {
        _lastUpdateResult = result;
        if (result.ErrorMessage != null)
        {
            UpdateStatusText = result.ErrorMessage;
            HasUpdate = false;
        }
        else if (result.IsUpdateAvailable)
        {
            HasUpdate = true;
            DownloadUrl = result.ReleaseUrl ?? "";
            string modeTag = ThemeService.ServerMode == 0 ? "MR" : "HR";
            string tag = result.LatestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? result.LatestVersion : $"v{result.LatestVersion}";
            DirectZipUrl = !string.IsNullOrWhiteSpace(result.DownloadZipUrl)
                ? result.DownloadZipUrl
                : $"https://github.com/torrq/ORTools/releases/download/{tag}/OSROTools_{tag}-{modeTag}.zip";
            UpdateStatusText = string.Format(
                LanguageService.Get("S.Settings.UpdateAvailable"), result.LatestVersion);
        }
        else
        {
            HasUpdate = false;
            UpdateStatusText = LanguageService.Get("S.Settings.UpdateUpToDate");
        }
    }
}
