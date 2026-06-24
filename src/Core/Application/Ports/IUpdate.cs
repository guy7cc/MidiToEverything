namespace MidiToEverything.Core.Application.Ports;

/// <summary>Checks the release host (GitHub) for a newer version than the running one.</summary>
public interface IUpdateChecker
{
    /// <summary>Returns the available update, or null when up to date / offline / on error.</summary>
    Task<UpdateInfo?> GetUpdateAsync(string currentVersion, CancellationToken cancellationToken = default);
}

/// <summary>Downloads and launches the update installer.</summary>
public interface IUpdateInstaller
{
    /// <summary>Download the installer to a temp file (verifying its hash if present); returns the path.</summary>
    Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Launch the downloaded installer. The caller should then exit so files can be replaced.</summary>
    void Launch(string installerPath);
}
