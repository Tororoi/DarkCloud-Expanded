#!/usr/bin/env python3
"""Shared self-contained HTML/JS scene viewer used by the per-town fishing viewers
(brownboo_viewer.py keeps its own inline copy; yellowdrops_viewer.py and any future town use this).

The renderer is a hand-rolled canvas projection (no external libs) with: per-layer show/hide checkboxes,
fill / wireframe / backface-cull toggles, node labels + bounding-box borders, a cursor->world (x,z)
coordinate readout on the water plane, a compass, and click / shift+click polygon selection that emits a
copyable list of triangles (for adding as fishing collision).

build_html(title, layers, node_labels=..., points=..., point_labels=..., coord_note=...) -> html string.
  layers: ordered list of dicts, each:
      {'key','label','tris':[[[x,y,z]*3]...],'color':[r,g,b],'alpha':float,'border':'#hex','on':bool}
  node_labels: [[centroid[x,y,z], name, bbox[minx,miny,minz,maxx,maxy,maxz], layer_key], ...]
  points:       optional [[x,y,z], ...] point-cloud overlay (toggle "points")
  point_labels: optional [[[x,y,z], text], ...] text markers drawn with the point overlay
  coord_note:   label suffix for the coordinate readout (e.g. "water y=0")
"""
import json


