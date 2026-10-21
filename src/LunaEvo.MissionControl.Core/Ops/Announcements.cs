// LUNA EVO · LunaEvo.MissionControl.Core/Ops/Announcements.cs
// [Core] Scheduled chat announcements: "every N minutes, say this" while LUNA EVO runs the server.
// Used by: ServerPage (sends them), SettingsPage (editor)
// Guide:   Meat and Potatoes/13-Automation.md

using System.Text.RegularExpressions;

namespace LunaEvo.MissionControl.Core.Ops;

public sealed record Announcement(int EveryMinutes, string Text);

public static partial class Announcements
{
    public const int MinMinutes = 5;
    public const int MaxLength = 200;

    /// <summary>
    /// One announcement per line: minutes, then the text ("60 Join our Discord: ..."). A ":" or "|" after the number is
    /// fine. Blank lines and lines starting with # are ignored. Returns the announcements and a message per bad line.
    /// </summary>
    public static (IReadOnlyList<Announcement> Items, IReadOnlyList<string> Problems) Parse(string? text)
    {
        var items = new List<Announcement>();
        var problems = new List<string>();
        var lines = (text ?? "").Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var match = LineRegex().Match(line);
            if (!match.Success || !int.TryParse(match.Groups["minutes"].Value, out var minutes))
            {
                problems.Add($"Line {i + 1}: start with the minutes, e.g. \"60 Welcome!\".");
                continue;
            }
            if (minutes < MinMinutes)
            {
                problems.Add($"Line {i + 1}: at least every {MinMinutes} minutes, so chat isn't spammed.");
                continue;
            }
            var message = match.Groups["text"].Value.Trim();
            items.Add(new Announcement(minutes, message.Length > MaxLength ? message[..MaxLength] : message));
        }
        return (items, problems);
    }

    /// <summary>The announcements due now; <paramref name="lastSent"/> is updated for them. <paramref name="since"/> is when the server started.</summary>
    public static IReadOnlyList<Announcement> Due(IReadOnlyList<Announcement> items, Dictionary<int, DateTime> lastSent, DateTime since, DateTime now)
    {
        var due = new List<Announcement>();
        for (var i = 0; i < items.Count; i++)
        {
            var last = lastSent.TryGetValue(i, out var at) ? at : since;
            if (now - last >= TimeSpan.FromMinutes(items[i].EveryMinutes))
            {
                due.Add(items[i]);
                lastSent[i] = now;
            }
        }
        return due;
    }

    [GeneratedRegex(@"^(?<minutes>\d+)\s*[:|]?\s+(?<text>\S.*)$")]
    private static partial Regex LineRegex();
}
