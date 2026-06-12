using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.Shared.Protocol;
using System.Collections.ObjectModel;
using System.Linq;

namespace ORTools.UI.ViewModels;

public partial class MacroSongStepViewModel : ObservableObject
{
    private readonly Services.WorkerService _worker;
    private readonly int _rowId;
    private readonly int _stepId;

    [ObservableProperty]
    private string _key = "None";

    public MacroSongStepViewModel(Services.WorkerService worker, int rowId, int stepId, string key)
    {
        _worker = worker;
        _rowId = rowId;
        _stepId = stepId;
        _key = key;
    }

    private bool _isUpdatingFromServer;

    public void UpdateData(string key)
    {
        _isUpdatingFromServer = true;
        Key = key;
        _isUpdatingFromServer = false;
    }

    partial void OnKeyChanged(string value) => SyncWithWorker();

    private void SyncWithWorker()
    {
        if (_isUpdatingFromServer) return;
        _worker.Send(new UpdateMacroSongStepCommand(_rowId, _stepId, Key));
    }
}

public partial class MacroSongRowViewModel : ObservableObject
{
    private readonly Services.WorkerService _worker;
    private readonly int _rowId;
    private bool _isUpdatingFromServer;

    [ObservableProperty]
    private string _triggerKey = "None";

    [ObservableProperty]
    private string _adaptationKey = "None";

    [ObservableProperty]
    private string _instrumentKey = "None";

    [ObservableProperty]
    private int _delay = 100;

    public ObservableCollection<MacroSongStepViewModel> Steps { get; } = new();

    public MacroSongRowViewModel(Services.WorkerService worker, MacroSongRowData data)
    {
        _worker = worker;
        _rowId = data.Id;
        _triggerKey = data.TriggerKey;
        _adaptationKey = data.AdaptationKey;
        _instrumentKey = data.InstrumentKey;
        _delay = data.Delay;

        int stepId = 1;
        foreach (var key in data.Sequence)
        {
            Steps.Add(new MacroSongStepViewModel(_worker, _rowId, stepId++, key));
        }
    }

    public void UpdateData(MacroSongRowData data)
    {
        _isUpdatingFromServer = true;
        try
        {
            TriggerKey = data.TriggerKey;
            AdaptationKey = data.AdaptationKey;
            InstrumentKey = data.InstrumentKey;
            Delay = data.Delay;

            for (int i = 0; i < data.Sequence.Count && i < Steps.Count; i++)
            {
                Steps[i].UpdateData(data.Sequence[i]);
            }
        }
        finally
        {
            _isUpdatingFromServer = false;
        }
    }

    partial void OnTriggerKeyChanged(string value)
    {
        if (_isUpdatingFromServer) return;
        _worker.Send(new UpdateMacroSongTriggerCommand(_rowId, TriggerKey));
    }

    partial void OnAdaptationKeyChanged(string value)
    {
        if (_isUpdatingFromServer) return;
        _worker.Send(new UpdateMacroSongAdaptationCommand(_rowId, AdaptationKey));
    }

    partial void OnInstrumentKeyChanged(string value)
    {
        if (_isUpdatingFromServer) return;
        _worker.Send(new UpdateMacroSongInstrumentCommand(_rowId, InstrumentKey));
    }

    partial void OnDelayChanged(int value)
    {
        if (_isUpdatingFromServer) return;
        _worker.Send(new UpdateMacroSongDelayCommand(_rowId, Delay));
    }

    [RelayCommand]
    private void ResetRow()
    {
        _worker.Send(new ResetMacroSongRowCommand(_rowId));
    }
}

public partial class MacroSongViewModel : ObservableObject
{
    private readonly Services.WorkerService _worker;

    public ObservableCollection<MacroSongRowViewModel> Rows { get; } = new();

    public MacroSongViewModel(Services.WorkerService worker)
    {
        _worker = worker;
        _worker.MacroSongConfigReceived += OnMacroSongConfigReceived;
    }

    private void OnMacroSongConfigReceived(MacroSongConfigUpdate update)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // Sync rows list size
            while (Rows.Count > update.Rows.Count)
                Rows.RemoveAt(Rows.Count - 1);
            
            while (Rows.Count < update.Rows.Count)
            {
                int index = Rows.Count;
                Rows.Add(new MacroSongRowViewModel(_worker, update.Rows[index]));
            }

            // Update data
            for (int i = 0; i < update.Rows.Count; i++)
            {
                Rows[i].UpdateData(update.Rows[i]);
            }
        });
    }
}
