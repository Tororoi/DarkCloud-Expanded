# Javelin — marine enemies (`Weapons/Ungaga/Javelin.cs`)

While the Javelin is the ACTIVE character's equipped weapon (Ungaga, or Xiao with Super Steve and a Javelin SynthSphere — `Javelin.Wielded`, started from her Super Steve case in `WeaponThreads`), every marine enemy on
the floor — slot category 2 (`EnemySlotOffsets.ResistancePack1` low ushort, copied from the species record's category at
spawn) — has:

| | |
|---|---|
| defense 0 | the packed defense pair `EnemySlotOffsets.DefenseStats` (+0x90: low = damage reduction, high = weapon defense, the one fed to SwordDmgCheck1) written 0 |
| ABS × 2 | `EnemySlotOffsets.Abs` (+0xB0, what the engine grants on the kill) doubled |

Gladius's pattern: each slot changed once, after it has been live 2 ticks (250 ms each — the spawn-time `EnemyStatNormalizer`
/ `EnemyStatScaler` have written its defense by then), re-armed when the slot empties, and the slots still live put back
(original defense and ABS) on unequip, character switch or leaving the floor.
