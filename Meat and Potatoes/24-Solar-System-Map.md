# 24 · Solar system map (2.2, live in 2.3)

[← Meat and Potatoes](README.md) · Code: `Core/Map/SolarSystem.cs` (orbits, bodies, planet packs, universe clock),
`Core/Ops/UniverseVessels.cs` (vessel orbits), `Core/Map/LiveVessels.cs` (the live merge, 2.3),
`Core/Live/ServerInfoClient.cs` (website orbits), `App/MapPage.cs` (`MapPage`, `MapCanvas`)

A top-down map of the server's universe: the bodies (stock plus the planet packs in your KSP's GameData) and every
vessel in `Universe/Vessels`, on its orbit, where it is now. Phase 1 is read-only and 2D.

## Orbits

`Orbit(Sma, Ecc, Inc, Lan, ArgPe, MeanAnomalyAtEpoch, Epoch)` is KSP's ORBIT node as stored: SMA in metres (negative
for an escape), ECC, INC/LAN/LPE in degrees, MNA in **radians** at EPH (seconds). Kopernicus uses the same elements
(`meanAnomalyAtEpoch` radians, or `meanAnomalyAtEpochD` degrees).

- `PositionAt(t, μ)`: mean motion n = √(μ/|a|³), M = MNA + n(t − EPH), Kepler's equation by Newton (elliptic
  `M = E − e·sin E`, hyperbolic `M = e·sinh H − H`, both with an iteration cap), then rotated by argPe, inc and LAN
  into the parent's frame. The map draws X/Y (looking down on the ecliptic); Z is kept for later.
- `Path(points, maxRadius)`: the ellipse, or the arc of a hyperbola out to `maxRadius` (the SOI), at most H = 6.
- SOI = a·(μ/μparent)^0.4. Checked: Mun 2,429,559 m; Kerbin's year 9,203,393 s against KSP's 9,203,545.
- Odd save values (ECC = 1, SMA 0 or 1e15, far from the epoch) give finite points: `MapTests` fuzzes them.

## Bodies

`SolarSystem.Stock()` is KSP 1.12's 17 bodies with radius, μ, orbit and FlightGlobals index (Sun 0, Kerbin 1, Mun 2,
Minmus 3, Moho 4, Eve 5, Duna 6, Ike 7, Jool 8, Laythe 9, Vall 10, Bop 11, Tylo 12, Gilly 13, Pol 14, Dres 15,
Eeloo 16). A vessel's ORBIT `REF` is that index, but packs renumber bodies, so `ForVessel` prefers `IDENT`
("Squad/Kerbin", "Urlum"); `Find` drops the "Squad/" part and maps Kerbol to Sun.

`SolarSystem.Load(gameData)` reads Kopernicus configs on top of stock, a small subset of ModuleManager:

- Files: `*.cfg` under 4 MB that mention both "Kopernicus" and "Body"; reparse points skipped (the same rule as every
  GameData scan, [14](14-Security.md)). Parsed with `KspConfigParser`.
- Top-level `@Kopernicus` / `@Kopernicus:FOR[X]` etc. Passes run in ModuleManager's order: none, BEFORE, FOR,
  AFTER, FINAL; then by file name. That's how OPM's `AFTER` move of Eeloo finds Sarnus.
- `Body` (new, from its `Template` if any), `@Body[x]` / `%Body[x]` (edit), `!Body[x]` (remove). Keys may carry
  `@`/`%`. Values computed by MM expressions (`*= 2`) aren't evaluated: the old value stays.
