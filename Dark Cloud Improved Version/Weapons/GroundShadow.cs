using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A round shadow on the floor for a prop the engine draws none for — the Terra Sword's boulder, the Cactus's
    /// Desert Bloom (cactus and palm). One at a time: the ISO's rock-shadow cave (DebugIfCave.RockShadow, in Draw_MainUnitShadow's
    /// pass) draws the frame CodeCaves.RockShadow names with MGDrawShadowFast while its flag is set.
    ///
    /// The frame is a flat unit disc (<see cref="DiscSegments"/> segments, y 0, the fan wound both ways) loaded by LoadMDSFile as a
    /// SHADOW model (kind 8: CVisualShadow — a lit mesh drawn in shadow mode comes out garbled). It lives in an item-cash entry of
    /// its own (label <see cref="CashKey"/>, one for every user): the disc loaded there by SetCashModel as a plain model with an 8×8
    /// stand-in of its material's texture, then again as the shadow model into the same entry's allocator (CashModel.ShadowRoot).
    /// The cash is the game's own allocation for the floor — menus do not load into it (they do into the dungeon read buffer: a disc
    /// kept there was overwritten by the party screen's ally models, and drawn behind the menu it reset the game) — so the shadow
    /// stays up through a pause. Its MDS borrows iwa.mds's header, node and materials, the MDT rebuilt (MdtCarve). Placed as a
    /// character's shadow frame is (CFrame's SetScale / SetRotation / SetPosition fields): AT the floor, the point handed to the draw
    /// <see cref="Drop"/> below it — the player's own convention (DrawShadow__10CCharacter); its TRS scale is the radius.</summary>
    internal static class GroundShadow
    {
        private const string Tag = "[GroundShadow] ";
        internal const int   CashKey = 30002;                  // the cash entry's label: no item has this id (the palm 30000, the rock 30001)
        private const int    DiscSegments = 16;
        private const int    StandInSize = 8;                  // the lit disc's texture (never drawn: only its entry must exist)
        private const int    ShadowNeedUnits = DiscSegments * 2 * 6 + 0x80;   // ~96 B of shadow VU data per triangle (both faces) + 2 KB
        private const float  Lift = 0.3f;                      // the frame this far above the floor (off its surface)
        private const float  Drop = 12.8f;                     // the point below the frame (DrawShadow__10CCharacter, 0x2A1888)

        private static readonly CashModel M = new CashModel(Tag, CashKey, "shadow disc", Files);
        private static byte[] _mds, _img;
        private static bool _shown;
        private static volatile uint _placed;                  // the shadow frame the last Show placed

        /// <summary>Every tick of a user's loop, before its pause check: in anything but play, the pause screen or the item menu (the
        /// character-change screen, an event, a floor change) the shadow off; shown again by the next Show.</summary>
        internal static void Guard()
        {
            if (Player.CheckDunIsWalkingMode() || Player.CheckDunIsPausedOrMenu()) return;
            Hide();
        }

        /// <summary>The disc's shadow root frame (guest), loading the entry and the shadow model when needed; 0 when it cannot be had.</summary>
        internal static uint Root() => Files().mds == null ? 0 : M.ShadowRoot(_mds, ShadowNeedUnits);

        /// <summary>Forget the load (a floor change empties the cash).</summary>
        internal static void Forget() { Hide(); _placed = 0; M.Forget(); }

        private static (byte[] mds, byte[] img) Files()
        {
            _mds ??= Disc();
            _img ??= IwaModel.StandInBank(StandInSize);
            return _mds == null || _img == null ? (null, null) : (_mds, _img);
        }

        /// <summary>The shadow on: centred at (<paramref name="x"/>, <paramref name="y"/>) on the floor at <paramref name="ground"/>,
        /// <paramref name="radius"/> across (null leaves the radius as it is — see <see cref="SetRadius"/>).</summary>
        internal static void Show(float x, float ground, float y, float? radius)
        {
            uint root = Root();
            if (root == 0) { Hide(); return; }
            _placed = root;
            long f = Memory.ToMmu(root);
            if (radius is float r) Memory.WriteVec3(f + CFrameVu1.TrsScaleX, r, r, r);
            Memory.WriteVec3(f + CFrameVu1.EulerX, 0f, 0f, 0f);
            Memory.WriteVec3(f + CFrameVu1.TrsPosX, x, ground + Lift, y);
            Memory.WriteInt (f + 0x23C, 0);                                                       // SetRotation's own clears
            Memory.WriteInt (f + CFrameVu1.WorldCacheB, 0);
            Memory.WriteInt (f + 0x248, Memory.ReadInt(f + 0x248) | 1);
            Memory.WriteInt (f + CFrameVu1.DirtyTrs, 1);
            Memory.WriteInt (f + CFrameVu1.WorldCacheA, 0);
            var b = new byte[0x30];
            BitConverter.GetBytes(root).CopyTo(b, CodeCaves.RockShadowFrame);
            BitConverter.GetBytes(x).CopyTo(b, CodeCaves.RockShadowPlane);
            BitConverter.GetBytes(ground + Lift - Drop).CopyTo(b, CodeCaves.RockShadowPlane + 4);
            BitConverter.GetBytes(y).CopyTo(b, CodeCaves.RockShadowPlane + 8);
            BitConverter.GetBytes(1f).CopyTo(b, CodeCaves.RockShadowPlane + 12);
            BitConverter.GetBytes(1f).CopyTo(b, CodeCaves.RockShadowDir + 4);                     // (0, 1, 0, 0): straight down, the engine's own
            Memory.WriteBytesBatch(CodeCaves.RockShadow + 4, b.AsSpan(4).ToArray());              // frame and vectors first…
            Memory.WriteInt(CodeCaves.RockShadow + CodeCaves.RockShadowFlag, 1);                  // …the flag last
            _shown = true;
        }

        /// <summary>The radius alone (the TRS scale) — for a caller growing it faster than its placement.</summary>
        internal static void SetRadius(float radius)
        {
            uint root = _placed;                                                                  // the frame the last Show placed (this runs at 2 ms)
            if (root == 0) return;
            long f = Memory.ToMmu(root);
            Memory.WriteVec3(f + CFrameVu1.TrsScaleX, radius, radius, radius);
            Memory.WriteInt (f + CFrameVu1.DirtyTrs, 1);
            Memory.WriteInt (f + CFrameVu1.WorldCacheA, 0);
        }

        internal static void Hide()
        {
            if (!_shown) return;
            Memory.WriteInt(CodeCaves.RockShadow + CodeCaves.RockShadowFlag, 0);
            _shown = false;
        }

        /// <summary>A one-node MDS of a flat unit disc (iwa.mds's header and node, its MDT rebuilt): a centre and DiscSegments rim
        /// points at y 0, a triangle fan wound both ways (one face survives whichever way the shadow program culls), one UV and
        /// one up normal, iwa's materials kept.</summary>
        private static byte[] Disc()
        {
            byte[] mds = IwaModel.ModelBytes();
            if (mds == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "no template model (iwa.mds) — no shadow"); return null; }
            const int NodeEnd = 0x80;                                                       // MDS header 0x10 + one 0x70 node; the MDT follows
            var m = MdtCarve.MdtParse(mds, NodeEnd);
            m.pos = new List<float[]> { new[] { 0f, 0f, 0f, 1f } };
            for (int i = 0; i < DiscSegments; i++)
            {
                double a = 2 * Math.PI * i / DiscSegments;
                m.pos.Add(new[] { (float)Math.Sin(a), 0f, (float)Math.Cos(a), 1f });
            }
            m.uv = new List<float[]> { new[] { 0f, 0f, 0f, 0f } };
            m.norm = new List<float[]> { new[] { 0f, 1f, 0f, 0f } };
            m.hasCol = false; m.col = null;
            var recs = new List<int[]>();
            void Tri(int a, int b, int c) { foreach (int v in new[] { a, b, c }) recs.Add(new[] { v, 0, 0 }); }
            for (int i = 0; i < DiscSegments; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % DiscSegments;
                Tri(0, a, b); Tri(0, b, a);
            }
            m.subs = new List<(int prim, int mat, List<int[]> recs)> { (3, 0, recs) };
            byte[] mdt = MdtCarve.MdtBuild(m);
            IsoBytes.U32(mdt, 0x14, (uint)m.uv.Count);                                      // the count words MdtBuild keeps verbatim
            IsoBytes.U32(mdt, 0x2C, (uint)m.norm.Count);
            var outb = new byte[NodeEnd + mdt.Length];
            Array.Copy(mds, outb, NodeEnd);
            Array.Copy(mdt, 0, outb, NodeEnd, mdt.Length);
            return outb;
        }
    }
}
