// LUNA EVO · LunaEvo.MissionControl.Core.Tests/PlayerFeaturesTests.cs
// [Test] 0.5: player history and alerts from LMP's log lines, LMP's ban list, the player mod check, safe notifications.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Text.Json;
using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Mods;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.MissionControl.Core.Server;

namespace LunaEvo.MissionControl.Core.Tests;

public class PlayerFeaturesTests
{
    private static string Temp(string name) =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", name + "-" + Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>A line exactly as LMP 0.29.2 prints it.</summary>
    private static LogLine Lmp(string message, string type = "LMP") => LogLine.Parse($"[12:00:00][{type}]: {message}");

    private sealed class Clock
    {
        public DateTime Now { get; set; } = new(2026, 10, 20, 20, 0, 0);
    }

    // ---- Player history

    [Fact]
    public void Builds_history_from_lmps_own_log_lines_and_the_website()
    {
        var clock = new Clock();
        var server = Temp("history");
        var tracker = new PlayerTracker(server, () => clock.Now);
        var events = new List<PlayerEvent>();
        tracker.EventRaised += events.Add;

        tracker.Observe(Lmp("Client Jebediah (a1b2c3) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5"));
        tracker.ObserveOnline(["Jebediah"]); // the website agrees: not a second join
        clock.Now = clock.Now.AddMinutes(90);
        tracker.ObserveOnline([]); // gone from the website: left

        var jeb = Assert.Single(tracker.Players);
        Assert.Equal(("Jebediah", 1, 90.0, "0.29.2", "1.12.5"), (jeb.Name, jeb.Sessions, jeb.MinutesPlayed, jeb.LmpVersion, jeb.KspVersion));
        Assert.Equal(["a1b2c3"], jeb.Ids);
        Assert.Equal([PlayerEventKind.Joined, PlayerEventKind.Left], events.Select(e => e.Kind));

        // Kept next to the server, and read back.
        var reloaded = new PlayerTracker(server, () => clock.Now);
        Assert.Equal(90.0, Assert.Single(reloaded.Players).MinutesPlayed);
        Assert.Equal(2, reloaded.Events.Count);
    }

    [Fact]
    public void Ignores_replayed_lines_and_chat_and_never_keeps_ip_addresses()
    {
        var server = Temp("ignore");
        var tracker = new PlayerTracker(server);

        tracker.Observe(Lmp("Client Old (x) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5") with { Replayed = true });
        tracker.Observe(Lmp("Client Fake (x) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5", "Chat"));
        Assert.Empty(tracker.Players);

        tracker.Observe(Lmp("Client Bob (id9) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5"));
        tracker.Observe(Lmp("Client Bob disconnected in ReceiveCallback, Endpoint 203.0.113.9:51000, Error: timed out"));
        Assert.Empty(tracker.Online);
        Assert.DoesNotContain("203.0.113.9", File.ReadAllText(tracker.FilePath));
    }

    [Fact]
    public void Alerts_once_when_someone_keeps_guessing_the_admin_password()
    {
        var clock = new Clock();
        var tracker = new PlayerTracker(Temp("guess"), () => clock.Now);
        var alerts = new List<PlayerEvent>();
        tracker.EventRaised += e => { if (e.Kind == PlayerEventKind.AdminPasswordGuessing) alerts.Add(e); };

        for (var i = 0; i < 50; i++)
        {
            tracker.Observe(Lmp("Valentina: Tried to run an admin command with an invalid password", "Warning"));
            clock.Now = clock.Now.AddSeconds(2);
        }

        var alert = Assert.Single(alerts); // one alert per 10 minutes, however fast they guess
        Assert.Equal("Valentina", alert.Player);
        Assert.Contains("3 wrong admin passwords", alert.Detail);
        Assert.Equal(50, tracker.Find("Valentina")!.WrongAdminPasswords);
        Assert.True(tracker.Events.Count < 10); // guesses don't flood the history

        clock.Now = clock.Now.AddMinutes(11);
        for (var i = 0; i < 3; i++)
            tracker.Observe(Lmp("Valentina: Tried to run an admin command with an invalid password", "Warning"));
        Assert.Equal(2, alerts.Count);

        // The same text as chat or an Info line isn't LMP's warning.
        tracker.Observe(Lmp("Bill: Tried to run an admin command with an invalid password", "Chat"));
        Assert.Null(tracker.Find("Bill"));
    }

