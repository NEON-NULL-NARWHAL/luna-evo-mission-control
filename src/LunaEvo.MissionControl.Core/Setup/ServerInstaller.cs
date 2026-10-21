// LUNA EVO · LunaEvo.MissionControl.Core/Setup/ServerInstaller.cs
// [Core] Downloads an LMP server zip and installs or updates it, never touching Config, Universe, logs or Plugins.
// Used by: SetupWizard, SettingsPage (update)
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

using System.IO.Compression;
using System.Security.Cryptography;

namespace LunaEvo.MissionControl.Core.Setup;

public sealed record InstallResult(int FilesWritten, string? BinariesBackup);

/// <summary>A download that failed its safety checks (wrong checksum, wrong size, untrusted address). Nothing was installed.</summary>
public sealed class DownloadVerificationException(string message) : Exception(message);

public sealed class ServerInstaller(HttpClient http)
{
    /// <summary>
    /// Top-level names in a server folder that belong to the user, not to LMP's download. An update never
    /// overwrites them (LMP's zips don't contain them anyway; this makes sure of it).
    /// </summary>
    public static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "Config", "Universe", "logs", "Plugins", "LunaEvo", "LMPPlayerBans.txt", "LunaEvo.MissionControl.exe",
    };

    /// <summary>LMP's server zips are 2-40 MB; anything far bigger isn't one.</summary>
    public const long MaxDownloadBytes = 500L * 1024 * 1024;

    /// <summary>Limits when unpacking, so a malicious "zip bomb" can't fill the disk.</summary>
    public const int MaxEntries = 20_000;
    public const long MaxUnpackedBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// Only GitHub over HTTPS. Release downloads start at github.com and redirect to *.githubusercontent.com.
    /// </summary>
    public static bool IsTrustedDownload(Uri? uri) =>
        uri is { Scheme: "https" } &&
        (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Downloads a release asset into a fresh temp folder and checks it: from GitHub over HTTPS (also after
    /// redirects), the expected size, and the SHA-256 GitHub published. Any failure deletes the file and throws
    /// <see cref="DownloadVerificationException"/>.
    /// </summary>
    /// <param name="allowUnverified">
    /// Allow an asset GitHub has no checksum for (only after asking the user). Size and host are still checked.
    /// </param>
    public async Task<string> DownloadAsync(LmpAsset asset, IProgress<double>? progress = null, CancellationToken cancel = default, bool allowUnverified = false)
    {
        if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out var url) || !IsTrustedDownload(url))
            throw new DownloadVerificationException($"Refusing to download from {asset.DownloadUrl}: LUNA EVO only downloads LMP from GitHub over HTTPS.");
        if (asset.Sha256 == null && !allowUnverified)
            throw new DownloadVerificationException($"GitHub has no checksum for {asset.Name}, so it can't be verified.");
        if (asset.Size > MaxDownloadBytes)
            throw new DownloadVerificationException($"{asset.Name} is {asset.Size / 1024 / 1024} MB, which is too big to be an LMP server.");

        // A new folder per download: nothing else can have prepared a file at this path.
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvo", "downloads", Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(folder, SafeFileName(asset.Name));

        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            if (!IsTrustedDownload(response.RequestMessage?.RequestUri ?? url))
                throw new DownloadVerificationException($"The download was redirected to {response.RequestMessage?.RequestUri?.Host}, which isn't GitHub.");

            var total = response.Content.Headers.ContentLength ?? asset.Size;
            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long done = 0;
            await using (var target = File.Create(path))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancel)) > 0)
                {
                    done += read;
                    if (done > MaxDownloadBytes)
                        throw new DownloadVerificationException($"{asset.Name} is bigger than an LMP server can be.");
                    sha.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancel);
                    if (total > 0)
                        progress?.Report(Math.Min(1, (double)done / total));
                }
            }

            if (asset.Size > 0 && done != asset.Size)
                throw new DownloadVerificationException($"{asset.Name} should be {asset.Size} bytes but {done} arrived. The download was incomplete or altered.");
            var actual = Convert.ToHexStringLower(sha.GetHashAndReset());
            if (asset.Sha256 != null && actual != asset.Sha256)
                throw new DownloadVerificationException($"{asset.Name} doesn't match the checksum GitHub published for it, so it was rejected. Try again; if it keeps happening, don't install it.");

            progress?.Report(1);
            return path;
        }
        catch
        {
            TryDelete(folder);
            throw;
        }
    }

    /// <summary>SHA-256 of a file as lowercase hex, the form GitHub publishes.</summary>
    public static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>
    /// Extracts an LMP server zip into <paramref name="serverDirectory"/>. LMP's zips put everything under an
    /// "LMPServer/" folder; that prefix is dropped. When the folder already has a server, the files about to be
    /// replaced are zipped into LunaEvo/ServerBackups first. The server must not be running.
    /// </summary>
    /// <param name="expectedSha256">If given, the zip is hashed again right before unpacking, in case it changed after downloading.</param>
    public static InstallResult Install(string zipPath, string serverDirectory, string? expectedSha256 = null)
    {
        if (expectedSha256 != null && Sha256Of(zipPath) != expectedSha256.ToLowerInvariant())
            throw new DownloadVerificationException("The downloaded file changed before it could be installed, so it was rejected.");

        Directory.CreateDirectory(serverDirectory);
        var root = Path.GetFullPath(serverDirectory) + Path.DirectorySeparatorChar;

        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > MaxEntries || zip.Entries.Sum(e => e.Length) > MaxUnpackedBytes)
            throw new InvalidDataException("That zip is far bigger than an LMP server, so it wasn't unpacked.");
        var entries = zip.Entries
            .Select(e => (Entry: e, Relative: StripRoot(e.FullName)))
            .Where(x => x.Relative.Length > 0 && !x.Entry.FullName.EndsWith('/') && !IsProtected(x.Relative))
            .ToList();
        // Every path is checked before anything is written ("zip slip"), so a bad entry can't leave a half-updated server.
        foreach (var (entry, relative) in entries)
            if (!Path.GetFullPath(Path.Combine(serverDirectory, relative)).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe path in the zip: {entry.FullName}");

        string? backup = null;
        var existing = entries.Select(x => Path.Combine(serverDirectory, x.Relative)).Where(File.Exists).ToList();
        if (existing.Count > 0)
        {
            var oldVersion = LmpVersion.OfServer(serverDirectory)?.ToString() ?? "unknown";
            var folder = Directory.CreateDirectory(Path.Combine(serverDirectory, "LunaEvo", "ServerBackups")).FullName;
            backup = Path.Combine(folder, $"server-{oldVersion}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
            using var archive = ZipFile.Open(backup, ZipArchiveMode.Create);
            foreach (var file in existing)
                archive.CreateEntryFromFile(file, Path.GetRelativePath(serverDirectory, file));
        }

        long written = 0;
        var buffer = new byte[81920];
        foreach (var (entry, relative) in entries)
        {
            var target = Path.GetFullPath(Path.Combine(serverDirectory, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            // Counted as it's written, not from the sizes the zip claims (which whoever made it chooses).
            using var source = entry.Open();
            using var output = File.Create(target);
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                if ((written += read) > MaxUnpackedBytes)
                    throw new InvalidDataException("That zip unpacks to far more than an LMP server, so unpacking stopped.");
                output.Write(buffer, 0, read);
            }
        }
        return new InstallResult(entries.Count, backup);
    }

    /// <summary>True if the folder looks like an LMP server (has Server.dll).</summary>
    public static bool IsServerFolder(string directory) => File.Exists(Path.Combine(directory, "Server.dll"));

    private static string SafeFileName(string name)
    {
        var file = Path.GetFileName(name.Replace('\\', '/').Split('/')[^1]);
        return string.IsNullOrWhiteSpace(file) || file.Trim('.').Length == 0 || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "download.zip" : file;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // Temp folder; Windows cleans it up eventually.
        }
    }

    private static string StripRoot(string name)
    {
        name = name.Replace('\\', '/');
        return name.StartsWith("LMPServer/", StringComparison.OrdinalIgnoreCase) ? name["LMPServer/".Length..] : name;
    }

    private static bool IsProtected(string relative) => Protected.Contains(relative.Split('/')[0]);
}
