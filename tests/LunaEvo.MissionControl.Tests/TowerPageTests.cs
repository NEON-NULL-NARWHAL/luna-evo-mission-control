// LUNA EVO · LunaEvo.MissionControl.Tests/TowerPageTests.cs
// [Test] The KSC tower page: start and stop the tower, two agents on its channel (one cleared, one holding), and what
//        the page shows of them. The agents talk through TowerServer.Answer (no network needed).
// Used by: dotnet test
// Guide:   Meat and Potatoes/28-Tower.md

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.MissionControl.Core.Tower;

namespace LunaEvo.MissionControl.Tests;

public class TowerPageTests
{
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static string Report(string callSign, double along, double cross) =>
        JsonSerializer.Serialize(new
        {
            callSign,
            latitude = Ksc.At("", along, cross, 900, 0).Latitude,
            longitude = Ksc.At("", along, cross, 900, 0).Longitude,
            altitude = 900,
            heading = 90,
            speed = 120,
            situation = "Flying",
        });

    /// <summary>A row's description (Ui.Row gives it the sublabel class).</summary>
    private static string Description(UiHarness ui, string id)
    {
        ui.Pump();
        return ui.Find<Control>(id).GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("sublabel")).Text ?? "";
    }

    [AvaloniaFact]
    public void The_tower_page_shows_two_agents_one_cleared_and_one_holding()
    {
        using var ui = new UiHarness(openServer: false);
        ui.OpenTab(MainWindow.TowerKey);
        Assert.StartsWith("Off.", Description(ui, "tower/status"));

        ui.Window.Tower.Start(FreePort());
        var server = ui.Window.Tower.Server!;
        Assert.Equal(200, server.Answer("POST", "/v1/report", Report("Alpha", 2000, -1000)).Status);
        server.Answer("POST", "/v1/report", Report("Bravo", 3000, 2000));
        server.Answer("POST", "/v1/say", """{"callSign":"Alpha","kind":"request-approach","text":""}""");
        server.Answer("POST", "/v1/say", """{"callSign":"Bravo","kind":"intent","text":"land"}""");
        ui.Window.Tower.Refresh();
        ui.Pump();

        Assert.Contains("number 1, on the RNAV 09 NORTH", Description(ui, "tower/plane/Alpha"));
        Assert.Contains("number 2, holding", Description(ui, "tower/plane/Bravo"));
        var channel = ui.Find<ScrollViewer>("tower/channel").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Assert.Contains(channel, l => l.Contains("KSC tower → Alpha:  Alpha, KSC tower, say intentions."));
        Assert.Contains(channel, l => l.Contains("cleared RNAV 09 NORTH"));
        Assert.Contains(channel, l => l.Contains("Bravo → KSC tower:  land"));
        Assert.Contains(channel, l => l.Contains("number 2. Hold at KSC-HS"));

        ui.Click(ui.Find<Button>("tower/start")); // stop
        Assert.Null(ui.Window.Tower.Server);
        Assert.StartsWith("Off.", Description(ui, "tower/status"));
    }
}
