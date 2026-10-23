# 27 · LUNA EVO patches for the LMP client (2.5; 2.6: three; 2.7: five, plus two in the server plugin)

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
Same message, same size, a better value, so unpatched players benefit too. It also applies to LMP's one-off sends
(decouple, undock, vessel switch), which the rig hasn't measured: at most one tick on single messages (audit 11, K2). Rig check: the best-fit offset went from
+20 ms to **0 ms**, and a second player now sees the vessel exactly LMP's intended 0.25 s behind.

## 2.7: network speed (audit 11's P3 and P1), measured

Both sides of each fix: the client patches in KSP, and the LUNA EVO server plugin (1.1, which now brings Harmony and
lives in `Plugins/LunaEvo/`). Each can be switched off for a test with `LUNAEVO_PATCHES_OFF="P1,P3"`
(`Start-Rig.ps1 -PatchesOff`); players never set it.

**P3, 1 ms timers** (`TimerResolution.cs`, shared): `timeBeginPeriod(1)`, plus `SetProcessInformation` asking Windows 11
not to ignore it for a hidden or background process. On anything but Windows it does nothing. Rig, one KSP flying and a
second player watching, every other patch on:

| | P3 off | P3 on |
|---|---|---|
| Time from one game to the other (median) | 29 ms | **22 ms** |
| Worst | 82 ms | **46 ms** |

What's left is mostly the frame: LMP sends at the end of a frame, so at the rig's 30 fps cap a position waits up to
33 ms before it's sent.

