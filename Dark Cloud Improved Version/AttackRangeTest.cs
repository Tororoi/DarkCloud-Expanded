using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// TEMP test: per-enemy miniboss attack/engage range, worked around the shared per-species STB. The AI gates its
    /// behaviours by comparing the live _GET_DISTANCE (cmd 10) result against a float literal in the species' loaded
    /// STB — and re-reads that literal every frame. We can't give one enemy its own literal, but only the enemy
    /// NEAREST the player matters for the engaged attack decision. So each tick we find the nearest live enemy of each
    /// target species and set the shared threshold to match ITS status: nearest is a MINIBOSS → thresholds ×<see
    /// cref="Multiplier"/> (longer reach); nearest is regular → exact vanilla thresholds (regulars feel untouched).
    /// The STB is only rewritten when the nearest's status flips (dedup). Caveat: while scaled, other (farther)
    /// same-species enemies briefly share the longer range. Self-reverts on floor reload.
    /// Targets: Skeleton Soldier &amp; Cursed Rose. Set <see cref="Enabled"/> = false / delete after judging.
    /// </summary>
    internal static class AttackRangeTest
    {
        internal static bool  Enabled    = true;
        internal static float Multiplier = 1.4f;
        private const int FnGetDistance  = 10;   // _GET_DISTANCE STB command

        private static readonly int[] Targets = { Enemies.SkeletonSoldier.Id, Enemies.CursedRose.Id };

        // per loaded STB: the _GET_DISTANCE threshold literals (byte offset + original IEEE-754 bits, floor-fresh)
        private static readonly Dictionary<int, List<(int off, int origBits)>> _thresholds = new();
        // per loaded STB: currently scaled (miniboss) vs vanilla; missing => vanilla / untouched
        private static readonly Dictionary<int, bool> _scaled = new();
        private static int _lastFloorKey = -1;

        internal static void Tick()
        {
            if (!Enabled) return;
            if (!Player.InDungeonFloor()) { Reset(); _lastFloorKey = -1; return; }

            int floorKey = (Memory.ReadByte(Addresses.checkDungeon) << 8) | Memory.ReadByte(Addresses.checkFloor);
            if (floorKey != _lastFloorKey) { Reset(); _lastFloorKey = floorKey; }   // STB reloads fresh each floor

            // one pass over the slots: nearest live enemy per target species
            int[]   nearestSlot = new int[Targets.Length];
            float[] nearestDist = new float[Targets.Length];
            Array.Fill(nearestSlot, -1);
            Array.Fill(nearestDist, float.MaxValue);
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int ti = Array.IndexOf(Targets, EnemySlots.GetFloorEnemyId(s));
                if (ti < 0) continue;
                float dist = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, 0) + EnemySlotOffsets.DistanceToPlayer);
                if (dist < nearestDist[ti]) { nearestDist[ti] = dist; nearestSlot[ti] = s; }
            }

            for (int ti = 0; ti < Targets.Length; ti++)
                if (nearestSlot[ti] >= 0)
                    UpdateNearest(Targets[ti], nearestSlot[ti], nearestDist[ti]);
        }

        private static void Reset()
        {
            _thresholds.Clear();
            _scaled.Clear();
        }

        private static void UpdateNearest(int species, int nearestSlot, float nearestDist)
        {
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(nearestSlot));
            if (stbNative == 0 || stbNative == -1) return;

            bool wantScaled = MiniBoss.miniBossEnemyNumbers.Contains(nearestSlot);
            bool isScaled   = _scaled.TryGetValue(stbNative, out bool cur) && cur;
            if (isScaled == wantScaled) return;   // nearest's status unchanged → nothing to do

            Apply(stbNative, wantScaled);
            _scaled[stbNative] = wantScaled;
            Console.WriteLine($"[AttackRange] {EnemySlots.GetEnemyName((ushort)species)} nearest=slot {nearestSlot} ({nearestDist:F1}) -> {(wantScaled ? $"MINIBOSS x{Multiplier}" : "vanilla")}");
        }

        private static void Apply(int stbNative, bool scaled)
        {
            long stb = Memory.ToMmu(stbNative);
            foreach (var (off, origBits) in Thresholds(stbNative))
            {
                int bits = scaled
                    ? BitConverter.SingleToInt32Bits(BitConverter.Int32BitsToSingle(origBits) * Multiplier)
                    : origBits;
                Memory.WriteInt(stb + off, bits);
            }
        }

        // Walk the STB once to record each _GET_DISTANCE threshold literal (byte offset + floor-fresh bits).
        private static List<(int off, int origBits)> Thresholds(int stbNative)
        {
            if (_thresholds.TryGetValue(stbNative, out var list)) return list;
            list = new List<(int, int)>();
            _thresholds[stbNative] = list;

            long stb = Memory.ToMmu(stbNative);
            if (Memory.ReadInt(stb) != StbVm.Magic) return list;
            byte[] d = Memory.ReadByteArray(stb, 0xC000);
            if (d == null || d.Length < 0x10) return list;
            int Word(int i) => d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
            int code = Word(StbVm.CodeSectionOff);
            if (code < StbVm.LabelTableOff || code >= d.Length) return list;

            for (int x = code; x + StbVm.InstrSize <= d.Length; x += 4)
            {
                if (Word(x) != StbVm.OpExt || Word(x + StbVm.OperandB) != 0) continue;   // op21 ext-call
                int argc = Word(x + StbVm.OperandA);
                if (argc < 1 || argc > 10) continue;
                int fp = x - argc * StbVm.InstrSize;                                     // first pushed arg = funcId
                if (fp < code || Word(fp) != StbVm.OpPush3 || Word(fp + StbVm.OperandA) != StbVm.TypeInt) continue;
                if (Word(fp + StbVm.OperandB) != FnGetDistance) continue;

                // threshold = first op3 FLOAT literal within ~4 records after the call (skip variable comparisons)
                for (int k = 1; k <= 4; k++)
                {
                    int r = x + k * StbVm.InstrSize;
                    if (r + StbVm.InstrSize > d.Length) break;
                    if (Word(r) == StbVm.OpPush3 && Word(r + StbVm.OperandA) == StbVm.TypeFloat)
                    {
                        int litOff = r + StbVm.OperandB;
                        if (BitConverter.Int32BitsToSingle(Word(litOff)) > 0f)
                            list.Add((litOff, Word(litOff)));
                        break;
                    }
                }
            }
            return list;
        }
    }
}
