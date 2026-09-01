#!/usr/bin/env python3
"""Shared self-contained HTML/JS scene viewer used by the per-town fishing viewers
(brownboo_viewer.py, queens_viewer.py, yellowdrops_viewer.py and any future town).

The renderer is a hand-rolled canvas projection (no external libs) with: per-layer show/hide checkboxes,
fill / wireframe / backface-cull toggles, node labels + bounding-box borders, a cursor->world (x,z)
coordinate readout on the water plane, a compass, and click / shift+click polygon selection that emits a
copyable list of triangles (for adding as fishing collision).

Navigation: drag=rotate, scroll=zoom, SPACE+drag=pan (view translate), CMD/CTRL+drag=world-XZ clip rect
acting as a true mask: geometry is CUT at the rect boundary (Sutherland-Hodgman in XZ), not culled per
poly; clicking a cut poly still selects/emits the original full triangle. "show whole town" resets. Layer
fills are always drawn fully opaque regardless of the layer's declared alpha (detail-work readability).

build_html(title, layers, node_labels=..., points=..., point_labels=..., coord_note=...) -> html string.
  layers: ordered list of dicts, each:
      {'key','label','tris':[[[x,y,z]*3]...],'color':[r,g,b],'alpha':float,'border':'#hex','on':bool}
  node_labels: [[centroid[x,y,z], name, bbox[minx,miny,minz,maxx,maxy,maxz], layer_key], ...]
  points:       optional [[x,y,z], ...] point-cloud overlay (toggle "points")
  point_labels: optional [[[x,y,z], text], ...] text markers drawn with the point overlay
  coord_note:   label suffix for the coordinate readout (e.g. "water y=0")
"""
import json
import re


