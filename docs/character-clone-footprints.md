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

- **`MaxNodes = 128` is safe for all six** (max is Osmond at 84). No change needed. Do NOT drop
  below ~100 — the cloth cave sits at `0x21F54000`, right after the 128-node pool
  (`0x21F40000`, ends `0x21F53800`); shrinking the pool into it would collide.
- **`MeshCave = 0x34000` (208 K) fits Toan, Xiao, and Ungaga**, and is 93 % full for Ungaga.
  It is **too small for Ruby (221 K), Osmond (209 K), and especially Goro (351 K, +68 %)**.

### What this means per goal
- **Ungaga + Xiao (current scope: Ungaga's Mirage, and Super Steve/Xiao inheritance) both fit
  today** — no cave surgery required.
- **Universal support** (using the clone for effects on Goro/Ruby/Osmond) requires a **bigger,
  relocated `MeshCave`** sized to ≥ `0x58000` (Goro). It can't grow in place — `MeshCave`
  (`0x21F80000`) already runs to `0x21FB4000`, near the top of the clean band
  (`0x1F30000`–`0x1FB4300`). A larger region (e.g. lower in the `0x1F10100` band above the
  HarderEnemyAI stubs) would be needed. Deferred until such an effect is actually built.

## Reproducing

The probe runs read-only whenever you're in a dungeon (`Mirage.ProbeCharacter`, called from the
loop). Cycle party members, pausing ≥2 s on each so the model settles; it logs one
`[Mirage/probe]` line per settled character and re-logs on any change. Character availability
depends on story progress.
