using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ORTools.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly WorkerService _worker;

    [RelayCommand]
    private void ToggleTheme()
    {
        Settings.Theme = ThemeService.GetInvertedTheme();
    }

    // ── Connection ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _connectionLabel = "Connecting...";
    [ObservableProperty] private bool   _isConnected;

    // ── App / client state ────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isApplicationOn;
    [ObservableProperty] private string _toggleKey = "None";
    [ObservableProperty] private string _appTitle = "OSRO Tools";
    [ObservableProperty] private string _appLogoSource = "pack://application:,,,/ORTools;component/Views/ortools-hr.png";
#if SERVERMODE_HR
    [ObservableProperty] private string _trayIconSource = "pack://application:,,,/ORTools;component/Views/ortools-hr.ico";
#else
    [ObservableProperty] private string _trayIconSource = "pack://application:,,,/ORTools;component/Views/ortools-mr.ico";
#endif
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
    [ObservableProperty] private string _characterName = "—";
    [ObservableProperty] private string _jobName       = "";
    [ObservableProperty] private string _mapName       = "";
    [ObservableProperty] private string _infoLine      = "";

    [ObservableProperty] private double _hpPercent;
    [ObservableProperty] private double _spPercent;
    [ObservableProperty] private double _wtPercent;

    [ObservableProperty] private string _hpText = "HP";
    [ObservableProperty] private string _spText = "SP";
    [ObservableProperty] private string _wtText = "Weight";

    // ── Child ViewModels ──────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isMiniMode;

    [RelayCommand]
    private void ToggleMiniMode()
    {
        IsMiniMode = !IsMiniMode;
    }

    public AutopotHPViewModel AutopotHP { get; }
    public AutopotSPViewModel AutopotSP { get; }
    public DebuffsViewModel Debuffs { get; }
    public SkillTimerViewModel SkillTimer { get; }
    public AutoOffViewModel AutoOff { get; }
    public AutobuffSkillViewModel AutobuffSkill { get; }
    public AutobuffOrderViewModel AutobuffOrder { get; }
    public AutobuffItemViewModel AutobuffItem { get; }
    public SkillSpammerViewModel SkillSpammer { get; }
    public SettingsViewModel Settings { get; }
    public ProfilesViewModel Profiles { get; }
    public MiscViewModel Misc { get; }
    public MacroSwitchViewModel MacroSwitch { get; }
    public MacroSongViewModel MacroSong { get; }
    public AtkDefViewModel AtkDef { get; }

    // ── Derived display properties ────────────────────────────────────────────

    public string ToggleButtonText => IsApplicationOn ? "Turn OFF" : "Turn ON";

    public bool HasSelectedProcess => SelectedProcess != null;

    public Brush ConnectionDotBrush => IsConnected
        ? CreateBrush("#4CAF50")
        : CreateBrush("#FF5252");

    public Brush HpBarBrush => HpPercent < 25
        ? CreateBrush("#F44336")
        : CreateBrush("#4CAF50");

    public Brush SpBarBrush => SpPercent < 25
        ? CreateBrush("#FF9800")
        : CreateBrush("#2196F3");

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel(WorkerService worker)
    {
        _worker = worker;
        
        AutopotHP = new AutopotHPViewModel(_worker);
        AutopotSP = new AutopotSPViewModel(_worker);
        Debuffs = new DebuffsViewModel(_worker);
        SkillTimer = new SkillTimerViewModel(_worker);
        AutoOff = new AutoOffViewModel(_worker);
        AutobuffSkill = new AutobuffSkillViewModel(_worker);
        AutobuffOrder = new AutobuffOrderViewModel(_worker);
        AutobuffItem = new AutobuffItemViewModel(_worker);
        SkillSpammer = new SkillSpammerViewModel(_worker);
        Settings = new SettingsViewModel(worker, AutobuffSkill);
        Profiles = new ProfilesViewModel(worker);
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
    partial void OnIsApplicationOnChanged(bool value)  => OnPropertyChanged(nameof(ToggleButtonText));
    partial void OnIsConnectedChanged(bool value)       => OnPropertyChanged(nameof(ConnectionDotBrush));
    partial void OnHpPercentChanged(double value)       => OnPropertyChanged(nameof(HpBarBrush));
    partial void OnSpPercentChanged(double value)       => OnPropertyChanged(nameof(SpBarBrush));


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
            AppTitle        = u.AppTitle ?? "OSRO Tools";
            AppLogoSource   = u.ServerMode == 1 
                ? "pack://application:,,,/ORTools;component/Views/ortools-hr.png" 
                : "pack://application:,,,/ORTools;component/Views/ortools-mr.png";
            ORTools.UI.Services.ThemeService.SetServerMode(u.ServerMode);
        });

    private void OnClientState(ClientStateUpdate u) =>
        Post(() =>
        {
            IsClientConnected    = u.Connected;
            ConnectedProcessName = u.ProcessName ?? "";

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
            HpPercent = u.MaxHp > 0 ? (double)u.CurrentHp / u.MaxHp * 100.0 : 0;
            SpPercent = u.MaxSp > 0 ? (double)u.CurrentSp / u.MaxSp * 100.0 : 0;
            HpText    = $"{u.CurrentHp:N0} / {u.MaxHp:N0}";
            SpText    = $"{u.CurrentSp:N0} / {u.MaxSp:N0}";
        });

    private void OnCharacter(CharacterUpdate u) =>
        Post(() =>
        {
            CharacterName = u.Name;
            JobName       = JobList.GetNameById((int)u.JobId);
            MapName       = u.Map;
            WtPercent     = u.WeightMax > 0 ? (double)u.WeightCur / u.WeightMax * 100.0 : 0;
            WtText        = $"{u.WeightCur} / {u.WeightMax}";

            string expPct = u.ExpToLevel > 0
                ? $"{(double)u.Exp / u.ExpToLevel * 100:0.00}%"
                : "100%";
            InfoLine = $"Lv{u.Level} / Lv{u.JobLevel} / Exp {expPct}";
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
                "I" => CreateBrush("#4CAF50"), // Green
                "W" => CreateBrush("#FF9800"), // Orange
                "E" => CreateBrush("#F44336"), // Red
                "D" => CreateBrush("#9E9E9E"), // Grey
                "S" => CreateBrush("#9C27B0"), // Purple
                _ => CreateBrush("#E0E0E0")
            };

            var item = new LogMessageItem(timestamp, u.Level, defaultColor);
            item.Segments.Add(new LogTextSegment("[", CreateBrush("#757575")));
            item.Segments.Add(new LogTextSegment(timestamp, CreateBrush("#757575")));
            item.Segments.Add(new LogTextSegment("][", CreateBrush("#757575")));
            item.Segments.Add(new LogTextSegment(u.Level, defaultColor));
            item.Segments.Add(new LogTextSegment("] ", CreateBrush("#757575")));

            if (u.Level == "S")
            {
                string[] statuses = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var statusEntry in statuses)
                {
                    string[] parts = statusEntry.Split(':');
                    if (parts.Length == 2)
                    {
                        item.Segments.Add(new LogTextSegment(parts[0], CreateBrush("#64B5F6"))); // ID in Blue
                        item.Segments.Add(new LogTextSegment(":", CreateBrush("#E0E0E0")));
                        
                        if (parts[1] == "**UNKNOWN**")
                            item.Segments.Add(new LogTextSegment(parts[1], CreateBrush("#F44336"))); // Red
                        else
                            item.Segments.Add(new LogTextSegment(parts[1], CreateBrush("#81C784"))); // Light Green
                    }
                    else
                    {
                        item.Segments.Add(new LogTextSegment(statusEntry, CreateBrush("#E0E0E0")));
                    }
                    item.Segments.Add(new LogTextSegment(" ", CreateBrush("#E0E0E0")));
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

    private static SolidColorBrush CreateBrush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex)!);
}
