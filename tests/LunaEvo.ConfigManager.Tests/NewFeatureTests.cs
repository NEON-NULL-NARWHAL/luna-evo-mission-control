// LUNA EVO · LunaEvo.ConfigManager.Tests/NewFeatureTests.cs
// [Test] 0.3 features through the UI: setup wizard, updater, players, mod import, object lists, review, profiles,
//        Universe backups, connect page, auto-restart, daily restart.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Ops;
using LunaEvo.ConfigManager.Core.Server;
using LunaEvo.ConfigManager.Core.Setup;
using LunaEvo.ConfigManager.Core.Tests;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Tests;

public class NewFeatureTests
{
    private const string Website = "http://127.0.0.1:8900/";
    private static string WebJson => File.ReadAllText(Path.Combine(TestPaths.Fixtures, "Web", "serverinfo.json"));

    // ---- Setup wizard and updates

    [AvaloniaFact]
    public void Wizard_downloads_lmp_writes_the_config_and_opens_the_server()
    {
        using var ui = new UiHarness(openServer: false);
        var zip = File.ReadAllBytes(TestData.MakeServerZip());
        ui.Web.Text(LmpReleaseFeed.ReleasesUrl, TestData.ReleasesJson(zip)).Bytes(TestData.ServerZipUrl, zip);
        ui.Answer = (title, buttons) => title.StartsWith(".NET") ? buttons.Count - 1 : null; // never run winget in tests

        ui.Click(ui.Find<Button>("welcome/setup"));
        ui.WaitFor(() => ui.Window.Wizard != null && ui.Find<ComboBox>("wizard/version").ItemCount > 0, "releases loaded");
        var version = ui.Find<ComboBox>("wizard/version");
        Assert.Equal("0.31.0", ((LmpRelease)version.SelectedItem!).Tag); // newest stable first
        version.SelectedItem = version.Items.Cast<LmpRelease>().Single(r => r.Tag == "0.29.2");

        var folder = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "wizard-" + Guid.NewGuid().ToString("N"));
        ui.Find<TextBox>("wizard/folder").Text = folder;
        ui.Find<TextBox>("wizard/name").Text = "Jeb's Junkyard";
        ui.Find<ComboBox>("wizard/mode").SelectedItem = "Career";
        ui.Find<ComboBox>("wizard/difficulty").SelectedItem = "Hard";
        ui.Click(ui.Find<Button>("wizard/install"));

