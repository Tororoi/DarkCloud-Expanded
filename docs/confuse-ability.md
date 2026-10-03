# The Confuse weapon ability

A new weapon ability beside Poison, Stop, Drain and the rest, entirely ISO-side. Native to Babel's Spear only; a SynthSphere of
it carries the ability to other weapons as any ability does. Built in phases.

## Phase 1 — shown in the menus (done)

- **The bit.** Ability word (`WEAPON_HAVE +0xEE`, Effect1 | Effect2 << 8) bit **0x4000** (Effect2 bit 0x40). No vanilla template
  sets 0x1, 0x4000 or 0x8000; 0x8000 is avoided because the word is read sign-extended. Babel's Spear's template
  (`WeaponList[357 − 257] +0x39`) gets it (`ElfWeaponPatches.PatchConfuseAbility`).
- **The list.** The status window's SPECIAL list is message 0x1A6 of the menu bank, a column of placeholders that
  `MenuClsMes::NowWeaponStatus` (main 0x20B7F0) fills with each set bit's name: system-bank message (bit + 0x45). The
  names, ids 0x46–0x52 of `meswin\system_1.mes`: Big bucks, Poor, Quench, Thirst, Poison, Stop, Steal, Fragile, Durable,
  Drain, Heal, Critical, Abs up (bits 1–13). The loop's bound (`slti at,a3,0xE` at 0x20B8E4) is raised to 0xF; bit 14 would
  read 0x53 ("Slot 1", used elsewhere), so the name add (`addiu a0,a3,0x45` at 0x20B8A8) jumps to `tools/stubs/confuse_name.s`
  (DebugInfomationIF tail 0x1B561C), which gives bit 14 message **0x45** — free in the bank, just before Big bucks.
- **The named list's rows.** `MenuClsMes::Draw1` (0x20B9E0) draws each listed ability's icon (the same `charaface` tile) and its
  bar — blue for good, red for bad, by `IsWeaponOptionGoodOrBad` (0x20F770): a 14-entry short table at 0x293C60 (bits 0–13:
  0,1,0,1,0,1,1,1,0,1,1,1,1,1) copied to the stack, so bit 14 read past it. Its bound (`slti at,s1,0xE` at 0x20BDC4) → 0xF, and
  the function rewritten in place (9 of its 16 words): bit 14 good, every other bit read straight from the static table.
- **The name.** "Confuse" added as message 0x45 to the four English system banks (`system_1.mes` — what `LoadSystemMessage`
  reads — `systeme.bin` its fallback, and the `system14` pair), `ConfuseAbilityBakes`, through `MesTextBaker.AppendMes` with the
  padding KEPT: the bank is allocated (`CDataAlloc<1,6000>` at 0x1CBCA00) by its file size, so the file grows by exactly the
  entry (47,886 → 47,906 B), below Atlamillia Insurance's fixed name-channel area (buffer byte 48,256).
