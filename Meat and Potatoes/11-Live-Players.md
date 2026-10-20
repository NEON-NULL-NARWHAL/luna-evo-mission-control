# 11 · Live players

[← Meat and Potatoes](README.md) · Code: `Core/Live/ServerInfoClient.cs`, `App/PlayersPage.cs` 🔌

## Where the data comes from

LMP has a small built-in web server (`Server/Web/WebServer.cs`), on when `WebsiteSettings.EnableWebsite` is true.
`GET http://<ListenAddress>:<Port>/` returns a **JSON array with one object**:

```json
[{
  "CurrentState": {
    "StartTime": "2026-10-13T03:59:54",
    "CurrentPlayers": ["Jebediah", "Valentina"],
    "CurrentVessels": [{ "Id": "...", "Name": "Kerbal X", "Type": "Ship", "Alt": 75.2, "ReferenceBody": 1, ... }],
    "Subspaces": [{ "Id": 72, "Time": 681407.49, "Creator": "Server" }],
    "BytesUsed": 104910848
  },
  "GeneralSettings": { "ServerName": "...", "MaxPlayers": 20, ... },
  "WarpSettings": { ... }, ...
}]
```

It refreshes every `RefreshIntervalMs` (5 s by default). `tests/Fixtures/Web/serverinfo.json` is a synthetic copy of
the real shape.

## The pieces

| Member | Notes |
|---|---|
| `ServerInfoClient.EndpointFor(session)` | `null` when the website is off. "Any" addresses (empty, `::`, `0.0.0.0`) become `127.0.0.1`; a specific address is used as-is. |
| `GetAsync` / `Parse` | Players (names), vessels (name, type, altitude, body index), subspaces, memory, max players, server name. |
| `Diff(before, after)` | Who joined and who left, for the Discord notices. |
| `PlayersPage` | Polls every 5 s while a server is running; cards (players / max, vessels, asteroids & comets, subspaces, memory); a row per player with **Kick** / **Ban** (sends `/kick name reason` / `/ban name reason`); vessels with type, body and altitude (asteroids and comets are counted, not listed). |

`ReferenceBody` is KSP's body index: 0 Sun, 1 Kerbin, 2 Mun, 3 Minmus, 4 Moho, 5 Eve, 6 Duna, 7 Ike, 8 Jool, 9 Laythe,
10 Vall, 11 Bop, 12 Tylo, 13 Gilly, 14 Pol, 15 Dres, 16 Eeloo. Modded planet packs add more (shown as "?").

## When the website is off

The page says so and offers **Turn on the website (this PC only)**: it sets `EnableWebsite = true` and
`ListenAddress = 127.0.0.1`, which takes effect after Save and a restart.

## Limits

- Kick/Ban only work when LUNA EVO started the server (it needs the server's console).
- LMP stores bans by player ID in `LMPPlayerBans.txt`, not by name, so there's no unban-by-name yet.
