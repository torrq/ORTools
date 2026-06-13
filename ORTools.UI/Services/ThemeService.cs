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

    public static void ApplyTheme(ThemeMode mode)
    {
        _currentMode = mode;
        
        bool useLight = false;
        if (mode == ThemeMode.System)
        {
            useLight = IsWindowsLightMode();
        }
        else
        {
            useLight = mode == ThemeMode.Light;
        }

        string themeUri = useLight 
            ? "pack://application:,,,/ORTools;component/Themes/ThemeLight.xaml" 
            : "pack://application:,,,/ORTools;component/Themes/ThemeDark.xaml";

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        
        var newTheme = new ResourceDictionary { Source = new Uri(themeUri) };

        // Inject ServerMode specific colors
        if (_serverMode == 0) // MR - Red
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
        else // HR - Green
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
