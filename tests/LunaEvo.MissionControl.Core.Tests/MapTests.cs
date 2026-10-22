// LUNA EVO · LunaEvo.MissionControl.Core.Tests/MapTests.cs
// [Test] The solar system map's model: Kepler orbits against known answers, the stock system, planet packs
//        (Kopernicus configs) and the server's universe clock.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.MissionControl.Core.Map;
using LunaEvo.MissionControl.Core.Ops;

namespace LunaEvo.MissionControl.Core.Tests;

public class MapTests
{
    private const double KerbinMu = 3.5316e12;

    [Fact]
    public void Stock_periods_match_ksp()
    {
        var system = SolarSystem.Stock();
        Assert.Equal(17, system.Bodies.Count);
        // KSP's own numbers: Kerbin's year 9,203,545 s, the Mun's month 138,984 s.
        Assert.InRange(system.Find("Kerbin")!.Orbit!.Period(system.Star.Mu)!.Value, 9_202_000, 9_205_000);
        Assert.InRange(system.Find("Mun")!.Orbit!.Period(KerbinMu)!.Value, 138_900, 139_100);
        Assert.Equal("Kerbin", system.Find("Mun")!.Parent);
        Assert.Equal("Sun", system.Find("Kerbol")!.Name);
        Assert.Equal("Kerbin", system.ForVessel("Squad/Kerbin", -1)!.Name);
        Assert.Equal("Duna", system.ForVessel(null, 6)!.Name); // by the stock index when there's no IDENT
        Assert.InRange(system.Find("Mun")!.SphereOfInfluence(system), 2.42e6, 2.44e6); // KSP: 2,429,559 m
    }

    [Fact]
    public void A_circular_orbit_goes_a_quarter_turn_in_a_quarter_period()
    {
        var orbit = new Orbit(1_000_000, 0, 0, 0, 0, 0, 0);
        var period = orbit.Period(KerbinMu)!.Value;
        var start = orbit.PositionAt(0, KerbinMu);
        var quarter = orbit.PositionAt(period / 4, KerbinMu);
        Assert.Equal(1_000_000, start.X, 1);
        Assert.Equal(0, start.Y, 1);
        Assert.Equal(0, quarter.X, 1);
        Assert.Equal(1_000_000, quarter.Y, 1);
        Assert.Equal(start.X, orbit.PositionAt(period * 3, KerbinMu).X, 1); // back after whole turns
    }

    [Fact]
    public void An_ellipse_is_closest_at_periapsis_and_farthest_at_apoapsis()
    {
        var orbit = new Orbit(1_000_000, 0.5, 30, 45, 60, 0, 0);
        var period = orbit.Period(KerbinMu)!.Value;
        Assert.Equal(500_000, orbit.PositionAt(0, KerbinMu).Length, 1);           // a(1-e) at mean anomaly 0
        Assert.Equal(1_500_000, orbit.PositionAt(period / 2, KerbinMu).Length, 1); // a(1+e) half a period later
        var path = orbit.Path(points: 90);
        Assert.Equal(91, path.Count);
        Assert.Equal(path[0].Length, path[^1].Length, 3); // closed
        Assert.All(path, p => Assert.InRange(p.Length, 499_999, 1_500_001));
        Assert.NotEqual(0, orbit.PositionAt(period / 4, KerbinMu).Z, 1); // inclined: out of the plane
    }

    [Fact]
    public void An_escape_trajectory_is_an_open_arc()
    {
        var orbit = new Orbit(-2_000_000, 1.5, 0, 0, 0, 0, 0); // KSP stores escapes with a negative SMA
        Assert.True(orbit.IsOpen);
        Assert.Null(orbit.Period(KerbinMu));
        Assert.Equal(1_000_000, orbit.PositionAt(0, KerbinMu).Length, 1); // periapsis |a|(e-1)
        Assert.True(orbit.PositionAt(50_000, KerbinMu).Length > 1_000_000);
        var path = orbit.Path(maxRadius: 10_000_000);
        Assert.All(path, p => Assert.True(p.Length <= 10_000_001));
        Assert.NotEqual(path[0].Y, path[^1].Y); // in on one side, out on the other
    }

