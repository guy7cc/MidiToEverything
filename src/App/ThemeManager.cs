using System.Linq;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Application = System.Windows.Application;

namespace MidiToEverything.App;

/// <summary>
/// Applies the dark/light palette and accent preset at runtime by mutating the <c>Pal.*</c> color
/// values in the merged Theme dictionary. Brushes in Theme.xaml bind their colors via DynamicResource,
/// so every <c>StaticResource</c> brush reference updates in place — no window reload needed.
/// </summary>
internal static class ThemeManager
{
    public static readonly IReadOnlyList<string> Themes = new[] { "dark", "light" };
    public static readonly IReadOnlyList<string> Accents = new[] { "blue", "green", "purple", "orange" };

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    // The current (dark) defaults, restated so we can switch back from light at runtime.
    private static readonly Dictionary<string, Color> Dark = new()
    {
        ["Pal.Bg"] = C("#FF1E1E22"),
        ["Pal.Caption"] = C("#FF161619"),
        ["Pal.Surface"] = C("#FF26262C"),
        ["Pal.SurfaceAlt"] = C("#FF2E2E36"),
        ["Pal.InputBg"] = C("#FF1B1B20"),
        ["Pal.Border"] = C("#FF3A3A42"),
        ["Pal.Text"] = C("#FFE6E6E6"),
        ["Pal.TextDim"] = C("#FF9A9AA4"),
        ["Pal.TextFaint"] = C("#FF74747F"),
        ["Pal.AccentText"] = C("#FF9FC8FF"),
        ["Pal.OnAccent"] = C("#FF0B1B2E"),
        ["Pal.ToggleOn"] = C("#FF20415F"),
        ["Pal.SelectedItem"] = C("#FF2A3A50"),
        ["Pal.Ok"] = C("#FF5BD68A"),
        ["Pal.Danger"] = C("#FFE06666"),
        ["Pal.Warn"] = C("#FFF0C674"),
        ["Pal.Hover"] = C("#FF3E3E48"),
        ["Pal.Pressed"] = C("#FF26262E"),
        ["Pal.ScrollThumb"] = C("#FF44444F"),
        ["Pal.ScrollThumbHover"] = C("#FF5A5A66"),
        ["Pal.CardTop"] = C("#FF3A3A45"),
        ["Pal.CardBottom"] = C("#FF2B2B33"),
        ["Pal.HeaderStart"] = C("#FF32323C"),
        ["Pal.HeaderEnd"] = C("#FF202026"),
    };

    private static readonly Dictionary<string, Color> Light = new()
    {
        ["Pal.Bg"] = C("#FFF4F4F7"),
        ["Pal.Caption"] = C("#FFE6E6EC"),
        ["Pal.Surface"] = C("#FFFFFFFF"),
        ["Pal.SurfaceAlt"] = C("#FFEAEAF0"),
        ["Pal.InputBg"] = C("#FFFFFFFF"),
        ["Pal.Border"] = C("#FFCBCBD4"),
        ["Pal.Text"] = C("#FF1E1E22"),
        ["Pal.TextDim"] = C("#FF55555F"),
        ["Pal.TextFaint"] = C("#FF8A8A95"),
        ["Pal.AccentText"] = C("#FF1A5FB0"),
        ["Pal.OnAccent"] = C("#FFFFFFFF"),
        ["Pal.ToggleOn"] = C("#FFD7E7FA"),
        ["Pal.SelectedItem"] = C("#FFD8E4F5"),
        ["Pal.Ok"] = C("#FF2E9E5B"),
        ["Pal.Danger"] = C("#FFCC4444"),
        ["Pal.Warn"] = C("#FFC9931F"),
        ["Pal.Hover"] = C("#FFE3E3EA"),
        ["Pal.Pressed"] = C("#FFD2D2DC"),
        ["Pal.ScrollThumb"] = C("#FFC2C2CC"),
        ["Pal.ScrollThumbHover"] = C("#FFA6A6B2"),
        ["Pal.CardTop"] = C("#FFFFFFFF"),
        ["Pal.CardBottom"] = C("#FFF0F0F4"),
        ["Pal.HeaderStart"] = C("#FFECECF2"),
        ["Pal.HeaderEnd"] = C("#FFDEDEE6"),
    };

    // Accent overrides only the hue (Pal.Accent); accent-text/on-accent stay theme-readable.
    private static readonly Dictionary<string, Color> AccentColors = new()
    {
        ["blue"] = C("#FF4FA3FF"),
        ["green"] = C("#FF5BD68A"),
        ["purple"] = C("#FFB58BFF"),
        ["orange"] = C("#FFF0A050"),
    };

    public static void Apply(string? theme, string? accent)
    {
        var dict = ThemeDictionary();
        if (dict is null)
        {
            return;
        }

        var palette = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase) ? Light : Dark;
        foreach (var kv in palette)
        {
            dict[kv.Key] = kv.Value;
        }

        var key = accent is not null && AccentColors.ContainsKey(accent) ? accent : "blue";
        dict["Pal.Accent"] = AccentColors[key];
    }

    private static ResourceDictionary? ThemeDictionary()
        => Application.Current?.Resources.MergedDictionaries.FirstOrDefault(d => d.Contains("Pal.Bg"));
}
