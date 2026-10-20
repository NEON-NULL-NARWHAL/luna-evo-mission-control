// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/TestData.cs
// [Test] Shared test data: a GitHub releases response shaped like LMP's, and server zips shaped like LMP's downloads.
// Used by: SetupTests, UI tests (linked)
// Guide:   Meat and Potatoes/05-Tests.md

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace LunaEvo.ConfigManager.Core.Tests;

internal static class TestData
{
    /// <summary>Where the fake releases' files live: GitHub, like the real ones (LUNA EVO only downloads from GitHub).</summary>
    public const string ZipBase = "https://github.com/LunaMultiplayer/LunaMultiplayer/releases/download/test";

    public const string ServerZipUrl = ZipBase + "/LunaMultiplayer-Server-Release.zip";

    /// <summary>
    /// GitHub's releases JSON, trimmed to the fields we read, shaped like LMP's real releases. Pass the server zip's
    /// bytes to give its assets the real size and SHA-256 "digest" GitHub publishes.
    /// </summary>
    public static string ReleasesJson(byte[]? serverZip = null, string zipBase = ZipBase)
    {
        _serverZip = serverZip;
        return JsonSerializer.Serialize(new object[]
    {
        Release("0.30.0-Draft", prerelease: true, "2026-06-02T03:31:56Z", zipBase, "LunaMultiplayer-Client-Release.zip", "LunaMultiplayer-Server-win-x64-Release.zip", "LunaMultiplayer-Server-linux-x64-Release.zip"),
        Release("0.29.2", prerelease: false, "2026-05-17T02:01:02Z", zipBase, "LunaMultiplayer-Client-Release.zip", "LunaMultiplayer-Server-Release.zip"),
        Release("0.29.1", prerelease: false, "2026-04-24T03:58:23Z", zipBase, "LunaMultiplayer-Server-Release.zip"),
        Release("0.31.0", prerelease: false, "2026-09-01T00:00:00Z", zipBase, "LunaMultiplayer-Server-Release.zip"),
        new { tag_name = "0.27.0", draft = false, prerelease = false, published_at = "2020-12-23T19:39:15Z", html_url = "", assets = new[] { Asset(zipBase, "LunaMultiplayer-Release.zip") } },
        new { tag_name = "0.32.0", draft = true, prerelease = false, published_at = "2026-10-01T00:00:00Z", html_url = "", assets = new[] { Asset(zipBase, "LunaMultiplayer-Server-Release.zip") } },
    });
    }

    [ThreadStatic]
    private static byte[]? _serverZip;

    private static object Release(string tag, bool prerelease, string published, string zipBase, params string[] assets) =>
        new { tag_name = tag, draft = false, prerelease, published_at = published, html_url = $"https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/{tag}", assets = assets.Select(a => Asset(zipBase, a)).ToArray() };

    private static object Asset(string zipBase, string name) =>
        _serverZip != null && name == "LunaMultiplayer-Server-Release.zip"
            ? new { name, browser_download_url = $"{zipBase}/{name}", size = (long)_serverZip.Length, digest = (string?)("sha256:" + Convert.ToHexStringLower(SHA256.HashData(_serverZip))) }
            : new { name, browser_download_url = $"{zipBase}/{name}", size = 1234L, digest = (string?)null };

    /// <summary>A zip shaped like LMP's: everything under "LMPServer/", plus a readme at the root.</summary>
    public static string MakeServerZip(string? extraEntry = null)
    {
        var path = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N") + ".zip");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in Directory.GetFiles(TestPaths.LmpServer))
            zip.CreateEntryFromFile(file, "LMPServer/" + Path.GetFileName(file));
        zip.CreateEntry("LMPServer/runtimes/win/lib/net6.0/placeholder.txt");
        using (var readme = new StreamWriter(zip.CreateEntry("LMP Readme.txt").Open()))
            readme.Write("readme");
        if (extraEntry != null)
            using (var extra = new StreamWriter(zip.CreateEntry(extraEntry).Open()))
                extra.Write("should not be extracted");
        return path;
    }


