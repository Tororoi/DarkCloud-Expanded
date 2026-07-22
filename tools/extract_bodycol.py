#!/usr/bin/env python3
"""Decode every enemy's _SET_BODY_COL (STB cmd 0x82 / funcId 130) body-collision spheres from
dun/monstor/<code>.stb in data.dat and write a markdown table (bone + radius), ordered by TableIndex.

Each sphere = (boneName, radius). The radius is the hittable size: CMonstorUnit::CheckDmg rebuilds a
CCollisionData sphere from it each frame and CheckHitUser registers a weapon hit when dist ≤ radius.
"""
import os
import re, struct, io

# Extracted Dark Cloud disc dir; required — see .env.sample.
DC1_DATA_DIR = os.environ.get("DC1_DATA_DIR")
if not DC1_DATA_DIR: raise SystemExit("Set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")

HED = os.path.join(DC1_DATA_DIR, "data.hed")
HD2 = os.path.join(DC1_DATA_DIR, "data.hd2")
DAT = os.path.join(DC1_DATA_DIR, "data.dat")
SRC = "Dark Cloud Improved Version/EnemyData.cs"
MD  = "enemy-body-collision-table.md"
FN_SET_BODY_COL = 130

hed = open(HED, "rb").read()
hd2 = open(HD2, "rb").read()
dat = open(DAT, "rb")

