using System.Windows.Controls;
using System.Windows.Input;
using ORTools.UI.Helpers;

namespace ORTools.UI.Views;

public partial class AutobuffSearchView : UserControl
{
    public AutobuffSearchView()
    {
        InitializeComponent();
    }

    private void KeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext != null)
        {
            dynamic vm = tb.DataContext;
            InputHelper.HandleKeyInput(tb, e, newKey => vm.Key = newKey);
        }
    }

    private void UserControl_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (this.IsVisible)
        {
            Dispatcher.BeginInvoke(new System.Action(() => SearchTextBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
