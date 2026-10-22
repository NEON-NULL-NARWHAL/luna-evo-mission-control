// LUNA EVO · LunaEvo.MissionControl.Core.Tests/TempCleanup.cs
// [Test] Keeps the tests' throwaway folders from piling up in the temp folder (they once filled a C: drive).
// Used by: both test projects (module start; the UI tests compile this file too), UiHarness (each test's own folders)
// Guide:   Meat and Potatoes/05-Tests.md

using System.Runtime.CompilerServices;

namespace LunaEvo.MissionControl.Core.Tests;

public static class TempCleanup
{
    /// <summary>
    /// Leftovers from earlier runs ("LunaEvoTests..." in the temp folder) older than this go when a test project starts.
    /// Not newer ones: the other test project may be running at the same moment.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(2);

    [ModuleInitializer]
    internal static void AtStart() => DeleteOld();

    public static void DeleteOld()
    {
        try
        {
            var temp = Path.GetTempPath();
            var roots = Directory.GetDirectories(temp, "LunaEvoTests*").ToList();
            var shared = Path.Combine(temp, "LunaEvoTests");
            var candidates = roots.Where(r => !string.Equals(r, shared, StringComparison.OrdinalIgnoreCase))
                .Concat(Directory.Exists(shared) ? Directory.GetFileSystemEntries(shared) : []);
            foreach (var entry in candidates)
                if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(entry) > MaxAge)
                    Delete(entry);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Cleaning up is never worth a failed run.
        }
    }

    /// <summary>Deletes a test's own folder (or file); never throws.</summary>
    public static void Delete(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            return; // only ever inside the temp folder
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Still in use (a fake server just stopped): the next run's DeleteOld gets it.
        }
    }
}
