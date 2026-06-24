namespace MidiToEverything.Core.Tests;

/// <summary>Locates repo-relative paths from the test output directory.</summary>
internal static class RepoPaths
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MidiToEverything.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName
                ?? throw new InvalidOperationException("Could not locate repo root (MidiToEverything.sln).");
        }
    }

    public static string SampleConfig => Path.Combine(Root, "samples", "config.sample.json");

    /// <summary>The action-acceptance runbook's test config (.claude/skills/test-actions/test-config.json).</summary>
    public static string AcceptanceConfig =>
        Path.Combine(Root, ".claude", "skills", "test-actions", "test-config.json");
}
