#!/usr/bin/env python3
"""Generate docs/dungeon-tiles.md — how dungeon tiles work, and whether any are unused.

Two independent questions, answered separately because they have different answers:
  1. TILE ART   — is any mesh shipped in a .mpd but never named by a floor .cfg?     (answer: almost none)
  2. TILE PARTS — is any DEF_PATS part defined but never PLACED by the map builder?  (answer: a large set,
                  but see the caveat — not every placement path is traced yet)
"""
import os
import struct, re

# Extracted Dark Cloud disc dir; required — see .env.sample.
DC1_DATA_DIR = os.environ.get("DC1_DATA_DIR")
if not DC1_DATA_DIR: raise SystemExit("Set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")

HED = os.path.join(DC1_DATA_DIR, "data.hed")
HD2 = os.path.join(DC1_DATA_DIR, "data.hd2")
DAT = os.path.join(DC1_DATA_DIR, "data.dat")
ELF = os.path.join(DC1_DATA_DIR, "SCUS_971.11")
OUT = "docs/dungeon-tiles.md"

# The chain tables: {neighbour-mask, part id, rotation}. vaddr -> (name, entries)
CHAIN = {0x279EE0: ("chainTableRoad", 15), 0x279FA0: ("chainTableRoom", 8),
         0x279EB0: ("chainTableDoor", 4),  0x27A000: ("chainTableDivid", 6),
         0x27A050: ("chainTableDividDoor", 2)}
# Parts the builder writes DIRECTLY (traced in mapPartsFilter / setUnderDungeonStart / BuildCharaSpecialParts)
# Every part the engine can put in a grid cell, and who puts it there. Traced exhaustively: FindXrefs on
# buildMapDat (0x1D56DA0) gives every writer, and BuildCharaSpecialParts writes the grid (+0x9C50) directly.
DIRECT = {17: "buildRoom — room interior",
          30: "mapPartsFilter", 31: "mapPartsFilter — 次の階へ (stairs to the next floor)",
          32: "mapPartsFilter — 回復の泉 (healing spring)", 40: "mapPartsFilter",
          45: "mapPartsFilter", 46: "mapPartsFilter",
          36: "setUnderDungeonStart / GetRoomLinkInfo",
          37: "GetRoomLinkInfo — room link", 38: "GetRoomLinkInfo — room link",
          39: "GetRoomLinkInfo — room link",
          59: "BuildCharaSpecialParts (dungeon 0 only)",
          68: "BuildCharaSpecialParts (dungeons 3, 4, 5) — ウンガガスイッチ",
          69: "BuildCharaSpecialParts (dungeons 4, 5)"}