**P1, a channel per vessel** (`LmpChannels.cs`, shared; `ChannelPatch` in KSP, `ServerChannelPatch` in the plugin):
a postfix on `VesselCliMsg`/`VesselSrvMsg.DefaultChannel` puts each vessel's fast updates (position, flight state,
update, resource) on channel 1-31 by vessel and kind instead of all on 0. Measured without KSP: the fake client flies
20 vessels at 20 updates a second each and counts what it sent; `tests/NetJitter` (a UDP relay) delays every packet
20-120 ms so they overtake each other like online (Lidgren's own simulation is compiled out of release builds, so LMP's
`-debug` can't do this); the fake observer counts what arrives:

| Updates lost | LMP | With P1 |
|---|---|---|
| Player → server (client patch) | **62.5%** | **18.2%** |
| Server → player (server plugin) | **62.7%** | **18.6%** |

The 18% left is sequencing doing its job: an update dropped because a newer one of the **same** vessel arrived first.
In KSP: a run with every patch on had the second player receive the rocket's positions on the new channels as before.

**P4, far vessels less often: measured, not shipped.** A prefix that moved unloaded remote vessels one tick in ten,
with 135 vessels moved by the fake fleet (`FakeLmpClient --fleet`) and the frame cap off: 106 fps without, 101 fps with,
within noise. Updating far vessels every tick is cheap here; upstream #547 has another cause. A patch with no measured
benefit is only risk, so it was removed.

## 2.7.1: P6, P2 and P7 looked at; none shipped

**P6 (flight state only on change):** already in LMP 0.29.2 (`VesselFlightStateMessageSender`: sent when the controls
change, plus every 500 ms). Audit 11 had it wrong.

**P2 (reliable vessel messages per vessel):** not safe as a channel patch; details and measurements in
[25](25-LMP-Under-The-Hood.md#client-to-client-performance-audit-11-2026-10-22). Possible later as client patch + server
plugin together.

**P7 (smoother remote vessels): measured with two real KSPs, not shipped.**

The rig now has a second KSP (`TestRig/KSP2`, a copy with kRPC on 50002, player RigViewer; `Start-Rig.ps1 -Viewer`), joined
through NetJitter (`-ViewerJitter "20,120,2"`: 20-120 ms delay, 2% lost). `RigRunner --viewer --drive` measures both ways
in one flight: the viewer's game showing the pilot's rocket, and the pilot's game showing the viewer's buggy (which drives
out of the runway's safety bubble first: inside it LMP hides it, and the rocket counts it as far away and sends every
150 ms instead of 50-80). Lessons from setting it up:

- LMP names other players' vessels by KSP's localization key there (`#autoLOC_501232`), not "Kerbal X".
- The Bug-E Buggy has no crew (a probe rover): find it by name, not by crew.
- Asking kRPC nonstop slowed the viewer's KSP below real time, and LMP then jumped its clock: the numbers were garbage.
  The viewer now pauses 10 ms between looks, and every run reports LMP time jumps during the flight in both games.
- kRPC only focuses the camera on another vessel in map view.

What LMP does today, with a clean network: the other player's rocket is shown a steady 0.27-0.30 s behind, within about
15 ms. With jitter it stops now and then (LMP holds a vessel at its last update until the next one arrives), shown 3.5-9.7%
of looks standing still, wobble 80-151 ms (95%).

The patch carried a held flying vessel on along its last segment (LMP's blend isn't clamped, so a blend past 1 extrapolates).

| Version | Result |
|---|---|
| 1: carry on, then start the next segment as far in as it was carried | Better in two runs (still 8.1% → 2.0%; wobble 133 → 69 ms), but in a third it went **backwards** 5.1% of looks and ran 80 ms ahead of LMP's schedule: LMP already shortens a late segment to catch up, and skipping frames on top of that overshot |
| 2: carry on, glide on from where it's shown, or keep it there until LMP passes it | Never backwards in a model of LMP's playback (unit-tested), but in the game: still 21% of looks, wobble 911 ms (95%), against 9.7% and 151 ms without |

LMP's playback (whole-tick segments, catch-up by shortening, holds) reacts to any nudge in ways a small patch can't
control. Doing this well means replacing how remote vessels are played back (by time, with prediction), not adjusting
it: a bigger piece of work, for the fork or a larger patch. Run-to-run noise is large too (the buggy, which P7 never
touched, varied 136-318 ms): judge only on several pairs of runs.

## Next patches

Plumes of other players' engines (LMP syncs the throttle and ignition, not the engines' actual output), the safe version
of P2 (client patch + server plugin), and P5 interest management in the server plugin.

**Plumes, first check (stock KSP, 2026-10-22):** mid-flight, the rocket's engines read through kRPC in both games matched:
the same engines active, throttle 1.00 on the burning ones, 0.00 and no fuel on the two burned-out boosters in both, and
thrust within 2% (the viewer's copy is 0.28 s behind, at a slightly different air pressure). So stock plumes have what
they need on the other player's screen. Still to check: throttle changes and shutdowns (timing), and Waterfall (used in
the owner's modpack; reads the engine values differently). Waterfall only goes into the rig's KSP copies, after checking
its license.

**Plumes, seen (stock KSP, 2026-10-22):** the viewer spectated the rocket (LMP lets a player switch to another player's
vessel; kRPC: `ActiveVessel = rocket` on the viewer, then `Camera.Pitch`/`Distance` to see the engines past the navball)
and the window was captured without taking focus (`PrintWindow` with `PW_RENDERFULLCONTENT`). The pilot cut the throttle
and put it back: on the viewer's screen the flame went out and came back, and the engines read the same in both games at
every step (throttle 0.00 → 1.00, thrust equal). Stock plumes work across players. Next: the same check with Waterfall.

## 2.8: the in-game LUNA EVO window

A button on KSP's toolbar (a green planet with an orbit, drawn in code: the patch stays one DLL) opens a window, in the
Space Center, flight, map and tracking station. Owner's picks (2026-10-22):

- **Connection:** server (an IPv4-mapped address shown as plain IPv4), ping, subspace and how far behind the latest
  player, and every other player with their vessel and status. LMP sends stock vessel names as KSP translation keys
  (`#autoLOC_501232`); the window runs them through `KSP.Localization.Localizer.Format` ("Kerbal X"). LMP's own list
  doesn't.
- **Quick actions:** "Sync my clock to the latest player" (`WarpSystem.SyncToSubspace`, what LMP's status window's sync
  button calls; only when not warping and someone is ahead by more than a second). "Watch <player>: <vessel>" for each
  loaded vessel another player holds the control lock of (`LockSystem.LockQuery.GetControlLockOwner`), via
  `FlightGlobals.SetActiveVessel`, which LMP turns into spectating; "Back to my vessel" afterwards.
- **LMP settings:** player name (LMP changes it only while disconnected), "New random color" (exactly what LMP's
  options window does on Set: save, `SendPlayerColorToServer`, `StatusWindow.ColorEventHandled = false`), "Ignore LMP's
  sync checks", and buttons that open LMP's chat, players and options windows.

Nothing new goes over the network: every action is a call LMP's own windows make.

**Building it without KSP:** the window needs more of KSP and Unity than the patches did, so the stand-ins grew
(`Stubs/AssemblyCSharp`: toolbar, `GameEvents`, `FlightGlobals`, `Localizer`; `Stubs/UnityCoreModule`: `Color`,
`Rect`, `Vector2`, `Texture2D`, `Screen`; new `Stubs/UnityIMGUIModule`: `GUI`, `GUILayout`). Each member is copied from
the real assembly's metadata, and `ClientPatchTests.Every_stand_in_member_is_in_ksp_with_the_same_shape` loads every
built stand-in and the rig's real KSP and fails on any difference in name, static-ness, struct/class, types or enum
values (checked to fail: a `long` instead of an `int` was caught). It needs the rig, so it runs locally.

**What the game taught us** (each found in the rig; KSP and LMP kept running, the button was just missing):

- At `KSPAddon.Startup.Instantly`, `GameEvents`' events don't exist yet: the loader adds the window from `Update` once
  they do.
- `EventVoid.Add` throws on a static method (KSP's wrapper reads the delegate's target): the loader registers instance
  methods that forward to `EvoWindow`.

Rig: `Start-Rig.ps1 -OpenWindow` sets `LUNAEVO_OPEN_WINDOW=1`, which opens the window through its own button (kRPC
can't click KSP's toolbar); players never set it, and it can only open the window.

## 2.9.1: what the owner's real two-player game showed

Watched live (read-only: the KSP and server logs, the server's LMP website, window captures that don't take focus), the
owner and a friend on the owner's server, 2026-10-22:

- **The time fix split them.** Its rule was "nobody else in my subspace". After any short warp, the other player is in
  another subspace, so the rule held, and every frame-rate dive (loads, vessel spawns: 0-4 fps) moved the slower player
  into a subspace of their own, behind. Whoever was behind couldn't see the other until they synced. Now the time fix
  steps in only when **nobody else is on the server** (`TimeSyncPatch.AloneOnServer`); with anyone online, LMP's jump
  keeps players together.
- **Stay in step** (`EvoWindow.KeepInStep`, every 2 s from `PatchLoader.Update`, on by default, switch in the window):
  another player 1-60 s ahead and this game not warping → `WarpSystem.SyncToSubspace`, what LMP's sync button does.
  More than 60 s is someone's deliberate warp: the window offers the button instead. The owner's idea ("force the
  update every couple of seconds unless in warp").
- Rig check (two KSPs): the viewer frozen 6 s with kRPC `HoldTick` got LMP's own correction (-7.2 s, no LUNA EVO
  subspace); after the pilot physics-warped, the viewer logged "23.6 s behind subspace 1: syncing to stay in step" and
  synced.

Also seen, not ours: "Malformed packet" warnings from Lidgren (also in logs from before the patches existed); they
stopped when the friend reconnected. The other player's vessel is hidden inside the 100 m safety bubble, and loaded
only within ~2.2 km (a marker beyond).

## 2.10: the LUNA EVO patches page

`PatchesPage` (rail: LUNA EVO → LUNA EVO patches) lists `Core/Join/PatchCatalog.All`: every fix, where it runs (KSP or
server), what it does for the player, what the rig measured and since when. Keep the catalog in step with this chapter
and 26 when a patch changes. Installing reuses the existing steps: `JoinPage.TogglePatchesAsync` for the KSP in Join's
folder box (it now keeps `LastPatchMessage` for the page) and `MapPage.TogglePluginAsync` for the open server, with the
same questions and checks (KSP closed, server stopped, LMP 0.29 only).
