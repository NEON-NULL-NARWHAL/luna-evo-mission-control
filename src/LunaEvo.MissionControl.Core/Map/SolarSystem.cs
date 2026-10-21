// LUNA EVO · LunaEvo.MissionControl.Core/Map/SolarSystem.cs
// [Core] The solar system map's model: Keplerian orbits, the stock Kerbol system, planet packs (Kopernicus configs in
//        GameData, e.g. Outer Planets Mod), and the universe time of an LMP server.
// Used by: MapPage
// Guide:   Meat and Potatoes/24-Solar-System-Map.md

using System.Globalization;
using LunaEvo.MissionControl.Core.Mods;

namespace LunaEvo.MissionControl.Core.Map;

/// <summary>A point in a body's frame, in metres: X/Y is the plane seen from above (the map), Z is out of it.</summary>
public readonly record struct Point3(double X, double Y, double Z)
{
    public static Point3 operator +(Point3 a, Point3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
}

/// <summary>
/// A Keplerian orbit, as KSP stores it (a vessel's ORBIT node, a body's Kopernicus Orbit node): semi-major axis (m,
/// negative for an escape), eccentricity, inclination, longitude of the ascending node and argument of periapsis
/// (degrees), the mean anomaly (radians) at the epoch (seconds of universe time), around a body with gravity μ (m³/s²).
/// </summary>
public sealed record Orbit(double Sma, double Ecc, double Inc, double Lan, double ArgPe, double MeanAnomalyAtEpoch, double Epoch)
{
    public bool IsOpen => Ecc >= 1;

    /// <summary>Where it is at <paramref name="time"/>, in the parent body's frame.</summary>
    public Point3 PositionAt(double time, double mu)
    {
        var a = Math.Abs(Sma);
        if (a < 1 || mu <= 0)
            return default;
        var n = Math.Sqrt(mu / (a * a * a)); // mean motion
        var meanAnomaly = MeanAnomalyAtEpoch + n * (time - Epoch);
        return IsOpen ? FromHyperbolic(SolveHyperbolic(meanAnomaly)) : FromEccentric(SolveElliptic(Normalize(meanAnomaly)));
    }

    /// <summary>The path to draw: the whole ellipse, or the part of an escape trajectory within <paramref name="maxRadius"/>.</summary>
    public IReadOnlyList<Point3> Path(int points = 180, double maxRadius = double.MaxValue)
    {
        var path = new List<Point3>(points + 1);
        if (!IsOpen)
        {
            for (var i = 0; i <= points; i++)
                path.Add(FromEccentric(2 * Math.PI * i / points));
            return path;
        }
        // Hyperbola: from the asymptote's limit (or the SOI edge) in, through periapsis, and out again.
        var a = Math.Abs(Sma);
        var hMax = Math.Acosh(Math.Clamp((maxRadius / a + 1) / Ecc, 1, 1e6));
        hMax = Math.Min(hMax, 6);
        for (var i = 0; i <= points; i++)
            path.Add(FromHyperbolic(-hMax + 2 * hMax * i / points));
        return path;
    }

    /// <summary>The period in seconds, or null for an open orbit.</summary>
    public double? Period(double mu) => IsOpen || mu <= 0 ? null : 2 * Math.PI * Math.Sqrt(Math.Pow(Math.Abs(Sma), 3) / mu);

    private Point3 FromEccentric(double e)
    {
        var a = Math.Abs(Sma);
        var x = a * (Math.Cos(e) - Ecc);
        var y = a * Math.Sqrt(Math.Max(0, 1 - Ecc * Ecc)) * Math.Sin(e);
        return Rotate(x, y);
    }

    private Point3 FromHyperbolic(double h)
    {
        var a = Math.Abs(Sma);
        var x = a * (Ecc - Math.Cosh(h));
        var y = a * Math.Sqrt(Ecc * Ecc - 1) * Math.Sinh(h);
        return Rotate(x, y);
    }

    // Perifocal (x towards periapsis) → the parent's frame: argument of periapsis, inclination, ascending node.
    private Point3 Rotate(double x, double y)
    {
        double R(double degrees) => degrees * Math.PI / 180;
        var (cw, sw) = (Math.Cos(R(ArgPe)), Math.Sin(R(ArgPe)));
        var (ci, si) = (Math.Cos(R(Inc)), Math.Sin(R(Inc)));
        var (co, so) = (Math.Cos(R(Lan)), Math.Sin(R(Lan)));
        var xp = cw * x - sw * y;
        var yp = sw * x + cw * y;
        return new Point3(co * xp - so * yp * ci, so * xp + co * yp * ci, yp * si);
    }

    private static double Normalize(double angle) => angle - 2 * Math.PI * Math.Floor(angle / (2 * Math.PI));

    // Kepler's equation M = E − e·sin E, by Newton's method (a good start converges in a few steps for any e < 1).
    private double SolveElliptic(double m)
    {
        var e = Ecc < 0.8 ? m : Math.PI;
        for (var i = 0; i < 50; i++)
        {
            var step = (e - Ecc * Math.Sin(e) - m) / (1 - Ecc * Math.Cos(e));
            e -= step;
            if (Math.Abs(step) < 1e-12)
                break;
        }
        return e;
    }

    // The hyperbolic form M = e·sinh H − H.
    private double SolveHyperbolic(double m)
    {
        var h = Math.Asinh(m / Ecc);
        for (var i = 0; i < 80; i++)
        {
            var step = (Ecc * Math.Sinh(h) - h - m) / (Ecc * Math.Cosh(h) - 1);
            h -= step;
            if (Math.Abs(step) < 1e-12)
                break;
        }
        return h;
    }
}

/// <summary>A planet, moon or star. <see cref="Parent"/> is null for the star.</summary>
public sealed record CelestialBody(string Name, string? Parent, double Radius, double Mu, Orbit? Orbit, string Color, int? Index = null)
{
    /// <summary>Sphere of influence radius (m): a · (μ / μparent)^0.4. Infinite for the star.</summary>
    public double SphereOfInfluence(SolarSystem system) =>
        Orbit == null || Parent == null || system.Find(Parent) is not { } parent
            ? double.PositiveInfinity
            : Math.Abs(Orbit.Sma) * Math.Pow(Mu / parent.Mu, 0.4);
}

/// <summary>
/// The bodies of a game. <see cref="Stock"/> is KSP's own Kerbol system; <see cref="Load"/> applies the planet packs in
/// a GameData folder on top (Kopernicus configs: new bodies, moved bodies, OPM's Eeloo around Sarnus...).
/// </summary>
public sealed class SolarSystem
{
    private readonly Dictionary<string, CelestialBody> _bodies;

    private SolarSystem(IEnumerable<CelestialBody> bodies) =>
        _bodies = bodies.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CelestialBody> Bodies => _bodies.Values;

    /// <summary>The bodies a planet pack added or changed (for "OPM: 13 bodies" on the page).</summary>
    public IReadOnlyList<string> FromPacks { get; private init; } = [];

    public CelestialBody? Find(string name) => _bodies.GetValueOrDefault(Normalize(name));

    /// <summary>The body a vessel's ORBIT refers to: by IDENT ("Squad/Kerbin", "Urlum"), else by stock index.</summary>
    public CelestialBody? ForVessel(string? ident, int index) =>
        (ident is { Length: > 0 } ? Find(ident) : null) ?? _bodies.Values.FirstOrDefault(b => b.Index == index);

    public IEnumerable<CelestialBody> ChildrenOf(string name) =>
        _bodies.Values.Where(b => string.Equals(b.Parent, name, StringComparison.OrdinalIgnoreCase));

    public CelestialBody Star => _bodies.Values.First(b => b.Parent == null);

    /// <summary>Where a body is at <paramref name="time"/>, relative to <paramref name="frame"/> (one of its ancestors, or itself).</summary>
    public Point3? PositionOf(string body, string frame, double time)
    {
        var position = new Point3(0, 0, 0);
        for (var current = Find(body); current != null; current = current.Parent == null ? null : Find(current.Parent))
        {
            if (string.Equals(current.Name, frame, StringComparison.OrdinalIgnoreCase))
                return position;
            if (current.Orbit == null || current.Parent == null || Find(current.Parent) is not { } parent)
                return null;
            position += current.Orbit.PositionAt(time, parent.Mu);
        }
        return null; // frame isn't an ancestor
    }

    private static string Normalize(string name)
    {
        var slash = name.LastIndexOf('/'); // "Squad/Kerbin"
        var trimmed = (slash >= 0 ? name[(slash + 1)..] : name).Trim();
        return trimmed.Equals("Kerbol", StringComparison.OrdinalIgnoreCase) ? "Sun" : trimmed;
    }

    // KSP 1.12's stock system (radius m, μ m³/s², orbit: SMA m, ecc, inc°, LAN°, argPe°, mean anomaly at epoch 0 rad).
    // The index is FlightGlobals' body index, which vessels' ORBIT REF uses.
    public static SolarSystem Stock() => new(StockBodies());

    private static IEnumerable<CelestialBody> StockBodies()
    {
        CelestialBody B(string name, string? parent, int index, double radius, double mu, string color, double sma = 0, double ecc = 0,
            double inc = 0, double lan = 0, double argPe = 0, double m0 = 0) =>
            new(name, parent, radius, mu, parent == null ? null : new Orbit(sma, ecc, inc, lan, argPe, m0, 0), color, index);
        return
        [
            B("Sun", null, 0, 261_600_000, 1.1723328e18, "#FFD24D"),
            B("Moho", "Sun", 4, 250_000, 1.6860938e11, "#8C6A5A", 5_263_138_304, 0.2, 7, 70, 15, 3.14),
            B("Eve", "Sun", 5, 700_000, 8.1717302e12, "#8E5BB5", 9_832_684_544, 0.01, 2.1, 15, 0, 3.14),
            B("Gilly", "Eve", 13, 13_000, 8_289_449.8, "#9C8A7A", 31_500_000, 0.55, 12, 80, 10, 0.9),
            B("Kerbin", "Sun", 1, 600_000, 3.5316e12, "#4A90D9", 13_599_840_256, 0, 0, 0, 0, 3.14),
            B("Mun", "Kerbin", 2, 200_000, 6.5138398e10, "#A0A0A0", 12_000_000, 0, 0, 0, 0, 1.7),
            B("Minmus", "Kerbin", 3, 60_000, 1.7658e9, "#B8E0D2", 47_000_000, 0, 6, 78, 38, 0.9),
            B("Duna", "Sun", 6, 320_000, 3.0136321e11, "#C1583A", 20_726_155_264, 0.051, 0.06, 135.5, 0, 3.14),
            B("Ike", "Duna", 7, 130_000, 1.8568369e10, "#7D7D7D", 3_200_000, 0.03, 0.2, 0, 0, 1.7),
            B("Dres", "Sun", 15, 138_000, 2.1484489e10, "#8F8577", 40_839_348_203, 0.145, 5, 280, 90, 3.14),
            B("Jool", "Sun", 8, 6_000_000, 2.82528e14, "#7CB342", 68_773_560_320, 0.05, 1.304, 52, 0, 0.1),
            B("Laythe", "Jool", 9, 500_000, 1.962e12, "#3D6EA8", 27_184_000, 0, 0, 0, 0, 3.14),
            B("Vall", "Jool", 10, 300_000, 2.074815e11, "#A8C8D8", 43_152_000, 0, 0, 0, 0, 0.9),
            B("Tylo", "Jool", 12, 600_000, 2.82528e12, "#D8C8B8", 68_500_000, 0, 0.025, 0, 0, 3.14),
            B("Bop", "Jool", 11, 65_000, 2.4868349e9, "#8B6F4E", 128_500_000, 0.235, 15, 10, 25, 0.9),
            B("Pol", "Jool", 14, 44_000, 7.2170208e8, "#D8C27A", 179_890_000, 0.171, 4.25, 2, 15, 0.9),
            B("Eeloo", "Sun", 16, 210_000, 7.4410815e10, "#DDDDDD", 90_118_820_000, 0.26, 6.15, 50, 260, 3.14),
        ];
    }

    /// <summary>
    /// Stock, then every Kopernicus "Body" in the GameData folder applied in ModuleManager's pass order (no pass, then
    /// :BEFORE, :FOR, :AFTER, :FINAL). A new body starts from its Template (a stock body) and takes what its config
    /// sets; an edit (%Body[Eeloo], @Body[Kerbin]) changes only what it lists; !Body[x] removes one. Only plain
    /// numbers are read: a value computed by a patch expression keeps the template's.
    /// </summary>
    public static SolarSystem Load(string? gameDataDirectory)
    {
        var bodies = StockBodies().ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
        if (gameDataDirectory != null && Directory.Exists(gameDataDirectory))
        {
            var edits = new List<(int Pass, string File, KspNode Node)>();
            foreach (var file in KopernicusFiles(gameDataDirectory))
                foreach (var top in SafeParse(file))
                    if (Operation(top.Name, out _) is var (verb, target, pass) && verb != '!' && target.Equals("Kopernicus", StringComparison.OrdinalIgnoreCase))
                        foreach (var body in top.Nodes)
                            edits.Add((pass, file, body));
            foreach (var (_, _, node) in edits.OrderBy(e => e.Pass).ThenBy(e => e.File, StringComparer.OrdinalIgnoreCase))
                Apply(bodies, node);
        }
        // A body whose parent was removed (or never defined) can't be placed; drop it rather than guess. So is one in a
        // loop (A round B round A, from a broken pack): everything that walks up the parents relies on reaching the star.
        bool ReachesStar(CelestialBody body)
        {
            for (var (current, depth) = (body, 0); depth < 64; depth++)
            {
                if (current.Parent == null)
                    return current.Name == "Sun";
                if (!bodies.TryGetValue(current.Parent, out var parent))
                    return false;
                current = parent;
            }
            return false;
        }
        var placed = bodies.Values.Where(ReachesStar).ToList();
        // "From packs": new bodies, and stock ones whose orbit, size or gravity really differ (Kopernicus' own
        // System.cfg restates every stock body, and visual mods touch them all without moving anything).
        var stock = StockBodies().ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
        return new SolarSystem(placed)
        {
            FromPacks = placed.Where(b => !stock.TryGetValue(b.Name, out var s) || s.Parent != b.Parent || s.Orbit != b.Orbit ||
                                          Math.Abs(s.Radius - b.Radius) > 1 || Math.Abs(s.Mu - b.Mu) > s.Mu * 1e-6)
                .Select(b => b.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    private static IEnumerable<string> KopernicusFiles(string gameData)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(gameData, "*.cfg", options))
        {
            string text;
            try
            {
                if (new FileInfo(file).Length > 4 * 1024 * 1024)
                    continue;
                text = File.ReadAllText(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            if (text.Contains("Kopernicus", StringComparison.Ordinal) && text.Contains("Body", StringComparison.Ordinal))
                yield return file;
        }
    }

    private static IReadOnlyList<KspNode> SafeParse(string file)
    {
        try
        {
            return KspConfigParser.Parse(File.ReadAllText(file));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // "@Kopernicus:FOR[OPM]" → ('@', "Kopernicus", pass 2); "%Body[Eeloo]" → ('%', "Body", ...) with the name in Bracket.
    private static (char Verb, string Target, int Pass)? Operation(string raw, out string? bracket)
    {
        bracket = null;
        var text = raw.Trim();
        var verb = text.Length > 0 && text[0] is '@' or '%' or '!' or '+' or '$' ? text[0] : ' ';
        if (verb != ' ')
            text = text[1..];
        var colon = text.IndexOf(':');
        var options = colon >= 0 ? text[colon..] : "";
        if (colon >= 0)
            text = text[..colon];
        var open = text.IndexOf('[');
        if (open >= 0 && text.IndexOf(']', open) is var close and > 0)
        {
            bracket = text[(open + 1)..close].Trim();
            text = text[..open];
        }
        var pass = options.Contains(":FINAL", StringComparison.OrdinalIgnoreCase) ? 4
            : options.Contains(":AFTER", StringComparison.OrdinalIgnoreCase) ? 3
            : options.Contains(":FOR", StringComparison.OrdinalIgnoreCase) ? 2
            : options.Contains(":BEFORE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        return (verb, text.Trim(), pass);
    }

    /// <summary>One Body node from a Kopernicus config, applied to the bodies so far.</summary>
    private static void Apply(Dictionary<string, CelestialBody> bodies, KspNode node)
    {
        if (Operation(node.Name, out var bracket) is not var (verb, target, _) || !target.Equals("Body", StringComparison.OrdinalIgnoreCase))
            return;
        var name = bracket ?? Value(node, "name");
        if (string.IsNullOrWhiteSpace(name))
            return;
        name = Normalize(name);
        if (verb == '!')
        {
            if (name != "Sun") // the map is drawn round the star; a pack can't take it away
                bodies.Remove(name);
            return;
        }

        var editing = verb is '@' or '%' && bodies.ContainsKey(name);
        var template = Child(node, "Template") is { } t && Value(t, "name") is { } templateName ? bodies.GetValueOrDefault(Normalize(templateName)) : null;
        var start = editing ? bodies[name] : template ?? (bodies.TryGetValue(name, out var existing) ? existing : null);
        if (start == null && verb == '@')
            return; // editing a body that doesn't exist
        var body = (start ?? new CelestialBody(name, "Sun", 100_000, 1e10, null, "#B0B0B0")) with { Name = name, Index = start?.Index };

        if (Child(node, "Properties") is { } properties)
        {
            // Gravity the way Kopernicus takes it: gravParameter, else mass (G·M), else surface gravity (g·R²).
            var radius = Number(properties, "radius") ?? body.Radius;
            var mu = Number(properties, "gravParameter")
                     ?? (Number(properties, "mass") is { } mass ? mass * 6.67430e-11 : (double?)null)
                     ?? (Number(properties, "geeASL") is { } gee ? gee * 9.80665 * radius * radius : (double?)null);
            body = body with { Radius = radius, Mu = mu ?? body.Mu };
        }
        if (Child(node, "Orbit") is { } orbit)
        {
            var o = body.Orbit ?? new Orbit(0, 0, 0, 0, 0, 0, 0);
            var meanAnomaly = Number(orbit, "meanAnomalyAtEpoch") ?? (Number(orbit, "meanAnomalyAtEpochD") is { } degrees ? degrees * Math.PI / 180 : null);
            o = new Orbit(Number(orbit, "semiMajorAxis") ?? o.Sma, Number(orbit, "eccentricity") ?? o.Ecc, Number(orbit, "inclination") ?? o.Inc,
                Number(orbit, "longitudeOfAscendingNode") ?? o.Lan, Number(orbit, "argumentOfPeriapsis") ?? o.ArgPe,
                meanAnomaly ?? o.MeanAnomalyAtEpoch, Number(orbit, "epoch") ?? o.Epoch);
            body = body with { Orbit = o, Parent = Value(orbit, "referenceBody") is { Length: > 0 } parent ? Normalize(parent) : body.Parent };
            if (Value(orbit, "color") is { } color && Color(color) is { } hex)
                body = body with { Color = hex };
        }
        if (name == "Sun")
            body = body with { Parent = null, Orbit = null }; // stays the star whatever a pack says
        else if (body.Parent == null)
            body = body with { Parent = "Sun" };
        if (body.Parent != null && (body.Orbit == null || Math.Abs(body.Orbit.Sma) < 1))
            return; // not placeable
        bodies[name] = body;
    }

    private static KspNode? Child(KspNode node, string name) =>
        node.Nodes.FirstOrDefault(n => Operation(n.Name, out _) is var (verb, target, _) && verb != '!' && target.Equals(name, StringComparison.OrdinalIgnoreCase));

    // Keys may carry a patch operator ("%referenceBody", "@radius"); the value is used as written.
    private static string? Value(KspNode node, string key) =>
        node.Values.LastOrDefault(v => v.Key.TrimStart('@', '%').Trim().Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.Trim();

    private static double? Number(KspNode node, string key) =>
        Value(node, key) is { } text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    // Kopernicus colors: "1,0.5,0.2,1" (0-1), "255,128,51,255" or "#RRGGBB".
    private static string? Color(string text)
    {
        if (text.StartsWith('#') && text.Length is 7 or 9)
            return text[..7];
        var parts = text.Split(',').Select(p => double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : -1).ToArray();
        if (parts.Length < 3 || parts.Take(3).Any(v => v < 0))
            return null;
        var scale = parts.Take(3).Any(v => v > 1) ? 1 : 255;
        return "#" + string.Concat(parts.Take(3).Select(v => ((int)Math.Clamp(v * scale, 0, 255)).ToString("X2")));
    }
}

/// <summary>An LMP server's universe time, as its clients compute it: seconds since Universe/StartTime.txt plus the
/// newest subspace's offset (Universe/Subspace.txt). Works whether the server is running or not.</summary>
public static class UniverseClock
{
    public static double? Now(string serverDirectory, DateTime? utcNow = null)
    {
        try
        {
            var universe = Path.Combine(serverDirectory, "Universe");
            var start = File.ReadLines(Path.Combine(universe, "StartTime.txt")).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));
            if (start == null || !DateTime.TryParse(start.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var started))
                return null;
            var offsets = File.ReadLines(Path.Combine(universe, "Subspace.txt"))
                .Where(l => !l.StartsWith('#') && l.Contains(':'))
                .Select(l => double.TryParse(l[(l.IndexOf(':') + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null)
                .OfType<double>().ToList();
            return ((utcNow ?? DateTime.UtcNow) - started).TotalSeconds + (offsets.Count > 0 ? offsets.Max() : 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>"Year 1, day 86, 03:12" on the Kerbin calendar (6-hour days, 426-day years).</summary>
    public static string Kerbin(double seconds)
    {
        var total = Math.Max(0, seconds);
        var year = (int)(total / (426 * 21600)) + 1;
        var day = (int)(total % (426 * 21600) / 21600) + 1;
        var time = TimeSpan.FromSeconds(total % 21600);
        return $"Year {year}, day {day}, {time.Hours:00}:{time.Minutes:00}";
    }
}
