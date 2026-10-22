// LUNA EVO · tests/RigRunner/SyncAnalysis.cs
// [Test] Works out how late position messages reach a second player, and what LMP would show them, from the rig's recordings.
// Used by: RigRunner (and its --selftest)
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Globalization;

namespace LunaEvo.RigRunner;

internal static class SyncAnalysis
{
    /// <summary>
    /// The flying player's own game: PC clock (UTC ticks), game clock (UT, s), altitude of the centre of mass and of the
    /// root part (m), speed (m/s). LMP sends the vessel's altitude, which is the root part's, so that's what's compared.
    /// </summary>
    public sealed record Sample(long WallTicks, double Ut, double Alt, double Speed, double RootAlt);

    /// <summary>Where a second KSP showed the flying vessel's root part, and when (PC clock, UTC ticks).</summary>
    public sealed record Viewed(long WallTicks, double RootAlt);

    /// <summary>A root part's position in the body's frame (m), when (PC clock, UTC ticks), and its speed (m/s; 0 if unknown).</summary>
    public sealed record Point(long WallTicks, double X, double Y, double Z, double Speed);

    /// <summary>
    /// For a vessel moving along the ground: turns positions into distance along its real track, so
    /// <see cref="AnalyzeView"/> can treat it like a climbing rocket's altitude. Each shown position counts as the nearest
    /// point of the real track (the real one sampled at most every 50 ms).
    /// </summary>
    public static (List<Sample> Track, List<Viewed> Along) AlongTrack(IReadOnlyList<Point> truth, IReadOnlyList<Point> shown)
    {
        var real = new List<Point>();
        foreach (var p in truth)
            if (real.Count == 0 || p.WallTicks - real[^1].WallTicks >= 500_000)
                real.Add(p);
        var track = new List<Sample>();
        var s = 0.0;
        for (var i = 0; i < real.Count; i++)
        {
            if (i > 0)
                s += Distance(real[i - 1], real[i]);
            track.Add(new Sample(real[i].WallTicks, 0, s, real[i].Speed, s));
        }
        var along = new List<Viewed>();
        foreach (var q in shown)
        {
            var best = (Gap: double.MaxValue, S: 0.0);
            for (var i = 0; i + 1 < real.Count; i++)
            {
                var (a, b) = (real[i], real[i + 1]);
                var (dx, dy, dz) = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
                var length2 = dx * dx + dy * dy + dz * dz;
                var f = length2 > 0 ? Math.Clamp(((q.X - a.X) * dx + (q.Y - a.Y) * dy + (q.Z - a.Z) * dz) / length2, 0, 1) : 0;
                var gap = Math.Pow(a.X + f * dx - q.X, 2) + Math.Pow(a.Y + f * dy - q.Y, 2) + Math.Pow(a.Z + f * dz - q.Z, 2);
                if (gap < best.Gap)
                    best = (gap, track[i].RootAlt + f * (track[i + 1].RootAlt - track[i].RootAlt));
            }
            if (best.Gap < 25 * 25) // further than 25 m off the real track isn't the same moment (or not the buggy)
                along.Add(new Viewed(q.WallTicks, best.S));
        }
        return (track, along);
    }

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2));

    /// <summary>A position message as the second player received it.</summary>
    public sealed record Received(long ArrivalTicks, string Vessel, double GameTime, double Alt);

    /// <summary>
    /// LMP replays other players' vessels this far behind their own clock: ping + their ping, clamped to 0.25-2.5 s
    /// (LmpClient/VesselUtilities/VesselCommon.cs, PositionAndFlightStateMessageOffsetSec). On one PC the pings are ~0.
    /// </summary>
    public const double MinReplayDelay = 0.25;

    public static List<Received> ReadObserved(string csv) =>
        File.ReadLines(csv).Skip(1).Select(l => l.Split(','))
            .Where(p => p.Length >= 6)
            .Select(p => new Received(long.Parse(p[0], CultureInfo.InvariantCulture), p[1],
                double.Parse(p[2], CultureInfo.InvariantCulture), double.Parse(p[5], CultureInfo.InvariantCulture)))
            .ToList();

    public static string Analyze(IReadOnlyList<Sample> truth, IReadOnlyList<Received> all, double replayDelay = MinReplayDelay)
    {
        if (truth.Count < 10)
            return "FAILED: too few samples from the game";
        // The flying vessel is the one that climbed most (debris and other players' vessels send too).
        var target = all.GroupBy(r => r.Vessel).OrderByDescending(g => g.Max(r => r.Alt) - g.Min(r => r.Alt)).FirstOrDefault();
        if (target == null)
            return "FAILED: the second player received no positions at all";
        var received = target.OrderBy(r => r.ArrivalTicks).ToList();
        var first = truth[0].Ut;
        var last = truth[^1].Ut;
        var inFlight = received.Where(r => r.GameTime >= first && r.GameTime <= last).ToList();
        if (inFlight.Count < 5)
            return $"FAILED: only {inFlight.Count} positions arrived during the flight";

        // How often the flying game sent, by its own clock.
        var gaps = inFlight.Zip(inFlight.Skip(1), (a, b) => b.GameTime - a.GameTime).Where(g => g > 0).ToList();
        // How long each message took: arrival on the PC clock minus when the flying game was at that moment.
        var latencies = inFlight.Select(r => (r.ArrivalTicks - WallAt(truth, r.GameTime)) / 1e7).Where(double.IsFinite).ToList();

        // What the second player's game shows at each sample: LMP plays back at (its clock - delay), blending between
        // the two messages around that time, and holds the last one when the next hasn't arrived.
        var errors = new List<(double Metres, double Speed)>();
        var behind = new List<double>(); // how long ago the vessel really was where the second player sees it
        foreach (var s in truth)
        {
            var arrived = received.Where(r => r.ArrivalTicks <= s.WallTicks).ToList();
            var shown = Shown(arrived, s.Ut - replayDelay);
            if (shown is not { } alt)
                continue;
            errors.Add((s.RootAlt - alt, s.Speed));
            if (s.Speed > 50 && UtAtAltitude(truth, alt) is { } then)
                behind.Add(s.Ut - then);
        }

        // Is each message's position where the game was at the message's own time stamp? Fit a time shift.
        var (shift, residual) = StampCheck(truth, inFlight);
        if (errors.Count == 0)
            return "FAILED: nothing to compare";
        return string.Join('\n',
            $"RESULT vessel {target.Key}: {inFlight.Count} positions received during {last - first:0} s of flight",
            $"  sent every {Median(gaps) * 1000:0} ms (median), longest gap {gaps.DefaultIfEmpty(0).Max() * 1000:0} ms",
            $"  network: {Median(latencies) * 1000:0} ms median from the flying game to the second player, {latencies.Max() * 1000:0} ms at most",
            $"  time stamps: a message's position is where the game was {shift * 1000:+0;-0;0} ms from its stamp (best fit, {residual:0.00} m rms left over)",
            $"  second player's view (LMP's replay, {replayDelay:0.00} s behind): {errors.Average(e => e.Metres):0} m behind on average, " +
            $"{errors.Max(e => Math.Abs(e.Metres)):0} m at most" +
            (behind.Count > 0 ? $"; in time, the second player sees the vessel where it was {Median(behind):0.00} s ago (median), {behind.Max():0.00} s at most" : ""));
    }

    /// <summary>
    /// How a real second KSP showed the vessel: how long ago the vessel really was where it's shown ("behind"), how
    /// much that wobbles (smooth playback keeps it constant), how often the shown vessel stood still while the real one
    /// climbed, and how often it went backwards (rubber-banding).
    /// </summary>
    public static string AnalyzeView(IReadOnlyList<Sample> truth, IReadOnlyList<Viewed> viewed, string who = "viewer sees the rocket",
        double minSpeed = 50, double minStep = 1)
    {
        var behind = new List<double>();
        var metres = new List<double>();
        int still = 0, back = 0, moving = 0;
        for (var i = 0; i < viewed.Count; i++)
        {
            var v = viewed[i];
            if (AltAtWall(truth, v.WallTicks) is not { } now || SpeedAtWall(truth, v.WallTicks) < minSpeed)
                continue;
            metres.Add(now - v.RootAlt);
            if (WallAtRootAlt(truth, v.RootAlt) is { } then)
                behind.Add((v.WallTicks - then) / 1e7);
            if (i == 0 || AltAtWall(truth, viewed[i - 1].WallTicks) is not { } before || now - before < minStep)
                continue;
            moving++;
            var shownStep = v.RootAlt - viewed[i - 1].RootAlt;
            if (shownStep < -minStep / 2)
                back++;
            else if (shownStep < 0.1 * (now - before))
                still++;
        }
        if (behind.Count < 20)
            return $"FAILED ({who}): the vessel was shown only {behind.Count} times while it was moving";
        var median = Median(behind);
        var wobble = behind.Select(b => Math.Abs(b - median)).ToList();
        return string.Join('\n',
            $"RESULT {who}: {behind.Count} looks at the other player's vessel",
            $"  shown where it was {median:0.000} s ago (median); wobble around that: {Median(wobble) * 1000:0} ms median, " +
            $"{Percentile(wobble, 0.95) * 1000:0} ms 95%, {wobble.Max() * 1000:0} ms worst",
            $"  {metres.Average():0} m behind on average, {metres.Max():0} m at most",
            $"  between looks: stood still {100.0 * still / Math.Max(moving, 1):0.0}%, went backwards {100.0 * back / Math.Max(moving, 1):0.0}% (of {moving})");
    }

    private static double? AltAtWall(IReadOnlyList<Sample> truth, long wall)
    {
        for (var i = 0; i + 1 < truth.Count; i++)
            if (truth[i].WallTicks <= wall && wall <= truth[i + 1].WallTicks && truth[i + 1].WallTicks > truth[i].WallTicks)
                return truth[i].RootAlt + (double)(wall - truth[i].WallTicks) / (truth[i + 1].WallTicks - truth[i].WallTicks) * (truth[i + 1].RootAlt - truth[i].RootAlt);
        return null;
    }

    private static double SpeedAtWall(IReadOnlyList<Sample> truth, long wall) =>
        truth.LastOrDefault(s => s.WallTicks <= wall)?.Speed ?? 0;

    /// <summary>The PC-clock time (ticks) when the climbing vessel's root part was at this altitude; null if never.</summary>
    private static double? WallAtRootAlt(IReadOnlyList<Sample> truth, double alt)
    {
        for (var i = 0; i + 1 < truth.Count; i++)
            if (truth[i].RootAlt <= alt && alt <= truth[i + 1].RootAlt && truth[i + 1].RootAlt > truth[i].RootAlt)
                return truth[i].WallTicks + (alt - truth[i].RootAlt) / (truth[i + 1].RootAlt - truth[i].RootAlt) * (truth[i + 1].WallTicks - truth[i].WallTicks);
        return null;
    }

    private static double Percentile(List<double> values, double p)
    {
        var sorted = values.Order().ToList();
        return sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
    }

    /// <summary>The PC-clock time (ticks) when the flying game's clock read <paramref name="ut"/>; NaN outside the samples.</summary>
    private static double WallAt(IReadOnlyList<Sample> truth, double ut)
    {
        for (var i = 0; i + 1 < truth.Count; i++)
            if (truth[i].Ut <= ut && ut <= truth[i + 1].Ut && truth[i + 1].Ut > truth[i].Ut)
            {
                var f = (ut - truth[i].Ut) / (truth[i + 1].Ut - truth[i].Ut);
                return truth[i].WallTicks + f * (truth[i + 1].WallTicks - truth[i].WallTicks);
            }
        return double.NaN;
    }

    /// <summary>When the (climbing) vessel's root part was at this altitude, by its own clock; null if never in the samples.</summary>
    private static double? UtAtAltitude(IReadOnlyList<Sample> truth, double alt)
    {
        for (var i = 0; i + 1 < truth.Count; i++)
            if (truth[i].RootAlt <= alt && alt <= truth[i + 1].RootAlt && truth[i + 1].RootAlt > truth[i].RootAlt)
                return truth[i].Ut + (alt - truth[i].RootAlt) / (truth[i + 1].RootAlt - truth[i].RootAlt) * (truth[i + 1].Ut - truth[i].Ut);
        return null;
    }

    private static double? RootAltAt(IReadOnlyList<Sample> truth, double ut)
    {
        for (var i = 0; i + 1 < truth.Count; i++)
            if (truth[i].Ut <= ut && ut <= truth[i + 1].Ut && truth[i + 1].Ut > truth[i].Ut)
                return truth[i].RootAlt + (ut - truth[i].Ut) / (truth[i + 1].Ut - truth[i].Ut) * (truth[i + 1].RootAlt - truth[i].RootAlt);
        return null;
    }

    /// <summary>The time shift (s, -0.2 to +0.2) that best lines messages up with the game's root part, and the rms left.</summary>
    private static (double Shift, double Rms) StampCheck(IReadOnlyList<Sample> truth, List<Received> messages)
    {
        var best = (Shift: 0.0, Rms: double.MaxValue);
        for (var ms = -200; ms <= 200; ms += 5)
        {
            var d = messages.Select(m => RootAltAt(truth, m.GameTime + ms / 1000.0) is { } a ? m.Alt - a : (double?)null)
                .Where(x => x.HasValue).Select(x => x!.Value).ToList();
            if (d.Count < 20)
                continue;
            var rms = Math.Sqrt(d.Average(x => x * x));
            if (rms < best.Rms)
                best = (ms / 1000.0, rms);
        }
        return best.Rms == double.MaxValue ? (0, double.NaN) : best;
    }

    private static double? Shown(List<Received> arrived, double playback)
    {
        var byTime = arrived.OrderBy(r => r.GameTime).ToList();
        if (byTime.Count == 0 || playback < byTime[0].GameTime)
            return null;
        for (var i = 0; i + 1 < byTime.Count; i++)
            if (byTime[i].GameTime <= playback && playback <= byTime[i + 1].GameTime)
            {
                var span = byTime[i + 1].GameTime - byTime[i].GameTime;
                var f = span > 0 ? (playback - byTime[i].GameTime) / span : 1;
                return byTime[i].Alt + f * (byTime[i + 1].Alt - byTime[i].Alt);
            }
        return byTime[^1].Alt; // the next message is late: LMP holds the vessel where the last one put it
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
            return 0;
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }

    /// <summary>
    /// Known answers: a vessel climbing at 100 m/s, a message every 0.15 s that takes 0.05 s to arrive, and a replay
    /// 0.25 s behind. The second player should see it 0.25 s (25 m) behind, and the network 50 ms.
    /// </summary>
    public static int SelfTest()
    {
        const long Tick = 10_000_000;
        var start = DateTime.UtcNow.Ticks;
        var truth = Enumerable.Range(0, 200).Select(i => new Sample(start + i * Tick / 10, 1000 + i * 0.1, 100 + i * 0.1 * 100, 100, 100 + i * 0.1 * 100)).ToList();
        var received = Enumerable.Range(0, 130).Select(i => 1000 + i * 0.15)
            .Select(ut => new Received(start + (long)((ut - 1000 + 0.05) * Tick), "v", ut, 100 + (ut - 1000) * 100)).ToList();
        var report = Analyze(truth, received);
        Console.WriteLine(report);
        // A viewer exactly 0.25 s behind, looked at every 30 ms: no wobble, never still or backwards.
        var view = Enumerable.Range(10, 500).Select(i => new Viewed(start + i * Tick * 3 / 100, 100 + (i * 0.03 - 0.25) * 100)).ToList();
        var viewReport = AnalyzeView(truth, view);
        Console.WriteLine(viewReport);
        // A buggy driving a curve at 10 m/s, sampled every 10 ms, shown 0.3 s behind every 50 ms.
        Point At(long ticks)
        {
            var t = (ticks - start) / 1e7;
            return new Point(ticks, 600_000 + 300 * Math.Sin(t / 30), 300 * (1 - Math.Cos(t / 30)), 0, 10);
        }
        var buggy = Enumerable.Range(0, 3000).Select(i => At(start + i * Tick / 100)).ToList();
        var shown = Enumerable.Range(20, 500).Select(i => At(start + i * Tick / 20 - 3 * Tick / 10) with { WallTicks = start + i * Tick / 20 }).ToList();
        var (track, along) = AlongTrack(buggy, shown);
        var buggyReport = AnalyzeView(track, along, "buggy", minSpeed: 2, minStep: 0.2);
        Console.WriteLine(buggyReport);
        var ok = buggyReport.Contains("0.300 s ago") && buggyReport.Contains("went backwards 0.0%") &&
viewReport.Contains("0.250 s ago") && viewReport.Contains("wobble around that: 0 ms median") && viewReport.Contains("still 0.0%, went backwards 0.0%")
                 && report.Contains("sent every 150 ms") && report.Contains("network: 50 ms") && report.Contains(": 25 m behind on average")
                 && report.Contains("game was 0 ms from its stamp")
                 && report.Contains("where it was 0.25 s ago");
        Console.WriteLine(ok ? "SELFTEST OK" : "SELFTEST FAILED");
        return ok ? 0 : 1;
    }
}
