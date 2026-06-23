#!/usr/bin/env python3
"""Augment enemy-motion-table.md in place:
  • add a Speed column (the 3rd value of each .chr KEY = the motion's default playback rate)
  • add a per-enemy note with the real ATTACK HIT WINDOWS — from the STB _SET_DMG_COL (cmd 131)
    `(bone, radius, startFrame, endFrame)`: the motion-frame range during which the enemy's attack
    collision is live and deals damage (followed by _SET_DMG_PARA (cmd 132) = the damage value).
    Each window is matched to the .chr motion whose frame range contains it (preferring the attack-
    named one). Also notes Attack motions (by JP name) and the defensive Block window (_SET_GUARD_FRAME
    cmd 244 — frames where the enemy guards and the PLAYER's hit is negated, NOT the enemy's attack).
Preserves intro, headers, JP names, meanings. Idempotent. Regenerate from a clean (un-augmented) table.
"""
import re, struct

HED = "/Users/thomascantwell/ROMs/dc_extracted/data.hed"
HD2 = "/Users/thomascantwell/ROMs/dc_extracted/data.hd2"
DAT = "/Users/thomascantwell/ROMs/dc_extracted/data.dat"
MD  = "enemy-motion-table.md"
FN_SET_DMG_COL, FN_SET_DMG_PARA, FN_SET_GUARD_FRAME = 131, 132, 244

# JP substrings that mark an offensive/damaging motion (broad on purpose).
ATTACK_KW = ["攻撃", "爪", "ブレス", "突進", "発射", "投げ", "噛", "斬", "タックル", "体当",
             "パンチ", "キック", "突き", "ビーム", "魔法", "吐", "振り下", "射撃", "砲"]

hed = open(HED, "rb").read(); hd2 = open(HD2, "rb").read(); dat = open(DAT, "rb")

