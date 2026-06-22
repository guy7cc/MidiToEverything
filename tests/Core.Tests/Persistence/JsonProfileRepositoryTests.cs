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
}
