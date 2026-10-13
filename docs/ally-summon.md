# Summoning an allied monster

A summoned ally is a real monster, run by the game's own monster code: its own STB script drives its AI, the engine steps,
animates, collides, hurts and kills it, and nothing about its behaviour is re-implemented by the mod. It takes no species row in
the floor's roster and none of the floor's 16 enemy slots. The Pocklekul's pockle is the one ally so far.

All per-frame work is native (ISO caves). The app arms the feature, logs what the native side reports, and runs the friendly fire,
which has to be planted from outside because the engine never tests one monster against another.

## The private monster manager

The floor's monsters live in one `CMonstorUnit` (`MainMonstorUnit`, 0x01DF87D0, 0x60750 bytes). It is a struct of parallel
16-entry arrays (units at +0x1E3D0 stride 0x190, characters at +0x1FCD0 stride 0x3510, scripts at +0x54DD0 stride 0x48, and more),
so a 17th slot cannot be added. The ally gets a **second, private manager** instead, whose unit 0 is the ally.

Every script command reads `NowMonstorUnit` (gp−0x6320), not `this`. The dungeon's three calls on the floor's manager
(`Step` at dun 0x1DB749C, `DrawMonstor` at 0x1DAE7E8, `DrawShadowMonstor` at 0x1DADD44) go through `ally_manager.s`. It makes each
call as before, then makes it again on the private manager with `NowMonstorUnit` pointing at it. A redirect helper in
`TargetRedirectCaves` makes the ally's unit read pointer-table entry 16 (16 + its unit) as "the player".

## The block

The manager, the ally's script arena and its model need 1.2 MB. They live in a block at the end of the summoner's character pool,
reserved by growing one allocation in `LoadChara2` (`DunPatches`, `PockleBlock.Units` = 75,000 units). The block ends where the
weapon pool starts, so it is found from the pool counters without a stored pointer. It is rebuilt (and empty) on every `LoadChara2`:
a floor change or a character switch.

