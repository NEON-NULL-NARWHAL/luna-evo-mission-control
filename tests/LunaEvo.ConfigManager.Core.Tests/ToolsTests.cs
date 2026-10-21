// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/ToolsTests.cs
// [Test] CKAN copies (verified download, versions), CKAN's instances and launch options, the LMP client installer.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.IO.Compression;
using LunaEvo.ConfigManager.Core.Setup;

namespace LunaEvo.ConfigManager.Core.Tests;

public class ToolsTests
{
    private static string Temp(string name) =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", name + "-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public async Task Downloads_verified_ckan_versions_into_its_own_folder()
    {
        CkanTools.Root = Temp("ckanroot");
        var exe = TestData.FakeExe("ckan 1.36.4");
        var http = new FakeHttp().Text(CkanTools.ReleasesUrl, TestData.CkanReleasesJson(exe)).Bytes(TestData.CkanExeUrl, exe);

        var available = await CkanTools.AvailableAsync(http.Client);
        var (release, asset) = Assert.Single(available); // the release without a checksum isn't offered
        Assert.Equal("v1.36.4", release.Tag);

        var path = await CkanTools.DownloadAsync(http.Client, release, asset);
        Assert.Equal(Path.Combine(CkanTools.Root, "v1.36.4", "ckan.exe"), path);
        Assert.Equal(exe, File.ReadAllBytes(path));
        Assert.Equal("v1.36.4", Assert.Single(CkanTools.Installed()).Version);
        CkanTools.Remove("v1.36.4");
        Assert.Empty(CkanTools.Installed());
    }

    [Fact]
    public void Reads_ckans_instances_and_launch_options()
    {
        CkanTools.ConfigFolder = Temp("ckancfg");
        var ksp = TestData.FakeKsp();
        File.WriteAllText(Path.Combine(CkanTools.ConfigFolder, "config.json"),
            "{ \"GameInstances\": [ { \"Name\": \"Career modded\", \"Path\": " + System.Text.Json.JsonSerializer.Serialize(ksp) + " }, { \"Name\": \"Gone\", \"Path\": \"C:\\nope\" } ] }");
        Assert.Equal([new CkanInstance("Career modded", ksp)], CkanTools.Instances()); // missing folders left out

        Directory.CreateDirectory(Path.Combine(ksp, "CKAN"));
        File.WriteAllText(Path.Combine(ksp, "CKAN", "GUIConfig.json"),
            "{ \"CommandLines\": [ \"KSP_x64.exe -single-instance -popupwindow\" ] }");
        Assert.Equal(["-single-instance", "-popupwindow"], CkanTools.LaunchOptions(ksp));
        Assert.Empty(CkanTools.LaunchOptions(Temp("nockan")));
    }

    private static (string Zip, string Sha) ClientZip(params (string Name, string Text)[] extra)
    {
        var path = Path.Combine(Temp("client"), "LunaMultiplayer-Client-Release.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var (name, text) in new[] { ("GameData/LunaMultiplayer/Plugins/LmpClient.dll", "new client"), ("GameData/000_Harmony/0Harmony.dll", "lmp's harmony") }.Concat(extra))
                using (var writer = new StreamWriter(zip.CreateEntry(name).Open()))
                    writer.Write(text);
        }
        return (path, ServerInstaller.Sha256Of(path));
    }

    [Fact]
    public void Installs_the_lmp_client_keeping_settings_and_never_replacing_harmony()
    {
        var ksp = TestData.FakeKsp(); // has GameData/LunaMultiplayer/Data/settings.xml (the server list)
        var (zip, sha) = ClientZip();

        Assert.NotNull(LmpClientInstaller.Install(zip, ksp, sha)); // the existing LunaMultiplayer folder (just Data) is backed up
        var gameData = Path.Combine(ksp, "GameData");
        Assert.Equal("new client", File.ReadAllText(Path.Combine(gameData, "LunaMultiplayer", "Plugins", "LmpClient.dll")));
        Assert.Equal("lmp's harmony", File.ReadAllText(Path.Combine(gameData, "000_Harmony", "0Harmony.dll"))); // added: was missing
        Assert.True(File.Exists(Path.Combine(gameData, "LunaMultiplayer", "Data", "settings.xml"))); // kept

        File.WriteAllText(Path.Combine(gameData, "000_Harmony", "0Harmony.dll"), "ckan's newer harmony");
        var backup = LmpClientInstaller.Install(zip, ksp, sha);
        Assert.True(Directory.Exists(backup));
        Assert.DoesNotContain(gameData, backup); // never inside GameData, where KSP would load it
        Assert.Equal("ckan's newer harmony", File.ReadAllText(Path.Combine(gameData, "000_Harmony", "0Harmony.dll"))); // untouched
        Assert.True(File.Exists(Path.Combine(gameData, "LunaMultiplayer", "Data", "settings.xml")));
    }

    [Fact]
    public void Refuses_a_tampered_or_escaping_client_zip()
    {
        var ksp = TestData.FakeKsp();
        var (zip, _) = ClientZip();
        Assert.Throws<DownloadVerificationException>(() => LmpClientInstaller.Install(zip, ksp, new string('0', 64)));
        var (evil, evilSha) = ClientZip(("GameData/LunaMultiplayer/../../../evil.dll", "x"));
        Assert.Throws<InvalidDataException>(() => LmpClientInstaller.Install(evil, ksp, evilSha));
        Assert.False(File.Exists(Path.Combine(ksp, "..", "evil.dll")));
        Assert.Throws<InvalidOperationException>(() => LmpClientInstaller.Install(zip, Temp("notksp"), "x"));
    }
}
