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
        
        // Initialize all known debuffs so they appear even if they don't have keys configured
        InitializeDebuffList();
    }

    private void InitializeDebuffList()
    {
        var knownDebuffs = new[]
        {
            ("Burning", "BURNING"),
            ("Chaos / Confusion", "CONFUSION"),
            ("Critical Wound", "NPC_CRITICALWOUND"),
            ("Curse", "CURSE"),
            ("Decrease AGI", "AL_DECAGI"),
            ("Freezing", "FREEZING"),
            ("Frozen", "FROZEN"),
            ("Poison", "POISON"),
            ("Silence", "SILENCE"),
            ("Sit", "SIT"),
            ("Deep Sleep", "DEEP_SLEEP"),
            ("Sleep", "SLEEP"),
            ("Slow Cast", "NPC_SLOWCAST"),
            ("Stone Curse (initial stage)", "STONECURSE_ING"),
            ("Stone Curse (petrified)", "STONECURSE"),
            ("Stun", "STUN")
        };

        foreach (var (displayName, id) in knownDebuffs)
        {
            var vm = new DebuffRecoveryItemViewModel
            {
                Name = id,
                DisplayName = displayName,
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
        _isUpdatingFromServer = true;

        Delay = config.Delay;

        foreach (var item in config.Items)
        {
            var vm = StatusRecoveryItems.FirstOrDefault(x => x.Name == item.Name);
            if (vm == null)
            {
                vm = new StatusRecoveryItemViewModel
                {
                    Name = item.Name,
                    DisplayName = GetStatusDisplayName(item.Name),
                    Key = item.Key
                };
                vm.OnKeyUpdated += (sender, key) =>
                {
                    if (!_isUpdatingFromServer && sender is StatusRecoveryItemViewModel itemVm)
                        _worker.Send(new UpdateStatusRecoveryItemCommand(itemVm.Name, key));
                };
                StatusRecoveryItems.Add(vm);
            }
            else
            {
                vm.Key = item.Key;
            }
        }

        // Sort items to match requested order: Green Potion, Panacea, Royal Jelly
        var greenPotion = StatusRecoveryItems.FirstOrDefault(x => x.Name == "GreenPotion");
        var panacea = StatusRecoveryItems.FirstOrDefault(x => x.Name == "Panacea");
        var royalJelly = StatusRecoveryItems.FirstOrDefault(x => x.Name == "RoyalJelly");

        StatusRecoveryItems.Clear();
        if (greenPotion != null) StatusRecoveryItems.Add(greenPotion);
        if (panacea != null) StatusRecoveryItems.Add(panacea);
        if (royalJelly != null) StatusRecoveryItems.Add(royalJelly);

        _isUpdatingFromServer = false;
    }

    private void OnDebuffConfigReceived(DebuffRecoveryConfigUpdate config)
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
    }

    partial void OnDelayChanged(int value)
    {
        if (!_isUpdatingFromServer)
        {
            _worker.Send(new UpdateStatusRecoverySettingsCommand(value));
            _worker.Send(new UpdateDebuffRecoverySettingsCommand(value));
        }
    }

    private string GetStatusDisplayName(string name)
    {
        if (name == "RoyalJelly") return "Royal Jelly";
        if (name == "GreenPotion") return "Green Potion";
        return name;
    }
}

public partial class DebuffRecoveryItemViewModel : ObservableObject
{
    public string Name { get; set; } = "";
    
    public string DisplayName { get; set; } = "";
    
    [ObservableProperty]
    private string _iconName = "";
    partial void OnIconNameChanged(string value) => OnPropertyChanged(nameof(ImagePath));
    public string ImagePath => $"pack://application:,,,/Icons/Debuffs/{IconName}.png";
    
    [ObservableProperty]
    private string _key = "None";

    public event EventHandler<string>? OnKeyUpdated;

    partial void OnKeyChanged(string value)
    {
        OnKeyUpdated?.Invoke(this, value);
    }
}


