// LUNA EVO · LunaEvo.MissionControl.Core/ErrorLog.cs
// [Core] Unexpected errors go to %APPDATA%\LunaEvo\MissionControl\logs\errors.log, so a bug report can say what happened.
// Used by: Program, App, CkanPage
// Guide:   Meat and Potatoes/04-App-UI.md

namespace LunaEvo.MissionControl.Core;

public static class ErrorLog
{
    /// <summary>Where errors are written. Tests point it at a temp file.</summary>
    public static string Path { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LunaEvo", "MissionControl", "logs", "errors.log");

    private static readonly Lock Gate = new();

    /// <summary>Appends one entry (UTC time, where, the exception). Never throws; the log is capped at 1 MB.</summary>
    public static void Write(string where, Exception error)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                if (File.Exists(Path) && new FileInfo(Path).Length > 1024 * 1024)
                    File.Move(Path, Path + ".old", overwrite: true);
                File.AppendAllText(Path, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] {where}{Environment.NewLine}{error}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Logging must never be the thing that fails.
        }
    }
}
