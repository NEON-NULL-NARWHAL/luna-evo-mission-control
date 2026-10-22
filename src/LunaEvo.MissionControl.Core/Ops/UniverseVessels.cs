// LUNA EVO · LunaEvo.MissionControl.Core/Ops/UniverseVessels.cs
// [Core] Vessel cleanup: list the vessels in a server's Universe and remove debris or abandoned ones (backed up first).
// Used by: VesselsPage
// Guide:   Meat and Potatoes/21-Vessel-Cleanup.md

using System.Globalization;
using System.IO.Compression;
using LunaEvo.MissionControl.Core.Mods;

namespace LunaEvo.MissionControl.Core.Ops;

/// <param name="Type">KSP's vessel type: Ship, Station, Probe, Debris, SpaceObject (asteroids/comets), Flag, EVA...</param>
/// <param name="Situation">KSP's situation: ORBITING, LANDED, FLYING, ESCAPING, PRELAUNCH...</param>
/// <param name="Body">The orbit's reference body index (0 Sun, 1 Kerbin, 2 Mun...), or -1.</param>
public sealed record UniverseVessel(string Id, string Name, string Type, string Situation, int Body, int PartCount, IReadOnlyList<string> Crew, string Path)
{
    public bool IsDebris => Type.Equals("Debris", StringComparison.OrdinalIgnoreCase);
    public bool IsSpaceObject => Type.Equals("SpaceObject", StringComparison.OrdinalIgnoreCase);
    public bool HasCrew => Crew.Count > 0;

    /// <summary>Its orbit (for the map), or null if the file has none or it can't be read.</summary>
    public Map.Orbit? Orbit { get; init; }

    /// <summary>The body it orbits as the file names it (ORBIT IDENT: "Squad/Kerbin", "Urlum"), or null.</summary>
    public string? BodyName { get; init; }

    /// <summary>Latitude and longitude in degrees, for landed and splashed vessels.</summary>
    public (double Lat, double Lon)? Surface { get; init; }

    /// <summary>Altitude above sea level (m) when it was saved.</summary>
    public double? Altitude { get; init; }

    /// <summary>The player flying it right now (from the LUNA EVO server plugin), or null.</summary>
    public string? FlownBy { get; init; }

    public bool IsLanded => Situation is "LANDED" or "SPLASHED" or "PRELAUNCH";
}

/// <summary>
/// LMP 0.29.2 keeps each vessel as <c>Universe/Vessels/&lt;id&gt;.txt</c>, a KSP VESSEL node
/// (Server/System/VesselStoreSystem.cs). It reads them all at start and rewrites them while it runs, so removing
/// vessels is only safe while the server is stopped; the UI enforces that, and a Universe backup is made first.
/// Removing a crewed vessel would leave its kerbals "assigned", so crewed vessels are never part of a bulk selection.
/// </summary>
public static class UniverseVessels
{
    public const int MaxVessels = 20_000;

    public static string Folder(string serverDirectory) => System.IO.Path.Combine(serverDirectory, "Universe", "Vessels");

    /// <summary>Every readable vessel; unreadable files are skipped (and stay where they are).</summary>
    public static IReadOnlyList<UniverseVessel> List(string serverDirectory)
    {
        var folder = Folder(serverDirectory);
        if (!Directory.Exists(folder))
            return [];
        var vessels = new List<UniverseVessel>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.txt").Take(MaxVessels))
        {
            try
            {
                vessels.Add(Parse(File.ReadAllText(file), file));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // A file LMP is still writing, or damaged: leave it alone.
            }
        }
        return vessels.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static UniverseVessel Parse(string text, string path)
    {
        // The VESSEL's own values sit at the top level of the file; wrap it so the parser keeps them.
        var root = KspConfigParser.Parse("VESSEL\n{\n" + text + "\n}\n").FirstOrDefault()
                   ?? throw new InvalidDataException("That isn't a KSP vessel.");
        var orbit = root.Nodes.FirstOrDefault(n => n.Name == "ORBIT");
        var body = int.TryParse(orbit?.Get("REF"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : -1;
        var parts = root.Nodes.Where(n => n.Name == "PART").ToList();
        var crew = parts.SelectMany(p => p.Values.Where(v => v.Key == "crew").Select(v => v.Value)).Where(c => c.Length > 0).Distinct().ToList();
        var id = System.IO.Path.GetFileNameWithoutExtension(path);
        double? N(KspNode? node, string key) =>
            node?.Get(key) is { } text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
        var elements = orbit == null ? null
            : N(orbit, "SMA") is { } sma && N(orbit, "ECC") is { } ecc && ecc >= 0
                ? new Map.Orbit(sma, ecc, N(orbit, "INC") ?? 0, N(orbit, "LAN") ?? 0, N(orbit, "LPE") ?? 0, N(orbit, "MNA") ?? 0, N(orbit, "EPH") ?? 0)
                : null;
        return new UniverseVessel(id, root.Get("name") is { Length: > 0 } name ? name : "(unnamed)", root.Get("type") ?? "Unknown",
            root.Get("sit") ?? "", body, parts.Count, crew, path)
        {
            Orbit = elements,
            BodyName = orbit?.Get("IDENT") is { Length: > 0 } ident ? ident : null,
            Surface = N(root, "lat") is { } lat && N(root, "lon") is { } lon ? (lat, lon) : null,
            Altitude = N(root, "alt"),
        };
    }

    /// <summary>
    /// Removes vessels: first zips the whole Universe ("beforecleanup", see <see cref="UniverseBackups"/>), then deletes
    /// their files. Returns the backup. The caller must make sure the server is stopped.
    /// </summary>
    public static UniverseSnapshot Remove(string serverDirectory, IEnumerable<UniverseVessel> vessels)
    {
        var folder = Path.GetFullPath(Folder(serverDirectory)) + Path.DirectorySeparatorChar;
        var targets = vessels.Select(v => Path.GetFullPath(v.Path)).Distinct().ToList();
        if (targets.Any(t => !t.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Only vessels in this server's Universe can be removed.");
        var backups = new UniverseBackups(serverDirectory);
        var (snapshot, skipped) = backups.Create("beforecleanup", compression: CompressionLevel.Fastest);
        if (skipped > 0)
        {
            // An incomplete backup can't undo the removal, so remove nothing.
            File.Delete(snapshot.Path);
            throw new IOException($"{skipped} Universe file(s) couldn't be read for the backup (is something using them?), so nothing was removed.");
        }
        backups.Prune(5, "beforecleanup");
        foreach (var target in targets)
            File.Delete(target);
        return snapshot;
    }
}
