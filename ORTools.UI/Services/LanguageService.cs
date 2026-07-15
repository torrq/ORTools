using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace ORTools.UI.Services;

public enum Language { English, Filipino }

/// <summary>
/// Manages runtime language switching by swapping the active string ResourceDictionary.
/// Mirrors the ThemeService pattern exactly. Language is UI-only — the Worker never needs it.
/// </summary>
public static class LanguageService
{
    private static Language _current = Language.English;

    public static Language Current => _current;

    /// <summary>Fired on the UI thread after the active string dictionary has been swapped.</summary>
    public static event Action? LanguageChanged;

    private static string LangFile => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Config", "language.json");

    /// <summary>
    /// Reads the saved language from disk and applies it.
    /// Call this from App.OnStartup, immediately after ThemeService.Initialize().
    /// </summary>
    public static void Initialize()
    {
        try
        {
            if (File.Exists(LangFile))
            {
                string json = File.ReadAllText(LangFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Language", out var prop) &&
                    prop.TryGetInt32(out int val) &&
                    Enum.IsDefined(typeof(Language), val))
                {
                    _current = (Language)val;
                }
            }
        }
        catch { }

        // Apply without saving (we just loaded from disk)
        Apply(_current, save: false);
    }

    /// <summary>
    /// Swaps the active string dictionary and persists the choice.
    /// Safe to call from the UI thread at any time.
    /// </summary>
    public static void Apply(Language lang, bool save = true)
    {
        _current = lang;

        string uri = lang switch
        {
            Language.Filipino => "pack://application:,,,/ORTools;component/Strings/tl.xaml",
            _                 => "pack://application:,,,/ORTools;component/Strings/en.xaml"
        };

        var dicts   = Application.Current.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Strings/") == true);

        var newDict = new ResourceDictionary { Source = new Uri(uri) };

        if (existing != null)
            dicts.Remove(existing);

        dicts.Add(newDict);

        if (save) Save();

        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Looks up a localized string from the current application resources.
    /// Falls back to the key name if the resource is missing.
    /// </summary>
    public static string Get(string key)
        => Application.Current?.Resources[key] as string ?? key;

    /// <summary>
    /// Always returns the language name in its own native script,
    /// regardless of which language is currently active.
    /// </summary>
    public static string GetDisplayName(Language lang) => lang switch
    {
        Language.Filipino => "Filipino",
        _                 => "English"
    };

    private static void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(LangFile);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(LangFile, JsonSerializer.Serialize(new { Language = (int)_current }));
        }
        catch { }
    }
}
