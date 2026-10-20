// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/TestPaths.cs
// [Test] Finds tests/Fixtures and makes throwaway copies of the configs.
// Used by: All tests
// Guide:   Meat and Potatoes/05-Tests.md

namespace LunaEvo.ConfigManager.Core.Tests;

/// <summary>
/// Locates the test fixtures inside the repo (tests/Fixtures), so the tests run on any clone:
/// <list type="bullet">
/// <item><c>Fixtures/Config</c>: our real server's 13 XML files, with passwords blanked.</item>
/// <item><c>Fixtures/LMPServer</c>: LMP 0.29.2's Server.dll and dependencies (MIT, see LMP-LICENSE.txt) plus sample logs.</item>
/// </list>
/// </summary>
internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();
    public static string Fixtures => Path.Combine(RepoRoot, "tests", "Fixtures");
    public static string ServerConfigs => Path.Combine(Fixtures, "Config");
    public static string LmpServer => Path.Combine(Fixtures, "LMPServer");

    /// <summary>A throwaway copy of the fixture configs, so tests never modify the originals.</summary>
    public static string CopyConfigs()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var file in Directory.GetFiles(ServerConfigs, "*.xml"))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
        return dir;
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "LunaEvo.ConfigManager.slnx")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Couldn't find LunaEvo.ConfigManager.slnx above the test output folder.");
    }
}
