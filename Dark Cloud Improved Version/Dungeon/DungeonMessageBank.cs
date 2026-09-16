using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Where the dungeon's message text lives THIS boot.
    ///
    /// The dungeon overlay loads <c>dun/message/ww_mes/dunmsd00_&lt;lang&gt;.mes</c> into a pool at GameInit and hands the
    /// address to ClsMes — <c>SetBuff__6ClsMes</c> (0x14DA00) stores it at +0x17A0. The mod overwrites two of those messages
    /// with its own text (<see cref="Dayuppy"/>), and used to do that at addresses captured from a vanilla run:
    /// 0x20998BB8 (message 10) and 0x20999EE8 (message 3319), i.e. the bank at 0x00998840 plus each message's own offset.
    ///
    /// Those constants died the moment the cat's character heap grew. GameInit carves every dungeon pool out of one 27 MB
    /// buffer in order, so raising the heap by 55,000 units pushed everything carved after it — this bank included — up by
    /// 880,512 B. The writes kept going to the old addresses, which now land in the TEXTURE pool, on top of the `gaiji`
    /// glyph sheet the dungeon draws all its text from: a ~200-byte band of message text straight through one row of
    /// letters. Every dungeon window drew b, c and d with a line struck through them, in dungeons only, with the app
    /// closed, on the patched disc alone — and the bytes recovered from the glyph sheet decoded as "Horn Head … no sign of
    /// monsters on this floor" (user 2026-09-15).
    ///
    /// So the address is resolved from the live bank instead, and a bank that cannot be read means the message is skipped
    /// rather than written somewhere arbitrary. The .mes layout (see MesTextBaker): u16 count, u16 endOff,
    /// count × {u16 id, u16 wordOff}, then the text; message <c>id</c>'s text starts at byte 2 × (count + wordOff + 1).
    /// </summary>
    internal static class DungeonMessageBank
    {
        private const string Tag = "[MesBank] ";
        /// <summary>The dungeon's ClsMes (dun BSS) and the field SetBuff fills with the loaded bank.</summary>
        private const long ClsMesDungeon = 0x21EB6420;
        private const int  BuffPtr = 0x17A0;
        /// <summary>Where the bank sits in a VANILLA dungeon. Kept only so the log can say how far it has moved.</summary>
        internal const uint VanillaBankBase = 0x00998840;
        /// <summary>The two messages the mod rewrites: its own dungeon notice, and the floor-clear/last-enemy slot.</summary>
        internal const int CustomId = 10, FloorClearId = 3319;

        private static uint _bank;                  // the bank pointer the index below came from (0 = nothing cached)
        private static ushort[] _ids, _offs;
        private static bool _missLogged;

        /// <summary>MMU address of message <paramref name="id"/>'s text, or 0 when the bank cannot be read.</summary>
        internal static long TextAddress(int id)
        {
            if (!Refresh()) return 0;
            for (int i = 0; i < _ids.Length; i++)
                if (_ids[i] == id) return Memory.ToMmu(_bank) + 2 * (_ids.Length + _offs[i] + 1);
            if (!_missLogged)
            {
                _missLogged = true;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"message {id} is not in the dungeon bank @0x{_bank:X}");
            }
            return 0;
        }

        /// <summary>Re-read the bank's index when the pointer changes (every dungeon load, and any time the pools move).</summary>
        private static bool Refresh()
        {
            uint bank = Memory.ReadUInt(ClsMesDungeon + BuffPtr) & Memory.PhysAddrMask;
            if (bank != 0 && bank == _bank && _ids != null) return true;
            _bank = 0; _ids = null; _offs = null;
            if (!Memory.IsValidGuest(bank)) return false;

            long mmu = Memory.ToMmu(bank);
            int count = Memory.ReadUShort(mmu);
            if (count <= 0 || count > 4096) return false;                  // not a .mes header — the dungeon is not loaded
            byte[] index = Memory.ReadByteArray(mmu + 4, count * 4);
            if (index == null || index.Length < count * 4) return false;

            var ids = new ushort[count]; var offs = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                ids[i]  = BitConverter.ToUInt16(index, i * 4);
                offs[i] = BitConverter.ToUInt16(index, i * 4 + 2);
            }
            _bank = bank; _ids = ids; _offs = offs; _missLogged = false;
            long moved = (long)bank - VanillaBankBase;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"dungeon message bank @0x{bank:X} ({count} messages)"
                              + (moved != 0 ? $" — {moved:+#,0;-#,0} B from where a vanilla dungeon puts it" : ""));
            return true;
        }
    }
}
