# Dragon's Y — lock-on speed and the Gemron shot

`Weapons/Xiao/DragonsY.cs` (threads `DragonsY.DragonsBreathEffect` and `DragonsY.LockOnSpeedEffect`),
`Weapons/BorrowedShots.cs`.

## Lock-on speed

`DragonsY.cs`. The buff is Dragon's Y's and is inherited by its line — Divine Beast Title, Angel Shooter and Angel
Gear (`DragonsY.LockOnSpeedGrants`) — and by Super Steve carrying any of the four's SynthSphere (driven from
`SphereInheritanceEffect`). Super Steve with a Dragon's Y sphere also has the charged shot below, of its own selected element
(`DragonsY.Wields`).

While a lock is on (`PlayerAction.LockOnActive` / `LockOnTargetSlot`) Xiao moves at 1.3× speed. The dungeon walk is
ROOT MOTION: `motionDrive` copies her position from her root frame's accumulated translation every frame and the
camera-relative stick vector (built by `MoveChara`, dun 0x1DB08A0 → 0x1DC4540) only steers, so there is no ground-speed
constant — the speed is the moving clip at its play rate. Locked on she strafes with the attack-stance clips (c04b KEYs
19–22, frames 180–230 / 120–170; 18 is the stance idle), so the motion-speed override (+0xC60, −1 = the KEY rate) is held
at 1.3 while one of those plays, and put back otherwise. The game writes −1 on every motion change, so the hold is
re-asserted each tick. (A native alternative — amplifying the per-frame root displacement, animation untouched — is
possible if the fast-footed look is unwanted.)

## The charged shot

Hold the shot for a second (charge flash, `ChargeTint` ramp, `ChargedShotWhp` ×2.0) and the pellet released becomes the
Gemron's ball of the selected element (elementHUD 00 Fire … 04 Holy): the pellet is taken the tick it appears and
`BorrowedShots.Fire` launches the ball from its position with its velocity — the pellet's own speed — at 1.5× its damage and
the pellet's remaining life; with no element selected it is the Black Dragon's shot (`b_boll`, cfg 22). Only when
this floor has no slot for it does the pellet itself fly on at ×5 sprite size with the same damage. The shot knocks
back with the Matador's kick (strength 2.5, decay 0.1): `ElfCave.CatGuardBypass` stamps it on any Xiao-owned entry
whose base damage equals `Mailbox.PelletKickDamage`, with the origin at the entry's own sphere centre (the burst), so
every enemy caught in the burst is shoved straight out of it in whatever direction that is (Heaven's Cloud does the
same from the mod side with `Enemies.RadialKnockback`); the guard window test then runs as vanilla — knockback
without guard crush. The cave tests the two damage marks BEFORE its cat shortcut (owner 1 + kick type 2 → pass): a
stamped entry carries the melee type from then on and outlives one CheckDmg, so with the shortcut first the shot passed
every guard after its first hit. The reorder made the cave 332 B, and it now lives in dun.bin over `MemoryMapDump`'s body
(`DunCave.CatGuardBypass` 0x1DAC070 — a printf-only debug routine; DunPatches writes the cave bytes and nops its three
callers), since the cave band had no such gap.

### Borrowed shots on every floor (RE)

A species' shot is a `BT_SHOT_EFFECT` config (0x70 B; 34 of them at ELF 0x27FA70) chosen by the species row's +0x68:
Fire `f_boll_3` (5), Ice `i_boll` (20), Thunder `t_boll` (23), Wind `e114a_ex` (24), Holy `e115a_ex` (25) — all shared
`dun\effect` files. The floor loader (`OpB_InitProcess`, dun) calls `SetupBaseModel` per species, which enters the config
into the floor's shot pack: `Entry__17CSHOT_EFFECT_PACK(NowShotEffect, cfg, read_buffer @*0x2A2384, 0x26, the monster
pool's CDataAlloc2 @0x1F066D0, 6)` — the pack has FIVE slots (`Entry` returns −1 when full, and a slot whose config
pointer matches is reused), the models go into the monster pool (~2.6 MB free). The config carries the victim mask at
+0x48 (1 = hurts the player, 2 = enemies), the element at +0x40, the wait at +0x38, the fly motion at +0x4E; the
sub-shot's step plants its damage entry with per-sub-shot owner (+0xA050), collider (+0xA070), ability flags (+0xA030,
SetWepStatus) and anti bytes (+0xA090, SetVsMonster) — so a Xiao-owned sub-shot with a mask-2 config is, to CheckDmg,
one of her pellets with the ball's element. The step plants that entry EVERY frame the phase has a radius (cfg +0x28 +
phase × 4: muzzle, flying, impact, expiry) and then holds off for the sub-shot's reload (+0xA138) frames — a Gemron's
explosion hurts an area for its whole animation, which on an enemy read as several hits per shot. So the copy's
FLYING radius (+0x2C) is zeroed (the contact sweep still runs) and the fired sub-shot's reload is 120 frames: the
impact's first frame is the one plant, with the full explosion radius, hitting every enemy inside it once; a miss's
expiry burst plants once likewise.

