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
- **Difficulty options** — Added Gameplay toggles: **Faster enemies** (enemy movement + animation/action speed, with an attack-hit-window dwell so fast swings still connect) and **Stronger enemies** (every enemy's stats are scaled by the next region's pool average over the current region's, never lowering anything, extrapolated past Demon Shaft 81-100; mimics keep their variant's stats wherever they are placed and, with the toggle on, are at least the next variant's; ABS rewards stay at the current region's level). Saved to the save file and re-applied on load.
- **Randomize enemies option** — New Gameplay toggle that re-rolls every non-boss/event floor's spawns on dungeon entry (see Enemy System → Randomized enemies). Saved to the save file and re-applied on load.
- **Harder enemy AI option** — New Gameplay toggle: any enemy with a real get-up animation can revive after death (~30% chance, at a fraction of max HP), and the natively-reviving undead (Mummy, Master Jacket, Gacious, Horn Head, …) revive far more often than vanilla. Saved to the save file and re-applied on load.
- **Bit-packed option storage** — Persisted option toggles are now bit-packed into three category-grouped save bytes (Graphics / Audio / Gameplay) instead of one byte each, freeing proven-unused save bytes for future options. Each toggle read-modify-writes a single bit, preserving the others in its byte.

---

## Fixes

- **No-drop enemies** — Regular enemy species that ship unable to drop items (flyers, Gol/Sil, …) had a working `DropChance` but a `DeathDropFlag` of 0, which made the engine skip their entire death-drop block. The flag is now flipped to 1 in the static species table so every spawn drops as intended. Scoped to regular `e####` enemies; bosses, effects, and the steal item are untouched.
- **Log file names** — Mod log filenames now use a correct `yyyy-MM-dd` date format (was `yyyy-dd-M`, which sorted wrong and collided across months).
- **Gacious in the randomizer** — The randomizer no longer places vanilla Gacious (a boss-type record that breaks as a regular enemy); Gacious (Enhanced) takes its slot at the same frequency.
- **Bait notice-radius table** — The mod's map of the game's bait table was off by one word: each bait's radius address was the previous bait's, and the Flamingo's bonus also wrote into an unrelated float before the table (128 → 138 while a Flamingo was owned). Corrected against the game's `esa_info` layout.
- **Dungeon character memory** — The dungeon's character-model pool is raised from 3.36 MB to 3.84 MB in the ISO's dungeon overlay (the disc-read staging buffer trimmed from 4.48 MB to 4.00 MB to pay for it), so Xiao's model with the cat baked in doesn't hang a party switch.
- **Shot slots** — Floors are no longer limited to five monster shot types: the five slots are shared among every config the floor needs, each read from disc at most once per floor.
- **Ally town dialogue** — An ally's own NPC lines and name tokens show again after an in-town switch.
- **Game text** — The ISO patch fixes typos in the game's English text and unifies NPC, place and item names (Mr. Mustache, Stu, Mahnia, Sugar, Yellow Drops, Sun & Moon Temple, Muska Desert, Halberd, the Georama shop and house names, and the Pike / Gina / Storage Guard name plates). Double spaces and spaces before punctuation are removed. The mod's own dialogue got the same pass and uses US spellings.

---

## Game Mechanics

