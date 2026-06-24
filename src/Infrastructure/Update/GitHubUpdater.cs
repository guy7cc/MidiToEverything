using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Infrastructure.Update;

/// <summary>
/// Checks GitHub Releases for a newer version (the repo publishes a per-machine MSI + portable zip
/// per tag). Pure parsing/comparison is in <see cref="ReleaseParser"/>; this only does the fetch.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private readonly HttpClient _http;
    private readonly string _latestUrl;
    private readonly string _releasesUrl;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    public GitHubUpdateChecker(
        string repository = "guy7cc/MidiToEverything",
        HttpClient? http = null,
        ILogger<GitHubUpdateChecker>? logger = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd("MidiToEverything-Updater");
        _http.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github+json");
        _latestUrl = $"https://api.github.com/repos/{repository}/releases/latest";
        _releasesUrl = $"https://api.github.com/repos/{repository}/releases?per_page=20";
        _logger = logger ?? NullLogger<GitHubUpdateChecker>.Instance;
    }

    public async Task<UpdateInfo?> GetUpdateAsync(string currentVersion, bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        try
        {
            UpdateInfo? candidate;
            if (includePrerelease)
            {
                // Prereleases are not surfaced by /releases/latest, so scan the recent list.
                var json = await _http.GetStringAsync(_releasesUrl, cancellationToken).ConfigureAwait(false);
                candidate = ReleaseParser.ParseReleases(json, includePrerelease: true);
            }
            else
            {
                var json = await _http.GetStringAsync(_latestUrl, cancellationToken).ConfigureAwait(false);
                candidate = ReleaseParser.ParseLatestRelease(json);
            }

            if (candidate is null)
            {
                return null;
            }

            return ReleaseParser.IsNewer(candidate.Version, currentVersion) ? candidate : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
            return null;
        }
    }
}

/// <summary>Downloads the MSI (verifying its SHA-256 when provided) and launches it with msiexec.</summary>
public sealed class MsiUpdateInstaller : IUpdateInstaller
{
    private readonly HttpClient _http;
    private readonly ILogger<MsiUpdateInstaller> _logger;

    public MsiUpdateInstaller(HttpClient? http = null, ILogger<MsiUpdateInstaller>? logger = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd("MidiToEverything-Updater");
        _logger = logger ?? NullLogger<MsiUpdateInstaller>.Instance;
    }

    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"MidiToEverything-{update.Version}.msi");

        using (var response = await _http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var dest = File.Create(path);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
                read += n;
                if (total is > 0)
                {
                    progress?.Report((double)read / total.Value);
                }
            }
        }

        VerifyHash(path, update.Sha256);
        return path;
    }

    public void Launch(string installerPath)
    {
        // /passive: unattended with a progress bar (still prompts UAC for the per-machine install).
        // The MajorUpgrade in the MSI replaces the installed version in place.
        Process.Start(new ProcessStartInfo("msiexec.exe", $"/i \"{installerPath}\" /passive")
        {
            UseShellExecute = true,
        });
    }

    // Guards against a corrupted/tampered download by comparing the GitHub-reported "sha256:..." digest.
    private void VerifyHash(string path, string? expected)
    {
        if (string.IsNullOrEmpty(expected) || !expected.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var want = expected["sha256:".Length..].Trim();
        using var stream = File.OpenRead(path);
        var got = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(got, want, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(path); } catch { /* ignore */ }
            throw new InvalidOperationException("Downloaded installer failed SHA-256 verification.");
        }

        _logger.LogInformation("Installer SHA-256 verified.");
    }
}
