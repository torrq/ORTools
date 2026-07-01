using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ORTools.UI.ViewModels;

public sealed partial class SkillTimerSlotViewModel : ViewModelBase
{
    private readonly Action<SkillTimerSlotViewModel> _onChanged;
    private readonly IDialogService _dialogService;

    public int Id { get; }

    [ObservableProperty] private string _key;
    [ObservableProperty] private int _delay;
    [ObservableProperty] private int _clickMode;
    [ObservableProperty] private bool _altKey;
    [ObservableProperty] private bool _enabled;

    public SkillTimerSlotViewModel(int id, string key, int delay, int clickMode, bool altKey, bool enabled, Action<SkillTimerSlotViewModel> onChanged, IDialogService dialogService)
    {
        Id = id;
        _key = key;
        _delay = delay;
        _clickMode = clickMode;
        _altKey = altKey;
        _enabled = enabled;
        _onChanged = onChanged;
        _dialogService = dialogService;
    }

    public void UpdateFromWorker(string key, int delay, int clickMode, bool altKey, bool enabled)
    {
        Key = key;
        Delay = delay;
        ClickMode = clickMode;
        AltKey = altKey;
        Enabled = enabled;
    }

    partial void OnKeyChanged(string value) => _onChanged(this);
    partial void OnDelayChanged(int value) => _onChanged(this);
    partial void OnClickModeChanged(int value) => _onChanged(this);
    partial void OnAltKeyChanged(bool value) => _onChanged(this);
    partial void OnEnabledChanged(bool value) => _onChanged(this);

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async System.Threading.Tasks.Task OpenTimePickerAsync()
    {
        var vm = new TimePickerViewModel(Delay);
        await _dialogService.ShowDialogAsync(vm);
        
        // TimePickerViewModel should set a TaskCompletionSource with the result
        var newDelay = await vm.ResultTask;
        if (newDelay.HasValue)
        {
            Delay = newDelay.Value;
        }
        _dialogService.CloseDialog();
    }
}

public sealed partial class SkillTimerViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private readonly IDialogService _dialogService;
    private bool _suppressCommands;

    public ObservableCollection<SkillTimerSlotViewModel> Slots { get; } = new();

    public SkillTimerViewModel(WorkerService worker, IDialogService dialogService)
    {
        _worker = worker;
        _dialogService = dialogService;
        _worker.SkillTimerConfigReceived += OnConfigReceived;

        for (int i = 1; i <= AppConstants.MaxSkillTimers; i++)
        {
            Slots.Add(new SkillTimerSlotViewModel(i, "None", 1000, 0, false, false, OnSlotChanged, _dialogService));
        }
    }

    private void OnSlotChanged(SkillTimerSlotViewModel slot)
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateSkillTimerSlotCommand(slot.Id, slot.Key, slot.Delay, slot.ClickMode, slot.AltKey, slot.Enabled));
    }

    private void OnConfigReceived(SkillTimerConfigUpdate update)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            foreach (var slotData in update.Slots)
            {
                var slot = Slots.FirstOrDefault(s => s.Id == slotData.Id);
                if (slot != null)
                {
                    _suppressCommands = true;
                    slot.UpdateFromWorker(slotData.Key, slotData.Delay, slotData.ClickMode, slotData.AltKey, slotData.Enabled);
                    _suppressCommands = false;
                }
            }
        }, DispatcherPriority.Background);
    }
}
