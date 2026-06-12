using System.Windows.Controls;
using ORTools.UI.ViewModels;

namespace ORTools.UI.Views
{
    public partial class AutoOffView : UserControl
    {
        public AutoOffView()
        {
            InitializeComponent();
        }

        private void Key1TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is AutoOffViewModel vm)
                ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.AutoOffKey1 = newKey, vm);
        }
        
        private void Key2TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is AutoOffViewModel vm)
                ORTools.UI.Helpers.InputHelper.HandleKeyInput(tb, e, newKey => vm.AutoOffKey2 = newKey, vm);
        }
    }
}
