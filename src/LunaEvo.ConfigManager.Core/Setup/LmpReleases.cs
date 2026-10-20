// LUNA EVO · LunaEvo.ConfigManager.Core/Setup/LmpReleases.cs
// [Core] Reads LMP's releases from GitHub and picks the right server download; parses LMP version numbers.
// Used by: SetupWizard, SettingsPage (update check), ServerInstaller
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LunaEvo.ConfigManager.Core.Setup;

/// <param name="Sha256">The SHA-256 GitHub publishes for the file (lowercase hex), or null if it has none.</param>
public sealed record LmpAsset(string Name, string DownloadUrl, long Size, string? Sha256 = null);

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

        // GitHub's JSON is outside our control: read it defensively, skipping anything that doesn't look right.
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("GitHub didn't return a list of releases.");
        var releases = new List<LmpRelease>();
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Bool(item, "draft"))
                continue;
            var prerelease = Bool(item, "prerelease");
            if (prerelease && !includePrereleases)
                continue;

            var tag = Str(item, "tag_name");
            var assets = item.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray()
                    .Where(a => a.ValueKind == JsonValueKind.Object)
                    .Select(a => new LmpAsset(Str(a, "name"), Str(a, "browser_download_url"),
                        a.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out var n) ? n : 0,
                        Digest(Str(a, "digest"))))
                    .ToList()
                : [];
            var release = new LmpRelease(tag, LmpVersion.Parse(tag), prerelease,
                item.TryGetProperty("published_at", out var published) && published.ValueKind == JsonValueKind.String && published.TryGetDateTimeOffset(out var when) ? when : DateTimeOffset.MinValue,
                Str(item, "html_url"),
                assets);
            if (release.ServerAsset() != null)
                releases.Add(release);
        }
        return releases.OrderByDescending(r => r.PublishedAt).ToList();
    }

    /// <summary>GitHub publishes "sha256:&lt;64 hex&gt;" per asset. Anything else counts as "no checksum".</summary>
    public static string? Digest(string text) =>
        text.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) && text.Length == 71 && text[7..].All(Uri.IsHexDigit)
            ? text[7..].ToLowerInvariant()
            : null;

    private static string Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static bool Bool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

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
