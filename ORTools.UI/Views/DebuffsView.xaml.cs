using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.Helpers;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class DebuffsView : UserControl
{
    public DebuffsView()
    {
        InitializeComponent();
    }

    private void KeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb)
        {
            if (tb.DataContext is StatusRecoveryItemViewModel srItem)
            {
                InputHelper.HandleKeyInput(tb, e, newKey => srItem.Key = newKey);
            }
            else if (tb.DataContext is DebuffRecoveryItemViewModel drItem)
            {
                InputHelper.HandleKeyInput(tb, e, newKey => drItem.Key = newKey);
            }
        }
    }

    private void DelayTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && DataContext is DebuffsViewModel vm)
        {
            InputHelper.HandleNumericUpDown(tb, e, 10, 600000, 50, newVal => vm.Delay = newVal);
        }
    }

    private void DelayTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }
}
