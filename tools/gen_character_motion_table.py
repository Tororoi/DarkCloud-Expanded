import os
import struct, re

# Extracted Dark Cloud disc dir; required — see .env.sample.
DC1_DATA_DIR = os.environ.get("DC1_DATA_DIR")
if not DC1_DATA_DIR: raise SystemExit("Set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")
HED=os.path.join(DC1_DATA_DIR, "data.hed"); HD2=os.path.join(DC1_DATA_DIR, "data.hd2"); DAT=os.path.join(DC1_DATA_DIR, "data.dat")
OUT="docs/character-motion-table.md"
hed=open(HED,'rb').read(); hd2=open(HD2,'rb').read(); dat=open(DAT,'rb')
TOAN_NOTE = ("the source KEY block has one line **commented out** (`// KEY 540, 546, 0.4 //26\u6295\u3052\u958b\u59cb`,\n"
 "throw-start). The loader skips it, so it is NOT a motion slot \u2014 the table below matches the RUNTIME\n"
 "ids (verified against `ToanKey_Play`: first combo swing = motion id 0x24 = **36** \u9023\u7d9a\u653b\u62381, end frame\n"
 "read from `DAT_01ea2064 + id*0x10 + 4`). Toan is the only playable character with a commented-out KEY.")

PCAT_NOTE = ("**the playable cat has only these four motions \u2014 there is no run and no brace.** Every OTHER cat model\n"
 "in the game (see the variant table below) carries a richer set, but their motion INDICES DO NOT MATCH this one:\n"
 "`c04pcat` orders them idle/walk/walk/sit, while all the others order them idle/sit/walk/brace/run. A motion id is\n"
 "only meaningful against the model that is actually loaded \u2014 id 1 is *walk* on the playable cat and *sit* on\n"
 "every other cat. Motion data is bound to the model (`MOTION 0, \"c04pcat.mot\"`), so ids are not portable.")

# (path, label, blurb, note). A bare code means dun\mainchara\<code>.chr.
MODELS=[
 ("c01d","Toan","sword; has \u653b\u6483(1)/(2) + full \u6e9c\u3081\u653b\u6483 (charge\u2192whirlwind) chain \u2014 the feature's target character",TOAN_NOTE),
 ("c04b","Xiao","ranged: \u69cb\u3048\u5f15\u304d (draw/nock) + \u6483\u3061 (shoot) \u2192 slingshot",None),
 ("gedit\\e01\\chara\\c04pcat.chr","Xiao (CAT FORM \u2014 playable)","\u30b7\u30e3\u30aa\u732b; the model the mod loads to play as cat Xiao in town (TownCharacter.cs)",PCAT_NOTE),
 ("c05a","Ruby","caster: \u9b54\u6cd51/\u9b54\u6cd52 + \u6e9c\u3081\u9b54\u6cd5 (charged magic)",None),
 ("c06b","Goro","melee: \u653b\u6483 + \u305f\u3081\u653b\u6483 (charge) chain",None),
 ("c10b","Ungaga","\u653b\u64831/2/3 + \u6e9c\u3081 (charge) \u2014 polearm",None),
 ("c18a","Osmond","\u6483\u3064 (shoot) \u2192 gun",None),
 ("c07a","(unused/NPC)","only idle/run/walk present",None),
]

# Every other cat model in the archive — documented so it is obvious what motions EXIST for the cat, even though
# the playable one cannot use them without swapping the model.
CAT_VARIANTS=[
 ("gedit\\e01\\chara\\c04cat.chr",     "town NPC cat"),
 ("gedit\\s86\\chara\\c04cat.chr",     "scene s86 \u2014 adds jump (\u8e0f\u307f\u5207\u308a / \u98db\u3073\uff2c / \u7740\u5730)"),
 ("gedit\\s87\\chara\\c04cat.chr",     "scene s87 \u2014 same set as s86"),
 ("gedit\\e01\\chara\\e04c04cat.chr",  "event e04 \u2014 adds float / head-tilt"),
 ("gedit\\s99\\chara\\e17c04cat.chr",  "event e17 \u2014 adds look-at-Toan / turn"),
 ("dun\\d01\\event\\e613c04cat.chr",   "dungeon event e613 \u2014 its OWN ordering (idle/walk/run/gasp/turn)"),
 ("dun\\d01\\event\\e614c04cat.chr",   "dungeon event e614 \u2014 same as e613"),
]
TR=[
 # ── cat-form vocabulary. MUST precede the generic terms below: TR is applied IN ORDER, so a shorter generic
 # entry listed first swallows the tail of a longer one (構え→"ready" turned 身構え into "身ready").
 ("身構え","brace / ready"),("踏み切り","take off (jump)"),("飛びＬ","leap"),("着地","land"),
 ("浮かぶ","float"),("そのまま首傾げ","head tilt"),("傾げﾙｰﾌﾟ","tilt (loop)"),("傾げ","tilt"),
 ("トアン見る","look at Toan"),("上見ﾙｰﾌﾟ","look up (loop)"),("振り向き","turn around"),
 ("ふりかえ戻り","turn back (to walk)"),("ふりかえﾙｰﾌﾟ","turn around (loop)"),("ふりかえ","turn around"),
 ("ハッ戻り","gasp (return to idle)"),("ハッﾙｰﾌﾟ","gasp (loop)"),("ハッ","gasp / startle"),("座り","sit"),
("連続攻撃","combo attack"),("溜め攻撃","charge attack"),("ため攻撃","charge attack"),("溜め魔法","charged magic"),
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
    e=entry(code if '\\' in code else 'mainchara\\'+code+'.chr')
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
for code,label,blurb,note in MODELS:
    r=motions(code)
    short = code.split('\\')[-1].replace('.chr','') if '\\' in code else code
    if not r: L.append("### %s `%s`\n\n*NOT FOUND*\n"%(label,short)); continue
    off,ms=r
    L.append("### %s `%s`\n"%(label,short))
    L.append("*Motions: %s @ data.dat 0x%X — %s*\n"%(code if '\\' in code else code+'.chr',off,blurb))
    if note:
        L.append("> NOTE: " + note.replace("\n","\n> ") + "\n")
    L.append("| Idx | Frames | Speed | Name (JP) | Meaning |")
    L.append("|---|---|---|---|---|")
    for idx,s,en,spd,nm in ms:
        L.append("| %d | %d–%d | %s | %s | %s |"%(idx,s,en,spd,nm if nm else "—",meaning(nm)))
    L.append("")

# ── every other cat model in the game ──────────────────────────────────────────────────────────────
L.append("### Cat-form variants (NOT playable — reference only)\n")
L.append("Every other `c04*cat` model in `data.dat`. They are listed because they show what cat animations")
L.append("EXIST — but a motion id only means anything against the model that is actually loaded, and these")
L.append("orderings differ from the playable `c04pcat`. To use one of these motions you would have to load")
L.append("that model, not just ask for the index.\n")
for path,desc in CAT_VARIANTS:
    r=motions(path)
    if not r: L.append("**`%s`** — NOT FOUND\n"%path); continue
    off,ms=r
    L.append("**`%s`** @ 0x%X — %s\n"%(path,off,desc))
    L.append("| Idx | Frames | Speed | Name (JP) | Meaning |")
    L.append("|---|---|---|---|---|")
    for idx,s,en,spd,nm in ms:
        L.append("| %d | %d–%d | %s | %s | %s |"%(idx,s,en,spd,nm if nm else "—",meaning(nm)))
    L.append("")

open(OUT,"w").write("\n".join(L)+"\n")
print("wrote",OUT)
