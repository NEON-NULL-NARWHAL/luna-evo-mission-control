// LUNA EVO · LunaEvo.ConfigManager/HomeServer.cs
// [UI] Home server mode's Windows bits: start at login (minimised), and keep the PC awake while the server runs.
// Used by: MainWindow, SettingsPage
// Guide:   Meat and Potatoes/13-Automation.md

using System.Runtime.InteropServices;
using LunaEvo.ConfigManager.Core.Setup;

namespace LunaEvo.ConfigManager;

internal static class HomeServer
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "LUNA EVO Config Manager";
    public const string MinimizedFlag = "--minimized";

    /// <summary>Tests replace this so the registry is never touched: (on, command line).</summary>
    internal static Action<bool, string> ApplyStartWithWindows { get; set; } = (on, command) =>
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
        if (on)
            key.SetValue(RunValue, command);
        else if (key.GetValue(RunValue) != null)
            key.DeleteValue(RunValue);
    };

    /// <summary>
    /// Adds or removes LUNA EVO from Windows' "run at login" list (current user only, no admin). Only the published
    /// exe is registered; a development build returns false.
    /// </summary>
    public static bool SetStartWithWindows(bool on, string? exe, string settingsPath, bool customSettings)
    {
        if (!AppUpdater.CanUpdateItself(exe))
            return false;
        var command = $"\"{exe}\" {MinimizedFlag}" + (customSettings ? $" --settings \"{settingsPath}\"" : "");
        try
        {
            ApplyStartWithWindows(on, command);
            return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    /// <summary>Keeps Windows from sleeping while the server runs (the display may still turn off). Call from the UI thread.</summary>
    public static void KeepAwake(bool on)
    {
        if (!OperatingSystem.IsWindows())
            return;
        SetThreadExecutionState(on ? EsContinuous | EsSystemRequired : EsContinuous);
    }

    private const uint EsContinuous = 0x80000000, EsSystemRequired = 0x00000001;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
