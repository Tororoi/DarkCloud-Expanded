# Dark Cloud Enhanced Mod

A community fork of the [Dark Cloud Enhanced Mod](https://github.com/Gundorada-Workshop/DarkCloud-Enhanced) originally created by Gundorada Workshop. This fork ports the mod to **.NET 8** and adds cross-platform support for **Windows, macOS, and Linux** via PINE IPC.

Enhanced Mod is a fan-made community project that brings new features and QoL changes for Dark Cloud. It runs as an external program alongside the PCSX2 emulator.

***To run Enhanced Mod you need .NET 8, PCSX2 v1.7 or later, and a North American (NTSC) version of Dark Cloud.*** The mod does not include the game — you must have your own legal copy.

For the full list of features and changes, see the original project's [Changelog](https://github.com/Gundorada-Workshop/DarkCloud-Enhanced/files/12431854/Full_Change_Log_Public_Release_v1.00.pdf).

For changes made in this fork, see [CHANGELOG.md](CHANGELOG.md).

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

---

## Testing Needed

### Platform

- [ ] **Running on Windows** — Launch mod, connect to PCSX2, confirm PINE handshake and basic dungeon loop
- [ ] **Running on Linux** — Same as above; verify Unix socket path and Flatpak network override if applicable

<details>
<summary>⚠️ **Mod Spoilers Ahead** — click to expand</summary>

<details>
<summary>Custom Weapon Effects</summary>

- [ ] **Cactus** — Equip in dungeon: hit enemy and observe water absorbed at a rate of 1 drop per 100 damage.
- [ ] **Heaven's Cloud** — Confirm 50% gooey proc triggers on hit
- [ ] **Aga's Sword** — Equip: Confirm Toan gains +15 defense. Take hits that would change defense externally: verify boost is re-applied if given fluffy doughnut. Unequip: verify +15 is removed
- [ ] **Brave Ark** — Equip in dungeon: get hit with Freeze, Poison, Curse, or Goo — confirm each is cleared within the polling interval. Stamina (not in resist mask) should still apply
- [ ] **Frozen Tuna (Goro)** — Equip in dungeon: WHP loss heals Goro HP proportionally at 2x factor from banked hp pool. On hit, 5% chance stops all non-ice enemies and freezes Goro. Blizzard, Sam, and Ice Gemron are immune to the stop proc

</details>

<details>
<summary>Miniboss System</summary>

- [ ] **Stat multipliers** — Kill a miniboss and confirm HP, ABS reward, and Gilda drop are roughly 4× the base enemy values (up from 3×)
- [ ] **Flavor rare drops (5%)** — Each dungeon has per-enemy rare drops; verify correct items drop (e.g., Evilcise from Yammich in DBC, Bandit Slingshot from Days of the Week enemies in WOF)
- [ ] **Flavor common drops (30%)** — Verify themed items drop from the correct enemies per dungeon
- [ ] **Weapon stat boosts on pickup** — When a miniboss drops a boosted weapon (e.g., Gladius in DBC, Frozen Tuna in Moon Sea), pick it up and confirm the preset stats are written to the weapon slot
- [ ] **Boost monitor cancels on floor change** — Do not pick up the weapon drop; advance to the next floor and confirm no orphaned boost thread is running

</details>

<details>
<summary>Mardan Sword Rework</summary>

- [ ] **Detection from bag/storage** — Place a Mardan sword in bag or storage (not equipped): confirm fishing FP multiplier is still applied
- [ ] **Multiplier values** — Eins: 1.2×, Twei: 1.5×, Arise: 2× FP gain
- [ ] **Mardan Twei Garayan bonus** — Own Mardan Twei or Arise Mardan; confirm a second independent roll for Garayan fish occurs and appears in the log
- [ ] **Arise Mardan size scaling** — Own Arise Mardan; enter a fishing area and confirm the console logs scaled size values. Fish that rolled near their max size should visibly increase; fish that rolled near their minimum should change very little
- [ ] **No Mardan sword owned** — Confirm FP multiplier is not applied and `hasMardanSword` is false

</details>

<details>
<summary>Fishing — Fish Steering</summary>

- [ ] **Mardan Eins steering** — Own Mardan Eins; cast a bait with affinity for Garayan (Poisonous Apple) or Umadakara (Carrot): confirm those fish visibly steer toward the player and `[Steer/Mardan]` appears in the log at an interval matching the affinity weight
- [ ] **No steering for non-Garayan/Umadakara** — Confirm that common fish (Gobbler, Nonky, etc.) are not steered by the Mardan Eins ability; only Mardan Garayan, Baron Garayan, and Umadakara should be affected
- [ ] **Bait Detection** - Confirm bait detection works when switching bait multiple times in same fishing session.

</details>

<details>
<summary>Fishing Quests</summary>

- [ ] **Pike (Norune, area 0)** — Accept a count quest or size-range quest from Pike; catch the required fish and confirm the quest completes (state byte → 2) and the dialogue reflects completion
- [ ] **Pao (Matataki, area 1)** — Same as above for Pao's quest
- [ ] **Sam (Queens, Area 19)** — Complete one of Sam's quests; confirm the multi-quest counter increments and, after the required number, the post-loop queens-quest trigger fires
- [ ] **Devia (Muska Lacka, Area 3)** — Accept and complete Devia's quest; confirm normal count/size completion flow

</details>

<details>
<summary>PINE Connection Recovery</summary>

- [ ] **Graceful disconnect** — While the mod is running and connected to PCSX2, close the emulator (or stop emulation). Confirm the mod does not crash and logs `[PINE] Stream closed — emulator disconnected.` or `[PINE] IO error — connection lost:`

</details>

<details>
<summary>Weapon Stat Changes</summary>

- [ ] **Bone Slingshot / Hardshooter** — Confirm each has a 50% chance to generate with the Fragile effect on a new game/weapon load
- [ ] **De Sanga** — Confirm 30% chance to generate with the Drain effect
- [ ] **Frozen Tuna base stats** — Confirm max attack 100, max MP 678, Stop effect, no buildup paths
- [ ] **Heaven's Cloud base stats** — Confirm max attack 180, max magic 180, no buildup paths, third attachment slot
- [ ] **Aga's Sword base stats** — Confirm max attack 190, no buildup paths
- [ ] **Skunk / Blessing Gun** — Confirm Snail and Jackal no longer buildup to Blessing Gun. Confirm Skunk is a final-form weapon (no buildup) with max attack 143, max magic 105; Blessing Gun max attack 87, max magic 80

</details>

</details>

---

### Troubleshooting

- **"Could not connect to PCSX2 via PINE"** — Make sure PCSX2 is running, PINE is enabled at slot 28011, and a game is loaded before starting the mod.
- **"PNACH File not active!"** — The PNACH file is missing from PCSX2's cheats folder, or cheats are not enabled. See the [Install the PNACH file](#install-the-pnach-file) section above.
- **Flatpak PCSX2 on Linux** — The Flatpak sandbox may block the PINE port. Run `flatpak override --user --share=network net.pcsx2.PCSX2` to allow it.
- **macOS firewall prompt** — Allow the mod to accept incoming connections when macOS asks.
