// LUNA EVO · LunaEvo.MissionControl.Tests/MapPageTests.cs
// [Test] 2.2: the solar system map page: bodies from GameData, vessels from the server's save, clicking and focusing.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using LunaEvo.MissionControl.Core.Server;
using LunaEvo.MissionControl.Core.Tests;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl.Tests;

public class MapPageTests
{
    [AvaloniaFact]
    public void Map_shows_vessels_on_their_orbits_and_clicking_one_shows_its_details()
    {
        using var ui = new UiHarness();
        TestData.AddOrbitingVessels(ui.ServerDirectory);
        var ksp = TestData.FakeKsp(withSettings: false); // never the real KSP's GameData
        Directory.CreateDirectory(Path.Combine(ksp, "GameData", "Pack"));
        File.WriteAllText(Path.Combine(ksp, "GameData", "Pack", "Body.cfg"),
            "@Kopernicus:FOR[Pack]\n{\n Body\n {\n  name = Testus\n  Properties { radius = 300000\n gravParameter = 1e11 }\n" +
            "  Orbit { referenceBody = Kerbin\n semiMajorAxis = 30000000 }\n }\n}\n");
        ui.Window.UpdateSettings(s => s with { KspFolder = ksp });

        ui.OpenTab(MainWindow.MapKey);
        var map = ui.Window.Map;
        Assert.Equal(18, map.System.Bodies.Count);
        Assert.Equal("18 bodies, 1 added or changed by planet packs in your GameData.", ui.Find<TextBlock>("map/packs").Text);
        Assert.Equal(4, map.Vessels.Count);
        Assert.Contains("4 vessel(s) on the server", ui.Find<TextBlock>("map/time").Text);
        Assert.Equal("Sun", map.Canvas.FocusBody);
        Assert.NotNull(ui.TryFind<Control>("map/vessel/s1")); // the rock orbits the Sun
        Assert.Null(ui.TryFind<Control>("map/vessel/k1"));

        // Kerbin: its moons (and the pack's) and the station on its orbit; the Mun lander belongs to the Mun.
        map.Focus("Kerbin");
        Draw(map.Canvas);
        Assert.Equal(["Minmus", "Mun", "Testus"], map.Canvas.BodyMarks.Select(m => m.Body).Where(b => b != "Kerbin").Order());
        var station = Assert.Single(map.Canvas.VesselMarks);
        Assert.Equal("k1", station.Vessel.Id);
        Assert.NotNull(ui.TryFind<Control>("map/vessel/k1"));
        Assert.Null(ui.TryFind<Control>("map/vessel/m1"));

        map.Canvas.Click(station.At + new Point(3, 3));
        ui.Pump();
        Assert.Equal("Kerbin Station", ui.Find<TextBlock>("map/selected").Text);
        Assert.True(Visible(ui, "Crew: Jebediah Kerman"));

        // Clicking a moon focuses it; the landed vessel is listed with its surface position.
        var mun = map.Canvas.BodyMarks.First(m => m.Body == "Mun");
        map.Canvas.Click(mun.At);
        ui.Pump();
        Assert.Equal("Mun", map.Canvas.FocusBody);
        ui.Click(ui.Find<Button>("map/show/m1"));
        Assert.True(Visible(ui, "On the surface at 3.50°, 45.00°"));
        ui.Click(ui.Find<Button>("map/up"));
        Assert.Equal("Kerbin", map.Canvas.FocusBody);

        // Hiding asteroids takes the rock off the map.
        map.Focus("Sun");
        ui.Find<CheckBox>("map/rocks").IsChecked = false;
        ui.Pump();
        Assert.DoesNotContain(map.Canvas.Vessels, v => v.Id == "s1");
        Assert.Null(ui.TryFind<Control>("map/vessel/s1"));
    }

    [AvaloniaFact]
    public void Map_goes_live_from_the_running_servers_website()
    {
        using var ui = new UiHarness(fakeServer: true);
        TestData.AddOrbitingVessels(ui.ServerDirectory);
        ui.Window.UpdateSettings(s => s with { KspFolder = TestData.FakeKsp(withSettings: false) });
        // The server now has the station higher than the save says, and a rocket launched since the last save.
        ui.Web.Text("http://127.0.0.1:8900/", """
            [{"CurrentState":{"StartTime":"2026-10-13T03:59:54","CurrentPlayers":["Jebediah"],"CurrentVessels":[
              {"Id":"k1","Name":"Kerbin Station","Type":"Station","Alt":400000,"SemiMajorAxis":1000000,"Eccentricity":0,"ReferenceBody":1},
              {"Id":"0b6c9a5e-0000-4000-8000-000000000042","Name":"New Rocket","Type":"Ship","Alt":80000,"SemiMajorAxis":690000,"Eccentricity":0.01,"ReferenceBody":1}
            ],"Subspaces":[],"BytesUsed":1}}]
            """);

        ui.OpenTab(MainWindow.MapKey);
        Assert.StartsWith("From the save (the server is stopped)", ui.Find<TextBlock>("map/source").Text);
        Assert.False(ui.Find<Button>("map/goLive").IsVisible); // the fixture's website is on

        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");
        ui.Await(ui.Window.Players!.PollAsync());
        ui.OpenTab(MainWindow.MapKey);
        var map = ui.Window.Map;
        Assert.StartsWith("Live from the running server", ui.Find<TextBlock>("map/source").Text);
        Assert.Equal(1_000_000, map.Vessels.Single(v => v.Id == "k1").Orbit!.Sma);
        Assert.Equal(["Jebediah Kerman"], map.Vessels.Single(v => v.Id == "k1").Crew); // the rest from the save
        Assert.Contains(map.Vessels, v => v.Name == "New Rocket");
        Assert.DoesNotContain(map.Vessels, v => v.Id == "d1"); // not on the server any more
        map.Focus("Kerbin");
        Assert.NotNull(ui.TryFind<Control>("map/vessel/0b6c9a5e-0000-4000-8000-000000000042"));
    }

