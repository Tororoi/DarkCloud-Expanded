using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Pocklekul — a guard held for a second summons a random pockle (a day of the week's, or Hell Pockle), growing in as it
    /// appears: a real monster on a private monster manager the game steps beside the floor's, its own script running natively, going for
    /// the locked-on enemy or the one nearest the summoner, walking beside them when there is none; guarding again summons nothing while
    /// one stands. All of it is native (tools/stubs/ally_brain.s and its fellows, ElfAllyPatches): this thread arms it while it is in hand
    /// (CodeCaves.AllyArmed = the summoner + 1 — disarming ends a standing pockle), flashes the summoner when the guard's charge is
    /// complete, logs what the brain reports (CodeCaves.AllyEvent) and names the pockle that came on screen ("Sunday summoned") as it
    /// appears in Cactus' smoke (<see cref="SummonSmoke"/>), tinted through its palette until the smoke is gone (<see cref="Tint"/>), and
    /// while one stands ticks its friendly fire (<see cref="ConfusionAlly"/>: its swings and shots on the enemies, theirs on it).</summary>
    internal static class Pocklekul
    {
        private const string Tag = "[Pocklekul] ";
        private const uint BrainVanillaWord0 = 0x27BDFFA0;   // sceDmaSendN's first word: a disc patched before the native Pocklekul
        private const int MessageMs = 2500;
        private const int SlowPassMs = 50;              // an attack's hit window is often 3–5 frames
        private static int _passes, _passSlow;
        private static double _passMs, _passMax;
        private const int ChargeFrames = 60;            // ally_brain.s's guard charge
        private const long VBlankCount = 0x202A2400;    // the vblank counter: it counts on while a disc read holds the floor's loop
        private const float SmokeScale = 0.5f;   // the summon smoke's size for a pockle (Cactus' too)
        private static readonly float[] SummonTint = { 150f, 100f, 120f };   // the pockle's ambient add as it appears
        private const float GrownFrame = 12f;     // the smoke's frame it is fully grown at (ally_grow.s: 18.3 game frames at the smoke's 0.6)
        private static long _unit;                // its unit while the tint is held (MMU; 0 = none)
        private const long Unit = 0x1E3D0;        // its unit 0 (MONSTOR) in the manager: its palette state —
        private const int  PalTarget = 0x130, PalNow = 0x140, PalCount = 0x164, PalSpeed = 0x168, PalWeight = 0x16C;   // the colour aimed at, the one drawn, cycles, weight step, weight
        private static readonly EnemyDefaults[] Pockles =   // ally_brain.s's pockle order (event 0x10 + index)
        {
            EnemySpecies.Sunday, EnemySpecies.Monday, EnemySpecies.Tuesday, EnemySpecies.Wednesday, EnemySpecies.Thursday,
            EnemySpecies.Friday, EnemySpecies.Saturday, EnemySpecies.HellPockle,
        };

        /// <summary>The Pocklekul's thread: runs while it is in the summoner's hand on a dungeon floor (a character change ends it; the
        /// weapon threads start it again for the next).</summary>
        public static void PockleSummonEffect()
        {
            int ch = Player.CurrentCharacterNum();
            bool InHand() => Player.InDungeonFloor() && Player.CurrentCharacterNum() == ch && Wielded();
            if (Memory.ReadUInt(Memory.ToMmu(DeadDmaCave.AllyBrain)) == BrainVanillaWord0)
            {
                Log("this disc has no native Pocklekul (patch it again) — nothing summoned");
                while (InHand()) Thread.Sleep(250);
                return;
            }
            uint seen = Memory.ReadUInt(CodeCaves.AllyEvent);
            bool armed = false, wasUp = false, latched = Memory.ReadInt(CodeCaves.AllyLatch) != 0;
            int fullAt = -1, latchAt = -1;   // DIAGNOSTIC: the vblanks the guard's charge filled and the summon began at (−1: not yet)
            while (InHand())
            {
                // armed only once the target redirect is (else the pockle's "player" would be the summoner)
                if (!armed && TargetRedirectCaves.Armed) { Memory.WriteInt(CodeCaves.AllyArmed, ch + 1); armed = true; }
                // the guard's charge complete (the brain latches it at 60 frames): the stock charge flash, unless one already stands
                bool latch = Memory.ReadInt(CodeCaves.AllyLatch) != 0;
                if (DebugDiagnostics.Enabled)
                {   // DIAGNOSTIC: the vblanks the charge filled and the summon began at, for the delay between them (logged at the summon)
                    int guard = Memory.ReadInt(CodeCaves.AllyGuard), vblank = Memory.ReadInt(VBlankCount);
                    if (guard >= ChargeFrames) { if (fullAt < 0) fullAt = vblank; } else if (!latch) fullAt = latchAt = -1;
                    if (latch && !latched) latchAt = vblank;
                }
                if (latch && !latched && armed && Memory.ReadUInt(CodeCaves.AllyManager) == 0) Player.FlashChargeComplete();
                latched = latch;
                uint ev = Memory.ReadUInt(CodeCaves.AllyEvent);
                if (ev != seen)
                {
                    seen = ev;
                    int code = (int)(ev & 0xFF);
                    Log(Describe(code) + (code >= 0x10 && code < 0x18 ? $" (shot-pack slots it filled 0x{Memory.ReadInt(CodeCaves.AllyShots):X})" : ""));
                    if (code >= 0x10 && code < 0x10 + Pockles.Length)
                    {
                        LogRooms(Pockles[code - 0x10].Name);
                        if (fullAt >= 0 && latchAt >= 0)
                            Log($"the summon began {latchAt - fullAt} vblanks after the charge filled (waiting for a quiet frame) and took {Memory.ReadInt(VBlankCount) - latchAt} (its disc reads)");
                        ConfusionAlly.Summoned();                   // no drops but stolen gold, no kill ABS
                        Poof();
                        string text = $"{Pockles[code - 0x10].Name} summoned";
                        DungeonMessages.DisplayMessage(text, 1, text.Length + 2, MessageMs);   // which pockle came
                    }
                }
                bool up = ConfusionAlly.Up;
                if (up && !DebugDiagnostics.Enabled) Confusion.Tick();
                else if (up)
                {   // DIAGNOSTIC: how long a friendly-fire pass takes — a pass slower than an attack's hit window lets it pass unseen
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    Confusion.Tick();
                    double ms = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                    _passes++; _passMs += ms; _passMax = Math.Max(_passMax, ms); if (ms > SlowPassMs) _passSlow++;
                }
                else if (wasUp)
                {
                    Confusion.EndAlly();   // it went: the enemies it provoked back on the player
                    if (_passes > 0) Log($"friendly-fire passes while it stood: {_passes}, mean {_passMs / _passes:F1} ms, max {_passMax:F1} ms, {_passSlow} over {SlowPassMs} ms");
                    _passes = _passSlow = 0; _passMs = _passMax = 0;
                }
                if (Memory.ReadUInt(CodeCaves.AllyPin) != 0 && (!up || SummonSmoke.Frame < 0)) Memory.WriteUInt(CodeCaves.AllyPin, 0);   // the smoke left where it was
                wasUp = up;
                SummonSmoke.Drive();
                Tint();
                Thread.Sleep(16);
            }
            Memory.WriteInt(CodeCaves.AllyArmed, 0);   // the brain ends a standing pockle on its next frame
            Confusion.EndAlly();
            Memory.WriteUInt(CodeCaves.AllyPin, 0);
            SummonSmoke.End();
        }

        /// <summary>The effect this ring wants in the summoner's SECOND main-character instance (Ruby's idle but for the Bandit's Ring's
        /// stolen shots, Xiao's idle): the summon smoke, whenever it is in hand.</summary>
        internal static BorrowedEffect WantedShot() => Wielded() ? SummonSmoke.Effect() : null;

        /// <summary>Ruby with the Pocklekul, or Xiao with Super Steve carrying its sphere — the two summoners with an ally block (AllyBlock:
        /// Ruby's LoadChara2 branch grows hers, Xiao's sits in her region, XiaoRegion, whenever the Divine Beast cat is not loaded in it).</summary>
        internal static bool Wielded()
            => (Player.CurrentCharacterNum() == Player.RubyId && Player.Weapon.GetCurrentWeaponId() == Items.pocklekul)
            || SuperSteve.Wields(Items.pocklekul);

        /// <summary>The pockle appears in Cactus' poof: at its spawn point (its block's +0x20 — the manager's −0xE0), turned to the summoner's facing,
        /// and pinned to it natively (CodeCaves.AllyPin: ally_hooks.s copies its position to the smoke's every frame) so it stays in the smoke
        /// as it walks off.</summary>
        private static void Poof()
        {
            uint m = Memory.ReadUInt(CodeCaves.AllyManager);
            if (m == 0 || !Memory.IsValidGuest(m)) return;
            long mgr = Memory.ToMmu(m), spawn = mgr - 0x100 + 0x20;
            if (SummonSmoke.Start(Memory.ReadFloat(spawn), Memory.ReadFloat(spawn + 4), Memory.ReadFloat(spawn + 8), Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY),
                                  SmokeScale, "pockle", held: false))
                Memory.WriteUInt(CodeCaves.AllyPin, (uint)(SummonSmoke.PosAddr - Memory.Pcsx2Base));
            // Its colour is its unit's PALETTE state, the one _STATUS_SET_PALLET drives (PalletStep mixes the scene's ambient toward the
            // target by a weight that moves by the speed each frame while the count is above 0; PalletSet draws it): the target = the
            // ambient (+0x140, which PalletStep keeps) plus the tint, the weight whole, the speed 0 — held by the game every frame.
            _unit = mgr + Unit;
            float ar = Memory.ReadFloat(_unit + PalNow), ag = Memory.ReadFloat(_unit + PalNow + 4), ab = Memory.ReadFloat(_unit + PalNow + 8);
            Memory.WriteVec3(_unit + PalTarget, ar + SummonTint[0], ag + SummonTint[1], ab + SummonTint[2]);
            Memory.WriteFloat(_unit + PalSpeed, 0f);
            Memory.WriteFloat(_unit + PalWeight, 1f);
            Memory.WriteInt(_unit + PalCount, 1);
            Tint();
        }

        /// <summary>The pockle's summon tint (<see cref="SummonTint"/>), held whole until it is fully grown (the smoke's frame
        /// <see cref="GrownFrame"/> — the Cactus copy's risen frame, the same growth); then one write sets the speed that takes the weight
        /// to 0 by the smoke's last frame, and the game fades it, back to the room's light. No smoke on the floor → let go at once.</summary>
        private static void Tint()
        {
            if (_unit == 0) return;
            if (Memory.ReadUInt(CodeCaves.AllyManager) == 0) { _unit = 0; return; }
            float f = SummonSmoke.Frame;
            if (f >= 0 && f < GrownFrame) return;
            Memory.WriteFloat(_unit + PalSpeed, f < 0 ? -1f : -SummonSmoke.ClipRate / (SummonSmoke.ClipEnd - GrownFrame));   // a frame's share of the fade
            _unit = 0;
        }

        /// <summary>How full the summon left the block's rooms (AllyBlock): the model room, the side room (its textures and script copy)
        /// and the script arena — the measure their sizes are kept to.</summary>
        private static void LogRooms(string who)
        {
            uint m = Memory.ReadUInt(CodeCaves.AllyManager);
            if (m == 0 || !Memory.IsValidGuest(m)) return;
            long block = Memory.ToMmu(m) - 0x100;
            string Room(int a) => $"{Memory.ReadInt(block + a + AllyBlock.AllocUsed):N0} of {Memory.ReadInt(block + a + AllyBlock.AllocCap):N0}";
            Log($"{who}'s rooms (units used): model {Room(AllyBlock.ModelAlloc)}, side {Room(AllyBlock.SideAlloc)}, script {Room(AllyBlock.ScriptAlloc)}");
        }

        private static string Describe(int code) => code switch
        {
            >= 0x10 and < 0x18 => $"{Pockles[code - 0x10].Name} summoned",
            2 => "the pockle fell",
            3 => "the pockle went (the Pocklekul or its summoner left the hand)",
            4 => "the pockle went (its floor changed, its block was overwritten, or an event took the floor)",
            5 => "no summon: no ally block (the summoner's character pool lacks it — Xiao's region too small or holding the cat, or a disc patched before the Pocklekul)",
            6 => "no summon: no VRAM room for its picture in the monsters' texture group",
            7 => "no summon: the pockle's model did not load",
            8 => "no summon: the pockle did not stand",
            9 => "a pockle already stands — no summon",
            11 => "the pockle was left behind (its summoner more than 300 u from it)",
            _ => $"event {code}",
        };

        private static void Log(string msg) => System.Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
