using MidiToEverything.Infrastructure.Obs;

namespace MidiToEverything.Infrastructure.Tests;

public class ObsAuthTests
{
    [Fact]
    public void ComputeAuth_MatchesKnownVector()
    {
        // base64(sha256(base64(sha256("testpass"+"testsalt")) + "testchallenge"))
        // independently computed; guards the obs-websocket v5 auth algorithm.
        Assert.Equal(
            "DH8rJzw8w3csbWfcnTbO18+zOu0c+LSevHghwA2BbW0=",
            ObsWebSocketClient.ComputeAuth("testpass", "testsalt", "testchallenge"));
    }
}
