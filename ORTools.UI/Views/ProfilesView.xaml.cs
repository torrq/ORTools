using System.Windows.Controls;

namespace ORTools.UI.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView()
    {
        InitializeComponent();
    }

    private void ListBoxItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is ViewModels.ProfileItemViewModel profile)
        {
            if (this.DataContext is ViewModels.ProfilesViewModel vm)
            {
                // To trigger profile switch, we just change the current profile. 
                // Wait, ProfilesViewModel doesn't have a Switch command exposed directly?
                // Let's just set the SelectedProfile and then ask Worker to switch it. 
                // But in MainWindowViewModel we have a watcher on CurrentProfile.
                // It's easier to just cast DataContext to ProfilesViewModel, but we need to send the SwitchProfileCommand directly.
                // Wait, ProfilesViewModel doesn't expose _worker! We can just use the MainWindowViewModel which is bound to the window's datacontext.
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow?.DataContext is ViewModels.MainWindowViewModel mainVm)
                {
                    mainVm.CurrentProfile = profile.Name;
                }
            }
        }
    }
}
