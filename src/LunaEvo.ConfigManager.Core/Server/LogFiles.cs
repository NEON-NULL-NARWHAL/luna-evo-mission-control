// LUNA EVO · LunaEvo.ConfigManager.Core/Server/LogFiles.cs
// [Core] Lists and reads LMP's logs/lmpserver_*.log files, and follows the newest one live (LogTail).
// Used by: ServerPage
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Text;

namespace LunaEvo.ConfigManager.Core.Server;

/// <summary>The lmpserver_*.log files LMP writes into its logs/ folder.</summary>
public static class LogFiles
{
    public const string Pattern = "lmpserver_*.log";

    /// <summary>The server's logs folder.</summary>
    public static string Folder(string serverDirectory) => Path.Combine(serverDirectory, "logs");

    /// <summary>Newest first.</summary>
    public static IReadOnlyList<FileInfo> List(string serverDirectory)
    {
        var folder = new DirectoryInfo(Folder(serverDirectory));
        return folder.Exists
            ? folder.GetFiles(Pattern).OrderByDescending(f => f.LastWriteTimeUtc).ToList()
            : [];
    }

    /// <summary>Parses a whole log file; lines without a [time][type] prefix inherit the kind above them.</summary>
    public static IReadOnlyList<LogLine> Read(string path)
    {
        var lines = new List<LogLine>();
        var kind = LogKind.Normal;
        foreach (var raw in ReadShared(path).Split('\n'))
        {
            var text = raw.TrimEnd('\r');
            if (text.Length == 0)
                continue;
            var line = LogLine.Parse(text, kind);
            kind = line.Kind;
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Reads a file the server still has open for writing.</summary>
    public static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// Follows the newest log file, for a server that was started outside LUNA EVO (so we can't read its console).
/// </summary>
public sealed class LogTail : IDisposable
{
    private readonly string _serverDirectory;
    private readonly int _initialLines;
    private readonly Timer _timer;
    private readonly Lock _gate = new();
    private string? _path;
    private long _position;
    private string _partial = "";
    private LogKind _lastKind = LogKind.Normal;

    /// <summary>Starts polling straight away. The first poll replays the last <paramref name="initialLines"/> lines.</summary>
    public LogTail(string serverDirectory, int initialLines = 300, TimeSpan? interval = null)
    {
        _serverDirectory = serverDirectory;
        _initialLines = initialLines;
        _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, interval ?? TimeSpan.FromSeconds(1));
    }

    /// <summary>Raised on a timer thread.</summary>
    public event Action<LogLine>? LineReceived;

    public string? CurrentFile => _path;

    /// <summary>Reads anything new. Runs on the timer; tests call it directly.</summary>
    public void Poll()
    {
        lock (_gate)
        {
            try
            {
                var newest = LogFiles.List(_serverDirectory).FirstOrDefault();
                if (newest == null)
                    return;

                if (newest.FullName != _path)
                {
                    // New file (server started, or LMP rotated the log at midnight): start near its end.
                    var first = _path == null;
                    _path = newest.FullName;
                    _partial = "";
                    var lines = LogFiles.Read(_path);
                    foreach (var line in first ? lines.TakeLast(_initialLines) : lines)
                        LineReceived?.Invoke(line);
                    _position = newest.Length;
                    return;
                }

                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < _position)
                    _position = 0;
                if (stream.Length == _position)
                    return;
                stream.Position = _position;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var text = _partial + reader.ReadToEnd();
                _position = stream.Length;

                var parts = text.Split('\n');
                _partial = parts[^1]; // incomplete last line, finished on the next poll
                foreach (var raw in parts[..^1])
                {
                    var trimmed = raw.TrimEnd('\r');
                    if (trimmed.Length == 0)
                        continue;
                    var line = LogLine.Parse(trimmed, _lastKind);
                    _lastKind = line.Kind;
                    LineReceived?.Invoke(line);
                }
            }
            catch (IOException)
            {
                // Rotating or locked for a moment: try again next tick.
            }
        }
    }

    public void Dispose() => _timer.Dispose();
}
