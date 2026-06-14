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
    public string ItemType { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;

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

        // In-place sync for Autobuff Order
        // 1. Remove items that are no longer in config
        var configNames = config.Items.Select(x => x.Name).ToHashSet();
        for (int i = ActiveItems.Count - 1; i >= 0; i--)
        {
            if (!configNames.Contains(ActiveItems[i].Name))
            {
                ActiveItems.RemoveAt(i);
            }
        }

        // 2. Add or update items in the correct order
        for (int i = 0; i < config.Items.Count; i++)
        {
            var itemData = config.Items[i];
            var existingItem = ActiveItems.FirstOrDefault(x => x.Name == itemData.Name);

            if (existingItem != null)
            {
                // Update properties
                existingItem.DisplayName = itemData.DisplayName;
                existingItem.Key = itemData.Key;
                existingItem.ItemType = itemData.ItemType;
                existingItem.IconName = itemData.IconName;

                // Move if order changed
                int currentIndex = ActiveItems.IndexOf(existingItem);
                if (currentIndex != i)
                {
                    ActiveItems.Move(currentIndex, i);
                }
            }
            else
            {
                // Insert new item at correct index
                ActiveItems.Insert(i, new AutobuffOrderItemViewModel
                {
                    Name = itemData.Name,
                    DisplayName = itemData.DisplayName,
                    Key = itemData.Key,
                    ItemType = itemData.ItemType,
                    IconName = itemData.IconName
                });
            }
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
