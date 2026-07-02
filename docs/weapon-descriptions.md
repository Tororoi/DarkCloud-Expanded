# Weapon descriptions (in-game menu text)

Where the in-game weapon description text lives, its encoding, and the constraints for
replacing it (goal: show each weapon's custom-effect ability in its description).
Decoder/encoder: `tools/mes_decode.py` (a clean dump of the blob is `tools/allmenu.mes.bin`).

## Where it lives on disc

`data.dat` → the menu paks under **`commenu\a_usa\`** (the directory the USA disc actually loads;
`a_eng` is the European English set, `a_jpn`/`a_fre`/`a_ger`/… the others; the bare `commenu\*.pak`
copies are JP-era leftovers). Each pak embeds its own `allmenu.mes` sub-file (pak sub-file header =
0x50 bytes: name@+0, u32 dataSize@+0x44, u32 nextSubfileOffset@+0x48). The copies are **NOT
byte-identical** — locate entries by INDEX (count 0xFF01 terminators), never by byte offset.
USA-English variants (all verified: **Heaven's Cloud = ordinal 218 in every copy**):

| pak(s) | entries | field2 | note |
|---|---|---|---|
| `a_usa\emenu.pak`, `option.pac`, `savetex.pak` | 0x1EF | 0xB614 | reference copy (tools/allmenu.mes.bin) |
| `a_usa\dungeon\dunmenu5.pak` | 0x1EF | 0xB612 | seen live in town + dungeon menus |
| `a_usa\dungeon\dunmenu4.pak` | 0x1F1 | 0xB704 | 2 extra entries (after the weapon block) |
| `a_usa\map\*.pac` (6) | 0x1EF | 0xB5B2 | world-map menus |
| `a_usa\dungeon\dunmenu3.pak`, `dunmenu_chk.pak` | 0x1F1 | 0xB77E | ⚠ DIFFERENT glyph encoding: plane 0x00, lowercase = ascii−0x22 — do NOT write 0xFD-plane text into these; detect the plane byte and skip/re-encode |

`meswin\sysarticle_*.mes` etc. are OTHER text (pickup "… acquired." messages); the weapon
descriptions are only in `allmenu.mes`.

## Text encoding (16-bit LE codes; high byte = plane)

| Plane | Meaning |
|---|---|
| `0xFDgg` | glyph: `0x01-0x3A` = `'!'..'Z'` (ascii−0x20); `0x3B-0x54` = `'a'..'z'` (ascii−0x26); `0x55`=' `0x57`=" `0x5B`=& `0x5D`=- `0x5F`=/ `0x61`=( `0x62`=) `0x6C`=, `0x6D`=. ; `0x6F-0x78` = `'0'..'9'` |
| `0xFF00 / 0xFF01 / 0xFF02` | newline / END-of-message / space |
| `0xFBxx` | icon/placeholder — `0xFBFF` right after a weapon name renders its level/icon |
| `0xFCxx` | color (`02`/`03` set, `00` reset); `0xF8xx` = button glyphs |

## Entry format & layout

`.mes` = `u16 count, u16 ?, count×(u16 id, u16 off)` header, then the glyph stream. In
practice enumerate entries by scanning `0xFF01` terminators (entry# below). A weapon entry:

```
"Name"[FBFF] ␊ line1 ␊ line2 [␊ line3] [FF01]     (lines ≈ 24 chars max, 2-3 lines)
```

- Toan swords batch 1: **entry#204 (Dagger[broken]) … #223 (AtlamilliaSword)** — for item
  ids 257-276, entry# == itemId − 53 (Heaven's Cloud 271 → #218).
- All other families follow (slingshots #224+, hammers #236+, armlets #249+, lances #261+,
  guns #273+); **2nd-half weapons are a separate batch at entry#453-#487** (MardanEins …
  SecretArmlet). 111 weapon entries total.

## Replacement constraints

- Text is offset-addressed → **in-place replacement only**: the new encoded text must fit
  the original entry's byte span (see `len=` in the tool dump; e.g. HC = 132 bytes ≈ 61
  glyphs incl. name+controls). Pad with spaces (`0xFF02`) or terminate early (`0xFF01`).
- On-disc patching would mean editing ~17 pak copies; **runtime patching of the loaded
  copy in EE RAM is the route**, via a FIXED pointer (no scan, no static ELF table — verified
  by glyph-encoded search of SCUS_971.11/dun.bin: the text exists only in the paks).

## Second bank: the shop menu (`itemshop.bin`)

The SHOP's weapon descriptions come from a different bank: **`itemshop.bin`** inside
`commenu\a_usa\itemshop.pak` (`count=0x137`, `field2=0x78BE`), registered into the same ClsMes slot.
It contains the same 111 weapon entries — descriptions byte-identical (same budgets) — at
**ordinal = allmenu entry# − 182** (verified for all 111). `shopman.bin` (count 0x1D5) is shopkeeper
dialogue, no weapon entries. The patcher recognizes both banks by entry count.

⚠ Async-load gotcha: the ClsMes pointer registers BEFORE the BG read fills the buffer (first read at a
fresh pointer often sees count=0). Classify on the (pointer, count) PAIR and re-check when either
changes; a patch racing the tail of a load is self-healed by the signature-revert check next tick.

## Runtime pointer chain (RE'd from BattleMenuTexEnter 0x1F59C0 / InitMenuMesSet 0x22C140)

Every menu screen's `*TexEnter` loader (battle menu, shop, save, fish, dun-enter, world map,
chara-change…) loads its own pak and re-registers its `allmenu.mes` copy:

```
buf = GetPackFile(GetReadBGFile()->pakData(+0x8C), "allmenu.mes")
InitMenuMesSet(i, buf) → SetBuff(ClsMes, buf)   // stores buf at ClsMes+0x17A0
SetBuffInfo(MenuClsMes 0x1D903F0, buf)
```

`SetBuff` fills four FIXED ClsMes globals (`0x1DA0B90/0x1DA2350/0x1DA3B10/0x1DA52D0`), so the
live text base is readable at **MMU `0x21DA2330` / `0x21DA3AF0` / `0x21DA52B0` / `0x21DA6A70`**
(all hold the same pointer; `+0x17A8` = base + `*(u16*)(buf+2)`). Message lookup for a weapon:
`ItemGetMes` (0x160290) → entry index = `ComItemInfo[itemId]+0x06` (short) + 100 (HC 271 →
118+100 = entry#218 ✓). Because each menu screen re-registers its own copy, a patch must be
**re-applied per menu load** (lazy maintain: deref the pointer, check a signature word in one
patched entry, rewrite if vanilla) — one-shot at game entry (WeaponsBalanceChanges-style timing)
will not stick. The base "system" bank is separate (fixed buffer `0x1CFCE00`, `SystemMes` sym,
ClsMes `0x1CFB630+0x17A4`), as is the event-bank table `DAT_01d3d3d0[8]` (`GetMes__Fi`).

## Current vanilla text (weapon entries — a_usa\emenu.pak copy)

**budget** = the writable description span in glyphs/characters (`\n` and spaces count 1 each) —
the maximum length of a `WeaponData.ModDescription` for that weapon (the patcher truncates + logs
beyond it). The quoted name line is preserved and does not count. Spans can vary by a few chars
between pak copies; the patcher measures the live copy each time, so treat these as the guide.

    entry#204 budget=30  "Dagger?"                   | Broken dagger due to | hard use.
    entry#205 budget=35  "Dagger[fbff]"              | Fairly easy to use. | default weapon.
    entry#206 budget=54  "Baselard[fbff]"            | A big single-edged | sword with long reach. | Easy to use.
    entry#207 budget=54  "Gladius[fbff]"             | Light-wt double-edged | sword. Fairly high | attack power.
    entry#208 budget=43  "Wise Owl Sword[fbff]"      | Legendary sword made | by Wise Owl's artisan.
    entry#209 budget=26  "Crystal Knife[fbff]"       | Stylet with magical | power.
    entry#210 budget=40  "Antique Sword[fbff]"       | Sword of antiquity | made a long time ago.
    entry#211 budget=28  "Buster Sword[fbff]"        | Double-edged powerful | sword.
    entry#212 budget=59  "Kitchen Knife[fbff]"       | Low attack power but | causes major damage | to water monsters.
    entry#213 budget=37  "Tsukikage[fbff]"           | Sword with sacred | power of moon beam.
    entry#214 budget=51  "Sun Sword[fbff]"           | Legendary sword with | power to cut through | darkness.
    entry#215 budget=54  "Serpent Sword[fbff]"       | Sword with absolute | power over snakes. | Extremely rare.
    entry#216 budget=54  "Macho Sword[fbff]"         | Sword with muscle power. | Arming it makes you | exercise.
    entry#217 budget=55  "Shamshir[fbff]"            | Characterized by its | curved edge.Light and | easy to use.
    entry#218 budget=47  "Heaven's Cloud[fbff]"      | Belongs to Katanas.Has | the strongest potential.
    entry#219 budget=50  "Lamb's Sword[fbff]"        | Timid.Sword like a lamb. | Appears to be weak but...
    entry#220 budget=37  "Dark Cloud[fbff]"          | Magical Sword with | power of darkness.
    entry#221 budget=53  "Brave Ark[fbff]"           | Legendary treasure that | was revived after many | years.
    entry#222 budget=52  "Big Bang[fbff]"            | Star energy is condensed | and creates sword's | energy.
    entry#223 budget=50  "Atlamillia Sword[fbff]"    | Sword that inherited | Atlamillia,the legendary | gem.
    entry#224 budget=40  "Wooden Slingshot?"         | Broken wooden slingshot | due to hard use.
    entry#225 budget=46  "Wooden Slingshot[fbff]"    | Slingshot made by | carving wood.Default | weapon.
    entry#226 budget=36  "Steel Slingshot[fbff]"     | Slingshot made of | steel.Looks heavy.
    entry#227 budget=54  "Bandit Slingshot[fbff]"    | Sometimes you can steal | items from monsters with | this.
    entry#228 budget=57  "Steve[fbff]"               | Mysterious Talking | Slingshot.Nobody knows | its true color.
    entry#229 budget=38  "Bone Slingshot[fbff]"      | Slingshot made of bone. | A bit fragile.
    entry#230 budget=41  "Hardshooter[fbff]"         | Launches powerful shot | but a bit fragile.
    entry#231 budget=45  "Double Impact[fbff]"       | Allows shooting two | bullets at the same | time.
    entry#232 budget=30  "Dragon's Y[fbff]"          | Slingshot with dragon's | power.
    entry#233 budget=48  "Divine Beast Title[fbff]"  | Having this gives you | the title of Divine | Beast.
    entry#234 budget=48  "Angel Shooter[fbff]"       | Angel's slingshot.Looks | cute but ultra powerful.
    entry#236 budget=30  "Mallet?"                   | Broken Mallet due to | hard use.
    entry#237 budget=44  "Mallet[fbff]"              | Light-weight and easy | to use.Default weapon.
    entry#238 budget=50  "Steel Hammer[fbff]"        | Hammer made of steel. | A little heavy but | powerful.
    entry#239 budget=59  "Magical Hammer[fbff]"      | Hammer with magical | power.Makes attribute | attack effective.
    entry#240 budget=60  "Battle Axe[fbff]"          | Cut any enemy into half | with single stroke. | A Hunter's must.
    entry#241 budget=28  "Turtle Shell[fbff]"        | Hammer made of turtle | shell.
    entry#242 budget=42  "Big Bucks Hammer[fbff]"    | Magical hammer.Stroking | it makes you rich.
    entry#243 budget=56  "Frozen Tuna[fbff]"         | Frozen like a rock. | You can't eat it.Use | it as a weapon.
    entry#244 budget=40  "Gaia Hammer[fbff]"         | Strongest hammer with | the power of Gaia.
    entry#245 budget=55  "Last Judgement[fbff]"      | Helluva strong hammer | loved by the gate keeper | of hell.
    entry#246 budget=55  "Tall Hammer[fbff]"         | Hammer with fearful | power generated by | monstrous curse.
    entry#247 budget=34  "Satan's Ax[fbff]"          | Ax used by dreadful | Satan of hell.
    entry#249 budget=28  "Gold Ring?"                | Broken ring due to | hard use.
    entry#250 budget=40  "Gold Ring[fbff]"           | Expensive golden armlet. | Default weapon.
    entry#251 budget=55  "Bandit's Ring[fbff]"       | Nice armlet that steals | items from enemy during | attack.
    entry#252 budget=40  "Crystal Ring[fbff]"        | Expensive ring made of | precious crystal.
    entry#253 budget=48  "Platinum Ring[fbff]"       | Durable armlet with | pretty strong magical | power.
    entry#254 budget=26  "Goddess Ring[fbff]"        | Armlet blessed by | goddess.
    entry#255 budget=33  "Fairy's Ring[fbff]"        | Legendary armlet made | by fairies.
    entry#256 budget=58  "Destruction Ring[fbff]"    | Armlet of darkness with | power that can destroy | everything.
    entry#257 budget=38  "Satan's Ring[fbff]"        | Ring with fearful spell | made by Satan.
    entry#258 budget=43  "Athena's Armlet[fbff]"     | Armlet with sacred | power of goddess Athena.
    entry#259 budget=50  "Mobius Ring[fbff]"         | Armlet absorbs power | from infinite space | and time.
    entry#261 budget=38  "Fighting Stick?"           | Broken Fighting Stick | due to hard use.
    entry#262 budget=58  "Fighting Stick[fbff]"      | A stick used in combat | by desert warriors. | Default weapon.
    entry#263 budget=35  "Javelin[fbff]"             | Light-weight and easy | to use lance.
    entry#264 budget=30  "Halberd[fbff]"             | Powerful lance with | axe blade.
    entry#265 budget=42  "DeSanga[fbff]"             | Light-weight and easy | to use forked lance.
    entry#266 budget=55  "Scorpion[fbff]"            | Lance with poisoned | blade.Puts enemy in | poisoned state.
    entry#267 budget=33  "Partisan[fbff]"            | Lance with sharp and | thick blade.
    entry#268 budget=46  "Mirage[fbff]"              | Legendary lance made | by ancient desert | people.
    entry#269 budget=41  "Terra Sword[fbff]"         | Strongest lance made | by fairies of Terra.
    entry#270 budget=49  "Hercules' Wrath[fbff]"     | Legendary lance with | dreadful destructive | power.
    entry#271 budget=36  "Babel's Spear[fbff]"       | Sacred lance that | slashes even time.
    entry#273 budget=35  "Machine Gun(broken)"       | Broken machine gun | due to hard use.
    entry#274 budget=39  "Machine Gun[fbff]"         | A powerful blazing gun. | Default weapon.
    entry#275 budget=21  "Jackal[fbff]"              | Upgraded machine gun.
    entry#276 budget=52  "Launcher[fbff]"            | Max. of four missiles | can be consecutively | launched.
    entry#277 budget=50  "Launcher V2[fbff]"         | Max. of 8 missiles can be | consecutively launched.
    entry#278 budget=52  "Blessing Gun[fbff]"        | Device emits magical | attribute.Attacks | various ways.
    entry#279 budget=45  "Skunk[fbff]"               | Upgraded blessing gun. | Spell emitting device.
    entry#280 budget=45  "G Crusher[fbff]"           | Emits powerful shot. | Great destructive power.
    entry#281 budget=47  "Hex aBlaster[fbff]"        | Powerful beam cannon | that emits magical | energy.
    entry#282 budget=53  "Star Breaker[fbff]"        | Machine gun weapon. | So powerful that it | breaks stars.
    entry#283 budget=52  "Supernova[fbff]"           | Strongest Beam-type | weapon. Filled with | space power.
    entry#453 budget=40  "Mardan Eins[fbff]"         | Sword dwelled in by | Lucky MardanGarayan.
    entry#454 budget=41  "Mardan Twei[fbff]"         | Sword dwelled in by | Lucky Mardan Garayan.
    entry#455 budget=40  "Arise Mardan[fbff]"        | Sword dwelled in by | Lucky MardanGarayan.
    entry#456 budget=34  "Aga's Sword[fbff]"         | Sword owned by | legendary Hero Aga.
    entry#457 budget=38  "Evilcise[fbff]"            | Single-edged sword | with magical power.
    entry#458 budget=28  "Small Sword[fbff]"         | Light and easy to use | sword.
    entry#459 budget=35  "Sand Breaker[fbff]"        | Made of desert sand. | Absorbs water.
    entry#460 budget=40  "Drain Seeker[fbff]"        | Feared magic sword. | Absorb health power.
    entry#461 budget=29  "Chopper[fbff]"             | Light and easy to use | dagger.
    entry#462 budget=35  "Choora[fbff]"              | A dagger with pretty | pattern on it.
    entry#463 budget=38  "Claymore[fbff]"            | Big & double-handed. | Heavy & powerful.
    entry#464 budget=37  "Maneater[fbff]"            | Fearful sword that | eats human souls.
    entry#465 budget=30  "Bone Rapier[fbff]"         | Made of bone. | Ability unknown.
    entry#466 budget=32  "Sax[fbff]"                 | Easy to use | single-handed sword.
    entry#467 budget=36  "7 Branch Sword[fbff]"      | Ancient sword with | mysterious power.
    entry#468 budget=36  "Dusack[fbff]"              | Unique single-hand | sword. Cuts well.
    entry#469 budget=42  "Cross Hinder[fbff]"        | Very powerful double | hand treasure sword.
    entry#470 budget=37  "Flamingo[fbff]"            | Unique slingshot | shaped like a bird.
    entry#471 budget=41  "Matador[fbff]"             | Slingshot made of ox | horn. Very powerful.
    entry#472 budget=34  "Plate Hammer[fbff]"        | Hammer made of steel | plate. Light.
    entry#473 budget=38  "Trial Hammer[fbff]"        | Blood & sweat hammer | w/warriors' soul.
    entry#474 budget=38  "Pocklekul[fbff]"           | Bracelet that honors | dwarf of forest.
    entry#475 budget=29  "Thorn Armlet[fbff]"        | Bracelet made of | woven thorn.
    entry#476 budget=42  "5 Foot Nail[fbff]"         | Cursed lance. A stab | may finish off enemy.
    entry#477 budget=41  "Cactus[fbff]"              | Pole weapon w/cactus | ball. Smashes enemy.
    entry#478 budget=37  "Snail[fbff]"               | Gun made of shell. | Fires magic bolts.
    entry#479 budget=42  "Swallow[fbff]"             | Enhanced machine gun. | Light & easy to use.
    entry#480 budget=35  "7th Heaven[fbff]"          | Beautiful sword. | Tears the heavens.
    entry#481 budget=36  "Sword Of Zeus[fbff]"       | Zeus' swords with | unlimited ability.
    entry#482 budget=32  "Chronicle Sword[fbff]"     | Worlds crumble | before its might.
    entry#483 budget=34  "Chronicle 2[fbff]"         | Proof of subjugation. | Demon Shaft.
    entry#484 budget=37  "Super Steve[fbff]"         | Powerful mysterious | Slingshot, Steve.
    entry#485 budget=36  "Angel Gear[fbff]"          | Magnum slingshot | Unimaginable power.
    entry#486 budget=30  "Inferno[fbff]"             | Great Hammer with | Hell's fire.
    entry#487 budget=42  "Secret Armlet[fbff]"       | Bracelet sealed with | origin of everything.
