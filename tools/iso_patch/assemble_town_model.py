#!/usr/bin/env python3
"""Assemble a swapped-in town ally's model: fill the 10 town motion slots (see
docs/town-swap-animation-map.md) by transplanting each chosen clip (same-rig, by joint name) into the
base model's body + shadow `.mot`, then rewrite the base cfg's KEY table to the slots, and redirect the
rebuilt `.chr` into the ISO's free DATA.DAT tail (same mechanism as transplant_battle_run.py).

Town slots played by the engine (`chara\\c01d.chr`): 0 idle · 1 run · 2 walk · 3 push-door · 4 pull-door ·
5 item-get · 6 item-get-loop · 7 damage(skip) · 8 fall · 9 land.

A slot is either KEPT (points at frames the base already has) or GRAFTED (a clip from another model spliced
into a free frame window of the base, body+shadow together). Nothing is bundled — every model is read from
the user's own ISO at patch time. Run AFTER the collision bake so the tail high-water is found past it.

  python3 tools/iso_patch/assemble_town_model.py [--iso "/path/Dark Cloud - Expanded.iso"] [--test]
"""
import os, sys, struct, re, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "lib"))
import ps2iso
import mot_codec as mc

SEC = ps2iso.SECTOR
def align(x, a=SEC): return (x + a - 1) & ~(a - 1)
DEFAULT_ISO = os.path.expanduser("~/ROMs/Patched ISOs/Dark Cloud - Expanded.iso")

