using System;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>DIAGNOSTIC: the frozen game's state, written to the log by the app itself (PCSX2 closes with the mod, so a frozen game
    /// cannot be probed from outside). <see cref="Tick"/> watches the floor's frame counter (HealAbility.TickCounter: every unpaused
    /// floor frame) while the floor walks unpaused; when it stands still for <see cref="FrozenSeconds"/> the state is dumped once (twice
    /// over, <see cref="ResampleMs"/> apart, so what still moves shows): the managers being stepped (NowMonstorUnit, each one's
    /// current_monster), every live unit of the floor's and the ally's (state, HP, species, motion and frame, position, its script's
    /// label and saved PC), the live collision entries and the live monster sub-shots.</summary>
    internal static class FreezeProbe
    {
        private const string Tag = "[FreezeProbe] ";
        private const double FrozenSeconds = 1.5;
        private const int    ResampleMs = 300;
        private const long   FloorManager = 0x21DF87D0, NowMonstorUnit = 0x202A34D0, VblankCount = 0x202A2400;
        private const int    Units = 0x1E3D0, UnitStride = 0x190, Charas = 0x1FCD0, CharaStride = 0x3510;
        private const int    Scripts = 0x54DD0, ScriptStride = 0x48, ScriptFunc = 0x2C, ScriptPc = 0x30, ScriptDone = 0x34, ScriptExt = 0x38;
        private const int    ScriptRunning = 0x50, CurrentMonster = 0x90;
        private static int _tick = -1;
        private static DateTime _since;
        private static bool _dumped;

        /// <summary>Each pass of a dungeon thread's loop.</summary>
        internal static void Tick()
        {
            int t = Memory.ReadInt(HealAbility.TickCounter);
            bool walking = Player.CheckDunIsWalkingMode() && !Player.CheckDunIsPausedOrMenu() && Memory.ReadInt(DungeonScriptEvent.BtEventMode) == 0;
            if (t != _tick || !walking) { _tick = t; _since = DateTime.UtcNow; _dumped = false; return; }
            if (_dumped || (DateTime.UtcNow - _since).TotalSeconds < FrozenSeconds) return;
            _dumped = true;
            Log($"the floor's frame counter has stood at {t} for {(DateTime.UtcNow - _since).TotalSeconds:0.0} s while walking — the game looks frozen");
            Dump("sample 1");
            System.Threading.Thread.Sleep(ResampleMs);
            Dump("sample 2");
        }

        private static void Dump(string which)
        {
            var sb = new StringBuilder();
            uint now = Memory.ReadUInt(NowMonstorUnit), ally = Memory.ReadUInt(CodeCaves.AllyManager);
            sb.Append($"{which}: vblank {Memory.ReadUInt(VblankCount)}, NowMonstorUnit 0x{now:X8} ({(now == (uint)(FloorManager & 0x1FFFFFFF) ? "the floor's" : now == ally && ally != 0 ? "the ALLY'S" : "?")}), ");
            sb.Append($"AllyManager 0x{ally:X8}, hold {Memory.ReadInt(CodeCaves.AllyHold)}, event 0x{Memory.ReadUInt(CodeCaves.AllyEvent):X}, lock-on {Memory.ReadInt(PlayerAction.LockOnTargetSlot)}, ");
            sb.Append($"texture buffer {Memory.ReadInt(TextureManager.Base + TextureManager.BufferUsed):N0} of {Memory.ReadInt(TextureManager.Base + TextureManager.BufferCap):N0} units");
            Log(sb.ToString());
            Manager("floor", FloorManager);
            if (ally != 0 && Memory.IsValidGuest(ally)) Manager("ally", Memory.ToMmu(ally));
            Pool();
            Shots();
        }

        private static void Manager(string name, long m)
        {
            Log($"  {name} manager: current_monster {Memory.ReadInt(m + CurrentMonster)}, paused {Memory.ReadInt(m + 0x98)}");
            for (int s = 0; s < 16; s++)
            {
                long u = m + Units + (long)s * UnitStride, c = m + Charas + (long)s * CharaStride, sc = m + Scripts + (long)s * ScriptStride;
                int state = Memory.ReadInt(u);
                if (state <= 0) continue;
                Log($"    unit {s}: state {state} hp {Memory.ReadInt(u + 0x24)} species {Memory.ReadUShort(u + 0x42)} motion {Memory.ReadInt(c + 0xC68)} " +
                    $"frame {Memory.ReadFloat(c + 0x2F0):0.0} pos ({Memory.ReadFloat(c + 0x10):0},{Memory.ReadFloat(c + 0x14):0},{Memory.ReadFloat(c + 0x18):0}) " +
                    $"script func 0x{Memory.ReadUInt(sc + ScriptFunc):X8} pc 0x{Memory.ReadUInt(sc + ScriptPc):X8} done {Memory.ReadInt(sc + ScriptDone)} ext {Memory.ReadInt(sc + ScriptExt)} " +
                    $"running {Memory.ReadInt(m + ScriptRunning + s * 4)} target 0x{(name == "floor" ? Memory.ReadUInt(CodeCaves.PtrAddr(s)) : Memory.ReadUInt(CodeCaves.PtrAddr(AggroTable.AllySlot + s))):X8}");
            }
        }

        private static void Pool()
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) { Log("  no collision pool"); return; }
            for (int i = 0; i < CollisionPool.Entries; i++)
            {
                if (!CollisionPool.IsActive(pool, i)) continue;
                long e = pool + i * CollisionPool.Stride;
                Log($"  entry {i}: owner {Memory.ReadInt(e + CollisionPool.Owner)} mask {Memory.ReadInt(e + CollisionPool.Mask):X} class {Memory.ReadFloat(e + CollisionPool.EntryClass):0.00} " +
                    $"dmg {Memory.ReadInt(e + 0x34)} react {Memory.ReadInt(e + 0x4C)} elem {Memory.ReadInt(e + CollisionPool.Element):X} gate {Memory.ReadInt(e + CollisionPool.GateA)}/{Memory.ReadInt(e + CollisionPool.GateB)} " +
                    $"hit {Memory.ReadInt(e + 0x5C)} +60 {Memory.ReadInt(e + 0x60)} +64 0x{Memory.ReadUInt(e + 0x64):X} pos ({Memory.ReadFloat(e):0},{Memory.ReadFloat(e + 4):0},{Memory.ReadFloat(e + 8):0}) " +
                    $"r {Memory.ReadFloat(e + CollisionPool.Radius):0.0} kick {Memory.ReadFloat(e + CollisionPool.KickStrengthOff):0.00}/{Memory.ReadFloat(e + CollisionPool.KickDecayOff):0.00}/{Memory.ReadInt(e + CollisionPool.KickTypeOff)} mark 0x{Memory.ReadUInt(e + 0x9C):X}");
            }
        }

        private static void Shots()
        {
            uint packG = Memory.ReadUInt(ShotEffectPack.NowShotEffectPtr);
            if (!Memory.IsValidGuest(packG)) return;
            long pack = Memory.ToMmu(packG);
            for (int p = 0; p < ShotEffectPack.PackSlots; p++)
            {
                long fx = pack + (long)p * ShotEffectPack.SlotStride;
                for (int i = 0; i < ShotEffectPack.SubShots; i++)
                {
                    if (Memory.ReadUShort(fx + ShotEffectPack.OffActive + i * 2) == 0) continue;
                    long o = fx + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride + ShotEffectPack.ObjPos;
                    Log($"  shot {p}.{i}: cfg 0x{Memory.ReadUInt(fx + ShotEffectPack.OffCfg):X8} phase {Memory.ReadUShort(fx + ShotEffectPack.OffPhase + i * 2)} owner {Memory.ReadShort(fx + ShotEffectPack.OffA060 + i * 2)} " +
                        $"pos ({Memory.ReadFloat(o):0},{Memory.ReadFloat(o + 4):0},{Memory.ReadFloat(o + 8):0})");
                }
            }
        }

        private static void Log(string msg) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
