// LUNA EVO · LunaEvo.MissionControl.Core/Schema/CommentRules.cs
// [Core] Reads limits like "Max 30 char" or "Min value = 5000" out of LMP's own setting descriptions.
// Used by: LmpSchemaLoader
// Guide:   Meat and Potatoes/02-Core.md

using System.Globalization;
using System.Text.RegularExpressions;

namespace LunaEvo.MissionControl.Core.Schema;

/// <summary>
/// Pulls validation limits out of LMP's [XmlComment] text, e.g. "Max 30 char", "Min value = 5000",
/// "From 0.0% to 100.0%", "Valid values are from 0 to 255".
/// </summary>
public static partial class CommentRules
{
    /// <summary>Finds any limits in a description. Each part is null when the text doesn't mention it.</summary>
    public static (decimal? Min, decimal? Max, int? MaxLength) Parse(string comment)
    {
        decimal? min = null, max = null;
        int? maxLength = null;

        var m = MaxCharsRegex().Match(comment);
        if (m.Success) maxLength = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);

        m = MinValueRegex().Match(comment);
        if (m.Success) min = ParseNumber(m.Groups["n"].Value);

        m = MaxValueRegex().Match(comment);
        if (m.Success) max = ParseNumber(m.Groups["n"].Value);

        m = RangeRegex().Match(comment);
        if (m.Success)
        {
            min = ParseNumber(m.Groups["min"].Value);
            max = ParseNumber(m.Groups["max"].Value);
        }

        return (min, max, maxLength);
    }

    private static decimal ParseNumber(string text) => decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    // "Max 30 char", "Max 30 chars", "255 chars max"
    [GeneratedRegex(@"(?:max\s+(?<n>\d+)\s+chars?\b|(?<n>\d+)\s+chars?\s+max)", RegexOptions.IgnoreCase)]
    private static partial Regex MaxCharsRegex();

    // "Min value = 5000", "Min value is 1"
    [GeneratedRegex(@"min\s+value\s*(?:=|is)\s*(?<n>-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex MinValueRegex();

    [GeneratedRegex(@"max\s+value\s*(?:=|is)\s*(?<n>-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex MaxValueRegex();

    // "From 0.0% to 100.0%", "Valid values are from 0 to 255"
    [GeneratedRegex(@"from\s+(?<min>-?\d+(?:\.\d+)?)%?\s+to\s+(?<max>-?\d+(?:\.\d+)?)%?", RegexOptions.IgnoreCase)]
    private static partial Regex RangeRegex();
}
