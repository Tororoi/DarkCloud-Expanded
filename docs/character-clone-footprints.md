# Character clone footprints — cave sizing for the Mirage clone system

The Mirage decoy renders a fully independent second instance of the player character by
deep-copying its mutable per-instance state into caves (frame tree + software-skinned
meshes + motion channels + cloth). That per-instance copy is **irreducible** — it's exactly
what the engine allocates for every character instance (see
[`cave-code-execution.md`](cave-code-execution.md) and the Mirage memory notes). So the
clone's cave budget is driven by *which character* casts it.

This table is what each playable character actually needs, measured at runtime by the
read-only probe in `Mirage.ProbeCharacter` (DFS the model tree at `PlayerChar+0xBC`, sum the
`CVisualMDTVu1` software-skinned meshes, read the `+0xC74` cloth list). Captured 2026-07-12.

## Footprints

| id | Char   | nodes | MDT meshes | mesh bytes        | cloth pieces (grids)     |
|----|--------|-------|------------|-------------------|--------------------------|
| 0  | Toan   | 77    | 4          | `0x22380` (136 K) | 3 — 4×5, 9×7, 9×7         |
| 1  | Xiao   | 79    | 2          | `0x2CF00` (179 K) | 0                        |
| 2  | Goro   | 60    | 2          | `0x57B30` (351 K) | 1 — 7×8                  |
| 3  | Ruby   | 52    | 1          | `0x37520` (221 K) | 0                        |
| 4  | Ungaga | 67    | 3          | `0x318A0` (198 K) | 2 — 5×8, 5×8             |
| 5  | Osmond | 84    | 1          | `0x343C0` (209 K) | 0                        |

Notes:
- **mesh bytes** = Σ per software-skinned mesh of `align16(visual 0x30) + align16(VU packet)
  + align16(MDT block)` — the clone must own all of it (MotionProc2 skins in-place into the
  MDT vertex array; the VU packet is DMA-referenced deferred; see `cave-code-execution.md`).
- **cloth** = pieces in the `+0xC74` list; each `CCloth` is a fixed `0x8550` object. Grids are
  `rows×cols` (`+0x2c`/`+0x30`), small — Toan's 3 pieces are the worst case.
- Character models share ~3 heap buffers (roots reuse across ids), so probe the *active*
  character; the probe gates on a ≥1 s stable `(id, root)` read because character switching
  lags (id flips several frames before the model swaps — a naive read mislabels).

## Implications for cave sizing

The clone's caves are sized to the **worst case across all six characters**, so every character is clonable:

- **MeshCave = `0x58000`** — sized for **Goro** (`0x57B30`), the largest. This was `0x34000` and excluded
  Goro/Ruby/Osmond; the room was found by capping HarderEnemyAI's stubs at 32 slots, trimming the node pool
  from 128 to 96 bones, and packing the decoy tables out of a mostly-empty `0x10000` hole.
- **NodePool = 96 bones** — covers **Osmond** (84), the largest skeleton.
- **FrameInf / BoneMtx** — per-bone buffers, sized for 97 / 99 bones.
- **ClothObjCave = 3 slots** — covers **Toan** (3 pieces), the most.

Full map and capacities: `CodeCaveAddresses.cs`.

### Why the capacities are stated next to the caves
These buffers scale with bone count and sit immediately before their neighbours, so an overrun does **not**
fail — it silently corrupts the next cave. That is not hypothetical: sized for Ungaga's 67 bones, Xiao's 79
overran `FrameInfCave` into `BoneMtxCave` and `BoneMtxCave` into `WeaponCave`, overwriting the grafted
weapon's root CFrame (its parent pointer read back as `1.0f` — a matrix diagonal), so Xiao's clone simply
held no weapon. `CharacterClone` now bounds-checks against every cave's declared size and **refuses to spawn**
rather than scribbling over a neighbour.

## Reproducing

The table above was captured by a temporary read-only probe (`Mirage.ProbeCharacter`), which has
since been **removed** — it ran every dungeon tick purely to produce this data, and the data is now
recorded here. To re-measure (e.g. after changing what the clone copies), re-add a probe that DFS's
the model tree at `PlayerChar + CharModel`, sums the `CVisualMDTVu1` software-skinned meshes, and
reads the `+0xC74` cloth list. Gate it on a **≥1 s stable `(id, root)`** read: character switching
lags — `CurrentCharacterNum()` flips several frames before the `+0xBC` model actually swaps, so a
naive read mislabels one character's footprint as another's. Cycle party members, pausing ≥2 s on
each so the model settles. Character availability depends on story progress.
