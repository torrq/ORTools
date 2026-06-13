using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ORTools.Shared.Protocol;

namespace ORTools.UI.Services;

public static class ThemeService
{
    private static ThemeMode _currentMode = ThemeMode.System;

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
        
        // Find existing theme dictionary
        var existingTheme = dictionaries.FirstOrDefault(d => 
            d.Source != null && d.Source.OriginalString.Contains("Theme"));
            
        if (existingTheme != null)
        {
            // Only swap if it's actually changing
            if (existingTheme.Source.OriginalString != themeUri)
            {
                dictionaries.Remove(existingTheme);
                dictionaries.Add(new ResourceDictionary { Source = new Uri(themeUri) });
            }
        }
        else
        {
            dictionaries.Add(new ResourceDictionary { Source = new Uri(themeUri) });
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
