// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/SchemaTests.cs
// [Test] Reading types, defaults, comments and presets out of LMP's Server.dll.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.ConfigManager.Core.Schema;

namespace LunaEvo.ConfigManager.Core.Tests;

public class SchemaTests
{
    private static readonly LmpSchema Schema = LmpSchemaLoader.Load(TestPaths.LmpServer);

    [Fact]
    public void Finds_a_schema_for_every_config_file()
    {
        var expected = Directory.GetFiles(TestPaths.ServerConfigs, "*.xml").Select(Path.GetFileName).Order();
        Assert.Equal(expected, Schema.Files.Select(f => f.FileName).Order());
    }

    [Fact]
    public void Reads_types_defaults_and_comments_from_server_dll()
    {
        var connection = Schema.Find("ConnectionSettings.xml")!;
        var heartbeat = connection.Find("HearbeatMsInterval")!; // LMP's spelling
        Assert.Equal(SettingKind.Integer, heartbeat.Kind);
        Assert.Equal("1000", heartbeat.DefaultValue);
        Assert.Contains("MUST be lower than the ConnectionMsTimeout", heartbeat.Description);

        var mtu = connection.Find("MaximumTransmissionUnit")!;
        Assert.Equal((1m, 8192m), (mtu.Min, mtu.Max));

        var red = Schema.Find("DedicatedServerSettings.xml")!.Find("Red")!;
        Assert.Equal((0m, 255m), (red.Min, red.Max));

        var general = Schema.Find("GeneralSettings.xml")!;
        Assert.Equal(30, general.Find("ServerName")!.MaxLength);
        Assert.Equal(SettingKind.MultilineText, general.Find("ServerMotd")!.Kind);
        Assert.Equal(["Sandbox", "Science", "Career"], general.Find("GameMode")!.EnumValues);

        Assert.Equal(5000m, Schema.Find("MasterServerSettings.xml")!.Find("MasterServerRegistrationMsInterval")!.Min);
        Assert.NotEmpty(Schema.Find("IntervalSettings.xml")!.Find("VesselUpdatesMsInterval")!.Description);
    }

    [Fact]
    public void Gameplay_defaults_come_from_difficulty_presets()
    {
        var gameplay = Schema.Find("GameplaySettings.xml")!;
        Assert.Equal(["Easy", "Hard", "Moderate", "Normal"], gameplay.Presets.Keys.Order());

        var revert = gameplay.Find("CanRevert")!;
        Assert.Equal("true", gameplay.DefaultFor(revert, "Easy"));
        Assert.Equal("false", gameplay.DefaultFor(revert, "Hard"));
    }

    [Fact]
    public void Mod_control_defaults_include_lmps_part_list()
    {
        var modControl = Schema.Find("LMPModControl.xml")!;
        Assert.Equal("ModControlStructure", modControl.RootElement);

        var parts = modControl.Find("AllowedParts")!;
        Assert.Equal(SettingKind.StringList, parts.Kind);
        Assert.Contains("mk1pod", parts.DefaultValue.Split('\n'));
        Assert.Equal(SettingKind.ObjectList, modControl.Find("MandatoryPlugins")!.Kind);
    }

    [Theory]
    [InlineData("Name of the server. Max 30 char", null, null, 30)]
    [InlineData("Specify the server's MOTD (message of the day). 255 chars max", null, null, 255)]
    [InlineData("Specify in miliseconds how often we will update the info with masterserver. Min value = 5000", 5000.0, null, null)]
    [InlineData("A percentage value to simulate the probability of a packet loss. From 0.0% to 100.0%", 0.0, 100.0, null)]
    [InlineData("Allow Reverting", null, null, null)]
    public void Parses_limits_from_comments(string comment, double? min, double? max, int? maxLength)
    {
        var rules = CommentRules.Parse(comment);
        Assert.Equal(((decimal?)min, (decimal?)max, maxLength), rules);
    }
}
