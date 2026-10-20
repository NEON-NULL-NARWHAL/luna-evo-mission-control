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
        Assert.Equal(["nav/console", "nav/players", "nav/connect", "nav/serverSettings", "nav/LMPModControl.xml", "nav/profiles", "nav/join", "nav/settings"], rail);

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
