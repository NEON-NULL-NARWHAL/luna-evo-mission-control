// LUNA EVO · LunaEvo.ConfigManager.Tests/PlayerFeaturesUiTests.cs
// [Test] 0.5 through the UI: player history and alerts from a running server, bans, notifications, the mod check, sharing rules.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LunaEvo.ConfigManager.Core.Server;
using LunaEvo.ConfigManager.Core.Tests;

namespace LunaEvo.ConfigManager.Tests;

public class PlayerFeaturesUiTests
{
    private static void Send(UiHarness ui, string command)
    {
        ui.Find<TextBox>("console/command").Text = command;
        ui.Click(ui.Find<Button>("console/send"));
    }

    [AvaloniaFact]
    public void A_running_server_builds_history_and_alerts_on_admin_password_guessing()
    {
        using var ui = new UiHarness(fakeServer: true);
        ui.Window.UpdateSettings(s => s with { NotifyPlayerActivity = true });
        ui.OpenTab(MainWindow.ConsolePage);
        ui.Click(ui.Find<Button>("console/start"));
        ui.WaitFor(() => ui.Server.State == ServerState.Running, "server running");
        ui.WaitFor(() => ui.Notifications.Any(n => n.Title.EndsWith(" is up")), "server up notification");

        Send(ui, "/fakejoin Valentina");
        ui.WaitFor(() => ui.Window.Tracker!.Online.Contains("Valentina"), "join seen");
        for (var i = 0; i < 3; i++)
        {
            Send(ui, "/fakeguess Valentina");
            Thread.Sleep(600); // LMP takes one command every 500 ms
        }
        ui.WaitFor(() => ui.Notifications.Any(n => n.Alert && n.Title == "Someone is guessing the admin password"), "alert");

        ui.OpenTab(MainWindow.PlayersKey);
        ui.Pump();
        Assert.Contains("is guessing the admin password", Texts(ui.Find<Border>("players/alert/AdminPasswordGuessing/Valentina"))[0]);
        Assert.Contains(ui.Notifications, n => n.Title == "Valentina joined");
        var history = Texts(ui.Find<Border>("players/history/Valentina"));
        Assert.Contains(history, t => t.Contains("1 session") && t.Contains("LMP 0.29.2") && t.Contains("3 wrong admin passwords"));

        ui.OpenTab(MainWindow.ConsolePage);
        Send(ui, "/fakeleave Valentina");
        ui.WaitFor(() => ui.Window.Tracker!.Online.Count == 0, "leave seen");
        Assert.DoesNotContain("203.0.113.9", File.ReadAllText(ui.Window.Tracker!.FilePath)); // IPs never kept

        ui.Click(ui.Find<Button>("console/stop"));
        ui.WaitFor(() => ui.Server.State == ServerState.Stopped, "stopped");
        ui.WaitFor(() => ui.Notifications.Any(n => n.Title.EndsWith(" stopped")), "stopped notification");
    }

    [AvaloniaFact]
    public void Joins_are_only_notified_when_asked_but_alerts_always_are()
    {
        using var ui = new UiHarness();
        var tracker = ui.Window.Tracker!;
        tracker.Observe(LogLine.Parse("[12:00:00][LMP]: Client Jeb (j1) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5"));
        tracker.Observe(LogLine.Parse("[12:00:01][LMP]: Client Bill (b1) failed to handshake: Banned. Disconnecting"));
        ui.Pump();

        Assert.DoesNotContain(ui.Notifications, n => n.Title == "Jeb joined");
        Assert.Contains(ui.Notifications, n => n.Alert && n.Title == "A banned player tried to join");

        ui.Window.UpdateSettings(s => s with { NotifyDesktop = false });
        tracker.Observe(LogLine.Parse("[12:00:02][LMP]: Client Bill (b1) failed to handshake: Banned. Disconnecting"));
        ui.Pump();
        Assert.Single(ui.Notifications, n => n.Title == "A banned player tried to join"); // Windows notifications off
    }

    [AvaloniaFact]
    public void Bans_are_listed_by_name_and_can_be_lifted_while_the_server_is_stopped()
    {
        using var ui = new UiHarness();
        File.WriteAllLines(Path.Combine(ui.ServerDirectory, "LMPPlayerBans.txt"), ["b1", "zz-unknown"]);
        ui.Window.Tracker!.Observe(LogLine.Parse("[12:00:00][LMP]: Client Bill (b1) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5"));
        ui.Window.Tracker.Observe(LogLine.Parse("[12:00:05][LMP]: Player 'Bill' was banned from the server: griefing"));
        ui.OpenTab(MainWindow.PlayersKey);
        ui.Pump();

        Assert.Equal(["Bill", "LMP id b1 · griefing"], Texts(ui.Find<Border>("players/ban/b1")).Take(2));
        Assert.Equal("Unknown player", Texts(ui.Find<Border>("players/ban/zz-unknown"))[0]);
        Assert.Contains(Texts(ui.Find<Border>("players/history/Bill")), t => t.Contains("banned: griefing"));

        ui.Click(ui.Find<Button>("players/unban/b1"));
        ui.Pump();
        Assert.Contains("Unban Bill?", ui.DialogsShown);
        Assert.Equal(["zz-unknown"], File.ReadAllLines(Path.Combine(ui.ServerDirectory, "LMPPlayerBans.txt")));
        Assert.Null(ui.TryFind<Border>("players/ban/b1"));
    }

