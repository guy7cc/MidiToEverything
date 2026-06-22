using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>obs-websocket connection parameters (docs/05 §5, Phase 3).</summary>
public sealed record ObsConnection(string Host, int Port, string Password);

/// <summary>
/// Port over OBS Studio control via obs-websocket v5 (docs/05 §3.2, Phase 3). Connection is
/// managed lazily by the adapter; <see cref="Send"/> is fire-and-forget.
/// </summary>
public interface IObsClient
{
    void Send(ObsOp op, string? arg);
}
