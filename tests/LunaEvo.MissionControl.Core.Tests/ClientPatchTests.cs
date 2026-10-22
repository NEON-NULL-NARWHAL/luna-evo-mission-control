// LUNA EVO · LunaEvo.MissionControl.Core.Tests/ClientPatchTests.cs
// [Test] The LMP client patch's address reading (LunaEvo.ClientPatches/LmpAddress.cs, compiled into these tests).
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.ClientPatches;

namespace LunaEvo.MissionControl.Core.Tests;

public class ClientPatchTests
{
    [Theory]
    [InlineData("lmp://127.0.0.1", "127.0.0.1", 8800)]
    [InlineData("lmp://127.0.0.1:8810", "127.0.0.1", 8810)] // LMP 0.29.2 throws on this one and never finishes starting
    [InlineData("lmp://kerbin.example.com:8800/", "kerbin.example.com", 8800)]
    [InlineData("LMP://Host:1", "Host", 1)]
    [InlineData("kerbin.example.com:65535", "kerbin.example.com", 65535)]
    [InlineData("lmp://[::1]:8800", "::1", 8800)]
    [InlineData("[2001:db8::1]", "2001:db8::1", 8800)]
    [InlineData("2001:db8::1", "2001:db8::1", 8800)] // bare IPv6: no port
    [InlineData("  lmp://100.64.0.7:8800  ", "100.64.0.7", 8800)]
    public void Reads_addresses_lmp_itself_cant(string text, string host, int port)
    {
        Assert.True(LmpAddress.TryParse(text, out var h, out var p));
        Assert.Equal((host, port), (h, p));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lmp://")]
    [InlineData("lmp://:8800")]
    [InlineData("lmp://host:0")]
    [InlineData("lmp://host:65536")]
    [InlineData("lmp://host:88a0")]
    [InlineData("lmp://host:-1")]
    [InlineData("lmp://ho st:8800")]
    [InlineData("lmp://user@host:8800")]
    [InlineData("lmp://host/path:8800")]
    [InlineData("[::1")]
    [InlineData("[::1]8800")]
    public void Refuses_anything_else(string? text)
    {
        Assert.False(LmpAddress.TryParse(text!, out _, out var port));
        Assert.Equal(LmpAddress.DefaultPort, port);
    }

    [Fact]
    public void Refuses_absurdly_long_input() => Assert.False(LmpAddress.TryParse("lmp://" + new string('a', 400), out _, out _));

    // The patches build against LUNA EVO's stand-ins for KSP's types (src/LunaEvo.ClientPatches/Stubs). Enum values are
    // compiled in as numbers, so the stand-ins' must be KSP's own.
    private static readonly Dictionary<string, int> KspValues = new()
    {
        ["GameScenes.LOADING"] = 0, ["GameScenes.MAINMENU"] = 2, ["GameScenes.SPACECENTER"] = 5, ["GameScenes.EDITOR"] = 6,
        ["GameScenes.FLIGHT"] = 7, ["GameScenes.TRACKSTATION"] = 8, ["GameScenes.MISSIONBUILDER"] = 21,
        ["KSPAddon+Startup.Instantly"] = -2, ["KSPAddon+Startup.MainMenu"] = 2, ["KSPAddon+Startup.SpaceCentre"] = 5, ["KSPAddon+Startup.Flight"] = 7,
    };

    [Fact]
    public void The_stand_ins_have_ksps_own_enum_values()
    {
        // The stand-in source, as written (an accidental edit fails here, on CI too).
        var stubs = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "src", "LunaEvo.ClientPatches", "Stubs", "AssemblyCSharp", "Stubs.cs"));
        foreach (var (name, value) in KspValues)
            Assert.Matches($@"\b{System.Text.RegularExpressions.Regex.Escape(name.Split('.')[1])} = {value},", stubs);

        // And where the test rig's KSP is next to the repo, against KSP itself (metadata only, nothing runs or is copied).
        var managed = Path.Combine(TestPaths.RepoRoot, "..", "TestRig", "KSP", "KSP_x64_Data", "Managed");
        if (!File.Exists(Path.Combine(managed, "Assembly-CSharp.dll")))
            return;
        var resolver = new System.Reflection.PathAssemblyResolver(Directory.GetFiles(managed, "*.dll")); // KSP's own mscorlib and all
        using var context = new System.Reflection.MetadataLoadContext(resolver, "mscorlib");
        var ksp = context.LoadFromAssemblyPath(Path.Combine(managed, "Assembly-CSharp.dll"));
        foreach (var (name, value) in KspValues)
        {
            var dot = name.LastIndexOf('.');
            var field = ksp.GetType(name[..dot])!.GetField(name[(dot + 1)..])!;
            Assert.Equal(value, Convert.ToInt32(field.GetRawConstantValue()));
        }
    }
}