stbmap = {}   # basename (no .stb) -> (off,size)
for i in range(len(hed) // 80):
    name = hed[i*80:i*80+80].split(b"\x00")[0].decode("latin1", "replace")
    if name.startswith("dun\\monstor\\") and name.endswith(".stb"):
        base = name[len("dun\\monstor\\"):-4]
        if base.endswith("_"):   # skip e03a.stb__ style dupes
            continue
        off, size, _ = struct.unpack_from("<III", hd2, 16 + i*32)
        stbmap.setdefault(base, (off, size))

def read_stb(base):
    if base not in stbmap:
        return None
    off, size = stbmap[base]
    dat.seek(off)
    return dat.read(size)

# filename (no ext) -> (off,size) for .chr models, to validate bone names
chrmap = {}
for i in range(len(hed) // 80):
    name = hed[i*80:i*80+80].split(b"\x00")[0].decode("latin1", "replace")
    if name.startswith("dun\\monstor\\") and name.endswith(".chr"):
        base = name[len("dun\\monstor\\"):-4]
        off, size, _ = struct.unpack_from("<III", hd2, 16 + i*32)
        chrmap.setdefault(base, (off, size))

_bonecache = {}
def bone_set(modelcode):
    if modelcode in _bonecache:
        return _bonecache[modelcode]
    base = modelcode if modelcode in chrmap else next((b for b in chrmap if b.startswith(modelcode)), None)
    bones = set()
    if base:
        off, size = chrmap[base]
        dat.seek(off)
        d = dat.read(min(size, 0x8000))
        # MDS frame names are null-delimited ascii identifiers
        for tok in re.findall(rb"[A-Za-z_][A-Za-z0-9_]{1,15}", d):
            s = tok.decode("latin1")
            bones.add(s)
    _bonecache[modelcode] = bones
    return bones

def clean_bone(stb, b, bones):
    end = stb.find(b"\x00", b)
    raw = stb[b:end if end >= 0 else b].decode("latin1", "replace").replace("\n", " ").strip()
    # accept only names that are an actual bone in this model's .chr; else it's the root body sphere
    return raw if (raw in bones and re.match(r"^[A-Za-z][A-Za-z0-9_]*$", raw)) else "root"

def body_spheres(modelcode):
    cands = [modelcode] if modelcode in stbmap else []
    for base in stbmap:
        if base.startswith(modelcode) and base not in cands:
            cands.append(base)
    for base in cands:
        stb = read_stb(base)
        if not stb:
            continue
        def W(o): return struct.unpack_from("<I", stb, o)[0] if o + 4 <= len(stb) else 0
        if W(0) != 0x425453:
            continue
        code = W(0x08)
        bones = bone_set(modelcode)
        spheres = []
        i = code
        n = len(stb)
        while i + 12 <= n:
            if W(i) == 21 and W(i + 8) == 0:
                argc = W(i + 4)
                if 2 <= argc <= 10:
                    fpos = i - argc * 12
                    if fpos >= code and W(fpos) == 3 and W(fpos + 4) == 1 and W(fpos + 8) == FN_SET_BODY_COL:
                        bone, radius = "root", None
                        for k in range(1, argc):
                            ap = fpos + k * 12
                            o, a, bb = W(ap), W(ap + 4), W(ap + 8)
                            if o == 3 and a == 3 and bone == "root":
                                bone = clean_bone(stb, bb, bones)
                            elif o == 3 and a == 2 and radius is None:
                                radius = struct.unpack("<f", struct.pack("<I", bb))[0]
                        if radius is not None:
                            spheres.append((bone, radius))
            i += 4
        return spheres, base
    return None, None

# parse EnemyData entries
src = io.open(SRC, encoding="utf-8").read()
rows = []
for m in re.finditer(r'EnemyDefaults (\w+) = new EnemyDefaults \{(.*?)\};', src, re.S):
    body = " ".join(m.group(2).split())
    name = re.search(r'Name="([^"]*)"', body)
    model = re.search(r'ModelCode="([^"]*)"', body)
    ti = re.search(r'TableIndex=(\d+)', body)
    idv = re.search(r'\bId=(\d+)', body)
    rows.append(dict(field=m.group(1),
                     name=name.group(1) if name else m.group(1),
                     model=model.group(1) if model else None,
                     ti=int(ti.group(1)) if ti else None,
                     id=idv.group(1) if idv else "?"))

rows.sort(key=lambda r: (r["ti"] is None, r["ti"] if r["ti"] is not None else 0))

def fmt(spheres):
    if spheres is None:
        return "*(no STB)*"
    if not spheres:
        return "*(none — no body collision)*"
    # group identical bone names? just list; show "@bone" only when not root
    parts = []
    for bone, r in spheres:
        rr = ("%g" % r)
        parts.append(rr if bone == "root" else f"{rr} @`{bone}`")
    return ", ".join(parts)

md = []
md.append("# Enemy body-collision (damage hitbox) table")
md.append("")
md.append("Per-species **damage hitbox** — the volume your weapon must reach to hurt the enemy. Decoded from each")
md.append("enemy's behaviour script `dun/monstor/<code>.stb` in `data.dat` (`_SET_BODY_COL`, STB cmd `0x82` / funcId")
md.append("`130`). Each call attaches a **collision sphere** to a model bone with a **radius**:")
md.append("")
md.append("- At spawn `_SET_BODY_COL(bone, radius)` records the sphere; `CMonstorUnit::CheckDmg` (ELF `0x1D9F10`)")
md.append("  rebuilds it each frame at the bone's live position, radius → `CCollisionData` element `+0x3C`.")
md.append("- A weapon hit registers when `dist(weaponPoint, bonePos) ≤ radius` — `CCollisionData::CheckHitUser`")
md.append("  (`0x1B5920`), called from `BtCheckDamageProc` (`0x1DBAFD0`).")
md.append("- Live address of a sphere's radius: `EnemyAddresses.BodyCollision.RadiusAddr(slot, bodyPart)`")
md.append("  (`= MainMonstorUnit.Base + 0x55390 + slot*0x510 + bodyPart*4`). Writing it once resizes the hitbox.")
md.append("")
md.append("Most enemies use a single `root` body sphere; bosses use several on named bones (a few early boss")
md.append("spheres reference messy/comment strings in the source data and fall back to the root body — shown as")
md.append("`root`). Sorted by `TableIndex`.")
md.append("")
md.append("| TableIndex | Id | Name | Model | Hitbox spheres (radius @ bone) |")
md.append("|---|---|---|---|---|")
for r in rows:
    ti = r["ti"] if r["ti"] is not None else "—"
    spheres, base = (None, None) if not r["model"] else body_spheres(r["model"])
    model = f"`{r['model']}`" if r["model"] else "—"
    md.append(f"| {ti} | {r['id']} | {r['name']} | {model} | {fmt(spheres)} |")

io.open(MD, "w", encoding="utf-8").write("\n".join(md).rstrip() + "\n")
nonempty = sum(1 for r in rows if r["model"] and body_spheres(r["model"])[0])
print(f"wrote {MD}: {len(rows)} rows")
