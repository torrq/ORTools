using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ORTools.UI.Helpers;

public static class InputHelper
{
    public static void HandleKeyInput(TextBox textBox, KeyEventArgs e, Action<string> onKeySet, object? sourceVM = null)
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



        onKeySet(newKey);
        
        // Remove focus so user doesn't accidentally overwrite it
        Keyboard.ClearFocus();
    }

    public static void HandleNumericUpDown(TextBox textBox, KeyEventArgs e, int min, int max, int step, Action<int> onValueSet)
    {
        // Handled entirely by the attached property behavior now (NumericTextBox_PreviewKeyDown).
        // Emptying this avoids double-execution and removes hardcoded limits in code-behinds.
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

    public static readonly System.Windows.DependencyProperty NumericDefaultProperty =
        System.Windows.DependencyProperty.RegisterAttached("NumericDefault", typeof(int), typeof(InputHelper), new System.Windows.PropertyMetadata(0));

    public static void SetNumericDefault(System.Windows.DependencyObject element, int value) => element.SetValue(NumericDefaultProperty, value);
    public static int GetNumericDefault(System.Windows.DependencyObject element) => (int)element.GetValue(NumericDefaultProperty);

    public static readonly System.Windows.DependencyProperty NumericMaxProperty =
        System.Windows.DependencyProperty.RegisterAttached("NumericMax", typeof(int), typeof(InputHelper), new System.Windows.PropertyMetadata(100000000));

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
                textBox.GotFocus += NumericTextBox_GotFocus;
                textBox.LostFocus += NumericTextBox_LostFocus;
                DataObject.AddPastingHandler(textBox, NumericTextBox_Pasting);
                
                // Initial formatting if not currently focused
                if (!textBox.IsFocused)
                {
                    FormatWithCommas(textBox);
                }
            }
            else
            {
                textBox.PreviewTextInput -= NumericTextBox_PreviewTextInput;
                textBox.TextChanged -= NumericTextBox_TextChanged;
                textBox.PreviewKeyDown -= NumericTextBox_PreviewKeyDown;
                textBox.GotFocus -= NumericTextBox_GotFocus;
                textBox.LostFocus -= NumericTextBox_LostFocus;
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
            string cleanText = tb.Text.Replace(",", "");
            if (string.IsNullOrWhiteSpace(cleanText) || cleanText == "-")
            {
                tb.Text = GetNumericDefault(tb).ToString();
                tb.CaretIndex = tb.Text.Length;
            }
            else if (int.TryParse(cleanText, out int val))
            {
                int max = GetNumericMax(tb);
                if (val > max)
                {
                    val = max;
                }
                
                if (!tb.IsFocused)
                {
                    string formatted = val.ToString("N0");
                    if (tb.Text != formatted)
                    {
                        tb.Text = formatted;
                    }
                }
                else
                {
                    string unformatted = val.ToString();
                    if (tb.Text != unformatted)
                    {
                        int caret = tb.CaretIndex;
                        tb.Text = unformatted;
                        tb.CaretIndex = caret;
                    }
                }
            }
        }
    }

    private static void NumericTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            string clean = tb.Text.Replace(",", "");
            if (tb.Text != clean)
            {
                tb.Text = clean;
            }
        }
    }

    private static void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            FormatWithCommas(tb);
        }
    }

    private static void FormatWithCommas(TextBox tb)
    {
        if (int.TryParse(tb.Text.Replace(",", ""), out int val))
        {
            string formatted = val.ToString("N0");
            if (tb.Text != formatted)
            {
                tb.Text = formatted;
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
            text = text.Replace(",", "");
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
