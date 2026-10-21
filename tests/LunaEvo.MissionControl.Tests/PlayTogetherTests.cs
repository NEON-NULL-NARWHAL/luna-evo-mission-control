// LUNA EVO · LunaEvo.MissionControl.Tests/PlayTogetherTests.cs
// [Test] 2.1: CKAN "which provider?" choices, Cancel, Copy log; a server's mod list from Join; the LMP version check;
//        the server browser's sort, ping, save and password prompt; the host's mod list export.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Tests;

namespace LunaEvo.MissionControl.Tests;

public class PlayTogetherTests
{
    private const string Plan = "About to install:";
    private const string Declined = "Continue? [Y/n] Installation aborted: User declined install list";

    /// <summary>A harness with LUNA EVO's CKAN copy and one CKAN install called "Modded".</summary>
    private static (UiHarness Ui, string Ksp) WithCkan(bool openServer = false)
    {
        var ui = new UiHarness(openServer);
        var exe = Path.Combine(Core.Setup.CkanTools.Root, "v1.36.4", "ckan.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        File.WriteAllBytes(exe, TestData.FakeExe("ckan"));
        var ksp = TestData.FakeKsp();
        Directory.CreateDirectory(Core.Setup.CkanTools.ConfigFolder);
        File.WriteAllText(Path.Combine(Core.Setup.CkanTools.ConfigFolder, "config.json"),
            "{ \"GameInstances\": [ { \"Name\": \"Modded\", \"Path\": " + System.Text.Json.JsonSerializer.Serialize(ksp) + " } ] }");
        ui.Window.UpdateSettings(s => s with { KspFolder = ksp });
        return (ui, ksp);
    }

    [AvaloniaFact]
    public void Ckan_asks_which_provider_before_installing_and_includes_it()
    {
        var (ui, ksp) = WithCkan();
        using var _ = ui;
        var runs = new List<IReadOnlyList<string>>();
        CkanPage.RunCkan = (_, arguments, _, line, _) =>
        {
            runs.Add(arguments);
            if (arguments[0] == "install" && !arguments.Contains("--headless") && !arguments.Contains("KSPBurst"))
            {
                // What the real ckan.exe 1.36.4 prints for ParallaxContinued, answered "n".
                line("Parallax Continued requires KSPBurst, which can be provided by multiple mods.");
                line("Which KSPBurst provider would you like to install?");
                line("1) KSPBurst (KSPBurst Compiler)");
                line("2) KSPBurst-Lite (KSPBurst Lite)");
                line("Selection cancelled");
                return Task.FromResult(1);
            }
            if (arguments[0] == "install" && !arguments.Contains("--headless"))
            {
                line(Plan);
                line(" * KSPBurst 1.7.4.11");
                line(Declined);
                return Task.FromResult(1);
            }
            return Task.FromResult(0);
        };
        ui.OpenTab(MainWindow.CkanKey);
        ui.Find<TextBox>("ckan/list").Text = "ParallaxContinued";

        ui.Await(ui.Window.Ckan.InstallListAsync());
        Assert.Contains("Which KSPBurst?", ui.DialogsShown); // answered with the first option
        var install = runs.Last();
        Assert.Contains("--headless", install);
        Assert.Contains("ParallaxContinued", install);
        Assert.Contains("KSPBurst", install); // headless CKAN would have left it out
        Assert.Contains("KSPBurst (for KSPBurst)", ui.Find<TextBlock>("ckan/result").Text);

        ui.Window.Ckan.CopyLogAsync().Wait();
        Assert.Contains(ui.Copied, t => t.Contains("Which KSPBurst provider") && t.Contains("will be provided by KSPBurst"));
    }

    [AvaloniaFact]
    public void Cancel_stops_a_running_check()
    {
        var (ui, _) = WithCkan();
        using var _ui = ui;
        CkanPage.RunCkan = async (_, arguments, _, _, cancel) =>
        {
            if (arguments[0] == "install")
                await Task.Delay(Timeout.Infinite, cancel); // a check that would run for ever
            return 0;
        };
        ui.OpenTab(MainWindow.CkanKey);
        ui.Find<TextBox>("ckan/list").Text = "MechJeb2";
        var check = ui.Window.Ckan.CheckListAsync();
        ui.WaitFor(() => ui.Find<Button>("ckan/cancel").IsVisible, "Cancel shown");
        ui.Click(ui.Find<Button>("ckan/cancel"));
        ui.WaitFor(() => check.IsCompleted, "check stopped");
        Assert.StartsWith("Canceled", ui.Find<TextBlock>("ckan/result").Text);
        Assert.False(ui.Find<Button>("ckan/cancel").IsVisible);
        Assert.True(ui.Find<Button>("ckan/install").IsEnabled);
    }

    [AvaloniaFact]
    public void Join_installs_the_servers_mods_through_the_ckan_page()
    {
        var (ui, ksp) = WithCkan();
        using var _ = ui;
        var runs = new List<IReadOnlyList<string>>();
        CkanPage.RunCkan = (_, arguments, _, line, _) =>
        {
            runs.Add(arguments);
            if (arguments[0] == "install")
            {
                line(Plan);
                line(" * MechJeb2 2.14.3.0");
                line(Declined);
                return Task.FromResult(1);
            }
            return Task.FromResult(0);
        };
        var file = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N") + ".ckan");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "depends": [ { "name": "MechJeb2" } ] }""");
        CkanPage.PickModList = _ => Task.FromResult<string?>(file);

        ui.OpenTab(MainWindow.JoinKey);
        ui.Await(ui.Window.Join.InstallServerModsAsync());
        Assert.Contains("MechJeb2", ui.Find<TextBox>("ckan/list").Text);
        Assert.Equal(Path.GetFullPath(ksp), ui.Find<ComboBox>("ckan/target").SelectedItem);
        Assert.StartsWith("CKAN can install this list: 1 mod(s)", ui.Find<TextBlock>("ckan/result").Text); // checked, not installed
        Assert.DoesNotContain(runs, r => r.Contains("--headless") && r[0] == "install");
    }

