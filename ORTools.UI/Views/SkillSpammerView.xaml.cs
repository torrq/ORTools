using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views;

public partial class SkillSpammerView : UserControl
{
    public SkillSpammerView()
    {
        InitializeComponent();
    }

    private void ToggleModeKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is SkillSpammerViewModel vm)
        {
            ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.ToggleModeKey = newKey, "SkillSpammer_ToggleKey");
        }
    }

    private void SpammerDelay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is SkillSpammerViewModel vm)
        {
            ORTools.UI.Helpers.InputHelper.HandleNumericUpDown(tb, e, 0, 1000, 10, newVal => vm.SpammerDelay = newVal);
        }
    }

    private void SpammerDelay_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
        {
            tb.Text = "0";
            tb.CaretIndex = 1;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
