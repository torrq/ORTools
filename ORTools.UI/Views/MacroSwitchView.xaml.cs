using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.Helpers;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class MacroSwitchView : UserControl
{
    public MacroSwitchView()
    {
        InitializeComponent();
    }

    private void TriggerKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is MacroSwitchRowViewModel vm)
        {
            InputHelper.HandleKeyInput(tb, e, key => vm.TriggerKey = key, vm);
        }
    }

    private void StepKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is MacroSwitchStepViewModel vm)
        {
            InputHelper.HandleKeyInput(tb, e, key => vm.Key = key, vm);
        }
    }

    private void StepDelay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is MacroSwitchStepViewModel vm)
        {
            InputHelper.HandleNumericUpDown(tb, e, 1, 99999, 50, val => vm.Delay = val);
        }
    }
}
