using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ORTools.UI.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ORTools.UI.ViewModels;

public sealed partial class UpdateAvailableDialogViewModel : ViewModelBase, ICancelableDialog
{
    private readonly IDialogService _dialogService;
    private readonly MainWindowViewModel _mainWindow;
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty] private string _latestVersion;
    [ObservableProperty] private string _releaseUrl;
    [ObservableProperty] private string _directZipUrl = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotUpdating))]
    private bool _isUpdating;
    public bool IsNotUpdating => !IsUpdating;

    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private string _errorMessage = "";
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    public string MessageText => string.Format(
        LanguageService.Get("S.Dialog.UpdateAvailableMessage"), LatestVersion);

    public UpdateAvailableDialogViewModel(
        string latestVersion,
        string releaseUrl,
        IDialogService dialogService,
        MainWindowViewModel mainWindow,
        string? downloadZipUrl = null)
    {
        LatestVersion = latestVersion;
        ReleaseUrl = releaseUrl;
        _dialogService = dialogService;
        _mainWindow = mainWindow;

        string modeTag = ThemeService.ServerMode == 0 ? "MR" : "HR";
        string tag = latestVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? latestVersion : $"v{latestVersion}";
        DirectZipUrl = !string.IsNullOrWhiteSpace(downloadZipUrl)
            ? downloadZipUrl
            : $"https://github.com/torrq/ORTools/releases/download/{tag}/OSROTools_{tag}-{modeTag}.zip";

        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(MessageText));
    }

    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (IsUpdating) return;
        IsUpdating = true;
        ErrorMessage = "";
        DownloadProgress = 0;
        UpdateStatus = "Starting update...";
        _downloadCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<(double Percent, string Status)>(p =>
            {
                DownloadProgress = p.Percent;
                UpdateStatus = p.Status;
            });

            await AutoUpdaterService.DownloadAndStageUpdateAsync(DirectZipUrl, progress, _downloadCts.Token);

            // Exit cleanly so the staged swap script can replace the exe and relaunch
            _mainWindow.ExitAppCommand.Execute(null);
        }
        catch (OperationCanceledException)
        {
            IsUpdating = false;
            UpdateStatus = "";
        }
        catch (Exception ex)
        {
            IsUpdating = false;
            UpdateStatus = "";
            ErrorMessage = $"Auto-update failed: {ex.Message}";
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
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
        _downloadCts?.Cancel();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        _dialogService.CloseDialog();
    }

    [RelayCommand]
    private void Cancel()
    {
        Close();
    }
}
