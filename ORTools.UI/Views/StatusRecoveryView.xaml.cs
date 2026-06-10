using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.ViewModels;
using ORTools.UI.Helpers;

namespace ORTools.UI.Views;

public partial class StatusRecoveryView : UserControl
{
    public StatusRecoveryView()
    {
        InitializeComponent();
    }

    private void KeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is StatusRecoveryItemViewModel item)
        {
            InputHelper.HandleKeyInput(tb, e, newKey => item.Key = newKey);
        }
    }

    private void DelayTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is StatusRecoveryViewModel vm)
        {
            InputHelper.HandleNumericUpDown(tb, e, 10, 10000, 10, newVal => vm.Delay = newVal);
        }
    }

    private void DelayTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }
}
