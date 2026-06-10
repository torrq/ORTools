using System.Windows;
using System.Windows.Input;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void ToggleKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is System.Windows.Controls.TextBox tb)
        {
            ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.UpdateToggleKeyCommand.Execute(newKey));
        }
    }

    private void ProcessList_DropDownOpened(object sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RefreshProcessListCommand.Execute(null);
        }
    }
}
