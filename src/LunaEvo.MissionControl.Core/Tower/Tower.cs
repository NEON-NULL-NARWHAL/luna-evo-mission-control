// LUNA EVO · LunaEvo.MissionControl.Core/Tower/Tower.cs
// [Core] The traffic controller (the owner's idea): KSC tower talks with AI agents (KSP Wingman) on its own channel,
//        asks their intentions, clears them for published RNAV approaches with the whole route, holds the second
//        plane while the first lands, and tells a landed plane to vacate the runway. Pure: no network, no clock.
// Used by: TowerServer, TowerPage
// Guide:   Meat and Potatoes/28-Tower.md

using System.Globalization;

namespace LunaEvo.MissionControl.Core.Tower;

/// <summary>One point of a route, as the tower sends it: degrees, meters above sea level, meters a second.</summary>
public sealed record TowerFix(string Name, double Latitude, double Longitude, double Altitude, double Speed);

/// <summary>An approach the tower clears a plane for: its name, runway, and the fixes to fly before the final.</summary>
public sealed record TowerRoute(string Name, string Runway, IReadOnlyList<TowerFix> Fixes);

/// <summary>Where to hold: circle this point at this altitude until cleared.</summary>
public sealed record TowerHold(string Fix, double Latitude, double Longitude, double Altitude, double Radius, int Number);

/// <summary>One line on the tower's channel. Route or Hold come with clearances and holds.</summary>
public sealed record TowerMessage(int Id, DateTime Time, string From, string To, string Kind, string Text,
    TowerRoute? Route = null, TowerHold? Hold = null);

/// <summary>What an agent reports every few seconds.</summary>
public sealed record PlaneReport(string CallSign, double Latitude, double Longitude, double Altitude, double Heading,
    double Speed, string Situation, string Phase = "");

public enum PlaneState { New, Asked, Local, Approach, Holding, Landed, Gone }

/// <summary>A plane as the tower sees it.</summary>
public sealed class TowerPlane(string callSign)
{
    public string CallSign { get; } = callSign;
    public PlaneState State { get; internal set; } = PlaneState.New;
    public PlaneReport? Last { get; internal set; }
    public DateTime LastSeen { get; internal set; }
    public string? Approach { get; internal set; }
    public int Number { get; internal set; }
    public string Intent { get; internal set; } = "";
    internal int DeliveredId;
    internal bool ToldClearedToLand;
}

public sealed class Tower
{
    /// <summary>A plane that hasn't reported for this long is gone (and gives up its place).</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public const string Name = "KSC tower";

    /// <summary>Kept in memory (audit 13, T2: a local flood can't grow it without end); the log keeps everything.</summary>
    public const int MaxChannel = 5000;

    /// <summary>Planes on the channel at once (KSC's pattern takes a handful; anything more is a flood).</summary>
    public const int MaxPlanes = 50;

    private readonly List<TowerMessage> _channel = [];
    private readonly Dictionary<string, TowerPlane> _planes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<TowerMessage> Channel => _channel;
    public IReadOnlyCollection<TowerPlane> Planes => _planes.Values;

    /// <summary>Whether a call sign may talk: one already on the channel, or a new one while there's room.</summary>
    public bool Accepts(string callSign) =>
        _planes.ContainsKey(callSign) || _planes.Values.Count(p => p.State != PlaneState.Gone) < MaxPlanes;

    /// <summary>A report from a plane: the tower's new messages for it (none, usually).</summary>
    public IReadOnlyList<TowerMessage> Report(PlaneReport report, DateTime now)
    {
        var plane = Plane(report.CallSign, now);
        plane.Last = report;
        plane.LastSeen = now;
        if (plane.State == PlaneState.New)
        {
            plane.State = PlaneState.Asked;
            Send(now, plane.CallSign, "ask-intent", $"{plane.CallSign}, {Name}, say intentions.");
        }
        var flying = report.Situation.Equals("Flying", StringComparison.OrdinalIgnoreCase);
        var stopped = report.Situation.Equals("Landed", StringComparison.OrdinalIgnoreCase) && report.Speed < 2;
        if (plane.State == PlaneState.Approach)
        {
            var (along, cross) = Ksc.Frame(report.Latitude, report.Longitude);
            if (!plane.ToldClearedToLand && flying && along > -9500 && along < 0 && Math.Abs(cross) < 1500)
            {
                plane.ToldClearedToLand = true;
                Send(now, plane.CallSign, "cleared-to-land", $"{plane.CallSign}, runway 09, cleared to land.");
            }
            if (stopped)
            {
                plane.State = PlaneState.Landed;
                plane.Number = 0;
                Send(now, plane.CallSign, "vacate", $"{plane.CallSign}, welcome. Vacate the runway.");
            }
        }
        // Off the runway (recovered: back at the Space Center, not flying) or gone: the next one may come.
        if (plane.State == PlaneState.Landed && !report.Situation.Equals("Landed", StringComparison.OrdinalIgnoreCase))
            plane.State = PlaneState.Gone;
        Expire(now);
        ClearNext(now);
        return Undelivered(plane);
    }

