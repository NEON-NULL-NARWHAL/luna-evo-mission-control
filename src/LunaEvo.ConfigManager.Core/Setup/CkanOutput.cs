// LUNA EVO · LunaEvo.ConfigManager.Core/Setup/CkanOutput.cs
// [Core] Reads ckan.exe's console output: what a dry run would install, and CKAN's errors in plain words.
// Used by: CkanPage
// Guide:   Meat and Potatoes/22-CKAN-and-LMP-Client.md

using System.Text.RegularExpressions;

namespace LunaEvo.ConfigManager.Core.Setup;

/// <summary>The outcome of a dry run ("Check with CKAN"): whether CKAN can install the list, and what it would add.</summary>
public sealed record CkanCheck(bool Ok, IReadOnlyList<string> WouldInstall, string Message);

public static partial class CkanOutput
{
    /// <summary>A dry run's output (CKAN asked "Continue?" and was told no).</summary>
    public static CkanCheck Check(IReadOnlyList<string> lines, int exitCode)
    {
        var declined = lines.Any(l => l.Contains("User declined", StringComparison.OrdinalIgnoreCase));
        var listed = lines.SkipWhile(l => !l.StartsWith("About to install", StringComparison.OrdinalIgnoreCase)).Skip(1)
            .Select(l => l.Trim()).Where(l => l.StartsWith("* ")).Select(l => l[2..]).ToList();
        if (declined || exitCode == 0)
            return new CkanCheck(true, listed, listed.Count > 0
                ? $"CKAN can install this list: {listed.Count} mod(s) in its plan (dependencies included)."
                : "CKAN can install this list. Everything in it is already installed.");
        return new CkanCheck(false, [], Problem(lines, exitCode));
    }

    /// <summary>CKAN's error in plain words, from its output (it prints a .NET stack trace when it gives up).</summary>
    public static string Problem(IReadOnlyList<string> lines, int exitCode)
    {
        foreach (var line in lines)
        {
            if (NotFound().Match(line) is { Success: true } m)
                return $"CKAN doesn't know the mod \"{m.Groups[1].Value.Trim()}\" (or not that version). Check the spelling, or turn off Exact versions.";
            if (line.Contains("not compatible", StringComparison.OrdinalIgnoreCase) || line.Contains("incompatible", StringComparison.OrdinalIgnoreCase))
                return "CKAN says a mod doesn't fit this KSP version: " + Clean(line);
            if (line.Contains("conflict", StringComparison.OrdinalIgnoreCase))
                return "CKAN found mods that conflict with each other: " + Clean(line);
            if (line.Contains("lock", StringComparison.OrdinalIgnoreCase) && line.Contains("registry", StringComparison.OrdinalIgnoreCase))
                return "CKAN has this install open somewhere else. Close CKAN's window and try again.";
        }
        var last = lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("at ") && !l.Contains(" left - ") && !l.StartsWith("---"))
            .TakeLast(2).Select(Clean).ToList();
        return $"CKAN stopped with an error (code {exitCode})" + (last.Count > 0 ? ": " + string.Join(" ", last) : ".");
    }

    // "Unhandled Exception: CKAN.ModuleNotFoundKraken: Module not found: NoSuchMod 1.2"
    [GeneratedRegex(@"Module not found:\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NotFound();

    private static string Clean(string line) =>
        (Regex.Replace(line, @"^(Unhandled Exception:\s*)?(CKAN\.)?\w+Kraken:\s*", "") is var s && s.Length > 300 ? s[..300] + "…" : s).Trim();
}
