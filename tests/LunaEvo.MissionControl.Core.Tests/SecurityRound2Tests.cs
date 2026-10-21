// LUNA EVO · LunaEvo.MissionControl.Core.Tests/SecurityRound2Tests.cs
// [Test] The second security audit (0.4.1): capped live info, safe Universe restore, no DTDs, profile names, weak passwords.
// Used by: dotnet test
// Guide:   Meat and Potatoes/14-Security.md

using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using LunaEvo.MissionControl.Core.Files;
using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl.Core.Tests;

public class SecurityRound2Tests
{
    private static string Temp(string name) =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", name + "-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public async Task Live_info_refuses_a_huge_answer_and_cuts_long_text()
    {
        var huge = "[" + new string(' ', ServerInfoClient.MaxResponseBytes + 10) + "]";
        var longName = new string('A', 5000);
        var json = $$"""[{ "CurrentState": { "CurrentPlayers": ["{{longName}}"] }, "GeneralSettings": { "ServerName": "{{longName}}" } }]""";
        var http = new FakeHttp().Text("http://big.example.com:8900/", huge).Text("http://long.example.com:8900/", json);
        var client = new ServerInfoClient(http.Client);

        await Assert.ThrowsAsync<JsonException>(() => client.GetAsync(new Uri("http://big.example.com:8900/")));

        var snapshot = await client.GetAsync(new Uri("http://long.example.com:8900/"));
        Assert.Equal(ServerInfoClient.MaxTextLength, snapshot.ServerName!.Length);
        Assert.Equal(ServerInfoClient.MaxTextLength, snapshot.Players[0].Length);
    }

    [Fact]
    public void Universe_restore_refuses_a_zip_that_escapes_and_leaves_the_universe_alone()
    {
        var server = Temp("uni-escape");
        var universe = Directory.CreateDirectory(Path.Combine(server, "Universe")).FullName;
        File.WriteAllText(Path.Combine(universe, "ship.txt"), "mine");
        var evil = Path.Combine(server, "evil.zip");
        using (var zip = ZipFile.Open(evil, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("ok.txt").Open()))
                writer.Write("fine");
            using (var writer = new StreamWriter(zip.CreateEntry("../outside.txt").Open()))
                writer.Write("escaped");
        }

        Assert.Throws<InvalidDataException>(() => new UniverseBackups(server).Restore(evil));

        Assert.Equal("mine", File.ReadAllText(Path.Combine(universe, "ship.txt"))); // untouched
        Assert.False(File.Exists(Path.Combine(server, "outside.txt")));
        Assert.Empty(Directory.GetDirectories(server, "Universe.restoring-*")); // staging cleaned up
    }

    [Fact]
    public void Universe_restore_of_a_damaged_zip_keeps_the_current_universe()
    {
        var server = Temp("uni-damaged");
        var universe = Directory.CreateDirectory(Path.Combine(server, "Universe")).FullName;
        File.WriteAllText(Path.Combine(universe, "ship.txt"), "mine");
        var broken = Path.Combine(server, "broken.zip");
        File.WriteAllBytes(broken, [0x50, 0x4B, 0x03, 0x04, 1, 2, 3]);

        Assert.ThrowsAny<Exception>(() => new UniverseBackups(server).Restore(broken));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(universe, "ship.txt")));
    }

    [Fact]
    public void Config_files_with_a_dtd_are_refused()
    {
        var path = Path.Combine(Temp("dtd"), "GeneralSettings.xml");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE lol [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">]>
            <GeneralSettingsDefinition><ServerName>&b;</ServerName></GeneralSettingsDefinition>
            """);
        Assert.Throws<XmlException>(() => ConfigFile.Load(path));
    }

    [Theory]
    [InlineData("CON", "CON-profile")]
    [InlineData("nul.txt", "nul.txt-profile")]
    [InlineData("COM1", "COM1-profile")]
    [InlineData("Console", "Console")]
    [InlineData("..", "Profile")]
    public void Profile_names_never_become_windows_device_names(string name, string folder) =>
        Assert.Equal(folder, ProfileStore.SafeName(name));

    [Fact]
    public void Profile_names_are_kept_to_a_sane_length() =>
        Assert.Equal(80, ProfileStore.SafeName(new string('x', 500)).Length);

    [Fact]
    public void Short_passwords_get_a_warning_but_still_save()
    {
        var session = ConfigSession.Open(TestPaths.LmpServer, TestPaths.CopyConfigs());
        session.SetValue(LmpRules.General, "AdminPassword", "1234");
        session.SetValue(LmpRules.General, "Password", "123");

        var issues = session.Validate().Where(i => i.FileName == LmpRules.General).ToList();
        Assert.Contains(issues, i => i.Setting == "AdminPassword" && i.Severity == Severity.Warning);
        Assert.Contains(issues, i => i.Setting == "Password" && i.Severity == Severity.Warning);
        Assert.DoesNotContain(issues, i => i.Severity == Severity.Error);

        session.SetValue(LmpRules.General, "AdminPassword", "correct horse battery");
        session.SetValue(LmpRules.General, "Password", "");
        Assert.DoesNotContain(session.Validate(), i => i.Setting is "AdminPassword" or "Password");
    }

    [Fact]
    public void Says_when_the_server_is_reachable_from_the_internet()
    {
        var session = ConfigSession.Open(TestPaths.LmpServer, TestPaths.CopyConfigs());
        session.SetValue(LmpRules.Connection, "Upnp", "false");
        session.SetValue("MasterServerSettings.xml", "RegisterWithMasterServer", "false");
        Assert.Empty(LmpRules.InternetExposure(session));

        session.SetValue(LmpRules.Connection, "Upnp", "true");
        session.SetValue(LmpRules.Website, "EnableWebsite", "true");
        session.SetValue("MasterServerSettings.xml", "RegisterWithMasterServer", "true");
        var reasons = LmpRules.InternetExposure(session);
        Assert.Equal(2, reasons.Count);
        Assert.Contains("TCP", reasons[0]); // UPnP opens the website port too
    }

    [Fact]
    public void Generated_passwords_are_long_random_and_pass_the_warning()
    {
        var a = LmpRules.GeneratePassword();
        Assert.Equal(20, a.Length);
        Assert.NotEqual(a, LmpRules.GeneratePassword());
        Assert.All(a, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
        Assert.True(a.Length >= LmpRules.MinAdminPassword);
    }

    [Fact]
    public void A_hand_edited_settings_file_with_nulls_still_loads()
    {
        var path = Path.Combine(Temp("settings"), "settings.json");
        File.WriteAllText(path, """{ "AccentColor": null, "Theme": null, "SavedServers": null, "DailyRestartTime": null }""");
        var settings = AppSettingsStore.Load(path);
        Assert.Equal(AppSettings.DefaultAccent, settings.AccentColor);
        Assert.Equal("Dark", settings.Theme);
        Assert.Empty(settings.SavedServers);
        Assert.Equal("", settings.DailyRestartTime);
    }
}
