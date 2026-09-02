# Mod Quests

Every quest the Enhanced Mod adds: who gives it, what it needs, whether it repeats, and what it pays.
Source of truth: `SideQuestManager.cs`, `TownCharacter.cs`, `Dialogues.cs`, `CustomChests.cs`;
the mod window's **Quest Tracker** panel (`QuestTracker.cs`) displays the live state of the monster,
fishing, item-hunt, and Mayor quests.

Talking to a quest NPC once plays their intro; from the next talk on they assign quests. Progress
state lives in save-adjacent mod bytes, so active quests persist and are shown in the tracker.

## 1. Monster Slayer quests — 4 givers, repeatable

| Giver | Town |
|---|---|
| Macho | Norune Village |
| Gob | Matataki Village |
| Jake | Queens |
| Chief Bonka | Muska Lacka |

- **Task:** defeat **8–18** (rolled) of a random enemy type in a random dungeon **you have unlocked**.
  Each dungeon rolls from its own 4-species list (e.g. DBC: Master Jackets / Dashers / Mimics /
  Dragons). The four NPCs avoid assigning duplicate enemy types concurrently. The **first-ever Macho
  quest is always Dashers** in Divine Beast Cave.
- **Requires:** the target dungeon unlocked; kills count only while the quest is active.
- **Repeatable:** yes — a new quest is assigned on the next talk after turning one in. All four can
  run at once (one per giver).
- **Reward:** **Powerup Powder** (item 178), placed in the first free bag slot.

## 2. Fishing quests — 4 givers, repeatable

| Giver | Town | Waters |
|---|---|---|
| Pike | Norune Village | Norune Pond |
| Pao | Matataki Village | Matataki Pond / Waterfall / anywhere (rolled) |
| Sam | Queens | Queens Sea |
| Devia | Muska Lacka | Oasis |

Each assignment is a 50/50 roll between two types:
- **Count quest:** catch **2** (Mardan Garayan / Baron Garayan specials: 1) of a named species from
  that water's spawn list.
- **Size quest:** catch any fish inside a rolled size window (window 5–10 cm wide; base ranges:
  Norune/Matataki 80–145 cm, Queens 90–170 cm, Oasis 100–185 cm).

- **Requires:** access to the town's fishing spot (and a rod/bait as usual).
- **Repeatable:** yes, immediately after turn-in.
- **Reward: Fishing Points.** Count quests: a per-town roll × the fish count (Norune 35–65,
  Matataki 43–73 — **doubled** for Mardan/Baron, Queens 51–81, Oasis 60–90). Size quests:
  roll between (max−30)..max of the required size, plus a town bonus (+10 Matataki, +20 Queens,
  +30 Oasis).

### Sam's special reward (one-time)
Complete **3 of Sam's quests** (lifetime count) → the **fish become visible while fishing at the
Queens Sea**, on top of the normal Fishing Points payout.

## 3. Item-hunt (backfloor treasure) quests — 4 givers, one-time

Hearing the NPC's rumor flags a unique item into that dungeon's **backfloor** chest pool; finding it
completes the hunt (the NPC acknowledges it — the item itself is the reward and is yours to keep).

| Giver | Town | Where it hides | Item |
|---|---|---|---|
| Laura | Norune Village | Divine Beast Cave backfloors | **Medusa Powder** (171) |
| Ro | Matataki Village | Wise Owl Forest backfloors | **Warp Powder** (173) |
| Phil | Queens | Shipwreck backfloors | **Shell Ring** (243) |
| Zabo | Muska Lacka | Sun & Moon Temple backfloors | **Hardening Powder** (172) |

