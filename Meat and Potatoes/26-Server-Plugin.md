# 26 · The LUNA EVO server plugin and the fake LMP client (2.4)

[← Meat and Potatoes](README.md) · Code: `src/LunaEvo.ServerPlugin/` (`LunaEvoPlugin`, `LiveState`),
`Core/Live/LiveFeed.cs`, `Core/Map/LiveVessels.cs` (`Overlay`), `Core/Ops/ServerPluginInstaller.cs`, `App/MapPage.cs`,
`tests/FakeLmpClient/`

The first piece of "fixes as patches through Mission Control" ([25](25-LMP-Under-The-Hood.md)): code that runs inside
the official LMP server without changing it. Version 1.0.0 only **watches**; it gives the map every vessel's latest
position and who flies what.

## How LMP loads it

LMP's server loads every DLL in `<server>/Plugins/` at start (`Server/Plugin/LmpPluginHandler.cs`,
`Assembly.UnsafeLoadFrom`) and calls each class that implements `ILmpPlugin`: `OnServerStart`/`OnServerStop`,
`OnUpdate` every 10 ms on its main thread, connect/authenticate/disconnect, and `OnMessageReceived`/`OnMessageSent`
for every message. Exceptions from a plugin are caught and logged at Debug, so a plugin bug can't stop the server, but
a slow plugin slows everyone: `OnMessageReceived` runs on the receive path, under one lock for all plugins.

Two things learned from LMP's code that matter for any plugin:

- **`OnMessageReceived` runs before LMP checks the sender is logged in** (`Server/Server/MessageReceiver.cs`). So
  our plugin only listens to `client.Authenticated` players; otherwise someone without the password could put
  made-up vessels on the map.