    [AvaloniaFact]
    public void Map_installs_the_server_plugin_and_shows_who_flies_what()
    {
        using var ui = new UiHarness(fakeServer: true);
        TestData.AddOrbitingVessels(ui.ServerDirectory);
        ui.Window.UpdateSettings(s => s with { KspFolder = TestData.FakeKsp(withSettings: false) });
        ui.OpenTab(MainWindow.MapKey);
        var button = ui.Find<Button>("map/plugin");
        Assert.True(button.IsVisible); // the fixture server is LMP 0.29.2
        Assert.StartsWith("Install the LUNA EVO server plugin", button.Content as string);

        ui.Await(ui.Window.Map.TogglePluginAsync());
        Assert.Contains("Install the LUNA EVO server plugin?", ui.DialogsShown);
        var dll = Core.Ops.ServerPluginInstaller.PathFor(ui.ServerDirectory);
        Assert.Equal(MapPage.PluginBytes(), File.ReadAllBytes(dll));
        Assert.Equal(MapPage.HarmonyBytes(), File.ReadAllBytes(Core.Ops.ServerPluginInstaller.HarmonyPathFor(ui.ServerDirectory)));
        Assert.Equal("Remove the LUNA EVO server plugin", button.Content as string);

        // What the plugin writes while the server runs: Jebediah is flying the station, higher than saved.
        Directory.CreateDirectory(Path.Combine(ui.ServerDirectory, "LunaEvo"));
        File.WriteAllText(Path.Combine(ui.ServerDirectory, "LunaEvo", "live.json"), $$"""
            {"Format":1,"Plugin":"1.0.0","Lmp":"0.29.2","WrittenUtc":"{{DateTime.UtcNow:O}}","Players":[{"Name":"Jebediah","Subspace":1,"Vessel":"k1"}],
             "Vessels":[{"Id":"k1","Body":1,"Lat":0,"Lon":0,"Alt":300000,"Orbit":[0,0,900000,0,0,0,0,1],"Landed":false,"Splashed":false,"FlownBy":"Jebediah"}]}
            """);
        var map = ui.Window.Map;
        map.Refresh();
        ui.Pump();
        Assert.StartsWith("Live from the LUNA EVO server plugin", ui.Find<TextBlock>("map/source").Text);
        Assert.EndsWith("1 player(s), 1 flying.", ui.Find<TextBlock>("map/source").Text);
        Assert.Equal(900_000, map.Vessels.Single(v => v.Id == "k1").Orbit!.Sma);
        map.Focus("Kerbin");
        ui.Click(ui.Find<Button>("map/show/k1"));
        Assert.Equal("Flown by Jebediah", ui.Find<TextBlock>("map/flownBy").Text);

        // LMP keeps its plugins open while it runs: changing it then is refused, with the reason.
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");
        ui.Await(map.TogglePluginAsync());
        Assert.Contains("Stop the server first", ui.DialogsShown);
        Assert.True(File.Exists(dll));
    }

    [AvaloniaFact]
    public void Map_offers_to_turn_the_website_on_for_a_live_map()
    {
        using var ui = new UiHarness();
        ui.Session.SetValue(LmpRules.Website, "EnableWebsite", "false");
        ui.OpenTab(MainWindow.MapKey);
        Assert.StartsWith("From the save, which LMP writes every few minutes", ui.Find<TextBlock>("map/source").Text);
        ui.Click(ui.Find<Button>("map/goLive"));
        Assert.Equal("true", ui.Session.GetValue(LmpRules.Website, "EnableWebsite"));
        Assert.Equal("127.0.0.1", ui.Session.GetValue(LmpRules.Website, "ListenAddress"));
        Assert.StartsWith("Website turned on for this PC only", ui.Find<TextBlock>("map/source").Text);
    }

    [Fact]
    public void Map_formats_distances_and_times_for_people()
    {
        Assert.Equal("85 km", MapPage.Distance(85_000));
        Assert.Equal("1.2 km", MapPage.Distance(1_234));
        Assert.Equal("12.5 Mm", MapPage.Distance(12_500_000));
        Assert.Equal("1 d 2 h", MapPage.Duration(21_600 + 7_200));
        Assert.Equal("in orbit", MapPage.Situation("ORBITING"));
        Assert.Equal("on the pad", MapPage.Situation("PRELAUNCH"));
    }

    /// <summary>Headless windows don't paint: render the map once into a bitmap so its marks exist.</summary>
    private static void Draw(MapCanvas canvas)
    {
        canvas.Measure(new Size(900, 560));
        canvas.Arrange(new Rect(0, 0, 900, 560));
        using var bitmap = new RenderTargetBitmap(new PixelSize(900, 560));
        bitmap.Render(canvas);
    }

    private static bool Visible(UiHarness ui, string text) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(ui.Window).OfType<TextBlock>().Any(t => t.Text == text && t.IsEffectivelyVisible);
}
