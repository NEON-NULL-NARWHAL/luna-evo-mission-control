# 25 · LMP under the hood: how sound is it? (research, 2026-10-22)

[← Meat and Potatoes](README.md) · Sources: the LMP code in `LunaMultiplayer/` (upstream `master`, b3b9efce, the
same as GitHub on 2026-10-22), LMP's GitHub issues and discussions, kRPC's and DarkMultiPlayer's repositories.

This answers four questions before any fork work starts: what LMP's design really is, what's weak in it, whether
fixing it means a rewrite, and how LUNA EVO can get live data out of the game and test inside it. Everything here
comes from reading the code and the issue tracker. **None of it has been measured in a running game yet**: the
in-game test rig (below) is how we'd check it.

## How LMP works, in one picture

Every player's KSP runs its own full copy of the universe, with its own physics. The server doesn't simulate
anything: it **relays messages and keeps the save**.

- **Who simulates what:** locks (`LmpCommon/Locks`). The player with a vessel's *control* or *update* lock runs its
  physics and broadcasts its state; everyone else shows a copy.
- **Positions:** the owner sends a position message every 50 ms when other players are near, every 150 ms otherwise
  (`VesselUpdatesMsInterval`, `SecondaryVesselUpdatesMsInterval`). The message carries body, lat/lon/alt, the orbit,
  velocity, rotation and game time (`LmpCommon/Message/Data/Vessel/VesselPositionMsgData.cs`).
- **Time:** each game's clock is tied to real time plus its *subspace* offset. Warping puts you in a new subspace ahead
  of the others; you see everyone else as they were in your past, and "sync" jumps you forward to them.
- **The save:** the server holds every vessel and scenario as KSP config text in memory, patches it with incoming
  messages, and writes it to `Universe/` on backups.

## What's weak, and where

| # | Area | What the code does | What players see | Upstream issues |
|---|---|---|---|---|
| W1 | **Remote vessel motion** | `VesselPositionSys/ExtensionMethods/VesselPositioner.cs`: every physics frame, each part's transform and rigidbody of a remote vessel is overwritten with a straight-line blend between two snapshots. Playback runs `ping + their ping` behind, clamped to 0.25-2.5 s (`VesselCommon.PositionAndFlightStateMessageOffsetSec`). When the next snapshot is late, nothing predicts ahead: the vessel waits, then catches up | Rubber-banding, jitter near other players, unphysical bumps: your physics collides with something that teleports every frame | #380 docking kicks, #422, #654 explodes near another player, #421 duplicates |
| W2 | **Time** | `TimeSync/TimeSyncSystem.cs`: if your game drifts from real time, physics speeds up or slows down (0.85x-1.2x); beyond 3.5 s it **jumps**. It does this even when you're alone in your subspace | Thrust cut and ships teleported seconds ahead on slower PCs; rendezvous broken by time jumps | #469, #255, #426 |
| W3 | **No authority** | `Server/Message/VesselMsgReader.cs` relays position, update, proto, resource and part messages from **any** client for **any** vessel; only *remove* checks the lock. Clients can take any lock with `force` (`Server/System/LockSystem.cs`) | Fine with friends. On a public server, one modified client can move, replace or wreck anyone's vessel | - |
| W4 | **Identity and passwords** | A player is their name plus a device ID their own client reports (`HandshakeSystem.cs`, `SystemInfo.deviceUniqueIdentifier`). The server password travels unencrypted in the connect packet (`LidgrenServer.cs`); the admin password has no guess limit (`AdminMsgReader.cs`, found in our audit 2) | Bans are easy to dodge; passwords readable on the network | - |
| W5 | **Save consistency** | The universe lives in memory as text, patched by background tasks (`Server/System/Vessel/*DataUpdater.cs`). The code itself carries comments about tasks "resurrecting" removed vessels; position is saved at most every 2.5 s, and only from players in the newest subspace. **Seen for real:** 0.29.2 refuses to start (Fatal, `VesselStoreSystem.LoadExistingVessels`) if a single vessel file lacks a field it expects | Ships going back in time after a rejoin, vanishing or duplicating; career data drifting | #400, #483, #506, #509 race in backups, #369, #603 |
| W6 | **Server threading** | One long-running task per player polling its send queue (`Server/Client/ClientStructure.cs`) | Fine for a handful of players; debated upstream for 50+ | #736 (maintainers: low priority) |
| W7 | **Tied to KSP's internals** | 85 Harmony patches into KSP (`LmpClient/Harmony/`), from docking and decoupling to contracts and the astronaut complex | Each patch is a place where a mod or a KSP quirk can break sync | #576, #651, #679, #696 |
| W8 | **Testing and releases** | About 35 automated tests for roughly 82,000 lines, almost none for the client. Since 0.29.0, releases are cherry-picked onto `Release/0_29_x` branches while `master` (0.30, .NET 10) moved on separately | Fixes on master take a long time to reach players; regressions are found by players | #671, #724 |

