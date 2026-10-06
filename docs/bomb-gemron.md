# Bomb Gemron

A new enemy species: Holy Gemron's rig and script under a paler sheet, its three gems replaced by thrown-bomb models. Immune
to every element, 40 ABS, Holy Gemron's other stats; placed by the randomizer only (no vanilla floor lists it). It explodes
when it dies, and when near death with the player close it self-destructs: a slow death animation, then the blast.

## Where each part lives

| Part | Where | Notes |
|---|---|---|
| Species record | `SpeciesRows` (index 167) baked at `CodeCaves.SpeciesRows` (0x1FAF500) | The vanilla table has no slack; the cave segment loads a data page in front of the band for the rows. `SetupBaseModel` reaches them through `tools/stubs/species_lookup.s` (SmoothRest cave), hooked at 0x1DFEE0. `EnemySpeciesTable.RecordAddress` maps indices ≥ 167 there. |
| Model `e167a.chr` | `ModsSpeciesBakes` (post-bake `mod-species`) | Holy Gemron's pack: the body sheet's CLUT desaturated/lightened except a kept list of entries, the gem meshes replaced by `bakudan.mds` scaled and turned per bomb, the bomb's sheet added to the bank, the glow overlays dropped. Byte-exact port of the Python preview builder. |
| Script `e167a.stb` | same bake | Holy's script plus two appended functions: the death path's wait fires `_SET_SHOT2` at frame 122 of motion 11; the AI loop's head calls a self-destruct check (HP < 25 %, player ≤ 22 units → motion 11 at 0.25×, blast at 122, fade, dead). |
| Archive entries | `IsoArchive.Rename` | The orphan `e147a` chr/stb entries renamed in DATA.HD2 (the engine's index — the USA build never reads DATA.HED) and in DATA.HED (the mod's). |
| Name | message 3320 of `dunmsd00_1.mes` | An empty vanilla slot (3000 + species id), written in place into the bank's padding. |
| Blast | shot config 8 (`ElfSpeciesPatches.PatchBlastConfig`) | `g_wave2`'s record (unused by any species or script) rewritten as `zibaku_f2`'s with its phase-0 radius 50 instead of 14: the outlaws' fireball, fire, knockdown, player only, 110 frames; damage = the script's argument (150), which the stat normalizer scales. Flat inside the radius — the engine has no distance falloff for enemy shots. |
| Shot | `BombGemron.cs` | The apple it throws (`ringo_ex`) carries the item bomb's mesh: the pack slot's trees have the apple node's visual swapped for the bomb's (the Big Bang's graft, on the species' slot), sub-shots at ×2, the bomb's textures kept in the monster block. Skipped while the Big Bang holds the bomb. |
| Fuse | `BombGemron.cs` | The engine's blinking hit mark (the guard-mark pool's entries 8–15) at the big bomb's wick tip, walking the wick to its base over the death motion so it reaches the base at the blast. The wick's centreline is a constant in the body bone's frame. |

## Tuning

- `ElfSpeciesPatches.BlastRadius` (50) — repatch to change.
- `EnemySpecies.BombGemron` — stats; `HomeOf` gives it Holy Gemron's home region for the normalizer.
- `ModSpeciesBakes` — the sheet adjustment (`Sat`/`Bri`/`Con`/`Light`, `Keep`), bomb sizes and spins; the self-destruct gate
  (`SelfDestructHp`, `SelfDestructRange`, `SelfDestructSpeed`), the blast frame and height.
- `BombGemron.ShotBombScale` (2) — the bomb on the shot.

## Lessons

- The species table has one reader, so an extension table behind a 32-byte stub was enough; but the rows are data the mod writes
  (a roster's MonsterType), and a PINE write into a page with executed code kills PCSX2. They live in a data page, loaded by the
  ELF so a game reset cannot lose them.
- The USA build builds its file-name tree from the names inside DATA.HD2 at boot. A rename that touches only DATA.HED leaves the
  engine unable to find the file (LoadFile's assert resets the game).
- A 35th shot config is blocked by the shot-slot sharing cave (it rejects indices past 34 and its per-config table is full), so a
  new blast means rewriting an orphan config.
- The item-bomb reaction word and the charge-attack radii in the data page are baked to their vanilla values: the page is
  zero-filled by the loader on every boot, and the app's startup seed alone left them 0 after a reset.
