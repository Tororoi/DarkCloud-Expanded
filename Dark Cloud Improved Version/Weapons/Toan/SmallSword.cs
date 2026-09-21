using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Small Sword — the opening combo swing comes out almost instantly.</summary>
    internal static class SmallSword
    {
        // ── Small Sword "Quick Draw" ───────────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Quick Draw (Small Sword, Tsukikage, Heaven's Cloud)
        /// While a Quick Draw sword is wielded, Toan's opening swing comes out almost instantly:
        /// once the first combo attack (action state 0x24) starts, the animation frame cursor
        /// (<see cref="PlayerAction.AnimFrameCursor"/>) is snapped forward past the wind-up
        /// to just before the hit window. ToanKey_Play gates the forward step-in (frames
        /// 820–820.5), weapon trail (824–825), swing sound (825) and hit window (825–828) off
        /// that same cursor, so skipping 820.5 → 824 keeps all of them — only the wind-up
        /// disappears (~14 game ticks → ~3). Follow-up combo hits (actions 0x25–0x28) and the
        /// charge attacks are untouched. Tsukikage and Heaven's Cloud inherit the effect for
        /// lineage reasons (the Small Sword builds up into them), on top of their own effects.
        /// </summary>
        public static void QuickDrawEffect()
        {
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride);
                if (equippedId != Items.smallsword && equippedId != Items.tsukikage &&
                    equippedId != Items.heavenscloud)
                    break;

                if (Player.CurrentCharacterNum() == Player.ToanId &&
                    !Player.CheckDunIsPaused() &&
                    Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionComboFirst)
                {
                    float frame = Memory.ReadFloat(PlayerAction.AnimFrameCursor);
                    // Wait until the engine's one-shot step-in write (820–820.5) has run, then
                    // fast-forward. The cursor advances 0.3/tick, so at least one poll always
                    // lands inside the window; a late catch just skips a little less.
                    if (frame >= PlayerAction.Combo1WindupSettled &&
                        frame < PlayerAction.Combo1TrailSpawn)
                        Memory.WriteFloat(PlayerAction.AnimFrameCursor,
                            PlayerAction.Combo1TrailSpawn);
                }

                // Tighter poll than the other effect loops: latency here directly delays the swing.
                Thread.Sleep(8);
            }
        }
    }
}
