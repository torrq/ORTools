using System.Windows.Controls;
using System.Windows.Input;

namespace ORTools.UI.Views;

public partial class MiscView : UserControl
{
    public MiscView()
    {
        InitializeComponent();
    }

    private void TransferKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && DataContext is ViewModels.MiscViewModel vm)
        {
            Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.TransferKey = newKey, "Misc_TransferKey");
        }
    }
}
