using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.Helpers;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class AutobuffItemView : UserControl
{
    public AutobuffItemView()
    {
        InitializeComponent();
    }

    private void KeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is AutobuffItemItemViewModel vm)
        {
            InputHelper.HandleKeyInput(tb, e, newKey => vm.Key = newKey, vm);
        }
    }

    private void DelayTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void DelayTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && DataContext is AutobuffItemViewModel vm)
        {
            InputHelper.HandleNumericUpDown(tb, e, 10, 600000, 50, newVal => vm.Delay = newVal);
        }
    }
}
