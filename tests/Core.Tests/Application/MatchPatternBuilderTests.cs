using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Tests.Application;

public class MatchPatternBuilderTests
{
    [Fact]
    public void AddProcess_ToEmpty_CreatesAnchoredClause()
    {
        var result = MatchPatternBuilder.AddProcess("", "chrome.exe");

        Assert.Equal(AddProcessStatus.Added, result.Status);
        Assert.Equal(@"^chrome\.exe$", result.Pattern);
        Assert.True(new MatchRule { Pattern = result.Pattern }.Matches("chrome.exe", "any"));
    }

    [Fact]
    public void AddProcess_ToExisting_OrMerges()
    {
        var result = MatchPatternBuilder.AddProcess(@"^a\.exe$", "b.exe");

        Assert.Equal(AddProcessStatus.Added, result.Status);
        var rule = new MatchRule { Pattern = result.Pattern };
        Assert.True(rule.Matches("a.exe", "x"));  // original still matches
        Assert.True(rule.Matches("b.exe", "x"));  // new one matches too
    }

    [Fact]
    public void AddProcess_PreservesTitleClause()
    {
        // Existing pattern is a title clause; adding a process must keep both working.
        var result = MatchPatternBuilder.AddProcess("Google Chrome$", "msedge.exe");

        var rule = new MatchRule { Pattern = result.Pattern };
        Assert.True(rule.Matches("anything.exe", "Page - Google Chrome"));
        Assert.True(rule.Matches("msedge.exe", "no title"));
    }

    [Fact]
    public void AddProcess_Duplicate_ReportsAlreadyPresent()
    {
        var result = MatchPatternBuilder.AddProcess(@"^chrome\.exe$", "chrome.exe");

        Assert.Equal(AddProcessStatus.AlreadyPresent, result.Status);
        Assert.Equal(@"^chrome\.exe$", result.Pattern);
    }

    [Fact]
    public void AddProcess_EmptyName_ReportsEmptyName()
    {
        Assert.Equal(AddProcessStatus.EmptyName, MatchPatternBuilder.AddProcess("x", "  ").Status);
    }

    [Fact]
    public void AddProcess_IntoInvalidExisting_ReportsReconstructionFailed()
    {
        var result = MatchPatternBuilder.AddProcess("(unbalanced", "a.exe");

        Assert.Equal(AddProcessStatus.ReconstructionFailed, result.Status);
        Assert.Equal("(unbalanced", result.Pattern); // unchanged
    }
}
