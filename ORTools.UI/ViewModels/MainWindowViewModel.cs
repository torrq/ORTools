using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ORTools.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly WorkerService _worker;

    // ── Connection ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _connectionLabel = "Connecting...";
    [ObservableProperty] private bool   _isConnected;

    // ── App / client state ────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isApplicationOn;
    [ObservableProperty] private string _toggleKey = "None";
    [ObservableProperty] private string _appTitle = "OSRO Tools";
    [ObservableProperty] private bool   _isClientConnected;
    [ObservableProperty] private string _connectedProcessName = "";

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

    public bool IsKeyInUse(string newKey, object? sourceVM = null)
    {
        if (string.IsNullOrWhiteSpace(newKey) || newKey == "None") return false;
        if ((sourceVM as string) == "Settings_DefaultToggleKey") return false;
        
        if (ToggleKey == newKey && (sourceVM as string) != "ToggleKeyTextBox") return true;
        
        if (SkillSpammer.ToggleModeKey == newKey && (sourceVM as string) != "SkillSpammer_ToggleKey") return true;
        foreach (var entry in SkillSpammer.AllKeys) if (entry.KeyName == newKey && entry.IsCheckedState != false && sourceVM != entry) return true;

        foreach (var slot in AutopotHP.Slots) if (slot.Key == newKey && sourceVM != slot) return true;
        foreach (var slot in AutopotSP.Slots) if (slot.Key == newKey && sourceVM != slot) return true;
        
        foreach (var sr in Debuffs.StatusRecoveryItems)
        {
            if (sr.Key == "None") continue;
            if (sr.Key == newKey && sourceVM != sr) return true;
        }

        foreach (var dr in Debuffs.DebuffItems)
        {
            if (dr.Key == "None") continue;
            if (dr.Key == newKey && sourceVM != dr) return true;
        }

        foreach (var st in SkillTimer.Slots) if (st.Key == newKey && sourceVM != st) return true;

        foreach (var group in AutobuffSkill.SkillGroups)
        {
            foreach (var item in group.Items)
            {
                if (item.Key == "None") continue;
                if (item.Key == newKey && sourceVM != item) return true;
            }
        }

        foreach (var group in AutobuffItem.ItemGroups)
        {
            foreach (var item in group.Items)
            {
                if (item.Key == "None") continue;
                if (item.Key == newKey && sourceVM != item) return true;
            }
        }
        
        if (Misc.TransferKey == newKey && (sourceVM as string) != "Misc_TransferKey") return true;

        foreach (var row in MacroSwitch.Rows)
        {
            if (row.TriggerKey != "None" && row.TriggerKey == newKey && sourceVM != row) return true;
            foreach (var step in row.Steps)
            {
                if (step.Key != "None" && step.Key == newKey && sourceVM != step) return true;
            }
        }

        foreach (var row in MacroSong.Rows)
        {
            if (row.TriggerKey == newKey && sourceVM != row) return true;
            if (row.AdaptationKey == newKey && sourceVM != row) return true;
            if (row.InstrumentKey == newKey && sourceVM != row) return true;
            foreach (var step in row.Steps)
            {
                if (step.Key == "None") continue;
                if (step.Key == newKey && sourceVM != step) return true;
            }
        }

        foreach (var row in AtkDef.Rows)
        {
            if (row.SpammerKey == newKey && sourceVM != row) return true;
        }

        return false;
    }
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
    }

    public ObservableCollection<object> Tabs { get; }

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

    private static void Post(Action action)
        => Application.Current?.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);

    private static SolidColorBrush CreateBrush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex)!);
}
