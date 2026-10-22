# Hosting a server

Set up a new LMP server with LUNA EVO, step by step.

## Set up a new server, step by step

**1. Start LUNA EVO and choose "Set up a new server".**
Run the exe from anywhere (your Desktop or Downloads is fine). With no server open, you get the welcome page.

![Welcome page](../docs/screenshots/welcome.png)

**2. Choose where the server lives and which LMP version.**
- **Folder:** a new or empty folder, e.g. `D:\Games\LMPServer`. The server, its settings and your game universe all live here.
- **LMP version:** the newest stable release is picked for you, straight from Luna Multiplayer's GitHub.
  **Everyone who plays needs the same version of the LMP mod in their KSP**, so note the number.
- **Show pre-releases:** leave it off unless you want to test newer, less-tested versions.

![Setup wizard, step 1 and 2](../docs/screenshots/wizard.png)

**3. Answer the questions about your server.**

| Question | What to pick |
|---|---|
| Server name | Anything up to 30 characters. Players see it when they connect. |
| Password | Optional. Players type it to join. |
| Game mode | **Sandbox** (everything unlocked), **Science** (tech tree) or **Career** (tech tree, money and contracts). |
| Difficulty | Easy / Normal / Moderate / Hard. You can fine-tune each rule later on the **Gameplay** tab. |
| Max players | How many people can be on at once. |
| Game port | Leave it at **8800** unless you know you need another. |
| Show on the public server list | **Off** for a server for friends (they join with your address). On to be listed in the server browsers. |
| Let LMP open router ports (UPnP) | **Off** if everyone is on your network or a VPN/mesh. On to let players from the internet in (if your router allows it). |
| Check players' mods (Mod Control) | **Off** lets players use any mods. On restricts them to the parts and plugins you allow (see **Mod Control → Import from GameData**). |
| Live player info | Leave it **on**: it powers the **Players** page, and only this PC can see the data. |
| Put LUNA EVO in the server folder | Leave it **on**, so next time you just double-click LUNA EVO in that folder. |

![Setup wizard, options](../docs/screenshots/wizard-options.png)

**4. Press "Download and set up".**
LUNA EVO downloads the LMP server, unpacks it and writes your settings **before** the server ever starts, so nothing
is public until you say so. LMP 0.29.x needs the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0);
if this PC doesn't have it, LUNA EVO offers to install it (Windows asks for permission). Then the new server opens on
the **Server console**.

![The new server, ready to start](../docs/screenshots/wizard-opened.png)

**5. Press Start.**
The console fills with LMP's startup messages. When you see **"All systems up and running"**, the server is ready, and
the dot next to Server console in the rail shows it's running. **Stop** shuts it down cleanly: LMP saves the universe.

![First start](../docs/screenshots/first-start.png)

**6. Tell your friends how to join.**
Open **Connect**. It lists the addresses to share (on your network, VPN or mesh: copy one of these), whether you're on
the public list, and whether anything is blocking players. If Windows Firewall might block them, press **Allow through
firewall** (Windows asks for permission once). **Copy invite** gives you a message to send; friends paste it into
LUNA EVO's Join page, or add the address in LMP's own server list.

![Connect page](../docs/screenshots/connect.png)
<sub>The address shown is an example; your own is filled in automatically. Your internet address stays hidden until you press Show.</sub>

**7. Make it yours (optional).**
**Server settings** has the rules (Essentials first, then a tab for every LMP file), **Mod Control** imports your mods,
**Profiles & backups** saves setups and snapshots the universe, and **App settings** has automatic restarts, scheduled
backups, notifications, updates and the app's colors. **Save**, then restart the server for changes to take effect.

**What your players need:** KSP 1.12 with the **LMP client mod in the same version** as your server. LUNA EVO installs
it for them (App settings → Downloads), or it's on [LMP's releases page](https://github.com/LunaMultiplayer/LunaMultiplayer/releases)
(`LunaMultiplayer-Client-Release.zip`).
