using System.Windows;

namespace ORTools.UI.Views.Dialogs
{
    public partial class ConfirmDeleteDialog : Window
    {
        public ConfirmDeleteDialog(string message)
        {
            InitializeComponent();
            this.MessageTextBlock.Text = message;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
