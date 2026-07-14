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

### Build the mod

1. In the root folder, run `dotnet restore`
2. In the root folder, run `dotnet build "Dark Cloud Improved Version.sln"`

### Quick build & run with `make`

If you have the [.NET 8 **SDK**](https://dotnet.microsoft.com/download/dotnet/8.0) and `make` (preinstalled on macOS and Linux), a `Makefile` in the repo root lets you build and launch in one step from the **repo root** — no need to locate the built `.dll`:

| From the repo root | What it does |
|--------------------|--------------|
| `make build` | Compile only (no launch) |
| `make` &nbsp;or&nbsp; `make user` | Build + launch in **user** mode (normal play) |
| `make dev` | Build + launch in **dev** mode (developer tabs) |
| `make sandbox` | Build + launch in **sandbox** mode (user UI + the **Sandbox** tab) |

These wrap dotnet-native launch profiles (`Dark Cloud Improved Version/Properties/launchSettings.json`), which also appear in the run/debug dropdown of IDEs. The equivalent without `make` is:

```
dotnet run --project "Dark Cloud Improved Version" --launch-profile user   # or dev / sandbox
```

> As with the manual method, PCSX2 must already be running with PINE enabled, cheats on, and a game loaded.

### macOS

1. Install .NET 8: `brew install --cask dotnet` (or download the installer from the link above)
2. Install PCSX2 via its [macOS release](https://pcsx2.net/downloads)
3. Copy `A5C05C78.pnach` to `~/Library/Application Support/PCSX2/cheats/` and enable cheats in PCSX2 (**System → Enable Cheats**)
4. Load your Dark Cloud ISO in PCSX2 and reach the main menu or in-game before starting the mod
5. Open Terminal in the folder containing `Dark Cloud Enhanced Mod.dll`. For a release download this is the extracted zip folder; if building from source it is `bin/Debug/net8.0/` inside the project. Run:
   ```
   dotnet "Dark Cloud Enhanced Mod.dll"
   ```
6. The mod window opens directly in **user** mode — no further clicks needed. (Dev and sandbox modes are launched via the `make` commands above.)

> **Note:** macOS may require granting the terminal or app network access when prompted by the firewall.

### Windows

1. Install .NET 8 from the [Microsoft download page](https://dotnet.microsoft.com/download/dotnet/8.0) if not already present
2. Install PCSX2 from [pcsx2.net](https://pcsx2.net/downloads)
3. Copy `A5C05C78.pnach` to `%APPDATA%\PCSX2\cheats\` and enable cheats in PCSX2 (**System → Enable Cheats**)
4. Load your Dark Cloud ISO in PCSX2 and reach the main menu or in-game before starting the mod
5. Run `Dark Cloud Enhanced Mod.exe` (or `dotnet "Dark Cloud Enhanced Mod.dll"` from a terminal)
6. The mod window opens directly in **user** mode — no further clicks needed. (Dev and sandbox modes are launched via the `make` commands above.)

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
6. The mod window opens directly in **user** mode — no further clicks needed. (Dev and sandbox modes are launched via the `make` commands above.)

---

## The mod window

The mod runs as a window alongside PCSX2 and opens in one of three **modes**. Regular players only ever need **user** mode — the others are for contributors and testing. The mode is chosen at launch (via the `make` commands / launch profiles above; running the app with no argument defaults to user mode).

### Modes

| Mode | How to launch | What it gives you |
|------|---------------|-------------------|
| **User** (default) | `make` / `make user`, or run the app with no arguments | The normal player UI |
| **Dev** | `make dev` | Developer tabs: low-level thread and debug controls |
| **Sandbox** | `make sandbox` | The user UI **plus** a **Sandbox** tab of power tools |

### User-mode tabs

- **General** — welcome message, mod version, Discord link, and Quit.
- **Options** — toggles that are **saved to your save file** and re-applied when you load it:
  - *Graphics* — enable graphical improvements; increased FOV
  - *Audio* — disable low-weapon-HP beeps; disable battle music; disable attack sounds; mute all music
  - *Gameplay* — **Faster enemies** (enemy movement + attack speed); **Stronger enemies** (scales enemies toward the next dungeon's power level); **Randomize enemies** (re-rolls each floor's spawns on dungeon entry); **Harder enemy AI** (enemies with a get-up animation can revive after death)
- **Quests** — a live tracker for active in-game quests (auto-refreshes when you open Manuals in-game).
- **Credits** — contributor credits.

### Sandbox tab (sandbox mode only)

Power/testing tools kept out of normal play:

- **Spawn-roster editor** — override the current dungeon's spawns with specific enemy species by TableIndex (a trailing `!` marks a species as spawn-once). Handy for testing a particular enemy.
- **Fish Data Farmer** — an automated fishing-data collection tool.

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
<summary>Custom Weapon Effects — second batch</summary>

- [ ] **Small Sword (Quick Draw)** — First combo swing comes out with no wind-up; verify Tsukikage and Heaven's Cloud inherit it
- [ ] **Tsukikage (Moonlit Focus)** — Lunge charge ready in ~0.25s, whirlwind in ~0.75s; verify Heaven's Cloud inherits it
- [ ] **Heaven's Cloud (charge scale)** — Hold the whirlwind charge: blade grows up to 3× with a flash at max; whirlwind reach matches the grown blade
- [ ] **Sun Sword (Solar Harvest)** — Kill many enemies with Sun Sword or Big Bang wielded; confirm occasional (1%) Sun attachment drops
- [ ] **Big Bang (Detonate)** — Hits explode with fireball + shockwave; nearby enemies take real splash damage and knockback; chained kills also explode
- [ ] **Buster Sword (True Buster)** — Attach an anti-category attachment: value counts as +4 in menu stats, damage, evolution, and absorb
- [ ] **Cross Hinder (Sanctifier)** — ~2× damage and ~2× ABS vs undead; undead killed by it never revive
- [ ] **Dark Cloud (Guard Crush)** — Hits land through a blocking enemy's guard (e.g. guarding skeletons)
- [ ] **7th Heaven (Divine Guard)** — Guard blocks every attack including guard-breakers (knockback only, no damage); Guard Crush also active
- [ ] **Kitchen Knife (Spring-Blessed Blade)** — Step into a healing spring: blade triples in length and attack doubles for ~60s; standing in the spring refreshes the timer
- [ ] **Macho Sword (Overtraining)** — With a Macho Sword owned, weapon ABS fills past max (up to 2×) and the overflow carries into the next level after level-up
- [ ] **Maneater (Blood Price)** — Curse applies per floor; holy water cures with no penalty; at critical durability drains 1 HP/s to restore durability, never killing
- [ ] **7 Branch Sword (Sevenfold Rite)** — Status Break refused below +7; at +7 the sphere keeps 77% of stats; menu hint reflects the rule
- [ ] **Atlamillia Sword (Insurance)** — With it owned, break a weapon in a dungeon: an Atla appears on a random floor of that dungeon containing the broken weapon's SynthSphere with level-scaled stats
- [ ] **Angel Gear (Halo & Homing)** — Fire with no target: pellets form a spinning halo (max 5); approach an enemy: pellets peel off with trails and home in, curving around walls, skipping guarding enemies
- [ ] **Super Steve (Sphere Inheritance)** — Attach spheres of several source weapons (e.g. Dark Cloud, Heaven's Cloud, Angel Gear, Mirage) and confirm the matching effect activates and the slingshot recolors to the source weapon's palette; detach and confirm it reverts
- [ ] **Mirage (Decoy)** — Hold guard to charge, release: a shimmering clone appears and enemies chase it (~12s); hitting an enemy makes only that enemy re-target you; Hercules' Wrath grants the same effect
- [ ] **Snail (Osmond)** — 5% Goo proc on hit

</details>

<details>
<summary>Harder Enemy AI</summary>

- [ ] **Toggle persistence** — Enable in Options, save, reload: toggle stays on and revival is active
- [ ] **Non-native revival** — Kill common enemies with a get-up animation: ~30% revive at partial HP after their collapse, then resume normal AI
- [ ] **Native undead revival** — Mummy / Master Jacket / Gacious / Horn Head revive noticeably more often (~45%)
- [ ] **Cross Hinder interaction** — Undead killed by Cross Hinder never revive even with the toggle on

</details>

<details>
<summary>Misc</summary>

- [ ] **In-game weapon descriptions** — Open the weapon menu: descriptions match the modded effects (check a reworked one like 7th Heaven); 7 Branch Sword's Status Break hint shows the Sevenfold Rite rule
- [ ] **Fishing records → Arise Mardan** — Set a new best record for a species: Arise Mardan's Max Magic increases after a few seconds; records list keeps one best entry per species
- [ ] **Town overhead camera** — Press Select in any town (e.g. Yellow Drops): bird's-eye camera works; exiting snaps the player to the ground safely

</details>

<details>
<summary>Miniboss System</summary>

- [ ] **Per-enemy spawn logic** — Confirm rate of spawn is approximately 1 miniboss per floor, but possible for no or multiple minibosses to appear.
- [ ] **Backfloor spawn** — Confirm minibosses may spawn in backfloor.
- [ ] **Miniboss persistence** — Confirm Miniboss will not respawn via returning from backfloors if already killed. Confirm Miniboss still spawns as same enemy species if not killed before going to backfloor then returning.
- [ ] **Stat multipliers** — Kill a miniboss and confirm HP, ABS reward, and Gilda drop are roughly 4× the base enemy values (up from 3×)
- [ ] **Flavor rare loot (5%)** — Each dungeon has per-enemy rare loot; verify correct items drop (e.g., Evilcise from Yammich in DBC, Bandit Slingshot from Days of the Week enemies in WOF)
- [ ] **Flavor common loot (30%)** — Verify themed items drop from the correct enemies per dungeon
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

---

## Fishing Simulator

**[Dark Cloud Fishing Simulator](https://script.google.com/macros/s/AKfycbwtFbtbH3h0z_jjyyXnIf3i7Hi08fxosvnIa0PXw9o2qthstTo7MB2RC2dF1nOf74_P7Q/exec)**

Tool for simulating Dark Cloud fishing sessions. No account or setup required — open it in any browser.

- Lists the static data values for all fish species, including size ranges, bait affinities and spawn weights
- Uses the game's native RNG and size calculation methods to generate size distribution charts across session and per-species simulation modes, and includes an FP calculator for exact reward lookups
