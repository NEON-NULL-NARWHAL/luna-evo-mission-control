// LUNA EVO · LunaEvo.MissionControl.Core.Tests/LiveModsOpsTests.cs
// [Test] Website JSON, GameData scanning, restarts, network helper, profiles, Universe backups.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Net;
using System.Text.Json;
using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Mods;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.MissionControl.Core.Server;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl.Core.Tests;

public class LiveModsOpsTests
{
    private static string WebFixture => File.ReadAllText(Path.Combine(TestPaths.Fixtures, "Web", "serverinfo.json"));

    // ---- Live players

    [Fact]
    public async Task Reads_players_vessels_and_subspaces_from_lmps_json()
    {
        var http = new FakeHttp().Text("http://127.0.0.1:8900/", WebFixture);
        var snapshot = await new ServerInfoClient(http.Client).GetAsync(new Uri("http://127.0.0.1:8900/"));

        Assert.Equal(["Jebediah", "Valentina"], snapshot.Players);
        Assert.Equal(3, snapshot.Vessels.Count);
        Assert.Equal("Mun Base Alpha", snapshot.Vessels[1].Name);
        Assert.Equal(2, snapshot.Subspaces.Count);
        Assert.Equal(20, snapshot.MaxPlayers);
        Assert.Equal(104910848, snapshot.BytesUsed);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("[5]")]
    [InlineData("[{\"CurrentState\": 3}]")]
    public void Rejects_json_that_isnt_lmps_with_a_json_error(string json) =>
        Assert.Throws<JsonException>(() => ServerInfoClient.Parse(JsonDocument.Parse(json).RootElement));

    [Fact]
    public void Tolerates_odd_values_inside_lmps_json()
    {
        var snapshot = ServerInfoClient.Parse(JsonDocument.Parse(
            "[{\"CurrentState\":{\"CurrentPlayers\":[\"Jeb\",1,{},null],\"CurrentVessels\":[7,{\"Name\":3}],\"Subspaces\":[{},{\"Id\":\"x\"}],\"BytesUsed\":\"lots\"},\"GeneralSettings\":[]}]").RootElement);
        Assert.Equal(["Jeb"], snapshot.Players);
        Assert.Single(snapshot.Vessels);
        Assert.Equal(2, snapshot.Subspaces.Count);
        Assert.Equal(0, snapshot.BytesUsed);
    }

    [Fact]
    public void Works_out_where_the_website_listens()
    {
        var session = ConfigSession.Open(TestPaths.LmpServer, TestPaths.CopyConfigs());
        Assert.Equal(new Uri("http://127.0.0.1:8900/"), ServerInfoClient.EndpointFor(session)); // fixture: enabled, any address

        session.SetValue(LmpRules.Website, "ListenAddress", "192.168.1.20");
        Assert.Equal(new Uri("http://192.168.1.20:8900/"), ServerInfoClient.EndpointFor(session));

        session.SetValue(LmpRules.Website, "EnableWebsite", "false");
        Assert.Null(ServerInfoClient.EndpointFor(session));
    }

    [Fact]
    public void Diffs_who_joined_and_left()
    {
        var before = ServerInfoClient.Parse(JsonDocument.Parse(WebFixture).RootElement);
        var after = before with { Players = ["Valentina", "Bill"] };

        var (joined, left) = ServerInfoClient.Diff(before, after);

        Assert.Equal(["Bill"], joined);
        Assert.Equal(["Jebediah"], left);
        Assert.Equal(["Jebediah", "Valentina"], ServerInfoClient.Diff(null, before).Joined);
    }

    // ---- GameData

    [Fact]
    public void Parses_ksp_config_nodes()
    {
        var nodes = KspConfigParser.Parse("""
            // a comment
            PART
            {
                name = delta_small  // trailing comment
                MODULE { name = ModuleLiftingSurface }
                RESOURCE
                {
                    name = LiquidFuel
                    amount = 10
                }
            }
            RESOURCE_DEFINITION { name = Ore }
            @PART[mk1pod] { @mass = 1 }
            """);

        Assert.Equal(["PART", "RESOURCE_DEFINITION", "@PART[mk1pod]"], nodes.Select(n => n.Name));
        Assert.Equal("delta_small", nodes[0].Get("name"));
        Assert.Equal(["MODULE", "RESOURCE"], nodes[0].Nodes.Select(n => n.Name));
        Assert.Equal("10", nodes[0].Nodes[1].Get("amount"));
        Assert.Equal("Ore", nodes[1].Get("name"));
    }

