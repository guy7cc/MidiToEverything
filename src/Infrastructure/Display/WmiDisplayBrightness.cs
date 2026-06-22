using System.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Infrastructure.Display;

/// <summary>
/// WMI adapter for <see cref="IDisplayBrightness"/> (docs/05 §5, Phase 2). Works for integrated
/// laptop panels via WmiMonitorBrightnessMethods; external monitors (DDC/CI) are not covered and
/// fail quietly.
/// </summary>
public sealed class WmiDisplayBrightness : IDisplayBrightness
{
    private readonly ILogger<WmiDisplayBrightness> _logger;

    public WmiDisplayBrightness(ILogger<WmiDisplayBrightness>? logger = null)
        => _logger = logger ?? NullLogger<WmiDisplayBrightness>.Instance;

    public void SetBrightness(double level)
    {
        var percent = (byte)Math.Clamp((int)Math.Round(level * 100), 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (var o in searcher.Get())
            {
                using var mo = (ManagementObject)o;
                mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, percent });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetBrightness failed (no WMI-controllable display?)");
        }
    }
}