| Offset | What |
|---|---|
| +0x00 | model allocator (CDataAlloc2) |
| +0x10 | script allocator |
| +0x20 | spawn point |
| +0x30 | idle point (beside the summoner) |
| +0x40 | magic `ALLY` |
| +0x44 | floor stamp (dungeon, floor, back floor) |
| +0x48 | target slot (−1 none) |
| +0x54 | texture-entry mask (summon scratch) |
| +0x70 | which species (0..7) |
| +0x74 | frames since the summon (growth) |
| +0x80 | aim point |
| +0x100 | the manager (0x60750 bytes) |
| +0x60850 | script arena (1,875 units) |
| +0x67D80 | the model (0xBD20 units, to the block's end) |

## Summoning

`ally_brain.s` runs once a frame after the floor's `Step`. A summon:

1. Copies the floor's manager into the block (its vtables, interpreters and shared collision model) and points its script table at
   the block's own arena. Entry 0 gets the arena and the rest are null, so `CleanViewMonstor`, which resets every script's
   allocator, never touches a floor monster's. It then resets the manager.
2. Checks there is VRAM room in the monsters' texture group (42) and notes the group's VRAM ends and its texture entries.
3. Notes the empty slots of the floor's shot pack (`ally_shots.s` before).
4. Loads the species with `SetupBaseModel` into the block's model allocator, with `NowMonstorUnit` on the private manager. Its
   textures join group 42 in place (the load takes nothing from the texture manager's copy buffer). Its shot configs enter the
   floor's shot pack, with their data in the block.
5. Notes the texture entries and the pack slots the load filled (`AllyTex`, `AllyShots`).
6. Spawns unit 0 with `SetupViewMonstor` in front of the summoner, stamps the block, and sets `AllyManager`. The game rides it from
   then on.

## Each frame

The brain validates the block (the pool still ends where it did, magic, floor stamp), the arming, the summoner and the event mode.
Then:

- **Growth:** `ally_grow.s` scales the model in over its first 20 frames (CCharacter +0x90).
- **Target:** the locked-on enemy, else the one it had, else the enemy nearest the summoner. The pointer-table entry names a point
  inside the enemy's nearest hit sphere (`ally_aim.s`). A large enemy's root sits deep inside its body, where the ally's approach and
  the block rule never let it get close enough to swing.
- **No enemy:** its script is parked on a YIELD each frame (`park`: `ScriptRunning` 1, the saved PC on YIELD records, speed 0) while
  `Step` keeps animating, moving and hurting it. It walks to the summoner's side (`ally_follow.s`, direct position steps, walk or run
  clip) and faces through the unit's own turn (+0x70 point, +0x84 rate). When an enemy appears, `release` restarts its AI from label
  100.
- **Leash:** more than 300 u from the summoner ends it.
- **Smoke:** while the summon smoke plays, its position is copied from the ally's each frame (`pin`, `AllyPin`).

## The end

When its block is gone or belongs to another floor, when it is disarmed, when the summoner leaves, when an event takes the floor,
when its script has killed it (unit 0 back to state −1), or when it is left behind, `ally_despawn.s` runs. It clears `AllyManager`
first, so nothing rides the manager any more, then the hold and its pointer-table entry. On the floor it was summoned on, it
empties the texture entries its load added, puts group 42's VRAM ends back, and empties the shot-pack slots its load filled
(`ally_shots.s` clear) when they still hold its configs.

## Combat

The engine never tests one monster against another, so every hit between the ally and the floor's enemies is a mod-planted pool
entry (`ConfusionAlly`, run from the Confusion tick; `AttackScan` reads the pool and the shot pack):

- **Its swings** go into the pool with mask 0 (`ally_hooks.s` swing at `CheckDmg` 0x1DA098), so they never hit the player. Owner 200
  with mask 0 identifies them exactly. A swing touching an enemy plants a player-form hit on that enemy, owned by the summoner: it
  gets the kill and the ABS. The damage is the swing's own, over the summoner's ranged distance factor.
- **Its shots** are stamped slot 0's. They are told apart from floor slot 0's by which unit they are first seen nearer, then swept
  against the enemies and taken through the engine's own contact.
- **Enemies' swings and shots on it** plant hits carrying only mask bit 8 (0x100). `ally_hooks.s` mask (at `CheckDmg` 0x1DAA7C) lets
  the ally's unit take bit 8 as well as the player's hits, and no floor unit takes bit 8, so an enemy overlapping it never takes a hit
  meant for it.
- **Provocation:** an enemy whose HP its hit lowers, or that its body stops (`ally_collide.s` → `ally_hooks.s` blocked →
  `AllyBlocked`), is pointed at it until it falls or goes, then back at the summoner.
- **Collision:** `ally_collide.s` (at `Step`'s `jal MoveChecMonster`, chained through the Babel's Spear cave) applies the block rule
  between the two managers' units both ways. `pblock` makes the player's move check the ally's manager too.
- **Drops:** at the summon the app clears the unit's drop flag (+0xDA) and ABS. The only thing it drops is gold its stealing swings
  took (+0x3C, which the death block drops).

## Where the code lives

| Stub | Host (dead ELF bodies) | What |
|---|---|---|
| `ally_manager.s` | sceMcSetFileInfo/Rename 0x11EDC8 | rides the three dun calls; calls the brain |
| `ally_follow.s` (+ turn tail) | sceSifSetRpcQueue.. 0x1176D8 (tail 0x11EEA0) | the walk; park and release |
| `ally_grow.s` | after the turn, 0x11EF4C | growth table; YIELD records |
| `ally_brain.s` (+ summon tail) | sceDmaSendN.. 0x10C7D0 (tail 0x1203D0) | per frame: charge, summon, validate, target, idle |
| `ally_aim.s` | sceDmaWatch.. 0x10CD20 | the aim point |
| `ally_despawn.s` | sceSifGetOtherData 0x117080 | the end |
| `ally_shots.s` | sceSifLoadElfPart.. 0x1191A0 | shot-pack slots in and out |
| `ally_hooks.s` | after ally_shots, 0x1192E4 | swing mask, hit mask, blocked, pin |
| `ally_collide.s` | sceSifLoadModuleBuffer 0x118C88 | monster and player block rules |

The brain is full (1,160 of 1,160 bytes); new per-frame work needs another host. `ElfAllyPatches` writes all of them and checks
every cross-call and hand-built address.

Data words (`CodeCaves`): `AllyManager` 0x01FAF7F8, `AllyBlocked` 0x01FAF7FC, `AllyShots` 0x01FAF824, `AllyTex` 0x01FAFCA0,
`AllyHold` 0x01FAFFC4, `AllyArmed` 0x01FAFFC8, `AllyGuard`/`AllyLatch` 0x01FAFFCC/D0, `AllyEvent` 0x01FAFFD4, `AllyPin` 0x01FAFFD8.

`AllyEvent` is `(count << 8) | code`:

| Code | Meaning |
|---|---|
| 0x10 + n | summoned (species n) |
| 2 | fell |
| 3 | disarmed, or the summoner is not in hand |
| 4 | block, floor or quiet floor gone |
| 5 | no block |
| 6 | no VRAM room |
| 7 | not loaded |
| 8 | not stood |
| 9 | a charge while one stands |
| 11 | left behind |

## What is the Pocklekul's

These parts are specific to the Pocklekul and change for another summon:

- The brain's guard charge (Ruby, R1, motion 9 or 33, 60 frames).
- The species roll: table index 10 + day, or Hell Pockle at 54.
- `AllyArmed` from `Pocklekul.cs`.
- The block's size and the summoner (Ruby's `LoadChara2` branch).
- The smoke and tint. The summon smoke is shared with the Cactus (`SummonSmoke`). The tint uses the unit's palette flash state:
  target +0x130, speed +0x168, weight +0x16C, count +0x164.

## Lessons

- **Never skip `Step` to hold the ally.** That froze its animation and its movement. Park its script instead.
- **Never write CCharacter +0x64 on a monster.** It is not a yaw there, and writing it sheared and stretched the model. Use the
  unit's turn point and rate.
- **Tint through the palette flash.** `PalletSet` rewrites the model's ambient every draw, so a direct tint flickers.
- **Empty the pack slots its load filled when it goes.** The pack dedupes by config pointer, so a later summon of the same species
  reused a slot whose data a different species' model had overwritten, and the EE died on the shot.
- **Read a pool entry once.** Entries are re-made every frame, so owner, mask and sphere from separate reads mix two entries.
- **`ori` is zero-extended.** In a `lui`/`ori` pair the `lui` takes the plain upper half. Two offsets built with `lui 2` landed
  0x10000 too far and wrote an unused unit.