        ui.WaitFor(() => ui.Window.Session != null, "server opened");
        var session = ui.Window.Session!;
        Assert.Equal(folder, session.ServerDirectory);
        Assert.Equal("Jeb's Junkyard", session.GetValue(LmpRules.General, "ServerName"));
        Assert.Equal("Career", session.GetValue(LmpRules.General, "GameMode"));
        Assert.Equal("false", session.GetValue("MasterServerSettings.xml", "RegisterWithMasterServer")); // private by default
        Assert.Equal("false", session.GetValue(LmpRules.Connection, "Upnp"));
        Assert.Equal("127.0.0.1", session.GetValue(LmpRules.Website, "ListenAddress"));
        Assert.Equal("false", session.GetValue(LmpRules.General, "ModControl"));
        Assert.Equal(13, Directory.GetFiles(Path.Combine(folder, "Config"), "*.xml").Length);
        Assert.Contains(ui.Find<Button>("nav/" + MainWindow.ConsolePage).Classes, c => c == "selected");
    }

    [AvaloniaFact]
    public void Update_check_offers_a_newer_lmp_and_updates_keeping_the_config()
    {
        using var ui = new UiHarness(fakeServer: true);
        var zip = File.ReadAllBytes(TestData.MakeServerZip());
        ui.Web.Text(LmpReleaseFeed.ReleasesUrl, TestData.ReleasesJson(zip)).Bytes(TestData.ServerZipUrl, zip);
        var config = File.ReadAllBytes(Path.Combine(ui.ConfigDirectory, LmpRules.General));

        ui.Await(ui.Window.CheckForUpdateAsync(force: true));
        ui.Pump();

        Assert.Equal("0.31.0", ui.Window.AvailableUpdate?.Tag);
        Assert.Equal("LMP 0.31.0 is available", ui.Find<TextBlock>("status/update").Text);

        ui.OpenTab(MainWindow.SettingsKey);
        ui.Click(ui.Find<Button>("settings/updateServer"));
        ui.WaitFor(() => Directory.Exists(Path.Combine(ui.ServerDirectory, "LunaEvo", "ServerBackups")) &&
                         Texts(ui.Window).Any(t => t.StartsWith("Updated to LMP 0.31.0")), "update finished");

        Assert.Single(Directory.GetFiles(Path.Combine(ui.ServerDirectory, "LunaEvo", "ServerBackups"), "server-0.29.2_*.zip"));
        Assert.Equal(config, File.ReadAllBytes(Path.Combine(ui.ConfigDirectory, LmpRules.General)));
    }

    // ---- Players

    [AvaloniaFact]
    public void Players_page_shows_who_is_online_and_kicks_with_a_reason()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.Web.Text(Website, WebJson);
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");

        ui.OpenTab(MainWindow.PlayersKey);
        ui.Await(ui.Window.Players!.PollAsync());
        ui.Pump();

        Assert.NotNull(ui.Find<Border>("players/row/Jebediah"));
        Assert.Equal("2 / 20", CardValue(ui, "players/card/Players"));
        Assert.Equal("2", CardValue(ui, "players/card/Vessels"));
        Assert.True(Texts(ui.Window).Any(t => t == "Mun Base Alpha"), string.Join(" | ", Texts(ui.Window)));
        Assert.Contains(Texts(ui.Window), t => t.Contains("Station · Mun · 1.5 km"));

        Dialogs.PromptResponder = (title, _) => title == "Kick Jebediah?" ? "AFK too long" : null;
        ui.Click(ui.Find<Button>("players/kick/Jebediah"));
        ui.WaitFor(() => ui.Server.VisibleLines.Any(l => l.Message == "Command input: /kick Jebediah AFK too long"), "kick command reached the server");
    }

    [AvaloniaFact]
    public void Players_page_offers_to_turn_the_website_on_for_this_pc_only()
    {
        using var ui = new UiHarness();
        ui.OpenTab(LmpRules.Website);
        ui.Find<ToggleSwitch>($"{LmpRules.Website}/EnableWebsite").IsChecked = false;
        ui.Pump();

        ui.OpenTab(MainWindow.PlayersKey);
        ui.Click(ui.Find<Button>("players/enableWebsite"));

        Assert.Equal("true", ui.Session.GetValue(LmpRules.Website, "EnableWebsite"));
        Assert.Equal("127.0.0.1", ui.Session.GetValue(LmpRules.Website, "ListenAddress"));
    }

    // ---- Mod control

    [AvaloniaFact]
    public void Plugin_lists_are_editable_and_saved_as_lmp_dllfile_entries()
    {
        const string file = "LMPModControl.xml";
        using var ui = new UiHarness();
        ui.OpenTab(file);

        ui.Click(ui.Find<Button>($"{file}/MandatoryPlugins/add"));
        var path = ui.Find<TextBox>($"{file}/MandatoryPlugins/0/FilePath");
        path.Text = "kerbalengineer/kerbalengineer.dll";
        path.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        ui.Pump();

        Assert.True(ui.Session.IsModified(file, "MandatoryPlugins"));
        ui.Click(ui.Find<Button>("save"));
        var saved = ConfigFile.Load(Path.Combine(ui.ConfigDirectory, file)).GetObjectList("MandatoryPlugins")!;
        Assert.Equal("kerbalengineer/kerbalengineer.dll", Assert.Single(saved)["FilePath"]);
        Assert.Contains("<DllFile>", File.ReadAllText(Path.Combine(ui.ConfigDirectory, file)));

        ui.Click(ui.Find<Button>($"{file}/MandatoryPlugins/0/remove"));
        Assert.Empty(ui.Session.GetObjectList(file, "MandatoryPlugins"));
    }

    [AvaloniaFact]
    public void Gamedata_import_fills_parts_resources_and_plugins()
    {
        const string file = "LMPModControl.xml";
        var gameData = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "kspimport-" + Guid.NewGuid().ToString("N"), "GameData")).FullName;
        Directory.CreateDirectory(Path.Combine(gameData, "MyMod", "Plugins"));
        File.WriteAllText(Path.Combine(gameData, "MyMod", "parts.cfg"), "PART { name = myMod_tank }\nRESOURCE_DEFINITION { name = Unobtainium }");
        File.Copy(Path.Combine(TestPaths.LmpServer, "Lidgren.Core.dll"), Path.Combine(gameData, "MyMod", "Plugins", "MyMod.dll"));
        Directory.CreateDirectory(Path.Combine(gameData, "SquadExpansion", "Serenity"));

        using var ui = new UiHarness();
        ui.OpenTab(file);
        ui.Window.ModImport!.PathBox.Text = Path.GetDirectoryName(gameData); // the KSP folder itself is fine too
        ui.Find<ToggleSwitch>("modimport/expansions").IsChecked = true;
        ui.Await(ui.Window.ModImport.ImportAsync());
        ui.Pump();

        Assert.Equal(["myMod.tank"], ui.Session.GetList(file, "AllowedParts"));
        Assert.Equal(["Unobtainium"], ui.Session.GetList(file, "AllowedResources"));
        Assert.Equal(["Serenity"], ui.Session.GetList(file, "RequiredExpansions"));
        Assert.Equal("mymod/plugins/mymod.dll", Assert.Single(ui.Session.GetObjectList(file, "OptionalPlugins"))["FilePath"]);
        Assert.StartsWith("Found 1 parts, 1 resources, 1 plugins and 1 expansions", ui.Find<TextBlock>("modimport/result").Text);

        // The review dialog summarises list changes instead of dumping hundreds of part names.
        var review = MainWindow.DescribeChanges(ui.Session.GetChanges());
        var before = ConfigFile.Load(Path.Combine(ui.ConfigDirectory, file)).GetList("AllowedParts")!.Distinct().Count(); // 531: the file has 16 duplicates
        Assert.Contains($"LMP Mod Control › Allowed Parts: 1 added, {before} removed", review);
    }

    // ---- Review before save

    [AvaloniaFact]
    public void Save_shows_old_and_new_values_and_can_be_cancelled()
    {
        using var ui = new UiHarness();
        ui.OpenTab(LmpRules.General);
        var before = ui.Session.GetValue(LmpRules.General, "ServerName");
        ui.Find<TextBox>($"{LmpRules.General}/ServerName").Text = "LUNA EVO";
        ui.Pump();

        string? shown = null;
        Dialogs.Responder = (title, message, buttons) =>
        {
            shown = message;
            return buttons.Count - 1; // Cancel
        };
        ui.Click(ui.Find<Button>("save"));

        Assert.Contains($"General › Server Name: {before} → LUNA EVO", shown);
        Assert.True(ui.Session.IsDirty);
        Assert.DoesNotContain("<ServerName>LUNA EVO</ServerName>", File.ReadAllText(Path.Combine(ui.ConfigDirectory, LmpRules.General)));
    }

    // ---- Profiles and Universe backups

    [AvaloniaFact]
    public void Profiles_save_and_switch_the_whole_config()
    {
        using var ui = new UiHarness(fakeServer: true);
        var original = ui.Session.GetValue(LmpRules.General, "ServerName");
        Dialogs.PromptResponder = (_, _) => "Weekend";
        ui.OpenTab(MainWindow.ProfilesKey);
        ui.Click(ui.Find<Button>("profiles/save"));
        Assert.NotNull(ui.Find<Border>("profiles/row/Weekend"));

        ui.OpenTab(LmpRules.General);
        ui.Find<TextBox>($"{LmpRules.General}/ServerName").Text = "Something else";
        ui.Pump();
        ui.Click(ui.Find<Button>("save"));

        ui.OpenTab(MainWindow.ProfilesKey);
        ui.Click(ui.Find<Button>("profiles/apply/Weekend"));
        ui.WaitFor(() => ui.DialogsShown.Contains("Profile applied"), "profile applied");

        Assert.Equal(original, ui.Window.Session!.GetValue(LmpRules.General, "ServerName"));
        Assert.Contains(original!, File.ReadAllText(Path.Combine(ui.ConfigDirectory, LmpRules.General)));
    }

    [AvaloniaFact]
    public void Universe_can_be_backed_up_and_restored_only_while_stopped()
    {
        using var ui = new UiHarness(fakeServer: true);
        var universe = Directory.CreateDirectory(Path.Combine(ui.ServerDirectory, "Universe", "Vessels")).FullName;
        File.WriteAllText(Path.Combine(universe, "KerbalX.txt"), "before");

        ui.OpenTab(MainWindow.ProfilesKey);
        ui.Click(ui.Find<Button>("profiles/backupNow"));
        ui.WaitFor(() => new UniverseBackups(ui.ServerDirectory).List().Count == 1, "backup made");
        File.WriteAllText(Path.Combine(universe, "KerbalX.txt"), "after");
        ui.Window.Profiles!.Refresh();
        ui.Pump();

        var snapshot = new UniverseBackups(ui.ServerDirectory).List()[0];
        ui.Click(ui.Find<Button>("profiles/restore/" + snapshot.Name));
        // The restore runs in the background and swaps folders, so the file can be missing for a moment.
        ui.WaitFor(() => File.Exists(Path.Combine(universe, "KerbalX.txt")) && Read(Path.Combine(universe, "KerbalX.txt")) == "before", "restored");
        Assert.Equal(2, new UniverseBackups(ui.ServerDirectory).List().Count); // plus the safety copy
    }

    // ---- Connect

    [AvaloniaFact]
    public void Connect_page_lists_addresses_and_reachability()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.ConnectKey);

        foreach (var address in NetworkHelper.LocalAddresses())
            Assert.NotNull(ui.Find<Border>("connect/address/" + address.Address));
        Assert.Contains(Texts(ui.Find<Border>("connect/public")), t => t.StartsWith("Internet address"));
        Assert.Contains(Texts(ui.Window), t => t == "Not on the public server list" || t == "Listed on the public server list");
        Assert.NotNull(ui.Find<Border>("connect/firewallRow"));
        Assert.Equal("Server is stopped", Texts(ui.Find<Border>("connect/listening"))[0]);
    }

    // ---- Automation

    [AvaloniaFact]
    public void Crashes_restart_automatically_then_give_up()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.Window.UpdateSettings(s => s with { AutoRestartOnCrash = true });
        ServerPage.ExtraArguments = ["--crash"];
        ServerPage.RestartDelay = TimeSpan.FromMilliseconds(100);

        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));

        ui.WaitFor(() => ui.Server.VisibleLines.Any(l => l.Message.Contains("keeps crashing")), "auto-restart gave up", seconds: 30);
        Assert.Equal(4, ui.Server.VisibleLines.Count(l => l.Message.StartsWith("All systems up"))); // first start + 3 restarts
    }

    [AvaloniaFact]
    public void Daily_restart_warns_players_then_restarts_cleanly()
    {
        using var ui = new UiHarness(fakeServer: true);
        var due = DateTime.Today.AddDays(1).AddHours(4);
        ui.Window.UpdateSettings(s => s with { DailyRestartTime = "04:00" });
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");

        ui.Await(ui.Server.CheckDailyRestartAsync(due.AddHours(-2)));
        Assert.Equal(due, ui.Server.NextDailyRestart);

        ui.Await(ui.Server.CheckDailyRestartAsync(due.AddSeconds(-30)));
        ui.WaitFor(() => ui.Server.VisibleLines.Any(l => l.Message.StartsWith("Command input: /say Scheduled server restart in 1 minute")), "warning reached the players");

        var restart = ui.Server.CheckDailyRestartAsync(due);
        ui.WaitFor(() => restart.IsCompleted, "restart finished");
        Assert.Contains(ui.Server.VisibleLines, l => l.Message == "Server exited (code 0).");
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "running again");
        Assert.Equal(due.AddDays(1), ui.Server.NextDailyRestart);
    }

    [AvaloniaFact]
    public void Scheduled_universe_backup_asks_lmp_to_save_then_zips()
    {
        using var ui = new UiHarness(fakeServer: true);
        Directory.CreateDirectory(Path.Combine(ui.ServerDirectory, "Universe", "Kerbals"));
        File.WriteAllText(Path.Combine(ui.ServerDirectory, "Universe", "Kerbals", "Jeb.txt"), "brave");
        ui.Window.UpdateSettings(s => s with { UniverseBackupHours = 1 });
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");

        var backup = ui.Server.CheckUniverseBackupAsync(DateTime.Now.AddHours(2), settle: TimeSpan.Zero);
        ui.WaitFor(() => backup.IsCompleted, "backup done");

        Assert.Equal("scheduled", Assert.Single(new UniverseBackups(ui.ServerDirectory).List()).Label);
        ui.WaitFor(() => ui.Server.VisibleLines.Any(l => l.Message == "Command input: /backup now"), "LMP asked to save first");
    }

    /// <summary>The file's text, or null while it's being swapped in.</summary>
    private static string? Read(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static List<string> Texts(Control control) =>
        control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();

    private static string CardValue(UiHarness ui, string id) => Texts(ui.Find<Border>(id)).Last();
}
