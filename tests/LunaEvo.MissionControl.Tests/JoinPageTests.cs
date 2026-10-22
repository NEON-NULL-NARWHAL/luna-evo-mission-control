// LUNA EVO · LunaEvo.MissionControl.Tests/JoinPageTests.cs
// [Test] The player side through the UI: check a server, save it, launch KSP into it; the host's share option; Ui.Open's guard.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.MissionControl.Core.Files;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Tests;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl.Tests;

public class JoinPageTests
{
    // The "friend's server" is this PC: the address resolves and pings for real; its website answers from the fake.
    private const string Host = "127.0.0.1";
    private static string WebJson => File.ReadAllText(Path.Combine(TestPaths.Fixtures, "Web", "serverinfo.json"));

    private static UiHarness Player()
    {
        var ui = new UiHarness(openServer: false);
        ui.Web.Text($"http://{Host}:8900/", WebJson);
        return ui;
    }

    [AvaloniaFact]
    public void Checks_a_server_without_hosting_anything()
    {
        using var ui = Player();
        ui.Click(ui.Find<Button>("welcome/join"));
        ui.Find<TextBox>("join/address").Text = Host;
        ui.Click(ui.Find<Button>("join/check"));
        ui.WaitFor(() => ui.Window.Join.Live != null, "server checked");

        Assert.Contains("This PC", Texts(ui.Find<Border>("join/ip/" + Host)));
        Assert.StartsWith("Ping ", Texts(ui.Find<Border>("join/ping"))[0]);
        Assert.StartsWith("Live info answered in", Texts(ui.Find<Border>("join/live"))[0]);
        Assert.Equal("LUNA EVO Test Server", Card(ui, "Server"));
        Assert.Equal("2 / 20", Card(ui, "Players"));
        Assert.Equal("Sandbox · Normal", Card(ui, "Mode"));
        Assert.Equal("None", Card(ui, "Password"));
        Assert.NotNull(ui.Find<Border>("join/player/Jebediah"));
        Assert.Contains("Station · Mun · 1.5 km", Texts(ui.Find<Border>("join/vessel/Mun Base Alpha")));
        Assert.Null(ui.TryFind<Border>("join/vessel/Ast. GHI-046")); // asteroids aren't listed
    }

    [AvaloniaFact]
    public void Explains_a_bad_address_and_a_server_without_live_info()
    {
        using var ui = new UiHarness(openServer: false); // no fake website this time
        ui.Window.Navigate(MainWindow.JoinKey);
        ui.Pump();

        ui.Find<TextBox>("join/address").Text = "not an address";
        ui.Click(ui.Find<Button>("join/check"));
        Assert.Contains(Texts(ui.Window), t => t.StartsWith("That doesn't look like a server address"));

        ui.Find<TextBox>("join/address").Text = Host;
        ui.Click(ui.Find<Button>("join/check"));
        ui.WaitFor(() => ui.Window.Join.Live != null, "server checked");
        Assert.Equal("No live info", Texts(ui.Find<Border>("join/live"))[0]);
        Assert.Contains(Texts(ui.Window), t => t == "No live info from this server");
    }

    [AvaloniaFact]
    public void Saves_and_removes_servers()
    {
        using var ui = Player();
        ui.Window.Navigate(MainWindow.JoinKey);
        ui.Pump();
        ui.Find<TextBox>("join/address").Text = Host + ":8800";
        ui.Click(ui.Find<Button>("join/check"));
        ui.WaitFor(() => ui.Window.Join.Live != null, "server checked");

        ui.Click(ui.Find<Button>("join/save"));

        var saved = Assert.Single(ui.Window.Settings.SavedServers);
        Assert.Equal(("LUNA EVO Test Server", Host, 8800), (saved.Name, saved.Host, saved.Port)); // named after the server
        Assert.NotNull(ui.Find<Border>($"join/saved/{Host}:8800"));
        ui.Click(ui.Find<Button>($"join/saved/remove/{Host}:8800"));
        Assert.Empty(ui.Window.Settings.SavedServers);
    }

