#!/usr/bin/env python3
"""Assemble every EE cave stub (.s) straight into Resources/isoPatch/stubs/<group>/ (the embedded copies the
C# build consumes). Single source of truth for each stub's assemble base VA — these MUST match
the STUB_VA / write-site constants in IsoPatcher.cs (the per-stub patch methods note their VA).

    python3 tools/stubs/build_ee_stubs.py            # assemble + write all
    python3 tools/stubs/build_ee_stubs.py --check    # assemble only; fail if any output differs from
                                            # the committed Resources copy (CI / pre-commit)

Run after editing any tools/stubs/*.s, then rebuild the app so the EmbeddedResource updates.
"""
import os as _os, sys as _sys
_sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
import toolpath  # noqa: F401 — puts every tools/ subfolder on sys.path
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from mips_asm import assemble

DEST = os.path.join(HERE, '..', '..', 'Dark Cloud Improved Version', 'Resources', 'isoPatch', 'stubs')

# (source .s in tools/, dest .bin under Resources/isoPatch/stubs/ — its feature group's folder — assemble base VA[, tail dest .bin, tail base VA])
# The folder is only the file's place on disk: the embedded resource keeps its bare name (the csproj's LogicalName), which is what
# ElfCaveWriter.Embedded looks up.
# A stub with a `#SPLIT` line is assembled as ONE image at its base with the gap up to the tail base blanked
# (`.space`), then written as two .bins: the head (before the marker) and the tail (from the tail base on).
# Count: every instruction line before the marker is one word (no pseudo-instructions that expand; one value per `.word`).
# The 0x1FB0xxx stubs live in the mod's own ELF PT_LOAD segment (the hijacked phdr3 —
# CodeCaveAddresses.ElfCave is the registry; ElfPatches.HijackPhdr3CaveSegment creates it). Their
# bases MUST match the ElfCave constants, and the segment start must stay 16KB-aligned: its host
# pages may hold NO runtime-written data (PINE writes to a compiled page SIGBUS PCSX2 — see the
# ElfCave doc's page-isolation rule). ⚠ The old home 0x228BB0..0x22A210 is LIVE dungeon code
# (CharaChangeLoop/Key/Draw, called from the dun.bin overlay) — never assemble anything there again.
STUBS = [
    ('canal_evict_fade_hook.s',  'town/canalEvictFadeHook.bin',   0x1FB0000),
    ('queens_spray_cave.s',      'town/queensSprayCave.bin',      0x1FB0050),
    ('spray_bias_shim.s',        'town/sprayBiasShim.bin',        0x1FB0150),
    ('cape_early_draw.s',        'town/capeEarlyDraw.bin',        0x1FB0190),
    ('fishline_split_caves.s',   'fishing/fishlineSplitCaves.bin',   0x1FB0210),
    ('fishline_uncast_gate.s',   'fishing/fishlineUncastGate.bin',   0x1FB0270),
    ('camera_norm_side.s',       'camera/cameraNormSide.bin',       0x1FB0350),
    ('stilts_heal.s',            'fishing/stiltsHeal.bin',           0x1FB0BD0),
    ('water_order_gate.s',       'fishing/waterOrderGate.bin',       0x1FB0C50),
    ('cat_pellet_follow.s',      'cat/catPelletFollow.bin',      0x1FB0D90),
    ('xiao_melee_flinch.s',      'combat/xiaoMeleeFlinch.bin',      0x1FB2250),
    ('prop_pellet_follow.s',     'cat/propPelletFollow.bin',     0x1FB1E30),
    ('cat_glow_draw.s',          'cat/catGlowDraw.bin',          0x1FB2000),
    ('cat_sphere_percent.s',     'cat/catSpherePercent.bin',     0x1FB21E0),
    ('cat_cape_tint.s',          'cat/catCapeTint.bin',          0x1FB22C0),
    ('cat_mask_tint.s',          'cat/catMaskTint.bin',          0x1FB2370),
    ('cat_load.s',               'cat/catLoad.bin',              0x1FB2480),
    ('cat_palette.s',            'cat/catPalette.bin',           0x1FB2700),
    ('cat_glow_palette.s',       'cat/catGlowPalette.bin',       0x1FB3A80),
    ('mirage_haze_draw.s',       'weapons/mirageHazeDraw.bin',       0x1FB3C40),
    ('supersteve_icon_draw.s',   'weapons/superSteveIconDraw.bin',   0x1FB3CE0),
    ('supersteve_icon_copy.s',   'weapons/superSteveIconCopy.bin',   0x1FB3DC0),
    ('cat_guard_bypass.s',       'cat/catGuardBypass.bin',       0x1DAC070),   # dun.bin: MemoryMapDump's body (DunPatches writes it)
    ('borrowed_shots_enter.s',   'weapons/borrowedShotsEnter.bin',   0x1FB1ED0, 'weapons/borrowedShotsEnterTail.bin', 0x1FB3F40),
    ('shared_shots.s',           'enemies/sharedShots.bin',          0x1B3788),    # main ELF: DebugInfomationDraw's body + 8 (ElfPatches.PatchSharedShots)
    ('steel_level_up.s',         'weapons/steelLevelUp.bin',         0x1B42E0),    # main ELF: DebugInfomationDraw's body, after pelletSprite (ElfPatches.PatchSteelLevelUp)
    ('circle_effects.s',         'combat/circleEffects.bin',        0x1B47C8),    # main ELF: DebugInfomationIF's body + 8 (ElfWeaponPatches.PatchCircleEffects); dun.bin's Run_TrapCircle jumps here
    ('spear_block.s',            'weapons/spearBlock.bin',           0x1B5030),    # main ELF: DebugInfomationIF's body (ElfWeaponPatches.PatchSpearBlock): a solid sphere for enemies (Babel's spear)
    ('player_spear_block.s',     'weapons/playerSpearBlock.bin',     0x1B5260),    # main ELF: DebugInfomationIF's body (ElfWeaponPatches.PatchSpearBlock): the same sphere, solid to the player
    ('shot_spear_block.s',       'weapons/shotSpearBlock.bin',       0x1B5350),    # main ELF: DebugInfomationIF's body (ElfWeaponPatches.PatchSpearBlock): the same column stops enemy shots
    ('rock_shadow.s',            'weapons/rockShadow.bin',           0x1B5460),    # main ELF: DebugInfomationIF's body (ElfWeaponPatches.PatchRockShadow): the Terra Sword boulder's shadow
    ('fall_drive.s',             'weapons/fallDrive.bin',            0x1B54B0),    # main ELF: DebugInfomationIF's body (ElfWeaponPatches.PatchFallDrive): the blade fall's mode 4
    ('guard_crush.s',            'combat/guardCrush.bin',           0x1B4650),    # main ELF: DebugInfomationDraw's free tail (ElfWeaponPatches.PatchGuardCrush): crush-marked hits pass guard windows
    ('guard_mask.s',             'combat/guardMask.bin',            0x1B4390),    # main ELF: DebugInfomationDraw's body after steelLevelUp — the guard gate's per-enemy window mask (ElfWeaponPatches.PatchGuardMask)
    ('confuse_name.s',           'weapons/confuseName.bin',          0x1B561C),    # main ELF: DebugInfomationIF's tail after fallDrive — ability bit 14's name (ElfWeaponPatches.PatchConfuseAbility)
    ('confuse_proc.s',           'weapons/confuseProc.bin',          0x22B248),    # main ELF: dead DebugItemGetKey's body — the Confuse ability's on-hit roll (ElfWeaponPatches.PatchConfuseProc)
    ('follow.s',                 'combat/follow.bin',               0x1B4690),    # main ELF: DebugInfomationDraw's free tail (ElfWeaponPatches.PatchFollow): a point carried with a unit
    ('bait_keep.s',              'weapons/baitKeep.bin',             0x27D230),
    ('species_lookup.s',         'enemies/speciesLookup.bin',        0x27D2F8),
    ('flash_slot.s',             'weapons/flashSlot.bin',            0x27D320),    # main ELF: dead SmoothRest body after the species lookup (ElfSpeciesPatches.PatchFlashSlot): the machine-gun flash's size and bone pin per slot    # main ELF: dead SmoothRest body after the Ruby cave (ElfSpeciesPatches): the species table's extension rows
    ('blow_dir.s',               'combat/blowDir.bin',              0x10A7D8),    # main ELF: the dead sceCdGetToc body (DunPatches): an item bomb throws the player away from its blast
    ('bomb_radius.s',            'enemies/bombRadius.bin',           0x10A880),    # main ELF: the dead sceCdGetToc body after the blow direction (ElfSpeciesPatches.PatchBombRadius): a shot config's own blast radius
    ('fuse_capture.s',           'enemies/fuseCapture.bin',          0x10A910),    # main ELF: the dead sceCdGetToc body after the blast radius (ElfSpeciesPatches.PatchFuseCapture): each Bomb Gemron's wick, captured in the monster draw
    ('bomb_tint.s',              'enemies/bombTint.bin',             0x10A1D0),    # main ELF: the dead sceCdReadChain body (ElfSpeciesPatches.PatchBombTint): the Bomb Gemron's big bomb reddens as its fuse burns
    ('eye_tint.s',               'enemies/eyeTint.bin',              0x10A3C0),    # main ELF: the dead sceCdReadChain body after freeze_break (ElfSpeciesPatches.PatchEyeTint): the Crystal Gemron's eyes and the Atla Gemron's atla tinted
    ('freeze_break.s',           'combat/freezeBreak.bin',          0x10A360),    # main ELF: the dead sceCdReadChain body after bomb_tint (ElfSpeciesPatches.PatchFreezeBreak): a hit breaks the player's freeze
    ('crystal_home.s',           'enemies/crystalHome.bin',          0x10B300),    # main ELF: the dead sceCdDiskReady body (ElfSpeciesPatches.PatchCrystalShots): one ice arrow turned towards the player
    ('crystal_prison.s',         'enemies/crystalPrison.bin',        0x118730),    # main ELF: the dead sceIoctl body after the two shot configs (ElfSpeciesPatches.PatchCrystalShots): the ice prison held while the player is frozen
    ('atla_shot.s',              'enemies/atlaShot.bin',             0x10A998),    # main ELF: the dead sceCdGetToc body's tail running on into the dead sceCdSeek body (ElfSpeciesPatches.PatchAtlaShot): the Atla Gemron's shot's hit radius and flight
    ('atla_collect.s',           'enemies/atlaCollect.bin',          0x10A690),    # main ELF: the dead sceCdReadIOPm body (ElfSpeciesPatches.PatchAtlaCollect): collecting the mod's atlas and Demon Shaft's high floors made safe
    ('atla_dungeon.s',           'enemies/atlaDungeon.bin',          0x10A750),    # main ELF: the dead sceCdReadIOPm body after atla_collect (ElfSpeciesPatches.PatchAtlaDungeon): Demon Shaft's atlas through Gallery of Time's tables
    ('ally_manager.s',           'ally/allyManager.bin',          0x11EDC8),    # main ELF: the dead sceMcSetFileInfo + sceMcRename bodies (ElfAllyPatches.PatchAllyManager): a summoned ally monster run on a private monster manager
    ('ally_collide.s',           'ally/allyCollide.bin',          0x118C88),    # main ELF: the dead sceSifLoadModuleBuffer body (ElfAllyPatches.PatchAllyManager): the ally and the floor's enemies block each other
    ('ally_brain.s',             'ally/allyBrain.bin',            0x10C7D0, 'ally/allySummon.bin', 0x1203D0),   # main ELF: the dead sceDmaSendN..SendM bodies, its summon in the dead scePadInfoPressMode..GetSlotMax bodies (ElfAllyPatches): the Pocklekul's pockle once a frame
    ('ally_follow.s',            'ally/allyFollow.bin',           0x1176D8, 'ally/allyTurn.bin', 0x11EEA0),     # main ELF: the dead sceSifSetRpcQueue..RemoveRpcQueue bodies, its turn after ally_manager.s (ElfAllyPatches): the ally walking beside the summoner
    ('ally_despawn.s',           'ally/allyDespawn.bin',          0x117080),    # main ELF: the dead sceSifGetOtherData body (ElfAllyPatches): the ally's end
    ('ally_settle.s',            'ally/allySettle.bin',           0x10B5A8),    # main ELF: the dead sceCdGetDiskType + sceCdGetDiskStatus bodies (ElfAllyPatches): the ally's last move finished before it idles
    ('ally_record.s',            'ally/allyRecord.bin',           0x10B648),    # main ELF: the dead sceCdGetDiskType + Status bodies after ally_settle.s (ElfAllyPatches): every attack sphere recorded while an ally stands
    ('ally_load.s',              'ally/allyLoad.bin',             0x117180),    # main ELF: the dead sceSifGetOtherData body after ally_despawn.s (ElfAllyPatches): the ally's load spread over its block
    ('ally_shots.s',             'ally/allyShots.bin',            0x1191A0),    # main ELF: the dead sceSifLoadElfPart..sceSifSetIopAddr bodies (ElfAllyPatches): the ally's shot configs taken back out of the pack
    ('ally_hooks.s',             'ally/allyHooks.bin',            0x1192E4),    # main ELF: the dead sceSifLoadElfPart..SetIopAddr bodies after ally_shots (ElfAllyPatches): the ally's hooks into the monster code (swing/hit masks, blocked enemies, the smoke pin, the full event)
    ('ally_aim.s',               'ally/allyAim.bin',              0x10CD20),    # main ELF: the dead sceDmaWatch..sceDmaCallback bodies (ElfAllyPatches): where the ally's AI goes for its enemy
    ('ally_grow.s',              'ally/allyGrow.bin',             0x11EF4C),    # main ELF: the dead sceMcRename body after ally_follow.s's turn (ElfAllyPatches): the ally's growth as it appears
    ('atla_draw.s',              'enemies/atlaDraw.bin',             0x10AAC0),    # main ELF: the dead sceCdSeek body after atla_shot, on into the dead sceCdStandby / Stop / Pause bodies (ElfSpeciesPatches.PatchAtlaDraw): the Atla Gemron's atla bouncing free
    ('crystal_shots.s',          'enemies/crystalShots.bin',         0x10BBD0),    # main ELF: the dead sceCdApplyNCmd body (ElfSpeciesPatches.PatchCrystalShots): the Crystal Gemron's ice arrow homing and ice prison    # main ELF: the dead sceCdReadChain body after bomb_tint (ElfSpeciesPatches.PatchEyeTint): the Crystal Gemron's eyes tinted white
    ('monster_bank.s',           'enemies/monsterBank.bin',          0x110848),    # main ELF: the dead soft-float routines' bodies (ElfSoundPatches.PatchMonsterBank): each randomized floor's monster bank
    ('monster_bank_io.s',        'enemies/monsterBankIo.bin',        0x10BEB8),    # main ELF: the dead CD streaming routines' bodies (ElfSoundPatches.PatchMonsterBank): monster_bank.s's helpers
    ('element_menu.s',           'weapons/elementMenu.bin',          0x22B410, 'weapons/elementMenuTail.bin', 0x22B5B8),
    ('element_menu_ruby.s',      'weapons/elementMenuRuby.bin',      0x27D270),    # main ELF: dead SmoothRest body after the bait keep (ElfElementMenuPatches): Ruby's shot effect reloaded when the picker closes on her   # main ELF: dead DebugItemGetKey tail (hooks) + DebugItemGetDraw body (sheet name, draw, cell test) — the quick-change menu as element picker (ElfElementMenuPatches)    # main ELF: dead SmoothRest body after the camera-height cave (ElfFishingPatches.PatchBaitKeep): the Poisonous Apple stays on the hook
    ('town_camera_collision.s',  'camera/townCameraCollision.bin',  0x14B838),
    ('camera_height.s',          'camera/cameraHeight.bin',         0x27D090),
]


