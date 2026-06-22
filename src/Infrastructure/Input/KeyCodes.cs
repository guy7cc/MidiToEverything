namespace MidiToEverything.Infrastructure.Input;

/// <summary>
/// Resolves key-name tokens (as used in <c>KeyAction.Keys</c>) to Windows virtual-key codes
/// and identifies modifiers. Pure and OS-call-free, so it is unit-tested without injecting
/// anything (docs/04_Roadmap.md M5).
/// </summary>
public static class KeyCodes
{
    /// <summary>Named (non-alphanumeric) keys → (virtual-key, isExtended).</summary>
    private static readonly Dictionary<string, (ushort Vk, bool Extended)> Named =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Modifiers
            ["ctrl"] = (0x11, false), ["control"] = (0x11, false),
            ["shift"] = (0x10, false),
            ["alt"] = (0x12, false), ["menu"] = (0x12, false),
            ["win"] = (0x5B, true), ["lwin"] = (0x5B, true), ["super"] = (0x5B, true),

            // Whitespace / editing
            ["space"] = (0x20, false), ["spacebar"] = (0x20, false),
            ["enter"] = (0x0D, false), ["return"] = (0x0D, false),
            ["tab"] = (0x09, false),
            ["esc"] = (0x1B, false), ["escape"] = (0x1B, false),
            ["backspace"] = (0x08, false), ["bksp"] = (0x08, false),
            ["delete"] = (0x2E, true), ["del"] = (0x2E, true),
            ["insert"] = (0x2D, true), ["ins"] = (0x2D, true),

            // Navigation (extended)
            ["home"] = (0x24, true), ["end"] = (0x23, true),
            ["pageup"] = (0x21, true), ["pgup"] = (0x21, true),
            ["pagedown"] = (0x22, true), ["pgdn"] = (0x22, true),
            ["up"] = (0x26, true), ["down"] = (0x28, true),
            ["left"] = (0x25, true), ["right"] = (0x27, true),

            // Misc
            ["capslock"] = (0x14, false),
            ["printscreen"] = (0x2C, true), ["pause"] = (0x13, false),

            // Function keys
            ["f1"] = (0x70, false), ["f2"] = (0x71, false), ["f3"] = (0x72, false),
            ["f4"] = (0x73, false), ["f5"] = (0x74, false), ["f6"] = (0x75, false),
            ["f7"] = (0x76, false), ["f8"] = (0x77, false), ["f9"] = (0x78, false),
            ["f10"] = (0x79, false), ["f11"] = (0x7A, false), ["f12"] = (0x7B, false),
        };

    private static readonly HashSet<string> Modifiers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ctrl", "control", "shift", "alt", "menu", "win", "lwin", "super",
        };

    /// <summary>True when the token denotes a modifier key (ctrl/shift/alt/win).</summary>
    public static bool IsModifier(string token) => Modifiers.Contains(token.Trim());

    /// <summary>
    /// Resolves a token to its virtual-key code and whether it is an extended key.
    /// Handles a–z and 0–9 directly; everything else via the named table.
    /// </summary>
    public static bool TryResolve(string token, out ushort virtualKey, out bool extended)
    {
        var key = token.Trim();
        if (key.Length == 1)
        {
            var c = char.ToLowerInvariant(key[0]);
            if (c is >= 'a' and <= 'z')
            {
                virtualKey = (ushort)(c - 'a' + 0x41);
                extended = false;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                virtualKey = (ushort)c;
                extended = false;
                return true;
            }
        }

        if (Named.TryGetValue(key, out var entry))
        {
            (virtualKey, extended) = entry;
            return true;
        }

        virtualKey = 0;
        extended = false;
        return false;
    }
}
