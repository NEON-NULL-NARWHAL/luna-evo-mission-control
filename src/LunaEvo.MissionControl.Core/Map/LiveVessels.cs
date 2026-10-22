// LUNA EVO · LunaEvo.MissionControl.Core/Map/LiveVessels.cs
// [Core] The live map: the save's vessels, brought up to date with what the running server holds in memory.
// Used by: MapPage
// Guide:   Meat and Potatoes/24-Solar-System-Map.md

using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Ops;

namespace LunaEvo.MissionControl.Core.Map;

/// <summary>
/// LMP writes vessels to Universe/Vessels only on backups (minutes apart), but its website lists the server's
/// in-memory copy, whose orbits players update every 2.5 s. The website only has orbits, names and types, so the
/// save fills in the rest (crew, parts, situation, the body's name).
/// </summary>
public static class LiveVessels
{
    /// <summary>A landed vessel this far above or below where it was saved has taken off.</summary>
    public const double TookOffMetres = 50;

    /// <summary>
    /// The vessels as the server has them now: saved ones with the live orbit and position, launches not saved yet,
    /// and none that the server has removed.
    /// </summary>
    public static IReadOnlyList<UniverseVessel> Merge(IReadOnlyList<UniverseVessel> saved, IReadOnlyList<VesselSummary> live)
    {
        var bodies = BodyNames(saved);
        var byId = new Dictionary<string, UniverseVessel>(StringComparer.OrdinalIgnoreCase);
        foreach (var vessel in saved)
            byId.TryAdd(Key(vessel.Id), vessel);

        var merged = new List<UniverseVessel>(live.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var now in live)
        {
            var id = Key(now.Id);
            if (id.Length == 0 || !seen.Add(id))
                continue;
            var bodyName = bodies.GetValueOrDefault(now.ReferenceBody);
            if (byId.TryGetValue(id, out var vessel))
            {
                // The website has no situation (LMP 0.29 leaves it empty), so the saved one stays, unless a landed
                // vessel has clearly moved up or down since (it took off) or changed body.
                var sameBody = vessel.Body == now.ReferenceBody;
                var tookOff = vessel.IsLanded && (!sameBody || vessel.Altitude is { } saidAlt && Math.Abs(now.Altitude - saidAlt) > TookOffMetres);
                merged.Add(vessel with
                {
                    Situation = tookOff ? "FLYING" : vessel.Situation,
                    Altitude = now.Altitude,
                    Body = now.ReferenceBody,
                    Orbit = now.Orbit ?? (sameBody ? vessel.Orbit : null),
                    BodyName = bodyName ?? (sameBody ? vessel.BodyName : null),
                    Surface = (now.Lat, now.Lon),
                });
            }
            else
            {
                merged.Add(new UniverseVessel(id, now.Name.Length > 0 ? now.Name : "(unnamed)", now.Type.Length > 0 ? now.Type : "Unknown",
                    "", now.ReferenceBody, 0, [], "")
                {
                    Orbit = now.Orbit,
                    BodyName = bodyName,
                    Surface = (now.Lat, now.Lon),
                    Altitude = now.Altitude,
                });
            }
        }
        return merged;
    }

    // LMP's ids are GUIDs (the file name, and "Id" on the website); compare them however they're written.
    private static string Key(string id) => Guid.TryParse(id, out var guid) ? guid.ToString("D") : id.Trim();

    /// <summary>
    /// Which body each ORBIT REF number means on this server. Planet packs renumber bodies (OPM's Sarnus is 21 here),
    /// and every saved vessel carries both the number and the name, so the save is the reference.
    /// </summary>
    public static IReadOnlyDictionary<int, string> BodyNames(IEnumerable<UniverseVessel> saved)
    {
        var names = new Dictionary<int, string>();
        foreach (var vessel in saved)
            if (vessel.BodyName is { Length: > 0 } name)
                names.TryAdd(vessel.Body, name);
        return names;
    }
}
