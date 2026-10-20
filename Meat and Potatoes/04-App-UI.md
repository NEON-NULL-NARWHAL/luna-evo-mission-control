# 04 · App UI

[← Meat and Potatoes](README.md) · Code: `src/LunaEvo.ConfigManager/` 🖥️

## Startup

`Program.Main` → (Ctrl+C helper? then exit) → `StartupOptions.Parse` → Avalonia → `App.axaml.cs` → `new MainWindow(options)`.

`MainWindow`'s constructor loads `AppSettings`, applies the theme, builds the rail, then opens a server folder:
`--server`, else the exe's own folder if `Server.dll` is next to it, else the last one used (if *Open the last server*
is on), else the welcome page.

## The window

```text
┌ rail (260 px) ─────────┬┬ page ──────────────────────────────────┐
│ LUNA EVO               ││ Headline                               │
│ Control                ││ path                                   │
│   Server Console   ●   ││ [banner: file-level warnings]          │
│   Players / Connect    ││                                        │
│   Profiles & Backups   ││                                        │
│ Game / Network / ...   ││ ┌ group: FileName.xml ────────────────┐ │
│   General      ② ●     ││ │ Label            [editor]   ↺       │ │
│   ...                  ││ │ description                         │ │
│ LUNA EVO               ││ │ error / reason line                 │ │
│   Settings             ││ └─────────────────────────────────────┘ │
│ ● Server running · 5m  ││                                        │
│ No problems found      ││                                        │
│ [Open server]          ││                                        │
│ [ Save ]               ││                                        │
└────────────────────────┴┴────────────────────────────────────────┘
```

- **Pages** are registered with `AddNav(key, navContent, page)`. The key is a file name, `MainWindow.ConsolePage` or
  `MainWindow.SettingsKey`; `Navigate(key)` switches pages.
- **Nav badges**: red count = errors in that file; green dot = unsaved changes. The Server Console item shows a dot
  while the server runs.
- `Refresh()` is the single place that re-validates and updates every row, badge, banner and the status area. Call it
  after any edit. Rows and list editors take it as their `changed` callback.

## Generated controls

| Class | Builds | Notes |
|---|---|---|
| `SettingRow` | One setting row | Editor by `SettingKind`. Number boxes have **no Min/Max** on purpose: the control would silently clamp a bad value from the file instead of flagging it. Passwords use `PasswordChar` plus a reveal button. Descriptions that only repeat the name are hidden. |
| `ListEditor` | One list group (mod control) | Items are `Entry(index, text, isDuplicate)` so identical names can be selected one at a time. |
| `ServerPage` | The Server Console | See [03](03-Server-Control.md). |
| `SettingsPage` | LUNA EVO Settings | Colour wheel (`ColorView`, ring shape), presets, theme, behaviour, automation, LMP updates, about, folders, links. |
| `ObjectListEditor` | One record list (plugins, mandatory parts) | See [12](12-Mod-Control-From-GameData.md). |
| `ModImportPanel` | Import from GameData | See [12](12-Mod-Control-From-GameData.md). |
| `SetupWizard` | Set up a new server | Shown from the welcome page. See [10](10-Setup-and-Updates.md). |
| `PlayersPage` | Players | See [11](11-Live-Players.md). |
| `ConnectPage` / `ProfilesPage` | Connect / Profiles & Backups | See [13](13-Automation.md). |
| `JoinPage` | Join a server | Works with no server open. See [15](15-Joining-Servers.md). |

Every interactive control gets an **AutomationId** via `.WithId(...)`, e.g. `GeneralSettings.xml/ServerName`,
`nav/console`, `console/start`, `settings/preset/Kerbin blue`. The UI tests and Windows UI Automation find controls by
these IDs, so **keep them stable** and give new controls one.

## Look and feel 🎨

The design follows **NOMM** (Nuclear Option Mod Manager): Material 3 colours from a seed, JetBrains Mono everywhere,
a navigation rail with pill selection, the main action as a big button at the bottom of the rail, and settings as
rounded groups of rows with a muted description line.

- **`App.axaml` is the single home of the look**: colour resources for Dark and Light, icon geometries, and style
  classes such as `headline`, `label`, `sublabel`, `group`, `row`, `nav`, `fab`, `text`, `chip`, `card`, `badge`,
  `banner`, and log kinds (`Error`, `Chat`...). Code only adds classes; it never sets colours.
- **`ThemeManager.Palette(accent, dark)`** derives every colour from the accent's hue and saturation at fixed lightness
  steps (the neutrals get a faint tint of the same hue). `Apply` swaps the brushes into the theme dictionaries and
  sets Fluent's own accent, so everything using `DynamicResource` recolours live.
- The **font** is set app-wide in `Program.BuildAvaloniaApp` (`FontManagerOptions.DefaultFamilyName`).
- **Icons** are Material Symbols paths stored as `StreamGeometry` resources (`IconSave`, `IconPlay`...); `Ui.Icon(key)` uses them.

## Dialogs

`Dialogs.ConfirmAsync / InfoAsync / ChooseAsync`. Avalonia has no message box, so this builds a small window.
`Dialogs.Responder` lets tests answer instead of showing a window: `(title, message, buttons) → index`.
