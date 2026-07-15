using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ORTools.Shared.Protocol;
using System.Collections.ObjectModel;
using System.Linq;

namespace ORTools.UI.ViewModels;

public partial class MacroSwitchStepViewModel : ObservableObject
{
    private readonly Services.WorkerService _worker;
    private readonly int _rowId;
    private readonly int _stepId;

    [ObservableProperty]
    private string _key = "None";

    [ObservableProperty]
    private int _delay = 100;

    [ObservableProperty]
    private bool _click;

    private bool _suppressCommands;

    public MacroSwitchStepViewModel(Services.WorkerService worker, int rowId, int stepId, MacroSwitchStepData data)
    {
        _worker = worker;
        _rowId = rowId;
        _stepId = stepId;
        _key = data.Key;
        _delay = data.Delay;
        _click = data.ClickMode == 1;
    }

    public void UpdateData(MacroSwitchStepData data)
    {
        _suppressCommands = true;
        Key = data.Key;
        Delay = data.Delay;
        Click = data.ClickMode == 1;
        _suppressCommands = false;
    }

    partial void OnKeyChanged(string value) => SyncWithWorker();
    partial void OnDelayChanged(int value) => SyncWithWorker();
    partial void OnClickChanged(bool value) => SyncWithWorker();

    private void SyncWithWorker()
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateMacroSwitchStepCommand(_rowId, _stepId, Key, Delay, Click ? 1 : 0));
    }
}

public partial class MacroSwitchRowViewModel : ObservableObject
{
    private readonly Services.WorkerService _worker;
    private readonly int _rowId;

    [ObservableProperty]
    private string _triggerKey = "None";

    private bool _suppressCommands;

    public ObservableCollection<MacroSwitchStepViewModel> Steps { get; } = new();

    public MacroSwitchRowViewModel(Services.WorkerService worker, MacroSwitchChainData data)
    {
        _worker = worker;
        _rowId = data.Id;
        _triggerKey = data.TriggerKey;

        int stepId = 1;
        foreach (var step in data.Steps)
        {
            Steps.Add(new MacroSwitchStepViewModel(_worker, _rowId, stepId++, step));
        }
    }

    public void UpdateData(MacroSwitchChainData data)
    {
        _suppressCommands = true;
        TriggerKey = data.TriggerKey;
        for (int i = 0; i < data.Steps.Count && i < Steps.Count; i++)
        {
            Steps[i].UpdateData(data.Steps[i]);
        }
        _suppressCommands = false;
    }

    partial void OnTriggerKeyChanged(string value)
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateMacroSwitchTriggerCommand(_rowId, TriggerKey));
    }

    [RelayCommand]
    private void ResetRow()
    {
        _worker.Send(new ResetMacroSwitchRowCommand(_rowId));
    }
}

public partial class MacroSwitchViewModel : ObservableObject
{
    private readonly Services.WorkerService _worker;

    public ObservableCollection<MacroSwitchRowViewModel> Rows { get; } = new();

    public MacroSwitchViewModel(Services.WorkerService worker)
    {
        _worker = worker;
        _worker.MacroSwitchConfigReceived += OnMacroSwitchConfigReceived;
    }

    private void OnMacroSwitchConfigReceived(MacroSwitchConfigUpdate update)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // Sync rows list size
            while (Rows.Count > update.Chains.Count)
                Rows.RemoveAt(Rows.Count - 1);
            
            while (Rows.Count < update.Chains.Count)
            {
                int index = Rows.Count;
                Rows.Add(new MacroSwitchRowViewModel(_worker, update.Chains[index]));
            }

            // Update data
            for (int i = 0; i < update.Chains.Count; i++)
            {
                Rows[i].UpdateData(update.Chains[i]);
            }
        });
    }
}
