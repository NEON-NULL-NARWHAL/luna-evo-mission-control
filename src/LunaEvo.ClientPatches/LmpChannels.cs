// LUNA EVO · LunaEvo.ClientPatches/LmpChannels.cs
// [Patch] Which Lidgren channel a vessel's fast updates go on (audit 11, P1); no LMP types, so the server plugin shares it.
// Used by: ChannelPatch (client), LunaEvoPlugin's ServerChannelPatch (server)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System;

namespace LunaEvo.ClientPatches
{
    /// <summary>
    /// The channel for one vessel's fast updates (position, flight state, update, resource: LMP's UnreliableSequenced
    /// messages). LMP puts them all on channel 0, and "sequenced" drops anything older than the newest seen on a channel,
    /// so when packets arrive out of order, vessel A's fresh update is thrown away because vessel B's (or A's flight state)
    /// arrived first (audit 11, P1). Spread over channels 1-31 by vessel and kind, an update is only ever dropped for a
    /// newer one of the same vessel and kind, which is what sequencing is for. Lidgren delivers any channel and LMP never
    /// looks at it, so unpatched players and servers receive these exactly as before.
    /// </summary>
    public static class LmpChannels
    {
        public static int ForVessel(Guid vessel, ushort kind) => 1 + (int)((uint)(vessel.GetHashCode() * 31 + kind) % 31u);
    }
}
