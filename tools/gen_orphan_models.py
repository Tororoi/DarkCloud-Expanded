#!/usr/bin/env python3
"""Generate docs/orphaned-enemy-models.md — every dun\\monstor model that EXISTS in data.dat but is
referenced by NO record in the ELF species table (MonstorTable).

Why these exist: the engine reaches a species ONLY through a species-table record, and the record names its
model (+0x00 -> dun/monstor/<code>.chr) and its behaviour script (+0x40 -> dun/monstor/<code>.stb)
INDEPENDENTLY. So a model with no record is unreachable no matter what the spawn roster says — and the gap
between "models shipped" and "models referenced" is exactly the set of enemies the game built and never used.

For each orphan we report: its own info.cfg title, its motion table, whether a matching .stb exists, and
whether its motion table is byte-identical to an IN-TABLE model (which would let it borrow that model's
script wholesale, since a script drives motions by INDEX).
"""
import struct, re

HED = "/Users/thomascantwell/ROMs/dc_extracted/data.hed"
HD2 = "/Users/thomascantwell/ROMs/dc_extracted/data.hd2"
DAT = "/Users/thomascantwell/ROMs/dc_extracted/data.dat"
ELF = "/Users/thomascantwell/ROMs/dc_extracted/SCUS_971.11"
OUT = "docs/orphaned-enemy-models.md"

TBL_OFF, STRIDE, NREC = 0x17FC00, 0x9C, 167   # species table inside the ELF file

TR = [("ぶら下がり","hang"),("はばたき","flap"),("上昇","rise"),("降下体当たり","dive slam"),
      # NOTE: order matters — TR is applied in sequence, so a longer term MUST precede any shorter one it
      # contains, or the generic entry swallows the tail (死亡→"death" turned 着対死亡 into "着対death").
      ("着対死亡","death (hits ground)"),("死亡ループ","death (loop)"),("死亡","death"),
      ("のけぞり","recoil"),("落下","fall"),
      ("バックステップ","back step"),("右ステップ","right step"),("左ステップ","left step"),
      ("ガード入り","guard (enter)"),("ガードループ","guard (loop)"),("ガード戻り","guard (return)"),
      ("起き上がり","get up"),("ダメージ","damage"),("予備動作","(windup)"),
      ("攻撃","attack"),("立ち","idle"),("歩き","walk"),("走り","run"),("ダミー","(dummy / unused)"),
      ("共通スティール","steal"),("体当たり","body slam"),("盗ったら早歩きで逃げる","flees at a fast walk if it steals"),
      ("パンチ","punch")]
def meaning(nm):
    if not nm: return "—"
    out = nm
    for jp,en in TR: out = out.replace(jp,en)
    return re.sub(r'\s+',' ',out).strip()

elf = open(ELF,'rb').read()
hed = open(HED,'rb').read(); hd2 = open(HD2,'rb').read(); dat = open(DAT,'rb')

def code(b):
    s = b.split(b'\0')[0]
    try: return s.decode('ascii')
    except Exception: return None

# ── what the species table references (main model + the 3 sub-model slots) ──
used, rec_of = set(), {}
for i in range(NREC):
    r = elf[TBL_OFF + i*STRIDE : TBL_OFF + (i+1)*STRIDE]
    for off in (0x00, 0x10, 0x20, 0x30):
        c = code(r[off:off+8])
        if c:
            used.add(c)
            rec_of.setdefault(c, i)

