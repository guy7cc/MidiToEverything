using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Persistence;

namespace MidiToEverything.Core.Tests.Persistence;

public class JsonProfileRepositoryTests : IDisposable
{
    private readonly string _dir;

    public JsonProfileRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MidiToEverything.Tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreateDefault_WritesFileOnFirstRun()
    {
        var repo = new JsonProfileRepository(_dir);
        Assert.False(File.Exists(repo.ConfigPath));

        var config = repo.LoadOrCreateDefault();

        Assert.True(File.Exists(repo.ConfigPath));
        Assert.Equal("base", config.BaseProfile.Id);
        Assert.Equal(2, config.Profiles.Count);
    }

    [Fact]
    public void LoadOrCreateDefault_SecondCall_LoadsExistingFile()
    {
        var repo = new JsonProfileRepository(_dir);
        repo.LoadOrCreateDefault();
        var firstWrite = File.GetLastWriteTimeUtc(repo.ConfigPath);

        var reloaded = repo.LoadOrCreateDefault();

        // File was not rewritten and content is intact.
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(repo.ConfigPath));
        Assert.Equal("clip-studio", reloaded.Profiles[0].Id);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAcrossInstances()
    {
        var original = DefaultConfig.Create() with
        {
            Settings = DefaultConfig.Create().Settings with { StartWithWindows = true },
        };
        new JsonProfileRepository(_dir).Save(original);

        var loaded = new JsonProfileRepository(_dir).Load();

        Assert.True(loaded.Settings.StartWithWindows);
        Assert.Equal(
            original.BaseProfile.Bindings.Count,
            loaded.BaseProfile.Bindings.Count);
        Assert.Equal(
            new[] { "ctrl", "z" },
            Assert.IsType<KeyAction>(loaded.BaseProfile.Bindings[0].Actions[0]).Keys);
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var repo = new JsonProfileRepository(_dir);

        Assert.Throws<FileNotFoundException>(() => repo.Load());
    }

    [Fact]
    public void LoadOrCreateDefault_CorruptFile_BacksUpAndRecreatesDefault()
    {
        var repo = new JsonProfileRepository(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(repo.ConfigPath, "{ this is not valid json");

        var config = repo.LoadOrCreateDefault();

        // App boots with defaults instead of crashing...
        Assert.Equal("base", config.BaseProfile.Id);
        Assert.Equal(2, config.Profiles.Count);
        // ...the bad file is preserved for recovery...
        var backup = Path.Combine(_dir, "config.corrupt.json");
        Assert.True(File.Exists(backup));
        Assert.Equal("{ this is not valid json", File.ReadAllText(backup));
        // ...and a fresh valid config now sits at the real path.
        Assert.Equal("clip-studio", repo.Load().Profiles[0].Id);
    }

    [Fact]
    public void LoadOrCreateDefault_NewerSchema_BacksUpAndRecreatesDefault()
    {
        var repo = new JsonProfileRepository(_dir);
        Directory.CreateDirectory(_dir);
        // A file from a future schema version must not brick an older app.
        File.WriteAllText(repo.ConfigPath, "{ \"version\": 9999, \"baseProfile\": null, \"profiles\": [] }");

        var config = repo.LoadOrCreateDefault();

        Assert.Equal("base", config.BaseProfile.Id);
        Assert.True(File.Exists(Path.Combine(_dir, "config.corrupt.json")));
    }

    [Fact]
    public void LoadOrCreateDefault_RepeatedCorruption_DoesNotClobberEarlierBackups()
    {
        var repo = new JsonProfileRepository(_dir);
        Directory.CreateDirectory(_dir);

        File.WriteAllText(repo.ConfigPath, "garbage-1");
        repo.LoadOrCreateDefault();

        // Corrupt it again; the previous backup must survive under a new name.
        File.WriteAllText(repo.ConfigPath, "garbage-2");
        repo.LoadOrCreateDefault();

        Assert.Equal("garbage-1", File.ReadAllText(Path.Combine(_dir, "config.corrupt.json")));
        Assert.Equal("garbage-2", File.ReadAllText(Path.Combine(_dir, "config.corrupt.1.json")));
    }
}
