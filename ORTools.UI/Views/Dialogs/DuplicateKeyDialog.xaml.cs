using System.Windows;

namespace ORTools.UI.Views.Dialogs
{
    public partial class DuplicateKeyDialog : Window
    {
        public DuplicateKeyDialog(string featureName)
        {
            InitializeComponent();
            this.MessageTextBlock.Text = $"This key is already in use by '{featureName}'. Do you want to continue and overwrite it?";
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
