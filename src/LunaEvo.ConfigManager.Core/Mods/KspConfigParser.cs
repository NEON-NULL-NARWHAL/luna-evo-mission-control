// LUNA EVO · LunaEvo.ConfigManager.Core/Mods/KspConfigParser.cs
// [Core] A small reader for KSP's .cfg "ConfigNode" files (PART { name = mk1pod ... }).
// Used by: GameDataScanner
// Guide:   Meat and Potatoes/12-Mod-Control-From-GameData.md

using System.Text;

namespace LunaEvo.ConfigManager.Core.Mods;

/// <summary>A KSP config node: a name, key = value pairs, and child nodes.</summary>
public sealed class KspNode(string name)
{
    public string Name { get; } = name;
    public List<KeyValuePair<string, string>> Values { get; } = [];
    public List<KspNode> Nodes { get; } = [];

    /// <summary>The first value for a key, or null.</summary>
    public string? Get(string key) => Values.FirstOrDefault(v => v.Key == key).Value;
}

/// <summary>
/// Parses the format KSP uses for parts, resources and mod settings:
/// <code>
/// PART            // a node name, then braces (on the same line or the next)
/// {
///     name = mk1pod
///     MODULE { name = ModuleCommand }
/// }
/// </code>
/// Comments start with //. Lenient on purpose: mods write these by hand, so malformed bits are skipped, not fatal.
/// </summary>
public static class KspConfigParser
{
    public static IReadOnlyList<KspNode> Parse(string text)
    {
        var root = new KspNode("root");
        var stack = new Stack<KspNode>();
        stack.Push(root);
        var pending = new StringBuilder(); // text since the last separator: a key = value, or a node name

        void FlushValue()
        {
            var line = pending.ToString().Trim();
            pending.Clear();
            var eq = line.IndexOf('=');
            if (eq > 0)
                stack.Peek().Values.Add(new(line[..eq].Trim(), line[(eq + 1)..].Trim()));
            else if (line.Length > 0)
                pending.Append(line); // probably a node name whose "{" is on the next line
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0)
                line = line[..comment];

            foreach (var c in line)
            {
                switch (c)
                {
                    case '{':
                    {
                        var name = pending.ToString().Trim();
                        pending.Clear();
                        // "key = value NODE {" can't happen in valid files, but keep only the last word as the name.
                        var eq = name.LastIndexOf('=');
                        if (eq >= 0)
                        {
                            stack.Peek().Values.Add(new(name[..eq].Trim(), ""));
                            name = "";
                        }
                        // Only the last word is the name; anything before it is stray text.
                        name = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
                        var node = new KspNode(name);
                        stack.Peek().Nodes.Add(node);
                        stack.Push(node);
                        break;
                    }
                    case '}':
                        FlushValue();
                        pending.Clear();
                        if (stack.Count > 1)
                            stack.Pop();
                        break;
                    default:
                        pending.Append(c);
                        break;
                }
            }
            // End of line ends a "key = value".
            if (pending.ToString().Contains('='))
                FlushValue();
            else if (pending.Length > 0)
                pending.Append(' ');
        }
        return root.Nodes;
    }
}
