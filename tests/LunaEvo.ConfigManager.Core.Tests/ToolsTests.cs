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
    [Fact]
    public void Reads_only_mod_names_and_versions_from_a_ckan_file()
    {
        var (mods, problems) = CkanModList.Parse("""
            { "spec_version": "v1.6", "identifier": "pack", "kind": "metapackage", "download": "https://evil.example/x.zip",
              "depends": [ { "name": "MechJeb2" }, { "name": "KerbalEngineerRedux", "version": "1.1.9.0" }, { "name": "--gamedir" },
                           { "name": "mechjeb2" }, { "name": "Kopernicus", "version": "1 && del" } ],
              "recommends": [ { "name": "Scatterer" } ] }
            """);
        Assert.Equal([new CkanMod("MechJeb2"), new CkanMod("KerbalEngineerRedux", "1.1.9.0"), new CkanMod("Kopernicus"), new CkanMod("Scatterer")], mods);
        Assert.Contains(problems, p => p.Contains("--gamedir")); // can never be read as an option
        Assert.Contains(problems, p => p.Contains("download link"));

        var command = Assert.Single(CkanModList.InstallCommands("C:\\KSP", mods));
        Assert.Equal(["install", "--headless", "--no-recommends", "--gamedir", "C:\\KSP", "MechJeb2", "KerbalEngineerRedux=1.1.9.0", "Kopernicus", "Scatterer"], command);
    }

    [Fact]
    public void Reads_a_plain_mod_list_and_flags_bad_input()
    {
        var (mods, problems) = CkanModList.Parse("# my mods\nMechJeb2\r\n\n  KerbalEngineerRedux 1.1.9.0\nnot/a/mod\n");
        Assert.Equal(["MechJeb2", "KerbalEngineerRedux"], mods.Select(m => m.Identifier));
        Assert.Single(problems);
        Assert.Contains("isn't a valid .ckan", Assert.Single(CkanModList.Parse("{ broken").Problems));
        Assert.Empty(CkanModList.Parse(null).Mods);

        var many = CkanModList.Parse(string.Join("\n", Enumerable.Range(0, CkanModList.MaxMods + 5).Select(i => "Mod" + i)));
        Assert.Equal(CkanModList.MaxMods, many.Mods.Count);
        Assert.Contains("first 2000", Assert.Single(many.Problems));
    }

    [Fact]
    public void Ckan_arguments_lock_and_installed_count()
    {
        var ksp = Temp("ksp");
        Assert.Equal(["update", "--headless", "--gamedir", ksp], CkanModList.UpdateArguments(ksp));
        var mods = Enumerable.Range(0, 1500).Select(i => new CkanMod("SomeLongModIdentifier" + i)).ToList();
        var commands = CkanModList.InstallCommands(ksp, mods);
        Assert.True(commands.Count > 1); // too long for one Windows command line
        Assert.All(commands, c => Assert.True(string.Join(" ", c).Length < 25_000));
        Assert.Equal(mods.Select(m => m.Identifier), commands.SelectMany(c => c.Skip(5)));
        Assert.All(commands, c => Assert.Equal(["install", "--headless", "--no-recommends", "--gamedir", ksp], c.Take(5)));

        Assert.False(CkanModList.IsLocked(ksp));
        Assert.Null(CkanModList.InstalledCount(ksp));
        Directory.CreateDirectory(Path.Combine(ksp, "CKAN"));
        var lockFile = Path.Combine(ksp, "CKAN", "registry.locked");
        File.WriteAllText(lockFile, Environment.ProcessId.ToString());
        Assert.True(CkanModList.IsLocked(ksp)); // held by a live process
        File.WriteAllText(lockFile, int.MaxValue.ToString());
        Assert.False(CkanModList.IsLocked(ksp)); // left behind by a CKAN that closed
        File.WriteAllText(lockFile, "garbage");
        Assert.True(CkanModList.IsLocked(ksp)); // unreadable: careful

        File.WriteAllText(Path.Combine(ksp, "CKAN", "registry.json"), """{ "installed_modules": { "A": {}, "B": {}, "C": {} } }""");
        Assert.Equal(3, CkanModList.InstalledCount(ksp));
        File.WriteAllText(Path.Combine(ksp, "CKAN", "registry.json"), "{ nope");
        Assert.Null(CkanModList.InstalledCount(ksp));
    }

    [Fact]
    public void A_gamedata_scan_is_reused_for_a_while()
    {
        var gameData = Temp("gamedata");
        Directory.CreateDirectory(Path.Combine(gameData, "Mod"));
        File.WriteAllText(Path.Combine(gameData, "Mod", "a.cfg"), "PART\n{\n name = first.part\n}\n");
        var first = Mods.GameDataScanner.ScanCached(gameData);
        File.WriteAllText(Path.Combine(gameData, "Mod", "b.cfg"), "PART\n{\n name = second.part\n}\n");
        Assert.Same(first, Mods.GameDataScanner.ScanCached(gameData));
        Assert.Same(first, Mods.GameDataScanner.ScanCached(gameData, hashPlugins: false)); // a hashed scan answers both
        Assert.Contains("second.part", Mods.GameDataScanner.Scan(gameData).Parts);
    }

    [Fact]
    public void Settings_are_saved_through_a_temp_file()
    {
        var path = Path.Combine(Temp("settings"), "settings.json");
        AppSettingsStore.Save(new AppSettings { KspFolder = "first" }, path);
        AppSettingsStore.Save(new AppSettings { KspFolder = "second" }, path);
        Assert.Equal("second", AppSettingsStore.Load(path).KspFolder);
        Assert.False(File.Exists(path + ".tmp"));
    }
    [Fact]
    public void Exports_an_installs_own_picks_without_the_windows_user_name()
    {
        var ksp = Temp("ksp");
        Directory.CreateDirectory(Path.Combine(ksp, "CKAN"));
        File.WriteAllText(Path.Combine(ksp, "CKAN", "installed-My KSP.ckan"), """
            { "spec_version": "v1.6", "identifier": "installed-My-KSP", "author": "somebody", "kind": "metapackage",
              "ksp_version_min": "1.8", "ksp_version_max": "1.12",
              "depends": [ { "name": "B9PartSwitch", "version": "v2.21.0.4" }, { "name": "MechJeb2" } ] }
            """);
        var list = CkanModList.InstalledList(ksp)!.Value;
        Assert.Equal([new CkanMod("B9PartSwitch", "v2.21.0.4"), new CkanMod("MechJeb2")], list.Mods);
        Assert.Equal(("1.8", "1.12"), (list.KspMin, list.KspMax));

        var exported = CkanModList.Export(list.Mods, "My KSP", list.KspMin, list.KspMax);
        Assert.DoesNotContain("somebody", exported);
        using var json = System.Text.Json.JsonDocument.Parse(exported);
        Assert.Equal("My-KSP", json.RootElement.GetProperty("identifier").GetString());
        Assert.Equal("metapackage", json.RootElement.GetProperty("kind").GetString());
        Assert.Equal("1.12", json.RootElement.GetProperty("ksp_version_max").GetString());
        Assert.Equal(list.Mods, CkanModList.Parse(exported).Mods); // it imports back the same

        Assert.DoesNotContain("v2.21.0.4", CkanModList.Export(list.Mods, "x", exactVersions: false));
        Assert.Null(CkanModList.InstalledList(Temp("nockan")));
    }

    [Fact]
    public async Task Says_whether_your_own_ckan_exe_is_an_official_build()
    {
        var exe = TestData.FakeExe("ckan 1.36.4");
        var folder = Temp("ownckan");
        var path = Path.Combine(folder, "ckan.exe");
        File.WriteAllBytes(path, exe);
        var http = new FakeHttp().Text(CkanTools.ReleasesUrl, TestData.CkanReleasesJson(exe));

        Assert.True(CkanTools.LooksLikeExe(path));
        Assert.Equal("v1.36.4", await CkanTools.IdentifyAsync(http.Client, path));

        var other = Path.Combine(folder, "other.exe");
        File.WriteAllBytes(other, TestData.FakeExe("something else"));
        Assert.Null(await CkanTools.IdentifyAsync(http.Client, other));

        var notExe = Path.Combine(folder, "notes.exe");
        File.WriteAllText(notExe, "hello");
        Assert.False(CkanTools.LooksLikeExe(notExe));
        Assert.False(CkanTools.LooksLikeExe(Path.Combine(folder, "missing.exe")));
    }
    [Fact]
    public void A_choice_group_is_reported_and_a_minimum_version_is_not_pinned()
    {
        var (mods, problems) = CkanModList.Parse("""
            { "depends": [ { "name": "B9PartSwitch", "suppress_recommendations": true },
                           { "any_of": [ { "name": "Kopernicus" }, { "name": "Kopernicus-BE" } ] },
                           { "name": "MechJeb2", "min_version": "2.14" } ] }
            """);
        Assert.Equal([new CkanMod("B9PartSwitch"), new CkanMod("MechJeb2")], mods);
        Assert.Contains("1 \"one of these\"", Assert.Single(problems));
    }
}
