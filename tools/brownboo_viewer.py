#!/usr/bin/env python3
"""Brownboo (map s04) fishing collision-vs-mesh 3D viewer generator.

Decodes the town's exact meshes from gedit/s04/scene.scn, places the instanced meshes (houses,
ladders, plants) from gedit/s04/mapinfo.cfg, builds the fishing collision (rock caps/skirts +
shore perimeter) and the cast rect, and writes a self-contained interactive HTML viewer
(brownboo_viewer.html) next to this script. Requires tools/extract_scene_mesh.py and a local
data.hed/hd2/dat extraction (see extract_scene_mesh.DAT_DIR).

Run: python3 tools/brownboo_viewer.py   ->  tools/brownboo_viewer.html (+ .json)
"""
import os, struct, re, sys, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from extract_scene_mesh import load_scene, parse_mds, read_verts, read_tris, xform, extract_mesh

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = HERE
HTML_NAME = "brownboo_viewer.html"

scn = load_scene('gedit/s04/scene.scn')
GOT = extract_mesh(scn)

def T(pred):
    out = []
    for name, (v, ts) in GOT.items():
        if pred(name):
            for a, b, c in ts: out.append([list(v[a]), list(v[b]), list(v[c])])
    return out

craterids = [f's04g01{n:02d}' for n in range(2, 17)] + ['s040101']
visual = {
    'watereffect': T(lambda n: 'za01' in n),
    'watersurf':   T(lambda n: 'czapp' in n),
    'shore':       T(lambda n: n.startswith('s04g0117')),
    'boardwalk':   T(lambda n: n.startswith('s04g03')),
    'stilts':      T(lambda n: n.startswith('s04g0401') or n in ('s04g402', 's04g403', 's04g404')),
    'rock':        T(lambda n: n.startswith('iwa')),
    'fence':       T(lambda n: n.startswith('st0')),
    'crater':      T(lambda n: any(n.startswith(c) for c in craterids)),
}

# ---- instanced meshes placed from mapinfo.cfg ----
cfg = load_scene('gedit/s04/mapinfo.cfg').decode('latin1', 'replace')
lines = cfg.splitlines(); placements = []; i = 0
while i < len(lines):
    m = re.match(r'\s*(GROUND|WATER)\s+"([^"]+)"', lines[i])
    if m and m.group(2).startswith('s04'):
        nums = []; j = i+1
        while j < len(lines) and len(nums) < 2 and j-i < 14:
            parts = [p.strip() for p in re.split(r'[,\t]', lines[j].split('//')[0]) if p.strip()]
            if parts and all(re.match(r'^-?\d+\.?\d*$', p) for p in parts) and len(parts) >= 3:
                nums.append([float(x) for x in parts[:3]])
            j += 1
        if len(nums) >= 2: placements.append((m.group(2), nums[0], nums[1]))
        i = j
    else: i += 1
# SCN sub-file directory
scndir = {}; o = 0x10
for _ in range(40):
    nm = scn[o:o+16].split(b'\x00')[0].decode('latin1', 'replace')
    if not nm or not nm[0].isalnum(): break
    off, size = struct.unpack_from('<II', scn, o+0x10); scndir.setdefault(nm, []).append((off, size)); o += 0x30
def subfile_mesh(off, size, skip=None):
    end = off+size; V = []; Tr = []
    for m in re.finditer(rb'MDS\x00', scn[off:end]):
        mds = off+m.start(); nodes = parse_mds(scn, mds)
        if nodes:
            for name, mo, mat in nodes:
                if skip and skip(name): continue   # e.g. the reusable door template (obj*) in house blocks
                fo = next((c for c in (mo, mds+mo) if mo and 0 < c < len(scn) and scn[c:c+3] == b'MDT'), None)
                if not fo: continue
                b = len(V); V += [xform(mat, v) for v in read_verts(scn, fo)]; Tr += [(a+b, c+b, d+b) for a, c, d in read_tris(scn, fo)]
        else:
            for mm in re.finditer(rb'MDT\x00', scn[mds:end]):
                fo = mds+mm.start(); b = len(V); V += [list(v) for v in read_verts(scn, fo)]; Tr += [(a+b, c+b, d+b) for a, c, d in read_tris(scn, fo)]; break
    return V, Tr
def placeY(v, pos, ry):
    th = math.radians(ry); c = math.cos(th); s = math.sin(th)
    return [[x*c+z*s+pos[0], y+pos[1], -x*s+z*c+pos[2]] for x, y, z in v]