    /// <summary>
    /// Something a plane says: "intent" (land, local, depart...) or "request-approach". The tower's answer comes back
    /// with the plane's undelivered messages.
    /// </summary>
    public IReadOnlyList<TowerMessage> Say(string callSign, string kind, string text, DateTime now)
    {
        var plane = Plane(callSign, now);
        plane.LastSeen = now;
        Log(now, callSign, Name, kind, text);
        var wantsToLand = kind == "request-approach" || (kind == "intent" && text.Contains("land", StringComparison.OrdinalIgnoreCase));
        if (wantsToLand && plane.State is not (PlaneState.Approach or PlaneState.Holding))
        {
            plane.Intent = "land";
            if (_planes.Values.Any(p => p.State == PlaneState.Approach || (p.State == PlaneState.Landed && p != plane)))
                Hold(plane, now);
            else
                Clear(plane, now);
        }
        else if (kind == "intent" && !wantsToLand)
        {
            plane.Intent = text;
            if (plane.State is PlaneState.New or PlaneState.Asked)
                plane.State = PlaneState.Local;
            Send(now, callSign, "info", $"{callSign}, {Name}, roger. Report when you want to land.");
        }
        return Undelivered(plane);
    }

    private void Clear(TowerPlane plane, DateTime now)
    {
        var r = plane.Last;
        var (along, cross) = r == null ? (0.0, 0.0) : Ksc.Frame(r.Latitude, r.Longitude);
        var route = Ksc.Pick(along, cross, r?.Heading ?? 90);
        plane.State = PlaneState.Approach;
        plane.Approach = route.Name;
        plane.Number = 1;
        plane.ToldClearedToLand = false;
        var first = route.Fixes[0];
        Send(now, plane.CallSign, "clearance", string.Create(CultureInfo.InvariantCulture,
            $"{plane.CallSign}, {Name}, cleared {route.Name}. Proceed to {first.Name} at {first.Altitude:0} m, {first.Speed:0} m/s, " +
            $"then {string.Join(", ", route.Fixes.Skip(1).Select(f => f.Name))}, runway 09."), Route: route);
    }

    private void Hold(TowerPlane plane, DateTime now)
    {
        var number = _planes.Values.Count(p => p.State is PlaneState.Approach or PlaneState.Holding or PlaneState.Landed && p != plane) + 1;
        var r = plane.Last;
        var (_, cross) = r == null ? (0.0, 1.0) : Ksc.Frame(r.Latitude, r.Longitude);
        // Its own side's downwind fix, stacked 300 m above anyone already holding, well clear of the final.
        var hold = Ksc.HoldFix(cross < 0 ? "N" : "S", 1200 + 300 * (number - 2), number);
        plane.State = PlaneState.Holding;
        plane.Number = number;
        var ahead = _planes.Values.FirstOrDefault(p => p.State is PlaneState.Approach or PlaneState.Landed && p != plane);
        Send(now, plane.CallSign, "hold", string.Create(CultureInfo.InvariantCulture,
            $"{plane.CallSign}, {Name}, number {number}. Hold at {hold.Fix}, {hold.Altitude:0} m, right turns. " +
            $"Expect the approach after {ahead?.CallSign ?? "the traffic"}."), Hold: hold);
    }

    /// <summary>When the runway is free (no one on approach or still on it), the next holding plane is cleared.</summary>
    private void ClearNext(DateTime now)
    {
        if (_planes.Values.Any(p => p.State is PlaneState.Approach or PlaneState.Landed))
            return;
        var next = _planes.Values.Where(p => p.State == PlaneState.Holding).OrderBy(p => p.Number).FirstOrDefault();
        if (next != null)
            Clear(next, now);
        foreach (var p in _planes.Values.Where(p => p.State == PlaneState.Holding))
            p.Number--;
    }

    private void Expire(DateTime now)
    {
        foreach (var p in _planes.Values.Where(p => p.State != PlaneState.Gone && now - p.LastSeen > Timeout).ToList())
        {
            p.State = PlaneState.Gone;
            Log(now, Name, "all", "info", $"{p.CallSign} lost from the channel.");
        }
    }

