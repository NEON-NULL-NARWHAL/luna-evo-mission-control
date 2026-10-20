// LUNA EVO · LunaEvo.ConfigManager.Core/Setup/LmpReleases.cs
// [Core] Reads LMP's releases from GitHub and picks the right server download; parses LMP version numbers.
// Used by: SetupWizard, SettingsPage (update check), ServerInstaller
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LunaEvo.ConfigManager.Core.Setup;

public sealed record LmpAsset(string Name, string DownloadUrl, long Size);

public sealed record LmpRelease(string Tag, Version? Version, bool Prerelease, DateTimeOffset PublishedAt, string PageUrl, IReadOnlyList<LmpAsset> Assets)
{
    /// <summary>
    /// The server zip for this platform. Up to 0.29 LMP shipped one "LunaMultiplayer-Server-Release.zip"
    /// (framework-dependent, needs .NET 6); from 0.30 it ships per platform, e.g. "LunaMultiplayer-Server-win-x64-Release.zip".
    /// </summary>
    public LmpAsset? ServerAsset(string runtime = "win-x64") =>
        Assets.FirstOrDefault(a => a.Name.Equals($"LunaMultiplayer-Server-{runtime}-Release.zip", StringComparison.OrdinalIgnoreCase))
        ?? Assets.FirstOrDefault(a => a.Name.Equals("LunaMultiplayer-Server-Release.zip", StringComparison.OrdinalIgnoreCase));

    public override string ToString() => Prerelease ? $"{Tag} (pre-release)" : Tag;
}

/// <summary>LMP's GitHub releases. Takes an HttpClient so tests can serve canned responses.</summary>
public sealed class LmpReleaseFeed(HttpClient http)
{
    public const string ReleasesUrl = "https://api.github.com/repos/LunaMultiplayer/LunaMultiplayer/releases?per_page=30";

    /// <summary>Releases that have a server download, newest first. Drafts are never included.</summary>
    public async Task<IReadOnlyList<LmpRelease>> GetAsync(bool includePrereleases, CancellationToken cancel = default)
    {
        using var response = await http.GetAsync(ReleasesUrl, cancel);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);

        var releases = new List<LmpRelease>();
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (item.GetProperty("draft").GetBoolean())
                continue;
            var prerelease = item.GetProperty("prerelease").GetBoolean();
            if (prerelease && !includePrereleases)
                continue;

            var tag = item.GetProperty("tag_name").GetString() ?? "";
            var assets = item.GetProperty("assets").EnumerateArray()
                .Select(a => new LmpAsset(a.GetProperty("name").GetString() ?? "", a.GetProperty("browser_download_url").GetString() ?? "", a.GetProperty("size").GetInt64()))
                .ToList();
            var release = new LmpRelease(tag, LmpVersion.Parse(tag), prerelease,
                item.TryGetProperty("published_at", out var published) && published.ValueKind == JsonValueKind.String ? published.GetDateTimeOffset() : DateTimeOffset.MinValue,
                item.TryGetProperty("html_url", out var page) ? page.GetString() ?? "" : "",
                assets);
            if (release.ServerAsset() != null)
                releases.Add(release);
        }
        return releases.OrderByDescending(r => r.PublishedAt).ToList();
    }

    /// <summary>The newest release, or null if GitHub returned none.</summary>
    public async Task<LmpRelease?> LatestAsync(bool includePrereleases, CancellationToken cancel = default) =>
        (await GetAsync(includePrereleases, cancel)).FirstOrDefault();
}

public static partial class LmpVersion
{
    /// <summary>"0.29.2", "0.29.2-compiled" or "0.30.0-Draft" → 0.29.2 / 0.30.0. Null if there's no version in it.</summary>
    public static Version? Parse(string? text)
    {
        var match = text == null ? null : VersionRegex().Match(text);
        return match is { Success: true } && Version.TryParse(match.Value, out var version) ? version : null;
    }

    /// <summary>The version of the LMP server in a folder, from Server.dll's product version.</summary>
    public static Version? OfServer(string serverDirectory)
    {
        var dll = Path.Combine(serverDirectory, "Server.dll");
        return File.Exists(dll) ? Parse(FileVersionInfo.GetVersionInfo(dll).ProductVersion) : null;
    }

    [GeneratedRegex(@"\d+\.\d+(\.\d+)?")]
    private static partial Regex VersionRegex();
}
