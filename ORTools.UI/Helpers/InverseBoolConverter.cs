using System.Globalization;
using System.Windows.Data;

namespace ORTools.UI.Helpers;

/// <summary>Inverts a boolean value for two-way binding (e.g. RadioButton IsChecked).</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
