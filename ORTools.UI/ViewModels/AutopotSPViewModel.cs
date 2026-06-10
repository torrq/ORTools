using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public sealed partial class AutopotSPSlotViewModel : ObservableObject
{
    private readonly Action<AutopotSPSlotViewModel> _onChange;
    private bool _syncing;

    public AutopotSPSlotViewModel(int id, Action<AutopotSPSlotViewModel> onChange)
    {
        Id = id;
        _onChange = onChange;
    }

    public int Id { get; }

    [ObservableProperty] private string _keyText = "None";
    [ObservableProperty] private int _percent;
    [ObservableProperty] private bool _enabled;

    partial void OnKeyTextChanged(string value) { if (!_syncing) _onChange(this); }
    partial void OnPercentChanged(int value) { if (!_syncing) _onChange(this); }
    partial void OnEnabledChanged(bool value) { if (!_syncing) _onChange(this); }

    public void SyncFrom(AutopotSlotData data)
    {
        _syncing = true;
        KeyText = data.Key;
        Percent = data.Percent;
        Enabled = data.Enabled;
        _syncing = false;
    }
}

public sealed partial class AutopotSPViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private bool _syncing;

    public ObservableCollection<AutopotSPSlotViewModel> Slots { get; } = new();

    [ObservableProperty] private int _delay = 50;

    public AutopotSPViewModel(WorkerService worker)
    {
        _worker = worker;
        for (int i = 1; i <= 5; i++)
            Slots.Add(new AutopotSPSlotViewModel(i, SlotChanged));
    }

    private void SlotChanged(AutopotSPSlotViewModel slot) =>
        _worker.Send(new UpdateAutopotSPSlotCommand(
            slot.Id, slot.KeyText, slot.Percent, slot.Enabled));

    public void RequestSlotOrder(IEnumerable<int> slotOrder) =>
        _worker.Send(new ReorderAutopotSPCommand(slotOrder.ToList()));

    partial void OnDelayChanged(int value)
    {
        if (!_syncing)
            _worker.Send(new UpdateAutopotSPSettingsCommand(value));
    }

    public void OnConfigUpdate(AutopotSPConfigUpdate u)
    {
        Post(() =>
        {
            _syncing = true;
            Delay = u.Delay;
            var ordered = new List<AutopotSPSlotViewModel>();
            foreach (var slot in u.Slots)
            {
                var vm = Slots.FirstOrDefault(s => s.Id == slot.Id);
                if (vm != null)
                {
                    ordered.Add(vm);
                    vm.SyncFrom(slot);
                }
            }

            Slots.Clear();
            foreach (var vm in ordered)
            {
                Slots.Add(vm);
            }

            _syncing = false;
        });
    }

    private static void Post(Action a) =>
        Application.Current?.Dispatcher.BeginInvoke(a, DispatcherPriority.Background);
}
