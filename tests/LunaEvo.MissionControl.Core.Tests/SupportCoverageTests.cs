// LUNA EVO · LunaEvo.MissionControl.Core.Tests/SupportCoverageTests.cs
// [Test] The error log (written exactly when things go wrong, so it must never fail) and finding a running Server.exe.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.MissionControl.Core.Server;

namespace LunaEvo.MissionControl.Core.Tests;

[Collection("ErrorLog")] // ErrorLog.Path is static
public class SupportCoverageTests
{
    [Fact]
    public void The_error_log_appends_caps_its_size_and_never_throws()
    {
        var before = ErrorLog.Path;
        var folder = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "log-" + Guid.NewGuid().ToString("N"));
        try
        {
            ErrorLog.Path = Path.Combine(folder, "logs", "errors.log");
            ErrorLog.Write("first", new InvalidOperationException("boom"));
            ErrorLog.Write("second", new IOException("disk"));
            var text = File.ReadAllText(ErrorLog.Path);
            Assert.Contains("first", text);
            Assert.Contains("System.IO.IOException: disk", text);

            File.WriteAllText(ErrorLog.Path, new string('x', 1024 * 1024 + 10)); // over the cap
            ErrorLog.Write("third", new Exception("after the cap"));
            Assert.True(File.Exists(ErrorLog.Path + ".old"));
            Assert.DoesNotContain("xxxx", File.ReadAllText(ErrorLog.Path));

            ErrorLog.Path = Path.Combine(folder, "logs", "errors.log", "not-a-folder", "x.log"); // can't be written
            ErrorLog.Write("unwritable", new Exception("must not throw"));
        }
        finally
        {
            ErrorLog.Path = before;
        }
    }

    [Fact]
    public async Task Finds_the_running_server_by_its_folder_and_not_another()
    {
        var server = FakeServer.CreateServerFolder();
        using var runner = new ServerRunner(server, FakeServer.Executable);
        runner.Start();
        try
        {
            Assert.True(ServerProcess.IsRunning(server, FakeServer.Executable));
            using (var found = ServerProcess.Find(server, FakeServer.Executable))
                Assert.Equal(runner.ProcessId, found?.Id);
            Assert.False(ServerProcess.IsRunning(FakeServer.CreateServerFolder(), FakeServer.Executable)); // same exe name, other folder
        }
        finally
        {
            await runner.StopAsync(TimeSpan.FromSeconds(20));
        }
        Assert.False(ServerProcess.IsRunning(server, FakeServer.Executable));
    }
}
