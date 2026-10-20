// LUNA EVO · LunaEvo.ConfigManager.Core/Server/DotNetRuntime.cs
// [Core] Checks the server's required .NET runtime (LMP 0.29 needs .NET 6) is installed before starting it.
// Used by: ServerPage, SettingsPage
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Text.Json;

namespace LunaEvo.ConfigManager.Core.Server;

/// <summary>
/// LMP 0.29.x is framework-dependent on .NET 6 (see Server.runtimeconfig.json). Without that runtime Server.exe
/// flashes an error and closes, so we check before starting it, like StartLunaServer.bat does.
/// </summary>
public static class DotNetRuntime
{
    public sealed record Requirement(string Framework, int Major, string Version, bool Installed);

    /// <summary>Null when the server doesn't need a shared runtime (self-contained) or has no runtimeconfig.</summary>
    public static Requirement? Check(string serverDirectory)
    {
        var config = Path.Combine(serverDirectory, "Server.runtimeconfig.json");
        if (!File.Exists(config))
            return null;

        using var json = JsonDocument.Parse(File.ReadAllText(config));
        if (!json.RootElement.TryGetProperty("runtimeOptions", out var options) ||
            !options.TryGetProperty("framework", out var framework))
            return null;

        var name = framework.GetProperty("name").GetString() ?? "Microsoft.NETCore.App";
        var version = framework.GetProperty("version").GetString() ?? "0.0.0";
        var major = int.Parse(version.Split('.')[0]);
        return new Requirement(name, major, version, InstalledVersions(name).Any(v => v.Major == major));
    }

    /// <summary>Versions of a shared .NET framework found in the usual install folders.</summary>
    public static IEnumerable<Version> InstalledVersions(string framework = "Microsoft.NETCore.App")
    {
        foreach (var root in DotNetRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var folder = Path.Combine(root, "shared", framework);
            if (!Directory.Exists(folder))
                continue;
            foreach (var dir in Directory.GetDirectories(folder))
            {
                var name = Path.GetFileName(dir).Split('-')[0]; // "6.0.36" or "10.0.0-rc.1"
                if (Version.TryParse(name, out var version))
                    yield return version;
            }
        }
    }

    private static IEnumerable<string> DotNetRoots()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root)
            yield return root;
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet");
        }
        else
        {
            yield return "/usr/share/dotnet";
            yield return "/usr/lib/dotnet";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet");
        }
    }
}
