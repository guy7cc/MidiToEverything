using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Tests.Domain;

public class MatchRuleTests
{
    [Fact]
    public void ProcessClause_MatchesProcessLine_AnyTitle()
    {
        var rule = new MatchRule { Pattern = @"^chrome\.exe$" };

        Assert.True(rule.Matches("chrome.exe", "anything at all"));
        Assert.False(rule.Matches("notchrome.exe", "anything"));
    }

    [Fact]
    public void TitleClause_MatchesTitleLine()
    {
        var rule = new MatchRule { Pattern = "Google Chrome$" };

        Assert.True(rule.Matches("chrome.exe", "Some Page - Google Chrome"));
        Assert.False(rule.Matches("chrome.exe", "Some Page - Mozilla Firefox"));
    }

    [Fact]
    public void OrPattern_MatchesEitherProcessOrTitle()
    {
        var rule = new MatchRule { Pattern = @"(?:^obs64\.exe$)|(?:OBS Studio$)" };

        Assert.True(rule.Matches("obs64.exe", "no title"));     // process line
        Assert.True(rule.Matches("other.exe", "x - OBS Studio")); // title line
        Assert.False(rule.Matches("other.exe", "unrelated"));
    }

    [Fact]
    public void EmptyPattern_NeverMatches()
    {
        Assert.False(new MatchRule { Pattern = "" }.Matches("anything.exe", "title"));
    }

    [Fact]
    public void InvalidPattern_NeverMatches_AndDoesNotThrow()
    {
        var rule = new MatchRule { Pattern = "(" }; // unbalanced — invalid regex

        var ex = Record.Exception(() => rule.Matches("a.exe", "t"));
        Assert.Null(ex);
        Assert.False(rule.Matches("a.exe", "t"));
    }
}
