using System.Windows.Controls;
using System.Windows.Input;
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
            ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.ToggleModeKey = newKey);
        }
    }
}
