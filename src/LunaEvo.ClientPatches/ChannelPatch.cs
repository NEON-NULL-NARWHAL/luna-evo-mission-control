// LUNA EVO · LunaEvo.ClientPatches/ChannelPatch.cs
// [Patch] Gives each vessel and kind of fast update its own Lidgren channel, so one vessel's update can't make another's drop.
// Used by: PatchLoader (Harmony postfix on VesselCliMsg.DefaultChannel)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using LmpCommon.Message.Client;
using LmpCommon.Message.Data.Vessel;

namespace LunaEvo.ClientPatches
{
    internal static class ChannelPatch
    {
        /// <summary>Harmony postfix: only LMP's channel 0 (the unreliable vessel updates) is changed.</summary>
        internal static void Postfix(VesselCliMsg __instance, ref int __result)
        {
            if (__result == 0 && __instance?.Data is VesselBaseMsgData data)
                __result = LmpChannels.ForVessel(data.VesselId, data.SubType);
        }
    }
}
