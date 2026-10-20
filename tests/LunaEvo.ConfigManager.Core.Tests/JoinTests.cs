// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/JoinTests.cs
// [Test] The player side: parsing addresses, probing a server, KSP launch plans, and LMP's own server list.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Net;
using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Join;

namespace LunaEvo.ConfigManager.Core.Tests;

public class JoinTests
{
    [Theory]
    [InlineData("192.168.1.20", "192.168.1.20", 8800)]
    [InlineData("192.168.1.20:9000", "192.168.1.20", 9000)]
    [InlineData("  kerbin.example.com:8800 ", "kerbin.example.com", 8800)]
    [InlineData("lmp://kerbin.example.com:8801/", "kerbin.example.com", 8801)]
    [InlineData("[fd00::1]:8800", "fd00::1", 8800)]
    [InlineData("fd00::1", "fd00::1", 8800)]
    public void Parses_server_addresses(string text, string host, int port)
    {
        var address = ServerAddress.Parse(text);
        Assert.NotNull(address);
        Assert.Equal((host, port), (address.Host, address.Port));
    }

    [Theory]
    [InlineData("")]
    [InlineData("host:notaport")]
    [InlineData("host:70000")]
    [InlineData("http://host/path")]
    [InlineData("user@host")]
    [InlineData("two words")]
    public void Rejects_things_that_arent_addresses(string text) => Assert.Null(ServerAddress.Parse(text));

    [Theory]
    [InlineData("127.0.0.1", "This PC")]
    [InlineData("192.168.1.20", "Local network")]
    [InlineData("10.0.0.5", "Local network")]
    [InlineData("100.101.102.103", "VPN or mesh network (e.g. Tailscale)")]
    [InlineData("203.0.113.7", "Internet")]
    public void Labels_what_kind_of_network_an_address_is_on(string address, string kind) =>
        Assert.Equal(kind, ServerProbe.Kind(IPAddress.Parse(address)));

    [Fact]
    public async Task Resolves_and_pings_this_pc()
    {
        var resolved = await ServerProbe.ResolveAsync("127.0.0.1");
        Assert.Equal("This PC", Assert.Single(resolved).Kind);

        var ping = await ServerProbe.PingAsync("127.0.0.1", count: 2);
        Assert.Equal(2, ping.Sent);
        Assert.True(ping.AnyReply);
    }

    [Fact]
    public async Task Reads_live_info_from_the_hosts_website_or_explains_why_not()
    {
        var json = File.ReadAllText(Path.Combine(TestPaths.Fixtures, "Web", "serverinfo.json"));
        var http = new FakeHttp().Text("http://kerbin.example.com:8900/", json).Text("http://other.example.com:8900/", "<html>not lmp</html>");

        var live = await ServerProbe.LiveInfoAsync(http.Client, ServerAddress.Parse("kerbin.example.com")!);
        Assert.True(live.Reachable);
        Assert.NotNull(live.LatencyMs);
        Assert.Equal("LUNA EVO Test Server", live.Snapshot!.ServerName);
        Assert.Equal("Sandbox", live.Snapshot.GameMode);
        Assert.False(live.Snapshot.HasPassword);
        Assert.Equal("Subspace", live.Snapshot.WarpMode);

        var notLmp = await ServerProbe.LiveInfoAsync(http.Client, ServerAddress.Parse("other.example.com")!);
        Assert.False(notLmp.Reachable);
        Assert.Contains("isn't LMP", notLmp.Error);

        var missing = await ServerProbe.LiveInfoAsync(http.Client, ServerAddress.Parse("nobody.example.com")!);
        Assert.False(missing.Reachable);
    }

    [Theory]
    [InlineData("kerbin.example.com", false, JoinMode.AutoConnect)]
    [InlineData("kerbin.example.com", true, JoinMode.PickFromList)]   // LMP can't take a password at launch
    [InlineData("kerbin.example.com:9000", false, JoinMode.PickFromList)] // LMP mis-parses a port in -lmp
    public void Auto_connects_only_where_lmps_launch_option_works(string address, bool password, JoinMode mode)
    {
        var server = ServerAddress.Parse(address)!;
        Assert.Equal(mode, KspLauncher.PlanFor(server, password));
        var arguments = KspLauncher.LaunchArguments(server, password);
        if (mode == JoinMode.AutoConnect)
            Assert.Equal(["-lmp", "lmp://kerbin.example.com"], arguments); // no port: that's the form LMP parses correctly
        else
            Assert.Empty(arguments);
    }

    [Fact]
    public void Adds_and_updates_servers_in_lmps_own_list_with_a_backup()
    {
        var ksp = TestData.FakeKsp();
        var path = KspLauncher.ClientSettingsPath(ksp);
        var original = File.ReadAllText(path);

        Assert.True(KspLauncher.AddToServerList(ksp, "Kerbin Space Agency", ServerAddress.Parse("kerbin.example.com:9000")!, "jebediah"));
        Assert.Equal(original, File.ReadAllText(path + ".lunaevo-backup")); // the backup is the file before each change
        Assert.False(KspLauncher.AddToServerList(ksp, "KSA (renamed)", ServerAddress.Parse("kerbin.example.com:9000")!, null));

        var servers = ConfigFile.Load(path).GetObjectList("Servers")!;
        Assert.Equal(2, servers.Count);
        Assert.Equal("KSA (renamed)", servers[0]["Name"]);
        Assert.Equal("9000", servers[0]["Port"]);
        Assert.Equal("jebediah", servers[0]["Password"]); // kept when not given again
        Assert.Equal("old.example.com", servers[1]["Address"]);
        Assert.Equal("Jebediah", ConfigFile.Load(path).GetValue("PlayerName")); // the rest of LMP's settings untouched
        Assert.Contains("\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Explains_when_lmp_has_no_settings_yet()
    {
        var ksp = TestData.FakeKsp(withSettings: false);
        Assert.Throws<FileNotFoundException>(() => KspLauncher.AddToServerList(ksp, "x", ServerAddress.Parse("a.example.com")!, null));
        Assert.True(KspLauncher.IsKspFolder(ksp));
        Assert.Null(KspLauncher.ClientVersion(ksp));
        Assert.False(KspLauncher.IsKspFolder(Path.GetTempPath()));
    }
}
