using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The Atla placement/collection system inside the CDngStatusData object (base 0x21CD954C).
    /// Layout derived from GetAtraNum/GetMaxAtraNum/GetAtraData/SetGetAtra (ELF 0x1BF770..),
    /// BtAtraListMake (0x1C09C0), BtAtraFloorCyoice (0x1C0D20, called by CDungeonMap::
    /// buildEventData at floor build — spawns entities for UNOBTAINED slots only) and
    /// BtAtraGetShort_Loop (0x1D2C70, the touch/collect flow → getAtraToSaveData →
    /// AtraPartsGet/AtraChipGet + GetAtraData marks the slot collected).
    /// </summary>
    internal static class AtlaSystem
    {
        internal const long StatusBase = 0x21CD954C;

        // ── Per-floor slots: 8 ints per floor, per dungeon ──
        internal const long SlotsBase       = StatusBase + 0x278;   // 0x21CD97C4
        internal const int  DungeonStride   = 0x500;                // 40 floors × 0x20
        internal const int  FloorStride     = 0x20;
        internal const int  SlotsPerFloor   = 8;
        internal const int  DungeonCount    = 6;
        internal const int  SlotEmpty       = -1;   // no atla
        internal const int  SlotRandomPart  = -2;   // unobtained, resolves to a random pool part
        internal const int  SlotCollected   = -3;   // obtained (read ONLY by the floor-select screen)
        // values >= 0: unobtained, index into the dungeon's parts list below

        internal static long SlotAddr(int dungeon, int floor, int slot) =>
            SlotsBase + dungeon * DungeonStride + floor * FloorStride + slot * 4;

        // ── Per-dungeon parts list: 100 entries of {int partId, int half, int count} ──
        // partId -1 = free entry. Collection decrements count and frees the entry at 0.
        internal const long PartsBase          = StatusBase + 0x2078;   // 0x21CDB5C4
        internal const int  PartsDungeonStride = 0x4B0;
        internal const int  PartsEntryStride   = 0xC;
        internal const int  PartsCount         = 100;

        internal static long PartsEntryAddr(int dungeon, int entry) =>
            PartsBase + dungeon * PartsDungeonStride + entry * PartsEntryStride;

        /// <summary>Part ids usable for mod-created atla: 24-39. AtraPartsGet no-ops for them
        /// in EVERY dungeon (GetEditPartsInfo 0x157FF0 and GetEditAtraPartsData 0x158E00 both
        /// hard-bound at id 0x17) — nothing is granted and nothing is written to save data.
        /// ⚠ Do NOT use ids ≥ 40: getAtraToSaveData routes them to AtraChipGet (0x158760),
        /// which has NO upper bounds check and appends the raw chip index (id-40) into the
        /// 128-short elem list in SAVE DATA (saveData + 0x4CCC + dungeon*0x100). The old
        /// sentinel 100 polluted saves with garbage value 60 this way (affected saves were
        /// cleaned by a since-removed one-time strip, 2026-07-05).</summary>
        internal const int SentinelPartIdFirst = 24;
        internal const int SentinelPartIdLast  = 39;

        /// <summary>EditElementData (ELF 0x25AF00): static per-(dungeon, partId) table of
        /// {int modelIdx, int nameIdx, int count}, 100 × 12B per dungeon. GetAtraMsgNo
        /// (0x218CC0) computes the ceremony/georama name message id as
        /// nameIdx + 1000 + 200*dungeon (+0x28 per category tier for ids ≥ 40). nameIdx is a
        /// full int32 and vanilla -1 for ids 24-39 — writing it points a sentinel part at ANY
        /// message id. Plain data (not code): safe to write via PINE.</summary>
        internal const long EditElementData = 0x2025AF00;
        internal static long NameIdxAddr(int dungeon, int partId) =>
            EditElementData + dungeon * 0x4B0 + partId * 12 + 4;

        /// <summary>Ceremony state global (BtAtraGetShort_Loop): 0 idle, 1 ball animation,
        /// 2 message shown. Returns to 0 in the same step that calls ClearSystemMes — the
        /// safe moment to undo name plumbing used by the message.</summary>
        internal const long AtraGetStatus = 0x202A3524;

        // ── Static ELF tables (int[6] / ptr[6]) ──
        internal const long MaxFloorTbl = 0x20279E40;   // floors per dungeon
        internal const long NoEntryTbl  = 0x20279E00;   // 6 native ptrs → -1-terminated lists of
                                                        // 1-based floor numbers atla must not use
    }
}
