using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Pocklekul — a guard held for a second summons a random day-of-the-week pockle: a real monster on a private monster manager
    /// the game steps beside the floor's, its own script running natively, going for the locked-on enemy or the one nearest Ruby, walking
    /// beside her when there is none; guarding again summons nothing while one stands. All of it is native (tools/stubs/ally_brain.s and its
    /// fellows, ElfAllyPatches): this thread arms it while the ring is in Ruby's hand (CodeCaves.AllyArmed — disarming ends a standing
    /// pockle), logs what it reports (CodeCaves.AllyEvent), and while one stands ticks its friendly fire (<see cref="ConfusionAlly"/>:
    /// its swings on the enemies, theirs and their shots on it).</summary>
    internal static class Pocklekul
    {
        private const string Tag = "[Pocklekul] ";
        private const uint BrainVanillaWord0 = 0x27BDFFA0;   // sceDmaSendN's first word: a disc patched before the native Pocklekul
        private static readonly string[] Days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

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
                if (ev != seen) { seen = ev; Log(Describe((int)(ev & 0xFF))); }
                bool up = ConfusionAlly.Up;
                if (up) Confusion.Tick();
                else if (wasUp) Confusion.EndAlly();   // it went: the enemies it provoked back on Ruby
                wasUp = up;
                Thread.Sleep(up ? 16 : 100);
            }
            Memory.WriteInt(CodeCaves.AllyArmed, 0);   // the brain ends a standing pockle on its next frame
            Confusion.EndAlly();
        }

        private static bool InHand()
            => Player.InDungeonFloor() && Player.CurrentCharacterNum() == Player.RubyId && Player.Weapon.GetCurrentWeaponId() == Items.pocklekul;

        private static string Describe(int code) => code switch
        {
            >= 0x10 and < 0x17 => $"{Days[code - 0x10]}'s pockle summoned",
            2 => "the pockle fell",
            3 => "the pockle went (the Pocklekul or Ruby left the hand)",
            4 => "the pockle went (its floor changed, its block was overwritten, or an event took the floor)",
            5 => "no summon: no pockle block (Ruby's character pool lacks it — a disc patched before the Pocklekul?)",
            6 => "no summon: no VRAM room in the monsters' texture group",
            7 => "no summon: the pockle's model did not load",
            8 => "no summon: the pockle did not stand",
            9 => "a pockle already stands — no summon",
            _ => $"event {code}",
        };

        private static void Log(string msg) => System.Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