    [AvaloniaFact]
    public void Join_says_when_your_lmp_version_doesnt_match_the_server()
    {
        using var ui = new UiHarness(openServer: false);
        var ksp = TestData.FakeKsp();
        ui.Window.UpdateSettings(s => s with { KspFolder = ksp });
        ui.OpenTab(MainWindow.JoinKey);
        ui.Window.Join.KspBox.Text = ksp;

        ui.Await(ui.Window.Join.OpenServerAsync("203.0.113.5", 8800, "0.30.0"));
        Assert.Equal(new Version(0, 30, 0), ui.Window.Join.ServerVersion);
        Assert.NotNull(ui.Find<Control>("join/versionMismatch")); // no LMP client in this KSP
        Assert.Contains("Install LMP 0.30.0", Texts(ui.Find<Button>("join/installClient")));

        // A pasted invite carries the version too.
        ui.Window.Join.AddressBox.Text = "Join my KSP server!\nAddress: 203.0.113.5\nLMP version: 0.29.2-compiled (you need the same LMP client mod)\n";
        ui.Await(ui.Window.Join.CheckAsync());
        Assert.Equal(new Version(0, 29, 2), ui.Window.Join.ServerVersion);
    }

    [AvaloniaFact]
    public void Server_browser_pings_sorts_saves_and_asks_for_a_password()
    {
        using var ui = new UiHarness(openServer: false);
        ui.Web.Text(ServerBrowser.MasterLists[0], TestData.MasterJson);
        BrowserPage.Ping = host => Task.FromResult<long?>(host == "198.51.100.7" ? 20 : 90);
        ui.OpenTab(MainWindow.BrowserKey);
        ui.Await(ui.Window.Browser.LoadAsync());
        ui.Find<CheckBox>("browser/myVersion").IsChecked = false;
        ui.WaitFor(() => Texts(ui.Find<Control>("browser/server/198.51.100.7:8810")).Any(t => t.Contains("20 ms")), "pings shown");

        ui.Find<ComboBox>("browser/sort").SelectedIndex = 1; // lowest ping first
        var first = ui.Window.GetVisualDescendants().OfType<Control>()
            .Select(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) ?? "").First(id => id.StartsWith("browser/server/"));
        Assert.Equal("browser/server/198.51.100.7:8810", first);

        ui.Click(ui.Find<Button>("browser/save/203.0.113.5:8800"));
        Assert.Contains(ui.Window.Settings.SavedServers, s => s.Host == "203.0.113.5" && s.Name == "Kerbin Club");
        Assert.Contains("Saved", Texts(ui.Find<Button>("browser/save/203.0.113.5:8800")));

        Dialogs.PromptResponder = (_, _) => "hunter2";
        var fun = ui.Window.Browser.Servers.Single(s => s.Port == 8810); // has a password
        ui.Await(ui.Window.Browser.JoinAsync(fun));
        Assert.Equal("hunter2", ui.Window.Join.PasswordBox.Text);
        Assert.Equal("198.51.100.7:8810", ui.Window.Join.AddressBox.Text);
    }

    [AvaloniaFact]
    public void Connect_exports_the_hosts_mod_list_for_players()
    {
        var (ui, ksp) = WithCkan(openServer: true);
        using var _ = ui;
        Directory.CreateDirectory(Path.Combine(ksp, "CKAN"));
        File.WriteAllText(Path.Combine(ksp, "CKAN", "installed-Modded.ckan"), """{ "author": "somebody", "depends": [ { "name": "MechJeb2" } ] }""");
        var target = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "mods.ckan");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        CkanPage.PickExportPath = (_, _) => Task.FromResult<string?>(target);
        ui.OpenTab(MainWindow.ConnectKey);
        ui.Await(ui.Window.Connect!.ExportModsAsync());
        Assert.Contains("MechJeb2", File.ReadAllText(target));
        Assert.StartsWith("Exported 1 mod(s) from Modded", ui.Find<TextBlock>("connect/modsResult").Text);
    }

    private static List<string> Texts(Control control) =>
        control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
}
