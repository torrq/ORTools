using System;
using System.Globalization;
using System.Windows.Data;
using ORTools.Shared.Protocol;

namespace ORTools.UI.Helpers;

public class ThemeModeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ThemeMode mode)
        {
            return mode switch
            {
                ThemeMode.System => "System",
                ThemeMode.GreenLight => "Green Light",
                ThemeMode.GreenDark => "Green Dark",
                ThemeMode.RedLight => "Red Light",
                ThemeMode.RedDark => "Red Dark",
                ThemeMode.BlueLight => "Blue Light",
                ThemeMode.BlueDark => "Blue Dark",
                ThemeMode.MonoLight => "Mono Light",
                ThemeMode.MonoDark => "Mono Dark",
                _ => mode.ToString()
            };
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