def _is_insn(line):
    t = line.split('#', 1)[0].strip()
    return bool(t) and not t.endswith(':') and (not t.startswith('.') or t.startswith('.word '))   # a hand-encoded `.word` is one too


def _split_blobs(src_text, base, tail_base):
    """Assemble a `#SPLIT` stub as one image with the gap blanked; return (head bytes, tail bytes)."""
    head, tail = src_text.split('#SPLIT\n', 1)
    head_len = 4 * sum(1 for l in head.splitlines() if _is_insn(l))
    gap = tail_base - base - head_len
    if gap < 0:
        raise SystemExit(f'split stub: the head ({head_len} B) runs past the tail base 0x{tail_base:X}')
    blob = assemble(head + f'.space {gap}\n' + tail, base)
    if len(blob) <= tail_base - base:
        raise SystemExit('split stub: no tail assembled')
    return blob[:head_len], blob[tail_base - base:]


def _emit(dest, blob, base, check, stale):
    path = os.path.join(DEST, dest)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    cur = open(path, 'rb').read() if os.path.exists(path) else None
    if blob == cur:
        print(f'  ok      {dest} ({len(blob)} B @0x{base:06X})')
    elif check:
        stale.append(dest)
        print(f'  STALE   {dest} (committed {0 if cur is None else len(cur)} B != assembled {len(blob)} B or bytes differ)')
    else:
        open(path, 'wb').write(blob)
        print(f'  WROTE   {dest} ({len(blob)} B @0x{base:06X})')


def main():
    check = '--check' in sys.argv[1:]
    stale = []
    for entry in STUBS:
        src, dest, base = entry[:3]
        text = open(os.path.join(HERE, src)).read()
        if len(entry) == 5:
            head, tail = _split_blobs(text, base, entry[4])
            _emit(dest, head, base, check, stale)
            _emit(entry[3], tail, entry[4], check, stale)
            continue
        _emit(dest, assemble(text, base), base, check, stale)
    if stale:
        raise SystemExit(f'--check failed: {", ".join(stale)} out of date — run tools/stubs/build_ee_stubs.py')


if __name__ == '__main__':
    main()