    [Fact]
    public void Scans_gamedata_like_lmps_client()
    {
        var gameData = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "gd-" + Guid.NewGuid().ToString("N"), "GameData")).FullName;
        void Write(string relative, string text)
        {
            var path = Path.Combine(gameData, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        Write("Squad/Parts/Aero/deltaWing/deltaWing.cfg", "PART\n{\n name = delta_small\n}\n");
        Write("Squad/Resources/ResourcesGeneric.cfg", "RESOURCE_DEFINITION\n{\n name = LiquidFuel\n}\n");
        Write("MyMod/Parts/parts.cfg", "PART { name = myMod_tank }\nPART:NEEDS[OtherMod] { name = myMod_adapter }\n@PART[mk1pod] { @mass = 2 }\nRESOURCE_DEFINITION { name = Unobtainium }\n");
        Write("SquadExpansion/MakingHistory/readme.txt", "");
        Write("SquadExpansion/Serenity/readme.txt", "");
        var dll = Path.Combine(TestPaths.LmpServer, "Lidgren.Core.dll");
        Directory.CreateDirectory(Path.Combine(gameData, "MyMod", "Plugins"));
        File.Copy(dll, Path.Combine(gameData, "MyMod", "Plugins", "MyMod.dll"));
        Directory.CreateDirectory(Path.Combine(gameData, "Squad", "Plugins"));
        File.Copy(dll, Path.Combine(gameData, "Squad", "Plugins", "KSPSteamCtrlr.dll"));
        Directory.CreateDirectory(Path.Combine(gameData, "LunaMultiplayer", "Plugins"));
        File.Copy(dll, Path.Combine(gameData, "LunaMultiplayer", "Plugins", "LmpClient.dll"));

        var scan = GameDataScanner.Scan(gameData);

        Assert.Equal(["delta.small", "myMod.adapter", "myMod.tank"], scan.Parts);
        Assert.Equal(["LiquidFuel", "Unobtainium"], scan.Resources);
        var plugin = Assert.Single(scan.Plugins);
        Assert.Equal("mymod/plugins/mymod.dll", plugin.FilePath);
        Assert.Matches("^([0-9A-F]{2}-){31}[0-9A-F]{2}$", plugin.Sha);
        Assert.StartsWith("MyMod. Version: ", plugin.Text);
        Assert.Equal(["MakingHistory", "Serenity"], scan.Expansions);
    }

    // ---- Restarts

    [Fact]
    public void Crash_restarts_stop_after_too_many_in_a_row()
    {
        var policy = new CrashRestartPolicy(maxRestarts: 3, window: TimeSpan.FromMinutes(10));
        var t = new DateTime(2026, 10, 20, 12, 0, 0);
        Assert.True(policy.ShouldRestart(t));
        Assert.True(policy.ShouldRestart(t.AddMinutes(1)));
        Assert.True(policy.ShouldRestart(t.AddMinutes(2)));
        Assert.False(policy.ShouldRestart(t.AddMinutes(3)));
        Assert.True(policy.ShouldRestart(t.AddMinutes(30))); // the old crashes have aged out
    }

    [Fact]
    public void Daily_restart_time_is_today_or_tomorrow()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0);
        Assert.Equal(new DateTime(2026, 10, 21, 4, 0, 0), DailyRestart.Next(now, "04:00"));
        Assert.Equal(new DateTime(2026, 10, 20, 23, 30, 0), DailyRestart.Next(now, "23:30"));
        Assert.Null(DailyRestart.Next(now, ""));
        Assert.Null(DailyRestart.Next(now, "25:00"));
    }

    [Fact]
    public async Task Runner_reports_whether_an_exit_was_asked_for()
    {
        using var runner = new ServerRunner(FakeServer.CreateServerFolder(), FakeServer.Executable);
        var exits = new List<(int Code, bool Requested)>();
        runner.Exited += (code, requested) => exits.Add((code, requested));

        runner.Start(["--crash"]);
        await Until(() => exits.Count == 1);
        Assert.Equal((3, false), exits[0]);

        runner.Start();
        await Until(() => runner.State == ServerState.Running);
        await runner.StopAsync(TimeSpan.FromSeconds(10));
        await Until(() => exits.Count == 2);
        Assert.Equal((0, true), exits[1]);
    }

    // ---- Hardening

    [Fact]
    public void A_broken_runtimeconfig_is_ignored_instead_of_crashing()
    {
        var server = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "rc-" + Guid.NewGuid().ToString("N"))).FullName;
        foreach (var text in new[] { "{ not json", "[]", "{\"runtimeOptions\":{\"framework\":{\"version\":\"abc\"}}}" })
        {
            File.WriteAllText(Path.Combine(server, "Server.runtimeconfig.json"), text);
            Assert.Null(DotNetRuntime.Check(server));
        }
    }

    [Fact]
    public void System_tools_are_run_from_fixed_locations_not_the_path()
    {
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "netsh.exe"), NetworkHelper.NetshPath);
        Assert.True(Path.IsPathRooted(NetworkHelper.NetshPath));
        Assert.True(Core.Setup.RuntimeInstaller.WingetPath == "winget" || Path.IsPathRooted(Core.Setup.RuntimeInstaller.WingetPath));
    }

    [Fact]
    public void Old_settings_with_a_discord_webhook_are_scrubbed_on_load()
    {
        var path = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"Theme\": \"Light\", \"DiscordWebhookUrl\": \"https://discord.com/api/webhooks/123/secret\" }");

        var settings = AppSettingsStore.Load(path);

        Assert.Equal("Light", settings.Theme);
        Assert.DoesNotContain("webhooks", File.ReadAllText(path));
    }

    // ---- Network

    [Fact]
    public void Finds_lmps_public_address_line_and_private_ranges()
    {
        Assert.Equal("203.0.113.7:8800", NetworkHelper.PublicAddressFrom(LogLine.Parse("[14:46:28][Debug]: Detected NAT addresses: 203.0.113.7:8800")));
        Assert.Null(NetworkHelper.PublicAddressFrom(LogLine.Parse("[14:46:28][LMP]: All systems up and running.")));
        Assert.True(NetworkHelper.IsPrivate(IPAddress.Parse("192.168.1.5")));
        Assert.True(NetworkHelper.IsPrivate(IPAddress.Parse("100.101.102.103"))); // Tailscale
        Assert.False(NetworkHelper.IsPrivate(IPAddress.Parse("203.0.113.7")));
        Assert.Contains("localport=8800", NetworkHelper.FirewallAddArguments(8800));
        Assert.Contains("protocol=UDP", NetworkHelper.FirewallAddArguments(8800));
        Assert.All(NetworkHelper.LocalAddresses(), a => Assert.False(IPAddress.IsLoopback(IPAddress.Parse(a.Address))));
    }

    // ---- Profiles

    [Fact]
    public void Profiles_save_list_apply_and_delete()
    {
        var server = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "prof-" + Guid.NewGuid().ToString("N"));
        var config = TestPaths.CopyConfigs();
        var store = new ProfileStore(server);
        var general = Path.Combine(config, LmpRules.General);
        var original = File.ReadAllText(general);

        store.Save("Career: weekend/1", config);
        Assert.Equal("Career- weekend-1", Assert.Single(store.List()).Name); // made safe for a folder name
        Assert.Equal(13, store.List()[0].FileCount);

        File.WriteAllText(general, "changed");
        var backup = store.Apply("Career: weekend/1", config);

        Assert.Equal(original, File.ReadAllText(general));
        Assert.Equal("changed", File.ReadAllText(Path.Combine(backup, LmpRules.General)));

        store.Delete("Career: weekend/1");
        Assert.Empty(store.List());
    }

    // ---- Universe backups

    [Fact]
    public void Universe_backups_create_restore_and_prune()
    {
        var server = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "uni-" + Guid.NewGuid().ToString("N"));
        var universe = Directory.CreateDirectory(Path.Combine(server, "Universe", "Vessels")).FullName;
        File.WriteAllText(Path.Combine(universe, "ship.txt"), "v1");
        var backups = new UniverseBackups(server);

        var (first, skipped) = backups.Create("manual", new DateTime(2026, 10, 20, 10, 0, 0));
        Assert.Equal(0, skipped);
        File.WriteAllText(Path.Combine(universe, "ship.txt"), "v2");
        File.WriteAllText(Path.Combine(universe, "extra.txt"), "new");

        var safety = backups.Restore(first.Path);

        Assert.Equal("v1", File.ReadAllText(Path.Combine(universe, "ship.txt")));
        Assert.False(File.Exists(Path.Combine(universe, "extra.txt")));
        Assert.NotNull(safety);
        Assert.Equal("beforerestore", safety.Label);
        Assert.Equal(2, backups.List().Count);

        // Later than the "before restore" snapshot (which uses the real clock), so pruning keeps these.
        for (var i = 1; i <= 3; i++)
            backups.Create("scheduled", new DateTime(2099, 1, 1, 0, 0, i));
        backups.Prune(keep: 2);
        Assert.Equal(2, backups.List().Count);
        Assert.All(backups.List(), b => Assert.Equal("scheduled", b.Label));
    }

    [Fact]
    public void Automatic_safety_snapshots_keep_their_own_count_and_never_remove_manual_ones()
    {
        var server = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "safety-" + Guid.NewGuid().ToString("N"));
        Assert.Null(new UniverseBackups(server).Safety("onstop")); // no Universe yet: nothing, no error
        Directory.CreateDirectory(Path.Combine(server, "Universe"));
        File.WriteAllText(Path.Combine(server, "Universe", "ship.txt"), "v1");
        var backups = new UniverseBackups(server);
        backups.Create("manual", new DateTime(2020, 1, 1));

        for (var i = 0; i < 3; i++)
            Assert.NotNull(backups.Safety("onstop", keep: 2));

        Assert.Equal(2, backups.List().Count(s => s.Label == "onstop"));
        Assert.Single(backups.List(), s => s.Label == "manual"); // the oldest, but kept
    }

    [Fact]
    public void Announcements_are_read_one_per_line_and_sent_when_due()
    {
        var (items, problems) = Announcements.Parse(string.Join("\n", "60 Join our Discord", "# a comment", "", "120: Backups every 2 h", "2 too often", "no number"));
        Assert.Equal([new Announcement(60, "Join our Discord"), new Announcement(120, "Backups every 2 h")], items);
        Assert.Equal(2, problems.Count);

        var start = new DateTime(2026, 10, 20, 12, 0, 0);
        var sent = new Dictionary<int, DateTime>();
        Assert.Empty(Announcements.Due(items, sent, start, start.AddMinutes(59)));
        Assert.Equal("Join our Discord", Assert.Single(Announcements.Due(items, sent, start, start.AddMinutes(60))).Text);
        Assert.Empty(Announcements.Due(items, sent, start, start.AddMinutes(61))); // not again until 120
        Assert.Equal(2, Announcements.Due(items, sent, start, start.AddMinutes(120)).Count);
    }

    [Fact]
    public void Lists_universe_vessels_and_removes_them_after_a_backup()
    {
        var server = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "vessels-" + Guid.NewGuid().ToString("N"));
        TestData.AddVessels(server);
        var vessels = UniverseVessels.List(server);
        Assert.Equal(4, vessels.Count);
        var station = vessels.Single(v => v.Id == "a1");
        Assert.Equal(("Kerbin Station", "Station", "ORBITING", 1, 2), (station.Name, station.Type, station.Situation, station.Body, station.PartCount));
        Assert.Equal(["Jebediah Kerman"], station.Crew);

        var backup = UniverseVessels.Remove(server, vessels.Where(v => v.IsDebris));
        Assert.Equal("beforecleanup", backup.Label);
        Assert.Equal(["a1", "d4"], UniverseVessels.List(server).Select(v => v.Id).Order());
        using (var zip = System.IO.Compression.ZipFile.OpenRead(backup.Path))
            Assert.Contains(zip.Entries, e => e.FullName == "Vessels/b2.txt"); // the removed ones are in the backup

        // If the backup can't read everything, nothing is removed.
        var path = Path.Combine(server, "Universe", "Vessels", "a1.txt");
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => UniverseVessels.Remove(server, UniverseVessels.List(server).Where(v => v.Id == "d4")));
        Assert.Contains(UniverseVessels.List(server), v => v.Id == "d4");
        Assert.Single(new UniverseBackups(server).List(), s => s.Label == "beforecleanup"); // the incomplete one was deleted

        var elsewhere = new UniverseVessel("x", "x", "Debris", "", 1, 1, [], Path.Combine(Path.GetTempPath(), "x.txt"));
        Assert.Throws<InvalidOperationException>(() => UniverseVessels.Remove(server, [elsewhere]));
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException();
            await Task.Delay(50);
        }
    }
}
