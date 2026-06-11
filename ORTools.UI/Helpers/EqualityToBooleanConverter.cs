using System;
using System.Globalization;
using System.Windows.Data;

namespace ORTools.UI.Helpers;

public class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b) return parameter;
        return Binding.DoNothing;
    }
}
