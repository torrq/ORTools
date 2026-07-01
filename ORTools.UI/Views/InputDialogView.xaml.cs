using System.Windows.Controls;

namespace ORTools.UI.Views;

public partial class InputDialogView : UserControl
{
    public InputDialogView()
    {
        InitializeComponent();
    }

    private void InputTextBox_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.CaretIndex = tb.Text.Length;
        }
    }
}
