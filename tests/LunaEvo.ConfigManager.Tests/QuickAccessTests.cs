// LUNA EVO · LunaEvo.ConfigManager.Tests/QuickAccessTests.cs
// [Test] 1.4 quick access: versions in the rail, clickable warnings and cards, quick edit, simple mode, server browser,
//        allowing a player's parts on your own server.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Join;
using LunaEvo.ConfigManager.Core.Mods;
using LunaEvo.ConfigManager.Core.Tests;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Tests;

public class QuickAccessTests
{
    [AvaloniaFact]
    public void Rail_lists_every_version_in_use()
    {
        using var ui = new UiHarness();
        var ksp = TestData.FakeKsp();
        File.WriteAllText(Path.Combine(ksp, "readme.txt"), "Version 1.12.5");
        ui.Window.UpdateSettings(s => s with { KspFolder = ksp });
        ui.Window.ShowVersions();
        var text = ui.Find<TextBlock>("status/versions").Text!;
        Assert.Contains("LUNA EVO " + SettingsPage.AppVersion, text);
        Assert.Contains("LMP server 0.29.2", text);
        Assert.Contains("KSP 1.12.5", text);
        Assert.Contains("LMP client not installed", text);
        Assert.Contains("CKAN not downloaded", text);
    }

    [AvaloniaFact]
    public void Clicking_the_warnings_lists_them_and_goes_to_the_setting()
    {
        using var ui = new UiHarness();
        ui.Session.SetValue(LmpRules.General, "AdminPassword", "123");
        ui.Window.ShowIssues();
        var chip = ui.Find<Button>($"issues/{LmpRules.General}/AdminPassword");
        ui.Click(chip);
        Assert.True(ui.Find<Control>($"{LmpRules.General}/AdminPassword").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Console_cards_edit_a_setting_in_place_and_save_it()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.ConsolePage);
        Dialogs.PromptResponder = (_, _) => "Mun Club";
        ui.Await(ui.Window.QuickEditAsync(LmpRules.General, "ServerName", "Server name"));
        Assert.Equal("Mun Club", ConfigFile.Load(Path.Combine(ui.ConfigDirectory, LmpRules.General)).GetValue("ServerName"));
        Assert.False(ui.Session.IsDirty);

        Dialogs.PromptResponder = (_, _) => "70000"; // not a valid port: refused, nothing changes
        ui.Await(ui.Window.QuickEditAsync(LmpRules.Connection, "Port", "Game port"));
        Assert.Contains("Not saved", ui.DialogsShown);
        Assert.NotEqual("70000", ui.Session.GetValue(LmpRules.Connection, "Port"));
    }

    [AvaloniaFact]
    public void Vessel_cards_filter_the_list()
    {
        using var ui = new UiHarness();
        TestData.AddVessels(ui.ServerDirectory);
        ui.OpenTab(MainWindow.VesselsKey);
        var all = Rows(ui).Count;
        ui.Click(ui.Find<Button>("vessels/card/Debris"));
        var debris = Rows(ui).Count;
        Assert.InRange(debris, 1, all - 1);
        Assert.Contains("selected", ui.Find<Button>("vessels/card/Debris").Classes);
        ui.Click(ui.Find<Button>("vessels/card/Debris")); // again: everything
        Assert.Equal(all, Rows(ui).Count);
    }

    [AvaloniaFact]
    public void Simple_mode_hides_the_advanced_pages_and_tabs()
    {
        using var ui = new UiHarness();
        Assert.True(ui.Find<Button>("nav/vessels").IsVisible);
        ui.Window.UpdateSettings(s => s with { SimpleMode = true });
        foreach (var hidden in new[] { "nav/players", "nav/vessels", "nav/LMPModControl.xml", "nav/profiles", "nav/crafts" })
            Assert.False(ui.TryFind<Button>(hidden)?.IsVisible ?? false, hidden);
        ui.OpenTab(MainWindow.ServerSettingsKey);
        Assert.True(ui.Find<Button>("nav/" + MainWindow.EssentialsKey).IsVisible);
        Assert.False(ui.Find<Button>("nav/" + LmpRules.Gameplay).IsVisible);
        Assert.True(ui.Find<Control>($"{LmpRules.General}/ServerName").IsEffectivelyVisible); // on Essentials
        ui.Window.UpdateSettings(s => s with { SimpleMode = false });
        Assert.True(ui.Find<Button>("nav/vessels").IsVisible);
    }

    [AvaloniaFact]
    public void Server_browser_lists_public_servers_and_joins_one()
    {
        using var ui = new UiHarness(openServer: false);
        ui.Web.Text(ServerBrowser.MasterLists[0], TestData.MasterJson);
        ui.OpenTab(MainWindow.BrowserKey);
        ui.Await(ui.Window.Browser.LoadAsync());
        ui.Find<CheckBox>("browser/myVersion").IsChecked = false;
        Assert.StartsWith("2 public servers from 1 of", ui.Find<TextBlock>("browser/status").Text);
        Assert.NotNull(ui.Find<Control>("browser/server/203.0.113.5:8800"));
        ui.Find<CheckBox>("browser/noPassword").IsChecked = true;
        Assert.Null(ui.TryFind<Control>("browser/server/198.51.100.7:8810"));

        ui.Click(ui.Find<Button>("browser/join/203.0.113.5:8800"));
        ui.WaitFor(() => ui.Window.Join.Server != null, "join page checked the server");
        Assert.Equal("203.0.113.5", ui.Window.Join.AddressBox.Text); // 8800 is the default, so it isn't written
    }

    [AvaloniaFact]
    public void Check_my_mods_can_allow_the_missing_parts_on_your_own_server()
    {
        using var ui = new UiHarness();
        var result = new ModCheckResult([], ["my.new.part"], ["MyResource"]);
        ui.Await(ui.Window.Join.AllowOnMyServerAsync(result));
        Assert.Contains("Allow these on your server?", ui.DialogsShown);
        var saved = ConfigSession.Open(ui.ServerDirectory, ui.ConfigDirectory);
        Assert.Contains("my.new.part", saved.GetList(MainWindow.ModControlFile, "AllowedParts"));
        Assert.Contains("MyResource", saved.GetList(MainWindow.ModControlFile, "AllowedResources"));
    }

    private static List<Control> Rows(UiHarness ui) =>
        ui.Window.GetVisualDescendants().OfType<Control>().Where(c => c.Name == null && (Avalonia.Automation.AutomationProperties.GetAutomationId(c) ?? "").StartsWith("vessels/row/")).ToList();
}
