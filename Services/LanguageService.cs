using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;

namespace WinZ.Services;

public static class LanguageService
{
    private static string _currentLanguage = "en-US";
    public static string CurrentLanguage => _currentLanguage;
    public static event Action? LanguageChanged;

    private static readonly Dictionary<string, string> _languages = new()
    {
        { "en-US", "English" },
        { "de-DE", "Deutsch" },
        { "ru-RU", "Русский" },
        { "cs-CZ", "Čeština" }
    };

    public static IReadOnlyDictionary<string, string> AvailableLanguages => _languages;

    public static void Initialize()
    {
        try
        {
            SetLanguage("en-US");
        }
        catch (Exception)
        {
             // MessageBox in App.xaml.cs will catch this
             throw;
        }
    }

    public static void SetLanguage(string code, bool save = false)
    {
        if (!_languages.ContainsKey(code)) return;
        _currentLanguage = code;

        try
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"/Themes/Languages/{code}.xaml", UriKind.Relative)
            };

            var appDicts = Application.Current.Resources.MergedDictionaries;
            var existing = appDicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Themes/Languages/"));
            
            if (existing != null)
                appDicts.Remove(existing);

            appDicts.Add(dict);
            
            // Sync WPF UI metadata with the selected culture
            var lang = System.Windows.Markup.XmlLanguage.GetLanguage(code);
            foreach (Window window in Application.Current.Windows)
                window.Language = lang;

            LanguageChanged?.Invoke();

            if (save)
            {
                _ = DataService.Instance.SaveSettingAsync("Language", code);
            }
        }
        catch (Exception)
        {
             throw;
        }
    }
}
