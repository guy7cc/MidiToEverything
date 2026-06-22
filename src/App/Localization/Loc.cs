using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MidiToEverything.App.Localization;

/// <summary>A selectable UI language (code + native display name).</summary>
public sealed record LanguageOption(string Code, string Display);

/// <summary>
/// Runtime-switchable localization (docs/07). Each language is a separate translation file shipped
/// next to the exe — <c>Resources/Localization/strings.&lt;code&gt;.json</c> mapping translation key →
/// text; the app looks a key up in the currently selected language's dictionary. Adding a language is
/// just dropping in a file. Changing the language raises an indexer change so all <c>{loc:Tr}</c>
/// bindings refresh live; missing keys fall back to English, then the key itself.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private const string DefaultLanguage = "ja";
    private const string FallbackLanguage = "en";

    public static Loc Instance { get; } = new();

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _byLanguage;
    private IReadOnlyDictionary<string, string> _current;

    private Loc()
    {
        _byLanguage = LoadAll();
        _current = Dict(DefaultLanguage);
        Language = _byLanguage.ContainsKey(DefaultLanguage) ? DefaultLanguage : _byLanguage.Keys.FirstOrDefault() ?? DefaultLanguage;

        Languages = _byLanguage
            .Select(kv => new LanguageOption(kv.Key, kv.Value.GetValueOrDefault("language.name", kv.Key)))
            .OrderBy(l => l.Code == DefaultLanguage ? 0 : l.Code == FallbackLanguage ? 1 : 2)
            .ThenBy(l => l.Code)
            .ToList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changes (for view models to re-raise computed labels).</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Current language code (e.g. "ja" / "en").</summary>
    public string Language { get; private set; }

    /// <summary>Languages discovered from the translation files (code + native name).</summary>
    public IReadOnlyList<LanguageOption> Languages { get; }

    public string this[string key] =>
        _current.TryGetValue(key, out var v) ? v
        : Dict(FallbackLanguage).TryGetValue(key, out var e) ? e
        : key;

    public void SetLanguage(string language)
    {
        var lang = _byLanguage.ContainsKey(language) ? language : DefaultLanguage;
        if (lang == Language && ReferenceEquals(_current, Dict(lang)))
        {
            return;
        }

        _current = Dict(lang);
        Language = lang;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Convenience for code paths (view models, tray) that need a string now.</summary>
    public static string T(string key) => Instance[key];

    private IReadOnlyDictionary<string, string> Dict(string code) =>
        _byLanguage.TryGetValue(code, out var d) ? d : EmptyDict;

    private static readonly IReadOnlyDictionary<string, string> EmptyDict = new Dictionary<string, string>();

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadAll()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(AppContext.BaseDirectory, "Resources", "Localization");
        if (!Directory.Exists(dir))
        {
            return result;
        }

        var pattern = new Regex(@"^strings\.([A-Za-z-]+)\.json$", RegexOptions.IgnoreCase);
        foreach (var file in Directory.GetFiles(dir, "strings.*.json"))
        {
            var match = pattern.Match(Path.GetFileName(file));
            if (!match.Success)
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(file);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                           ?? new Dictionary<string, string>();
                result[match.Groups[1].Value] = dict;
            }
            catch
            {
                // skip a malformed/locked translation file rather than crashing startup
            }
        }

        return result;
    }
}
