// LUNA EVO · LunaEvo.MissionControl.Core.Tests/KnowledgeTests.cs
// [Test] The agents' knowledge: skills graded from flights, lessons that must cite them (and merge when they say the
//        same), unlocking the tree, saving and loading, and the tower's knowledge endpoints. Also the short visual.
// Used by: dotnet test
// Guide:   Meat and Potatoes/30-Knowledge.md

using System.Text.Json;
using LunaEvo.MissionControl.Core.Tower;

namespace LunaEvo.MissionControl.Core.Tests;

public class KnowledgeTests
{
    private static readonly DateTime T0 = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    private static readonly string[] RnavLanding = ["Parked", "TakingOff", "Climbing", "OnRoute", "OnFinal", "Flaring", "RollingOut", "Stopped"];

    [Fact]
    public void Grades_each_skill_a_flight_used()
    {
        var good = Knowledge.Grade("landed", "RNAV 09 SOUTH", 0, 8, RnavLanding);
        Assert.Equal(new Dictionary<string, bool> { ["takeoff"] = true, ["route"] = true, ["land-rnav"] = true }, good);

        // Off the centerline or a go-around: the landing wasn't a good one (the takeoff and the route still were).
        Assert.False(Knowledge.Grade("landed", "RNAV 09 SOUTH", 0, 22, RnavLanding)["land-rnav"]);
        Assert.False(Knowledge.Grade("landed", "RNAV 09 SOUTH", 1, 3, RnavLanding)["land-rnav"]);

        // Held, then cleared: hold and sequencing; a visual landing is its own skill.
        var held = Knowledge.Grade("landed", "RNAV 09 SOUTH", 0, 5, ["TakingOff", "Climbing", "Holding", "OnRoute", "OnFinal", "Stopped"]);
        Assert.True(held["hold"] && held["sequencing"]);
        Assert.True(Knowledge.Grade("landed", "VISUAL 09", 0, 5, ["TakingOff", "Climbing", "OnRoute", "OnFinal", "Stopped"])["land-visual"]);
        // Its own plan (the rig's): ending on F09 it's an RNAV landing, on F09V a visual one.
        Assert.True(Knowledge.Grade("landed", "FILED KSC-OH KSC-N1 KSC-N2 KSC-N3 F09", 0, 4, ["TakingOff", "OnRoute", "OnFinal", "Stopped"])["land-rnav"]);
        Assert.True(Knowledge.Grade("landed", "FILED KSC-OH KSC-VN1 KSC-VN2 KSC-VN3 F09V", 0, 4, ["TakingOff", "OnRoute", "OnFinal", "Stopped"])["land-visual"]);

        // A crash on final: the landing failed, the takeoff didn't.
        var crash = Knowledge.Grade("crashed", "RNAV 09 NORTH", 0, null, ["TakingOff", "Climbing", "OnRoute", "OnFinal", "Crashed"]);
        Assert.False(crash["land-rnav"]);
        Assert.True(crash["takeoff"]);
        Assert.Empty(Knowledge.Grade("ended in the air", null, 0, null, ["Cruising", "NotFlying"])); // joined in the air: nothing
    }

    [Fact]
    public void Training_drills_and_new_planes_are_skills_too()
    {
        string[] visual = ["TakingOff", "Climbing", "Cruising", "OnRoute", "OnFinal", "Stopped"];
        var fast = Knowledge.Grade("landed", "VISUAL 09", 0, 30, visual, maxSpeed: 226);
        Assert.True(fast["fly-fast"]);
        Assert.False(fast.ContainsKey("fly-high"));
        Assert.False(Knowledge.Grade("crashed", null, 0, null, ["TakingOff", "Climbing", "Crashed"], maxHeight: 6100)["fly-high"]); // didn't get back down
        Assert.False(Knowledge.Grade("landed", "VISUAL 09", 0, 5, visual, maxHeight: 4300)["fly-high"]); // tried, not high enough

        var k = new Knowledge();
        Assert.False(k.AddFlight("Alpha", "Aeris 3A", "landed", "VISUAL 09", 0, 5, 1000, 5, 1, visual, T0).Skills.ContainsKey("new-craft")); // its first plane
        Assert.True(k.AddFlight("Alpha", "Stearwing A300", "landed", "VISUAL 09", 0, 5, 1000, 8, 1, visual, T0).Skills["new-craft"]);
        Assert.False(k.AddFlight("Alpha", "Stearwing A300", "landed", "VISUAL 09", 0, 5, 1000, 8, 1, visual, T0).Skills.ContainsKey("new-craft")); // not new now
        Assert.Equal(9, Knowledge.Tree.Count);
    }

