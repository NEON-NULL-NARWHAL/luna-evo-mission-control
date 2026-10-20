# 16 · Player history, alerts and bans

[← Meat and Potatoes](README.md) · Code: `Core/Live/PlayerTracker.cs`, `Core/Live/BanList.cs`, `App/PlayersPage.cs`, `App/ServerPage.cs` (feeds lines)

The Players page shows who's online *now* ([11](11-Live-Players.md)). Since 0.5 it also remembers **who played, when
and for how long**, raises **alerts**, and manages **LMP's ban list**.

## Where the data comes from

LMP has no player database, so `PlayerTracker` builds one from two things LMP already gives us:

| Source | What it gives | Notes |
|---|---|---|
| **Log lines** (`ServerPage.Flush` → `Tracker.Observe`) | Joins with LMP's unique id and the client's LMP/KSP versions, kicks, bans, banned players refused, wrong admin passwords, some leaves | Works for a server LUNA EVO runs *or* follows (`LogTail`). Lines replayed from an old log file (`LogLine.Replayed`) are ignored, so restarting the app doesn't count yesterday again. |
| **Website player list** (`PlayersPage.PollAsync` → `Tracker.ObserveOnline`) | Joins and leaves, every 5 s | LMP logs normal leaves only at **Debug** level, so this is the reliable leave signal. |
| **Server stopped** (`Tracker.ServerStopped`) | Ends every open session | From `OnExitedAsync` and when a followed server exits. |

The exact LMP 0.29.2 messages are the regexes at the bottom of `PlayerTracker.cs`. All of them were checked against a
real server's logs (every join, leave and kick line matched).

| LMP source | Message |
|---|---|
| `HandshakeSystem` | `Client <name> (<id>) handshake successful, LMP Version: <v>, KSP Version: <v>` |
| `HandshakeSystem` | `Client <name> (<id>) failed to handshake: Banned. Disconnecting` |
| `BanPlayerCommand` | `Player '<name>' was banned from the server: <reason>` |
| `KickCommand` | `Kicking <name> from the server` |
| `AdminMsgReader` (Warning) | `<name>: Tried to run an admin command with an invalid password` |
| `ClientException` / `ClientConnectionHandler` (Debug) | `Client <name> disconnected in ...` / `<name> sent Connection end message` |

⚠️ **Trust:** names are matched with LMP's own rule (`A-Z a-z 0-9 - _`, `HandshakeSystemValidator`), and **chat lines are
never read** (players type them). A modified client could still put a line break in a chat message to fake a log line;
the worst that does is a wrong history entry or a false alert. **IP addresses are never stored**, although LMP's
disconnect lines contain them.

## Stored where

`<server>/LunaEvo/players.json` (next to the server, like profiles and backups): players (name, LMP ids, first/last
seen, sessions, minutes played, versions, wrong admin passwords, ban reason, your note) and the last 1000 events.
Written to a temp file and moved into place; a damaged file starts over instead of stopping the app.

## Alerts

- **Admin password guessing:** 3 wrong admin passwords from one player within 10 minutes. One alert per player per
  window, however fast they guess, and individual guesses are counted, not logged as events (a brute-force run would
  otherwise flood the history). LMP doesn't limit guesses itself ([07](07-LMP-Gotchas.md) #32).
- **Banned player tried to join.**

Alerts show at the top of the Players page for 24 hours (with Kick/Ban if the player is still online) and are
always notified ([17](17-Notifications.md)).

## Bans

LMP's `/ban` writes the player's **unique id** (not their name) to `<server>/LMPPlayerBans.txt`, and there's no
`/unban`. `BanList` reads that file; the page shows names by looking the id up in the history. **Unban is only enabled
while the server is stopped**: LMP keeps the list in memory and rewrites the file on the next ban, which would bring
the removed id back. The old file is copied to `LunaEvo/Backups/` first.

## Tests

`PlayerFeaturesTests` (Core) and `PlayerFeaturesUiTests` (UI). The fake server has test-only commands that print the
real LMP lines: `/fakejoin <name>`, `/fakeguess <name>`, `/fakeleave <name>`.
