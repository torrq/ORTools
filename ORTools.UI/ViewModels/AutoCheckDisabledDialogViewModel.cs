using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.UI.Services;

namespace ORTools.UI.ViewModels;

public sealed partial class AutoCheckDisabledDialogViewModel : ViewModelBase, ICancelableDialog
{
    private readonly IDialogService _dialogService;
    private readonly MainWindowViewModel _mainWindow;

    public AutoCheckDisabledDialogViewModel(IDialogService dialogService, MainWindowViewModel mainWindow)
    {
        _dialogService = dialogService;
        _mainWindow = mainWindow;
    }

    [RelayCommand]
    private void GoToUpdates()
    {
        _dialogService.CloseDialog();
        _mainWindow.NavigateToSettingsUpdates();
    }

    [RelayCommand]
    private void Close()
    {
        _dialogService.CloseDialog();
    }

    [RelayCommand]
    private void Cancel()
    {
        Close();
    }
}
