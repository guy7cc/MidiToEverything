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
    public void Resolves_NamedKeys(string token, int expectedVk)
    {
        Assert.True(KeyCodes.TryResolve(token, out var vk, out _));
        Assert.Equal(expectedVk, vk);
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
    public void IsModifier_True_ForModifiers(string token) => Assert.True(KeyCodes.IsModifier(token));

    [Theory]
    [InlineData("a")]
    [InlineData("space")]
    [InlineData("f1")]
    public void IsModifier_False_ForNonModifiers(string token) => Assert.False(KeyCodes.IsModifier(token));

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("ctrl+z")] // a chord string is not a single token
    public void Unknown_TokensFail(string token) => Assert.False(KeyCodes.TryResolve(token, out _, out _));
}
