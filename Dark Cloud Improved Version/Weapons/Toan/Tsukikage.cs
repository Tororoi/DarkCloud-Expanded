using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Tsukikage — charge attacks build twice as fast.</summary>
    internal static class Tsukikage
    {
        // ── Tsukikage "Moonlit Focus" ──────────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Moonlit Focus (Tsukikage, Heaven's Cloud)
        /// Holding X charges the charge attack twice as fast as normal weapons. The engine adds
        /// 1/60 to the charge meter (<see cref="PlayerAction.ChargeMeter"/>) per wind-up frame
        /// (= 1.0/second); this effect feeds in a second 1.0/second of wall time while the
        /// wind-up state is active, so lunge readies in ~0.25s and whirlwind in ~0.75s. The
        /// charge level is re-derived from the meter every wind-up frame by the engine, so the
        /// flash/tier progression follows automatically. Heaven's Cloud inherits the effect for
        /// lineage reasons (Tsukikage builds up into it), on top of its own charge-scaling
        /// effect — firing the whirlwind sooner simply means less time for the blade to grow.
        /// </summary>
        public static void TsukikageEffect()
        {
            DateTime lastTick = DateTime.UtcNow;

            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride);
                if (equippedId != Items.tsukikage && equippedId != Items.heavenscloud)
                    break;

                DateTime now = DateTime.UtcNow;
                // Clamp so time spent outside the windup (or in a long poll hiccup) never dumps
                // a big lump into the meter the moment charging starts.
                float elapsed = Math.Min((float)(now - lastTick).TotalSeconds, 0.1f);
                lastTick = now;

                if (Player.CurrentCharacterNum() == Player.ToanId &&
                    !Player.CheckDunIsPaused() &&
                    Memory.ReadInt(PlayerAction.ChargeActionState) == PlayerAction.ActionWindup)
                {
                    float meter = Memory.ReadFloat(PlayerAction.ChargeMeter);
                    if (meter > 0f && meter < PlayerAction.ChargeMeterCap)
                    {
                        float boosted = Math.Min(PlayerAction.ChargeMeterCap,
                            meter + elapsed * PlayerAction.ChargeMeterPerSecond);
                        Memory.WriteFloat(PlayerAction.ChargeMeter, boosted);
                    }
                }

                Thread.Sleep(16);
            }
        }
    }
}
