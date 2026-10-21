// LUNA EVO · LunaEvo.MissionControl.Core.Tests/AppUpdaterTests.cs
// [Test] LUNA EVO updating itself: picking the release, verified download, the exe swap, clean-up, dev builds.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Net;
using LunaEvo.MissionControl.Core.Setup;

namespace LunaEvo.MissionControl.Core.Tests;

public class AppUpdaterTests
{
    private static string TempExe(string text)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "app-" + Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(dir, AppUpdater.ExeName);
        File.WriteAllBytes(exe, TestData.FakeExe(text));
        return exe;
    }

    [Fact]
    public async Task Finds_the_newest_verifiable_release_newer_than_this_one()
    {
        var exe = TestData.FakeExe("new");
        var http = new FakeHttp().Text(AppUpdater.ReleasesUrl, TestData.AppReleasesJson(exe));
        var updater = new AppUpdater(http.Client);

        var update = await updater.CheckAsync(new Version(0, 5, 0));
        Assert.Equal(("v9.9.9", new Version(9, 9, 9)), (update!.Release.Tag, update.Version)); // not the pre-release
        Assert.Contains("New things", update.Release.Notes);

        Assert.Null(await updater.CheckAsync(new Version(9, 9, 9))); // already newest
        Assert.Null(await new AppUpdater(new FakeHttp().Text(AppUpdater.ReleasesUrl, TestData.AppReleasesJson(exe, withDigest: false)).Client)
            .CheckAsync(new Version(0, 5, 0))); // no checksum: never offered
    }

    [Fact]
    public async Task Explains_when_github_wont_show_the_releases()
    {
        var hidden = new AppUpdater(new FakeHttp().Status(AppUpdater.ReleasesUrl, HttpStatusCode.NotFound).Client);
        var e = await Assert.ThrowsAsync<AppUpdateUnavailableException>(() => hidden.CheckAsync(new Version(0, 5, 0)));
        Assert.Contains("private", e.Message);
    }

    [Fact]
    public async Task Downloads_verifies_and_swaps_the_running_exe()
    {
        var newExe = TestData.FakeExe("new version");
        var http = new FakeHttp().Text(AppUpdater.ReleasesUrl, TestData.AppReleasesJson(newExe)).Bytes(TestData.AppExeUrl, newExe);
        var updater = new AppUpdater(http.Client);
        var current = TempExe("old version");

        var update = (await updater.CheckAsync(new Version(0, 5, 0)))!;
        var downloaded = await updater.DownloadAsync(update);
        var old = AppUpdater.Install(downloaded, current, update);

        Assert.Equal(newExe, File.ReadAllBytes(current));
        Assert.Equal(TestData.FakeExe("old version"), File.ReadAllBytes(old));
        Assert.False(File.Exists(current + ".new"));

        AppUpdater.CleanUp(current); // what the new version does on startup
        Assert.False(File.Exists(old));
    }

    [Fact]
    public async Task Refuses_a_download_that_doesnt_match_and_leaves_the_app_alone()
    {
        var published = TestData.FakeExe("what GitHub says");
        var http = new FakeHttp().Text(AppUpdater.ReleasesUrl, TestData.AppReleasesJson(published))
            .Bytes(TestData.AppExeUrl, TestData.FakeExe("tampered on the way"));
        var updater = new AppUpdater(http.Client);
        var current = TempExe("old version");
        var update = (await updater.CheckAsync(new Version(0, 5, 0)))!;

        await Assert.ThrowsAsync<DownloadVerificationException>(() => updater.DownloadAsync(update));

        // And if the file changes after downloading, the swap checks again.
        var swapped = Path.Combine(Path.GetDirectoryName(current)!, "download.exe");
        File.WriteAllBytes(swapped, TestData.FakeExe("swapped later"));
        Assert.Throws<DownloadVerificationException>(() => AppUpdater.Install(swapped, current, update));
        Assert.Equal(TestData.FakeExe("old version"), File.ReadAllBytes(current));
        Assert.False(File.Exists(current + ".old"));
        Assert.False(File.Exists(current + ".new"));
    }

    [Fact]
    public void Refuses_something_that_isnt_a_program()
    {
        var notExe = System.Text.Encoding.ASCII.GetBytes("<html>surprise</html>");
        var current = TempExe("old version");
        var download = Path.Combine(Path.GetDirectoryName(current)!, "download.exe");
        File.WriteAllBytes(download, notExe);
        var update = new AppUpdate(new LmpRelease("v9.9.9", new Version(9, 9, 9), false, DateTimeOffset.Now, "", []),
            new LmpAsset(AppUpdater.ExeName, TestData.AppExeUrl, notExe.Length, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(notExe))));

        Assert.Throws<DownloadVerificationException>(() => AppUpdater.Install(download, current, update));
        Assert.Equal(TestData.FakeExe("old version"), File.ReadAllBytes(current));
    }

    [Fact]
    public void Only_the_published_single_file_exe_updates_itself()
    {
        var exe = TempExe("x");
        Assert.True(AppUpdater.CanUpdateItself(exe));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe)!, "LunaEvo.MissionControl.dll"), "");
        Assert.False(AppUpdater.CanUpdateItself(exe)); // a development build
        Assert.False(AppUpdater.CanUpdateItself(@"C:\Program Files\dotnet\dotnet.exe"));
        Assert.False(AppUpdater.CanUpdateItself(null));
    }
    [Fact]
    public async Task Finds_updates_published_under_the_old_exe_name_and_prefers_the_new_one()
    {
        var exe = TestData.FakeExe("v9.9.9");
        var legacyOnly = new FakeHttp().Text(AppUpdater.ReleasesUrl, TestData.AppReleasesJson(exe, assetName: AppUpdater.LegacyExeName));
        Assert.Equal(AppUpdater.LegacyExeName, (await new AppUpdater(legacyOnly.Client).CheckAsync(new Version(1, 4, 0)))!.Exe.Name);

        // Both names on one release (what release.ps1 publishes from 2.0): the new one is used.
        var both = TestData.AppReleasesJson(exe).Replace("\"assets\":[", "\"assets\":[{\"name\":\"LunaEvo.ConfigManager.exe\",\"browser_download_url\":\"https://github.com/x/old.exe\",\"size\":1,\"digest\":\"sha256:00\"},");
        var update = await new AppUpdater(new FakeHttp().Text(AppUpdater.ReleasesUrl, both).Client).CheckAsync(new Version(1, 4, 0));
        Assert.Equal(AppUpdater.ExeName, update!.Exe.Name);
    }

    [Fact]
    public void A_copy_still_called_LunaEvo_ConfigManager_exe_updates_itself()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "legacy-" + Guid.NewGuid().ToString("N"))).FullName;
        var exe = Path.Combine(folder, AppUpdater.LegacyExeName);
        File.WriteAllBytes(exe, TestData.FakeExe("1.4.0"));
        Assert.True(AppUpdater.CanUpdateItself(exe));
        Assert.False(AppUpdater.CanUpdateItself(Path.Combine(folder, "Something.exe")));
    }

    [Fact]
    public void Settings_from_before_the_rename_are_copied_over_once()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "migrate-" + Guid.NewGuid().ToString("N"))).FullName;
        var legacy = Path.Combine(root, "ConfigManager", "settings.json");
        var current = Path.Combine(root, "MissionControl", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        AppSettingsStore.Save(new AppSettings { KspFolder = "K" }, legacy);

        Assert.True(AppSettingsStore.MigrateFrom(legacy, current));
        Assert.Equal("K", AppSettingsStore.Load(current).KspFolder);
        Assert.True(File.Exists(legacy)); // copied, not moved
        AppSettingsStore.Save(new AppSettings { KspFolder = "changed" }, current);
        Assert.False(AppSettingsStore.MigrateFrom(legacy, current)); // only once
        Assert.Equal("changed", AppSettingsStore.Load(current).KspFolder);
    }
}
