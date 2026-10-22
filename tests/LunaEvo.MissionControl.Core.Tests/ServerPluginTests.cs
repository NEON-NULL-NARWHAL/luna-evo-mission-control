// LUNA EVO · LunaEvo.MissionControl.Core.Tests/ServerPluginTests.cs
// [Test] The LUNA EVO server plugin and Mission Control's side of it: LMP's real messages in, live.json out, read back.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Text;
using LmpCommon.Message;
using LmpCommon.Message.Data.Vessel;
using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Map;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.ServerPlugin;

namespace LunaEvo.MissionControl.Core.Tests;

public class ServerPluginTests
{
    private static readonly ClientMessageFactory Factory = new();
    private static readonly Guid Station = Guid.Parse("0b6c9a5e-0000-4000-8000-000000000001");

    private static VesselPositionMsgData Position(Guid id, double sma, bool landed = false)
    {
        var p = Factory.CreateNewMessageData<VesselPositionMsgData>();
        p.VesselId = id;
        p.BodyIndex = 21;
        p.BodyName = "Sarnus";
        p.GameTime = 1_000_000;
        p.SubspaceId = 7;
        p.Landed = landed;
        p.LatLonAlt = [1.5, -2.5, 120_000];
        p.Orbit = [3, 0.01, sma, 40, 50, 1.25, 999_000, 21]; // INC, ECC, SMA, LAN, LPE, MNA, EPH, REF
        p.VelocityVector = [1, 2, 3];
        return p;
    }

    [Fact]
    public void What_the_plugin_writes_is_what_mission_control_reads()
    {
        var plugin = new LunaEvoPlugin();
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        plugin.Observe("Jebediah", Position(Station, 8_000_000), now);
        var json = plugin.State.ToJson("1.0.0", "0.29.2", now, [new PlayerState("Jebediah", 7, Station.ToString())],
            [new SubspaceState(7, 1_000_000)], new Dictionary<Guid, string> { [Station] = "Jebediah" });

        var feed = LiveFeed.Parse(json)!;
        Assert.Equal(now, feed.WrittenUtc);
        Assert.Equal("0.29.2", feed.LmpVersion);
        Assert.Equal(new LivePlayer("Jebediah", 7, Station.ToString()), Assert.Single(feed.Players));
        var vessel = Assert.Single(feed.Vessels);
        // LMP's message order is INC, ECC, SMA...; the map's Orbit is (SMA, ECC, INC, LAN, ArgPe, MNA, EPH).
        Assert.Equal(new Orbit(8_000_000, 0.01, 3, 40, 50, 1.25, 999_000), vessel.Orbit);
        Assert.Equal(21, vessel.Body);
        Assert.Equal("Jebediah", vessel.FlownBy);
        Assert.Equal((1.5, -2.5, 120_000.0), (vessel.Lat, vessel.Lon, vessel.Alt));
    }

    [Fact]
    public void The_plugin_ignores_broken_messages_and_forgets_removed_vessels()
    {
        var plugin = new LunaEvoPlugin();
        var broken = Position(Guid.NewGuid(), 1);
        broken.Orbit = [1, 2];
        plugin.Observe("x", broken, DateTime.UtcNow);
        var empty = Position(Guid.Empty, 1);
        plugin.Observe("x", empty, DateTime.UtcNow);
        Assert.Equal(0, plugin.State.Count);

        var weird = Position(Station, double.NaN);
        weird.LatLonAlt = [double.PositiveInfinity, 0, 0];
        plugin.Observe("x", weird, DateTime.UtcNow);
        var feed = LiveFeed.Parse(plugin.State.ToJson("1", "0.29.2", DateTime.UtcNow, [], [], new Dictionary<Guid, string>()))!;
        Assert.Null(Assert.Single(feed.Vessels).Orbit); // still a valid file, just no usable orbit

        var remove = Factory.CreateNewMessageData<VesselRemoveMsgData>();
        remove.VesselId = Station;
        plugin.Observe("x", remove, DateTime.UtcNow);
        Assert.Equal(0, plugin.State.Count);
    }