# Georama editor module (injected into the shared renderer when build_html(georama=...) is given). Parts
# render THROUGH the z-buffer with the scene; click a palette part to pick up, click a platform to place,
# drag a placed part to move it, R=rotate, Del=delete, Esc=drop. Uses the renderer's rot()/W/H/zoom/draw.
_GEORAMA_JS = r'''const GEO=D.georama;
let gpl=[],gHeld=null,gRot=0,gSel=-1,gMove=-1,gCur=null,gShowColl=false,gShowCamColl=false,gShowLod1=false,gShowLod2=false,gShowCand=false,gShowParts=true;const gUsed=new Set();
const GF=()=>Math.min(W,H)*0.5*zoom/300;
function gFp(n,r){const p=GEO.parts[n];return (r&1)?[p.fd,p.fw]:[p.fw,p.fd];}
function gXform(tris,X,Y,Z,r){const a=r*Math.PI/2,ca=Math.cos(a),sa=Math.sin(a),o=[];
 for(const t of tris){const nt=[];for(const p of t){const x=p[0],z=p[2];nt.push([x*ca-z*sa+X,p[1]+Y,x*sa+z*ca+Z]);}o.push(nt);}return o;}
function gCellC(reg,cx,cz,fw,fd){const R=GEO.regions[reg];return [R.x0+(cx+fw/2)*GEO.cell,R.y,R.z0+(cz+fd/2)*GEO.cell];}
function gPush(all,tri,c,a){const r=[rot(tri[0]),rot(tri[1]),rot(tri[2])];
 const nz=(r[1][0]-r[0][0])*(r[2][1]-r[0][1])-(r[1][1]-r[0][1])*(r[2][0]-r[0][0]);
 const nx=(r[1][1]-r[0][1])*(r[2][2]-r[0][2])-(r[1][2]-r[0][2])*(r[2][1]-r[0][1]);
 const ny=(r[1][2]-r[0][2])*(r[2][0]-r[0][0])-(r[1][0]-r[0][0])*(r[2][2]-r[0][2]);
 const nl=Math.hypot(nx,ny,nz)||1;
 all.push({k:'t',g:1,r,c,a,depth:(r[0][2]+r[1][2]+r[2][2])/3,sh:0.4+0.6*Math.abs(nz/nl)});}
function gAddTris(all){
 for(const rd of GEO.roads){const y=rd.y+0.5,q=[[rd.x-50,y,rd.z-50],[rd.x+50,y,rd.z-50],[rd.x+50,y,rd.z+50],[rd.x-50,y,rd.z+50]];
  gPush(all,[q[0],q[1],q[2]],[100,100,120],1);gPush(all,[q[0],q[2],q[3]],[100,100,120],1);}
 if(gShowParts)gpl.forEach((o,i)=>{const wt=gXform(GEO.parts[o.name].tris,o.x,o.y,o.z,o.rot),k=GEO.parts[o.name].kind,
   col=i===gSel?[120,230,150]:(k==='tree'?[90,170,110]:[200,165,120]);for(const t of wt)gPush(all,t,col,1);});
 if(gShowColl)gpl.forEach(o=>{const P=GEO.parts[o.name];
   if(P.cnodes){for(const nd of P.cnodes){const wt=gXform(nd.t,o.x,o.y,o.z,o.rot);for(const t of wt)gPush(all,t,nd.col,1);}}
   else if(P.ctris){const wt=gXform(P.ctris,o.x,o.y,o.z,o.rot);for(const t of wt)gPush(all,t,[230,20,20],1);}});
 if(gShowCamColl)gpl.forEach(o=>{const cm=GEO.parts[o.name].camtris;if(cm){const wt=gXform(cm,o.x,o.y,o.z,o.rot);
   for(const t of wt)gPush(all,t,[80,200,255],1);}});
 if(gShowLod1)gpl.forEach(o=>{const lm=GEO.parts[o.name].lod1tris;if(lm){const wt=gXform(lm,o.x,o.y,o.z,o.rot);
   for(const t of wt)gPush(all,t,[110,200,160],0.65);}});
 if(gShowLod2)gpl.forEach(o=>{const lm=GEO.parts[o.name].lod2tris;if(lm){const wt=gXform(lm,o.x,o.y,o.z,o.rot);
   for(const t of wt)gPush(all,t,[230,130,120],0.65);}});
 if(gShowCand)gpl.forEach(o=>{const lm=GEO.parts[o.name].candtris;if(lm){const wt=gXform(lm,o.x,o.y,o.z,o.rot);
   for(const t of wt)gPush(all,t,[255,220,90],0.7);}});
 if(gHeld&&gCur){const fp=gFp(gHeld,gRot),ct=gCellC(gCur.reg,gCur.cx,gCur.cz,fp[0],fp[1]),wt=gXform(GEO.parts[gHeld].tris,ct[0],ct[1],ct[2],gRot),
   col=GEO.parts[gHeld].kind==='tree'?[150,230,160]:[255,210,150];for(const t of wt)gPush(all,t,col,0.55);}}
function gPick(mx,my){const f=GF(),rx=(mx-W/2)/f,ry=-(my-H/2)/f,cyw=Math.cos(yaw),syw=Math.sin(yaw),sp=Math.sin(pitch),cp=Math.cos(pitch);
 if(Math.abs(sp)<0.05)return null;
 for(const R of GEO.regions){const z1=((R.y-CTR[1])*cp-ry)/sp,wx=rx*cyw+z1*syw+CTR[0],wz=-rx*syw+z1*cyw+CTR[2];
  const cX=Math.floor((wx-R.x0)/GEO.cell),cZ=Math.floor((wz-R.z0)/GEO.cell);
  if(cX>=0&&cX<R.nx&&cZ>=0&&cZ<R.nz)return {reg:R.id,cx:cX,cz:cZ,wx,wz};}return null;}
function gPickPart(mx,my){const pk=gPick(mx,my);if(!pk)return -1;let b=1e9,s=-1;
 gpl.forEach((o,i)=>{const dx=o.x-pk.wx,dz=o.z-pk.wz,d=dx*dx+dz*dz;if(d<b&&d<14000){b=d;s=i;}});return s;}
function gMoveTo(mx,my){const pk=gPick(mx,my);if(!pk||gMove<0)return;const o=gpl[gMove],fp=gFp(o.name,o.rot),R=GEO.regions[pk.reg];
 const cx0=Math.max(0,Math.min(pk.cx,R.nx-fp[0])),cz0=Math.max(0,Math.min(pk.cz,R.nz-fp[1])),ct=gCellC(pk.reg,cx0,cz0,fp[0],fp[1]);
 o.x=ct[0];o.y=ct[1];o.z=ct[2];gUpd();}
function gPlace(mx,my){const pk=gPick(mx,my);if(!pk)return;const fp=gFp(gHeld,gRot),R=GEO.regions[pk.reg];
 const cx0=Math.max(0,Math.min(pk.cx,R.nx-fp[0])),cz0=Math.max(0,Math.min(pk.cz,R.nz-fp[1])),ct=gCellC(pk.reg,cx0,cz0,fp[0],fp[1]);
 gpl.push({name:gHeld,x:ct[0],y:ct[1],z:ct[2],rot:gRot});if(GEO.parts[gHeld].kind==='bldg'){gUsed.add(gHeld);gSetHeld(null);}gUpd();gPalRefresh();draw();}
function gOverlay(f){cx.lineWidth=0.5;cx.font='11px monospace';const P=p=>{const r=rot(p);return [W/2+r[0]*f,H/2-r[1]*f];};
 for(const R of GEO.regions){cx.strokeStyle='rgba(90,150,170,0.45)';cx.beginPath();
  for(let i=0;i<=R.nx;i++){const a=P([R.x0+i*GEO.cell,R.y,R.z0]),b=P([R.x0+i*GEO.cell,R.y,R.z0+R.nz*GEO.cell]);cx.moveTo(a[0],a[1]);cx.lineTo(b[0],b[1]);}
  for(let j=0;j<=R.nz;j++){const a=P([R.x0,R.y,R.z0+j*GEO.cell]),b=P([R.x0+R.nx*GEO.cell,R.y,R.z0+j*GEO.cell]);cx.moveTo(a[0],a[1]);cx.lineTo(b[0],b[1]);}
  cx.stroke();const c=P([R.x0,R.y,R.z0]);cx.fillStyle='#8bd';cx.fillText('reg '+R.id+' y='+R.y,c[0]+3,c[1]-3);}
 if(gCur){const R=GEO.regions[gCur.reg],fp=gHeld?gFp(gHeld,gRot):[1,1],x=R.x0+gCur.cx*GEO.cell,z=R.z0+gCur.cz*GEO.cell,y=R.y;
  const cc=[[x,y,z],[x+fp[0]*GEO.cell,y,z],[x+fp[0]*GEO.cell,y,z+fp[1]*GEO.cell],[x,y,z+fp[1]*GEO.cell]].map(P);
  cx.strokeStyle=gHeld?'#6ee7b7':'#8ac';cx.lineWidth=2;cx.beginPath();cx.moveTo(cc[0][0],cc[0][1]);for(let k=1;k<4;k++)cx.lineTo(cc[k][0],cc[k][1]);cx.closePath();cx.stroke();}}
function gUpd(){document.getElementById('gpc').textContent=gpl.length;
 document.getElementById('gout').value=gpl.map(o=>o.name+' ('+Math.round(o.x)+','+Math.round(o.z)+') y'+o.y+' r'+o.rot).join(String.fromCharCode(10));}
function gPalRefresh(){for(const b of document.querySelectorAll('.gpb')){b.classList.toggle('sel',b.dataset.p===gHeld);
  b.classList.toggle('used',b.dataset.k==='bldg'&&gUsed.has(b.dataset.p)&&b.dataset.p!==gHeld);}}
function gSetHeld(n){if(n&&GEO.parts[n].kind==='bldg'&&gUsed.has(n))return;gHeld=n;gSel=-1;
 document.getElementById('gheld').textContent=n||'none';gPalRefresh();draw();}
function gDefault(){gpl=GEO.default.map(o=>Object.assign({},o));gUsed.clear();
 for(const o of gpl)if(GEO.parts[o.name]&&GEO.parts[o.name].kind==='bldg')gUsed.add(o.name);gSel=-1;gUpd();gPalRefresh();draw();}
for(const b of document.querySelectorAll('.gpb'))b.onclick=()=>gSetHeld(b.dataset.p);
document.getElementById('gparts').onchange=e=>{gShowParts=e.target.checked;draw();};
document.getElementById('gcoll').onchange=e=>{gShowColl=e.target.checked;draw();};
document.getElementById('gcamcoll').onchange=e=>{gShowCamColl=e.target.checked;draw();};
{const e4=document.getElementById('glod1');if(e4)e4.onchange=e=>{gShowLod1=e.target.checked;draw();};}
{const e5=document.getElementById('glod2');if(e5)e5.onchange=e=>{gShowLod2=e.target.checked;draw();};}
{const e6=document.getElementById('gcand');if(e6)e6.onchange=e=>{gShowCand=e.target.checked;draw();};}
document.getElementById('greset').onclick=gDefault;
document.getElementById('gclear').onclick=()=>{gpl=[];gUsed.clear();gSel=-1;gUpd();gPalRefresh();draw();};
addEventListener('keydown',e=>{const k=e.key.toLowerCase();
 if(k==='r'){if(gHeld)gRot=(gRot+1)&3;else if(gSel>=0){gpl[gSel].rot=(gpl[gSel].rot+1)&3;gUpd();}draw();}
 else if(k==='escape')gSetHeld(null);
 else if(k==='delete'||k==='backspace'){if(gSel>=0){const o=gpl[gSel];if(GEO.parts[o.name]&&GEO.parts[o.name].kind==='bldg')gUsed.delete(o.name);gpl.splice(gSel,1);gSel=-1;gUpd();gPalRefresh();draw();}}});
gDefault();'''


def _pcnt(georama, key):
    """' (h04:587 h05:725 ...)' — per-part tri counts for a georama part attribute, for toggle labels."""
    if not georama:
        return ''
    items = [(n, len(p[key])) for n, p in sorted(georama.get('parts', {}).items()) if key in p]
    if not items:
        return ''
    return ' <span style="color:#889">(' + ' '.join(f'{n[-3:]}:{c}' for n, c in items) + ')</span>'


