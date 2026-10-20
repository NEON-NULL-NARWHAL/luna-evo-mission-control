// LUNA EVO · LunaEvo.ConfigManager.Core/Server/ConsoleControl.cs
// [Core] Sends Ctrl+C to the server (LMP's only clean way to stop) via a tiny helper process.
// Used by: ServerRunner, ServerPage, Program.Main
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace LunaEvo.ConfigManager.Core.Server;

/// <summary>
/// Sends Ctrl+C to a console process. LMP has no "stop" command: on Windows it hooks Ctrl+C with
/// SetConsoleCtrlHandler and runs its clean Exit() (which makes a backup), so that's how we ask it to stop.
/// </summary>
/// <remarks>
/// Windows can only send Ctrl+C to a console you're attached to, and the signal hits every process on that
/// console, sender included. Doing that from the app itself proved fatal to it, so a short-lived helper process
/// (our own exe started with <see cref="HelperArgument"/>) attaches, sends, and exits. Same idea as CliWrap's signaler.
/// </remarks>
public static class ConsoleControl
{
    public const string HelperArgument = "--luna-evo-send-ctrl-c";
    private const uint CtrlCEvent = 0;

    /// <summary>Executable that understands <see cref="HelperArgument"/>. Defaults to the running app.</summary>
    public static string? HelperPath { get; set; } = Environment.ProcessPath;

    /// <summary>Call before starting a server so it doesn't inherit an "ignore Ctrl+C" flag from whoever started us.</summary>
    public static void AllowCtrlCInChildren()
    {
        if (OperatingSystem.IsWindows())
            SetConsoleCtrlHandler(IntPtr.Zero, false);
    }

    /// <returns>True if the signal was delivered.</returns>
    public static bool SendCtrlC(int processId)
    {
        if (!OperatingSystem.IsWindows())
            return Run(File.Exists("/bin/kill") ? "/bin/kill" : "kill", "-INT", processId.ToString(CultureInfo.InvariantCulture));
        return HelperPath != null && Run(HelperPath, HelperArgument, processId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The helper side: call first thing in Main. Returns the exit code when <paramref name="args"/> ask for the
    /// helper, otherwise null and the program carries on normally.
    /// </summary>
    public static int? RunHelper(string[] args)
    {
        if (args is not [HelperArgument, var pidText] || !uint.TryParse(pidText, out var processId))
            return null;

        FreeConsole();
        if (!AttachConsole(processId))
            return 2;
        SetConsoleCtrlHandler(IntPtr.Zero, true); // this process ignores it; it starts no children, so inheritance is moot
        return GenerateConsoleCtrlEvent(CtrlCEvent, 0) ? 0 : 3;
    }

    private static bool Run(string file, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process == null || !process.WaitForExit(10_000))
                return false;
            return process.ExitCode == 0;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);
}