- **Requires:** hearing the rumor first (the item won't spawn before), then backfloor access
  (backfloor keys) in that dungeon.
- **Repeatable:** no.

## 4. Yellow Drops quests — 2 givers, one-time

Both NPCs require **all 6 allies recruited** ("find our Boss first") before offering anything.

### The puzzle — Magical Crystal
- **Task:** place the four items "people in Blue Terra needed" in the **first four bag slots in
  order**: **Gooey Peach (167), Escape Powder (175), Stamina Drink (150), Bomb (159)**.
- **Reward:** **Magical Crystal** (234) — while in inventory it acts like the dungeon Magical
  Crystal but **always active**; dungeon-found Crystals are replaced with extra loot from then on.

### The challenge — Map
- **Task:** clear **Moon Sea Floor 7's backside** using **only Toan**, armed **only with the
  Dagger**; healing fountains are disabled there.
- **Reward:** **Map** (233) — always-active dungeon Map while carried; dungeon-found Maps are
  replaced with extra loot.

## 5. Brownboo collection quest (Pickle) — one-time

- **Task:** reach **100% collection** — every obtainable item, attachment, ultimate weapon, and
  secret item, carried or in storage (quest items and stat-boost consumables excluded). Pickle
  reports progress every time you talk to him.
- **Reward:** **Flame Key** (248) — "try if you can use it somewhere" — and it unlocks the Norune
  Mayor's endgame quest line (below).

## 6. Norune Mayor endgame quests — repeatable until maxed

- **Requires:** Demon Shaft conquered (**Chronicle 2** owned) **and** Pickle's 100%-collection key.
  On unlock, the Mayor raises every ally's caps — **max HP 250, max thirst 12, max defence 99** —
  and stocks daily rotating shop items.
- **Task (each quest):** clear the **backside of a random Demon Shaft floor (1–99)** using **only
  the named ally** (rolled from all six). Backfloor keys are sold by the Fairy King.
- **Reward:** one stat-boost item you still need, rolled with need-weighting: **Fruit of Eden**
  (180, max HP), **Gourd** (182, thirst), or the ally-specific defence food (Fluffy Doughnut /
  Fish Candy / Grass Cake / Witch Parfait / Scorpion Jerky / Carrot Cookie, 136–141).
- **Repeatable:** yes — until **every ally's HP, thirst, and defence are maxed**, at which point the
  Mayor declares the quest line (and the mod's content) complete.

## How quest state persists

The mod writes **no files of its own**. All quest state lives in **unused padding bytes inside the
game's own save-data region** (the live `CSaveData` image in EE RAM, around the vanilla
fish-acquired flags at `0x21CE4439`): the per-NPC quest blocks `0x21CE4402–0x21CE4438`, the
item-hunt/Mayor/availability flags `0x21CE4451–0x21CE448F`, and the mod-options bytes
`0x21CE4490–0x21CE4495`. When the player saves, the game's own save `memcpy` serializes that whole
region to the memory card — the mod bytes ride along; on load they come back with it. (Bytes at
`0x21CE4496`+ are LIVE vanilla save data — never reused; the free range was verified by diffing a
new-game save against an old one — see `ModWindow.cs`.)

Consequences:
- **Mid-quest progress persists** — kill/fish counters are decremented in those save bytes, so an
  in-progress quest resumes exactly where it was.
- **Item rewards** ride the vanilla inventory/storage; **Fishing Points** are the game's own saved
  FP field.
- **Runtime flags are re-derived, not persisted**: e.g. Sam's fish-visibility reward is stored as
  save byte `0x21CE4430`; each fishing session the mod reads it and re-applies the live engine
  flag (`0x202A1FA0`). Systems that need a loaded save gate on `SaveDataPtr != 0`.
- The Quest Tracker panel just polls these bytes, so it shows the restored state after a reload.

⚠ One implication: quest state is **per save file** (it lives in the save), and loading an older
save rewinds quests with it.

### Padding capacity map (for adding new quests)

The mod's claimed span is `0x21CE43FE – 0x21CE4495`; **`0x21CE4496`+ is LIVE vanilla save data**
(hard boundary, verified by diffing a new-game save vs an old save — see `ModWindow.cs`). Current
allocations within the span:

| range | owner |
|---|---|
| `0x43FE` | Brownboo/Pickle flag |
| `0x4402–0x4438` | monster + fishing quest blocks (55 B, fully packed) |
| `0x4439–0x444A` | **vanilla** fish-acquired flags (18 B — game data mid-region) |
| `0x444B`, `0x444F`, `0x4450` | dialogue/quest flags |
| `0x4451–0x4455` | item hunts + Demon Shaft chest flag |
| `0x4459`, `0x445D`, `0x445E`, `0x4462` | Yellow Drops quests |
| `0x4463`, `0x4464`, `0x4468–0x446B` | Mayor chain |
| `0x446C` | cheat flag |
| `0x446D–0x446E` | Sword of Zeus attack counter (ushort) |
| `0x4474–0x447B` | quest-availability flags (8) |
| `0x447C–0x448A` | daily-shop item ushorts + flag (15 B) |
| `0x448B`, `0x448C` | game-cleared + first-quest flags |
| `0x4490–0x4492` | mod options (bit-packed) |

**Free: 29 bytes in nine gaps** — `0x43FF–0x4401` (3), `0x444C–0x444E` (3), `0x4456–0x4458` (3),
`0x445A–0x445C` (3), `0x445F–0x4461` (3), `0x4465–0x4467` (3), `0x446F–0x4473` (5, the only gap
that fits a monster-quest-style block contiguously), `0x448D–0x448F` (3), `0x4493–0x4495` (3, the
documented proven-free block).

In quest terms: ~29 more 1-byte hint quests, ~4 monster-slayer-style (6 B incl. availability), or
~2 fishing-style (9–11 B). Bit-packing one-time quests (2 bits each: not-started/heard/done)
stretches the same 29 bytes to ~116 quest states. Notes: only `0x4493–0x4495` are *documented*
proven-free; verify any other gap the same way (new-game vs old-save diff, claim only
zero-in-both bytes) before first use. Blocks need not be contiguous — field addresses are
independent constants. If more space is ever needed, extend **downward below `0x43FE`** with the
same verification; never upward past `0x4495`.

## Planned quests (not yet implemented)

**21 one-time quests: 15 attachment-buff quests** (reward: that buster/anti-enemy attachment type
now gives **+7**) **and 6 defence-item quests** (reward: an ally's defence food). Reward flags need
no storage — "+7 active" and "item given" are derived from quest status == done.

### Defence-item quests
| # | Name / giver | Requirement | Reward |
|---|---|---|---|
| 1 | **Old man and the sea** — Kye, Matataki | Catch a **Kaiji ≥ 450 cm** (Kaiji max size buffed to 480 cm while active) | Fish Candy |
| 2 | **Good things come in small packages** — giver TBD | Catch a **30 cm Piccoly** | Grass Cake |
| 3 | **The carrot and the stick** — giver TBD | Catch **100 Umadakara**. (Quirk, always on: Haley Holeys killed by Ungaga drop Carrot unless they have a forced drop) | Carrot Cookie + **DeSanga** weapon |
| 4 | TBD | TBD | Fluffy Doughnut |
| 5 | TBD | TBD | Scorpion Jerky |
| 6 | TBD | TBD | Witch Parfait |

### Attachment-buff quests (+7)
| # | Name | Requirement sketch | Buff |
|---|---|---|---|
| 7 | **Big fish in a small pond** | Norune Pond fish are vanishing → investigate (villager saw a huge shadow at night / Pike suspects a Gobbler eating the others; bait hint: regular bait won't work anymore). Fish at midnight, run 1 of each bait out → catch the big Gobbler | Dragon Buster +7 *(pond boss)* |
| 8 | **The exorcist** | Mummies overrun the Temple. Given an **unbreakable Cross Hinder**; kill **100 Mummies** with it | Undead Buster +7 |
| 9 | **Gone fishing** | Researcher wants deep-sea fish stomach biology → Shipwreck floors become Gyon/Gunny/Auntie Medu only; a Gyon miniboss drops **Frozen Tuna** | Sea Killer +7 |
| 10 | **Macho brothers' rock smashing** | Kill **15 Rockanoff** as **Toan with Dagger, without getting hit**; floors become Rockanoff-only while active | Rock Buster +7 |
| 11 | **Mushroom foraging** (Xiao only) | Kill **5 Flifli minibosses** on Xiao-limited floor 4; floor becomes Flifli/Cannibal Plant/King Prickly | Plant Buster +7 |
| 12 | **A Horse with No Name** | A distressed Umadakara stranded in Muska Racka: always spawns while active, flees the player, erratic speed, won't bite. Fish in early morning, catch all other fish first, then catch it **with a Carrot**; return it to Peanut Pond | Beast Buster +7 |
| 13 | **Whacka-mole** (Goro only) | Earth Diggers eat a forest carrot garden (floor 12): kill **100 Earth Diggers** as Goro; Goro-limited floor becomes Earth Digger/Haley Holey only | Flying Buster +7 *(reward is "pesticide that keeps Haley Holeys away")* |
| 14 | **Giant Hama Hama** | A disputed giant terrorizes the seas near Queens. Clues: hunts just before nightfall, eats only fish → fish at end of dusk with **Petite Fish** bait; catch all but one fish (or catch 10 first if caught fish can be respawned — TBD), then the last fish is the **390 cm Hama Hama** with bait affinity on | Metal +7 |
| 15 | **1000 Mimics** — Gina | "Mimics are cute — find me 1000!" Kill **1000 Mimics** → punchline reward: she gives you a **Mimi** ("those were monsters, not Mimics") | Mimic Buster +7 |
| 16 | **The soul-eating sword** | Shady guy in Queens seeks a mythical sword that eats souls → **sell him the Maneater**. (Possible extension: cursed-sword challenge — at 1 HP, beat 100 enemies without getting hit) | Mage Buster +7 |
| 17–21 | TBD (remaining buster types) | TBD | +7 each |

### Save-state budget (fits current padding)
Only quest **status** and **long-running counters** persist; floor swaps, fish behavior, no-hit
tracking, time-of-day gates, and bait-run-out (inventory-derived) are all runtime.

| layout | statuses (21 quests) | counters | total vs 29 B free |
|---|---|---|---|
| naive: 1 byte/status | 21 B | 8 B | 29 B — zero headroom |
| **bit-packed (recommended)**: 2 bits/status (3 bits for staged #7/#12/#14); status enum 0 never-talked / 1 intro / 2 active / 3 done | ~6–7 B | 8 B | **~15 B — half the region spare** |

Counters: #3 100 (1 B) · #8 100 (1 B) · #10 15 (1 B) · #11 5 (1 B) · #13 100 (1 B) · #16 100 (1 B) ·
**#15 1000 (2 B ushort — the only multi-byte field; place in the 5-byte gap `0x446F–0x4473`)**.
The 5 unsketched busters cost ~2 bits each + 1 counter byte only if kill-N. No separate
availability bytes — intro-heard is folded into the status enum.

## Quick reference

| Quest | Giver(s) | Repeatable | Reward |
|---|---|---|---|
| Monster Slayer | Macho, Gob, Jake, Chief Bonka | ✔ (4 concurrent) | Powerup Powder |
| Fishing (count / size) | Pike, Pao, Sam, Devia | ✔ (4 concurrent) | Fishing Points |
| Sam ×3 milestone | Sam | one-time | Visible fish at Queens Sea |
| Backfloor item hunts | Laura, Ro, Phil, Zabo | one-time each | Medusa/Warp/Hardening Powder, Shell Ring |
| Yellow Drops puzzle | YD villager | one-time | Always-active Magical Crystal |
| Yellow Drops challenge | YD villager | one-time | Always-active Map |
| 100% collection | Pickle (Brownboo) | one-time | Flame Key + Mayor unlock |
| Mayor ally challenges | Norune Mayor | ✔ until stats maxed | Eden / Gourd / defence foods + raised caps |
