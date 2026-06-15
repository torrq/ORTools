using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ORTools.UI.ViewModels;

public sealed partial class AutopotSPViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private bool _suppressCommands;

    [ObservableProperty] private int _delay = 100;

    public ObservableCollection<AutopotSlotViewModel> Slots { get; } = new();

    public AutopotSPViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.AutopotSPConfigReceived += OnConfigReceived;

        for (int i = 1; i <= 8; i++)
        {
            Slots.Add(new AutopotSlotViewModel(i, "None", 0, false, OnSlotChanged));
        }
    }

    private void OnSlotChanged(AutopotSlotViewModel slot)
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateAutopotSPSlotCommand(slot.Id, slot.Key, slot.Percent, slot.Enabled));
    }

    partial void OnDelayChanged(int value) => NotifySettingsChanged();

    private void NotifySettingsChanged()
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateAutopotSPSettingsCommand(Delay));
    }

    private void OnConfigReceived(AutopotSPConfigUpdate update)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _suppressCommands = true;

            Delay = update.Delay;

            foreach (var slotData in update.Slots)
            {
                var slot = Slots.FirstOrDefault(s => s.Id == slotData.Id);
                if (slot != null)
                {
                    slot.UpdateFromWorker(slotData.Key, slotData.Percent, slotData.Enabled);
                }
                else
                {
                    Slots.Add(new AutopotSlotViewModel(slotData.Id, slotData.Key, slotData.Percent, slotData.Enabled, OnSlotChanged));
                }
            }

            _suppressCommands = false;
        }, DispatcherPriority.Background);
    }
}
