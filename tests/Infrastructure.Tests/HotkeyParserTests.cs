using MidiToEverything.Infrastructure.Input;

namespace MidiToEverything.Infrastructure.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void Parses_CtrlAltPause()
    {
        Assert.True(HotkeyParser.TryParse("ctrl+alt+pause", out var mods, out var vk));
        Assert.Equal(HotkeyParser.ModControl | HotkeyParser.ModAlt, mods);
        Assert.Equal(0x13u, vk); // VK_PAUSE
    }

    [Theory]
    [InlineData("ctrl+shift+f5", 0x74)]      // VK_F5
    [InlineData("win+s", 0x53)]              // 's'
    [InlineData("alt+1", 0x31)]              // '1'
    public void Parses_VariousCombos(string spec, int expectedVk)
    {
        Assert.True(HotkeyParser.TryParse(spec, out _, out var vk));
        Assert.Equal((uint)expectedVk, vk);
    }

    [Fact]
    public void Modifiers_MapToFlags()
    {
        Assert.True(HotkeyParser.TryParse("ctrl+alt+shift+win+a", out var mods, out _));
        Assert.Equal(HotkeyParser.ModControl | HotkeyParser.ModAlt | HotkeyParser.ModShift | HotkeyParser.ModWin, mods);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ctrl+alt")]      // no main key
    [InlineData("ctrl+nope")]     // unknown key
    [InlineData("a+b")]           // two main keys
    [InlineData(null)]
    public void Rejects_InvalidSpecs(string? spec)
        => Assert.False(HotkeyParser.TryParse(spec, out _, out _));
}