    [Fact]
    public void Only_fresh_files_of_the_right_format_count_as_live()
    {
        var server = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "feed-" + Guid.NewGuid().ToString("N"))).FullName;
        var path = LiveFeed.PathFor(server);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        string File(DateTime written, int format = 1) => $$"""{"Format":{{format}},"WrittenUtc":"{{written:O}}","Players":[],"Vessels":[]}""";

        Assert.Null(LiveFeed.Read(server, now)); // none
        System.IO.File.WriteAllText(path, File(now.AddSeconds(-1)));
        Assert.NotNull(LiveFeed.Read(server, now));
        System.IO.File.WriteAllText(path, File(now.AddSeconds(-30))); // left behind by a crashed server
        Assert.Null(LiveFeed.Read(server, now));
        System.IO.File.WriteAllText(path, File(now.AddHours(1)));
        Assert.Null(LiveFeed.Read(server, now));
        System.IO.File.WriteAllText(path, File(now, format: 2));
        Assert.Null(LiveFeed.Read(server, now));
        System.IO.File.WriteAllText(path, "not json");
        Assert.Null(LiveFeed.Read(server, now));
    }

    [Fact]
    public void Plugin_positions_go_on_top_and_say_who_flies_what()
    {
        var saved = new UniverseVessel(Station.ToString(), "Station", "Station", "LANDED", 21, 9, ["Valentina Kerman"], "s.txt")
        {
            BodyName = "Sarnus",
            Orbit = new Orbit(1, 0, 0, 0, 0, 0, 0),
        };
        var orbit = new Orbit(8_000_000, 0, 0, 0, 0, 0, 0);
        var feed = new LiveFeed(DateTime.UtcNow, "1.0.0", "0.29.2", [], [
            new LiveVessel(Station.ToString().ToUpperInvariant(), 21, "Sarnus", 0, 0, 5000, orbit, false, false, "Jebediah", DateTime.UtcNow),
            new LiveVessel("0b6c9a5e-0000-4000-8000-000000000077", 21, "", 0, 0, 0, null, true, false, null, DateTime.UtcNow),
        ]);

        var vessels = LiveVessels.Overlay([saved], feed, LiveVessels.BodyNames([saved]));
        var station = vessels.Single(v => v.Name == "Station");
        Assert.Equal(orbit, station.Orbit);
        Assert.Equal("Jebediah", station.FlownBy);
        Assert.False(station.IsLanded); // it was saved landed; the plugin says it isn't any more
        Assert.Equal(["Valentina Kerman"], station.Crew);
        var launched = vessels.Single(v => v.Name == "(new vessel)");
        Assert.True(launched.IsLanded);
        Assert.Equal("Sarnus", launched.BodyName); // number 21, learned from the save
    }

    [Fact]
    public void Installs_updates_and_removes_the_plugin()
    {
        var server = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "plugin-" + Guid.NewGuid().ToString("N"))).FullName;
        var plugin = Encoding.ASCII.GetBytes("MZ plugin v1");
        Assert.True(ServerPluginInstaller.Supports(new Version(0, 29, 2)));
        Assert.False(ServerPluginInstaller.Supports(new Version(0, 30, 0)));
        Assert.False(ServerPluginInstaller.Supports(null));

        var harmony = Encoding.ASCII.GetBytes("MZ harmony");
        Assert.Equal(PluginStatus.NotInstalled, ServerPluginInstaller.Status(server, plugin));

        // A 2.4-2.6 install (the plugin alone in Plugins/) shows as "Update", and installing moves it.
        Directory.CreateDirectory(Path.Combine(server, "Plugins"));
        File.WriteAllBytes(ServerPluginInstaller.LegacyPathFor(server), plugin);
        Assert.Equal(PluginStatus.Different, ServerPluginInstaller.Status(server, plugin));
        ServerPluginInstaller.Install(server, plugin, harmony);
        Assert.False(File.Exists(ServerPluginInstaller.LegacyPathFor(server))); // two copies would both load
        Assert.Equal(harmony, File.ReadAllBytes(ServerPluginInstaller.HarmonyPathFor(server)));
        Assert.Equal(PluginStatus.Installed, ServerPluginInstaller.Status(server, plugin));
        File.Delete(ServerPluginInstaller.HarmonyPathFor(server));
        Assert.Equal(PluginStatus.Different, ServerPluginInstaller.Status(server, plugin)); // Harmony missing: needs a repair
        ServerPluginInstaller.Install(server, plugin, harmony);
        Assert.Equal(PluginStatus.Different, ServerPluginInstaller.Status(server, Encoding.ASCII.GetBytes("MZ plugin v2")));
        Assert.False(File.Exists(ServerPluginInstaller.PathFor(server) + ".tmp"));

        Directory.CreateDirectory(Path.Combine(server, "LunaEvo"));
        File.WriteAllText(LiveFeed.PathFor(server), "{}");
        ServerPluginInstaller.Remove(server);
        Assert.Equal(PluginStatus.NotInstalled, ServerPluginInstaller.Status(server, plugin));
        Assert.False(Directory.Exists(Path.Combine(server, "Plugins", "LunaEvo")));
        Assert.False(File.Exists(LiveFeed.PathFor(server)));
    }
}
