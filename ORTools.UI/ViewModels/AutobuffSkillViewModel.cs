using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public partial class AutobuffSkillItemViewModel : ObservableObject
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";

    [ObservableProperty]
    private string _iconName = "";
    partial void OnIconNameChanged(string value) => OnPropertyChanged(nameof(ImagePath));
    public string ImagePath => $"pack://application:,,,/Assets/Icons/Skills/{IconName}.png";

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

public class AutobuffSkillGroupViewModel : ObservableObject
{
    public string GroupName { get; set; } = "";
    public ObservableCollection<AutobuffSkillItemViewModel> Items { get; } = new();
}

public partial class AutobuffSkillViewModel : ObservableObject
{
    private readonly WorkerService _worker;

    public ObservableCollection<AutobuffSkillGroupViewModel> SkillGroups { get; } = new();

    public event Action? ConfigUpdated;

    [ObservableProperty]
    private int _delay = 50;

    private bool _isUpdatingFromServer;

    public AutobuffSkillViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.AutobuffSkillConfigReceived += OnConfigReceived;
    }

    private void OnConfigReceived(AutobuffSkillConfigUpdate config)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _isUpdatingFromServer = true;

            Delay = config.Delay;

            if (SkillGroups.Count == 0)
            {
                foreach (var groupData in config.Groups)
                {
                    var groupVm = new AutobuffSkillGroupViewModel { GroupName = groupData.GroupName };
                    foreach (var itemData in groupData.Items)
                    {
                        var itemVm = new AutobuffSkillItemViewModel
                        {
                            Name = itemData.Name,
                            DisplayName = itemData.DisplayName,
                            Key = itemData.Key,
                            IconName = itemData.IconName,
                            IsEnabled = itemData.IsEnabled
                        };
                        itemVm.OnKeyUpdated += (sender, key) =>
                        {
                            if (!_isUpdatingFromServer && sender is AutobuffSkillItemViewModel vm)
                                _worker.Send(new UpdateAutobuffSkillItemCommand(vm.Name, key));
                        };
                        itemVm.OnEnabledToggled += (sender, enabled) =>
                        {
                            if (!_isUpdatingFromServer && sender is AutobuffSkillItemViewModel vm)
                                _worker.Send(new ToggleAutobuffSkillEnabledCommand(vm.Name, enabled));
                        };
                        groupVm.Items.Add(itemVm);
                    }
                    SkillGroups.Add(groupVm);
                }
            }
            else
            {
                // In-place update to prevent massive WPF layout lag
                foreach (var groupData in config.Groups)
                {
                    var groupVm = SkillGroups.FirstOrDefault(g => g.GroupName == groupData.GroupName);
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
            _worker.Send(new UpdateAutobuffSkillSettingsCommand(value));
    }
}




