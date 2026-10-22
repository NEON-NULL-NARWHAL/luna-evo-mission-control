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
        // The flying vessel is the one the observer heard about most (debris and others send too).
        var target = all.GroupBy(r => r.Vessel).OrderByDescending(g => g.Count()).FirstOrDefault();
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
        var ok = report.Contains("sent every 150 ms") && report.Contains("network: 50 ms") && report.Contains(": 25 m behind on average")
                 && report.Contains("game was 0 ms from its stamp")
                 && report.Contains("where it was 0.25 s ago");
        Console.WriteLine(ok ? "SELFTEST OK" : "SELFTEST FAILED");
        return ok ? 0 : 1;
    }
}
