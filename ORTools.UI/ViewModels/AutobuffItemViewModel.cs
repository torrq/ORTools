using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public partial class AutobuffItemItemViewModel : ObservableObject
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";

    [ObservableProperty]
    private string _iconName = "";
    partial void OnIconNameChanged(string value) => OnPropertyChanged(nameof(ImagePath));
    public string ImagePath => $"pack://application:,,,/Assets/Icons/Items/{IconName}.png";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(EffectiveIsEnabled))]
    private string _key = "None";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(EffectiveIsEnabled))]
    private bool _isEnabled = true;

    public bool IsActive => Key != "None" && IsEnabled;
    public bool EffectiveIsEnabled => Key == "None" || IsEnabled;

    public event EventHandler<string>? OnKeyUpdated;
    public event EventHandler<bool>? OnEnabledToggled;

    partial void OnKeyChanged(string value)
    {
        OnKeyUpdated?.Invoke(this, value);
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        if (Key == "None") return;
        IsEnabled = !IsEnabled;
        OnEnabledToggled?.Invoke(this, IsEnabled);
    }
}

public class AutobuffItemGroupViewModel : ObservableObject
{
    public string GroupName { get; set; } = "";
    public ObservableCollection<AutobuffItemItemViewModel> Items { get; } = new();
}

public partial class AutobuffItemViewModel : ObservableObject
{
    private readonly WorkerService _worker;

    public ObservableCollection<AutobuffItemGroupViewModel> ItemGroups { get; } = new();

    public event Action? ConfigUpdated;

    [ObservableProperty]
    private int _delay = 50;

    private bool _isUpdatingFromServer;

    public AutobuffItemViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.AutobuffItemConfigReceived += OnConfigReceived;
    }

    private void OnConfigReceived(AutobuffItemConfigUpdate config)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _isUpdatingFromServer = true;

            Delay = config.Delay;

            if (ItemGroups.Count == 0)
            {
                foreach (var groupData in config.Groups)
                {
                    var groupVm = new AutobuffItemGroupViewModel { GroupName = groupData.GroupName };
                    foreach (var itemData in groupData.Items)
                    {
                        var itemVm = new AutobuffItemItemViewModel
                        {
                            Name = itemData.Name,
                            DisplayName = itemData.DisplayName,
                            Key = itemData.Key,
                            IconName = itemData.IconName,
                            IsEnabled = itemData.IsEnabled
                        };
                        itemVm.OnKeyUpdated += (sender, key) =>
                        {
                            if (!_isUpdatingFromServer && sender is AutobuffItemItemViewModel vm)
                                _worker.Send(new UpdateAutobuffItemCommand(vm.Name, key));
                        };
                        itemVm.OnEnabledToggled += (sender, enabled) =>
                        {
                            if (!_isUpdatingFromServer && sender is AutobuffItemItemViewModel vm)
                                _worker.Send(new ToggleAutobuffItemEnabledCommand(vm.Name, enabled));
                        };
                        groupVm.Items.Add(itemVm);
                    }
                    ItemGroups.Add(groupVm);
                }
            }
            else
            {
                // In-place update to prevent massive WPF layout lag
                foreach (var groupData in config.Groups)
                {
                    var groupVm = ItemGroups.FirstOrDefault(g => g.GroupName == groupData.GroupName);
                    if (groupVm == null) continue;

                    foreach (var itemData in groupData.Items)
                    {
                        var itemVm = groupVm.Items.FirstOrDefault(i => i.Name == itemData.Name);
                        if (itemVm != null)
                        {
                            if (itemVm.Key != itemData.Key) itemVm.Key = itemData.Key;
                            if (itemVm.IsEnabled != itemData.IsEnabled) itemVm.IsEnabled = itemData.IsEnabled;
                        }
                    }
                }
            }
            _isUpdatingFromServer = false;
            ConfigUpdated?.Invoke();
        });
    }

    partial void OnDelayChanged(int value)
    {
        if (!_isUpdatingFromServer)
            _worker.Send(new UpdateAutobuffItemSettingsCommand(value));
    }
}



