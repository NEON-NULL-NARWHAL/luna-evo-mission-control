# 12 · Mod control from GameData

[← Meat and Potatoes](README.md) · Code: `Core/Mods/`, `App/ModImportPanel.cs`, `App/ObjectListEditor.cs` 🔌

## What LMP checks (0.29.2)

From `LmpClient/Systems/Mod/ModSystem.cs` and `ModFileHandler.cs`, when `GeneralSettings.ModControl` is on:

| List | Meaning |
|---|---|
| `AllowedParts` / `AllowedResources` | Vessels may only use these. |
| `RequiredExpansions` | `MakingHistory`, `Serenity` (Breaking Ground). |
| `MandatoryPlugins` (DllFile) | Players must have these DLLs; if `Sha` is set, an exact match. |
| `OptionalPlugins` (DllFile) | Allowed DLLs; only matters when `AllowNonListedPlugins` is off. |
| `ForbiddenPlugins` (ForbiddenDllFile) | Players with these can't join. |
| `MandatoryParts` (MandatoryPart) | Parts players must have. |

A **DllFile** is `Text`, `Link`, `FilePath`, `Sha`. `FilePath` is the DLL's path **relative to GameData, lowercase,
with `/`** (e.g. `mechjeb2/plugins/mechjeb2.dll`). `Sha` is `BitConverter.ToString(SHA256(file))`, e.g. `3F-A0-…`.
LMP skips `Squad*` and `LunaMultiplayer` folders.

## The scanner (`GameDataScanner.Scan`)

Mirrors LMP's in-game generator (`GenerateModControlFile`), but works from the files on disk:

- **Parts:** every `PART` (and `PART:NEEDS[...]`) node's `name`, with `_` → `.` like KSP's PartLoader (`delta_small` → `delta.small`).
- **Resources:** every `RESOURCE_DEFINITION` node's `name`.
- **Plugins:** every `.dll` under non-Squad, non-LMP folders, with path, hash and `"Name. Version: x"` text.
- **Expansions:** folders under `GameData/SquadExpansion`.

`KspConfigParser` reads KSP's `.cfg` format (nodes, `key = value`, `//` comments, braces on the same or next line),
leniently, because mods write these by hand.

**Checked on a real modded install:** 4,382 `.cfg` files → 1,527 parts, 209 resources, 132 plugins, both expansions;
every stock part that exists in the game was found.

### Limitation ⚠️

**ModuleManager patches aren't applied** (`@PART`, `+PART`, `!PART`). Parts created by `+PART` copies or removed by
`!PART` are missed or kept. LMP's in-game generator (Mod Control window → *Generate*) sees the patched result; use it
if a big MM-based mod depends on this.

## In the app

- **Import from GameData** (top of the Mod Control page): the path is pre-filled from Steam's library list if KSP is
  found. Options: replace or add to the current lists; add plugins to Optional Plugins (with hashes); require the
  expansions found. It edits in memory; review and Save as usual. Turn on **Mod Control** in General for LMP to
  enforce it.
- **Object list editors:** one row per entry with a box per field; commits on Enter or leaving the box. Backed by
  `ConfigSession.GetObjectList / SetObjectList` and `ConfigFile.GetObjectList / SetObjectList`, which write the same
  `<DllFile><Text/>...</DllFile>` shape as LMP's `XmlSerializer`.
- The schema marks these as `SettingKind.ObjectList`, with `ItemElement` (e.g. `DllFile`) and `ItemFields` read
  from LMP's own record classes, so new fields would appear automatically.
