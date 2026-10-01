using System;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Partisan: Ungaga's three combo swings play a third faster — Shamshir's factor (<see cref="Shamshir.SpeedFactor"/>,
    /// 4/3); the charge keeps its pace. UngagaKey_Play's swings are attack states 0x25 / 0x26 / 0x27, which play motions 37 / 38 /
    /// 39 (攻撃１ 670–678 at a step of 0.28, 攻撃２ 678–694 at 0.32, 攻撃３ 695–714 at 0.32) and hand over when the motion cursor
    /// is within a window at the playing entry's own end: ONE frame for swings 1 and 3, HALF a frame for swing 2
    /// (end − 0.5 ≤ cursor ≤ end). A step under the window's width always lands in it, so swing 2's step must stay under 0.5:
    /// 0.32 × 4/3 ≈ 0.43 does (twice its speed would skip the hand-over to swing 3).
    /// Super Steve carrying a Partisan SynthSphere shoots faster as a Shamshir sphere makes it (Shamshir.DriveSphere: Xiao's draw
    /// ×1.6, her shoot at 0.95, from Super Steve's dispatch).
    ///
    /// Shamshir's method and gating (its summary has the why): the entry is found through the character's own Mot_List
    /// pointer, checked against its known frames before it is touched, written only in walking mode once the same list has
    /// been seen on two consecutive ticks, and put back when the Partisan goes or another character is out.</summary>
    internal static class Partisan
    {
        private const string Tag = "[Partisan] ";
        /// <summary>A swing whose play rate the Partisan changes: its list index, the frames that identify it, its stock step.</summary>
        private readonly struct Swing
        {
            public readonly int Id, Start, End; public readonly float Stock;
            public float Fast => Stock * Shamshir.SpeedFactor;
            public Swing(int id, int start, int end, float stock) { Id = id; Start = start; End = end; Stock = stock; }
        }
        private static readonly Swing[] Swings = { new(37, 670, 678, 0.28f), new(38, 678, 694, 0.32f), new(39, 695, 714, 0.32f) };
        private const int    TickMs = 100;
        private const byte   WalkingMode = 1;      // Addresses.dungeonMode: on the floor, nothing loading
        private static long  _list, _seenList;     // the list the step was raised in (MMU; 0 = none), and the one seen last tick

        internal static bool Wielded() => Player.CurrentCharacterNum() == Player.UngagaId && Player.Weapon.GetCurrentWeaponId() == Items.partisan;

        public static void QuickSwingEffect()
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"combo swings at {Shamshir.SpeedFactor:F2}× (KEY 37–39 steps 0.28/0.32/0.32 → {0.28f * Shamshir.SpeedFactor:F2}/{0.32f * Shamshir.SpeedFactor:F2}/{0.32f * Shamshir.SpeedFactor:F2})");
            bool logged = false;
            while (Player.Weapon.GetCurrentWeaponId() == Items.partisan && Player.InDungeonFloor())
            {
                if (Player.CurrentCharacterNum() == Player.UngagaId)
                {
                    if (Drive() && !logged) { logged = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"combo steps raised in the motion list at 0x{_list:X}"); }
                }
                else Restore();                                                   // an ally out: Ungaga's swing at its own pace
                Thread.Sleep(TickMs);
            }
            Restore();
        }

        private static bool Drive()
        {
            if (Memory.ReadByte(Addresses.dungeonMode) != WalkingMode) return false;   // a load or a menu: the pointer is not to be trusted
            long list = MotionList();
            if (list != _seenList) { _seenList = list; return false; }               // first sight: settle a tick before writing
            if (list == 0) return false;
            if (_list != 0 && list != _list) SetSteps(_list, fast: false);            // the list moved: the old one back to stock
            _list = list;
            return SetSteps(list, fast: true);
        }

        private static void Restore()
        {
            if (_list == 0) return;
            SetSteps(_list, fast: false);
            _list = 0;
        }

        /// <summary>The active character's Mot_List when all three swings sit at their known frames (Ungaga's dungeon set), else 0.</summary>
        private static long MotionList()
        {
            long list = (uint)Memory.ReadInt(CharacterMotion.Base + CCharacter.MotionList);
            if (!Memory.IsValidGuest(list)) return 0;
            list = Memory.ToMmu((int)list);
            foreach (Swing w in Swings) if (!Matches(list, w)) return 0;
            return list;
        }

        private static bool Matches(long list, Swing w)
        {
            long e = list + w.Id * CCharacter.MotionEntryStride;
            return Memory.ReadInt(e + CCharacter.MotionEntryStart) == w.Start && Memory.ReadInt(e + CCharacter.MotionEntryEnd) == w.End;
        }

        /// <summary>Every swing's step at its fast or stock figure, each re-checked by its frames and left alone when it holds neither;
        /// true when any was written.</summary>
        private static bool SetSteps(long list, bool fast)
        {
            bool wrote = false;
            foreach (Swing w in Swings)
            {
                if (!Matches(list, w)) continue;
                long addr = list + w.Id * CCharacter.MotionEntryStride + CCharacter.MotionEntryStep;
                float cur = Memory.ReadFloat(addr), want = fast ? w.Fast : w.Stock;
                if (Math.Abs(cur - w.Stock) > 0.01f && Math.Abs(cur - w.Fast) > 0.01f) continue;   // not a step of ours to change
                if (Math.Abs(cur - want) < 1e-4f) continue;
                Memory.WriteFloat(addr, want);
                wrote = true;
            }
            return wrote;
        }
    }
}
