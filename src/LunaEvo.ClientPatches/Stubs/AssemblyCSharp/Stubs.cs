// LUNA EVO · LunaEvo.ClientPatches/Stubs/AssemblyCSharp/Stubs.cs
// [Patch] Stand-ins with the names and shapes of the few KSP types the patches use, so they build without KSP.
// Used by: LunaEvo.ClientPatches (compile time only; in the game, KSP's real Assembly-CSharp is used)
// Guide:   Meat and Potatoes/27-Client-Patches.md

// Written by LUNA EVO, no Squad code: only names, and enum values read from KSP 1.12.5's metadata. The compiler bakes
// enum values in as numbers, so they must match KSP's exactly (checked in ClientPatchTests). Members are empty: they
// never run, the game's own ones do.

public enum GameScenes
{
    LOADING = 0,
    LOADINGBUFFER = 1,
    MAINMENU = 2,
    SETTINGS = 3,
    CREDITS = 4,
    SPACECENTER = 5,
    EDITOR = 6,
    FLIGHT = 7,
    TRACKSTATION = 8,
    PSYSTEM = 9,
    MISSIONBUILDER = 21,
}

[System.AttributeUsage(System.AttributeTargets.Class)]
public class KSPAddon : System.Attribute
{
    public enum Startup
    {
        Instantly = -2,
        MainMenu = 2,
        SpaceCentre = 5,
        Flight = 7,
    }

    public KSPAddon(Startup startup, bool once)
    {
    }
}

public class HighLogic : UnityEngine.MonoBehaviour
{
    public static GameScenes LoadedScene;
}

public class Vessel : UnityEngine.MonoBehaviour
{
    /// <summary>True while the vessel is "on rails" (no physics, moved along its orbit).</summary>
    public bool packed;
}