    [AvaloniaFact]
    public void Launches_ksp_with_lmps_auto_connect_on_the_default_port()
    {
        using var ui = Player();
        var (ksp, launches) = FakeKspWithLmp();
        ui.Window.Navigate(MainWindow.JoinKey);
        ui.Pump();
        ui.Window.Join.KspBox.Text = ksp;
        ui.Find<TextBox>("join/address").Text = Host;
        ui.Click(ui.Find<Button>("join/check"));
        ui.WaitFor(() => ui.Window.Join.Live != null, "server checked");

        ui.Click(ui.Find<Button>("join/launch"));
        ui.WaitFor(() => launches.Count == 1, "KSP launched");

        Assert.Equal((ksp, "-lmp lmp://127.0.0.1"), (launches[0].Folder, string.Join(" ", launches[0].Arguments)));
        Assert.StartsWith("KSP is starting. LMP connects to 127.0.0.1 by itself", ui.Find<TextBlock>("join/launchResult").Text);
        var entry = ConfigFile.Load(KspLauncher.ClientSettingsPath(ksp)).GetObjectList("Servers")![0];
        Assert.Equal(("LUNA EVO Test Server", Host), (entry["Name"], entry["Address"])); // also added to LMP's list
    }

    [AvaloniaFact]
    public void With_a_password_the_server_goes_into_lmps_list_instead()
    {
        using var ui = Player();
        var (ksp, launches) = FakeKspWithLmp();
        ui.Window.Navigate(MainWindow.JoinKey);
        ui.Pump();
        ui.Window.Join.KspBox.Text = ksp;
        ui.Find<TextBox>("join/address").Text = Host;
        ui.Window.Join.PasswordBox.Text = "jebediah";

        ui.Click(ui.Find<Button>("join/launch"));
        ui.WaitFor(() => launches.Count == 1, "KSP launched");

        Assert.Empty(launches[0].Arguments); // LMP can't take a password on the command line
        Assert.Contains("pick", ui.Find<TextBlock>("join/launchResult").Text);
        Assert.Equal("jebediah", ConfigFile.Load(KspLauncher.ClientSettingsPath(ksp)).GetObjectList("Servers")![0]["Password"]);
        ui.Window.UpdateSettings(s => s with { KspFolder = ksp }); // force a settings save...
        Assert.DoesNotContain("jebediah", File.ReadAllText(ui.SettingsPath)); // ...and the password isn't in it
    }

    [AvaloniaFact]
    public void With_luna_evos_patches_a_port_and_a_password_still_connect_by_themselves()
    {
        var patches = System.Text.Encoding.ASCII.GetBytes("MZ fake patches");
        var original = JoinPage.PatchBytes;
        JoinPage.PatchBytes = () => patches; // CI builds have no KSP to compile the real patches against
        try
        {
            using var ui = Player();
            var (ksp, launches) = FakeKspWithLmp();
            ui.Window.Navigate(MainWindow.JoinKey);
            ui.Pump();
            ui.Window.Join.KspBox.Text = ksp;
            ui.Pump();
            Assert.StartsWith("LMP can't join a server by itself", ui.Find<TextBlock>("join/patchesStatus").Text);

            ui.Await(ui.Window.Join.TogglePatchesAsync());
            Assert.Contains("Install the LUNA EVO patches for LMP?", ui.DialogsShown);
            Assert.Equal(patches, File.ReadAllBytes(ClientPatches.PathFor(ksp)));
            Assert.Contains(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(ui.Find<Button>("join/patches")).OfType<TextBlock>(), t => t.Text == "Remove");

            ui.Find<TextBox>("join/address").Text = Host + ":9000";
            ui.Window.Join.PasswordBox.Text = "jebediah";
            ui.Click(ui.Find<Button>("join/launch"));
            ui.WaitFor(() => launches.Count == 1, "KSP launched");
            Assert.Equal("-lmp lmp://127.0.0.1:9000", string.Join(" ", launches[0].Arguments)); // the password isn't on the command line...
            Assert.Equal("jebediah", ConfigFile.Load(KspLauncher.ClientSettingsPath(ksp)).GetObjectList("Servers")![0]["Password"]); // ...it's here
            Assert.StartsWith("KSP is starting. LMP connects to 127.0.0.1:9000 by itself", ui.Find<TextBlock>("join/launchResult").Text);

            ui.Await(ui.Window.Join.TogglePatchesAsync());
            Assert.False(File.Exists(ClientPatches.PathFor(ksp)));
        }
        finally
        {
            JoinPage.PatchBytes = original;
        }
    }

