using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Dusack — "No Fool's Gold": a mimic can be hit while it wakes. A chest-mimic's wake is a GUARD: the monster-script
    /// bake (MonsterScriptBakes) turned its 100 invincibility frames into a guard window over the "appear" clip, frames 10–28
    /// (a king's 10–27), registered the moment the box opens, and CheckDmg drops every hit that lands inside it. Dark Cloud,
    /// 7th Heaven and the Divine Beast cat get through it by zeroing every guard window of every enemy; this zeroes that one
    /// window alone, on mimics and king mimics alone — their guard MOTION (block frames 170–189) still blocks — for as long as
    /// the Dusack is in Toan's hand on a dungeon floor, and puts it back when it goes. The Brave Ark carries it too
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

        private static readonly ushort[,] _snap     = new ushort[EnemyAddresses.FloorSlots.Count, EnemyAddresses.GuardWindows.WindowCount];
        private static readonly ushort[]  _species  = new ushort[EnemyAddresses.FloorSlots.Count];
        private static readonly bool[]    _captured = new bool[EnemyAddresses.FloorSlots.Count];
        private static byte _lastFloor = 0xFF;

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
        /// her pellets; off, put back. Toan's thread and this never run at once (his sword is sheathed while she is out).</summary>
        internal static void DriveSphere(bool active) => Drive(active);

        /// <summary>On: every live mimic's WAKE guard window held at zero (flags captured on first sight; a chest-mimic registers
        /// its windows only as it wakes, so a window found armed later is captured then). Off: each captured flag put back.</summary>
        private static void Drive(bool crush)
        {
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != _lastFloor) { _lastFloor = floor; Array.Clear(_captured, 0, _captured.Length); }

            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.RenderStatus)) < 1) { _captured[slot] = false; continue; }
                ushort species = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.EnemySpeciesId));
                if (!MimicSpecies.Contains(species)) { _captured[slot] = false; continue; }
                if (!_captured[slot] || _species[slot] != species)
                {
                    for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++) _snap[slot, w] = Memory.ReadUShort(EnemyAddresses.GuardWindows.FlagAddr(slot, w));
                    _species[slot] = species;
                    _captured[slot] = true;
                }
                for (int w = 0; w < EnemyAddresses.GuardWindows.WindowCount; w++)
                {
                    if (!IsWakeWindow(slot, w)) continue;                              // the guard motion's window: the mimic's to keep
                    long addr = EnemyAddresses.GuardWindows.FlagAddr(slot, w);
                    ushort cur = Memory.ReadUShort(addr);
                    if (crush && cur != 0 && _snap[slot, w] == 0) _snap[slot, w] = cur;   // armed since the capture (the wake): remembered for the restore
                    ushort want = crush ? (ushort)0 : _snap[slot, w];
                    if (cur != want) Memory.WriteUShort(addr, want);
                }
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
