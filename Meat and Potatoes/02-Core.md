# 02 · Core: settings, files, validation

[← Meat and Potatoes](README.md) · Code: `src/LunaEvo.ConfigManager.Core/` 🧠

## `Schema/`: what settings exist 🔌

### `LmpSchemaLoader.Load(serverDirectory)` → `LmpSchema`

| Step | What happens |
|---|---|
| Load | `Server.dll` (and any dependency it asks for) is read **as bytes** into a collectible `AssemblyLoadContext`, which is unloaded afterwards. No file locks, no leftovers. |
| Find classes | Types in `Server.Settings.Definition` ending in `SettingsDefinition` → file `XxxSettings.xml`. Plus `LmpCommon.ModFile.Structure.ModControlStructure` → `LMPModControl.xml`. |
| Defaults | A fresh instance, with every `SetDefault*()` method run (that's how LMP fills a new mod-control file). |
| Presets | Every other parameterless `Set*()` method, e.g. Gameplay's `SetEasy/SetNormal/SetModerate/SetHard`, recorded as preset name → values. |
| Descriptions | LMP's `[XmlComment(Value=...)]`, matched **by name** because the attribute type lives in LMP's assembly, not ours. |
| Limits | `CommentRules.Parse(description)`, combined with the property type's own range (a `byte` is 0 to 255). |
| Kind | `bool` → Bool; integer types → Integer; `float/double/decimal` → Decimal; enums → Enum (names kept); `string` → Text (MultilineText for the MOTD); `List<string>` → StringList; anything else → Unsupported (shown read-only). |

Values are formatted the way `XmlSerializer` writes them (`FormatValue`): `true`/`false`, invariant numbers, enum names.

### `SettingsSchema.cs`

Plain records with no behavior beyond lookups:
`SettingDefinition(Name, Kind, ClrTypeName, Description, DefaultValue, EnumValues, Min, Max, MaxLength)` and
`SettingsFileSchema(FileName, RootElement, Settings, Presets)` with `DefaultFor(setting, presetName)`.

## `Files/`: reading and writing XML 🔌⚠️

### `ConfigFile`

| Member | Notes |
|---|---|
| `Load(path)` | Detects the encoding **from the bytes** (BOM, or a zero byte for UTF-16), not from the declaration. LMP 0.29 writes `encoding="utf-16"` on UTF-8 files, and `XDocument.Load` throws on that. |
| `GetValue` / `GetList` | Element text, or the `<string>` children of a list. `null` if the element isn't in the file. |
| `SetValue(name, value, comment)` | Changes the text; adds the element (with its comment, LMP style) before the closing tag if missing. Normalizes `\r\n` to `\n` (the line endings are restored on render). |
| `SetList` | Rewrites the `<string>` children with LMP's indentation; empty lists become `<X></X>` like LMP writes. |
| `Render()` / `Save()` | The original text before and after the root element is kept verbatim; the root is re-serialized and the file's own line endings re-applied. **An unchanged file saves byte-for-byte**, and a test enforces it for all 13 real files. |

### `BackupService`

`Backup(configDir, files)` copies into `Config/Backups/yyyy-MM-dd_HH-mm-ss/` (adding `_2`, `_3` on a clash) and returns
the folder. `Prune(configDir, keep)` deletes the oldest folders beyond `keep` (0 keeps everything).

## `Validation/`: is this config OK?

### `SettingValidator.Validate(setting, value)` → error text or `null`

Bool must parse; numbers must parse and be within Min/Max; text within MaxLength; enums must match a name
**exactly** (LMP's `XmlSerializer` is case-sensitive, so `sandbox` would break the server).

### `LmpRules` ⚠️: where LMP knowledge lives

| Member | Rule |
|---|---|
| `Check(session)` | Heartbeat < ConnectionMsTimeout (error). Game port = website port while the website is on (warning). Gameplay values ≠ the current difficulty preset (file-level warning: LMP will switch to *Custom* on startup). |
| `InactiveReason(session, file, setting)` | Red/Green/Blue inactive while `UseRainbowEffect` is on. Funds/reputation settings only in Career; science settings in Career and Science. |
| `PresetFor(session, file)` | GameplaySettings defaults follow `GeneralSettings.GameDifficulty`. |

`Issue(FileName, Setting?, Severity, Message)`: a `null` Setting means the issue is about the whole file and shows as
a banner at the top of the page.

## `ConfigSession`: the heart ⭐

```text
Open(server, config?)       load schema + files, snapshot values
GetValue(file, setting)     current value; the default if the file lacks it
SetValue / SetList          edit in memory, update dirty state
IsModified(file, setting)   differs from disk?
IsDirty / DirtyFiles        files with real differences (changing a value back un-dirties)
DefaultFor(file, setting)   what Reset uses (preset-aware)
Validate()                  all issues: per-setting + LmpRules.Check
Save(backupsToKeep)         backup → write → prune → re-snapshot; returns the backup folder
```

## `AppSettings` / `AppSettingsStore`

A record of LUNA EVO's own preferences, saved as JSON in `%APPDATA%\LunaEvo\ConfigManager\settings.json` (or
`--settings`). `Load` returns defaults for a missing or corrupt file, so a bad file can never stop the app starting.
To add a preference, see [08 · Recipes](08-Recipes.md#add-a-luna-evo-preference).
