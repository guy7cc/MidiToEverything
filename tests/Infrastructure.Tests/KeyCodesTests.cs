using MidiToEverything.Infrastructure.Input;

namespace MidiToEverything.Infrastructure.Tests;

public class KeyCodesTests
{
    [Theory]
    [InlineData("a", 0x41)]
    [InlineData("z", 0x5A)]
    [InlineData("A", 0x41)] // case-insensitive
    [InlineData("0", 0x30)]
    [InlineData("9", 0x39)]
    public void Resolves_LettersAndDigits(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out var extended));
        Assert.Equal(expectedVk, vk);
        Assert.False(extended);
    }

    [Theory]
    [InlineData("ctrl", 0x11)]
    [InlineData("shift", 0x10)]
    [InlineData("alt", 0x12)]
    [InlineData("space", 0x20)]
    [InlineData("enter", 0x0D)]
    [InlineData("f5", 0x74)]
    [InlineData("f24", 0x87)]
    public void Resolves_NamedKeys(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out _));
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    // symbol and its word alias map to the same OEM virtual-key
    [InlineData("-", 0xBD)]
    [InlineData("minus", 0xBD)]
    [InlineData("=", 0xBB)]
    [InlineData("equals", 0xBB)]
    [InlineData("plus", 0xBB)]      // the "=/+" physical key
    [InlineData("[", 0xDB)]
    [InlineData("]", 0xDD)]
    [InlineData("\\", 0xDC)]
    [InlineData(";", 0xBA)]
    [InlineData("'", 0xDE)]
    [InlineData("comma", 0xBC)]     // the "," key — alias only ("," is a chord separator)
    [InlineData(".", 0xBE)]
    [InlineData("/", 0xBF)]
    [InlineData("`", 0xC0)]
    public void Resolves_OemPunctuation(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out var extended));
        Assert.Equal(expectedVk, vk);
        Assert.False(extended);
    }

    [Theory]
    [InlineData("numpad0", 0x60)]
    [InlineData("num9", 0x69)]
    [InlineData("add", 0x6B)]
    [InlineData("subtract", 0x6D)]
    [InlineData("multiply", 0x6A)]
    [InlineData("decimal", 0x6E)]
    public void Resolves_NumpadKeys(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out var extended));
        Assert.Equal(expectedVk, vk);
        Assert.False(extended);
    }

    [Theory]
    [InlineData("divide", 0x6F)]    // numpad "/" is an extended key
    [InlineData("numpadenter", 0x0D)]
    [InlineData("rctrl", 0xA3)]
    [InlineData("ralt", 0xA5)]
    [InlineData("rwin", 0x5C)]
    [InlineData("apps", 0x5D)]
    public void ExtendedKeys_AreFlaggedExtended(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out var extended));
        Assert.Equal(expectedVk, vk);
        Assert.True(extended);
    }

    [Theory]
    [InlineData("numlock", 0x90)]
    [InlineData("scrolllock", 0x91)]
    [InlineData("lshift", 0xA0)]    // left shift is not an extended key
    [InlineData("lctrl", 0xA2)]
    public void Resolves_LocksAndLeftModifiers_NotExtended(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out var extended));
        Assert.Equal(expectedVk, vk);
        Assert.False(extended);
    }

    [Theory]
    [InlineData("left", 0x25)]
    [InlineData("up", 0x26)]
    [InlineData("delete", 0x2E)]
    [InlineData("home", 0x24)]
    public void NavigationKeys_AreExtended(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out var extended));
        Assert.Equal(expectedVk, vk);
        Assert.True(extended);
    }

    [Theory]
    [InlineData("ctrl")]
    [InlineData("Control")]
    [InlineData("shift")]
    [InlineData("alt")]
    [InlineData("win")]
    [InlineData("rctrl")]
    [InlineData("lalt")]
    [InlineData("altgr")]
    public void IsModifier_True_ForModifiers(string token) => Assert.True(KeyCodes.IsModifier(token));

    [Theory]
    [InlineData("a")]
    [InlineData("space")]
    [InlineData("f1")]
    [InlineData("-")]
    [InlineData("numpad1")]
    [InlineData("apps")] // the context-menu key is a normal key, not a modifier
    public void IsModifier_False_ForNonModifiers(string token) => Assert.False(KeyCodes.IsModifier(token));

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("ctrl+z")] // a chord string is not a single token
    public void Unknown_TokensFail(string token) => Assert.False(KeyCodes.TryResolve(token, out _, out _));

    [Theory]
    [InlineData("ctrl", true)]   // resolves to a virtual key
    [InlineData("-", true)]      // resolves via the OEM table
    [InlineData("^", true)]      // unmapped single char → sent as a literal Unicode character
    [InlineData("~", true)]
    [InlineData("@", true)]
    [InlineData("あ", true)]     // any single character is sendable
    [InlineData("nope", false)]  // a multi-char typo is not sendable
    [InlineData("", false)]
    public void IsSendable_AllowsKnownKeysAndSingleChars(string token, bool sendable)
        => Assert.Equal(sendable, KeyCodes.IsSendable(token));
}
