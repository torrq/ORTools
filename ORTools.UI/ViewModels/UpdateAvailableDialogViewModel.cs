using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.UI.Services;
using System.Diagnostics;

namespace ORTools.UI.ViewModels;

public sealed partial class UpdateAvailableDialogViewModel : ViewModelBase, ICancelableDialog
{
    private readonly IDialogService _dialogService;
    private readonly MainWindowViewModel _mainWindow;

    [ObservableProperty] private string _latestVersion;
    [ObservableProperty] private string _releaseUrl;
    [ObservableProperty] private string _directZipUrl = "";

    public string MessageText => string.Format(
        LanguageService.Get("S.Dialog.UpdateAvailableMessage"), LatestVersion);

    public UpdateAvailableDialogViewModel(string latestVersion, string releaseUrl, IDialogService dialogService, MainWindowViewModel mainWindow)
    {
        LatestVersion = latestVersion;
        ReleaseUrl = releaseUrl;
        _dialogService = dialogService;
        _mainWindow = mainWindow;

        string modeTag = ThemeService.ServerMode == 0 ? "MR" : "HR";
        string tag = latestVersion.StartsWith("v", System.StringComparison.OrdinalIgnoreCase) ? latestVersion : $"v{latestVersion}";
        DirectZipUrl = $"https://github.com/torrq/ORTools/releases/download/{tag}/OSROTools_{tag}-{modeTag}.zip";

        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(MessageText));
    }

    [RelayCommand]
    private void OpenDirectZipUrl()
    {
        if (!string.IsNullOrEmpty(DirectZipUrl))
        {
            Process.Start(new ProcessStartInfo(DirectZipUrl) { UseShellExecute = true });
        }
        Close();
    }

    [RelayCommand]
    private void OpenReleaseUrl()
    {
        if (!string.IsNullOrEmpty(ReleaseUrl))
        {
            Process.Start(new ProcessStartInfo(ReleaseUrl) { UseShellExecute = true });
        }
        Close();
    }

    [RelayCommand]
    private void DisableAutoChecking()
    {
        _mainWindow.Settings.CheckForUpdatesOnStartup = false;
        Close();

        var alert = new AutoCheckDisabledDialogViewModel(_dialogService, _mainWindow);
        _ = _dialogService.ShowDialogAsync(alert);
    }

    [RelayCommand]
    private void Close()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _dialogService.CloseDialog();
    }

    [RelayCommand]
    private void Cancel()
    {
        Close();
    }
}
