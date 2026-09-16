using System.Windows;
using System.Windows.Media;

namespace ORTools.UI.Helpers;

/// <summary>
/// Centralized color palette for the UI.
/// Edit hex values here to restyle without hunting through ViewModels.
/// </summary>
public static class AppColors
{
    // ── Log level colors ──────────────────────────────────────────────────────
    public static readonly SolidColorBrush Info    = B("#87CEEB"); // LightSkyBlue / DeepSkyBlue
    public static readonly SolidColorBrush Warning = B("#FFA500"); // Orange / DarkAmber
    public static readonly SolidColorBrush Error   = B("#FF4444"); // Red / Crimson
    public static readonly SolidColorBrush Debug   = B("#9370DB"); // MediumPurple / Violet
    public static readonly SolidColorBrush Status  = B("#228B22"); // ForestGreen
    public static readonly SolidColorBrush Default = B("#E0E0E0"); // Light grey / Dark charcoal

    // ── Structural / punctuation ──────────────────────────────────────────────
    public static readonly SolidColorBrush Bracket   = B("#5A5A5A"); // Dim grey / Slate
    public static readonly SolidColorBrush Separator = B("#E0E0E0"); // Light grey / Dark grey

    // ── Status-line segment colors ────────────────────────────────────────────
    public static readonly SolidColorBrush StatusId      = B("#32CD32"); // LimeGreen / Emerald
    public static readonly SolidColorBrush StatusKnown   = B("#98FB98"); // PaleGreen / Teal
    public static readonly SolidColorBrush StatusUnknown = B("#FFFF00"); // Yellow / Amber

    // ── Status bar / indicator colors ─────────────────────────────────────────
    public static readonly SolidColorBrush Connected    = B("#228B22"); // ForestGreen — connection dot
    public static readonly SolidColorBrush Disconnected = B("#FF4444"); // Red         — connection dot
    public static readonly SolidColorBrush HpLow        = B("#FF4444"); // Red         — HP bar below 25%
    public static readonly SolidColorBrush HpNormal     = B("#228B22"); // ForestGreen — HP bar normal
    public static readonly SolidColorBrush SpLow        = B("#FFA500"); // Orange      — SP bar below 25%
    public static readonly SolidColorBrush SpNormal     = B("#87CEEB"); // LightSkyBlue — SP bar normal

    public static void ApplyTheme(bool isLight)
    {
        UpdateBrush(Info,          "AppDebugTextInfoBrush",          isLight ? "#0369A1" : "#87CEEB");
        UpdateBrush(Warning,       "AppDebugTextWarningBrush",       isLight ? "#B45309" : "#FFA500");
        UpdateBrush(Error,         "AppDebugTextErrorBrush",         isLight ? "#DC2626" : "#FF4444");
        UpdateBrush(Debug,         "AppDebugTextDebugBrush",         isLight ? "#6D28D9" : "#9370DB");
        UpdateBrush(Status,        "AppDebugTextStatusBrush",        isLight ? "#15803D" : "#228B22");
        UpdateBrush(Default,       "AppDebugTextDefaultBrush",       isLight ? "#111827" : "#E0E0E0");
        UpdateBrush(Bracket,       "AppDebugTextBracketBrush",       isLight ? "#4B5563" : "#5A5A5A");
        UpdateBrush(Separator,     "AppDebugTextSeparatorBrush",     isLight ? "#374151" : "#E0E0E0");
        UpdateBrush(StatusId,      "AppDebugTextStatusIdBrush",      isLight ? "#047857" : "#32CD32");
        UpdateBrush(StatusKnown,   "AppDebugTextStatusKnownBrush",   isLight ? "#0F766E" : "#98FB98");
        UpdateBrush(StatusUnknown, "AppDebugTextStatusUnknownBrush", isLight ? "#D97706" : "#FFFF00");
    }

    private static void UpdateBrush(SolidColorBrush brush, string resourceKey, string fallbackHex)
    {
        if (Application.Current != null && Application.Current.TryFindResource(resourceKey) is SolidColorBrush resBrush)
        {
            brush.Color = resBrush.Color;
        }
        else
        {
            brush.Color = (Color)ColorConverter.ConvertFromString(fallbackHex)!;
        }
    }

    private static SolidColorBrush B(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex)!);
}
