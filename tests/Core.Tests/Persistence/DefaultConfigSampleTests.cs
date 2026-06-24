using MidiToEverything.Core.Persistence;

namespace MidiToEverything.Core.Tests.Persistence;

/// <summary>
/// Golden-file guard: the committed samples/config.sample.json must stay in sync with what
/// DefaultConfig produces, so the documented sample never drifts from real output.
/// </summary>
public class DefaultConfigSampleTests
{
    [Fact]
    public void DefaultConfig_MatchesCommittedSample()
    {
        var expected = Normalize(File.ReadAllText(RepoPaths.SampleConfig));
        var actual = Normalize(ConfigSerializer.Serialize(DefaultConfig.Create()));

        Assert.Equal(expected, actual);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();

    // One-shot generator (kept disabled). Enable, run `dotnet test --filter Regenerate`,
    // then disable again to refresh the sample after an intentional schema change.
    [Fact(Skip = "generator; enable manually to regenerate the golden sample")]
    public void Regenerate()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RepoPaths.SampleConfig)!);
        File.WriteAllText(RepoPaths.SampleConfig, ConfigSerializer.Serialize(DefaultConfig.Create()));
    }
}