    private TowerPlane Plane(string callSign, DateTime now)
    {
        if (!_planes.TryGetValue(callSign, out var plane) || plane.State == PlaneState.Gone && now - plane.LastSeen > Timeout)
            _planes[callSign] = plane = new TowerPlane(callSign);
        return plane;
    }

    private void Send(DateTime now, string to, string kind, string text, TowerRoute? Route = null, TowerHold? Hold = null) =>
        Log(now, Name, to, kind, text, Route, Hold);

    private int _nextId = 1;

    private void Log(DateTime now, string from, string to, string kind, string text, TowerRoute? route = null, TowerHold? hold = null)
    {
        _channel.Add(new TowerMessage(_nextId++, now, from, to, kind, text, route, hold));
        if (_channel.Count > MaxChannel)
            _channel.RemoveRange(0, _channel.Count - MaxChannel);
        foreach (var gone in _planes.Where(p => p.Value.State == PlaneState.Gone && now - p.Value.LastSeen > Timeout * 10).Select(p => p.Key).ToList())
            _planes.Remove(gone);
    }

    private IReadOnlyList<TowerMessage> Undelivered(TowerPlane plane)
    {
        var mine = _channel.Where(m => m.Id > plane.DeliveredId && m.From == Name && m.To.Equals(plane.CallSign, StringComparison.OrdinalIgnoreCase)).ToList();
        plane.DeliveredId = _nextId - 1;
        return mine;
    }
}

/// <summary>
/// KSC's runway 09 and its published approaches (the same as KSP Wingman's, which flies them without a tower): the
/// runway frame is meters along the runway from the touchdown point and to the right of the centerline.
/// </summary>
public static class Ksc
{
    public const double ThresholdLatitude = -0.0486, ThresholdLongitude = -74.7244, Heading = 90, TouchdownAfterThreshold = 300;
    public const double KerbinRadius = 600_000;
    private const double MetersPerDegree = KerbinRadius * Math.PI / 180;

    public static (double Along, double Cross) Frame(double latitude, double longitude)
    {
        var north = (latitude - ThresholdLatitude) * MetersPerDegree;
        var east = (longitude - ThresholdLongitude) * MetersPerDegree * Math.Cos(latitude * Math.PI / 180);
        return (east - TouchdownAfterThreshold, -north); // landing east: along is east, right of the centerline is south
    }

    public static TowerFix At(string name, double along, double cross, double altitude, double speed)
    {
        var lat = ThresholdLatitude - cross / MetersPerDegree;
        var lon = ThresholdLongitude + (along + TouchdownAfterThreshold) / (MetersPerDegree * Math.Cos(lat * Math.PI / 180));
        return new TowerFix(name, Math.Round(lat, 6), Math.Round(lon, 6), altitude, speed);
    }

    private static TowerFix Final => At("F09", -8000, 0, 69 + 8000 * Math.Tan(4 * Math.PI / 180), 90);

    public static TowerRoute StraightIn => new("RNAV 09 STRAIGHT-IN", "KSC 09", [At("KSC-W12", -12_000, 0, 900, 120), Final]);
    public static TowerRoute North => new("RNAV 09 NORTH", "KSC 09",
        [At("KSC-N1", -1000, -4000, 900, 110), At("KSC-N2", -11_000, -4000, 800, 90), At("KSC-N3", -11_000, 0, 750, 90), Final]);
    public static TowerRoute South => new("RNAV 09 SOUTH", "KSC 09",
        [At("KSC-S1", -1000, 4000, 900, 110), At("KSC-S2", -11_000, 4000, 800, 90), At("KSC-S3", -11_000, 0, 750, 90), Final]);

    /// <summary>The same rule as Wingman's own: far out west and facing the runway, straight in; otherwise its side.</summary>
    public static TowerRoute Pick(double along, double cross, double heading)
    {
        var off = ((heading - Heading) % 360 + 540) % 360 - 180;
        if (along < -9000 && Math.Abs(cross) < 3000 && Math.Abs(off) < 60)
            return StraightIn;
        return cross < 0 ? North : South;
    }

    /// <summary>
    /// A holding point 4 km past the touchdown point and 7 km to the side: its 2 km circle stays 3.8 km from the downwind
    /// leg's first fix (and 300 m above the pattern), clear of the final.
    /// </summary>
    public static TowerHold HoldFix(string side, double altitude, int number)
    {
        var fix = At($"KSC-H{side}", 4000, side == "N" ? -7000 : 7000, altitude, 100);
        return new TowerHold(fix.Name, fix.Latitude, fix.Longitude, altitude, 2000, number);
    }
}
