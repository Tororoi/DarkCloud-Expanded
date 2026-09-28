using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Shamshir — "Swift Strikes": Toan's five combo swings play a third faster; the charge attacks (lunge and
    /// whirlwind) keep theirs. The Dusack, 7 Branch Sword, Atlamillia Sword and Chronicle Sword carry it too (<see cref="Grants"/>),
    /// and any of the five as Super Steve's sphere hands her the shot speed-up.
    ///
    /// Each motion's play rate is the KEY step of its Mot_List entry (CCharacter +0x344, 0x10 a motion: start, end, step),
    /// which Step__10CCharacter reads every frame; so the five combo entries' steps are raised in place (0.3 → 0.4 frames a
    /// tick) and put back when the sword goes. Every entry is checked against its known frame range before it is touched,
    /// so a list that is not Toan's dungeon set (an ally out, a reload under way) is never written. ToanKey_Play gates the
    /// hits on windows 2–4 frames wide and the chain inputs on windows exactly 1 frame wide, so a step of 0.6 still lands in
    /// every one of them (as does 0.4); a step above 1.0 could skip a chain window, so the factor stays under that.
    ///
    /// Super Steve carrying its SynthSphere draws and shoots faster the same way (<see cref="DriveSphere"/>): Xiao's c04b
    /// draw entry (11) is raised ×1.6 and the shoot (13) to a step of 0.95; the hold (12, a zero-step loop) is left alone. The
    /// draw hands over inside [end − 2, end], a two-frame window, so a step of 1.12 still lands. ⚠ The shoot must never
    /// reach 1.0: BattleActionPlay_Jinn releases the pellet only while the cursor is strictly between 251 and 252, and the
    /// shoot motion starts at 251, so 0.95 is as fast as it can play and still fire.</summary>
    internal static class Shamshir
    {
        internal const float SpeedFactor = 0.4f / 0.3f;   // the combo step 0.3 → 0.4

        /// <summary>Whether a weapon (or a sphere's source weapon) carries Swift Strikes.</summary>
        internal static bool Grants(int weaponId) =>
            weaponId == Items.shamshir || weaponId == Items.dusack || weaponId == Items.sevenbranchsword ||
            weaponId == Items.atlamilliasword || weaponId == Items.chroniclesword;
        private const int    TickMs      = 100;

        /// <summary>A motion whose play rate the sword changes: its list index, the frames that identify it, its stock step and
        /// the step it plays at while the Shamshir is out.</summary>
        private readonly struct Swing
        {
            public readonly int Id, Start, End; public readonly float Stock, Fast;
            public Swing(int id, int start, int end, float stock, float fast) { Id = id; Start = start; End = end; Stock = stock; Fast = fast; }
        }

        // Toan's c01d combo swings (ToanKey_Play's 0x24–0x28 states; step 0.3 stock).
        private static readonly Swing[] ToanSwings =
        {
            new Swing(36, 820, 830, 0.3f, 0.3f * SpeedFactor), new Swing(37, 830, 838, 0.3f, 0.3f * SpeedFactor),
            new Swing(38, 838, 847, 0.3f, 0.3f * SpeedFactor), new Swing(39, 847, 857, 0.3f, 0.3f * SpeedFactor),
            new Swing(40, 856, 884, 0.3f, 0.3f * SpeedFactor),
        };
        internal const float SphereDrawFactor = 1.6f;
        /// <summary>The largest step at which the shoot motion, starting at 251, still lands in the (251, 252) release window.</summary>
        internal const float SphereShootStep = 0.95f;
        // Xiao's c04b shot: 11 draw (240–251, 0.7), 13 shoot (251–255, 0.7). The hold (12, 250–250) is a parked zero-step loop.
        private static readonly Swing[] XiaoSwings =
        {
            new Swing(11, 240, 251, 0.7f, 0.7f * SphereDrawFactor), new Swing(13, 251, 255, 0.7f, SphereShootStep),
        };

        private static long _list;         // MMU address of the Mot_List Toan's steps were raised in (0 = none)
        private static long _sphereList;   // …and Xiao's, for the sphere

        public static void SwiftStrikesEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Shamshir] swift strikes: combo swings at {SpeedFactor:F2}× (KEY step 0.3 → {0.3f * SpeedFactor:F2}); charge attacks untouched");
            bool logged = false;
            while (Grants(Player.Weapon.GetCurrentWeaponId()) && Player.InDungeonFloor())
            {
                if (Player.CurrentCharacterNum() == Player.ToanId)
                {
                    int written = Drive(ToanSwings, ref _list, "[Shamshir] ");
                    if (written > 0 && !logged) { logged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Shamshir] {written} combo step(s) raised in the motion list at 0x{_list:X}"); }
                }
                else Restore(ToanSwings, ref _list);                              // an ally out: Toan's swings at their own pace
                Thread.Sleep(TickMs);
            }
            Restore(ToanSwings, ref _list);
        }

        private static bool _sphereLogged;
        /// <summary>Every dispatch tick while Super Steve carries the Shamshir sphere: Xiao's draw at ×1.6 its step and her shoot at 0.95; off,
        /// back at stock.</summary>
        internal static void DriveSphere(bool active)
        {
            if (!active) { Restore(XiaoSwings, ref _sphereList); _sphereLogged = false; return; }
            int written = Drive(XiaoSwings, ref _sphereList, "[Shamshir] sphere: ");
            if (written > 0 && !_sphereLogged) { _sphereLogged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[Shamshir] sphere: draw step 0.7 → {0.7f * SphereDrawFactor:F2}, shoot step 0.7 → {SphereShootStep:F2} in the motion list at 0x{_sphereList:X}"); }
        }

        /// <summary>The active character's motion list at the set's fast steps when it is the set's own list; a list that moved
        /// has the old one put back first. Returns the number of steps written this tick.</summary>
        private static int Drive(Swing[] set, ref long held, string tag)
        {
            long list = MotionListOf(set);
            if (list == 0) return 0;
            if (held != 0 && list != held) SetSteps(held, set, fast: false);   // the list moved: the old one back to stock
            held = list;
            return SetSteps(list, set, fast: true);
        }

        private static void Restore(Swing[] set, ref long held)
        {
            if (held == 0) return;
            SetSteps(held, set, fast: false);
            held = 0;
        }

        /// <summary>The active character's Mot_List when every entry of <paramref name="set"/> sits at its known frames, else 0.</summary>
        private static long MotionListOf(Swing[] set)
        {
            long list = (uint)Memory.ReadInt(CharacterMotion.Base + CCharacter.MotionList);
            if (!Memory.IsValidGuest(list)) return 0;
            list = Memory.ToMmu((int)list);
            foreach (Swing w in set)
                if (!Matches(list, w)) return 0;
            return list;
        }

        private static bool Matches(long list, Swing w)
        {
            long e = list + w.Id * CCharacter.MotionEntryStride;
            return Memory.ReadInt(e + CCharacter.MotionEntryStart) == w.Start && Memory.ReadInt(e + CCharacter.MotionEntryEnd) == w.End;
        }

        /// <summary>Every entry of <paramref name="set"/> in <paramref name="list"/> at its fast or its stock step, each re-checked
        /// by its frames first and left alone when it holds neither figure; the number written.</summary>
        private static int SetSteps(long list, Swing[] set, bool fast)
        {
            int written = 0;
            foreach (Swing w in set)
            {
                if (!Matches(list, w)) continue;
                long addr = list + w.Id * CCharacter.MotionEntryStride + CCharacter.MotionEntryStep;
                float cur = Memory.ReadFloat(addr);
                if (Math.Abs(cur - w.Stock) > 0.01f && Math.Abs(cur - w.Fast) > 0.01f) continue;   // not a step of ours to change
                float want = fast ? w.Fast : w.Stock;
                if (Math.Abs(cur - want) > 0.001f) { Memory.WriteFloat(addr, want); written++; }
            }
            return written;
        }
    }
}
