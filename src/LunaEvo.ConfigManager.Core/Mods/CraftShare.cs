// LUNA EVO · LunaEvo.ConfigManager.Core/Mods/CraftShare.cs
// [Core] Craft designs: find them (KSP saves, LMP's craft library), export to a .lunacraft share file, import with a part check.
// Used by: CraftsPage
// Guide:   Meat and Potatoes/19-Sharing-Crafts.md

using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LunaEvo.ConfigManager.Core.Mods;

/// <param name="Facility">"VAB" or "SPH".</param>
/// <param name="Source">Where it is: "KSP save default" or "Server library (Jebediah)".</param>
public sealed record CraftInfo(string Name, string Facility, string Path, IReadOnlyList<string> Parts, int PartCount, string Source);

/// <summary>What's inside a .lunacraft file, before it's imported anywhere.</summary>
public sealed record SharedCraft(CraftInfo Craft, string CraftText, byte[]? Thumbnail);

/// <summary>
/// A <c>.lunacraft</c> file is a zip with <c>craft.craft</c> (KSP's own file, unchanged), <c>manifest.json</c> (name,
/// facility, parts) and optionally <c>thumbnail.png</c>. Crafts live in KSP at
/// <c>saves/&lt;save&gt;/Ships/VAB|SPH/*.craft</c> and on an LMP server at <c>Universe/Crafts/&lt;player&gt;/VAB|SPH/*.craft</c>
/// (Server/System/CraftLibrarySystem.cs).
/// </summary>
public static partial class CraftShare
{
    public const string Extension = ".lunacraft";
    public const long MaxFileBytes = 20 * 1024 * 1024;
    private static readonly string[] Facilities = ["VAB", "SPH"];

    /// <summary>Reads a .craft file: its name, VAB/SPH and the parts it uses (KSP's "part = name_id" lines).</summary>
    public static CraftInfo Read(string path, string source) => Parse(File.ReadAllText(path), path, source);

    public static CraftInfo Parse(string text, string path, string source)
    {
        // KSP puts ship/type at the top level, outside any node; wrap it so the parser keeps them.
        var root = KspConfigParser.Parse("CRAFT\n{\n" + text + "\n}\n").FirstOrDefault()
                   ?? throw new InvalidDataException("That isn't a KSP craft file.");
        // Stock crafts are named by a translation key ("#autoLOC_501214"); their file name is the readable name.
        var name = root.Get("ship")?.Split("//")[0].Trim() is { Length: > 0 } ship && !ship.StartsWith('#') ? ship : System.IO.Path.GetFileNameWithoutExtension(path);
        var facility = root.Get("type") is { } type && Facilities.Contains(type, StringComparer.OrdinalIgnoreCase) ? type.ToUpperInvariant() : FacilityFromPath(path);
        var parts = root.Nodes.Where(n => n.Name == "PART").Select(n => PartName(n.Get("part") ?? "")).Where(p => p.Length > 0).ToList();
        if (parts.Count == 0 && root.Get("ship") == null)
            throw new InvalidDataException("That isn't a KSP craft file (no ship or parts in it).");
        return new CraftInfo(name, facility, path, parts.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(), parts.Count, source);
    }

    /// <summary>The craft files in every save of a KSP install.</summary>
    public static IReadOnlyList<CraftInfo> InKsp(string kspFolder) =>
        Find(Path.Combine(kspFolder, "saves"), save => Path.Combine(save, "Ships"), save => $"KSP save {Path.GetFileName(save)}");

    /// <summary>The crafts in an LMP server's craft library.</summary>
    public static IReadOnlyList<CraftInfo> InServer(string serverDirectory) =>
        Find(Path.Combine(serverDirectory, "Universe", "Crafts"), player => player, player => $"Server library ({Path.GetFileName(player)})");

    /// <summary>KSP saves that can receive a craft (have a persistent.sfs).</summary>
    public static IReadOnlyList<string> Saves(string kspFolder)
    {
        var saves = Path.Combine(kspFolder, "saves");
        return Directory.Exists(saves)
            ? Directory.GetDirectories(saves).Where(d => File.Exists(Path.Combine(d, "persistent.sfs"))).Select(d => Path.GetFileName(d)!).Order().ToList()
            : [];
    }

    /// <summary>Writes a .lunacraft file. The thumbnail KSP made for it is included when it exists.</summary>
    public static void Export(CraftInfo craft, string target, string? kspFolder = null)
    {
        var text = File.ReadAllText(craft.Path);
        using var stream = File.Create(target);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        Write(zip, "craft.craft", text);
        Write(zip, "manifest.json", JsonSerializer.Serialize(new { format = 1, name = craft.Name, facility = craft.Facility, parts = craft.Parts, partCount = craft.PartCount }));
        if (kspFolder != null && Thumbnail(craft, kspFolder) is { } thumb)
            zip.CreateEntryFromFile(thumb, "thumbnail.png");
    }

