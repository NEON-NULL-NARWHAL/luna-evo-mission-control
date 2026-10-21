// LUNA EVO · LunaEvo.MissionControl.Core/Server/LogLine.cs
// [Core] One parsed console/log line: time, kind (Error, Chat, Debug...) and message.
// Used by: ServerRunner, LogFiles, ServerPage
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Text.RegularExpressions;

namespace LunaEvo.MissionControl.Core.Server;

public enum LogKind
{
    /// <summary>LMP's plain "[LMP]" lines.</summary>
    Normal,
    Info,
    Debug,
    Warning,
    Error,
    Fatal,
    Chat,
    /// <summary>NetworkDebug and VerboseNetwork.</summary>
    Network,
    /// <summary>Messages from LUNA EVO itself ("Starting server...").</summary>
    Manager,
}

/// <summary>One line of server output. LMP writes "[HH:mm:ss][Type]: message" (LmpCommon.BaseLogger).</summary>
public sealed partial record LogLine(string? Time, LogKind Kind, string Message)
{
    /// <summary>The type as LMP wrote it ("LMP", "NetworkDebug", ...).</summary>
    public string Tag { get; init; } = Kind switch
    {
        LogKind.Normal => "LMP",
        LogKind.Manager => "LUNA EVO",
        _ => Kind.ToString(),
    };

    /// <summary>
    /// True for old lines replayed from a log file when LUNA EVO starts following a server. They're shown, but player
    /// history and alerts ignore them, so restarting the app doesn't count yesterday's joins again.
    /// </summary>
    public bool Replayed { get; init; }

    public override string ToString() => Time == null ? Message : $"[{Time}][{Tag}]: {Message}";

    /// <param name="previous">Kind of the line before, used for continuation lines such as stack traces.</param>
    public static LogLine Parse(string raw, LogKind previous = LogKind.Normal)
    {
        var match = LineRegex().Match(raw);
        if (!match.Success)
            return new LogLine(null, previous, raw);

        var kind = match.Groups["type"].Value switch
        {
            "LMP" => LogKind.Normal,
            "Info" => LogKind.Info,
            "Debug" => LogKind.Debug,
            "Warning" => LogKind.Warning,
            "Error" => LogKind.Error,
            "Fatal" => LogKind.Fatal,
            "Chat" => LogKind.Chat,
            "NetworkDebug" or "VerboseNetwork" => LogKind.Network,
            _ => LogKind.Normal,
        };
        return new LogLine(match.Groups["time"].Value, kind, match.Groups["message"].Value) { Tag = match.Groups["type"].Value };
    }

    /// <summary>Stamped in UTC, because LMP stamps its own lines in UTC; otherwise the console would read out of order.</summary>
    public static LogLine FromManager(string message) => new(DateTime.UtcNow.ToString("HH:mm:ss"), LogKind.Manager, message);

    [GeneratedRegex(@"^\[(?<time>\d{2}:\d{2}:\d{2})\]\[(?<type>[A-Za-z]+)\]: ?(?<message>.*)$")]
    private static partial Regex LineRegex();
}
