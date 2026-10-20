// LUNA EVO · LunaEvo.ConfigManager/Program.cs
// [UI] Entry point: runs the Ctrl+C helper if asked, parses --server/--config/--settings, starts Avalonia.
// Used by: Windows, when you launch the exe
// Guide:   Meat and Potatoes/04-App-UI.md

using Avalonia;
using Avalonia.Media;
using LunaEvo.ConfigManager.Core.Server;

namespace LunaEvo.ConfigManager;

internal static class Program
{
    public const string FontFamily = "avares://LunaEvo.ConfigManager/Assets/Fonts#JetBrains Mono";

    public static StartupOptions Options { get; private set; } = new(null, null);

    [STAThread]
    public static int Main(string[] args)
    {
        // The app doubles as its own Ctrl+C helper for stopping the server (see ConsoleControl).
        if (ConsoleControl.RunHelper(args) is { } helperExit)
            return helperExit;

        Options = StartupOptions.Parse(args);
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
internal sealed record StartupOptions(string? ServerDirectory, string? ConfigDirectory, string? SettingsPath = null)
{
    /// <summary>Reads --server, --config and --settings; falls back to the exe's own folder when it sits next to Server.dll.</summary>
    public static StartupOptions Parse(string[] args)
    {
        string? server = null, config = null, settings = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--server") server = Path.GetFullPath(args[++i]);
            else if (args[i] == "--config") config = Path.GetFullPath(args[++i]);
            else if (args[i] == "--settings") settings = Path.GetFullPath(args[++i]); // portable mode, e.g. on a USB stick
        }

        if (server == null && File.Exists(Path.Combine(AppContext.BaseDirectory, "Server.dll")))
            server = AppContext.BaseDirectory;

        return new StartupOptions(server, config, settings);
    }
}
