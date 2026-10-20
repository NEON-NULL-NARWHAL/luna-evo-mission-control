// LUNA EVO · LunaEvo.ConfigManager.Core/Schema/LmpSchemaLoader.cs
// [Core] Builds the list of files and settings by reflecting over the server's own Server.dll, so new LMP settings appear automatically.
// Used by: ConfigSession.Open
// Guide:   Meat and Potatoes/02-Core.md

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml;

namespace LunaEvo.ConfigManager.Core.Schema;

/// <summary>
/// Builds the schema by reflecting over the settings classes inside the server's own Server.dll,
/// so the GUI always matches the server version it is pointed at.
/// </summary>
public static class LmpSchemaLoader
{
    private const string SettingsNamespace = "Server.Settings.Definition";
    private const string ModControlType = "LmpCommon.ModFile.Structure.ModControlStructure";
    private const string ModControlFileName = "LMPModControl.xml";

    /// <summary>Reads every settings class from serverDirectory/Server.dll. Throws if it's missing or has no LMP settings.</summary>
    public static LmpSchema Load(string serverDirectory)
    {
        var serverDll = Path.Combine(serverDirectory, "Server.dll");
        if (!File.Exists(serverDll))
            throw new FileNotFoundException($"No Server.dll found in {serverDirectory}. Point this at an LMP server folder.", serverDll);

        // Collectible and loaded from bytes, so we don't lock the server's DLLs while the app is open.
        var context = new AssemblyLoadContext("LmpServer", isCollectible: true);
        context.Resolving += (ctx, name) =>
        {
            var path = Path.Combine(serverDirectory, name.Name + ".dll");
            return File.Exists(path) ? LoadFromBytes(ctx, path) : null;
        };

        try
        {
            var assembly = LoadFromBytes(context, serverDll);
            var version = FileVersionOf(serverDll) ?? assembly.GetName().Version?.ToString() ?? "unknown";

            var files = new List<SettingsFileSchema>();
            foreach (var type in GetLoadableTypes(assembly).OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                if (type.Namespace == SettingsNamespace && type.Name.EndsWith("SettingsDefinition", StringComparison.Ordinal))
                    files.Add(BuildFile(type, type.Name[..^"Definition".Length] + ".xml"));
                else if (type.FullName == ModControlType)
                    files.Add(BuildFile(type, ModControlFileName));
            }

            if (files.Count == 0)
                throw new InvalidOperationException($"Server.dll in {serverDirectory} has no LMP settings classes.");

            return new LmpSchema(version, files);
        }
        finally
        {
            context.Unload();
        }
    }

