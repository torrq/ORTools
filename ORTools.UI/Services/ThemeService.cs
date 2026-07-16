using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ORTools.Shared.Protocol;

namespace ORTools.UI.Services;

public static class ThemeService
{
    private static ThemeMode _currentMode = ThemeMode.BlueLight;
    private static int _serverMode = 1; // 1 = HR, 0 = MR

    public static bool IsCurrentThemeLight { get; private set; }

    /// <summary>1 = HR, 0 = MR.</summary>
    public static int ServerMode => _serverMode;

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

        try
        {
            string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "config.json");
            if (System.IO.File.Exists(configPath))
            {
                string json = System.IO.File.ReadAllText(configPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Theme", out var themeProp) && themeProp.TryGetInt32(out int themeVal))
                {
                    _currentMode = (ThemeMode)themeVal;
                }
            }
        }
        catch { }

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

    public static ThemeMode GetNextColorTheme()
    {
        bool isLight = IsCurrentThemeLight;

        ThemeMode[] lightThemes = { ThemeMode.GreenLight, ThemeMode.RedLight, ThemeMode.BlueLight, ThemeMode.MonoLight };
        ThemeMode[] darkThemes = { ThemeMode.GreenDark, ThemeMode.RedDark, ThemeMode.BlueDark, ThemeMode.MonoDark };

        var themes = isLight ? lightThemes : darkThemes;

        int index = Array.IndexOf(themes, _currentMode);

        if (index == -1)
        {
            string family = _serverMode == 1 ? "Green" : "Red";
            ThemeMode effectiveMode = isLight
                ? Enum.Parse<ThemeMode>($"{family}Light")
                : Enum.Parse<ThemeMode>($"{family}Dark");
            index = Array.IndexOf(themes, effectiveMode);
        }

        return themes[(index + 1) % themes.Length];
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
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D1B0B0"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF0F0"),
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#df3e3e"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B03D3D"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#df3e3e"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#170808"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#522828"),
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#a51e2e"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B2D2D"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E56B6B"));
            }
        }
        else if (colorFamily == "Blue")
        {
            if (useLight)
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8FC8F5"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DFF0FF"),
                    new Point(0, 0), new Point(1, 1));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#006dd1"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#113e99"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#006dd1"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#030A1A"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14568B"),
                    new Point(0, 0), new Point(1, 1));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#208cff"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0C4687"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#72CCFC"));
                newTheme["AppSubtleBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BEBEBE"));
            }
        }
        else if (colorFamily == "Mono")
        {
            if (useLight)
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C7C7C7"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"),
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4e4e4e"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4e4e4e"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#09090b"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3f3f46"),
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#828282"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#444444"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A0A0A0"));
            }
        }
        else // Green
        {
            if (useLight)
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B5CDA3"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F2F9ED"),
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#78b10a"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5C7C2F"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#78b10a"));
            }
            else
            {
                newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0D1209"),
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3E572D"),
                    new Point(0, 0), new Point(1, 0));
                newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#78b10a"));
                newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D521F"));
                newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8BB54C"));
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
