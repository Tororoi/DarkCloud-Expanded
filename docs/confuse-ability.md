# The Confuse weapon ability

A new weapon ability beside Poison, Stop, Drain and the rest, entirely ISO-side. Native to Babel's Spear only; a SynthSphere of
it carries the ability to other weapons as any ability does. Built in phases.

## Phase 1 — shown in the menus (done)

- **The bit.** Ability word (`WEAPON_HAVE +0xEE`, Effect1 | Effect2 << 8) bit **0x4000** (Effect2 bit 0x40). No vanilla template
  sets 0x1, 0x4000 or 0x8000; 0x8000 is avoided because the word is read sign-extended. Babel's Spear's template
  (`WeaponList[357 − 257] +0x39`) gets it (`ElfConfusePatches.PatchConfuseAbility`).
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
  under 5 % of 2³¹ (Stop's 4 %, Poison's 10 %), and the monster's type (unit + slot·400 + 0x1E410) not 2 — set
  `CodeCaves.ConfuseProc[slot]` (0x01FAFFE0, a byte each). Gated as Critical is (1 %, type ≠ 2): a flat 5 % on every
  enemy but bosses and boss companions, status susceptibility ignored. Poison and Stop scale their chance by the
  susceptibility (ItemStatusRes; the hit's shared 0–100 roll under it), which made Confuse land at 2.5–3.5 % on typical
  enemies (50–70) — and the mod retunes ItemStatusRes per enemy for poison/stop, which must not move Confuse. Babel's
  Spear's floor-wide confusion skips bosses too.
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

The stars over confused enemies spin in a CSHOT_EFFECT of the mod's own — a RESIDENT instance — so that every confused enemy can
wear them on any floor, whatever weapons, items and character effects are in use. `StarsLane` owns the instance (carve, construct,
enter, re-enter, gate); `ConfusionStars` places, grows (0.25 s to 1.5×), loops and scales the stars (`Fade`: Babel's copy), the follow
cave carrying each with its enemy. The Terra nut's own stars and the gem-slot lanes are gone.

- **The instance.** A CSHOT_EFFECT (0xA160) carved by `StarsLane` from the top of the monster pool once per floor — the pool is a
  bump allocator the floor's load resets: the block zeroed, the pool's used counter bumped past it — its address in the gate's +0xC.
  Carved only between loader requests: the loader cave is the pool's only other mid-floor allocator, and it carves only while a
  request is in flight. When the instance and the stars' region (4,096 units; the stars take ~2,450 at 6 sub-shots) would not both
  fit, the floor goes without stars. 8 sub-shots: the stars go to the confused enemies NEAREST the player; as one recovers or dies
  the next takes its star.
- **Constructed before it is entered.** A CSHOT_EFFECT holds nine CCharacters (+0x10 and the eight sub-shots at +0x11C0),
  each with its vtable at +0xA0, written only by `__ct__12CSHOT_EFFECT` (0x143680) — which the game runs for its own static
  instances at boot. Initialize/Entry2 call through those vtables, so an unconstructed block jumps to garbage ("Jump to
  unaligned address 0x02228821" on entering a floor — first seen with the instance in `frame_info_cam`, then in the pool).
  StarsLane posts the carved instance to `CodeCaves.StarsConstruct` (0x01FAFF70); the stars STEP cave runs the constructor on
  it and clears the word; only then (the last-built sub-object's vtable checked non-zero) is the loader request written.
- **Texture block.** The cave enters a non-main instance into texture block 0x10 without clearing it, beside the main
  effect's. Every MAIN re-entry (any borrowed config — Ungaga's syougekiha / zibaku_f too — or a menu's character reload)
  runs DeleteTextureBlock(0x10) and refills the block from its VRAM base, so the stars' baked VRAM address then shows the new
  effect's pixels (seen: another effect's rings). StarsLane finds the stars' texture entry after each entry (name `e114ex`,
  block 0x10) and watches it; once cleared (Initialize__8CTexture zeroes block and name) the gate closes and the stars are
  entered again into the same instance and region (the block's allocator and mark written back, so the cave reuses it while
  its signature and mark hold).
- **Stepped and drawn.** The second-effect caves (`ElfConfusePatches.PatchSecondEffect`, hooked at the live-instance step /
  draw, dun 0x1DB8740 / 0x1DAEB90) end in a jump to a continuation in `DebugItemGetKey`'s body (`ElfConfusePatches.StarsTail`,
  0x22B300 / 0x22B380) that steps / draws the stars instance behind `CodeCaves.StarsGate` (0x01FAFFF0: live, region base, mark,
  instance) — the mod's live word, the region's signature ("BSHT" + the mark, 16 B under its allocator base) and the monster pool
  at or past the mark — then their epilogue. The check also covers the instance: it lies below the region, so a pool rewound
  under it fails the same test and nothing is stepped from stale memory.
- **Never under the cave.** The loader cave works on a request across frames (it loads the file and waits on the disc), so a
  block changed under it would mix two requests — the data of one under the config of the other. The block is never changed
  while a request is in flight (magic set, state 0): StarsLane starts only when the block is idle, waits for the answer with no
  timeout (an answer slower than 10 s is logged once; only a floor change abandons a request), and puts the block back only
  after it (1, or −1 "no room" — retried after 3 s, up to 3 tries a floor).
- **Only while the dungeon is quiet.** The cave reads the stars' file into the loader's read buffer, which the menus load
  into too. The quick character select (BtMiniChrSelect_Loop: sled 0 sets driveStepHold 0x2A3564 + frameCaputer 0x2A3568
  and holds the step; sled 2 runs StartQuickChange — every texture block deleted, quickchr.pac read into read_buffer — and
  sets dungeonMode 0x2A355C = 5, whose step runs the cave again) froze with an empty screen when a request written as it
  opened was served over its data: the request waited out the held step and was served once the menu's step resumed, over
  the menu's data. StarsLane asks only after 0.5 s of dungeonMode 1 with no hold, no capture, no pause/menu, re-checks before
  the magic word, and withdraws a pending request a menu opens under. The withdrawal is safe only while the cave has not begun
  on the request — the menu's opening holds the step, so the cave cannot be mid-way — and "begun" is read from the block's
  allocator words, still exactly as written: a carve fills the base in, a reuse zeroes the used count. A withdrawn try is not
  counted; it is asked again once the dungeon is quiet.
- **Entered once per floor.** The loader cave serves ONE request block, BorrowedShots'. `StarsLane`, from BorrowedShots' loop on
  each new floor before that block's own effect is asked again: the block saved, the stars' request written (the stars' config,
  the path, the carved instance, a fresh region — the loader cave's sub-shot count is the block's +0x2BC), the cave answers,
  the block put back as it was (its magic word last); the stars' config copied to `CodeCaves.StarsCfg` and the instance pointed
  at it, because the block's own config changes under it when BorrowedShots' effect is asked again; the gate opened with the
  instance and the region's base and mark, its live word last. Leaving the floor closes it.
