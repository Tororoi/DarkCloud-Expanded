# Element picker — the quick-change menu as the weapon's element switch

D-pad **Up** in a dungeon opens the SELECT quick-change ring showing the five element stones and a grey synth sphere (None)
instead of the party. **X** applies the highlighted element to the equipped weapon and closes the menu; **Circle** leaves it.
Cells the weapon carries no amount of draw dim and refuse; so does None for Ruby and for Osmond's machine-gun mode, whose
weapons always carry an element. SELECT still opens the character ring (now for a party of one as well).

## Pieces

| piece | where | what |
|---|---|---|
| `tools/stubs/element_menu.s` | main ELF, the dead `DebugItemGetKey` tail (0x22B410) + `DebugItemGetDraw` body (0x22B5B8) | the six caves below and the cell test |
| `ElfElementMenuPatches` | five main-ELF sites, one word each | `jal` into the caves |
| `DunPatches` | overlay `MoveChara` 0x1DB14A8 / 0x1DB14C0 | the SELECT read → the trigger cave; `slti …,2` → `slti …,1` on the party size |
| `ElementMenuIconBake` (post-bake `element-menu-icon`) | `commenu\a_usa\quickchr.pac` → `quickchr.img` → `wepicon` | the None cell: cell 125 (dummy item 86) ← a grey copy of the synth sphere's cell 129 |
| `Dungeon/ElementMenu.cs` | mod, once a dungeon tick | applies a confirmed pick: the weapon record's HUD element byte, the HUD tint, Ruby's armlet refresh |

Runtime words (`CodeCaves`, the runtime-data span): `ElementMenuMode` 0x01FAF4E0 (1 from an Up opening until the menu closes),
`ElementMenuPick` 0x01FAF4E4 (confirmed cell + 1; the mod zeroes it), `ElementMenuTex` 0x01FAF4E8 (the `wepicon` CTexture, looked
up once per opening).

## How the menu is bent

The menu is `StartQuickChange` → per frame `CharaChangeLoop` → `CharaChangeKey` + `CharaChangeDraw` (main ELF, called from the
overlay). `ChangeMenu` is at 0x01DA8D30: +0 `selected`, +2 `party_size`, +3 `step`, +0x4C `unk_4c`.

- **trig** (overlay `jal Down(SELECT)`): returns non-zero for SELECT or Up and sets `ElementMenuMode` to which. The overlay's
  "party ≥ 2" test is relaxed to ≥ 1 so the picker opens for a lone Toan.
- **start** (`StartQuickChange`'s `lb v0,5(v0)`, the party size): in picker mode `party_size` = 6 and `selected` = the live
  weapon's element (`NowWeaponHave.best_elem` at 0x01EA75A6; anything outside 0..4 → the None cell), and the sheet cache is cleared.
  The cave restores `at`, which the caller's delay slot had just loaded for the store that follows.
- **pre** (`CharaChangeKey`'s cursor-move `jal CharaChangeInitToGL`): skipped in picker mode — no character preload per move.
- **xkey** (`CharaChangeKey`'s SELECT-step `jal Down(X)`): in picker mode a press on a selectable cell writes the live weapon's
  element, stores the pick, sets `step` = LOADED (6) with `unk_4c` = 16, and plays the confirm sound; the LOADED step then
  closes the menu on its next frame through the character change's own exit (`BtMenuLoad2(0)`, `LockOffTargte`,
  `InitReadBG`, result 1) — the overlay sees the same character and resumes play. A dim cell plays the refuse sound. The cave
  returns 0 either way, so the handler sees no press. In character mode it is the plain `Down(X)`.
- **draw** (`CharaChangeDraw`'s per-cell `jal DrawMenu2DSprite`): in picker mode the sheet is `wepicon` (the USA quick-menu
  pack holds `quickchara` and `wepicon`; the attachment icons sit from cell 120 = `ComItemInfo.icon_index` + 87), the source
  rect is cell 120 + `s2` (`u = cell*32, v = 480`, 32×32 — the stones of items 81..85, then the baked None at cell 125), the
  destination the 48 px cell's centre, and the shade 0x80/0x40 by the cell test.
- **close** (`CharaChangeLoop`'s first `jal MenuTextureReload` on the way out): clears `ElementMenuMode`.
- **valid** (shared): a stone needs a non-zero amount at 0x01EA75A7+cell; None is barred when `ChangeStatusDataPt->cur_chara`
  is 3 (Ruby) or 5 with the machine-gun word 0x01DC4520 set.

Keystone notes: no `at` use anywhere except the deliberate restore in `start`; strings go in as `.word`; the tail begins with
the sheet name so its address is fixed (0x22B5B8) and the draw cave follows at +0xC.
