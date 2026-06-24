namespace MidiToEverything.Infrastructure.Input;

/// <summary>
/// Parses a hotkey string (e.g. <c>"ctrl+alt+pause"</c>) into the Win32 <c>RegisterHotKey</c>
/// modifier mask and virtual-key. Reuses <see cref="KeyCodes"/> for the main key. Pure, so it is
/// unit-tested without any OS calls.
/// </summary>
public static class HotkeyParser
{
    // RegisterHotKey fsModifiers flags.
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    private static readonly char[] Separators = { '+', ' ', ',' };

    /// <summary>
    /// Parse <paramref name="spec"/> into a modifier mask and virtual key. Returns false for an
    /// empty spec, an unknown token, or a spec with no non-modifier key (a bare modifier can't be a
    /// hotkey).
    /// </summary>
    public static bool TryParse(string? spec, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(spec))
        {
            return false;
        }

        var hasMainKey = false;
        foreach (var token in spec.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= ModControl;
                    break;
                case "alt" or "menu":
                    modifiers |= ModAlt;
                    break;
                case "shift":
                    modifiers |= ModShift;
                    break;
                case "win" or "super" or "lwin":
                    modifiers |= ModWin;
                    break;
                default:
                    if (hasMainKey || !KeyCodes.TryResolve(token, out var vk, out _))
                    {
                        return false; // unknown token, or a second main key
                    }

                    virtualKey = vk;
                    hasMainKey = true;
                    break;
            }
        }

        return hasMainKey;
    }
}
