using System.Text.Json;

namespace MidiToEverything.Core.Application;

/// <summary>An available newer release: its version, the MSI download URL, and the release page.</summary>
public sealed record UpdateInfo(string Version, string InstallerUrl, string ReleaseUrl, string? Sha256 = null);

/// <summary>
/// Pure parsing of a GitHub "latest release" response and version comparison — no I/O, so the
/// update decision is unit-testable (docs/04_Roadmap.md). The network fetch lives in Infrastructure.
/// </summary>
public static class ReleaseParser
{
    /// <summary>
    /// Extract the version, MSI asset URL, and release page from a GitHub
    /// <c>/releases/latest</c> JSON body. Returns null for drafts/prereleases or when no MSI asset
    /// is attached.
    /// </summary>
    public static UpdateInfo? ParseLatestRelease(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (Bool(root, "draft") || Bool(root, "prerelease"))
            {
                return null;
            }

            var version = String(root, "tag_name");
            if (string.IsNullOrEmpty(version))
            {
                return null;
            }

            var releaseUrl = String(root, "html_url") ?? "";
            string? installerUrl = null;
            string? sha256 = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = String(asset, "name");
                    if (name is not null && name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        installerUrl = String(asset, "browser_download_url");
                        sha256 = String(asset, "digest"); // e.g. "sha256:abc..."
                        break;
                    }
                }
            }

            return string.IsNullOrEmpty(installerUrl)
                ? null
                : new UpdateInfo(NormalizeVersion(version), installerUrl!, releaseUrl, sha256);
        }
    }

    /// <summary>True when <paramref name="candidate"/> is a strictly newer version than <paramref name="current"/>.</summary>
    public static bool IsNewer(string candidate, string current)
        => TryVersion(candidate, out var c) && TryVersion(current, out var cur) && c > cur;

    private static bool TryVersion(string raw, out Version version)
        => Version.TryParse(NormalizeVersion(raw), out version!);

    // Strip a leading "v" and any prerelease/build suffix ("v1.2.3-beta+5" -> "1.2.3").
    private static string NormalizeVersion(string raw)
        => raw.Trim().TrimStart('v', 'V').Split('-', '+')[0];

    private static bool Bool(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static string? String(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