# Each character: base .chr + 10 slots. A slot = dict(frames=(start,end), speed, name, src=None|<src_chr>,
# win=(lo,hi)). src=None keeps the base's own frames [start,end]. src set = graft that source .chr's clip
# frames `win` into the base at [start,end] (same length), body+shadow. Multiple slots can share one graft
# (set src only on the first; later slots with same frames just re-KEY).
CHARS = {
    "Xiao": dict(
        base="gedit/e01/chara/c04pcat.chr",
        slots=[
            dict(idx=0, frames=(10, 20),  speed=0.10, name="idle",  src=None),
            dict(idx=1, frames=(120, 136), speed=0.50, name="run",   src="gedit/s86/chara/c04cat.chr",   win=(120, 136)),
            dict(idx=2, frames=(60, 80),  speed=0.30, name="walk",  src=None),
            # door = e04c04cat #5's vertical opening (frames 160-169), pulled back off the door: the cat is
            # longer than Toan, so the door-open teleport (Toan's collision) overshoots the head into the door.
            # root_offset shifts the root node -z = BACK (out of the door). TUNABLE — nudge the z magnitude.
            dict(idx=3, frames=(150, 159), speed=0.20, name="door",  src="gedit/e01/chara/e04c04cat.chr", win=(160, 169), root_offset=(0.0, 0.0, -2.0)),
            dict(idx=4, frames=(150, 159), speed=0.20, name="door2", src=None),   # reuse the grafted door frames
            # sit = s86/c04cat motion 1 (frames 30-40), a complete looping sit — grafted into the base at 30-40
            # and shared by both item slots. The town engine loops the armed KEY window, so 30-40 cycles cleanly.
            dict(idx=5, frames=(30, 40),  speed=0.10, name="sit",      src="gedit/s86/chara/c04cat.chr", win=(30, 40)),
            dict(idx=6, frames=(30, 40),  speed=0.10, name="sit-loop", src=None),   # reuse the grafted sit, full 30-40
            # slot 7 (damage — never triggered in town) repurposed as the ladder REFUSAL: e613c04cat's head-shake
            # sequence #6 no [115-135] / #7 hold [140-150] / #8 return [155-165], grafted whole into fresh frames
            # past the land clip. TownLadder plays it via the idle-motion cave when a blocked mount raises
            # RefusalRequested (one pass, then back to idle).
            dict(idx=7, frames=(228, 278), speed=0.30, name="refuse(shake)", src="dun/d01/event/e613c04cat.chr", win=(115, 165)),
            dict(idx=8, frames=(205, 214), speed=0.50, name="fall(leap)", src="gedit/s86/chara/c04cat.chr", win=(205, 214)),
            dict(idx=9, frames=(215, 227), speed=0.36, name="land",  src="gedit/s86/chara/c04cat.chr",   win=(215, 227)),
            # idx 10 = DOUBLE-DOOR (両開き): Toan's c01d has an 11th KEY the door system plays at double doors —
            # every ally build must fill it or double-doors play nothing (or worse, a choreography clip).
            dict(idx=10, frames=(150, 159), speed=0.20, name="door3(dbl)", src=None),   # reuse the grafted door clip
            # CHOREOGRAPHY-ONLY slots (the engine drives 0-10; these are played by TownLadder's jump script via
            # _SET_NPC_MOTION, which accepts any KEY index — the docs/town-swap-animation-map.md up-ladder plan):
            # 11 = s86 #3 "ready" crouch; 12 = e04c04cat #5 float/hop-up, fast, NO root offset (the door slot's
            # copy of this clip is z-shifted for the door teleport — unusable mid-flight).
            dict(idx=11, frames=(95, 105),  speed=0.40, name="jump-ready", src="gedit/s86/chara/c04cat.chr", win=(95, 105)),
            dict(idx=12, frames=(285, 294), speed=0.60, name="float-up",   src="gedit/e01/chara/e04c04cat.chr", win=(160, 169)),
            # 13 = the base's own walk (60-80) copied to fresh frames and REVERSED — the backwards steps of the
            # ladder-jump alignment (the engine can't play a clip backwards; speed overrides are gated positive).
            dict(idx=13, frames=(300, 320), speed=0.30, name="walk-back",  src="gedit/e01/chara/c04pcat.chr", win=(60, 80), reverse=True),
        ],
    ),
    # Osmond (docs/town-swap-animation-map.md "Osmond — cataloged 2026-09-05"): base c18p (4 KEYs; body frames
    # 1..80). CUTSCENE CONSTRAINT: the replayable s13/s2201 talk-to-Osmond scene plays idx 0 and 3 — their
    # KEY values (frames+speed) are reproduced EXACTLY. Doors reuse the talk clip (the user's resolved call);
    # run = the same #1 clip at a bumped KEY speed (no better run exists); item-get = talk held at frame 69
    # (hand-out pose, sampled hold); fall/land = e403c18a #10/#11 (jump-down seq's fall-loop + land).
    "Osmond": dict(
        base="gedit/e05/chara/c18p.chr",
        # MESH-NODE GRAFT: c18p's rig LACKS the helicopter propeller assembly, so the heli clips below
        # animated nothing visible. Append the 13 propeller nodes from the s13 flight model — the mast
        # chain backpack->bone4->bone5->bone6->tukene1 plus three blade arms (obj41/42/43, obj38/39/40,
        # obj35/36/37) — with their rigid MDT chunks and .bbp bind rows, and hold them at e402c18a's
        # idle pose (frame 15; == the folded/stowed BIND pose, verified) outside the heli-clip windows.
        # Textures: the blades map the same 'c18a01' atlas the backpack shell already renders with;
        # c18p's copy differs from s13's only by requantization noise (mean 10/255) -> no TIM2 append.
        mesh_graft=dict(
            src="gedit/s13/chara/e402c18a.chr",
            pose_frame=15,          # inside e402c18a's idle KEY 10-20 (blades stowed there)
            nodes=["bone4", "bone5", "bone6", "tukene1", "obj41", "obj42", "obj43",
                   "obj38", "obj39", "obj40", "obj35", "obj36", "obj37"],
            # SYNTHETIC SPIN: e402c18a never rotates the assembly (blades static in every flight
            # window; vanilla's visible spin is the un-ported VERTEX_ANIME stream), so rotation
            # keyframes are authored on the hub `tukene1` (bind rotation = identity; the three blade
            # arms spread 120 deg in its local X-Z plane at y~0.485 -> spin axis = local +Y, the mast
            # direction). rate=72 deg/clip-frame: at TownLadder's 0.5x playback override that's
            # 36 deg/engine-frame = 6 rev/s — the intended fast read (36 deg/f netted only ~1.2 rev/s).
            # 720 deg across the 10-frame loop == 0 mod 360 -> still a seamless wrap; 72-deg steps
            # stay < 90 for the engine's quaternion blend.
            # PHASE CONTINUITY (handoff_advance): the three windows play back-to-back at TownLadder's
            # 0.5x override (one ENGINE frame = 0.5 clip frames = 36 deg at rate 72), and the C# also
            # phase-locks the flight to whole loop periods, so every window hand-off must ADVANCE the
            # displayed rotation by exactly one engine step or the propeller visibly stalls for a
            # frame. The loop is therefore phase-shifted +36 deg (rampup ends ==0, loop starts at 36)
            # and the rampdown is a 4-DOF cubic starting one step PAST the loop's final ==0 phase:
            # theta(275)=36, theta'(275)=rate, theta'(290)=0, theta(290)==0 mod 360.
            # handoff_advance MUST match rate * TownLadder HeliFlightSpeed (72 * 0.5 = 36).
            spin=dict(
                node="tukene1", axis=(0.0, 1.0, 0.0), rate=72.0, handoff_advance=36.0,
                rampup=(240, 255),      # start-fly: ease-in 0 -> rate, whole turns (2 @ 72 deg/f)
                loop=(260, 270),        # fly-loop: full rate, phase +handoff; rate*len == 0 mod 360
                rampdown=(275, 290),    # rev-startfly: ease-out rate -> 0, from +handoff to rest
            ),
        ),
        slots=[
            dict(idx=0, frames=(10, 20),   speed=0.10, name="idle",        src=None),   # EXACT original (cutscene)
            # run in its OWN window (base's 30-50 copied to 130-150): the engine's run↔walk foot-phase blend
            # (ids 1/2 hard-case) breaks when both ids share one window at different speeds — the "walk plays
            # faster than run" inversion. Toan's own c01d runs/walks from separate windows.
            dict(idx=1, frames=(130, 150), speed=0.75, name="run(sped)",   src="gedit/e05/chara/c18p.chr", win=(30, 50)),
            dict(idx=2, frames=(30, 50),   speed=0.20, name="walk",        src=None),   # eased from the 0.30 original (read fast in-game)
            dict(idx=3, frames=(60, 80),   speed=0.20, name="door(talk)",  src=None),   # EXACT original (cutscene)
            dict(idx=4, frames=(60, 80),   speed=0.30, name="door2(talk)", src=None),   # pull-door: Toan's door cadence
            dict(idx=5, frames=(90, 92),   speed=0.10, name="item(hold69)", hold=69),   # talk hand-out pose, held
            dict(idx=6, frames=(90, 92),   speed=0.10, name="item-loop",   src=None),   # same held pose
            dict(idx=7, frames=(10, 20),   speed=0.10, name="damage(skip)", src=None),
            dict(idx=8, frames=(100, 108), speed=0.30, name="fall",        src="gedit/e05/chara/e403c18a.chr", win=(210, 218)),
            dict(idx=9, frames=(110, 125), speed=0.30, name="land",        src="gedit/e05/chara/e403c18a.chr", win=(220, 235)),
            dict(idx=10, frames=(60, 80),  speed=0.30, name="door3(dbl)",  src=None),   # double-door (両開き) = talk clip
            # CHOREOGRAPHY (TownLadder's Osmond sequences, played via _SET_NPC_MOTION):
            # down-ladder launch = e403 #9 jump-down dive; up-ladder = the helicopter backpack set from
            # e402c18a·s13 (#16 propeller-out / #17 start-fly / #18 fly-loop) + the REVERSED pair for the
            # top-of-ladder landing (descend + stow), per the catalog.
            dict(idx=11, frames=(155, 175), speed=0.30, name="jump-down",    src="gedit/e05/chara/e403c18a.chr", win=(185, 205)),
            dict(idx=12, frames=(180, 235), speed=0.25, name="propeller",    src="gedit/s13/chara/e402c18a.chr", win=(280, 335)),
            dict(idx=13, frames=(240, 255), speed=0.25, name="start-fly",    src="gedit/s13/chara/e402c18a.chr", win=(345, 360)),
            dict(idx=14, frames=(260, 270), speed=0.20, name="fly-loop",     src="gedit/s13/chara/e402c18a.chr", win=(360, 370)),
            dict(idx=15, frames=(275, 290), speed=0.25, name="rev-startfly", src="gedit/s13/chara/e402c18a.chr", win=(345, 360), reverse=True),
            dict(idx=16, frames=(295, 350), speed=0.30, name="rev-propeller(stow)", src="gedit/s13/chara/e402c18a.chr", win=(280, 335), reverse=True),
        ],
    ),
    # Goro (user catalog + the VANILLA treehouse climb decoded from gedit/s01/event.stb): base c06p has real
    # separate run (60-80) / walk (30-50) windows — kept EXACT. Doors = idle per the catalog ("just idle").
    # Item-get = dungeon c06b #34/#35; refusal "no" = dungeon #31 (played via the idle-motion cave, slot 7).
    # Ladder clips (e102 · s01, Goro-rig): fall #6, land/mount-hop #3, and the ALTERNATING climb jumps #4/#5 —
    # the exact clips the vanilla cutscene chains (_ASQ_MOTION_PLAY 3 → NEXT 4 → PLAY 5 over a slow linear rise).
    "Goro": dict(
        base="gedit/s01/chara/c06p.chr",
        # ROOT MOTION (opt-in): recover source root/hip channels the town .mot never carried — a
        # source track active in a graft window with no matching dest track is otherwise dropped.
        # Creation anchors the rest of the timeline at the dest node's BIND value (what the engine
        # held with no track), so grafted root offsets can't leak into idle/walk. Data notes
        # (verified): the climb bounce is null69 (hip) chan-2 y/z — name-matched, it grafts even
        # without this; the vanilla zigzag's lateral x is NOT baked in e102's clips (x == -0.000 in
        # both hop windows — the cutscene's ASQ path supplies it), so TownLadder must keep steering
        # x. What creation recovers: c06b's item/refusal hip ROTATION (null69 chan-0), item-node
        # rotation, null71_1 micro root translation (body+shadow), and e102's 4 static dcol keys.
        root_motion=True,
        slots=[
            dict(idx=0,  frames=(10, 20),   speed=0.15, name="idle",        src=None),   # EXACT original
            dict(idx=1,  frames=(60, 80),   speed=0.40, name="run",         src=None),   # EXACT original
            dict(idx=2,  frames=(30, 50),   speed=0.25, name="walk",        src=None),   # EXACT original
            dict(idx=3,  frames=(10, 20),   speed=0.15, name="door(idle)",  src=None),   # catalog: just idle
            dict(idx=4,  frames=(10, 20),   speed=0.15, name="door2(idle)", src=None),
            dict(idx=5,  frames=(90, 100),  speed=0.30, name="item",        src="dun/mainchara/c06b.chr", win=(515, 525)),
            dict(idx=6,  frames=(105, 115), speed=0.30, name="item-loop",   src="dun/mainchara/c06b.chr", win=(530, 540)),
            dict(idx=7,  frames=(120, 149), speed=0.50, name="refuse(no)",  src="dun/mainchara/c06b.chr", win=(421, 450)),
            # fall/land = the VANILLA DESCENT's own clips (event 101, e101c06a): #3 falling 75-95, #4 slow-stand
            # landing 95-105 — the RE showed the original picks (e102 #6/#3) were actually peer-down/panting.
            dict(idx=8,  frames=(155, 175), speed=0.30, name="fall",        src="gedit/s01/chara/e101.chr", win=(75, 95),  src_cfg="e101c06a.cfg"),
            dict(idx=9,  frames=(180, 190), speed=0.25, name="land(stand)", src="gedit/s01/chara/e101.chr", win=(95, 105), src_cfg="e101c06a.cfg"),
            dict(idx=10, frames=(10, 20),   speed=0.15, name="door3(idle)", src=None),
            # CHOREOGRAPHY (TownLadder's Goro climb): the vanilla alternating ladder jump-kicks (e102 #4/#5).
            dict(idx=11, frames=(195, 215), speed=0.30, name="climb-hopA",  src="gedit/s01/chara/e102.chr", win=(90, 110), src_cfg="e102c06a.cfg"),
            dict(idx=12, frames=(220, 240), speed=0.30, name="climb-hopB",  src="gedit/s01/chara/e102.chr", win=(110, 130), src_cfg="e102c06a.cfg"),
            # #4 (the standing→bent-over crouch, slot 9) baked REVERSED = bent→standing. The jump-down windup
            # plays this then the forward crouch (continuous at the standing seam) before launching.
            dict(idx=13, frames=(245, 255), speed=0.25, name="rev-land",    src="gedit/s01/chara/e101.chr", win=(95, 105), src_cfg="e101c06a.cfg", reverse=True),
        ],
    ),
    # Ungaga — catalog docs/town-swap-animation-map.md (adapted from the c10p plan onto e323_2c10a, the
    # shipped swap model: cloth + native run window + shadow). Slots 0-4 keep e323's own windows (3/4 = his
    # talk clips = the door anims), so the e04 recruitment cutscene stays mostly intact; the battle run is
    # grafted INTO the native 60-80 window (retiring transplant_battle_run's Ungaga row). New clips go in
    # fresh windows past his 226-frame timeline. LADDERS: none — he refuses at BOTH ends (slot 7 NG pose).
    "Ungaga": dict(
        base="gedit/e04/chara/e323_2c10a.chr",
        root_motion=True,
        slots=[
            dict(idx=0,  frames=(10, 20),   speed=0.10, name="idle",        src=None),
            dict(idx=1,  frames=(60, 80),   speed=0.55, name="run",         src="dun/mainchara/c10b.chr", win=(60, 80)),
            dict(idx=2,  frames=(30, 50),   speed=0.27, name="walk",        src=None),
            # doors: the chest-hand talk gesture (more visible than the subtle 95-105 talk), pulled back
            # off the door by a root offset (applies once — 4/10 share the window).
            dict(idx=3,  frames=(115, 131), speed=0.20, name="door(chest)", src=None, root_offset=(0.0, 0.0, -2.5)),
            dict(idx=4,  frames=(115, 131), speed=0.20, name="door2",       src=None),
            dict(idx=5,  frames=(240, 250), speed=0.30, name="item",        src="dun/mainchara/c10b.chr", win=(630, 640)),
            dict(idx=6,  frames=(255, 265), speed=0.30, name="item-loop",   src="dun/mainchara/c10b.chr", win=(643, 653)),
            dict(idx=7,  frames=(270, 310), speed=0.22, name="refuse(NG)",  src="dun/mainchara/c10b.chr", win=(490, 530),
                 pin_nodes=dict(nodes=["kon_1", "r_handa"], ref=15)),   # staff rides the hand at its idle grip
            # fall/land per the catalog: damage-big REVERSED = falling forward; land = a 2f settle.
            # fall = STATIC frame 297 of c10b's damage-big (a 1-frame KEY @speed 0 — vanilla precedent:
            # c10b's own "26投げ停止 186,186,0.0"). No animation, just the falling pose.
            dict(idx=8,  frames=(315, 315), speed=0.0,  name="fall(296)",   src="dun/mainchara/c10b.chr", win=(296, 296)),
            dict(idx=9,  frames=(357, 358), speed=0.20, name="land(rev)",   src="dun/mainchara/c10b.chr", win=(295, 296), reverse=True),   # 296->295, continuous from the static-296 fall
            dict(idx=10, frames=(115, 131), speed=0.20, name="door3",      src=None),
        ],
    ),
    # Ruby — catalog docs/town-swap-animation-map.md. Base = c05a "simple" (the e03 town NPC: only
    # idle/walk/run and NO shadow) + SHADOW INJECTION from the dungeon model (c05s set; its rig names are
    # identical to e223c05s's). All locomotion grafted from dun c05a (same windows the town model uses, and
    # it keeps body+shadow authored together); doors = e223's pat/knock; float/jump = e228 (her Norune-intro
    # event model). e228's jump BAKES the cutscene's 43u leap in root/hip translation and its loop HOLDS the
    # leap end offset -> root_freeze pins both to rest values (11=own window start, 12=the float's rest @175).
    "Ruby": dict(
        base="gedit/e03/chara/c05a.chr",
        root_motion=True,                             # town mot has NO tracks for several deformer nodes -> create
        # (The garble/freeze saga was the legacy pnach "fix animation" hack text-editing her cfg in RAM —
        # removed from A5C05C78.pnach 2026-09-07. Shadow donor = e223c05a's set: town-proven, rig names
        # identical to dun c05s, small enough to copy unstripped.)
        shadow_inject=dict(src="gedit/e03/chara/e223c05a.chr",
                           model="e223c05s.mds", mot="e223c05s.mot", bbp="e223c05s.bbp", wgt="e223c05s.wgt"),
        slots=[
            dict(idx=0,  frames=(10, 20),   speed=0.15, name="idle",        src="dun/mainchara/c05a.chr", win=(10, 20)),
            dict(idx=1,  frames=(60, 80),   speed=0.55, name="run",         src="dun/mainchara/c05a.chr", win=(60, 80)),
            dict(idx=2,  frames=(30, 50),   speed=0.25, name="walk",        src="dun/mainchara/c05a.chr", win=(30, 50)),
            # root_offset -z = pull her BACK off the door (the pat clip reaches forward). TUNABLE.
            dict(idx=3,  frames=(90, 102),  speed=0.15, name="door(pat)",   src="gedit/e03/chara/e223c05a.chr", win=(100, 112), root_offset=(0.0, 0.0, -3.0)),
            dict(idx=4,  frames=(90, 102),  speed=0.15, name="door2(pat)",  src=None),   # same window as #3
            dict(idx=5,  frames=(120, 130), speed=0.30, name="item",        src="dun/mainchara/c05a.chr", win=(575, 585)),
            dict(idx=6,  frames=(135, 145), speed=0.30, name="item-loop",   src="dun/mainchara/c05a.chr", win=(590, 600)),
            dict(idx=7,  frames=(150, 170), speed=0.20, name="refuse(no)",  src="dun/mainchara/c05a.chr", win=(605, 625)),
            dict(idx=8,  frames=(175, 195), speed=0.10, name="fall(float)", src="gedit/e03/chara/e228c05a.chr", win=(10, 30)),
            dict(idx=9,  frames=(10, 20),   speed=0.15, name="land(idle)",  src=None),   # settle to idle
            dict(idx=10, frames=(90, 102),  speed=0.15, name="door3(pat)",  src=None),
            # CHOREOGRAPHY (TownLadder): her ladder jump-down + mid-air hold, root travel frozen (e228's
            # jump BAKES the cutscene's 43u leap; the loop HOLDS the leap end offset).
            dict(idx=11, frames=(200, 230), speed=0.20, name="jump",        src="gedit/e03/chara/e228c05a.chr", win=(285, 315), root_freeze=200),
            dict(idx=12, frames=(235, 245), speed=0.10, name="jump-loop",   src="gedit/e03/chara/e228c05a.chr", win=(320, 330), root_freeze=175),
        ],
    ),
}


