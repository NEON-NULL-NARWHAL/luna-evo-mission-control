# 08 · Recipes

[← Meat and Potatoes](README.md)

Step-by-step guides for the changes you're most likely to make. Each ends with **run `dotnet test`**.

## Add a validation rule across settings

*Example: warn if `MaxPlayers` is over 50.*

1. Open `src/LunaEvo.ConfigManager.Core/Validation/LmpRules.cs`.
2. In `Check(session)`, read values with `session.GetValue(file, setting)` and `yield return` an `Issue`:
   ```csharp
   if (Int(session.GetValue(General, "MaxPlayers")) is > 50)
       yield return new Issue(General, "MaxPlayers", Severity.Warning, "More than 50 players can be heavy on a home server.");
   ```
   `Severity.Error` blocks saving; `Warning` doesn't. A `null` setting makes it a banner for the whole file.
3. Add a test in `tests/LunaEvo.ConfigManager.Core.Tests/ConfigSessionTests.cs` (copy `Heartbeat_must_be_lower_than_timeout`).

## Grey out a setting that doesn't apply

1. In `LmpRules.InactiveReason`, return a short reason when it has no effect:
   ```csharp
   if (fileName == Website && setting != "EnableWebsite" && !IsTrue(session.GetValue(Website, "EnableWebsite")))
       return "Only used when EnableWebsite is on.";
   ```
2. Nothing else is needed: `SettingRow.Refresh` disables the editor and shows the reason.
3. Add a test (see `Greys_out_settings_that_have_no_effect`).

## Support a new LMP setting

Usually **nothing to do**: it's read from `Server.dll`. Only if it needs a special editor:

- a new type → add a `SettingKind` and map it in `LmpSchemaLoader.KindOf`, then add an editor case in
  `SettingRow.CreateEditor` and `ShowValue`;
- a new config file → add it to a section in `MainWindow.Sections` (otherwise it lands in "Other").

## Add a quick command to the console

Add `("/name args", "What it does")` to `ServerPage.QuickCommands`. Commands that destroy things should only fill the
box (that's how chips work), never send directly.

## Add a LUNA EVO preference

1. Add a property with a default to `AppSettings` (Core).
2. Add a row in `SettingsPage`'s constructor using the `Toggle(...)` or `Number(...)` helpers, giving it an id
   `settings/<name>`.
3. Read it where needed via `host.Settings.YourSetting`.
4. Add a test in `SettingsPageTests`.

## Add a page to the rail

1. Build your page as a `Control` (see `SettingsPage` for a pattern).
2. In `MainWindow.BuildPages` (and `ShowWelcome` if it should work without a server), call
   `AddHeading("Section")` and `AddNav("key", new TextBlock { Text = "Name" }, page.View)`.
3. Test with `ui.OpenTab("key")`.

## Add a Discord notification

Call `_host.Notify("emoji **text**")` from wherever the event happens (see `ServerPage.OnStateChanged` and
`OnExitedAsync`). It does nothing if no webhook is set and never throws. Mention it in [13](13-Automation.md).

## Add a question to the setup wizard

1. Add a control field in `SetupWizard` (copy `_maxPlayers` or a `Toggle(...)`) with an id `wizard/<name>`.
2. Add a row in the constructor's group 2 with `Ui.Row(label, description, control)`.
3. Add its value to `Choices()` as `[(file, setting)] = value`; `ConfigGenerator` does the rest.
4. Assert it in `NewFeatureTests.Wizard_downloads_lmp_writes_the_config_and_opens_the_server`.

## Change colours or styles

- Fixed colours (error, warning, log chat/network): the theme dictionaries in `App.axaml`.
- Colours derived from the accent: `ThemeManager.Palette` (lightness steps per role).
- A new look for something: add a style class in `App.axaml` (`<Style Selector="Border.myThing">`) and
  `control.Classes.Add("myThing")` in code.

## Update the test fixtures from a real server

1. Copy the server's `Config/*.xml` into `tests/Fixtures/Config/` **byte-for-byte** (Explorer or `Copy-Item`; not
   Git Bash's `sed`, which converts CRLF to LF).
2. Blank `Password`/`AdminPassword` and change `ServerName` with a byte-safe tool (Python `bytes.replace`, or a
   hex-aware editor).
3. For a new LMP version, replace `tests/Fixtures/LMPServer/*.dll` and update the version expectations in `SchemaTests`
   and `SettingsPageTests`.
4. `dotnet test`.
