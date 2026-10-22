// LUNA EVO · LunaEvo.MissionControl.Core/Map/LiveVessels.cs
// [Core] The live map: the save's vessels, brought up to date with what the running server holds in memory.
// Used by: MapPage (website orbits, and the LUNA EVO server plugin's live.json)
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

    /// <summary>
    /// The server plugin's positions on top (newer than the website's, and they say whether it's landed and who flies
    /// it). Vessels it has seen that aren't in the list yet are added: launched since the last save.
    /// </summary>
    public static IReadOnlyList<UniverseVessel> Overlay(IReadOnlyList<UniverseVessel> vessels, LiveFeed feed, IReadOnlyDictionary<int, string> bodyNames)
    {
        var positions = new Dictionary<string, LiveVessel>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in feed.Vessels)
            positions.TryAdd(Key(v.Id), v);
        var result = new List<UniverseVessel>(vessels.Count);
        foreach (var vessel in vessels)
        {
            if (!positions.Remove(Key(vessel.Id), out var now))
            {
                result.Add(vessel);
                continue;
            }
            var bodyName = bodyNames.GetValueOrDefault(now.Body) ?? (vessel.Body == now.Body ? vessel.BodyName : null);
            result.Add(vessel with
            {
                Body = now.Body,
                BodyName = bodyName ?? (now.BodyName.Length > 0 ? now.BodyName : null),
                Orbit = now.Orbit ?? vessel.Orbit,
                Surface = (now.Lat, now.Lon),
                Altitude = now.Alt,
                Situation = Situation(now, vessel.Situation, vessel.IsLanded),
                FlownBy = now.FlownBy,
            });
        }
        foreach (var now in positions.Values)
            result.Add(new UniverseVessel(Key(now.Id), "(new vessel)", "Unknown", Situation(now, "", false), now.Body, 0, [], "")
            {
                Orbit = now.Orbit,
                BodyName = bodyNames.GetValueOrDefault(now.Body) ?? (now.BodyName.Length > 0 ? now.BodyName : null),
                Surface = (now.Lat, now.Lon),
                Altitude = now.Alt,
                FlownBy = now.FlownBy,
            });
        return result;
    }

    // The position message says landed or splashed; otherwise it's moving, and a vessel saved as landed has taken off.
    private static string Situation(LiveVessel now, string saved, bool savedLanded) =>
        now.Landed ? "LANDED" : now.Splashed ? "SPLASHED" : savedLanded ? "FLYING" : saved;

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
