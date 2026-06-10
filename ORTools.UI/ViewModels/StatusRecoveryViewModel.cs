using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public partial class StatusRecoveryViewModel : ObservableObject
{
    private readonly WorkerService _worker;

    public ObservableCollection<StatusRecoveryItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private int _delay = 50;

    private bool _isUpdatingFromServer;

    public StatusRecoveryViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.StatusRecoveryConfigReceived += OnConfigReceived;
    }

    private void OnConfigReceived(StatusRecoveryConfigUpdate config)
    {
        _isUpdatingFromServer = true;

        Delay = config.Delay;

        foreach (var item in config.Items)
        {
            var vm = Items.FirstOrDefault(x => x.Name == item.Name);
            if (vm == null)
            {
                vm = new StatusRecoveryItemViewModel
                {
                    Name = item.Name,
                    DisplayName = GetDisplayName(item.Name),
                    Key = item.Key
                };
                vm.OnKeyUpdated += (sender, key) =>
                {
                    if (!_isUpdatingFromServer)
                        _worker.Send(new UpdateStatusRecoveryItemCommand(sender.Name, key));
                };
                Items.Add(vm);
            }
            else
            {
                vm.Key = item.Key;
            }
        }

        _isUpdatingFromServer = false;
    }

    partial void OnDelayChanged(int value)
    {
        if (!_isUpdatingFromServer)
        {
            _worker.Send(new UpdateStatusRecoverySettingsCommand(value));
        }
    }

    private string GetDisplayName(string name)
    {
        if (name == "RoyalJelly") return "Royal Jelly";
        if (name == "GreenPotion") return "Green Potion";
        return name;
    }
}
