# 27 · LUNA EVO patches for the LMP client (2.5, three patches in 2.6)

[← Meat and Potatoes](README.md) · Code: `src/LunaEvo.ClientPatches/` (`PatchLoader`, `CommandLinePatch`, `LmpAddress`, `TimeSyncPatch`, `StampPatch`, `Stubs/`),
`Core/Join/ClientPatches.cs`, `Core/Join/KspLauncher.cs`, `App/JoinPage.cs`

The owner's plan from [25](25-LMP-Under-The-Hood.md#delivering-fixes-as-patches-through-mission-control-owners-idea-2026-10-22):
fix LMP with patches on top of the official client, delivered by Mission Control, never changing what goes over the
network. This is the first one.

## The bug it fixes (LMP 0.29.2, seen in the test rig)

KSP started with `-lmp lmp://host:port` is supposed to join that server at the main menu. LMP's
`MainSystem.HandleCommandLineArgs` cuts the address at `address.LastIndexOf(":")` measured on the **whole** text but
applied to the text without `lmp://`, so for any normal port it's past the end and `Substring` throws
`ArgumentOutOfRangeException`. That happens inside `MainSystem.Awake`, so the rest of LMP's startup (its network
included) never runs: no "LMP Finished awakening", no connection, for the whole session. There's also no way to give a
password. So until now Mission Control could only auto-connect to port 8800 without a password, and put every other
server in LMP's list for the player to click.

## How the patch works

- A KSP mod, one DLL in `GameData/000_LunaEvoPatches/` ("000_" so KSP loads it before `LunaMultiplayer`; checked in
  KSP.log). `PatchLoader` is a `KSPAddon(Startup.Instantly)`: it checks the LMP client is 0.29.x and that
  `HandleCommandLineArgs` exists, then uses **LMP's own Harmony** (`000_Harmony`) to put `CommandLinePatch.Prefix`
  in front of it. On any other version, or any error, it patches nothing and LMP runs as it always has.
- `Prefix` does what LMP's method does (`-debug`, `-logFile`, `-lmp`) and returns false so the original doesn't run.
  The address goes through `LmpAddress.TryParse`: `lmp://host`, `lmp://host:port`, `host:port`, `[IPv6]:port`, a
  trailing slash; anything else is refused (no host, port outside 1-65535, spaces, `@`, `/`, over 300 characters).
- **The password never goes on the command line** (any program on the PC can read a process's command line). At the
  main menu, before LMP connects, `FillPassword` takes it from LMP's own server list: the entry with the same address
  and port, which Mission Control writes anyway. The log says "(with its password)", never the password.
- Nothing sent to the server changes, so patched and unpatched players play together on any LMP server.

## Building (anywhere, no KSP needed, since 2.6)

Players never build anything: the patches travel inside the Mission Control exe. But a KSP mod normally compiles
against KSP's own DLLs, which can't go in a public repo, so in 2.5 only a PC with KSP could make a release. Since 2.6:

- **LMP's client** (`LmpClient.dll`, `Lidgren.Network.dll`, MIT) is in `tests/Fixtures/LmpClient`, taken from LMP
  0.29.2's checksum-verified release zip, with LMP's license. **Harmony** (MIT) comes from NuGet (`Lib.Harmony` 2.2.2;
  LMP ships 2.2.1 with the same API, and in the game LMP's copy is the one loaded).
- **KSP and Unity** are replaced at compile time by LUNA EVO's own stand-ins (`Stubs/AssemblyCSharp`,
  `Stubs/UnityCoreModule`): assemblies with the same names that declare only what the patches use (`KSPAddon`,
  `GameScenes`, `HighLogic.LoadedScene`, `Vessel.packed`, `MonoBehaviour`, `Object.DontDestroyOnLoad`/`Destroy`,
  `Debug.Log`, `Time.fixedDeltaTime`/`timeScale`). No Squad or Unity code: names, signatures, and enum values read
  from KSP 1.12.5's metadata. In the game, KSP binds the patch to its real assemblies by name (not strong-named).
- **Enum values are compiled in as numbers**, so they must be KSP's: `ClientPatchTests.The_stand_ins_have_ksps_own_enum_values`
  checks the stand-in source against the recorded values everywhere, and against KSP itself where `TestRig/KSP` exists.
  Adding a member to a stand-in: read its exact shape from KSP's metadata first (Meat and Potatoes 26's rig has KSP).