    [Fact]
    public void Reads_planet_packs_new_bodies_moves_and_removals()
    {
        var gameData = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "gamedata-" + Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(gameData, "OPM"));
        File.WriteAllText(Path.Combine(gameData, "OPM", "Sarnus.cfg"), """
            @Kopernicus:FOR[OPM]
            {
                Body
                {
                    name = Sarnus
                    Template { name = Jool }
                    Properties { radius = 5300000
                                 geeASL = 0.298 }
                    Orbit { referenceBody = Sun
                            semiMajorAxis = 125798522368
                            eccentricity = 0.0534
                            inclination = 2.02
                            color = 0.866,0.722,0.529,1 }
                }
            }
            """);
        File.WriteAllText(Path.Combine(gameData, "OPM", "Eeloo.cfg"), """
            @Kopernicus:AFTER[OPM]
            {
                %Body[Eeloo]
                {
                    %Orbit
                    {
                        %referenceBody = Sarnus
                        semiMajorAxis = 19105978
                        eccentricity = 0.0034
                    }
                }
                !Body[Dres] {}
            }
            """);
        File.WriteAllText(Path.Combine(gameData, "OPM", "Other.cfg"), "PART { name = notABody }"); // skipped

        var system = SolarSystem.Load(gameData);
        var sarnus = system.Find("Sarnus")!;
        Assert.Equal("Sun", sarnus.Parent);
        Assert.Equal(5_300_000, sarnus.Radius);
        Assert.Equal(0.298 * 9.80665 * 5_300_000.0 * 5_300_000.0, sarnus.Mu, sarnus.Mu * 1e-9);
        Assert.Equal("#DCB886", sarnus.Color);
        Assert.Equal(0.0534, sarnus.Orbit!.Ecc);
        var eeloo = system.Find("Eeloo")!;
        Assert.Equal("Sarnus", eeloo.Parent); // moved, after Sarnus exists (AFTER runs after FOR)
        Assert.Equal(19_105_978, eeloo.Orbit!.Sma);
        Assert.Equal(6.15, eeloo.Orbit.Inc); // not in the edit: kept from stock
        Assert.Null(system.Find("Dres"));
        Assert.Equal(["Eeloo", "Sarnus"], system.FromPacks);
        Assert.Contains("Eeloo", system.ChildrenOf("Sarnus").Select(b => b.Name));
        Assert.Equal(17, SolarSystem.Load(null).Bodies.Count); // no GameData: stock
    }

    [Fact]
    public void A_broken_pack_cant_loop_bodies_or_take_the_star_away()
    {
        var gameData = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "gamedata-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(gameData, "Broken.cfg"), """
            @Kopernicus
            {
                Body { name = Alpha
                       Orbit { referenceBody = Beta
                               semiMajorAxis = 1000000 } }
                Body { name = Beta
                       Orbit { referenceBody = Alpha
                               semiMajorAxis = 1000000 } }
                @Body[Sun] { Orbit { referenceBody = Kerbin
                                     semiMajorAxis = 5 } }
                !Body[Sun] {}
                @Body[Kerbin] { Orbit { referenceBody = Kerbin } }
            }
            """);
        var system = SolarSystem.Load(gameData);
        Assert.Equal("Sun", system.Star.Name);
        Assert.Null(system.Star.Parent);
        Assert.Null(system.Find("Alpha"));
        Assert.Null(system.Find("Beta"));
        Assert.Null(system.Find("Kerbin")); // orbits itself
        Assert.Null(system.Find("Mun"));    // so its moons can't be placed either
        Assert.Equal(14, system.Bodies.Count);
        Assert.NotNull(system.PositionOf("Duna", "Sun", 0));
    }

    [Fact]
    public void Places_a_moon_relative_to_any_ancestor()
    {
        var system = SolarSystem.Stock();
        var munFromKerbin = system.PositionOf("Mun", "Kerbin", 1000)!.Value;
        var kerbinFromSun = system.PositionOf("Kerbin", "Sun", 1000)!.Value;
        var munFromSun = system.PositionOf("Mun", "Sun", 1000)!.Value;
        Assert.Equal(12_000_000, munFromKerbin.Length, 1);
        Assert.Equal(kerbinFromSun.X + munFromKerbin.X, munFromSun.X, 1);
        Assert.Null(system.PositionOf("Mun", "Duna", 0)); // Duna isn't an ancestor
    }

    [Fact]
    public void Universe_time_is_seconds_since_start_plus_the_newest_subspace()
    {
        var server = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "clock-" + Guid.NewGuid().ToString("N"))).FullName;
        var universe = Directory.CreateDirectory(Path.Combine(server, "Universe")).FullName;
        File.WriteAllText(Path.Combine(universe, "StartTime.txt"), "#comment\n2026-10-13T03:59:54\n");
        File.WriteAllText(Path.Combine(universe, "Subspace.txt"), "#comment\n0:100.5\n99:1139181.1458405056\n");
        var now = UniverseClock.Now(server, new DateTime(2026, 10, 13, 4, 0, 54, DateTimeKind.Utc))!.Value;
        Assert.Equal(60 + 1139181.1458405056, now, 3);
        Assert.Equal("Year 1, day 53, 04:27", UniverseClock.Kerbin(now));
        Assert.Null(UniverseClock.Now(Path.Combine(server, "nothing")));
    }

    [Fact]
    public void Reads_a_vessels_orbit_body_and_surface_position()
    {
        var vessel = UniverseVessels.Parse("""
            pid = 1
            name = Mun Lander
            type = Lander
            sit = LANDED
            lat = -1.5
            lon = 23.25
            ORBIT
            {
                SMA = 1307799.5
                ECC = 0.384
                INC = 30.2
                LPE = 92.2
                LAN = 311.05
                MNA = 5.5
                EPH = 702306.7
                REF = 2
                IDENT = Squad/Mun
            }
            """, "a.txt");
        Assert.Equal(new Orbit(1307799.5, 0.384, 30.2, 311.05, 92.2, 5.5, 702306.7), vessel.Orbit);
        Assert.Equal("Squad/Mun", vessel.BodyName);
        Assert.Equal((-1.5, 23.25), vessel.Surface);
        Assert.True(vessel.IsLanded);
    }

    [Fact]
    public void Reads_live_orbits_from_lmps_website()
    {
        var json = File.ReadAllText(Path.Combine(TestPaths.Fixtures, "Web", "serverinfo.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var rock = Live.ServerInfoClient.Parse(doc.RootElement).Vessels.Single(v => v.Name == "Ast. GHI-046");
        Assert.Equal(new Orbit(228902176.0, 0.38, 240.6, 300.5, 193.5, 104.9, 1.0), rock.Orbit);
        Assert.Equal((-56.9, 146.8), (rock.Lat, rock.Lon));
        using var broken = System.Text.Json.JsonDocument.Parse(
            """[{"CurrentState":{"CurrentVessels":[{"Id":"x","SemiMajorAxis":"big","Eccentricity":-1},{"Id":"y","SemiMajorAxis":0,"Eccentricity":0}]}}]""");
        Assert.All(Live.ServerInfoClient.Parse(broken.RootElement).Vessels, v => Assert.Null(v.Orbit));
    }

    [Fact]
    public void Live_data_moves_saved_vessels_adds_launches_and_drops_removed_ones()
    {
        const string station = "0b6c9a5e-0000-4000-8000-000000000001", lander = "0b6c9a5e-0000-4000-8000-000000000002", gone = "0b6c9a5e-0000-4000-8000-000000000003";
        var oldOrbit = new Orbit(700_000, 0, 0, 0, 0, 0, 0);
        UniverseVessel Saved(string id, string name, string sit, int body, string ident, double alt) =>
            new(id, name, "Ship", sit, body, 5, ["Jebediah Kerman"], id + ".txt") { Orbit = oldOrbit, BodyName = ident, Altitude = alt, Surface = (1, 2) };
        var saved = new[]
        {
            Saved(station, "Station", "ORBITING", 1, "Squad/Kerbin", 100_000),
            Saved(lander, "Lander", "LANDED", 21, "Sarnus", 1500), // a pack body: the save says what 21 means
            Saved(gone, "Gone", "ORBITING", 1, "Squad/Kerbin", 100_000),
        };
        var newOrbit = new Orbit(800_000, 0.1, 5, 0, 0, 1, 50);
        var live = new[]
        {
            new Live.VesselSummary(station, "Station", "Ship", 150_000, 1) { Orbit = newOrbit },
            new Live.VesselSummary(lander, "Lander", "Ship", 1520, 21) { Lat = 3, Lon = 4 },            // still on the ground
            new Live.VesselSummary("0b6c9a5e-0000-4000-8000-000000000009", "New Rocket", "Ship", 5000, 21) { Orbit = newOrbit },
            new Live.VesselSummary(" ", "Junk", "Ship", 0, 1), // no id
            new Live.VesselSummary(station.ToUpperInvariant(), "Station", "Ship", 0, 1), // the same one again
        };

        var merged = LiveVessels.Merge(saved, live);
        Assert.Equal(3, merged.Count); // "Gone" was removed on the server; junk ignored
        var s = merged.Single(v => v.Id == station);
        Assert.Equal(newOrbit, s.Orbit);
        Assert.Equal(["Jebediah Kerman"], s.Crew); // kept from the save
        var l = merged.Single(v => v.Id == lander);
        Assert.True(l.IsLanded);
        Assert.Equal((3.0, 4.0), l.Surface);
        Assert.Equal(oldOrbit, l.Orbit); // no live orbit: the saved one stays
        var n = merged.Single(v => v.Name == "New Rocket");
        Assert.Equal("Sarnus", n.BodyName);
        Assert.Equal(0, n.PartCount);

        // A landed vessel that is now 2 km up has taken off.
        var flying = LiveVessels.Merge(saved, [new Live.VesselSummary(lander, "Lander", "Ship", 3500, 21) { Orbit = newOrbit }]);
        Assert.False(flying.Single().IsLanded);
        Assert.Equal(newOrbit, flying.Single().Orbit);
    }

    [Fact]
    public void Odd_orbits_from_save_files_still_give_finite_positions()
    {
        // Values a save (or a broken mod) can hold: parabolic, nearly so, huge, tiny, far from the epoch.
        var random = new Random(42);
        double[] eccs = [0, 0.5, 0.9999999, 1, 1.0000001, 3, 1e6];
        double[] smas = [1, 600_000, -600_000, 1e15, -1e15, 0.5, 0];
        foreach (var ecc in eccs)
            foreach (var sma in smas)
                for (var i = 0; i < 5; i++)
                {
                    var orbit = new Orbit(sma, ecc, random.NextDouble() * 360, random.NextDouble() * 360, random.NextDouble() * 360,
                        random.NextDouble() * 100 - 50, random.NextDouble() * 1e9);
                    var time = random.NextDouble() * 1e12;
                    Assert.True(Finite(orbit.PositionAt(time, KerbinMu)), $"{orbit} at {time}");
                    Assert.All(orbit.Path(points: 30, maxRadius: 1e9), p => Assert.True(Finite(p), $"{orbit} path"));
                }

        static bool Finite(Point3 p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
    }
}
