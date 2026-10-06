using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb Gemron (EnemySpecies.BombGemron) in play. The species itself is the disc's: its record (SpeciesRows), model,
    /// script and name (ModSpeciesBakes), its blast the radius-50 fireball of shot config 8 (ElfSpeciesPatches.PatchBlastConfig).
    /// Two things are the app's, while one is on the floor:
    ///  · its shot carries the bomb: the pack slot entered for <c>ringo_ex</c> (the apple it throws) has the apple node's visual swapped
    ///    for the item bomb's in every tree (the template and each sub-shot's own), the sub-shots scaled up, and the bomb's textures
    ///    kept in the monster block — the graft BombCarrier makes on the Big Bang's borrowed apple shot, on the species' slot instead;
    ///  · the fuse burns: the engine's blinking hit mark (CHitPointMark, the guard-mark pool's upper entries) sits at the big bomb's
    ///    wick tip, and during the death motion (11, frames 105–125, the blast at 122) walks the wick to its base — the wick's
    ///    centreline in the body bone's frame (<see cref="Fuse"/>) through that bone's world matrix as last drawn.</summary>
    internal static class BombGemron
    {
        private const string Tag = "[BombGemron] ";
        private const int DeathMotion = 11, ShotConfig = 4;                             // ringo_ex's index in the config table
        private const float FuseStart = 105f, BlastFrame = 122f;
        private const string BodyBone = "tama1__m", CarrierNode = "dokuring__m";      // the big bomb's bone; the apple node in the shot's trees
        private const int MonsterTextureBlock = 0x26;                                  // BtLoadMonstor's texture block for the species and their shots
        private const float ShotBombScale = 2f;                                        // the bomb on the apple shot, over the apple's own size
        private const int MarkLife = 16, MarkFirst = 8;                                // the guard-mark pool: entries 8..15 are ours (0 is the guard's)
        /// <summary>The big bomb's wick centreline, tip first, in the body bone's frame (the bake's bomb at 3.5 / 1.22 scale, turned as
        /// the preview page shows it; game_data's albino_gemron.py prints it).</summary>
        private static readonly float[][] Fuse =
        {
            new[] { -1.8859f, -4.0466f, 0.7365f }, new[] { -1.9521f, -4.2506f, 0.7291f }, new[] { -2.2054f, -4.1255f, 1.1838f },
            new[] { -2.5026f, -4.1713f, 1.6148f }, new[] { -2.8156f, -4.2594f, 2.0474f }, new[] { -3.0795f, -4.1413f, 2.5146f },
            new[] { -3.1451f, -3.7073f, 2.846f },  new[] { -2.9902f, -3.1825f, 2.888f },  new[] { -2.7392f, -2.7216f, 2.7487f },
            new[] { -2.4669f, -2.2995f, 2.556f },
        };
        private const long MarkPool = 0x21EC4940;                                      // MyHitPointMark: 16 × 0x20 {pos vec4, timer, blink, on}
        private const int MarkStride = 0x20, MarkTimer = 0x10, MarkBlink = 0x14, MarkOn = 0x18;

        private static readonly uint[] _bodyNode = new uint[EnemyAddresses.FloorSlots.Count];
        private static long _graftSlot;                                                // the pack slot grafted (0 = none)
        private static uint _graftRoot, _graftVisual;
        private static readonly List<(uint node, uint applevisual)> _grafts = new();
        private static readonly List<long> _objs = new();
        private static bool _bigBangNoted;

        /// <summary>Once a dungeon tick.</summary>
        internal static void Tick()
        {
            bool any = false;
            for (int slot = 0; slot < EnemyAddresses.FloorSlots.Count; slot++)
            {
                long a = EnemyAddresses.FloorSlots.SlotAddr(slot, 0);
                if (Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) <= 0 || Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId) != EnemySpecies.BombGemron.Id)
                { _bodyNode[slot] = 0; MarkOff(slot); continue; }
                any = true;
                Spark(slot);
            }
            if (any) Graft(); else Ungraft();
        }

        // ───────────────────────────── the fuse ─────────────────────────────
        private static void Spark(int slot)
        {
            long chara = ModelScaleOffsets.ModelBase - CCharacter.CharScale + (long)slot * ModelScaleOffsets.ModelStride;   // the model table sits at CCharacter + 0x90
            if (_bodyNode[slot] == 0)
            {
                uint root = Memory.ReadGuestPtr(chara + CCharacter.CharModel);
                _bodyNode[slot] = Memory.IsValidGuest(root) ? BombCarrier.NodeNamed(root, BodyBone) : 0;
                if (_bodyNode[slot] == 0) return;
            }
            float t = 0f;
            long model = ModelScaleOffsets.ModelBase + (long)slot * ModelScaleOffsets.ModelStride;
            if (Memory.ReadInt(model + ModelScaleOffsets.PlayingMotionId) == DeathMotion)
                t = Math.Max(0f, Math.Min(1f, (Memory.ReadFloat(model + ModelScaleOffsets.PlayingMotionFrame) - FuseStart) / (BlastFrame - FuseStart)));
            float s = t * (Fuse.Length - 1); int i = Math.Min(Fuse.Length - 2, (int)s); float f = s - i;
            float lx = Fuse[i][0] + (Fuse[i + 1][0] - Fuse[i][0]) * f, ly = Fuse[i][1] + (Fuse[i + 1][1] - Fuse[i][1]) * f, lz = Fuse[i][2] + (Fuse[i + 1][2] - Fuse[i][2]) * f;
            byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(_bodyNode[slot]) + CFrameVu1.WorldMatrix, 0x40);
            if (b == null) return;
            float M(int k) => BitConverter.ToSingle(b, k * 4);
            float x = lx * M(0) + ly * M(4) + lz * M(8) + M(12), h = lx * M(1) + ly * M(5) + lz * M(9) + M(13), y = lx * M(2) + ly * M(6) + lz * M(10) + M(14);
            if (float.IsNaN(x) || float.IsNaN(h) || float.IsNaN(y) || (M(12) == 0f && M(13) == 0f && M(14) == 0f)) return;
            long e = MarkPool + (long)(MarkFirst + slot % (16 - MarkFirst)) * MarkStride;
            Memory.WriteVec3(e, x, h, y); Memory.WriteFloat(e + 0xC, 1f);
            if (Memory.ReadInt(e + MarkOn) != 1) { Memory.WriteInt(e + MarkBlink, 1); Memory.WriteInt(e + MarkOn, 1); }
            Memory.WriteInt(e + MarkTimer, MarkLife);                                   // kept alight: the engine blinks it 4 on / 4 off
        }

        private static void MarkOff(int slot)
        {
            long e = MarkPool + (long)(MarkFirst + slot % (16 - MarkFirst)) * MarkStride;
            if (Memory.ReadInt(e + MarkTimer) == MarkLife && Memory.ReadInt(e + MarkOn) == 1) Memory.WriteInt(e + MarkTimer, 1);   // ours: let it go out
        }

        // ───────────────────────────── the shot ─────────────────────────────
        /// <summary>The bomb's mesh onto the apple shot's trees in the pack slot entered for ringo_ex (re-asserted when a rebuild or the
        /// slot-sharing cave puts an apple back; re-done when the slot is re-entered: new trees).</summary>
        private static void Graft()
        {
            if (BombCarrier._grafts.Count > 0)                                           // the Big Bang holds the bomb's textures in its own block
            { if (!_bigBangNoted) { _bigBangNoted = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the Big Bang has the bomb: the apple stays an apple"); } return; }
            _bigBangNoted = false;
            long slot = ShotSlot();
            if (slot == 0) { Ungraft(); return; }
            uint root = Memory.ReadGuestPtr(slot + 0xCC);
            if (!Memory.IsValidGuest(root)) { Ungraft(); return; }
            uint bombRoot = BombModel.Root();
            if (bombRoot == 0) return;
            uint bombVis = Memory.ReadGuestPtr(Memory.ToMmu(bombRoot) + CFrameVu1.GeomPtr);
            if (!Memory.IsValidGuest(bombVis)) return;
            if (slot == _graftSlot && root == _graftRoot && bombVis == _graftVisual)
            {
                foreach (var (node, _) in _grafts)
                    if (Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) != _graftVisual) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, _graftVisual);
                foreach (long obj in _objs)
                    if (Math.Abs(Memory.ReadFloat(obj + CCharacter.CharScale) - ShotBombScale) > 0.01f) Memory.WriteVec3(obj + CCharacter.CharScale, ShotBombScale, ShotBombScale, ShotBombScale);
                BombModel.KeepTextures(MonsterTextureBlock);
                return;
            }
            Ungraft();
            var roots = new List<uint> { root };
            int count = Memory.ReadInt(slot + ShotEffectPack.OffCount);
            for (int i = 0; i < Math.Min(count, ShotEffectPack.SubShots); i++)
            {
                long obj = slot + ShotEffectPack.OffObj + (long)i * ShotEffectPack.ObjStride;
                uint r = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
                if (Memory.IsValidGuest(r) && !roots.Contains(r)) roots.Add(r);
                _objs.Add(obj);
            }
            var found = new List<(uint, uint)>();
            foreach (uint r in roots)
            {
                uint node = BombCarrier.NodeNamed(r, CarrierNode);
                uint vis = node != 0 ? Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) : 0;
                if (node == 0 || (!Memory.IsValidGuest(vis) && vis != bombVis)) { _objs.Clear(); return; }   // a tree still being built: next tick
                if (vis != bombVis) found.Add((node, vis));
            }
            foreach (var (node, _) in found) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, bombVis);
            foreach (long obj in _objs) Memory.WriteVec3(obj + CCharacter.CharScale, ShotBombScale, ShotBombScale, ShotBombScale);
            _grafts.AddRange(found); _graftSlot = slot; _graftRoot = root; _graftVisual = bombVis;
            BombModel.KeepTextures(MonsterTextureBlock);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the bomb's mesh on the apple shot: {found.Count} tree(s) of slot 0x{slot:X}, {_objs.Count} sub-shots at ×{ShotBombScale:0.#}");
        }

        /// <summary>The pack slot whose config is ringo_ex (0 = not entered on this floor).</summary>
        private static long ShotSlot()
        {
            uint pack = Memory.ReadGuestPtr(ShotEffectPack.NowShotEffectPtr);
            if (!Memory.IsValidGuest(pack)) return 0;
            uint cfg = Memory.ReadUInt(ShotEffectPack.CfgTable + ShotConfig * 4);
            for (int i = 0; i < ShotEffectPack.PackSlots; i++)
            {
                long s = Memory.ToMmu(pack) + (long)i * ShotEffectPack.SlotStride;
                if (Memory.ReadUInt(s + ShotEffectPack.OffCfg) == cfg) return s;
            }
            return 0;
        }

        private static void Ungraft()
        {
            if (_grafts.Count > 0 && Memory.ReadGuestPtr(_graftSlot + 0xCC) == _graftRoot)
            {
                foreach (var (node, apple) in _grafts)
                    if (Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) == _graftVisual) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, apple);
                foreach (long obj in _objs) Memory.WriteVec3(obj + CCharacter.CharScale, 1f, 1f, 1f);
            }
            if (_grafts.Count > 0) BombModel.ReleaseTextures();
            _grafts.Clear(); _objs.Clear(); _graftSlot = 0; _graftRoot = 0; _graftVisual = 0;
        }

        /// <summary>A new floor: the slots are new enemies, the pack re-entered, the cash emptied.</summary>
        internal static void Reset()
        {
            Array.Clear(_bodyNode, 0, _bodyNode.Length);
            _grafts.Clear(); _objs.Clear(); _graftSlot = 0; _graftRoot = 0; _graftVisual = 0;
            BombModel.ReleaseTextures();
            BombModel.Forget();
        }
    }
}