**Where Xiao's shots live (2026-09-19): the MAIN-CHARACTER effect instance, not the pack.** Beside the pack the ELF keeps
two more CSHOT_EFFECTs of the same layout, `CharaMainEffect` @0x1E8DA60 and `CharaMainEffectCrash` @0x1E97BC0 (one config,
six sub-shots each). The floor loader fills the first with the active character's own `wep_eff` effect
(`Get_Main_EffectPtr(charId, element)` dun 0x1DBA060 → a dun.bin table: Toan `c01_fuusya`, Xiao `mgan01`, Goro
`c06a_tameex`, Ruby `c05_?03` by element, Ungaga `c10a_ex`, Osmond his guns; `MainChara_Effect` dun 0x1DBA230 →
`Entry2__12CSHOT_EFFECT` 0x1AD260 from the read buffer, allocator = the character effects pool 0x1F06680, texture block
0x10) and the dungeon loop steps and draws it through the live pointer gp−0x6304 (0x2A34EC). Nothing of Xiao's ever fires
hers, so the borrowed shots take it over — and the pack's five slots stay the monsters'. The character effects pool is
only what her 4.24 MB heap has left after the cat-bearing model (211 KB, 70 KB of it mgan01), far too small for a
Gemron ball (25,500 units ≈ 408 KB), so the cave hands Entry2 an allocator of its own: a region carved from the
monster pool once per floor, reset to 0 used before every entry so a config switch mid-floor reuses it whole. The
region's size is the largest measured need among the known effects plus 1,024 units (`BorrowedShots.KnownUnits`: t_boll
19,867 … e114a_ex 23,822 → every known effect asks 24,846, so switching reuses one region; an unknown effect gets 32,768).
A region is reused only when its capacity covers the wanted config's reserve — the game's allocator answers an overflow
with an endless loop, which is what froze the game on an element switch when each effect carved with its own smaller
reserve; otherwise the cave carves a fresh, larger region — a DBC floor's pool is ~413,600 units, Wise Owl's ~278,800, and a Gallery floor
rostered with all five Gemrons had only 24,096 left after the species loaded. The enemy randomizer's per-floor budget
therefore subtracts `BorrowedShots.Headroom` (the seeded effect's reserve, 0 when none) so the species leave the shot
its room; a floor staged before Dragon's Y was equipped can still refuse it (state −1: the pellet ×5 fallback fires).

`BorrowedShots.Seed` writes a copy of the config (mask → 2, flying radius 0, flags masked to the element bits so the
Black Dragon's Freeze does not ride along) and the container's path into runtime data at 0x21FAEF40 ("SHOT" magic,
config +0x10, state +0x250, path +0x258, the cave's CDataAlloc2 +0x298, LoadFile's size out +0x2A8, reserve +0x2AC)
with state 0. `ElfCave.BorrowedShotsEnter` (in two pieces — the head at 0x1FB1ED0, the tail at 0x1FB3F40, the head ending
in a `b` to the tail; build_ee_stubs.py assembles both from one `#SPLIT` source — because the band has no 470 B gap and
can NEVER grow past 0x1FB4000: that is runtime data, the fishing bobber pointer and the cat's block, and code there
crashed PCSX2), the head of the
per-frame step chain (`jal step__5CSHOT`, dun 0x1DB874C — ahead of the Matador's follower), checks the instance each
frame: config pointer ours and state 1 → nothing. Config pointer not ours → the loader ran: a new floor, or a DUNGEON MENU
(`BtMenuLoadChara` reloads the character and calls `MainChara_Effect` again, refilling the instance with mgan01 — every
menu visit). If the pool's used counter still equals the mark the cave stored after its last carve (+0x2B0), nothing was
allocated since and the region is reused; otherwise carve a fresh one (pool used += reserve; over capacity → state −1,
magic dropped — the game's own overflow is a hang). Before the mark, every menu visit carved another region and a
nearly-full floor ran out after the first menu ("NOT entered" until the next floor). Then, and
on state 0 (another config seeded), re-enter the way the weapon-change path does: `Initialize__12CSHOT_EFFECT`,
`DeleteTextureBlock(mgr, 0x10)` + `CleanUpBuffer` + `CleanUpTextureList` (the previous effect's textures),
`LoadFile(path, read_buffer, &size)` + `wait_now_loading_vsync` (a mid-floor disc read, which the engine also does for
character switches), `Entry2(instance, cfg, read_buffer, 0x10, our allocator, 6)`, state 1, and the live pointer set to
the instance. `BorrowedShots.Start` (app start) keeps the block matched to what the providers want so it is in place
before a floor loads, restores the magic on each new floor, and logs the state the cave writes with the region's
usage (for tuning the reserve). Firing is the Guardian Reflector's `Set` replica at the instance's address
(`ShotEffectPack.CharaMainEffect`). Ruby's own balls (`c05_f03`, loaded only for character 3) were rejected: the user
wants the Gemrons'.

History: the first cut entered the ball into the monster pack (five slots shared with every shooting species on the
floor: a first version entering all five configs at floor load froze the load; then one slot reused per element with a
monster-pool watermark rewind). It cost the enemy randomizer a slot whenever Dragon's Y was out.

The pack's five slots are the monsters' — and since 2026-09-19 they are shared among every config a floor needs, so a
roster is no longer limited to five: docs/shot-slot-sharing.md.

### BorrowedShots is the general loader

`Weapons/BorrowedShots.cs` is not Dragon's Y's: any ability hands `BorrowedShots.Start(...)` a provider
(`Func<BorrowedEffect>`) answering with the effect it wants entered right now (or null) — `BorrowedShots.TableConfig(index)`
for one of the game's 34 (container under `dun/effect`), or `BorrowedShots.CustomConfig(template, name, muzzle, fly,
impact, expire, dir)` for the mod's own: a game config's numbers naming any `<dir><name>.chr` (`EffectDir` or `WepEffDir`,
so a character's effect such as Toan's `c01_fuusya` needs no archive copy) with the phase motions (KEY ordinals) that file
has — and fires it with `BorrowedShots.Fire(effect, …)` (a flying shot) or `BorrowedShots.Burst(effect, x, h, y, damage,
scale)` (a stationary one, played in its muzzle phase where it is planted). `BorrowedShots.DunConfig(addr, name)` reads a
character's own wep_eff config out of dun.bin once the overlay is resident — Toan's whirlwind (`ShotEffectPack.
WhirlwindCfg`: muzzle radius 20, motion 0, nothing after) was tried as Heaven's Cloud's blast on Super Steve through
`Burst` + `SetPhaseRadius` + `SetElement` and looked wrong as a burst, so Heaven's Cloud keeps the wind gem burst; the
plumbing stays for the next borrowed effect. One effect is entered at a time
(the first answer); Dragon's Y's provider is `DragonsY.WantedShot` (its per-element table `ShotEffectPack.DragonsYCfg`).
Every provider answers null unless Xiao is the ACTIVE character: the instance belongs to whoever is out, and Ruby's and
Osmond's own shots live in it.

Effect files under `dun/effect` no config names and no code references: `_b_boll`, `_f_boll_2`, `_f_boll_3`,
`_i_boll` (KEY 0 flight, 1 burst — older balls), `_f_boll` (KEY 0 stop, 1 flight; bbp/wgt), `zibaku_f`/`zibaku_r`/
`zibaku_t` (fire/ice/thunder bursts, one KEY), `explosion` (a large burst, one KEY), `es_zone` (one standing KEY).
Config layout past the mask: +0x4C/+0x4E/+0x50/+0x52 the muzzle/flying/impact/expiry motions (shorts, −1 = none);
+0x58 a float, +0x5C..+0x6C ints (unmapped).

**The loader's name rule.** `Entry__12CSHOT_EFFECT` (0x1ACC70) uses the config's one name twice: `dun/effect/%s.chr`
for the archive file, then `%s.cfg` for the record it asks that container for. A container whose cfg record is named
otherwise loads nothing — the slot is still handed back, but the monster pool watermark does not move (the tour log
showed `257316 → 257316` for every underscore ball, against 6–9 k units for the zibaku bursts). That is why the five
underscore balls (`b_boll.cfg` inside `_b_boll.chr`, …) and `es_zone` (`info.cfg`) showed nothing. The bake step
`tools/iso_patch/borrow_shot_effects.py` (IsoPatcher.BakeBorrowedShots) copies each such container onto its own
name with `<name>.cfg` appended (the source cfg's payload; the text still names the source's own records, which are
in the copy), so they load. The same table line borrows any other character's effect onto a dead name — a copy with
the appended record; the source file is never modified.