- **A plugin can set `message.Handled = true` and LMP then ignores the message.** That's how the server checks from
  [25](25-LMP-Under-The-Hood.md) (W3: refuse moving a vessel you don't control) can be built as a plugin later. 1.0.0
  never sets it.

## What it does

- `Observe` copies each `VesselPositionMsgData` (body, lat/lon/alt, the 8 orbit numbers INC, ECC, SMA, LAN, LPE, MNA,
  EPH, REF, velocity, game time, subspace, landed/splashed) into `LiveState`, a dictionary by vessel id (at most
  20,000). LMP recycles message objects, so arrays are cloned. `VesselRemoveMsgData` removes the entry.
- `OnUpdate` writes `<server>/LunaEvo/live.json` every 500 ms while something changed, otherwise every 2 s (a
  heartbeat), on a background task (temp file then rename), never two at once. The snapshot adds players (name,
  subspace, the vessel they control, from LMP's control locks) and subspaces. Non-finite numbers are written as 0, so
  the file is always valid JSON. Player names and ids only: no addresses.
- `OnServerStop` deletes the file. After a crash the file stays, so readers judge by `WrittenUtc` (`LiveFeed.MaxAge`, 6 s).
- No network port, no writes anywhere else, no changes to any message.

It targets **net6.0** (LMP 0.29's runtime) and compiles against `tests/Fixtures/LMPServer/Server.dll` (LMP 0.29.2, MIT;
LmpCommon is built into it). `ServerPluginInstaller.Supports` offers it only for LMP 0.29.x; 0.30 (.NET 10, changed
code) needs its own build later.

## 1.1 (Mission Control 2.7): two server-side fixes

The plugin also applies audit 11's P3 (1 ms timers on Windows) and the server leg of P1 (a Harmony postfix on
`VesselSrvMsg.DefaultChannel`: relayed fast updates on a channel per vessel), measured in
[27](27-Client-Patches.md#27-network-speed-audit-11s-p3-and-p1-measured). It now brings Harmony (`Lib.Harmony` 2.3.5,
.NET 6 build), so both DLLs go in `Plugins/LunaEvo/` (LMP loads Plugins/ recursively; its resolver finds Harmony among
the loaded plugins). Mission Control embeds both and moves a 2.4-2.6 install (`Plugins/LunaEvo.ServerPlugin.dll`). Off
switches: `LUNAEVO_PATCHES_OFF`. Undone at server stop.

## Mission Control's side

- The app embeds the built DLL (`LunaEvo.MissionControl.csproj`: a `ProjectReference` with
  `ReferenceOutputAssembly=false`, then an `EmbeddedResource`), so there's nothing to download. `MapPage.PluginBytes()`.
- Map → **Install / Update / Remove the LUNA EVO server plugin** (`map/plugin`): only with the server stopped (LMP keeps
  plugins open), after a dialog that says exactly what it does. `ServerPluginInstaller.Status` compares SHA-256, so an
  older or foreign copy shows as "Update".
- `LiveFeed.Read` reads the file defensively (16 MB cap, format 1, finite numbers, text capped, fresh only) and
  `LiveVessels.Overlay` puts its positions on top of the website/save list: orbit, position, landed or not, **Flown by**.
  Vessels it knows that the list doesn't are added as "(new vessel)". The map then redraws every second.
- Order of sources: plugin (every message) > website (2.5 s per vessel, [24](24-Solar-System-Map.md)) > save (minutes).

## The fake LMP client (`tests/FakeLmpClient`)

A console program that joins a real LMP server with **no KSP**, using LMP's own message classes and Lidgren from
`Server.dll`: connect (password in the hail), `HandshakeRequestMsgData`, ask for subspaces and join the newest, take a
control lock, then 20 `VesselPositionMsgData` a second on a circular orbit.

```
dotnet run --project tests/FakeLmpClient -- --port 8810 --password <pw> --name Tester --vessel <guid> --sma 900000 --seconds 20
```

Only against a sandbox server (never someone else's). It's the first part of the test rig from
[25](25-LMP-Under-The-Hood.md): server-side behavior can now be tested for real without a game. Next it can grow
two players, warps and docking messages.

## The in-game test rig (set up and first run 2026-10-22)

Lives next to the repo, in `TestRig/` (never in git: it holds Squad's game files). Its own README lists everything:

- **KSP:** a stock 1.12.5 copy (game, Squad, SquadExpansion), never the owner's modded install, with the LMP 0.29.2
  client and kRPC 0.7.0 (+ ModuleManager, which kRPC ships). All downloads checked against GitHub's SHA-256 and unzipped
  with a path check. Light settings: 1280x720 windowed, low graphics, 30 fps, muted, runs in the background.
- **kRPC** starts by itself and listens on **127.0.0.1 only** (ports 50000/50001): a C# client (`KRPC.Client` on NuGet)
  can then fly the game.
- **Server:** LMP 0.29.2 from the release zip, the sandbox config (8810/8910, no master server, no UPnP, website on
  127.0.0.1), and this plugin. First start checked: fresh universe, plugin writing.
- **Players:** one KSP (`RigPilot`) plus `FakeLmpClient` as the second player keeps it to one game window and ~4 GB
  of RAM. A second KSP copy can be added later with hard links on the same drive.
- KSP is launched only when the owner says so: it takes the screen while it opens.
- **Joining without clicks:** LMP's `-lmp` command line only works on port 8800 without a password (its port parsing
  cuts the address at a position measured on the wrong string, and there's no password option). So the rig server
  listens on **127.0.0.1:8800** (this PC only) with no password; don't run it while the owner's real server (also
  8800) runs. LMP also hides its windows until its disclaimer is accepted, so the rig's client settings accept it.
- **kRPC only answers in a game scene** (it has no main-menu scene), so the runner waits until LMP has joined and
  loaded the Space Center. kRPC's C# client is built for .NET Framework 4.7.2 and works on .NET 10.

### Running the rig

`tests/RigRunner/Start-Rig.ps1 [-Fresh] [-Slow] [-NoPatch]` starts the rig server (hidden) and the rig KSP, after
checking no `Server.exe` or KSP runs (the owner's real server shares port 8800). `-Fresh` deletes the rig's universe
(an old rocket on the pad blocks a launch), `-Slow` caps KSP below real time, `-NoPatch` parks the client patches
(and drops the server password, which unpatched LMP can't use). Then `RigRunner` (below), and close KSP and the
server when done. Results go to `TestRig/Results/<time>/` (`game.csv`, `observer.csv`, `run.log`).

### `tests/RigRunner` and the first run

`dotnet run --project tests/RigRunner -- --seconds 25` with the rig server and the rig KSP (`KSP_x64.exe -lmp
lmp://127.0.0.1`) running: it waits for kRPC, checks the server sees the pilot, launches stock Kerbal X straight up,
and every second compares the game's altitude with the last position the server plugin received for the vessel
flown by `RigPilot`.

First run (2026-10-22): KSP loaded in about 2 minutes, LMP connected and synced in about a second, kRPC answered in
the Space Center, the launch worked.

| What | Result |
|---|---|
| Server behind the game | 38 m on average, 94 m at most, rising with speed (6 m at 15 m/s, 94 m at 239 m/s) |
| Age of the last message the server had | 374 ms on average |

Read it as **about 0.4 s behind**, which is LMP's send interval for a player alone (`SecondaryVesselUpdatesMsInterval`,
150 ms, since nobody else is near) plus the plugin writing `live.json` every 500 ms. That's what the **server** knows.
Another player sees it later still: LMP replays remote vessels 0.25-2.5 s behind on purpose (W1 in
[25](25-LMP-Under-The-Hood.md)). Measuring that needs a second player that records what LMP sends it: done in the second and third runs below.

### What a second player sees (runs 2 and 3, 2026-10-22)

`RigRunner` now starts `FakeLmpClient --observe` as a second player (`RigObserver`) that records every position LMP
relays to it with its arrival time, samples the game 10 times a second (`krpc.HoldTick()` so the clock and the altitude
come from the same physics tick), flies 30 s, and `SyncAnalysis` works out:

- **network**: when each message arrived against when the flying game was at that moment;
- **the second player's view**: LMP's replay rule from its code (play back at the viewer's clock minus
  ping + their ping, clamped to 0.25-2.5 s; blend between the two messages around that time; hold the last one when the
  next is late) applied to the messages the observer really received. `--selftest` checks it on known answers.

| | Run 3 (patched client, password server) |
|---|---|
| Messages from the rocket | 376 in 30 s, every 60 ms (median; the launch clamps count as "nearby", so LMP sends at the fast rate), longest gap 180 ms |
| Network, flying game to the second player | 54 ms median, 97 ms at most (one PC, so this is LMP's own queues, not the internet) |
| Second player's view | the rocket where it really was **0.17 s ago** (median, 0.19 s at most): 23 m behind on average, 54 m at most, climbing at 129 m/s on average |

Two findings:

- **"A message's position is about 0.09 s ahead of its stamp" (solved, 2.6).** Fitting a time shift **and** a constant
  height to the same data gave +20 ms and +10.3 m with 0.12 m rms: LMP sends the altitude of the vessel's **root part**
  (the pod, about 10 m above the centre of mass that kRPC's `MeanAltitude` gives), which at 129 m/s looks like 70 ms.
  The runner now samples the root part too (`Parts.Root.Position` → `AltitudeAtPosition`) and reports a stamp check;
  with that, run A: **+20 ms, 0.01 m rms**, exactly one physics tick, which is real: patch 3 in
  [27](27-Client-Patches.md) fixes it (run A2: 0 ms). With the old rockets cleared (`Start-Rig.ps1 -Fresh`) LMP sends
  every 160 ms (nothing else loaded nearby) instead of 60 ms.
- **The game clock ran 3.7% fast** during the flight: LMP's time sync speeding the game up to catch the server's clock
  (W2 in [25](25-LMP-Under-The-Hood.md)), seen live.

These are LMP's rules applied to real messages, not a second KSP on screen. A second rig KSP would confirm them, and is
the way to measure W1 fixes.

## Checked for real (2026-10-22)
## Checked for real (2026-10-22)

The real LMP 0.29.2 server, in a sandbox copy of the owner's universe (ports 8810/8910, no master server, no UPnP,
website on localhost): the plugin loaded ("LUNA EVO server plugin 1.0.0: writing live positions"), the fake client
logged in and flew a saved vessel at 900 km and then 950 km. `live.json` showed the player, their subspace and
`FlownBy`, and Mission Control's own `LiveFeed.Read` + `Overlay` read it 0.1 s old with the new orbit. LMP's website
showed the new orbit too. It also showed W3 for real: a client that had never touched that vessel took its control lock
and moved it, and the server accepted it.

## Tests

`ServerPluginTests` (Core): LMP's real `VesselPositionMsgData` in, `live.json` out, read back by `LiveFeed`
(the plugin and the app agree on the format); broken and non-finite messages; removals; stale, future and
wrong-format files; the overlay; install/update/remove. `FuzzTests.Server_plugin_live_feed`. `MapPageTests`: install
from the Map page, a live file showing "Flown by", and refusal while the server runs.
