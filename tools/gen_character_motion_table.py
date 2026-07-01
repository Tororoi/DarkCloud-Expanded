import struct, re
HED="/Users/thomascantwell/ROMs/dc_extracted/data.hed"; HD2="/Users/thomascantwell/ROMs/dc_extracted/data.hd2"; DAT="/Users/thomascantwell/ROMs/dc_extracted/data.dat"
OUT="docs/character-motion-table.md"
hed=open(HED,'rb').read(); hd2=open(HD2,'rb').read(); dat=open(DAT,'rb')
MODELS=[
 ("c01d","Toan","sword; has 攻撃(1)/(2) + full 溜め攻撃 (charge→whirlwind) chain — the feature's target character"),
 ("c04b","Xiao","ranged: 構え引き (draw/nock) + 撃ち (shoot) → slingshot"),
 ("c05a","Ruby","caster: 魔法1/魔法2 + 溜め魔法 (charged magic)"),
 ("c06b","Goro","melee: 攻撃 + ため攻撃 (charge) chain"),
 ("c10b","Ungaga","攻撃1/2/3 + 溜め (charge) — polearm"),
 ("c18a","Osmond","撃つ (shoot) → gun"),
 ("c07a","(unused/NPC)","only idle/run/walk present"),
]
TR=[("連続攻撃","combo attack"),("溜め攻撃","charge attack"),("ため攻撃","charge attack"),("溜め魔法","charged magic"),
 ("攻撃態勢","attack stance"),("攻撃構え","attack ready"),("攻撃待機","attack idle"),("攻撃","attack"),
 ("通常ダメージ","damage"),("ダメージ受け走り","run (hit)"),("ダメージ大","damage (big)"),("ダメージ","damage"),("ダメ−ジ","damage"),("ダメだし","(dmg-out)"),
 ("起き上がり","get up"),("置きあがり","get up"),
 ("ガードループ","guard (loop)"),("ガード入り","guard (enter)"),("ガード戻り","guard (return)"),
 ("ガード解除","guard (release)"),("ガード移動","guard move"),("ガード","guard"),
 ("溜めループ","charge (loop)"),("溜め移動","charge move"),("溜め","charge"),("ため","charge"),
 ("魔法","magic"),("撃ち","shoot"),("撃つ","shoot"),("構え引き","draw"),("構え","ready"),
 ("アイテム投げ","item throw"),("投げ停止","throw (stop)"),("投げ開始","throw (start)"),("投げる","throw"),("投げ","throw"),("飲む","drink"),("飲み","drink"),
 ("やられ","downed"),("死亡","death"),("死に","death"),("落下モーション","fall motion"),
 ("右後退","step right"),("左後退","step left"),("後退","step back"),("前進","step fwd"),("前移動","move fwd"),
 ("右移動","move right"),("左移動","move left"),("後ろ移動","move back"),("バックステップ","back step"),
 ("右ステップ","right step"),("左ステップ","left step"),("移動","move"),
 ("立ち＞ガード","idle→guard"),("ガード＞立ち","guard→idle"),("立ち＞溜め","idle→charge"),("溜め＞立ち","charge→idle"),
 ("構え＞ガード","ready→guard"),("ガード＞構え","guard→ready"),
 ("（右）","(right)"),("（左）","(left)"),("（前）","(fwd)"),("（後ろ）","(back)"),("（後）","(back)"),
 ("（ル−プ）","(loop)"),("（ループ）","(loop)"),("ループ","loop"),("ル−プ","loop"),("技","skill"),
 ("立ち","idle"),("走り","run"),("歩き","walk"),
]
def meaning(nm):
    if not nm: return "—"
    out=nm
    for jp,en in TR: out=out.replace(jp,en)
    return out.strip()
def entry(sub):
    for i in range(len(hed)//80):
        n=hed[i*80:i*80+80].split(b'\x00')[0].decode('latin1','replace')
        if n.endswith(sub):
            o,s,_=struct.unpack_from('<III',hd2,16+i*32); return o,s
    return None
def motions(code):
    e=entry('mainchara\\'+code+'.chr')
    if not e: return None
    off,size=e; dat.seek(off); d=dat.read(size); out=[]
    for idx,m in enumerate(re.finditer(rb'KEY[ \t]+(\d+),[ \t]*(\d+),[ \t]*([\d.]+),?[ \t]*//([^\r\n]*)', d)):
        s,en,sp,rest=m.groups()
        t=rest.decode('shift_jis','replace').split()
        if t and t[-1].lstrip('-').isdigit(): t=t[:-1]
        nm=re.sub(r'^\d+','',' '.join(t).strip()).strip()
        spd=sp.decode().rstrip('0').rstrip('.') or '0'
        out.append((idx,int(s),int(en),spd,nm))
    return off,out
L=["# Character motion table\n",
"Per-character animation / motion lists for the **playable characters** (in-dungeon body models",
"`dun\\mainchara\\cNN*.chr`), decoded from each `.chr` `info.cfg` KEY block in `data.dat` — same format as",
"`enemy-motion-table.md`. Each `KEY <start>,<end>,<speed>, //<name(Shift-JIS)>` is one motion; **Idx** is the",
"motion-table index. Sections are labelled by MODEL CODE (definitive); the character name is INFERRED from the",
"weapon-specific motions (noted per header) — treat non-Toan names as best-effort.\n",
"**Speed** is the third KEY value — the motion's default per-frame playback rate (the baked \"KEY speed\"). It is",
"exactly the value the animation-speed knob **`CharacterMotion.MotionSpeedOverride` (`0x21EA2980`)** REPLACES",
"when set positive (`Step__CCharacter`; −1.0 = use this KEY speed), so this column is the per-motion baseline",
"for the weapon-speed-scaled-animation feature (`CharacterAddresses.cs`, `player-motion-speed` memory). Rows",
"with no name (frames like `0-0`/`1-1`) are unused/placeholder slots kept for index alignment.\n"]
for code,label,blurb in MODELS:
    r=motions(code)
    if not r: L.append("### %s `%s`\n\n*NOT FOUND*\n"%(label,code)); continue
    off,ms=r
    L.append("### %s `%s`\n"%(label,code))
    L.append("*Motions: %s.chr @ data.dat 0x%X — %s*\n"%(code,off,blurb))
    L.append("| Idx | Frames | Speed | Name (JP) | Meaning |")
    L.append("|---|---|---|---|---|")
    for idx,s,en,spd,nm in ms:
        L.append("| %d | %d–%d | %s | %s | %s |"%(idx,s,en,spd,nm if nm else "—",meaning(nm)))
    L.append("")
open(OUT,"w").write("\n".join(L)+"\n")
print("wrote",OUT)
