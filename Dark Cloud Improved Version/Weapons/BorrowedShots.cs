using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// A species' shot effect, borrowed by one of Xiao's abilities, available on every floor. A species' shot is a
    /// BT_SHOT_EFFECT config (<see cref="ShotEffectPack.CfgTable"/>) the floor loader enters into the shot pack (five slots)
    /// with the floor's monster pool as allocator; the configs name shared `dun\effect` files, so entering one on a floor
    /// without its species works the same way. Each ability hands <see cref="Start"/> a provider naming the config it wants
    /// right now (−1 = none); the first non-negative answer wins. <see cref="Seed"/> writes a copy of that config — victim mask
    /// switched from the player to ENEMIES, flags cut to the element bits — into <see cref="CodeCaves.BorrowedShotBlock"/> (one
    /// at a time: each costs the floor its file in the monster pool and one of the pack's five slots, and a first cut
    /// entering five froze a floor load); ElfCave.BorrowedShotsEnter, at the head of the per-frame step chain, keeps it
    /// entered — after a floor load rebuilds the pack, and the frame after another config is seeded — recording the slot
    /// and, before each entry, the monster pool's fill level. ANOTHER config on the same floor reuses that one slot:
    /// <see cref="Seed"/> empties it (the sub-shots' active flags and the slot's config pointer, which is what the loader
    /// leaves for a species without shots), rewinds the pool to the recorded level so the old model's memory is reused, and
    /// writes −1 — the cave enters the new config the next frame. A full pack clears the block's magic until the mod seeds
    /// again. <see cref="Fire"/> is the Set replica the Guardian Reflector re-fires with, for a XIAO-owned shot: the engine
    /// steps it, collides it with enemies (the mask) and plants its damage entry with her owner id, ability flags and
    /// anti-category bytes, so CheckDmg treats it exactly as one of her pellets — with the shot's own element. The step
    /// plants that entry every frame a phase has a radius, then keeps quiet for the sub-shot's reload frames; so the copy's
    /// FLYING radius is zeroed (the contact sweep still runs) and <see cref="Fire"/> sets the reload past any impact
    /// animation: the impact's first frame is the one plant — the whole explosion radius, every enemy inside it once — and
    /// a miss's expiry burst likewise.
    /// </summary>
    internal static class BorrowedShots
    {
        private const string Tag = "[BorrowedShots] ";
        private const byte   PlantReload = 120;   // frames of silence after a plant (a byte): longer than any impact animation
        private static Func<int>[] _wanted = Array.Empty<Func<int>>();   // each ability's answer: the config index it wants, or −1
        private static int _seededCfg = -1;       // the config index in the block (−1 = none)
        private static int _lastSlot = -3;         // the slot word last logged
        private static int _lastFloor = -1;        // checkFloor last seen in a floor: a change → the slot word back to −1 (retry)
        private static bool _wasInFloor, _lastQuiet;
        private static System.Threading.Thread _thread;

        /// <summary>Keep the block matched to what the abilities want, from app start — ready BEFORE a floor loads (the loader
        /// runs before any ability thread does). Each <paramref name="wanted"/> answers with a config index or −1, reading
        /// whatever it keys on (an equipped weapon's inventory record is valid in town and dungeon alike); the first
        /// non-negative answer is seeded, none → cleared.</summary>
        internal static void Start(params Func<int>[] wanted)
        {
            _wanted = wanted;
            if (_thread != null && _thread.IsAlive) return;
            _thread = new System.Threading.Thread(Loop) { IsBackground = true, Name = "BorrowedShots" };
            _thread.Start();
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    int cfg = -1;
                    foreach (var want in _wanted) { cfg = want(); if (cfg >= 0) break; }
                    if (cfg >= 0 && cfg < ShotEffectPack.CfgCount) Seed(cfg); else Clear();
                    bool inFloor = Player.InDungeonFloor();
                    int floor = inFloor ? Memory.ReadUShort(Addresses.checkFloor) : -1;
                    if (_seededCfg >= 0 && inFloor && (!_wasInFloor || floor != _lastFloor))
                    {   // a new floor: enter it again (the cave's own check catches the rebuilt pack too; this restores a full pack's cleared magic)
                        Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotSlot, -1);
                        Memory.WriteInt(CodeCaves.BorrowedShotBlock, (int)CodeCaves.BorrowedShotMagic);
                    }
                    _wasInFloor = inFloor; _lastFloor = floor;
                    int packSlot = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotSlot);
                    bool quiet = Memory.ReadInt(CodeCaves.BorrowedShotBlock) == 0;
                    if (_seededCfg >= 0 && inFloor && (packSlot != _lastSlot || quiet != _lastQuiet))
                    {
                        _lastSlot = packSlot; _lastQuiet = quiet;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"config {_seededCfg} → pack slot {packSlot}" + (quiet ? " (the pack is full on this floor)" : packSlot < 0 ? " (to be entered)" : ""));
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); }
                System.Threading.Thread.Sleep(250);
            }
        }

        private static void Clear()
        {
            if (_seededCfg < 0) return;
            Memory.WriteInt(CodeCaves.BorrowedShotBlock, 0);         // no magic: the cave enters nothing
            ReleaseSlot();
            _seededCfg = -1; _lastSlot = -3;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "block cleared (no ability wants a shot)");
        }

        /// <summary>Put the config in the block unless it is there already, slot −1: the cave enters it on its next frame in
        /// a floor. When another config is entered on this floor, its slot is emptied and the pool rewound first, so the new
        /// one takes the same slot and memory.</summary>
        private static void Seed(int cfgIndex)
        {
            if (cfgIndex == _seededCfg) return;
            uint cfgAddr = Memory.ReadUInt(ShotEffectPack.CfgTable + cfgIndex * 4);
            byte[] c = Memory.ReadBytesBatch(0x20000000L + cfgAddr, ShotEffectPack.CfgSize);
            if (c == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "config table unreadable — no borrowed shots"); return; }
            BitConverter.GetBytes(2).CopyTo(c, ShotEffectPack.CfgVictimMask);                            // hurts enemies, not the player
            BitConverter.GetBytes(0f).CopyTo(c, ShotEffectPack.CfgRadiusFlying);                         // no planting in flight: the impact's first frame is the first plant
            int flags = BitConverter.ToInt32(c, ShotEffectPack.CfgFlags);
            BitConverter.GetBytes(flags & ShotEffectPack.CfgElementBits).CopyTo(c, ShotEffectPack.CfgFlags);   // the element only — no ailment (the Black Dragon's shot carries Freeze)
            Memory.WriteInt(CodeCaves.BorrowedShotBlock, 0);                                               // quiet while the block changes
            ReleaseSlot();
            Memory.WriteBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCfg, c);
            Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotSlot, -1);
            Memory.WriteInt(CodeCaves.BorrowedShotBlock, (int)CodeCaves.BorrowedShotMagic);
            _seededCfg = cfgIndex; _lastSlot = -3;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"config {cfgIndex} seeded — entered on the next frame in a floor");
        }

        /// <summary>Give the entered slot back: nothing in flight, the slot's config pointer cleared (an empty slot to Entry),
        /// the monster pool rewound to before its model was loaded. Only while in a floor with a slot recorded.</summary>
        private static void ReleaseSlot()
        {
            int slot = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotSlot);
            if (slot < 0 || !Player.InDungeonFloor()) return;
            long pack = Memory.ReadInt(ShotEffectPack.NowShotEffectPtr);
            if (pack <= 0) return;
            long inst = pack + 0x20000000 + slot * ShotEffectPack.SlotStride;
            if (Memory.ReadInt(inst + ShotEffectPack.OffCfg) != CodeCaves.BorrowedShotBlockGuest + CodeCaves.BorrowedShotCfg) return;   // not ours (a rebuilt pack)
            for (int i = 0; i < ShotEffectPack.SubShots; i++) Memory.WriteUShort(inst + ShotEffectPack.OffActive + i * 2, 0);
            Memory.WriteInt(inst + ShotEffectPack.OffCount, 0);
            Memory.WriteInt(inst + ShotEffectPack.OffLastIdx, -1);
            Memory.WriteInt(inst + ShotEffectPack.OffCfg, 0);
            int mark = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotWatermark);
            int used = Memory.ReadInt(DataPools.Monstor + DataPools.Used);
            if (mark > 0 && mark <= used) Memory.WriteInt(DataPools.Monstor + DataPools.Used, mark);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"slot {slot} released (monster pool {used} → {mark} units)");
        }

        /// <summary>The pack slot holding the config's shot on this floor, or −1 (also when another config is seeded).</summary>
        internal static int SlotFor(int cfgIndex)
        {
            if (cfgIndex != _seededCfg) return -1;
            int slot = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotSlot);
            return slot < 0 ? -1 : slot;
        }

        /// <summary>Fire the config's shot from <paramref name="x"/>/<paramref name="h"/>/<paramref name="y"/> with velocity
        /// <paramref name="vx"/>/<paramref name="vh"/>/<paramref name="vy"/> (units per frame), <paramref name="damage"/> base
        /// damage and <paramref name="life"/> frames of flight, as Xiao's. False when the config has no slot on this floor or
        /// its sub-shots are all busy.</summary>
        internal static bool Fire(int cfgIndex, float x, float h, float y, float vx, float vh, float vy, int damage, int life)
        {
            int slot = SlotFor(cfgIndex);
            if (slot < 0) return false;
            long pack = Memory.ReadInt(ShotEffectPack.NowShotEffectPtr);
            if (pack <= 0) return false;
            long inst = pack + 0x20000000 + slot * ShotEffectPack.SlotStride;
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return false;
            int j = -1;
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) == 0) { j = i; break; }
            if (j < 0) return false;
            long cfg = Memory.ReadInt(inst + ShotEffectPack.OffCfg);
            if (cfg <= 0) return false;
            cfg += 0x20000000;
            long obj = inst + ShotEffectPack.OffObj + j * ShotEffectPack.ObjStride, dirA = inst + ShotEffectPack.OffDir + j * 0x10;
            int flyMot = (short)Memory.ReadUShort(cfg + ShotEffectPack.CfgFlyMotion);
            long ftab = Memory.ReadInt(obj + ShotEffectPack.ObjFrameTb);
            float startFrame = ftab > 0 ? Memory.ReadInt(ftab + 0x20000000 + flyMot * 0x10) : 1;
            long rec = WeaponHave.BattleWeaponRecord;
            // Set's own field order; active LAST. Owner 1 = Xiao, collider 0 as her pellets, her live flags and anti bytes.
            Memory.WriteUShort(inst + ShotEffectPack.OffPhase + j * 2, 1);              // flying, no muzzle
            Memory.WriteVec3  (obj + ShotEffectPack.ObjPos, x, h, y);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 12, 1f);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotId, flyMot);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotFlag, 4);
            Memory.WriteFloat (obj + ShotEffectPack.ObjMotSpd, -1f);
            Memory.WriteFloat (obj + ShotEffectPack.ObjFrame, startFrame);
            Memory.WriteVec3  (dirA, vx, vh, vy);
            Memory.WriteInt   (inst + ShotEffectPack.OffWait + j * 4, life);
            Memory.WriteInt   (inst + ShotEffectPack.OffDamage + j * 4, damage);
            Memory.WriteInt   (inst + ShotEffectPack.OffUserCol + j * 4, 0);
            Memory.WriteUShort(inst + ShotEffectPack.OffOwner + j * 2, 1);
            Memory.WriteUShort(inst + ShotEffectPack.OffAttr2 + j * 2, 0);
            Memory.WriteUShort(inst + ShotEffectPack.OffA060 + j * 2, 0xFFFF);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0B0 + j * 4, -1);
            Memory.WriteFloat (inst + ShotEffectPack.OffA0D0 + j * 4, -1f);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0F0 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffA110 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffWepFlags + j * 4, Memory.ReadUShort(rec + WeaponHave.AbilityFlagsOffset));
            Memory.WriteUInt  (inst + ShotEffectPack.OffAntiPtr + j * 4, (uint)(rec - 0x20000000 + WeaponHave.WeaponAntiOffset));
            Memory.WriteByte  (inst + ShotEffectPack.OffSndFlag + j, 0);
            Memory.WriteByte  (inst + ShotEffectPack.OffReload + j, PlantReload);
            Memory.WriteByte  (inst + ShotEffectPack.OffLatch + j, 0);                 // plants its damage
            Memory.WriteInt   (inst + ShotEffectPack.OffLastIdx, j);
            ShotEffects.FaceAlong(obj, vx, vh, vy);
            Memory.WriteUShort(inst + ShotEffectPack.OffActive + j * 2, 1);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"config {cfgIndex} shot from slot {slot}#{j}: damage {damage}, {life} frames, v=({vx:F2},{vh:F2},{vy:F2})");
            return true;
        }
    }
}
