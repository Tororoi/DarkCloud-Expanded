using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Pocklekul's pockle as a real monster, run by the game: a PRIVATE monster manager (a CMonstorUnit, 0x60750 B) in Ruby's
    /// pockle block whose unit 0 is the pockle — loaded and spawned by the engine's own SetupBaseModel / SetupViewMonstor, then stepped,
    /// drawn and shadowed after the floor's manager every frame by tools/stubs/ally_manager.s (CodeCaves.AllyManager), with NowMonstorUnit
    /// pointing at it so its own script (label 1, 100, 110, 120) runs exactly as on any floor. "The player" of its script is its pointer-
    /// table entry (<see cref="AggroTable.SetAlly"/>: entry 16, the redirect helper's for the private manager's unit 0): the locked-on enemy,
    /// else the live enemy nearest Ruby, else a point at Ruby's right.
    ///
    /// The manager starts as a copy of the floor's (its vtables, interpreters and shared collision model), its script table rewritten —
    /// entry 0 its own arena in the block, the rest null, so CleanViewMonstor's arena reset never touches a floor monster's — then reset.
    /// The model goes into the block (its own CDataAlloc2); its texture bank joins the monsters' group 42 as any monster's does, and the
    /// entries it added are taken out again (and the group's VRAM end put back) when it goes. It goes when its script kills it (the unit
    /// back to state −1), when the ring leaves Ruby's hand, or with the floor; the manager word is cleared first, so the game stops riding it.</summary>
    internal static class PockleUnit
    {
        private const string Tag = "[Pocklekul/PockleUnit] ";
        private const uint MainManagerGuest = 0x01DF87D0;        // MainMonstorUnit
        private const int  ManagerSize = 0x60750;
        private const uint Memcpy = 0x001033B0, CleanViewMonstor = 0x001DF9F0, SetupBaseModel = 0x001DFE90, SetupViewMonstor = 0x001E02B0;
        private const int  MonsterTexBlock = 42, ShotTexBlock = 0x26;
        // the manager's fields
        private const int  ScriptTable = 0x00, ModelCount = 0x48, CurrentMonster = 0x90, RequestedEvent = 0x94, Paused = 0x98;
        private const int  Units = 0x1E3D0, UnitStride = 0x190, UnitState = 0x00;   // monster[16]
        private const int  Charas = 0x1FCD0, CharaStride = 0x3510;                  // chara[16][0]
        // the block (Ruby's pockle block, PockleBlock)
        private const int  ModelAlloc = 0x000, ScriptAlloc = 0x010, SpawnPos = 0x020, IdlePoint = 0x030, Manager = 0x040;
        private const int  ScriptArena = Manager + ManagerSize, ScriptArenaUnits = 1875;   // MonstorScriptBuffer's size
        private const int  ModelData = ScriptArena + ScriptArenaUnits * 16;
        private const float SpawnAhead = 15f, SideOffset = 14f, TargetRange = 150f, LeashRange = 200f;
        private const int   LoadTimeoutMs = 5000;

        private static long _block;                              // the block it lives in (MMU), 0 = none
        private static int  _day = -1, _floorKey = -1;
        private static uint _tex42End, _tex42Top;                // group 42's VRAM ends before it joined
        private static readonly List<int> _ourEntries = new();   // the texture entries its load added
        private static int  _target = -1;

        internal static bool Up => _block != 0;
        private static uint G(long mmu) => (uint)(mmu & Memory.PhysAddrMask);
        private static long Mgr => _block + Manager;

        /// <summary>A random day's pockle loaded and spawned in front of Ruby on the private manager. The caller holds the floor quiet.</summary>
        internal static bool Summon(int day)
        {
            if (Up) return false;
            long block = PockleModel.BlockAddr();
            if (block == 0) { Log("no pockle block (Ruby's character pool lacks it — a disc patched before the Pocklekul?)"); return false; }
            if (Memory.ReadUInt(CodeCaves.AllyManager) != 0) { Log("an ally manager is already being ridden"); return false; }
            var species = PockleModel.Days[day];
            int ti = DaySpecies[day].TableIndex ?? -1;
            if (ti < 0) { Log($"{species.day}: no table index"); return false; }

            uint mgrG = G(block + Manager);
            // the copy of the floor's manager, its script table rewritten, reset
            if (!NativeCall.Invoke(Memcpy, out _, mgrG, MainManagerGuest, ManagerSize, timeoutMs: 1000)) { Log("the manager was not copied (no dungeon frame took the call)"); return false; }
            var table = new byte[16 * 4];
            BitConverter.GetBytes(G(block + ScriptAlloc)).CopyTo(table, 0);
            Memory.WriteBytesBatch(block + Manager + ScriptTable, table);
            WriteAlloc(block + ScriptAlloc, block + ScriptArena, ScriptArenaUnits);
            WriteAlloc(block + ModelAlloc, block + ModelData, PockleBlock.Units - ModelData / 16);
            if (!NativeCall.Invoke(CleanViewMonstor, out _, mgrG, 0)) { Log("the manager was not reset"); return false; }
            Memory.WriteInt(block + Manager + ModelCount, 0);
            Memory.WriteInt(block + Manager + CurrentMonster, 0);
            Memory.WriteInt(block + Manager + RequestedEvent, -1);
            Memory.WriteInt(block + Manager + Paused, 0);

            // room in the monsters' texture group: it grows by the pockle's bank
            long g42 = TextureManager.Base + TextureManager.Blocks + (long)MonsterTexBlock * TextureManager.BlockStride;
            uint cursor = Memory.ReadUInt(TextureManager.Base + TextureManager.Cursor);
            _tex42Top = Memory.ReadUInt(g42 + TextureManager.BlkBase); _tex42End = Memory.ReadUInt(g42 + TextureManager.BlkTop);
            if (_tex42End + 0x180 > cursor) { Log($"no VRAM room in the monsters' group (it ends at 0x{_tex42End:X}, the cursor is 0x{cursor:X})"); return false; }
            var before = EntriesOf(MonsterTexBlock);

            Memory.WriteUInt(CodeCaves.AllyManager, mgrG);        // call_priv's manager (the hooks ride it from now on: no unit lives yet)
            Memory.WriteUInt(CodeCaves.AllyCallFunc, SetupBaseModel);
            if (!NativeCall.Invoke(DeadMcCave.AllyCallPriv, out uint loaded, mgrG, 0, (uint)ti, ShotTexBlock, G(block + ModelAlloc), timeoutMs: LoadTimeoutMs) || loaded == 0)
            { Memory.WriteUInt(CodeCaves.AllyManager, 0); Log($"{species.code}: SetupBaseModel was not made (v0 {loaded})"); return false; }
            int units = Memory.ReadInt(block + ModelAlloc + 8);

            float x = Memory.ReadFloat(Addresses.dunPositionX), h = Memory.ReadFloat(Addresses.dunPositionZ), y = Memory.ReadFloat(Addresses.dunPositionY);
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            Memory.WriteVec3(block + SpawnPos, x + SpawnAhead * MathF.Sin(yaw), h, y + SpawnAhead * MathF.Cos(yaw));
            Memory.WriteFloat(block + SpawnPos + 0xC, 1f);
            _block = block; _day = day; _floorKey = FloorKey(); _target = -1;
            WriteIdlePoint();
            AggroTable.SetAlly(G(block + IdlePoint));
            Memory.WriteUInt(CodeCaves.AllyCallFunc, SetupViewMonstor);
            if (!NativeCall.Invoke(DeadMcCave.AllyCallPriv, out uint spawned, mgrG, 0, G(block + SpawnPos), 0xFFFFFFFFu, timeoutMs: 1000) || spawned == 0)
            { Log($"{species.code}: SetupViewMonstor was not made (v0 {spawned})"); Despawn("not spawned"); return false; }

            _ourEntries.Clear();
            foreach (int e in EntriesOf(MonsterTexBlock)) if (!before.Contains(e)) _ourEntries.Add(e);
            Log($"{species.day}'s pockle ({species.code}, table index {ti}) runs on the private manager 0x{mgrG:X}: model {units:N0} units, "
                + $"{_ourEntries.Count} texture entr{(_ourEntries.Count == 1 ? "y" : "ies")} in group 42 (VRAM end 0x{_tex42End:X} → 0x{Memory.ReadUInt(g42 + TextureManager.BlkTop):X})");
            return true;
        }

        /// <summary>Each tick while it lives: its target (the locked-on enemy, else the live enemy nearest Ruby, else the point at her right),
        /// and the end — its script killed it, the floor changed, an event took the floor.</summary>
        internal static void Tick()
        {
            if (!Up) return;
            if (FloorKey() != _floorKey || PockleModel.BlockAddr() != _block) { Despawn("its floor or Ruby's pool changed"); return; }
            if (Memory.ReadInt(DungeonScriptEvent.BtEventMode) != 0) { Despawn("an event took the floor"); return; }
            if (Memory.ReadInt(Mgr + Units + UnitState) == -1) { Despawn("fallen"); return; }

            float rx = Memory.ReadFloat(Addresses.dunPositionX), ry = Memory.ReadFloat(Addresses.dunPositionY);
            int pick = PickTarget(rx, ry, _target);
            if (pick != _target)
            {
                _target = pick;
                Log(_target >= 0 ? $"targets slot {_target}" : "no enemy near — to Ruby's side");
            }
            WriteIdlePoint();
            AggroTable.SetAlly(_target >= 0 ? G(EnemyAddresses.CharObjects.PosAddr(_target)) : G(_block + IdlePoint));
        }

        /// <summary>The game stops riding the manager (first), the ally's table entry goes back to the player, its texture entries go.</summary>
        internal static void Despawn(string why)
        {
            if (!Up) return;
            Memory.WriteUInt(CodeCaves.AllyManager, 0);
            AggroTable.SetAlly(0);
            if (Player.InDungeonFloor() && FloorKey() == _floorKey)
            {
                foreach (int e in _ourEntries)
                    Memory.WriteBytesBatch(TextureManager.Base + TextureManager.Entries + (long)e * TextureManager.EntryStride, new byte[TextureManager.EntryStride]);
                long g42 = TextureManager.Base + TextureManager.Blocks + (long)MonsterTexBlock * TextureManager.BlockStride;
                if (_ourEntries.Count > 0) { Memory.WriteUInt(g42 + TextureManager.BlkTop, _tex42End); Memory.WriteUInt(g42 + TextureManager.BlkBase, _tex42Top); }
            }
            Log($"{(_day >= 0 ? PockleModel.Days[_day].day : "the")}'s pockle gone ({why})");
            _ourEntries.Clear(); _block = 0; _day = -1; _target = -1;
        }

        private static void WriteIdlePoint()
        {
            float rx = Memory.ReadFloat(Addresses.dunPositionX), rh = Memory.ReadFloat(Addresses.dunPositionZ), ry = Memory.ReadFloat(Addresses.dunPositionY);
            float yaw = Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
            Memory.WriteVec3(_block + IdlePoint, rx + MathF.Cos(yaw) * SideOffset, rh, ry - MathF.Sin(yaw) * SideOffset);
            Memory.WriteFloat(_block + IdlePoint + 0xC, 1f);
        }

        private static void WriteAlloc(long at, long data, int units)
        {
            var a = new byte[16];
            BitConverter.GetBytes(G(data)).CopyTo(a, 0); BitConverter.GetBytes(G(data)).CopyTo(a, 4);
            BitConverter.GetBytes(0).CopyTo(a, 8); BitConverter.GetBytes(units).CopyTo(a, 0xC);
            Memory.WriteBytesBatch(at, a);
        }

        private static HashSet<int> EntriesOf(int block)
        {
            var set = new HashSet<int>();
            for (int i = 1; i < TextureManager.MaxEntries; i++)
                if (Memory.ReadUShort(TextureManager.Base + TextureManager.Entries + (long)i * TextureManager.EntryStride + TextureManager.EntryBlock) == block) set.Add(i);
            return set;
        }

        private static float Dist(int slot, float x, float y)
        {
            long p = EnemyAddresses.CharObjects.PosAddr(slot);
            float dx = Memory.ReadFloat(p) - x, dy = Memory.ReadFloat(p + 8) - y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The locked-on enemy if it is near Ruby, else the current target while it lives near her, else the live enemy nearest
        /// her within <see cref="TargetRange"/>, else −1.</summary>
        private static int PickTarget(float rx, float ry, int current)
        {
            int locked = Memory.ReadInt(PlayerAction.LockOnTargetSlot);
            if (locked >= 0 && locked < EnemyAddresses.FloorSlots.Count && Enemies.IsLive(locked) && Dist(locked, rx, ry) <= LeashRange) return locked;
            if (current >= 0 && Enemies.IsLive(current) && Dist(current, rx, ry) <= LeashRange) return current;
            int best = -1; float bd = TargetRange;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                float d = Dist(s, rx, ry);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        private static readonly EnemyDefaults[] DaySpecies =
            { EnemySpecies.Sunday, EnemySpecies.Monday, EnemySpecies.Tuesday, EnemySpecies.Wednesday, EnemySpecies.Thursday, EnemySpecies.Friday, EnemySpecies.Saturday };

        private static int FloorKey()
            => (Memory.ReadByte(Addresses.checkDungeon) << 16) | (Memory.ReadByte(Addresses.checkFloor) << 8) | (Memory.ReadByte(Addresses.dunBackFloorFlag) != 0 ? 1 : 0);

        private static void Log(string msg) => Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + msg);
    }
}
