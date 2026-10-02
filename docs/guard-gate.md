# The guard gate

Every guard bypass in the mod goes through one ISO-side gate, judged inside `CheckDmg__12CMonstorUnit` (main 0x1D9F10)
on the frame a hit is tested. Nothing writes an enemy's own guard windows any more.

## How an enemy guards

Each enemy slot has three guard windows (`MainMonstorUnit + slot·0x20`: flags `+0x60550` short[3], start frame
`+0x60558` float[3], end frame `+0x60564` float[3]), registered by its script's `_SET_GUARD_FRAME` (cmd 244). A hit that
lands while the enemy's motion frame (`+slot·0x3510 + 0x1FFC0`) is inside an armed window becomes a guard reaction (spark,
clang, a tenth of the weapon-HP drain) and deals no damage. Zeroing the flags from the mod is a race: a script re-registers
them whenever its label runs (chest mimics do it as they wake), so the gate decides per hit instead.

## The chain

CheckDmg's window-flag load (main 0x1DAC78, `addu at,v0,at`; its `lh` delay slot nop'd) jumps to:

1. **`tools/stubs/guard_crush.s`** (0x1B4650) — the damage entry (`NowColData + s2·0xA0`) carries `CodeCaves.CrushMark`
   ("CRIK", 0x4B495243) at +0x9C → no window. Set per hit by whoever plants it:
   - the Divine Beast cat (`cat_pellet_follow.s` on its native hit; `DivineBeastTitle.PlantHit` on the thread fallback);
   - the Terra Sword's rock and nut (blast, pinned, bonk);
   - Big Bang's falloff when asked (`PlantFalloff(guardBreak: true)`): the judgement blade's landing, the Sword of Zeus's
     bolt, Hercules' strike (and the Super Steve spheres that fire them).
   The mark shares the no-drain mark's high half (0x4B49), which is all the no-drain caves test: a crushing hit of
   Ungaga's bills no weapon HP either. `CCollisionData::Set` (0x1B57A0) clears +0x9C on every entry the engine makes
   (`ElfWeaponPatches.PatchSetClearsMark`: its dead first `sw zero,0x20(a1)` at 0x1B5858 retargeted to +0x9C) — vanilla
   never wrote it, so the cat's mark rode on into the next pellet planted in the same entry and that pellet passed the
   guard. The cat cave stamps its mark after its own Set; the mod still clears the mark when it retires an entry.
2. **`tools/stubs/guard_mask.s`** (0x1B4390) — `CodeCaves.GuardMask` (0x01FAFCB0, one byte per slot) has the window's bit
   (bit w = window w) → no window. Written by `GuardGate` from two inputs:
   - `GuardGate.NobodyBlocks(bool)` — every window of every enemy: Dark Cloud and 7th Heaven while drawn (and as Super
     Steve's spheres), a Solar Shot's blinding, Big Bang's whirl;
   - `GuardGate.IgnoreWindows(slot, bits)` — chosen windows of one enemy: the Dusack's mimic wake window (frames 10–28,
     a king's 10–27; their guard motion 170–189 still blocks).
3. **`tools/stubs/cat_guard_bypass.s`** (dun 0x1DAC070, over `MemoryMapDump`) — the pellet rules, keyed on a Xiao-owned
   entry's base damage: the Matador's charged pellet passes (with its kick); Dragon's Y's shot gets its kick and takes the
   vanilla window test. Then the vanilla load.

## What does not pass

A hit with the melee kick type (+0x98 == 2) gets no bypass for that: the cat cave used to pass any Xiao-owned type-2 entry
(the cat's mark), which also let Super Steve's mod-planted hits through — Babel's hands, the Cactus pricks, the Sun /
Zeus / Big Bang falloff and bomb blasts of her spheres. Those now guard-check like their source weapons' hits; the cat
carries the crush mark instead.

`GuardGate` logs once when a bypass is wanted and the guard-mask cave is not in the ISO (repatch).
