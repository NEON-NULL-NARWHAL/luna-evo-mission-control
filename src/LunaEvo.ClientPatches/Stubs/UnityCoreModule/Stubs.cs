// LUNA EVO · LunaEvo.ClientPatches/Stubs/UnityCoreModule/Stubs.cs
// [Patch] Stand-ins with the names and shapes of the few Unity types the patches use, so they build without KSP.
// Used by: LunaEvo.ClientPatches (compile time only; in the game, Unity's real UnityEngine.CoreModule is used)
// Guide:   Meat and Potatoes/27-Client-Patches.md

// Written by LUNA EVO, no Unity code: the class chain MonoBehaviour derives from and the static methods the patches
// call, with Unity's own signatures, so the compiled calls bind to the real ones in the game. Bodies never run.

namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(Object target) => throw new System.NotSupportedException();

        public static void Destroy(Object obj) => throw new System.NotSupportedException();
    }

    public class Component : Object
    {
    }

    public class Behaviour : Component
    {
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public static class Time
    {
        public static float fixedDeltaTime => throw new System.NotSupportedException();

        public static float timeScale { get => throw new System.NotSupportedException(); set => throw new System.NotSupportedException(); }
    }

    public static class Debug
    {
        public static void Log(object message) => throw new System.NotSupportedException();

        public static void LogError(object message) => throw new System.NotSupportedException();
    }
}
