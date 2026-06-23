namespace MidiToEverything.Infrastructure.Input;

/// <summary>
/// Resolves key-name tokens (as used in <c>KeyAction.Keys</c>) to Windows virtual-key codes
/// and identifies modifiers. Pure and OS-call-free, so it is unit-tested without injecting
/// anything (docs/04_Roadmap.md M5).
/// </summary>
public static class KeyCodes
{
    /// <summary>
    /// Named keys → (virtual-key, isExtended). Aims to cover every key on a standard 104/105-key
    /// keyboard. Single-character symbol tokens (e.g. <c>-</c>, <c>[</c>) are resolved here too;
    /// word aliases (e.g. <c>minus</c>, <c>comma</c>) exist for readability and for keys whose
    /// symbol is a chord separator (<c>,</c> and space can't be tokens, so use <c>comma</c>).
    /// OEM/symbol keys send the physical key by scancode, so the produced glyph follows the
    /// target's keyboard layout.
    /// </summary>
    private static readonly Dictionary<string, (ushort Vk, bool Extended)> Named =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── Modifiers ──
            ["ctrl"] = (0x11, false), ["control"] = (0x11, false),
            ["shift"] = (0x10, false),
            ["alt"] = (0x12, false), ["menu"] = (0x12, false),
            ["win"] = (0x5B, true), ["lwin"] = (0x5B, true), ["super"] = (0x5B, true),
            ["rwin"] = (0x5C, true),
            ["lctrl"] = (0xA2, false), ["lcontrol"] = (0xA2, false),
            ["rctrl"] = (0xA3, true), ["rcontrol"] = (0xA3, true),
            ["lshift"] = (0xA0, false), ["rshift"] = (0xA1, false),
            ["lalt"] = (0xA4, false), ["ralt"] = (0xA5, true), ["altgr"] = (0xA5, true),
            ["apps"] = (0x5D, true), ["contextmenu"] = (0x5D, true), ["menukey"] = (0x5D, true),

            // ── Whitespace / editing ──
            ["space"] = (0x20, false), ["spacebar"] = (0x20, false),
            ["enter"] = (0x0D, false), ["return"] = (0x0D, false),
            ["tab"] = (0x09, false),
            ["esc"] = (0x1B, false), ["escape"] = (0x1B, false),
            ["backspace"] = (0x08, false), ["bksp"] = (0x08, false),
            ["delete"] = (0x2E, true), ["del"] = (0x2E, true),
            ["insert"] = (0x2D, true), ["ins"] = (0x2D, true),

            // ── Navigation (extended) ──
            ["home"] = (0x24, true), ["end"] = (0x23, true),
            ["pageup"] = (0x21, true), ["pgup"] = (0x21, true),
            ["pagedown"] = (0x22, true), ["pgdn"] = (0x22, true),
            ["up"] = (0x26, true), ["down"] = (0x28, true),
            ["left"] = (0x25, true), ["right"] = (0x27, true),

            // ── Locks / system ──
            ["capslock"] = (0x14, false), ["caps"] = (0x14, false),
            ["numlock"] = (0x90, false),
            ["scrolllock"] = (0x91, false), ["scrlk"] = (0x91, false),
            ["printscreen"] = (0x2C, true), ["prtsc"] = (0x2C, true), ["sysrq"] = (0x2C, true),
            ["pause"] = (0x13, false), ["break"] = (0x13, false),

            // ── OEM punctuation (US layout) ──
            ["-"] = (0xBD, false), ["minus"] = (0xBD, false),
            ["="] = (0xBB, false), ["equals"] = (0xBB, false), ["equal"] = (0xBB, false), ["plus"] = (0xBB, false),
            ["["] = (0xDB, false), ["lbracket"] = (0xDB, false), ["leftbracket"] = (0xDB, false), ["openbracket"] = (0xDB, false),
            ["]"] = (0xDD, false), ["rbracket"] = (0xDD, false), ["rightbracket"] = (0xDD, false), ["closebracket"] = (0xDD, false),
            ["\\"] = (0xDC, false), ["backslash"] = (0xDC, false),
            [";"] = (0xBA, false), ["semicolon"] = (0xBA, false),
            ["'"] = (0xDE, false), ["quote"] = (0xDE, false), ["apostrophe"] = (0xDE, false),
            ["comma"] = (0xBC, false), // the "," key (a chord separator, so only this alias is usable)
            ["."] = (0xBE, false), ["period"] = (0xBE, false), ["dot"] = (0xBE, false),
            ["/"] = (0xBF, false), ["slash"] = (0xBF, false), ["forwardslash"] = (0xBF, false),
            ["`"] = (0xC0, false), ["backtick"] = (0xC0, false), ["grave"] = (0xC0, false), ["tilde"] = (0xC0, false),

            // ── Numeric keypad (digits/decimal need NumLock on) ──
            ["numpad0"] = (0x60, false), ["num0"] = (0x60, false),
            ["numpad1"] = (0x61, false), ["num1"] = (0x61, false),
            ["numpad2"] = (0x62, false), ["num2"] = (0x62, false),
            ["numpad3"] = (0x63, false), ["num3"] = (0x63, false),
            ["numpad4"] = (0x64, false), ["num4"] = (0x64, false),
            ["numpad5"] = (0x65, false), ["num5"] = (0x65, false),
            ["numpad6"] = (0x66, false), ["num6"] = (0x66, false),
            ["numpad7"] = (0x67, false), ["num7"] = (0x67, false),
            ["numpad8"] = (0x68, false), ["num8"] = (0x68, false),
            ["numpad9"] = (0x69, false), ["num9"] = (0x69, false),
            ["multiply"] = (0x6A, false), ["numpadmultiply"] = (0x6A, false),
            ["add"] = (0x6B, false), ["numpadadd"] = (0x6B, false),
            ["subtract"] = (0x6D, false), ["numpadsubtract"] = (0x6D, false),
            ["decimal"] = (0x6E, false), ["numpaddecimal"] = (0x6E, false),
            ["divide"] = (0x6F, true), ["numpaddivide"] = (0x6F, true),
            ["numpadenter"] = (0x0D, true), ["numenter"] = (0x0D, true),

            // ── Function keys (F13–F24 are off standard keyboards but harmless to support) ──
            ["f1"] = (0x70, false), ["f2"] = (0x71, false), ["f3"] = (0x72, false),
            ["f4"] = (0x73, false), ["f5"] = (0x74, false), ["f6"] = (0x75, false),
            ["f7"] = (0x76, false), ["f8"] = (0x77, false), ["f9"] = (0x78, false),
            ["f10"] = (0x79, false), ["f11"] = (0x7A, false), ["f12"] = (0x7B, false),
            ["f13"] = (0x7C, false), ["f14"] = (0x7D, false), ["f15"] = (0x7E, false),
            ["f16"] = (0x7F, false), ["f17"] = (0x80, false), ["f18"] = (0x81, false),
            ["f19"] = (0x82, false), ["f20"] = (0x83, false), ["f21"] = (0x84, false),
            ["f22"] = (0x85, false), ["f23"] = (0x86, false), ["f24"] = (0x87, false),
        };

    private static readonly HashSet<string> Modifiers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ctrl", "control", "shift", "alt", "menu", "win", "lwin", "super", "rwin",
            "lctrl", "lcontrol", "rctrl", "rcontrol", "lshift", "rshift", "lalt", "ralt", "altgr",
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
