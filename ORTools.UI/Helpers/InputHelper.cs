using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace ORTools.UI.Helpers;

public static class InputHelper
{
    public static void HandleKeyInput(TextBox textBox, KeyEventArgs e, Action<string> onKeySet)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;

        string newKey = (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Escape)
            ? "None"
            : e.Key.ToString();

        // Check for duplicates via MainWindowViewModel (the root context)
        var mainWindowVm = (ViewModels.MainWindowViewModel)System.Windows.Application.Current.MainWindow.DataContext;
        if (newKey != "None" && mainWindowVm.IsKeyInUse(newKey))
        {
            var dialog = new Views.Dialogs.DuplicateKeyDialog("another feature")
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true)
            {
                Keyboard.ClearFocus();
                return;
            }
        }

        onKeySet(newKey);
        
        // Remove focus so user doesn't accidentally overwrite it
        Keyboard.ClearFocus();
    }

    public static void HandleNumericUpDown(TextBox textBox, KeyEventArgs e, int min, int max, int step, Action<int> onValueSet)
    {
        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            e.Handled = true;
            if (int.TryParse(textBox.Text, out int currentValue))
            {
                int newValue = e.Key == Key.Up ? currentValue + step : currentValue - step;
                newValue = Math.Clamp(newValue, min, max);
                onValueSet(newValue);
            }
        }
    }
}
