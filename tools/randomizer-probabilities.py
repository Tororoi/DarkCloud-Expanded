import re, random, collections, datetime

ROOT = "."   # repo-root-relative; run this from the repo root: python3 tools/randomizer-probabilities.py
SRC = ROOT + "/Dark Cloud Improved Version/EnemyData.cs"
txt = open(SRC, encoding="utf-8").read()

species = {}
for m in re.finditer(r'internal static readonly EnemyDefaults (\w+) = new EnemyDefaults \{(.*?)\};', txt, re.S):
    fld, body = m.group(1), m.group(2)
    def g(pat, cast, default=None, b=body):
        mm = re.search(pat, b); return cast(mm.group(1)) if mm else default
    species[fld] = dict(name=g(r'Name="([^"]*)"',str,fld), ti=g(r'TableIndex=(\d+)',int,None),
                        id=g(r'Id=(\d+)',int,0), code=g(r'ModelCode="([^"]*)"',str,None), fp=g(r'ModelFootprint=(\d+)',int,None))
FP_FALLBACK=60000
def fpf(fld): return species[fld]['fp'] if species[fld]['fp'] is not None else FP_FALLBACK
groups={}
for m in re.finditer(r'Dictionary<int, EnemyDefaults> (\w+)\s*=\s*(?://[^\n]*\n\s*)?Group\(([^;]*?)\)\s*;', txt, re.S):
    groups[m.group(1)]=[a.strip() for a in m.group(2).split(',') if a.strip()]
# resolve Merge(...) dicts (e.g. NativeDS) to the union of their referenced Group dicts
for m in re.finditer(r'Dictionary<int, EnemyDefaults> (\w+)\s*=\s*(?://[^\n]*\n\s*)?Merge\(([^;]*?)\)\s*;', txt, re.S):
    merged=[]
    for g in (a.strip() for a in m.group(2).split(',') if a.strip()):
        merged += groups.get(g, [])
    groups[m.group(1)]=merged
tg_block=re.search(r'ThemeGroups\s*=\s*\{(.*?)\};',txt,re.S).group(1)
theme_specs=[(m.group(1),m.group(2),m.group(3)=='true') for m in re.finditer(r'\("([^"]+)",\s*(\w+),\s*(true|false)\)',tg_block)]
thd_blk=re.search(r'ThemeHomeDungeon\s*=\s*new\(\)\s*\{(.*?)\};',txt,re.S).group(1)
theme_home={m.group(1):int(m.group(2)) for m in re.finditer(r'"([^"]+)",\s*(\d+)',thd_blk)}
twm_blk=re.search(r'ThemeWeightMultiplier\s*=\s*new\(\)\s*\{(.*?)\};',txt,re.S).group(1)
theme_mult={m.group(1):float(m.group(2)) for m in re.finditer(r'"([^"]+)",\s*([\d.]+)',twm_blk)}
gm_blk=re.search(r'ThemeGuaranteedMimics\s*=\s*new\(\)\s*\{([^}]*)\}',txt).group(1)
guaranteed_mimics={m.group(1) for m in re.finditer(r'"([^"]+)"',gm_blk)}
NATIVE_BOOST=4.0; FAR_WEIGHT=0.10
def theme_weight(disp,d):
    h=theme_home.get(disp)
    if h is None: base=1.0
    else:
        prox=FAR_WEIGHT+(1-FAR_WEIGHT)*(1-abs(d-h)/6.0)
        base=prox*NATIVE_BOOST if d==h else prox
    return base*theme_mult.get(disp,1.0)
def parse_array(name): return [x.strip() for x in re.search(name+r'\s*=\s*\{([^}]*)\}',txt).group(1).split(',') if x.strip()]
mimics_by_d=parse_array('MimicsByDungeon'); native_by_d=parse_array('NativeByDungeon')
be_blk=re.search(r'BossEnemies\s*=\s*new\(\)\s*\{(.*?)\};',txt,re.S).group(1)
boss_ids={species[m.group(1)]['id'] for m in re.finditer(r'(\w+)\.Id',be_blk)}
eligible=[]
for fld,s in species.items():
    if s['ti'] is None or s['id']==0: continue
    if not s['code'] or s['code'][0]!='e': continue
    if s['id'] in boss_ids or 'Mimic' in (s['name'] or ''): continue
    eligible.append(s['ti'])
ti_name={s['ti']:s['name'] for s in species.values() if s['ti'] is not None}
themes=[(disp,[species[f]['ti'] for f in groups[dfld]],rff) for (disp,dfld,rff) in theme_specs]

