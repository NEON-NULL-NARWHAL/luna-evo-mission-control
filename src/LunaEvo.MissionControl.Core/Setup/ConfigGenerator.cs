// LUNA EVO · LunaEvo.MissionControl.Core/Setup/ConfigGenerator.cs
// [Core] Writes a brand-new server's config files from the schema, with the wizard's choices applied.
// Used by: SetupWizard
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

using LunaEvo.MissionControl.Core.Files;
using LunaEvo.MissionControl.Core.Schema;

namespace LunaEvo.MissionControl.Core.Setup;

/// <summary>
/// Why not just start the server once and let LMP write them? Because LMP's defaults register with the public
/// server list and ask your router to open ports (UPnP) before you've had a say. Writing the files ourselves means
/// the very first start already uses your choices. LMP rewrites them in its own format on startup, so the exact
/// layout here doesn't matter as long as XmlSerializer can read it.
/// </summary>
public static class ConfigGenerator
{
    /// <summary>
    /// Creates each config file that doesn't exist yet. Values come from <paramref name="choices"/> (keyed by file and
    /// setting), then the GameplaySettings preset for the chosen GameDifficulty, then LMP's defaults.
    /// </summary>
    /// <returns>The files created.</returns>
    public static IReadOnlyList<string> CreateMissing(LmpSchema schema, string configDirectory,
        IReadOnlyDictionary<(string File, string Setting), string> choices)
    {
        Directory.CreateDirectory(configDirectory);
        choices.TryGetValue(("GeneralSettings.xml", "GameDifficulty"), out var difficulty);

        var created = new List<string>();
        foreach (var fileSchema in schema.Files)
        {
            var path = Path.Combine(configDirectory, fileSchema.FileName);
            if (File.Exists(path))
                continue;

            var file = ConfigFile.CreateNew(path, fileSchema.RootElement);
            var preset = fileSchema.FileName == "GameplaySettings.xml" ? difficulty : null;
            foreach (var setting in fileSchema.Settings)
            {
                var value = choices.TryGetValue((fileSchema.FileName, setting.Name), out var chosen)
                    ? chosen
                    : fileSchema.DefaultFor(setting, preset);
                switch (setting.Kind)
                {
                    case SettingKind.StringList:
                        file.SetList(setting.Name, value.Split('\n', StringSplitOptions.RemoveEmptyEntries), setting.Description);
                        break;
                    case SettingKind.ObjectList:
                        file.SetObjectList(setting.Name, setting.ItemElement!, setting.ItemFields, [], setting.Description);
                        break;
                    case SettingKind.Unsupported:
                        file.SetValue(setting.Name, "", setting.Description);
                        break;
                    default:
                        file.SetValue(setting.Name, value, setting.Description);
                        break;
                }
            }
            file.Save();
            created.Add(path);
        }
        return created;
    }
}
