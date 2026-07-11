using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace ORTools.UI.ViewModels;

public sealed partial class AutoOffViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private bool _suppressCommands;

    [ObservableProperty] private bool _autoOffOverweight;
    [ObservableProperty] private int _autoOffOverweightMode = 90;
    [ObservableProperty] private string _autoOffKey1 = "None";
    [ObservableProperty] private string _autoOffKey2 = "None";
    [ObservableProperty] private bool _autoOffKillClient;
    [ObservableProperty] private bool _switchAmmo;
    [ObservableProperty] private string _ammo1Key = "None";
    [ObservableProperty] private string _ammo2Key = "None";
    [ObservableProperty] private int _autoOffTime = 1;
    [ObservableProperty] private bool _isTimerRunning;
    [ObservableProperty] private bool _isTimerPaused;
    [ObservableProperty] private string _selectedTimeText = "0m";
    [ObservableProperty] private string _remainingTimeText = "0m";
    [ObservableProperty] private string _remainingSecondsText = "";
    [ObservableProperty] private string _remainingCombinedText = "";
    [ObservableProperty] private int _runningRemainingSeconds;
    [ObservableProperty] private int _runningTotalMinutes;
    [ObservableProperty] private int _maxTime = 480;
    partial void OnMaxTimeChanged(int value) => UpdateQuickButtons();

    public ObservableCollection<string> AvailableKeys { get; } = new();
    public ObservableCollection<int> QuickButtons { get; } = new();

    public AutoOffViewModel(WorkerService worker)
    {
        _worker = worker;
        
        // Populate standard keys used in ORTools UI
        AvailableKeys.Add("None");
        for (int i = 1; i <= 12; i++) AvailableKeys.Add($"F{i}");
        for (int i = 0; i <= 9; i++) AvailableKeys.Add($"{i}");
        foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ") AvailableKeys.Add(c.ToString());
        AvailableKeys.Add("Space"); AvailableKeys.Add("Insert"); AvailableKeys.Add("Delete");
        AvailableKeys.Add("Home"); AvailableKeys.Add("End"); AvailableKeys.Add("PageUp"); AvailableKeys.Add("PageDown");

        UpdateQuickButtons();

        _worker.AutoOffConfigReceived += OnConfigReceived;
        _worker.AutoOffTimerStateReceived += OnTimerStateReceived;
    }

    private void UpdateQuickButtons()
    {
        QuickButtons.Clear();

        // HR skips 6/7h (jumps straight to 8h); MR only goes up to 6h.
        var hours = ThemeService.ServerMode == 1
            ? new[] { 1, 2, 3, 4, 5, 8 }
            : new[] { 1, 2, 3, 4, 5, 6 };

        foreach (var h in hours) QuickButtons.Add(h);
    }

    private void OnTimerStateReceived(AutoOffTimerStateUpdate update)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            IsTimerRunning = update.IsRunning;
            IsTimerPaused = update.IsPaused;
            int hours = update.SelectedMinutes / 60;
            int minutes = update.SelectedMinutes % 60;
            SelectedTimeText = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
            
            if (update.IsRunning)
            {
                int rMin = (update.RemainingSeconds + 59) / 60;
                int rHr = rMin / 60;
                int rM = rMin % 60;
                RemainingTimeText = rHr > 0 ? $"{rHr}h {rM}m" : $"{rM}m";

                int secs = update.RemainingSeconds % 60;
                RemainingSecondsText = $"{secs:00}s";

                int totalRemaining = Math.Max(0, update.RemainingSeconds);
                int hoursLeft = totalRemaining / 3600;
                int minsLeft = (totalRemaining % 3600) / 60;
                int secsLeft = totalRemaining % 60;
                RemainingCombinedText = hoursLeft > 0
                    ? $"{hoursLeft}h {minsLeft}m {secsLeft:00}s"
                    : minsLeft > 0
                        ? $"{minsLeft}m {secsLeft:00}s"
                        : $"{secsLeft}s";

                // Drives the dial's live countdown ring/second-hand — kept separate from
                // AutoOffTime so dragging the dial mid-run never disturbs the active countdown.
                RunningRemainingSeconds = update.RemainingSeconds;
                RunningTotalMinutes = update.RunningMinutes;
            }
            else
            {
                RemainingTimeText = "0m";
                RemainingSecondsText = "";
                RemainingCombinedText = "";
                RunningRemainingSeconds = 0;
                RunningTotalMinutes = 0;
            }
        }, DispatcherPriority.Background);
    }

    private void OnConfigReceived(AutoOffConfigUpdate update)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _suppressCommands = true;
            AutoOffOverweight = update.AutoOffOverweight;
            AutoOffOverweightMode = update.AutoOffOverweightMode;
            AutoOffKey1 = update.AutoOffKey1;
            AutoOffKey2 = update.AutoOffKey2;
            AutoOffKillClient = update.AutoOffKillClient;
            AutoOffTime = update.AutoOffTime;
            _suppressCommands = false;
        }, DispatcherPriority.Background);
    }

    [RelayCommand] private void ToggleTimer() => _worker.Send(new ToggleAutoOffTimerCommand(!IsTimerRunning));

    [RelayCommand]
    private void PauseTimer() => _worker.Send(new PauseAutoOffTimerCommand(!IsTimerPaused));

    [RelayCommand] private void SetTime(int hours) 
    {
        AutoOffTime = hours * 60;
    }

    partial void OnAutoOffOverweightChanged(bool value) => SendUpdate();
    partial void OnAutoOffOverweightModeChanged(int value) => SendUpdate();
    partial void OnAutoOffKey1Changed(string value) => SendUpdate();
    partial void OnAutoOffKey2Changed(string value) => SendUpdate();
    partial void OnAutoOffKillClientChanged(bool value) => SendUpdate();
    partial void OnAutoOffTimeChanged(int value) 
    {
        int hours = value / 60;
        int mins = value % 60;
        SelectedTimeText = hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
        SendUpdate();
    }

    private void SendUpdate()
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateAutoOffSettingsCommand(
            AutoOffOverweight,
            AutoOffOverweightMode,
            AutoOffKey1,
            AutoOffKey2,
            AutoOffKillClient,
            SwitchAmmo,
            Ammo1Key,
            Ammo2Key,
            AutoOffTime));
    }
}
