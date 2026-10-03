using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Dusack — "No Fool's Gold": a mimic can be hit while it wakes. A chest-mimic's wake is a GUARD: the monster-script
    /// bake (MonsterScriptBakes) turned its 100 invincibility frames into a guard window over the "appear" clip, frames 10–28
    /// (a king's 10–27), registered the moment the box opens, and CheckDmg drops every hit that lands inside it. Dark Cloud and
    /// 7th Heaven open every guard window of every enemy, and the Divine Beast cat's hit passes any; this opens that one window
    /// alone (the guard gate's per-enemy mask, GuardGate), on mimics and king mimics alone — their guard MOTION (block frames
    /// 170–189) still blocks — for as long as the Dusack is in Toan's hand on a dungeon floor. The Brave Ark carries it too
    /// (<see cref="Grants"/>), and Super Steve with either sword's SynthSphere has it for her pellets (<see cref="DriveSphere"/>).
    /// Dark Cloud and 7th Heaven do not need it: their Guard Crush breaks every guard.</summary>
    internal static class Dusack
    {
        private const int TickMs = 100;

        /// <summary>Whether a weapon (or a sphere's source weapon) carries No Fool's Gold.</summary>
        internal static bool Grants(int weaponId) => weaponId == Items.dusack || weaponId == Items.braveark;

        private static readonly HashSet<ushort> MimicSpecies = new()
        {
            (ushort)EnemySpecies.MimicDBC.Id, (ushort)EnemySpecies.MimicSMT.Id, (ushort)EnemySpecies.MimicMS.Id, (ushort)EnemySpecies.MimicWOF.Id,
            (ushort)EnemySpecies.MimicSW.Id, (ushort)EnemySpecies.MimicGoT.Id, (ushort)EnemySpecies.MimicDS.Id,
            (ushort)EnemySpecies.KingMimicDBC.Id, (ushort)EnemySpecies.KingMimicSMT.Id, (ushort)EnemySpecies.KingMimicMS.Id, (ushort)EnemySpecies.KingMimicWOF.Id,
            (ushort)EnemySpecies.KingMimicSW.Id, (ushort)EnemySpecies.KingMimicGoT.Id, (ushort)EnemySpecies.KingMimicDS.Id,
        };

        // The baked wake window's frames (MonsterScriptBakes: Mimic (10, 28), King Mimic (10, 27)); a window with any other
        // frames is the mimic's own guard motion and is left alone.
        private const float WakeStart = 10f, WakeEndMimic = 28f, WakeEndKing = 27f;

        public static void NoFoolsGoldEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Dusack] no fool's gold: mimics' wake guard (frames {WakeStart:g}–{WakeEndMimic:g} / {WakeEndKing:g}) held open while the sword is out; their guard motion still blocks");
            while (Grants(Player.Weapon.GetCurrentWeaponId()) && Player.InDungeonFloor())
            {
                Drive(Player.CurrentCharacterNum() == Player.ToanId);   // an ally out: the mimics guard as they do
                Thread.Sleep(TickMs);
            }
            Drive(false);
        }

        /// <summary>Every dispatch tick while Super Steve carries a Dusack or Brave Ark sphere: the mimics' wake window held open for
        /// her pellets; off, ignored no more. Toan's thread and this never run at once (his sword is sheathed while she is out).</summary>
        internal static void DriveSphere(bool active) => Drive(active);

        /// <summary>On: every live mimic's WAKE guard window passed by the guard gate (GuardGate.IgnoreWindows; the window itself
        /// is left as the script set it). Off: no window of any slot ignored.</summary>
        private static void Drive(bool crush)
        {
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                byte bits = 0;
                if (crush && Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) >= 1
                    && MimicSpecies.Contains(Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId))))
                    for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                        if (IsWakeWindow(slot, w)) bits |= (byte)(1 << w);           // the guard motion's window: the mimic's to keep
                GuardGate.IgnoreWindows(slot, bits);
            }
        }

        /// <summary>Whether a slot's guard window spans the baked wake frames (its start and end are what the bake registers).</summary>
        private static bool IsWakeWindow(int slot, int window)
        {
            long b = EnemyAddresses.MainMonstorUnit.Base + (long)slot * EnemyAddresses.GuardWindows.Stride;
            float start = Memory.ReadFloat(b + EnemyAddresses.GuardWindows.StartOffset + window * 4);
            float end   = Memory.ReadFloat(b + EnemyAddresses.GuardWindows.EndOffset + window * 4);
            return start == WakeStart && (end == WakeEndMimic || end == WakeEndKing);
        }
    }
}