- **Heal ability cadence** — Every weapon with the Heal ability now heals every 3 seconds instead of 4 (patched into the dungeon overlay on the ISO).
- **Poison wears off** — Enemy poison ends after status susceptibility × 2 seconds (half with Harder Enemy AI on); re-poisoning does not extend it.
- **A hit breaks your freeze** — Taking a hit while frozen ends the freeze at once, even from an attack that freezes (any other ailment stays).
- **Element picker** — D-pad Up opens the quick-change ring as an element picker showing the elements the equipped weapon carries plus None (a grey synth sphere; not offered to Ruby or Osmond's machine gun). X applies, Circle cancels. SELECT still opens the character ring. Replaces the old D-pad Up/Down element cycling.
- **Weapon effects bill WHP** — The big weapon effects below (Solar Flash 5, Big Bang blast 20, Zeus bolt 10, Terra Sword impact 10, Hercules' Wrath 20) charge WHP through the game's own weapon-wear routine, so Endurance, Durable, Fragile, Auto Repair Powder and breaking all apply.
- **Ungaga's charge shot no longer drains on hit** — His held charge fires a shot every 30 frames (~0.5 s), each costing 0.8 WHP as before; the hits those shots land, blocked or not, used to drain the weapon again and now cost nothing (patched into the ISO's damage routine).
- **Confuse (new weapon ability)** — A new ability with its own menu name and icon: a 5% flat chance per hit (bosses immune) to confuse the enemy for 20 s. A confused enemy attacks whatever is nearest, enemy or player, and its hits hurt other enemies; an enemy it hits turns on it. Carried natively by Babel's Spear and passed on by its SynthSphere.

### Fishing

- **Custom fishing spots — Queens, Brownboo, Yellow Drops** — Three new towns are fishable. Each spot gets a native carved sign and trigger, the vanilla entry/quit menus, bait menu, and catch text, all baked into a patched copy of the player's own ISO so the minigame runs fully natively. Each town has its own fish species pool, plus two vanilla pool tweaks (Matataki: Gummy → Niler; East Harbor: Piccoly → Gobbler).
- **Queens canal tide** — The canal water level follows time of day (low in the morning, vanilla by afternoon/night, high at dusk); the visible surface, fishing water, and fish depth all track it. At low tide the drained canal floor is walkable and fishable: a ladder (carved from the player's own Moon Factory data) is injected on the canal wall to climb down, with a second sign to initiate fishing on the floor. At other tides the ladder shows a "tide too high" message instead, and a rising tide warps a player caught below to the East Harbor dock. Low tide adds waterfall mist and wading shows an animated ripple.
- **Yellow Drops west bank** — The bank is reshaped to be wider and the town water raised so the new spot fishes naturally from shore.
- **Casting & line feel** — The line pays out along the cast direction in normally unfishable areas due to the limitations of the vanilla line geometry (split into above/below-water segments at the bobber), casts into the Queens canal walls stop at the wall instead of clipping through, and the fishing camera centers on the bobber at a per-spot height (low over the canal floor).
- **Brownboo pond rocks** — The three pond rocks have real collision: casts and fish no longer pass through them.
- **Fishing prize exchange** — The slingshot on offer is now the Flamingo for 1000 FP, replacing the Matador (2000 FP in Dark Cloud Enhanced, 1400 in vanilla). Baked into the patched ISO's prize table.
- **Fruit of Eden for sale** — The Queens fruit stand stocks the Fruit of Eden at 6000 gold.
- **Fishing quest system** — Refactored fishing quest tracking. Tracks fishing quests for Pike (Norune, area 0), Pao (Matataki Waterfall, area 1), Sam (Area 19), and Devia (Area 3). Supports count quests and size-range quests; monitors quest state byte and fires the Sam post-loop queens-quest trigger after the required number of completions.
- **Fish steering** — Passive fish-steering loop at Matataki Waterfall and East Harbor nudges all fish toward the player every 10 seconds. Mardan Eins ownership adds a separate steering pass for Garayan and Umadakara fish at an interval weighted by bait affinity.
- **Mardan Sword rework** — Detects all Mardan swords from bag and storage (not only equipped). FP multipliers: Eins 1.2×, Twei 1.5×, Arise 2×. Mardan Twei and Arise Mardan trigger a second independent Garayan fish roll. Arise Mardan applies the full size transform: native smoothing, a linear scale to 2× the species max, then a second smoothing pass over the scaled range (hard cap at exactly 2× max).
- **Smooth native fish size distribution** — Every non-Arise fishing session smooths the size the game rolls, filling the sparse region just below the species max so the distribution ramps into the cap instead of spiking at it. Does not change the max. Arise Mardan sessions include this smoothing internally.
- **Rerolled slots use the native size formula** — Mardan Twei/Arise slot rerolls now roll size via the game's native slot-init formula (12-draw Irwin-Hall RNG, asymmetric slope, clamped to `[0.5×BaseSize, MaxSize]`) instead of a flat uniform draw, then receive the smoothing/Arise effect like any other slot.
- **Fishing records feed Arise Mardan's magic** — Each fish species' best recorded catch grants Arise Mardan bonus Max Magic on a curve: +1 at the species' vanilla max size, rising to a per-species cap at the 2× Arise size cap (43 for normal species, 117 for the two Garayans). Maxing every record takes Arise Mardan from its base 120 Max Magic to the 999 cap. The native records list is also deduped to one best entry per species, so the records screen shows per-species maxes and a new catch always has a free slot.

### Towns

- **Town camera overhaul** — The follow camera now resolves town collision natively (baked into the patched ISO's executable): swept sliding along walls with contact friction, pull-in through tight pinches, ceiling ducking, ground clearance, and deadzoned right-stick height control, running against re-authored per-town camera meshes (Queens structures and snake statue, Brownboo's tunnel rock).
- **Queens snake statue** — Fully climbable: its player collision is now the full detailed visual mesh.
- **Brownboo cleanup** — The overhead edit-mode view is fixed (crater walls backface-cull, stray corner triangles removed), houses turn see-through when the camera is inside them.
- **Overhead camera everywhere** — The Georama bird's-eye camera (Select button) now works in every town map, not just the first five, along with the leftover developer fast-run speed while it's active. Exiting the camera safely snaps the player back to the ground.
- **Play as any ally in town** — Committing an ally in the town party menu now swaps your character **in place**: no reload, no respawn at the entrance, and town state (position, time, events) is untouched. Works in every walkable town. Fishing as an ally works too — the session runs normally and the ally is restored automatically afterward.
- **Dungeon-style party menu commit** — Selecting an ally in the town party menu now plays the dungeon's own switch sequence: the confirm chime, the character portraits spreading off-screen, and the menu closing itself. Locked allies still get the vanilla reject beep.
- **Full town animation sets for all five allies** — Each ally has a complete, hand-built town moveset: proper idle/run/walk, door opening, item pickup, ledge falls and landings, and a "no" refusal animation. Highlights: Xiao's cat form sits down when idle and gets battle-quality movement; Goro, Ruby, and Ungaga run with their dungeon-quality run animations; Ruby finally casts a shadow in town.
- **Quest talk menus** — Quest NPCs no longer carry a permanent "Do you have any sidequests?" line: while a quest is on offer, saying "Hello" starts it; while one is under way, the menu gains an "About the quest." line instead. Every other NPC's menu drops the quest line.
- **Per-ally ladder behavior** — Each ally handles ladders with their own animations: **Xiao** leaps up or down in one cat-like bound, **Goro** reproduces his treehouse cutscene climb (four quick hops up; crouch-and-spring jump down), **Ruby** floats up or down the ladder, **Osmond** dives off ledges and rides his helicopter backpack up (propeller deploy, spin, and stow included), and **Ungaga** flatly refuses at both ends. Refusals play in full — arms crossed, head shake — before control returns.

### Enemy System

- **Randomized enemies** — When enabled, each non-boss/event floor (normal + Ura) is staged with a fresh 9-species roster on dungeon entry, weighted toward the dungeon's native mimic and king mimic. Floors are staged lazily (at most once per visit, so backtracking never re-rolls) and reverted on dungeon exit. Out-of-place spawns are rescaled by the stat normalizer so they stay fair.
- **Themed floors** — A randomized floor has a chance to spawn a single themed group (cards, days of the week, dragons, …) instead of the random mix — either filling the whole floor with the group or capping it to one-of-each and backfilling with mimics or dungeon natives.
- **Spawn any species on regular floors** — Replacing spawn-table entity IDs/models lets any enemy — including bosses and minibosses (Master Utan, Minotaur Joe, Black Knight Mount, King's Curse, Ice Queen, Dark Genie final form) and mimics — appear on normal dungeon floors. Boss behavior scripts are patched in loaded memory so a non-native boss spawns at its floor position instead of snapping to its arena origin, and on death it collapses/fades and interrupts its motion instead of triggering a victory cutscene. Multi-part bosses are forced spawn-once (one skeleton).
- **Mesh-buffer guard** — Randomized rosters are budgeted against measured per-species model footprints so a floor never overruns the engine's mesh buffer.
- **Stat normalization** — An out-of-region spawn's HP, ABS, defense and damage are bounded by the region's own enemies: lifted to the pool average when it comes from an earlier region, capped at the highest pool max seen so far when it comes from a later one, never moved the wrong way. Look-alike families (`EnemySpecies.SimilarEnemies`, e.g. Cave Bat < Evil Bat < their Enhanced forms) keep their order on every floor. Bosses and boss support entities are left out. The Demon Shaft bands' back floors were off by one and are fixed.
- **Bomb Gemron** — A new species: an Ice Gemron recoloured in cold blues with amber accents, holding thrown bombs in place of its gems and fighting like a Holy Gemron. Immune to every element, 40 ABS, placed by the randomizer only. It throws bombs that burst like the Big Bang's shots, explodes like the Big Bang's drop when it dies (radius 50), and self-destructs when near death with the player close (it rears back as the spark walks the fuse and its bomb reddens under a growing glow, then the blast), staying hittable until it blows.
- **Crystal Gemron** — A new species: a Holy Gemron recoloured in pale blues with white eyes, holding breaking crystal balls in place of its gems. When it dies, the balls shatter upright in its grasp with their glints, light beams and rings. It fires the Ice Queen's homing ice arrow, which always freezes you and encases you in her ice prison until you break free. Immune to every element, 100 ABS, placed by the randomizer only.
- **Bomb knockback** — Bombs (thrown, trapped or an enemy's) knock the player away from the blast; they always threw him the same world direction.
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

Abilities marked come from the upstream mod; everything else is this fork's.

**Toan**
- **Mardan Eins** — Draws rare fish to the player's location at an interval weighted by their bait affinity. FP x1.2 for all non-Garayan fish.
- **Mardan Twei** — Reroll non-Garayan fish for an additional chance (same as native game's initial chance) to turn them into Mardan or Baron Garayan. Mardan Eins ability occurs at an increased rate. FP x1.5 for all non-Garayan fish.
- **Arise Mardan** — Smooths the native size distribution, then scales fish up to 2x their original size (larger initial sizes receiving a scale factor closer to 2x), then smooths again over the scaled range; final size is hard-capped at exactly 2x the species max. Mardan Eins ability occurs at an increased rate. FP x2 for all non-Garayan fish. Fishing records grant it bonus Max Magic (see Fishing).
- **Dagger** — TBD.
- **Baselard ("Heavy Hand")** — Every hit throws the enemy ~17 units. **Super Steve's sphere** gives every pellet the throw.
- **Gladius ("Jacket Hunter")** — Master Jacket kills give 4× ABS. **Super Steve's sphere**: the same.
- **Crysknife ("Crystal Affinity")** — Thrown gems deal 2× damage while it is wielded. **Super Steve's sphere**: the same.
- **Crysknife ("Circle Amplifier")** — Shared with Goro's Magical Hammer. Owning one (bag or storage) doubles every magic circle's effect; owning both triples it: attack-up lasts 30 → 60/90 s, WHP and stat rolls ×2/×3, enemy rage or slow 5 → 10/15 s, the WHP-loss circle drops WHP to 1, and the ABS-max and WHP-cure circles add 1/2 Powerup or Auto Repair Powder. Not passed on by a sphere.
- **Bone Rapier ("Gravedigger")** — Alongside its Skeleton Key door bypass, the reviving undead it kills stay down. **Super Steve's sphere** (Cross Hinder, Bone Rapier or Bone Slingshot): the same.
- **Bone Rapier ("Skeleton Key")** — Allows bypassing bone doors while equipped. **Super Steve's sphere** (Bone Rapier or Bone Slingshot): the same.
- **Shamshir ("Swift Strikes")** — Combo swings play 4/3× faster; charge attacks unchanged. Inherited by Dusack, 7 Branch Sword, Atlamillia Sword and Chronicle Sword. **Super Steve's sphere** (any of the five, or a Partisan's): the draw plays 1.6× faster and the shot fires sooner.
- **Small Sword ("Quick Draw")** — The opening combo swing comes out almost instantly, skipping the wind-up. Inherited by Tsukikage and Heaven's Cloud through the buildup line. **Super Steve's sphere** (also a Tsukikage or Heaven's Cloud sphere): the shot fires instantly on release.
- **Serpent Sword** — TBD.
- **Buster Sword ("Buster Boost")** — Anti-category attachments (Dinoslayer … Mage Slayer) are worth +4 instead of +3 when attached to a Buster Sword.
- **Kitchen Knife ("Spring's Blessing")** — Stepping into a healing spring blesses the knife for ~60 seconds: the blade visibly grows to triple length and its attack doubles. Standing in the spring refreshes the timer. Stepping into a spring also restores its WHP to max.
- **Sax ("Fine Fare")** — The floor's chests upgrade while it is drawn: water → Premium Water, bread/cheese → Premium Chicken. Dusack, 7 Branch Sword, Atlamillia Sword and Chronicle Sword inherit it, each adding its own upgrade. **Super Steve's sphere** (any of the five swords): that sword's tier.
- **Sand Breaker** — TBD.
- **Chopper** — TBD.
- **Wise Owl Sword ("Wise Owl Always Knows")** — While a Wise Owl Sword is owned, a message displays when you are near an enemy carrying one of the three keys in Wise Owl Forest.
- **Antique Sword** — TBD.
- **Dusack ("No Fool's Gold")** — Mimics and king mimics can be hit while they wake (their guard still blocks). Inherits Sax's Fine Fare and adds Treasure Key → Gold Bullion; inherits Shamshir's Swift Strikes. **Super Steve's sphere** (Dusack or Brave Ark): the same; Dusack's also Fine Fare at its tier and Swift Strikes.
- **Tsukikage ("Moonlit Focus")** — Charge attacks build twice as fast (lunge ready in ~0.25 s, whirlwind in ~0.75 s). Inherits Small Sword's Quick Draw. Inherited by Heaven's Cloud. **Super Steve's sphere** (also a Heaven's Cloud sphere): pellets fly 2× faster, plus Quick Draw.
- **Evilcise ("Jealous Soul")** — Applies curse immediately on equip (including from pause menu). Breaking the curse with holy water applies poison and sets HP to 1. Curse is reapplied on floor change; stripped on unequip or leaving the dungeon. **Super Steve's sphere** curses Xiao the same way.
- **Macho Sword ("Overtraining")** — While a Macho Sword is owned (bag or storage), every weapon's ABS keeps filling past its max — up to 2× — and the overflow carries into the next level, so a weapon starts its new level with a head start. (Replaces the old Shadow Boxing effect.)
- **Choora** — TBD.
- **Lamb's Sword** — TBD.
- **Drain Seeker** — TBD.
- **Sun Sword ("Solar Harvest")** — While wielding the Sun Sword, Big Bang or Sword of Zeus, every enemy killed has a 1% chance to drop a Sun attachment. **Super Steve's sphere** (any of the three): the same.
- **Sun Sword ("Solar Flash")** — Hold guard 1.5 s to prime the blade (the room dims while charging); the next swing floods the floor with light: every enemy within 300 units takes an elementless hit at 0.25× attack with a short knockback, and the whole floor is blinded for 5 s — enemies stop and guard, and while the light lasts nothing can block. 5 WHP. One flash at a time; an unused charge fades after 10 s. **Super Steve's sphere**: guard 2 s primes, and the next pellet carries the flash to wherever it lands (the struck enemy takes the pellet instead of the light hit).
- **Claymore ("Greatsword")** — The blade is 1.8× its size and reach (hit point 10.4 → 18.7 units), with the Baselard's throw on every hit. **Super Steve's sphere** gives the throw only.
- **Maneater ("Blood Price")** — Cursed each floor like Evilcise, but curing it with holy water carries no penalty (it just stays off until the next floor). While the sword's durability is critically low it drains 1 HP per second to restore durability (never fatal). **Super Steve's sphere** curses Xiao the same way; the drain restores Super Steve.
- **Aga's Sword ("Defensive Legacy")** — Grants Toan +15 defense while equipped; boost is re-applied if external changes alter defense. Removed on unequip. **Super Steve's sphere** gives Xiao the +15.
- **Brave Ark ("Hero's Courage")** — Resist Freeze, Poison, Curse, and Goo status effects. Inherits Dusack's No Fool's Gold. **Super Steve's sphere**: clears the same four statuses from Xiao, and No Fool's Gold.
- **7 Branch Sword ("Sevenfold Rite")** — Refuses to Status Break below +7; at +7 or higher the resulting SynthSphere keeps 77% of the weapon's stats instead of the normal 60%. The Status Break menu hint explains the rule. Inherits Shamshir's Swift Strikes, and Sax's Fine Fare with Dusack's upgrade plus Repair → Auto Repair Powder (25%). **Super Steve's sphere**: Swift Strikes and Fine Fare at its tier.
- **Heaven's Cloud ("Typhoon")** — Holding the whirlwind charge grows the blade — up to 3× at a full hold, with a flash at max — for a bigger, longer-reaching whirlwind. Also inherits Small Sword's Quick Draw and Tsukikage's Moonlit Focus. **Super Steve's sphere**: a 3 s held shot grows the pellet up to 8× at up to 1.5× attack, and its hit raises a wind blast — 60 → 160-unit radius with charge, 0.75× attack × (0.5 + 0.5 × charge) — with knockback; plus Quick Draw and Moonlit Focus.
- **Cross Hinder ("Sanctifier")** — Roughly double damage and double ABS reward against undead, and undead it kills can no longer revive. Also lock-on reach ×2, inherited by Big Bang and Sword of Zeus. **Super Steve's sphere** (Cross Hinder, Big Bang or Sword of Zeus): the reach.
- **Atlamillia Sword ("Atlamillia Insurance")** — While owned (bag or storage), a weapon breaking in a dungeon is no longer a total loss: an Atla appears on a random floor of that dungeon containing a SynthSphere of the broken weapon, keeping 10% of its stats per weapon level (up to 50% at +4 or higher). Attachments are lost. Collecting the Atla delivers the sphere like any georama Atla. Inherits Shamshir's Swift Strikes, and Sax's Fine Fare with Dusack's upgrade plus Repair → Auto Repair Powder (25%). **Super Steve's sphere**: Swift Strikes and Fine Fare at its tier.
- **Dark Cloud ("Guard Crush")** — Toan's hits cut straight through enemy guards; every blow connects even while an enemy is blocking. Inherited by 7th Heaven. **Super Steve's sphere**: her pellets pass every guard.
- **Big Bang ("Detonate")** — The whirlwind is a blast at Toan's feet: 4×/3×/2×/1× attack at 10/25/40/50 units, elementless, ~50-unit knockback, every guard broken, 20 WHP. Inherits the Sun Sword's Solar Flash at 0.5× attack; primed and locked on, a 2× copy of the blade hangs over the target and drops on the first swing — the landing is the same blast plus the flash and the 5 s blind, with every enemy turned to face it (20 WHP). Explosions (Halloween, self-destructs) can't hurt Toan while it is held. Inherits Cross Hinder's lock-on reach ×2. Replaces the old per-hit explosion. **Super Steve's sphere**: guard 3 s primes a 4× bomb over the locked target that drops on release for the blast, flash and blind (20 WHP; unlocked, the flash alone for 5 WHP); a 1 s charged shot throws a 2× bomb at 0.5–2× attack within 25 units (10 WHP); plain pellets are 1× bombs at pellet damage; explosions can't hurt her either; plus Solar Harvest and the lock-on reach.
- **7th Heaven ("Divine Guard")** — Perfect guard: blocks every enemy attack and projectile, including heavy hits that normally break guard (those just knock Toan back instead). Also inherits Dark Cloud's Guard Crush. Only while Toan is the active character. **Super Steve's sphere** gives Xiao the perfect guard and Guard Crush.
- **Sword of Zeus ("Lightning")** — Inherits the Sun Sword's Solar Harvest and Solar Flash (the flash itself does no damage; 5 s blind) and Cross Hinder's lock-on reach ×2. A bolt is half a Big Bang blast — 2×/1.5×/1×/0.5× attack at 10/25/40/50 units — through any guard, 10 WHP. Primed and locked on, the blade drops on the first swing and each later combo hit calls a bolt on the target; not locked on, one bolt on each of the nearest 6 enemies within 300 units (10 WHP for the volley). Its charge attack is always a lunge (no whirlwind); held 3 s past the lunge threshold it jumps 1.5× higher and lands a bolt where it comes down. **Super Steve's sphere**: guard 3 s primes — released unlocked, bolts on the nearest 6 within 300 units (10 WHP); locked on, a 5 s window in which every pellet that lands calls a bolt (10 WHP each); a 1 s charged shot calls a bolt wherever the pellet ends; bolt-calling pellets do no damage themselves; plus Solar Harvest and the lock-on reach.
- **Chronicle Sword** — Attacks hit all nearby targets for a percentage of damage. Inherits Shamshir's Swift Strikes, and Sax's Fine Fare with Dusack's upgrade plus Repair → Auto Repair Powder (25%). **Super Steve's sphere**: Swift Strikes and Fine Fare at its tier.
- **Chronicle 2** — While owned (bag or storage), every chest is a big chest, a Powerup Powder roll always stands (otherwise 80% are re-rolled), and the clown always rolls the weapon table.

**Xiao**
- **Wooden Slingshot** — TBD.
- **Bone Slingshot ("Skeleton Key", "Gravedigger")** — Bone doors open without their key, and the reviving undead stay down, as with the Bone Rapier. **Super Steve's sphere** (either bone weapon): the same.
- **Steel Slingshot ("Endurance Up")** — While its WHP is low (the gauge's warning state), each shot costs half the WHP; Durable and Fragile stack on top. Level-ups grant +2 endurance instead of +1 and twice the max-WHP roll. **Super Steve's sphere**: the low WHP effect only.
- **Steve** — TBD.
- **Bandit Slingshot ("Steal Shot")** — A steal that lands on an enemy with a projectile takes the projectile: until another steal or the floor ends, every pellet is that enemy's shot, flags and all, at 2× attack and with its own element. The item's "acquired" notice adds "[enemy]'s projectile is now yours". Self-detonations are never taken (a species' other shot is). **Super Steve's sphere** (either Bandit): the same.
- **Flamingo ("Lock-On Distance")** — Enemies can be locked on to from twice as far (inherited by Dragon's Y, Divine Beast Title, Angel Shooter and Angel Gear). **Super Steve's sphere** (any of the five, or Cross Hinder, Big Bang or Sword of Zeus): the same.
- **Flamingo ("Bait Flamboyance")** — While owned (Xiao's bag or storage), every bait's notice radius — the distance at which a fish turns toward the hook — grows 10 units per Flamingo, up to three.
- **Hardshooter ("Ricochet")** — A pellet that lands on an enemy spawns a second pellet at the impact that flies at the next nearest enemy (or in a random direction with none near), once per shot; it cannot strike the enemy it came from. A pellet that ends on a wall does not ricochet. **Super Steve's sphere**: the same.
- **Double Impact** — Every shot is two real pellets side by side, each at 0.75× attack and each rolling the weapon's abilities on its own, drawn as the Steel Slingshot's stone; both ricochet as the Hardshooter's do, at different targets. **Super Steve's sphere**: the same.
- **Matador ("Charging Bull")** — Hold the shot for a second (the charge flash marks it) and the pellet released is charged: 1.5× damage, it lands through an enemy's guard, and it hits with a hammer-swing shove that knocks the enemy back along its flight line. The charged pellet flies as a projection of the slingshot itself: an orange copy of the model at its own size, wrapped in the cat's glow, with the pellet hidden inside. **Super Steve's sphere**: the same, projecting its own model on the charged pellet.
- **Super Steve ("Sphere Inheritance")** — Super Steve inherits the custom effect of whichever weapon's SynthSphere is attached (one sphere at a time); the sphere's weapon icon shows on the dungeon HUD and a slingshot sphere lends its pellet sprite. Each inherited form is described with its weapon. Ownership passives and upgrade-related abilities (Macho Sword, Wise Owl Sword, Chronicle 2, Buster Sword, 7 Branch Sword, Crysknife) don't transfer.
- **Dragon's Y ("Dragon's Breath")** — Locked on, Xiao moves at 1.3× speed (inherited by Divine Beast Title, Angel Shooter and Angel Gear). Dragon's Breath: a shot held for a second fires the Gemron ball of the selected element (the Black Dragon's with none) at 1.5× attack; the weapon's abilities roll on it (the ball's own ailment is stripped), and its knockback comes from 4 units behind the burst along the flight line, so a head-on target is pushed onward. **Super Steve's sphere**: the shot; the speed with any of the four's.
- **Divine Beast Title ("Spirit Beast")** — Hold the shot 1 s (the charge flash marks it) and the pellet becomes Xiao's cat: it flies out along the shot, lands, walks at the locked-on enemy (else the nearest), and pounces. Touching an enemy in the air or on landing lands one hit at 2× attack with the weapon's magic and selected element, through any guard, with a sword blow's stagger and shove; the cat then fades out over 0.5 s. It times out after 20 s. It wears a glow in the weapon's colour; the rig is baked into Xiao's dungeon model by the ISO patcher, so it is resident in every dungeon. **Super Steve's sphere**: the same cat.
- **Angel Shooter ("Guardian Grace")** — While Xiao guards, the slingshot's heal ability keeps ticking and each tick tops her up to 4 HP, with a spring sparkle, a soft white flash and the heal chime. Inherits Divine Beast Title's Spirit Beast: its cat flies on wings, is 1.1× larger, pounces from farther and steers through the leap. **Super Steve's sphere**: the heal, and the cat at this size and range wearing a cape and mask in the slingshot's selected element colour instead of wings.
- **Angel Gear ("Guardian Reflector")** — While Xiao guards, a giant copy of the slingshot stands in front of her and orbits to face the nearest incoming shot or enemy. Enemy shots that reach its pouch are caught (drawn back with the weapon's own animation) and fired back at the nearest enemy, dealing damage through the normal weapon formula with the shot's own element or status: fireballs burn, sticky shots goo, poison gas poisons; resistances, immunities and the "No Effect" flash all apply. The slingshot is also a physical shield: enemies stop at it and swing at it instead of her. Five melee hits break it (weapon-break sound), and the attack gauge doubles as its health bar, refilling before it can return. Inherits Angel Shooter's Guardian Grace at 8 HP per tick with a golden burst and the change jingle, and Divine Beast Title's Spirit Beast: its cat flies on wings, is 1.2× larger, pounces from farther and steers through the leap. **Super Steve's sphere**: its own model stands as the shield, the 8 HP heal, and the cat at this size and range wearing a cape and mask in the slingshot's selected element colour instead of wings.
- **Angel Gear** — Applies Heal regeneration to all allies while equipped. **Super Steve's sphere** keeps the party heal.

**Goro**
- **Mallet** — TBD.
- **Frozen Tuna ("Cold Storage")** — Each point of WHP lost banks 2 HP into a healing pool. When Goro takes damage, the pool drains at 1 HP per 0.5 seconds. Healing pauses if HP reaches max; banked HP is preserved until the next hit. The pool resets on weapon repair or switch. On hit, 5% chance stops all non-ice enemies and freezes Goro for 3 seconds. Blizzard, Sam, and Ice Gemron are immune to the stop proc. **Super Steve's sphere**: the pool and the 5% stop work for Xiao (freezing her too).
- **Steel Hammer** — TBD.
- **Trial Hammer** — TBD.
- **Turtle Shell** — TBD.
- **Big Bucks Hammer** — TBD.
- **Plate Hammer** — TBD.
- **Magical Hammer ("Circle Amplifier")** — Shared with Toan's Crysknife. Owning one (bag or storage) doubles every magic circle's effect; owning both triples it: attack-up lasts 30 → 60/90 s, WHP and stat rolls ×2/×3, enemy rage or slow 5 → 10/15 s, the WHP-loss circle drops WHP to 1, and the ABS-max and WHP-cure circles add 1/2 Powerup or Auto Repair Powder. Not passed on by a sphere.
- **Battle Ax** — TBD.
- **Gaia Hammer** — TBD.
- **Last Judgement** — TBD.
- **Satan's Ax** — TBD.
- **Tall Hammer** — Gradually reduces enemy size on hit until they reach 30% of their original size. **Super Steve's sphere** shrinks the enemies her pellets hit.
- **Inferno** — Scales attack power with missing HP (up to +100%) and missing thirst (up to +50%).

**Ruby**
- **Gold Ring** — TBD.
- **Platinum Ring** — TBD.
- **Pocklekul** — TBD.
- **Bandit's Ring ("Steal Shot")** — The Bandit Slingshot's stolen projectile for Ruby's quick fire only; her charged shot stays her own. **Super Steve's sphere**: the stolen shot.
- **Fairy's Ring** — TBD.
- **Crystal Ring** — TBD.
- **Goddess Ring** — TBD.
- **Satan's Ring** — TBD.
- **Destruction Ring** — TBD.
- **Thorn Armlet** — TBD.
- **Athena's Armlet** — TBD.
- **Mobius Ring** — Increases damage output the longer Ruby charges an attack. **Super Steve's sphere**: damage ×1.5 per 1.5 s of hold (the pellet grows to match, up to 15×).
- **Secret Armlet ("Favoured Circles")** — While owned (bag or storage), every bad magic circle is dealt as its good counterpart: gilda, WHP, stat and element losses become the same gains, and enemy rage becomes every enemy slowed for 5 s. Replaces the equipped-only re-roll.

**Ungaga**
- **Fighting Stick** — TBD.
- **Javelin ("Marine Hunter")** — Marine enemies have 0 defense and give 2× ABS while it is equipped. **Super Steve's sphere**: the same.
- **5 Foot Nail** — TBD.
- **Halberd ("Tornado Charge Buff")** — Ungaga's held-charge shot (fired every 30 frames) flies farther and faster and is drawn larger: 1.4× travel, 1.1× size. Scorpion, Mirage, Cactus, Hercules' Wrath, Terra Sword and Babel's Spear inherit it at higher tiers. **Super Steve's sphere** (any of the seven): a 0.5 s charged shot at 1.5× attack and 2× WHP, flying faster and drawn larger by the sphere's tier.
- **DeSanga ("Vampire")** — Every kill restores the weapon 5 WHP. **Super Steve's sphere**: the same.
- **Scorpion ("Venom")** — Each Poison proc cures Ungaga's own poison and gives the weapon 50% of that enemy's ABS (capped at its max). Halberd's Tornado Charge Buff at 1.7× travel, 1.3× size. **Super Steve's sphere**: the same (curing Xiao), and the Tornado charged shot at its tier.
- **Scorpion ("Venom Lure")** — While a Scorpion is owned (any bag or storage), the Poisonous Apple notices fish from twice as far.
- **Partisan ("Quick Combo")** — Combo swings play 4/3× faster (Shamshir's factor); the charge keeps its pace. **Super Steve's sphere** gives Swift Strikes' shot speed.
- **Mirage ("Decoy")** — Holding guard 0.25 s plants a shimmering clone of Ungaga with a heat-haze effect; enemies chase it instead of him for 12 s (18 s when cast by Hercules' Wrath). The illusion breaks per enemy — hit one and it re-targets you — and a new hold hands off to a fresh decoy. The Mirage line (Mirage, Terra Sword, Hercules' Wrath, Babel's Spear) also locks on from 2× the distance. Only one targeting effect (decoy, shield ring, judgement blade) owns the enemies' target at a time; confusion yields to it. Halberd's Tornado Charge Buff at 2.0× travel, 1.5× size. **Super Steve's sphere** (Mirage or Hercules' Wrath): the decoy, and the Tornado charged shot at its tier.
- **Cactus ("Absorb")** — Hits restore thirst by damage ÷ 10 (100 damage = one water drop); dry species (rock, metal, undead) give nothing. **Super Steve's sphere**: pellet hits restore Xiao's thirst.
- **Cactus ("Desert Bloom")** — Hold guard 0.25 s: a 3× cactus rises 10 units ahead and stands 10 s as a solid column (radius 6), pricking enemies within 2 units of it for 1/6 attack every 0.25 s with a short throw; no WHP. Holding again moves it. Halberd's Tornado Charge Buff at 2.0× travel, 1.5× size. **Super Steve's sphere**: raises Queens' two trees instead — a wall (no damage) for 10 s; plus the Tornado charged shot at its tier.
- **Hercules' Wrath ("Air Strike")** — Level 1 is the Mirage decoy (0.25 s guard; it lasts 18 s). Keep guarding 5 s more and the room dims as the spear turns gold; the next swing summons a beam from above that dispels the decoy with a blast with 4×/3×/2×/1× attack at 20/50/80/100 units, through any guard, 20 WHP cost. Halberd's Tornado Charge Buff at 2.5× travel, 1.7× size. **Super Steve's sphere**: the beam strikes where her first primed pellet dies (20 WHP); the decoy comes with it, and the Tornado charged shot at its tier.
- **Terra Sword ("Big Rock")** — Hold guard 5 s to prime (the weapon turns green); locked on, Master Utan's boulder drops from 500 units (~2.4 s) onto the target: 4× attack to every enemy within 26 units, about twice Big Bang's knockback, through any guard, 10 WHP. The rock rests 20 s as a solid column and deals 1× attack per second to enemies under it. Halberd's Tornado Charge Buff at 2.5× travel, 1.7× size. **Super Steve's sphere**: drops a 2× nut on the locked target's head instead — 0.5× attack through guard, and the target is confused while the nut rests (~20 s); no blast; plus the Tornado charged shot at its tier.
- **Babel's Spear ("Curse of Babel")** — Hold guard 5 s: a 5× spear rises under the locked target (else the nearest enemy within 60 units, else 15 units ahead). The tip strikes for 1× attack with the Baselard's throw; the spear then stands 20 s spinning at 240°/s, its spikes hitting enemies within 2 units of the column for 1/6 attack every 0.25 s (no WHP), solid to enemies, shots and the player. Every non-boss enemy on the floor is confused until it fades (~24 s). Its own hits carry the new Confuse ability. Replaces the 6% Stop. Halberd's Tornado Charge Buff at 2.5× travel, 1.7× size. **Super Steve's sphere**: raises a 4× statue of Super Steve in the spear's place; its hands strike enemies within 6 units for 0.5× attack every 0.75 s; same 5 s charge and floor-wide confusion; plus the Tornado charged shot at its tier.

**Osmond**
- **Launcher** — TBD.
- **Machine Gun** — TBD.
- **Jackal** — TBD.
- **Launcher V2** — TBD.
- **Blessing Gun ("Blessed Bait")** — While a Blessing Gun is owned (any bag or storage), bait is only spent on a fight: it stays on the hook when the float sinks or a hooked fish gets off (the two bait-loss rolls fail, patched into the ISO).
- **Snail ("Slime Trail")** — 5% chance on hit to apply Gooey to the struck enemy. **Super Steve's sphere**: the same.
- **Skunk ("Longer Flame")** — The flamethrower reaches twice as far.
- **Swallow** — TBD.
- **G Crusher** — TBD.
- **Hexa Blaster** — TBD.
- **Star Breaker ("Shooting Stars")** — 2% chance on kill to receive an empty synthsphere. **Super Steve's sphere**: the same.
- **Supernova** — 11% chance per hit to apply a random status effect (Freeze, Poison, Stamina, or Gooey) to each enemy struck.

### Weapon Ability Changes

**Toan**
- **Evilcise** — 100% Poor
- **Macho Sword** — 100% Abs Up

**Xiao**
- **Bone Slingshot** — 50% Fragile
- **Hardshooter** — 50% Fragile
- **Matador** — 100% Critical

**Goro**
- **Frozen Tuna** — 100% Stop

**Ruby**
- **Goddess Ring** — 100% Heal
- **Athena's Armlet** — 100% Abs Up

**Ungaga**
- **DeSanga** — 100% Drain
- **Babel's Spear** — Confuse (new ability: 5% per hit, 20 s, bosses immune; see Game Mechanics)


### Weapon Stat Changes

**Toan**
- **Baselard** — Endurance set to 30.
- **Bone Rapier** — WHP set to 38, Magic set to 26
- **Small Sword** — WHP set to 35, Magic set to 17, Sea Killer removed, Metal Breaker set to 10
- **Kitchen Knife** — WHP set to 50, Attack set to 25, Endurance set to 30, Ice removed, Thunder set to 8, Sea Killer set to 90.
- **Sax** — Speed set to 60, Fire set to 6, Sky Hunter set to 10
- **Sand Breaker** — WHP set to 45, Endurance set to 25, third attachment slot
- **Chopper** — Speed set to 60
- **Antique Sword** — Speed set to 70, Fire set to 15.
- **Tsukikage** — Endurance set to 33, Speed set to 80
- **Choora** — WHP set to 57, Attack set to 45, Speed set to 70, Ice set to 10, Thunder set to 15, Undead Buster set to 15, Beast Buster set to 15, Metal Breaker set to 15, third attachment slot
- **Lamb's Sword** — Third attachment slot; transform and stats thresholds set to 50%
- **Drain Seeker** — WHP set to 60
- **Claymore** — Undead Buster set to 10, Beast Buster set to 10, Mage Slayer set to 10
- **Maneater** — Endurance set to 44, Speed set to 70, Magic set to 45, Ice/Thunder/Holy/Undead/Beast/Metal set to 15, Mimic Breaker set to 10
- **Aga's Sword** — Max attack 190
- **Brave Ark** — Third attachment slot
- **7 Branch Sword** — WHP set to 47, Endurance set to 47, Magic set to 37; Dino Slayer, Undead, Sea, Stone, Plant, Sky, Mimic set to 7; Beast Buster and Mage Slayer set to 8; Metal Breaker set to 10
- **Heaven's Cloud** — Max attack 180, max magic 180, third attachment slot
- **Cross Hinder** — Endurance set to 50, Speed set to 70, Magic set to 32
- **Big Bang** — Speed set to 70
- **Chronicle 2** — Max Attack set to 999

**Xiao**
- **Wooden Slingshot** — Attack set to 6, Magic set to 2, Fire set to 4
- **Bone Slingshot** — Attack set to 11, Endurance set to 30
- **Hardshooter** — Speed set to 60

**Goro**
- **Frozen Tuna** — WHP set to 65, max attack 100, max MP 678, fourth attachment slot
- **Trial Hammer** — Attack set to 30, Endurance set to 25
- **Turtle Shell** — Magic set to 10
- **Gaia Hammer** — Endurance set to 25

**Ruby**
- **Gold Ring** — Attack set to 15, Magic set to 30
- **Platinum Ring** — Attack set to 23
- **Pocklekul** — Attack set to 28, Magic set to 28, Holy removed
- **Bandit's Ring** — Attack set to 30, Max Attack set to 50, Magic set to 20
- **Thorn Armlet** — Max attack 90, max magic 72, Stone Breaker and Beast Buster set to 20

**Ungaga**
- **All weapons** — +10 Attack, +10 Max Attack, +15 Endurance
- **Babel's Spear** — Fourth attachment slot

**Osmond**
- **All weapons** — +15 Attack, +15 Max Attack
- **Blessing Gun** — Max attack 87, max magic 80
- **Skunk** — Max attack 143, max magic 105

## Weapon Buildup Paths

All buildup paths as modified by this mod. `★` marks final forms (no further buildup).

---

### Toan

> **Mod changes:** Choora builds up to Maneater only. Heaven's Cloud and Aga's Sword are terminal weapons.

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

Kitchen Knife
  ├─ Sax ─────────────────┐
  └─ Chopper ─┬─ Choora → Maneater → Atlamillia Sword → Chronicle Sword ★
               └──────────┴─ Dusack ─┬─ Brave Ark → Dark Cloud → 7th Heaven ★
                                      └─ 7 Branch Sword → Atlamillia Sword → Chronicle Sword ★

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

> **Mod changes:** Hardshooter builds up to Double Impact only (Matador path removed). Double Impact builds up to Matador only (was Divine Beast Title), so every Steel and Bandit line runs through Matador before Divine Beast Title.

```
Steel Slingshot → Hardshooter → Double Impact → Matador → Divine Beast Title → Angel Shooter → Angel Gear ★

Bone Slingshot → Flamingo → Dragon's Y → Divine Beast Title → Angel Shooter → Angel Gear ★

Bandit Slingshot ─┬─ Hardshooter → Double Impact → Matador → Divine Beast Title → Angel Shooter → Angel Gear ★
                  └─ Double Impact → Matador → Divine Beast Title → Angel Shooter → Angel Gear ★

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
