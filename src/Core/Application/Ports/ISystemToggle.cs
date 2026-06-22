using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>Port over toggleable Windows settings, e.g. dark mode (docs/05 §5, Phase 2).</summary>
public interface ISystemToggle
{
    void Toggle(WindowsSetting setting);
}

/// <summary>Port over display brightness (docs/05 §5, Phase 2). Level is a scalar 0..1.</summary>
public interface IDisplayBrightness
{
    void SetBrightness(double level);
}
