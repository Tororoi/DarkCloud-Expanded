using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The one writer of the enemies' TARGET POINTER table (CodeCaves.PtrTable: per slot, the address the redirect caves
    /// (<see cref="TargetRedirectCaves"/>) make `_GET_POSITION`/`_GET_DISTANCE` read as "the player"; every write here is gated on
    /// <see cref="TargetRedirectCaves.Armed"/>). Weapon targeting effects — Mirage's decoy, the Angel Gear's shield
    /// ring, the judgement blade's redirect, the Pocklekul's pockle (the enemies it has hit) — take the table whole (<see cref="Claim"/>, <see cref="Write"/>) and hand it back
    /// with every slot on the live player (<see cref="Release"/>). Only one weapon is ever active, so only one holds it at a
    /// time; a second claim displaces the first and is logged. Confusion is the exception: it points single slots
    /// (<see cref="PointConfused"/>) only while no weapon effect holds the table, is superseded the moment one does, and re-points
    /// its slots on its next tick once the table is released.</summary>
    internal static class AggroTable
    {
        private const string Tag = "[AggroTable] ";
        internal enum Holder { None, MirageDecoy, ShieldRing, JudgementBlade, Pockle }

        /// <summary>The first entry past the floor's 16: a unit of the ally's private monster manager reads entry AllySlot + its unit
        /// (TargetRedirectCaves' helper). Never a floor slot's: the holders' writes and the resets leave it as <see cref="SetAlly"/> set it.</summary>
        internal const int AllySlot = 16;
        private static uint _allyTarget;                      // the ally's target pointer (0 = the live player)

        /// <summary>The ally's target pointer (a 16-byte-aligned vec4's guest address; 0 = the live player).</summary>
        internal static void SetAlly(uint ptr)
        {
            lock (_lock)
            {
                _allyTarget = ptr;
                if (TargetRedirectCaves.Armed || ptr == 0) Memory.WriteUInt(CodeCaves.PtrAddr(AllySlot), ptr != 0 ? ptr : StbExternCmd.PlayerPosGuest);
            }
        }

        private static void KeepAlly(byte[] ptrs)
        {
            if (_allyTarget != 0 && ptrs.Length >= (AllySlot + 1) * CodeCaves.PtrStride)
                BitConverter.GetBytes(_allyTarget).CopyTo(ptrs, AllySlot * CodeCaves.PtrStride);
        }

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
            if (!TargetRedirectCaves.Armed) return;
            lock (_lock) { if (Held == who) { KeepAlly(ptrs); Memory.WriteBytesBatch(CodeCaves.PtrTable, ptrs); } }
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

        /// <summary>Every slot on the live player (the table's rest state; also the cold-arm fill before any enemy reads it — written
        /// whether or not the caves are armed yet, since TargetRedirectCaves fills it while arming).</summary>
        internal static void ResetAll()
        {
            var buf = new byte[CodeCaves.TableSlots * CodeCaves.PtrStride];
            for (int s = 0; s < CodeCaves.TableSlots; s++) BitConverter.GetBytes(StbExternCmd.PlayerPosGuest).CopyTo(buf, s * CodeCaves.PtrStride);
            KeepAlly(buf);
            Memory.WriteBytesBatch(CodeCaves.PtrTable, buf);
        }

        /// <summary>Confusion's pointer for one slot, written (when it differs) only while no weapon effect holds the table; false
        /// when a weapon effect does.</summary>
        internal static bool PointConfused(int slot, uint ptr)
        {
            if (!TargetRedirectCaves.Armed) return false;
            lock (_lock)
            {
                if (Held != Holder.None) return false;
                if (Memory.ReadUInt(CodeCaves.PtrAddr(slot)) != ptr) Memory.WriteUInt(CodeCaves.PtrAddr(slot), ptr);
                return true;
            }
        }
    }
}
