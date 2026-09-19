# Dragon's Y — lock-on speed and the Gemron shot

`Weapons/Xiao/DragonsY.cs` (thread `CustomXiaoEffects.DragonsYEffect`), `Weapons/Xiao/BorrowedShots.cs`.

## Lock-on speed

While a lock is on (`PlayerAction.LockOnActive` / `LockOnTargetSlot`) Xiao moves at double speed. The dungeon walk is
ROOT MOTION: `motionDrive` copies her position from her root frame's accumulated translation every frame and the
camera-relative stick vector (built by `MoveChara`, dun 0x1DB08A0 → 0x1DC4540) only steers, so there is no ground-speed
constant — the speed is the moving clip at its play rate. Locked on she strafes with the attack-stance clips (c04b KEYs
19–22, frames 180–230 / 120–170; 18 is the stance idle), so the motion-speed override (+0xC60, −1 = the KEY rate) is held
at 1.5 while one of those plays, and put back otherwise. The game writes −1 on every motion change, so the hold is
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
without guard crush.

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

`BorrowedShots.Seed` writes a copy of the config of the element in use (mask → 2, flags masked to the element bits so
the Black Dragon's Freeze does not ride along) into runtime data at 0x21FAEF40 ("SHOT"
magic, count, copies from +0x10, slots from +0x250); `ElfCave.BorrowedShotsEnter` (0x1FB3F40), now the head of the per-frame step chain
(`jal step__5CSHOT`, dun 0x1DB874C — ahead of the Matador's follower), keeps it entered: each frame it checks that the
recorded slot still holds the config (a floor load rebuilds the pack) and that one is recorded at all (a re-seed for a
new element writes −1), and calls Entry only then — a mid-floor disc load, which the engine also does for character
switches. A full pack clears the block's magic until the mod seeds again (a floor change or a new element). One config at a
time, in ONE slot: the cave records the monster pool's fill level before each entry, and on a new element the mod
empties the slot (sub-shot flags, count, and the config pointer — an empty slot to Entry, as the loader leaves for a
species without shots), rewinds the pool to that level so the old model's memory is reused, and writes −1; the new
ball is entered the next frame. A first cut entering all five at floor load froze the load; a second left every
switched-to element resident. The rewind assumes nothing else took monster-pool memory after our entry mid-floor. `BorrowedShots.Start` (app start) keeps the block matched to Xiao's equipped inventory weapon — Dragon's Y with an element
→ that element's config, else cleared — so it is in place before a floor loads, and logs the slot word the cave writes. Firing is
the Guardian Reflector's `Set` replica (`ShotEffectPack` now holds the pack layout for both). Ruby's own balls
(`c05_f03`, loaded only for character 3) were rejected: the user wants the Gemrons', and her set would not fit Xiao's
heap anyway.

### BorrowedShots is the general loader

`Weapons/BorrowedShots.cs` is not Dragon's Y's: any ability hands `BorrowedShots.Start(...)` a provider (`Func<int>`)
answering with the `ShotEffectPack.CfgTable` index it wants entered right now (or −1), and fires it with
`BorrowedShots.Fire(cfgIndex, …)`. One config is entered at a time (the first non-negative answer); Dragon's Y's
provider is `DragonsY.WantedShot` (its per-element table `ShotEffectPack.DragonsYCfg`).
