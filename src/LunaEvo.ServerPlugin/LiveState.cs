// LUNA EVO · LunaEvo.ServerPlugin/LiveState.cs
// [Plugin] What the server plugin knows right now: each vessel's latest position message, and the JSON it writes.
// Used by: LunaEvoPlugin
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Collections.Concurrent;
using System.Text.Json;

namespace LunaEvo.ServerPlugin;

/// <summary>One vessel's latest position, copied out of LMP's message (LMP reuses its message objects).</summary>
internal sealed record VesselState(
    Guid Id, int Body, string BodyName, double Lat, double Lon, double Alt, double[] Orbit, double[] Velocity,
    double GameTime, int Subspace, bool Landed, bool Splashed, string From, DateTime ReceivedUtc);

internal sealed record PlayerState(string Name, int Subspace, string? Vessel);

internal sealed record SubspaceState(int Id, double Time);

/// <summary>
/// Kept small and fast on purpose: LMP calls the plugin on its receive path, under a lock shared by all plugins.
/// </summary>
internal sealed class LiveState
{
    /// <summary>The file's layout version; Mission Control refuses formats it doesn't know.</summary>
    public const int Format = 1;

    /// <summary>A bound on memory, whatever clients send (a big server has a few thousand vessels).</summary>
    public const int MaxVessels = 20_000;

    private readonly ConcurrentDictionary<Guid, VesselState> _vessels = new();
    private long _changes;

    public int Count => _vessels.Count;
    public long Changes => Interlocked.Read(ref _changes);

    /// <summary>A position message arrived (orbit: INC, ECC, SMA, LAN, LPE, MNA, EPH, REF, as LMP sends it).</summary>
    public void Position(VesselState state)
    {
        if (state.Id == Guid.Empty || state.Orbit.Length != 8 || state.Velocity.Length != 3)
            return;
        if (!_vessels.ContainsKey(state.Id) && _vessels.Count >= MaxVessels)
            return;
        _vessels[state.Id] = state;
        Interlocked.Increment(ref _changes);
    }

    public void Removed(Guid id)
    {
        if (_vessels.TryRemove(id, out _))
            Interlocked.Increment(ref _changes);
    }

    public void Clear()
    {
        _vessels.Clear();
        Interlocked.Increment(ref _changes);
    }

    /// <summary>
    /// The JSON Mission Control reads. Player names and vessel ids only: never addresses (LMP knows them, this
    /// file doesn't).
    /// </summary>
    public byte[] ToJson(string pluginVersion, string lmpVersion, DateTime nowUtc, IEnumerable<PlayerState> players,
        IEnumerable<SubspaceState> subspaces, IReadOnlyDictionary<Guid, string> controllers)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("Format", Format);
            json.WriteString("Plugin", pluginVersion);
            json.WriteString("Lmp", lmpVersion);
            json.WriteString("WrittenUtc", nowUtc);
            json.WriteStartArray("Players");
            foreach (var player in players)
            {
                json.WriteStartObject();
                json.WriteString("Name", player.Name);
                json.WriteNumber("Subspace", player.Subspace);
                if (player.Vessel != null)
                    json.WriteString("Vessel", player.Vessel);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("Subspaces");
            foreach (var subspace in subspaces)
            {
                json.WriteStartObject();
                json.WriteNumber("Id", subspace.Id);
                Number(json, "Time", subspace.Time);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("Vessels");
            foreach (var v in _vessels.Values)
            {
                json.WriteStartObject();
                json.WriteString("Id", v.Id);
                json.WriteNumber("Body", v.Body);
                json.WriteString("BodyName", v.BodyName);
                Number(json, "Lat", v.Lat);
                Number(json, "Lon", v.Lon);
                Number(json, "Alt", v.Alt);
                json.WriteStartArray("Orbit");
                foreach (var x in v.Orbit)
                    json.WriteNumberValue(double.IsFinite(x) ? x : 0);
                json.WriteEndArray();
                json.WriteStartArray("Velocity");
                foreach (var x in v.Velocity)
                    json.WriteNumberValue(double.IsFinite(x) ? x : 0);
                json.WriteEndArray();
                Number(json, "GameTime", v.GameTime);
                json.WriteNumber("Subspace", v.Subspace);
                json.WriteBoolean("Landed", v.Landed);
                json.WriteBoolean("Splashed", v.Splashed);
                json.WriteString("From", v.From);
                json.WriteString("ReceivedUtc", v.ReceivedUtc);
                if (controllers.TryGetValue(v.Id, out var controller))
                    json.WriteString("FlownBy", controller);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    // JSON has no NaN or infinity; a bad number from a client must not break the whole file.
    private static void Number(Utf8JsonWriter json, string name, double value) => json.WriteNumber(name, double.IsFinite(value) ? value : 0);
}
