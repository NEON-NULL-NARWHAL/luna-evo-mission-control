// LUNA EVO · LunaEvo.ConfigManager.Core/Setup/RuntimeInstaller.cs
// [Core] Installs the .NET runtime an LMP server needs, using Windows' own package manager (winget).
// Used by: SetupWizard, ServerPage, SettingsPage
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

using System.Diagnostics;

namespace LunaEvo.ConfigManager.Core.Setup;

public static class RuntimeInstaller
{
    /// <summary>winget's package id for the plain .NET runtime of a major version, e.g. "Microsoft.DotNet.Runtime.6".</summary>
    public static string WingetId(int major) => $"Microsoft.DotNet.Runtime.{major}";

    /// <summary>The winget arguments we run. Public so tests (and curious readers) can see exactly what happens.</summary>
    public static IReadOnlyList<string> WingetArguments(int major) =>
        ["install", "--id", WingetId(major), "--exact", "--accept-source-agreements", "--accept-package-agreements"];

    /// <summary>
    /// winget's real location (it's an App Installer alias in the user's WindowsApps folder), so we don't pick up
    /// some other "winget.exe" from the PATH. Falls back to the bare name if Windows keeps it elsewhere.
    /// </summary>
    public static string WingetPath
    {
        get
        {
            var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
            return File.Exists(alias) ? alias : "winget";
        }
    }

    /// <summary>
    /// Runs winget in a visible window (it shows progress and asks Windows for permission itself) and waits.
    /// Returns false if winget isn't available or failed; the caller then offers the download page instead.
    /// </summary>
    public static async Task<bool> InstallAsync(int major, CancellationToken cancel = default)
    {
        try
        {
            var info = new ProcessStartInfo(WingetPath) { UseShellExecute = true };
            foreach (var argument in WingetArguments(major))
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process == null)
                return false;
            await process.WaitForExitAsync(cancel);
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // no winget on this PC
        }
    }
}
