using System.ComponentModel;

namespace MidiToEverything.App.Localization;

/// <summary>
/// Runtime-switchable localization (docs/07). A singleton whose indexer returns the string for the
/// current language; changing the language raises an indexer change so all <c>{loc:Tr}</c> bindings
/// refresh live. Strings live in <see cref="Strings"/> (ja/en); missing keys fall back to en, then
/// the key itself.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private IReadOnlyDictionary<string, string> _current = Strings.Ja;

    private Loc()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Current language code ("ja" / "en").</summary>
    public string Language { get; private set; } = "ja";

    public string this[string key] =>
        _current.TryGetValue(key, out var v) ? v
        : Strings.En.TryGetValue(key, out var e) ? e
        : key;

    public void SetLanguage(string language)
    {
        var lang = language == "en" ? "en" : "ja";
        if (lang == Language)
        {
            return;
        }

        _current = lang == "en" ? Strings.En : Strings.Ja;
        Language = lang;
        // Empty/"Item[]" refreshes every indexer binding; also notify Language watchers.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised after the language changes (for view models to re-raise computed labels).</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Convenience for code paths (view models, tray) that need a string now.</summary>
    public static string T(string key) => Instance[key];
}
