using MidiToEverything.Core;

namespace MidiToEverything.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsProductName()
    {
        Assert.Equal("MidiToEverything", AppInfo.Name);
    }

    [Fact]
    public void Version_IsNonEmptySemverLike()
    {
        var version = AppInfo.Version;
        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Contains(".", version);
    }

    [Fact]
    public void DataDirectory_IsUnderAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.StartsWith(appData, AppInfo.DataDirectory);
        Assert.EndsWith("MidiToEverything", AppInfo.DataDirectory);
    }
}
