using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public partial class MiscViewModel : ViewModelBase
{
    private readonly WorkerService _worker;
    private bool _suppressCommands;

    [ObservableProperty]
    private string _transferKey = "None";

    public MiscViewModel(WorkerService worker)
    {
        _worker = worker;
        _worker.TransferHelperConfigReceived += OnTransferConfigUpdate;
    }

    private void OnTransferConfigUpdate(TransferHelperConfigUpdate update)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _suppressCommands = true;
            TransferKey = update.TransferKey;
            _suppressCommands = false;
        });
    }

    partial void OnTransferKeyChanged(string value)
    {
        if (_suppressCommands) return;
        _worker.Send(new UpdateTransferHelperCommand(value));
    }
}
