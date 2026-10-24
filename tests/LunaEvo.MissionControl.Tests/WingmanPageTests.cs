// LUNA EVO · LunaEvo.MissionControl.Tests/WingmanPageTests.cs
// [Test] KSP Wingman from LUNA EVO: installing it (patches → Tools), its mod in a KSP, Ollama's row, the Wingman page
//        appearing on the rail, and agents on it (fake console processes: events shown, orders sent, stop).
// Used by: dotnet test
// Guide:   Meat and Potatoes/29-Wingman.md

using System.IO.Compression;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.MissionControl.Core.Tests;
using LunaEvo.MissionControl.Core.Wingman;

namespace LunaEvo.MissionControl.Tests;

public class WingmanPageTests
{
    private const string ZipUrl = "https://github.com/NEON-NULL-NARWHAL/ksp-wingman/releases/download/v0.6.0/KspWingman-0.6.0.zip";

    private static string Description(UiHarness ui, string id)
    {
        ui.Pump();
        return ui.Find<Control>(id).GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("sublabel")).Text ?? "";
    }

    private static byte[] ReleaseZip()
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var name in new[] { "KspWingman.exe", "KspWingman.Mcp.exe", @"GameData\KspWingman\KspWingman.Bridge.dll" })
                using (var writer = new StreamWriter(zip.CreateEntry(name).Open()))
                    writer.Write("MZ " + name);
        return memory.ToArray();
    }

    private static string ReleasesJson(byte[] zip) => System.Text.Json.JsonSerializer.Serialize(new[]
    {
        new
        {
            tag_name = "v0.6.0", prerelease = false, draft = false, html_url = "https://github.com/x", published_at = "2026-10-23T12:00:00Z", body = "",
            assets = new[] { new { name = "KspWingman-0.6.0.zip", browser_download_url = ZipUrl, size = zip.Length, digest = "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(zip)) } },
        },
    });

    [AvaloniaFact]
    public void The_agents_knowledge_is_kept_on_the_open_server()
    {
        using var ui = new UiHarness(); // a server open
        Assert.Equal(Path.Combine(ui.ServerDirectory, "LunaEvo", "agents", "knowledge.json"), ui.Window.KnowledgePath);
    }

    [AvaloniaFact]
    public void Installs_wingman_its_mod_and_shows_its_page()
    {
        using var ui = new UiHarness(openServer: false);
        var zip = ReleaseZip();
        ui.Web.Text(WingmanTools.ReleasesUrl, ReleasesJson(zip)).Bytes(ZipUrl, zip)
            .Text("http://127.0.0.1:11434/api/version", """{"version":"0.40.9"}""")
            .Text("http://127.0.0.1:11434/api/tags", """{"models":[{"name":"gemma4:e4b"}]}""");
        var ksp = TestData.FakeKsp();
        ui.Window.Join.KspBox.Text = ksp;
        Assert.False(ui.Find<Button>("nav/wingman").IsVisible); // not installed yet

        ui.OpenTab(MainWindow.PatchesKey);
        ui.WaitFor(() => Description(ui, "patches/ollamaStatus").Contains("0.40.9"), "Ollama checked");
        Assert.Contains("qwen3:4b-instruct (2.5 GB", Description(ui, "patches/ollamaStatus"));
        Assert.NotNull(ui.TryFind<Button>("patches/pull"));
        Assert.StartsWith("Not installed.", Description(ui, "patches/wingmanStatus"));
        Assert.Null(ui.TryFind<Control>("patches/bridgeStatus"));

        ui.Click(ui.Find<Button>("patches/wingman"));
        ui.WaitFor(() => WingmanTools.Installed() != null && ui.Find<TextBlock>("patches/result").Text!.Contains("installed"), "Wingman installed");
        Assert.StartsWith("0.6.0 installed.", Description(ui, "patches/wingmanStatus"));
        Assert.True(ui.Find<Button>("nav/wingman").IsVisible);
        Assert.Contains("Wingman's mod isn't installed · kRPC isn't", Description(ui, "patches/bridgeStatus"));

        ui.Click(ui.Find<Button>("patches/bridge"));
        Assert.True(File.Exists(Path.Combine(ksp, "GameData", "KspWingman", "KspWingman.Bridge.dll")));
        Assert.Contains("Wingman's mod is installed", Description(ui, "patches/bridgeStatus"));
        Assert.NotNull(ui.TryFind<Button>("patches/krpc")); // kRPC still missing: off to CKAN
        ui.Click(ui.Find<Button>("patches/krpc"));
        Assert.Contains("selected", ui.Find<Button>("nav/ckan").Classes);

        // Already the newest.
        ui.OpenTab(MainWindow.PatchesKey);
        ui.Click(ui.Find<Button>("patches/wingman"));
        ui.WaitFor(() => ui.Find<TextBlock>("patches/result").Text!.Contains("is the newest"), "update check");
    }

    private sealed class FakeAgent : IAgentProcess
    {
        public List<string> Sent { get; } = [];
        public bool HasExited { get; private set; }
        public event Action<string?>? Output;
        public event Action? Exited;
        public void Say(string line) => Output?.Invoke(line);

        public void Send(string line)
        {
            Sent.Add(line);
            if (line.Contains("\"quit\""))
                Kill();
        }

        public void Kill()
        {
            HasExited = true;
            Exited?.Invoke();
        }

        public void Dispose()
        {
        }
    }

    [AvaloniaFact]
    public void Agents_on_the_wingman_page_show_their_flight_and_take_orders()
    {
        using var ui = new UiHarness(openServer: false);
        var folder = Directory.CreateDirectory(Path.Combine(WingmanTools.Root, "0.6.0", "GameData", "KspWingman")).Parent!.Parent!.FullName;
        File.WriteAllText(Path.Combine(folder, "KspWingman.exe"), "MZ");
        File.WriteAllText(Path.Combine(folder, "KspWingman.Mcp.exe"), "MZ");
        var fakes = new List<(System.Diagnostics.ProcessStartInfo Info, FakeAgent Agent)>();
        ui.Window.Wingman.Host = new AgentHost(info => { var f = new FakeAgent(); fakes.Add((info, f)); return f; });
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var towerPort = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        ui.Window.Tower.Start(towerPort);

        ui.OpenTab(MainWindow.WingmanKey);
        Assert.Contains("0.6.0", Description(ui, "wingman/status"));
        Assert.True(ui.Find<TextBlock>("wingman/none").IsVisible);
        ui.Find<TextBox>("wingman/port").Text = "50010";
        ui.Find<TextBox>("wingman/planner").Text = "qwen3:4b";
        ui.Click(ui.Find<Button>("wingman/start"));
        var (info, agent) = Assert.Single(fakes);
        Assert.Equal("qwen3:4b", info.ArgumentList[info.ArgumentList.IndexOf("--planner-model") + 1]);
        Assert.Contains("--ipc", info.ArgumentList);
        Assert.Equal($"http://localhost:{towerPort}/", info.Environment["WINGMAN_TOWER_URL"]);
        Assert.Equal("Wingman2", ui.Find<TextBox>("wingman/callSign").Text); // ready for the next one

        // A second agent on the same KSP is refused.
        ui.Find<TextBox>("wingman/port").Text = "50010";
        ui.Click(ui.Find<Button>("wingman/start"));
        Assert.Contains("already flying the KSP on kRPC port 50010", ui.Find<TextBlock>("wingman/result").Text);
        Assert.Single(fakes);

        agent.Say("""{"type":"event","kind":"phase","text":"Parked → TakingOff","data":{"from":"Parked","to":"TakingOff"}}""");
        agent.Say("""{"type":"event","kind":"tower","text":"Wingman1, KSC tower, cleared RNAV 09 NORTH.","data":{"kind":"clearance"}}""");
        agent.Say("""{"type":"event","kind":"flight","text":"Flight landed: stopped 1105 m down KSC runway 09.","data":{"result":"landed"}}""");
        ui.WaitFor(() => ui.Find<TextBlock>("wingman/agent/Wingman1/summary").Text!.Contains("last flight: Flight landed"), "events shown");
        Assert.StartsWith("taking off · kRPC port 50010", ui.Find<TextBlock>("wingman/agent/Wingman1/summary").Text);
        var lines = ui.Find<ScrollViewer>("wingman/agent/Wingman1/events").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
        Assert.Contains(lines, l => l.Contains("tower  Wingman1, KSC tower, cleared RNAV 09 NORTH."));
        Assert.False(ui.Find<TextBlock>("wingman/none").IsVisible);

        ui.Find<TextBox>("wingman/agent/Wingman1/order").Text = "land at KSC";
        ui.Click(ui.Find<Button>("wingman/agent/Wingman1/send"));
        Assert.Equal("land at KSC", JsonNode.Parse(agent.Sent.Last())!["text"]!.GetValue<string>());
        Assert.Equal("", ui.Find<TextBox>("wingman/agent/Wingman1/order").Text);

        // Free flight: asked for, shown while the console says it's on, stopped from the same button.
        ui.Click(ui.Find<Button>("wingman/agent/Wingman1/free"));
        Assert.Equal("free", JsonNode.Parse(agent.Sent.Last())!["type"]!.GetValue<string>());
        agent.Say("""{"type":"event","kind":"free","text":"Free flight for 30 minutes: practising landings around KSC. Any order stops it."}""");
        agent.Say("""{"type":"event","kind":"free","text":"Next: land on the short visual (fewest good landings)."}""");
        ui.WaitFor(() => ui.Find<TextBlock>("wingman/agent/Wingman1/summary").Text!.Contains("· free flight"), "free flight shown");
        ui.Click(ui.Find<Button>("wingman/agent/Wingman1/free"));
        Assert.Equal("stop-free", JsonNode.Parse(agent.Sent.Last())!["type"]!.GetValue<string>());
        agent.Say("""{"type":"event","kind":"free","text":"Free flight stopped for an order."}""");
        ui.WaitFor(() => !ui.Find<TextBlock>("wingman/agent/Wingman1/summary").Text!.Contains("free flight"), "free flight over");

        // What the agents know: a flight and a lesson through the tower's channel, shown with everyone's numbers; Delete.
        var server = ui.Window.Tower.Server!;
        var flight = server.Answer("POST", "/v1/flight", """{"callSign":"Wingman1","result":"landed","approach":"VISUAL 09","goArounds":0,"minutes":6,"landing":{"downRunway":900,"offCenterline":5,"headingError":1},"phases":["TakingOff","Climbing","OnRoute","OnFinal","Stopped"]}""");
        Assert.Equal(200, flight.Status);
        Assert.Equal(200, server.Answer("POST", "/v1/lesson", """{"callSign":"Wingman1","skill":"land-visual","text":"Join the short visual's downwind at 700 m.","evidence":[1]}""").Status);
        ui.Window.Wingman.Refresh();
        Assert.Contains("1 of 1 good", Description(ui, "wingman/skill/land-visual"));
        Assert.Contains("locked until route and land-visual", Description(ui, "wingman/skill/land-rnav"));
        Assert.Contains(Path.Combine(Path.GetDirectoryName(ui.Window.SettingsPath)!, "agents", "knowledge.json"), Description(ui, "wingman/knowledge/where"));
        Assert.True(File.Exists(server.KnowledgePath)); // no server open: next to the settings
        ui.Click(ui.Find<Button>("wingman/lesson/1/delete"));
        Assert.Null(ui.TryFind<Control>("wingman/lesson/1"));

        ui.Click(ui.Find<Button>("wingman/agent/Wingman1/stop"));
        ui.WaitFor(() => ui.Find<TextBlock>("wingman/agent/Wingman1/summary").Text!.StartsWith("stopped"), "agent stopped");
        Assert.False(ui.Find<TextBox>("wingman/agent/Wingman1/order").IsEnabled);
        ui.Click(ui.Find<Button>("wingman/agent/Wingman1/stop")); // now "Remove"
        Assert.Null(ui.TryFind<Control>("wingman/agent/Wingman1"));
        Assert.True(ui.Find<TextBlock>("wingman/none").IsVisible);
    }
}