def _cfg_motions(pack, prefer=None):
    """Return (cfg_record, body_mot, body_mds, shadow_mot, shadow_mds) from a .chr's main cfg (the one whose
    payload holds MODEL/MOTION/KEY). Dual-bundled event models (e101/e102 carry Toan AND Goro) need `prefer`
    = the exact cfg record name (e.g. 'e101c06a.cfg') — the first-match heuristic picks whichever cfg comes
    first in the pack."""
    cand = [r for r in pack.records if r.name.lower().endswith('.cfg')]
    if prefer:
        cand = [r for r in cand if r.name.lower() == prefer.lower()] + [r for r in cand if r.name.lower() != prefer.lower()]
    cfg = None
    for r in cand:
        if b'KEY_START' in r.payload and b'MOTION' in r.payload:
            cfg = r; break
    if cfg is None and cand:
        cfg = cand[0]
    t = cfg.payload
    def grab(tag):
        m = re.search(tag + rb'[ \t]+"([^"]+)"', t)
        return m.group(1).decode('latin1') if m else None
    body_mot = grab(rb'MOTION[ \t]+0,')
    if not body_mot:  # MOTION 0, "x.mot", ...
        m = re.search(rb'MOTION[ \t]+0,[ \t]*"([^"]+)"', t); body_mot = m.group(1).decode('latin1') if m else None
    body_mds = grab(rb'MODEL')
    sh_mot = None
    m = re.search(rb'SHADOW_MOTION[ \t]+"([^"]+)"', t); sh_mot = m.group(1).decode('latin1') if m else None
    sh_mds = grab(rb'SHADOW_MODEL')
    return cfg, body_mot, body_mds, sh_mot, sh_mds


def _cfg_bbp(cfg_payload):
    """The body MOTION 0 line's .bbp filename (2nd quoted arg of `MOTION 0, "x.mot", "x.bbp", ...`)."""
    m = re.search(rb'MOTION[ \t]+0,[ \t]*"[^"]+",[ \t]*"([^"]+)"', cfg_payload)
    return m.group(1).decode('latin1') if m else None


def _quat_mul(a, b):
    """Hamilton product, scalar-first (w,x,y,z) — the .mot chan-0 storage order."""
    aw, ax, ay, az = a; bw, bx, by, bz = b
    return (aw * bw - ax * bx - ay * by - az * bz,
            aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw)


def _quat_to_mat(q):
    """quat -> 3x3, the engine convention (mot-format.md §10.2; reproduces the stored bind 3x3)."""
    w, x, y, z = q
    return ((1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)),
            (2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)),
            (2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)))


