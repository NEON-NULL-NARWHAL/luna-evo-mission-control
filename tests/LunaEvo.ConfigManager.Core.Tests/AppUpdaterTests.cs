// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/AppUpdaterTests.cs
// [Test] LUNA EVO updating itself: picking the release, verified download, the exe swap, clean-up, dev builds.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Net;
using LunaEvo.ConfigManager.Core.Setup;

namespace LunaEvo.ConfigManager.Core.Tests;

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
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe)!, "LunaEvo.ConfigManager.dll"), "");
        Assert.False(AppUpdater.CanUpdateItself(exe)); // a development build
        Assert.False(AppUpdater.CanUpdateItself(@"C:\Program Files\dotnet\dotnet.exe"));
        Assert.False(AppUpdater.CanUpdateItself(null));
    }
}
