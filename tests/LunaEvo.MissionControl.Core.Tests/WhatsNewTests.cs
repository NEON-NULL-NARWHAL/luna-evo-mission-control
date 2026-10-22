// LUNA EVO · LunaEvo.MissionControl.Core.Tests/WhatsNewTests.cs
// [Test] Reading CHANGELOG.md for the What's new tab (Core/Setup/WhatsNew.cs).
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.MissionControl.Core.Setup;

namespace LunaEvo.MissionControl.Core.Tests;

public class WhatsNewTests
{
    [Fact]
    public void Reads_versions_newest_first_as_plain_text()
    {
        const string changelog = """
            # Changelog

            ## 2.8.0 (2026-10-22)

            ### Added
            - **A LUNA EVO button in KSP**: see [27](Meat%20and%20Potatoes/27.md), with `code`
              and a wrapped second line.

            ## 2.7.1 (2026-10-22)

            ### Fixed
            - One fix.

            ## 2.7.0
            - Older.
            """;
        var entries = WhatsNew.Parse(changelog, count: 2);
        Assert.Equal(["2.8.0 (2026-10-22)", "2.7.1 (2026-10-22)"], entries.Select(e => e.Heading));
        Assert.Equal(["ADDED", "- A LUNA EVO button in KSP: see 27, with code and a wrapped second line."], entries[0].Lines);
        Assert.Equal(["FIXED", "- One fix."], entries[1].Lines);
    }

    [Fact]
    public void The_real_changelog_starts_with_this_version()
    {
        var entries = WhatsNew.Parse(File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "CHANGELOG.md")));
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.NotEmpty(e.Lines));
    }

    [Theory]
    [InlineData("")]
    [InlineData("## ")]
    [InlineData("no headings at all\n- just text")]
    [InlineData("## 1\n  \n  \n### \n[](")]
    public void Odd_text_never_throws(string text) => _ = WhatsNew.Parse(text);
}