def _mat_to_quat(R):
    """EXACT inverse of _quat_to_mat (Shepperd, signs per mot-format.md §10.2 / extract_model.py)."""
    (m00, m01, m02), (m10, m11, m12), (m20, m21, m22) = R
    tr = m00 + m11 + m22
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        return (0.25 * s, (m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s)
    if m00 >= m11 and m00 >= m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        return ((m21 - m12) / s, 0.25 * s, (m01 + m10) / s, (m02 + m20) / s)
    if m11 >= m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        return ((m02 - m20) / s, (m01 + m10) / s, 0.25 * s, (m12 + m21) / s)
    s = math.sqrt(1.0 + m22 - m00 - m11) * 2
    return ((m10 - m01) / s, (m02 + m20) / s, (m12 + m21) / s, 0.25 * s)


def _mds_bind(pl, i):
    """(parent, 3x3 bind rows, bind translation) of node i in a .mds payload."""
    p = 0x18 + i * 0x70
    par = struct.unpack_from('<i', pl, p + 0x24)[0]
    M = struct.unpack_from('<16f', pl, p + 0x28)
    return par, (M[0:3], M[4:7], M[8:11]), list(M[12:15])


def _fold_spans(windows, lo, hi):
    """Complement of the graft windows within [lo,hi] — the frame spans where grafted nodes must hold
    the folded pose."""
    spans, cur = [], lo
    for a, b in sorted(windows):
        if cur < a:
            spans.append((cur, a - 1))
        cur = max(cur, b + 1)
    if cur <= hi:
        spans.append((cur, hi))
    return spans


def _graft_mesh_nodes(base, sp, mg, windows, maxframe):
    """MESH-NODE GRAFT: append the named CFrame nodes (+ their rigid MDT mesh chunks + .bbp bind rows)
    from the source rig onto the base body .mds, then give the base body .mot one track per source
    blade channel, anchored to the source's stowed pose (frame mg['pose_frame']) across every frame
    span OUTSIDE the graft `windows`. Runs BEFORE the motion grafts: splice_motion_by_joint only
    writes dest tracks that EXIST, so creating the tracks here is what lets the heli clips land their
    blade keyframes into the windows by name. The anchors matter even when the stowed pose equals the
    bind pose — once a node has a track, the engine clamps to its nearest keyframe and the bind-matrix
    fallback is gone.

    .mds layout (mot-format.md §10, re-verified on c18p/e402c18a): count @+0x08, node table @0x18
    stride 0x70 {name@+0x00, MESH offset@+0x20 (ABSOLUTE within the payload, 0 = no geometry),
    parent index@+0x24, bind matrix@+0x28, id@+0x68, 0x70@+0x6C}; the LAST record's two trailing
    words physically OVERLAP the first MDT chunk, which starts at 0x18+count*0x70-8. Appending K
    nodes therefore shifts the whole mesh block by K*0x70 — every existing nonzero mesh offset is
    rebased by that delta (all other existing record/mesh bytes stay identical) and the grafted
    nodes' chunks are appended verbatim after the block. .bbp = count x 64-byte per-node bind rows
    (no header; same-named nodes carry byte-identical rows in both rigs) -> extended with the source
    rows of the grafted nodes. The shadow rig is a blob mesh without these nodes and is untouched
    (guarded)."""
    scfg, sbmot, sbmds, _, _ = _cfg_motions(sp)
    cfg, bmot, bmds, _smot, smds = _cfg_motions(base)
    names = mg["nodes"]
    bp = base.find(bmds).payload
    spl = sp.find(sbmds).payload
    bnames = mc.read_mds_frames(bp)
    snames = mc.read_mds_frames(spl)
    nb, K = len(bnames), len(names)
    dup = [n for n in names if n in bnames]
    if dup:
        raise SystemExit(f"mesh_graft: nodes already in base rig: {dup}")
    missing = [n for n in names if n not in snames]
    if missing:
        raise SystemExit(f"mesh_graft: nodes not in source rig: {missing}")
    if smds:
        clash = [n for n in names if n in mc.read_mds_frames(base.find(smds).payload)]
        if clash:
            raise SystemExit(f"mesh_graft: base SHADOW rig carries {clash} — mirroring unsupported")
    mesh0 = 0x18 + nb * 0x70 - 8                     # first MDT (overlaps last record's tail words)
    firsts = [struct.unpack_from('<I', bp, 0x18 + i * 0x70 + 0x20)[0] for i in range(nb)]
    if min(o for o in firsts if o) != mesh0 or bp[mesh0:mesh0 + 4] != b'MDT\x00':
        raise SystemExit("mesh_graft: base .mds table/mesh-overlap layout not as expected")
    delta = K * 0x70
    new_index = {n: nb + k for k, n in enumerate(names)}
    # --- node table: complete the old last record, bump count, rebase existing mesh offsets ---
    table = bytearray(bp[:mesh0]) + struct.pack('<II', nb, 0x70)
    struct.pack_into('<I', table, 0x08, nb + K)
    for i in range(nb):
        off = struct.unpack_from('<I', table, 0x18 + i * 0x70 + 0x20)[0]
        if off:
            struct.pack_into('<I', table, 0x18 + i * 0x70 + 0x20, off + delta)
    # --- grafted records (parents remapped by NAME) + their MDT chunks (appended past the block) ---
    mblock = bp[mesh0:]
    p_start = 0x18 + (nb + K) * 0x70 - 8 + len(mblock)
    recs, chunks = bytearray(), bytearray()
    for k, n in enumerate(names):
        si = snames.index(n)
        rec = bytearray(spl[0x18 + si * 0x70:0x18 + si * 0x70 + 0x68])
        spar = struct.unpack_from('<i', rec, 0x24)[0]
        pname = snames[spar] if spar >= 0 else None
        if pname in bnames:
            npar = bnames.index(pname)
        elif pname in new_index:
            npar = new_index[pname]
        else:
            raise SystemExit(f"mesh_graft: node {n} parent {pname!r} resolves to neither base nor grafted set")
        if npar >= nb + k:
            raise SystemExit(f"mesh_graft: node {n} parent {pname!r} would come AFTER it — reorder `nodes`")
        struct.pack_into('<i', rec, 0x24, npar)
        smo = struct.unpack_from('<I', rec, 0x20)[0]
        if smo:
            if spl[smo:smo + 4] != b'MDT\x00':
                raise SystemExit(f"mesh_graft: node {n} mesh @0x{smo:X} is not an MDT chunk")
            csz = struct.unpack_from('<I', spl, smo + 8)[0]
            struct.pack_into('<I', rec, 0x20, p_start + len(chunks))
            chunks += spl[smo:smo + csz]
            chunks += b'\x00' * (-len(chunks) % 16)
        recs += rec + struct.pack('<II', nb + k + 1, 0x70)
    recs = recs[:-8]                                 # new last record: tail words = first mesh bytes (overlap)
    base.replace_payload(bmds, bytes(table) + bytes(recs) + mblock + bytes(chunks))
    # --- .bbp: append the source's 64-byte bind rows for the grafted nodes ---
    bbp, sbbp = _cfg_bbp(cfg.payload), _cfg_bbp(scfg.payload)
    bb, sb = base.find(bbp).payload, sp.find(sbbp).payload
    if len(bb) != nb * 64 or len(sb) != len(snames) * 64:
        raise SystemExit("mesh_graft: .bbp is not count*64 bytes — layout assumption broken")
    add = b''.join(sb[snames.index(n) * 64:(snames.index(n) + 1) * 64] for n in names)
    base.replace_payload(bbp, bb + add)
    # --- body .mot: one new track per source blade channel, folded anchors outside the windows ---
    sm = mc.Mot.from_record(sp.find(sbmot))
    dm = mc.Mot.from_record(base.find(bmot))
    spans = _fold_spans(windows, 1, maxframe)
    spin = mg.get("spin")
    if spin and spin["node"] not in new_index:
        raise SystemExit(f"mesh_graft.spin: node {spin['node']!r} is not in the grafted set")
    by_node = {}
    for st in sm.tracks:
        nname = snames[st.w0] if st.w0 < len(snames) else None
        if nname in new_index:
            by_node.setdefault(nname, []).append(st)
    created = []
    for n in names:                                  # per node, ascending new w0 (keeps .mot track order)
        for st in by_node.get(n, []):
            v = _sample_track(st, mg["pose_frame"])
            if v is None:
                continue
            kfs = [mc.Keyframe(struct.pack('<4I4f', f, 0, 0, 0, *v))
                   for lo, hi in spans for f in sorted({lo, hi})]
            dm.tracks.append(mc.Track(new_index[n], st.w1, st.w2, st.w3, st.w6, st.w7, kfs))
            created.append((n, st.w2))
        if spin and n == spin["node"] and not any(st.w2 == 0 for st in by_node.get(n, [])):
            # the spin hub has NO source rotation track (it holds bind) — synthesize an anchor-only
            # chan-0 track at the bind orientation so _apply_spin (after the motion grafts) can fill
            # the flight windows; once a track exists the bind fallback is gone, so the anchors keep
            # the hub at rest everywhere else.
            si = snames.index(n)
            qb = _mat_to_quat(_mds_bind(spl, si)[1])
            kfs = [mc.Keyframe(struct.pack('<4I4f', f, 0, 0, 0, *qb))
                   for lo, hi in spans for f in sorted({lo, hi})]
            dm.tracks.append(mc.Track(new_index[n], 0, 0, 0x20, mc.TAG_W6, mc.TAG_W7, kfs))
            created.append((n, 0))
    base.replace_payload(bmot, dm.rebuild()[dm.data_off:])
    return dict(nodes=K, mesh_bytes=len(chunks), bbp_bytes=len(add), tracks=created,
                delta=delta, spans=spans)


def _apply_spin(base, mg):
    """SYNTHETIC SPIN: author chan-0 keyframes on the mesh-graft's hub node so the whole propeller
    subtree rotates about the hub's LOCAL spin axis. The source rig never rotates the assembly (its
    visible spin is VERTEX_ANIME, which we don't port), so the rotation is synthesized:

      rampup   ease-in cubic θ(t)=at³+bt² (θ'(0)=0, θ'(n)=rate, total = whole turns — a,b solved),
      loop     full `rate` (deg/clip-frame, config; default 360/len) with keyframes every frame —
               rate*len must be ≡ 0 mod 360 so the loop wraps seamlessly (validated),
      rampdown the mirrored ease-out (starts at rate, ends at rest on a whole turn).

    Keyframes are q_axis(θ) ⊗ q_bind, so θ≡0 (mod 360°) equals the bind orientation — which is what
    the fold anchors hold — making the ramp-end boundaries and every clip hand-off continuous. Steps
    must stay < 90° (rate < 90 enforced, ramp steps checked) with a sign-continuity pass (consistent
    hemisphere), safe for the engine's nlerp-style blend. MUST run AFTER the motion grafts: slot
    machinery (_reverse_window in particular) mirrors every track's keys inside its window and would
    corrupt pre-baked spin."""
    spin = mg["spin"]
    _cfg, bmot, bmds, _smot, _smds = _cfg_motions(base)
    pl = base.find(bmds).payload
    frames = mc.read_mds_frames(pl)
    w0 = frames.index(spin["node"])
    dm = mc.Mot.from_record(base.find(bmot))
    t = dm.track_by(w0, 0)
    if t is None:
        raise SystemExit(f"spin: hub {spin['node']} chan-0 track missing (mesh graft creates it)")
    lo_l, hi_l = spin["loop"]
    rate = spin.get("rate", 360.0 / (hi_l - lo_l))   # deg/clip-frame at full speed
    if rate <= 0 or rate >= 90.0:
        raise SystemExit(f"spin: rate {rate} deg/frame outside (0,90) — quaternion steps would flip")
    if abs((rate * (hi_l - lo_l)) % 360.0) > 1e-6 and abs((rate * (hi_l - lo_l)) % 360.0 - 360.0) > 1e-6:
        raise SystemExit(f"spin: rate*looplen = {rate * (hi_l - lo_l)} deg is not a whole number of "
                         f"turns — the loop wrap would pop")
    def ramp(n):
        """θ(t)=at³+bt² with θ'(0)=0, θ'(n)=rate, θ(n)=T (whole turns, ~2/3·rate·n for a monotone
        ease). Returns (θ(t), T)."""
        T = 360.0 * max(1, round(rate * n * 2.0 / 3.0 / 360.0))
        a = (rate * n - 2 * T) / n ** 3
        b = (3 * T - rate * n) / n ** 2
        return (lambda x: a * x ** 3 + b * x ** 2), T
    h = spin.get("handoff_advance", 0.0)             # deg the DISPLAYED rotation advances per engine
                                                     # frame (= rate * playback multiplier); phases the
                                                     # windows so hand-offs keep spinning (0 = none)
    if h and not (0.0 < h < 90.0):
        raise SystemExit(f"spin: handoff_advance {h} outside (0,90)")
    keys = {}                                        # frame -> θ in degrees
    if spin.get("rampup"):
        lo, hi = spin["rampup"]
        f, _T = ramp(hi - lo)
        for fr in range(lo, hi + 1):
            keys[fr] = f(fr - lo)                    # ends ≡ 0 mod 360 (rest phase)
    for fr in range(lo_l, hi_l + 1):
        keys[fr] = h + (fr - lo_l) * rate            # loop phase-shifted +h: first displayed frame is
                                                     # one engine step past the rampup's final ≡0
    if spin.get("rampdown"):
        lo, hi = spin["rampdown"]
        n = hi - lo
        # 4-DOF cubic θ(t)=c3t³+c2t²+c1t+c0 pinned by θ(0)=h (one engine step past the loop's final
        # ≡0 displayed phase — the C# phase-locks flight to whole loop periods), θ'(0)=rate,
        # θ'(n)=0, θ(n)=T ≡ 0 mod 360. Whole-turn totals are tried nearest the ease target first;
        # keep the first that is monotone with every clip-frame step < 90°.
        sol, tried = None, []
        for T in sorted((k * 360.0 for k in range(1, 9)),
                        key=lambda T: abs(T - (h + rate * n * 2.0 / 3.0))):
            D = T - h - rate * n
            c3 = (-rate * n - 2 * D) / n ** 3
            c2 = (D - c3 * n ** 3) / n ** 2
            def th(x, c3=c3, c2=c2): return c3 * x ** 3 + c2 * x ** 2 + rate * x + h
            def dth(x, c3=c3, c2=c2): return 3 * c3 * x * x + 2 * c2 * x + rate
            crit = [x for x in ([-c2 / (3 * c3)] if c3 else []) if 0 < x < n]
            mono = all(dth(x) > -1e-9 for x in crit + [0, n])
            maxstep = max(th(k + 1) - th(k) for k in range(n))
            tried.append((T, mono, round(maxstep, 2)))
            if mono and maxstep < 90.0:
                sol = th
                break
        if sol is None:
            raise SystemExit(f"spin: no whole-turn rampdown cubic satisfies θ(0)={h}, θ'(0)={rate}, "
                             f"θ'(n)=0 monotone with steps < 90° — tried (T, monotone, maxstep): {tried}")
        for fr in range(lo, hi + 1):
            keys[fr] = sol(fr - lo)
    if any(k.frame in keys for k in t.keyframes):
        raise SystemExit("spin: a spin window collides with existing hub keys (fold anchors?)")
    ordered = sorted(keys.items())
    for (fa, da), (fb, db) in zip(ordered, ordered[1:]):
        if fb == fa + 1 and abs(db - da) >= 90.0:
            raise SystemExit(f"spin: step {da:.1f}->{db:.1f} deg at frame {fb} is >= 90 — "
                             f"quaternion interpolation would take the short way round")
    ax = spin.get("axis", (0.0, 1.0, 0.0))
    qb = _mat_to_quat(_mds_bind(pl, w0)[1])
    for fr, deg in sorted(keys.items()):
        th = math.radians(deg)
        s = math.sin(th / 2)
        q = _quat_mul((math.cos(th / 2), ax[0] * s, ax[1] * s, ax[2] * s), qb)
        t.keyframes.append(mc.Keyframe(struct.pack('<4I4f', fr, 0, 0, 0, *q)))
    t.keyframes.sort(key=lambda k: k.frame)
    prev = None                                      # hemisphere continuity for the engine's nlerp
    for k in t.keyframes:
        v = k.value
        if prev is not None and sum(a * b for a, b in zip(prev, v)) < 0:
            struct.pack_into('<4f', k.raw, 0x10, *[-x for x in v])
            v = k.value
        prev = v
    base.replace_payload(bmot, dm.rebuild()[dm.data_off:])
    return dict(node=spin["node"], w0=w0, keys=len(keys), rate=rate)


def _alias_root(sframes, dframes):
    """Same-character rigs sometimes differ ONLY in the ROOT node's name (c06p 'c06a_1_1_3_4' vs e102
    'c06a_1_1_3') — a by-NAME remap then drops root tracks (splice_motion_by_joint already maps node
    0->0 positionally, but _seal_graft's remap is name-only). Both roots are node 0: alias src[0] to
    dst[0]'s name when neither name exists in the other rig. GATED under a character's `root_motion`
    flag — ungated it shifted Osmond's approved build (his e402/e403 root names differ from c18p's)."""
    if sframes and dframes and sframes[0] != dframes[0] \
            and sframes[0] not in dframes and dframes[0] not in sframes:
        return [dframes[0]] + list(sframes[1:])
    return sframes


def _create_missing_tracks(dst, src, sframes, dframes, slo, shi, dpl, spans):
    """MISSING-TRACK CREATION (root-motion recovery): a source track with keys in the graft window
    whose remapped (w0, w2) has no destination track is silently dropped by splice_motion_by_joint —
    for Goro that loses the ladder/item clips' baked root & hip channels, which his town .mot never
    carried. Create the destination track FIRST: rest anchors at both edges of every non-grafted span
    (`spans`), valued at the DEST node's BIND for that channel — bind translation row for chan 2,
    bind-rotation quaternion for chan 0 — i.e. exactly what the engine held while no track existed,
    so the grafted values cannot leak into idle/walk (the engine clamps a track past its outer keys).
    The splice that follows lands the window keys; _seal_graft seals the window edges. New tracks are
    inserted keeping (w0, w2) ascending. Returns [(node name, w0, chan), ...]."""
    remap = mc.build_joint_remap(sframes, dframes)
    have = {(t.w0, t.w2) for t in dst.tracks}
    created = []
    for st in src.tracks:
        if not st.frames_in(slo, shi):
            continue
        dw = remap.get(st.w0)
        if dw is None or (dw, st.w2) in have:
            continue
        _par, B, tr = _mds_bind(dpl, dw)
        v = _mat_to_quat(B) if st.w2 == 0 else (tr[0], tr[1], tr[2], 0.0)
        kfs = [mc.Keyframe(struct.pack('<4I4f', f, 0, 0, 0, *v))
               for lo, hi in spans for f in sorted({lo, hi})]
        nt = mc.Track(dw, st.w1, st.w2, st.w3, st.w6, st.w7, kfs)
        at = next((i for i, t in enumerate(dst.tracks) if (t.w0, t.w2) > (dw, st.w2)), len(dst.tracks))
        dst.tracks.insert(at, nt)
        have.add((dw, st.w2))
        created.append((dframes[dw], dw, st.w2))
    return created


def _graft(dst_pack, dst_mot, dst_mds, src_pack, src_mot, src_mds, slo, shi, dlo, dhi,
           root_motion=False, spans=None):
    dst = mc.Mot.from_record(dst_pack.find(dst_mot))
    src = mc.Mot.from_record(src_pack.find(src_mot))
    dframes = mc.read_mds_frames(dst_pack.find(dst_mds).payload)
    sframes = mc.read_mds_frames(src_pack.find(src_mds).payload)
    created = []
    if root_motion:                                  # opt-in: root alias + missing-track creation
        sframes = _alias_root(sframes, dframes)
        created = _create_missing_tracks(dst, src, sframes, dframes, slo, shi,
                                         dst_pack.find(dst_mds).payload, spans)
    mc.splice_motion_by_joint(dst, src, sframes, dframes, slo, shi, dlo, dhi)
    dst_pack.replace_payload(dst_mot, dst.rebuild()[dst.data_off:])
    return created


def _sample_track(t, frame):
    """The track's interpolated value at `frame`: lerp for translations, sign-corrected nlerp for rotation
    quaternions (visually equivalent to the engine's blend at keyframe-neighbor angular gaps)."""
    import math
    prev = max((k for k in t.keyframes if k.frame <= frame), key=lambda k: k.frame, default=None)
    nxt = min((k for k in t.keyframes if k.frame >= frame), key=lambda k: k.frame, default=None)
    if prev is None and nxt is None: return None
    if prev is None: return nxt.value
    if nxt is None or nxt.frame == prev.frame: return prev.value
    u = (frame - prev.frame) / (nxt.frame - prev.frame)
    a, b = prev.value, nxt.value
    if t.w2 == 0:                                     # quaternion
        if sum(x * y for x, y in zip(a, b)) < 0: b = tuple(-x for x in b)
        v = [ax + (bx - ax) * u for ax, bx in zip(a, b)]
        n = math.sqrt(sum(x * x for x in v)) or 1.0
        return tuple(x / n for x in v)
    return tuple(ax + (bx - ax) * u for ax, bx in zip(a, b))


def _bake_hold(pack, mot_name, src_frame, dlo, dhi):
    """Bake a STATIC held pose: sample every track at `src_frame` and write sealed keyframes at dlo AND dhi
    with that value (only ~1/5 of tracks keyframe any given source frame — the rest interpolate, so a plain
    window graft of a single frame would leave most joints drifting toward neighboring clips)."""
    m = mc.Mot.from_record(pack.find(mot_name))
    for t in m.tracks:
        v = _sample_track(t, src_frame)
        if v is None or not t.keyframes:
            continue
        t.keyframes[:] = [k for k in t.keyframes if not (dlo <= k.frame <= dhi)]
        for f in (dlo, dhi):
            kf = t.keyframes[0].copy()
            kf.frame = f
            struct.pack_into('<4f', kf.raw, 0x10, *v)
            t.keyframes.append(kf)
        t.keyframes.sort(key=lambda k: k.frame)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


def _seal_graft(dst_pack, dst_mot, dst_mds, src_pack, src_mot, src_mds, slo, shi, dlo, dhi,
                root_motion=False):
    """Guarantee every grafted track has explicit keyframes AT the window edges (dlo/dhi), valued by sampling
    the SOURCE at slo/shi. Sparse tracks (no kf exactly on an edge) otherwise interpolate across into the
    NEIGHBORING clips — a pop at loop wraps (Osmond's fall-loop) and pose bleed at clip starts (the old
    Ruby arm-raise). Runs after every graft, before reverse/root_offset."""
    dframes = mc.read_mds_frames(dst_pack.find(dst_mds).payload)
    sframes = mc.read_mds_frames(src_pack.find(src_mds).payload)
    if root_motion:
        sframes = _alias_root(sframes, dframes)
    remap = {i: dframes.index(n) for i, n in enumerate(sframes) if n in dframes}
    dm = mc.Mot.from_record(dst_pack.find(dst_mot))
    sm = mc.Mot.from_record(src_pack.find(src_mot))
    dtracks = {(t.w0, t.w2): t for t in dm.tracks}
    changed = False
    for st in sm.tracks:
        if st.w0 not in remap:
            continue
        dt = dtracks.get((remap[st.w0], st.w2))
        if dt is None or not dt.keyframes:
            continue
        for sf, df in ((slo, dlo), (shi, dhi)):
            if any(k.frame == df for k in dt.keyframes):
                continue
            v = _sample_track(st, sf)
            if v is None:
                continue
            kf = dt.keyframes[0].copy()
            kf.frame = df
            struct.pack_into('<4f', kf.raw, 0x10, *v)
            dt.keyframes.append(kf)
            dt.keyframes.sort(key=lambda k: k.frame)
            changed = True
    if changed:
        dst_pack.replace_payload(dst_mot, dm.rebuild()[dm.data_off:])


def _reverse_window(pack, mot_name, dlo, dhi):
    """Mirror the keyframes inside [dlo,dhi] (frame -> dlo+dhi-frame, re-sorted ascending) — bakes a clip that
    plays BACKWARDS (the engine cannot: both motion-speed override paths are gated positive)."""
    m = mc.Mot.from_record(pack.find(mot_name))
    for t in m.tracks:
        win = [kf for kf in t.keyframes if dlo <= kf.frame <= dhi]
        if not win:
            continue
        for kf in win:
            kf.frame = dlo + dhi - kf.frame
        t.keyframes.sort(key=lambda k: k.frame)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


def _apply_root_offset(pack, mot_name, dlo, dhi, off):
    """Add (dx,dy,dz) to the root-motion node's translation keyframes in [dlo,dhi] — shifts the whole pose in
    world space (used to pull the door reach back off the door). Root node = the first chan-2 track."""
    m = mc.Mot.from_record(pack.find(mot_name))
    rt = next((t for t in m.tracks if t.w2 == 2), None)
    if rt is None:
        return
    for kf in rt.keyframes:
        if dlo <= kf.frame <= dhi:
            v = list(kf.value)
            v[0] += off[0]; v[1] += off[1]; v[2] += off[2]
            struct.pack_into('<4f', kf.raw, 0x10, *v)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


def _inject_shadow(base, donor, inj):
    """Give a shadow-less base a shadow: copy the donor's shadow set (mds/bbp/wgt/mot) into the pack and
    declare it in the cfg (SHADOW_VERTEX_ANIME + SHADOW_MODEL after the MODEL line + a real SHADOW_MOTION).
    The copied .mot is stripped to frames <= inj['keep'] (each track keeps >=1 key so later splices find
    it) — every authored window is spliced by the slot grafts afterwards, the strip just drops the donor's
    unused clip bulk (Ruby: dun c05s.mot 470KB -> ~70KB). Built for Ruby's c05a-simple (its cfg says
    SHADOW_MOTION \"\", \"\", \"\")."""
    for key in ('model', 'bbp', 'wgt', 'mot'):
        r = donor.find(inj[key])
        if r is None:
            raise SystemExit(f"shadow_inject: donor lacks {inj[key]}")
        if base.find(r.name):
            raise SystemExit(f"shadow_inject: base already has {r.name}")
        base.records.append(mc.Record(r.name, r.data_off, r.size, r.stride, r.raw))
    keep = inj.get('keep')
    if keep:
        m = mc.Mot.from_record(base.find(inj['mot']))
        for t in m.tracks:
            kept = [k for k in t.keyframes if k.frame <= keep]
            t.keyframes[:] = kept or t.keyframes[:1]
        base.replace_payload(inj['mot'], m.rebuild()[m.data_off:])
    cfg = _cfg_motions(base)[0]
    pl = cfg.payload
    # canonical vanilla line order: VERTEX_ANIME / SHADOW_VERTEX_ANIME ... MODEL / SHADOW_MODEL
    va = pl.find(b'\r\n', pl.find(b'VERTEX_ANIME')) + 2
    pl = pl[:va] + b'SHADOW_VERTEX_ANIME 1\r\n' + pl[va:]
    eol = pl.find(b'\r\n', pl.find(b'MODEL ')) + 2                # after the MODEL line ("MODEL " with the
    pl = (pl[:eol]                                                # space so SHADOW_MODEL can't match first)
          + b'SHADOW_MODEL "%s"\r\n' % inj['model'].encode('latin1') + pl[eol:])
    pl = re.sub(rb'SHADOW_MOTION[^\r\n]*',
                b'SHADOW_MOTION "%s", "%s", "%s"' % tuple(inj[k].encode('latin1') for k in ('mot', 'bbp', 'wgt')),
                pl, count=1)
    base.replace_payload(cfg.name, pl)


def _freeze_roots(pack, mot_name, ref_frame, dlo, dhi):
    """Freeze TRANSLATION inside [dlo,dhi]: every chan-2 track with keys in the window gets all of them set
    to its own value sampled at dst frame `ref_frame` — kills baked root/hip travel (Ruby's e228 jump bakes
    the cutscene's 43u leap; its loop HOLDS the leap end offset) so the script owns world motion. The
    reference window must already be on the assembled timeline (list the reference slot first)."""
    m = mc.Mot.from_record(pack.find(mot_name))
    for t in m.tracks:
        if t.w2 != 2:
            continue
        win = [k for k in t.keyframes if dlo <= k.frame <= dhi]
        if not win:
            continue
        v = _sample_track(t, ref_frame)
        if v is None:
            continue
        for kf in win:
            struct.pack_into('<4f', kf.raw, 0x10, *v)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


def _pin_nodes(pack, mot_name, mds_name, nodes, ref, dlo, dhi):
    """Pin NAMED nodes to their `ref`-frame pose across [dlo,dhi]: drop their window keys and seal the
    edges with the sampled value (rotation AND translation). For a held prop that the clip's source rig
    animated under a different name (Ungaga's staff kon_1 vs c10b's weapon): without keys in the window
    the engine clamps the node toward its NEAREST timeline keys (his kon-pose grip at 226) — the staff
    'slides in his hand'. Pinned to the idle grip it rides the hand rigidly."""
    m = mc.Mot.from_record(pack.find(mot_name))
    frames = mc.read_mds_frames(pack.find(mds_name).payload)
    for t in m.tracks:
        if t.w0 >= len(frames) or frames[t.w0] not in nodes:
            continue
        v = _sample_track(t, ref)
        if v is None:
            continue
        t.keyframes[:] = [k for k in t.keyframes if not (dlo <= k.frame <= dhi)]
        for f in (dlo, dhi):
            kf = t.keyframes[0].copy()
            kf.frame = f
            struct.pack_into('<4f', kf.raw, 0x10, *v)
            t.keyframes.append(kf)
        t.keyframes.sort(key=lambda k: k.frame)
    pack.replace_payload(mot_name, m.rebuild()[m.data_off:])


def _rewrite_keys(cfg_payload, slots):
    """Replace the KEY_START..MOTION_END block's KEY lines with one KEY per slot (slot order = index)."""
    lines = b"".join(
        b"KEY\t%d,\t%d,\t%.2f, //%s\r\n" % (s["frames"][0], s["frames"][1], s["speed"], (s["name"][:12]).encode('latin1'))
        for s in sorted(slots, key=lambda s: s["idx"])
    )
    ks = cfg_payload.find(b'KEY_START')
    nl = cfg_payload.find(b'\r\n', ks) + 2           # after "KEY_START 0"
    me = cfg_payload.find(b'MOTION_END', nl)
    return cfg_payload[:nl] + lines + cfg_payload[me:]


def assemble(base_bytes, read_src, char):
    """read_src(name)->bytes reads any .chr from the ISO. Returns (new_base_chr, report)."""
    base = mc.Pack.parse(base_bytes)
    pad = char.get("pad_bbp")
    if pad:                                          # shift the RUNTIME arena layout: the engine's motion
        cfg0 = _cfg_motions(base)                    # buffer allocs bbp first, so padding the bbp record
        bbp = _cfg_bbp(cfg0[0].payload)              # moves every track + the KEY table by the same amount
        base.replace_payload(bbp, base.find(bbp).payload + b'\x00' * pad)   # (trailing bytes are never read)
    inj = char.get("shadow_inject")
    if inj:                                          # shadow FIRST so the graft loop sees smot/smds
        _inject_shadow(base, mc.Pack.parse(read_src(inj["src"])), inj)
    cfg, bmot, bmds, smot, smds = _cfg_motions(base)
    src_cache = {}
    def src(name):
        if name not in src_cache:
            src_cache[name] = mc.Pack.parse(read_src(name))
        return src_cache[name]
    grafts = 0
    mg = char.get("mesh_graft")
    mg_rep = None
    if mg:                                           # mesh-node graft FIRST: motion grafts then remap
        windows = [tuple(s["frames"]) for s in char["slots"] if s.get("src") == mg["src"]]
        maxframe = max(s["frames"][1] for s in char["slots"])
        mg_rep = _graft_mesh_nodes(base, src(mg["src"]), mg, windows, maxframe)
    rm = bool(char.get("root_motion"))               # opt-in root alias + missing-track creation
    rm_spans, rm_created = None, {"body": [], "shadow": []}
    if rm:
        owned = [tuple(s["frames"]) for s in char["slots"] if s.get("src") or s.get("hold") is not None]
        rm_spans = _fold_spans(owned, 1, max(s["frames"][1] for s in char["slots"]))
    for s in char["slots"]:
        dlo, dhi = s["frames"]
        touched = False
        if s.get("src"):
            sp = src(s["src"])
            scfg, sbmot, sbmds, ssmot, ssmds = _cfg_motions(sp, prefer=s.get("src_cfg"))
            if mg and s["src"] != mg["src"]:         # only the mesh-graft source may drive the new nodes
                clash = set(mg["nodes"]) & set(mc.read_mds_frames(sp.find(sbmds).payload))
                if clash:
                    raise SystemExit(f"slot {s['name']}: source rig carries grafted nodes {sorted(clash)} "
                                     f"but its window is outside the fold-span model")
            wlo, whi = s["win"]
            rm_created["body"] += _graft(base, bmot, bmds, sp, sbmot, sbmds, wlo, whi, dlo, dhi,
                                         root_motion=rm, spans=rm_spans)              # body
            _seal_graft(base, bmot, bmds, sp, sbmot, sbmds, wlo, whi, dlo, dhi, root_motion=rm)
            if smot and ssmot:                                                        # shadow (keep body+shadow in sync)
                rm_created["shadow"] += _graft(base, smot, smds, sp, ssmot, ssmds, wlo, whi, dlo, dhi,
                                               root_motion=rm, spans=rm_spans)
                _seal_graft(base, smot, smds, sp, ssmot, ssmds, wlo, whi, dlo, dhi, root_motion=rm)
            touched = True
        # post-ops apply to grafted AND kept (src=None) windows alike — a native window can be offset/
        # reversed/frozen/held in place (Ungaga's native talk-door gets a root_offset pull-back).
        if s.get("hold") is not None:                # static held pose sampled from the (post-graft) timeline
            _bake_hold(base, bmot, s["hold"], dlo, dhi)
            if smot: _bake_hold(base, smot, s["hold"], dlo, dhi)
            touched = True
        if s.get("root_offset"):                                                      # pull the reach back (door)
            _apply_root_offset(base, bmot, dlo, dhi, s["root_offset"])
            if smot: _apply_root_offset(base, smot, dlo, dhi, s["root_offset"])
            touched = True
        if s.get("reverse"):                                                          # bake a backwards-playing clip
            _reverse_window(base, bmot, dlo, dhi)
            if smot: _reverse_window(base, smot, dlo, dhi)
            touched = True
        if s.get("root_freeze") is not None:                                          # kill baked root travel
            _freeze_roots(base, bmot, s["root_freeze"], dlo, dhi)
            if smot: _freeze_roots(base, smot, s["root_freeze"], dlo, dhi)
            touched = True
        if s.get("pin_nodes"):                                                        # pin props to a ref pose
            pn = s["pin_nodes"]
            _pin_nodes(base, bmot, bmds, pn["nodes"], pn["ref"], dlo, dhi)
            touched = True
        if touched:
            grafts += 1
    if mg and mg.get("spin"):                        # AFTER the grafts/reverse bakes (they mirror windows)
        mg_rep["spin"] = _apply_spin(base, mg)
    base.replace_payload(cfg.name, _rewrite_keys(cfg.payload, char["slots"]))
    new_chr = base.rebuild()
    # guard: re-parse + confirm 10 KEYs
    chk = mc.Pack.parse(new_chr)
    _, keys = _cfg_motions(chk)[0], re.findall(rb'KEY[ \t]+\d+,', _cfg_motions(chk)[0].payload)
    rep = dict(grafts=grafts, keys=len(keys), size=len(new_chr))
    if mg_rep:
        rep["mesh_graft"] = mg_rep
    if rm:
        rep["root_tracks"] = rm_created
    return new_chr, rep


# ---------------------------------------------------------------- ISO install (tail redirect)
def _hd2_slot(hd2_r, i): return hd2_r["ext"] * SEC + 16 + i * 32
def _free_tail(f, dat_size, hd2_r, hed):
    mx = 0
    for i in range(len(hed) // 80):
        f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8))
        if 0 < off + size <= dat_size: mx = max(mx, off + size)
    return align(mx)


