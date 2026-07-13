using System.Windows;
using ORTools.UI.Services;
using ORTools.UI.ViewModels;
using ORTools.UI.Views;

namespace ORTools.UI;

public partial class App : Application
{
    private WorkerService? _workerService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Initialize();
        LanguageService.Initialize();

        _workerService = new WorkerService();
        var mainWindow = new MainWindow
        {
            DataContext = new MainWindowViewModel(_workerService)
        };

        MainWindow = mainWindow;
        mainWindow.Show();

        _ = _workerService.StartAsync(CancellationToken.None);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _workerService?.Dispose();
        base.OnExit(e);
    }
}
