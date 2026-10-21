// LUNA EVO · LunaEvo.MissionControl.Core.Tests/ServerSupportTests.cs
// [Test] Log parsing, log tailing, .NET runtime check, app settings and backup pruning.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Collections.Concurrent;
using LunaEvo.MissionControl.Core.Files;
using LunaEvo.MissionControl.Core.Server;

namespace LunaEvo.MissionControl.Core.Tests;

public class ServerSupportTests
{
    [Theory]
    [InlineData("[23:59:54][LMP]: All systems up and running. Поехали!", "23:59:54", LogKind.Normal, "All systems up and running. Поехали!")]
    [InlineData("[00:06:06][Warning]: You're using default settings", "00:06:06", LogKind.Warning, "You're using default settings")]
    [InlineData("[04:02:48][NetworkDebug]: ping", "04:02:48", LogKind.Network, "ping")]
    [InlineData("[04:02:48][VerboseNetwork]: bytes", "04:02:48", LogKind.Network, "bytes")]
    [InlineData("[04:02:48][Chat]: Jeb: hi", "04:02:48", LogKind.Chat, "Jeb: hi")]
    public void Parses_lmp_log_lines(string raw, string time, LogKind kind, string message)
    {
        var line = LogLine.Parse(raw);
        Assert.Equal((time, kind, message), (line.Time, line.Kind, line.Message));
        Assert.Equal(raw, line.ToString());
    }

    [Fact]
    public void Real_server_logs_parse_with_known_levels()
    {
        var logs = LogFiles.List(TestPaths.LmpServer);
        Assert.NotEmpty(logs);

        var lines = logs.SelectMany(f => LogFiles.Read(f.FullName)).ToList();
        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Message.Contains("Luna Server version"));
        Assert.True(lines.Count(l => l.Time != null) > lines.Count / 2, "Most lines should have LMP's [time][type] prefix");
    }

    [Fact]
    public async Task Log_tail_follows_the_newest_file_and_partial_lines()
    {
        var server = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "tail-" + Guid.NewGuid().ToString("N"));
        var logs = Directory.CreateDirectory(LogFiles.Folder(server)).FullName;
        var path = Path.Combine(logs, "lmpserver_2026-10-20_10-00-00.log");
        File.WriteAllText(path, "[10:00:00][LMP]: old 1\n[10:00:01][LMP]: old 2\n");

        var lines = new ConcurrentQueue<LogLine>();
        using var tail = new LogTail(server, initialLines: 1, interval: TimeSpan.FromHours(1));
        tail.LineReceived += lines.Enqueue;
        tail.Poll();
        Assert.Equal(["old 2"], lines.Select(l => l.Message));

        await using (var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)))
        {
            await writer.WriteAsync("[10:00:02][Error]: new\n[10:00:03][LMP]: half");
        }
        tail.Poll();
        Assert.Equal(["old 2", "new"], lines.Select(l => l.Message));
        Assert.Equal(LogKind.Error, lines.Last().Kind);

        File.AppendAllText(path, " done\n");
        tail.Poll();
        Assert.Equal("half done", lines.Last().Message);
    }

    [Fact]
    public void Detects_the_net6_runtime_the_server_needs()
    {
        var requirement = DotNetRuntime.Check(TestPaths.LmpServer);
        Assert.NotNull(requirement);
        Assert.Equal(6, requirement.Major);
        Assert.Equal(DotNetRuntime.InstalledVersions().Any(v => v.Major == 6), requirement.Installed);
        Assert.Null(DotNetRuntime.Check(Path.GetTempPath()));
    }

    [Fact]
    public void App_settings_round_trip_and_survive_a_corrupt_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "settings.json");
        Assert.Equal(new AppSettings(), AppSettingsStore.Load(path));

        var settings = new AppSettings
        {
            AccentColor = "#FF8800", Theme = "Light", BackupsToKeep = 3, LastServerDirectory = @"C:\LMP",
            SavedServers = [new SavedServer("Kerbin Space Agency", "kerbin.example.com", 9000)],
        };
        AppSettingsStore.Save(settings, path);
        var saved = File.ReadAllText(path);
        AppSettingsStore.Save(AppSettingsStore.Load(path), path);
        Assert.Equal(saved, File.ReadAllText(path)); // records compare lists by reference, so compare what's stored
        Assert.Equal(9000, AppSettingsStore.Load(path).SavedServers.Single().Port);

        File.WriteAllText(path, "{ not json");
        Assert.Equal(new AppSettings(), AppSettingsStore.Load(path));
    }

    [Fact]
    public void Old_backups_are_pruned()
    {
        var config = TestPaths.CopyConfigs();
        var file = Path.Combine(config, "GeneralSettings.xml");
        for (var i = 0; i < 5; i++)
            BackupService.Backup(config, [file], new DateTime(2026, 10, 20, 10, 0, i));

        BackupService.Prune(config, keep: 2);

        var left = Directory.GetDirectories(Path.Combine(config, BackupService.BackupFolderName)).Select(Path.GetFileName).Order();
        Assert.Equal(["2026-10-20_10-00-03", "2026-10-20_10-00-04"], left);

        BackupService.Prune(config, keep: 0);
        Assert.Equal(2, Directory.GetDirectories(Path.Combine(config, BackupService.BackupFolderName)).Length);
    }
}
