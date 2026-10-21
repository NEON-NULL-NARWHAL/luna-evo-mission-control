// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/SecurityRound6Tests.cs
// [Test] Audit 6: dot-only names, device-name crafts, size-capped release feeds.
// Used by: dotnet test
// Guide:   Meat and Potatoes/14-Security.md

using LunaEvo.ConfigManager.Core.Mods;
using LunaEvo.ConfigManager.Core.Setup;

namespace LunaEvo.ConfigManager.Core.Tests;

public class SecurityRound6Tests
{
    [Fact]
    public void Removing_a_ckan_version_called_dot_dot_deletes_nothing_above_its_folder()
    {
        var parent = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", "tools-" + Guid.NewGuid().ToString("N"))).FullName;
        var marker = Path.Combine(parent, "keep.txt");
        File.WriteAllText(marker, "keep");
        CkanTools.Root = Directory.CreateDirectory(Path.Combine(parent, "CKAN")).FullName;

        CkanTools.Remove("..");
        CkanTools.Remove(".");

        Assert.True(File.Exists(marker));
        Assert.True(Directory.Exists(CkanTools.Root));
    }

    [Theory]
    [InlineData("CON", "CON-craft")]
    [InlineData("nul.v2", "nul.v2-craft")]
    [InlineData("Com1 ", "Com1-craft")]
    [InlineData("Kerbal X", "Kerbal X")]
    [InlineData("..", "Craft")]
    public void Craft_names_never_become_windows_devices(string name, string expected) =>
        Assert.Equal(expected, CraftShare.SafeName(name));

    [Fact]
    public async Task A_huge_release_list_is_refused_instead_of_buffered()
    {
        var http = new FakeHttp().Text(LmpReleaseFeed.ReleasesUrl, "[" + new string(' ', LmpReleaseFeed.MaxFeedBytes + 10) + "]");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new LmpReleaseFeed(http.Client).GetAsync(false));
    }
}
