// LUNA EVO · LunaEvo.MissionControl.Tests/MapPageTests.cs
// [Test] 2.2: the solar system map page: bodies from GameData, vessels from the server's save, clicking and focusing.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using LunaEvo.MissionControl.Core.Tests;

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
