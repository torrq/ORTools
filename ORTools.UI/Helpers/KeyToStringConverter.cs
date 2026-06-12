using System;
using System.Globalization;
using System.Windows.Data;

namespace ORTools.UI.Helpers;

public class KeyToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key)
        {
            return FormatKey(key);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // We never need to convert back from the pretty string to the Keys enum string
        // because the Key bindings are updated explicitly via InputHelper.HandleKeyInput
        // and we don't want the pretty string to accidentally overwrite the bound property.
        return Binding.DoNothing;
    }

    public static string FormatKey(string key)
    {
        if (key.StartsWith("NumPad")) return "NP" + key.Substring(6);
        
        return key switch
        {
            "Oemtilde" => "`",
            "Oemplus" => "+",
            "OemMinus" => "-",
            "OemPeriod" => ".",
            "Oemcomma" => ",",
            "OemOpenBrackets" => "[",
            "OemCloseBrackets" => "]",
            "OemQuotes" => "'",
            "OemSemicolon" => ";",
            "OemQuestion" => "/",
            "OemPipe" => "\\",
            "OemBackslash" => "\\",
            "OemClear" => "Clr",
            "Return" => "Ent",
            "Next" => "PgDn",
            "Prior" => "PgUp",
            "Capital" => "Caps",
            "Escape" => "Esc",
            "Delete" => "Del",
            "Insert" => "Ins",
            "Space" => "Spc",
            "LeftShift" => "LShift",
            "RightShift" => "RShift",
            "LeftCtrl" => "LCtrl",
            "RightCtrl" => "RCtrl",
            "LeftAlt" => "LAlt",
            "RightAlt" => "RAlt",
            "D0" => "0",
            "D1" => "1",
            "D2" => "2",
            "D3" => "3",
            "D4" => "4",
            "D5" => "5",
            "D6" => "6",
            "D7" => "7",
            "D8" => "8",
            "D9" => "9",
            _ => key
        };
    }
}
