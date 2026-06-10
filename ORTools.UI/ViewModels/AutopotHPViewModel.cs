using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

// ── Single slot ───────────────────────────────────────────────────────────────

public sealed partial class AutopotHPSlotViewModel : ObservableObject
{
    private readonly Action<AutopotHPSlotViewModel> _onChange;
    private bool _syncing;

    public AutopotHPSlotViewModel(int id, Action<AutopotHPSlotViewModel> onChange)
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

    /// <summary>Update from Worker without triggering sends back.</summary>
    public void SyncFrom(AutopotSlotData data)
    {
        _syncing = true;
        KeyText = data.Key;
        Percent = data.Percent;
        Enabled = data.Enabled;
        _syncing = false;
    }
}

// ── Tab ViewModel ─────────────────────────────────────────────────────────────

public sealed partial class AutopotHPViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private bool _syncing;

    public ObservableCollection<AutopotHPSlotViewModel> Slots { get; } = new();

    [ObservableProperty] private int _delay = 50;
    [ObservableProperty] private bool _stopOnCriticalInjury;

    public AutopotHPViewModel(WorkerService worker)
    {
        _worker = worker;
        for (int i = 1; i <= 5; i++)
            Slots.Add(new AutopotHPSlotViewModel(i, SlotChanged));
    }

    // ── Slot change → send to Worker ──────────────────────────────────────────

    private void SlotChanged(AutopotHPSlotViewModel slot) =>
        _worker.Send(new UpdateAutopotHPSlotCommand(
            slot.Id, slot.KeyText, slot.Percent, slot.Enabled));

    public void RequestSlotOrder(IEnumerable<int> slotOrder) =>
        _worker.Send(new ReorderAutopotHPCommand(slotOrder.ToList()));

    partial void OnDelayChanged(int value)
    {
        if (!_syncing)
            _worker.Send(new UpdateAutopotHPSettingsCommand(value, StopOnCriticalInjury));
    }

    partial void OnStopOnCriticalInjuryChanged(bool value)
    {
        if (!_syncing)
            _worker.Send(new UpdateAutopotHPSettingsCommand(Delay, value));
    }

    // ── Receive config from Worker ────────────────────────────────────────────

    public void OnConfigUpdate(AutopotHPConfigUpdate u)
    {
        Post(() =>
        {
            _syncing = true;
            Delay = u.Delay;
            StopOnCriticalInjury = u.StopOnCriticalInjury;
            var ordered = new List<AutopotHPSlotViewModel>();
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
