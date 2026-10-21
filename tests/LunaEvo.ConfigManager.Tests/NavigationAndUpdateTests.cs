// LUNA EVO · LunaEvo.ConfigManager.Tests/NavigationAndUpdateTests.cs
// [Test] 0.6: the tidier rail (Server settings tabs, Mod Control), page tabs, and updating LUNA EVO from the UI.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.ConfigManager.Core.Setup;
using LunaEvo.ConfigManager.Core.Tests;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Tests;

public class NavigationAndUpdateTests
{
    [AvaloniaFact]
    public void Config_files_are_tabs_on_one_server_settings_page()
    {
        using var ui = new UiHarness();
        var rail = RailButtons(ui);
        Assert.Equal(["nav/console", "nav/players", "nav/vessels", "nav/connect", "nav/serverSettings", "nav/LMPModControl.xml", "nav/profiles", "nav/join", "nav/crafts", "nav/ckan", "nav/settings"], rail);

        ui.OpenTab(MainWindow.ConsolePage);
        Assert.Null(ui.TryFind<Button>("nav/WarpSettings.xml")); // not in the rail any more

        ui.Window.Navigate("WarpSettings.xml");
        ui.Pump();
        Assert.Contains("selected", ui.Find<Button>("nav/serverSettings").Classes);
        Assert.Contains("selected", ui.Find<Button>("nav/WarpSettings.xml").Classes);
        Assert.True(ui.Find<Control>("WarpSettings.xml/WarpMode").IsEffectivelyVisible);
        Assert.False(ui.Find<Control>($"{LmpRules.General}/ServerName").IsEffectivelyVisible); // other tabs hidden

        // An error on any tab shows on the rail entry too.
        ui.OpenTab(LmpRules.General);
        ui.Find<TextBox>($"{LmpRules.General}/ServerName").Text = new string('x', 40);
        ui.Pump();
        Assert.Equal(["1"], Badges(ui.Find<Button>("nav/serverSettings")));
        Assert.Equal(["1"], Badges(ui.Find<Button>("nav/" + LmpRules.General)));
    }

    [AvaloniaFact]
    public void Password_fields_can_generate_a_strong_password_and_exposure_is_flagged()
    {
        using var ui = new UiHarness();
        ui.OpenTab(LmpRules.General);
        ui.Click(ui.Find<Button>($"{LmpRules.General}/AdminPassword/generate"));
        Assert.Equal(20, ui.Session.GetValue(LmpRules.General, "AdminPassword")!.Length);
        Assert.DoesNotContain(ui.Session.Validate(), i => i.Setting == "AdminPassword");

        ui.Session.SetValue(LmpRules.Connection, "Upnp", "true");
        ui.Window.RefreshAll();
        ui.OpenTab(MainWindow.ConsolePage);
        Assert.True(ui.Find<Border>("console/exposure").IsVisible);
        ui.Session.SetValue(LmpRules.Connection, "Upnp", "false");
        ui.Session.SetValue("MasterServerSettings.xml", "RegisterWithMasterServer", "false");
        ui.Window.RefreshAll();
        Assert.False(ui.Find<Border>("console/exposure").IsVisible);
    }

    [AvaloniaFact]
    public void Crafts_are_exported_and_imported_into_the_servers_library()
    {
        using var ui = new UiHarness();
        var ksp = TestData.KspWithCraft();
        ui.Window.UpdateSettings(s => s with { KspFolder = ksp });
        ui.OpenTab(MainWindow.CraftsKey);
        Assert.NotNull(ui.Find<Border>("crafts/row/KSP save career/Kerbal X"));

        var file = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "Kerbal X.lunacraft");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        CraftsPage.PickExportPath = (_, _) => Task.FromResult<string?>(file);
        ui.Await(ui.Window.Crafts.ExportAsync(Core.Mods.CraftShare.InKsp(ksp)[0], ksp));
        Assert.True(File.Exists(file));

        CraftsPage.PickImportPath = _ => Task.FromResult<string?>(file);
        ui.Answer = (title, buttons) => title.StartsWith("Import") ? buttons.ToList().IndexOf("Add to the server's library") : null;
        ui.Await(ui.Window.Crafts.ImportAsync());

