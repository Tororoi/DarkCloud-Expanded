# Cash models

How the mod puts a model of its own into the game's item-model cash so that copies of it can be drawn (`BladeProp`) or
grafted onto an effect. The class is `CashModel` (Weapons/CashModel.cs); its users are the wrappers listed below. The facts
here are the ones a reader of the code needs and the lessons that shaped it; the addresses and offsets stay in the code
(`ItemModels`, `TextureManager`, `CashModel`).

## The engine's cash

- **`CMainItemModel`** (`ItemModels.ItemModelPtr` 0x21EC78A8) holds **six cash entries**: root frames at +0, item ids at +0x18,
  refcounts at +0x30. It is the game's own allocation for the floor — menus do not load into it — and it is **emptied on a
  floor change**, so a `CashModel` checks its root before every use and reloads when it is gone.
- **The loader.** The game loads an active item's model from the item menu: the two files (an MDS and an IMG/IM2 bank) into
  the menu's read buffer (`ItemModels.MenuBufferPtr` 0x202A2CD4, allocated only while a menu is open; mds at +0, img at
  +0xFA10), then `SetCashModel` (0x1D45E0: `(this, itemId, mds, img, imgSize)`), which allocates from the entry's allocator,
  uploads the entry's texture block and builds the frames. `SetCashModel` stores only the item id as the entry's label, so a
  model of the mod's own takes a label no item has.
- **The allocator.** One `CDataAlloc2` per entry (`BtItemCashArea` 0x21F067E0 + i·0x10: +0 base, +8 units used, +0xC capacity,
  16-byte units), **0x9C5 units = 40,016 B** (GameInit). The model, its built frames (0x270 per node) and a copy of the texture
  bank must all fit. ⚠ `Alloc` loops forever on an overrun — the game hangs — so `CashModel.EstimateUnits` refuses a load
  that would not fit, and `ShadowRoot` refuses when the entry lacks the room.
- **`LoadMDSFile`** (0x1262B0: `(mds, allocator, kind, 0, 0)` → root frame): kind 0 a lit model (what `SetCashModel` builds),
  kind 8 a SHADOW model (`CVisualShadow` visuals, what `MGDrawShadowFast` draws). A lit mesh drawn in shadow mode comes out
  garbled, so a shadow needs its own kind-8 load — `CashModel.ShadowRoot` puts one into the same entry's allocator.

## Texture block numbering

- A cash entry's textures go into texture block **0x38 + cash index** (`CashModel.CashBlockBase`).
- The clone-weapon slot's pass uses block **0x1D** (`WeaponPassBlock`: the per-character formula, chara 3 → 0x11 + 12); the
  main-character effect's pass (a borrowed shot's) uses block **0x10** (`MainEffectBlock`).
- Every block's window is packed up from the same VRAM base — the enemy, effect and weapon blocks lie over the same pages —
  and each pass reloads ITS block before it draws, never the cash's. The last upload before a draw wins.

## How the mod loads one

`CashModel.Root()`:

1. If the entry last loaded still holds this root under this label, return it.
2. Else scan the six entries for the label (the game's own load under the same id, or a load whose bookkeeping was lost).
3. Else, no more than once per 2 s and never while paused or in a menu: write the two files into the dungeon loader's read
   buffer (`ShotEffectPack.ReadBufferPtr`; where every floor's packs land, idle between loads — or the menu's buffer when one
   is allocated), check `EstimateUnits` against the entry capacity, and call `SetCashModel` through the call-request cave
   (`NativeCall.Invoke`). Then find the entry by label.
4. After any load or rediscovery: `ApplyFullTexture` (when the model has full textures) and `PlaceTextures`.

`EstimateUnits` is a safe-side estimate of what `SetCashModel` takes from the allocator: the texture bank's copy, a 0x270
frame per node, a visual (with 64-byte alignment) per mesh, ~16 B per record per stream of VU data + 64 per sub-mesh,
padded 15 %. Calibration: the rock (IwaModel) estimates ~33.5 KB against the ~32.9 KB it actually takes.

The two factory helpers: `FromChrPack` (the MDS and bank of a `.chr` pack on the ISO, the bank optionally transformed) and
`BuiltOnce` (any builder run once a session; a failure is logged once and not retried).

## The texture's home: a reserved window (`PlaceTextures`)

The entry's TEX0 (its pixels and its CLUT) is moved to a window reserved **above every block's top**, the texture manager's
downward cursor moved below it — a real allocation, so nothing is ever handed out on top of it — and the same TEX0 word is
patched in the model's own draw packet (`SweepModel`: the VU packet, a skinned visual's second packet and its MDT), which
every copy and graft draws from. Only addresses inside the cash block's window are shifted: a CLUT can sit elsewhere, and a
shifted address outside the window wraps in the GS's 14 bits onto some other texture's page. `Forget` hands the reservation
back when the cursor still stands on it, and keeps it (reused by the next load) when something was handed out below.

