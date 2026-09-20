# Divine Beast Title — the cat shot

Reference for `Weapons/Xiao/DivineBeastCat.cs` and its ELF caves (`tools/stubs/cat_*.s`, `tools/iso_patch/build_cat_pack.py`).
The mod side is five classes that share their members through `using static`: `DivineBeastCat` (the constants, the looks, the
thread, charge and launch, the hit, the heap watch), `CatCopy` (the copy and her MOTION 1 channel), `CatFlight` (the cave
handshake, aim, the hit's element, the flight step), `CatCape` (the cape cloth) and `CatTextures` (the texture block).

The code comments describe current behaviour only. This file holds the findings behind decisions that are no longer
obvious from the code — why a thing is done the way it is, and what does not work — so the reasoning survives without
being retold inline.

## Cape / cloth (CCloth)

- **The wind must be a SHAPE, not a force.** A force was tried first — gravity aimed from the cat's face, since the
  engine's own wind is undirected noise — and every version bunched the cape into a vertical spike. The cause is
  structural: the collar is pinned, so displacement from the rest shape grows from nothing at the collar to everything
  at the hem, and the spring pulls hardest exactly where the wind carried the cloth furthest. The sheet goes into
  compression along its length and buckles. No tuning removes it. Writing the blown shape into the REST array instead
  leaves the spring nothing to fight.
- **Write the rest shape, never the velocity array.** The rest (+0x110) is the only per-particle field the engine reads
  and never writes during play. Writing velocities means reading 3 KB the engine owns and handing it back several
  engine steps later; on the bind frame the engine ZEROES velocities for the teleport and the mod handed the
  pre-teleport ones straight back — a cloth explosion that stretched the cape across the screen when the cat fired.
- **Tear detection must measure the sheet, not the cat.** A cat riding its pellet covers 5 u/frame, so the cloth
  legitimately trails far behind it; testing distance-from-rest put the cape into a reseat 16 times a second for a whole
  flight. The cape's own collar-to-hem span is ~5 units (≤10 under load) and the tears seen in the log were 40–54.
- **Packet size comes from the cloth's own figure** (+0x1C, CreateVUData's return, 16-byte units). A hardcoded 0x2000
  fallback gave a 12×16 lattice 8,192 B when it needed 17,760 — the engine wrote 9.5 KB past the end and the game
  jumped into garbage.
- The mask's private vtable was found only because the guard checked: the vptr is at **+0x08**, not offset 0 as single
  inheritance usually puts it.

## Textures / VRAM

- The struck-glyph investigation: `MoveCatVram` was switched OFF for one build to test whether the cat's VRAM move was
  responsible. It was not — the glyphs stayed struck, and in that run Xiao was never switched in, so nothing ran.
- Claiming the window without moving the manager's cursor (+0x14, a DOWNWARD bump allocator) let later textures land on
  top of the cat's pages; fonts are entered that way and came back speckled.
- A script event's `DeleteTextureBlock` wipes the cat entries while they are tagged to the slot group: after the chasm
  jump every spawn found 0 entries and the copy rebuilt every half second.
- An address-keyed restore broke when the manager's entry list shifted underneath it — textures stayed garbled until a
  party switch rebuilt the manager. Restores are keyed by NAME.

## Build cost (why the work moved into the machine)

Spawn was 9.4 s when every field crossed PINE individually. Moving the bulk copy into ElfCave's CatCopyQueue removed
~600 KB of traffic at ~100 KB/s; scanning the already-in-hand bytes instead of re-reading 300 KB took the texture
re-tag from 5.0 s to negligible. The build is now under a second.

## Behaviour decisions

- The menu does NOT wipe the cat's texture entries (measured: 8/8 registered on open, throughout, and on close), so the
  cat is held through it exactly as the PAUSE screen holds it rather than being despawned.
- Arming happens on RELEASE, not at charge-ready, so a cat already out survives until the next shot is actually fired.
- The float-up's play rate lives in its KEY entry, not the speed override: Step's play-once stop test looks ahead by the
  KEY rate while the advance uses the override, so a faster override overshoots and the clip wraps.

## Cape wind — the remaining geometry

These sat above `CapeWindLift` / `CapeRippleReach` / `CapeRippleRows` in code. The wind is settled and not intended to
change further, so the reasoning lives here and the constants carry one-line descriptions.

**Everything is in the ANCHOR's frame**, so the shape follows the cat's spine with no facing maths: −x runs down the cape
away from the collar, +x back toward it; −y lifts off the back. GRAVITY stays zero.

**The sheet cannot LENGTHEN.** Its rest distances were fixed from the baked lattice when the cloth loaded and the
distance constraints enforce them, so a row that rises must draw IN toward the collar by as much as the geometry
demands — otherwise the target sits further away than the cloth can reach and the lift spends itself pulling against
those constraints. The trim is not a constant: a row `d` down the cape raised by `l` spans `√(d² − l²)`, computed per
row from the cape's own measured length (`_capeSpan`). That is why the lift can be raised freely and the shape stays
reachable.