    [AvaloniaFact]
    public void Wont_launch_without_ksp_and_lmp()
    {
        using var ui = Player();
        var launched = false;
        JoinPage.StartKsp = (_, _) => launched = true;
        ui.Window.Navigate(MainWindow.JoinKey);
        ui.Pump();
        ui.Find<TextBox>("join/address").Text = Host;

        ui.Window.Join.KspBox.Text = Path.GetTempPath();
        ui.Click(ui.Find<Button>("join/launch"));
        Assert.Contains("Can't find KSP_x64.exe", ui.Find<TextBlock>("join/launchResult").Text);

        ui.Window.Join.KspBox.Text = TestData.FakeKsp(); // KSP but no LMP client
        ui.Click(ui.Find<Button>("join/launch"));
        Assert.Contains("LMP isn't installed", ui.Find<TextBlock>("join/launchResult").Text);
        Assert.False(launched);
    }

    [AvaloniaFact]
    public void Host_can_share_live_info_with_players_after_confirming()
    {
        using var ui = new UiHarness();
        ui.Session.SetValue(LmpRules.Website, "ListenAddress", "127.0.0.1");
        ui.OpenTab(MainWindow.ConnectKey);
        Assert.Equal("Live info is only visible on this PC", Texts(ui.Find<Border>("connect/shareRow"))[0]);

        ui.Click(ui.Find<Button>("connect/share"));

        Assert.Contains("Share live info with players?", ui.DialogsShown);
        Assert.Equal("", ui.Session.GetValue(LmpRules.Website, "ListenAddress"));
        Assert.StartsWith("Sharing live info on TCP 8900", Texts(ui.Find<Border>("connect/shareRow"))[0]);
        ui.Click(ui.Find<Button>("connect/stopSharing"));
        Assert.Equal("127.0.0.1", ui.Session.GetValue(LmpRules.Website, "ListenAddress"));
    }

    [AvaloniaFact]
    public void Ui_open_only_opens_web_links_and_folders()
    {
        Assert.True(Ui.CanOpen("https://github.com/LunaMultiplayer/LunaMultiplayer"));
        Assert.True(Ui.CanOpen(Path.GetTempPath()));
        Assert.False(Ui.CanOpen("notepad.exe"));
        Assert.False(Ui.CanOpen(Path.Combine(Environment.SystemDirectory, "notepad.exe")));
        Assert.False(Ui.CanOpen("file:///C:/Windows/System32/calc.exe"));
        Assert.False(Ui.CanOpen("."));
    }

    /// <summary>A fake KSP with the LMP client "installed" (any DLL named LmpClient.dll), and launches captured.</summary>
    private static (string Folder, List<(string Folder, IReadOnlyList<string> Arguments)> Launches) FakeKspWithLmp()
    {
        var ksp = TestData.FakeKsp();
        var plugins = Directory.CreateDirectory(Path.Combine(ksp, "GameData", "LunaMultiplayer", "Plugins")).FullName;
        File.Copy(Path.Combine(TestPaths.LmpServer, "Server.dll"), Path.Combine(plugins, "LmpClient.dll"));
        var launches = new List<(string, IReadOnlyList<string>)>();
        JoinPage.StartKsp = (folder, arguments) =>
        {
            launches.Add((folder, arguments));
            return true;
        };
        return (ksp, launches);
    }

    private static string Card(UiHarness ui, string label) => Texts(ui.Find<Border>("join/card/" + label)).Last();

    private static List<string> Texts(Control control) =>
        control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
}
