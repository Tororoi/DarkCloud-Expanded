# Town-swap animation map

Per-character plan for what animation each swapped-in ally plays in town. The town character plays a
**fixed motion index** into the loaded model's KEY table (see [mot-format §8](../game_data/docs/mot-format.md)),
so for each ally we fill the town-`c01d` slots below with a clip chosen (in the model viewer) from that
character's own models. Clips come from several source models per character; the plan is to load one
verified-**cutscene-safe** base model per ally and transplant the chosen clips into the right slots
(same-rig, by joint name — the tool in `tools/iso_patch/transplant_battle_run.py`).

## Town motion slots (what the town system plays)

`chara\c01d.chr` (Toan's town model) — the indices the town engine actually plays:

| idx | trigger |
|---|---|
| 0 | idle (standing) |
| 1 | run (stick > 0.85) |
| 2 | walk |
| 3 | door — push-open |
| 4 | door — pull-open |
| 5 | item get |
| 6 | item get (loop) |
| 7 | damage — **never triggered in town; skip for all allies** |
| 8 | **fall** (walked off a ledge) |
| 9 | **land** |

Ladders are a **separate model** (`chara\c01dhashigo.chr` for Toan), driven by a system-event script — not
a slot in the body model. Handled per-character below (baseline: don't crash; then the sequences noted).

Motion source refs are `model · scene #id` as shown in the viewer.

---

## Xiao (cat form) — cataloged 2026-09-04

**Motion source models** (all `c04cat` rig, cat form): `c04cat · s86` (base — best idle/walk/run + jump),
`e17c04cat · s99` (doors/talk), `e04c04cat · e01` (ladder-up jump).

`c04cat · s86` table: `#0 stand · #1 sit · #2 walk · #3 ready(brace) · #4 run · #5 take-off · #6 leap · #7 land`.

### Town slots
| Town slot | Source clip | Notes |
|---|---|---|
| 0 idle | `c04cat · s86 #0` (stand) | **after a few seconds standing, switch to `#1` sit** — the cat sits when idle (special behavior, below) |
| 1 run | `c04cat · s86 #4` | |
| 2 walk | `c04cat · s86 #2` | |
| 3 push-door | `e17c04cat · s99 #5` (+`#6` hold-loop) | `#5` = the reach/finish, `#6` = looping hold of its end pose |
| 4 pull-door | `e17c04cat · s99 #5` (+`#6` hold-loop) | same clip for both doors |
| 5 item get | `c04cat · s86 #1` (sit) | confirmed |
| 6 item get (loop) | `c04cat · s86 #1` held | the sit, held |
| 7 damage | — | **not needed — town characters never take damage** (applies to every ally) |
| 8 fall | `c04cat · s86 #6` (leap) | mid-air of the jump |
| 9 land | `c04cat · s86 #7` (land) | matches Toan `c01d #9` |

### Refusal / "no" (used when an action is rejected)
`e613c04cat · d01 #6` (no) → `#7` (hold) → `#8` (return to idle). Frames 115-165.

### Jump / fall-land
`c04cat · s86` `#3 ready → #5 take-off → #6 leap → #7 land` is a full jump. The fall/land slots reuse `#6`/`#7`.

### Ladders
- **Down a ladder:** the full jump `c04cat · s86 #3 → #5 → #6 → #7`.
- **Up a ladder:** `c04cat · s86 #3` (ready) → `e04c04cat · e01 #5` (float/hop-up, **played much faster**) →
  `c04cat · s86 #6 → #7` (leap + land at the top). One clean hop up.

### Talk / read sign (nice-to-have; no vanilla town anim)
`e17c04cat · s99 #5` (+`#6` hold) — a look/lean that reads as attention.

### Special behaviors
- **Idle → sit:** hold `#0` stand on entering idle; after ~N seconds with no input, cross to `#1` sit. Needs a
  small idle-timer in the town character step (not just a slot fill).

### Implementation notes
- **Safe base:** `c04pcat · e01` is verified cutscene-safe (nothing animates it; the reservation trace). `c04cat · s86`
  itself is referenced by `s86/event.stb` (safety unverified), so prefer **loading `c04pcat` and transplanting the
  chosen `s86`/`s99`/`e01` clips into its slots**, rather than loading `s86 c04cat` directly. Confirm the three
  source `c04cat` rigs share `c04pcat`'s node table (they should — same cat rig) before splicing.
- Slots to fill by transplant: 1(run), 2(walk), and the new 3/4/5/6/8/9 + the ladder clips; 0(idle)+idle-timer.

---

## Ungaga — cataloged 2026-09-05

**Base model:** `c10p · s32` (cutscene-safe + has his cloth). Motions transplanted from `c10b · dun\mainchara`
(the dungeon body) plus `c10p`'s own `#3`.

`c10b` refs: `#0 idle · #1 run · #2 walk · #6 damage-big (295-334, falls backward → #7 get-up 315-334) ·
#29 NG-pose/refusal (490-530) · #34 get-item-in (630-640) · #35 get-item-loop (643-653)`.

### Town slots
| Town slot | Source clip | Notes |
|---|---|---|
| 0 idle | `c10b #0` | |
| 1 run | `c10b #1` | |
| 2 walk | `c10b #2` | |
| 3 push-door | `c10p #3` (talk) | c10p's own talk anim |
| 4 pull-door | `c10p #3` (talk) | |
| 5 item get | `c10b #34` | |
| 6 item get (loop) | `c10b #35` | |
| 7 damage | — | skip |
| 8 fall | `c10b #6` **reversed, from frame 297** | falling forward (the recoil played backward) — *custom clip, bake* |
| 9 land | `c10b #6` **frames 297→295** (reversed) | short settle — *custom clip, bake* |

### Refusal / "no"
`c10b #29` (NG pose, 490-530).

### Talk / read sign
`c10p #3` (talk).

### Ladders
- **Down a ladder:** `c10b #6` played *forward* (295 = fall backward → …), **hold frame 315** as the grounded
  "falling" state — so Ungaga just *falls* down ladders (get-up `#7`/315-334 can finish at the bottom). Joke-adjacent.
- **Up a ladder:** `c10b #29` (NG pose) — Ungaga **refuses** to climb up ("he doesn't like ladders"). No climb.

### Custom clips to bake (the town engine only plays a KEY forward, so these need pre-baked clips)
- fall = `c10b #6` keyframes reversed, window anchored at frame 297.
- land = `c10b #6` reversed, 297→295.
- down-ladder = `c10b #6` forward through 315, then hold 315.
(Exact frames/direction are a visual judgment — I'll bake them and you verify in the viewer/in-game.)

---

## Ruby — TBD
## Goro — TBD
## Osmond — cataloged 2026-09-05

**Base model:** `c18p · e05` (`#0 idle · #1 run · #2 walk · #3 talk`). Idle/walk fine as-is; **run = just speed up
`#1`** (no better run exists — bump the KEY speed). Extra clips from `e403c18a · e05`, `e402c18a · s13`,
`e409c18a · s31`.

Source refs: `e403c18a·e05 #9 jump-down(185-205) · #10 fall-loop(210-218) · #11 land(220-235)`;
`e402c18a·s13 #16 propeller-out(280-335) · #17 start-fly(345-360) · #18 fly-loop(360-370) ·
#19/20/21 both-hands-talk in/loop/out(380-400) · #35/36/37 crossed-arms in/loop/→talk(564-598)`;
`e409c18a·s31 #12 "giant launch (in)"(225-237)` (reads as knocking when looped).

### Town slots
| Town slot | Source clip | Notes |
|---|---|---|
| 0 idle | `c18p #0` | keep (also cutscene-reserved) |
| 1 run | `c18p #1`, **sped up** | KEY-speed bump |
| 2 walk | `c18p #2` | fine as-is |
| 3 push-door | `c18p #3` (talk) | **resolved: reuse the talk clip for doors** — keeps idx 3 intact for the s2201 talk scene |
| 4 pull-door | `c18p #3` (talk) | same (or `e409c18a·s31 #12` knock if we ever add idx 4 separately) |
| 5 item get | `c18p #3` **held at frame 69** (hand out) | *custom clip, bake* |
| 6 item get (loop) | `c18p #3` held at frame 69 | |
| 7 damage | — | skip |
| 8 fall | `e403c18a·e05 #10` (fall-loop) | |
| 9 land | `e403c18a·e05 #11` (land) | |

> ⚠ **idx 3 conflict:** the reservation trace found `c18p` idx 3 (talk) is played by a **replayable** talk-to-Osmond
> event (`s13/s2201`). Town push-door is idx 3, so putting the knock there makes Osmond knock when you talk to him
> in that scene. Options: (a) accept that minor replayable glitch; (b) keep idx 3 = talk and let push-doors play the
> talk anim (odd but unbroken); (c) duplicate c18p. **Needs your call.** (Pull-door at idx 4 is a safe append.)

### Refusal / "no"
`e402c18a·s13 #35` (crossed-arms in) → **`#36` (loop ×N — "not impressed")** → `#37` (→talk) → `#21` (out). *scripted seq.*

### Talk / read sign
`e402c18a·s13 #19` (in) → `#20` (loop) → `#21` (out).

### Ladders
- **Down:** `e403c18a·e05 #9 jump-down → #10 fall-loop → #11 land`.
- **Up (helicopter backpack):** `e402c18a·s13 #16 propeller-out → #17 start-fly → #18 fly-loop` (loop `#18` while
  climbing) → at the top **reversed `#17` → reversed `#16`** (land + stow the backpack). *scripted seq; rev clips baked.*

### Custom clips to bake
- item-get = `c18p #3` clamped/held at **frame 69**.
- up-ladder landing = reversed `#17`, reversed `#16`.
- run = `c18p #1` at higher KEY speed (data-only).

---

---

## Cross-cutting implementation notes
- **Custom behaviors** beyond simple slot-fills, to build in the town character code / ladder script:
  - Xiao **idle→sit** timer (hold `#0`, cross to `#1` after N seconds idle).
  - **Refusal** animation hook (Xiao `e613 #6/7/8`, Ungaga `c10b #29`) — play on a rejected action (e.g. Ungaga's
    up-ladder).
  - **Ladder sequences** (Xiao hop-up/down; Ungaga fall-down / refuse-up) — the ladder is a system-event script,
    so these are scripted motion sequences, not single slots.
  - **Reversed / held-frame** clips (Ungaga fall/land/down-ladder) — bake with `mot_codec` (reverse keyframe order +
    remap frame indices, or clamp to a hold frame).
- **Same-rig check:** before splicing, confirm each source model shares the base model's `.mds` node table
  (Xiao: `s86`/`s99`/`e01` `c04cat` → `c04pcat`; Ungaga: `c10b` → `c10p`). Same character/form, so expected to match.
