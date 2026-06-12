using System.Windows.Controls;

namespace ORTools.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SongRows_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is TextBox tb && DataContext is ViewModels.SettingsViewModel vm)
        {
            Helpers.InputHelper.HandleNumericUpDown(tb, e, 1, 100, 1, newVal => vm.SongRows = newVal);
        }
    }

    private void MacroSwitchRows_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is TextBox tb && DataContext is ViewModels.SettingsViewModel vm)
        {
            Helpers.InputHelper.HandleNumericUpDown(tb, e, 1, 100, 1, newVal => vm.MacroSwitchRows = newVal);
        }
    }

    private void AtkDefRows_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is TextBox tb && DataContext is ViewModels.SettingsViewModel vm)
        {
            Helpers.InputHelper.HandleNumericUpDown(tb, e, 1, 100, 1, newVal => vm.AtkDefRows = newVal);
        }
    }

    private void DefaultToggleKey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is TextBox tb && DataContext is ViewModels.SettingsViewModel vm)
        {
            Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.DefaultToggleStateKey = newKey, "Settings_DefaultToggleKey");
        }
    }

    private void RowsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
        {
            tb.Text = "1";
            tb.CaretIndex = 1;
        }
    }
}
