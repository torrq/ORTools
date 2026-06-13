using System;
using System.Globalization;
using System.Windows.Data;

namespace ORTools.UI.Helpers;

public class EqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] != null && values[1] != null)
        {
            return values[0].ToString() == values[1].ToString();
        }
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