houses, ladders, plants = [], [], []
used = {}
for name, pos, rot in placements:
    if name.startswith('s04g') or name.startswith('s04w'): continue
    idx = used.get(name, 0); used[name] = idx+1
    ents = scndir.get(name, [])
    if not ents: continue
    off, size = ents[min(idx, len(ents)-1)]
    # houses embed a reusable door template (obj*) at local origin — exclude it (it isn't there in-game)
    skip = (lambda n: n.startswith('obj')) if name.startswith('s04h') else None
    v, t = subfile_mesh(off, size, skip)
    if not v: continue
    pv = placeY(v, pos, rot[1]); tris = [[pv[a], pv[b], pv[c]] for a, b, c in t]
    (houses if name.startswith('s04h') else ladders if name.startswith('s04r') else plants).extend(tris)
visual['houses'] = houses; visual['ladders'] = ladders; visual['plants'] = plants

# ---- collision (rocks) ----
def hull(pts):
    pts = sorted(set((round(x, 1), round(z, 1)) for x, z in pts))
    if len(pts) < 3: return pts
    cr = lambda o, a, b: (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lo = []
    for p in pts:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    up = []
    for p in reversed(pts):
        while len(up) >= 2 and cr(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    return lo[:-1]+up[:-1]
def decim(h, n): return h if len(h) <= n else [h[int(i*len(h)/n)] for i in range(n)]

# ---- UNSIMPLIFIED collision reference: exact mesh triangles, clipped to the collision height band ----
CY_LO, CY_HI = -9, 74        # full collision height band (CY_HI tracks BOX_TOP below)
WATER = 0                    # WaterLevel — perimeter/plants/houses collision caps here
def clip_y(tri, lo, hi):
    """Clip one triangle to the slab lo <= y <= hi; return fan-triangulated pieces (possibly empty)."""
    def cut(poly, above, yv):
        out = []
        for i in range(len(poly)):
            a = poly[i]; b = poly[(i+1) % len(poly)]
            ain = (a[1] >= yv) if above else (a[1] <= yv)
            bn  = (b[1] >= yv) if above else (b[1] <= yv)
            if ain: out.append(a)
            if ain != bn:
                t = (yv - a[1]) / (b[1] - a[1])
                out.append([a[0]+t*(b[0]-a[0]), yv, a[2]+t*(b[2]-a[2])])
        return out
    poly = cut([list(v) for v in tri], True, lo)
    if len(poly) < 3: return []
    poly = cut(poly, False, hi)
    if len(poly) < 3: return []
    return [[poly[0], poly[i], poly[i+1]] for i in range(1, len(poly)-1)]
def clip_group(tris, lo=CY_LO, hi=CY_HI):
    out = []
    for t in tris: out += clip_y(t, lo, hi)
    return out
def line_x(p1, p2, p3, p4):
    (x1, y1), (x2, y2), (x3, y3), (x4, y4) = p1, p2, p3, p4
    d = (x1-x2)*(y3-y4)-(y1-y2)*(x3-x4)
    if abs(d) < 1e-9: return None
    t = ((x1-x3)*(y3-y4)-(y1-y3)*(x3-x4))/d
    return (x1+t*(x2-x1), y1+t*(y2-y1))
def inset_polygon(pts, dd):
    """Offset a closed polygon inward by dd (edge-normal offset + adjacent-edge intersection)."""
    n = len(pts); cx0 = sum(p[0] for p in pts)/n; cz0 = sum(p[1] for p in pts)/n
    edges = []
    for i in range(n):
        a = pts[i]; b = pts[(i+1) % n]; ex, ez = b[0]-a[0], b[1]-a[1]
        nx, nz = -ez, ex; L = math.hypot(nx, nz) or 1; nx /= L; nz /= L
        mx, mz = (a[0]+b[0])/2, (a[1]+b[1])/2
        if nx*(cx0-mx)+nz*(cz0-mz) < 0: nx, nz = -nx, -nz     # inward
        edges.append(((a[0]+nx*dd, a[1]+nz*dd), (b[0]+nx*dd, b[1]+nz*dd)))
    out = []
    for i in range(n):
        p = line_x(edges[(i-1) % n][0], edges[(i-1) % n][1], edges[i][0], edges[i][1])
        out.append(list(p) if p else list(edges[i][0]))
    return out

col_rocks  = clip_group(T(lambda n: n.startswith('iwa')))                                            # -9..44
col_stilts = clip_group(T(lambda n: n.startswith('s04g0401') or n in ('s04g402', 's04g403', 's04g404')))  # -9..44
col_plants = clip_group(plants, hi=WATER)   # placed s04a01, capped at water
col_build  = clip_group(houses, hi=WATER)   # placed s04h*,  capped at water
# perimeter: the traced shoreline inset 20 units inward, extruded to a vertical wall CY_LO..WATER
perim = [-243,-72,-147,-250,-115,-271,-91,-281,-37,-294,55,-296,71,-292,164,-239,218,-176,266,-68,287,10,295,24,291,95,285,108,285,131,249,169,205,204,179,214,98,232,76,232,10,245,-67,230,-192,160,-248,59,-258,25,-251,-31,-235,-69]
Pin = inset_polygon([(perim[i], perim[i+1]) for i in range(0, len(perim), 2)], 20)
col_perim = []
for i in range(len(Pin)):
    a = Pin[i]; b = Pin[(i+1) % len(Pin)]
    col_perim.append([[a[0], CY_LO, a[1]], [b[0], CY_LO, b[1]], [b[0], WATER, b[1]]])
    col_perim.append([[a[0], CY_LO, a[1]], [b[0], WATER, b[1]], [a[0], WATER, a[1]]])
# Fishing rect as a 3D BOX filled with a 6-unit point grid: from fish depth (WaterLevel - fishDepth)
# up to BOX_TOP (arbitrary for now — later = the height at which bobber/hook collisions matter).
# edges (compass: E=+X, W=-X, N=-Z, S=+Z): W=-300, E=240, N=-260, S=300
RECT_X1, RECT_Z1, RECT_X2, RECT_Z2 = -300, -260, 240, 300
FISH_DEPTH = 0 - 6      # WaterLevel 0 - fishDepth 6
BOX_TOP = 74            # TODO: set to the real bobber/hook collision height
def frange(a, b, step):
    out = []; v = a
    while v <= b + 1e-6: out.append(round(v, 3)); v += step
    return out
fishbox = [[x, y, z] for x in frange(RECT_X1, RECT_X2, 10)
                     for y in frange(FISH_DEPTH, BOX_TOP, 10)
                     for z in frange(RECT_Z1, RECT_Z2, 10)]
# coordinate labels (shown with the fishing box): origin, the ! marker, and the 4 rect corners (at top)
fishlabels = [[[0, 0, 0], "0,0"], [[74, 10, -20], "74,10,-20 (!)"]]
for cxx, czz in [(RECT_X1, RECT_Z1), (RECT_X2, RECT_Z1), (RECT_X2, RECT_Z2), (RECT_X1, RECT_Z2)]:
    fishlabels.append([[cxx, BOX_TOP, czz], f"{cxx},{czz}"])

# The fishing trigger point (the "!" marker) and its interaction radius, as a solid sphere.
# Must match the Brownboo Spot in CustomFishingSpot.cs (tx,ty,tz + InteractRadius).
def uv_sphere(cx0, cy0, cz0, r, rings=10, segs=14):
    def pt(i, j):
        th = math.pi * i / rings; ph = 2 * math.pi * j / segs
        return [cx0 + r*math.sin(th)*math.cos(ph), cy0 + r*math.cos(th), cz0 + r*math.sin(th)*math.sin(ph)]
    tris = []
    for i in range(rings):
        for j in range(segs):
            a, b, c, d = pt(i, j), pt(i+1, j), pt(i+1, j+1), pt(i, j+1)
            tris.append([a, b, c]); tris.append([a, c, d])
    return tris
fishpoint = uv_sphere(74, 10, -20, 10)   # trigger (74,10,-20), InteractRadius 10

D = {'visual': visual, 'col_rocks': col_rocks, 'col_stilts': col_stilts, 'col_plants': col_plants,
     'col_build': col_build, 'col_perim': col_perim,
     'fishbox': fishbox, 'fishlabels': fishlabels, 'fishpoint': fishpoint}
js = json.dumps(D, separators=(',', ':'))   # embedded directly in the self-contained HTML
LAY = [
    ('watereffect','water-edge foam','D.visual.watereffect','[100,140,160]',0.5,'#adf'),
    ('watersurf','water surface','D.visual.watersurf','[40,110,140]',0.30,'#8bd'),
    ('shore','shore ring','D.visual.shore','[95,82,60]',1,'#ccc'),
    ('board','boardwalk','D.visual.boardwalk','[70,85,110]',1,'#ccc'),
    ('stilts','stilts','D.visual.stilts','[55,65,80]',1,'#ccc'),
    ('rock','rock','D.visual.rock','[125,98,72]',1,'#ccc'),
    ('fence','fence','D.visual.fence','[75,75,62]',1,'#ccc'),
    ('houses','houses','D.visual.houses','[95,72,52]',1,'#c96'),
    ('ladders','ladders','D.visual.ladders','[150,140,175]',1,'#bbf'),
    ('plants','plants','D.visual.plants','[110,160,90]',1,'#ad6'),
    ('crater','crater (full)','D.visual.crater','[58,54,50]',1,'#ccc'),
    ('fishpoint','! marker + radius','D.fishpoint','[255,175,210]',0.95,'#fbd'),
    ('crock','COLL rocks','D.col_rocks','[255,70,70]',0.85,'#f66'),
    ('cstilt','COLL stilts','D.col_stilts','[255,150,40]',0.85,'#fa4'),
    ('cplant','COLL plants','D.col_plants','[80,230,120]',0.9,'#5e8'),
    ('cbuild','COLL buildings','D.col_build','[205,120,255]',0.7,'#c8f'),
    ('cperim','COLL perimeter (inset 20)','D.col_perim','[60,210,255]',0.5,'#3df'),
]
_off = ("crater", "fishpoint", "crock", "cstilt", "cplant", "cbuild", "cperim")   # off by default
checks = "".join(f'<label><input type=checkbox id=t_{i} {"checked" if i not in _off else ""}> <span style="color:{lc}">{lb}</span></label><br>' for i, lb, src, c, a, lc in LAY)
# fishrect is special: a hot-pink 3D point-grid box + coordinate labels, handled in draw() not as a layer
checks += '<label><input type=checkbox id=t_fishrect> <span style="color:#f4a">fishing box + coords</span></label><br>'
pushes = "".join(f"if(on('t_{i}')&&{src}) L.push({{t:{src},c:{c},a:{a}}});\n" for i, lb, src, c, a, lc in LAY)
html = '''<div style="margin:0;background:#0d1117;color:#ddd;font-family:monospace;overflow:hidden">
<canvas id="c" style="display:block;cursor:grab;touch-action:none"></canvas>
<div style="position:fixed;top:8px;left:8px;font-size:11px;line-height:1.5;background:rgba(13,17,23,.85);padding:8px 10px;border-radius:6px;user-select:none">
<b>Brownboo COMPLETE</b><br><span style="color:#888">drag=rotate scroll=zoom &middot; compass: N=-Z E=+X</span><br>
CHECKS<div id="err" style="color:#f66"></div></div></div>
<script>try{
const D=JSON_DATA;
const cv=document.getElementById('c'),cx=cv.getContext('2d');
let W,H;function resize(){W=cv.width=innerWidth||900;H=cv.height=innerHeight||700;}resize();addEventListener('resize',()=>{resize();draw();});
let yaw=0.6,pitch=-0.55,zoom=1.5;
const on=id=>document.getElementById(id).checked;
function layers(){const L=[];
PUSHES
return L;}
function rot(p){let x=p[0],y=p[1],z=p[2];let cy=Math.cos(yaw),sy=Math.sin(yaw);let x1=x*cy-z*sy,z1=x*sy+z*cy;
 let cp=Math.cos(pitch),sp=Math.sin(pitch);let y1=y*cp-z1*sp,z2=y*sp+z1*cp;return [x1,y1,z2];}
function draw(){
 cx.fillStyle='#0d1117';cx.fillRect(0,0,W,H);
 const f=Math.min(W,H)*0.5*zoom/300, all=[];
 for(const L of layers()){ if(!L.t) continue; for(const tri of L.t){
   const r=[rot(tri[0]),rot(tri[1]),rot(tri[2])];
   const nz=(r[1][0]-r[0][0])*(r[2][1]-r[0][1])-(r[1][1]-r[0][1])*(r[2][0]-r[0][0]);
   const nx=(r[1][1]-r[0][1])*(r[2][2]-r[0][2])-(r[1][2]-r[0][2])*(r[2][1]-r[0][1]);
   const ny=(r[1][2]-r[0][2])*(r[2][0]-r[0][0])-(r[1][0]-r[0][0])*(r[2][2]-r[0][2]);
   const nlen=Math.hypot(nx,ny,nz)||1;
   all.push({k:'t',r,c:L.c,a:L.a,depth:(r[0][2]+r[1][2]+r[2][2])/3,sh:0.4+0.6*Math.abs(nz/nlen)});
 }}
 if(on('t_fishrect')) for(const p of D.fishbox){const r=rot(p);all.push({k:'p',r,depth:r[2]});}
 all.sort((p,q)=>p.depth-q.depth);
 for(const o of all){
  if(o.k==='p'){cx.fillStyle='#ff1493';cx.fillRect(W/2+o.r[0]*f-1.4,H/2-o.r[1]*f-1.4,2.8,2.8);continue;}
  const pts=o.r.map(p=>[W/2+p[0]*f,H/2-p[1]*f]);
  cx.beginPath();cx.moveTo(pts[0][0],pts[0][1]);cx.lineTo(pts[1][0],pts[1][1]);cx.lineTo(pts[2][0],pts[2][1]);cx.closePath();
  const c=o.c;cx.fillStyle='rgba('+(c[0]*o.sh|0)+','+(c[1]*o.sh|0)+','+(c[2]*o.sh|0)+','+o.a+')';cx.fill();
 }
 if(on('t_fishrect')) drawLabels();
 drawCompass();
}
function drawLabels(){
 const f=Math.min(W,H)*0.5*zoom/300;
 cx.save();cx.font='bold 12px monospace';cx.textAlign='left';cx.textBaseline='middle';
 for(const lb of D.fishlabels){const r=rot(lb[0]);const x=W/2+r[0]*f,y=H/2-r[1]*f;
  cx.fillStyle='#ff1493';cx.beginPath();cx.arc(x,y,3,0,7);cx.fill();
  const w=cx.measureText(lb[1]).width;cx.fillStyle='rgba(0,0,0,.75)';cx.fillRect(x+4,y-7,w+5,14);
  cx.fillStyle='#ff9fd6';cx.fillText(lb[1],x+6,y);}
 cx.restore();
}
// Compass overlay: world convention N=-Z, S=+Z, E=+X, W=-X. Rotates with the view so you can call
// out headings. N is red + thicker.
function drawCompass(){
 const cxp=W-80,cyp=58,CR=34;
 cx.save();cx.font='bold 13px monospace';cx.textAlign='center';cx.textBaseline='middle';cx.lineWidth=1;
 cx.fillStyle='rgba(13,17,23,.7)';cx.beginPath();cx.arc(cxp,cyp,CR+13,0,7);cx.fill();
 const dirs=[['N',[0,0,-1],'#f66'],['E',[1,0,0],'#9cf'],['S',[0,0,1],'#999'],['W',[-1,0,0],'#9cf']];
 for(const d of dirs){
  const r=rot(d[1]);let sx=r[0],sy=-r[1];const L2=Math.hypot(sx,sy)||1;sx/=L2;sy/=L2;
  cx.strokeStyle=d[2];cx.lineWidth=d[0]=='N'?2.5:1.3;
  cx.beginPath();cx.moveTo(cxp,cyp);cx.lineTo(cxp+sx*CR,cyp+sy*CR);cx.stroke();
  cx.fillStyle=d[2];cx.fillText(d[0],cxp+sx*(CR+9),cyp+sy*(CR+9));
 }
 cx.restore();
}
draw();
let drag=false,px,py;
cv.addEventListener('pointerdown',e=>{drag=true;px=e.clientX;py=e.clientY;cv.style.cursor='grabbing';});
addEventListener('pointerup',()=>{drag=false;cv.style.cursor='grab';});
addEventListener('pointermove',e=>{if(!drag)return;yaw+=(e.clientX-px)*.01;pitch+=(e.clientY-py)*.01;px=e.clientX;py=e.clientY;draw();});
cv.addEventListener('wheel',e=>{e.preventDefault();zoom*=e.deltaY<0?1.1:0.9;draw();},{passive:false});
for(const cb of document.querySelectorAll('input')) cb.addEventListener('change',draw);
}catch(e){document.getElementById('err').textContent='ERR: '+e.message;}</script>'''
open(os.path.join(OUT, HTML_NAME), 'w').write(html.replace('CHECKS', checks).replace('PUSHES', pushes).replace('JSON_DATA', js))
print("visual:", {k: len(v) for k, v in visual.items()})
