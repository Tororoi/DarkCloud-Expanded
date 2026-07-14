# Changelog

All changes made to this fork of [Dark Cloud Enhanced Mod](https://github.com/Gundorada-Workshop/DarkCloud-Enhanced).

---

## Tech Stack

- **Cross-platform support** — Ported to **.NET 8** and added macOS and Linux compatibility via PINE IPC over Unix domain socket (`$TMPDIR/pcsx2.sock`). Windows continues to use TCP port 28011.

---

## Mod Window

- **Quest Tracker** — Added a quest tracker panel to the mod window that displays active quests and their completion state.
- **Launch modes** — The mod window now opens directly into one of three modes instead of requiring a "Launch as User" click: **user** (normal play), **dev** (low-level thread/debug tabs), and **sandbox** (user UI plus a **Sandbox** tab of power tools). Modes are selected via dotnet launch profiles and a `Makefile` (`make` / `make user` / `make dev` / `make sandbox`).
- **Sandbox tab** — Sandbox mode adds a tab with testing tools kept out of normal play: a spawn-roster editor (override the current dungeon's spawns by TableIndex, with a trailing `!` to mark a species spawn-once) and the Fish Data Farmer.
- **Difficulty options** — Added Gameplay toggles: **Faster enemies** (enemy movement + animation/action speed, with an attack-hit-window dwell so fast swings still connect) and **Stronger enemies** (scales enemies toward the next dungeon's power level). Saved to the save file and re-applied on load.
- **Randomize enemies option** — New Gameplay toggle that re-rolls every non-boss/event floor's spawns on dungeon entry (see Enemy System → Randomized enemies). Saved to the save file and re-applied on load.
- **Harder enemy AI option** — New Gameplay toggle: any enemy with a real get-up animation can revive after death (~30% chance, at a fraction of max HP), and the natively-reviving undead (Mummy, Master Jacket, Gacious, Horn Head, …) revive far more often than vanilla. Saved to the save file and re-applied on load.
- **Bit-packed option storage** — Persisted option toggles are now bit-packed into three category-grouped save bytes (Graphics / Audio / Gameplay) instead of one byte each, freeing proven-unused save bytes for future options. Each toggle read-modify-writes a single bit, preserving the others in its byte.

---

## Fixes

- **Max Thirst memory addresses** — Corrected the memory addresses used to read and write Max Thirst value.
- **No-drop enemies** — Regular enemy species that ship unable to drop items (flyers, Gol/Sil, …) had a working `DropChance` but a `DeathDropFlag` of 0, which made the engine skip their entire death-drop block. The flag is now flipped to 1 in the static species table so every spawn drops as intended. Scoped to regular `e####` enemies; bosses, effects, and the steal item are untouched.
- **Log file names** — Mod log filenames now use a correct `yyyy-MM-dd` date format (was `yyyy-dd-M`, which sorted wrong and collided across months).

---

## Game Mechanics

### Fishing

- **Fishing quest system** — Refactored fishing quest tracking. Tracks fishing quests for Pike (Norune, area 0), Pao (Matataki Waterfall, area 1), Sam (Area 19), and Devia (Area 3). Supports count quests and size-range quests; monitors quest state byte and fires the Sam post-loop queens-quest trigger after the required number of completions.
- **Fish steering** — Passive fish-steering loop at Matataki Waterfall and Queens Harbor nudges all fish toward the player every 10 seconds. Mardan Eins ownership adds a separate steering pass for Garayan and Umadakara fish at an interval weighted by bait affinity.
- **Mardan Sword rework** — Detects all Mardan swords from bag and storage (not only equipped). FP multipliers: Eins 1.2×, Twei 1.5×, Arise 2×. Mardan Twei and Arise Mardan trigger a second independent Garayan fish roll. Arise Mardan applies the full size transform: native smoothing, a linear scale to 2× the species max, then a second smoothing pass over the scaled range (hard cap at exactly 2× max).
- **Smooth native fish size distribution** — Every non-Arise fishing session smooths the size the game rolls, filling the sparse region just below the species max so the distribution ramps into the cap instead of spiking at it. Does not change the max. Arise Mardan sessions include this smoothing internally.
- **Rerolled slots use the native size formula** — Mardan Twei/Arise slot rerolls now roll size via the game's native slot-init formula (12-draw Irwin-Hall RNG, asymmetric slope, clamped to `[0.5×BaseSize, MaxSize]`) instead of a flat uniform draw, then receive the smoothing/Arise effect like any other slot.
- **Fishing records feed Arise Mardan's magic** — Each fish species' best recorded catch grants Arise Mardan bonus Max Magic on a curve: +1 at the species' vanilla max size, rising to a per-species cap at the 2× Arise size cap (43 for normal species, 117 for the two Garayans). Maxing every record takes Arise Mardan from its base 120 Max Magic to the 999 cap. The native records list is also deduped to one best entry per species, so the records screen shows per-species maxes and a new catch always has a free slot.

### Towns

- **Overhead camera everywhere** — The Georama bird's-eye camera (Select button) now works in every town map, not just the first five, along with the leftover developer fast-run speed while it's active. Exiting the camera safely snaps the player back to the ground.

### Enemy System

- **Randomized enemies** — When enabled, each non-boss/event floor (normal + Ura) is staged with a fresh 9-species roster on dungeon entry, weighted toward the dungeon's native mimic and king mimic. Floors are staged lazily (at most once per visit, so backtracking never re-rolls) and reverted on dungeon exit. Out-of-place spawns are rescaled by the stat normalizer so they stay fair.
- **Themed floors** — A randomized floor has a chance to spawn a single themed group (cards, days of the week, dragons, …) instead of the random mix — either filling the whole floor with the group or capping it to one-of-each and backfilling with mimics or dungeon natives.
- **Spawn any species on regular floors** — Replacing spawn-table entity IDs/models lets any enemy — including bosses and minibosses (Master Utan, Minotaur Joe, Black Knight Mount, King's Curse, Ice Queen, Dark Genie final form) and mimics — appear on normal dungeon floors. Boss behavior scripts are patched in loaded memory so a non-native boss spawns at its floor position instead of snapping to its arena origin, and on death it collapses/fades and interrupts its motion instead of triggering a victory cutscene. Multi-part bosses are forced spawn-once (one skeleton).
- **Mesh-buffer guard** — Randomized rosters are budgeted against measured per-species model footprints so a floor never overruns the engine's mesh buffer.
- **Mimic visibility** — Roster-spawned mimics had their view-gate baked off in the model template and rendered invisible; the gate is now set after spawn so they appear correctly.
- **Stat normalization** — Non-native / randomized spawns are rescaled toward the current dungeon's power level (HP, defense, and damage), so out-of-place enemies aren't trivial or unfair.
- **Reusable stat scaling** — Live per-slot / per-species stat scaling (HP, defense, melee, projectile) is driven through one shared pipeline used by the normalizer, the difficulty options, and miniboss buffs.

### Miniboss System

- **Stamina status removed** - Minibosses no longer have Stamina status by default.
- **Combat scaling** — Minibosses now get a scaled hitbox, dynamically buffed projectile damage, and aggro/reticle ranges enlarged to match their size, with walking animation synced to their scale.
- **Thematic loot** — Each dungeon has per-enemy flavor drops: 5% rare drop and 30% common drop tables with dungeon-appropriate items. See tables below.
- **Boosted weapon drops** — Minibosses can drop weapons with preset stats written directly to the weapon slot on pickup. Boost monitor cancels cleanly on floor change.

#### Dungeon 0 — Divine Beast Cavern

| Enemy | Rare Drop (5%) | Common Drop (30%) |
|-------|---------------|-------------------|
| Master Jacket | Gladius | Gladius |
| Yammich | Evilcise | Amethyst |
| Statue Dog | Steve | Turquoise |
| Skeleton Soldier | — | Bone Rapier or Bone Slingshot |
| Statue | — | Steel Slingshot |
| Dasher | — | Topaz |
| Opar | — | Opal |
| Rockanoff | — | Diamond |
| Dragon | — | Ruby or Garnet |

**Boosted weapons:** Gladius (Master Jacket) → ATK+15, Holy+50, Anti-Undead+50

#### Dungeon 1 — Wise Owl Forest

| Enemy | Rare Drop (5%) | Common Drop (30%) |
|-------|---------------|-------------------|
| Days of the Week (all 7) | Bandit Slingshot | Powerup Powder |
| Werewolf | Lamb's Sword | — |
| Earth Digger | Trial Hammer | Plate Hammer |
| Halloween | Steve | — |
| Haley Holey | — | Sapphire or Flamingo |
| King Prickly | — | Steel Hammer |

**Boosted weapons:** Bandit Slingshot (Days of the Week) → ATK+35, Wind+40, Anti-Mimic+40 · Lamb's Sword (Werewolf) → ATK+60, Magic+30, WHP+99

#### Dungeon 2 — Shipwreck

| Enemy | Rare Drop (5%) | Common Drop (30%) |
|-------|---------------|-------------------|
| Auntie Medu | Serpent Sword | Turquoise or Garnet |
| Captain | Gold Bullion | Flamingo |
| Corcea | Dusack | Chopper |
| Mask of Prajna | Small Sword | Amethyst |
| Gyon | — | Frozen Tuna or Aquamarine |
| Gunny | — | Pearl |
| Cursed Rose | — | Thorn Armlet |
| Pirate's Chariot | — | Steel Hammer or Powerup Powder |
| Sam | — | Crystal Ring |

**Boosted weapons:** Small Sword (Mask of Prajna) → ATK+40, Thunder+40, Anti-Mage+40

#### Dungeon 3 — Sun & Moon Temple

| Enemy | Rare Drop (5%) | Common Drop (30%) |
|-------|---------------|-------------------|
| Blue Dragon | Dragon's Y | Amethyst, Aquamarine, or Turquoise |
| Dune | Cactus | Powerup Powder |
| Golem | Sun Sword or Tsukikage | Auto Repair Powder |
| Mummy | Claymore | Revival Powder or Peridot |
| Steel Giant | Platinum Ring | Opal or Diamond |
| Bomber Head | — | Powerup Powder |
| Crabby Hermit | — | Opal |
| Mr. Blare | — | Blessing Gun |

**Boosted weapons:** Platinum Ring (Steel Giant) → ATK+30, Ice+40, Anti-Rock+40, Durable

#### Dungeon 4 — Moon Sea

| Enemy | Rare Drop (5%) | Common Drop (30%) |
|-------|---------------|-------------------|
| Space Gyon | Frozen Tuna | Javelin |
| Crescent Baron | Tsukikage or Mirage | Pearl |
| Hell Pockle | Satan's Ring | Pocklekul |
| Moon Digger | Trial Hammer | Magical Hammer |
| Titan | Gaia Hammer | Any gem |
| Vulcan | Blessing Gun | Peridot, Garnet, Topaz, or Diamond |
| White Fang | De Sanga | Topaz |
| Arthur | — | Swallow or 5 Foot Nail |
| Moon Bug | — | Powerup Powder |

**Boosted weapons:** Frozen Tuna (Space Gyon) → WHP+99, Endurance+99, Ice+99, Anti-Marine+99, BigBucks, Fragile · Blessing Gun (Vulcan) → ATK+50, Magic+30, Fire+40, Anti-Dragon+40, Anti-Plant+20 · De Sanga (White Fang) → Drain · Pocklekul (Hell Pockle) → Steal

#### Dungeon 5 — Gallery of Time

| Enemy | Rare Drop (5%) | Common Drop (30%) |
|-------|---------------|-------------------|
| Diamond | Big Bang | Diamond (gem) |
| Joker | Super Steve | Super Steve |
| Spade | Dark Cloud | Brave Ark |
| Heart | Angel Shooter | Goddess Ring |
| Club | Cactus | Steel Hammer |
| Dark Flower | Thorn Armlet | Thorn Armlet or 7 Branch Sword |
| Black Dragon | Dragon's Y | Peridot |
| Alexander | — | Drain Seeker |
| Blizzard | — | Aquamarine, Amethyst, or Turquoise |
| Curse Dancer | — | Maneater or Cross Hinder |
| Billy | — | Trial Hammer |

**Boosted weapons:** Big Bang (Diamond) → WHP+99, Durable · Super Steve (Joker) → WHP+99, ATK+80, Magic+50, Steal, Abs Up · Dark Cloud (Spade) → WHP+99, Fire+40, Holy+40, Anti-Mage+30, Stop, Drain · Angel Shooter (Heart) → WHP+99, Fire+40, Thunder+40, Holy+40 · Cactus (Club) → WHP+99, Endurance+99, Wind+50, Anti-Plant+40, Anti-Marine+40, Critical · Thorn Armlet (Dark Flower) → Abs Up · Dragon's Y (Black Dragon) → Thunder+40, Anti-Beast+40, Anti-Sky+40 · Trial Hammer (Billy) → Thunder+50

#### Dungeon 6 — Demon Shaft

| Enemy | Rare Drop (5%) | Common Drop (30%) |
|-------|---------------|-------------------|
| Fire Gemron | Satan's Ring | Sun or Ruby |
| Ice Gemron | Crystal Ring | Sun or Aquamarine |
| Thunder Gemron | Trial Hammer | Sun or Pearl |
| Wind Gemron | Heaven's Cloud | Sun or Sapphire |
| Holy Gemron | Goddess Ring | Sun or Peridot |
| Nikapous | Macho Sword | — |
| Hornhead | Satan's Ax | Matador or Bone Rapier |
| Silver Gear | Destruction Ring | Bone Slingshot |
| Bishop Q | — | Flamingo |

**Boosted weapons:** Crystal Ring (Ice Gemron) → Ice+50 · Trial Hammer (Thunder Gemron) → Thunder+50 · Heaven's Cloud (Wind Gemron) → Wind+50 · Flamingo (Bishop Q) → Abs Up

### Custom Weapon Effects

- **In-game weapon descriptions** — The weapon description text in the in-game menus is rewritten live to describe each weapon's modded effect (including dynamic hints such as the 7 Branch Sword's Status Break rule), so the menu always matches what the weapon actually does.

#### This Fork

**Toan**
- **Mardan Eins** — Draws rare fish to the player's location at an interval weighted by their bait affinity. FP x1.2 for all non-Garayan fish.
- **Mardan Twei** — Reroll non-Garayan fish for an additional chance (same as native game's initial chance) to turn them into Mardan or Baron Garayan. Mardan Eins ability occurs at an increased rate. FP x1.5 for all non-Garayan fish.
- **Arise Mardan** — Smooths the native size distribution, then scales fish up to 2x their original size (larger initial sizes receiving a scale factor closer to 2x), then smooths again over the scaled range; final size is hard-capped at exactly 2x the species max. Mardan Eins ability occurs at an increased rate. FP x2 for all non-Garayan fish. Fishing records grant it bonus Max Magic (see Fishing).
- **Evilcise ("Jealous Soul")** — Applies curse immediately on equip (including from pause menu). Breaking the curse with holy water applies poison and sets HP to 1. Curse is reapplied on floor change; stripped on unequip or leaving the dungeon.
- **Maneater ("Blood Price")** — Cursed each floor like Evilcise, but curing it with holy water carries no penalty (it just stays off until the next floor). While the sword's durability is critically low it drains 1 HP per second to restore durability (never fatal).
- **Aga's Sword ("Defensive Legacy")** — Grants Toan +15 defense while equipped; boost is re-applied if external changes alter defense. Removed on unequip.
- **Brave Ark ("Hero's Courage")** — Resist Freeze, Poison, Curse, and Goo status effects.
- **Wise Owl Sword ("Wise Owl Always Knows")** — While a Wise Owl Sword is owned, a message displays when you are near an enemy carrying one of the three keys in Wise Owl Forest.
- **Small Sword ("Quick Draw")** — The opening combo swing comes out almost instantly, skipping the wind-up. Inherited by Tsukikage and Heaven's Cloud through the buildup line.
- **Tsukikage ("Moonlit Focus")** — Charge attacks build twice as fast (lunge ready in ~0.25 s, whirlwind in ~0.75 s). Inherits Small Sword effect. Inherited by Heaven's Cloud.
- **Heaven's Cloud** — Holding the whirlwind charge grows the blade — up to 3× at a full hold, with a flash at max — for a bigger, longer-reaching whirlwind. Also inherits Small Sword and Tsukikage effects.
- **Sun Sword ("Solar Harvest")** — While wielding Sun Sword (or its evolution Big Bang), every enemy killed has a 1% chance to drop a Sun attachment.
- **Big Bang ("Detonate")** — Every enemy Toan hits triggers an explosion.
- **Buster Sword ("True Buster")** — Anti-category attachments (Dinoslayer … Mage Slayer) are worth +4 instead of +3 when attached to a Buster Sword.
- **Cross Hinder ("Sanctifier")** — Roughly double damage and double ABS reward against undead, and undead it kills can no longer revive.
- **Dark Cloud ("Guard Crush")** — Toan's hits cut straight through enemy guards; every blow connects even while an enemy is blocking. Inherited by 7th Heaven.
- **7th Heaven ("Divine Guard")** — Perfect guard: blocks every enemy attack and projectile, including heavy hits that normally break guard (those just knock Toan back instead). Also inherits Dark Cloud effect.
- **Kitchen Knife ("Spring-Blessed Blade")** — Stepping into a healing spring blesses the knife for ~60 seconds: the blade visibly grows to triple length and its attack doubles. Standing in the spring refreshes the timer.
- **Macho Sword ("Overtraining")** — While a Macho Sword is owned (bag or storage), every weapon's ABS keeps filling past its max — up to 2× — and the overflow carries into the next level, so a weapon starts its new level with a head start. (Replaces the old Shadow Boxing effect.)
- **7 Branch Sword ("Sevenfold Rite")** — Refuses to Status Break below +7; at +7 or higher the resulting SynthSphere keeps 77% of the weapon's stats instead of the normal 60%. The Status Break menu hint explains the rule.
- **Atlamillia Sword ("Atlamillia Insurance")** — While owned (bag or storage), a weapon breaking in a dungeon is no longer a total loss: an Atla appears on a random floor of that dungeon containing a SynthSphere of the broken weapon, keeping 10% of its stats per weapon level (up to 50% at +4 or higher). Attachments are lost. Collecting the Atla delivers the sphere like any georama Atla.

**Xiao**
- **Angel Gear ("Halo & Homing")** — Pellets fired with no enemy in range gather into a slow-spinning halo of up to 5 above Xiao's head. When an enemy approaches, pellets peel off one at a time with glowing trails and home in on it, arcing around walls and skipping enemies that are guarding. (The party-wide HP regen from Dark Cloud Enhanced remains.)
- **Super Steve ("Sphere Inheritance")** — Super Steve inherits the custom effect of whichever weapon's SynthSphere is attached to it (one sphere at a time = one inherited effect), and recolors itself in the source weapon's palette. Ownership/upgrade passives (Macho Sword, Wise Owl Sword, Chronicle 2, Buster Sword, 7 Branch Sword) deliberately don't transfer.

**Goro**
- **Frozen Tuna ("Cold Storage")** — Each point of WHP lost banks 2 HP into a healing pool. When Goro takes damage, the pool drains at 1 HP per 0.5 seconds. Healing pauses if HP reaches max; banked HP is preserved until the next hit. The pool resets on weapon repair or switch. On hit, 5% chance stops all non-ice enemies and freezes Goro for 3 seconds. Blizzard, Sam, and Ice Gemron are immune to the stop proc.

**Ungaga**
- **Cactus ("Absorb")** — Custom thirst effect which drains moisture from enemies. Dry enemies are unaffected.
- **Mirage ("Decoy")** — Holding guard charges a mirage; on release a shimmering clone of Ungaga with a heat-haze effect is planted at that spot, and enemies chase the decoy instead of him for ~12 seconds (a new charge refreshes it). The illusion breaks per enemy — hit one and it re-targets you. Hercules' Wrath inherits the effect.

**Osmond**
- **Snail** — 5% chance on hit to apply Gooey to the struck enemy.

#### Dark Cloud Enhanced

**Toan**
- **Bone Rapier** — Allows bypassing bone doors while equipped.
- **Chronicle Sword** — Attacks hit all nearby targets for a percentage of damage.

**Xiao**
- **Angel Gear** — Applies Heal regeneration to all allies while equipped.

**Goro**
- **Tall Hammer** — Gradually reduces enemy size on hit until they reach 30% of their original size.
- **Inferno** — Scales attack power with missing HP and missing thirst; bonus scales up to 100% of current total attack.

**Ruby**
- **Mobius Ring** — Increases damage output the longer Ruby charges an attack.
- **Secret Armlet** — All magic circle effects on the current floor are turned into positive outcomes while equipped.

**Ungaga**
- **Babel Spear** — 6% chance on hit to stop all enemies for 5 seconds.
- **Hercules' Wrath** — 30% chance on taking a hit to gain the Stamina status effect.

**Osmond**
- **Supernova** — 10% chance per hit to apply a random status effect (Freeze, Poison, Stamina, or Gooey) to each enemy struck.
- **Star Breaker** — 2% chance on kill to receive an empty synthsphere.

### Weapon Ability Changes

#### This Fork

**Xiao**
- **Bone Slingshot** — 50% Fragile
- **Hardshooter** — 50% Fragile

**Goro**
- **Frozen Tuna** — 100% Stop

**Ruby**
- **Thorn Armlet** — 50% Poison

**Ungaga**
- **De Sanga** — 30% Drain

#### Dark Cloud Enhanced

**Toan**
- **Macho Sword** — 100% Abs Up
- **Heaven's Cloud** — 25% Poison, 25% Critical
- **Dark Cloud** — 25% Poison, 25% Stop
- **Big Bang** — 25% Critical, 25% Stop
- **Atlamillia Sword** — 25% Heal, 25% Stop
- **Dusack** — 50% Steal

**Xiao**
- **Matador** — 100% Critical

**Ruby**
- **Athena's Armlet** — 100% Abs Up
- **Goddess Ring** — 50% Heal
- **Destruction Ring** — 50% Critical
- **Satan's Ring** — 50% Drain

**Osmond**
- **Skunk** — 50% Poison
- **Swallow** — 50% Steal

### Weapon Stat Changes

#### This Fork

- **Frozen Tuna** — Max attack 100, max MP 678, third attachment slot
- **Heaven's Cloud** — Max attack 180, max magic 180, third attachment slot
- **Aga's Sword** — Max attack 190
- **Skunk** — Max attack 143, max magic 105
- **Blessing Gun** — Max attack 87, max magic 80
- **Thorn Armlet** — Stone Breaker and Beast Buster set to 20

#### Dark Cloud Enhanced

**Toan**
- **Baselard** — Endurance set to 30.
- **Antique Sword** — Speed set to 70, Fire set to 15.
- **Kitchen Knife** — WHP set to 50, Attack set to 25, Endurance set to 30, Ice removed, Thunder set to 8, Sea Killer set to 90.
- **Tsukikage** — Endurance set to 33, Speed set to 80
- **Heaven's Cloud** — Third attachment slot
- **Lamb's Sword** — Third attachment slot; transform and stats thresholds set to 50%
- **Brave Ark** — Third attachment slot
- **Big Bang** — Speed set to 70
- **Small Sword** — WHP set to 35, Magic set to 17, Sea Killer removed, Metal Breaker set to 10
- **Sand Breaker** — WHP set to 45, Endurance set to 25, third attachment slot
- **Drain Seeker** — WHP set to 60
- **Chopper** — Speed set to 60
- **Choora** — WHP set to 57, Attack set to 45, Speed set to 70, Ice set to 10, Thunder set to 15, Undead Buster set to 15, Beast Buster set to 15, Metal Breaker set to 15, third attachment slot
- **Claymore** — Undead Buster set to 10, Beast Buster set to 10, Mage Slayer set to 10
- **Maneater** — Endurance set to 44, Speed set to 70, Magic set to 45, Ice/Thunder/Holy/Undead/Beast/Metal set to 15, Mimic Breaker set to 10
- **Bone Rapier** — WHP set to 38, Magic set to 26
- **Sax** — Speed set to 60, Fire set to 6, Sky Hunter set to 10
- **7 Branch Sword** — WHP set to 47, Endurance set to 47, Magic set to 37; Dino Slayer, Undead, Sea, Stone, Plant, Sky, Mimic set to 7; Beast Buster and Mage Slayer set to 8; Metal Breaker set to 10
- **Cross Hinder** — Endurance set to 50, Speed set to 70, Magic set to 32
- **Chronicle 2** — Max Attack set to 999

**Xiao**
- **Wooden Slingshot** — Attack set to 6, Magic set to 2, Fire set to 4
- **Bone Slingshot** — Attack set to 11, Endurance set to 30
- **Hardshooter** — Speed set to 60

**Goro**
- **Turtle Shell** — Magic set to 10
- **Frozen Tuna** — WHP set to 65
- **Gaia Hammer** — Endurance set to 25
- **Trial Hammer** — Attack set to 30, Endurance set to 25

**Ruby**
- **Gold Ring** — Attack set to 15, Magic set to 30
- **Bandit's Ring** — Attack set to 30, Max Attack set to 50, Magic set to 20
- **Platinum Ring** — Attack set to 23
- **Pocklekul** — Attack set to 28, Magic set to 28, Holy removed

**Ungaga**
- **All weapons** — +10 Attack, +10 Max Attack, +15 Endurance
- **Babel Spear** — Fourth attachment slot

**Osmond**
- **All weapons** — +15 Attack, +15 Max Attack

---

## Developer Tooling

Not player-facing; listed so contributors know what exists.

- **Ghidra EE decompiler toolchain** (`tools/ghidra/`) — one-command decompilation of the game's PS2 EE code (`decompile.sh main|dun "<function>"`), symbol export/apply scripts, and an xref finder. The game ELF ships a full demangled C++ symbol table (`symbols.txt`).
- **Code cave scanner** (`CodeCaveScanner.cs` + `tools/find_code_caves.py`) — background sweeps of EE RAM across sessions to find provably-unused regions for the mod's injected bytecode; findings accumulate in `CodeCaveFindings.txt`. Proven caves and mod cave layout live in `CodeCaveAddresses.cs`.
- **Georama probe** (`GeoramaProbe.cs`) — read-only dumper of town Georama parts, water surfaces, event points, and fishing-sign parameters, supporting the custom-fishing-spot research (`docs/custom-fishing-spot.md`).
- **Asset tools** (`tools/`) — `.mes` menu-text decoder, TIM2/`.mds` surgery scripts, and generators for the motion/tile/model reference tables in `docs/`.
- **Docs library** (`docs/`) — new reverse-engineering references: game damage formulas, per-attack enemy damage/guard tables, enemy & character motion tables, chest loot tables, dungeon tile grid, code caves, cave code execution, orphaned enemy models, and more. Existing root-level docs moved into `docs/`.
- **Reusable effect primitives** — general building blocks for future weapon effects: elemental gem bursts at any point/scale (`GemBurst.cs`), rings of real damaging pellets (`ShrapnelBurst.cs`), screen-space heat shimmer (`HeatHaze.cs`), glowing motion trails (`TrailRibbon.cs`), radial knockback shockwaves, and a full player-clone renderer (`CharacterClone.cs`).

---

## Weapon Buildup Paths

All buildup paths as modified by this mod. `★` marks final forms (no further buildup).

---

### Toan

> **Mod changes:** Kitchen Knife has no buildup paths. Choora builds up to Maneater only. Heaven's Cloud and Aga's Sword are terminal weapons.

```
Baselard
  ├─ Sax ─────┐
  └─ Shamshir ─┴─ Dusack ─┬─ Brave Ark → Dark Cloud → 7th Heaven ★
                           └─ 7 Branch Sword → Atlamillia Sword → Chronicle Sword ★

Gladius
  ├─ Small Sword → Tsukikage → Heaven's Cloud ★
  └─ Chopper ─┬─ Choora → Maneater → Atlamillia Sword → Chronicle Sword ★
               └─ Dusack ─┬─ Brave Ark → Dark Cloud → 7th Heaven ★
                           └─ 7 Branch Sword → Atlamillia Sword → Chronicle Sword ★

Crysknife
  ├─ Small Sword → Tsukikage → Heaven's Cloud ★
  └─ Sandbreaker → Antique Sword → Brave Ark → Dark Cloud → 7th Heaven ★

Buster Sword → Claymore → Cross Hinder → Big Bang → Sword of Zeus ★

Wise Owl Sword → Lamb's Sword → Atlamillia Sword → Chronicle Sword ★

Bone Rapier → Evilcise → Drainseeker → Dark Cloud → 7th Heaven ★

Kitchen Knife ★

Sun Sword → Big Bang → Sword of Zeus ★

Macho Sword
  ├─ Aga's Sword ★
  └─ Cross Hinder → Big Bang → Sword of Zeus ★

Serpent Sword
  ├─ Tsukikage → Heaven's Cloud ★
  └─ Evilcise → Drainseeker → Dark Cloud → 7th Heaven ★

Mardan Eins → Mardan Twei → Arise Mardan ★

Chronicle 2 ★
```

---

### Xiao

```
Steel Slingshot → Hardshooter ─┬─ Double Impact ─┬─ Divine Beast Title → Angel Shooter → Angel Gear ★
                                └─ Matador ────────┘

Bone Slingshot → Flamingo → Dragon's Y → Divine Beast Title → Angel Shooter → Angel Gear ★

Bandit Slingshot ─┬─ Hardshooter ─┬─ Double Impact ─┬─ Divine Beast Title → Angel Shooter → Angel Gear ★
                  │               └─ Matador ────────┘
                  └─ Double Impact → Divine Beast Title → Angel Shooter → Angel Gear ★

Steve → Super Steve ★
```

---

### Goro

> **Mod changes:** Big Bucks Hammer builds up to Magical Hammer only (direct Gaia Hammer path removed). Frozen Tuna is a terminal weapon.

```
Steel Hammer → Plate Hammer → Magical Hammer ─┬─ Gaia Hammer ─┐
                                               └─ Last Judgement ┴─ Tall Hammer ★

Trial Hammer → Gaia Hammer → Tall Hammer ★

Big Bucks Hammer → Magical Hammer ─┬─ Gaia Hammer ─┐
                                    └─ Last Judgement ┴─ Tall Hammer ★

Turtle Shell ─┬─ Magical Hammer ─┬─ Gaia Hammer ─┐
              │                   └─ Last Judgement ┴─ Tall Hammer ★
              └─ Battle Axe → Satan's Axe → Inferno ★

Battle Axe → Satan's Axe → Inferno ★

Frozen Tuna ★
```

---

### Ruby

> **Mod changes:** Thorn Armlet now builds to Destruction Ring only (was Platinum Ring). Pocklekul gains a second path to Thorn Armlet.

```
Platinum Ring ─┬─ Crystal Ring ─┬─ Goddess Ring → Athena's Armlet → Secret Armlet ★
               │                └─ Satan's Ring → Mobius Ring ★
               └─ Fairy Ring → Destruction Ring → Mobius Ring ★

Bandit's Ring ─┬─ Crystal Ring ─┬─ Goddess Ring → Athena's Armlet → Secret Armlet ★
               │                └─ Satan's Ring → Mobius Ring ★
               ├─ Goddess Ring → Athena's Armlet → Secret Armlet ★
               └─ Pocklekul ─┬─ Fairy Ring → Destruction Ring → Mobius Ring ★
                              └─ Thorn Armlet → Destruction Ring → Mobius Ring ★
```

---

### Ungaga

```
Javelin ─┬─ Desanga ─┐
          └─ Partisan ┴─ Cactus → Terra Sword → Babel Spear ★

Halberd → Scorpion ─┬─ Mirage ─┬─ Terra Sword → Babel Spear ★
                    │           └─ Hercules' Wrath ★
                    └─ Cactus → Terra Sword → Babel Spear ★

5 Foot Nail ─┬─ Scorpion ─┬─ Mirage ─┬─ Terra Sword → Babel Spear ★
             │             │           └─ Hercules' Wrath ★
             │             └─ Cactus → Terra Sword → Babel Spear ★
             └─ Partisan → Cactus → Terra Sword → Babel Spear ★
```

---

### Osmond

> **Mod changes:** Skunk is a terminal weapon. Jackal and Snail no longer build up to Blessing Gun; Blessing Gun is a standalone starting weapon.

```
Jackal → Swallow → G Crusher → Star Breaker ★

Snail → Hexa Blaster → Supernova ★

Blessing Gun → Skunk ★
```
