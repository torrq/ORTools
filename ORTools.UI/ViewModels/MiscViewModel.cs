using CommunityToolkit.Mvvm.ComponentModel;
using ORTools.Shared.Protocol;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public partial class MiscViewModel : ViewModelBase
{
    private readonly WorkerService _worker;

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
            TransferKey = update.TransferKey;
        });
    }

    partial void OnTransferKeyChanged(string value)
    {
        _worker.Send(new UpdateTransferHelperCommand(value));
    }
}
