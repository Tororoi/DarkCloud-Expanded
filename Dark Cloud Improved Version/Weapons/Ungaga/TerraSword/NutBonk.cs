using System;
using static Dark_Cloud_Improved_Version.TerraSword;
using static Dark_Cloud_Improved_Version.RockFall;
using static Dark_Cloud_Improved_Version.RockImpact;
using static Dark_Cloud_Improved_Version.TargetDimming;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve's nut landing on the target's head: the frame the fall-drive cave turns that landing into the armed hop,
    /// <see cref="Bonk"/> gives the hop its own gravity, plants the hit (BonkShare× the attack as a melee-style hit with the vanilla
    /// melee kick from the player, crush-marked, no weapon ability), confuses the enemy for as long as the nut stays (the shared
    /// Confusion), and releases the target's darkening and the weapon's green. One of the <see cref="TerraSword"/> classes, which
    /// share their members through using static.</summary>
    internal static class NutBonk
    {
        private const string Tag = "[TerraSword/NutBonk] ";

        /// <summary>The nut landed on the head and the cave turned it into the armed hop that frame: the hop's own gravity, then the
        /// hit — BonkShare× the attack as a melee-style hit (the entry's own reaction and the vanilla melee kick from the player; the
        /// engine bills its weapon HP as for any hit) — the stars on the enemy, the enemy confused, the darkening released, the green
        /// fading.</summary>
        internal static void Bonk()
        {
            int slot = _target;
            Memory.WriteFloat(CodeCaves.BladeFall + CodeCaves.BladeFallG, BonkGravity / 3600f);
            Memory.WriteInt  (CodeCaves.FallDrive + CodeCaves.FallDriveHopped, 0);
            _falling = false; _bouncing = true; _headArmed = false;
            _ground = _hopGround;
            FallRowsOff();
            BladeProp.SetScale(FullScale);
            DimEnd();
            _tintFadeFrom = GameClock.Now;
            if (slot < 0 || !Enemies.IsLive(slot)) return;
            var sh = new Shell { What = "bonk", Slot = slot, Hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)) };
            sh.Replant = PlantBonk;
            PlantBonk(sh);
            Confusion.Confuse(slot, GameClock.Now.AddSeconds(ConfusionHold));
            _bonked = slot;
        }

        /// <summary>The bonk's hit on <paramref name="sh"/>'s enemy: BonkShare× the attack, the vanilla melee kick from the player.</summary>
        private static bool PlantBonk(Shell sh)
        {
            int slot = sh.Slot;
            int attack = Math.Max(1, (int)Math.Round(Player.Weapon.GetCurrentWeaponAttack() * BonkShare));
            // A plain melee hit's kick, from the player; crush-marked: through any guard (a Xiao hit is still billed: the no-drain caves
            // are Ungaga's); no poison, stop, critical, steal or drain from a drop.
            var kick = new EnemyHit.Kick(Memory.ReadFloat(Addresses.dunPositionX), Memory.ReadFloat(Addresses.dunPositionZ), Memory.ReadFloat(Addresses.dunPositionY),
                                         MeleeKickWords.VanillaStrength12, MeleeKickWords.VanillaDecay12);
            int idx = EnemyHit.TryPlant(slot, attack, HitRadius, kick, out var at, mark: CodeCaves.CrushMark, weaponAbilities: false);
            if (idx < 0) return false;
            sh.Idx = idx; sh.Ticks = ShellLifeTicks; sh.Tries++;
            _shells.Add(sh);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"bonk{(sh.Tries > 1 ? $" (again, {sh.Tries})" : "")}: enemy slot {slot} for {attack} (entry {idx}) — planted at ({at.X:F0},{at.H:F0},{at.Y:F0}) r {at.Radius:F1}");
            return true;
        }
    }
}
