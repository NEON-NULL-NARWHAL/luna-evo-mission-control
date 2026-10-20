# 19 · Sharing crafts

[← Meat and Potatoes](README.md) · Code: `Core/Mods/CraftShare.cs`, `App/CraftsPage.cs`

The **Crafts** page (rail → Play) shares craft *designs* between friends and servers. Vessels flying in a game's
Universe aren't covered (yet): copying those safely needs much more care (ids, crew, orbits, a stopped server).

## Where crafts live

| Where | Path | Source label |
|---|---|---|
| KSP | `saves/<save>/Ships/VAB|SPH/*.craft`, thumbnails in `thumbs/<save>_<VAB|SPH>_<name>.png` | "KSP save &lt;save&gt;" |
| LMP server | `Universe/Crafts/<player>/VAB|SPH/*.craft` (`Server/System/CraftLibrarySystem.cs`; players upload from LMP's in-game craft library, 10 per player by default) | "Server library (&lt;player&gt;)" |

A `.craft` is a KSP ConfigNode file with `ship` and `type` at the top level (outside any node; `CraftShare.Parse`
wraps the text in a node so `KspConfigParser` keeps them) and one `PART { part = <name>_<id> }` per part. Stock crafts
use a translation key as `ship` (`#autoLOC_501214`), so their file name is used instead.

## The .lunacraft file

A zip with exactly: `craft.craft` (KSP's file, unchanged), `manifest.json` (format 1, name, facility, parts) and
optionally `thumbnail.png`. `CraftShare.Open` refuses anything else, anything over 20 MB, and a craft that doesn't
parse, so a renamed zip can't drop other files.

## Import

1. The craft's parts are compared with a GameData scan of the player's KSP (`MissingParts`), and the dialog says which
   are missing (usually a mod they don't have).
2. Destinations: the KSP save picked on the page (`Ships/<facility>/`, plus the thumbnail into `thumbs/`), or the
   server's library under a `Shared` folder (server stopped, since LMP reads its library while it runs).
3. Never overwrites: "Kerbal X (2).craft" if the name is taken.

## Tests

`CraftShareTests` (reading, export/import round trip, no overwrite, thumbnails, the part check, refusing bad files,
stock names) and `NavigationAndUpdateTests.Crafts_are_exported_and_imported_into_the_servers_library` (UI).
`TestData.KspWithCraft()` makes a KSP folder with a "career" save holding Kerbal X. `CraftsPage.PickExportPath` /
`PickImportPath` throw in every harness unless a test replaces them.