    [Fact]
    public void Lessons_need_evidence_and_merge_when_they_say_the_same()
    {
        var k = new Knowledge();
        var flight = k.AddFlight("Alpha", "Aeris 3A", "landed", "RNAV 09 SOUTH", 1, 9, 1300, 20, 4, RnavLanding, T0);
        var bravo = k.AddFlight("Bravo", "Aeris 3A", "landed", "RNAV 09 SOUTH", 0, 9, 1100, 5, 2, RnavLanding, T0);

        Assert.Contains("must cite", k.AddLesson("Alpha", "land-rnav", "Slow to 85 m/s before F09 to avoid a go-around.", [], T0).Problem);
        Assert.Contains("must cite", k.AddLesson("Alpha", "land-rnav", "Slow to 85 m/s before F09 to avoid a go-around.", [bravo.Id], T0).Problem); // not Alpha's
        Assert.Contains("must cite", k.AddLesson("Alpha", "hold", "Hold tighter turns at KSC-HS please.", [flight.Id], T0).Problem); // didn't hold
        Assert.Contains("no skill", k.AddLesson("Alpha", "warp", "Warp is fun but not allowed.", [flight.Id], T0).Problem);
        Assert.Contains("10 to", k.AddLesson("Alpha", "land-rnav", "short", [flight.Id], T0).Problem);

        var (lesson, _) = k.AddLesson("Alpha", "land-rnav", "Slow to 85 m/s before F09 to avoid a go-around.", [flight.Id], T0);
        Assert.NotNull(lesson);
        // Bravo says much the same from its own flight: confirmed, not a second lesson.
        var (same, _) = k.AddLesson("Bravo", "land-rnav", "Slow to 85 m/s before F09 to avoid a go-around next time.", [bravo.Id], T0.AddHours(1));
        Assert.Equal(lesson!.Id, same!.Id);
        Assert.Equal(1, same.Confirmations);
        Assert.Equal([flight.Id, bravo.Id], same.Evidence);
        Assert.Single(k.Lessons);

        // Unconfirmed lessons expire; confirmed ones stay.
        var (other, _) = k.AddLesson("Alpha", "takeoff", "Rotate at 60 m/s with the Aeris 3A on the runway.", [flight.Id], T0);
        k.Expire(T0 + Knowledge.Expiry + TimeSpan.FromHours(2));
        Assert.DoesNotContain(k.Lessons, l => l.Id == other!.Id);
        Assert.Contains(k.Lessons, l => l.Id == lesson.Id);
    }

    [Fact]
    public void Skills_unlock_from_good_flights_by_any_agent_and_survive_a_restart()
    {
        var k = new Knowledge();
        Assert.True(k.Unlocked("takeoff"));
        Assert.False(k.Unlocked("land-rnav")); // needs route and land-visual first
        for (var i = 0; i < Knowledge.UnlockAfter; i++)
        {
            k.AddFlight(i % 2 == 0 ? "Alpha" : "Bravo", "Aeris 3A", "landed", "RNAV 09 SOUTH", 0, 9, 1200, 5, 2, RnavLanding, T0);
            k.AddFlight("Alpha", "Aeris 3A", "landed", "VISUAL 09", 0, 5, 1200, 5, 2, ["TakingOff", "Climbing", "OnRoute", "OnFinal", "Stopped"], T0);
        }
        Assert.True(k.Unlocked("land-rnav"));
        Assert.Equal(new Proficiency(2, 2), k.Of("land-rnav", "Alpha"));
        Assert.Equal(new Proficiency(3, 3), k.Of("land-rnav"));
        k.AddLesson("Alpha", "land-rnav", "Slow to 85 m/s before F09 to avoid a go-around.", [1], T0);

        var path = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "knowledge-" + Guid.NewGuid().ToString("N"), "knowledge.json");
        k.Save(path);
        var back = Knowledge.Load(path);
        Assert.Equal(k.Flights.Count, back.Flights.Count);
        Assert.Equal(k.Of("land-rnav"), back.Of("land-rnav"));
        Assert.Single(back.Lessons);
        Assert.Equal(k.Flights.Count + 1, back.AddFlight("C", "", "crashed", null, 0, 1, null, null, null, [], T0).Id); // ids go on

