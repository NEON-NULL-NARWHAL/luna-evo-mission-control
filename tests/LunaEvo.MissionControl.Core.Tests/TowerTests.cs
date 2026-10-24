// LUNA EVO · LunaEvo.MissionControl.Core.Tests/TowerTests.cs
// [Test] The tower: asking intentions, clearing one plane and holding the next, cleared to land, vacating, handing
//        the runway to the next plane, losing a silent plane, and the HTTP channel's answers to good and bad requests.
// Used by: dotnet test
// Guide:   Meat and Potatoes/28-Tower.md

using System.Text.Json;
using LunaEvo.MissionControl.Core.Tower;

namespace LunaEvo.MissionControl.Core.Tests;

public class TowerTests
{
    private static readonly DateTime T0 = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A report from a point in KSC's runway frame (meters along the runway from touchdown, right of the centerline).</summary>
    private static PlaneReport At(string callSign, double along, double cross, double heading = 90, string situation = "Flying", double speed = 120)
    {
        var fix = Ksc.At("", along, cross, 900, 0);
        return new PlaneReport(callSign, fix.Latitude, fix.Longitude, 900, heading, speed, situation);
    }

    [Fact]
    public void Asks_a_new_plane_its_intentions_once()
    {
        var tower = new Tower.Tower();
        var first = tower.Report(At("Wingman", 5000, -2000), T0);
        Assert.Equal("ask-intent", Assert.Single(first).Kind);
        Assert.Empty(tower.Report(At("Wingman", 5500, -2000), T0.AddSeconds(3)));
        var answer = tower.Say("Wingman", "intent", "local flight, sightseeing", T0.AddSeconds(4));
        Assert.Equal("info", Assert.Single(answer).Kind);
        Assert.Equal(PlaneState.Local, tower.Planes.Single().State);
    }

    [Fact]
    public void Clears_the_first_and_holds_the_second_then_hands_over_the_runway()
    {
        var tower = new Tower.Tower();
        tower.Report(At("Alpha", 2000, -1000), T0); // north of the runway
        tower.Report(At("Bravo", 3000, 2000), T0); // south of it
        var alpha = Assert.Single(tower.Say("Alpha", "request-approach", "", T0.AddSeconds(1)));
        Assert.Equal("clearance", alpha.Kind);
        Assert.Equal("RNAV 09 NORTH", alpha.Route!.Name);
        Assert.Equal(["KSC-N1", "KSC-N2", "KSC-N3", "F09"], alpha.Route.Fixes.Select(f => f.Name));

        var bravo = Assert.Single(tower.Say("Bravo", "intent", "land at KSC", T0.AddSeconds(2)));
        Assert.Equal("hold", bravo.Kind);
        Assert.Equal(2, bravo.Hold!.Number);
        Assert.Equal("KSC-HS", bravo.Hold.Fix);
        Assert.Equal(1200, bravo.Hold.Altitude);
        Assert.Contains("after Alpha", bravo.Text);

        // Both report every 30 s (agents report every few seconds; a silent one is lost after a minute). Alpha flies its
        // pattern, then the final: cleared to land once; down and stopped: vacate. Bravo holds all along.
        var alphaSaid = new List<string>();
        for (var t = 0.5; t <= 6; t += 0.5)
        {
            var alphaAt = t < 4 ? At("Alpha", -7000, -3000) : t < 6 ? At("Alpha", -3000, 50) : At("Alpha", 800, 5, situation: "Landed", speed: 0);
            alphaSaid.AddRange(tower.Report(alphaAt, T0.AddMinutes(t)).Select(m => m.Kind));
            Assert.Empty(tower.Report(At("Bravo", 2000, 6000), T0.AddMinutes(t)));
        }
        Assert.Equal(["cleared-to-land", "vacate"], alphaSaid);
        // Recovered (back at the Space Center): the runway is Bravo's.
        tower.Report(At("Alpha", 800, 5, situation: "", speed: 0), T0.AddMinutes(6.5));
        var cleared = Assert.Single(tower.Report(At("Bravo", 2000, 6000), T0.AddMinutes(6.6)));
        Assert.Equal("clearance", cleared.Kind);
        Assert.Equal("RNAV 09 SOUTH", cleared.Route!.Name);
    }

    [Fact]
    public void A_plane_that_taxied_off_the_runway_hands_it_over()
    {
        var tower = new Tower.Tower();
        tower.Report(At("Alpha", 2000, -1000), T0);
        tower.Report(At("Bravo", 3000, 2000), T0);
        tower.Say("Alpha", "request-approach", "", T0);
        tower.Say("Bravo", "request-approach", "", T0);
        tower.Report(At("Bravo", 2000, 6000), T0.AddMinutes(0.5)); // its messages so far (asked, hold)
        Assert.Equal("vacate", Assert.Single(tower.Report(At("Alpha", 900, 4, situation: "Landed", speed: 0), T0.AddMinutes(1))).Kind);
        // Taxiing off at walking pace, still landed: on the runway until it's well beside it (Wingman parks 90 m out).
        tower.Report(At("Alpha", 920, 40, situation: "Landed", speed: 5), T0.AddMinutes(1.2));
        Assert.Empty(tower.Report(At("Bravo", 2000, 6000), T0.AddMinutes(1.3)));
        tower.Report(At("Alpha", 930, 90, situation: "Landed", speed: 0), T0.AddMinutes(1.5));
        Assert.Equal("clearance", Assert.Single(tower.Report(At("Bravo", 2000, 6000), T0.AddMinutes(1.6))).Kind);
    }

