using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Infrastructure.Shell;

/// <summary>Launches programs/files/URLs via ShellExecute (docs/05 §5).</summary>
public sealed class ShellLauncher : IShellLauncher
{
    private readonly ILogger<ShellLauncher> _logger;

    public ShellLauncher(ILogger<ShellLauncher>? logger = null)
        => _logger = logger ?? NullLogger<ShellLauncher>.Instance;

    public void Launch(string target, string? arguments, string? workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            };
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                psi.Arguments = arguments;
            }

            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Launch failed for target '{Target}'", target);
        }
    }
}
