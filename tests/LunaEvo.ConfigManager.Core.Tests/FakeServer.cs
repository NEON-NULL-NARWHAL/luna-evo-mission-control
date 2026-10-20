// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/FakeServer.cs
// [Test] Copies the FakeLmpServer build into a throwaway server folder.
// Used by: Server tests
// Guide:   Meat and Potatoes/05-Tests.md

namespace LunaEvo.ConfigManager.Core.Tests;

/// <summary>Copies the FakeLmpServer build into a throwaway "server folder".</summary>
internal static class FakeServer
{
    public const string Executable = "FakeLmpServer.exe";

    public static string CreateServerFolder()
    {
        // Test output is .../tests/<project>/bin/<Configuration>/net10.0/
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var build = Path.Combine(TestPaths.RepoRoot, "tests", "FakeLmpServer", "bin", configuration, "net10.0");
        if (!File.Exists(Path.Combine(build, Executable)))
            throw new FileNotFoundException($"Build tests/FakeLmpServer first ({build}).");

        // The test host can't be the Ctrl+C helper, so the fake server exe does that job too.
        Core.Server.ConsoleControl.HelperPath = Path.Combine(build, Executable);

        var dir = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "server-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var file in Directory.GetFiles(build))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
        return dir;
    }
}