- Properties: `radius`; gravity from `gravParameter`, else `mass`·G, else `geeASL`·9.80665·R². Orbit: the elements
  above, `referenceBody`, `color` (0-1, 0-255 or #hex).
- Kept only if the body reaches the Sun through its parents (no loops, no missing parents) and has an orbit. The Sun
  can't be removed or given a parent. A broken pack loses its own bodies, never the map.
- `FromPacks`: new bodies plus stock ones whose parent, orbit, radius or μ really changed. Kopernicus' own
  `System.cfg` restates every stock body, and visual mods touch them without moving anything.

Checked on the owner's GameData (OPM, MPE, BetterDres, BetterKerbol...): 47 bodies, 33 from packs, Eeloo round Sarnus.

## Live positions (2.3)

LMP writes `Universe/Vessels` only on backups, minutes apart. While the server runs, its website (`GET /`, the same
answer the Players page reads every 5 s) lists the server's **in-memory** vessels, whose orbits players' games update
every 2.5 s (`Server/System/Vessel/VesselPositionDataUpdater.cs`; only from players in the newest subspace).

- `ServerInfoClient.Parse` reads `SemiMajorAxis`, `Eccentricity`, `Inclination`, `LongitudeOfAscendingNode`,
  `ArgumentOfPeriapsis`, `MeanAnomaly`, `Epoch` (the save's ORBIT values and units), `Lat`, `Lon`, `Alt` and
  `ReferenceBody`. Non-finite numbers, a negative eccentricity or SMA 0 mean no orbit.
- `MapPage.Live()` uses `PlayersPage.Last` while the server runs and it's under 30 s old (`LastAt`); no second poll.
- `LiveVessels.Merge(saved, live)`: the website's list decides **which** vessels exist (new launches appear, removed ones
  go); the save fills in crew, parts and situation. LMP 0.29 never fills in `Situation`, so a saved landed vessel stays
  landed unless it's more than 50 m higher or lower than saved, or on another body (then it's "FLYING").
- The website names the body only by number. Planet packs renumber bodies (OPM's Sarnus is 21, Urlum 27, Neidon 33 on
  the owner's server), so `LiveVessels.BodyNames` learns each number from the save's ORBIT `REF`/`IDENT` pairs.
- `map/source` says where positions come from; `map/goLive` turns the website on for this PC only
  (`PlayersPage.EnableWebsite`).
- Checked against the real LMP 0.29.2 server (sandbox copy of the owner's universe, ports 8810/8910): 51 vessels, every
  body found, the 11 landed ones kept on the ground.

## Universe time

`UniverseClock.Now(server)` = (now UTC − `Universe/StartTime.txt`) + the largest offset in `Universe/Subspace.txt`
("id:seconds"). That's how LMP clients compute it, and it works with the server stopped. `Kerbin(t)` formats it with
6-hour days and 426-day years. No server files: the newest vessel epoch.

## The page

`MapPage` (rail entry **Map**, after Vessels; hidden in simple mode, `MainWindow.AdvancedPages`):

- Focus picker (the body tree), **Up**, **Zoom to vessels / Show all**, Asteroids & comets, Debris, Refresh.
- Re-reads vessels every 30 s while visible (LMP rewrites them while running) and redraws every 2 s.
- GameData comes from `Settings.KspFolder`, else `KspLauncher.FindKsp()`. It's re-read only when that changes or on Refresh.
- "Selected" shows apoapsis/periapsis above the surface, inclination, period (Kerbin days), crew; landed vessels show
  latitude and longitude. "Around this body" lists the vessels whose orbit is around the focus.

`MapCanvas` draws the focus body, its children's orbits and positions ("Name (n)" = vessels around it), the vessels
(orbit lines skipped for asteroids unless selected), and the SOI as a dashed circle. Landed vessels sit on the rim at
their longitude, with one label "n on the surface". The scale comes from `Extent()`: moons always count; vessels count
until one is more than 4× farther than the one before (from the third on), so one probe at the edge of the SOI doesn't
shrink everything. `FitVessels` fits to vessels only. Labels skip spots already taken (`_labels`). Wheel zooms around the
pointer, drag pans, `Click` picks the nearest vessel within 10 px, else a body (which focuses it).

`VesselMarks` and `BodyMarks` are what was drawn last, for tests and clicks.

## Tests

`MapTests` (Core: orbit math, stock periods, packs incl. moves, removals and broken packs, the clock, vessel parsing,
odd orbits). `MapPageTests` (UI: a fake KSP with one pack body, `TestData.AddOrbitingVessels`, focus, click, details,
filters). Headless windows don't paint, so the test renders the canvas into a `RenderTargetBitmap` to get marks.

## Next (phase 2 ideas)

Full-rate positions and who's flying what from a LUNA EVO server plugin ([25](25-LMP-Under-The-Hood.md)); time scrubbing; maneuver/encounter
prediction across SOIs; 3D tilt (Z is already computed); KSP's real body textures from GameData.
