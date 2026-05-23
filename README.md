# Dark Cloud Enhanced Mod

A community fork of the [Dark Cloud Enhanced Mod](https://github.com/Gundorada-Workshop/DarkCloud-Enhanced) originally created by Gundorada Workshop. This fork ports the mod to **.NET 8** and adds cross-platform support for **Windows, macOS, and Linux** via PINE IPC.

Enhanced Mod is a fan-made community project that brings new features and QoL changes for Dark Cloud. It runs as an external program alongside the PCSX2 emulator.

***To run Enhanced Mod you need .NET 8, PCSX2 v1.7 or later, and a North American (NTSC) version of Dark Cloud.*** The mod does not include the game — you must have your own legal copy.

For the full list of features and changes, see the original project's [Changelog](https://github.com/Gundorada-Workshop/DarkCloud-Enhanced/files/12431854/Full_Change_Log_Public_Release_v1.00.pdf).

---

## Running Enhanced Mod alongside PCSX2

Enhanced Mod communicates with PCSX2 using the **PINE IPC** protocol over a local TCP connection. You must enable PINE in PCSX2 before launching the mod.

### Prerequisites

- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed
- PCSX2 **v1.7 or later** (PINE is not available in v1.6)
- A North American (NTSC) ISO of Dark Cloud

### Enable PINE in PCSX2

1. Open PCSX2 and go to **Settings → Advanced**
2. Under the **PINE** section, enable **PINE Server** and set the port to 28011 (necessary for Windows users)
3. Save settings and **load your game** — PINE only starts once emulation is running

> **Platform note:** On Windows, PINE uses TCP port 28011. On macOS and Linux it uses a Unix domain socket (`$TMPDIR/pcsx2.sock`) — no port configuration needed.

### Install the PNACH file

The mod ships with a PNACH patch file (`A5C05C78.pnach`) that PCSX2 must load alongside the game. Copy it from the mod's `Resources/PNACH/` folder into PCSX2's cheats folder, then enable cheats in PCSX2.

| Platform | Cheats folder |
|----------|--------------|
| **Windows** | `%APPDATA%\PCSX2\cheats\` |
| **macOS** | `~/Library/Application Support/PCSX2/cheats/` |
| **Linux (native)** | `~/.config/PCSX2/cheats/` |
| **Linux (Flatpak)** | `~/.var/app/net.pcsx2.PCSX2/config/PCSX2/cheats/` |

After copying the file, enable cheats in PCSX2 via **System → Enable Cheats**. The mod window will show "PNACH File not active!" if this step is missed.

### macOS

1. Install .NET 8: `brew install --cask dotnet` (or download the installer from the link above)
2. Install PCSX2 via its [macOS release](https://pcsx2.net/downloads)
3. Copy `A5C05C78.pnach` to `~/Library/Application Support/PCSX2/cheats/` and enable cheats in PCSX2 (**System → Enable Cheats**)
4. Load your Dark Cloud ISO in PCSX2 and reach the main menu or in-game before starting the mod
5. Open Terminal in the folder containing `Dark Cloud Enhanced Mod.dll`. For a release download this is the extracted zip folder; if building from source it is `bin/Debug/net8.0/` inside the project. Run:
   ```
   dotnet "Dark Cloud Enhanced Mod.dll"
   ```
6. Click **Launch as User** in the mod window

> **Note:** macOS may require granting the terminal or app network access when prompted by the firewall.

### Windows

1. Install .NET 8 from the [Microsoft download page](https://dotnet.microsoft.com/download/dotnet/8.0) if not already present
2. Install PCSX2 from [pcsx2.net](https://pcsx2.net/downloads)
3. Copy `A5C05C78.pnach` to `%APPDATA%\PCSX2\cheats\` and enable cheats in PCSX2 (**System → Enable Cheats**)
4. Load your Dark Cloud ISO in PCSX2 and reach the main menu or in-game before starting the mod
5. Run `Dark Cloud Enhanced Mod.exe` (or `dotnet "Dark Cloud Enhanced Mod.dll"` from a terminal)
6. Click **Launch as User** in the mod window

### Linux

1. Install .NET 8 using your distribution's package manager, for example:
   - **Ubuntu/Debian:** `sudo apt-get install -y dotnet-runtime-8.0`
   - **Fedora:** `sudo dnf install dotnet-runtime-8.0`
   - **Arch:** `sudo pacman -S dotnet-runtime`
2. Install PCSX2 via your package manager or [Flatpak](https://flathub.org/apps/net.pcsx2.PCSX2): `flatpak install flathub net.pcsx2.PCSX2`
   - If using the Flatpak version, grant it network access: `flatpak override --user --share=network net.pcsx2.PCSX2`
3. Copy `A5C05C78.pnach` to `~/.config/PCSX2/cheats/` (native) or `~/.var/app/net.pcsx2.PCSX2/config/PCSX2/cheats/` (Flatpak) and enable cheats in PCSX2 (**System → Enable Cheats**)
4. Load your Dark Cloud ISO in PCSX2 and reach the main menu or in-game before starting the mod
5. Open a terminal in the folder containing `Dark Cloud Enhanced Mod.dll`. For a release download this is the extracted zip folder; if building from source it is `bin/Debug/net8.0/` inside the project. Run:
   ```
   dotnet "Dark Cloud Enhanced Mod.dll"
   ```
6. Click **Launch as User** in the mod window

### Troubleshooting

- **"Could not connect to PCSX2 via PINE"** — Make sure PCSX2 is running, PINE is enabled at slot 28011, and a game is loaded before starting the mod.
- **"PNACH File not active!"** — The PNACH file is missing from PCSX2's cheats folder, or cheats are not enabled. See the [Install the PNACH file](#install-the-pnach-file) section above.
- **Flatpak PCSX2 on Linux** — The Flatpak sandbox may block the PINE port. Run `flatpak override --user --share=network net.pcsx2.PCSX2` to allow it.
- **macOS firewall prompt** — Allow the mod to accept incoming connections when macOS asks.
