using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

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

    public ObservableCollection<string> AvailableKeys { get; } = new();

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

        _worker.AutoOffConfigReceived += OnConfigReceived;
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
            SwitchAmmo = update.SwitchAmmo;
            Ammo1Key = update.Ammo1Key;
            Ammo2Key = update.Ammo2Key;
            AutoOffTime = update.AutoOffTime;
            _suppressCommands = false;
        }, DispatcherPriority.Background);
    }

    partial void OnAutoOffOverweightChanged(bool value) => SendUpdate();
    partial void OnAutoOffOverweightModeChanged(int value) => SendUpdate();
    partial void OnAutoOffKey1Changed(string value) => SendUpdate();
    partial void OnAutoOffKey2Changed(string value) => SendUpdate();
    partial void OnAutoOffKillClientChanged(bool value) => SendUpdate();
    partial void OnSwitchAmmoChanged(bool value) => SendUpdate();
    partial void OnAmmo1KeyChanged(string value) => SendUpdate();
    partial void OnAmmo2KeyChanged(string value) => SendUpdate();
    partial void OnAutoOffTimeChanged(int value) => SendUpdate();

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
