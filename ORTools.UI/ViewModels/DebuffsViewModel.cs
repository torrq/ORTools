using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public partial class DebuffsViewModel : ObservableObject
{
    private readonly WorkerService _worker;

    public ObservableCollection<StatusRecoveryItemViewModel> StatusRecoveryItems { get; } = new();
    public ObservableCollection<DebuffRecoveryItemViewModel> DebuffItems { get; } = new();

    [ObservableProperty]
    private int _delay = 50;

    private bool _isUpdatingFromServer;

    public DebuffsViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.StatusRecoveryConfigReceived += OnStatusConfigReceived;
        _worker.DebuffRecoveryConfigReceived += OnDebuffConfigReceived;

        // Initialize lists so they appear immediately with correct icons and order
        InitializeStatusList();
        InitializeDebuffList();
    }

    private void InitializeStatusList()
    {
        var items = new[]
        {
            ("GreenPotion", "Green Potion"),
            ("Panacea", "Panacea"),
            ("RoyalJelly", "Royal Jelly")
        };

        foreach (var (name, displayName) in items)
        {
            var vm = new StatusRecoveryItemViewModel
            {
                Name = name,
                DisplayName = displayName,
                Key = "None"
            };
            vm.OnKeyUpdated += (sender, key) =>
            {
                if (!_isUpdatingFromServer && sender is StatusRecoveryItemViewModel itemVm)
                    _worker.Send(new UpdateStatusRecoveryItemCommand(itemVm.Name, key));
            };
            StatusRecoveryItems.Add(vm);
        }
    }

    private void InitializeDebuffList()
    {
        var knownDebuffs = new[]
        {
            ("Burning", "BURNING", "burning"),
            ("Chaos / Confusion", "CONFUSION", "chaos"),
            ("Critical Wound", "NPC_CRITICALWOUND", "critical_wound"),
            ("Curse", "CURSE", "curse"),
            ("Decrease AGI", "AL_DECAGI", "decrease_agi"),
            ("Freezing", "FREEZING", "freezing"),
            ("Poison", "POISON", "poison_status"),
            ("Silence", "SILENCE", "silence"),
            ("Sit", "SIT", "sit"),
            ("Deep Sleep", "DEEP_SLEEP", "deep_sleep"),
            ("Sleep", "SLEEP", "sleep"),
            ("Slow Cast", "NPC_SLOWCAST", "slow_cast"),
        };

        foreach (var (displayName, id, icon) in knownDebuffs)
        {
            var vm = new DebuffRecoveryItemViewModel
            {
                Name = id,
                DisplayName = displayName,
                IconName = icon,
                Key = "None"
            };
            vm.OnKeyUpdated += (sender, key) =>
            {
                if (!_isUpdatingFromServer && sender is DebuffRecoveryItemViewModel itemVm)
                    _worker.Send(new UpdateDebuffRecoveryItemCommand(itemVm.Name, key));
            };
            DebuffItems.Add(vm);
        }
    }

    private void OnStatusConfigReceived(StatusRecoveryConfigUpdate config)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _isUpdatingFromServer = true;

            Delay = config.Delay;

            foreach (var item in config.Items)
            {
                var vm = StatusRecoveryItems.FirstOrDefault(x => x.Name == item.Name);
                if (vm != null)
                {
                    vm.Key = item.Key;
                }
            }

            _isUpdatingFromServer = false;
        });
    }

    private void OnDebuffConfigReceived(DebuffRecoveryConfigUpdate config)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _isUpdatingFromServer = true;

            Delay = config.Delay;

            // Reset all keys to None first
            foreach (var item in DebuffItems)
            {
                item.Key = "None";
            }

            // Apply mapped keys
            foreach (var item in config.Items)
            {
                var vm = DebuffItems.FirstOrDefault(x => x.Name == item.Name);
                if (vm != null)
                {
                    vm.Key = item.Key;
                    vm.IconName = item.IconName;
                }
            }

            _isUpdatingFromServer = false;
        });
    }

    partial void OnDelayChanged(int value)
    {
        if (!_isUpdatingFromServer)
        {
            _worker.Send(new UpdateStatusRecoverySettingsCommand(value));
            _worker.Send(new UpdateDebuffRecoverySettingsCommand(value));
        }
    }
}

public partial class DebuffRecoveryItemViewModel : ObservableObject
{
    public string Name { get; set; } = "";

    public string DisplayName { get; set; } = "";

    [ObservableProperty]
    private string _iconName = "";
    partial void OnIconNameChanged(string value) => OnPropertyChanged(nameof(ImagePath));
    public string ImagePath => $"pack://application:,,,/Assets/Icons/Debuffs/{IconName}.png";

    [ObservableProperty]
    private string _key = "None";

    public event EventHandler<string>? OnKeyUpdated;

    partial void OnKeyChanged(string value)
    {
        OnKeyUpdated?.Invoke(this, value);
    }
}