CAPS=[413608,278824,342304,442572,298592,324208,270000]; SAFETY=0.90
DUN=["DBC","WOF","SW","SMT","MS","GoT","DS"]
mimics_d=[(species[groups[mimics_by_d[d]][0]]['ti'],species[groups[mimics_by_d[d]][1]]['ti']) for d in range(7)]
natives_d=[[species[f]['ti'] for f in groups[native_by_d[d]]] for d in range(7)]
ThemeChance=.5;MimicChance=.40;KingMimicChance=.28;ThemeCapOneChance=.5;ThemeMimicFillChance=.5;RosterFill=9
FPS={s['ti']:fpf(f) for f,s in species.items() if s['ti'] is not None}
def fp(ti): return FPS.get(ti,FP_FALLBACK)

def build(d,budget,rng):
    mimic,king=mimics_d[d]; natives=natives_d[d]; roster=[]; used=[0]
    def add(ti):
        if ti in roster: return True
        if roster and used[0]+fp(ti)>budget: return False
        roster.append(ti); used[0]+=fp(ti); return True
    if rng.random()<ThemeChance:
        cand=[]; wts=[]
        for i,(disp,mem,rff) in enumerate(themes):
            if rff and sum(fp(t) for t in mem)>budget: continue
            cand.append(i); wts.append(theme_weight(disp,d))
        cand.append(-1); wts.append(1.0)              # mimics theme, unweighted
        rr=rng.random()*sum(wts); pick=cand[-1]; acc=0
        for k in range(len(cand)):
            acc+=wts[k]
            if rr<acc: pick=cand[k]; break
        if pick==-1: members=[mimic,king];rff=False;isM=True;disp="Mimics"
        else: members=list(themes[pick][1]);rff=themes[pick][2];isM=False;disp=themes[pick][0]
        rng.shuffle(members)
        def fillers():
            if rng.random()<ThemeMimicFillChance: return [mimic,king]
            n=list(natives);rng.shuffle(n);return n
        if isM or rff:
            for ti in members:
                if len(roster)>=RosterFill: break
                add(ti)
            if (not isM) and disp in guaranteed_mimics:
                if len(roster)<RosterFill: add(mimic)
                if len(roster)<RosterFill: add(king)
            elif (not isM) and rng.random()<ThemeCapOneChance:
                for ti in fillers():
                    if len(roster)>=RosterFill: break
                    if ti in roster: continue
                    add(ti)
        elif rng.random()>=ThemeCapOneChance:
            for ti in members:
                if len(roster)>=RosterFill: break
                if not add(ti): break
        else:
            fl=fillers(); g=-1
            for ti in fl:
                if add(ti): g=ti; break
            for ti in members:
                if len(roster)>=RosterFill: break
                if not add(ti): break
            for ti in fl:
                if len(roster)>=RosterFill: break
                if ti==g or ti in roster: continue
                add(ti)
    else:
        if rng.random()<MimicChance: add(mimic)
        if rng.random()<KingMimicChance: add(king)
        gd=0
        while len(roster)<RosterFill and gd<1000:
            gd+=1; p=eligible[rng.randrange(len(eligible))]
            if p in roster: continue
            if not add(p): break
    return roster

# A base enemy and its Demon-Shaft 'Enhanced' reskins are visually identical and share an Id, so they're ONE
# on-screen enemy. Collapse rows by Id: a visual enemy "appears" on a floor if ANY of its variant TIs lands in
# the roster, and its theme list is the union across all variants.
ti2id  = {s['ti']: s['id'] for s in species.values() if s['ti'] is not None}
def base_name(n): return re.sub(r'\s*\(Enhanced.*\)$','',n)
id2name = {}
for s in species.values():
    if s['ti'] is None: continue
    id2name.setdefault(s['id'], base_name(s['name']))
    if '(Enhanced' not in (s['name'] or ''): id2name[s['id']] = s['name']
theme_of=collections.defaultdict(set)   # id -> set(theme display)
for disp,mem,rff in themes:
    for t in set(mem): theme_of[ti2id[t]].add(disp)

N=300000; rng=random.Random(2026)
per_d=[collections.Counter() for _ in range(7)]   # per dungeon: id -> floors-present
for d in range(7):
    b=int(CAPS[d]*SAFETY)
    for _ in range(N):
        ids={ti2id[ti] for ti in build(d,b,rng)}
        for eid in ids: per_d[d][eid]+=1

