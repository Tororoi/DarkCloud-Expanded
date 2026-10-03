using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The one writer of the enemies' TARGET POINTER table (CodeCaves.PtrTable: per slot, the address the Mirage caves make
    /// `_GET_POSITION`/`_GET_DISTANCE` read as "the player"). Weapon targeting effects — Mirage's decoy, the Angel Gear's shield
    /// ring, the judgement blade's redirect — take the table whole (<see cref="Claim"/>, <see cref="Write"/>) and hand it back
    /// with every slot on the live player (<see cref="Release"/>). Only one weapon is ever active, so only one holds it at a
    /// time; a second claim displaces the first and is logged. Confusion is the exception: it points single slots
    /// (<see cref="PointConfused"/>) only while no weapon effect holds the table, is superseded the moment one does, and re-points
    /// its slots on its next tick once the table is released.</summary>
    internal static class AggroTable
    {
        private const string Tag = "[AggroTable] ";
        internal enum Holder { None, MirageDecoy, ShieldRing, JudgementBlade }

        /// <summary>The weapon effect holding the table (None = nobody; confusion may point slots).</summary>
        internal static Holder Held { get; private set; }
        /// <summary>Set by Confusion each tick: a confused or provoked enemy is being pointed somewhere.</summary>
        internal static bool ConfusionActive;
        private static readonly object _lock = new();

        /// <summary>A weapon effect takes the table (its first Write follows; nothing is written here).</summary>
        internal static void Claim(Holder who)
        {
            lock (_lock)
            {
                if (Held == who) return;
                if (Held != Holder.None) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{who} takes the table from {Held}");
                Held = who;
            }
        }

        /// <summary>The whole table, by its holder; a writer that does not hold it writes nothing.</summary>
        internal static void Write(Holder who, byte[] ptrs)
        {
            if (!Mirage.Armed) return;
            lock (_lock) { if (Held == who) Memory.WriteBytesBatch(CodeCaves.PtrTable, ptrs); }
        }

        /// <summary>The holder lets go: every slot back on the live player. A release by a displaced holder changes nothing.</summary>
        internal static void Release(Holder who)
        {
            lock (_lock)
            {
                if (Held != who) return;
                Held = Holder.None;
                ResetAll();
            }
        }

        /// <summary>Every slot on the live player (the table's rest state; also the cold-arm fill before any enemy reads it).</summary>
        internal static void ResetAll()
        {
            if (!Mirage.Armed) return;
            var buf = new byte[CodeCaves.TableSlots * CodeCaves.PtrStride];
            for (int s = 0; s < CodeCaves.TableSlots; s++) BitConverter.GetBytes(StbExternCmd.PlayerPosGuest).CopyTo(buf, s * CodeCaves.PtrStride);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, buf);
        }

        /// <summary>Confusion's pointer for one slot, written (when it differs) only while no weapon effect holds the table; false
        /// when a weapon effect does.</summary>
        internal static bool PointConfused(int slot, uint ptr)
        {
            if (!Mirage.Armed) return false;
            lock (_lock)
            {
                if (Held != Holder.None) return false;
                if (Memory.ReadUInt(CodeCaves.PtrAddr(slot)) != ptr) Memory.WriteUInt(CodeCaves.PtrAddr(slot), ptr);
                return true;
            }
        }
    }
}
