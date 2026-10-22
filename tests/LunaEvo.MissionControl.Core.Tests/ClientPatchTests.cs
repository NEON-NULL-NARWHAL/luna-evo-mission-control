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
}
