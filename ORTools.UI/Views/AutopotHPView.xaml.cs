using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class AutopotHPView : UserControl
{
    public AutopotHPView()
    {
        InitializeComponent();
    }

    private void KeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AutopotSlotViewModel slot)
        {
            ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, newKey => slot.Key = newKey);
        }
    }

    private void DelayTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AutopotHPViewModel vm)
        {
            ORTools.UI.Helpers.InputHelper.HandleNumericUpDown(tb, e, 10, 10000, 10, newVal => vm.Delay = newVal);
        }
    }

    private void DelayTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void DelayTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
        {
            tb.Text = "0";
            tb.CaretIndex = 1;
        }
    }
}
