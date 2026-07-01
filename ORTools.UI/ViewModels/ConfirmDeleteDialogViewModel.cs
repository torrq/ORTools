using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ORTools.UI.ViewModels;

public sealed partial class ConfirmDeleteDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> ResultTask => _tcs.Task;

    [ObservableProperty] private string _messageText;

    public ConfirmDeleteDialogViewModel(string message)
    {
        MessageText = message;
    }

    [RelayCommand]
    private void Confirm()
    {
        _tcs.TrySetResult(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs.TrySetResult(false);
    }
}
