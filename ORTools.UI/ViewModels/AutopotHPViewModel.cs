using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ORTools.UI.ViewModels;

public sealed partial class AutopotHPViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private bool _suppressCommands;

    [ObservableProperty] private int _delay = 100;
    [ObservableProperty] private bool _stopOnCriticalInjury;

    public ObservableCollection<AutopotSlotViewModel> Slots { get; } = new();

    public AutopotHPViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.AutopotHPConfigReceived += OnConfigReceived;

        for (int i = 1; i <= 5; i++)
        {
            Slots.Add(new AutopotSlotViewModel(i, "None", 0, false, OnSlotChanged));
        }
    }

    private void OnSlotChanged(AutopotSlotViewModel slot)
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateAutopotHPSlotCommand(slot.Id, slot.Key, slot.Percent, slot.Enabled));
    }

    partial void OnDelayChanged(int value) => NotifySettingsChanged();
    partial void OnStopOnCriticalInjuryChanged(bool value) => NotifySettingsChanged();

    private void NotifySettingsChanged()
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateAutopotHPSettingsCommand(Delay, StopOnCriticalInjury));
    }

    private void OnConfigReceived(AutopotHPConfigUpdate update)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _suppressCommands = true;

            Delay = update.Delay;
            StopOnCriticalInjury = update.StopOnCriticalInjury;

            foreach (var slotData in update.Slots)
            {
                var slot = Slots.FirstOrDefault(s => s.Id == slotData.Id);
                if (slot != null)
                {
                    slot.UpdateFromWorker(slotData.Key, slotData.Percent, slotData.Enabled);
                }
            }

            _suppressCommands = false;
        }, DispatcherPriority.Background);
    }
}
