// LUNA EVO · LunaEvo.ClientPatches/StampPatch.cs
// [Patch] Stamps each position message with the time its position is really for: one physics tick later than LMP does.
// Used by: PatchLoader (Harmony postfix on LmpClient.Systems.VesselPositionSys.VesselPositionMessageSender.CreateMessageFromVessel)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using LmpCommon.Message.Data.Vessel;
using UnityEngine;

namespace LunaEvo.ClientPatches
{
    /// <summary>
    /// Measured in the test rig (Meat and Potatoes 26): for a vessel under physics, the position LMP puts in a message is
    /// where the vessel is one physics tick (Time.fixedDeltaTime, 0.02 s) after the time it stamps on it. Every other
    /// player then plays it back one tick early. This adds that tick to the stamp, so the value is right; the message
    /// itself (its fields, its size, when it's sent) doesn't change, and unpatched players simply get a better time.
    /// Vessels "on rails" (packed) are moved along their orbit for the stamped time and are left alone.
    /// </summary>
    internal static class StampPatch
    {
        internal static void Postfix(Vessel vessel, VesselPositionMsgData __result)
        {
            if (__result != null && vessel != null && !vessel.packed)
                __result.GameTime += Time.fixedDeltaTime;
        }
    }
}
