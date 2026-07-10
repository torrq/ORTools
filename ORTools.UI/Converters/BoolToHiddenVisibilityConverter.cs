using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ORTools.UI.Converters
{
    /// <summary>
    /// Like BooleanToVisibilityConverter, but returns Hidden instead of Collapsed for false.
    /// Use this when an element's layout space must stay reserved even while invisible,
    /// e.g. so sibling content doesn't shift/re-center when it toggles off.
    /// </summary>
    public class BoolToHiddenVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Hidden;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }
}
