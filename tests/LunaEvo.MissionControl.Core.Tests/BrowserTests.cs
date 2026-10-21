// LUNA EVO · LunaEvo.MissionControl.Core.Tests/BrowserTests.cs
// [Test] The server browser: master server JSON read defensively and merged; KSP's game version.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.MissionControl.Core.Join;

namespace LunaEvo.MissionControl.Core.Tests;

public class BrowserTests
{
    [Fact]
    public async Task Merges_master_server_lists_and_reads_them_defensively()
    {
        var http = new FakeHttp().Text("https://a.example/json", TestData.MasterJson).Text("https://b.example/json", TestData.MasterJson); // c.example: 404
        ServerBrowser.MasterLists = ["https://a.example/json", "https://b.example/json", "https://c.example/json"];
        try
        {
            var (servers, answered) = await new ServerBrowser(http.Client).GetAsync();
            Assert.Equal(2, answered);
            Assert.Equal(2, servers.Count); // the same servers from two masters are listed once; broken entries skipped
            var club = servers.Single(s => s.Port == 8800);
            Assert.Equal("Kerbin Club", club.Name); // control characters removed
            Assert.Equal(("Career", 3, 20, "203.0.113.5:8800"), (club.GameMode, club.Players, club.MaxPlayers, club.Address));
            Assert.Equal("https://example.com/kerbin", club.Website);
            var fun = servers.Single(s => s.Port == 8810);
            Assert.Equal(("Sandbox", 0, true), (fun.GameMode, fun.Players, fun.Password));
            Assert.Null(fun.Website); // only http(s) links
        }
        finally
        {
            ServerBrowser.MasterLists = ["https://ms.lmp.dasskelett.dev/json", "https://ms-nightly.lmp.dasskelett.dev/json", "https://lmp.nightshade.fun/json", "https://lmp.nightshade.fun/nightly/json"];
        }
    }

    [Theory]
    [InlineData("1.2.3.4:8800", "1.2.3.4", 8800)]
    [InlineData("[2001:db8::1]:8800", "2001:db8::1", 8800)]
    [InlineData("kerbin.example:8800", null, 0)] // master servers give IPs; anything else is skipped
    [InlineData("1.2.3.4:0", null, 0)]
    public void Reads_endpoints(string text, string? host, int port) =>
        Assert.Equal(host == null ? null : (host, port), ServerBrowser.Endpoint(text));

    [Fact]
    public void Reads_kerbals_game_version_from_the_readme()
    {
        var ksp = TestData.FakeKsp();
        Assert.Null(KspLauncher.GameVersion(ksp));
        File.WriteAllText(Path.Combine(ksp, "readme.txt"), "Kerbal Space Program\n\nVersion 1.12.5\n\nSome notes about Version 2 of something");
        Assert.Equal("1.12.5", KspLauncher.GameVersion(ksp));
    }
    [Fact]
    public void Ports_outside_1_to_65535_are_errors()
    {
        var session = ConfigSession.Open(TestPaths.LmpServer, TestPaths.CopyConfigs());
        session.SetValue(Validation.LmpRules.Connection, "Port", "70000");
        session.SetValue(Validation.LmpRules.Website, "Port", "0");
        var issues = session.Validate();
        Assert.Contains(issues, i => i.Setting == "Port" && i.FileName == Validation.LmpRules.Connection && i.Severity == Validation.Severity.Error);
        Assert.Contains(issues, i => i.Setting == "Port" && i.FileName == Validation.LmpRules.Website && i.Severity == Validation.Severity.Error);
    }
}