Things that are **fine** and don't need replacing: the network transport (Lidgren UDP with reliable and unreliable
channels), the message framework and compression, the server's settings and file layout, the mod control system, the
craft library and the website.

## Is LMP still alive upstream?

Yes, more than it was for years. 2026 has had about 270 commits from a new group of maintainers (Drew Banyai,
AdmiralRadish, Shiralyn, BraveCaperCat2, DasSkelett), releases 0.29.0 to 0.29.2, and a 0.30 nightly. The original
author (gavazquez) still reviews design questions. On W2 he wrote that "ideally the physics should run on the server
instead of in clients but that would involve A LOT of work". Development is coordinated in their Discord. Pull
requests are accepted into `master`.

DarkMultiPlayer, the project LMP grew from, was last pushed in January 2025. We found no other active KSP 1 multiplayer mod.

## Rewrite or fix?

**Not a rewrite.** The weak parts are a few systems, not the whole framework:

- **W1 and W2** are the "it doesn't feel like real multiplayer" problems. Together they're about 2,000 lines
  (`VesselPositionSys` about 1,070, `TimeSync` 260, `Warp` 680), and the messages they use already carry what a better design
  needs (orbit, velocity and game time). They can be replaced behind the same messages: extrapolate from the orbit
  and velocity instead of waiting, smooth corrections instead of teleports, and let a player who is alone in a
  subspace drift instead of jumping. This is the highest-value fork work, and also the riskiest to get wrong without
  measurements.
- **W3 and W4** matter once servers are public. Server-side checks are contained changes: lock-checked vessel
  messages, plausibility limits, a guess limit, and a real account key instead of the device ID.
- **W5** wants a careful server refactor (one writer per vessel, no fire-and-forget saves). Medium size.
- **W6 to W8** are maintenance, and the cheapest wins upstream.

**What isn't realistic:** true server-run physics. KSP's physics is Unity's, inside KSP; a server would need to run
KSP itself (a headless "host" game) to simulate. That's a different project, years not weeks, and it would still
inherit KSP's limits. The practical path to "more like real multiplayer" is better prediction and smoothing (W1),
saner time (W2), and the server checking more (W3).

**Fork or contribute?** Everyone on a server must run the same client mod, so a fork with a different protocol splits
players into "LUNA EVO servers" and "LMP servers". Best order:

1. Fixes that don't change the protocol (W2's "alone in your subspace", W4's guess limit, W5's races, the `-lmp`
   port and password parsing): **send them upstream as pull requests** and use them in our build meanwhile.
2. Protocol-changing work (a new sync model): build it in the LUNA EVO fork, behind a version check, measured with the
   test rig, and offer it upstream once it's proven. Mission Control already installs the matching client, so players
   would get the right one without thinking about it.

## Delivering fixes as patches through Mission Control (owner's idea, 2026-10-22)

LUNA EVO users are already a group: Mission Control installs their LMP client, their CKAN mods and their server. So
fixes can reach them as **patches on top of the official LMP**, instead of a separate LMP that splits the community.

- **Client side:** a small KSP mod, "LUNA EVO Patches" (`GameData/LunaEvoPatches/`). It uses the Harmony library that
  LMP itself ships (`000_Harmony`, Harmony 2) to change LMP's methods as KSP loads, including private ones like
  `TimeSyncSystem.CheckGameTime`. The LMP files stay the official ones.
- **Server side:** LMP's own plugin folder (`Plugins/`, `ILmpPlugin`). A LUNA EVO plugin can add checks (W3, W4) and,
  with Harmony, fix server code (W5).
- **The one rule: never change what goes over the wire.** A patch may change how one game shows or handles things
  (smoothing other players' ships, W1; not jumping in time when you're alone in your subspace, W2), or what the server
  accepts. Then patched and unpatched players still play together, on any LMP server. Anything that needs new message
  fields stays for the fork.
- **Safety:** each patch names the exact LMP version it was written for and checks the method it changes is there. On
  any other version it stays off and says so in KSP's log. Each one can be turned off in Mission Control, which also
  shows which are active. Downloads are verified like the LMP client. A server with Mod Control on must allow the
  patch DLL (Import from GameData does that).
