using System.Windows.Media;

namespace ORTools.UI.Helpers;

/// <summary>
/// Centralized color palette for the UI.
/// Edit hex values here to restyle without hunting through ViewModels.
/// </summary>
public static class AppColors
{
    // ── Log level colors ──────────────────────────────────────────────────────
    public static readonly SolidColorBrush Info    = B("#87CEEB"); // LightSkyBlue
    public static readonly SolidColorBrush Warning = B("#FFA500"); // Orange
    public static readonly SolidColorBrush Error   = B("#FF4444"); // Red
    public static readonly SolidColorBrush Debug   = B("#9370DB"); // MediumPurple
    public static readonly SolidColorBrush Status  = B("#228B22"); // ForestGreen
    public static readonly SolidColorBrush Default = B("#E0E0E0"); // Light grey

    // ── Structural / punctuation ──────────────────────────────────────────────
    public static readonly SolidColorBrush Bracket   = B("#5A5A5A"); // Dim grey (brackets, timestamps)
    public static readonly SolidColorBrush Separator = B("#E0E0E0"); // Light grey (colons, spaces)

    // ── Status-line segment colors ────────────────────────────────────────────
    public static readonly SolidColorBrush StatusId      = B("#32CD32"); // LimeGreen  — effect ID
    public static readonly SolidColorBrush StatusKnown   = B("#98FB98"); // PaleGreen  — known status value
    public static readonly SolidColorBrush StatusUnknown = B("#FFFF00"); // Yellow     — **UNKNOWN**

    // ── Status bar / indicator colors ─────────────────────────────────────────
    public static readonly SolidColorBrush Connected    = B("#228B22"); // ForestGreen — connection dot
    public static readonly SolidColorBrush Disconnected = B("#FF4444"); // Red         — connection dot
    public static readonly SolidColorBrush HpLow        = B("#FF4444"); // Red         — HP bar below 25%
    public static readonly SolidColorBrush HpNormal     = B("#228B22"); // ForestGreen — HP bar normal
    public static readonly SolidColorBrush SpLow        = B("#FFA500"); // Orange      — SP bar below 25%
    public static readonly SolidColorBrush SpNormal     = B("#87CEEB"); // LightSkyBlue — SP bar normal

    private static SolidColorBrush B(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex)!);
}
