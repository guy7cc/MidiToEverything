namespace MidiToEverything.Core.Application.Ports;

/// <summary>Port over HTTP requests / webhooks (docs/05 §5, Phase 3). Fire-and-forget.</summary>
public interface IHttpSender
{
    void Send(string url, string method, string? body);
}

/// <summary>Port over OSC-over-UDP sending (docs/05 §5, Phase 3).</summary>
public interface IOscSender
{
    /// <summary><paramref name="target"/> is "host:port"; <paramref name="args"/> is space-separated.</summary>
    void Send(string target, string address, string args);
}
