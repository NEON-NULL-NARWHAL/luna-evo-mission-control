// LUNA EVO · LunaEvo.MissionControl.Core.Tests/SetupTests.cs
// [Test] Release feed, version parsing, install/update from zips, and generating a new server's config.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Serialization;
using LunaEvo.MissionControl.Core.Schema;
using LunaEvo.MissionControl.Core.Setup;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl.Core.Tests;

public class SetupTests
{
    [Fact]
    public async Task Release_feed_skips_drafts_and_releases_without_a_server_zip()
    {
        var http = new FakeHttp().Text(LmpReleaseFeed.ReleasesUrl, TestData.ReleasesJson());
        var feed = new LmpReleaseFeed(http.Client);

        var stable = await feed.GetAsync(includePrereleases: false);
        Assert.Equal(["0.31.0", "0.29.2", "0.29.1"], stable.Select(r => r.Tag));

        var all = await feed.GetAsync(includePrereleases: true);
        Assert.Equal(["0.31.0", "0.30.0-Draft", "0.29.2", "0.29.1"], all.Select(r => r.Tag));
        Assert.Equal("LunaMultiplayer-Server-win-x64-Release.zip", all[1].ServerAsset()!.Name);
        Assert.Equal("LunaMultiplayer-Server-Release.zip", all[2].ServerAsset()!.Name);
        Assert.Equal(new Version(0, 30, 0), all[1].Version);
    }

    [Theory]
    [InlineData("0.29.2", "0.29.2")]
    [InlineData("0.29.2-compiled", "0.29.2")]
    [InlineData("0.30.0-Draft", "0.30.0")]
    [InlineData("v1.2", "1.2")]
    public void Parses_lmp_versions(string text, string expected) => Assert.Equal(Version.Parse(expected), LmpVersion.Parse(text));

    [Fact]
    public void Reads_the_installed_server_version()
    {
        Assert.Equal(new Version(0, 29, 2), LmpVersion.OfServer(TestPaths.LmpServer));
        Assert.Null(LmpVersion.OfServer(Path.GetTempPath()));
    }

    [Fact]
    public void Installs_a_fresh_server_from_lmps_zip_layout()
    {
        var target = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "install-" + Guid.NewGuid().ToString("N"));

        var result = ServerInstaller.Install(TestData.MakeServerZip(), target);

