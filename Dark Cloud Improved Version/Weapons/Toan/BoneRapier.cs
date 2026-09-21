using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Bone Rapier — the skeleton key: bone doors open without their key and the reviving undead stay down (shared with Xiao's Bone Slingshot and Super Steve's bone spheres).</summary>
    internal static class BoneRapier
    {
        // ── Bone Rapier ────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Toggles the effect of the bone rapier
        /// </summary>
        /// <param name="isActive">True if to active the effect</param>
        public static void BoneRapierEffect(bool isActive)
        {
            if (isActive)
            {
                //Set BypassBoneDoor
                if (!Dungeon.IsBypassBoneDoor()) Dungeon.SetBypassBoneDoor(true);
            }
            //Otherwise reset BypassBoneDoor
            else if (Dungeon.IsBypassBoneDoor()) Dungeon.SetBypassBoneDoor(false);
        }

        /// <summary>
        /// Trigger to open a bone door
        /// </summary>
        public static void BoneDoorTrigger()
        {
            while (!Dungeon.doorIsOpen &&
                    Player.InDungeonFloor() &&
                    (Player.Weapon.GetCurrentWeaponId() == Items.bonerapier || Player.Weapon.GetCurrentWeaponId() == Items.boneslingshot))
            {
                //Bone door opened through Bone Rapier (or Xiao's Bone Slingshot, which shares the key)
                if (Memory.ReadByte(Addresses.dungDoorType) == 250 &&
                    Dungeon.IsBypassBoneDoor() &&
                    Memory.ReadInt(0x21D56800) == 15903712) //Aux address to help determine if the bone door specifically was opened)
                {
                    int ms = 0;

                    while (Memory.ReadInt(Addresses.hideHud) == 1 && ms < 2000)
                    {
                        Thread.Sleep(100);
                        ms += 100;
                        continue;
                    }

                    //Display our custom message
                    DungeonMessages.DisplayMessage("You can hear an ominous voice\nlaughing 'Rattle me bones!'", 2, 29, 4000);
                    Dungeon.doorIsOpen = true;
                }
                //Bone door opened normally without Bone Rapier
                else if (Memory.ReadByte(Addresses.dungDoorType) == 250 &&
                        !Dungeon.IsBypassBoneDoor() &&
                        Memory.ReadInt(0x21D56800) == 15903712 //Aux address to help determine if the bone door specifically was opened
                        )
                {
                    Dungeon.doorIsOpen = true;
                }

                Thread.Sleep(500);
            }
        }

        /// <summary>Whether the equipped weapon carries the bone key: the Bone Rapier, Xiao's Bone Slingshot, or Super Steve
        /// with either one's SynthSphere.</summary>
        internal static bool BoneKeyWielded()
        {
            int id = Player.Weapon.GetCurrentWeaponId();
            if (id == Items.bonerapier || id == Items.boneslingshot) return true;
            if (id != Items.supersteve) return false;
            int sphere = SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord);
            return sphere == Items.bonerapier || sphere == Items.boneslingshot;
        }

        /// <summary>The bone key's second half, the Cross Hinder's NO REVIVAL: while the Bone Rapier or Bone Slingshot is out
        /// (or Super Steve with either sphere), the loaded death scripts of the reviving undead are patched once per floor so
        /// the revive roll never wins (<see cref="PatchUndeadRevivers"/>), and put back when the key goes. Runs while one of
        /// the three weapons is equipped; a sphere change on Super Steve patches or restores mid-floor.</summary>
        public static void BoneKeyNoRevivalEffect()
        {
            var patched = new List<(long CellValueAddr, int OrigThr)>();
            byte floor = 0xFF; bool applied = false;
            while (Player.InDungeonFloor())
            {
                int id = Player.Weapon.GetCurrentWeaponId();
                if (id != Items.bonerapier && id != Items.boneslingshot && id != Items.supersteve) break;
                Thread.Sleep(250);
                byte f = Memory.ReadByte(Addresses.checkFloor);
                if (f != floor) { floor = f; patched.Clear(); applied = false; }   // the old floor's STBs are gone
                bool wanted = BoneKeyWielded();
                if (wanted && !applied) { CrossHinder.PatchUndeadRevivers(patched); applied = true; }
                else if (!wanted && applied) { CrossHinder.RestoreUndeadRevivers(patched); applied = false; }
            }
            if (applied && Memory.ReadByte(Addresses.checkFloor) == floor) CrossHinder.RestoreUndeadRevivers(patched);
        }
    }
}
