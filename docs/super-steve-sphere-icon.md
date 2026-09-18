# Super Steve — the attached sphere's icon

Super Steve inherits the ability of whichever weapon's SynthSphere is attached (`SuperSteveAbilities.AttachedSphere`).
The sphere is shown as that weapon's icon drawn over Steve on the dungeon HUD, natively, by `ElfCave.SuperSteveIconDraw`
(`tools/stubs/supersteve_icon_draw.s`, hooked at the overlay's `jal topStatusInfo`, dun 0x1DB0364);
`SuperSteveAbilities.DriveSphereIcon` switches it on with its screen position in the mailbox once per sphere change.
This replaced the earlier palette swap, which recoloured the slingshot model with the source weapon's CLUT.

## The dungeon HUD (RE)

`topStatusInfo(x, y, floor)` (0x1B04F0, called each frame from the overlay's `MotionProcess`) draws the whole status
HUD from two textures, `stayframe` (frames) and `itempack` (256×192, `commenu\a_usa\itempack.img` — the sheet with
the Floor boxes, the `speed` label, the status words). Its last sprite is **the equipped weapon's icon**: cell (0,0) of
`itempack`, drawn 32×32 at screen (29, 388) — Super Steve's icon is Steve's face, which is the bottom-left "Steve".
The three active items are cells (32..96, 0) of the same sheet, drawn at x = 0x126 + 0x28·i.

Those cells are kept filled by GS moves (`setItemToReserved` = `MoveImageTest`, VRAM to VRAM, 8-bit indices):
`DngActiveWeaponTextureCopy` copies the equipped and default weapons' cells out of the `wepicon` sheet,
`DngActiveItemTextureCopy` the active items' out of `itemicon`. Both sheets come from `commenu\…\itemlst.img`
(`LoadActiveItemIcon` → `SetTempTexture` slot 0x28) and stay registered in the dungeon; `itempack` shares their
256-colour palette exactly, which is what makes the index moves lossless. (`dun\etc\itempack.img` is a different,
Japanese-labelled sheet with its own palette — not the one the USA HUD loads.)

**Icon cells.** `RetCTex` maps an item id through `ComItemInfo` (+0x4 = icon index): u = (icon & 7) × 32,
v = (icon >> 3) × 32, on `wepicon` for class 0/2 and `itemicon` for class 1. `wepicon` is 256×640 (8 × 20 cells);
Super Steve is icon 55 (row 6, col 7), Angel Gear 56 (row 7, col 0). Weapons (ids 257–376) are all class 2.
T8 pictures in the files are GS-swizzled (PSMT8); `tools` has no viewer — the unswizzle used to verify the mapping is
the standard PSMT8 block formula.

**The sheets are transient.** A run with the manager logged showed `wepicon` absent in the dungeon (only `itempack`
registered) until the menu loaded its own copy. `LoadActiveItemIcon` registers the sheets at floor start, the game's
copies land while they exist, and only the cells moved into `itempack` — resident all floor — survive; the per-frame
copy call is harmless in between (its `GetTexture` returns 0). So the sphere icon uses two caves, mirroring the game:

- **copy** (`ElfCave.SuperSteveIconCopy`, on every `jal DngActiveWeaponTextureCopy`): the game copies only on its
  menu paths — `ExitDunEnterMenu` (item menu exit, where a sphere is attached), `BtMenuLoad2` (battle menu),
  `WeaponSelectKey`, `CharaChangeLoop` — each while that menu has `wepicon` registered — plus the overlay's two
  sites (dun 0x1DAE36C in the per-frame step path, which is what fills the equipped weapon's cell at dungeon entry
  while the floor-start sheet is registered, and 0x1DAE608). The overlay's symbol map is unreliable for these two
  (`Step__10CMajinBeem` / `Draw__11CSeireiKing` are not their real owners). The cave is not gated on the mod: it reads the
  CURRENT sphere from the equipped record itself (attach list → SynthSphere 0x5A → source id → `ComItemInfo+4`), so
  it never depends on when the mod ticks, and `setItemToReserved("wepicon", u, v, "itempack", 64, 32)` moves the cell
  into `itempack`'s spare cell (64, 32) — blank in the art and outside every game copy target. The spare cell is a
  constant in both caves, not a mailbox word: on a save loaded straight into a dungeon the entry-time copy fires before
  the mod has written anything, and a cell read from the mailbox then is (0, 0) — Steve's own — which the cave had to
  refuse, so the icon only appeared after a menu. Counters in the mailbox (`SsIconDiag*`) record each stage and are
  reported in the `sphere icon:` log line; they are how the dead overlay site was found.
- **draw** (`ElfCave.SuperSteveIconDraw`, on `jal topStatusInfo`): `set2DSprite(Vif1Packet, GetTexture("itempack"),
  dst, src (64, 32, 32, 32), 0x80)` at `SuperSteveAbilities.SsIconX/Y/Size`, tuned in game.

`Vif1Packet` is the global at 0x2A23C4; the name strings are the ELF's own (`wepicon` 0x2A2190, `itempack` 0x29F040).

## The character menu (RE, not used)

The dungeon's character menu (`BattleMenuDraw` → `DrawCharaSelect` → `DrawSelCharaStatus`) draws a per-character
panel: the board `charastb`, gauges via `DngComStatus(x−132, y+10, chara, alpha)`, and the equipped weapon's icon at
(x−128, y+72) from the `WepIcon` global (gp−0x6950 = 0x2A2EA0; `ItemIcon` gp−0x6958; gp = 0x2A97F0). Its textures come
from `commenu\a_usa\charatex.img`. The portrait slides against the panel during menu transitions.