    /// <summary>Opens a .lunacraft file: only the expected entries, size-limited, and the craft must parse.</summary>
    public static SharedCraft Open(string path)
    {
        if (new FileInfo(path).Length > MaxFileBytes)
            throw new InvalidDataException("That file is far too big to be a craft.");
        using var zip = ZipFile.OpenRead(path);
        string? text = null;
        byte[]? thumbnail = null;
        foreach (var entry in zip.Entries)
        {
            if (entry.Length > MaxFileBytes)
                throw new InvalidDataException("That file is far too big to be a craft.");
            switch (entry.FullName)
            {
                case "craft.craft":
                    text = System.Text.Encoding.UTF8.GetString(ReadBounded(entry));
                    break;
                case "thumbnail.png":
                    thumbnail = ReadBounded(entry);
                    break;
                case "manifest.json":
                    break; // informational; the craft itself is the truth
                default:
                    throw new InvalidDataException($"That isn't a LUNA EVO craft file (unexpected {entry.FullName}).");
            }
        }
        if (text == null)
            throw new InvalidDataException("That isn't a LUNA EVO craft file (no craft in it).");
        return new SharedCraft(Parse(text, path, "Shared file"), text, thumbnail);
    }

    /// <summary>
    /// Reads an entry, stopping at <see cref="MaxFileBytes"/> however big it really unpacks to: a zip entry's size is
    /// only what the file claims, so a "zip bomb" could otherwise fill memory.
    /// </summary>
    private static byte[] ReadBounded(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + read > MaxFileBytes)
                throw new InvalidDataException("That file unpacks to far more than a craft ever is. Not opening it.");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }

    /// <summary>The craft's parts that this KSP install doesn't have (from a GameData scan).</summary>
    public static IReadOnlyList<string> MissingParts(CraftInfo craft, IEnumerable<string> installedParts)
    {
        var installed = new HashSet<string>(installedParts, StringComparer.Ordinal);
        return craft.Parts.Where(p => !installed.Contains(p)).ToList();
    }

    /// <summary>Puts the craft into a KSP save's Ships folder. Never overwrites: "Name (2).craft" if taken. Returns the path.</summary>
    public static string ImportToKsp(SharedCraft shared, string kspFolder, string save) =>
        WriteCraft(shared, Path.Combine(kspFolder, "saves", SafeName(save), "Ships", shared.Craft.Facility), kspFolder, save);

    /// <summary>Puts the craft into the server's craft library under a "Shared" folder. Stop the server first (LMP lists crafts as it runs).</summary>
    public static string ImportToServer(SharedCraft shared, string serverDirectory) =>
        WriteCraft(shared, Path.Combine(serverDirectory, "Universe", "Crafts", "Shared", shared.Craft.Facility), null, null);

    /// <summary>A craft name that's safe as a file name.</summary>
    public static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim('.', ' ');
        safe = safe.Length == 0 ? "Craft" : safe.Length > 80 ? safe[..80].TrimEnd('.', ' ') : safe;
        // A friend's craft called "CON" or "NUL" would name a Windows device, not a file.
        return Ops.ProfileStore.IsDeviceName(safe) ? safe + "-craft" : safe;
    }

    private static string WriteCraft(SharedCraft shared, string folder, string? kspFolder, string? save)
    {
        Directory.CreateDirectory(folder);
        var name = SafeName(shared.Craft.Name);
        var path = Path.Combine(folder, name + ".craft");
        for (var i = 2; File.Exists(path); i++)
            path = Path.Combine(folder, $"{name} ({i}).craft");
        File.WriteAllText(path, shared.CraftText);
        if (shared.Thumbnail != null && kspFolder != null && save != null)
        {
            var thumbs = Directory.CreateDirectory(Path.Combine(kspFolder, "thumbs")).FullName;
            File.WriteAllBytes(Path.Combine(thumbs, $"{SafeName(save)}_{shared.Craft.Facility}_{Path.GetFileNameWithoutExtension(path)}.png"), shared.Thumbnail);
        }
        return path;
    }

    private static IReadOnlyList<CraftInfo> Find(string root, Func<string, string> shipsFolder, Func<string, string> source)
    {
        var crafts = new List<CraftInfo>();
        if (!Directory.Exists(root))
            return crafts;
        foreach (var owner in Directory.GetDirectories(root))
            foreach (var facility in Facilities)
            {
                var folder = Path.Combine(shipsFolder(owner), facility);
                if (!Directory.Exists(folder))
                    continue;
                foreach (var file in Directory.GetFiles(folder, "*.craft").Order(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        crafts.Add(Read(file, source(owner)));
                    }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                    {
                        // An unreadable or broken craft is skipped; the rest still show.
                    }
                }
            }
        return crafts;
    }

    // KSP's thumbnails: thumbs/<save>_<VAB|SPH>_<craft file name>.png
    private static string? Thumbnail(CraftInfo craft, string kspFolder)
    {
        var save = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(craft.Path))));
        var thumb = Path.Combine(kspFolder, "thumbs", $"{save}_{craft.Facility}_{Path.GetFileNameWithoutExtension(craft.Path)}.png");
        return File.Exists(thumb) ? thumb : null;
    }

    private static string FacilityFromPath(string path) =>
        Path.GetFileName(Path.GetDirectoryName(path))?.ToUpperInvariant() is "SPH" ? "SPH" : "VAB";

    // "mk1pod.v2_4294967296" → "mk1pod.v2"
    private static string PartName(string value) => PartId().Replace(value.Trim(), "");

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(text);
    }

    [GeneratedRegex(@"_\d+$")]
    private static partial Regex PartId();
}
