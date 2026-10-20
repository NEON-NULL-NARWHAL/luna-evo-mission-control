// LUNA EVO · LunaEvo.ConfigManager.Core/Ops/Restarts.cs
// [Core] When to restart the server by itself: after a crash (with a limit) and at a daily time.
// Used by: ServerPage
// Guide:   Meat and Potatoes/13-Automation.md

using System.Globalization;

namespace LunaEvo.ConfigManager.Core.Ops;

/// <summary>
/// Restarts after a crash, but gives up if it keeps crashing (a broken config would otherwise loop forever).
/// </summary>
public sealed class CrashRestartPolicy(int maxRestarts = 3, TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromMinutes(10);
    private readonly Queue<DateTime> _recent = new();

    /// <summary>How long to wait before restarting, so a crash loop doesn't spin.</summary>
    public static TimeSpan Delay { get; } = TimeSpan.FromSeconds(10);

    /// <summary>Records a crash at <paramref name="now"/>. True if we should restart, false if it crashed too often.</summary>
    public bool ShouldRestart(DateTime now)
    {
        while (_recent.Count > 0 && now - _recent.Peek() > _window)
            _recent.Dequeue();
        if (_recent.Count >= maxRestarts)
            return false;
        _recent.Enqueue(now);
        return true;
    }

    public void Reset() => _recent.Clear();
}

public static class DailyRestart
{
    /// <summary>Players get a chat warning this long before a scheduled restart.</summary>
    public static TimeSpan Warning { get; } = TimeSpan.FromMinutes(1);

    /// <summary>Parses "HH:mm" (24-hour). Null for empty or invalid, which means "off".</summary>
    public static TimeSpan? ParseTime(string? text) =>
        TimeSpan.TryParseExact(text?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time) ? time : null;

    /// <summary>The next time at or after <paramref name="now"/> that matches the daily time, or null if off.</summary>
    public static DateTime? Next(DateTime now, string? hhmm)
    {
        if (ParseTime(hhmm) is not { } time)
            return null;
        var today = now.Date + time;
        return today >= now ? today : today.AddDays(1);
    }
}
