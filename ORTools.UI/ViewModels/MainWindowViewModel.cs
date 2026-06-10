using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ORTools.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly WorkerService _worker;

    // ── Connection ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _connectionLabel = "Connecting...";
    [ObservableProperty] private bool _isConnected;

    // ── App / client state ────────────────────────────────────────────────────
    [ObservableProperty] private bool _isApplicationOn;
    [ObservableProperty] private bool _isClientConnected;
    [ObservableProperty] private string _connectedProcessName = "";

    // ── Process list ──────────────────────────────────────────────────────────
    [ObservableProperty] private List<string> _processList = new();
    [ObservableProperty] private string? _selectedProcess;

    // ── Profiles ──────────────────────────────────────────────────────────────
    [ObservableProperty] private List<string> _profileList = new() { "Default" };
    [ObservableProperty] private string _currentProfile = "Default";

    // ── Character info ────────────────────────────────────────────────────────
    [ObservableProperty] private string _characterName = "—";
    [ObservableProperty] private string _mapName = "";
    [ObservableProperty] private string _infoLine = "";
    [ObservableProperty] private double _hpPercent;
    [ObservableProperty] private double _spPercent;
    [ObservableProperty] private double _wtPercent;
    [ObservableProperty] private string _hpText = "HP";
    [ObservableProperty] private string _spText = "SP";
    [ObservableProperty] private string _wtText = "Weight";

    // ── Tab child ViewModels ──────────────────────────────────────────────────
    public AutopotHPViewModel AutopotHP { get; }
    public AutopotSPViewModel AutopotSP { get; }

    // ── Derived ───────────────────────────────────────────────────────────────
    public string ToggleButtonText => IsApplicationOn ? "Turn OFF" : "Turn ON";
    public bool HasSelectedProcess => !string.IsNullOrWhiteSpace(SelectedProcess);

    public Brush ConnectionDotBrush => IsConnected
        ? CreateBrush("#4CAF50") : CreateBrush("#FF5252");

    public Brush HpBarBrush => HpPercent < 25
        ? CreateBrush("#F44336") : CreateBrush("#4CAF50");

    public Brush SpBarBrush => SpPercent < 25
        ? CreateBrush("#FF9800") : CreateBrush("#2196F3");

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindowViewModel(WorkerService worker)
    {
        _worker = worker;

        AutopotHP = new AutopotHPViewModel(worker);
        AutopotSP = new AutopotSPViewModel(worker);

        worker.ConnectionChanged += OnConnectionChanged;
        worker.AppStateReceived += OnAppState;
        worker.ClientStateReceived += OnClientState;
        worker.HpSpReceived += OnHpSp;
        worker.CharacterReceived += OnCharacter;
        worker.ProcessListReceived += OnProcessList;
        worker.ProfileListReceived += OnProfileList;
        worker.ErrorReceived += OnError;
        worker.AutopotHPConfigReceived += AutopotHP.OnConfigUpdate;
        worker.AutopotSPConfigReceived += AutopotSP.OnConfigUpdate;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleApplication()
    {
        if (IsApplicationOn) _worker.Send(new TurnOffCommand());
        else _worker.Send(new TurnOnCommand());
    }

    [RelayCommand]
    private void ConnectToProcess()
    {
        if (SelectedProcess is { } p) _worker.Send(new ConnectClientCommand(p));
    }

    [RelayCommand]
    private void DisconnectClient() => _worker.Send(new DisconnectClientCommand());

    [RelayCommand]
    private void RefreshProcessList() => _worker.Send(new RequestProcessListCommand());

    private bool _suppressProfileCommand;
    partial void OnCurrentProfileChanged(string value)
    {
        if (!_suppressProfileCommand) _worker.Send(new SwitchProfileCommand(value));
    }

    partial void OnIsApplicationOnChanged(bool value) => OnPropertyChanged(nameof(ToggleButtonText));
    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectionDotBrush));
    partial void OnHpPercentChanged(double value) => OnPropertyChanged(nameof(HpBarBrush));
    partial void OnSpPercentChanged(double value) => OnPropertyChanged(nameof(SpBarBrush));
    partial void OnSelectedProcessChanged(string? value) => OnPropertyChanged(nameof(HasSelectedProcess));

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnConnectionChanged(WorkerService.Status s) =>
        Post(() =>
        {
            IsConnected = s == WorkerService.Status.Connected;
            ConnectionLabel = s switch
            {
                WorkerService.Status.Connected => "Connected",
                WorkerService.Status.Connecting => "Connecting...",
                WorkerService.Status.Disconnected => "Disconnected",
                _ => "Unknown"
            };
        });

    private void OnAppState(AppStateUpdate u) => Post(() => IsApplicationOn = u.IsOn);

    private void OnClientState(ClientStateUpdate u) =>
        Post(() =>
        {
            IsClientConnected = u.Connected;
            ConnectedProcessName = u.ProcessName ?? "";
        });

    private void OnHpSp(HpSpUpdate u) =>
        Post(() =>
        {
            HpPercent = u.MaxHp > 0 ? (double)u.CurrentHp / u.MaxHp * 100.0 : 0;
            SpPercent = u.MaxSp > 0 ? (double)u.CurrentSp / u.MaxSp * 100.0 : 0;
            HpText = $"HP  {u.CurrentHp:N0} / {u.MaxHp:N0}";
            SpText = $"SP  {u.CurrentSp:N0} / {u.MaxSp:N0}";
        });

    private void OnCharacter(CharacterUpdate u) =>
        Post(() =>
        {
            CharacterName = u.Name;
            MapName = u.Map;
            WtPercent = u.WeightMax > 0 ? (double)u.WeightCur / u.WeightMax * 100.0 : 0;
            WtText = $"Weight  {u.WeightCur} / {u.WeightMax}";
            string expPct = u.ExpToLevel > 0
                ? $"{(double)u.Exp / u.ExpToLevel * 100:0.00}%" : "100%";
            InfoLine = $"Lv{u.Level} / Lv{u.JobLevel} / Exp {expPct}";
        });

    private void OnProcessList(ProcessListUpdate u) => Post(() => ProcessList = u.Processes);

    private void OnProfileList(ProfileListUpdate u) =>
        Post(() =>
        {
            _suppressProfileCommand = true;
            ProfileList = u.Profiles;
            CurrentProfile = u.CurrentProfile;
            _suppressProfileCommand = false;
        });

    private void OnError(ErrorUpdate u) =>
        Post(() => Console.WriteLine($"[Worker Error] {u.Message}"));

    private static void Post(Action a) =>
        Application.Current?.Dispatcher.BeginInvoke(a, DispatcherPriority.Background);

    private static SolidColorBrush CreateBrush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex)!);
}