# ── what data.dat actually ships ──
# NOTE: scan the WHOLE archive. An earlier version of this tool only looked in dun\monstor and therefore
# "proved" that Killer Snake had no model — it does: gedit\s97\chara\e116.chr (キラースネイク). Never let the
# scan scope silently become the conclusion.
files, allfiles = {}, {}
for i in range(len(hed)//80):
    n = hed[i*80:i*80+80].split(b'\x00')[0].decode('latin1','replace')
    if not n: continue
    o,s,_ = struct.unpack_from('<III', hd2, 16 + i*32)
    allfiles[n] = (o,s)
    if n.startswith('dun\\monstor\\'):
        files[n.split('\\')[-1]] = (o,s)

def info(c):
    """(title, [(idx,start,end,speed,name)]) for a model code, or None."""
    if c + '.chr' not in files: return None
    o,s = files[c + '.chr']
    dat.seek(o); d = dat.read(s)                       # FULL read — the KEY block can sit past 400 KB
    m = re.search(rb'//([^\r\n]*)\r?\nIMG', d)
    title = m.group(1).decode('shift_jis','replace').strip() if m else ''
    ms = []
    for idx,k in enumerate(re.finditer(rb'KEY[ \t]+(\d+),[ \t]*(\d+),[ \t]*([\d.]+),?[ \t]*//([^\r\n]*)', d)):
        st,en,sp,rest = k.groups()
        nm = rest.decode('shift_jis','replace')
        nm = re.sub(r'[\t\u3000]+', ' ', nm).strip()      # tabs / full-width spaces the artists used to align
        nm = re.sub(r'(?<!\S)\d+(?!\S)', '', nm)         # the index numbers they wrote INTO the name
        nm = re.sub(r'\s+', ' ', nm).strip()
        ms.append((idx, int(st), int(en), sp.decode().rstrip('0').rstrip('.') or '0', nm))
    return (title, ms, o, s)

def info_path(path):
    """(title, motions, model, off, size) for any .chr anywhere in the archive."""
    if path not in allfiles: return None
    o,s = allfiles[path]
    dat.seek(o); d = dat.read(s)
    m  = re.search(rb'//([^\r\n]*)\r?\nIMG', d)
    md = re.findall(rb'MOTION[ \t]+\d+,[ \t]*"([^"]+)"', d)
    title = m.group(1).decode('shift_jis','replace').strip() if m else ''
    n = len(re.findall(rb'KEY[ \t]+\d+,', d))
    return (title, n, [x.decode() for x in md], o, s)

models   = sorted(c[:-4] for c in files if c.endswith('.chr'))
orphans  = sorted(set(models) - used)
in_table = [c for c in models if c in used]

def asset_cfg(c):
    """The info.cfg block (IMG / MODEL / MOTION / BODY_SIZE). Two .chr files sharing this are THE SAME ASSET
    packaged twice — same mesh, same texture, same motion data — differing only in uninitialised padding."""
    if c + '.chr' not in files: return None
    o,s = files[c + '.chr']
    dat.seek(o); d = dat.read(s)
    i = d.find(b'IMG 0'); j = d.find(b'KEY_START', i)
    return d[i:j].strip() if i >= 0 and j > i else None

# motion-table fingerprint (frames only — the script indexes by position, so this is what has to line up)
def fp(c):
    r = info(c)
    return tuple((s,e) for _,s,e,_,_ in r[1]) if r else None
fps  = {c: fp(c) for c in models}
cfgs = {c: asset_cfg(c) for c in models}

L = []
A = L.append
A("# Orphaned enemy models\n")
A("Models that EXIST in `data.dat` (`dun\\monstor\\*.chr`) but are referenced by **no record** in the ELF")
A("species table (`MonstorTable`, 167 records × 0x9C @ file 0x17FC00 / RAM 0x0027FB00).\n")
A("**Why this list is the interesting one.** The engine reaches a species *only* through a species-table")
A("record. `BtLoadMonstor` reads the floor roster's `+4` field and hands it straight to `SetupBaseModel`,")
A("which does `record = &MonstorTable + index * 0x9C` — so the roster stores a **TableIndex, not an EID**, and")
A("there is no EID→record lookup anywhere in the spawn path. The record then names, independently:\n")
A("| field | selects |")
A("|---|---|")
A("| `+0x00` ModelCode | `dun/monstor/<code>.chr` — the model |")
A("| `+0x40` ScriptCode | `dun/monstor/<code>.stb` — the behaviour script |")
A("| `+0x10/+0x20/+0x30` | up to 3 extra sub-models (multi-part bosses) |")
A("| `+0x50…` | HP, resistances, drops, body size |\n")
A("Because model and script are chosen **separately**, a record fully defines an enemy — and the game already")
A("exploits this: records 141/142/152/153/163/164 pair model `e109a`/`e110a` with *different* scripts")
A("(`e147a`, `e148a`, `e156a`, `e157a`, `e165a`, `e166a`). That is the Demon-Shaft \"Enhanced\" pattern, and it")
A("is the template for adding a species: **existing model + existing script + new stats = a new enemy.**\n")
A("(Those `e147a`-style `.chr` files DO exist as models too, but they are **duplicate containers** — identical")
A("`info.cfg`, i.e. the same `e109a.mds` mesh and `e109a01.img` texture as the model the record actually names.")
A("So the Enhanced mimics look the same and only *behave* differently. They are not extra appearances.)\n")
A("A model with no record is therefore unreachable no matter what the spawn roster says. These are the enemies")
A("the game built and never shipped.\n")
A("## Adding one\n")
A("1. **A record.** All 167 are occupied and index 167 is already other data, so you cannot simply append.")
A("   Either repurpose an existing TableIndex at runtime (the mod already writes species-table fields — see")
A("   `Enemies.EnableEnemyDrops`), or relocate the table into a code cave and patch the `lui`/`addiu` that")
A("   forms `&MonstorTable` (an in-place edit at the cold window).")
A("2. **A model.** Present — that is what this table lists.")
A("3. **A script.** The gap for most of them. A `.stb` drives animation by **motion INDEX**, so a borrowed")
A("   script only works if the motion tables line up. The *Script* column below says whether that is free.\n")
A("> **Do NOT infer a model code from an EID.** Model codes do not reliably track enemy ids")
A("> (e.g. `e55a` is Living Armor, id 55 — but the numbering is not a rule). Always confirm against the")
A("> species record or the model's own `info.cfg` title.\n")

# ── summary ──
A("## THE RESTORATION SCOPE\n")
A("Of everything below, this is the whole list of **enemies the game built and never used**:\n")
A("| Enemy | Where | Needs |")
A("|---|---|---|")
A("| **Bat** `e04a` (18 motions: hang / rise / dive-slam / 3 deaths) | `dun\\monstor` | a species record + a script |")
A("| **Bat** `e13a` (18 motions, simpler) | `dun\\monstor` | a species record + a script |")
A("| **Killer Snake** `e54a`/`e54b` (EID 54) | packed in `gedit\\s97\\chara\\e116.chr` | a name alias + a record + a script |")
A("")
A("**That is the whole list.** In particular there are NO extra appearances hiding in the orphans: `e07a__`,")
A("`e56a_`, `e20a_` and the six `e147a`-style mimic files all name the SAME mesh and texture as an enemy that")
A("already exists (`e07a.mds`, `e56a.mds`, `e20a.mds`, `e109a.mds`, `e110a.mds`), so they would look identical")
A("in game. They are alternate BUILDS of the same asset, not alternate skins — adding one gains nothing.\n")
A("Everything else in this document is an NPC, a boss sub-part, a prop, an effect, or a scene copy of an enemy")
A("that already has a species record. **It is not restorable as an enemy because it is not an enemy.**\n")
A("## Summary\n")
A("| | count |")
A("|---|---|")
A("| species-table records | %d |" % NREC)
A("| distinct models the table references | %d |" % len(used & set(models)))
A("| models shipped in `data.dat` | %d |" % len(models))
A("| **orphaned (shipped, never referenced)** | **%d** |" % len(orphans))
A("")

rows = []
for c in orphans:
    r = info(c)
    if not r: continue
    title, ms, off, size = r
    has_stb = (c + '.stb') in files
    # EVERY in-table model with the same motion fingerprint — several enemies share a rig, and that shared rig
    # is exactly why the game can pair one model with another's script. Prefer the name-sibling (e07a for
    # e07a__) when it is among them, since that is the one whose script was authored for this body.
    twins = [t for t in in_table if fps[t] and fps[t] == fps[c] and t != c]
    sib = next((t for t in twins if c.startswith(t)), None)
    twin = sib or (twins[0] if twins else None)
    # Is this just the same asset packaged twice? Several "orphan models" are byte-for-byte the same mesh,
    # texture and motion as an in-table model — the .chr differs only in uninitialised padding. They are NOT
    # a distinct appearance, and calling them an unused model would be wrong.
    dup = next((t for t in in_table if cfgs[t] and cfgs[t] == cfgs[c] and t != c), None)
    exact = dup is not None and files[dup + '.chr'][1] == size   # same asset AND same file size
    rows.append((c, title, ms, off, size, has_stb, twin, twins, dup, exact))

A("## The orphans\n")
A("A `.chr` names its mesh and texture in its `info.cfg` (`MODEL \"x.mds\"`, `IMG \"x01.img\"`). If an orphan")
A("names the SAME mesh and texture as an in-table model, it is not a new appearance no matter how the file")
A("differs — it would look identical in game. That check is what the Verdict column reports.\n")
A("| Model | Title (JP) | Motions | Own `.stb`? | Shares a rig with (in-table) | Verdict |")
A("|---|---|---|---|---|---|")
for c,title,ms,off,size,has_stb,twin,twins,dup,exact in rows:
    if exact:        verdict = "**duplicate container of `%s`** — same asset, same size. No new appearance." % dup
    elif dup:        verdict = "same mesh+texture as `%s` (a different BUILD, not a different look)" % dup
    elif has_stb:    verdict = "**ready** — model + its own script"
    elif twin:       verdict = "**ready** — borrow `%s.stb` (same rig)" % twin
    elif ms:         verdict = "needs a script — no rig match"
    else:            verdict = "not an enemy body"
    shared = ", ".join("`%s`" % t for t in twins) if twins else "—"
    A("| `%s` | %s | %d | %s | %s | %s |" %
      (c, title or "—", len(ms), "yes" if has_stb else "no", shared, verdict))
A("")

A("## Motion tables\n")
for c,title,ms,off,size,has_stb,twin,twins,dup,exact in rows:
    if not ms: continue
    A("### `%s` — %s\n" % (c, title or "(untitled)"))
    bits = ["`dun\\monstor\\%s.chr` @ data.dat 0x%X (%s B)" % (c, off, f"{size:,}")]
    bits.append("own `.stb`: **%s**" % ("yes" if has_stb else "NO"))
    if dup:
        bits.append("**same mesh+texture as `%s`** — not a new appearance%s"
                    % (dup, " (byte-size identical: a duplicate container)" if exact else ""))
    if twins:
        bits.append("motion table **identical to** " + ", ".join("`%s`" % t for t in twins) +
                    " — it can run `%s.stb` unchanged" % twin)
    A("*" + " · ".join(bits) + "*\n")
    A("| Idx | Frames | Speed | Name (JP) | Meaning |")
    A("|---|---|---|---|---|")
    for idx,s,e,sp,nm in ms:
        A("| %d | %d–%d | %s | %s | %s |" % (idx, s, e, sp, nm or "—", meaning(nm)))
    A("")

# ── enemy-coded models living OUTSIDE dun\monstor ────────────────────────────────────────────────
epat = re.compile(r'^(e\d+[a-z_0-9]*)$')
out_of_dir = []
for path in sorted(allfiles):
    if not path.endswith('.chr'): continue
    d, base = path.rsplit('\\', 1)
    base = base[:-4]
    if d.startswith('dun\\monstor') or not epat.match(base): continue
    r = info_path(path)
    if r: out_of_dir.append((d, base, r))

# ── models outside dun\monstor, classified by WHETHER THEY FIGHT ────────────────────────────────
# The scene folders are mostly the cutscene cast. To size the real restoration scope we need to separate
# creatures from people and props, and the honest signal is the MOTION VOCABULARY: anything the game intends
# to fight has attack / damage / death / guard / battle motions. An NPC has idle, walk and talk.
COMBAT = ['攻撃','ダメージ','ダメ−ジ','死亡','やられ','ガード','バトル','のけぞり','起き上がり']
# characters that are the PARTY or a story NPC, not a monster — by the code packed inside the bundle
PARTY = re.compile(r'^(c01|c03c|c04|c05|c06|c07|c10|c18|p\d+|cam)', re.I)

fighters, quiet = [], []
for path in sorted(allfiles):
    if not path.endswith('.chr'): continue
    d, base = path.rsplit('\\', 1); base = base[:-4]
    if d.startswith('dun\\monstor') or not epat.match(base): continue
    o,s = allfiles[path]
    dat.seek(o); dd = dat.read(s)
    m = re.search(rb'//([^\r\n]*)\r?\nIMG', dd)
    title = m.group(1).decode('shift_jis','replace').strip() if m else ''
    names = ' '.join(k.group(4).decode('shift_jis','replace') for k in
        re.finditer(rb'KEY[ \t]+(\d+),[ \t]*(\d+),[ \t]*([\d.]+),?[ \t]*//([^\r\n]*)', dd))
    mots = [x.decode()[:-4] for x in re.findall(rb'MOTION[ \t]+\d+,[ \t]*"([^"]+)"', dd)]
    hits = [c for c in COMBAT if c in names]
    rec = (base, d, title, mots, hits)
    (fighters if hits else quiet).append(rec)

seenf = {}
for base,d,title,mots,hits in fighters:
    seenf.setdefault(base, (d,title,mots,hits))

A("## Models OUTSIDE `dun\\monstor` — and which of them are ENEMIES\n")
A("The scene folders (`gedit\\*\\chara\\`, `dun\\d01\\event\\`) hold the **cutscene cast**: story NPCs, per-scene")
A("copies of the party, boss sub-parts, props and effects. Only a handful are creatures the game means to")
A("FIGHT, and that is the only part of this list that matters for restoring enemies.\n")
A("The honest test is the **motion vocabulary** — anything meant to fight has attack / damage / death / guard /")
A("battle motions; an NPC has idle, walk and talk. Of **%d** distinct `e*`-coded models out here, only **%d**"
  % (len(set(b for b,_,_,_,_ in fighters) | set(b for b,_,_,_,_ in quiet)), len(seenf)))
A("have any combat motion at all.\n")
A("| Model | Title (JP) | Packs | Combat motions | What it actually is |")
A("|---|---|---|---|---|")
VERDICT = {
 'e116':     "**KILLER SNAKE — the one genuine unused enemy.** Meshes `e54a`/`e54b` = EID 54.",
 'e117_0':   "another `s97` bundle packing the same `e54a`/`e54b` Killer Snake meshes",
 'e115':     "`s97` bundle; packs `e54a` alongside Toan",
 'e101':     "Toan/Goro cutscene battle poses — party, not an enemy",
 'e101eb':   "Goro cutscene battle poses — party",
 'e17eb':    "Cedric (シーダ) — story NPC",
 'e17c01d':  "Toan — party",
 'e23':      "Cedric (シーダ) — story NPC",
 'e15c12a':  "Dran (`c12a`) — a boss that ALREADY has a species record",
 'e24c12a':  "Dran (`c12a`) — already in the species table",
 'e17a':     "笑いポックル4 — already in the species table",
 'e25a':     "大砲 (cannon) — already in the species table",
 'e28e03a':  "skeleton (`e03a`) — already in the species table",
 'e306c15c': "King's Curse phase part (`c15c`) — the boss already exists",
 'e502c17a': "新魔人 (`c17a`) — the boss already exists",
 'e503c17a': "新魔人 (`c17a`) — the boss already exists",
 'e411drobo':"ロボ — a scripted set-piece robot from the `s37`/`s40` event",
 'e28eb':    "event bundle (`e01a` skeleton + Toan)",
 'e325':     "エンガ (`p54a`) — a story NPC from Ungaga's chapter, not an enemy",
}
for base in sorted(seenf):
    d,title,mots,hits = seenf[base]
    A("| `%s` | %s | %s | %s | %s |" % (
        base, title or "—",
        ", ".join("`%s`" % x for x in mots) if mots else "—",
        " ".join(hits),
        VERDICT.get(base, "—")))
A("")
# ── and the full inventory, so nothing found is lost ──
seenq = {}
for base,d,title,mots,hits in quiet:
    if base not in seenq: seenq[base] = (d,title,mots,[])
    else: seenq[base][3].append(d)
A("### The rest — every other `e*` model out there (no combat motions)\n")
A("Documented for completeness. These are NPCs, boss sub-parts, props and effects — **none is an enemy** —")
A("but this is the full inventory of what the scan found, %d distinct models. *Packs* lists the characters"
  % len(seenq))
A("bundled inside each `.chr`, which is how the cutscene bundles reveal their real cast.\n")
A("| Model | Title (JP) | Motions | Packs | First seen in |")
A("|---|---|---|---|---|")
for base in sorted(seenq):
    d, title, mots, _ = seenq[base]
    r = info_path(d + "\\" + base + ".chr")
    nmot = r[1] if r else 0
    A("| `%s` | %s | %d | %s | `%s` |" % (
        base, title or "—", nmot,
        ", ".join("`%s`" % x for x in mots) if mots else "—", d))
A("")

A("**Scope, stated plainly:** exactly **one** unused enemy lives outside `dun\\monstor` — **Killer Snake**")
A("(`e54a`/`e54b`, packed inside the `s97` cutscene bundles). Everything else with combat motions is either the")
A("party striking battle poses, or a boss/enemy that already owns a species record. The remaining **%d** models"
  % len(set(b for b,_,_,_,_ in quiet)))
A("have **no combat motion of any kind** — they are NPCs, boss sub-parts, props (`宝箱` chest, `ランプ` lamp,")
A("`聖剣` holy sword, `足場` platform) and effects. They are documented here for completeness, but they are not")
A("enemies and nothing about them is restorable as one.\n")

A("## Killer Snake — it EXISTS\n")
A("**`gedit\\s97\\chara\\e116.chr`** — title **キラースネイク**. EID 54's model was built and never shipped to the")
A("dungeon.\n")
A("The file is a **cutscene bundle**, not one enemy — three packed characters, hence 3 `KEY_START` blocks of 8")
A("motions each (`バトル 0`–`バトル 7`):\n")
A("```")
A("MODEL     e116e54b.mds")
A("MOTION 0  e116e54b.mot     <- Killer Snake, variant b")
A("MOTION 0  e116e54a.mot     <- Killer Snake, variant a")
A("MOTION 1  e116c01d.mot     <- Toan")
A("```\n")
A("The meshes are named **`e54a` / `e54b`** — the species-table-style code for **EID 54**. So this was authored as")
A("a normal dungeon enemy; only the cutscene copy survived. Its motions are a cutscene set (`バトル 0-7`), so it")
A("would need a behaviour script written from scratch, not borrowed.\n")

A("## Restoring one without touching the ISO\n")
A("`SetupBaseModel` builds the model path from a FIXED format string — the record's ModelCode is the only")
A("variable part, and the field is just 16 bytes, so a path cannot be smuggled into it:\n")
A("```")
A("dun/monstor/%s.chr     <- model     (fmt @ ELF 0x29CFC0)")
A("dun/monstor/%s.stb     <- script    (fmt @ ELF 0x29CFF0)")
A("```\n")
A("**But the file index is a tree in RAM, and it is patchable.** `LoadFile` -> `SearchFile` (0x13E980) splits the")
A("path on `/` and walks a `NAME_TREE` from the global **`tree` @ EE `0x202A24EC`**, matching each component")
A("case-insensitively (`search_tree` 0x13EEF0). A node is:\n")
A("| off | field |")
A("|---|---|")
A("| `+0x00` | `char*` name |")
A("| `+0x04` | value — what `SearchFile` returns for a leaf (the file\'s archive entry) |")
A("| `+0x08` | first child |")
A("| `+0x0C` | next sibling |")
A("")
A("So the mod can **alias a name onto existing file data**, with pure data writes and no ISO change:\n")
A("1. Walk `tree` -> `dun` -> `monstor`.")
A("2. Build a new node in a code cave: name string `\"e54a.chr\"`, value = the leaf value of")
A("   `gedit/s97/chara/e116.chr`.")
A("3. Splice it into `monstor`\'s child list (write the new node into a sibling `+0x0C` chain).")
A("4. Point a species record\'s ModelCode at `e54a`. The engine now loads the cutscene bundle\'s bytes when it")
A("   asks for `dun/monstor/e54a.chr`.\n")
A("This is the usual \"feed the game data it already interprets\" move: no injected code, and the engine does the")
A("load itself.\n")
A("**The limit of the approach:** it can only REDIRECT to data already on the disc. It cannot introduce new")
A("bytes. So a trimmed, single-character `e54a.chr` (extracted out of the 1.3 MB three-character bundle) cannot")
A("be added this way — that would need a real archive rebuild. The open question for Killer Snake is therefore")
A("whether `LoadPackData3` copes with the multi-character bundle as-is (it reads the first `info.cfg` block), and")
A("whether 1.3 MB fits the enemy model budget. Worth testing against a throwaway ISO before committing.\n")
A("**The orphans already inside `dun\\monstor` need none of this** — their files are already on the right path.")
A("They need only a species record and a script, which is entirely RAM-side.\n")

open(OUT,"w").write("\n".join(L) + "\n")
print("wrote", OUT, "—", len(rows), "orphans in dun\\monstor,", len(out_of_dir), "enemy-coded models outside it")
