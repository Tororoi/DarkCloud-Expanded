# Changelog

All changes made to this fork of [Dark Cloud Enhanced Mod](https://github.com/Gundorada-Workshop/DarkCloud-Enhanced).

---

## Tech Stack

- **Cross-platform support** — Ported to **.NET 8** and added macOS and Linux compatibility via PINE IPC over Unix domain socket (`$TMPDIR/pcsx2.sock`). Windows continues to use TCP port 28011.

---

## Mod Window

- **Quest Tracker** — Added a quest tracker panel to the mod window that displays active fishing quests and their completion state.

---

## Fixes

- **Max Thirst memory addresses** — Corrected the memory addresses used to read and write Max Thirst value.

---

## Game Mechanics

### Fishing

- **Fishing quest system** — Refactored fishing quest tracking. Tracks fishing quests for Pike (Norune, area 0), Pao (Matataki Waterfall, area 1), Sam (Area 19), and Devia (Area 3). Supports count quests and size-range quests; monitors quest state byte and fires the Sam post-loop queens-quest trigger after the required number of completions.
- **Fish steering** — Passive fish-steering loop at Matataki Waterfall drifts all fish toward the player roughly every 11 seconds. Mardan Eins ownership adds a separate steering pass for Garayan and Umadakara fish at an interval weighted by bait affinity.
- **Mardan Sword rework** — Detects all Mardan swords from bag and storage (not only equipped). FP multipliers: Eins 1.2×, Twei 1.5×, Arise 2×. Mardan Twei and Arise Mardan trigger a second independent Garayan fish roll. Arise Mardan scales fish size at an increasing factor gradually toward the species max size.

### Miniboss System

- **Stat multipliers** — Miniboss HP, ABS reward, and Gilda drop increased from 3× to 5× base enemy values.
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

- **Evilcise** — Applies curse immediately on equip (including from pause menu). Breaking the curse with holy water applies poison and sets HP to 1. Curse is reapplied on floor change; stripped on unequip or leaving the dungeon.
- **Heaven's Cloud** — 50% chance to apply Gooey on hit.
- **Aga's Sword** — Grants Toan +15 defense while equipped; boost is re-applied if external changes alter defense. Removed on unequip.
- **Brave Ark** — Polls for Freeze, Poison, Curse, and Goo status effects and clears them within the polling interval. Stamina is intentionally excluded from the resist mask.
- **Frozen Tuna (Goro)** — WHP loss heals Goro HP proportionally. On hit, 20% chance stops all non-ice enemies and freezes Goro. Blizzard, Sam, and Ice Gemron are immune to the stop proc.
- **Wise Owl Sword** — While a Wise Owl Sword is in bag, storage, or equipped in Wise Owl Forest, picking up a Shiny Stone, Red Berry, or Pointy Chestnut displays "You found Wise Owl's favorite!" if it's the floor key.
- **Cactus** — Added custom thirst effect which drains moisture from enemies. Dry enemies are unaffected.

### Weapon Stat Changes

- **Bone Slingshot / Hardshooter** — 50% chance to generate with the Fragile effect.
- **De Sanga** — 30% chance to generate with the Drain effect.
- **Frozen Tuna** — Max attack 100, max MP 678, Stop effect, no buildup paths.
- **Heaven's Cloud** — Max attack 180, max magic 180, no buildup paths, third attachment slot.
- **Aga's Sword** — Max attack 190, no buildup paths.
- **Skunk** — Final-form weapon (no buildup), max attack 143, max magic 105.
- **Blessing Gun** — Max attack 87, max magic 80.
- **Flamethrower weapons** — Rebalanced stats.
