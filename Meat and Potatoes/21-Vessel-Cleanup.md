# 21 · Vessel cleanup

[← Meat and Potatoes](README.md) · Code: `Core/Ops/UniverseVessels.cs`, `App/VesselsPage.cs`

The **Vessels** page (rail → Server) lists everything in the Universe and removes what's not wanted: the biggest
performance lever a host has, since every vessel is something every player's game keeps in step.

## Where vessels live

LMP 0.29.2 keeps each vessel as `Universe/Vessels/<id>.txt`, a KSP `VESSEL` node with `name`, `type`, `sit` at the
top level, an `ORBIT { REF = <body index> }` and one `PART` per part (`crew = <name>` inside crewed parts)
(`Server/System/VesselStoreSystem.cs`). The parser wraps the text in a node so top-level values survive. Checked
against a real server's 51 vessel files: all parsed (33 of them were asteroids/comets, LMP's `NumberOfAsteroids` /
`NumberOfComets` spawns).

## Rules

- **Server stopped:** LMP loads vessels at start and rewrites them while running, so removal is disabled while it runs.
- **Backup first:** `UniverseVessels.Remove` zips the whole Universe (`beforecleanup`, fast compression, last 5 kept)
  before deleting anything; Profiles & Backups restores it.
- **Never crewed vessels in a bulk selection** ("Select all debris", "Select asteroids & comets"): their kerbals
  would be left "assigned". They can still be ticked one by one, and the dialog warns.
- Only files inside this server's `Universe/Vessels` can be removed.

## Tests

`LiveModsOpsTests.Lists_universe_vessels_and_removes_them_after_a_backup` (Core) and
`NavigationAndUpdateTests.Vessel_cleanup_selects_debris_but_never_crewed_vessels` (UI), using `TestData.AddVessels`.
