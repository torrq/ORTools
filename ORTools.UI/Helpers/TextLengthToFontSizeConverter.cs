using System;
using System.Globalization;
using System.Windows.Data;

namespace ORTools.UI.Helpers;

public class TextLengthToFontSizeConverter : IValueConverter
{
    public double NormalSize { get; set; } = 13.0;
    public double SmallSize { get; set; } = 10.0;
    public int Threshold { get; set; } = 30;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && text.Length > Threshold)
        {
            return SmallSize;
        }
        return NormalSize;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
