using MidiToEverything.Core.Application;

namespace MidiToEverything.Core.Tests.Application;

public class ReleaseParserTests
{
    // Shape mirrors the real GitHub /releases/latest response (trimmed).
    private const string LatestJson = """
    {
      "tag_name": "v0.3.0",
      "html_url": "https://github.com/guy7cc/MidiToEverything/releases/tag/v0.3.0",
      "draft": false,
      "prerelease": false,
      "assets": [
        { "name": "MidiToEverything-v0.3.0-win-x64.msi",
          "browser_download_url": "https://github.com/guy7cc/MidiToEverything/releases/download/v0.3.0/MidiToEverything-v0.3.0-win-x64.msi",
          "digest": "sha256:526d57f861a93f644b4b1576d212caa9c942a075f8f727f00c6718879edff792" },
        { "name": "MidiToEverything-v0.3.0-win-x64.zip",
          "browser_download_url": "https://github.com/guy7cc/MidiToEverything/releases/download/v0.3.0/MidiToEverything-v0.3.0-win-x64.zip" }
      ]
    }
    """;

    [Fact]
    public void ParseLatestRelease_ExtractsVersionMsiAndHash()
    {
        var info = ReleaseParser.ParseLatestRelease(LatestJson);

        Assert.NotNull(info);
        Assert.Equal("0.3.0", info!.Version); // "v" stripped
        Assert.EndsWith("MidiToEverything-v0.3.0-win-x64.msi", info.InstallerUrl);
        Assert.Equal("https://github.com/guy7cc/MidiToEverything/releases/tag/v0.3.0", info.ReleaseUrl);
        Assert.StartsWith("sha256:", info.Sha256);
    }

    [Theory]
    [InlineData("{ \"prerelease\": true, \"tag_name\": \"v9.9.9\", \"assets\": [] }")]
    [InlineData("{ \"draft\": true, \"tag_name\": \"v9.9.9\", \"assets\": [] }")]
    [InlineData("{ \"tag_name\": \"v1.0.0\", \"assets\": [ { \"name\": \"notes.txt\" } ] }")] // no MSI
    [InlineData("not json")]
    public void ParseLatestRelease_ReturnsNull_ForDraftPrereleaseNoMsiOrJunk(string json)
        => Assert.Null(ReleaseParser.ParseLatestRelease(json));

    [Theory]
    [InlineData("0.3.0", "0.1.0", true)]
    [InlineData("v0.3.0", "0.3.0", false)]   // same version, v-prefixed
    [InlineData("0.2.0", "0.3.0", false)]    // older
    [InlineData("1.0.0", "0.9.9", true)]
    [InlineData("0.3.1-beta", "0.3.0", true)] // prerelease suffix stripped, 0.3.1 > 0.3.0
    [InlineData("garbage", "0.3.0", false)]
    public void IsNewer_ComparesNumerically(string candidate, string current, bool expected)
        => Assert.Equal(expected, ReleaseParser.IsNewer(candidate, current));
}
