# Ungaga's Mirage — the decoy clone and its heat shimmer

Design and runtime: `Weapons/Ungaga/Mirage.cs` (the state machine), `Weapons/CharacterClone.cs` (the clone),
`Weapons/Ungaga/HeatHaze.cs` (the shimmer). Super Steve inherits the ability through the same code when a Mirage
sphere is attached.

## The heat shimmer

The game has exactly one framebuffer distortion: `DrawRaster__9CFireOmni` (0x162310), the haze over a fire. It draws
from its object alone — a position at +0x20..+0x28 (it adds 3.0 to the height itself), a wave phase at +0x04, and two
textures fetched by name each call: `blender` (the framebuffer, always registered) and `alpha01` (the fire pack's
distortion mask, resident only when the dungeon carries `fire.img`). It has no notion of tiles or distance.

The tile walker `DrawRaster__11CDungeonMap` (0x1C4610) is where every gate lives: it visits only the fire tiles within
±4 of the *camera* and within 240 units, rotates each emitter's offset by its tile's orientation, writes the world
position into the one `CFireOmni` the map embeds at +0x50 (so it lands at map+0x70..+0x7C) and calls `DrawRaster` on
it. The overlay's `setTexScroll` steps the phase at map+0x54 every frame, fires or not.

The shipped shimmer (`ElfCave.MirageHazeDraw`, `tools/stubs/mirage_haze_draw.s`) replaces the loop's `jal` to the
walker (dun 0x1DAEBCC): it performs the walk, then draws one more raster at the clone's root CFrame — its posed world
translation at +0x180, as of that frame — lifted by a mailbox float. The PNACH's size, amplitude and wave-speed
patches sit inside `DrawRaster`/`RasterStep` and apply to it unchanged. A dungeon without `alpha01` draws nothing.

### What was tried first, and why it was abandoned

1. **Marking a new fire tile** in the 20×20 array so the walker would draw at the clone. The tile-array write made
   the engine treat that floor tile as fire-tile *geometry*: floor collision broke. The fire-struct write was not the
   culprit (writing a real torch's raster count had no collision effect) — the tile entry was.
2. **Hijacking a lit torch's fire struct**: append a raster emitter to it, offset the emitter to the clone, and
   restore the struct on despawn. It worked, but every gate in the walker worked against it: it needed a lit torch
   in the dungeon, only tiles within ±4 of the camera are walked (the player stood in for the camera, with
   hysteresis, and the anchor had to be handed from torch to torch as they moved), the emitter offset is rotated by
   the anchor tile's orientation so error grew with distance, and the position crossed PINE on the mod's tick while
   the clone moved at 60 fps. The PNACH's 240-unit dist-gate relaxation (0x1C4714) exists for this version; the
   cave does not need it.

## Holding through a pause

The clone's slot is marked in `DungeonCharaDraw.StepSkipTable` while the game holds (`CharacterClone.Held`), which
freezes its motion, cloth and shadow without touching its channel; `MaintainInternal` keeps the mark. The decoy
timer, hand-off fade and aggro hold read `GameClock`, so they stand still on their own. The PNACH gate flag's value 3
("decoy up but paused") predates this and is no longer written.