def build_html(title, layers, node_labels=None, points=None, point_labels=None,
               points_label="markers + coords", coord_note="water y=0", points_on=True, georama=None):
    node_labels = node_labels or []
    points = points or []
    point_labels = point_labels or []

    # D: all geometry, embedded directly in the self-contained HTML.
    D = {'nodelabels': node_labels, 'points': points, 'plabels': point_labels}
    for L in layers:
        D[L['key']] = L['tris']
    if georama is not None:
        D['georama'] = georama       # {parts:{name:{tris,kind,fw,fd}}, regions:[...], default:[...], roads:[...], cell}
    js = json.dumps(D, separators=(',', ':'))

    # ---- optional georama editor: parts palette + placement/move, rendered THROUGH the z-buffer.
    #      Lives in a "Georama editor" folder in the LEFT toggle panel; the folder's master checkbox
    #      hides parts/overlay and disables placement without touching the inner toggle states.
    if georama is not None:
        def gpal_group(kind, title):
            items = "".join(
                f'<button class="gpb" data-p="{nm}" data-k="{kind}">{nm[3:]} '
                f'<span>{georama["parts"][nm]["fw"]}x{georama["parts"][nm]["fd"]}</span></button>'
                for nm in sorted(georama['parts']) if georama['parts'][nm]['kind'] == kind)
            return f'<div class="gpt">{title}</div>{items}' if items else ''
        georama_fold = ('<style>.gpb{margin:1px;font-size:10px;background:#1b2330;color:#cde;border:1px solid #345;'
                    'border-radius:3px;cursor:pointer}.gpb span{color:#789}.gpb.sel{background:#2b6;color:#032}'
                    '.gpb.used{opacity:.35}.gpt{color:#9ab;margin:5px 0 2px;font-size:10px;font-weight:bold}</style>'
                    '<div style="width:215px">'
                    '<span style="color:#888">click=pick up &middot; drag a placed part=move<br>'
                    'R=rotate Del=delete Esc=drop</span>'
                    + gpal_group('bldg', 'Buildings (once)') + gpal_group('road', 'Roads') + gpal_group('tree', 'Trees')
                    + '<div style="margin-top:6px;border-top:1px solid #444;padding-top:4px">held: '
                    '<b id="gheld" style="color:#6ee7b7">none</b><br>'
                    '<label style="color:#cde;cursor:pointer"><input type="checkbox" id="gparts" checked '
                    'style="vertical-align:middle"> show parts</label><br>'
                    '<label style="color:#f7787f;cursor:pointer"><input type="checkbox" id="gcoll" '
                    'style="vertical-align:middle"> vanilla player coll _a (georama)</label><br>'
                    '<label style="color:#5bf;cursor:pointer"><input type="checkbox" id="gcamcoll" '
                    'style="vertical-align:middle"> vanilla cam coll _c (georama)' + _pcnt(georama, 'camtris') + '</label><br>'
                    '<label style="color:#7da;cursor:pointer"><input type="checkbox" id="glod1" '
                    'style="vertical-align:middle"> simplified visual LOD1 medium' + _pcnt(georama, 'lod1tris') + '</label><br>'
                    '<label style="color:#e87;cursor:pointer"><input type="checkbox" id="glod2" '
                    'style="vertical-align:middle"> simplified visual LOD2 low' + _pcnt(georama, 'lod2tris') + '</label><br>'
                    '<label style="color:#fd6;cursor:pointer"><input type="checkbox" id="gcand" '
                    'style="vertical-align:middle"> custom _c candidate [NOT BAKED — vanilla _c kept]' + _pcnt(georama, 'candtris') + '</label><br>'
                    '<button id="greset">reset to default</button> <button id="gclear">clear</button><br>'
                    'placed: <b id="gpc">0</b></div>'
                    '<textarea id="gout" readonly style="width:100%;height:90px;margin-top:5px;background:#0d1117;'
                    'color:#6ee7b7;border:1px solid #333;border-radius:4px;font-size:10px;box-sizing:border-box"></textarea></div>')
        georama_js = _GEORAMA_JS
    else:
        georama_fold, georama_js = '', 'const GEO=null;'

    cnt = {f"t_{L['key']}": len(L['tris']) for L in layers}
    cnt_js = json.dumps(cnt, separators=(',', ':'))

    # ---- toggle panel with FOLDERS: layers sharing a 'group' collapse into a <details> folder whose
    #      summary checkbox is a master on/off for everything inside — it greys the children out and
    #      excludes them from rendering WITHOUT changing any child's own checked state.
    def _gid(name):
        return re.sub(r'[^a-z0-9]+', '_', name.lower()).strip('_')

    def _fold_html(gid, title, inner, open_=False):
        return (f'<details class="fold" id="fold_{gid}"{" open" if open_ else ""}>'
                f'<summary><input type=checkbox id=g_{gid} checked> <b>{title}</b></summary>'
                f'<div class="fbody">{inner}</div></details>')

    def _lyr_row(L):
        return (f'<label><input type=checkbox id=t_{L["key"]} {"checked" if L.get("on") else ""}> '
                f'<span style="color:{L["border"]}">{L["label"]}</span> '
                f'<span style="color:#777">({len(L["tris"])})</span></label><br>')

    grp_map = {}                       # toggle id -> folder gid (JS gates visibility on the folder master)
    folds, order = {}, []              # folders in first-seen order, ungrouped rows inline
    for L in layers:
        g = L.get('group')
        if not g:
            order.append(('raw', _lyr_row(L))); continue
        gid = _gid(g)
        if gid not in folds:
            folds[gid] = {'title': g, 'rows': []}
            order.append(('fold', gid))
        folds[gid]['rows'].append(_lyr_row(L))
        grp_map[f't_{L["key"]}'] = gid

    checks = ('<style>details.fold{margin:2px 0}details.fold summary{cursor:pointer;color:#aac}'
              'details.fold .fbody{margin-left:15px}details.fold.off .fbody{opacity:.35}</style>')
    for kind, val in order:
        checks += val if kind == 'raw' else _fold_html(val, folds[val]['title'], ''.join(folds[val]['rows']))
    checks += (f'<label><input type=checkbox id=t_points {"checked" if points_on else ""}> '
               f'<span style="color:#f4a">{points_label}</span> '
               f'<span style="color:#777">({len(points)} pts)</span></label><br>')
    if georama_fold:
        checks += _fold_html('georama', 'Georama editor', georama_fold)
    render_rows = ('<label><input type=checkbox id=t_steep> <span style="color:#ff2db4">steep &amp; above-water (highlight)</span></label><br>'
                   '<label><input type=checkbox id=t_vonly> <span style="color:#5bf">vertical tris only</span></label><br>'
                   '<label><input type=checkbox id=r_fill checked> fill</label> '
                   '<label><input type=checkbox id=r_wire> wireframe</label><br>'
                   '<label><input type=checkbox id=r_cull> backface cull</label><br>'
                   '<label><input type=checkbox id=r_wind> <span style="color:#fe0">winding: backface &rarr; neon yellow</span></label><br>'
                   '<label><input type=checkbox id=r_labels> node labels + borders</label><br>'
                   '<button id="clipreset" style="margin-top:3px;font-size:10px;cursor:pointer">'
                   'show whole town (reset clip rect)</button>')
    for rid in ('t_steep', 't_vonly', 'r_fill', 'r_wire', 'r_cull', 'r_wind', 'r_labels'):
        grp_map[rid] = 'render'
    checks += _fold_html('render', 'Render settings', render_rows, open_=True)
    checks += '<div style="margin-top:5px;border-top:1px solid #444;padding-top:4px">selected: <b id="tot" style="color:#fff">0</b> polys</div>'
    grp_js = json.dumps(grp_map, separators=(',', ':'))

    pushes = "".join(
        f"if(on('t_{L['key']}')&&D[{json.dumps(L['key'])}]) "
        f"L.push({{t:D[{json.dumps(L['key'])}],c:{L['color']},a:{L['alpha']}}});\n"
        for L in layers)

    html = '''<div style="margin:0;background:#0d1117;color:#ddd;font-family:monospace;overflow:hidden">
<canvas id="c" style="display:block;cursor:grab;touch-action:none"></canvas>
<div style="position:fixed;top:8px;left:8px;font-size:11px;line-height:1.5;background:rgba(13,17,23,.85);padding:8px 10px;border-radius:6px;user-select:none;max-height:96vh;overflow:auto">
<b>TITLE</b><br><span style="color:#888">drag=rotate scroll=zoom &middot; space+drag=pan &middot; shift+drag=box select &middot; cmd+drag=clip rect &middot; compass: N=-Z E=+X</span><br>
CHECKS<div id="err" style="color:#f66"></div></div></div>
<div id="coord" style="position:fixed;bottom:10px;left:10px;font-size:16px;font-weight:bold;background:rgba(13,17,23,.92);padding:7px 14px;border-radius:6px;color:#6ee7b7;user-select:none">move cursor over the surface for coordinates</div>
<div style="position:fixed;bottom:10px;right:10px;width:300px;font-size:11px;background:rgba(13,17,23,.94);padding:8px 10px;border-radius:6px">
<b>Selected polys: <span id="selcount" style="color:#f44">0</span></b>
<button id="selclear" style="float:right;font-size:10px">clear</button>
<button id="selundo" style="float:right;font-size:10px;margin-right:4px">undo</button><br>
<span style="color:#888">click=nearest poly &middot; shift+click=add/remove &middot; shift+drag=box add &middot; Ctrl/Cmd+Z=undo &middot; <b style="color:#fdba5b">F+drag=fishing rect</b></span>
<input id="fishout" readonly spellcheck="false" placeholder="hold F + drag on the water to define a fishing rect" onclick="this.select()" style="width:100%;margin-top:5px;background:#0d1117;color:#fdba5b;border:1px solid #333;border-radius:4px;font-family:monospace;font-size:10px;box-sizing:border-box;padding:3px"/>
<textarea id="sellist" readonly spellcheck="false" placeholder="clicked polys appear here: x0,y0,z0, x1,y1,z1, x2,y2,z2 (one triangle per line)" style="width:100%;height:130px;margin-top:5px;background:#0d1117;color:#6ee7b7;border:1px solid #333;border-radius:4px;font-family:monospace;font-size:10px;box-sizing:border-box"></textarea>
</div>
<script>try{
const D=JSON_DATA;
const CNT=CNT_DATA;
const GRP=GRP_DATA;   // toggle id -> folder gid; a folder's summary checkbox master-gates its children
function updateTotal(){let s=0;for(const k in CNT){if(on(k))s+=CNT[k];}const t=document.getElementById('tot');if(t)t.textContent=s;}
const cv=document.getElementById('c'),cx=cv.getContext('2d');
let W,H;function resize(){W=cv.width=innerWidth||900;H=cv.height=innerHeight||700;}resize();addEventListener('resize',()=>{resize();draw();});
let yaw=0.6,pitch=0.55,zoom=1.5;   // +pitch = camera ABOVE the model, looking down
const WATER=COORDY, STEEP_NY=0.2, VONLY_NY=0.35;   // bobber lands on |normal.Y|>0.2; vertical face = |ny|<0.35
const foldOn=gid=>{const m=document.getElementById('g_'+gid);return !m||m.checked;};
const on=id=>{const e=document.getElementById(id);if(!e||!e.checked)return false;const g=GRP[id];return !g||foldOn(g);};
const GEOON=()=>foldOn('georama');   // georama folder master gates parts/overlay/placement
// folder master checkboxes: grey the body when off (children keep their own checked state);
// stopPropagation so clicking the checkbox doesn't also collapse/expand the <details>
for(const d of document.querySelectorAll('details.fold')){const m=d.querySelector('summary input');if(!m)continue;
 m.addEventListener('click',e=>e.stopPropagation());
 m.addEventListener('change',()=>d.classList.toggle('off',!m.checked));}
function layers(){const L=[];
PUSHES
return L;}
function rot0(p){let x=p[0],y=p[1],z=p[2];let cy=Math.cos(yaw),sy=Math.sin(yaw);let x1=x*cy-z*sy,z1=x*sy+z*cy;
 let cp=Math.cos(pitch),sp=Math.sin(pitch);let y1=y*cp-z1*sp,z2=y*sp+z1*cp;return [x1,y1,z2];}
let CTR=[0,0,0];   // world-space orbit pivot: space+drag translates it in the view plane, so
                   // rotation AND zoom stay centred on the panned point. rot0 = raw (compass).
function rot(p){return rot0([p[0]-CTR[0],p[1]-CTR[1],p[2]-CTR[2]]);}
let CLIP=null,RSEL=null;   // CLIP=[minX,maxX,minZ,maxZ] world-XZ mask rect (cmd+drag); RSEL = in-progress drag
let FSEL=null,FRECT=null,FHELD=false;   // F+drag = define a fishing rect on the water plane (world XZ)
function inClip(wx,wz){return !CLIP||(wx>=CLIP[0]&&wx<=CLIP[1]&&wz>=CLIP[2]&&wz<=CLIP[3]);}
// Sutherland-Hodgman clip of a world tri against the CLIP rect's 4 vertical planes (y interpolated),
// so the rect behaves as a true mask: boundary-straddling polys are cut at the edge, not dropped whole.
function clipTriXZ(t,R){
 let poly=t;
 for(const pl of [[1,0,-R[0]],[-1,0,R[1]],[0,1,-R[2]],[0,-1,R[3]]]){   // ax*x+az*z+c>=0 = inside
  const out=[];
  for(let i=0;i<poly.length;i++){
   const A=poly[i],B=poly[(i+1)%poly.length];
   const da=pl[0]*A[0]+pl[1]*A[2]+pl[2], db=pl[0]*B[0]+pl[1]*B[2]+pl[2];
   if(da>=0)out.push(A);
   if((da>=0)!==(db>=0)){const s=da/(da-db);out.push([A[0]+(B[0]-A[0])*s,A[1]+(B[1]-A[1])*s,A[2]+(B[2]-A[2])*s]);}
  }
  poly=out; if(poly.length<3)return [];
 }
 const tris=[];for(let i=1;i<poly.length-1;i++)tris.push([poly[0],poly[i],poly[i+1]]);
 return tris;
}
function draw(){
 cx.fillStyle='#0d1117';cx.fillRect(0,0,W,H);
 const f=Math.min(W,H)*0.5*zoom/300, all=[];
 const steep=on('t_steep'), vonly=on('t_vonly');
 for(const L of layers()){ if(!L.t) continue; for(const tri0 of L.t){
  const parts=CLIP?clipTriXZ(tri0,CLIP):[tri0];   // mask: cut at the rect boundary
  for(const tri of parts){
   let wny=0, wl=1;                                // world normal Y (for steep highlight / vertical-only filter)
   if(steep||vonly){
     const wnx=(tri[1][1]-tri[0][1])*(tri[2][2]-tri[0][2])-(tri[1][2]-tri[0][2])*(tri[2][1]-tri[0][1]);
     wny=(tri[1][2]-tri[0][2])*(tri[2][0]-tri[0][0])-(tri[1][0]-tri[0][0])*(tri[2][2]-tri[0][2]);
     const wnz=(tri[1][0]-tri[0][0])*(tri[2][1]-tri[0][1])-(tri[1][1]-tri[0][1])*(tri[2][0]-tri[0][0]);
     wl=Math.hypot(wnx,wny,wnz)||1;
   }
   if(vonly && Math.abs(wny)/wl>=VONLY_NY) continue;   // "vertical only": drop near-horizontal faces from view+pick
   const r=[rot(tri[0]),rot(tri[1]),rot(tri[2])];
   const nz=(r[1][0]-r[0][0])*(r[2][1]-r[0][1])-(r[1][1]-r[0][1])*(r[2][0]-r[0][0]);
   const nx=(r[1][1]-r[0][1])*(r[2][2]-r[0][2])-(r[1][2]-r[0][2])*(r[2][1]-r[0][1]);
   const ny=(r[1][2]-r[0][2])*(r[2][0]-r[0][0])-(r[1][0]-r[0][0])*(r[2][2]-r[0][2]);
   const nlen=Math.hypot(nx,ny,nz)||1;
   let c=L.c,a=1;   // layer alphas (L.a) forced fully opaque for detail work
   if(steep && Math.abs(wny)/wl<=STEEP_NY && (tri[0][1]+tri[1][1]+tri[2][1])/3>WATER){ c=[255,45,180]; a=1; }
   all.push({k:'t',r,c,a,w:tri0,depth:(r[0][2]+r[1][2]+r[2][2])/3,sh:0.4+0.6*Math.abs(nz/nlen)});
 }}}
 if(on('t_points')) for(const p of D.points){if(CLIP&&!inClip(p[0],p[2]))continue;const r=rot(p);all.push({k:'p',r,depth:r[2]});}
 if(GEO&&GEOON())gAddTris(all);   // georama parts render THROUGH the z-buffer with the scene
 all.sort((p,q)=>p.depth-q.depth);
 const rfill=on('r_fill'), rwire=on('r_wire'), rcull=on('r_cull'), rwind=on('r_wind'), rlabels=on('r_labels');
 PICK=[];
 // Correct per-pixel occlusion via a software z-buffer (handles interpenetrating models the painter's-order
 // fill can't). Full-res when settled; HALF-res while dragging (FASTDRAW) then scaled up, to keep it smooth.
 if(rfill) zfill(all,f,rcull,rwind, FASTDRAW?0.5:1);
 for(const o of all){
  if(o.k==='p'){cx.fillStyle='#ff1493';cx.fillRect(W/2+o.r[0]*f-1.4,H/2-o.r[1]*f-1.4,2.8,2.8);continue;}
  const pts=o.r.map(p=>[W/2+p[0]*f,H/2-p[1]*f]);
  const area=(pts[1][0]-pts[0][0])*(pts[2][1]-pts[0][1])-(pts[1][1]-pts[0][1])*(pts[2][0]-pts[0][0]);
  if(rcull && area>=0) continue;
  if(!o.g) PICK.push({pts,zs:[o.r[0][2],o.r[1][2],o.r[2][2]],w:o.w});   // georama parts (g) are not collision-pickable
  if(rwire){ cx.beginPath();cx.moveTo(pts[0][0],pts[0][1]);cx.lineTo(pts[1][0],pts[1][1]);cx.lineTo(pts[2][0],pts[2][1]);cx.closePath();
   cx.strokeStyle='rgba('+o.c[0]+','+o.c[1]+','+o.c[2]+',0.9)';cx.lineWidth=0.6;cx.stroke(); }
 }
 drawSelected(f);
 if(rlabels) drawNodeLabels(f);
 if(on('t_points')) drawLabels();
 if(GEO&&GEOON())gOverlay(f);
 drawClipRect(f);
 drawFishRect(f);
 drawCompass();
 drawBoxSel();
}
function drawClipRect(f){
 const R=RSEL?[Math.min(RSEL.x0,RSEL.x1),Math.max(RSEL.x0,RSEL.x1),Math.min(RSEL.z0,RSEL.z1),Math.max(RSEL.z0,RSEL.z1)]:CLIP;
 if(!R)return;
 const P=p=>{const r=rot(p);return [W/2+r[0]*f,H/2-r[1]*f];};
 const c=[P([R[0],WATER,R[2]]),P([R[1],WATER,R[2]]),P([R[1],WATER,R[3]]),P([R[0],WATER,R[3]])];
 cx.save();cx.setLineDash([6,4]);cx.strokeStyle=RSEL?'#6ee7b7':'rgba(110,231,183,0.55)';cx.lineWidth=1.5;
 cx.beginPath();cx.moveTo(c[0][0],c[0][1]);for(let i=1;i<4;i++)cx.lineTo(c[i][0],c[i][1]);cx.closePath();cx.stroke();cx.restore();
}
function drawFishRect(f){
 const R=FSEL?[Math.min(FSEL.x0,FSEL.x1),Math.max(FSEL.x0,FSEL.x1),Math.min(FSEL.z0,FSEL.z1),Math.max(FSEL.z0,FSEL.z1)]:FRECT;
 if(!R)return;
 const P=p=>{const r=rot(p);return [W/2+r[0]*f,H/2-r[1]*f];};
 const c=[P([R[0],WATER,R[2]]),P([R[1],WATER,R[2]]),P([R[1],WATER,R[3]]),P([R[0],WATER,R[3]])];
 cx.save();cx.setLineDash([8,4]);cx.strokeStyle=FSEL?'#fdba5b':'rgba(253,186,91,0.8)';cx.lineWidth=2;
 cx.beginPath();cx.moveTo(c[0][0],c[0][1]);for(let i=1;i<4;i++)cx.lineTo(c[i][0],c[i][1]);cx.closePath();cx.stroke();
 cx.fillStyle='rgba(253,186,91,0.10)';cx.fill();cx.restore();
}
// ---- software z-buffer fill (correct occlusion for interpenetrating models) ----
// Two passes: opaque (writes depth) then transparent (depth-tested, alpha-blended, back-to-front). `all`
// is pre-sorted ascending by view depth (far first), which is the order the painter fill used, so nearer =
// LARGER depth -> keep the larger-depth pixel. Convention verified against the existing painter output.
let ZIMG=null, ZBUF=null, ZCAN=null, ZCTX=null, ZW=0, ZH=0;
function zfill(all,f,rcull,rwind,scale){
 const sw=Math.max(1,Math.round(W*scale)), sh=Math.max(1,Math.round(H*scale));
 if(ZW!==sw||ZH!==sh){ ZW=sw;ZH=sh; ZIMG=cx.createImageData(sw,sh); ZBUF=new Float32Array(sw*sh);
   if(!ZCAN){ZCAN=document.createElement('canvas');ZCTX=ZCAN.getContext('2d');} ZCAN.width=sw;ZCAN.height=sh; }
 const d=ZIMG.data, zb=ZBUF;
 for(let i=0;i<d.length;i+=4){ d[i]=13;d[i+1]=17;d[i+2]=23;d[i+3]=255; }
 zb.fill(-Infinity);
 const op=[],tr=[];
 for(const o of all){ if(o.k!=='t') continue; (o.a>=0.95?op:tr).push(o); }
 for(const o of op) raster(o,f,d,zb,rcull,rwind,true,sw,sh,scale);   // opaque: write depth
 for(const o of tr) raster(o,f,d,zb,rcull,rwind,false,sw,sh,scale);  // transparent: test only, blend
 if(scale===1){ cx.putImageData(ZIMG,0,0); }
 else { ZCTX.putImageData(ZIMG,0,0); cx.imageSmoothingEnabled=true; cx.drawImage(ZCAN,0,0,sw,sh,0,0,W,H); }
}
function raster(o,f,d,zb,rcull,rwind,wd,sw,sh,scale){
 const ax=(W/2+o.r[0][0]*f)*scale, ay=(H/2-o.r[0][1]*f)*scale, bx=(W/2+o.r[1][0]*f)*scale, by=(H/2-o.r[1][1]*f)*scale, gx=(W/2+o.r[2][0]*f)*scale, gy=(H/2-o.r[2][1]*f)*scale;
 const area=(bx-ax)*(gy-ay)-(by-ay)*(gx-ax);
 if(area===0 || (rcull && area>=0)) return;
 const z0=o.r[0][2],z1=o.r[1][2],z2=o.r[2][2], c=o.c, a=o.a;
 // winding overlay: a tri whose BACKFACE is toward the viewer (screen area > 0 under this projection)
 // fills neon yellow — face-orientation check, like Blender's overlay. Cull off to see both sides.
 let cr=Math.min(255,c[0]*o.sh)|0, cg=Math.min(255,c[1]*o.sh)|0, cb=Math.min(255,c[2]*o.sh)|0;
 if(rwind && area>0){ cr=255; cg=238; cb=0; }
 let minx=Math.max(0,Math.floor(Math.min(ax,bx,gx))), maxx=Math.min(sw-1,Math.ceil(Math.max(ax,bx,gx)));
 let miny=Math.max(0,Math.floor(Math.min(ay,by,gy))), maxy=Math.min(sh-1,Math.ceil(Math.max(ay,by,gy)));
 const inv=1/area;
 for(let y=miny;y<=maxy;y++){
  for(let x=minx;x<=maxx;x++){
   const px=x+0.5, py=y+0.5;
   const eBC=(gx-bx)*(py-by)-(gy-by)*(px-bx);
   const eCA=(ax-gx)*(py-gy)-(ay-gy)*(px-gx);
   const eAB=(bx-ax)*(py-ay)-(by-ay)*(px-ax);
   if((eBC<0||eCA<0||eAB<0)&&(eBC>0||eCA>0||eAB>0)) continue;   // outside (edges not all same sign)
   const z=(eBC*z0+eCA*z1+eAB*z2)*inv, idx=y*sw+x;
   if(z<=zb[idx]) continue;
   if(wd) zb[idx]=z;
   const o4=idx*4;
   if(a>=0.95){ d[o4]=cr; d[o4+1]=cg; d[o4+2]=cb; }
   else { d[o4]=cr*a+d[o4]*(1-a); d[o4+1]=cg*a+d[o4+1]*(1-a); d[o4+2]=cb*a+d[o4+2]*(1-a); }
  }
 }
}
let FASTDRAW=false;   // true while dragging -> use the cheap painter fill; false -> z-buffer
let PICK=[], SELECTED=[], SELUNDO=[], BSEL=null;
function triKey(t){ return t.map(p=>p.map(v=>Math.round(v*10)/10).join(',')).join('|'); }
function pushUndo(){ SELUNDO.push(SELECTED.slice()); if(SELUNDO.length>80) SELUNDO.shift(); }
function undoSel(){ if(SELUNDO.length){ SELECTED=SELUNDO.pop(); updateSel(); draw(); } }
function drawSelected(f){
 for(const t of SELECTED){
  const p=t.map(v=>{const r=rot(v);return [W/2+r[0]*f,H/2-r[1]*f];});
  cx.beginPath();cx.moveTo(p[0][0],p[0][1]);cx.lineTo(p[1][0],p[1][1]);cx.lineTo(p[2][0],p[2][1]);cx.closePath();
  cx.fillStyle='rgba(255,20,20,0.85)';cx.fill();
  cx.strokeStyle='#fff';cx.lineWidth=1.2;cx.stroke();
 }
}
function ptInTri(px,py,a,b,c){
 const s=(ax,ay,bx,by)=>(px-bx)*(ay-by)-(ax-bx)*(py-by);
 const d1=s(a[0],a[1],b[0],b[1]),d2=s(b[0],b[1],c[0],c[1]),d3=s(c[0],c[1],a[0],a[1]);
 return !(((d1<0)||(d2<0)||(d3<0))&&((d1>0)||(d2>0)||(d3>0)));
}
// depth of a tri AT the click point via barycentric interp of its screen verts (nearer = LARGER depth), so a big
// angled tri no longer wins on centroid depth — the surface actually under the cursor does.
function depthAt(o,px,py){
 const a=o.pts[0],b=o.pts[1],c=o.pts[2];
 const den=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]);
 if(Math.abs(den)<1e-6) return (o.zs[0]+o.zs[1]+o.zs[2])/3;
 const wA=((b[0]-px)*(c[1]-py)-(b[1]-py)*(c[0]-px))/den;
 const wB=((c[0]-px)*(a[1]-py)-(c[1]-py)*(a[0]-px))/den;
 return wA*o.zs[0]+wB*o.zs[1]+(1-wA-wB)*o.zs[2];
}
function pickAt(mx,my,add){
 let best=null,bestz=-Infinity;
 for(const o of PICK){ if(ptInTri(mx,my,o.pts[0],o.pts[1],o.pts[2])){ const z=depthAt(o,mx,my); if(z>bestz){bestz=z;best=o;} } }
 if(!best){ if(!add){ pushUndo(); SELECTED=[]; updateSel(); draw(); } return; }
 const k=triKey(best.w);
 pushUndo();
 if(add){ const i=SELECTED.findIndex(t=>triKey(t)===k); if(i>=0) SELECTED.splice(i,1); else SELECTED.push(best.w); }
 else { SELECTED=[best.w]; }
 updateSel(); draw();
}
// shift+drag rubber-band: add every FRONT-FACING tri whose screen centroid falls in the box to the selection.
function boxSelect(b){
 const x0=Math.min(b.x0,b.x1),x1=Math.max(b.x0,b.x1),y0=Math.min(b.y0,b.y1),y1=Math.max(b.y0,b.y1);
 pushUndo();
 const seen=new Set(SELECTED.map(triKey));
 for(const o of PICK){
  const ar=(o.pts[1][0]-o.pts[0][0])*(o.pts[2][1]-o.pts[0][1])-(o.pts[1][1]-o.pts[0][1])*(o.pts[2][0]-o.pts[0][0]);
  if(ar>=0) continue;                              // front-facing only (skip backfaces)
  const cxp=(o.pts[0][0]+o.pts[1][0]+o.pts[2][0])/3, cyp=(o.pts[0][1]+o.pts[1][1]+o.pts[2][1])/3;
  if(cxp<x0||cxp>x1||cyp<y0||cyp>y1) continue;
  const k=triKey(o.w); if(seen.has(k)) continue; seen.add(k); SELECTED.push(o.w);
 }
 updateSel();
}
function drawBoxSel(){ if(!BSEL) return;
 const x=Math.min(BSEL.x0,BSEL.x1),y=Math.min(BSEL.y0,BSEL.y1),w=Math.abs(BSEL.x1-BSEL.x0),h=Math.abs(BSEL.y1-BSEL.y0);
 cx.save();cx.strokeStyle='#6ee7b7';cx.setLineDash([5,3]);cx.lineWidth=1.5;cx.strokeRect(x,y,w,h);
 cx.fillStyle='rgba(110,231,183,0.12)';cx.fillRect(x,y,w,h);cx.restore();
}
function updateSel(){
 const el=document.getElementById('sellist'), n=document.getElementById('selcount');
 if(n) n.textContent=SELECTED.length;
 if(el) el.value=SELECTED.map(t=>t.map(p=>p.map(v=>Math.round(v*100)/100).join(',')).join(', ')).join(String.fromCharCode(10));
}
function drawNodeLabels(f){
 cx.save();cx.font='bold 11px monospace';cx.textAlign='left';cx.textBaseline='middle';
 for(const nl of D.nodelabels){
  const [cen,name,bb,lay]=nl;
  if(!on('t_'+lay)) continue;
  const C=[[bb[0],bb[1],bb[2]],[bb[3],bb[1],bb[2]],[bb[3],bb[1],bb[5]],[bb[0],bb[1],bb[5]],
           [bb[0],bb[4],bb[2]],[bb[3],bb[4],bb[2]],[bb[3],bb[4],bb[5]],[bb[0],bb[4],bb[5]]].map(p=>{const r=rot(p);return [W/2+r[0]*f,H/2-r[1]*f];});
  const E=[[0,1],[1,2],[2,3],[3,0],[4,5],[5,6],[6,7],[7,4],[0,4],[1,5],[2,6],[3,7]];
  cx.strokeStyle='rgba(255,235,120,0.9)';cx.lineWidth=1;cx.beginPath();
  for(const [a,b] of E){ cx.moveTo(C[a][0],C[a][1]);cx.lineTo(C[b][0],C[b][1]); }
  cx.stroke();
  const r=rot(cen), x=W/2+r[0]*f, y=H/2-r[1]*f, w=cx.measureText(name).width;
  cx.fillStyle='rgba(0,0,0,.8)';cx.fillRect(x+3,y-7,w+6,14);
  cx.fillStyle='#ffeb78';cx.fillText(name,x+6,y);
 }
 cx.restore();
}
function drawLabels(){
 const f=Math.min(W,H)*0.5*zoom/300;
 cx.save();cx.font='bold 12px monospace';cx.textAlign='left';cx.textBaseline='middle';
 for(const lb of D.plabels){const r=rot(lb[0]);const x=W/2+r[0]*f,y=H/2-r[1]*f;
  cx.fillStyle='#ff1493';cx.beginPath();cx.arc(x,y,3,0,7);cx.fill();
  const w=cx.measureText(lb[1]).width;cx.fillStyle='rgba(0,0,0,.75)';cx.fillRect(x+4,y-7,w+5,14);
  cx.fillStyle='#ff9fd6';cx.fillText(lb[1],x+6,y);}
 cx.restore();
}
function drawCompass(){
 const cxp=W-80,cyp=58,CR=34;
 cx.save();cx.font='bold 13px monospace';cx.textAlign='center';cx.textBaseline='middle';cx.lineWidth=1;
 cx.fillStyle='rgba(13,17,23,.7)';cx.beginPath();cx.arc(cxp,cyp,CR+13,0,7);cx.fill();
 const dirs=[['N',[0,0,-1],'#f66'],['E',[1,0,0],'#9cf'],['S',[0,0,1],'#999'],['W',[-1,0,0],'#9cf']];
 for(const d of dirs){
  const r=rot0(d[1]);let sx=r[0],sy=-r[1];const L2=Math.hypot(sx,sy)||1;sx/=L2;sy/=L2;
  cx.strokeStyle=d[2];cx.lineWidth=d[0]=='N'?2.5:1.3;
  cx.beginPath();cx.moveTo(cxp,cyp);cx.lineTo(cxp+sx*CR,cyp+sy*CR);cx.stroke();
  cx.fillStyle=d[2];cx.fillText(d[0],cxp+sx*(CR+9),cyp+sy*(CR+9));
 }
 cx.restore();
}
GEORAMA_JS
draw();
let drag=false,px,py,downX,downY,downShift,moved=false;
let SPACE=false,panning=false;
addEventListener('keydown',e=>{if(e.code==='Space'&&!/INPUT|TEXTAREA|BUTTON/.test(e.target.tagName)){SPACE=true;e.preventDefault();if(!drag)cv.style.cursor='move';}});
addEventListener('keyup',e=>{if(e.code==='Space'){SPACE=false;if(!panning)cv.style.cursor='grab';}});
addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.code==='KeyZ'&&!/INPUT|TEXTAREA/.test(e.target.tagName)){e.preventDefault();undoSel();}});
addEventListener('keydown',e=>{if(e.code==='KeyF'&&!/INPUT|TEXTAREA/.test(e.target.tagName)){FHELD=true;if(!drag)cv.style.cursor='crosshair';}});
addEventListener('keyup',e=>{if(e.code==='KeyF'){FHELD=false;if(!FSEL)cv.style.cursor='grab';}});
// screen -> world (x,z) on the y=WATER plane (same inversion as the coordinate readout); null when too flat
function groundPt(mx,my){const f=Math.min(W,H)*0.5*zoom/300,rx=(mx-W/2)/f,ry=-(my-H/2)/f;
 const cyw=Math.cos(yaw),syw=Math.sin(yaw),sp=Math.sin(pitch);
 if(Math.abs(sp)<0.08)return null;
 const z1=((WATER-CTR[1])*Math.cos(pitch)-ry)/sp;
 return [rx*cyw+z1*syw+CTR[0],-rx*syw+z1*cyw+CTR[2]];}
cv.addEventListener('pointerdown',e=>{
 const rr=cv.getBoundingClientRect();
 if(FHELD){const g=groundPt(e.clientX-rr.left,e.clientY-rr.top);
   if(g){FSEL={x0:g[0],z0:g[1],x1:g[0],z1:g[1]};FASTDRAW=true;draw();}return;}   // F+drag = fishing rect
 if(e.metaKey||e.ctrlKey){moved=true;const g=groundPt(e.clientX-rr.left,e.clientY-rr.top);
   if(g){RSEL={x0:g[0],z0:g[1],x1:g[0],z1:g[1]};FASTDRAW=true;draw();}return;}   // start clip-rect drag
 if(e.shiftKey){const mx=e.clientX-rr.left,my=e.clientY-rr.top;   // shift+drag = rubber-band select
   BSEL={x0:mx,y0:my,x1:mx,y1:my};downX=e.clientX;downY=e.clientY;moved=false;FASTDRAW=true;return;}
 if(SPACE){moved=true;panning=true;FASTDRAW=true;px=e.clientX;py=e.clientY;cv.style.cursor='move';return;}
 if(GEO&&GEOON()&&!gHeld){const gm=gPickPart(e.clientX-rr.left,e.clientY-rr.top);
   if(gm>=0){gMove=gm;gSel=gm;FASTDRAW=true;draw();return;}}   // grab a placed part to move it
 drag=true;FASTDRAW=true;px=e.clientX;py=e.clientY;downX=e.clientX;downY=e.clientY;downShift=e.shiftKey;moved=false;cv.style.cursor='grabbing';});
addEventListener('pointerup',e=>{
 if(BSEL){ const r=cv.getBoundingClientRect();
   if(moved) boxSelect(BSEL); else pickAt(e.clientX-r.left,e.clientY-r.top,true);   // no drag = shift+click add/remove
   BSEL=null;FASTDRAW=false;draw();return;}
 if(FSEL){const w=Math.abs(FSEL.x1-FSEL.x0),h=Math.abs(FSEL.z1-FSEL.z0);
  if(w>2&&h>2){FRECT=[Math.min(FSEL.x0,FSEL.x1),Math.max(FSEL.x0,FSEL.x1),Math.min(FSEL.z0,FSEL.z1),Math.max(FSEL.z0,FSEL.z1)];
   const o=document.getElementById('fishout');
   if(o){o.value='rect (x1,z1,x2,z2) = '+Math.round(FRECT[0])+', '+Math.round(FRECT[2])+', '+Math.round(FRECT[1])+', '+Math.round(FRECT[3]);}}
  FSEL=null;FASTDRAW=false;cv.style.cursor=FHELD?'crosshair':'grab';draw();return;}
 if(RSEL){const w=Math.abs(RSEL.x1-RSEL.x0),h=Math.abs(RSEL.z1-RSEL.z0);
  CLIP=(w>2&&h>2)?[Math.min(RSEL.x0,RSEL.x1),Math.max(RSEL.x0,RSEL.x1),Math.min(RSEL.z0,RSEL.z1),Math.max(RSEL.z0,RSEL.z1)]:CLIP;
  RSEL=null;FASTDRAW=false;draw();return;}
 if(panning){panning=false;FASTDRAW=false;cv.style.cursor=SPACE?'move':'grab';draw();return;}
 if(GEO&&gMove>=0){gMove=-1;FASTDRAW=false;draw();return;}
 if(GEO&&GEOON()&&gHeld){const rr=cv.getBoundingClientRect();gPlace(e.clientX-rr.left,e.clientY-rr.top);return;}
 const wasDrag=drag; drag=false;FASTDRAW=false;
 if(!wasDrag) return;   // pointerup NOT from a canvas press (e.g. a checkbox click) — leave the selection alone
 cv.style.cursor='grab';
 if(!moved){const r=cv.getBoundingClientRect();pickAt(e.clientX-r.left,e.clientY-r.top,downShift);}
 else draw();});   // re-render the settled view with the z-buffer after a drag
addEventListener('pointermove',e=>{
 if(BSEL){const rr=cv.getBoundingClientRect();BSEL.x1=e.clientX-rr.left;BSEL.y1=e.clientY-rr.top;
  if(Math.abs(e.clientX-downX)+Math.abs(e.clientY-downY)>4)moved=true;draw();return;}
 if(FSEL){const rr=cv.getBoundingClientRect(),g=groundPt(e.clientX-rr.left,e.clientY-rr.top);
  if(g){FSEL.x1=g[0];FSEL.z1=g[1];draw();}return;}
 if(RSEL){const rr=cv.getBoundingClientRect(),g=groundPt(e.clientX-rr.left,e.clientY-rr.top);
  if(g){RSEL.x1=g[0];RSEL.z1=g[1];draw();}return;}
 if(panning){const f=Math.min(W,H)*0.5*zoom/300,dx=(e.clientX-px)/f,dy=(e.clientY-py)/f;
  const cy=Math.cos(yaw),sy=Math.sin(yaw),sp=Math.sin(pitch),cp=Math.cos(pitch);
  // world right=(cy,0,-sy), world view-up=(-sp*sy,cp,-sp*cy): content follows the mouse
  CTR[0]+=-cy*dx-sp*sy*dy; CTR[1]+=cp*dy; CTR[2]+=sy*dx-sp*cy*dy;
  px=e.clientX;py=e.clientY;draw();return;}
 if(GEO&&gMove>=0){const rr=cv.getBoundingClientRect();gMoveTo(e.clientX-rr.left,e.clientY-rr.top);draw();return;}
 if(!drag)return;
 if(Math.abs(e.clientX-downX)+Math.abs(e.clientY-downY)>4)moved=true;
 yaw+=(e.clientX-px)*.01;pitch+=(e.clientY-py)*.01;px=e.clientX;py=e.clientY;draw();});
document.getElementById('clipreset').onclick=()=>{CLIP=null;RSEL=null;draw();};
document.getElementById('selclear').onclick=()=>{pushUndo();SELECTED=[];updateSel();draw();};
document.getElementById('selundo').onclick=undoSel;
let zoomTimer=null;
cv.addEventListener('wheel',e=>{e.preventDefault();zoom*=e.deltaY<0?1.1:0.9;FASTDRAW=true;draw();
 if(zoomTimer)clearTimeout(zoomTimer);
 zoomTimer=setTimeout(()=>{FASTDRAW=false;draw();},150);   // snap back to full-res once the wheel settles
},{passive:false});
const coordEl=document.getElementById('coord');
cv.addEventListener('pointermove',e=>{
 const r=cv.getBoundingClientRect(),mx=e.clientX-r.left,my=e.clientY-r.top;
 if(GEO&&GEOON()){const pk=gPick(mx,my);gCur=pk;if(gHeld&&!drag)draw();}
 const f=Math.min(W,H)*0.5*zoom/300,rx=(mx-W/2)/f,ry=-(my-H/2)/f;
 const cyw=Math.cos(yaw),syw=Math.sin(yaw),sp=Math.sin(pitch);
 if(Math.abs(sp)<0.08){coordEl.textContent='tilt the view down to read coords';return;}
 const z1=((WATER-CTR[1])*Math.cos(pitch)-ry)/sp,
       wx=Math.round(rx*cyw+z1*syw+CTR[0]), wz=Math.round(-rx*syw+z1*cyw+CTR[2]);
 coordEl.textContent='x = '+wx+'    z = '+wz+'    (COORDNOTE)';
});
// ---- persist toggle state (checkboxes + folder open/closed) across refreshes ----
const LSKEY=STORAGEKEY;
function saveState(){ try{ const s={cb:{},fold:{}};
  for(const cb of document.querySelectorAll('input[type=checkbox]')) if(cb.id) s.cb[cb.id]=cb.checked;
  for(const d of document.querySelectorAll('details.fold')) if(d.id) s.fold[d.id]=d.open;
  localStorage.setItem(LSKEY,JSON.stringify(s)); }catch(e){} }
function loadState(){ try{ const s=JSON.parse(localStorage.getItem(LSKEY)||'null'); if(!s)return;
  for(const id in (s.cb||{})){const e=document.getElementById(id); if(e)e.checked=s.cb[id];}
  for(const id in (s.fold||{})){const e=document.getElementById(id); if(e&&'open'in e)e.open=s.fold[id];}
  for(const d of document.querySelectorAll('details.fold')){const m=d.querySelector('summary input'); if(m)d.classList.toggle('off',!m.checked);}
 }catch(e){} }
loadState();
for(const cb of document.querySelectorAll('input')) cb.addEventListener('change',()=>{saveState();draw();updateTotal();});
for(const d of document.querySelectorAll('details.fold')) d.addEventListener('toggle',saveState);
draw(); updateTotal();
}catch(e){document.getElementById('err').textContent='ERR: '+e.message;}</script>'''

    return (html.replace('TITLE', title).replace('CHECKS', checks).replace('PUSHES', pushes)
            .replace('JSON_DATA', js).replace('CNT_DATA', cnt_js).replace('GRP_DATA', grp_js)
            .replace('GEORAMA_JS', georama_js).replace('STORAGEKEY', json.dumps('sv:' + title))
            .replace('COORDY', repr(float(_coord_y(coord_note)))).replace('COORDNOTE', coord_note))


def _coord_y(note):
    # pull a "y=<n>" out of the coord note if present, else 0 (the plane the cursor readout intersects)
    import re
    m = re.search(r'y\s*=\s*(-?\d+(?:\.\d+)?)', note)
    return float(m.group(1)) if m else 0.0
