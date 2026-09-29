# Effect rendering — how the game draws `.chr` effects (RE)

What an effect pack (dun\effect, wep_eff, monster companions such as c17_hikari, cutscene props) carries, and how
the engine turns it into pixels. The effects atlas (`game_data/viewers/effects/effect_viewer.py`, untracked) reproduces
these rules; this is the reference for it.

## Per-node render flags — the node-name suffix

`LoadMDSFile` (0x1262B0) copies each MDS node name to `CFrame+0x118` and `SetFrameAttr` (0x125EF0) reads every letter
after the first `__` as a flag (case-insensitive); `CFrameVu1::DrawVu1` (0x129400) folds them into the GS TEST/ZBUF/ALPHA
registers per draw. Digits after other letters (`czapp_8_`) are name uniqueness only.

| letter | effect |
|---|---|
| `c` | unlit: constant vertex colour 128 (texel × material, no lighting) |
| `z` | ZBUF.ZMSK = 1: no depth write |
| `o` | TEST.ZTST = ALWAYS: no depth test |
| `s` | backface cull on |
| `n` | two-sided (the default is already two-sided; `n` forces it) |
| `a` + 2 hex | TEST.AREF alpha-test reference (`a01` = discard alpha < 1) |
| `a` + `pp` | ALPHA 0x48: additive blend (src × srcA + dst) |
| `a` + `nn` | ALPHA 0x42: subtractive blend (dst − src × srcA) |
| `b` + `a` | billboard, full camera-facing |
| `b` + `y` | billboard about the Y axis |
| `m` | per-vertex fog / eye-vector VU1 path |

Census over the 451 effect packs: `czapp` (unlit, no z-write, additive) and `cappz` are by far the commonest, then
`czappba` (billboarded additive sprites), `czann` (subtractive smoke/shadow), `cz` (unlit alpha-blended).

## Textures — two bank kinds

A pack's `.img` is a bank of named TIM2 pictures (8-bit indexed, 256-colour CLUT in CSM1 order: index bits 3 and 4
swap before use). The bank magic says how the PIXELS are stored: `IM2\0` banks keep them in the GS PSMT8 32-bit block
order (the classic vertical-stripe look if read row-major) — Goro's and the Dark Genie's bodies, most dungeon effects,
monster companions and cutscene props, 394 of 405 measured; `IMG\0` banks are row-major (Toan, Xiao, f_boll…). No TIM2
header byte differs (GsTex0 is zero everywhere); the magic is the rule. `extract_model.tim2_rgba(block, swizzled=…)`
takes it, `unswizzle8` does the block un-swizzle.

## Materials (MDT +0x38, 0x60 B each)

`+0x00 f32[3]` colour, `+0x0C f32` alpha (c17_hikari's discs start at 0.5), `+0x10 f32[3]` second colour, `+0x30 f32`
50.0 (constant), `+0x34` texture name. Colour and alpha are the base values the material tracks animate.

## Motion tracks (`MotionProc` 0x147D20, switch on track w2)

| w2 | what | value |
|---|---|---|
| 0 | rotation | quaternion (w, x, y, z), slerped |
| 1 | scale | (x, y, z) → `SetScale` on the node |
| 2 | translation | (x, y, z) → node local +0x200 |
| 0xC | VERTEX animation | w1 = 1-based vertex index into the node's MDT positions; value = the vertex's local position; consecutive tracks of one node are written together and set `CFrame+0xBA = 1` |
| 0x28 | material alpha | w1 = material index; material +0x0C = 1 − value (the track holds transparency) |
| 0x29 | material colour | w1 = material index; material +0x00..08 = value |
| 0x1E / 0x1F / 0x20 / 0x21 | camera position / reference / roll / fov (cutscenes) | |
| 0x32 / 0x33 | node flag word +0xB0 (0/3 and 2/1 by value < 1): visibility | |
| 20 | (.wgt) skin weights | |

Census: 0 ×6183, 1 ×3902, 2 ×5417, 0xC ×8861, 0x28 ×4023, 0x32/0x33 ×18. Effects are mostly vertex-morphed, scaled and
faded through material alpha. Only 46 of ~6000 effect meshes carry vertex colours.

## Texture animation (cfg)

```
TEX_ANIME <group>, <enabled>
TEX_SCROLL_DATA "<dst tex>", x, y, w, h, "<src tex>", sx, sy, dx, dy, <short>, <flag>
TEX_ANIME_END
```

`CommandTEX_SCROLL_DATA` (0x167FA0) registers a `CTexAnimeData`; `TexAnime` (0x167170) runs once per game frame
(from character draws only: DrawMonstor, EdDrawCharacter, MainDraw — never from `Draw__12CSHOT_EFFECT`, so an effect
fired as a shot shows its scroll static): it copies the source rectangle (sx, sy, w, h) of the source texture into the
destination rectangle (x, y, w, h) of the destination texture with an accumulated offset (`+0x4C += dx`, `+0x50 += dy`,
each wrapped into [0, w) / [0, h)), in up to four `MGMoveImage` pieces so the wrap is seamless. f_boll: the right half of
its 256-wide texture is the left half scrolling up 4 texels a frame. The atlas does the same as a UV remap in the shader.

## Playback

The cfg's `KEY start, end, speed` windows: the frame cursor advances `speed` motion frames per 60 Hz tick across the
window. Keyframes are sparse and linearly interpolated (rotation slerped); outside a track's range the end values hold.
Billboards rebuild the node's world rotation toward the camera each draw, keeping its translation and scale.
