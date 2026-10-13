using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Pocklekul — a guard held for a second summons a random pockle (a day of the week's, or Hell Pockle), growing in as it
    /// appears: a real monster on a private monster manager the game steps beside the floor's, its own script running natively, going for
    /// the locked-on enemy or the one nearest Ruby, walking beside her when there is none; guarding again summons nothing while one
    /// stands. All of it is native (tools/stubs/ally_brain.s and its fellows, ElfAllyPatches): this thread arms it while the ring is in
    /// Ruby's hand (CodeCaves.AllyArmed — disarming ends a standing pockle), logs what it reports (CodeCaves.AllyEvent) and names the
    /// pockle that came on screen ("Sunday summoned") as it appears in Cactus' smoke (<see cref="SummonSmoke"/>), and while one stands
    /// ticks its friendly fire (<see cref="ConfusionAlly"/>: its swings and shots on the enemies, theirs on it).</summary>
    internal static class Pocklekul
    {
        private const string Tag = "[Pocklekul] ";
        private const uint BrainVanillaWord0 = 0x27BDFFA0;   // sceDmaSendN's first word: a disc patched before the native Pocklekul
        private const int MessageMs = 2500;
        private const float SmokeScale = 0.5f;   // the summon smoke's size for a pockle (Cactus' too)
        private static readonly EnemyDefaults[] Pockles =   // ally_brain.s's pockle order (event 0x10 + index)
        {
            EnemySpecies.Sunday, EnemySpecies.Monday, EnemySpecies.Tuesday, EnemySpecies.Wednesday, EnemySpecies.Thursday,
            EnemySpecies.Friday, EnemySpecies.Saturday, EnemySpecies.HellPockle,
        };

        /// <summary>The Pocklekul's thread: runs while the ring is in Ruby's hand on a dungeon floor.</summary>
        public static void PockleSummonEffect()
        {
            if (Memory.ReadUInt(Memory.ToMmu(DeadDmaCave.AllyBrain)) == BrainVanillaWord0)
            {
                Log("this disc has no native Pocklekul (patch it again) — nothing summoned");
                while (InHand()) Thread.Sleep(250);
                return;
            }
            uint seen = Memory.ReadUInt(CodeCaves.AllyEvent);
            bool armed = false, wasUp = false;
            while (InHand())
            {
                // armed only once the target redirect is (else the pockle's "player" would be Ruby)
                if (!armed && TargetRedirectCaves.Armed) { Memory.WriteInt(CodeCaves.AllyArmed, 1); armed = true; }
                uint ev = Memory.ReadUInt(CodeCaves.AllyEvent);
                if (ev != seen)
                {
                    seen = ev;
                    int code = (int)(ev & 0xFF);
                    long tm = TextureManager.Base;
                    string took = code >= 0x10 && code < 0x18
                        ? $", its load took {Memory.ReadInt(CodeCaves.AllyTexBuf + 4) - Memory.ReadInt(CodeCaves.AllyTexBuf):N0}; shot-pack slots it filled 0x{Memory.ReadInt(CodeCaves.AllyShots):X}" : "";
                    Log(Describe(code) + $" (texture buffer {Memory.ReadInt(tm + TextureManager.BufferUsed):N0} of {Memory.ReadInt(tm + TextureManager.BufferCap):N0} units{took})");
                    if (code >= 0x10 && code < 0x10 + Pockles.Length)
                    {
                        ConfusionAlly.Summoned();                   // no drops but stolen gold, no kill ABS
                        Poof();
                        string text = $"{Pockles[code - 0x10].Name} summoned";
                        DungeonMessages.DisplayMessage(text, 1, text.Length + 2, MessageMs);   // which pockle came
                    }
                }
                FreezeProbe.Tick();                    // DIAGNOSTIC: a frozen floor's state into the log (the King Mimic freeze)
                bool up = ConfusionAlly.Up;
                if (up) Confusion.Tick();
                else if (wasUp) { Confusion.EndAlly(); SummonSmoke.Unpin(); }   // it went: the enemies it provoked back on Ruby, the smoke left where it was
                wasUp = up;
                SummonSmoke.Drive();
                Thread.Sleep(16);
            }
            Memory.WriteInt(CodeCaves.AllyArmed, 0);   // the brain ends a standing pockle on its next frame
            Confusion.EndAlly();
            SummonSmoke.End();
        }

        /// <summary>The effect this ring wants in Ruby's SECOND main-character instance (idle but for the Bandit's Ring's stolen shots):
        /// the summon smoke, whenever the Pocklekul is in her hand.</summary>
        internal static BorrowedEffect WantedShot()
            => Player.CurrentCharacterNum() == Player.RubyId && Player.Weapon.GetCurrentWeaponId() == Items.pocklekul ? SummonSmoke.Effect() : null;

        /// <summary>The pockle appears in Cactus' poof: at its spawn point (its block's +0x20 — the manager's −0xE0), turned to Ruby's facing,
        /// and pinned to it (its unit 0's position, the manager's +0x1FCE0) so it stays in the smoke as it walks off.</summary>
        private static void Poof()
        {
            uint m = Memory.ReadUInt(CodeCaves.AllyManager);
            if (m == 0 || !Memory.IsValidGuest(m)) return;
            long mgr = Memory.ToMmu(m), spawn = mgr - 0x100 + 0x20;
            SummonSmoke.Start(Memory.ReadFloat(spawn), Memory.ReadFloat(spawn + 4), Memory.ReadFloat(spawn + 8), Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY),
                              SmokeScale, "pockle", pinTo: mgr + 0x1FCE0);
        }

        private static bool InHand()
            => Player.InDungeonFloor() && Player.CurrentCharacterNum() == Player.RubyId && Player.Weapon.GetCurrentWeaponId() == Items.pocklekul;

        private static string Describe(int code) => code switch
        {
            >= 0x10 and < 0x18 => $"{Pockles[code - 0x10].Name} summoned",
            2 => "the pockle fell",
            3 => "the pockle went (the Pocklekul or Ruby left the hand)",
            4 => "the pockle went (its floor changed, its block was overwritten, or an event took the floor)",
            5 => "no summon: no pockle block (Ruby's character pool lacks it — a disc patched before the Pocklekul?)",
            6 => "no summon: no room for its picture (the monsters' VRAM group or the texture buffer)",
            7 => "no summon: the pockle's model did not load",
            8 => "no summon: the pockle did not stand",
            9 => "a pockle already stands — no summon",
            11 => "the pockle was left behind (Ruby more than 300 u from it)",
            _ => $"event {code}",
        };

        private static void Log(string msg) => System.Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