- The project builds with the app (`ProjectReference`, embedded resource), on CI too. `release.ps1` still checks the
  exe carries it. Checked in the rig: the stand-in build loads in KSP and applies all three patches.

`LmpAddress.cs` has no KSP types, so Core.Tests compiles the same file to test and fuzz it.

## In Mission Control

- **Join → Install the LUNA EVO patches** (`join/patches`), shown when the chosen KSP has the LMP 0.29 client and this
  build carries the patches. A dialog says exactly what it does; KSP must be closed. **Update** when a different copy
  is there, **Remove** takes it out (and the folder if empty).
- With the patches in that KSP, `KspLauncher.PlanFor(..., patched: true)` is always `AutoConnect`, and
  `LaunchArguments` gives `-lmp lmp://host:port` (IPv6 in brackets). The server, with its password, is saved in LMP's
  list first, as before.
- Tests fake the bytes with `JoinPage.PatchBytes`.

## Checked for real (2026-10-22, test rig)

1. Without the patch, KSP with `-lmp lmp://127.0.0.1:8800`: `ArgumentOutOfRangeException` in
   `HandleCommandLineArgs` from `MainSystem.Awake`, no "LMP Finished awakening", no connection.
2. With the patch, same command, server now **with a password** (saved only in LMP's list): "Client patches applied
   to LMP 0.29.2", "Connecting via command line to: 127.0.0.1, port: 8800", "LMP Finished awakening", "Using the saved
   server entry ... (with its password)", "Connected", "Handshake successful". Then the sync measurement ran in the same
   session ([26](26-Server-Plugin.md)).

## Patch 2 (2.6): no time jumps when you're alone in your subspace

LMP keeps each game on its subspace's clock (`TimeSyncSystem.CheckGameTime`): within 3.5 s it runs the game 0.85-1.2x,
beyond that it sets the game's time. That jump teleports the clock but not your vessel's flight, and cuts engines
mid-burn (upstream issue #469). With others in your subspace that keeps you together; alone, there's nobody to keep up
with.

`TimeSyncPatch` (a Harmony prefix) leaves everything to LMP except one case: more than 3.5 s off **and** nobody else in
your subspace (`ClientSubspaceList`; LMP's own `AloneInCurrentSubspace` is commented out, and was inverted). Then it
does what LMP does when a time warp ends: `CurrentSubspace = -1`, then LMP's private `RequestNewSubspace`, so the
server makes a subspace at your game's own time (`WarpNewSubspaceMsgData`, the same messages as always) and LMP
switches you to it without reprocessing vessels. At most once every 30 s (`MoveInterval`); in between, nothing is
jumped and the game runs at normal speed. Any error inside the patch falls back to LMP's own behavior.

Trade-off: in a subspace behind someone else's newer one, the server doesn't save your vessel positions until you sync
up (LMP saves positions only from the newest subspace). That's LMP's normal state for anyone in a past subspace: every
player who doesn't follow a warp is in it already.

Rig check (Start-Rig.ps1 `-Slow`: 10 fps and 0.03 s of game per frame, so the game runs at about a third of real time;
a 60 s climb):

| | LMP alone | Patched |
|---|---|---|
| Clock jumps the game saw | **11**, each +3.6 s of game clock in 0.2 s, while the rocket gained 1-12 m (it should be ~360 m) | **0** |
| LMP's "Adjusted time" jumps in KSP.log | 12 | 0 |
| LUNA EVO subspace moves | - | 3 (13 before the 30 s limit) |

## Patch 3 (2.6): position messages carry the right time

The rig measured (Meat and Potatoes 26) that for a vessel under physics, the position in an LMP message is where the
vessel is **one physics tick after** the time stamped on it (+20 ms, 0.01 m rms). `StampPatch` (a Harmony postfix on
`VesselPositionMessageSender.CreateMessageFromVessel`) adds `Time.fixedDeltaTime` to `GameTime` for unpacked vessels.
Same message, same size, a better value, so unpatched players benefit too. Rig check: the best-fit offset went from
+20 ms to **0 ms**, and a second player now sees the vessel exactly LMP's intended 0.25 s behind.

## Next patches

Smoother remote vessels (W1 in [25](25-LMP-Under-The-Hood.md)): predicting ahead from the orbit and velocity LMP
already sends, instead of waiting and teleporting. It changes what each player sees, so it must be measured on a real
second screen: a second rig KSP (Start-Rig.ps1 can grow a `-Second` copy with hard links) with kRPC reading where it
shows the other player's vessel.
