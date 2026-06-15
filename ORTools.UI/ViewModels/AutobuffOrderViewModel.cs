using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace ORTools.UI.ViewModels;

public partial class AutobuffOrderItemViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DisplayKey => Key.ToUpper();

    [ObservableProperty]
    private string _itemType = string.Empty;
    partial void OnItemTypeChanged(string value) => OnPropertyChanged(nameof(IconPath));

    [ObservableProperty]
    private string _iconName = string.Empty;
    partial void OnIconNameChanged(string value) => OnPropertyChanged(nameof(IconPath));

    public string IconPath => ItemType == "Skill" 
        ? $"pack://application:,,,/Icons/Skills/{IconName}.png" 
        : $"pack://application:,,,/Icons/Items/{IconName}.png";
}

public partial class AutobuffOrderViewModel : ObservableObject
{
    private readonly WorkerService _worker;
    private bool _isUpdatingFromServer;

    public ObservableCollection<AutobuffOrderItemViewModel> ActiveItems { get; } = new();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public AutobuffOrderViewModel()
    {
        // For designer
    }
#pragma warning restore CS8618

    public AutobuffOrderViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.AutobuffOrderConfigReceived += OnConfigReceived;
    }

    private void OnConfigReceived(AutobuffOrderConfigUpdate config)
    {
        _isUpdatingFromServer = true;

        ActiveItems.Clear();
        foreach (var itemData in config.Items)
        {
            ActiveItems.Add(new AutobuffOrderItemViewModel
            {
                Name = itemData.Name,
                DisplayName = itemData.DisplayName,
                Key = itemData.Key,
                ItemType = itemData.ItemType,
                IconName = itemData.IconName
            });
        }

        _isUpdatingFromServer = false;
    }

    [RelayCommand]
    private void MoveUp(AutobuffOrderItemViewModel item)
    {
        int index = ActiveItems.IndexOf(item);
        if (index > 0)
        {
            ActiveItems.Move(index, index - 1);
            SendOrderUpdate();
        }
    }

    [RelayCommand]
    private void MoveDown(AutobuffOrderItemViewModel item)
    {
        int index = ActiveItems.IndexOf(item);
        if (index >= 0 && index < ActiveItems.Count - 1)
        {
            ActiveItems.Move(index, index + 1);
            SendOrderUpdate();
        }
    }

    [RelayCommand]
    private void DeleteItem(AutobuffOrderItemViewModel item)
    {
        if (item.ItemType == "Skill")
        {
            _worker.Send(new UpdateAutobuffSkillItemCommand(item.Name, "None"));
        }
        else if (item.ItemType == "Item")
        {
            _worker.Send(new UpdateAutobuffItemCommand(item.Name, "None"));
        }
    }

    private void SendOrderUpdate()
    {
        if (_isUpdatingFromServer) return;
        var orderedNames = ActiveItems.Select(x => x.Name).ToList();
        _worker.Send(new UpdateAutobuffOrderCommand(orderedNames));
    }
}