        File.WriteAllText(path, "{ not json");
        Assert.Empty(Knowledge.Load(path).Flights); // unreadable: starts empty, the file kept as .bad
        Assert.True(File.Exists(path + ".bad"));
    }

    [Fact]
    public void The_tower_records_flights_takes_lessons_and_answers_what_an_agent_knows()
    {
        using var server = new TowerServer(0, () => T0);
        var flight = server.Answer("POST", "/v1/flight", JsonSerializer.Serialize(new
        {
            callSign = "Alpha", craft = "Aeris 3A", result = "landed", approach = "RNAV 09 SOUTH", goArounds = 0, minutes = 8.3,
            landing = new { downRunway = 1329, offCenterline = 8, headingError = 4 }, phases = RnavLanding,
        }));
        Assert.Equal(200, flight.Status);
        var id = JsonDocument.Parse(flight.Body).RootElement.GetProperty("flight").GetProperty("id").GetInt64();
        Assert.True(JsonDocument.Parse(flight.Body).RootElement.GetProperty("flight").GetProperty("skills").GetProperty("land-rnav").GetBoolean());

        Assert.Equal(422, server.Answer("POST", "/v1/lesson", """{"callSign":"Alpha","skill":"land-rnav","text":"No evidence here at all.","evidence":[]}""").Status);
        Assert.Equal(200, server.Answer("POST", "/v1/lesson", JsonSerializer.Serialize(new { callSign = "Alpha", skill = "land-rnav", text = "Brakes on below 60 m/s keep it straight.", evidence = new[] { id } })).Status);

        var known = JsonDocument.Parse(server.Answer("POST", "/v1/knowledge", """{"callSign":"Alpha"}""").Body).RootElement;
        var rnav = known.GetProperty("skills").EnumerateArray().First(s => s.GetProperty("id").GetString() == "land-rnav");
        Assert.Equal(1, rnav.GetProperty("mine").GetProperty("good").GetInt32());
        Assert.False(rnav.GetProperty("unlocked").GetBoolean());
        Assert.Equal("Brakes on below 60 m/s keep it straight.", rnav.GetProperty("lessons")[0].GetProperty("text").GetString());
        Assert.Equal(id, known.GetProperty("flights")[0].GetProperty("id").GetInt64());

        Assert.Equal(400, server.Answer("POST", "/v1/flight", """{"callSign":"Alpha"}""").Status); // no result
        Assert.True(server.DeleteLesson(1));
        Assert.Empty(server.Knowledge.Lessons);
    }

    private static PlaneReport At(string callSign, double along, double cross, double heading = 90) =>
        new(callSign, Ksc.At("", along, cross, 900, 0).Latitude, Ksc.At("", along, cross, 900, 0).Longitude, 900, heading, 120, "Flying");

    [Fact]
    public void Close_in_and_alone_a_plane_gets_the_short_visual_or_what_it_asked_for()
    {
        // The owner saw agents fly far out for an RNAV route: close in with nobody waiting, the short visual is quicker.
        var tower = new Tower.Tower();
        tower.Report(At("Alpha", 3000, 1500), T0);
        var visual = tower.Say("Alpha", "request-approach", "request approach, KSC runway 09", T0).Single(m => m.Kind == "clearance");
        Assert.Equal(Ksc.VisualName, visual.Route!.Name);
        Assert.Equal(["KSC-VS1", "KSC-VS2", "KSC-VS3", "F09V"], visual.Route.Fixes.Select(f => f.Name));
        Assert.All(visual.Route.Fixes, f => Assert.InRange(f.Altitude, 300, 700)); // Wingman flies fixes from 300 m (audit 15)
        Assert.True(visual.Route.Fixes[0].Latitude < Ksc.ThresholdLatitude); // its own side (south)

        // Far out: an RNAV route. Asking for one by name (alone): that one.
        var far = new Tower.Tower();
        far.Report(At("Alpha", 20_000, 2000), T0);
        Assert.Equal("RNAV 09 SOUTH", far.Say("Alpha", "request-approach", "", T0).Single(m => m.Kind == "clearance").Route!.Name);
        var asked = new Tower.Tower();
        asked.Report(At("Alpha", 3000, 1500), T0);
        Assert.Equal("RNAV 09 NORTH", asked.Say("Alpha", "request-approach", "request RNAV 09 NORTH", T0).Single(m => m.Kind == "clearance").Route!.Name);

        // Traffic: with another plane still waiting behind it, the next one gets the published route, not its wish.
        var busy = new Tower.Tower();
        busy.Report(At("Alpha", 3000, 1500), T0);
        busy.Report(At("Bravo", 3000, -2000), T0);
        busy.Report(At("Charlie", 5000, -3000), T0);
        busy.Say("Alpha", "request-approach", "visual", T0);
        busy.Say("Bravo", "request-approach", "visual", T0); // holds
        busy.Say("Charlie", "request-approach", "visual", T0); // holds behind Bravo
        busy.Report(At("Charlie", 5000, -3000), T0.AddSeconds(29));
        busy.Report(At("Alpha", 900, 90) with { Situation = "Landed", Speed = 0 }, T0.AddSeconds(30));
        var next = busy.Report(At("Bravo", 3000, -2000), T0.AddSeconds(31));
        Assert.Equal("RNAV 09 NORTH", next.Single(m => m.Kind == "clearance").Route!.Name);
    }
}
