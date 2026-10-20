// LUNA EVO · LunaEvo.ConfigManager.Core/Setup/AppUpdater.cs
// [Core] LUNA EVO updating itself from its own GitHub releases: check, download (SHA-256 verified), swap the exe.
// Used by: MainWindow (startup check, Update and restart), SettingsPage
// Guide:   Meat and Potatoes/18-Updating-LUNA-EVO.md

using System.Diagnostics;

namespace LunaEvo.ConfigManager.Core.Setup;

/// <summary>A newer LUNA EVO on GitHub and its exe.</summary>
public sealed record AppUpdate(LmpRelease Release, LmpAsset Exe)
{
    public Version Version => Release.Version!;
}

/// <summary>Thrown when GitHub won't show the releases (offline, rate-limited, or the repository is private).</summary>
public sealed class AppUpdateUnavailableException(string message) : Exception(message);

/// <summary>
/// Updates come from GitHub <b>releases</b> (built by release.ps1), not from every push: a push is source code, a
/// release carries the built exe. The swap works because Windows lets a running exe be renamed, just not overwritten:
/// the running file becomes LunaEvo.ConfigManager.exe.old, the new one takes its name, the app restarts, and the new
/// copy deletes the .old file on startup.
/// </summary>
public sealed class AppUpdater(HttpClient http)
{
    public const string Repository = "NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager";
    public const string ReleasesUrl = $"https://api.github.com/repos/{Repository}/releases?per_page=20";
    public const string ReleasesPage = $"https://github.com/{Repository}/releases";
    public const string ExeName = "LunaEvo.ConfigManager.exe";

    /// <summary>
    /// The newest release that's newer than <paramref name="current"/> and has a verifiable exe, or null if this is
    /// the newest. Releases whose exe has no published SHA-256 are skipped: LUNA EVO never installs itself unverified.
    /// </summary>
    public async Task<AppUpdate?> CheckAsync(Version current, bool includePrereleases = false, CancellationToken cancel = default)
    {
        IReadOnlyList<LmpRelease> releases;
        try
        {
            releases = await LmpReleaseFeed.GetAllAsync(http, ReleasesUrl, includePrereleases, cancel);
        }
        catch (HttpRequestException e) when (e.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            throw new AppUpdateUnavailableException("GitHub doesn't show LUNA EVO's releases. The repository may be private.");
        }
        catch (HttpRequestException e) when (e.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new AppUpdateUnavailableException("GitHub is limiting requests right now. Try again in an hour.");
        }

        return releases
            .Where(r => r.Version != null && r.Version > Normalize(current))
            .Select(r => (Release: r, Exe: r.Assets.FirstOrDefault(a => a.Name.Equals(ExeName, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Exe is { Sha256: not null })
            .OrderByDescending(x => x.Release.Version)
            .Select(x => new AppUpdate(x.Release, x.Exe!))
            .FirstOrDefault();
    }

    /// <summary>Downloads the new exe to a temp folder: GitHub only, size-limited, SHA-256 checked (see ServerInstaller).</summary>
    public Task<string> DownloadAsync(AppUpdate update, IProgress<double>? progress = null, CancellationToken cancel = default) =>
        new ServerInstaller(http).DownloadAsync(update.Exe, progress, cancel, allowUnverified: false);

    /// <summary>
    /// Puts the downloaded exe in place of <paramref name="currentExe"/>: copies it next to it, checks the hash and
    /// version again there, then renames the running exe to ".old" and the new one into its name. If anything fails,
    /// the current exe is left (or put back) as it was. Returns the ".old" path.
    /// </summary>
    public static string Install(string downloadedExe, string currentExe, AppUpdate update)
    {
        var staged = currentExe + ".new";
        var old = currentExe + ".old";
        File.Copy(downloadedExe, staged, overwrite: true);
        try
        {
            if (ServerInstaller.Sha256Of(staged) != update.Exe.Sha256)
                throw new DownloadVerificationException("The downloaded LUNA EVO doesn't match GitHub's checksum. Nothing was changed.");
            if (!LooksLikeExe(staged))
                throw new DownloadVerificationException("The download isn't a Windows program. Nothing was changed.");
            if (LmpVersion.Parse(FileVersionInfo.GetVersionInfo(staged).ProductVersion) is { } version && Normalize(version) != Normalize(update.Version))
                throw new DownloadVerificationException($"The download says it's LUNA EVO {version}, not {update.Version}. Nothing was changed.");

            if (File.Exists(old))
                File.Delete(old);
            File.Move(currentExe, old); // allowed while it runs
            try
            {
                File.Move(staged, currentExe);
            }
            catch
            {
                File.Move(old, currentExe);
                throw;
            }
            return old;
        }
        finally
        {
            if (File.Exists(staged))
                File.Delete(staged);
        }
    }

    /// <summary>Deletes the previous version's ".old" file after an update. Never throws.</summary>
    public static void CleanUp(string? currentExe)
    {
        if (currentExe == null)
            return;
        foreach (var leftover in new[] { currentExe + ".old", currentExe + ".new" })
        {
            try
            {
                if (File.Exists(leftover))
                    File.Delete(leftover);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The old process may still be closing; next start tries again.
            }
        }
    }

    /// <summary>
    /// Only the published single-file exe can replace itself. A development build has its DLLs next to it (and
    /// `dotnet run` runs dotnet.exe), so it's never touched.
    /// </summary>
    public static bool CanUpdateItself(string? exe) =>
        exe != null && Path.GetFileName(exe).Equals(ExeName, StringComparison.OrdinalIgnoreCase) &&
        !File.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "LunaEvo.ConfigManager.dll"));

    private static bool LooksLikeExe(string path)
    {
        using var stream = File.OpenRead(path);
        return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
    }

    // "0.5" and "0.5.0" are the same version; Version says they aren't.
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