    /// <summary>A fake KSP folder with LMP's client settings, shaped like the real file (utf-16 declaration, UTF-8 bytes, CRLF).</summary>
    public static string FakeKsp(bool withSettings = true)
    {
        var ksp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "ksp-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(ksp, Join.KspLauncher.ExeName), "");
        var data = Directory.CreateDirectory(Path.Combine(ksp, "GameData", "LunaMultiplayer", "Data")).FullName;
        if (withSettings)
            File.WriteAllText(Path.Combine(data, "settings.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n" +
                "<SettingStructure xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n" +
                "  <PlayerName>Jebediah</PlayerName>\r\n" +
                "  <Servers>\r\n" +
                "    <ServerEntry>\r\n      <Port>8800</Port>\r\n      <Name>Old one</Name>\r\n      <Address>old.example.com</Address>\r\n      <Password />\r\n    </ServerEntry>\r\n" +
                "  </Servers>\r\n" +
                "</SettingStructure>");
        return ksp;
    }

    /// <summary>An LMPModControl.xml with the given lists (inner XML), for mod-check tests.</summary>
    public static string ModRules(bool allowNonListed, string mandatory = "", string optional = "", string forbidden = "", string parts = "",
        string mandatoryParts = "", string expansions = "")
    {
        var path = Path.Combine(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "rules-" + Guid.NewGuid().ToString("N"))).FullName, "LMPModControl.xml");
        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="utf-16"?>
            <ModControlStructure xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <RequiredExpansions>{expansions}</RequiredExpansions>
              <AllowNonListedPlugins>{(allowNonListed ? "true" : "false")}</AllowNonListedPlugins>
              <MandatoryPlugins>{mandatory}</MandatoryPlugins>
              <OptionalPlugins>{optional}</OptionalPlugins>
              <ForbiddenPlugins>{forbidden}</ForbiddenPlugins>
              <MandatoryParts>{mandatoryParts}</MandatoryParts>
              <AllowedParts>{parts}</AllowedParts>
              <AllowedResources><string>LiquidFuel</string></AllowedResources>
            </ModControlStructure>
            """);
        return path;
    }

    public static string RuleDll(string path, string sha = "", string element = "DllFile") =>
        $"<{element}><Text>{Path.GetFileNameWithoutExtension(path)}</Text><FilePath>{path}</FilePath>{(element == "DllFile" ? $"<Sha>{sha}</Sha>" : "")}</{element}>";

    public const string AppExeUrl = "https://github.com/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/releases/download/v9.9.9/LunaEvo.ConfigManager.exe";

    /// <summary>"An exe": just enough to pass the MZ check (no version info, so the version check is skipped).</summary>
    public static byte[] FakeExe(string text) => System.Text.Encoding.ASCII.GetBytes("MZ" + text);

    /// <summary>LUNA EVO's own GitHub releases: an older one, a newer one with the exe and its digest, and a newer one without a digest.</summary>
    public static string AppReleasesJson(byte[] exe, bool withDigest = true)
    {
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(exe));
        object Release(string tag, bool prerelease, object[] assets) => new
        {
            tag_name = tag, prerelease, draft = false, html_url = "https://github.com/x", published_at = "2026-10-20T12:00:00Z",
            body = $"## {tag}" + "\n" + "New things.", assets,
        };
        object Asset(string url, int size, string? digest) => new { name = "LunaEvo.ConfigManager.exe", browser_download_url = url, size, digest };
        return System.Text.Json.JsonSerializer.Serialize(new[]
        {
            Release("v10.0.0", true, [Asset(AppExeUrl.Replace("v9.9.9", "v10.0.0"), exe.Length, "sha256:" + sha)]), // pre-release: skipped
            Release("v9.9.9", false, [Asset(AppExeUrl, exe.Length, withDigest ? "sha256:" + sha : null)]),
            Release("v0.1.0", false, [Asset(AppExeUrl.Replace("v9.9.9", "v0.1.0"), exe.Length, "sha256:" + sha)]),
        });
    }
}
