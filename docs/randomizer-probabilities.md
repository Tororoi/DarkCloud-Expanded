# Enemy Randomizer — Appearance Probabilities

_Generated 2026-06-25 from `Dark Cloud Improved Version/EnemyData.cs` + `EnemyModelInjector.cs` by Monte-Carlo simulation (300,000 floors per dungeon). Re-run `tools/randomizer-probabilities.py` after changing themes/membership._

## What this measures

For each dungeon, the probability that a given enemy is present **somewhere on a freshly-staged floor** (i.e. lands in that floor's spawn roster, so it spawns at least once). It is **per floor**, not per dungeon run — over many floors you'll see almost everything eligible. The simulation replays the exact `StageFloorRoster` → `BuildFloorRoster` / `BuildThemedRoster` logic, including the model-buffer budget, the distinct-fill, theme selection, `requireFullFit` eligibility, and the weighted mimic insertion.

## The decision tree (per floor)

```
floor
 ├─ 50%  NON-THEMED MIX  (BuildFloorRoster)
 │      • +dungeon Mimic      @ 40%   (weighted insertion)
 │      • +dungeon King Mimic  @ 28%
 │      • fill to 9 distinct species, uniform from the 119-strong
 │        eligible pool (RandomizerValid: all non-boss, non-mimic
 │        regulars incl. Demon-Shaft 'Enhanced' reskins), budget-capped
 │
 └─ 50%  THEMED          (BuildThemedRoster)
        • pick 1 theme from the eligible themes + the per-dungeon
          Mimics theme, WEIGHTED: non-native themes weigh 1; a
          dungeon-native theme (the Demon Shaft regions) ramps from
          0.10 (most distant dungeon) up to 1.0 at home, ×4 when home
        • 50% whole-group (all repeatable)  /  50% capped (1 each
          + repeatable mimic OR native fillers, 50/50)
```
Constants: `ThemeChance 0.50`, `MimicChance 0.40`, `KingMimicChance 0.28`, `ThemeCapOneChance 0.50`, `ThemeMimicFillChance 0.50`, roster = 9 entries, budget = `ModelBufferCapMin × 0.90`.

## Theme selection by dungeon

A `requireFullFit` theme (Cards, Days, Gemron Elementals, and the five elements) is only offered on a floor whose buffer fits the **whole** group; otherwise it's dropped that floor. Selection among the rest is **weighted**: a non-native theme weighs 1, while a Demon-Shaft region theme ramps from 0.10 (in DBC) up to 1.0 at home and ×4 in DS itself. The columns below show the pick chance (within the 50% themed half) of one non-native theme vs. one DS-region theme.

| Dungeon | Buffer budget | Eligible themes (of 33) | A non-native theme | A DS-region theme | requireFullFit themes excluded |
|---|--:|--:|--:|--:|---|
| DBC | 372,247 | 33 | 2.4% | 0.2% | — |
| WOF | 250,941 | 27 | 2.7% | 0.7% | Cards, Cards (Enhanced), Days of the Week, Gemron Elementals, Ice, Wind |
| SW | 308,073 | 32 | 2.4% | 0.9% | Gemron Elementals |
| SMT | 398,314 | 33 | 2.3% | 1.2% | — |
| MS | 268,732 | 31 | 2.3% | 1.6% | Days of the Week, Gemron Elementals |
| GoT | 291,787 | 31 | 2.3% | 1.9% | Days of the Week, Gemron Elementals |
| DS | 243,000 | 26 | 1.8% | 7.3% | Cards, Cards (Enhanced), Days of the Week, Gemron Elementals, Ice, Thunder, Wind |

_DS (Demon Shaft) has no measured `ModelBufferCapMin`; the runtime reads the live cap. This report assumes 270 KB for DS, so its numbers are an estimate._

## Headline findings

- **Mimics are the most common single enemies in their home dungeon** (~32–34% of floors for the Mimic, ~22–27% for the King Mimic) and never appear elsewhere.
- **Regular enemies cluster ~3–7% per floor**, with a tight spread now that nearly every enemy sits in 1–2 themes. The driver is theme count: a two-theme enemy (e.g. Pirate's Chariot — Pirates/Metal) edges out a one-theme enemy.
- **Base + Enhanced are merged into one row** (same `Id`, visually identical), so a creature's odds combine its base and reskin variants and the themes column shows every theme any variant is in (e.g. Gol — Precious + Thunder).
- **A dungeon's own natives get a home spike** from the capped-theme native fill (e.g. Rockanoff 12% in DBC vs ~4–5% elsewhere).
- **`requireFullFit` themes dip in tight-buffer dungeons**: Cards/Days/Gemron (and Ice/Wind/Thunder in the tightest) are dropped on WOF/MS/GoT/DS floors, so their members read lower there.
- **Demon Shaft region themes are proximity-weighted**: each is rare far from home (0.4% pick in DBC) and ramps up with depth, then jumps to a 4× home boost in Demon Shaft (9.8% vs ~2.4% for a non-native theme). Enemies that *only* live in DS themes (Gemrons, Bishop Q, Silver Gear) therefore skew toward the deeper dungeons.

## Per-enemy probability — regular enemies

One row per **visual enemy**: a base enemy and its Enhanced reskins (same `Id`) are merged, so the probability is how often you see *that creature* on a floor and the themes column lists **all** themes any of its variants belongs to. `Avg` = mean across the 7 dungeons' floors; per-dungeon columns are % of floors it appears on.

| Enemy | Id | Avg | DBC | WOF | SW | SMT | MS | GoT | DS | themes |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|
| Evil Bat | 61 | 10.6% | 11 | 9 | 9 | 11 | 9 | 15 | 10 | <sub>Demon Shaft: Holy, Halloween, Sky Dwellers</sub>
| Rockanoff | 77 | 9.1% | 15 | 7 | 8 | 9 | 7 | 8 | 10 | <sub>Demon Shaft: Ice, Rock</sub>
| Pirate's Chariot | 25 | 8.8% | 9 | 7 | 12 | 9 | 7 | 8 | 8 | <sub>Demon Shaft: Wind, Metal, Pirates</sub>
| Cave Bat | 60 | 8.8% | 14 | 7 | 8 | 9 | 7 | 8 | 9 | <sub>Demon Shaft: Thunder, Sky Dwellers</sub>
| Master Jacket | 1 | 8.4% | 13 | 7 | 8 | 10 | 7 | 8 | 7 | <sub>Demon Shaft: Fire, Graveyard Shift, Undead</sub>
| Statue Dog | 303 | 8.4% | 13 | 7 | 7 | 9 | 7 | 8 | 8 | <sub>Demon Shaft: Holy, Rock</sub>
| Crabby Hermit | 71 | 8.3% | 8 | 7 | 7 | 14 | 7 | 7 | 8 | <sub>Demon Shaft: Wind, Marine</sub>
| Gacious | 317 | 8.1% | 9 | 7 | 8 | 10 | 7 | 8 | 8 | <sub>Demon Shaft: Holy, Holy, Undead</sub>
| Cursed Rose | 68 | 8.1% | 8 | 7 | 11 | 9 | 7 | 7 | 8 | <sub>Demon Shaft: Wind, Plants</sub>
| Titan | 33 | 8.0% | 8 | 6 | 7 | 12 | 8 | 7 | 7 | <sub>Demon Shaft: Holy, Rock</sub>
| Skeleton Soldier | 3 | 7.8% | 13 | 7 | 8 | 10 | 6 | 7 | 5 | <sub>Graveyard Shift, Halloween, Undead</sub>
| Crescent Baron | 76 | 7.8% | 8 | 7 | 7 | 9 | 9 | 7 | 8 | <sub>Demon Shaft: Holy, Sky Dwellers</sub>
| Alexander | 43 | 7.7% | 8 | 6 | 7 | 9 | 7 | 9 | 7 | <sub>Demon Shaft: Wind, Metal</sub>
| Horn Head | 319 | 7.7% | 9 | 7 | 8 | 9 | 7 | 7 | 7 | <sub>Demon Shaft: Ice, Graveyard Shift, Holy, Undead</sub>
| Rash Dasher | 63 | 7.6% | 8 | 6 | 7 | 9 | 7 | 9 | 7 | <sub>Beasts, Demon Shaft: Thunder</sub>
| Steel Giant | 64 | 7.6% | 8 | 6 | 7 | 12 | 6 | 7 | 7 | <sub>Demon Shaft: Ice, Metal</sub>
| Gyon | 24 | 7.6% | 8 | 6 | 10 | 9 | 6 | 7 | 7 | <sub>Demon Shaft: Thunder, Marine</sub>
| Gol | 90 | 7.5% | 8 | 6 | 7 | 12 | 7 | 7 | 6 | <sub>Demon Shaft: Thunder, Pirates, Precious, Thunder</sub>
| Bomber Head | 49 | 7.5% | 8 | 6 | 7 | 11 | 6 | 7 | 7 | <sub>Bombs Away, Demon Shaft: Wind</sub>
| Space Gyon | 72 | 7.4% | 8 | 6 | 7 | 9 | 8 | 7 | 7 | <sub>Demon Shaft: Wind, Marine</sub>
| Arthur | 40 | 7.4% | 8 | 6 | 7 | 9 | 8 | 7 | 7 | <sub>Demon Shaft: Fire, Metal</sub>
| Living Armor | 55 | 7.4% | 8 | 6 | 7 | 9 | 6 | 8 | 7 | <sub>Demon Shaft: Holy, Metal</sub>
| Yammich | 301 | 7.3% | 12 | 5 | 6 | 8 | 6 | 6 | 8 | <sub>Demon Shaft: Ice, Holy</sub>
| Witch Hellza | 21 | 7.3% | 8 | 6 | 7 | 9 | 8 | 7 | 7 | <sub>Demon Shaft: Ice, Mages</sub>
| Silver Gear | 318 | 7.2% | 9 | 5 | 8 | 9 | 7 | 7 | 6 | <sub>Demon Shaft: Wind, Graveyard Shift, Undead, Wind</sub>
| White Fang | 56 | 7.1% | 8 | 6 | 7 | 8 | 8 | 7 | 6 | <sub>Beasts, Demon Shaft: Fire</sub>
| Joker | 48 | 6.9% | 8 | 4 | 7 | 9 | 7 | 8 | 6 | <sub>Cards, Cards (Enhanced), Demon Shaft: Holy</sub>
| Mask of Prajna | 75 | 6.9% | 7 | 5 | 10 | 8 | 6 | 6 | 7 | <sub>Demon Shaft: Thunder, Holy</sub>
| Spade | 47 | 6.9% | 8 | 4 | 7 | 9 | 7 | 9 | 5 | <sub>Cards, Cards (Enhanced), Demon Shaft: Thunder</sub>
| Club | 45 | 6.9% | 8 | 4 | 7 | 9 | 7 | 8 | 5 | <sub>Cards, Cards (Enhanced), Demon Shaft: Ice</sub>
| Lich | 51 | 6.8% | 8 | 5 | 6 | 9 | 6 | 8 | 6 | <sub>Demon Shaft: Holy, Undead</sub>
| Diamond | 46 | 6.7% | 8 | 4 | 7 | 9 | 7 | 8 | 5 | <sub>Cards, Cards (Enhanced), Demon Shaft: Fire</sub>
| Halloween | 10 | 6.7% | 8 | 6 | 6 | 8 | 6 | 6 | 6 | <sub>Demon Shaft: Fire, Halloween</sub>
| Heart | 44 | 6.7% | 8 | 4 | 7 | 9 | 6 | 8 | 5 | <sub>Cards, Cards (Enhanced), Demon Shaft: Wind</sub>
| Auntie Medu | 26 | 6.4% | 7 | 5 | 8 | 8 | 5 | 6 | 6 | <sub>Demon Shaft: Ice, Gorgon</sub>
| Mummy | 50 | 6.4% | 7 | 5 | 6 | 11 | 5 | 6 | 5 | <sub>Demon Shaft: Fire, Halloween</sub>
| Vulcan | 70 | 6.4% | 7 | 5 | 6 | 7 | 7 | 6 | 6 | <sub>Demon Shaft: Fire, Fire</sub>
| Sil | 91 | 6.4% | 7 | 5 | 6 | 10 | 5 | 6 | 6 | <sub>Demon Shaft: Fire, Pirates, Precious</sub>
| Haley Holey | 305 | 6.3% | 7 | 8 | 6 | 7 | 6 | 6 | 5 | <sub>Bait, Whack-a-Mole</sub>
| Werewolf | 7 | 6.3% | 7 | 7 | 6 | 7 | 6 | 6 | 4 | <sub>Beasts, Halloween</sub>
| Moon Digger | 66 | 6.2% | 6 | 6 | 6 | 7 | 8 | 6 | 5 | <sub>Bait</sub>
| Dark Flower | 67 | 6.1% | 7 | 6 | 6 | 7 | 5 | 7 | 5 | <sub>Not the Bees, Plants</sub>
| Dune | 32 | 6.0% | 6 | 5 | 6 | 10 | 5 | 6 | 4 | <sub>Rock, Wind</sub>
| Corcea | 28 | 5.9% | 6 | 4 | 8 | 7 | 5 | 5 | 6 | <sub>Demon Shaft: Ice, Pirates</sub>
| Captain | 27 | 5.9% | 6 | 4 | 8 | 7 | 5 | 5 | 6 | <sub>Demon Shaft: Thunder, Pirates</sub>
| Witch Illza | 22 | 5.9% | 6 | 7 | 6 | 6 | 5 | 6 | 4 | <sub>Bait</sub>
| King Prickly | 306 | 5.9% | 6 | 7 | 6 | 6 | 5 | 6 | 4 | <sub>Bait</sub>
| Dragon | 59 | 5.8% | 9 | 5 | 6 | 6 | 5 | 5 | 4 | <sub>Dragons, Fire</sub>
| Blue Dragon | 73 | 5.3% | 6 | 4 | 5 | 9 | 5 | 5 | 3 | <sub>Dragons, Ice</sub>
| Dasher | 6 | 5.2% | 9 | 5 | 5 | 5 | 4 | 5 | 4 | <sub>Beasts</sub>
| Opar | 304 | 5.2% | 9 | 4 | 5 | 5 | 4 | 4 | 4 | <sub>Marine</sub>
| Gunny | 23 | 5.2% | 5 | 5 | 8 | 5 | 4 | 5 | 4 | <sub>Marine</sub>
| FliFli | 8 | 5.1% | 5 | 7 | 5 | 5 | 4 | 5 | 4 | <sub>Plants</sub>
| Mr. Blare | 31 | 5.0% | 5 | 4 | 5 | 8 | 4 | 4 | 4 | <sub>Fire, Outlaws</sub>
| Cannibal Plant | 11 | 4.9% | 5 | 6 | 5 | 5 | 4 | 4 | 4 | <sub>Plants</sub>
| Curse Dancer | 52 | 4.9% | 5 | 4 | 5 | 5 | 4 | 7 | 3 | <sub>Mages</sub>
| Bishop Q | 316 | 4.8% | 5 | 4 | 5 | 6 | 4 | 5 | 5 | <sub>Demon Shaft: Thunder, Mages</sub>
| Nikapous | 308 | 4.7% | 5 | 4 | 5 | 5 | 4 | 5 | 5 | <sub>Demon Shaft: Fire, Mages</sub>
| Billy | 69 | 4.6% | 5 | 4 | 5 | 5 | 4 | 6 | 3 | <sub>Outlaws, Thunder</sub>
| Ghost | 42 | 4.5% | 9 | 3 | 4 | 5 | 3 | 4 | 3 | <sub>Halloween</sub>
| Sam | 85 | 4.5% | 5 | 3 | 7 | 5 | 4 | 4 | 3 | <sub>Ice, Outlaws</sub>
| Black Dragon | 74 | 4.4% | 5 | 4 | 4 | 5 | 4 | 5 | 3 | <sub>Dragons</sub>
| Gemron (Holy) | 315 | 4.2% | 5 | 3 | 4 | 6 | 3 | 4 | 4 | <sub>Demon Shaft: Holy, Gemron Elementals, Holy</sub>
| Earth Digger | 12 | 4.1% | 5 | 5 | 4 | 5 | 4 | 4 | 2 | <sub>Thunder, Whack-a-Mole</sub>
| Gemron (Thunder) | 313 | 4.1% | 5 | 3 | 4 | 6 | 3 | 4 | 4 | <sub>Demon Shaft: Thunder, Gemron Elementals, Thunder</sub>
| Gemron (Fire) | 311 | 4.0% | 5 | 3 | 4 | 6 | 3 | 4 | 4 | <sub>Demon Shaft: Fire, Fire, Gemron Elementals</sub>
| Phantom | 58 | 4.0% | 4 | 2 | 4 | 10 | 3 | 3 | 2 | <sub>Wind</sub>
| Hornet | 9 | 3.9% | 4 | 6 | 4 | 4 | 3 | 3 | 3 | <sub>Not the Bees</sub>
| Statue | 5 | 3.9% | 7 | 3 | 3 | 4 | 3 | 3 | 3 | <sub>Gorgon</sub>
| Gemron (Ice) | 312 | 3.9% | 5 | 2 | 4 | 6 | 3 | 4 | 4 | <sub>Demon Shaft: Ice, Gemron Elementals, Ice</sub>
| Gemron (Wind) | 314 | 3.8% | 5 | 2 | 4 | 6 | 3 | 4 | 3 | <sub>Demon Shaft: Wind, Gemron Elementals, Wind</sub>
| Moon Bug | 57 | 3.8% | 4 | 3 | 4 | 4 | 6 | 3 | 2 | <sub>Thunder</sub>
| Golem | 30 | 3.5% | 4 | 2 | 3 | 7 | 3 | 3 | 2 | <sub>Wind</sub>
| Blizzard | 65 | 3.3% | 4 | 2 | 3 | 4 | 3 | 5 | 2 | <sub>Ice</sub>
| Hell Pockle | 62 | 3.2% | 3 | 3 | 3 | 4 | 5 | 3 | 2 | <sub>Precious</sub>
| Saturday | 20 | 3.1% | 4 | 4 | 4 | 4 | 2 | 2 | 2 | <sub>Days of the Week</sub>
| Sunday | 14 | 3.0% | 4 | 3 | 4 | 4 | 2 | 2 | 2 | <sub>Days of the Week</sub>
| Friday | 19 | 3.0% | 4 | 3 | 4 | 4 | 2 | 2 | 2 | <sub>Days of the Week</sub>
| Monday | 15 | 3.0% | 4 | 3 | 4 | 4 | 2 | 2 | 2 | <sub>Days of the Week</sub>
| Tuesday | 16 | 3.0% | 4 | 3 | 3 | 4 | 2 | 2 | 2 | <sub>Days of the Week</sub>
| Wednesday | 17 | 3.0% | 4 | 3 | 4 | 4 | 2 | 2 | 2 | <sub>Days of the Week</sub>
| Thursday | 18 | 3.0% | 4 | 3 | 4 | 4 | 2 | 2 | 2 | <sub>Days of the Week</sub>

## Per-enemy probability — mimics (dungeon-locked)

Mimics only appear in their own dungeon, so `Avg` (across all 7) understates them; read the home-dungeon column.

| Enemy | Id | Avg | DBC | WOF | SW | SMT | MS | GoT | DS |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| Mimic (Gallery of Time) | 83 | 4.8% | 0 | 0 | 0 | 0 | 0 | 34 | 0 |
| Mimic (Divine Beast Cave) | 35 | 4.8% | 34 | 0 | 0 | 0 | 0 | 0 | 0 |
| Mimic (Sun & Moon Temple) | 37 | 4.8% | 0 | 0 | 0 | 34 | 0 | 0 | 0 |
| Mimic (Shipwreck) | 81 | 4.8% | 0 | 0 | 33 | 0 | 0 | 0 | 0 |
| Mimic (Demon Shaft) | 309 | 4.7% | 0 | 0 | 0 | 0 | 0 | 0 | 33 |
| Mimic (Wise Owl Forest) | 79 | 4.7% | 0 | 33 | 0 | 0 | 0 | 0 | 0 |
| Mimic (Moon Sea) | 39 | 4.6% | 0 | 0 | 0 | 0 | 32 | 0 | 0 |
| King Mimic (Divine Beast Cave) | 34 | 3.9% | 27 | 0 | 0 | 0 | 0 | 0 | 0 |
| King Mimic (Sun & Moon Temple) | 36 | 3.7% | 0 | 0 | 0 | 26 | 0 | 0 | 0 |
| King Mimic (Shipwreck) | 80 | 3.5% | 0 | 0 | 24 | 0 | 0 | 0 | 0 |
| King Mimic (Gallery of Time) | 82 | 3.3% | 0 | 0 | 0 | 0 | 0 | 23 | 0 |
| King Mimic (Wise Owl Forest) | 78 | 3.2% | 0 | 22 | 0 | 0 | 0 | 0 | 0 |
| King Mimic (Moon Sea) | 38 | 2.9% | 0 | 0 | 0 | 0 | 21 | 0 | 0 |
| King Mimic (Demon Shaft) | 310 | 2.9% | 0 | 0 | 0 | 0 | 0 | 0 | 20 |

## Caveats

- **Per floor, not per run.** Over a full dungeon you'll encounter most eligible enemies; these are single-floor odds.
- **"Appears" = in the roster.** A capped (SpawnCap 1) enemy shows up once; a repeatable one many times — both count as "appears".
- **DS budget is an estimate** (live-cap; not yet measured). DBC–GoT use measured `ModelBufferCapMin`.
- **Front-side roster modelled.** Non-themed floors roll the normal and Ura sides independently; this models one side per floor (what you meet on a visit).
- Monte-Carlo with 300,000 samples/dungeon → ~±0.2% absolute error.
