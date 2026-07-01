using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ORTools.UI.Helpers;

public class HpPercentToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            var normalBrush = Application.Current.TryFindResource("AppHpNormalBrush") as LinearGradientBrush;
            if (normalBrush == null || normalBrush.GradientStops.Count < 2)
            {
                return new SolidColorBrush(Colors.Green);
            }

            Color topColor = normalBrush.GradientStops[0].Color;
            Color bottomColor = normalBrush.GradientStops[1].Color;

            if (percent >= 15)
            {
                return normalBrush;
            }

            // Interpolate from Dark Red (0%) to Vibrant Red (15%)
            // ratio = 0.0 (0%) to 1.0 (15%)
            double ratio = Math.Max(0, percent) / 15.0;

            // Helper to interpolate between two byte values
            byte Interpolate(byte color0, byte color15, double r)
            {
                return (byte)(color0 + (color15 - color0) * r);
            }

            // Top Color
            // Dark Red Top (0%): 0x88, 0x00, 0x00
            // Vibrant Red Top (15%): 0xFF, 0x11, 0x11
            byte tR = Interpolate(0x88, 0xFF, ratio);
            byte tG = Interpolate(0x00, 0x11, ratio);
            byte tB = Interpolate(0x00, 0x11, ratio);

            // Bottom Color
            // Dark Red Bottom (0%): 0x44, 0x00, 0x00
            // Vibrant Red Bottom (15%): 0xBB, 0x00, 0x00
            byte bR = Interpolate(0x44, 0xBB, ratio);
            byte bG = Interpolate(0x00, 0x00, ratio);
            byte bB = Interpolate(0x00, 0x00, ratio);

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
