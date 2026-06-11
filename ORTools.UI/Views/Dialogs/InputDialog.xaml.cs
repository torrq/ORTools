using System.Windows;

namespace ORTools.UI.Views.Dialogs
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public InputDialog(string message, string title, string defaultValue = "")
        {
            InitializeComponent();
            this.Title = title;
            this.MessageTextBlock.Text = message;
            this.InputTextBox.Text = defaultValue;
            this.InputTextBox.SelectAll();
            this.InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.InputText = this.InputTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