- **The icon.** `WeaponOptionStatusDraw` (0x20F7F0, bound `slti at,s5,0xE` at 0x20F9D4 → 0xF) draws 20×20 tiles from the
  `charaface` sheet (256×320, 8-bit, PSMT8-swizzled, in the IM2 bank `commenu\a_usa\charatex.img` and the dungeon menu pack
  `dunmenu5.pak`): bits 1–7 from column u 0xEC, bits 8–14 from u 0xD8, row v = 0x50 + 20·(n − 1) (bits 8 and 9 swap rows). Bit
  14's tile is (0xD8, 0xC8), empty, beside Steal. Its art is the asset `Resources/isoPatch/confuse_icon.png` (20×20 RGBA, drawn
  in the sheet's palette): decoded at patch time (`Png`, a minimal 8-bit RGB/RGBA reader), each pixel mapped to the sheet's
  nearest palette entry (its CLUT in CSM1 order; alpha < 128 → the tiles' clear corner entry), written in place into every US sheet whose Heal tile is the vanilla one — `charatex.img` and `dunmenu5.pak`; the older variants
  (`_charatex.img`, `nameregi.pak`, `dunmenu3/4/_chk.pak`) differ and are left alone.

## Phase 2 — the effect

- **The roll (ISO).** `tools/stubs/confuse_proc.s` (0x22B248, in the dead body of `DebugItemGetKey` — the item menu's debug
  sub-mode 5, which nothing sets; it and `DebugItemGetDraw` now return at once). CheckDmg's paths out of the Stop roll meet at
  0x1DBAA4 (`lw v1,0x90(s5)`), which jumps there: the hit's ability word (unit + slot·0x510 + 0x55754) has 0x4000, `rand()`
  under 5 % of 2³¹ (Stop's 4 %, Poison's 10 %), and the hit's shared roll f20 under the monster's status susceptibility
  (unit + slot·400 + 0x1E4AE; 0 = immune) — as Poison and Stop — set `CodeCaves.ConfuseProc[slot]` (0x01FAFFE0, a byte each).
- **The behaviour (mod).** `ConfuseAbility` (a loop from app start) confuses each proc'd enemy for 20 s and clears its byte
  (`Confusion`: the nearest enemy or the player). Every enemy a confused one hits remembers it, most recent first; one not
  confused goes after its most recent attacker still confused and hurts only it; when that attacker's confusion ends it turns
  on the next one still confused, else back to the player — so does a confused enemy whose own time runs out while others
  hit it. Provoking is the default (Babel's Spear too). The loop ticks Confusion whenever anyone is confused (weapons that
  confuse tick it as well; passes are locked and rate-limited) and drives the stars.
- Every enemy has its own timer: a hit's proc gives that enemy 20 s (a fresh proc restarts it); `Confusion.Unconfuse(slot)` ends
  one alone. Babel's summoned spear confuses every ACTIVE enemy on the floor for its duration — a dormant one (RenderStatus 1: a
  chest mimic still shut, or an enemy too far off to have been activated) joins the moment it wakes — and its end ends all confusion; the Terra nut's fade ends only its bonked enemy's. Its
  regular hits carry the ability natively. The stars never sit over a dormant enemy.

## Phase 3 — stars on every floor

- **The instance.** A CSHOT_EFFECT of its own in main BSS `frame_info_cam` (0x1E3D030, 62,400 B, referenced by nothing —
  no pointer, no `lui` pair, no gp-relative access in the ELF or the overlay): `CodeCaves.StarsInstance`. 8 sub-shots: the
  stars go to the confused enemies NEAREST the player; as one recovers or dies the next takes its star.
- **Stepped and drawn.** The second-effect caves (`ElfWeaponPatches.PatchSecondEffect`, hooked at the live-instance step /
  draw, dun 0x1DB8740 / 0x1DAEB90) end in a jump to a continuation in `DebugItemGetKey`'s body (`StarsTail`, 0x22B300 /
  0x22B380) that steps / draws the stars instance behind `CodeCaves.StarsGate` (0x01FAFFF0: live, region base, mark) — the
  mod's live word, the region's signature ("BSHT" + the mark, 16 B under its allocator base) and the monster pool at or past
  the mark — then their epilogue.
- **Entered once per floor.** `StarsLane`, from BorrowedShots' loop before that block's own effect is asked again: the block
  saved, the stars' request written (fresh region, 4,096 units, 8 sub-shots — the loader cave's count is now the block's
  +0x2BC), the cave answers, the block put back; the stars' config copied to `CodeCaves.StarsCfg` and the instance pointed at
  it; the gate opened. Leaving the floor closes it. `ConfusionStars` places, grows (0.25 s to 1.5×), loops and scales them
  (`Fade`: Babel's copy), the follow cave carrying each with its enemy. The Terra nut's own stars and the gem-slot lanes are
  gone.