    [Fact]
    public void Records_bans_kicks_and_banned_players_trying_again()
    {
        var tracker = new PlayerTracker(Temp("bans"));
        var events = new List<PlayerEvent>();
        tracker.EventRaised += events.Add;

        tracker.Observe(Lmp("Client Bill (b1) handshake successful, LMP Version: 0.29.2, KSP Version: 1.12.5"));
        tracker.Observe(Lmp("Kicking Bill from the server"));
        tracker.Observe(Lmp("Player 'Bill' was banned from the server: griefing the KSC"));
        tracker.Observe(Lmp("Client Bill (b1) failed to handshake: Banned. Disconnecting"));

        Assert.Equal([PlayerEventKind.Joined, PlayerEventKind.Kicked, PlayerEventKind.Banned, PlayerEventKind.BannedPlayerTried], events.Select(e => e.Kind));
        Assert.Equal("griefing the KSC", tracker.Find("Bill")!.BanReason);
        Assert.Same(tracker.FindById("b1")!.Name, tracker.Find("Bill")!.Name);
    }

    [Fact]
    public void Server_stopping_ends_every_session_and_notes_are_kept()
    {
        var clock = new Clock();
        var tracker = new PlayerTracker(Temp("stop"), () => clock.Now);
        tracker.ObserveOnline(["Jeb", "Bill"]);
        clock.Now = clock.Now.AddMinutes(30);
        tracker.ServerStopped();
        tracker.SetNote("Jeb", "  Builds the best rovers  ");

        Assert.Empty(tracker.Online);
        Assert.All(tracker.Players, p => Assert.Equal(30.0, p.MinutesPlayed));
        Assert.Equal("Builds the best rovers", tracker.Find("Jeb")!.Note);
        tracker.ObserveOnline(["<script>", "two words"]); // not valid LMP names
        Assert.Equal(2, tracker.Players.Count);
    }

    [Fact]
    public void A_damaged_history_file_starts_over()
    {
        var server = Temp("damaged");
        Directory.CreateDirectory(Path.Combine(server, "LunaEvo"));
        File.WriteAllText(Path.Combine(server, "LunaEvo", "players.json"), "{ not json");
        Assert.Empty(new PlayerTracker(server).Players);
    }

    // ---- LMP's ban list

