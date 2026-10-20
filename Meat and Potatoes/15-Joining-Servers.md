# 15 · Joining servers (the player side)

[← Meat and Potatoes](README.md) · Code: `Core/Join/ServerProbe.cs`, `Core/Join/KspLauncher.cs`, `App/JoinPage.cs`, `App/ConnectPage.cs`

Everything else in LUNA EVO is for the host. **Join a server** (rail → Play) is for everyone else: check a friend's
server before joining, then start KSP straight into it. It works with no server open, from the welcome page's
"Join a friend's server".

## What the page shows

| Group | Source |
|---|---|
| **Server** | The address box. `ServerAddress.Parse` takes `host`, `host:port`, `[ipv6]:port` and `lmp://host:port`. The live info port (default 8900) is separate, because it's TCP and the game port is UDP. |
| **My servers** | `AppSettings.SavedServers` (`SavedServer(Name, Host, Port, WebsitePort)`). Named after the server when its live info answered. **Passwords are never saved.** |
| **Network** | `ServerProbe.ResolveAsync` (DNS) labelled by `ServerProbe.Kind`: This PC, Local network, VPN or mesh (`100.64.0.0/10`, Tailscale/CGNAT), Internet. `PingAsync`: 4 ICMP pings, average and best. "No replies" doesn't mean the server is down, since many PCs drop pings. |
| **Server info** | `ServerProbe.LiveInfoAsync`: `GET http://host:8900/`, the same LMP website JSON as the Players page ([11](11-Live-Players.md)), 5-second timeout. Name, players/max, mode and difficulty, password yes/no, mod control, warp mode, description. |
| **Players online / Vessels in game** | From the same JSON. Asteroids and comets are left out (`Ui.ListedVessels`); vessel lines use the shared `Ui.DescribeVessel` (type · body · altitude). |
| **Play** | KSP folder (found by `KspLauncher.FindKsp` in your Steam libraries, or remembered from last time), the LMP client version (`LmpClient.dll` ProductVersion), password, **Launch KSP and join**, **Add to LMP's server list**. |

"Refresh every 10 seconds" re-runs the whole check. `CheckAsync` catches everything, because it runs from button, Enter and timer handlers.

### Live info needs the host to share it

LMP's website listens on `127.0.0.1` by default, so players get "No live info". The host's **Connect** page has a
**Share with players** row: it confirms, then sets `EnableWebsite = true` and `ListenAddress = ""` (all addresses), and
offers a TCP firewall rule. **Stop sharing** puts back `127.0.0.1`. The host must save and restart the server.

## Direct connect: what LMP can and can't do

We read LMP's client to see how a game can be started *into* a server
(`LunaMultiplayer/LmpClient/MainSystem.cs`, around line 527):

- `KSP_x64.exe -lmp lmp://host` makes LMP connect by itself once KSP reaches the main menu.
- **With a port it's broken.** For `lmp://host:9000` LMP does
  `address.Substring(6).Substring(0, address.LastIndexOf(":"))`: the index comes from the *unsliced* string, so the
  host is cut wrong (or it throws), and the port is then parsed from what's left, which no longer contains it.
  Only the default port **8800** works, by leaving the port out.
- **There's no password option.** A passworded server can't be joined from the command line.

So `KspLauncher.PlanFor(server, hasPassword)` picks one of two plans:

| Plan | When | What happens |
|---|---|---|
| `AutoConnect` | Port 8800 and no password | `KSP_x64.exe -lmp lmp://host`. KSP opens and joins. |
| `PickFromList` | Any other port, or a password | No arguments. The server (with its password) goes to the **top of LMP's own server list**, and the player clicks it. |

Both plans also add the server to LMP's list, so it's there next time.

### LMP's server list

`GameData/LunaMultiplayer/Data/settings.xml`, root `<SettingStructure>`, with
`<Servers><ServerEntry><Port/><Name/><Address/><Password/></ServerEntry>...`. It has the same lying `utf-16`
declaration as the server's files, so it's edited with `ConfigFile` (in place, CRLF kept; see [07](07-LMP-Gotchas.md)).
`KspLauncher.AddToServerList`:

- refuses while KSP is running (LMP rewrites the file on exit and would undo the change);
- refuses if the file doesn't exist yet (LMP creates it on the first start of KSP with the mod);
- copies it to `settings.xml.lunaevo-backup` before each change;
- updates an entry with the same address and port, or inserts at the top;
- keeps the saved password if none is given.

### The fix for the LUNA EVO fork

This is a good first change for our LMP fork: parse `lmp://host:port` properly (from the sliced string, IPv6 in
brackets) and add `-lmppassword`. Then `PlanFor` can always return `AutoConnect` when the client is a LUNA EVO build.

## Testing it

- `JoinTests` (Core): address parsing and rejection, network kinds, real resolve and ping of 127.0.0.1, live info and
  its errors via `FakeHttp`, the launch plans, and the server list round trip on a fake KSP (`TestData.FakeKsp`).
- `JoinPageTests` (UI): check, save/remove, auto-connect launch, the password path (and that the password isn't saved),
  missing KSP or LMP, the host's Share flow, and `Ui.CanOpen`.
- `JoinPage.StartKsp` is the one seam that starts KSP. `UiHarness` makes it **throw** unless a test replaces it, so no
  test can ever start a real game.
