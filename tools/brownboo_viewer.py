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
def subfile_mesh(off, size):
    end = off+size; V = []; Tr = []
    for m in re.finditer(rb'MDS\x00', scn[off:end]):
        mds = off+m.start(); nodes = parse_mds(scn, mds)
        if nodes:
            for name, mo, mat in nodes:
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
    v, t = subfile_mesh(off, size)
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
meshes = extract_mesh(scn, prefix='iwa'); cand = []
for name, (verts, tris) in meshes.items():
    for a, b, c in tris:
        va, vb, vc = verts[a], verts[b], verts[c]
        ux, uy, uz = vb[0]-va[0], vb[1]-va[1], vb[2]-va[2]; vx, vy, vz = vc[0]-va[0], vc[1]-va[1], vc[2]-va[2]
        ny = uz*vx-ux*vz; L = math.sqrt((uy*vz-uz*vy)**2+ny*ny+(ux*vy-uy*vx)**2) or 1
        ay = (va[1]+vb[1]+vc[1])/3
        if ny/L > 0.2 and ay < 40: cand.append((ay, [list(va), list(vb), list(vc)]))
cand.sort(key=lambda t: t[0]); col_top = [t for _, t in cand[:110]]
col_skirt = []
for name, (verts, tris) in meshes.items():
    foot = [(x, z) for x, y, z in verts if -6 <= y <= 6] or [(x, z) for x, y, z in verts]
    h = decim(hull(foot), 12); n = len(h)
    for i in range(n):
        a = h[i]; b = h[(i+1) % n]
        col_skirt.append([[a[0], -11, a[1]], [b[0], -11, b[1]], [b[0], 2, b[1]]])
        col_skirt.append([[a[0], -11, a[1]], [b[0], 2, b[1]], [a[0], 2, a[1]]])
perim = [-243,-72,-147,-250,-115,-271,-91,-281,-37,-294,55,-296,71,-292,164,-239,218,-176,266,-68,287,10,295,24,291,95,285,108,285,131,249,169,205,204,179,214,98,232,76,232,10,245,-67,230,-192,160,-248,59,-258,25,-251,-31,-235,-69]
P = [(perim[i], perim[i+1]) for i in range(0, len(perim), 2)]; col_perim = []
for i in range(len(P)):
    a = P[i]; b = P[(i+1) % len(P)]
    col_perim.append([[a[0], -11, a[1]], [b[0], -11, b[1]], [b[0], 3, b[1]]])
    col_perim.append([[a[0], -11, a[1]], [b[0], 3, b[1]], [a[0], 3, a[1]]])
x1, z1, x2, z2, yy = -258, -296, 295, 245, 15
fishrect = [[[x1, yy, z1], [x2, yy, z1], [x2, yy, z2]], [[x1, yy, z1], [x2, yy, z2], [x1, yy, z2]]]

D = {'visual': visual, 'col_top': col_top, 'col_skirt': col_skirt, 'col_perim': col_perim, 'fishrect': fishrect}
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
    ('fishrect','fishing rect (Y=15)','D.fishrect','[255,80,190]',0.28,'#f8b'),
    ('top','COLLISION rock top','D.col_top','[255,70,70]',0.9,'#ff5555'),
    ('skirt','COLLISION skirt','D.col_skirt','[255,200,50]',0.55,'#ffcc33'),
    ('perim','COLLISION perimeter','D.col_perim','[60,210,255]',0.55,'#33ddff'),
]
checks = "".join(f'<label><input type=checkbox id=t_{i} {"checked" if i not in ("crater","fishrect") else ""}> <span style="color:{lc}">{lb}</span></label><br>' for i, lb, src, c, a, lc in LAY)
pushes = "".join(f"if(on('t_{i}')&&{src}) L.push({{t:{src},c:{c},a:{a}}});\n" for i, lb, src, c, a, lc in LAY)
html = '''<div style="margin:0;background:#0d1117;color:#ddd;font-family:monospace;overflow:hidden">
<canvas id="c" style="display:block;cursor:grab;touch-action:none"></canvas>
<div style="position:fixed;top:8px;left:8px;font-size:11px;line-height:1.5;background:rgba(13,17,23,.85);padding:8px 10px;border-radius:6px;user-select:none">
<b>Brownboo COMPLETE</b><br><span style="color:#888">drag=rotate scroll=zoom</span><br>
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
   all.push({r,c:L.c,a:L.a,depth:(r[0][2]+r[1][2]+r[2][2])/3,sh:0.4+0.6*Math.abs(nz/nlen)});
 }}
 all.sort((p,q)=>p.depth-q.depth);
 for(const o of all){
  const pts=o.r.map(p=>[W/2+p[0]*f,H/2-p[1]*f]);
  cx.beginPath();cx.moveTo(pts[0][0],pts[0][1]);cx.lineTo(pts[1][0],pts[1][1]);cx.lineTo(pts[2][0],pts[2][1]);cx.closePath();
  const c=o.c;cx.fillStyle='rgba('+(c[0]*o.sh|0)+','+(c[1]*o.sh|0)+','+(c[2]*o.sh|0)+','+o.a+')';cx.fill();
 }
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
