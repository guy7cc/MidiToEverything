using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>Port over system audio endpoint volume (docs/05_ActionExpansion.md §5, Phase 1).</summary>
public interface ISystemAudio
{
    /// <summary>Set the endpoint volume to a scalar 0..1.</summary>
    void SetVolume(VolumeTarget target, double level);
}
