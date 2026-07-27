using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.UI.Services;
using System.Threading.Tasks;

namespace ORTools.UI.ViewModels;

public sealed partial class AlertDialogViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly IDialogService? _dialogService;

    public Task<bool> ResultTask => _tcs.Task;

    [ObservableProperty] private string _titleText;
    [ObservableProperty] private string _messageText;

    public AlertDialogViewModel(string title, string message, IDialogService? dialogService = null)
    {
        TitleText = title;
        MessageText = message;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs.TrySetResult(true);
        _dialogService?.CloseDialog();
    }

    [RelayCommand]
    private void Confirm()
    {
        _tcs.TrySetResult(true);
        _dialogService?.CloseDialog();
    }
}