        Assert.True(ServerInstaller.IsServerFolder(target));
        Assert.True(File.Exists(Path.Combine(target, "runtimes", "win", "lib", "net6.0", "placeholder.txt")));
        Assert.Null(result.BinariesBackup);
        Assert.True(result.FilesWritten > 10);
    }

    [Fact]
    public void Updating_keeps_config_universe_and_logs_and_backs_up_old_binaries()
    {
        var target = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "update-" + Guid.NewGuid().ToString("N"));
        ServerInstaller.Install(TestData.MakeServerZip(), target);
        Directory.CreateDirectory(Path.Combine(target, "Config"));
        File.WriteAllText(Path.Combine(target, "Config", "GeneralSettings.xml"), "mine");
        Directory.CreateDirectory(Path.Combine(target, "Universe"));
        File.WriteAllText(Path.Combine(target, "Universe", "StartTime.txt"), "mine");

        // A malicious or odd zip that tries to ship a Config file must not overwrite ours.
        var result = ServerInstaller.Install(TestData.MakeServerZip(extraEntry: "LMPServer/Config/GeneralSettings.xml"), target);

        Assert.Equal("mine", File.ReadAllText(Path.Combine(target, "Config", "GeneralSettings.xml")));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(target, "Universe", "StartTime.txt")));
        Assert.NotNull(result.BinariesBackup);
        using var backup = ZipFile.OpenRead(result.BinariesBackup);
        Assert.Contains(backup.Entries, e => e.FullName == "Server.dll");
        Assert.Contains("server-0.29.2_", Path.GetFileName(result.BinariesBackup));
    }

    [Fact]
    public void Refuses_zip_entries_that_escape_the_folder()
    {
        var target = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "slip-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(() => ServerInstaller.Install(TestData.MakeServerZip(extraEntry: "LMPServer/../../evil.txt"), target));
    }

    private static LmpAsset Asset(byte[] bytes, string? sha = "auto", long? size = null, string url = TestData.ServerZipUrl) =>
        new("LunaMultiplayer-Server-Release.zip", url, size ?? bytes.Length,
            sha == "auto" ? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)) : sha);

    [Fact]
    public async Task Downloads_with_progress_and_verifies_githubs_checksum()
    {
        var bytes = File.ReadAllBytes(TestData.MakeServerZip());
        var http = new FakeHttp().Bytes(TestData.ServerZipUrl, bytes);
        var reports = new List<double>();

        var path = await new ServerInstaller(http.Client).DownloadAsync(Asset(bytes), new SyncProgress(reports.Add));

        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(1, reports[^1]);
        Assert.Equal(ServerInstaller.Sha256Of(path), Asset(bytes).Sha256);
    }

    [Fact]
    public async Task Rejects_a_download_that_doesnt_match_the_checksum_and_deletes_it()
    {
        var bytes = File.ReadAllBytes(TestData.MakeServerZip());
        var tampered = bytes.ToArray();
        tampered[^10] ^= 0xFF;
        var http = new FakeHttp().Bytes(TestData.ServerZipUrl, tampered);
        var downloads = Path.Combine(Path.GetTempPath(), "LunaEvo", "downloads");

        var error = await Assert.ThrowsAsync<DownloadVerificationException>(() => new ServerInstaller(http.Client).DownloadAsync(Asset(bytes)));

        Assert.Contains("doesn't match the checksum", error.Message);
        // Nothing left behind: no file with the tampered bytes. (Counting folders raced with other tests downloading.)
        var left = Directory.Exists(downloads)
            ? Directory.EnumerateFiles(downloads, "*", SearchOption.AllDirectories).Where(f => new FileInfo(f).Length == tampered.Length)
            : [];
        Assert.DoesNotContain(left, f => File.ReadAllBytes(f).AsSpan().SequenceEqual(tampered));
    }

    [Fact]
    public async Task Refuses_unverifiable_downloads_unless_the_user_agreed()
    {
        var bytes = File.ReadAllBytes(TestData.MakeServerZip());
        var http = new FakeHttp().Bytes(TestData.ServerZipUrl, bytes);
        var installer = new ServerInstaller(http.Client);

        await Assert.ThrowsAsync<DownloadVerificationException>(() => installer.DownloadAsync(Asset(bytes, sha: null)));
        var path = await installer.DownloadAsync(Asset(bytes, sha: null), allowUnverified: true);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("http://github.com/LunaMultiplayer/x.zip")]           // not HTTPS
    [InlineData("https://evil.example/LunaMultiplayer-Server.zip")]   // not GitHub
    [InlineData("https://github.com.evil.example/x.zip")]             // look-alike host
    [InlineData("file:///C:/Windows/notepad.exe")]
    public async Task Only_downloads_from_github_over_https(string url)
    {
        var bytes = File.ReadAllBytes(TestData.MakeServerZip());
        var http = new FakeHttp().Bytes(url, bytes);
        await Assert.ThrowsAsync<DownloadVerificationException>(() => new ServerInstaller(http.Client).DownloadAsync(Asset(bytes, url: url)));
        Assert.Empty(http.Requests); // refused before connecting
        Assert.True(ServerInstaller.IsTrustedDownload(new Uri("https://objects.githubusercontent.com/github-production-release-asset/x")));
    }

    [Fact]
    public async Task Rejects_a_download_of_the_wrong_size()
    {
        var bytes = File.ReadAllBytes(TestData.MakeServerZip());
        var http = new FakeHttp().Bytes(TestData.ServerZipUrl, bytes);
        var error = await Assert.ThrowsAsync<DownloadVerificationException>(() =>
            new ServerInstaller(http.Client).DownloadAsync(Asset(bytes, sha: null, size: bytes.Length + 1), allowUnverified: true));
        Assert.Contains("incomplete or altered", error.Message);
    }

    [Fact]
    public void Install_checks_the_zip_again_right_before_unpacking()
    {
        var zip = TestData.MakeServerZip();
        var target = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "recheck-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<DownloadVerificationException>(() => ServerInstaller.Install(zip, target, expectedSha256: new string('0', 64)));
        Assert.False(ServerInstaller.IsServerFolder(target));

        ServerInstaller.Install(zip, target, ServerInstaller.Sha256Of(zip));
        Assert.True(ServerInstaller.IsServerFolder(target));
    }

    [Fact]
    public async Task Release_feed_reads_githubs_sha256_digest()
    {
        var bytes = File.ReadAllBytes(TestData.MakeServerZip());
        var http = new FakeHttp().Text(LmpReleaseFeed.ReleasesUrl, TestData.ReleasesJson(bytes));
        var release = (await new LmpReleaseFeed(http.Client).GetAsync(false)).Single(r => r.Tag == "0.29.2");
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), release.ServerAsset()!.Sha256);
        Assert.Null(LmpReleaseFeed.Digest("md5:abc"));
        Assert.Null(LmpReleaseFeed.Digest("sha256:not-hex"));
    }

    [Fact]
    public async Task Release_feed_survives_odd_json_from_github()
    {
        var http = new FakeHttp().Text(LmpReleaseFeed.ReleasesUrl, "[{\"tag_name\":5},{\"assets\":\"nope\"},7,{\"tag_name\":\"0.29.2\",\"assets\":[{\"name\":\"LunaMultiplayer-Server-Release.zip\"}]}]");
        var releases = await new LmpReleaseFeed(http.Client).GetAsync(true);
        Assert.Equal("0.29.2", Assert.Single(releases).Tag);

        var notAList = new FakeHttp().Text(LmpReleaseFeed.ReleasesUrl, "{\"message\":\"API rate limit exceeded\"}");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new LmpReleaseFeed(notAList.Client).GetAsync(true));
    }

    [Fact]
    public void Generates_a_complete_config_that_lmp_can_read()
    {
        var schema = LmpSchemaLoader.Load(TestPaths.LmpServer);
        var config = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "gen-" + Guid.NewGuid().ToString("N"));
        var choices = new Dictionary<(string, string), string>
        {
            [(LmpRules.General, "ServerName")] = "Kerbin & Friends <Server>",
            [(LmpRules.General, "GameMode")] = "Career",
            [(LmpRules.General, "GameDifficulty")] = "Hard",
            [("MasterServerSettings.xml", "RegisterWithMasterServer")] = "false",
            [(LmpRules.Connection, "Upnp")] = "false",
        };

        var created = ConfigGenerator.CreateMissing(schema, config, choices);

        Assert.Equal(schema.Files.Count, created.Count);
        var session = ConfigSession.Open(TestPaths.LmpServer, config);
        Assert.Equal("Kerbin & Friends <Server>", session.GetValue(LmpRules.General, "ServerName"));
        Assert.Equal("false", session.GetValue("MasterServerSettings.xml", "RegisterWithMasterServer"));
        // GameplaySettings follow the Hard preset, not the property defaults.
        Assert.Equal(schema.Find(LmpRules.Gameplay)!.Presets["Hard"]["CanRevert"], session.GetValue(LmpRules.Gameplay, "CanRevert"));
        Assert.DoesNotContain(session.Validate(), i => i.FileName == LmpRules.Gameplay); // matches the preset exactly
        Assert.Contains("mk1pod", session.GetList("LMPModControl.xml", "AllowedParts"));
        Assert.DoesNotContain(session.Validate(), i => i.Severity == Severity.Error);
        Assert.Contains("\r\n", File.ReadAllText(created[0]));

        // The real test: LMP's own classes, through XmlSerializer exactly as the server loads them.
        AssertLmpCanDeserialize(schema, config);

        // Existing files are left alone.
        Assert.Empty(ConfigGenerator.CreateMissing(schema, config, choices));
    }

    [Fact]
    public void Object_lists_round_trip_and_stay_readable_by_lmp()
    {
        var schema = LmpSchemaLoader.Load(TestPaths.LmpServer);
        var config = TestPaths.CopyConfigs();
        var session = ConfigSession.Open(TestPaths.LmpServer, config);
        var plugins = schema.Find("LMPModControl.xml")!.Find("MandatoryPlugins")!;
        Assert.Equal(SettingKind.ObjectList, plugins.Kind);
        Assert.Equal("DllFile", plugins.ItemElement);
        Assert.Equal(["Text", "Link", "FilePath", "Sha"], plugins.ItemFields);

        session.SetObjectList("LMPModControl.xml", "MandatoryPlugins",
        [
            new Dictionary<string, string> { ["Text"] = "Kerbal Engineer", ["Link"] = "https://example.test", ["FilePath"] = "kerbalengineer/kerbalengineer.dll", ["Sha"] = "AB-CD" },
        ]);
        session.SetObjectList("LMPModControl.xml", "MandatoryParts",
        [
            new Dictionary<string, string> { ["Text"] = "Needs the pod", ["PartName"] = "mk1pod" },
        ]);
        Assert.True(session.IsModified("LMPModControl.xml", "MandatoryPlugins"));
        var change = Assert.Single(session.GetChanges(), c => c.Setting == "MandatoryPlugins");
        Assert.Contains("kerbalengineer.dll", change.After);
        session.Save();

        var reopened = ConfigSession.Open(TestPaths.LmpServer, config);
        var row = Assert.Single(reopened.GetObjectList("LMPModControl.xml", "MandatoryPlugins"));
        Assert.Equal("AB-CD", row["Sha"]);
        Assert.Equal("mk1pod", Assert.Single(reopened.GetObjectList("LMPModControl.xml", "MandatoryParts"))["PartName"]);
        Assert.False(reopened.IsDirty);
        AssertLmpCanDeserialize(schema, config);
    }

    /// <summary>Loads LMP's Server.dll for real and deserializes every file with XmlSerializer, like LMP does.</summary>
    private static void AssertLmpCanDeserialize(LmpSchema schema, string configDirectory)
    {
        var context = new AssemblyLoadContext("lmp-xml-check"); // XmlSerializer needs a non-collectible context
        context.Resolving += (ctx, name) =>
        {
            var p = Path.Combine(TestPaths.LmpServer, name.Name + ".dll");
            return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
        };
        var server = context.LoadFromAssemblyPath(Path.Combine(TestPaths.LmpServer, "Server.dll"));
        var types = server.GetTypes();
        foreach (var file in schema.Files)
        {
            var type = types.Single(t => t.Name == file.RootElement);
            var text = File.ReadAllText(Path.Combine(configDirectory, file.FileName)).Replace(">True<", ">true<").Replace(">False<", ">false<");
            using var reader = new StringReader(text);
            var value = new XmlSerializer(type).Deserialize(reader);
            Assert.NotNull(value);
        }
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