all_ids=set(ti2id[t] for t in eligible)
for d in range(7):
    all_ids|=set(ti2id[t] for t in mimics_d[d])|set(ti2id[t] for t in natives_d[d])
for _,mem,_ in themes: all_ids|=set(ti2id[t] for t in mem)

mimic_ids={ti2id[t] for d in range(7) for t in mimics_d[d]}
# variant TIs per id, for display
id2tis=collections.defaultdict(list)
for t in eligible+[x for d in range(7) for x in mimics_d[d]]:
    if t not in id2tis[ti2id[t]]: id2tis[ti2id[t]].append(t)

def row(eid):
    probs=[per_d[d][eid]/N for d in range(7)]
    return (id2name.get(eid,str(eid)), eid, sum(probs)/7, probs, sorted(theme_of.get(eid,[])))
regular=sorted([row(i) for i in all_ids if i not in mimic_ids], key=lambda r:-r[2])
mimics=sorted([row(i) for i in mimic_ids], key=lambda r:-max(r[3]))

L=[]
L.append("# Enemy Randomizer — Appearance Probabilities\n")
L.append(f"_Generated {datetime.date.today().isoformat()} from `Dark Cloud Improved Version/EnemyData.cs` + `EnemyModelInjector.cs` by Monte-Carlo simulation ({N:,} floors per dungeon). Re-run `tools/randomizer-probabilities.py` after changing themes/membership._\n")
L.append("## What this measures\n")
L.append("For each dungeon, the probability that a given enemy is present **somewhere on a freshly-staged floor** (i.e. lands in that floor's spawn roster, so it spawns at least once). It is **per floor**, not per dungeon run — over many floors you'll see almost everything eligible. The simulation replays the exact `StageFloorRoster` → `BuildFloorRoster` / `BuildThemedRoster` logic, including the model-buffer budget, the distinct-fill, theme selection, `requireFullFit` eligibility, and the weighted mimic insertion.\n")
L.append("## The decision tree (per floor)\n")
L.append("```")
L.append("floor")
L.append(" ├─ 50%  NON-THEMED MIX  (BuildFloorRoster)")
L.append(" │      • +dungeon Mimic      @ 40%   (weighted insertion)")
L.append(" │      • +dungeon King Mimic  @ 28%")
L.append(" │      • fill to 9 distinct species, uniform from the 119-strong")
L.append(" │        eligible pool (RandomizerValid: all non-boss, non-mimic")
L.append(" │        regulars incl. Demon-Shaft 'Enhanced' reskins), budget-capped")
L.append(" │")
L.append(" └─ 50%  THEMED          (BuildThemedRoster)")
L.append("        • pick 1 theme from the eligible themes + the per-dungeon")
L.append("          Mimics theme, WEIGHTED: non-native themes weigh 1; a")
L.append("          dungeon-native theme (the Demon Shaft regions) ramps from")
L.append("          0.10 (most distant dungeon) up to 1.0 at home, ×4 when home")
L.append("        • 50% whole-group (all repeatable)  /  50% capped (1 each")
L.append("          + repeatable mimic OR native fillers, 50/50)")
L.append("```")
L.append("Constants: `ThemeChance 0.50`, `MimicChance 0.40`, `KingMimicChance 0.28`, `ThemeCapOneChance 0.50`, `ThemeMimicFillChance 0.50`, roster = 9 entries, budget = `ModelBufferCapMin × 0.90`.\n")

L.append("## Theme selection by dungeon\n")
L.append("A `requireFullFit` theme (Cards, Days, Gemron Elementals, and the five elements) is only offered on a floor whose buffer fits the **whole** group; otherwise it's dropped that floor. Selection among the rest is **weighted**: a non-native theme weighs 1, while a Demon-Shaft region theme ramps from 0.10 (in DBC) up to 1.0 at home and ×4 in DS itself. The columns below show the pick chance (within the 50% themed half) of one non-native theme vs. one DS-region theme.\n")
L.append(f"| Dungeon | Buffer budget | Eligible themes (of {len(themes)}) | A non-native theme | A DS-region theme | requireFullFit themes excluded |")
L.append("|---|--:|--:|--:|--:|---|")
for d in range(7):
    b=int(CAPS[d]*SAFETY)
    cand=[(disp, theme_weight(disp,d)) for disp,mem,rff in themes if (not rff) or sum(fp(t) for t in mem)<=b]
    cand.append(("Mimics",1.0))
    tot=sum(w for _,w in cand)
    nonnat=1.0/tot*100
    ds=[w for disp,w in cand if disp.startswith("Demon Shaft")]
    dstxt = f"{ds[0]/tot*100:.1f}%" if ds else "—"
    excl=[disp for disp,mem,rff in themes if rff and sum(fp(t) for t in mem)>b]
    L.append(f"| {DUN[d]} | {b:,} | {len(cand)-1} | {nonnat:.1f}% | {dstxt} | {', '.join(excl) if excl else '—'} |")