files = {}
for i in range(len(hed) // 80):
    name = hed[i*80:i*80+80].split(b"\x00")[0].decode("latin1", "replace")
    if name.startswith("dun\\monstor\\"):
        files.setdefault(name[len("dun\\monstor\\"):], struct.unpack_from("<III", hd2, 16 + i*32)[:2])

def read_file(base):
    if base not in files: return None
    off, size = files[base]; dat.seek(off); return dat.read(size)

def find_base(code, ext):
    if code + ext in files: return code + ext
    if code + "a" + ext in files: return code + "a" + ext   # standard variant suffix (e.g. e110 -> e110a)
    return None

_chr = {}
def chr_motions(code):
    """code -> {idx: (speed_str, jp_name, start, end)} by KEY order."""
    if code in _chr: return _chr[code]
    out = {}; d = read_file(find_base(code, ".chr") or "")
    if d:
        for idx, m in enumerate(re.finditer(rb"KEY[ \t]+(\d+),[ \t]*(\d+),[ \t]*([\d.]+),?[ \t]*//([^\r\n]*)", d)):
            start, end, speed, rest = m.groups()
            toks = rest.decode("shift_jis", "replace").split()
            if toks and toks[-1].lstrip("-").isdigit(): toks = toks[:-1]
            spd = speed.decode().rstrip("0").rstrip(".") or "0"
            out[idx] = (spd, " ".join(toks).strip(), int(start), int(end))
    _chr[code] = out
    return out

def _walk_ext(stb):
    """yield (funcId, [args]) for each ext-call; arg = ('i',val)|('f',val)|('v',idx)."""
    def W(o): return struct.unpack_from("<I", stb, o)[0] if o + 4 <= len(stb) else 0
    def F(b): return struct.unpack("<f", struct.pack("<I", b))[0]
    if len(stb) < 12 or W(0) != 0x425453: return
    code_off = W(8); n = len(stb); i = code_off
    while i + 12 <= n:
        if W(i) == 21 and W(i+8) == 0:
            argc = W(i+4); fp = i - argc*12
            if fp >= code_off and W(fp) == 3 and W(fp+4) == 1:
                args = []
                for k in range(1, argc):
                    ap = fp + k*12; o, a, b = W(ap), W(ap+4), W(ap+8)
                    args.append(("i", b) if (o == 3 and a == 1) else ("f", F(b)) if (o == 3 and a == 2) else ("v", a))
                yield W(fp+8), args
        i += 4

_stb = {}
def stb_combat(code):
    """code -> (hit_windows, block_frames). hit_windows = [(start,end,radius,damage)]."""
    if code in _stb: return _stb[code]
    stb = read_file(find_base(code, ".stb") or "")
    hits, blocks = [], []
    if stb:
        for fn, args in _walk_ext(stb):
            if fn == FN_SET_DMG_COL:
                # args = (bone_var, radius_float, startFrame_float, endFrame_float)
                floats = [v for t, v in args if t == "f"]
                if len(floats) >= 3:
                    radius, start, end = floats[0], floats[1], floats[2]
                    hits.append([int(round(start)), int(round(end)), radius, None])
            elif fn == FN_SET_DMG_PARA:
                vals = [v for t, v in args if t in ("i", "f")]
                if vals:
                    for h in reversed(hits):                # damage pairs with the most recent DMG_COL
                        if h[3] is None: h[3] = int(round(vals[0])); break
            elif fn == FN_SET_GUARD_FRAME:
                vals = [int(round(v)) for t, v in args if t in ("i", "f")]
                if len(vals) >= 2 and (vals[0], vals[1]) not in blocks:
                    blocks.append((vals[0], vals[1]))
    _stb[code] = (hits, blocks)
    return _stb[code]

def match_motion(start, end, mot):
    mid = (start + end) / 2
    cands = [idx for idx, (_s, _jp, ms, me) in mot.items() if ms <= mid <= me]
    atk = [idx for idx in cands if any(k in mot[idx][1] for k in ATTACK_KW)]
    return min(atk) if atk else (min(cands) if cands else None)

# ── walk the markdown ───────────────────────────────────────────────────────
lines = open(MD, encoding="utf-8").read().split("\n")
out = []; cur = None
hdr_re = re.compile(r"^###\s+\S+\s+—\s+.*`([A-Za-z0-9_]+)`")
row_re = re.compile(r"^\|\s*(\d+)\s*\|\s*([^|]*)\|\s*([^|]*)\|\s*([^|]*)\|\s*$")

i = 0
while i < len(lines):
    ln = lines[i]
    hm = hdr_re.match(ln)
    if hm:
        cur = hm.group(1); out.append(ln); i += 1; continue

    if cur and ln.startswith("*Motions:"):
        out.append(ln)
        if i + 2 < len(lines) and lines[i+2].startswith("*") and "indow" in lines[i+2]:
            i += 1; continue                                   # note already present (idempotent)
        mot = chr_motions(cur)
        atk = [str(idx) for idx, (_s, jp, _ms, _me) in sorted(mot.items()) if any(k in jp for k in ATTACK_KW)]
        hits, blocks = stb_combat(cur)
        hit_strs = []
        for s, e, r, dmg in hits:
            mid = match_motion(s, e, mot)
            extra = []
            if mid is not None: extra.append(f"m{mid}")
            if r: extra.append(f"r{r:g}")
            if dmg is not None: extra.append(f"dmg{dmg}")
            hit_strs.append(f"{s}–{e}" + (f" ({', '.join(extra)})" if extra else ""))
        parts = ["Hit windows: " + ("; ".join(hit_strs) if hit_strs else "none")]
        parts.append("Attack motions: " + (", ".join(atk) if atk else "none"))
        if blocks: parts.append("Block frames: " + ", ".join(f"{a}–{b}" for a, b in blocks))
        out.append(""); out.append("*" + " · ".join(parts) + "*  ")
        i += 1; continue

    if ln.strip() == "| Idx | Frames | Name (JP) | Meaning |":
        out.append("| Idx | Frames | Speed | Name (JP) | Meaning |")
        if i + 1 < len(lines) and set(lines[i+1].strip()) <= set("|-"):
            out.append("|---|---|---|---|---|"); i += 2; continue
        i += 1; continue

    rm = row_re.match(ln)
    if rm and cur:
        idx = int(rm.group(1))
        spd = chr_motions(cur).get(idx, ("", "", 0, 0))[0]
        out.append(f"| {idx} | {rm.group(2).strip()} | {spd} | {rm.group(3).strip()} | {rm.group(4).strip()} |")
        i += 1; continue

    out.append(ln); i += 1

open(MD, "w", encoding="utf-8").write("\n".join(out))
nhit = sum(1 for c in _stb.values() if c[0])
print(f"augmented {MD}: {len(_chr)} models, {nhit} with attack hit windows")