    [Fact]
    public void A_silent_plane_gives_up_its_place()
    {
        var tower = new Tower.Tower();
        tower.Report(At("Alpha", 2000, -1000), T0);
        tower.Report(At("Bravo", 3000, 2000), T0);
        tower.Say("Alpha", "request-approach", "", T0);
        Assert.Equal("hold", tower.Say("Bravo", "request-approach", "", T0).Single().Kind);
        // Alpha crashed (its agent stopped reporting): after the timeout Bravo is cleared.
        Assert.Empty(tower.Report(At("Bravo", 2000, 6000), T0.AddSeconds(30)));
        Assert.Equal("clearance", Assert.Single(tower.Report(At("Bravo", 2000, 6000), T0 + Tower.Tower.Timeout + TimeSpan.FromSeconds(1))).Kind);
    }

    [Fact]
    public void Ksc_frame_and_fixes_agree()
    {
        var fix = Ksc.At("x", -8000, 0, 628, 90);
        var (along, cross) = Ksc.Frame(fix.Latitude, fix.Longitude);
        Assert.InRange(along, -8002, -7998);
        Assert.InRange(cross, -2, 2);
        Assert.Equal("RNAV 09 STRAIGHT-IN", Ksc.Pick(-12_000, 500, 85).Name);
        Assert.Equal("RNAV 09 NORTH", Ksc.Pick(1000, -800, 90).Name);
    }

    [Fact]
    public void Browsers_and_other_hosts_are_refused_and_floods_are_capped()
    {
        // Audit 13: T1 (a web page posting to localhost, DNS rebinding), T2 (planes and the channel can't grow without end).
        Assert.Null(TowerServer.Refuse(null, "localhost:8920", "application/json; charset=utf-8", 8920));
        Assert.Null(TowerServer.Refuse(null, "127.0.0.1:8920", "application/json", 8920));
        Assert.NotNull(TowerServer.Refuse("https://evil.example", "localhost:8920", "application/json", 8920));
        Assert.NotNull(TowerServer.Refuse(null, "evil.example:8920", "application/json", 8920));
        Assert.NotNull(TowerServer.Refuse(null, "localhost:9999", "application/json", 8920));
        Assert.NotNull(TowerServer.Refuse(null, "localhost:8920", "text/plain", 8920));
        Assert.NotNull(TowerServer.Refuse(null, null, null, 8920));

        using var server = new TowerServer(0, () => T0);
        string Say(string callSign, string text) => JsonSerializer.Serialize(new { callSign, kind = "intent", text });
        for (var i = 0; i < Tower.Tower.MaxPlanes; i++)
            Assert.Equal(200, server.Answer("POST", "/v1/say", Say($"p{i}", "local")).Status);
        Assert.Equal(429, server.Answer("POST", "/v1/say", Say("one too many", "local")).Status);
        Assert.Equal(200, server.Answer("POST", "/v1/say", Say("p0", "still me")).Status);
        for (var i = 0; i < Tower.Tower.MaxChannel; i++)
            server.Answer("POST", "/v1/say", Say("p1", "spam"));
        Assert.True(server.Tower.Channel.Count <= Tower.Tower.MaxChannel);
    }

    [Fact]
    public void The_channel_is_kept_as_data_one_json_line_per_message()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "tower-" + Guid.NewGuid().ToString("N"));
        var log = Path.Combine(folder, "tower.jsonl");
        try
        {
            using var server = new TowerServer(0, () => T0) { LogPath = log };
            server.Answer("POST", "/v1/report", """{"callSign":"Alpha","latitude":-0.05,"longitude":-74.7,"situation":"Flying"}""");
            server.Answer("POST", "/v1/say", """{"callSign":"Alpha","kind":"request-approach","text":""}""");
            var lines = File.ReadAllLines(log);
            Assert.Equal(["ask-intent", "request-approach", "clearance"], lines.Select(l => JsonDocument.Parse(l).RootElement.GetProperty("kind").GetString()));
            Assert.Equal("RNAV 09 SOUTH", JsonDocument.Parse(lines[2]).RootElement.GetProperty("route").GetProperty("name").GetString());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_channel_answers_good_requests_and_refuses_bad_ones()
    {
        using var server = new TowerServer(0, () => T0);
        var (status, body) = server.Answer("POST", "/v1/report", """{"callSign":"Wingman","latitude":-0.05,"longitude":-74.7,"altitude":900,"heading":90,"speed":120,"situation":"Flying"}""");
        Assert.Equal(200, status);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("ask-intent", doc.RootElement.GetProperty("messages")[0].GetProperty("kind").GetString());

        Assert.Equal(405, server.Answer("GET", "/v1/report", "{}").Status);
        Assert.Equal(400, server.Answer("POST", "/v1/report", "not json").Status);
        Assert.Equal(400, server.Answer("POST", "/v1/report", """{"latitude":1,"longitude":2}""").Status); // no call sign
        Assert.Equal(400, server.Answer("POST", "/v1/report", """{"callSign":"x","latitude":"north"}""").Status);
        Assert.Equal(400, server.Answer("POST", "/v1/say", """{"callSign":"x"}""").Status); // no kind
        Assert.Equal(404, server.Answer("POST", "/v1/launch", """{"callSign":"x"}""").Status);
        Assert.Equal(413, server.Answer("POST", "/v1/say", null).Status);
        // Long or odd text is cut and cleaned, not refused.
        var (ok, _) = server.Answer("POST", "/v1/say", JsonSerializer.Serialize(new { callSign = new string('W', 99), kind = "intent", text = "land\u0007" + new string('x', 900) }));
        Assert.Equal(200, ok);
        Assert.All(server.Tower.Channel, m => Assert.True(m.Text.Length <= 300 && m.From.Length <= 40 && !m.Text.Contains('\u0007')));
    }
}