def build_html(title, layers, node_labels=None, points=None, point_labels=None,
               points_label="markers + coords", coord_note="water y=0"):
    node_labels = node_labels or []
    points = points or []
    point_labels = point_labels or []

    # D: all geometry, embedded directly in the self-contained HTML.
    D = {'nodelabels': node_labels, 'points': points, 'plabels': point_labels}
    for L in layers:
        D[L['key']] = L['tris']
    js = json.dumps(D, separators=(',', ':'))

    cnt = {f"t_{L['key']}": len(L['tris']) for L in layers}
    cnt_js = json.dumps(cnt, separators=(',', ':'))

    checks = "".join(
        f'<label><input type=checkbox id=t_{L["key"]} {"checked" if L.get("on") else ""}> '
        f'<span style="color:{L["border"]}">{L["label"]}</span> '
        f'<span style="color:#777">({len(L["tris"])})</span></label><br>'
        for L in layers)
    checks += (f'<label><input type=checkbox id=t_points checked> '
               f'<span style="color:#f4a">{points_label}</span> '
               f'<span style="color:#777">({len(points)} pts)</span></label><br>')
    checks += '<label><input type=checkbox id=t_steep> <span style="color:#ff2db4">steep &amp; above-water (highlight)</span></label><br>'
    checks += ('<div style="margin-top:5px;border-top:1px solid #444;padding-top:4px">'
               '<label><input type=checkbox id=r_fill checked> fill</label> '
               '<label><input type=checkbox id=r_wire> wireframe</label> '
               '<label><input type=checkbox id=r_cull> backface cull</label><br>'
               '<label><input type=checkbox id=r_labels> node labels + borders</label></div>')
    checks += '<div style="margin-top:5px;border-top:1px solid #444;padding-top:4px">selected: <b id="tot" style="color:#fff">0</b> polys</div>'

    pushes = "".join(
        f"if(on('t_{L['key']}')&&D.{L['key']}) L.push({{t:D.{L['key']},c:{L['color']},a:{L['alpha']}}});\n"
        for L in layers)

    html = '''<div style="margin:0;background:#0d1117;color:#ddd;font-family:monospace;overflow:hidden">
<canvas id="c" style="display:block;cursor:grab;touch-action:none"></canvas>
<div style="position:fixed;top:8px;left:8px;font-size:11px;line-height:1.5;background:rgba(13,17,23,.85);padding:8px 10px;border-radius:6px;user-select:none;max-height:96vh;overflow:auto">
<b>TITLE</b><br><span style="color:#888">drag=rotate scroll=zoom &middot; compass: N=-Z E=+X</span><br>
CHECKS<div id="err" style="color:#f66"></div></div></div>
<div id="coord" style="position:fixed;bottom:10px;left:10px;font-size:16px;font-weight:bold;background:rgba(13,17,23,.92);padding:7px 14px;border-radius:6px;color:#6ee7b7;user-select:none">move cursor over the surface for coordinates</div>
<div style="position:fixed;bottom:10px;right:10px;width:300px;font-size:11px;background:rgba(13,17,23,.94);padding:8px 10px;border-radius:6px">
<b>Selected polys: <span id="selcount" style="color:#f44">0</span></b>
<button id="selclear" style="float:right;font-size:10px">clear</button><br>
<span style="color:#888">click a poly to select (red) &middot; shift+click to add/remove</span>
<textarea id="sellist" readonly spellcheck="false" placeholder="clicked polys appear here: x0,y0,z0, x1,y1,z1, x2,y2,z2 (one triangle per line)" style="width:100%;height:130px;margin-top:5px;background:#0d1117;color:#6ee7b7;border:1px solid #333;border-radius:4px;font-family:monospace;font-size:10px;box-sizing:border-box"></textarea>
</div>
<script>try{
const D=JSON_DATA;
const CNT=CNT_DATA;
function updateTotal(){let s=0;for(const k in CNT){const e=document.getElementById(k);if(e&&e.checked)s+=CNT[k];}const t=document.getElementById('tot');if(t)t.textContent=s;}
const cv=document.getElementById('c'),cx=cv.getContext('2d');
let W,H;function resize(){W=cv.width=innerWidth||900;H=cv.height=innerHeight||700;}resize();addEventListener('resize',()=>{resize();draw();});
let yaw=0.6,pitch=0.55,zoom=1.5;   // +pitch = camera ABOVE the model, looking down
const WATER=COORDY, STEEP_NY=0.2;   // bobber lands on |normal.Y|>0.2; <=0.2 is too steep to collide
const on=id=>document.getElementById(id).checked;
function layers(){const L=[];
PUSHES
return L;}
function rot(p){let x=p[0],y=p[1],z=p[2];let cy=Math.cos(yaw),sy=Math.sin(yaw);let x1=x*cy-z*sy,z1=x*sy+z*cy;
 let cp=Math.cos(pitch),sp=Math.sin(pitch);let y1=y*cp-z1*sp,z2=y*sp+z1*cp;return [x1,y1,z2];}
function draw(){
 cx.fillStyle='#0d1117';cx.fillRect(0,0,W,H);
 const f=Math.min(W,H)*0.5*zoom/300, all=[];
 const steep=on('t_steep');
 for(const L of layers()){ if(!L.t) continue; for(const tri of L.t){
   const r=[rot(tri[0]),rot(tri[1]),rot(tri[2])];
   const nz=(r[1][0]-r[0][0])*(r[2][1]-r[0][1])-(r[1][1]-r[0][1])*(r[2][0]-r[0][0]);
   const nx=(r[1][1]-r[0][1])*(r[2][2]-r[0][2])-(r[1][2]-r[0][2])*(r[2][1]-r[0][1]);
   const ny=(r[1][2]-r[0][2])*(r[2][0]-r[0][0])-(r[1][0]-r[0][0])*(r[2][2]-r[0][2]);
   const nlen=Math.hypot(nx,ny,nz)||1;
   let c=L.c,a=L.a;
   if(steep){
     const wnx=(tri[1][1]-tri[0][1])*(tri[2][2]-tri[0][2])-(tri[1][2]-tri[0][2])*(tri[2][1]-tri[0][1]);
     const wny=(tri[1][2]-tri[0][2])*(tri[2][0]-tri[0][0])-(tri[1][0]-tri[0][0])*(tri[2][2]-tri[0][2]);
     const wnz=(tri[1][0]-tri[0][0])*(tri[2][1]-tri[0][1])-(tri[1][1]-tri[0][1])*(tri[2][0]-tri[0][0]);
     const wl=Math.hypot(wnx,wny,wnz)||1, cy=(tri[0][1]+tri[1][1]+tri[2][1])/3;
     if(Math.abs(wny)/wl<=STEEP_NY && cy>WATER){ c=[255,45,180]; a=1; }
   }
   all.push({k:'t',r,c,a,w:tri,depth:(r[0][2]+r[1][2]+r[2][2])/3,sh:0.4+0.6*Math.abs(nz/nlen)});
 }}
 if(on('t_points')) for(const p of D.points){const r=rot(p);all.push({k:'p',r,depth:r[2]});}
 all.sort((p,q)=>p.depth-q.depth);
 const rfill=on('r_fill'), rwire=on('r_wire'), rcull=on('r_cull'), rlabels=on('r_labels');
 PICK=[];
 // Correct per-pixel occlusion via a software z-buffer (handles interpenetrating models the painter's-order
 // fill can't). Full-res when settled; HALF-res while dragging (FASTDRAW) then scaled up, to keep it smooth.
 if(rfill) zfill(all,f,rcull, FASTDRAW?0.5:1);
 for(const o of all){
  if(o.k==='p'){cx.fillStyle='#ff1493';cx.fillRect(W/2+o.r[0]*f-1.4,H/2-o.r[1]*f-1.4,2.8,2.8);continue;}
  const pts=o.r.map(p=>[W/2+p[0]*f,H/2-p[1]*f]);
  const area=(pts[1][0]-pts[0][0])*(pts[2][1]-pts[0][1])-(pts[1][1]-pts[0][1])*(pts[2][0]-pts[0][0]);
  if(rcull && area>=0) continue;
  PICK.push({pts,depth:o.depth,w:o.w});
  if(rwire){ cx.beginPath();cx.moveTo(pts[0][0],pts[0][1]);cx.lineTo(pts[1][0],pts[1][1]);cx.lineTo(pts[2][0],pts[2][1]);cx.closePath();
   cx.strokeStyle='rgba('+o.c[0]+','+o.c[1]+','+o.c[2]+',0.9)';cx.lineWidth=0.6;cx.stroke(); }
 }
 drawSelected(f);
 if(rlabels) drawNodeLabels(f);
 if(on('t_points')) drawLabels();
 drawCompass();
}
// ---- software z-buffer fill (correct occlusion for interpenetrating models) ----
// Two passes: opaque (writes depth) then transparent (depth-tested, alpha-blended, back-to-front). `all`
// is pre-sorted ascending by view depth (far first), which is the order the painter fill used, so nearer =
// LARGER depth -> keep the larger-depth pixel. Convention verified against the existing painter output.
let ZIMG=null, ZBUF=null, ZCAN=null, ZCTX=null, ZW=0, ZH=0;
function zfill(all,f,rcull,scale){
 const sw=Math.max(1,Math.round(W*scale)), sh=Math.max(1,Math.round(H*scale));
 if(ZW!==sw||ZH!==sh){ ZW=sw;ZH=sh; ZIMG=cx.createImageData(sw,sh); ZBUF=new Float32Array(sw*sh);
   if(!ZCAN){ZCAN=document.createElement('canvas');ZCTX=ZCAN.getContext('2d');} ZCAN.width=sw;ZCAN.height=sh; }
 const d=ZIMG.data, zb=ZBUF;
 for(let i=0;i<d.length;i+=4){ d[i]=13;d[i+1]=17;d[i+2]=23;d[i+3]=255; }
 zb.fill(-Infinity);
 const op=[],tr=[];
 for(const o of all){ if(o.k!=='t') continue; (o.a>=0.95?op:tr).push(o); }
 for(const o of op) raster(o,f,d,zb,rcull,true,sw,sh,scale);   // opaque: write depth
 for(const o of tr) raster(o,f,d,zb,rcull,false,sw,sh,scale);  // transparent: test only, blend
 if(scale===1){ cx.putImageData(ZIMG,0,0); }
 else { ZCTX.putImageData(ZIMG,0,0); cx.imageSmoothingEnabled=true; cx.drawImage(ZCAN,0,0,sw,sh,0,0,W,H); }
}
function raster(o,f,d,zb,rcull,wd,sw,sh,scale){
 const ax=(W/2+o.r[0][0]*f)*scale, ay=(H/2-o.r[0][1]*f)*scale, bx=(W/2+o.r[1][0]*f)*scale, by=(H/2-o.r[1][1]*f)*scale, gx=(W/2+o.r[2][0]*f)*scale, gy=(H/2-o.r[2][1]*f)*scale;
 const area=(bx-ax)*(gy-ay)-(by-ay)*(gx-ax);
 if(area===0 || (rcull && area>=0)) return;
 const z0=o.r[0][2],z1=o.r[1][2],z2=o.r[2][2], c=o.c, a=o.a;
 const cr=Math.min(255,c[0]*o.sh)|0, cg=Math.min(255,c[1]*o.sh)|0, cb=Math.min(255,c[2]*o.sh)|0;
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
let PICK=[], SELECTED=[];
function triKey(t){ return t.map(p=>p.map(v=>Math.round(v*10)/10).join(',')).join('|'); }
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
function pickAt(mx,my,add){
 let best=null;
 for(const o of PICK){ if(ptInTri(mx,my,o.pts[0],o.pts[1],o.pts[2])){ if(!best||o.depth>best.depth) best=o; } }
 if(!best){ if(!add){SELECTED=[];updateSel();draw();} return; }
 const k=triKey(best.w);
 if(add){ const i=SELECTED.findIndex(t=>triKey(t)===k); if(i>=0) SELECTED.splice(i,1); else SELECTED.push(best.w); }
 else { SELECTED=[best.w]; }
 updateSel(); draw();
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
  const t=document.getElementById('t_'+lay); if(!t||!t.checked) continue;
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
  const r=rot(d[1]);let sx=r[0],sy=-r[1];const L2=Math.hypot(sx,sy)||1;sx/=L2;sy/=L2;
  cx.strokeStyle=d[2];cx.lineWidth=d[0]=='N'?2.5:1.3;
  cx.beginPath();cx.moveTo(cxp,cyp);cx.lineTo(cxp+sx*CR,cyp+sy*CR);cx.stroke();
  cx.fillStyle=d[2];cx.fillText(d[0],cxp+sx*(CR+9),cyp+sy*(CR+9));
 }
 cx.restore();
}
draw();
let drag=false,px,py,downX,downY,downShift,moved=false;
cv.addEventListener('pointerdown',e=>{drag=true;FASTDRAW=true;px=e.clientX;py=e.clientY;downX=e.clientX;downY=e.clientY;downShift=e.shiftKey;moved=false;cv.style.cursor='grabbing';});
addEventListener('pointerup',e=>{drag=false;FASTDRAW=false;cv.style.cursor='grab';
 if(!moved){const r=cv.getBoundingClientRect();pickAt(e.clientX-r.left,e.clientY-r.top,downShift);}
 else draw();});   // re-render the settled view with the z-buffer after a drag
addEventListener('pointermove',e=>{if(!drag)return;
 if(Math.abs(e.clientX-downX)+Math.abs(e.clientY-downY)>4)moved=true;
 yaw+=(e.clientX-px)*.01;pitch+=(e.clientY-py)*.01;px=e.clientX;py=e.clientY;draw();});
document.getElementById('selclear').onclick=()=>{SELECTED=[];updateSel();draw();};
let zoomTimer=null;
cv.addEventListener('wheel',e=>{e.preventDefault();zoom*=e.deltaY<0?1.1:0.9;FASTDRAW=true;draw();
 if(zoomTimer)clearTimeout(zoomTimer);
 zoomTimer=setTimeout(()=>{FASTDRAW=false;draw();},150);   // snap back to full-res once the wheel settles
},{passive:false});
const coordEl=document.getElementById('coord');
cv.addEventListener('pointermove',e=>{
 const r=cv.getBoundingClientRect(),mx=e.clientX-r.left,my=e.clientY-r.top;
 const f=Math.min(W,H)*0.5*zoom/300,rx=(mx-W/2)/f,ry=-(my-H/2)/f;
 const cyw=Math.cos(yaw),syw=Math.sin(yaw),sp=Math.sin(pitch);
 if(Math.abs(sp)<0.08){coordEl.textContent='tilt the view down to read coords';return;}
 const z1=-(ry-WATER*Math.cos(pitch))/sp, wx=Math.round(rx*cyw+z1*syw), wz=Math.round(-rx*syw+z1*cyw);
 coordEl.textContent='x = '+wx+'    z = '+wz+'    (COORDNOTE)';
});
for(const cb of document.querySelectorAll('input')) cb.addEventListener('change',()=>{draw();updateTotal();});
updateTotal();
}catch(e){document.getElementById('err').textContent='ERR: '+e.message;}</script>'''

    return (html.replace('TITLE', title).replace('CHECKS', checks).replace('PUSHES', pushes)
            .replace('JSON_DATA', js).replace('CNT_DATA', cnt_js)
            .replace('COORDY', repr(float(_coord_y(coord_note)))).replace('COORDNOTE', coord_note))


def _coord_y(note):
    # pull a "y=<n>" out of the coord note if present, else 0 (the plane the cursor readout intersects)
    import re
    m = re.search(r'y\s*=\s*(-?\d+(?:\.\d+)?)', note)
    return float(m.group(1)) if m else 0.0