**The ripple must ramp in from the collar** (`CapeRippleReach`) for two reasons: a pinned sheet flutters least at its
pinned end, and a swell larger than a row's own distance from the collar would make the draw-in geometry above collapse
that row onto the collar.

**Wavelength matters for BUNCHING, not just looks.** Neighbouring rows differ in velocity by roughly
`amplitude × 2π / wavelength`; where that difference points them at each other the sheet is in compression and buckles —
the pile-up seen at the hem. A longer wave flattens the gradient; the rest spring (CAPE_PHYSICS K) is the other half,
since each particle is pulled toward its OWN place in the shape and that restores spacing.

**Tear threshold.** `CapeTearSpan` compares the cloth against itself (pinned corner → mid-hem), never against the cat:
a cat riding its pellet covers 5 u/frame, so the cloth legitimately trails. Real span ~5 units, ~10 under a hard flight
while the 4 constraint passes catch up; observed tears were 40–54, so 20 sits clear of both.

**The spring is a per-axis VECTOR** (CCloth +0xE0/+0xE4/+0xE8) applied component-wise to the correction, but in WORLD
axes — hence `StiffenCape` rebuilding it every tick from the cat's facing, mixing the stiff and slack values by the
squares of the facing so the pair rotates smoothly through the diagonals. An axis-aligned diagonal is all the engine can
express: the off-diagonal terms of the true rotated tensor have nowhere to go, and at 45° the two values meet in the
middle. `Facing()` must return a unit vector — feed it (0, 0) and both horizontal axes come out zero, leaving the cape
with no restoring force. The baked .clo carries `CapeSpringAlong` as its seed for the first frame or two.

**Cape ambient is FLAT** — it lifts every part of the cape equally, so the more of it there is the less the scene's own
per-normal lighting (the part that SHOWS the ripples) counts for. Enough to read red in a dark dungeon, not so much that
it washes the shading out. The cat's blue is 12/24/48 for comparison.

## The cape at the cat's size

The cat is drawn at a per-look size (`WeaponLook.Scale`: the Title 1.0, the Angel Shooter 1.1, the Angel Gear 1.2) through the slot's scale, which every
bone's matrix carries. The cloth sim is only half scale-aware: its **targets** (`+0x7550` = LW(anchor) × rest) and the
pinned edge come through the anchor's matrix and grow with the cat, but the **StretchBind rest lengths** (`+0x3110`,
x = across, y = along the hang) were measured from the lattice once at load, in rig units, and are never rescaled — so at
any size but 1.0 every tie is `scale×` too short for the sheet the spring pulls toward, and the cape bunches toward the
collar (mildly at 1.2, plainly at 1.4 when tried). The body capsules' **radii** (`CBound +0x10`) are world constants too, while their
endpoints ride the bone. `ScaleClothToCat` and `CloneBounds` size both to the cat at spawn, plus the wind gain (a world
velocity). K, follow and the mod's breeze (rest-space) need nothing. The gait speeds and glow are stated for the 1.0
cat and scale with its size, so a bigger cat covers proportionally more ground at the same clip rate.

## Cape clone recipe and the element palette

**The cape's CCloth (0x8550) is built by HER pack load**, not by the mod: the baked `cat_cape` node plus `catcape.clo`,
whose FRAME resolves the node in Xiao's own tree and whose bind 3×3 is HIDE_SCALE'd so on her the cloth collapses to a
point. `TakeHerCape` lifts that object out of her cloth list the moment it appears (every reload rebuilds it) and keeps
it as the clone template; her list entry is zeroed so she neither steps nor draws it.

`SpawnCape` then clones it onto the copy following CharacterClone.CopyCloth: the whole object, two private draw packets
(the engine rebuilds the packet from the particles on every draw, so the copy must own them), the anchor re-pointed at
the copy's `cat_sebone2` because the rest lattice was authored in that bone's space, no body capsules, the Verlet
"previous" array seeded from "current" so the first step is quiet, and the copy's +0xC74 pointing at a one-entry list.
The dungeon chara loop steps every slot's cloth while `MirageSceneGateFlag == 1` (the Mirage pnach's ClothStep swap),
which the cat already sets; `Draw__10CCharacter` draws the list.

**The element recolour is a palette write, not a texture swap.** `build_cat_pack.flat_tim2` bakes the cape texture as
32×32 pixels that are ALL palette index 0 followed by 256 identical CLUT entries, so the whole cape is a single palette
entry. Repainting that entry in the texture manager's own copy recolours the cape live, because the dungeon draw loop
re-uploads the cat's texture group before drawing the slot — the same mechanism WeaponTextureSwap uses on Super Steve.
Painting all 256 entries also makes it immune to CLUT ordering, since every entry holds the same colour. The mask shares
`catcape` (wing_bake.MASK_TEX), so it follows with no work of its own.
