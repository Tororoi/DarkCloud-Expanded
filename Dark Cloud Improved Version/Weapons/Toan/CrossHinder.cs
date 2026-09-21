using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Cross Hinder — roughly double damage and ABS against undead, and the reviving undead stay down (the death-script patch the bone key reuses).</summary>
    internal static class CrossHinder
    {
        // ── Cross Hinder "Sanctifier" ──────────────────────────────────────────────────────
        private const int CrossHinderAbsMult = 2;           // undead slot Abs multiplier (applied once per enemy)

        private const int CrossHinderPatchedThreshold = 0;  // rand(100) < 0 is never true → the revive branch never taken

        private const int ReviverSigBytes = 96;             // signature window before the threshold cell (verified unique + relocation-safe per file)

        /// <summary>The seven undead species whose STB death label (120) rolls a revive: the threshold cell
        /// (a push-t1 literal) sits at a fixed FILE offset; **`rand(100) &lt; threshold` = REVIVE** (via
        /// _STATUS_SET_LIFE + get-up), else the real death (verified against the interpreter: branch
        /// targets resolve codeBase-relative; the ≥-threshold path is red-flash → death motion → SET_DEAD).
        /// Patching the literal to 0 makes death certain. (All other undead die plainly — confirmed by a
        /// rigid 7-cell pattern sweep of ALL 172 monster STBs.)</summary>
        private static readonly (string Stb, int ThrCell, int OrigThr)[] UndeadRevivers =
        {
            ("monstor\\e01a.stb",  0x715C, 10),   // Master Jacket (10% revive)
            ("monstor\\e50a.stb",  0x4A08, 18),   // Mummy (18%)
            ("monstor\\e117a.stb", 0x7550, 16),   // Gacious (Enhanced) (16%)
            ("monstor\\e119a.stb", 0x54DC, 14),   // Horn Head (14%)
            ("monstor\\e124a.stb", 0x7494, 16),   // Gacious (16%)
            ("monstor\\e129a.stb", 0x7174, 10),   // Master Jacket (Enhanced)
            ("monstor\\e131a.stb", 0x4A08, 18),   // Mummy (Enhanced)
        };

        /// <summary>
        /// Ability Name: Sanctifier (Cross Hinder; Super Steve with its sphere has it too — <see cref="CrossHinderWielded"/>)
        /// Against UNDEAD enemies (slot category 1):
        ///   • ~2× damage — the BATTLE weapon record's anti-undead byte (+0x1C+1) is raised past the 99 menu
        ///     cap to the value the damage formula (dmg += dmg × 0.015 × anti) needs for double damage:
        ///     newAnti = (2×(1+0.015×native) − 1)/0.015, byte-capped at 255. Read live per hit, menu untouched.
        ///   • 2× ABS — every undead slot's kill-ABS value (+0x0B0) is doubled ONCE when it appears on the
        ///     floor (write-once, no per-hit racing); restored for live slots on unequip.
        ///   • NO REVIVAL — the loaded death scripts of the five reviving undead species are patched once per
        ///     floor: the revive roll's threshold literal is set to 0 so the revive branch never wins
        ///     (one u32 per script, restored on unequip). No engine call, no per-death watching.
        /// </summary>
        public static void CrossHinderEffect()
        {
            int n = EnemyAddresses.FloorSlots.Count;
            var absOriginal = new int[n];
            var absDoubled = new bool[n];
            var patched = new List<(long CellValueAddr, int OrigThr)>();
            byte floor = 0xFF;
            int nativeAnti = -1, targetAnti = -1;
            long antiAddr = WeaponHave.BattleWeaponRecord + WeaponHave.AntiArrayOffset + (int)EnemyCategory.Undead;

            while (CrossHinderWielded() && Player.InDungeonFloor())
            {
                Thread.Sleep(250);   // nothing here is latency-critical — all writes are one-time/asserted
                byte f = Memory.ReadByte(Addresses.checkFloor);
                if (f != floor)
                {
                    floor = f;
                    Array.Clear(absDoubled, 0, n);
                    patched.Clear();                 // old floor's STBs are gone — do NOT write stale addresses
                    PatchUndeadRevivers(patched);    // one multi-needle RAM sweep, then one u32 write per script
                }
                if (Player.CheckDunIsPaused()) continue;

                // 2× ABS: double each undead slot's kill-ABS once, as soon as it exists on the floor.
                for (int h = 0; h < n; h++)
                {
                    int render = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.RenderStatus));
                    if (render <= 0) { absDoubled[h] = false; continue; }   // empty/released slot → rearm
                    if (absDoubled[h]) continue;
                    if (Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.ResistancePack1))
                        != (ushort)EnemyCategory.Undead) continue;

                    long absAddr = EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.Abs);
                    absOriginal[h] = Memory.ReadInt(absAddr);
                    Memory.WriteInt(absAddr, absOriginal[h] * CrossHinderAbsMult);
                    absDoubled[h] = true;
                }

                // ~2× damage: keep the battle record's anti-undead byte asserted. The record is rebuilt
                // (re-capped) whenever equipment changes, which this self-heals: any value other than our
                // target is treated as the fresh native value and boosted from it.
                if (CrossHinderWielded())
                {
                    int cur = Memory.ReadByte(antiAddr);
                    if (cur != targetAnti)
                    {
                        nativeAnti = cur;
                        targetAnti = Math.Min(byte.MaxValue, (int)Math.Round((2.0 * (1 + 0.015 * nativeAnti) - 1) / 0.015));
                        Memory.WriteByte(antiAddr, (byte)targetAnti);
                    }
                }
            }

            // Restore on unequip / character switch / dungeon exit.
            if (Memory.ReadByte(Addresses.checkFloor) == floor)   // same floor → our patch addresses are still valid
            {
                foreach ((long addr, int orig) in patched)
                    if (Memory.ReadInt(addr) == CrossHinderPatchedThreshold) Memory.WriteInt(addr, orig);
                for (int h = 0; h < n; h++)
                    if (absDoubled[h] &&
                        Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.RenderStatus)) > 0)
                        Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(h, EnemySlotOffsets.Abs), absOriginal[h]);
            }
            if (nativeAnti >= 0 && Memory.ReadByte(antiAddr) == targetAnti)
                Memory.WriteByte(antiAddr, (byte)nativeAnti);
        }

        /// <summary>Whether the equipped weapon is the Cross Hinder, or Super Steve carrying its SynthSphere (the anti-undead
        /// byte, the ABS and the no-revival then apply to Super Steve's own record).</summary>
        internal static bool CrossHinderWielded()
        {
            int id = Player.Weapon.GetCurrentWeaponId();
            if (id == Items.crosshinder) return true;
            return id == Items.supersteve && SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord) == Items.crosshinder;
        }

        internal static void RestoreUndeadRevivers(List<(long CellValueAddr, int OrigThr)> patched)
        {
            foreach ((long addr, int orig) in patched)
                if (Memory.ReadInt(addr) == CrossHinderPatchedThreshold) Memory.WriteInt(addr, orig);
            patched.Clear();
        }

        /// <summary>Locate every loaded reviver STB (one 32MB sweep matching all five 96-byte signatures —
        /// the bytes just before each threshold cell, relocation-safe and unique per file) and patch the
        /// threshold literal to <see cref="CrossHinderPatchedThreshold"/>. Each hit is verified (the cell
        /// must be a push-t1 of the original threshold) before writing. Records what it patched for restore.</summary>
        internal static void PatchUndeadRevivers(List<(long, int)> patched)
        {
            var needles = new List<(byte[] Sig, int OrigThr)>();
            foreach ((string stb, int thrCell, int origThr) in UndeadRevivers)
            {
                byte[] file = GameDataFiles.TryReadEntry(stb);
                if (file == null || thrCell < ReviverSigBytes) continue;
                var sig = new byte[ReviverSigBytes];
                Array.Copy(file, thrCell - ReviverSigBytes, sig, 0, ReviverSigBytes);
                needles.Add((sig, origThr));
            }
            if (needles.Count == 0) return;

            const int Block = 0x40000;
            int overlap = ReviverSigBytes - 1;
            for (long off = 0; off < Memory.EeRamSize; off += Block - overlap)
            {
                int size = (int)Math.Min(Block, Memory.EeRamSize - off);
                if (size <= overlap) break;
                byte[] buf;
                try { buf = Memory.ReadBytesBatch(Memory.Pcsx2Base + off, size); }
                catch { continue; }
                if (buf == null) continue;

                for (int i = 0; i + ReviverSigBytes <= buf.Length; i++)
                {
                    foreach ((byte[] sig, int origThr) in needles)
                    {
                        if (buf[i] != sig[0]) continue;
                        bool match = true;
                        for (int j = 1; j < ReviverSigBytes && match; j++) match = buf[i + j] == sig[j];
                        if (!match) continue;

                        // The threshold cell follows the signature: verify push-t1 of a sane threshold.
                        // (Accept any 1..100 — Harder Enemy AI may have buffed it; the holy weapon overrides.
                        // Restore still writes the NATIVE original; Harder AI re-asserts on the next floor.)
                        long cell = Memory.Pcsx2Base + off + i + ReviverSigBytes;
                        int curThr = Memory.ReadInt(cell + 8);
                        if (Memory.ReadInt(cell) == 3 && Memory.ReadInt(cell + 4) == 1 &&
                            curThr > 0 && curThr <= 100)
                        {
                            Memory.WriteInt(cell + 8, CrossHinderPatchedThreshold);
                            patched.Add((cell + 8, origThr));
                        }
                    }
                }
            }
            if (patched.Count > 0)
                Console.WriteLine($"[CrossHinder] revive rolls disabled in {patched.Count} loaded script(s)");
        }
    }
}
