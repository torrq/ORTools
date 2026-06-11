using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ORTools.UI.Helpers;

public static class InputHelper
{
    public static void HandleKeyInput(TextBox textBox, KeyEventArgs e, Action<string> onKeySet)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;

        string newKey = e.Key switch
        {
            Key.Back or Key.Delete or Key.Escape => "None",
            Key.PageUp => "Prior",
            Key.PageDown => "Next",
            Key.Enter or Key.Return => "Return",
            Key.OemTilde => "Oemtilde",
            Key.OemPlus => "Oemplus",
            Key.OemMinus => "OemMinus",
            Key.OemPeriod => "OemPeriod",
            Key.OemComma => "Oemcomma",
            Key.OemOpenBrackets => "OemOpenBrackets",
            Key.OemCloseBrackets => "OemCloseBrackets",
            Key.OemQuotes => "OemQuotes",
            Key.OemSemicolon => "OemSemicolon",
            Key.OemQuestion => "OemQuestion",
            Key.OemPipe => "OemPipe",
            Key.OemBackslash => "OemBackslash",
            Key.OemClear => "OemClear",
            _ => e.Key.ToString()
        };

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

    // ── Attached Properties for Unified Numeric Input ─────────────────────────

    public static readonly System.Windows.DependencyProperty IsNumericInputProperty =
        System.Windows.DependencyProperty.RegisterAttached("IsNumericInput", typeof(bool), typeof(InputHelper), new System.Windows.PropertyMetadata(false, OnIsNumericInputChanged));

    public static void SetIsNumericInput(System.Windows.DependencyObject element, bool value) => element.SetValue(IsNumericInputProperty, value);
    public static bool GetIsNumericInput(System.Windows.DependencyObject element) => (bool)element.GetValue(IsNumericInputProperty);

    public static readonly System.Windows.DependencyProperty NumericMinProperty =
        System.Windows.DependencyProperty.RegisterAttached("NumericMin", typeof(int), typeof(InputHelper), new System.Windows.PropertyMetadata(0));

    public static void SetNumericMin(System.Windows.DependencyObject element, int value) => element.SetValue(NumericMinProperty, value);
    public static int GetNumericMin(System.Windows.DependencyObject element) => (int)element.GetValue(NumericMinProperty);

    public static readonly System.Windows.DependencyProperty NumericMaxProperty =
        System.Windows.DependencyProperty.RegisterAttached("NumericMax", typeof(int), typeof(InputHelper), new System.Windows.PropertyMetadata(1000000));

    public static void SetNumericMax(System.Windows.DependencyObject element, int value) => element.SetValue(NumericMaxProperty, value);
    public static int GetNumericMax(System.Windows.DependencyObject element) => (int)element.GetValue(NumericMaxProperty);

    public static readonly System.Windows.DependencyProperty NumericStepProperty =
        System.Windows.DependencyProperty.RegisterAttached("NumericStep", typeof(int), typeof(InputHelper), new System.Windows.PropertyMetadata(10));

    public static void SetNumericStep(System.Windows.DependencyObject element, int value) => element.SetValue(NumericStepProperty, value);
    public static int GetNumericStep(System.Windows.DependencyObject element) => (int)element.GetValue(NumericStepProperty);

    private static void OnIsNumericInputChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
                textBox.TextChanged += NumericTextBox_TextChanged;
                textBox.PreviewKeyDown += NumericTextBox_PreviewKeyDown;
                DataObject.AddPastingHandler(textBox, NumericTextBox_Pasting);
            }
            else
            {
                textBox.PreviewTextInput -= NumericTextBox_PreviewTextInput;
                textBox.TextChanged -= NumericTextBox_TextChanged;
                textBox.PreviewKeyDown -= NumericTextBox_PreviewKeyDown;
                DataObject.RemovePastingHandler(textBox, NumericTextBox_Pasting);
            }
        }
    }

    private static void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private static void NumericTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            if (string.IsNullOrWhiteSpace(tb.Text) || tb.Text == "-")
            {
                tb.Text = GetNumericMin(tb).ToString();
                tb.CaretIndex = tb.Text.Length;
            }
            else if (int.TryParse(tb.Text, out int val))
            {
                int max = GetNumericMax(tb);
                if (val > max)
                {
                    tb.Text = max.ToString();
                    tb.CaretIndex = tb.Text.Length;
                }
            }
        }
    }

    private static void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        if (sender is TextBox tb && (e.Key == Key.Up || e.Key == Key.Down))
        {
            e.Handled = true;
            if (int.TryParse(tb.Text, out int currentValue))
            {
                int step = GetNumericStep(tb);
                int min = GetNumericMin(tb);
                int max = GetNumericMax(tb);
                
                int newValue = e.Key == Key.Up ? currentValue + step : currentValue - step;
                newValue = Math.Clamp(newValue, min, max);
                
                tb.Text = newValue.ToString();
                tb.CaretIndex = tb.Text.Length;
            }
        }
    }

    private static void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string text = (string)e.DataObject.GetData(typeof(string));
            if (!int.TryParse(text, out _))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }
}
