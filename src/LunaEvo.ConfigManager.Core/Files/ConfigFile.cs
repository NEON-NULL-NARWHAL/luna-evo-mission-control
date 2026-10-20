// LUNA EVO · LunaEvo.ConfigManager.Core/Files/ConfigFile.cs
// [Core] One LMP XML file, edited in place so comments, order, line endings and encoding survive byte-for-byte.
// Used by: ConfigSession
// Guide:   Meat and Potatoes/02-Core.md

using System.Text;
using System.Xml.Linq;

namespace LunaEvo.ConfigManager.Core.Files;

/// <summary>
/// One LMP config file, edited in place so comments, ordering, line endings and the original
/// declaration survive. An unchanged file renders back byte-for-byte.
/// </summary>
/// <remarks>
/// LMP 0.29.x declares encoding="utf-16" but writes UTF-8 without a BOM, which makes
/// XDocument.Load(path) throw. So we decode the bytes ourselves and parse the text.
/// </remarks>
public sealed class ConfigFile
{
    private const string ListItemElement = "string";

    private readonly Encoding _encoding;
    private readonly string _prefix;
    private readonly string _suffix;
    private readonly bool _crlf;

    private ConfigFile(string path, Encoding encoding, string text)
    {
        Path = path;
        _encoding = encoding;
        _crlf = text.Contains("\r\n");

        var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        Root = document.Root ?? throw new InvalidDataException($"{path} has no root element.");

        // Keep the declaration and anything around the root exactly as written.
        var rootStart = text.IndexOf("<" + Root.Name.LocalName, StringComparison.Ordinal);
        var rootEnd = text.LastIndexOf('>') + 1;
        _prefix = text[..rootStart];
        _suffix = text[rootEnd..];
    }

    public string Path { get; }
    public XElement Root { get; }

    /// <summary>Reads the file, detecting the real encoding from its bytes (not the declaration, which lies in LMP 0.29).</summary>
    public static ConfigFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var encoding = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, encoding.Preamble.Length, bytes.Length - encoding.Preamble.Length);
        return new ConfigFile(path, encoding, text);
    }

    /// <summary>True if the file contains this setting's element.</summary>
    public bool Has(string name) => Root.Element(name) != null;

    /// <summary>The element's text, or null if the setting isn't in the file.</summary>
    public string? GetValue(string name) => Root.Element(name)?.Value;

    /// <summary>The &lt;string&gt; children of a list element, or null if it isn't in the file.</summary>
    public IReadOnlyList<string>? GetList(string name) =>
        Root.Element(name)?.Elements(ListItemElement).Select(e => e.Value).ToList();

    /// <summary>Sets a setting's text. Adds the element (with its comment) at the end if the file doesn't have it yet.</summary>
    public void SetValue(string name, string value, string? comment = null)
    {
        // TextBoxes give us \r\n on Windows; XML stores \n and we restore the file's line endings on render.
        value = value.Replace("\r\n", "\n");
        var element = Root.Element(name) ?? AddElement(name, comment);
        element.Value = value;
    }

    /// <summary>Replaces a list's &lt;string&gt; children, indented the way LMP writes them.</summary>
    public void SetList(string name, IEnumerable<string> items, string? comment = null)
    {
        var element = Root.Element(name) ?? AddElement(name, comment);
        var indent = IndentOf(element);
        element.RemoveNodes();

        var list = items.ToList();
        if (list.Count == 0)
        {
            element.Value = ""; // LMP writes empty lists as <X></X>
            return;
        }

        foreach (var item in list)
            element.Add(new XText("\n" + indent + "  "), new XElement(ListItemElement, item));
        element.Add(new XText("\n" + indent));
    }

    /// <summary>The whole file as text, exactly as it will be saved.</summary>
    /// <summary>The items of an object list (e.g. MandatoryPlugins), each as field name → value. Null if the element is missing.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>>? GetObjectList(string name) =>
        Root.Element(name)?.Elements()
            .Select(item => (IReadOnlyDictionary<string, string>)item.Elements()
                .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value))
            .ToList();

    /// <summary>Replaces an object list, writing each item as &lt;ItemElement&gt; with one child per field, LMP style.</summary>
    public void SetObjectList(string name, string itemElement, IReadOnlyList<string> fields,
        IEnumerable<IReadOnlyDictionary<string, string>> items, string? comment = null)
    {
        var element = Root.Element(name) ?? AddElement(name, comment);
        var indent = IndentOf(element);
        element.RemoveNodes();

        var rows = items.ToList();
        if (rows.Count == 0)
        {
            element.Value = "";
            return;
        }

        foreach (var row in rows)
        {
            var item = new XElement(itemElement);
            foreach (var field in fields)
                item.Add(new XText("\n" + indent + "    "), new XElement(field, row.TryGetValue(field, out var value) ? value : ""));
            item.Add(new XText("\n" + indent + "  "));
            element.Add(new XText("\n" + indent + "  "), item);
        }
        element.Add(new XText("\n" + indent));
    }

    /// <summary>
    /// A new, empty LMP-style file (UTF-8, CRLF, XmlSerializer's namespaces), ready for SetValue/SetList to fill.
    /// Used to create a server's first config without starting it with LMP's defaults.
    /// </summary>
    public static ConfigFile CreateNew(string path, string rootElement)
    {
        var text = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                   $"<{rootElement} xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r\n" +
                   $"</{rootElement}>";
        return new ConfigFile(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), text);
    }

    public string Render()
    {
        // XmlWriter turns \n into the platform newline, so normalise before applying the file's own.
        var body = Root.ToString(SaveOptions.DisableFormatting).Replace("\r\n", "\n");
        if (_crlf)
            body = body.Replace("\n", "\r\n");
        return _prefix + body + _suffix;
    }

    /// <summary>Writes back to where it was loaded from.</summary>
    public void Save() => SaveAs(Path);

    /// <summary>Writes the file with its original encoding and BOM (or lack of one).</summary>
    public void SaveAs(string path)
    {
        var text = Render();
        var preamble = _encoding.GetPreamble();
        var bytes = _encoding.GetBytes(text);
        using var stream = File.Create(path);
        stream.Write(preamble);
        stream.Write(bytes);
    }

    private XElement AddElement(string name, string? comment)
    {
        var element = new XElement(name);
        var nodes = new List<XNode>();
        if (!string.IsNullOrEmpty(comment))
            nodes.AddRange([new XText("\n  "), new XComment(comment.Replace("--", "- -"))]);
        nodes.AddRange([new XText("\n  "), element]);

        // Insert before the whitespace that precedes </Root>, so the closing tag stays on its own line.
        if (Root.LastNode is XText trailing && string.IsNullOrWhiteSpace(trailing.Value))
            trailing.AddBeforeSelf(nodes);
        else
            Root.Add(nodes, new XText("\n"));
        return element;
    }

    private static string IndentOf(XElement element)
    {
        if (element.PreviousNode is XText text)
        {
            var value = text.Value;
            var lineStart = value.LastIndexOf('\n');
            return lineStart >= 0 ? value[(lineStart + 1)..] : value;
        }
        return "  ";
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes is [0xEF, 0xBB, 0xBF, ..]) return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        if (bytes is [0xFF, 0xFE, ..]) return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        if (bytes is [0xFE, 0xFF, ..]) return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        // Genuine UTF-16 without a BOM: "<" followed by a zero byte.
        if (bytes is [(byte)'<', 0, ..]) return new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
        if (bytes is [0, (byte)'<', ..]) return new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
