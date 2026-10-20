// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/ConfigFileTests.cs
// [Test] Byte-for-byte round trips and in-place edits of the real config files.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.ConfigManager.Core.Files;

namespace LunaEvo.ConfigManager.Core.Tests;

public class ConfigFileTests
{
    public static TheoryData<string> RealFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(TestPaths.ServerConfigs, "*.xml").Order())
            data.Add(Path.GetFileName(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(RealFiles))]
    public void Unchanged_file_saves_byte_for_byte(string fileName)
    {
        var dir = TestPaths.CopyConfigs();
        var path = Path.Combine(dir, fileName);
        var original = File.ReadAllBytes(path);

        ConfigFile.Load(path).Save();

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Changing_one_value_changes_only_that_line()
    {
        var path = Path.Combine(TestPaths.CopyConfigs(), "GeneralSettings.xml");
        var before = File.ReadAllLines(path);

        var file = ConfigFile.Load(path);
        file.SetValue("ServerName", "LUNA EVO");
        file.Save();

        var after = File.ReadAllLines(path);
        Assert.Equal(before.Length, after.Length);
        var changed = before.Zip(after).Where(pair => pair.First != pair.Second).ToList();
        Assert.Single(changed);
        Assert.Equal("  <ServerName>LUNA EVO</ServerName>", changed[0].Second);
    }

    [Fact]
    public void Multiline_values_keep_the_files_line_endings()
    {
        var path = Path.Combine(TestPaths.CopyConfigs(), "GeneralSettings.xml");
        var file = ConfigFile.Load(path);

        file.SetValue("ServerMotd", "Hi %Name%!\r\nWelcome to LUNA EVO.");
        file.Save();

        var text = File.ReadAllText(path);
        Assert.Contains("<ServerMotd>Hi %Name%!\r\nWelcome to LUNA EVO.</ServerMotd>", text);
        Assert.DoesNotContain("\r\r\n", text);
        Assert.Equal("Hi %Name%!\nWelcome to LUNA EVO.", ConfigFile.Load(path).GetValue("ServerMotd"));
    }

    [Fact]
    public void Missing_setting_is_added_with_its_comment_before_the_closing_tag()
    {
        var path = Path.Combine(TestPaths.CopyConfigs(), "IntervalSettings.xml");
        var file = ConfigFile.Load(path);
        Assert.False(file.Has("NewSetting"));

        file.SetValue("NewSetting", "42", "A setting from a newer LMP");
        file.Save();

        var lines = File.ReadAllLines(path);
        Assert.Equal("</IntervalSettingsDefinition>", lines[^1]);
        Assert.Equal("  <NewSetting>42</NewSetting>", lines[^2]);
        Assert.Equal("  <!--A setting from a newer LMP-->", lines[^3]);
    }

    [Fact]
    public void Lists_round_trip_with_lmps_layout()
    {
        var path = Path.Combine(TestPaths.CopyConfigs(), "LMPModControl.xml");
        var file = ConfigFile.Load(path);
        var parts = file.GetList("AllowedParts")!;
        Assert.True(parts.Count > 100);

        // Writing the same list back must not change a byte.
        var original = File.ReadAllBytes(path);
        file.SetList("AllowedParts", parts);
        file.Save();
        Assert.Equal(original, File.ReadAllBytes(path));

        file.SetList("RequiredExpansions", ["MakingHistory"]);
        file.SetList("AllowedResources", []);
        file.Save();

        var reloaded = ConfigFile.Load(path);
        Assert.Equal(["MakingHistory"], reloaded.GetList("RequiredExpansions"));
        Assert.Empty(reloaded.GetList("AllowedResources")!);
        var text = File.ReadAllText(path);
        Assert.Contains("  <RequiredExpansions>\r\n    <string>MakingHistory</string>\r\n  </RequiredExpansions>", text);
        Assert.Contains("<AllowedResources></AllowedResources>", text);
    }
}
