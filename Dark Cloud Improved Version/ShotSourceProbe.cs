using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMPORARY test: validate patching the CACHED per-slot melee-damage field that _SET_DMG_PARA writes at the
    /// enemy's init — MMU + slot*0x350 + 0x5A4D0 + bodyPart*4 (handler ELF 0x1E3FD0). Patching the STB after init
    /// is too late (the value is already latched here), so this is the real normalization target for live enemies.
    /// For the target species we scan that per-slot array for its native melee value and write the scaled value
    /// (idempotent: only rewrites entries still at the native value). Default: Arthur (Id 40), 116 -> 58.
    /// Remove once the real patcher is built.
    /// </summary>
    internal static class ShotSourceProbe
    {
        internal static bool Enabled       = false;  // disabled — melee normalization now lives in EnemyStatNormalizer
        internal static int  TargetSpecies = 40;   // Arthur (Id 40 / TableIndex 35)
        internal static int  NativeValue   = 116;  // Arthur's _SET_DMG_PARA value
        internal static int  ScaledValue   = 58;   // half

        // Per-slot _SET_DMG_PARA cache: base offset within MainMonstorUnit, per-slot stride, scan window.
        private const int DmgArrayOffset = 0x5A4D0;
        private const int SlotStride     = 0x350;
        private const int ScanBytes      = 0x100;

        private static readonly HashSet<long> _loggedAddrs = new();

        internal static void Tick()
        {
            if (!Enabled) return;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                long slotB = EnemyAddresses.FloorSlots.SlotAddr(s, 0);
                if (Memory.ReadInt(slotB + EnemySlotOffsets.RenderStatus) < 0) continue;
                if (Memory.ReadUShort(slotB + EnemySlotOffsets.EnemySpeciesId) != TargetSpecies) continue;

                long arr = EnemyAddresses.MainMonstorUnit.Base + (long)s * SlotStride + DmgArrayOffset;
                byte[] d = Memory.ReadByteArray(arr, ScanBytes);
                if (d == null || d.Length < 4) continue;
                for (int i = 0; i + 4 <= d.Length; i += 4)
                {
                    int v = d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
                    if (v != NativeValue) continue;          // only patch entries still at the native value
                    long addr = arr + i;
                    Memory.WriteInt(addr, ScaledValue);
                    if (_loggedAddrs.Add(addr))
                        Console.WriteLine($"[DmgCache] slot {s}: dmgArray+0x{i:X} (0x{addr:X8}) {NativeValue} -> {ScaledValue}");
                }
            }
        }
    }
}
