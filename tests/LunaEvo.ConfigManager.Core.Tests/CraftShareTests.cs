// LUNA EVO · LunaEvo.ConfigManager.Core.Tests/CraftShareTests.cs
// [Test] Sharing crafts: reading KSP .craft files, export/import of .lunacraft, the part check, refusing bad files.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.IO.Compression;
using LunaEvo.ConfigManager.Core.Mods;

namespace LunaEvo.ConfigManager.Core.Tests;

public class CraftShareTests
{
    private static string Temp(string name) =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "LunaEvoTests", name + "-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public void Reads_a_craft_like_ksp_does()
    {
        var ksp = TestData.KspWithCraft();
        var craft = Assert.Single(CraftShare.InKsp(ksp));
        Assert.Equal(("Kerbal X", "VAB", 4, "KSP save career"), (craft.Name, craft.Facility, craft.PartCount, craft.Source));
        Assert.Equal(["MyMod.Wheel", "Size3To2Adapter.v2", "mk1pod.v2"], craft.Parts); // ids stripped, distinct
        Assert.Equal(["career"], CraftShare.Saves(ksp));
    }

    [Fact]
    public void Exports_and_imports_into_another_save_and_a_server_without_overwriting()
    {
        var ksp = TestData.KspWithCraft();
        var file = Path.Combine(Temp("export"), "Kerbal X" + CraftShare.Extension);
        CraftShare.Export(CraftShare.InKsp(ksp)[0], file, ksp);

        var shared = CraftShare.Open(file);
        Assert.Equal("Kerbal X", shared.Craft.Name);
        Assert.NotNull(shared.Thumbnail);

        var friend = TestData.KspWithCraft(); // already has a "Kerbal X"
        var path = CraftShare.ImportToKsp(shared, friend, "career");
        Assert.EndsWith(Path.Combine("Ships", "VAB", "Kerbal X (2).craft"), path);
        Assert.Equal(TestData.KerbalX, File.ReadAllText(path)); // KSP's file, unchanged
        Assert.True(File.Exists(Path.Combine(friend, "thumbs", "career_VAB_Kerbal X (2).png")));

        var server = Temp("server");
        var onServer = CraftShare.ImportToServer(shared, server);
        Assert.Equal(Path.Combine(server, "Universe", "Crafts", "Shared", "VAB", "Kerbal X.craft"), onServer);
        Assert.Equal("Server library (Shared)", Assert.Single(CraftShare.InServer(server)).Source);
    }

    [Fact]
    public void Stock_crafts_named_by_translation_key_use_their_file_name() =>
        Assert.Equal("AeroEquus", CraftShare.Parse(string.Join("\n", "ship = #autoLOC_501214 //#autoLOC_501214 = AeroEquus", "type = VAB", "PART", "{", " part = mk1pod.v2_1", "}"),
            Path.Combine("Ships", "VAB", "AeroEquus.craft"), "test").Name);

    [Fact]
    public void Lists_the_parts_this_install_doesnt_have()
    {
        var craft = CraftShare.Parse(TestData.KerbalX, "Kerbal X.craft", "test");
        Assert.Equal(["MyMod.Wheel"], CraftShare.MissingParts(craft, ["mk1pod.v2", "Size3To2Adapter.v2"]));
    }

    [Fact]
    public void Refuses_files_that_arent_craft_shares()
    {
        var dir = Temp("bad");
        string Zip(string name, params (string Entry, string Text)[] entries)
        {
            var path = Path.Combine(dir, name);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var (entry, text) in entries)
                using (var writer = new StreamWriter(zip.CreateEntry(entry).Open()))
                    writer.Write(text);
            return path;
        }

        Assert.Throws<InvalidDataException>(() => CraftShare.Open(Zip("extra.lunacraft", ("craft.craft", TestData.KerbalX), ("../evil.dll", "x"))));
        Assert.Throws<InvalidDataException>(() => CraftShare.Open(Zip("empty.lunacraft", ("manifest.json", "{}"))));
        Assert.Throws<InvalidDataException>(() => CraftShare.Open(Zip("notcraft.lunacraft", ("craft.craft", "hello there"))));
        var notZip = Path.Combine(dir, "plain.lunacraft");
        File.WriteAllText(notZip, "not a zip");
        Assert.ThrowsAny<InvalidDataException>(() => CraftShare.Open(notZip));
        Assert.Equal("Bad-Name", CraftShare.SafeName("Bad/Name"));

        // A zip bomb: tiny file, but the craft unpacks to more than the limit.
        var bomb = Path.Combine(dir, "bomb.lunacraft");
        using (var zip = ZipFile.Open(bomb, ZipArchiveMode.Create))
        using (var entry = zip.CreateEntry("craft.craft", CompressionLevel.SmallestSize).Open())
        {
            var zeros = new byte[1024 * 1024];
            for (var i = 0; i <= CraftShare.MaxFileBytes / zeros.Length; i++)
                entry.Write(zeros);
        }
        Assert.True(new FileInfo(bomb).Length < 1024 * 1024);
        Assert.Throws<InvalidDataException>(() => CraftShare.Open(bomb));
    }
}
