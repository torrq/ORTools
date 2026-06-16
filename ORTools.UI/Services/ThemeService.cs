using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ORTools.Shared.Protocol;

namespace ORTools.UI.Services;

public static class ThemeService
{
    private static ThemeMode _currentMode = ThemeMode.System;
    private static int _serverMode = 1; // 1 = HR, 0 = MR

    public static bool IsCurrentThemeLight { get; private set; }

    public static void SetServerMode(int serverMode)
    {
        if (_serverMode != serverMode)
        {
            _serverMode = serverMode;
            ApplyTheme(_currentMode);
        }
    }

    public static void Initialize()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyTheme(_currentMode);
    }

    public static ThemeMode[] GetAvailableThemes()
    {
        if (_serverMode == 1) // HR
            return new[] { ThemeMode.System, 
                           ThemeMode.GreenLight, ThemeMode.RedLight, ThemeMode.BlueLight, ThemeMode.MonoLight,
                           ThemeMode.GreenDark, ThemeMode.RedDark, ThemeMode.BlueDark, ThemeMode.MonoDark };
        else // MR
            return new[] { ThemeMode.System, 
                           ThemeMode.RedLight, ThemeMode.GreenLight, ThemeMode.BlueLight, ThemeMode.MonoLight,
                           ThemeMode.RedDark, ThemeMode.GreenDark, ThemeMode.BlueDark, ThemeMode.MonoDark };
    }

    public static ThemeMode GetInvertedTheme()
    {
        if (_currentMode == ThemeMode.System)
        {
            bool isLight = IsWindowsLightMode();
            if (_serverMode == 1) return isLight ? ThemeMode.GreenDark : ThemeMode.GreenLight;
            else return isLight ? ThemeMode.RedDark : ThemeMode.RedLight;
        }
        
        return _currentMode switch
        {
            ThemeMode.GreenLight => ThemeMode.GreenDark,
            ThemeMode.GreenDark => ThemeMode.GreenLight,
            ThemeMode.RedLight => ThemeMode.RedDark,
            ThemeMode.RedDark => ThemeMode.RedLight,
            ThemeMode.BlueLight => ThemeMode.BlueDark,
            ThemeMode.BlueDark => ThemeMode.BlueLight,
            ThemeMode.MonoLight => ThemeMode.MonoDark,
            ThemeMode.MonoDark => ThemeMode.MonoLight,
            _ => ThemeMode.GreenLight
        };
    }

    public static void ApplyTheme(ThemeMode mode)
    {
        _currentMode = mode;
        
        bool useLight = false;
        string colorFamily = "Green"; // Default to Green (HR)

        if (mode == ThemeMode.System)
        {
            useLight = IsWindowsLightMode();
            colorFamily = _serverMode == 1 ? "Green" : "Red";
        }
        else
        {
            useLight = mode == ThemeMode.GreenLight || mode == ThemeMode.RedLight || mode == ThemeMode.BlueLight || mode == ThemeMode.MonoLight;
            if (mode == ThemeMode.GreenLight || mode == ThemeMode.GreenDark) colorFamily = "Green";
            else if (mode == ThemeMode.RedLight || mode == ThemeMode.RedDark) colorFamily = "Red";
            else if (mode == ThemeMode.BlueLight || mode == ThemeMode.BlueDark) colorFamily = "Blue";
            else if (mode == ThemeMode.MonoLight || mode == ThemeMode.MonoDark) colorFamily = "Mono";
        }

        IsCurrentThemeLight = useLight;

        string themeUri = useLight 
            ? "pack://application:,,,/ORTools;component/Themes/ThemeLight.xaml" 
            : "pack://application:,,,/ORTools;component/Themes/ThemeDark.xaml";

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var newTheme = new ResourceDictionary { Source = new Uri(themeUri) };

        if (colorFamily == "Red")
        {
            if (useLight)
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5C9C9"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F7EDED"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D34A4A"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B03D3D"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B1010"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D1F1F"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8A3A3A"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B2D2D"));
            }
        }
        else if (colorFamily == "Blue")
        {
            if (useLight)
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C1D1E1"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E6EEF4"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A7197"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2F5C7C"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10161E"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F2B3D"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#284F6B"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F3D52"));
            }
        }
        else if (colorFamily == "Mono")
        {
            if (useLight)
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0E0E0"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F5F5"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#808080"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#181818"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#444444"));
            }
        }
        else // Green
        {
            if (useLight)
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CFE1C1"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ECF4E6"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#71973A"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5C7C2F"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#161E10"), 
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B3D1F"), 
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4F6B28"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D521F"));
            }
        }

        var existingTheme = dictionaries.FirstOrDefault(d => 
            d.Source != null && d.Source.OriginalString.Contains("Theme"));

        if (existingTheme != null)
        {
            dictionaries.Remove(existingTheme);
            dictionaries.Add(newTheme);
        }
        else
        {
            dictionaries.Add(newTheme);
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentMode == ThemeMode.System)
        {
            // Windows theme might have changed
            Application.Current.Dispatcher.Invoke(() => ApplyTheme(ThemeMode.System));
        }
    }

    private static bool IsWindowsLightMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int useLight)
            {
                return useLight == 1;
            }
        }
        catch
        {
            // Default to dark if we can't read the registry
        }
        return false;
    }
}
