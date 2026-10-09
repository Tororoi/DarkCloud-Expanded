using System;
using System.Collections.Generic;
using static Dark_Cloud_Improved_Version.BigBangShot;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb's mesh on the apple shot: every tree of the entered apple-shot instance (the template and each sub-shot's
    /// own) has its apple node's visual swapped for the bomb model's, so each fired shot flies and turns as the apple does but draws
    /// the bomb (<see cref="GraftBomb"/>, re-done when the instance is re-entered and re-asserted when a rebuild puts an apple back),
    /// the sub-shot objects' scales held at their shot's size; the apples' own visuals go back on <see cref="Ungraft"/>. One of the
    /// <see cref="BigBangShot"/> classes, which share their members through using static.</summary>
    internal static class BombCarrier
    {
        private const string Tag = "[BigBangShot/BombCarrier] ";
        internal static uint _graftRoot, _graftVisual;                  // the entered instance's template root (the entry the grafts belong to) and the bomb's visual
        /// <summary>The Bomb's mesh onto the apple shot's nodes. The entered instance holds a template tree (+0xCC) AND one tree
        /// per sub-shot object (each object's own model pointer) — the sub-shots draw their own — so every tree is walked for
        /// the node that draws (the apple, `dokuring__m`) and its visual pointer swapped for the bomb model's; the bomb's
        /// textures are tagged into the effect's texture block for as long as it stays. The apples' own visuals go back on
        /// Stop. Re-done whenever the instance is re-entered (a new floor: new trees).</summary>
        internal static readonly List<(uint node, uint carrierVisual)> _grafts = new List<(uint, uint)>();
        internal static readonly Dictionary<long, float> _objScale = new Dictionary<long, float>();   // each sub-shot object's scale while grafted — the fired shot's kind sets it (an animated node's matrix is rebuilt every frame; the object's scale is not)
        internal static void GraftBomb()
        {
            if (_carrier == null || !BorrowedShots.Entered(_carrier)) { _graftRoot = 0; _grafts.Clear(); return; }
            uint root = Memory.ReadGuestPtr(_carrier.Instance + 0xCC);
            if (!Memory.IsValidGuest(root)) { _graftRoot = 0; _grafts.Clear(); return; }
            if (root == _graftRoot)
            {
                if (_grafts.Count == 0) return;
                foreach (var (node, _) in _grafts)
                    if (Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) != _graftVisual) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, _graftVisual);   // a rebuild put an apple back
                foreach (var kv in _objScale)
                    if (Math.Abs(Memory.ReadFloat(kv.Key + CCharacter.CharScale) - kv.Value) > 0.01f) Memory.WriteVec3(kv.Key + CCharacter.CharScale, kv.Value, kv.Value, kv.Value);
                return;
            }
            uint bombRoot = BombModel.Root();
            if (bombRoot == 0) return;
            uint bombVis = Memory.ReadGuestPtr(Memory.ToMmu(bombRoot) + CFrameVu1.GeomPtr);
            if (!Memory.IsValidGuest(bombVis)) return;
            _grafts.Clear(); _objScale.Clear(); _graftRoot = root; _graftVisual = bombVis;
            var roots = new List<uint> { root };
            int count = Memory.ReadInt(_carrier.Instance + ShotEffectPack.OffCount);
            for (int i = 0; i < Math.Min(count, ShotEffectPack.SubShots); i++)
            {
                long obj = _carrier.Instance + ShotEffectPack.OffObj + i * ShotEffectPack.ObjStride;
                uint r = Memory.ReadGuestPtr(obj + CCharacter.CharModel);
                if (Memory.IsValidGuest(r) && !roots.Contains(r)) roots.Add(r);
                Memory.WriteVec3(obj + CCharacter.CharScale, PelletBombScale, PelletBombScale, PelletBombScale);   // the object's scale, which the draw re-applies every frame; a fire sets its shot's
                _objScale[obj] = PelletBombScale;
            }
            // The apple's node BY NAME: a tree still being built (the instance re-entered) has the impact's light node with a visual
            // before the apple has one, so a tree without a posed apple node waits (nothing recorded; next tick tries again).
            var found = new List<(uint node, uint carrierVisual)>();
            foreach (uint r in roots)
            {
                uint node = NodeNamed(r, CarrierNode);
                uint carrierVis = node != 0 ? Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) : 0;
                if (node == 0 || (!Memory.IsValidGuest(carrierVis) && carrierVis != bombVis)) { _graftRoot = 0; _grafts.Clear(); _objScale.Clear(); return; }
                if (carrierVis != bombVis) found.Add((node, carrierVis));
            }
            foreach (var (node, _) in found) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, bombVis);
            _grafts.AddRange(found);
            if (_grafts.Count == 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the apple shot's trees already carry the bomb"); return; }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the bomb's mesh grafted onto `{CarrierNode}` in {_grafts.Count} of the apple shot's trees ({roots.Count} trees: the template and the sub-shots) → visual 0x{bombVis:X}");
        }
        private const int MaxTreeNodes = 512;                                           // the Crystal Gemron's tree, the biggest searched, has 296

        /// <summary>The node under <paramref name="root"/> (root included) named <paramref name="name"/>; 0 when none.</summary>
        internal static uint NodeNamed(uint root, string name)
        {
            var work = new Stack<uint>(); work.Push(root); int guard = 0;
            while (work.Count > 0 && guard++ < MaxTreeNodes)
            {
                uint n = work.Pop();
                if (!Memory.IsValidGuest(n)) continue;
                byte[] nb = Memory.ReadBytesBatch(Memory.ToMmu(n) + CFrameVu1.Name, 16);
                if (nb != null && System.Text.Encoding.ASCII.GetString(nb).Split('\0')[0] == name) return n;
                for (uint c = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.RootChild); Memory.IsValidGuest(c); c = Memory.ReadGuestPtr(Memory.ToMmu(c) + CFrameVu1.RootSibling)) work.Push(c);
            }
            return 0;
        }
        internal static void Ungraft()
        {
            if (_grafts.Count > 0 && _carrier != null && Memory.ReadGuestPtr(_carrier.Instance + 0xCC) == _graftRoot)
            {
                foreach (var (node, carrierVis) in _grafts)
                    if (Memory.ReadGuestPtr(Memory.ToMmu(node) + CFrameVu1.GeomPtr) == _graftVisual) Memory.WriteUInt(Memory.ToMmu(node) + CFrameVu1.GeomPtr, carrierVis);   // the apple's own visual back
                foreach (long obj in _objScale.Keys) Memory.WriteVec3(obj + CCharacter.CharScale, 1f, 1f, 1f);                                                                  // …and its size
            }
            _graftRoot = 0; _graftVisual = 0; _grafts.Clear(); _objScale.Clear();
        }
    }
}
