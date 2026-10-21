// LUNA EVO · LunaEvo.MissionControl.Core.Tests/CodeHygieneTests.cs
// [Test] Checks on the source itself: no doc comment left above another one (a sign code was inserted in between).
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

namespace LunaEvo.MissionControl.Core.Tests;

public class CodeHygieneTests
{
    [Fact]
    public void No_doc_comment_is_separated_from_its_member()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var orphans = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i + 1 < lines.Length; i++)
                if (lines[i].TrimStart().StartsWith("///") && lines[i].TrimEnd().EndsWith("</summary>") && lines[i + 1].TrimStart().StartsWith("/// <summary>"))
                    orphans.Add($"{Path.GetRelativePath(src, file)}:{i + 1}");
        }
        Assert.Empty(orphans);
    }
}
