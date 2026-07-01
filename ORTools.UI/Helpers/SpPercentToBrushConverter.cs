using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ORTools.UI.Helpers;

public class SpPercentToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            var normalBrush = Application.Current.TryFindResource("AppSpNormalBrush") as LinearGradientBrush;
            if (normalBrush == null || normalBrush.GradientStops.Count < 2)
            {
                return new SolidColorBrush(Colors.Blue);
            }

            Color topColor = normalBrush.GradientStops[0].Color;
            Color bottomColor = normalBrush.GradientStops[1].Color;

            if (percent >= 15)
            {
                return normalBrush;
            }

            // Interpolate from Dark Purple (0%) to Vibrant Purple (15%)
            // ratio = 0.0 (0%) to 1.0 (15%)
            double ratio = Math.Max(0, percent) / 15.0;

            // Helper to interpolate between two byte values
            byte Interpolate(byte color0, byte color15, double r)
            {
                return (byte)(color0 + (color15 - color0) * r);
            }

            // Top Color
            // Dark Purple Top (0%): 0x4C, 0x19, 0x7F
            // Vibrant Purple Top (15%): 0x99, 0x33, 0xFF
            byte tR = Interpolate(0x4C, 0x99, ratio);
            byte tG = Interpolate(0x19, 0x33, ratio);
            byte tB = Interpolate(0x7F, 0xFF, ratio);

            // Bottom Color
            // Dark Purple Bottom (0%): 0x2A, 0x00, 0x66
            // Vibrant Purple Bottom (15%): 0x55, 0x00, 0xCC
            byte bR = Interpolate(0x2A, 0x55, ratio);
            byte bG = Interpolate(0x00, 0x00, ratio);
            byte bB = Interpolate(0x66, 0xCC, ratio);

            return new LinearGradientBrush(
                Color.FromRgb(tR, tG, tB),
                Color.FromRgb(bR, bG, bB),
                normalBrush.StartPoint,
                normalBrush.EndPoint);
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
