using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Helpers;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ORTools.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDialogService
{
    private readonly WorkerService _worker;

    [RelayCommand]
    private void ToggleTheme()
    {
        Settings.Theme = ThemeService.GetInvertedTheme();
    }

    [RelayCommand]
    private void ToggleColor()
    {
        Settings.Theme = ThemeService.GetNextColorTheme();
    }

    // ── Connection ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _connectionLabel = "Connecting...";
    [ObservableProperty] private bool   _isConnected;

    // ── App / client state ────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isApplicationOn;
    [ObservableProperty] private string _toggleKey = "None";
    [ObservableProperty] private string _appTitle = "OSRO Tools";
    [ObservableProperty] private string _baseAppTitle = "OSRO Tools";
    [ObservableProperty] private string _appLogoSource = "pack://application:,,,/ORTools;component/Views/ortools-hr.png";
#if SERVERMODE_HR
    private readonly string _baseIconPath = "pack://application:,,,/ORTools;component/Views/ortools-hr.ico";
#else
    private readonly string _baseIconPath = "pack://application:,,,/ORTools;component/Views/ortools-mr.ico";
#endif

    private ImageSource? _trayIconOn;
    private ImageSource? _trayIconOff;

    [ObservableProperty] private ImageSource? _trayIconSource;
    [ObservableProperty] private bool   _isClientConnected;
    [ObservableProperty] private string _connectedProcessName = "";

    public bool ForceExit { get; private set; }

    // ── Process list ──────────────────────────────────────────────────────────
    [ObservableProperty] private List<ProcessEntry> _processList    = new();
    [ObservableProperty] private ProcessEntry?      _selectedProcess;

    // ── Profiles ──────────────────────────────────────────────────────────────
    [ObservableProperty] private List<string> _profileList    = new() { "Default" };
    [ObservableProperty] private string       _currentProfile = "Default";

    // ── Character info ────────────────────────────────────────────────────────
    private DateTime _sessionStartTime;
    private long _totalExpGained;
    private uint _lastExp;
    private uint _lastLevel;
    private uint _lastExpToLevel;
    private string _lastCharName = "";
    private uint _currentHp;
    private uint _maxHp;
    private uint _currentSp;
    private uint _maxSp;


    // NumberFormatter logic moved to Utils/NumberFormatter.cs

    [ObservableProperty] private string _characterName = "—";
    [ObservableProperty] private string _infoExpPerHourText = "";
    [ObservableProperty] private string _infoExpPerHourTooltip = "";
    [ObservableProperty] private string _jobName       = "";
    [ObservableProperty] private string _mapName       = "";
    [ObservableProperty] private string _infoLvLabel1Text = "";
    [ObservableProperty] private string _infoLvValue1Text = "";
    [ObservableProperty] private string _infoSeparator1Text = "";
    [ObservableProperty] private string _infoLvLabel2Text = "";
    [ObservableProperty] private string _infoLvValue2Text = "";
    [ObservableProperty] private string _infoSeparator2Text = "";
    [ObservableProperty] private string _infoExpLabelText = "";
    [ObservableProperty] private string _infoExpValueText = "";

    [ObservableProperty] private double _hpPercent;
    [ObservableProperty] private double _spPercent;
    [ObservableProperty] private double _wtPercent;
    
    [ObservableProperty] private bool _isHpLow;
    [ObservableProperty] private bool _isSpLow;

    [ObservableProperty] private string _hpCurrentText = "";
    [ObservableProperty] private string _hpMaxText = "";
    [ObservableProperty] private string _hpSeparatorText = "";

    [ObservableProperty] private string _spCurrentText = "";
    [ObservableProperty] private string _spMaxText = "";
    [ObservableProperty] private string _spSeparatorText = "";

    [ObservableProperty] private string _wtCurrentText = "";
    [ObservableProperty] private string _wtMaxText = "";
    [ObservableProperty] private string _wtSeparatorText = "";
    [ObservableProperty] private string _wtPercentLeftBracketText = "";
    [ObservableProperty] private string _wtPercentValueText = "";
    [ObservableProperty] private string _wtPercentRightBracketText = "";
    [ObservableProperty] private string _wtLabelText = "";

    // ── Child ViewModels ──────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isMiniMode;

    [RelayCommand]
    private void ToggleMiniMode()
    {
        IsMiniMode = !IsMiniMode;
    }

    [ObservableProperty] private bool _isMiniTimerVisible;
    [ObservableProperty] private string _miniTimerText = "";

    public AutopotHPViewModel AutopotHP { get; }
    public AutopotSPViewModel AutopotSP { get; }
    public DebuffsViewModel Debuffs { get; }
    public SkillTimerViewModel SkillTimer { get; }
    public AutoOffViewModel AutoOff { get; }
    public AutobuffSearchViewModel AutobuffSearch { get; }
    public AutobuffSkillViewModel AutobuffSkill { get; }
    public AutobuffOrderViewModel AutobuffOrder { get; }
    public AutobuffItemViewModel AutobuffItem { get; }
    public SkillSpammerViewModel SkillSpammer { get; }
    public SettingsViewModel Settings { get; }
    public ProfilesViewModel Profiles { get; }
    public StatusLoggerViewModel StatusLogger { get; }
    public MiscViewModel Misc { get; }
    public MacroSwitchViewModel MacroSwitch { get; }
    public MacroSongViewModel MacroSong { get; }
    public AtkDefViewModel AtkDef { get; }

    // ── Derived display properties ────────────────────────────────────────────

    public string ToggleButtonText => LanguageService.Get(
        IsApplicationOn ? "S.Main.TurnOff" : "S.Main.TurnOn");

    public bool HasSelectedProcess => SelectedProcess != null;

    public Brush ConnectionDotBrush => IsConnected
        ? AppColors.Connected
        : AppColors.Disconnected;



    // ── Dialog Overlay ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isDialogVisible;
    [ObservableProperty] private ViewModelBase? _currentDialogContent;

    public async Task ShowDialogAsync(ViewModelBase dialogViewModel)
    {
        CurrentDialogContent = dialogViewModel;
        IsDialogVisible = true;
        // Wait until IsDialogVisible becomes false (managed by CloseDialog)
        // This is a simple implementation, a better way is for the caller to await
        // the specific TaskCompletionSource of the dialog. The IDialogService doesn't 
        // necessarily need to block here if the dialogViewModel has its own TCS.
        await Task.CompletedTask;
    }

    public void CloseDialog()
    {
        IsDialogVisible = false;
        CurrentDialogContent = null;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel(WorkerService worker)
    {
        _worker = worker;
        GenerateTrayIcons();
        AutopotSP = new AutopotSPViewModel(_worker);
        Debuffs = new DebuffsViewModel(_worker);
        SkillTimer = new SkillTimerViewModel(_worker, this);
        AutoOff = new AutoOffViewModel(_worker, this);
        AutobuffSkill = new AutobuffSkillViewModel(_worker);
        AutopotHP = new AutopotHPViewModel(_worker);
        AutobuffOrder = new AutobuffOrderViewModel(_worker);
        AutobuffItem = new AutobuffItemViewModel(_worker);
        AutobuffSearch = new AutobuffSearchViewModel(AutobuffSkill, AutobuffItem);
        SkillSpammer = new SkillSpammerViewModel(_worker);
        Settings = new SettingsViewModel(worker, AutobuffSkill);
        Profiles = new ProfilesViewModel(worker, this);
        StatusLogger = new StatusLoggerViewModel(worker);
        Misc = new MiscViewModel(worker);
        MacroSwitch = new MacroSwitchViewModel(_worker);
        MacroSong = new MacroSongViewModel(_worker);
        AtkDef = new AtkDefViewModel(_worker);

        // Map ViewModels to tabs
        Tabs = new ObservableCollection<object>
        {
            AutopotHP,
            AutopotSP,
            Debuffs,
            SkillTimer,
            AutobuffSearch,
            AutobuffSkill,
            AutobuffItem,
            SkillSpammer,
            Profiles
        };
        worker.AppStateReceived    += OnAppState;
        worker.ClientStateReceived += OnClientState;
        worker.HpSpReceived        += OnHpSp;
        worker.CharacterReceived   += OnCharacter;
        worker.ProcessListReceived += OnProcessList;
        worker.ConnectionChanged   += OnConnectionChanged;
        worker.ProfileListReceived += OnProfileList;
        worker.ErrorReceived       += OnError;
        worker.LogMessageReceived  += OnLogMessage;
        worker.AutoOffTimerStateReceived += OnAutoOffTimerState;

        // Initialize localized labels
        WtLabelText = LanguageService.Get("S.Main.Weight") + " ";
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        WtLabelText = LanguageService.Get("S.Main.Weight") + " ";
        OnPropertyChanged(nameof(ToggleButtonText));
    }

    public ObservableCollection<object> Tabs { get; }
    public ObservableCollection<LogMessageItem> LogMessages { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleApplication()
    {
        if (IsApplicationOn)
            _worker.Send(new TurnOffCommand());
        else
            _worker.Send(new TurnOnCommand());
    }

    [RelayCommand]
    private void ConnectToProcess()
    {
        if (SelectedProcess is { } p)
            _worker.Send(new ConnectClientCommand(p.Id));
    }

    [RelayCommand]
    private void DisconnectClient()
        => _worker.Send(new DisconnectClientCommand());

    [RelayCommand]
    private void ShowWindow()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Show();
            if (mainWindow.WindowState == WindowState.Minimized)
                mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }
    }

    [RelayCommand]
    private void SelectProfile(string profile)
    {
        if (!string.IsNullOrEmpty(profile))
            CurrentProfile = profile;
    }

    [RelayCommand]
    private void ExitApp()
    {
        ForceExit = true;
        _worker.Send(new ShutdownCommand());
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void ResetExpTracker()
    {
        _sessionStartTime = default;
        _totalExpGained = 0;
        InfoExpPerHourText = "XP/h: Calc...";
        InfoExpPerHourTooltip = "Waiting for EXP gain...\nClick to reset";
    }

    [RelayCommand]
    private void RefreshProcessList()
        => _worker.Send(new RequestProcessListCommand());

    [RelayCommand]
    private void UpdateToggleKey(string key)
        => _worker.Send(new UpdateToggleKeyCommand(key));

    // Triggered when user picks a different profile in the ComboBox
    private bool _suppressProfileCommand;
    partial void OnCurrentProfileChanged(string value)
    {
        if (!_suppressProfileCommand)
            _worker.Send(new SwitchProfileCommand(value));
    }

    private bool _suppressProcessCommand;
    partial void OnSelectedProcessChanged(ProcessEntry? value)
    {
        OnPropertyChanged(nameof(HasSelectedProcess));
        if (!_suppressProcessCommand && value != null)
        {
            _worker.Send(new ConnectClientCommand(value.Id));
        }
    }

    // Recompute derived properties when their inputs change
    partial void OnIsApplicationOnChanged(bool value)
    {
        OnPropertyChanged(nameof(ToggleButtonText));
        if (_trayIconOn != null && _trayIconOff != null)
        {
            TrayIconSource = value ? _trayIconOn : _trayIconOff;
        }
    }
    partial void OnIsConnectedChanged(bool value)       => OnPropertyChanged(nameof(ConnectionDotBrush));



    // ── Event handlers — all marshal to UI thread ─────────────────────────────

    private void OnConnectionChanged(WorkerService.Status s) =>
        Post(() =>
        {
            IsConnected      = s == WorkerService.Status.Connected;
            ConnectionLabel  = s switch
            {
                WorkerService.Status.Connected    => "AUS!!",
                WorkerService.Status.Connecting   => "FOCK!",
                WorkerService.Status.Disconnected => "DISCONNECTED",
                _                                 => "UNKNOWN"
            };
        });

    private void OnAppState(AppStateUpdate u) =>
        Post(() =>
        {
            IsApplicationOn = u.IsOn;
            ToggleKey       = u.ToggleKey ?? "None";
            BaseAppTitle   = u.AppTitle ?? "OSRO Tools";
            UpdateAppTitle();
            AppLogoSource   = u.ServerMode == 1
                ? "pack://application:,,,/ORTools;component/Views/ortools-hr.png"
                : "pack://application:,,,/ORTools;component/Views/ortools-mr.png";
            ORTools.UI.Services.ThemeService.SetServerMode(u.ServerMode);
        });

    private void UpdateAppTitle()
    {
        if (IsMiniTimerVisible)
        {
            AppTitle = $"{BaseAppTitle} - Auto Off: {MiniTimerText}";
        }
        else
        {
            AppTitle = BaseAppTitle;
        }
    }

    private void OnAutoOffTimerState(AutoOffTimerStateUpdate u) =>
        Post(() =>
        {
            if (u.IsRunning)
            {
                IsMiniTimerVisible = true;
                
                int rMin = (u.RemainingSeconds + 59) / 60;
                int rHr = rMin / 60;
                int rM = rMin % 60;
                MiniTimerText = rHr > 0 ? $"{rHr}h {rM}m" : $"{rM}m";
                UpdateAppTitle();
            }
            else
            {
                IsMiniTimerVisible = false;
                MiniTimerText = "";
                UpdateAppTitle();
            }
        });

    private void OnClientState(ClientStateUpdate u) =>
        Post(() =>
        {
            IsClientConnected    = u.Connected;
            ConnectedProcessName = u.ProcessName ?? "";

            if (!IsClientConnected)
            {
                _suppressProcessCommand = true;
                SelectedProcess = null;
                _suppressProcessCommand = false;

                if (!Settings.KeepDeadClientInfo)
                {
                    ClearCharacterInfo();
                }
            }

            if (IsClientConnected && !string.IsNullOrEmpty(ConnectedProcessName) && ProcessList.Count > 0)
            {
                var match = ProcessList.FirstOrDefault(p => p.Id == ConnectedProcessName);
                if (match != null && SelectedProcess?.Id != match.Id)
                {
                    _suppressProcessCommand = true;
                    SelectedProcess = match;
                    _suppressProcessCommand = false;
                }
            }
        });

    private void OnHpSp(HpSpUpdate u) =>
        Post(() =>
        {
            if (!IsClientConnected) return;

            _currentHp = u.CurrentHp;
            _maxHp = u.MaxHp;
            _currentSp = u.CurrentSp;
            _maxSp = u.MaxSp;

            HpPercent = u.MaxHp > 0 ? (double)u.CurrentHp / u.MaxHp * 100.0 : 0;
            SpPercent = u.MaxSp > 0 ? (double)u.CurrentSp / u.MaxSp * 100.0 : 0;
            IsHpLow = HpPercent < 25;
            IsSpLow = SpPercent < 25;

            if (u.MaxHp > 0)
            {
                HpCurrentText = $"{u.CurrentHp:N0}";
                HpSeparatorText = "/";
                HpMaxText = $"{u.MaxHp:N0}";
            }
            else
            {
                HpCurrentText = "";
                HpSeparatorText = "";
                HpMaxText = "";
            }

            if (u.MaxSp > 0)
            {
                SpCurrentText = $"{u.CurrentSp:N0}";
                SpSeparatorText = "/";
                SpMaxText = $"{u.MaxSp:N0}";
            }
            else
            {
                SpCurrentText = "";
                SpSeparatorText = "";
                SpMaxText = "";
            }
        });

    private void OnCharacter(CharacterUpdate u) =>
        Post(() =>
        {
            if (!IsClientConnected) return;

            CharacterName = u.Name;
            JobName       = JobList.GetNameById((int)u.JobId);
            MapName       = u.Map;
            WtPercent     = u.WeightMax > 0 ? (double)u.WeightCur / u.WeightMax * 100.0 : 0;

            if (u.WeightMax > 0)
            {
                WtCurrentText = $"{u.WeightCur:N0}";
                WtSeparatorText = "/";
                WtMaxText = $"{u.WeightMax:N0}";
                WtPercentLeftBracketText = " (";
                WtPercentValueText = $"{(int)WtPercent}%";
                WtPercentRightBracketText = ")";
            }
            else
            {
                WtCurrentText = "";
                WtSeparatorText = "";
                WtMaxText = "";
                WtPercentLeftBracketText = "";
                WtPercentValueText = "";
                WtPercentRightBracketText = "";
            }

            if (_sessionStartTime == default || _lastCharName != u.Name)
            {
                _sessionStartTime = DateTime.Now;
                _totalExpGained = 0;
            }
            else
            {
                long diff = 0;
                if (u.Level == _lastLevel)
                {
                    diff = (long)u.Exp - _lastExp;
                }
                else if (u.Level > _lastLevel)
                {
                    diff = (long)_lastExpToLevel - _lastExp + u.Exp;
                }

                // Start the clock on the very first EXP gain, so town idle time doesn't ruin the average
                if (_totalExpGained <= 0 && diff > 0)
                {
                    _sessionStartTime = DateTime.Now;
                }

                _totalExpGained += diff;

                if (_totalExpGained < 0)
                {
                    _totalExpGained = 0; // Prevent negative total if they die immediately after login
                }
            }



            _lastExp = u.Exp;
            _lastLevel = u.Level;
            _lastExpToLevel = u.ExpToLevel;
            _lastCharName = u.Name;

            string expPct = u.ExpToLevel > 0
                ? $"{(double)u.Exp / u.ExpToLevel * 100:0.00}%"
                : "100%";

            if (Settings.ShowExpPerHour)
            {
                if (_totalExpGained > 0)
                {
                    var timeElapsed = DateTime.Now - _sessionStartTime;
                    InfoExpPerHourTooltip = $"Logging for: {(int)timeElapsed.TotalHours:00}:{timeElapsed.Minutes:00}:{timeElapsed.Seconds:00}\nClick to reset";

                    if (timeElapsed.TotalMinutes >= 1)
                    {
                        double expPerHour = _totalExpGained / timeElapsed.TotalHours;
                        InfoExpPerHourText = $"XP/h: {Utils.NumberFormatter.FormatExp(expPerHour)}";
                    }
                    else
                    {
                        InfoExpPerHourText = "XP/h: Calc...";
                    }
                }
                else
                {
                    InfoExpPerHourText = "";
                    InfoExpPerHourTooltip = "Waiting for EXP gain...\nClick to reset";
                }
            }
            else
            {
                InfoExpPerHourText = "";
            }

            InfoLvLabel1Text = "Lv";
            InfoLvValue1Text = $"{u.Level}";
            InfoSeparator1Text = " / ";
            InfoLvLabel2Text = "Lv";
            InfoLvValue2Text = $"{u.JobLevel}";
            InfoSeparator2Text = " / ";
            InfoExpLabelText = "Exp";
            InfoExpValueText = expPct;
        });

    private void OnProcessList(ProcessListUpdate u) =>
        Post(() =>
        {
            ProcessList = u.Processes;

            if (IsClientConnected && !string.IsNullOrEmpty(ConnectedProcessName))
            {
                var match = ProcessList.FirstOrDefault(p => p.Id == ConnectedProcessName);
                if (match != null && SelectedProcess?.Id != match.Id)
                {
                    _suppressProcessCommand = true;
                    SelectedProcess = match;
                    _suppressProcessCommand = false;
                }
            }
        });

    private void OnProfileList(ProfileListUpdate u) =>
        Post(() =>
        {
            _suppressProfileCommand = true;
            ProfileList    = u.Profiles;
            CurrentProfile = u.CurrentProfile;
            _suppressProfileCommand = false;
        });

    private void OnError(ErrorUpdate u) =>
        Post(() => Console.WriteLine($"[Worker Error] {u.Message}"));
        // Phase 3: show in a status bar or toast notification

    private void OnLogMessage(LogMessageUpdate u) =>
        Post(() =>
        {
            if (LogMessages.Count > 500)
                LogMessages.RemoveAt(0);

            var timeMatch = Regex.Match(u.Message, @"^\[(\d{2}:\d{2}:\d{2})\]\[(.*?)\] (.*)$");
            string timestamp = timeMatch.Success ? timeMatch.Groups[1].Value : DateTime.Now.ToString("HH:mm:ss");
            string text = timeMatch.Success ? timeMatch.Groups[3].Value : u.Message;

            SolidColorBrush defaultColor = u.Level switch
            {
                "I" => AppColors.Info,
                "W" => AppColors.Warning,
                "E" => AppColors.Error,
                "D" => AppColors.Debug,
                "S" => AppColors.Status,
                _ => AppColors.Default
            };

            var item = new LogMessageItem(timestamp, u.Level, defaultColor);
            item.Segments.Add(new LogTextSegment("[", AppColors.Bracket));
            item.Segments.Add(new LogTextSegment(timestamp, AppColors.Bracket));
            item.Segments.Add(new LogTextSegment("][", AppColors.Bracket));
            item.Segments.Add(new LogTextSegment(u.Level, defaultColor));
            item.Segments.Add(new LogTextSegment("] ", AppColors.Bracket));

            if (u.Level == "S")
            {
                string[] statuses = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var statusEntry in statuses)
                {
                    string[] parts = statusEntry.Split(':');
                    if (parts.Length == 2)
                    {
                        item.Segments.Add(new LogTextSegment(parts[0], AppColors.StatusId));
                        item.Segments.Add(new LogTextSegment(":", AppColors.Separator));

                        if (parts[1] == "**UNKNOWN**")
                            item.Segments.Add(new LogTextSegment(parts[1], AppColors.StatusUnknown));
                        else
                            item.Segments.Add(new LogTextSegment(parts[1], AppColors.StatusKnown));
                    }
                    else
                    {
                        item.Segments.Add(new LogTextSegment(statusEntry, AppColors.Separator));
                    }
                    item.Segments.Add(new LogTextSegment(" ", AppColors.Separator));
                }
            }
            else
            {
                item.Segments.Add(new LogTextSegment(text, defaultColor));
            }

            LogMessages.Add(item);
        });

    private static void Post(Action action)
        => Application.Current?.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);

    private void ClearCharacterInfo()
    {
        CharacterName = "—";
        JobName = "";
        MapName = "";
        InfoLvLabel1Text = "";
        InfoLvValue1Text = "";
        InfoSeparator1Text = "";
        InfoLvLabel2Text = "";
        InfoLvValue2Text = "";
        InfoSeparator2Text = "";
        InfoExpLabelText = "";
        InfoExpValueText = "";
        InfoExpPerHourText = "";
        InfoExpPerHourTooltip = "";

        HpPercent = 0;
        SpPercent = 0;
        WtPercent = 0;
        IsHpLow = false;
        IsSpLow = false;

        HpCurrentText = "";
        HpMaxText = "";
        HpSeparatorText = "";
        SpCurrentText = "";
        SpMaxText = "";
        SpSeparatorText = "";
        WtCurrentText = "";
        WtMaxText = "";
        WtSeparatorText = "";
        WtPercentLeftBracketText = "";
        WtPercentValueText = "";
        WtPercentRightBracketText = "";
        WtPercentRightBracketText = "";
    }

    private void GenerateTrayIcons()
    {
        try
        {
            var baseImage = new System.Windows.Media.Imaging.BitmapImage();
            baseImage.BeginInit();
            baseImage.UriSource = new System.Uri(_baseIconPath);
            baseImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            baseImage.EndInit();

            _trayIconOn = CreateIconWithDot(baseImage, Brushes.LimeGreen);
            _trayIconOff = CreateIconWithDot(baseImage, Brushes.Red);
            TrayIconSource = _trayIconOff;
        }
        catch
        {
            // Fallback
        }
    }

    private ImageSource CreateIconWithDot(System.Windows.Media.Imaging.BitmapImage baseImage, Brush dotColor)
    {
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawImage(baseImage, new Rect(0, 0, 16, 16));
            var pen = new Pen(Brushes.Black, 1.0);
            ctx.DrawEllipse(dotColor, pen, new Point(12, 12), 3.5, 3.5);
        }
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
