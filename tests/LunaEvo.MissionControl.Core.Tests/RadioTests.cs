// LUNA EVO · LunaEvo.MissionControl.Core.Tests/RadioTests.cs
// [Test] The radio: the tower writes its channel to radio.jsonl when it talks on LMP chat, and the server plugin's relay
//        says each new line as "KSC tower", one a second, never as anyone else, cut short.
// Used by: dotnet test
// Guide:   Meat and Potatoes/28-Tower.md

using System.Text.Json;
using LunaEvo.MissionControl.Core.Tower;
using LunaEvo.ServerPlugin;

namespace LunaEvo.MissionControl.Core.Tests;

public class RadioTests
{
    private static readonly DateTime T0 = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_tower_writes_its_channel_for_the_plugin_only_when_it_talks_on_chat()
    {
        var radio = Path.Combine(Path.GetTempPath(), "LunaEvoTests", "radio-" + Guid.NewGuid().ToString("N"), "radio.jsonl");
        using var server = new TowerServer(0, () => T0);
        server.Answer("POST", "/v1/say", """{"callSign":"Alpha","kind":"intent","text":"local flight"}""");
        Assert.False(File.Exists(radio)); // off

        server.RadioPath = radio;
        server.Answer("POST", "/v1/say", """{"callSign":"Alpha","kind":"request-approach","text":"request visual"}""");
        var lines = File.ReadAllLines(radio).Select(l => JsonDocument.Parse(l).RootElement).ToList();
        Assert.Equal(["Alpha", "KSC tower"], lines.Select(l => l.GetProperty("from").GetString()));
        Assert.StartsWith("Alpha, KSC tower, cleared", lines[1].GetProperty("text").GetString());
    }

    [Fact]
    public void The_plugin_says_new_lines_as_the_tower_one_a_second()
    {
        var path = Path.Combine(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "radio-" + Guid.NewGuid().ToString("N"))).FullName, "radio.jsonl");
        File.WriteAllText(path, """{"id":1,"from":"KSC tower","to":"Alpha","text":"old news"}""" + "\n");
        var relay = new RadioRelay(path);
        Assert.Null(relay.Next(T0)); // what was there before the server started isn't said

        File.AppendAllText(path,
            """{"id":2,"from":"Alpha","to":"KSC tower","text":"request visual"}""" + "\n" +
            """{"id":3,"from":"KSC tower","to":"Alpha","text":"Alpha, KSC tower, cleared VISUAL 09."}""" + "\n" +
            """{"id":4,"from":"Mallory","to":"KSC tower","text":"x\u0007y"}""" + "\n" +
            "not json\n" +
            """{"id":3,"from":"KSC tower","text":"a repeat"}""" + "\n" +
            "{\"id\":5,\"from\":\"KSC tower\",\"text\":\"" + new string('a', 500) + "\"}\n");
        Assert.Equal("Alpha → tower: request visual", relay.Next(T0.AddSeconds(1)));
        Assert.Null(relay.Next(T0.AddSeconds(1.5))); // one a second
        Assert.Equal("Alpha, KSC tower, cleared VISUAL 09.", relay.Next(T0.AddSeconds(2)));
        Assert.Equal("Mallory → tower: xy", relay.Next(T0.AddSeconds(3))); // never said as them; no control characters
        Assert.Equal(RadioRelay.MaxText, relay.Next(T0.AddSeconds(4))!.Length);
        Assert.Null(relay.Next(T0.AddSeconds(5)));
        Assert.Equal("KSC tower", RadioRelay.Speaker);
    }
}
