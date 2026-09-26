using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Weapon HP an ability spends — a bolt, a blast, a flash — taken by the ENGINE'S OWN drain at the moment it
    /// strikes, as if the sword had landed a hit. The bill is posted as swing-equivalents (base WHP / 1.5) to
    /// CodeCaves.WhpBill and the WHP-bill cave (ElfWeaponPatches.PatchWhpBill) calls SwordDmgCheck1 with it on the next
    /// frame: <c>(1.5 − 0.01 × Endurance) × factor</c>, halved by Durable and doubled by Fragile, the 10 % / 5 % warnings,
    /// an Auto Repair Powder at 0, and the break — the Dagger fallback, the message, the re-equip — are all the engine's,
    /// which is the only place the break exists (a WHP written to 0 from here would sit there until the next landed
    /// hit). One bill is in the words at a time; others posted before it is taken wait in <see cref="_pending"/> and
    /// go out on the following frames.</summary>
    internal static class WeaponWhp
    {
        private const float  SwingBase = 1.5f;                 // what one swing costs at zero Endurance: a factor of 1
        private const int    FlushMs = 8;                       // how often a waiting bill retries the words (the cave takes one a frame)
        private static readonly object _lock = new object();
        private static float  _pending;                         // swing-equivalents not yet in the words
        private static long   _watchAddr;                       // the WHP word of the last bill posted, read back once the engine has taken it
        private static string _watchTag;
        private static Thread _flusher;

        /// <summary>Post <paramref name="baseWhp"/> (the cost before Endurance) against the weapon in the ACTIVE character's
        /// hand, which must be <paramref name="weaponId"/>; the engine takes it on the next frame.</summary>
        internal static void Drain(ushort weaponId, float baseWhp, string tag)
        {
            if (Player.Weapon.GetCurrentWeaponId() != weaponId) return;        // not the blade that was spent
            int ch = Player.CurrentCharacterNum();
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(ch));
            if (bag < 0 || bag >= DngStatusData.MaxWeaponSlots) return;
            long rec = DngStatusData.WeaponRecord(ch, bag);
            if (Memory.ReadUShort(rec) != weaponId) return;
            float factor = baseWhp / SwingBase;
            float whp = Memory.ReadFloat(rec + WeaponHave.InventoryWeaponWhpOffset);
            lock (_lock) { _pending += factor; _watchAddr = rec + WeaponHave.InventoryWeaponWhpOffset; _watchTag = tag; }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"{tag}bills {baseWhp:F0} base WHP (×{factor:F2} swings) to the engine; WHP {whp:F0} before");
            Flush();
            if (_flusher == null || !_flusher.IsAlive)
            { _flusher = new Thread(FlushLoop) { IsBackground = true, Name = "WeaponWhp" }; _flusher.Start(); }
        }

        /// <summary>What waits goes into the words if the last bill has been taken (the cave zeroes the factor as it
        /// calls); the magic goes in first so the cave never reads a factor it should not act on.</summary>
        private static void Flush()
        {
            lock (_lock)
            {
                if (_pending <= 0f) return;
                if (Memory.ReadFloat(CodeCaves.WhpBill + CodeCaves.WhpBillFactor) != 0f) return;   // the last one is still to be taken
                Memory.WriteUInt(CodeCaves.WhpBill + CodeCaves.WhpBillMagic, CodeCaves.WhpBillMagicValue);
                Memory.WriteFloat(CodeCaves.WhpBill + CodeCaves.WhpBillFactor, _pending);
                _pending = 0f;
            }
        }
        private static void FlushLoop()
        {
            bool posted = true;
            while (true)
            {
                try
                {
                    Thread.Sleep(FlushMs);
                    if (_pending > 0f) { Flush(); posted = true; continue; }
                    if (posted && _watchAddr != 0 && Memory.ReadFloat(CodeCaves.WhpBill + CodeCaves.WhpBillFactor) == 0f)
                    {   // the engine has taken the last of it: say where the blade stands
                        posted = false;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"{_watchTag}taken by the engine; WHP {Memory.ReadFloat(_watchAddr):F1} now");
                    }
                    if (!posted) Thread.Sleep(50);
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WeaponWhp] flush failed: " + e.Message); Thread.Sleep(200); }
            }
        }
    }
}
