// LUNA EVO · LunaEvo.MissionControl.Core.Tests/DiskSpaceTests.cs
// [Test] The nearly-full-drive warning for the server's drive (Core/Ops/DiskSpace.cs).
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using LunaEvo.MissionControl.Core.Ops;

namespace LunaEvo.MissionControl.Core.Tests;

public class DiskSpaceTests
{
    [Fact]
    public void Warns_only_below_two_gigabytes()
    {
        var before = DiskSpace.FreeBytes;
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "server");
            DiskSpace.FreeBytes = _ => DiskSpace.LowBytes;
            Assert.Null(DiskSpace.Warning(folder));
            DiskSpace.FreeBytes = _ => DiskSpace.LowBytes - 1;
            Assert.StartsWith("Only 2.0 GB free on ", DiskSpace.Warning(folder));
            DiskSpace.FreeBytes = _ => null; // can't tell: no warning
            Assert.Null(DiskSpace.Warning(folder));
        }
        finally
        {
            DiskSpace.FreeBytes = before;
        }
    }

    [Fact]
    public void Reads_the_real_drive_of_a_folder() => Assert.True(DiskSpace.FreeBytes(Path.GetTempPath()) > 0);
}
