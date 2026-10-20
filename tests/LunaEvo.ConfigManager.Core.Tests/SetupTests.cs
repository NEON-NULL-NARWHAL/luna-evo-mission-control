// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/SetupTests.cs
// [Test] Release feed, version parsing, install/update from zips, and generating a new server's config.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Serialization;
using LunaEvo.ConfigManager.Core.Schema;
using LunaEvo.ConfigManager.Core.Setup;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Core.Tests;

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

    [Fact]
    public async Task Downloads_with_progress()
    {
        var bytes = File.ReadAllBytes(TestData.MakeServerZip());
        var http = new FakeHttp().Bytes("https://example.test/dl/LunaMultiplayer-Server-Release.zip", bytes);
        var reports = new List<double>();

        var path = await new ServerInstaller(http.Client).DownloadAsync(
            new LmpAsset("LunaMultiplayer-Server-Release.zip", "https://example.test/dl/LunaMultiplayer-Server-Release.zip", bytes.Length),
            new SyncProgress(reports.Add));

        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(1, reports[^1]);
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