elf = open(ELF,'rb').read()
hed = open(HED,'rb').read(); hd2 = open(HD2,'rb').read(); dat = open(DAT,'rb')
allf = {}
for i in range(len(hed)//80):
    n = hed[i*80:i*80+80].split(b'\x00')[0].decode('latin1','replace')
    if n: allf[n] = struct.unpack_from('<III', hd2, 16+i*32)[:2]
def read(p):
    o,s = allf[p]; dat.seek(o); return dat.read(s)
def elfrd(v, n):
    return elf[v-0x100000+0x100 : v-0x100000+0x100+n]

# ── the chain tables, straight out of the ELF ──
chains, reach = {}, set()
for v,(name,cnt) in sorted(CHAIN.items(), key=lambda kv: kv[1][0]):
    rows = []
    for i in range(cnt):
        m,t,r = struct.unpack_from('<iii', elfrd(v+i*12, 12))
        rows.append((m,t,r)); reach.add(t)
    chains[name] = (v, rows)
reach |= set(DIRECT)

# ── floors: parts defined, meshes shipped, meshes referenced ──
TILE = re.compile(rb'([a-z]\d\d[a-z]\d\d_[0-9a-z]{1,2}\.mds)\x00')
cfgs = sorted(n for n in allf if re.match(r'^dun\\d\d\d?\w*\.cfg$', n))
floors = {}
for c in cfgs:
    stem = c[4:-4]
    mpd = 'dun\\mpd_pack\\%s.mpd' % stem
    if mpd not in allf: continue
    txt = read(c).decode('shift_jis','replace')
    active = '\n'.join(l for l in txt.splitlines() if not re.match(r'^\s*//', l))
    blocks = re.findall(r'DEF_PATS(.*?)DEF_ENDS', active, re.S)
    refs   = set(re.findall(r'"([^"]+)"', active))
    ships  = set(m.group(1).decode() for m in TILE.finditer(read(mpd)))
    dead   = len([l for l in txt.splitlines() if re.match(r'^\s*//\s*(PT_|DEF_PATS)', l)])
    floors[stem] = dict(blocks=blocks, refs=refs, ships=ships, dead=dead, dun=stem[:3])

# per-dungeon: which shipped tile meshes are never named?
byDun = {}
for stem,f in floors.items():
    d = byDun.setdefault(f['dun'], dict(ships=set(), refs=set()))
    d['ships'] |= f['ships']; d['refs'] |= f['refs']

def part_desc(b):
    mesh = re.findall(r'PT_(?:BASE|NPC)\s+"([^"]+)"', b)
    note = re.search(r'//\s*([^\r\n]+)', b)
    return (mesh[0] if mesh else "(no mesh)"), (note.group(1).strip() if note else "")

L=[]; A=L.append
A("# Dungeon tiles\n")
A("How a dungeon floor is built from tiles, and whether any tile content went unused.\n")
A("## The system\n")
A("A floor is a **20×20 grid**. Each cell holds a **part index** (`CDungeonMap + 0x9C50`, stride 0x10) and a")
A("**rotation** (`+0x9C54`). A part is defined in the floor's `.cfg`; the meshes live in the matching `.mpd`.\n")
A("```")
A("//no32                                     <- the part's index is its ORDER among DEF_PATS blocks")
A("DEF_PATS")
A(" PT_BASE \"d01g01_0.mds\" ,0 ,0,0,0, 0      <- visual mesh (a part may have several)")
A(" PT_COLS \"d01g01_a.mds\" ,0                <- collision mesh")
A(" PT_CAM  \"d01g01_v.mds\" ,0                <- camera mesh")
A(" PT_FIRE -3.74, 2.73, 4.10                <- torch position")
A("DEF_ENDS")
A("```")
A("Directives seen across all 35 floor configs: `PT_BASE`, `PT_COLS`, `PT_CAM`, `PT_LIGHT`, `PT_GLIGHT`,")
A("`PT_MARKER`, `PT_HIT_MARKER`, `PT_FIRE`, `PT_NPC`, `PT_WATER`, `PT_ROT`, `PT_SCALE`, `PT_HEAL_ZONE`,")
A("`PT_DRAW_FLAG`.\n")
A("**Part index = the order of the `DEF_PATS` blocks** (counting only uncommented ones). Verified against the")
A("engine: `mapPartsFilter` writes part `0x20` (32) for the healing spring, and block 32 of `d01main_a.cfg` is")
A("exactly the 回復の泉 part — the one carrying `PT_NPC \"d01o03_m.chr\"` (the waterfall, 滝) and `PT_WATER`.\n")

A("## How the builder chooses a part\n")
A("`buildRandomMap` (0x1CB670) carves rooms, then `mapPartsFilter` (0x1C5550) walks every cell, builds a")
A("**4-bit mask of which neighbours are open**, and looks the mask up in a chain table to get `{part, rotation}`.")
A("The whole tile vocabulary of the game is therefore these five tables:\n")
for name,(v,rows) in chains.items():
    A("**`%s`** @ ELF `0x%X` — %d entries" % (name, v, len(rows)))
    A("")
    A("| neighbour mask | part | rot |")
    A("|---|---|---|")
    for m,t,r in rows:
        A("| `0x%X` | %d | %d |" % (m, t, r))
    A("")
A("Plus parts written **directly** by the builder, outside the chain tables:\n")
A("| part | written by |")
A("|---|---|")
for p in sorted(DIRECT):
    A("| %d (`0x%X`) | %s |" % (p, p, DIRECT[p]))
A("")
A("`setStair` (0x1C7530) does not introduce new parts — it *copies* a neighbouring cell's part id.\n")
A("So the parts the map builder can place, on any floor, are exactly:\n")
A("```")
A("   " + ", ".join(str(x) for x in sorted(reach)))
A("```\n")

unused_art, total_ships = {}, 0
for dun in sorted(byDun):
    ships, refs = byDun[dun]['ships'], byDun[dun]['refs']
    unused_art[dun] = sorted(x for x in ships - refs if x[:-4]+'.chr' not in refs)
    total_ships += len(ships)
n_un = sum(len(v) for v in unused_art.values())

A("## Q1 — is any tile ART unused?\n")
A("**Essentially no.** Of **%d** tile meshes shipped across the seven dungeons' `.mpd` packs, only **%d** are"
  % (total_ships, n_un))
A("never named by an ACTIVE directive in any floor config:\n")
A("| Dungeon | tile meshes shipped | never referenced |")
A("|---|---|---|")
for dun in sorted(byDun):
    u = unused_art[dun]
    A("| %s | %d | %s |" % (dun, len(byDun[dun]['ships']), ", ".join("`%s`" % x for x in u) if u else "—"))
A("")
A("- **`d06o17_3.mds`** — a *third* visual variant of a tile whose `_2` variant IS used. The only piece of tile")
A("  art in the game that is shipped, complete, and simply never placed.")
A("- **`d06h13_v.mds`** — a camera mesh for a tile that is otherwise used.")
A("- **`d06o15_1.mds` / `d06o15_2.mds`** — D6 meshes that ride along in D7's pack (D7's config was derived from")
A("  D6's). They ARE named in D7's config, but only in **commented-out** directives, so they never load.\n")
A("No hidden rooms, no cut corridors. The tile art that shipped is, to within four meshes, the tile art used.\n")

A("## Q2 — is any defined PART never placed?  **YES.**\n")
A("Every writer of a grid cell has now been traced (`FindXrefs` on `buildMapDat` @ `0x1D56DA0`, plus the")
A("functions that write the grid directly). The complete set of parts the engine can EVER place is:\n")
A("```")
A("   " + ", ".join(str(x) for x in sorted(reach)))
A("```\n")
A("Everything else a floor defines is **loaded into memory and never used**. On a 70-part floor that is")
A("**31 parts**: `%s`.\n" % ", ".join(str(i) for i in range(70) if i not in reach))
A("Most are redundant **orientation variants** — parts are authored in families of four (one per wall")
A("configuration), but the engine stores rotation SEPARATELY (grid `+0x9C54`) and rotates a single part, so it")
A("only ever asks for one or two members of each family. Those are not lost content, just spare authoring.\n")
A("**But some are real content.** Below, only the unreached parts whose meshes NO reachable part uses — i.e.")
A("genuinely unique art that ships, loads, and is never placed:\n")

REACHSET = reach
for c in sorted(n for n in allf if re.match(r'^dun\\d\d\dmain_\w\.cfg$', n)):
    stem = c[4:-4]
    if not stem.endswith('_a'): continue          # the floors are near-identical; show one per dungeon
    txt = read(c).decode('shift_jis','replace')
    active = '\n'.join(l for l in txt.splitlines() if not re.match(r'^\s*//', l))
    blocks = re.findall(r'DEF_PATS(.*?)DEF_ENDS', active, re.S)
    used = set()
    for i,b in enumerate(blocks):
        if i in REACHSET: used |= set(re.findall(r'PT_(?:BASE|NPC)\s+"([^"]+)"', b))
    rows=[]
    for i,b in enumerate(blocks):
        if i in REACHSET: continue
        novel = set(re.findall(r'PT_(?:BASE|NPC)\s+"([^"]+)"', b)) - used
        if novel:
            note = re.search(r'//\s*([^\r\n]+)', b)
            rows.append((i, sorted(novel), note.group(1).strip() if note else ''))
    if not rows: continue
    A("**`%s`** — %d parts, %d never placed, %d of them unique art\n"
      % (stem, len(blocks), len([i for i in range(len(blocks)) if i not in REACHSET]), len(rows)))
    A("| part | unique meshes | label |")
    A("|---|---|---|")
    for i,ms,note in rows:
        A("| %d | %s | %s |" % (i, ", ".join("`%s`" % m for m in ms), "**%s**" % note if note else ""))
    A("")

A("### The headline: the Goro and Ruby switches were never placed\n")
A("Every dungeon defines a **character-switch family** — four parts (one per orientation) per character:\n")
A("| parts | mesh | label | placed? |")
A("|---|---|---|---|")
A("| 55–58 | `uNN01` | (unnamed) | **no** |")
A("| 60–63 | `uNN03` | **ゴロースイッチ** — Goro switch | **NO** |")
A("| 64–67 | `uNN04` | **ルビーースイッチ** — Ruby switch | **NO** |")
A("| 68–69 | `uNN05` | **ウンガガスイッチ** — Ungaga switch | **yes** — dungeons 3, 4, 5 |")
A("")
A("`BuildCharaSpecialParts` is the only thing that places a character switch, and it branches on `selectMapNo`")
A("(the dungeon: DBC=0, Wise Owl=1, …). Exhaustively, it writes exactly three parts:\n")
A("```")
A("selectMapNo == 0  ->  part 59          (and only while a story flag is below 8)")
A("selectMapNo == 3  ->  part 68          Ungaga switch")
A("selectMapNo == 4  ->  parts 68, 69")
A("selectMapNo == 5  ->  parts 68, 69")
A("```\n")
A("It never writes 60–67. **The Goro switch and the Ruby switch are fully authored — meshes, collision, all four")
A("orientations, in nearly every dungeon — and the engine has no code path that can ever place them.** Only")
A("Ungaga's switch shipped. Dungeon 6 also defines part 59 as **ネコ** (\"cat\"), which only dungeon 0 ever places.\n")
A("### The other unused content\n")
A("- **D1's minecart has more track pieces.** Part 40 (`トロッコ`, `d01o11_1.mds`) IS placed; parts 41–44")
A("  (`d01o12_1`, `d01o13_1`, `d01o14_1`, `d01o15_0`) are different track meshes and are never placed.")
A("- **Corner-entry rooms** (parts 47–54, `hNN26`/`hNN30` — the 角入り pieces) are defined in every dungeon and")
A("  never placed. These are the same pieces that appear COMMENTED OUT in the D6/D7 configs.")
A("- **Object rooms** (parts 33–35 in D2/D3, `oNN04`–`oNN06`) — never placed.\n")
A("All of this art is in the `.mpd`, is loaded when you enter the floor, and is then never drawn. Because the")
A("part index is just an int in the grid (`CDungeonMap + 0x9C50`), a mod can place any of them by writing the")
A("cell — no ISO change needed.\n")

A("## Q3 — content disabled in the configs\n")
A("The floor configs carry **%d commented-out part directives**. Most are dead weight, but not all:\n"
  % sum(f['dead'] for f in floors.values()))
A("| Floor | commented-out directives |")
A("|---|---|")
for stem in sorted(floors):
    if floors[stem]['dead']:
        A("| `%s` | %d |" % (stem, floors[stem]['dead']))
A("")
A("What they actually are:\n")
A("- **384 of them (64 × 6) are in the D7 configs** and reference D6 meshes (`d06h26_0`, `d06h30_0` —")
A("  角入り, \"corner entry\") that **are not even in D7's packs**. D7's config was derived from D6's and these")
A("  came along as dead copy-paste. Not restorable, and never were.")
A("- **Genuinely cut parts whose art was also removed**: `d02g05_1` (a D2 room piece) and `d04o07_3` — the")
A("  meshes are not in the packs, so there is nothing to restore.")
A("- **Extra rotations of parts that ARE in the game.** The nicest example: the Divine Beast Cave has a")
A("  **minecart** (`トロッコ`, `d01mob.mds`), live as part 77 at rotation 0 — rotations 1/2/3 (parts 78–80)")
A("  were commented out. Same for D6's `黒い霧` (\"black fog\") `PT_NPC`, which is active elsewhere.\n")

A("## Incidental\n")
A("The `.mpd` packer leaked its build environment into the shipped data — the string **`OS=Windows_NT`** sits")
A("in the middle of `d03main_a.mpd`. It corrupts naive filename extraction (it reads as a name prefix), which")
A("is worth knowing before trusting any string scan of these packs.\n")

open(OUT,'w').write("\n".join(L) + "\n")
print("wrote", OUT)