L.append("\n_DS (Demon Shaft) has no measured `ModelBufferCapMin`; the runtime reads the live cap. This report assumes 270 KB for DS, so its numbers are an estimate._\n")

L.append("## Headline findings\n")
L.append("- **Mimics are the most common single enemies in their home dungeon** (~32–34% of floors for the Mimic, ~22–27% for the King Mimic) and never appear elsewhere.")
L.append("- **Regular enemies cluster ~3–7% per floor**, with a tight spread now that nearly every enemy sits in 1–2 themes. The driver is theme count: a two-theme enemy (e.g. Pirate's Chariot — Pirates/Metal) edges out a one-theme enemy.")
L.append("- **Base + Enhanced are merged into one row** (same `Id`, visually identical), so a creature's odds combine its base and reskin variants and the themes column shows every theme any variant is in (e.g. Gol — Precious + Thunder).")
L.append("- **A dungeon's own natives get a home spike** from the capped-theme native fill (e.g. Rockanoff 12% in DBC vs ~4–5% elsewhere).")
L.append("- **`requireFullFit` themes dip in tight-buffer dungeons**: Cards/Days/Gemron (and Ice/Wind/Thunder in the tightest) are dropped on WOF/MS/GoT/DS floors, so their members read lower there.")
L.append("- **Demon Shaft region themes are proximity-weighted**: each is rare far from home (0.4% pick in DBC) and ramps up with depth, then jumps to a 4× home boost in Demon Shaft (9.8% vs ~2.4% for a non-native theme). Enemies that *only* live in DS themes (Gemrons, Bishop Q, Silver Gear) therefore skew toward the deeper dungeons.\n")

def fmt(r):
    name,eid,ov,probs,th=r
    thtxt = f" <sub>{', '.join(th)}</sub>" if th else ""
    return f"| {name} | {eid} | {ov*100:.1f}% | " + " | ".join(f"{p*100:.0f}" for p in probs) + f" |{thtxt}"

L.append("## Per-enemy probability — regular enemies\n")
L.append("One row per **visual enemy**: a base enemy and its Enhanced reskins (same `Id`) are merged, so the probability is how often you see *that creature* on a floor and the themes column lists **all** themes any of its variants belongs to. `Avg` = mean across the 7 dungeons' floors; per-dungeon columns are % of floors it appears on.\n")
L.append("| Enemy | Id | Avg | "+" | ".join(DUN)+" | themes |")
L.append("|---|--:|--:|"+"--:|"*7+"---|")
for r in regular: L.append(fmt(r))
L.append("")
L.append("## Per-enemy probability — mimics (dungeon-locked)\n")
L.append("Mimics only appear in their own dungeon, so `Avg` (across all 7) understates them; read the home-dungeon column.\n")
L.append("| Enemy | Id | Avg | "+" | ".join(DUN)+" |")
L.append("|---|--:|--:|"+"--:|"*7)
for r in mimics:
    name,eid,ov,probs,th=r
    L.append(f"| {name} | {eid} | {ov*100:.1f}% | " + " | ".join(f"{p*100:.0f}" for p in probs)+" |")
L.append("")
L.append("## Caveats\n")
L.append("- **Per floor, not per run.** Over a full dungeon you'll encounter most eligible enemies; these are single-floor odds.")
L.append("- **\"Appears\" = in the roster.** A capped (SpawnCap 1) enemy shows up once; a repeatable one many times — both count as \"appears\".")
L.append("- **DS budget is an estimate** (live-cap; not yet measured). DBC–GoT use measured `ModelBufferCapMin`.")
L.append("- **Front-side roster modelled.** Non-themed floors roll the normal and Ura sides independently; this models one side per floor (what you meet on a visit).")
L.append("- Monte-Carlo with "+f"{N:,} samples/dungeon → ~±0.2% absolute error.")
L.append("")
open(ROOT+"/randomizer-probabilities.md","w").write("\n".join(L))
print("wrote randomizer-probabilities.md  themes=%d regular=%d mimics=%d"%(len(themes),len(regular),len(mimics)))
