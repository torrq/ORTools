using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ORTools.Shared.Protocol;
using ORTools.UI.Helpers;
using ORTools.Worker;

namespace ORTools.UI.Services;

public static class ThemeService
{
    private static ThemeMode _currentMode = ThemeMode.BlueLight;
    private static int _serverMode = 1; // 1 = HR, 0 = MR
    private static bool _customIsLight = AppConfig.DefaultCustomThemeIsLight;
    private static string _customColor = AppConfig.DefaultCustomThemeColor;

    public static bool IsCurrentThemeLight { get; private set; }
    public static bool CustomIsLight => _customIsLight;
    public static string CustomColor => _customColor;

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
                if (doc.RootElement.TryGetProperty("CustomThemeIsLight", out var isLightProp))
                {
                    _customIsLight = isLightProp.GetBoolean();
                }
                if (doc.RootElement.TryGetProperty("CustomThemeColor", out var colorProp) && colorProp.GetString() is string cStr)
                {
                    _customColor = cStr;
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
                           ThemeMode.GreenDark, ThemeMode.RedDark, ThemeMode.BlueDark, ThemeMode.MonoDark,
                           ThemeMode.Custom };
        else // MR
            return new[] { ThemeMode.System,
                           ThemeMode.RedLight, ThemeMode.GreenLight, ThemeMode.BlueLight, ThemeMode.MonoLight,
                           ThemeMode.RedDark, ThemeMode.GreenDark, ThemeMode.BlueDark, ThemeMode.MonoDark,
                           ThemeMode.Custom };
    }

    public static ThemeMode GetInvertedTheme()
    {
        if (_currentMode == ThemeMode.Custom)
        {
            _customIsLight = !_customIsLight;
            return ThemeMode.Custom;
        }

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

        ThemeMode[] lightThemes = _serverMode == 1
            ? new[] { ThemeMode.GreenLight, ThemeMode.RedLight, ThemeMode.BlueLight, ThemeMode.MonoLight, ThemeMode.Custom }
            : new[] { ThemeMode.RedLight, ThemeMode.GreenLight, ThemeMode.BlueLight, ThemeMode.MonoLight, ThemeMode.Custom };

        ThemeMode[] darkThemes = _serverMode == 1
            ? new[] { ThemeMode.GreenDark, ThemeMode.RedDark, ThemeMode.BlueDark, ThemeMode.MonoDark, ThemeMode.Custom }
            : new[] { ThemeMode.RedDark, ThemeMode.GreenDark, ThemeMode.BlueDark, ThemeMode.MonoDark, ThemeMode.Custom };

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

        ThemeMode next = themes[(index + 1) % themes.Length];
        if (next == ThemeMode.Custom)
        {
            _customIsLight = isLight;
        }
        return next;
    }

    public static void ApplyTheme(ThemeMode mode, bool? customIsLight = null, string? customColor = null)
    {
        if (customIsLight.HasValue) _customIsLight = customIsLight.Value;
        if (!string.IsNullOrWhiteSpace(customColor)) _customColor = customColor;

        _currentMode = mode;

        bool useLight = false;
        string colorFamily = "Green"; // Default to Green (HR)

        if (mode == ThemeMode.Custom)
        {
            useLight = _customIsLight;
            colorFamily = "Custom";
        }
        else if (mode == ThemeMode.System)
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

        if (colorFamily == "Custom")
        {
            System.Windows.Media.Color parsedColor;
            try
            {
                parsedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_customColor);
            }
            catch
            {
                parsedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(AppConfig.DefaultCustomThemeColor);
            }
            GenerateCustomTheme(newTheme, useLight, parsedColor);
        }
        else if (colorFamily == "Red")
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

        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => AppColors.ApplyTheme(useLight));
        }
        else
        {
            AppColors.ApplyTheme(useLight);
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

    public static void ColorToHsl(System.Windows.Media.Color color, out double h, out double s, out double l)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        l = (max + min) / 2.0;

        if (delta < 0.00001)
        {
            h = 0;
            s = 0;
        }
        else
        {
            s = l <= 0.5 ? (delta / (max + min)) : (delta / (2.0 - max - min));

            if (Math.Abs(r - max) < 0.00001)
                h = ((g - b) / delta) % 6.0;
            else if (Math.Abs(g - max) < 0.00001)
                h = 2.0 + (b - r) / delta;
            else
                h = 4.0 + (r - g) / delta;

            h *= 60.0;
            if (h < 0) h += 360.0;
        }
    }

    public static System.Windows.Media.Color HslToColor(double h, double s, double l)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        l = Math.Clamp(l, 0.0, 1.0);

        double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
        double m = l - c / 2.0;

        double r = 0, g = 0, b = 0;
        if (h < 60)       { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else              { r = c; g = 0; b = x; }

        byte red   = (byte)Math.Round(Math.Clamp(r + m, 0.0, 1.0) * 255);
        byte green = (byte)Math.Round(Math.Clamp(g + m, 0.0, 1.0) * 255);
        byte blue  = (byte)Math.Round(Math.Clamp(b + m, 0.0, 1.0) * 255);

        return System.Windows.Media.Color.FromRgb(red, green, blue);
    }

    private static void GenerateCustomTheme(ResourceDictionary newTheme, bool useLight, System.Windows.Media.Color baseColor)
    {
        ColorToHsl(baseColor, out double h, out double s, out double l);

        if (useLight)
        {
            // Light mode:
            // 1. AppPrimaryBrush: Needs sufficient contrast on white/light gray.
            double primaryL = Math.Clamp(l, 0.30, 0.48);
            var primaryColor = HslToColor(h, s, primaryL);
            var primaryHoverColor = HslToColor(h, s, Math.Max(0.20, primaryL * 0.80));
            var primaryPressedColor = HslToColor(h, s, Math.Max(0.14, primaryL * 0.65));
            var linkColor = primaryColor;

            // Header gradient: pastel tint (Stop 0) -> ultra-light soft tint (Stop 1)
            double headerSat = Math.Min(0.35, s * 0.45);
            var headerStop0 = HslToColor(h, headerSat, 0.76);
            var headerStop1 = HslToColor(h, Math.Min(0.25, s * 0.30), 0.96);

            newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                headerStop0, headerStop1, new Point(0, 0), new Point(1, 0));
            newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(primaryColor);
            newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush(primaryHoverColor);
            newTheme["AppPrimaryPressedBrush"] = new System.Windows.Media.SolidColorBrush(primaryPressedColor);
            newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush(linkColor);
        }
        else
        {
            // Dark mode:
            // 1. AppPrimaryBrush: Needs to stand out on dark panels (#1E1E1E / #252525).
            double primaryL = Math.Clamp(l, 0.50, 0.68);
            var primaryColor = HslToColor(h, s, primaryL);
            var primaryHoverColor = HslToColor(h, Math.Max(s * 0.85, 0.15), Math.Max(0.16, primaryL * 0.60));
            var primaryPressedColor = HslToColor(h, Math.Max(s * 0.80, 0.10), Math.Max(0.10, primaryL * 0.40));

            // Link color: bright, readable on dark
            double linkL = Math.Clamp(primaryL + 0.12, 0.65, 0.80);
            var linkColor = HslToColor(h, Math.Min(1.0, s * 0.90), linkL);

            // Header gradient: deep black tint (Stop 0) -> rich dark accent (Stop 1)
            double headerSat0 = Math.Min(0.40, s * 0.50);
            double headerSat1 = Math.Min(0.55, s * 0.75);
            var headerStop0 = HslToColor(h, headerSat0, 0.06);
            var headerStop1 = HslToColor(h, headerSat1, 0.24);

            newTheme["AppHeaderBrush"] = new System.Windows.Media.LinearGradientBrush(
                headerStop0, headerStop1, new Point(0, 0), new Point(1, 0));
            newTheme["AppPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(primaryColor);
            newTheme["AppPrimaryHoverBrush"] = new System.Windows.Media.SolidColorBrush(primaryHoverColor);
            newTheme["AppPrimaryPressedBrush"] = new System.Windows.Media.SolidColorBrush(primaryPressedColor);
            newTheme["AppLinkBrush"] = new System.Windows.Media.SolidColorBrush(linkColor);
            newTheme["AppSubtleBrush"] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BEBEBE"));
        }
    }
}
