using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public sealed partial class ConfirmDeleteDialogViewModel : ViewModelBase, ICancelableDialog
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> ResultTask => _tcs.Task;

    [ObservableProperty] private string _titleText;
    [ObservableProperty] private string _messageText;
    [ObservableProperty] private string _confirmButtonText;

    public ConfirmDeleteDialogViewModel(string message, string? title = null, string? confirmText = null)
    {
        TitleText = string.IsNullOrWhiteSpace(title) ? LanguageService.Get("S.Dialog.ConfirmDelete") : title;
        MessageText = message;
        ConfirmButtonText = string.IsNullOrWhiteSpace(confirmText) ? LanguageService.Get("S.Dialog.Delete") : confirmText;
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