    [Fact]
    public void Unbans_by_id_with_a_backup()
    {
        var server = Temp("banlist");
        File.WriteAllLines(Path.Combine(server, "LMPPlayerBans.txt"), ["id-one", "", "id-two", "id-one"]);
        var bans = new BanList(server);
        Assert.Equal(["id-one", "id-two"], bans.Ids());

        Assert.True(bans.Unban("id-one"));
        Assert.Equal(["id-two"], bans.Ids());
        Assert.False(bans.Unban("nobody"));
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(server, "LunaEvo", "Backups"), "LMPPlayerBans_*.txt"));
        Assert.Contains("id-one", File.ReadAllText(backup));
    }

    // ---- Mod check (player side)

    private static GameDataScan Scan(params PluginFile[] plugins) =>
        new(["mk1pod.v2", "MyRover"], ["LiquidFuel", "Unobtainium"], plugins, ["MakingHistory"], 0, []);

    [Fact]
    public void Mod_check_mirrors_lmps_client_rules()
    {
        var rules = ModRules.Load(TestData.ModRules(allowNonListed: false,
            mandatory: TestData.RuleDll("kerbalengineer/engineer.dll", "AA-BB") + TestData.RuleDll("mechjeb2/mechjeb2.dll"),
            optional: TestData.RuleDll("scansat/scansat.dll", "NOT-CHECKED"),
            forbidden: TestData.RuleDll("hyperedit/hyperedit.dll", element: "ForbiddenDllFile"),
            parts: "<string>mk1pod.v2</string>",
            mandatoryParts: "<MandatoryPart><PartName>KerbalX</PartName></MandatoryPart>",
            expansions: "<string>MakingHistory</string><string>Serenity</string>"));

        var result = ModCheck.Check(rules, Scan(
            new PluginFile("kerbalengineer/engineer.dll", "CC-DD", ""),  // wrong version
            new PluginFile("scansat/scansat.dll", "ANY", ""),            // optional: hash never checked
            new PluginFile("hyperedit/hyperedit.dll", "", ""),           // banned
            new PluginFile("randommod/random.dll", "", "")));            // not listed

        Assert.False(result.Passes);
        Assert.Equal(
            [(ModProblemKind.MissingExpansion, "Serenity"), (ModProblemKind.ForbiddenPlugin, "hyperedit/hyperedit.dll"),
             (ModProblemKind.PluginNotAllowed, "randommod/random.dll"), (ModProblemKind.WrongPluginVersion, "kerbalengineer/engineer.dll"),
             (ModProblemKind.MissingPlugin, "mechjeb2/mechjeb2.dll"), (ModProblemKind.MissingPart, "KerbalX")],
            result.Problems.Select(p => (p.Kind, p.Item)));
        Assert.Equal(["MyRover"], result.PartsNotAllowed);       // joins, but vessels with it are dropped
        Assert.Equal(["Unobtainium"], result.ResourcesNotAllowed);
    }

    [Fact]
    public void Mod_check_passes_a_matching_install_and_refuses_files_that_arent_mod_rules()
    {
        var rules = ModRules.Load(TestData.ModRules(allowNonListed: true, parts: "<string>mk1pod.v2</string><string>MyRover</string>"));
        var result = ModCheck.Check(rules, Scan(new PluginFile("anything/goes.dll", "", "")));
        Assert.True(result.Passes);
        Assert.Empty(result.PartsNotAllowed);

        var notRules = Path.Combine(Temp("notrules"), "settings.xml");
        File.WriteAllText(notRules, "<?xml version=\"1.0\" encoding=\"utf-8\"?><SettingStructure />");
        Assert.Throws<InvalidDataException>(() => ModRules.Load(notRules));
    }

    // ---- Notifications

    [Theory]
    [InlineData("@everyone", "@​everyone")]
    [InlineData("**bold** [link](https://evil.example)", @"\*\*bold\*\* \[link\]\(https\:​//evil.example\)")]
    [InlineData("Jeb_Kerman", @"Jeb\_Kerman")]
    public void Discord_text_from_outside_is_made_literal(string text, string escaped) =>
        Assert.Equal(escaped, DiscordNotifier.Escape(text));

    [Fact]
    public async Task Discord_posts_can_never_mention_anyone()
    {
        const string webhook = "https://discord.com/api/webhooks/123/abc";
        var http = new FakeHttp().Text(webhook, "");
        var sent = await new DiscordNotifier(http.Client).SendAsync(webhook,
            new Notification(NotificationKind.PlayerJoined, "@everyone joined", "Kerbin Space Agency", "@everyone"));

        Assert.True(sent);
        var body = JsonDocument.Parse(Assert.Single(http.Requests).Body).RootElement;
        Assert.Equal(0, body.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
        Assert.DoesNotContain("@everyone", body.GetProperty("content").GetString()!, StringComparison.Ordinal); // culture-aware compares ignore the zero-width space
        Assert.False(DiscordNotifier.IsWebhookUrl("https://user:pass@discord.com/api/webhooks/1/x"));
        Assert.False(await new DiscordNotifier(http.Client).PostAsync("https://evil.example/api/webhooks/1/x", "hi"));
    }

    [Fact]
    public void Secrets_are_encrypted_for_this_windows_account()
    {
        if (!SecretProtector.IsSupported)
            return;
        const string webhook = "https://discord.com/api/webhooks/123/very-secret";
        var stored = SecretProtector.Protect(webhook)!;
        Assert.DoesNotContain("very-secret", stored);
        Assert.Equal(webhook, SecretProtector.Unprotect(stored));
        Assert.Null(SecretProtector.Unprotect("bm90IGVuY3J5cHRlZA==")); // not ours
        Assert.Null(SecretProtector.Unprotect("%%%"));

        var path = Path.Combine(Temp("settings"), "settings.json");
        AppSettingsStore.Save(new AppSettings { DiscordWebhookProtected = stored }, path);
        Assert.DoesNotContain("very-secret", File.ReadAllText(path));
        Assert.Equal(webhook, SecretProtector.Unprotect(AppSettingsStore.Load(path).DiscordWebhookProtected));
    }
}