- **Building:** the server plugin builds anywhere (LMP's `Server.dll` is MIT and already in `tests/Fixtures`). The client
  patches compile against KSP's own DLLs from the builder's KSP install, which can't go in a public repo. So CI tests the
  plugin, and the client patches are built and tested locally, with the in-game rig.
- **Upstream:** every patch is also a tested, ready-made pull request for LMP.

This covers most of the list above (W1, W2, W3, W4 and parts of W5) without a hard fork.

## Ideas from the owner, checked against the code

| Idea | Where it stands |
|---|---|
| Proper spectating | LMP has a spectate mode (`VesselCommon.IsSpectating`, spectate locks): you watch another player's vessel without controlling it. It inherits W1's jitter. A better one (free camera, switch between players, see their throttle and stage) is client work on top of W1 |
| Separate space centers on the same Kerbin | Not in LMP: one KSC, shared facilities (`ShareUpgradeableFacilities`). Extra launch sites exist via mods (Kerbal Konstructs); per-player or per-group bases need new sync for facilities and launch sites. Big, later |
| Faster, with more error checking | W1/W2 (smoothness) and W3/W5 (checks) above |
| Claude playing the game | Possible through kRPC, below. Scripted flights, not freehand flying |

## Getting live data out of the game (for the map and Mission Control)

Three levels, cheapest first. Each works with today's LMP 0.29.2:

1. **LMP's website, no mods (map phase 2a).** `GET /` already returns every vessel with its orbit
   (`Server/Web/Structures/VesselInfo.cs`: SMA, ECC, INC, LAN, argPe, mean anomaly, epoch, body), from the server's
   in-memory copy, updated at most every 2.5 s per vessel. The Players page already reads this endpoint. The map can
   switch to it while the server runs: about 5 s behind instead of minutes. Needs *Enable Website* (local only is
   fine).
2. **A LUNA EVO server plugin (phase 2b).** LMP loads any DLL in `Plugins/` (`Server/Plugin/ILmpPlugin.cs`) and gives
   it **every message** (`OnMessageReceived`/`OnMessageSent`), every connect and disconnect, and a 10 ms tick. A small
   plugin could serve positions at full rate (20 a second), who flies what (locks), subspaces and chat to Mission
   Control over a local-only channel. No client mod, so it covers all players. It runs inside the server process, so
   it must be small, defensive and built for that server's .NET (net6 for 0.29, net10 for 0.30).
3. **A client mod for player telemetry (phase 3).** Fuel, stages, maneuver nodes, targets: only the player's own
   game knows these. LMP already has a channel for it: `ModApiSystem.SendModMessage(modName, bytes, relay)` sends a
   mod's data through LMP's own connection to the server, where the plugin from level 2 receives it. So a LUNA EVO
   KSP mod needs no network code of its own and works on stock LMP servers.

## Testing inside the game (nobody has done this for LMP)

LMP's own testing is "players try it". For a sync rewrite we'd need numbers first. The rig:

- **A sandbox KSP**: a stock copy of KSP 1.12.5 in its own folder (never the owner's modded install), with the LMP
  client and **[kRPC](https://github.com/krpc/krpc)**. kRPC is a KSP mod that lets an outside program read and control
  the game (launch, stage, throttle, warp, read positions), and it has a C# client. It's actively maintained:
  v0.7.0 came out 2026-10-21, for KSP 1.12.5.
- **Two clients on one PC**: two sandbox copies connect to a sandbox LMP server (website, UPnP and master server off).
  A test runner in C# (it can live in this repo, next to `tests/`) flies scripted scenarios: both in orbit,
  rendezvous, docking, one warps while the other doesn't, a slow-PC simulation.
- **What it measures**: for each vessel, the position the owner's game reports against where the other game shows it,
  every frame. That's the rubber-band number, in metres. Plus time error, jumps and lost messages. Run it before
  and after every sync change.
- **Limits**: KSP can't run without a window (no headless mode), takes about a minute to load, and two stock copies
  need roughly 4-8 GB of RAM. Like the screenshot runs, it takes over the desktop while it runs, so it's run when the
  owner says so. "Claude plays the game" here means scripted, measured flights through kRPC, not freehand flying.

## Suggested order

1. Map phase 2a from the website: small, useful now, no mods. **Done in 2.3.0.**
2. The in-game test rig, plus the LUNA EVO server plugin (2b), which also records sync data for the rig. **Plugin and
   a no-KSP fake client done in 2.4.0 ([26](26-Server-Plugin.md)); the KSP part of the rig waits on O3.**
3. Upstream PRs for the non-protocol fixes (W2 alone-in-subspace, W4 guess limit, `-lmp` port and password, W5 races).
4. The sync rewrite (W1, then W2) in the fork, measured with the rig.
5. Server checks (W3/W4) before public servers matter.
6. The telemetry client mod (phase 3) and the bigger ideas (spectating, separate bases).

The owner chooses; open questions are in [`DECISIONS.md`](../DECISIONS.md#open-waiting-on-the-owner).
