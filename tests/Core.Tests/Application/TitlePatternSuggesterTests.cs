using System.Text.RegularExpressions;
using MidiToEverything.Core.Application;

namespace MidiToEverything.Core.Tests.Application;

public class TitlePatternSuggesterTests
{
    [Fact]
    public void Browser_MatchesAnyFutureTabOfSameBrowser()
    {
        var pattern = TitlePatternSuggester.Suggest("動画 - YouTube - Google Chrome");

        Assert.Matches(pattern, "まったく別のページ - Google Chrome");
        Assert.DoesNotMatch(pattern, "別ページ - Mozilla Firefox");
    }

    [Fact]
    public void Notepad_JapaneseAppName_Matches()
    {
        var pattern = TitlePatternSuggester.Suggest("*無題 - メモ帳");

        Assert.Matches(pattern, "別の文書 - メモ帳");
        Assert.DoesNotMatch(pattern, "別の文書 - ワードパッド");
    }

    [Fact]
    public void Versioned_AppToleratesVersionDrift()
    {
        var pattern = TitlePatternSuggester.Suggest("note - Sample Vault - Obsidian 1.12.7");

        Assert.Matches(pattern, "他のノート - Vault - Obsidian 1.13.0"); // version changed
        Assert.Matches(pattern, "x - Obsidian"); // version absent
    }

    [Fact]
    public void NoSeparator_WholeTitleIsAppName()
    {
        var pattern = TitlePatternSuggester.Suggest("Discord");

        Assert.Matches(pattern, "Discord");
        Assert.Matches(pattern, "#general - Discord");
    }

    [Fact]
    public void RegexMetacharacters_AreEscaped()
    {
        var pattern = TitlePatternSuggester.Suggest("file.txt - C++ (Editor)");

        // The pattern must be a valid regex and match the same trailing segment literally.
        Assert.Matches(pattern, "other - C++ (Editor)");
        Assert.DoesNotMatch(pattern, "other - CXX [Editor]");
    }

    [Fact]
    public void EmptyTitle_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TitlePatternSuggester.Suggest(""));
        Assert.Equal(string.Empty, TitlePatternSuggester.Suggest("   "));
    }

    [Fact]
    public void Suggested_PatternIsAValidRegex()
    {
        var pattern = TitlePatternSuggester.Suggest("doc - Visual Studio Code");
        var ex = Record.Exception(() => Regex.IsMatch("x", pattern));
        Assert.Null(ex);
    }
}
