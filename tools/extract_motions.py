#!/usr/bin/env python3
"""Extract per-enemy motion documentation comment blocks from EnemyData.cs into a
dedicated markdown file (ordered by TableIndex), and (optionally) strip them from source.

Dry run (default): writes the .md and prints a report, leaves EnemyData.cs untouched.
Pass --apply to also rewrite EnemyData.cs with the motion comment lines removed.
"""
import re, sys, io

SRC = "Dark Cloud Improved Version/EnemyData.cs"
MD  = "enemy-motion-table.md"
APPLY = "--apply" in sys.argv

with io.open(SRC, encoding="utf-8") as f:
    lines = f.readlines()
n = len(lines)

decl_re = re.compile(r'^\s*internal static readonly EnemyDefaults (\w+)\s*=\s*new EnemyDefaults')
def is_comment(s): return s.lstrip().startswith("//")

entries = []
for i, ln in enumerate(lines):
    m = decl_re.match(ln)
    if not m:
        continue
    field = m.group(1)
    buf, j = [], i
    while j < n:
        buf.append(lines[j])
        if '};' in lines[j]:
            break
        j += 1
    init = " ".join(buf)
    def grab(pat):
        mm = re.search(pat, init)
        return mm.group(1) if mm else None
    ti = grab(r'TableIndex=(\d+)')
    entries.append(dict(decl=i, field=field,
                        name=grab(r'Name="([^"]*)"'),
                        model=grab(r'ModelCode="([^"]*)"'),
                        ti=int(ti) if ti else None,
                        eid=grab(r'Id=(\d+)')))

delete = set()
for e in entries:
    k = e["decl"] - 1
    run = []
    while k >= 0 and is_comment(lines[k]):
        run.append(k); k -= 1
    run.reverse()
    mstart = next((x for x in run if "Motions:" in lines[x]), None)
    if mstart is None:
        e["motion"] = None
        continue
    region = [x for x in run if x >= mstart]
    e["motion"] = region
    delete.update(region)

def strip_comment(idx):
    s = lines[idx].rstrip("\n")
    s = s.lstrip()
    if s.startswith("//"):
        s = s[2:]
    if s.startswith(" "):
        s = s[1:]
    return s

def render(e):
    out = []
    ti = e["ti"] if e["ti"] is not None else "—"
    model = ("`%s`" % e["model"]) if e["model"] else ""
    out.append("### %s — %s %s" % (ti, e["name"] or e["field"], model))
    out.append("")
    captions, table = [], []
    for idx in e["motion"]:
        txt = strip_comment(idx)
        if "\t" in txt:
            table.append(txt)
        else:
            captions.append(txt)
    for c in captions:
        out.append("*%s*  " % c)
    if captions:
        out.append("")
    if table:
        header = table[0].split("\t")
        # if first table line isn't the Idx header, synthesise one
        if not header[0].strip().lower().startswith("idx"):
            header = ["Idx", "Frames", "Name (JP)", "Meaning"]
            rows = table
        else:
            rows = table[1:]
        ncol = len(header)
        out.append("| " + " | ".join(h.strip() for h in header) + " |")
        out.append("|" + "|".join(["---"] * ncol) + "|")
        for r in rows:
            cols = [c.strip() for c in r.split("\t")]
            cols += [""] * (ncol - len(cols))
            out.append("| " + " | ".join(cols[:ncol]) + " |")
        out.append("")
    return out

withmot = [e for e in entries if e["motion"]]
withmot.sort(key=lambda e: (e["ti"] is None, e["ti"] if e["ti"] is not None else 0))

md = []
md.append("# Enemy motion table")
md.append("")
md.append("Per-species animation / motion lists, decoded from each enemy model's `<code>.chr` `info.cfg` KEY")
md.append("block inside `data.dat`. Each `KEY <start>,<end>,<speed>, //<name(Shift-JIS)> <idx>` is one motion;")
md.append("`idx` is the motion-table index passed to `_SET_MOTION`. **死亡** (`shibou`) marks the death/collapse")
md.append("motion used by `EnemyModelInjector.BossScriptPatcher.CollapseMotion`. Locate a `.chr` via `data.hed`")
md.append("(filename @ `index*80`) → `data.hd2` (entry @ `16 + index*32`: `[+0]`=data.dat offset, `[+4]`=size,")
md.append("`[+8]`=sector). See the `datadat-index-and-chr-motions` note. Sorted by `TableIndex`.")
md.append("")
for e in withmot:
    md.extend(render(e))

with io.open(MD, "w", encoding="utf-8") as f:
    f.write("\n".join(md).rstrip() + "\n")

print("entries=%d  with-motion=%d  lines-to-delete=%d" % (len(entries), len(withmot), len(delete)))
missing = [e for e in withmot if not e["name"] or e["ti"] is None]
print("entries with motion but missing name/ti: %d" % len(missing))
for e in missing:
    print("   field=%s name=%r ti=%r model=%r" % (e["field"], e["name"], e["ti"], e["model"]))

if APPLY:
    kept = [ln for i, ln in enumerate(lines) if i not in delete]
    with io.open(SRC, "w", encoding="utf-8") as f:
        f.writelines(kept)
    print("APPLIED: removed %d lines from %s" % (len(delete), SRC))
