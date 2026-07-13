using System;
using System.Globalization;
using System.Windows.Data;
using ORTools.UI.Services;

namespace ORTools.UI.Helpers;

/// <summary>
/// Converts a <see cref="Language"/> enum value to its native display name.
/// Always returns the name in the language's own script so the selector is never broken
/// by the current active language.
/// </summary>
public class LanguageToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Language lang ? LanguageService.GetDisplayName(lang) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
