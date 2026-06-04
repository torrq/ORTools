using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ORTools.UI.Services;
using ORTools.UI.ViewModels;
using ORTools.UI.Views;

namespace ORTools.UI;

public partial class App : Application
{
    private WorkerService? _workerService;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _workerService = new WorkerService();
            var vm         = new MainWindowViewModel(_workerService);

            desktop.MainWindow = new MainWindow { DataContext = vm };

            desktop.Exit += (_, _) =>
            {
                _workerService.Dispose();
            };

            // Begin connecting to (or launching) the Worker in the background
            _ = _workerService.StartAsync(CancellationToken.None);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
