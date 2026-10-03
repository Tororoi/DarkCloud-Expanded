using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Kitchen Knife — a healing spring blesses the knife: triple length, double attack, for a minute, and its
    /// weapon HP mended to full each time Toan steps into the water.</summary>
    internal static class KitchenKnife
    {
        // ── Kitchen Knife: "Spring's Blessing" ───────────────────────────────────────────
        private const float KkTargetLength   = 12f;    // blade length while blessed (stock dcol1 = 4.187)

        private const float KkAttackMult     = 2f;     // battle-copy attack multiplier while blessed

        private const int   KkBoostSeconds   = 60;     // blessing duration; re-entering the spring refreshes it

        /// <summary>
        /// Kitchen Knife bonus: stepping into a healing spring blesses the knife — the blade instantly
        /// grows to length <see cref="KkTargetLength"/> (mesh scale, visual + dcol hit together) and the
        /// BATTLE-copy attack is doubled (past the menu cap, Quick-Draw style). Lasts
        /// <see cref="KkBoostSeconds"/>s from the moment the player last stood in the spring; flavor
        /// messages mark the blessing and its fading (no numbers shown). Scale and attack are re-asserted
        /// each tick, so floor reloads / battle-record refreshes can't strand a half-applied boost. Each
        /// step INTO the water (the edge, so wading about does not spam it) also mends the knife's weapon
        /// HP to its maximum — the inventory record, the copy the engine drains and the menu shows.
        /// </summary>
        public static void SpringsBlessingEffect()
        {
            bool boosted = false, wasInSpring = false;
            bool warnedNoBlade = false;
            ushort baseAtk = 0;
            DateTime deadline = DateTime.MinValue;
            bool haveKk = ToanWeapons.TryGetValue(Items.kitchenknife, out WeaponData kk);
            float factor = KkTargetLength /
                (haveKk && kk.Dcol1.HasValue ? Math.Abs(kk.Dcol1.Value) : 4.187f);
            // The blade frame is named after the weapon's OWN model code: c01w08 → "w08". It is NOT a shared
            // "blade" name — Heaven's Cloud's is "w14" (c01w14), which is why hardcoding that found nothing here.
            string kkCode = haveKk ? kk.Code : "c01w08";

            while (Player.Weapon.GetCurrentWeaponId() == Items.kitchenknife && Player.InDungeonFloor())
            {
                Thread.Sleep(100);
                if (Player.CheckDunIsPaused()) continue;   // the countdown is on GameClock, so it holds on its own

                // InZoneFlag is a 16-bit field (the engine writes it with `sh`) — reading it as an int pulls in
                // the adjacent halfword, so the == 1 test fails whenever that neighbour is non-zero.
                bool inSpring = Memory.ReadUShort(HealingSpring.InZoneFlag) == 1 &&
                                Player.CurrentCharacterNum() == Player.ToanId;
                long atkAddr = WeaponHave.BattleWeaponRecord + WeaponHave.EffAttackOffset;

                if (inSpring && !wasInSpring) MendWhp(!boosted);             // stepping in: the knife made whole
                wasInSpring = inSpring;
                if (inSpring)
                {
                    if (!boosted)
                    {
                        baseAtk = Memory.ReadUShort(atkAddr);
                        Memory.WriteUShort(atkAddr, (ushort)Math.Min(baseAtk * KkAttackMult, ushort.MaxValue));
                        boosted = true;
                        DungeonMessages.DisplayMessage("The spring's blessing surges\nthrough the Kitchen Knife!", 2, 30, 4000);
                    }
                    deadline = GameClock.Now.AddSeconds(KkBoostSeconds);   // standing in the water keeps it fresh
                }

                if (boosted)
                {
                    if (GameClock.Now > deadline)
                    {
                        ushort cur = Memory.ReadUShort(atkAddr);
                        if (cur == (ushort)Math.Min(baseAtk * KkAttackMult, ushort.MaxValue))
                            Memory.WriteUShort(atkAddr, baseAtk);            // untouched by reloads → restore
                        Weapons.ScaleWeaponBlade(kkCode, 1f);
                        boosted = false;
                        DungeonMessages.DisplayMessage("The spring's blessing fades\nfrom the Kitchen Knife...", 2, 30, 4000);
                    }
                    else
                    {
                        // Same mechanism Heaven's Cloud uses; the blade frame is derived from the weapon's own
                        // model code (c01w08 -> "w08"), so the visible blade and its dcol hit point grow together.
                        if (!Weapons.ScaleWeaponBlade(kkCode, factor) && !warnedNoBlade)
                        {
                            warnedNoBlade = true;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                $"[KitchenKnife] blade mesh for code '{kkCode}' not located — dumping the model's frame tree:");
                            Weapons.DumpWeaponFrameTree();
                        }
                        ushort cur = Memory.ReadUShort(atkAddr);
                        if (cur == baseAtk)                                  // battle record was refreshed → re-apply
                            Memory.WriteUShort(atkAddr, (ushort)Math.Min(baseAtk * KkAttackMult, ushort.MaxValue));
                    }
                }
            }

            // Weapon swapped / left the dungeon mid-blessing: quietly put everything back.
            wasInSpring = false;
            if (boosted)
            {
                long atkAddr = WeaponHave.BattleWeaponRecord + WeaponHave.EffAttackOffset;
                if (Memory.ReadUShort(atkAddr) == (ushort)Math.Min(baseAtk * KkAttackMult, ushort.MaxValue))
                    Memory.WriteUShort(atkAddr, baseAtk);
                Weapons.ScaleWeaponBlade(kkCode, 1f);
            }
        }

        /// <summary>The knife's weapon HP to its maximum on the inventory record of the bag slot Toan has it in.
        /// <paramref name="withBlessing"/>: the blessing's own message is about to show, so none of ours.</summary>
        private static void MendWhp(bool withBlessing)
        {
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.ToanId));
            if (bag < 0 || bag >= DngStatusData.MaxWeaponSlots) return;
            long rec = DngStatusData.WeaponRecord(Player.ToanId, bag);
            if (Memory.ReadUShort(rec) != Items.kitchenknife) return;
            float whp = Memory.ReadFloat(rec + WeaponHave.InventoryWeaponWhpOffset);
            short max = Memory.ReadShort(rec + WeaponHave.InventoryWeaponMaxWhpOffset);
            if (max <= 0 || whp >= max) return;
            Memory.WriteFloat(rec + WeaponHave.InventoryWeaponWhpOffset, max);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[KitchenKnife] the spring mends the knife: WHP {whp:F0} → {max}");
            if (!withBlessing) DungeonMessages.DisplayMessage("The spring mends\nthe Kitchen Knife.", 2, 30, 3000);
        }
    }
}
