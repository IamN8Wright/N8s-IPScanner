using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace N8sIPScanner;

public static class GitHubUpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/IamN8Wright/N8s-IPScanner/releases/latest";
    private const string ReleasesPageUrl = "https://github.com/IamN8Wright/N8s-IPScanner/releases";

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        var currentVersion = GetCurrentVersion();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("N8s-IPScanner/2.3.9");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var response = await client.GetAsync(LatestReleaseApiUrl, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new UpdateCheckResult
            {
                CurrentVersion = currentVersion,
                LatestVersion = null,
                IsUpdateAvailable = false,
                ReleaseUrl = ReleasesPageUrl,
                StatusText = "No published GitHub releases found yet.",
                Message = "The GitHub repo is connected, but it does not have a published release yet. Create a release with a newer version tag, such as v2.4.0, and attach the new EXE or ZIP."
            };
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var release = JsonSerializer.Deserialize<GitHubRelease>(json);

        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
        {
            return new UpdateCheckResult
            {
                CurrentVersion = currentVersion,
                LatestVersion = null,
                IsUpdateAvailable = false,
                ReleaseUrl = ReleasesPageUrl,
                StatusText = "Could not read latest GitHub release.",
                Message = "GitHub responded, but the latest release data could not be understood."
            };
        }

        var latestVersion = TryParseVersion(release.TagName);
        var updateAvailable = latestVersion is not null && latestVersion > currentVersion;
        var bestAsset = release.Assets?
            .FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ??
            release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        return new UpdateCheckResult
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            LatestTag = release.TagName,
            ReleaseName = release.Name,
            IsUpdateAvailable = updateAvailable,
            ReleaseUrl = string.IsNullOrWhiteSpace(release.HtmlUrl) ? ReleasesPageUrl : release.HtmlUrl,
            AssetName = bestAsset?.Name,
            AssetDownloadUrl = bestAsset?.BrowserDownloadUrl,
            StatusText = updateAvailable
                ? $"Update available: {release.TagName}"
                : $"N8s IP Scanner is up to date. Current: v{currentVersion}",
            Message = BuildMessage(currentVersion, latestVersion, release, updateAvailable, bestAsset)
        };
    }

    public static void OpenReleasePage(string? releaseUrl)
    {
        var url = string.IsNullOrWhiteSpace(releaseUrl) ? ReleasesPageUrl : releaseUrl;

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
    }

    private static Version? TryParseVersion(string tagName)
    {
        var cleaned = tagName.Trim();

        if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[1..];
        }

        var dashIndex = cleaned.IndexOf('-');
        if (dashIndex >= 0)
        {
            cleaned = cleaned[..dashIndex];
        }

        return Version.TryParse(cleaned, out var version) ? version : null;
    }

    private static string BuildMessage(
        Version currentVersion,
        Version? latestVersion,
        GitHubRelease release,
        bool updateAvailable,
        GitHubReleaseAsset? asset)
    {
        var latestText = latestVersion is null ? release.TagName : "v" + latestVersion;
        var assetText = asset is null ? "No EXE/ZIP asset found on the release yet." : $"Best download asset: {asset.Name}";

        if (updateAvailable)
        {
            return
                $"A newer version is available.\n\n" +
                $"Current version: v{currentVersion}\n" +
                $"Latest release: {latestText}\n" +
                $"{assetText}\n\n" +
                "Open the GitHub release page to download it.";
        }

        return
            $"You are already on the current published version.\n\n" +
            $"Current version: v{currentVersion}\n" +
            $"Latest release: {latestText}\n" +
            $"{assetText}";
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAsset[]? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}

public sealed class UpdateCheckResult
{
    public Version CurrentVersion { get; init; } = new(0, 0, 0, 0);
    public Version? LatestVersion { get; init; }
    public string? LatestTag { get; init; }
    public string? ReleaseName { get; init; }
    public bool IsUpdateAvailable { get; init; }
    public string? ReleaseUrl { get; init; }
    public string? AssetName { get; init; }
    public string? AssetDownloadUrl { get; init; }
    public string StatusText { get; init; } = "";
    public string Message { get; init; } = "";
}
