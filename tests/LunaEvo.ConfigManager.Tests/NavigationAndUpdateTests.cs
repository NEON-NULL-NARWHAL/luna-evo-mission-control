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
        Assert.Equal(["nav/console", "nav/players", "nav/vessels", "nav/connect", "nav/serverSettings", "nav/LMPModControl.xml", "nav/profiles", "nav/join", "nav/crafts", "nav/settings"], rail);

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
