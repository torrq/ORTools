using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ORTools.UI.ViewModels;

public sealed partial class AlertDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> ResultTask => _tcs.Task;

    [ObservableProperty] private string _titleText;
    [ObservableProperty] private string _messageText;

    public AlertDialogViewModel(string title, string message)
    {
        TitleText = title;
        MessageText = message;
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs.TrySetResult(true);
    }
}
