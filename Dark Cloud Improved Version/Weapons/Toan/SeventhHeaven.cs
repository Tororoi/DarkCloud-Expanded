using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>7th Heaven — a perfect guard that blocks every enemy attack and projectile.</summary>
    internal static class SeventhHeaven
    {
        // ── 7th Heaven "Divine Guard" ──────────────────────────────────────────────────────
        // Which BST shot types (34) we've downgraded 3→2 (global table). Which per-enemy melee dmg-cols we've
        // downgraded (per floor). Captured/species keyed like the Dark Cloud guard-crush to catch slot reuse.
        private static readonly bool[] _shBstDown = new bool[BehaviorScriptTable.Count];

        private static readonly bool[,] _shMeleeDown = new bool[EnemyAddresses.FloorSlots.Count, EnemyAddresses.EnemyAttackParams.MaxCols];

        private static readonly bool[] _shMeleeCaptured = new bool[EnemyAddresses.FloorSlots.Count];

        private static readonly ushort[] _shMeleeSpecies = new ushort[EnemyAddresses.FloorSlots.Count];

        private static byte _shLastFloor = 0xFF;

        /// <summary>
        /// Ability Name: Divine Guard (7th Heaven)
        /// While 7th Heaven is wielded, Toan's guard is perfect — it blocks EVERY enemy attack and projectile,
        /// including the heavy "type 3" hits that normally break guard. Mechanism: BtCheckDamageProc only lets a
        /// guard block reaction-type 2/4 attacks; type-3 ones bypass it. This watcher downgrades every incoming
        /// attack's reaction type from 3 to 2 while the sword is equipped — melee via each enemy's dmg-col param
        /// (EnemyAttackParams.ReactionAddr) and projectiles via the shot-effect table (BehaviorScriptTable +0x44)
        /// — so nothing is unguardable. Side effect (accepted): unguarded, those attacks now knockback instead of
        /// knocking you down (softer). The reaction types are set once at spawn, so this holds with no race; the
        /// originals are restored (3) on unequip / dungeon exit. 7th Heaven also inherits Dark Cloud's Guard Crush
        /// (its guard-break vs enemies) for lineage reasons — see DarkCloud.DarkCloudEffect + the Dungeon dispatch.
        /// </summary>
        public static void SeventhHeavenEffect()
        {
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                if (Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride) != Items.seventhheaven)
                    break;

                SeventhHeavenSoftenAttacks(true);
                Thread.Sleep(100);
            }
            SeventhHeavenSoftenAttacks(false);   // restore original reaction types on unequip / exit
        }

        internal static void SeventhHeavenSoftenAttacks(bool active)
        {
            // Projectiles: downgrade every type-3 shot type in the shot-effect table (BST +0x44) to type-2.
            for (int i = 0; i < BehaviorScriptTable.Count; i++)
            {
                long addr = Memory.Pcsx2Base | (uint)BehaviorScriptTable.FieldAddress(i, BehaviorScriptTable.HitReactionType);
                if (active && !_shBstDown[i])
                {
                    if (Memory.ReadInt(addr) == 3)
                    { Memory.WriteInt(addr, 2); _shBstDown[i] = true; }
                }
                else if (!active && _shBstDown[i])
                {
                    if (Memory.ReadInt(addr) == 2) Memory.WriteInt(addr, 3);
                    _shBstDown[i] = false;
                }
            }

            // Melee: downgrade each enemy's type-3 dmg-col reaction to type-2 (captured once per enemy per floor).
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _shLastFloor)
            {
                _shLastFloor = floor;
                for (int s = 0; s < _shMeleeCaptured.Length; s++)
                {
                    _shMeleeCaptured[s] = false;
                    for (int c = 0; c < EnemyAddresses.EnemyAttackParams.MaxCols; c++) _shMeleeDown[s, c] = false;
                }
            }
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                bool live = active &&
                    Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) >= 1;
                if (live)
                {
                    ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
                    if (_shMeleeCaptured[slot] && _shMeleeSpecies[slot] == species) continue; // already handled this enemy
                    _shMeleeSpecies[slot] = species;
                    _shMeleeCaptured[slot] = true;
                    uint[] rx = Memory.ReadUIntBatch(EnemyAddresses.EnemyAttackParams.ReactionAddr(slot, 0),
                                                     EnemyAddresses.EnemyAttackParams.MaxCols);
                    for (int col = 0; col < EnemyAddresses.EnemyAttackParams.MaxCols; col++)
                        if (rx[col] == 3)
                        {
                            Memory.WriteInt(EnemyAddresses.EnemyAttackParams.ReactionAddr(slot, col), 2);
                            _shMeleeDown[slot, col] = true;
                        }
                }
                else if (_shMeleeCaptured[slot])
                {
                    // Slot went inactive (enemy gone) or we're restoring on unequip: put type-3s back only when
                    // restoring (!active) — a dead enemy's stale param is overwritten on the next spawn anyway.
                    for (int col = 0; col < EnemyAddresses.EnemyAttackParams.MaxCols; col++)
                        if (_shMeleeDown[slot, col])
                        {
                            if (!active)
                            {
                                long a = EnemyAddresses.EnemyAttackParams.ReactionAddr(slot, col);
                                if (Memory.ReadInt(a) == 2) Memory.WriteInt(a, 3);
                            }
                            _shMeleeDown[slot, col] = false;
                        }
                    _shMeleeCaptured[slot] = false;
                }
            }
        }
    }
}
