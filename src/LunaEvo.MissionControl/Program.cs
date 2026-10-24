// LUNA EVO · LunaEvo.MissionControl/Program.cs
// [UI] Entry point: runs the Ctrl+C helper if asked, parses --server/--config/--settings, starts Avalonia.
// Used by: Windows, when you launch the exe
// Guide:   Meat and Potatoes/04-App-UI.md

using Avalonia;
using Avalonia.Media;
using LunaEvo.MissionControl.Core.Server;

namespace LunaEvo.MissionControl;

internal static class Program
{
    public const string FontFamily = "avares://LunaEvo.MissionControl/Assets/Fonts#JetBrains Mono";

    public static StartupOptions Options { get; private set; } = new(null, null);

    [STAThread]
    public static int Main(string[] args)
    {
        // The app doubles as its own Ctrl+C helper for stopping the server (see ConsoleControl).
        if (ConsoleControl.RunHelper(args) is { } helperExit)
            return helperExit;

        Options = StartupOptions.Parse(args);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception x) Core.ErrorLog.Write("Unhandled", x); };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Core.ErrorLog.Write("Background task", e.Exception);
            e.SetObserved();
        };
        // A bug in a button handler is logged and the app carries on, instead of closing with your unsaved edits.
        // Only here (the real app): headless tests rely on such errors failing the test.
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Core.ErrorLog.Write("UI", e.Exception);
            e.Handled = true;
        };
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Avalonia setup. Also used by the Avalonia designer, so keep the name.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions { DefaultFamilyName = FontFamily })
            .LogToTrace();
}

/// <param name="ServerDirectory">Folder with Server.dll. Defaults to the app's own folder when it ships next to Server.exe.</param>
/// <param name="ConfigDirectory">Folder with the XML files. Defaults to ServerDirectory/Config.</param>
/// <param name="SettingsPath">Where LUNA EVO keeps its own settings. Defaults to %APPDATA%; tests use a temp file.</param>
/// <param name="TowerPort">--tower [port]: start the KSC tower at startup (for agents and rig tests).</param>
/// <param name="WingmanAgents">--wingman-agent CallSign:port[:model], any number: start those agents at startup (rig tests).</param>
internal sealed record StartupOptions(string? ServerDirectory, string? ConfigDirectory, string? SettingsPath = null, bool Minimized = false, int? TowerPort = null,
    IReadOnlyList<string>? WingmanAgentList = null)
{
    public IReadOnlyList<string> WingmanAgents => WingmanAgentList ?? [];

    /// <summary>Reads --server, --config, --settings, --tower and --wingman-agent; falls back to the exe's own folder when it sits next to Server.dll.</summary>
    public static StartupOptions Parse(string[] args)
    {
        string? server = null, config = null, settings = null;
        var agents = new List<string>();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--wingman-agent") agents.Add(args[++i]);
            else if (args[i] == "--server") server = Path.GetFullPath(args[++i]);
            else if (args[i] == "--config") config = Path.GetFullPath(args[++i]);
            else if (args[i] == "--settings") settings = Path.GetFullPath(args[++i]); // portable mode, e.g. on a USB stick
        }

        if (server == null && File.Exists(Path.Combine(AppContext.BaseDirectory, "Server.dll")))
            server = AppContext.BaseDirectory;

        // --tower, with or without a port after it.
        int? tower = null;
        var t = Array.IndexOf(args, "--tower");
        if (t >= 0)
            tower = t + 1 < args.Length && int.TryParse(args[t + 1], out var port) && port is > 0 and < 65536 ? port : Core.Tower.TowerServer.DefaultPort;

        // --minimized: started by Windows at login in home server mode; open hidden in the notification area.
        return new StartupOptions(server, config, settings, args.Contains(HomeServer.MinimizedFlag), tower, agents);
    }
}
