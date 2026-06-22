#!/usr/bin/env python3
"""Strip superfluous provenance comments from the EnemyDefaults fields in EnemyData.cs and
reorder those fields by TableIndex (null TableIndex sorted last, original order preserved).

Only the EnemyDefaults static-field region (between the class header and `static Enemies()`)
is touched; the static constructor, the `all[]` array, and the name dictionaries are untouched.

Dry run (default): writes EnemyData.cs.new and prints a report. Pass --apply to overwrite.
"""
import re, sys, io

SRC = "Dark Cloud Improved Version/EnemyData.cs"
APPLY = "--apply" in sys.argv

with io.open(SRC, encoding="utf-8") as f:
    lines = f.readlines()
n = len(lines)

decl_re = re.compile(r'^\s*internal static readonly EnemyDefaults (\w+)\s*=\s*new EnemyDefaults')
ctor_re = re.compile(r'^\s*static Enemies\(\)')

# region = [first EnemyDefaults decl's leading comments .. last EnemyDefaults '};']
first_decl = next(i for i, l in enumerate(lines) if decl_re.match(l))
ctor_idx   = next(i for i, l in enumerate(lines) if ctor_re.match(l))
# last '};' that closes an EnemyDefaults before the ctor
last_end = None
i = first_decl
while i < ctor_idx:
    if decl_re.match(lines[i]):
        j = i
        while j < ctor_idx and '};' not in lines[j]:
            j += 1
        last_end = j
        i = j + 1
    else:
        i += 1

# region of leading comments for the FIRST entry: walk back over comment/blank lines
region_start = first_decl
while region_start - 1 >= 0 and (lines[region_start-1].strip() == '' or lines[region_start-1].lstrip().startswith('//')):
    # stop at the motion-pointer block (keep it in the header)
    if 'enemy-motion-table.md' in lines[region_start-1] or 'CollapseMotion' in lines[region_start-1] or 'datadat-index-and-chr-motions' in lines[region_start-1]:
        break
    region_start -= 1
region_end = last_end   # inclusive

PROVENANCE = re.compile(
    r'^(confirmed from (clean )?dump\b'
    r'|from static table\b'
    r'|All entries from static table\b'
    r'|Stats from EnemySpeciesTable scan\b'
    r'|TableIndex values confirmed from ISO\b)', re.I)

def comment_text(line):
    s = line.strip()
    return s[2:].lstrip() if s.startswith('//') else None

def keep_comment(line):
    t = comment_text(line)
    if t is None:
        return False
    if t.startswith('──') or '────' in t:   # dungeon / section divider
        return False
    if PROVENANCE.match(t):
        return False
    return True

# Parse entries within the region
entries = []
pending = []
i = region_start
removed_provenance = 0
removed_headers = 0
while i <= region_end:
    line = lines[i]
    if decl_re.match(line):
        j = i
        body = []
        while j <= region_end and '};' not in lines[j]:
            body.append(lines[j]); j += 1
        body.append(lines[j])
        text = ' '.join(b.strip() for b in body)
        m = re.search(r'TableIndex=(\d+)', text)
        ti = int(m.group(1)) if m else None
        nm = re.search(r'Name="([^"]*)"', text)
        kept = []
        for c in pending:
            if keep_comment(c):
                kept.append(c)
            else:
                t = comment_text(c)
                if t and (t.startswith('──') or '────' in t):
                    removed_headers += 1
                elif t and PROVENANCE.match(t):
                    removed_provenance += 1
        entries.append(dict(ti=ti, name=nm.group(1) if nm else None, comments=kept, body=body))
        pending = []
        i = j + 1
    elif line.strip() == '':
        i += 1
    else:
        pending.append(line)
        i += 1

# stable sort by TableIndex (None last)
entries_sorted = sorted(entries, key=lambda e: (e['ti'] is None, e['ti'] if e['ti'] is not None else 0))

# Emit
out = []
out.extend(lines[:region_start])
if out and out[-1].strip() != '':
    out.append('\n')
for e in entries_sorted:
    out.extend(e['comments'])
    out.extend(e['body'])
    out.append('\n')
# tail: from region_end+1 (skip blank/divider duplicates handled by tail content)
tail = lines[region_end+1:]
# drop leading blank lines of tail (we already emitted one)
k = 0
while k < len(tail) and tail[k].strip() == '':
    k += 1
out.extend(tail[k:])

dst = SRC if APPLY else SRC + ".new"
with io.open(dst, "w", encoding="utf-8") as f:
    f.writelines(out)

print("entries=%d  region lines=%d->%d" % (len(entries), region_start+1, region_end+1))
print("removed provenance comments=%d  removed section headers=%d" % (removed_provenance, removed_headers))
print("null-TableIndex entries (sorted last):",
      [e['name'] for e in entries_sorted if e['ti'] is None])
ti_list = [e['ti'] for e in entries_sorted if e['ti'] is not None]
print("TableIndex order sorted & ascending:", ti_list == sorted(ti_list))
dups = [t for t in set(ti_list) if ti_list.count(t) > 1]
print("duplicate TableIndex:", dups)
print("wrote", dst)
