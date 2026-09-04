using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Repairs EXPLODED cloth on the town player. On the initial town load, EdLoadMainChara builds the
    /// character at the origin (SetPosition(0,0,0)) and the game then teleports it to the spawn point —
    /// so the cloth solver gets one frame with a ~1400-unit spring stretch, and a piece's particles can
    /// blow up to ±1e35..1e38 (squaring those overflows fp32 → the sim never recovers). Visual: that
    /// piece renders in deep space = "missing" (Toan's FRONT cape half on a fresh load). A character
    /// swap fixed it only because InitCloth re-seeds the particles from the current pose.
    ///
    /// Savestate forensics (2026-09, no-cape vs cape-OK): CCloth layout — grid dims at +0x2C × +0x30
    /// (piece particle count, e.g. 9×7=63), CURRENT particle positions at +0x1110 (stride 0x10 xyz_),
    /// PREVIOUS (Verlet pair) at +0x2110, rest pose at +0xF0.. (intact in both states). In the broken
    /// state 54/63 particles were exploded in BOTH arrays while particle 0 (the anchor) stayed sane —
    /// the skeleton drives it every frame.
    ///
    /// Fix: when a particle's coordinate is non-finite or beyond any sane world bound, snap its xyz in
    /// BOTH arrays to the anchor's position (prev == current also zeroes its velocity); the piece then
    /// re-drapes naturally over the next few frames, exactly like a fresh swap. Healthy particles are
    /// left untouched. Character-agnostic — covers any town character with cloth, every town entry.
    /// </summary>
    internal static class ClothUnstick
    {
        private const long ClothListOff  = 0xC74;    // CCharacter +0xC74 → 4-slot CCloth pointer array
        private const int  ClothMaxPieces = 4;       // Draw__CCharacter walks 4 cloth slots
        private const long DimAOff       = 0x2C;     // CCloth grid dims: count = [+0x2C] × [+0x30]
        private const long DimBOff       = 0x30;
        private const long CurArrOff     = 0x1110;   // current particle positions, stride 0x10 (x,y,z,_)
        private const long PrevArrOff    = 0x2110;   // previous positions (Verlet pair), same layout
        private const int  ParticleStride = 0x10;
        private const int  MaxParticles  = 256;      // 16×16 grid cap (CCloth ctor)
        private const float ExplodedBound = 1e6f;    // world coords are ±2e4; anything past this is garbage

        private const int TickInterval = 20;         // main loop ticks (~50 ms each) between scans → ~1 s
        private static int _tick;

        private static void Log(string m) =>
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[ClothUnstick] " + m);

        internal static void Tick()
        {
            if (++_tick < TickInterval) return;
            _tick = 0;

            // Only touch a settled character: during events/loads (incl. the ally swap) the cloth chain
            // can be mid-rebuild. Explosion state persists, so catching it on a walking tick is enough.
            if (Memory.ReadInt(EditLoop.GameMode) != EditLoop.GameModeWalking) return;

            uint chara = Memory.ReadUInt(EditLoop.CharaPtr) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(chara)) return;
            long charaMmu = Memory.ToMmu(chara);
            uint list = Memory.ReadUInt(charaMmu + ClothListOff) & Memory.PhysAddrMask;
            if (!Memory.IsValidGuest(list)) return;
            long listMmu = Memory.ToMmu(list);

            for (int i = 0; i < ClothMaxPieces; i++)
            {
                uint piece = Memory.ReadUInt(listMmu + i * 4) & Memory.PhysAddrMask;
                if (piece == 0) continue;
                if (!Memory.IsValidGuest(piece)) continue;
                RepairPiece(i, Memory.ToMmu(piece));
            }
        }

        private static void RepairPiece(int slot, long piece)
        {
            int count = Memory.ReadInt(piece + DimAOff) * Memory.ReadInt(piece + DimBOff);
            if (count <= 1 || count > MaxParticles) return;

            byte[] cur = Memory.ReadBytesBatch(piece + CurArrOff, count * ParticleStride);

            // Anchor = particle 0 of the current array (skeleton-driven, stays sane even when the rest
            // explode). If IT is broken too, fall back to skipping — nothing safe to seed from.
            float ax = BitConverter.ToSingle(cur, 0);
            float ay = BitConverter.ToSingle(cur, 4);
            float az = BitConverter.ToSingle(cur, 8);
            if (!Sane(ax) || !Sane(ay) || !Sane(az)) return;
            byte[] anchor = new byte[12];
            Buffer.BlockCopy(cur, 0, anchor, 0, 12);

            int fixedCount = 0;
            for (int p = 1; p < count; p++)
            {
                int off = p * ParticleStride;
                float x = BitConverter.ToSingle(cur, off);
                float y = BitConverter.ToSingle(cur, off + 4);
                float z = BitConverter.ToSingle(cur, off + 8);
                if (Sane(x) && Sane(y) && Sane(z)) continue;

                // Snap xyz to the anchor in BOTH Verlet arrays (prev == current → zero velocity); the
                // 4th word of each entry is preserved. The solver re-drapes the piece from there.
                Memory.WriteByteArray(piece + CurArrOff + off, anchor);
                Memory.WriteByteArray(piece + PrevArrOff + off, anchor);
                fixedCount++;
            }
            if (fixedCount > 0)
                Log($"cloth slot {slot}: re-seeded {fixedCount}/{count - 1} exploded particles at the anchor " +
                    $"({ax:F0}, {ay:F0}, {az:F0}) — piece re-drapes");
        }

        private static bool Sane(float v) =>
            !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < ExplodedBound;
    }
}