        Assert.True(File.Exists(Path.Combine(ui.ServerDirectory, "Universe", "Crafts", "Shared", "VAB", "Kerbal X.craft")));
        Assert.Contains("aren't in your GameData", ui.Find<TextBlock>("crafts/result").Text); // the fake KSP has no parts
        Assert.NotNull(ui.Find<Border>("crafts/row/Server library (Shared)/Kerbal X"));
    }

    [AvaloniaFact]
    public void Search_finds_a_setting_on_any_tab_and_opens_it()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.ServerSettingsKey);
        ui.Find<TextBox>("serverSettings/search").Text = "heartbeat";
        ui.Pump();

        ui.Click(ui.Find<Button>("serverSettings/result/ConnectionSettings.xml/HearbeatMsInterval"));
        ui.Pump();
        Assert.Contains("selected", ui.Find<Button>("nav/ConnectionSettings.xml").Classes);
        Assert.True(ui.Find<Control>("ConnectionSettings.xml/HearbeatMsInterval").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Announcements_go_to_chat_on_schedule()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.Window.UpdateSettings(s => s with { Announcements = "30 Welcome to Kerbin Space Agency!" });
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == Core.Server.ServerState.Running, "server running");

        ui.Server.CheckAnnouncements(DateTime.Now);              // starts the clock
        ui.Server.CheckAnnouncements(DateTime.Now.AddMinutes(31));
        ui.WaitFor(() => ui.Server.VisibleLines.Any(l => l.Message == "Command input: /say Welcome to Kerbin Space Agency!"), "announcement sent");
    }

    [AvaloniaFact]
    public void Home_server_mode_registers_with_windows_and_hides_instead_of_closing()
    {
        using var ui = new UiHarness();
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "home-" + Guid.NewGuid().ToString("N"))).FullName;
        MainWindow.CurrentExe = Path.Combine(dir, AppUpdater.ExeName);
        File.WriteAllText(MainWindow.CurrentExe, "MZ");
        var registered = new List<(bool On, string Command)>();
        HomeServer.ApplyStartWithWindows = (on, command) => registered.Add((on, command));
        ui.OpenTab(MainWindow.SettingsKey);

        ui.Find<ToggleSwitch>("settings/homeServer").IsChecked = true;
        ui.Pump();
        Assert.True(ui.Window.Settings.HomeServerMode);
        var (on, command) = Assert.Single(registered);
        Assert.True(on);
        Assert.Contains("--minimized", command);
        Assert.Contains("--settings", command); // the harness uses its own settings file, so it's passed along

        var closed = false;
        ui.Window.Closed += (_, _) => closed = true;
        ui.Window.Close();
        ui.Pump();
        Assert.False(closed); // hidden to the notification area (or left open if there isn't one), never closed

        ui.Find<ToggleSwitch>("settings/homeServer").IsChecked = false;
        ui.Pump();
        Assert.False(registered.Last().On);
    }

    [AvaloniaFact]
    public void The_invite_says_where_and_which_version_but_never_the_password()
    {
        using var ui = new UiHarness();
        ui.Session.SetValue(LmpRules.General, "Password", "secret-password-123");
        ui.OpenTab(MainWindow.ConnectKey);
        var invite = ui.Window.Connect!.Invite()!;
        Assert.Contains("Address: ", invite);
        Assert.Contains("LMP version: 0.29.2", invite);
        Assert.Contains("Password: yes, ask me", invite);
        Assert.DoesNotContain("secret-password-123", invite);
    }

    [AvaloniaFact]
    public void The_console_shows_the_servers_memory_and_cpu_while_it_runs()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.OpenTab(MainWindow.ConsolePage);
        Assert.False(ui.TryFind<Border>("console/health/Memory")?.IsEffectivelyVisible == true); // nothing while stopped
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == Core.Server.ServerState.Running, "server running");
        ui.Server.ShowHealth();
        ui.Pump();
        Assert.True(ui.Find<Border>("console/health/Memory").IsEffectivelyVisible);
        Assert.NotNull(ui.Find<Border>("console/health/CPU"));
    }

    [AvaloniaFact]
    public void Vessel_cleanup_selects_debris_but_never_crewed_vessels()
    {
        using var ui = new UiHarness();
        TestData.AddVessels(ui.ServerDirectory);
        ui.OpenTab(MainWindow.VesselsKey);
        Assert.Equal("2", Texts(ui.Find<Border>("vessels/card/Debris")).Last());

        ui.Click(ui.Find<Button>("vessels/selectDebris"));
        ui.Pump();
        Assert.True(ui.Find<CheckBox>("vessels/select/b2").IsChecked);
        Assert.False(ui.Find<CheckBox>("vessels/select/c3").IsChecked); // debris, but Bob is aboard

        ui.Await(ui.Window.Vessels!.RemoveSelectedAsync());
        Assert.Contains("Remove 1 vessel(s)?", ui.DialogsShown);
        Assert.False(File.Exists(Path.Combine(ui.ServerDirectory, "Universe", "Vessels", "b2.txt")));
        Assert.True(File.Exists(Path.Combine(ui.ServerDirectory, "Universe", "Vessels", "c3.txt")));
        Assert.StartsWith("Removed 1 vessel(s).", ui.Find<TextBlock>("vessels/result").Text);
    }

    [Fact]
    public void Lmps_heartbeat_typo_is_shown_spelled_right()
    {
        Assert.Equal("Heartbeat Ms Interval", Ui.Humanize("HearbeatMsInterval")); // the XML keeps LMP's name
    }

    [AvaloniaFact]
    public void Ckan_page_says_whether_ckan_is_installed_gets_it_and_opens_it()
    {
        var exe = TestData.FakeExe("ckan");
        using var ui = new UiHarness();
        ui.Web.Text(Core.Setup.CkanTools.ReleasesUrl, TestData.CkanReleasesJson(exe)).Bytes(TestData.CkanExeUrl, exe);
        var opened = new List<string>();
        CkanPage.StartCkan = path =>
        {
            opened.Add(path);
            return true;
        };
        ui.OpenTab(MainWindow.CkanKey);
        Assert.True(Visible(ui, "Not installed"));

        ui.Await(ui.Window.Ckan.DownloadAsync());
        ui.Pump();
        Assert.StartsWith("CKAN v1.36.4 downloaded and verified", ui.Find<TextBlock>("ckan/result").Text);
        Assert.True(Visible(ui, "Installed: CKAN v1.36.4"));
        ui.Click(ui.Find<Button>("ckan/open"));
        Assert.EndsWith(Path.Combine("v1.36.4", "ckan.exe"), Assert.Single(opened));

        // Settings → Downloads points here now.
        ui.OpenTab(MainWindow.SettingsKey);
        ui.Click(ui.Find<Button>("settings/tab/downloads"));
        ui.Click(ui.Find<Button>("settings/goCkan"));
        Assert.True(ui.Find<Control>("ckan/install").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Ckan_page_installs_a_loaded_mod_list_by_name_only()
    {
        var ckanExe = TestData.FakeExe("ckan");
        using var ui = new UiHarness(openServer: false);
        var installed = Path.Combine(Core.Setup.CkanTools.Root, "v1.36.4", "ckan.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        File.WriteAllBytes(installed, ckanExe);
        var ksp = TestData.FakeKsp();
        Directory.CreateDirectory(Core.Setup.CkanTools.ConfigFolder);
        File.WriteAllText(Path.Combine(Core.Setup.CkanTools.ConfigFolder, "config.json"),
            "{ \"GameInstances\": [ { \"Name\": \"Modded\", \"Path\": " + System.Text.Json.JsonSerializer.Serialize(ksp) + " } ] }");
        var runs = new List<IReadOnlyList<string>>();
        CkanPage.RunCkan = (exe, arguments, line) =>
        {
            Assert.Equal(installed, exe);
            runs.Add(arguments);
            line("CKAN says hi");
            return Task.FromResult(0);
        };
        CkanPage.PickModList = _ =>
        {
            var file = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N") + ".ckan");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, """
                { "spec_version": "v1.6", "identifier": "friends-pack", "download": "https://evil.example/x.zip",
                  "depends": [ { "name": "MechJeb2" }, { "name": "KerbalEngineerRedux", "version": "1.1.9.0" } ] }
                """);
            return Task.FromResult<string?>(file);
        };
        ui.OpenTab(MainWindow.CkanKey);
        Assert.True(Visible(ui, "Modded"));
        ui.Click(ui.Find<Button>("ckan/load"));
        ui.Pump();
        Assert.Contains("MechJeb2", ui.Find<TextBox>("ckan/list").Text);
        Assert.Equal(ksp, ui.Find<ComboBox>("ckan/target").SelectedItem);

        ui.Await(ui.Window.Ckan.InstallListAsync());
        ui.Pump();
        // Only names and versions reach CKAN; the file's own download link never does.
        Assert.Equal([["update", "--headless", "--gamedir", ksp], ["install", "--headless", "--no-recommends", "--gamedir", ksp, "MechJeb2", "KerbalEngineerRedux=1.1.9.0"]], runs);
        Assert.Contains("CKAN says hi", (IEnumerable<string>)ui.Find<ListBox>("ckan/log").ItemsSource!);
        Assert.StartsWith("Done", ui.Find<TextBlock>("ckan/result").Text);
        Assert.Contains("Install 2 mod(s) with CKAN?", ui.DialogsShown);
    }

    [AvaloniaFact]
    public void Ckan_page_refuses_while_ckan_has_the_install_open()
    {
        using var ui = new UiHarness(openServer: false);
        var installed = Path.Combine(Core.Setup.CkanTools.Root, "v1.36.4", "ckan.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        File.WriteAllBytes(installed, TestData.FakeExe("ckan"));
        var ksp = TestData.FakeKsp();
        Directory.CreateDirectory(Path.Combine(ksp, "CKAN"));
        File.WriteAllText(Path.Combine(ksp, "CKAN", "registry.locked"), Environment.ProcessId.ToString()); // a live process holds the lock
        ui.Window.UpdateSettings(s => s with { KspFolder = ksp });
        ui.OpenTab(MainWindow.CkanKey);
        ui.Find<TextBox>("ckan/list").Text = "MechJeb2";

        ui.Await(ui.Window.Ckan.InstallListAsync());
        Assert.StartsWith("CKAN has this install open", ui.Find<TextBlock>("ckan/result").Text);
        Assert.Empty(ui.DialogsShown);
    }

    [AvaloniaFact]
    public void Join_offers_ckan_installs_and_launches_with_ckans_options()
    {
        using var ui = new UiHarness(openServer: false);
        var ksp = TestData.FakeKsp();
        Directory.CreateDirectory(Path.Combine(ksp, "GameData", "LunaMultiplayer", "Plugins"));
        File.Copy(Path.Combine(TestPaths.LmpServer, "Server.dll"), Path.Combine(ksp, "GameData", "LunaMultiplayer", "Plugins", "LmpClient.dll"));
        Directory.CreateDirectory(Path.Combine(ksp, "CKAN"));
        File.WriteAllText(Path.Combine(ksp, "CKAN", "GUIConfig.json"), "{ \"CommandLines\": [ \"KSP_x64.exe -single-instance\" ] }");
        Directory.CreateDirectory(Core.Setup.CkanTools.ConfigFolder);
        File.WriteAllText(Path.Combine(Core.Setup.CkanTools.ConfigFolder, "config.json"),
            "{ \"GameInstances\": [ { \"Name\": \"Modded\", \"Path\": " + System.Text.Json.JsonSerializer.Serialize(ksp) + " } ] }");
        var launches = new List<IReadOnlyList<string>>();
        JoinPage.StartKsp = (_, arguments) =>
        {
            launches.Add(arguments);
            return true;
        };
        ui.Window.Navigate(MainWindow.JoinKey);
        ui.Window.Join.ShowCkanInstances(); // re-read CKAN's config written above
        ui.Pump();
        ui.Click(ui.Find<Button>("join/ckan/Modded"));
        Assert.Equal(ksp, ui.Window.Join.KspBox.Text);
        ui.Find<TextBox>("join/address").Text = "127.0.0.1";
        ui.Await(ui.Window.Join.LaunchAsync());

        Assert.Equal(["-single-instance", "-lmp", "lmp://127.0.0.1"], Assert.Single(launches));
    }

    [AvaloniaFact]
    public void Players_settings_and_join_pages_have_tabs()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.PlayersKey);
        ui.Click(ui.Find<Button>("players/tab/bans"));
        Assert.True(Visible(ui, "Nobody is banned"));
        Assert.False(Visible(ui, "Nobody is online"));

        ui.OpenTab(MainWindow.SettingsKey);
        ui.Click(ui.Find<Button>("settings/tab/updates"));
        Assert.True(ui.Find<Control>("settings/checkAppUpdates").IsEffectivelyVisible);
        Assert.False(ui.Find<Control>("settings/accentHex").IsEffectivelyVisible);

        ui.OpenTab(MainWindow.JoinKey);
        ui.Click(ui.Find<Button>("join/tab/mods"));
        Assert.True(ui.Find<Control>("join/modCheck").IsEffectivelyVisible);
        Assert.True(ui.Find<Control>("join/launch").IsEffectivelyVisible); // Play is always there
        Assert.False(ui.Find<Control>("join/address").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Updates_luna_evo_and_restarts_into_the_new_version()
    {
        var newExe = TestData.FakeExe("LUNA EVO 9.9.9");
        using var ui = new UiHarness();
        ui.Web.Text(AppUpdater.ReleasesUrl, TestData.AppReleasesJson(newExe)).Bytes(TestData.AppExeUrl, newExe);
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "self-" + Guid.NewGuid().ToString("N"))).FullName;
        var current = Path.Combine(dir, AppUpdater.ExeName);
        File.WriteAllBytes(current, TestData.FakeExe("this version"));
        MainWindow.CurrentExe = current;
        var restarted = new List<string>();
        MainWindow.StartNewVersion = (exe, _) =>
        {
            restarted.Add(exe);
            return true;
        };

        ui.Await(ui.Window.CheckForAppUpdateAsync(force: true));
        ui.Pump();
        Assert.Equal("LUNA EVO 9.9.9 is available", ui.Find<TextBlock>("status/appUpdate").Text);
        ui.OpenTab(MainWindow.SettingsKey);
        ui.Click(ui.Find<Button>("settings/tab/updates"));
        Assert.Equal("Update to v9.9.9 and restart", Texts(ui.Find<Button>("settings/appUpdate")).Last());

        ui.Session.SetValue(LmpRules.General, "ServerName", "Unsaved");
        ui.Answer = (title, _) => title == "Unsaved changes" ? 1 : null; // update without saving
        ui.Await(ui.Window.UpdateAppAsync());

        Assert.Equal([current], restarted);
        Assert.Equal(newExe, File.ReadAllBytes(current));
        Assert.True(File.Exists(current + ".old"));
        Assert.False(ui.Window.IsVisible); // closed so the new version takes over
    }

    [AvaloniaFact]
    public void A_development_build_points_to_the_release_page_instead()
    {
        var newExe = TestData.FakeExe("LUNA EVO 9.9.9");
        using var ui = new UiHarness();
        ui.Web.Text(AppUpdater.ReleasesUrl, TestData.AppReleasesJson(newExe));
        ui.Await(ui.Window.CheckForAppUpdateAsync(force: true));

        var message = ui.Await(ui.Window.UpdateAppAsync()); // CurrentExe is null in tests: like `dotnet run`
        Assert.Contains("can't replace itself", message);
        Assert.Contains(AppUpdater.ReleasesPage, message);
    }

    private static List<string> RailButtons(UiHarness ui) =>
        ui.Window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("nav"))
            .Select(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b) ?? "").ToList();

    private static List<string> Badges(Button button) =>
        button.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("badge") && b.IsVisible)
            .Select(b => (b.Child as TextBlock)?.Text ?? "").ToList();

    private static bool Visible(UiHarness ui, string text) =>
        ui.Window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text && t.IsEffectivelyVisible);

    private static List<string> Texts(Control control) =>
        control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
}
