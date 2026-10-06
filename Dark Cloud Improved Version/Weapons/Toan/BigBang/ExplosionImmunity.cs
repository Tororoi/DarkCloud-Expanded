using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Immunity to the game's explosions while a Big Bang blade is held: the four shot configs that ARE the
    /// explosions (<see cref="ExplosionCfgs"/>) and the bomb-reaction word are given a reaction the player's damage
    /// handler does not act on, so their entries are consumed and nothing reaches him; enemies are unaffected.
    /// <see cref="ArmImmunity"/> puts it on (retrying until the config table is up), <see cref="RestoreImmunity"/>
    /// puts the shared ELF words back, <see cref="DriveImmunity"/> is the switch for a wielder that inherits it (Super
    /// Steve with a Big Bang sphere). Big Bang arms it every tick and restores it on Reset.</summary>
    internal static class ExplosionImmunity
    {
        // ── immunity to explosions ───────────────────────────────────────────────────────
        // The four shot configs that ARE the explosions: Halloween's thrown pumpkin and the three self-destructs
        // (zibaku = 自爆) that Mr. Blare, Bomber Head, Sam and Billy blow themselves up with. Their entries take the
        // reaction straight from the config word, and BtCheckDamageProc subtracts the player's HP inside its
        // reaction branches — 3, and 2-or-4 — so a reaction outside that set is INERT: the entry is consumed and
        // nothing reaches him. No code patch, no pool scanning, four words.
        //
        // ⚠ Global, static ELF data shared by every enemy of those species, so it MUST be put back when the blade
        // goes away — Reset does it. Enemies are unaffected either way: CMonstorUnit::CheckDmg never reads the
        // reaction, so a reflected shot still damages them normally.
        private static readonly int[] ExplosionCfgs = { 3, 16, 17, 18 };   // pump_bom, zibaku_f2, zibaku_r2, zibaku_t2 (the Bomb Gemron's are item-bomb blasts: the bomb-reaction word)
        private const int    ReactionInert   = 5;      // 1 and 5 are both unhandled; 4 is the light flinch and DAMAGES
        private static readonly int[] _cfgReaction = new int[ExplosionCfgs.Length];
        private static bool  _immune;

        /// <summary>Where config <paramref name="index"/> keeps its reaction. ⚠ ShotEffectPack.CfgTable is an array of
        /// POINTERS to the 0x70-byte records, not the records themselves (BorrowedShots.TableConfig reads it that
        /// way): the record lives at *(CfgTable + index × 4); the 34-entry pointer array has the species table behind
        /// it.</summary>
        private static long ReactionAddr(int index)
        {
            uint cfg = Memory.ReadUInt(ShotEffectPack.CfgTable + (long)index * 4);
            return Memory.IsValidGuest(cfg) ? Memory.ToMmu(cfg) + ShotEffectPack.CfgReaction : 0;
        }

        /// <summary>The effect file a config names (its first field) — the check that we are looking at the record
        /// we think we are.</summary>
        private static string ConfigName(int index)
        {
            uint cfg = Memory.ReadUInt(ShotEffectPack.CfgTable + (long)index * 4);
            if (!Memory.IsValidGuest(cfg)) return "?";
            byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(cfg) + ShotEffectPack.CfgName, ShotEffectPack.CfgNameLen);
            int n = Array.IndexOf(b, (byte)0);
            return System.Text.Encoding.ASCII.GetString(b, 0, n < 0 ? b.Length : n);
        }

        /// <summary>The explosions inert (on) or dangerous again (off), for a wielder that inherits it (Super Steve with a Big
        /// Bang sphere).</summary>
        internal static void DriveImmunity(bool on) { if (on) ArmImmunity(); else RestoreImmunity(); }

        /// <summary>Make the explosions inert while the blade is in hand (see ExplosionCfgs).</summary>
        internal static void ArmImmunity()
        {
            if (_immune) return;
            for (int i = 0; i < ExplosionCfgs.Length; i++)
            {
                long a = ReactionAddr(ExplosionCfgs[i]);
                if (a == 0) return;                                  // the table is not up yet — try again next tick
                _cfgReaction[i] = Memory.ReadInt(a);
                Memory.WriteInt(a, ReactionInert);
            }
            Memory.WriteInt(CodeCaves.BombReaction, ReactionInert);   // chest traps, thrown bombs, Halloween's pumpkin
            _immune = true;
            // Reported by NAME and read back: a reaction that did not take is invisible in play (the hit simply
            // happens).
            for (int i = 0; i < ExplosionCfgs.Length; i++)
            {
                long a = ReactionAddr(ExplosionCfgs[i]);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[ExplosionImmunity] cfg {ExplosionCfgs[i]} \"{ConfigName(ExplosionCfgs[i])}\": reaction {_cfgReaction[i]} → {Memory.ReadInt(a)}"
                    + (Memory.ReadInt(a) == ReactionInert ? "" : "  ⚠ DID NOT TAKE"));
            }
        }

        /// <summary>…and dangerous again when it is not. ⚠ Never leave this on: the configs are shared ELF data.</summary>
        /// <summary>The item-bomb reaction word at the vanilla knockdown reaction (the ISO patch, ElfDamagePatches.PatchBombReaction,
        /// made it data): seeded at startup (SessionController.ApplyNewChanges), and put back by <see cref="RestoreImmunity"/>.</summary>
        internal static void SeedBombReaction() =>
            Memory.WriteInt(CodeCaves.BombReaction, CodeCaves.BombReactionVanilla);

        internal static void RestoreImmunity()
        {
            if (!_immune) return;
            for (int i = 0; i < ExplosionCfgs.Length; i++)
            {
                long a = ReactionAddr(ExplosionCfgs[i]);
                if (a != 0) Memory.WriteInt(a, _cfgReaction[i]);
            }
            SeedBombReaction();
            _immune = false;
        }
    }
}