    [AvaloniaFact]
    public void Player_notes_are_kept_with_the_server()
    {
        using var ui = new UiHarness();
        ui.Window.Tracker!.ObserveOnline(["Jeb"]);
        ui.OpenTab(MainWindow.PlayersKey);
        ui.Pump();
        Dialogs.PromptResponder = (_, _) => "Builds the best rovers";

        ui.Click(ui.Find<Button>("players/note/Jeb"));
        ui.Pump();

        Assert.Equal("Builds the best rovers", ui.Window.Tracker.Find("Jeb")!.Note);
        Assert.Contains(Texts(ui.Find<Border>("players/history/Jeb")), t => t.Contains("Note: Builds the best rovers"));
        Dialogs.PromptResponder = null;
    }

    [AvaloniaFact]
    public void A_discord_webhook_is_stored_encrypted_and_posts_safely()
    {
        if (!Core.Ops.SecretProtector.IsSupported)
            return;
        const string webhook = "https://discord.com/api/webhooks/123/very-secret";
        using var ui = new UiHarness();
        ui.Web.Text(webhook, "");
        ui.OpenTab(MainWindow.SettingsKey);

        ui.Find<TextBox>("settings/discordWebhook").Text = "https://not-discord.example/hook";
        ui.Click(ui.Find<Button>("settings/discordSave"));
        Assert.StartsWith("That isn't a Discord webhook URL", ui.Find<TextBlock>("settings/discordResult").Text);

        ui.Find<TextBox>("settings/discordWebhook").Text = webhook;
        ui.Click(ui.Find<Button>("settings/discordSave"));
        Assert.Equal("", ui.Find<TextBox>("settings/discordWebhook").Text); // never shown again
        Assert.Equal("A webhook is saved.", ui.Find<TextBlock>("settings/discordResult").Text);
        Assert.DoesNotContain("very-secret", File.ReadAllText(ui.SettingsPath));

        ui.Window.Tracker!.Observe(LogLine.Parse("[12:00:01][LMP]: Client Everyone (e1) failed to handshake: Banned. Disconnecting"));
        ui.Pump();
        ui.WaitFor(() => ui.Web.Requests.Any(r => r.Url == webhook), "Discord post");
        var body = JsonDocument.Parse(ui.Web.Requests.First(r => r.Url == webhook).Body).RootElement;
        Assert.Equal(0, body.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());

        ui.Click(ui.Find<Button>("settings/discordRemove"));
        Assert.Null(ui.Window.Settings.DiscordWebhookProtected);
    }

    [AvaloniaFact]
    public void Players_check_their_mods_against_the_servers_rules()
    {
        using var ui = new UiHarness(openServer: false);
        var ksp = TestData.FakeKsp();
        var lmpData = Path.Combine(ksp, "GameData", "LunaMultiplayer", "Data");
        // The rules LMP saved the last time this player joined a mod-controlled server.
        File.Copy(TestData.ModRules(allowNonListed: false, mandatory: TestData.RuleDll("kerbalengineer/engineer.dll")),
            Path.Combine(lmpData, "LMPModControl.xml"));
        var mod = Directory.CreateDirectory(Path.Combine(ksp, "GameData", "RandomMod", "Plugins")).FullName;
        File.Copy(Path.Combine(TestPaths.LmpServer, "Lidgren.Core.dll"), Path.Combine(mod, "Random.dll"));

        ui.Window.Navigate(MainWindow.JoinKey);
        ui.Window.Join.KspBox.Text = ksp;
        ui.Pump();
        Assert.Equal(Path.Combine(lmpData, "LMPModControl.xml"), ui.Window.Join.ModRulesBox.Text); // found by itself

        ui.Await(ui.Window.Join.CheckModsAsync());
        ui.Pump();

        Assert.StartsWith("2 problems LMP will stop you joining for", ui.Find<TextBlock>("join/modSummary").Text);
        Assert.Equal("Not allowed: randommod/plugins/random.dll", Texts(ui.Find<Border>("join/modProblem/randommod/plugins/random.dll"))[0]);
        Assert.Equal("Missing mod: kerbalengineer/engineer.dll", Texts(ui.Find<Border>("join/modProblem/kerbalengineer/engineer.dll"))[0]);
    }

    [AvaloniaFact]
    public void Hosts_share_their_saved_mod_rules()
    {
        using var ui = new UiHarness();
        ui.OpenTab("LMPModControl.xml");
        var target = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "for-friends.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        ModImportPanel.PickSharePath = _ => Task.FromResult<string?>(target);

        ui.Await(ui.Window.ModImport!.ShareAsync());

        Assert.Equal(File.ReadAllText(Path.Combine(ui.ConfigDirectory, "LMPModControl.xml")), File.ReadAllText(target));
        Assert.StartsWith("Saved for-friends.xml", ui.Find<TextBlock>("modimport/shareResult").Text);
    }

    private static List<string> Texts(Control control) =>
        control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
}