def run(iso, chars=CHARS, log=print):
    if not os.path.exists(iso):
        raise SystemExit(f"ISO not found: {iso}")
    with open(iso, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hd2_r, dat_r = recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC; dat_size = dat_r["size"]
        hed = ps2iso.read_file(f, recs["DATA.HED"])
        tail = _free_tail(f, dat_size, hd2_r, hed)
        def read_src(name):
            i = ps2iso.archive_find(hed, name)
            if i is None: raise SystemExit(f"{name} not in archive")
            f.seek(_hd2_slot(hd2_r, i)); off, size = struct.unpack("<II", f.read(8)); f.seek(dat_iso + off); return f.read(size)
        def redirect(name, data):
            nonlocal tail
            i = ps2iso.archive_find(hed, name)
            slot = _hd2_slot(hd2_r, i)
            if tail + len(data) > dat_size: raise SystemExit("out of DATA.DAT tail")
            f.seek(dat_iso + tail); f.write(data)
            sec, cnt = tail >> 11, (len(data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack("<IIII", tail, len(data), sec, cnt))
            f.seek(dat_iso + sec * SEC); assert f.read(len(data)) == data, f"{name} readback"
            log(f"redirected {name}: -> {len(data):,} B @sector {sec:#x}")
            tail = align(tail + len(data))
        for who, ch in chars.items():
            base = read_src(ch["base"])
            new_chr, rep = assemble(base, read_src, ch)
            log(f"{who}: {ch['base']} assembled — {rep['grafts']} grafts, {rep['keys']} KEYs, {len(base):,}->{rep['size']:,} B")
            redirect(ch["base"], new_chr)
        log("DONE (town-model assembly)")


def _verify_mesh_graft(base_bytes, new_chr, ch):
    """Deep-check a mesh-node graft: node counts/parents, byte-identity of every original record and
    mesh chunk (mesh offsets rebased by exactly delta), .bbp extension, blade tracks per heli clip,
    folded anchors outside the windows, shadow untouched, pack round-trip."""
    mg = ch["mesh_graft"]
    old, new = mc.Pack.parse(base_bytes), mc.Pack.parse(new_chr)
    cfg, bmot, bmds, smot, smds = _cfg_motions(new)
    opl, npl = old.find(bmds).payload, new.find(bmds).payload
    onames, nnames = mc.read_mds_frames(opl), mc.read_mds_frames(npl)
    nb, K = len(onames), len(mg["nodes"])
    delta = K * 0x70
    assert len(nnames) == nb + K, f"node count {len(nnames)} != {nb + K}"
    assert struct.unpack_from('<I', npl, 0x08)[0] == nb + K, "header count not updated"
    assert nnames[nb:] == mg["nodes"], "grafted node order/names wrong"
    for i in range(nb):                                    # original records byte-identical (offset rebased)
        o = opl[0x18 + i * 0x70:0x18 + i * 0x70 + 0x68]
        n = npl[0x18 + i * 0x70:0x18 + i * 0x70 + 0x68]
        omo = struct.unpack_from('<I', o, 0x20)[0]
        nmo = struct.unpack_from('<I', n, 0x20)[0]
        assert o[:0x20] == n[:0x20] and o[0x24:] == n[0x24:], f"node {i} {onames[i]} record changed"
        assert nmo == (omo + delta if omo else 0), f"node {i} mesh offset not rebased by 0x{delta:X}"
        if omo:
            sz = struct.unpack_from('<I', opl, omo + 8)[0]
            assert opl[omo:omo + sz] == npl[nmo:nmo + sz], f"node {i} {onames[i]} mesh chunk changed"
    mesh_bytes = 0
    for i in range(nb, nb + K):                            # grafted records: valid parents + real chunks
        par = struct.unpack_from('<i', npl, 0x18 + i * 0x70 + 0x24)[0]
        assert 0 <= par < i, f"grafted node {nnames[i]} parent {par} invalid"
        mo = struct.unpack_from('<I', npl, 0x18 + i * 0x70 + 0x20)[0]
        if mo:
            assert npl[mo:mo + 4] == b'MDT\x00', f"grafted node {nnames[i]} mesh not an MDT"
            mesh_bytes += struct.unpack_from('<I', npl, mo + 8)[0]
    bbp = _cfg_bbp(cfg.payload)
    obb, nbb = old.find(bbp).payload, new.find(bbp).payload
    assert len(nbb) == (nb + K) * 64 and nbb[:len(obb)] == obb, ".bbp not extended in place"
    # blade tracks: folded anchors outside windows, grafted keys inside each heli clip
    m = mc.Mot.from_record(new.find(bmot))
    blade = [t for t in m.tracks if t.w0 >= nb]
    assert blade and all(t.w0 < nb + K for t in blade), "blade track w0 out of range"
    windows = [tuple(s["frames"]) for s in ch["slots"] if s.get("src") == mg["src"]]
    maxframe = max(s["frames"][1] for s in ch["slots"])
    spans = _fold_spans(windows, 1, maxframe)
    for t in blade:
        vals = {kf.frame: kf.value for kf in t.keyframes}
        def _same(a, b):                              # chan-0: ±q is the same rotation (double cover)
            return (abs(sum(x * y for x, y in zip(a, b))) > 1.0 - 1e-6) if t.w2 == 0 else a == b
        for lo, hi in spans:
            assert lo in vals and hi in vals, f"blade w0={t.w0} chan{t.w2} missing fold anchor {lo}/{hi}"
            assert _same(vals[lo], vals[hi]) and _same(vals[lo], vals[spans[0][0]]), \
                f"blade w0={t.w0} fold anchors not constant"
    per_clip = {}
    for s in ch["slots"]:
        if s.get("src") != mg["src"]:
            continue
        dlo, dhi = s["frames"]
        per_clip[s["name"]] = sum(1 for t in blade if any(dlo <= kf.frame <= dhi for kf in t.keyframes))
    assert all(v for v in per_clip.values()), f"a heli clip landed no blade tracks: {per_clip}"
    # shadow rig/motion untouched by the mesh graft (blob shadow has no blade nodes)
    assert new.find(smds).raw == old.find(smds).raw, "shadow .mds changed"
    shn = len(mc.read_mds_frames(new.find(smds).payload))
    assert all(t.w0 < shn for t in mc.Mot.from_record(new.find(smot)).tracks), "shadow .mot got blade tracks"
    assert mc.Pack.parse(new_chr).rebuild() == new_chr, "pack round-trip failed"
    print(f"   mesh_graft OK: {nb}->{nb + K} nodes, +{mesh_bytes} mesh B, fold spans {spans}")
    print(f"   blade tracks: {len(blade)}; per heli clip: {per_clip}")
    if mg.get("spin"):
        _verify_spin(npl, nnames, m, mg, nb, K)


def _verify_spin(npl, nnames, m, mg, nb, K):
    """FK the propeller under the synthesized spin: in hub-LOCAL coordinates every subtree leaf must
    trace a circle about the spin axis at constant angular velocity across the loop window, wrap
    seamlessly, and the window boundaries must hand off continuously (hub at rest orientation mod
    360 at the ramp ends)."""
    spin = mg["spin"]
    hub = nnames.index(spin["node"])
    trk = {(t.w0, t.w2): t for t in m.tracks}

    def world(i, f):
        par, B, tr = _mds_bind(npl, i)
        q = _sample_track(trk[(i, 0)], f) if (i, 0) in trk else None
        R = _quat_to_mat(q) if q else B
        v = _sample_track(trk[(i, 2)], f) if (i, 2) in trk else None
        t3 = list(v[:3]) if v else tr
        if par < 0:
            return [list(r) for r in R], t3
        PR, PT = world(par, f)
        return ([[sum(R[r][k] * PR[k][c] for k in range(3)) for c in range(3)] for r in range(3)],
                [sum(t3[k] * PR[k][c] for k in range(3)) + PT[c] for c in range(3)])

    kids = {}
    for i in range(nb, nb + K):
        kids.setdefault(_mds_bind(npl, i)[0], []).append(i)
    desc, stack = [], [hub]
    while stack:
        for c in kids.get(stack.pop(), []):
            desc.append(c); stack.append(c)
    tips = [i for i in desc if i not in kids]
    ax = spin.get("axis", (0.0, 1.0, 0.0))
    u = (1.0, 0.0, 0.0) if abs(ax[0]) < 0.9 else (0.0, 0.0, 1.0)   # basis perpendicular to the axis
    u = [u[i] - ax[i] * sum(a * b for a, b in zip(u, ax)) for i in range(3)]
    un = math.sqrt(sum(x * x for x in u)); u = [x / un for x in u]
    v = [ax[1] * u[2] - ax[2] * u[1], ax[2] * u[0] - ax[0] * u[2], ax[0] * u[1] - ax[1] * u[0]]
    lo, hi = spin["loop"]
    rate = spin.get("rate", 360.0 / (hi - lo))
    hub_par, hub_bind, _t = _mds_bind(npl, hub)
    for tip in tips:
        loc = []
        for f in range(lo, hi + 1):
            # hub REST frame = bind local rotation x parent world (the hub frame WITHOUT the spin
            # track) — expressing the tip in the spinning hub's own frame would cancel the spin.
            PR, _pt = world(hub_par, f)
            RR = [[sum(hub_bind[r][k] * PR[k][c] for k in range(3)) for c in range(3)] for r in range(3)]
            _hr, HP = world(hub, f)
            _, TP = world(tip, f)
            rel = [TP[k] - HP[k] for k in range(3)]
            loc.append([sum(rel[k] * RR[c][k] for k in range(3)) for c in range(3)])   # rest-frame local
        axial = [sum(p[k] * ax[k] for k in range(3)) for p in loc]
        rad, ang = [], []
        for p in loc:
            x = sum(p[k] * u[k] for k in range(3)); z = sum(p[k] * v[k] for k in range(3))
            rad.append(math.hypot(x, z)); ang.append(math.degrees(math.atan2(z, x)))
        steps = [(ang[i + 1] - ang[i]) % 360.0 for i in range(len(ang) - 1)]
        steps = [s - 360.0 if s > 180.0 else s for s in steps]
        wrap = math.sqrt(sum((a - b) ** 2 for a, b in zip(loc[0], loc[-1])))
        assert max(axial) - min(axial) < 0.02, f"tip {nnames[tip]} wobbles along the axis"
        assert max(rad) - min(rad) < 0.02, f"tip {nnames[tip]} radius varies {min(rad):.3f}..{max(rad):.3f}"
        assert all(abs(abs(s) - rate) < 0.5 for s in steps), f"tip {nnames[tip]} angular velocity uneven: {steps}"
        assert wrap < 1e-3, f"tip {nnames[tip]} loop wrap discontinuous ({wrap:.4f})"
        print(f"   spin tip {nnames[tip]}: r={sum(rad)/len(rad):.3f} (dev {max(rad)-min(rad):.5f}), "
              f"step {min(abs(s) for s in steps):.2f}..{max(abs(s) for s in steps):.2f} deg/f, wrap err {wrap:.2e}")
    # boundary continuity: ramps start/end at the rest orientation (mod 360)
    ht = trk[(hub, 0)]
    qb = _mat_to_quat(_mds_bind(npl, hub)[1])
    for label, fa in [("rampup start", spin["rampup"][0]), ("rampdown end", spin["rampdown"][1])]:
        d = abs(sum(a * b for a, b in zip(_sample_track(ht, fa), qb)))
        assert d > 1.0 - 1e-6, f"spin boundary {label} != rest: |dot|={d:.6f}"
    h = spin.get("handoff_advance", 0.0)
    if h:
        # DISPLAYED-sequence hand-offs: simulate playback at the override (one engine frame advances
        # h/rate clip frames) straight through both window switches — every displayed step must keep
        # advancing by ~h degrees, the switch steps and loop steps exactly so; a stall reads 0.
        sc = h / rate                                # engine step in clip frames (0.5 at HeliFlightSpeed)
        def qang(a, b):
            return math.degrees(2 * math.acos(min(1.0, abs(sum(x * y for x, y in zip(a, b))))))
        up_end, dn_lo = spin["rampup"][1], spin["rampdown"][0]
        # loop's last displayed frame before the switch sits one step BEFORE the window end (the C#
        # phase-locks flight to whole loop periods, so its phase is the loop-start phase - h ≡ 0)
        seqs = [("rampup->loop", [up_end - sc * (3 - i) for i in range(4)] + [lo + sc * i for i in range(4)],
                 range(0, 3), range(4, 7)),          # ramp-side steps, loop-side steps
                ("loop->rampdown", [hi - sc * (4 - i) for i in range(4)] + [dn_lo + sc * i for i in range(4)],
                 range(4, 7), range(0, 3))]
        for label, seq, rampside, loopside in seqs:
            qs = [_sample_track(ht, f) for f in seq]
            steps = [round(qang(a, b), 2) for a, b in zip(qs, qs[1:])]
            print(f"   spin hand-off {label}: displayed Δ deg/engine-frame = {steps} (switch step = {steps[3]})")
            assert all(s > 1.0 for s in steps), f"{label}: zero-step (stall) in {steps}"
            assert abs(steps[3] - h) < 0.05, f"{label}: switch step {steps[3]} != {h}"
            assert all(abs(steps[i] - h) < 0.05 for i in loopside), f"{label}: loop-side steps off {h}: {steps}"
            assert all(abs(steps[i] - h) < 2.0 for i in rampside), \
                f"{label}: ramp-side step >2° from {h} next to the switch: {steps}"
    steps_all = [k.frame for k in ht.keyframes]
    for i in range(len(steps_all) - 1):     # nlerp safety: adjacent keys < 90 deg apart
        d = abs(sum(a * b for a, b in zip(_sample_track(ht, steps_all[i]), _sample_track(ht, steps_all[i + 1]))))
        assert d > math.cos(math.radians(45.1)), "adjacent hub keys >= 90 deg apart"
    print(f"   spin OK: hub {spin['node']} w0={hub}, {len(ht.keyframes)} keys, rate {rate:.1f} deg/f"
          + (f", hand-offs advance {h:.0f} deg/engine-frame" if h else ", ramps end at rest"))


def _verify_root_motion(base_bytes, new_chr, ch):
    """Deep-check the root-motion opt-in: created tracks exist only where sources demanded them, rest
    anchors reproduce the dest BIND outside every grafted window, and the climb windows carry the
    hip root motion. Prints the climb x/y envelopes (the zigzag/bounce numbers)."""
    old, new = mc.Pack.parse(base_bytes), mc.Pack.parse(new_chr)
    _cfg, bmot, bmds, smot, smds = _cfg_motions(new)
    owned = [tuple(s["frames"]) for s in ch["slots"] if s.get("src") or s.get("hold") is not None]
    spans = _fold_spans(owned, 1, max(s["frames"][1] for s in ch["slots"]))
    for tag, motn, mdsn in [("body", bmot, bmds), ("shadow", smot, smds)]:
        if not motn or old.find(motn) is None:        # injected shadow: no old-base counterpart to diff
            continue
        om = mc.Mot.from_record(old.find(motn))
        nm2 = mc.Mot.from_record(new.find(motn))
        pl = new.find(mdsn).payload
        frames = mc.read_mds_frames(pl)
        have = {(t.w0, t.w2) for t in om.tracks}
        created = [t for t in nm2.tracks if (t.w0, t.w2) not in have]
        assert all(t.w0 < len(frames) for t in nm2.tracks), f"{tag}: track w0 out of rig range"
        for t in created:
            _p, B, tr = _mds_bind(pl, t.w0)
            rest = _mat_to_quat(B) if t.w2 == 0 else (tr[0], tr[1], tr[2], 0.0)
            vals = {k.frame for k in t.keyframes}
            for lo, hi in spans:                       # anchors present at every non-grafted span edge
                assert lo in vals and hi in vals, \
                    f"{tag} {frames[t.w0]} c{t.w2}: missing rest anchor {lo}/{hi}"
            for k in t.keyframes:                      # every key OUTSIDE grafted windows == bind rest
                if any(lo <= k.frame <= hi for lo, hi in owned):
                    continue
                if t.w2 == 0:
                    ok = abs(sum(a * b for a, b in zip(k.value, rest))) > 1.0 - 1e-6
                else:
                    ok = all(abs(a - b) < 1e-5 for a, b in zip(k.value, rest))
                assert ok, f"{tag} {frames[t.w0]} c{t.w2} @f{k.frame}: rest anchor != bind {rest}"
        print(f"   root_motion {tag}: created {[(frames[t.w0], t.w0, 'c%d' % t.w2) for t in created]}"
              f" — rest anchors == bind at {len(spans)} spans")
    # Goro-only deep check — climb windows: hip (null69) root motion present; print the bounce envelopes
    bm = mc.Mot.from_record(new.find(bmot))
    frames = mc.read_mds_frames(new.find(bmds).payload)
    hip = next((t for t in bm.tracks if frames[t.w0] == 'null69' and t.w2 == 2), None)
    if hip is not None:
        for label, lo, hi in [("climb-hopA 185-205", 185, 205), ("climb-hopB 210-230", 210, 230),
                              ("climb both 185-230", 185, 230)]:
            w = hip.frames_in(lo, hi)
            assert len(w) >= 10, f"{label}: hip chan-2 carries only {len(w)} keys"
            xs = [k.value[0] for k in w]; ys = [k.value[1] for k in w]; zs = [k.value[2] for k in w]
            print(f"   climb hip(null69) c2 {label}: keys={len(w)} x[{min(xs):.3f},{max(xs):.3f}] "
                  f"y[{min(ys):.2f},{max(ys):.2f}] z[{min(zs):.2f},{max(zs):.2f}]")
            assert max(ys) - min(ys) > 1.0, f"{label}: no bounce amplitude in y"
    assert mc.Pack.parse(new_chr).rebuild() == new_chr, "pack round-trip failed"


# APPROVED builds (field-validated): --test fails loudly if a change drifts them. Update these pins
# only when a new build for that character is deliberately approved.
# (re-pinned 2026-09-06: replace_payload now pads strides to 16 like vanilla — the unaligned-cfg fix
# that solved Ruby's garbled load; Xiao/Osmond gained pad bytes only.)
APPROVED = {"Xiao": (710128, "3d82e365d04aa51a"), "Osmond": (802656, "c39758570722762a")}


def _test():
    """Assemble from the extracted disc (mot_codec.load_pack) and verify KEY table + grafted frames."""
    import hashlib
    for who, ch in CHARS.items():
        base_bytes = mc.read_subfile(ch["base"].replace('/', '\\'))[2]
        def read_src(name): return mc.read_subfile(name.replace('/', '\\'))[2]
        new_chr, rep = assemble(base_bytes, read_src, ch)
        print(f"{who}: {rep}")
        if who in APPROVED:
            size, sha = APPROVED[who]
            got = hashlib.sha256(new_chr).hexdigest()[:16]
            assert (len(new_chr), got) == (size, sha), \
                f"{who} drifted from the APPROVED build: {len(new_chr)} B sha {got} != {size} B sha {sha}"
            print(f"   APPROVED build byte-identical: {size:,} B sha {sha}")
        if ch.get("mesh_graft"):
            _verify_mesh_graft(base_bytes, new_chr, ch)
        if ch.get("root_motion"):
            _verify_root_motion(base_bytes, new_chr, ch)
        chk = mc.Pack.parse(new_chr)
        cfg = _cfg_motions(chk)[0]
        keys = re.findall(rb'KEY[ \t]+(\d+),[ \t]*(\d+),[ \t]*([\d.]+),?[ \t]*//([^\r\n]*)', cfg.payload)
        for i, (a, b, s, nm) in enumerate(keys):
            print(f"   #{i} {a.decode()}-{b.decode()} sp={s.decode()} {nm.decode('shift_jis','replace')[:14]}")
        # verify a grafted body frame exists (run @120, land @215)
        m = mc.Mot.from_record(chk.find(_cfg_motions(chk)[1]))
        fr = set(kf.frame for t in m.tracks for kf in t.keyframes)
        print(f"   body frames now include 120(run)={120 in fr} 150(door)={150 in fr} 205(fall)={205 in fr} 215(land)={215 in fr}")


def main():
    if "--test" in sys.argv: _test(); return
    iso = DEFAULT_ISO
    if "--iso" in sys.argv: iso = sys.argv[sys.argv.index("--iso") + 1]
    run(iso)


if __name__ == "__main__":
    main()
