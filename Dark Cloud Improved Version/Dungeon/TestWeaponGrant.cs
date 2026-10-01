using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>TEMP (testing Partisan) — remove once tested: on the first walkable dungeon frame of the mod session, the Partisan is
    /// put in Ungaga's weapons by the game's own pickup routine, CDngStatusData::GetItem (main 0x1BE060: an item id above 0x100 is a
    /// weapon — its owner looked up, the first empty weapon slot of theirs found, the record built by WepDataListToHaveCopy), called
    /// through the call-request cave; skipped when he already has one.</summary>
    internal static class TestWeaponGrant
    {
        private const uint GetItem = 0x001BE060;
        private const int  WeaponId = Items.partisan;
        private static bool _done;
        private static int  _tries;
        private static DateTime _next;
        private const int   MaxTries = 20;

        internal static void Tick()
        {
            if (_done || DateTime.UtcNow < _next) return;
            _next = DateTime.UtcNow.AddSeconds(1);                       // one attempt a second while it fails
            for (int slot = 0; slot < DngStatusData.MaxWeaponSlots; slot++)
                if (Memory.ReadUShort(DngStatusData.WeaponRecord(Player.UngagaId, slot)) == WeaponId)
                { _done = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[TestWeaponGrant] Ungaga already has the Partisan"); return; }
            bool called = NativeCall.Invoke(GetItem, out uint slotGot, (uint)(DngStatusData.Base - 0x20000000L), (uint)WeaponId, 0u, timeoutMs: 1500);
            if (called)
            {
                _done = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[TestWeaponGrant] " + ((int)slotGot >= 0 ? $"Partisan given to Ungaga (weapon slot {(int)slotGot})" : "GetItem found no free weapon slot for the Partisan"));
                return;
            }
            // Not taken: is the call-request cave in memory at all (its first word non-zero), and does the WHP-bill cave hand on to it?
            uint caveWord = Memory.ReadUInt(0x20000000L + CodeCaves.DebugIfCave.CallRequest);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[TestWeaponGrant] attempt {++_tries}: GetItem not taken within 1.5 s (call-request cave word 0x{caveWord:X8}{(caveWord == 0 ? " — the cave is not in this ISO: repatch" : "")})");
            if (_tries >= MaxTries) _done = true;
        }
    }
}