Why a window at all: a copy drawn in the clone slot binds its texture before that slot's own upload lands (its draw is in
the packet the GS reads first), and another pass — the apple shot's — uploads its own textures over the same pages. The
Bomb's copy sampled whichever texture happened to hold them: the slingshot's atlas, the apple. Once uploaded into the
window, the pages are the model's for good. The Divine Beast cat's textures use the same window (docs/divine-beast-title.md);
claiming a window without moving the manager's cursor lets later textures land on it.

## Textures too big for the entry: stand-ins, then the full picture

A 256² 8-bit picture (66.6 KB) does not fit the 40,016-byte allocator with a model. The trick:

1. **Stand-in bank.** `StandInBank` hands the cash an IMG bank of the same texture names resampled to n² (nearest texel —
   8-bit indices cannot be blended), each CLUT kept, row-major under the IMG magic (the source banks are IM2 in PSMT8 block
   order, un-swizzled on the way). The model loads and gets its entries.
2. **Full picture.** `FullPictures` keeps the pictures whole; after the load `ApplyFullTexture` re-points each stand-in's
   entry at them. The manager uploads an entry (`ReloadTexture` 0x133070) from its own fields — width +2, height +4, bytes
   per texel +6, level-0 pixels +0x38 (mipmaps +0x3C…), CLUT +0x48, swizzled flag +0x4C, VRAM placement from the TEX0 word
   at +0x28 — so those fields and the TEX0 (TBW, TW, TH, CBP just past the pixels) are rewritten, the model's packet swept
   for the same word, and `PlaceTextures` reserves a window as big as the full textures (`_windowMin`), each texture
   page-aligned (32 GS blocks).
3. **Where the data lives.** The full pixels and CLUTs are kept high in the dungeon loader's read buffer (about 4 MB; the
   cash loads use only its first 0xFA10 + an image) at the model's `FullOffset` — the rock at +0x300000, Queens' trees at
   +0x320000, each model its own span. Menus load into that buffer too: the party screen streams the ally models over this
   span. So `KeepTextures` writes the data again whole after any gap in the upkeep longer than 100 ms (the users tick every
   16 ms; a longer gap was a pause or a menu) and otherwise spot-checks each texture's first and last 16 bytes and its
   CLUT's first 16, rewriting all of it when anything differs.

The read buffer is not safe for anything that must survive a menu: a shadow disc once kept there was overwritten by the
party screen's ally models and, drawn behind the menu, reset the game — which is why `GroundShadow` lives in a cash entry
of its own instead.

## Keeping a copy uploaded (`KeepTextures`, `Tick`, `ReleaseTextures`)

While a copy is drawn through another pass's block, the model's entries are re-tagged into that block and the block is
marked unloaded (`BlkLoaded` = 0) every tick, so its uploader re-sends every entry, ours among them — the same trick the Sun
Sword's disc uses to stay uploaded (docs/solar-flash.md). With the window above, one upload is enough and the rest are
cheap. The entries are tagged back into the cash's block when the copy goes. The cash's own block is kept unloaded too
(`Tick`), so the game's own draw of the same cash model (a thrown Bomb) re-sends it there.

## The wrappers

Each is a static class holding one `CashModel` and forwarding `Root` / `Forget` / `KeepTextures`:

| Wrapper | Label | What it loads |
|---|---|---|
| `BombModel` (Weapons/Xiao/BigBangShot) | 159, the Bomb's own item id | `dun\item\main_data\bakudan.mds` + `.img` as they are — the copy BigBangShot hangs and throws. Shares the entry with a real active-item Bomb. |
| `KinomiModel` (Weapons/Xiao) | 30003 | The nut of `gedit\s04\chara\e114kinomi.chr` whole (one 128² texture; fits the entry) — Super Steve's Terra Sword sphere. |
| `IwaModel` (Weapons/Ungaga) | 30001 | Master Utan's boulder from `gedit\s96\chara\iwa.chr`: `iwa.mds` whole, its 256² texture `d02b10` as a 128² stand-in with the full picture applied at +0x300000 — the Terra Sword's falling rock. |
| `QueensTrees` (Weapons/Xiao) | 30000 | Georama part 12 (Queens' trees) built once a session from `gedit\e03\scene.scn` and `e03b01.img`, its 256² texture `e03b04` as an 8² stand-in with the full picture applied at +0x320000 — Super Steve's Desert Bloom. |
| `GroundShadow` (Weapons) | 30002 | A flat unit disc (iwa.mds's header and materials, an 8² stand-in texture), loaded lit by `SetCashModel` and then as a kind-8 shadow model into the same entry (`ShadowRoot`) — the floor shadow under the rock and the cactus. |

Labels 30000–30003 are ids no item has; a wrapper's `CashKey` constant names its own.
