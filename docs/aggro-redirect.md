# The enemies' target table (aggro redirect)

The Mirage caves (docs/mirage, `Weapons/Ungaga/Mirage.cs`) make every monster script's `_GET_POSITION` / `_GET_DISTANCE`
read, instead of the player's position, the address held in a per-slot POINTER TABLE: `CodeCaves.PtrTable`
(0x21F19000, `CodeCaves.TableSlots` entries of `CodeCaves.PtrStride`). A slot pointing at the live player's position
(`StbExternCmd.PlayerPosGuest`) is vanilla; a slot pointing anywhere else makes that enemy chase, face and attack that
point instead. The caves are armed at the main menu (`Mirage.Armed`); without them the table is never read and nothing
below writes it.

## Who points the table where

| Effect | Owner | Points slots at |
|---|---|---|
| Mirage's decoy | `Mirage` (`AggroTable.Holder.MirageDecoy`) | fooled slots → `CodeCaves.DecoyPos`; the rest → the player |
| Angel Gear's shield ring | `AngelGear` (`ShieldRing`) | every enemy outside the ring → its own ring point (`SlingshotProp.RingTable`); nearer ones → the player |
| The judgement blade's fall (Big Bang, Sword of Zeus) | `EnemyFacing` (`JudgementBlade`) | every live slot → `CodeCaves.JudgementPos` |
| Confusion (the Confuse ability, Babel's Spear, the Terra nut) | `Confusion` (no holder) | each confused slot → its victim, the player or a wander point; each provoked slot → its attacker |

## The one writer: `AggroTable`

`Weapons/AggroTable.cs` is the only code that writes `CodeCaves.PtrTable`. The rule it enforces:

- **Only one weapon is ever active, so at most one weapon effect holds the table.** A weapon effect `Claim`s the
  table, `Write`s it whole while it runs (a writer that does not hold it writes nothing), and `Release`s it, which puts
  every slot back on the live player. A second claim while one is held displaces the first and is logged
  (`[AggroTable] X takes the table from Y`); the displaced holder's later release changes nothing.
- **Confusion is the exception and always yields.** It points single slots (`PointConfused`) only while no weapon effect
  holds the table. The moment a weapon effect claims it, confusion's pointers are overwritten by that effect's table and
  its further writes are refused; when the effect releases, the table rests on the player and confusion's next tick
  re-points its slots (it writes a slot whenever the table's value differs from what it wants). Confusion's own state
  (timers, provokers, friendly fire, tint, stars) runs on regardless.
- `AggroTable.ConfusionActive` is the flag Confusion raises while any slot is confused or provoked; it is informational
  (ConfuseAbility ticks Confusion while it is up). The Mirage decoy no longer writes the table on every tick while idle:
  the table's rest state is "every slot on the player", written at cold-arm (`ResetAll`) and by every release.

## Why

Before this, Mirage rewrote the whole table every fast tick whenever a dungeon floor was up — standing down only for the
ring and for confusion — so a judgement-blade redirect, which set no ownership flag, was overwritten within one Mirage
tick. Each effect also kept its own "everything back to the player" loop. The owner model replaces three writers and
two by-convention flags with one priority rule, the one the user set: one weapon targeting effect at a time, confusion
superseded by any of them.
