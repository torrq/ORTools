using System;
using System.Globalization;
using System.Windows.Data;

namespace ORTools.UI.Converters
{
    /// <summary>
    /// Multi-value converter for the Auto Off quick-hour chips.
    /// values[0]: the chip's hour value (int), bound to the item itself.
    /// values[1]: the current AutoOffTime in minutes (int), bound from the view model.
    /// Returns true when the chip represents the currently selected time exactly.
    /// </summary>
    public class HourSelectedConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not int hour || values[1] is not int totalMinutes)
                return false;

            return totalMinutes == hour * 60;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
