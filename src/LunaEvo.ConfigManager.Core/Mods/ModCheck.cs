// LUNA EVO · LunaEvo.ConfigManager.Core/Mods/ModCheck.cs
// [Core] Checks a player's GameData against a server's mod rules (LMPModControl.xml), the way LMP's client does.
// Used by: JoinPage
// Guide:   Meat and Potatoes/12-Mod-Control-From-GameData.md

using LunaEvo.ConfigManager.Core.Files;

namespace LunaEvo.ConfigManager.Core.Mods;

public sealed record RuleDll(string FilePath, string Sha, string Text);

/// <summary>A server's LMPModControl.xml, read for checking.</summary>
public sealed record ModRules(
    bool AllowNonListedPlugins,
    IReadOnlyList<RuleDll> MandatoryPlugins,
    IReadOnlyList<RuleDll> OptionalPlugins,
    IReadOnlyList<RuleDll> ForbiddenPlugins,
    IReadOnlyList<string> MandatoryParts,
    IReadOnlyList<string> AllowedParts,
    IReadOnlyList<string> AllowedResources,
    IReadOnlyList<string> RequiredExpansions)
{
    /// <summary>LMP's rules for even a huge modpack are a few MB; a rules file from a friend can't be bigger than this.</summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    public static ModRules Load(string path)
    {
        if (new FileInfo(path).Length > MaxFileBytes)
            throw new InvalidDataException("That file is far too big to be LMP mod rules.");
        var file = ConfigFile.Load(path);
        if (file.Root.Name.LocalName != "ModControlStructure")
            throw new InvalidDataException("That isn't an LMP mod control file (LMPModControl.xml).");

        IReadOnlyList<RuleDll> Dlls(string name) => (file.GetObjectList(name) ?? [])
            .Select(d => new RuleDll((d.GetValueOrDefault("FilePath") ?? "").ToLowerInvariant(), d.GetValueOrDefault("Sha") ?? "", d.GetValueOrDefault("Text") ?? ""))
            .Where(d => d.FilePath.Length > 0).ToList();

        return new ModRules(
            !string.Equals(file.GetValue("AllowNonListedPlugins")?.Trim(), "false", StringComparison.OrdinalIgnoreCase),
            Dlls("MandatoryPlugins"), Dlls("OptionalPlugins"), Dlls("ForbiddenPlugins"),
            (file.GetObjectList("MandatoryParts") ?? []).Select(p => p.GetValueOrDefault("PartName") ?? "").Where(p => p.Length > 0).ToList(),
            file.GetList("AllowedParts") ?? [],
            file.GetList("AllowedResources") ?? [],
            file.GetList("RequiredExpansions") ?? []);
    }
}

public enum ModProblemKind { MissingExpansion, ForbiddenPlugin, PluginNotAllowed, MissingPlugin, WrongPluginVersion, MissingPart }

public sealed record ModProblem(ModProblemKind Kind, string Item, string Detail);

public sealed record ModCheckResult(IReadOnlyList<ModProblem> Problems, IReadOnlyList<string> PartsNotAllowed, IReadOnlyList<string> ResourcesNotAllowed)
{
    /// <summary>True if LMP would let this install connect.</summary>
    public bool Passes => Problems.Count == 0;
}

/// <summary>
/// Mirrors LMP 0.29.2's client check (LmpClient/Systems/Mod/ModFileHandler.cs, CheckFilesAndExpansions):
/// missing expansions; any DLL that's forbidden; any DLL that's neither mandatory nor optional when non-listed plugins
/// aren't allowed; missing mandatory DLLs, or a different SHA-256 when one is set (optional plugins' hashes are never
/// checked); missing mandatory parts. Parts and resources outside the allowed lists don't stop you joining, but the
/// server drops vessels that use them (Server/System/Vessel/VesselDataUpdater.cs), so they're listed separately.
/// </summary>
public static class ModCheck
{
    public static ModCheckResult Check(ModRules rules, GameDataScan scan)
    {
        var problems = new List<ModProblem>();

        foreach (var expansion in rules.RequiredExpansions.Except(scan.Expansions, StringComparer.Ordinal))
            problems.Add(new ModProblem(ModProblemKind.MissingExpansion, expansion, "The server requires this expansion."));

        var installed = scan.Plugins.ToDictionary(p => p.FilePath, p => p.Sha, StringComparer.Ordinal);
        foreach (var plugin in scan.Plugins)
        {
            if (rules.ForbiddenPlugins.FirstOrDefault(f => f.FilePath == plugin.FilePath) is { } forbidden)
                problems.Add(new ModProblem(ModProblemKind.ForbiddenPlugin, plugin.FilePath, Describe(forbidden, "The server bans this mod. Remove it to join.")));
            else if (!rules.AllowNonListedPlugins && rules.MandatoryPlugins.All(m => m.FilePath != plugin.FilePath) && rules.OptionalPlugins.All(o => o.FilePath != plugin.FilePath))
                problems.Add(new ModProblem(ModProblemKind.PluginNotAllowed, plugin.FilePath, "The server only allows listed mods, and this one isn't listed. Remove it to join."));
        }

        foreach (var mandatory in rules.MandatoryPlugins)
        {
            if (!installed.TryGetValue(mandatory.FilePath, out var sha))
                problems.Add(new ModProblem(ModProblemKind.MissingPlugin, mandatory.FilePath, Describe(mandatory, "The server requires this mod.")));
            else if (mandatory.Sha.Length > 0 && sha.Length > 0 && sha != mandatory.Sha)
                problems.Add(new ModProblem(ModProblemKind.WrongPluginVersion, mandatory.FilePath, Describe(mandatory, "You have a different version of this mod from the server's.")));
        }

        var parts = new HashSet<string>(scan.Parts, StringComparer.Ordinal);
        foreach (var part in rules.MandatoryParts.Where(p => !parts.Contains(p)))
            problems.Add(new ModProblem(ModProblemKind.MissingPart, part, "The server requires this part."));

        var notAllowedParts = rules.AllowedParts.Count == 0 ? [] : scan.Parts.Except(rules.AllowedParts, StringComparer.Ordinal).ToList();
        var notAllowedResources = rules.AllowedResources.Count == 0 ? [] : scan.Resources.Except(rules.AllowedResources, StringComparer.Ordinal).ToList();
        return new ModCheckResult(problems, notAllowedParts, notAllowedResources);
    }

    private static string Describe(RuleDll dll, string why) => dll.Text.Length > 0 ? $"{why} ({dll.Text})" : why;
}