    private static Assembly LoadFromBytes(AssemblyLoadContext context, string path)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        return context.LoadFromStream(stream);
    }

    private static string? FileVersionOf(string path)
    {
        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
        return string.IsNullOrWhiteSpace(info.ProductVersion) ? null : info.ProductVersion;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null)!;
        }
    }

    private static SettingsFileSchema BuildFile(Type type, string fileName)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && !HasAttribute(p, "XmlIgnoreAttribute"))
            .OrderBy(p => p.MetadataToken)
            .ToList();

        // LMP creates new files from a fresh instance with every SetDefault*() applied (e.g. ModControl's part list).
        var defaults = Activator.CreateInstance(type)!;
        foreach (var method in ParameterlessMethods(type).Where(m => m.Name.StartsWith("SetDefault", StringComparison.Ordinal)))
            method.Invoke(defaults, null);

        var settings = properties.Select(p => BuildSetting(p, defaults)).ToList();

        // Other Set*() methods are presets, e.g. GameplaySettings SetEasy/SetNormal/SetModerate/SetHard.
        var presets = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var method in ParameterlessMethods(type).Where(m => m.Name.StartsWith("Set", StringComparison.Ordinal)
                                                                   && !m.Name.StartsWith("SetDefault", StringComparison.Ordinal)))
        {
            var instance = Activator.CreateInstance(type)!;
            method.Invoke(instance, null);
            presets[method.Name[3..]] = properties.ToDictionary(p => p.Name, p => FormatValue(p.GetValue(instance)));
        }

        return new SettingsFileSchema(fileName, type.Name, settings, presets);
    }

    private static IEnumerable<MethodInfo> ParameterlessMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(void) && !m.IsSpecialName);

    private static SettingDefinition BuildSetting(PropertyInfo property, object defaults)
    {
        var type = property.PropertyType;
        var description = XmlCommentOf(property);
        var defaultValue = FormatValue(property.GetValue(defaults));
        var (min, max, maxLength) = CommentRules.Parse(description);

        var kind = KindOf(type, property.Name, defaultValue);
        if (kind == SettingKind.Integer)
        {
            // Never allow beyond what the property type can hold (e.g. byte colours are 0-255).
            var (typeMin, typeMax) = IntegerRange(type);
            min = min is { } a ? Math.Max(a, typeMin) : typeMin;
            max = max is { } b ? Math.Min(b, typeMax) : typeMax;
        }

        var enumValues = type.IsEnum ? Enum.GetNames(type) : Array.Empty<string>();
        var definition = new SettingDefinition(property.Name, kind, TypeName(type), description, defaultValue, enumValues, min, max, maxLength);
        if (kind == SettingKind.ObjectList)
        {
            // XmlSerializer writes List<DllFile> as <DllFile> elements whose children are DllFile's properties.
            var item = type.GetGenericArguments()[0];
            definition = definition with
            {
                ItemElement = item.Name,
                ItemFields = item.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite).OrderBy(p => p.MetadataToken).Select(p => p.Name).ToList(),
                DefaultValue = "",
            };
        }
        return definition;
    }

    private static string TypeName(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>"
            : type.Name;

    private static SettingKind KindOf(Type type, string name, string defaultValue)
    {
        if (type == typeof(bool)) return SettingKind.Bool;
        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long)) return SettingKind.Integer;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return SettingKind.Decimal;
        if (type.IsEnum) return SettingKind.Enum;
        if (type == typeof(string))
            return defaultValue.Contains('\n') || name.Contains("Motd", StringComparison.OrdinalIgnoreCase)
                ? SettingKind.MultilineText
                : SettingKind.Text;
        if (type == typeof(List<string>)) return SettingKind.StringList;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) && IsSimpleRecord(type.GetGenericArguments()[0]))
            return SettingKind.ObjectList;
        return SettingKind.Unsupported;
    }

    /// <summary>A class with a parameterless constructor and only string properties, like LMP's DllFile.</summary>
    private static bool IsSimpleRecord(Type type) =>
        type.IsClass && type.GetConstructor(Type.EmptyTypes) != null &&
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).All(p => p.PropertyType == typeof(string));

    private static (decimal Min, decimal Max) IntegerRange(Type type) => type switch
    {
        _ when type == typeof(byte) => (byte.MinValue, byte.MaxValue),
        _ when type == typeof(sbyte) => (sbyte.MinValue, sbyte.MaxValue),
        _ when type == typeof(short) => (short.MinValue, short.MaxValue),
        _ when type == typeof(ushort) => (ushort.MinValue, ushort.MaxValue),
        _ when type == typeof(uint) => (uint.MinValue, uint.MaxValue),
        _ when type == typeof(long) => (long.MinValue, long.MaxValue),
        _ => (int.MinValue, int.MaxValue),
    };

    private static string XmlCommentOf(PropertyInfo property)
    {
        // Matched by name: the attribute type lives in LMP's assembly, not ours.
        var attribute = property.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "XmlCommentAttribute");
        return attribute?.GetType().GetProperty("Value")?.GetValue(attribute) as string ?? "";
    }

    private static bool HasAttribute(PropertyInfo property, string attributeName) =>
        property.GetCustomAttributes().Any(a => a.GetType().Name == attributeName);

    /// <summary>Formats a value the way XmlSerializer writes it to the file.</summary>
    public static string FormatValue(object? value) => value switch
    {
        null => "",
        string s => s,
        bool b => XmlConvert.ToString(b),
        float f => XmlConvert.ToString(f),
        double d => XmlConvert.ToString(d),
        decimal m => XmlConvert.ToString(m),
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable items => string.Join("\n", items.Cast<object?>().Select(FormatValue)),
        _ => value.ToString() ?? "",
    };
}
