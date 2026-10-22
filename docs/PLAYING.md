# Playing

Find a server, get the mods, join, and play together without opening ports.

## Find a public server (server browser)

**Play → Server browser** lists every public LMP server, read from LMP's own master servers (the list LMP shows in
game). Filters: **Only my LMP version** (your KSP's LMP client; on by default, because the versions must match),
**No password**, **Has players**, and a search; sort by players, ping (the top 40 are pinged) or name. **Save** adds a
server to My servers. **Join** asks for the password if the server has one, fills the server into the Join page and
checks it, ready for **Launch KSP and join**. A server's own website link opens in your browser.

## Join a friend's server

1. Start LUNA EVO and choose **Join a friend's server** (or **Play → Join a server** in the rail).
2. Type the address your host gave you (e.g. `192.168.1.20` or `100.101.102.103:8800`), or paste their invite, and
   press **Check server**. You see where it is and your ping; server info, players and vessels appear if the host
   pressed **Share with players** on their Connect page.
3. Check the **Kerbal Space Program** folder (found automatically for Steam; your CKAN installs are one click away)
   and that your LMP version matches the server's. When Join knows the server's version (a pasted invite says it),
   it tells you if yours differs and offers **Install LMP x.y.z**. Need the server's mods? **Install the server's
   mods...** opens the `.ckan` list your host sent.
4. Type the password if there is one, and press **Launch KSP and join**.
   - KSP starts with the same options as CKAN's Play button, so your modded install runs exactly as usual.
   - Default port (8800), no password: LMP connects by itself.
   - Otherwise the server is at the top of LMP's server list with your password filled in: click it and connect.
     (LMP's command-line option can't take a port or a password; we plan to fix that in the LUNA EVO fork.)
5. **Save to my servers** to skip typing next time.
6. **Check my mods** (optional): LUNA EVO uses the rules LMP saved the last time you joined a server with mod
   control, or the file your host sent (Mod Control → Share these rules with players), and lists exactly what LMP
   would refuse.

## Using the CKAN page

Most groups use **CKAN** for their mods. Open **Play → CKAN mods**; the page has three parts, top to bottom.

**1. Which CKAN LUNA EVO uses**
- **Not installed?** Press **Download CKAN**: the official `ckan.exe` from CKAN's GitHub, checked against GitHub's
  SHA-256 and kept in `%LOCALAPPDATA%\LunaEvo\Tools\CKAN` (one copy per version, no mystery copies in Downloads).
- **Already have CKAN?** Press **Choose ckan.exe...** and pick yours. The page then says **Using your own CKAN** and
  whether it's an official build ("Official CKAN v1.36.4"). **Use LUNA EVO's copy** switches back.
- **Open CKAN** starts CKAN's normal window. If CKAN is already open, the page says so.

**2. Your KSP installs (from CKAN)**
- Every KSP install CKAN knows, with its LMP version, how many mods CKAN installed and how many you picked.
- **Export mod list...** saves the mods you picked as a `.ckan` file to send to friends or post for your server
  (without your Windows user name, which CKAN's own file contains). Leave **Exact versions** on so everyone gets the
  same versions; turn it off to let CKAN pick the newest compatible ones.
- **Use as mod list** copies that install's mods into the box below, to install them into another KSP install.

**3. Install a mod list**
1. Close KSP, and close CKAN's window if it has that install open.
2. Put the list in the box: **Load a .ckan file...** (a modpack from a friend, CKAN's **File → Export modpack**, or
   one exported above), paste a `.ckan` file's text, or type mod names one per line (`#` starts a comment).
3. Pick the KSP install in **Into this KSP install**.
4. Optional: **Check with CKAN** works the list out without installing anything (CKAN is told "no" when it asks to
   continue): it says whether everything can be installed, or which mod it doesn't know. If a mod needs "one of
   several" others (Parallax needs a KSPBurst, Scatterer a sun flare), LUNA EVO asks you which, using CKAN's own options.
   **Install** asks the same before it starts, because CKAN's command line would silently leave those out.
5. Press **Install with CKAN** and confirm. CKAN refreshes its catalog, works out the dependencies and downloads
   everything, with its messages underneath. If a mod is unknown or doesn't fit your KSP version, CKAN stops and
   undoes that install, and the page says why.

**Cancel** stops CKAN while it runs (an install asks first), and **Copy log** copies CKAN's messages for a bug report.
Only mod names and versions are read from a list. Anything else in the file, such as a download link, is ignored.
Setting up a server? Step 3 of the setup wizard can get CKAN and the LMP client for you.

**From a host:** **Connect → Export my mod list...** saves your CKAN mods as a `.ckan` file. Players press **Join →
Install the server's mods...**, open it, and the CKAN page loads it for their KSP and checks it.

## Play together without opening any ports (Tailscale, Twingate)

The safest way to host for friends is a **mesh VPN**: everyone's PC joins a private network that works over the
internet, so friends connect to your server as if they were on your LAN. **Nothing is opened on your router**, and
nobody outside your mesh can even see the server. This is how we run ours.

**Tailscale** (easiest, free for personal use):

1. Install [Tailscale](https://tailscale.com/download) on the server PC and sign in.
2. Each friend installs Tailscale too, and you invite them to your tailnet (admin console → *Share* the server PC, or
   add them as users).
3. On the server PC, open LUNA EVO's **Connect** page: the Tailscale address (`100.x.y.z`, labeled VPN or mesh) is
   in *Addresses to share*. Press **Copy** and send it to your friends. With MagicDNS on, the PC's name works too.
4. Friends use that address on LUNA EVO's **Join a server** page (it shows "VPN or mesh network") or in LMP's own
   server list.

**Twingate** works too: run a Twingate **Connector** on your home network, add the server PC as a **Resource**
(allow UDP 8800 for the game, and TCP 8900 if you share live info), and give your friends access. They connect to the
Resource's address with the Twingate client running.

**Then keep the server private.** In LUNA EVO:

| Setting | For a mesh server |
|---|---|
| Connection → **Upnp** | **Off.** Nothing needs opening, and UPnP would open ports to the whole internet. LUNA EVO shows a "reachable from the internet" banner if it's on. |
| Master Server → **Register With Master Server** | **Off.** Friends join by address; the public list is for strangers. |
| Connect → **Allow through firewall** | Press it once. Windows Firewall still applies to mesh traffic. |
| Connect → **Share with players** (optional) | Lets friends' Join page show live info (players, vessels) over the mesh, on TCP 8900. |
| General → **Password** | Still worth setting a long one (**Generate a strong password**), as is a long admin password. |

Mesh addresses are private to your group, so they're fine to share with friends, but they're still yours: don't post
them publicly.
