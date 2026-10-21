// LUNA EVO · LunaEvo.MissionControl.Core.Tests/ServerRunnerTests.cs
// [Test] Starting, commanding and stopping a (fake) server for real, including the kill fallback.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Collections.Concurrent;
using LunaEvo.MissionControl.Core.Server;

namespace LunaEvo.MissionControl.Core.Tests;

public class ServerRunnerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Starts_streams_output_takes_commands_and_stops_cleanly_with_ctrl_c()
    {
        using var runner = new ServerRunner(FakeServer.CreateServerFolder(), FakeServer.Executable);
        var lines = new ConcurrentQueue<LogLine>();
        runner.LineReceived += lines.Enqueue;

        runner.Start();
        Assert.NotNull(runner.ProcessId);
        await Until(() => runner.State == ServerState.Running);
        Assert.Contains(lines, l => l is { Kind: LogKind.Normal } && l.Message.StartsWith("All systems up and running"));
        Assert.Contains(lines, l => l.Kind == LogKind.Debug);

        runner.SendCommand("/help");
        runner.SendCommand("hello kerbals");
        await Until(() => lines.Any(l => l.Kind == LogKind.Chat && l.Message == "Server: hello kerbals"));
        Assert.Contains(lines, l => l.Message == "Command input: /help");

        var graceful = await runner.StopAsync(Wait);

        Assert.True(graceful);
        await Until(() => runner.State == ServerState.Stopped);
        Assert.Contains(lines, l => l.Message == "So long and thanks for all the fish!");
        Assert.Contains(lines, l => l.Kind == LogKind.Manager && l.Message == "Server exited (code 0).");
        Assert.Null(runner.ProcessId);
    }

    [Fact]
    public async Task Kills_the_server_when_it_ignores_ctrl_c()
    {
        using var runner = new ServerRunner(FakeServer.CreateServerFolder(), FakeServer.Executable);
        var lines = new ConcurrentQueue<LogLine>();
        runner.LineReceived += lines.Enqueue;

        runner.Start(["--ignore-ctrl-c"]);
        await Until(() => runner.State == ServerState.Running);

        var graceful = await runner.StopAsync(TimeSpan.FromSeconds(2));

        Assert.False(graceful);
        await Until(() => runner.State == ServerState.Stopped);
        Assert.Contains(lines, l => l.Message == "Ignoring Ctrl+C");
        Assert.Contains(lines, l => l.Kind == LogKind.Manager && l.Message.Contains("was killed"));
    }

    [Fact]
    public async Task Startup_failure_is_shown_and_the_server_exits_instead_of_hanging()
    {
        using var runner = new ServerRunner(FakeServer.CreateServerFolder(), FakeServer.Executable);
        var lines = new ConcurrentQueue<LogLine>();
        runner.LineReceived += lines.Enqueue;

        runner.Start(["--fatal"]);
        await Until(() => runner.State == ServerState.Stopped);

        Assert.Contains(lines, l => l.Kind == LogKind.Fatal && l.Message.Contains("Address already in use"));
        // The stack trace line has no prefix, so it inherits Fatal from the line above.
        Assert.Contains(lines, l => l is { Kind: LogKind.Fatal, Time: null } && l.Message.Contains("SetupLidgrenServer"));
        Assert.Contains(lines, l => l.Message == "Server exited (code 1).");
    }

    [Fact]
    public void Refuses_to_start_without_the_executable()
    {
        using var runner = new ServerRunner(Path.GetTempPath(), "NoSuchServer.exe");
        Assert.Throws<FileNotFoundException>(() => runner.Start());
        Assert.Equal(ServerState.Stopped, runner.State);
    }

    [Fact]
    public void A_server_exe_windows_wont_start_leaves_the_runner_ready_to_try_again()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "badexe-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(folder, "Server.exe"), "this is not a program");
        using var runner = new ServerRunner(folder);
        Assert.Throws<System.ComponentModel.Win32Exception>(() => runner.Start());
        Assert.Equal(ServerState.Stopped, runner.State); // used to stay "Starting" forever
        Assert.Throws<System.ComponentModel.Win32Exception>(() => runner.Start()); // not "already running"
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition not met in time.");
            await Task.Delay(50);
        }
    }
}
