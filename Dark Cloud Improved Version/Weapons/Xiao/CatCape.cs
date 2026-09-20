using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static Dark_Cloud_Improved_Version.DivineBeastCat;
using static Dark_Cloud_Improved_Version.CatFlight;
using static Dark_Cloud_Improved_Version.CatCopy;
using static Dark_Cloud_Improved_Version.CatTextures;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Super Steve cape on the cat: her cape cloth (CCloth 0x8550) taken as the template, spawned and scaled onto the copy, watched, breezed, stiffened and reseeded. One of the <see cref="DivineBeastCat"/> classes, which share their members through using static.</summary>
    internal static class CatCape
    {
        // ── the Super Steve cape (CCloth 0x8550) ───────────────────────────────────────────────────────────
        internal static long _capeObj;
        private static uint _capeTemplate;                                    // her CCloth for cat_cape, taken out of her draw list by TakeHerCape
        internal static int _capeSweepTick;

        /// <summary>The cape's cloth record lives in HER pack, so the engine builds it for XIAO and hangs it off her own cloth
        /// list — anchored to the hidden cat_cape node at her origin, where it draws as a sheet at her feet whether or not the cat
        /// is out. Take it out of her list the moment it appears (every reload rebuilds it) and keep the object as the template
        /// the cat's copy is cloned from.</summary>
        internal static void TakeHerCape()
        {
            uint herList = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.ClothList);
            if (!Memory.IsValidGuest(herList)) return;
            for (int i = 0; i < CCloth.ClothMaxPieces; i++)
            {
                uint obj = Memory.ReadGuestPtr(Memory.ToMmu(herList) + i * 4);
                if (!Memory.IsValidGuest(obj)) continue;
                uint frame = Memory.ReadGuestPtr(Memory.ToMmu(obj) + CCloth.ClothAttach);
                if (!Memory.IsValidGuest(frame) || ReadName(frame) != CapeNodeName) continue;
                Memory.WriteInt(Memory.ToMmu(herList) + i * 4, 0);
                if (_capeTemplate != obj) Log($"cape: took her own copy out of her cloth list (entry {i}, 0x{obj:X}) — it is the clone template");
                _capeTemplate = obj;
                return;
            }
        }
        private static int  _capeWatch;
        /// <summary>Clone her cape's CCloth onto the copy, CharacterClone.CopyCloth's recipe: the whole object, two private
        /// draw packets (the engine rebuilds the packet from the particles every draw), the anchor re-pointed at the copy's
        /// cat_sebone2 where the rest lattice was authored, no body capsules, the Verlet "previous" seeded from "current" so
        /// the first step is quiet, and the copy's +0xC74 pointing at a one-entry list. Her own list entry is zeroed so she
        /// neither steps nor draws it. The dungeon chara loop steps every slot's cloth while MirageSceneGateFlag == 1 (the
        /// Mirage pnach's ClothStep swap), which the cat already sets; Draw__10CCharacter draws the list.</summary>
        internal static void SpawnCape()
        {
            _capeObj = 0;
            TakeHerCape();                                                        // a reload rebuilds her copy: re-cache it first
            uint herList = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.ClothList);
            if (!Memory.IsValidGuest(herList)) { Log("cape: she has no cloth list — is the ISO patched with the cape?"); return; }
            uint template = 0; int entry = -1;
            for (int i = 0; i < CCloth.ClothMaxPieces; i++)
            {
                uint obj = Memory.ReadGuestPtr(Memory.ToMmu(herList) + i * 4);
                if (!Memory.IsValidGuest(obj)) continue;
                uint frame = Memory.ReadGuestPtr(Memory.ToMmu(obj) + CCloth.ClothAttach);
                if (Memory.IsValidGuest(frame) && ReadName(frame) == CapeNodeName) { template = obj; entry = i; break; }
            }
            if (template == 0 && Memory.IsValidGuest(_capeTemplate))              // cleared from her list by an earlier spawn: still hers, still intact
            {
                uint frame = Memory.ReadGuestPtr(Memory.ToMmu(_capeTemplate) + CCloth.ClothAttach);
                if (Memory.IsValidGuest(frame) && ReadName(frame) == CapeNodeName) template = _capeTemplate;
            }
            if (template == 0) { Log("cape: no cloth anchored to " + CapeNodeName + " in her list — is the ISO patched with the cape?"); return; }
            {
                int i = entry; uint obj = template;
                byte[] o = Memory.ReadBytesBatch(Memory.ToMmu(obj), CCloth.ClothObjSize);
                if (o == null) { Log("cape: template read failed"); return; }
                int wide = BitConverter.ToInt32(o, 0x2C), hang = BitConverter.ToInt32(o, 0x30);   // outer (across the back) × inner (down the cape; index 0 pinned)
                uint b0 = (uint)BitConverter.ToInt32(o, CCloth.ClothBuf0) & Memory.PhysAddrMask, b1 = (uint)BitConverter.ToInt32(o, CCloth.ClothBuf0 + 4) & Memory.PhysAddrMask;
                // How big a draw packet this cloth builds. Take it from the cloth's OWN figure (+0x1C, what CreateVUData returned
                // at init, in 16-byte units) and never from a guess: the packet is rebuilt into these buffers from scratch every
                // draw, so one byte short is an overrun straight through the rest of the cave (a 12 × 16 lattice needs 17,760 B).
                // The pointer gap is only a cross-check; the packet figure wins.
                int packet = BitConverter.ToInt32(o, CCloth.ClothPacketUnits) * 16;
                int gap = (int)(b1 - b0);
                int bufSize = Math.Max(packet, gap > 0 && gap < 0x20000 ? gap : 0);
                bufSize = (bufSize + 0x3F) & ~0x3F;
                if (packet <= 0 || bufSize > 0x20000)
                {
                    Log($"cape: refusing to clone — packet {packet} B, pointer gap {gap} B, neither is a sane buffer size");
                    return;
                }
                long cObj = TakeCave(CCloth.ClothObjSize, out uint cObjG);
                long cB0 = TakeCave(bufSize, out uint cB0G), cB1 = TakeCave(bufSize, out uint cB1G), cList = TakeCave(16, out uint cListG);
                if (cObj == 0 || cB0 == 0 || cB1 == 0 || cList == 0) { Log("cape: no cave room — no cape"); return; }
                int anchor = NodeIndexOf(CapeAnchorName);
                if (anchor < 0) { Log("cape: no " + CapeAnchorName + " in the copy — no cape"); return; }
                BitConverter.GetBytes(cB0G).CopyTo(o, CCloth.ClothActive);
                BitConverter.GetBytes(cB0G).CopyTo(o, CCloth.ClothBuf0);
                BitConverter.GetBytes(cB1G).CopyTo(o, CCloth.ClothBuf0 + 4);
                BitConverter.GetBytes((uint)(CodeCaves.NodePoolGuest + anchor * CFrameVu1.NodeStride)).CopyTo(o, CCloth.ClothAttach);
                uint bounds = CloneBounds((uint)BitConverter.ToInt32(o, CCloth.ClothBounds) & Memory.PhysAddrMask, out int nb);
                BitConverter.GetBytes(bounds).CopyTo(o, CCloth.ClothBounds);                 // the cat's own body capsules
                ScaleClothToCat(o, wide, hang);
                BitConverter.GetBytes(0).CopyTo(o, 0x50);                                    // wind: the step refreshes it from the character
                Array.Copy(o, CCloth.ClothCur, o, CCloth.ClothPrev, CCloth.ClothArrayBytes);                                    // previous = current: a quiet first step
                Memory.WriteBytesBatch(cObj, o);
                Memory.WriteBytesBatch(cB0, Memory.ReadBytesBatch(Memory.ToMmu(b0), bufSize) ?? new byte[bufSize]);
                Memory.WriteBytesBatch(cB1, Memory.ReadBytesBatch(Memory.ToMmu(b0), bufSize) ?? new byte[bufSize]);
                byte[] list = new byte[16]; BitConverter.GetBytes(cObjG).CopyTo(list, 0);
                Memory.WriteBytesBatch(cList, list);
                Memory.WriteUInt(SlotAddr() + CCharacter.ClothList, cListG);
                if (i >= 0) Memory.WriteInt(Memory.ToMmu(herList) + i * 4, 0);                 // she neither steps nor draws the template
                _capeObj = cObj; _capeTemplate = obj; _capeWatch = 0; _capeWide = wide; _capeHang = hang;
                // The wind's taper watches ONE particle — the middle of the hem, the point that swings furthest from the shape the
                // cape is meant to hold. Its slot is (column × 0x100 + row × 0x10), and BOTH indices come from the cloth itself, so
                // re-tessellating the cape in the bake cannot leave the runtime watching some point up its middle.
                _capeHemParticle = (_capeWide / 2) * CCloth.ClothColumnStride + (_capeHang - 1) * CCloth.ClothParticleStride;
                _capeRest = Memory.ReadBytesBatch(cObj + CCloth.ClothRest, _capeWide * CCloth.ClothColumnStride);   // read ONCE: the wind rides on it
                if (_capeRest != null)                                                   // its own length, for the lift's geometry
                {
                    int mid = (_capeWide / 2) * CCloth.ClothColumnStride;
                    _capeSpan = Math.Abs(BitConverter.ToSingle(_capeRest, mid) - BitConverter.ToSingle(_capeRest, mid + (_capeHang - 1) * 0x10));
                }
                TintCape();
                string V(int off) => $"({BitConverter.ToSingle(o, off):F2},{BitConverter.ToSingle(o, off + 4):F2},{BitConverter.ToSingle(o, off + 8):F2})";
                Log($"cape physics: cat scale {CatScale:F2}, K {V(CCloth.ClothK)} gravity {V(CCloth.ClothGravity)} follow {V(CCloth.ClothFollow)} wind {BitConverter.ToSingle(o, CCloth.ClothWindScale):F2} normal {BitConverter.ToSingle(o, CCloth.ClothNormal):F2} floor {BitConverter.ToSingle(o, 0x4C):F1} (flag {BitConverter.ToInt32(o, 0x48)})");
                Log($"cape: {wide} wide × {hang} down cloth cloned from 0x{obj:X} → 0x{cObjG:X} (buffers 0x{bufSize:X} ×2 for a {packet} B packet, her gap was 0x{gap:X}), anchored to the copy's {CapeAnchorName} (n{anchor}), {nb} body capsule(s) on {string.Join("/", CapeBoundBones.Take(nb))}{(i >= 0 ? $"; her entry {i} cleared" : "")}");
            }
        }

        /// <summary>The index of a bone in the cat copy's node pool, or −1.</summary>
        private static int NodeIndexOf(string name)
        {
            for (int k = 0; k < _nodeCount; k++) if (ReadName((uint)(CodeCaves.NodePoolGuest + k * CFrameVu1.NodeStride)) == name) return k;
            return -1;
        }

        /// <summary>Clone the template cape's CBound chain into the cave, re-pointing capsule <i>i</i> at the cat copy's
        /// <see cref="CapeBoundBones"/>[i] (in the .clo's own order) so the cloth collides with the cat instead of her. Returns the
        /// guest pointer to the head of the new chain (0 = none), and how many capsules it holds.</summary>
        /// <summary>Size the cloth's world-unit terms to the cat. The sim's targets come through the anchor's matrix and so
        /// carry the cat's scale, but the StretchBind rest lengths (<see cref="CCloth.ClothTie"/>) were measured from the
        /// lattice at load in rig units and stay there — left alone, every tie is CatScale× too short for the sheet the
        /// spring is pulling toward, and the cape bunches toward the collar. The wind gain is a world velocity, sized with
        /// the sheet so the flutter reads the same. K, follow and the mod's own breeze are fractions or anchor-space: untouched.</summary>
        private static void ScaleClothToCat(byte[] o, int wide, int hang)
        {
            for (int a = 0; a < wide; a++)
                for (int b = 0; b < hang; b++)
                {
                    int t = CCloth.ClothTie + a * CCloth.ClothColumnStride + b * CCloth.ClothParticleStride;
                    BitConverter.GetBytes(BitConverter.ToSingle(o, t)     * CatScale).CopyTo(o, t);       // across
                    BitConverter.GetBytes(BitConverter.ToSingle(o, t + 4) * CatScale).CopyTo(o, t + 4);   // along the hang
                }
            BitConverter.GetBytes(BitConverter.ToSingle(o, CCloth.ClothWindScale) * CatScale).CopyTo(o, CCloth.ClothWindScale);
        }

        /// <summary>Clone the cape's body capsules onto the copy's own bones (<see cref="CapeBoundBones"/>), radii sized to the
        /// cat: the capsule's endpoints ride the bone's scaled matrix, its radii are world constants.</summary>
        private static uint CloneBounds(uint bnd, out int count)
        {
            uint head = 0; long prev = 0; count = 0;
            while (Memory.IsValidGuest(bnd) && count < CapeBoundBones.Length)
            {
                byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(bnd), CBound.BoundSize);
                if (b == null) break;
                uint next = (uint)BitConverter.ToInt32(b, CBound.BoundNext) & Memory.PhysAddrMask;
                int bone = NodeIndexOf(CapeBoundBones[count]);
                if (bone < 0) { Log("cape: no " + CapeBoundBones[count] + " in the copy — capsule skipped"); break; }
                long cB = TakeCave(CBound.BoundSize, out uint cBG);
                if (cB == 0) { Log("cape: no cave room for the body capsules"); break; }
                BitConverter.GetBytes((uint)(CodeCaves.NodePoolGuest + bone * CFrameVu1.NodeStride)).CopyTo(b, CBound.BoundFrameA);
                BitConverter.GetBytes(0).CopyTo(b, CBound.BoundFrameB);                       // A alone carries both endpoints
                BitConverter.GetBytes(0).CopyTo(b, CBound.BoundNext);                         // the chain is re-linked below
                for (int r = 0; r < 12; r += 4)
                {
                    float radius = BitConverter.ToSingle(b, CBound.BoundRadii + r) * CatScale;
                    BitConverter.GetBytes(radius).CopyTo(b, CBound.BoundRadii + r);
                    BitConverter.GetBytes(radius > 0f ? 1f / radius : 0f).CopyTo(b, CBound.BoundRadiiInv + r);
                }
                Memory.WriteBytesBatch(cB, b);
                if (prev != 0) Memory.WriteUInt(prev + CBound.BoundNext, cBG); else head = cBG;
                prev = cB; count++; bnd = next;
            }
            return head;
        }

        /// <summary>Diagnostics while the cape is up: is it being stepped (particle 0 leaves its rest-local spot for the world) and
        /// drawn (the active packet pointer flips between the two buffers)? Logged every ~1 s.</summary>
        internal static void WatchCape()
        {
            if (_capeObj == 0 || ++_capeWatch % 60 != 0) return;
            byte[] o = Memory.ReadBytesBatch(_capeObj, CCloth.ClothCur + _capeWide * CCloth.ClothColumnStride);   // through the last current position
            if (o == null) return;
            string P(int off) => $"({BitConverter.ToSingle(o, off):F2},{BitConverter.ToSingle(o, off + 4):F2},{BitConverter.ToSingle(o, off + 8):F2})";
            uint active = (uint)BitConverter.ToInt32(o, CCloth.ClothActive), anchor = (uint)BitConverter.ToInt32(o, CCloth.ClothAttach);
            long s = SlotAddr();
            int Rest(int a, int b) => CCloth.ClothRest + a * CCloth.ClothColumnStride + b * CCloth.ClothParticleStride;
            int Cur(int a, int b)  => CCloth.ClothCur  + a * CCloth.ClothColumnStride + b * CCloth.ClothParticleStride;
            int aEnd = _capeWide - 1, bEnd = _capeHang - 1;   // the lattice: a across the collar, b down the hang (0 = the collar edge, bEnd = the hem)
            Log($"cape watch: cur a0b0 {P(Cur(0, 0))} a0b1 {P(Cur(0, 1))} a0b{bEnd} {P(Cur(0, bEnd))} a{aEnd}b0 {P(Cur(aEnd, 0))} a{aEnd}b{bEnd} {P(Cur(aEnd, bEnd))} | rest a0b0 {P(Rest(0, 0))} a0b1 {P(Rest(0, 1))} a1b0 {P(Rest(1, 0))} | anchor-centroid {P(CCloth.ClothAnchorWorld)} local {P(CCloth.ClothAnchorLocal)} active 0x{active:X}");
            uint bnd = (uint)BitConverter.ToInt32(o, CCloth.ClothBounds) & Memory.PhysAddrMask;
            var caps = new List<string>();
            while (Memory.IsValidGuest(bnd) && caps.Count < 4)
            {
                byte[] b = Memory.ReadBytesBatch(Memory.ToMmu(bnd), CBound.BoundSize);
                if (b == null) break;
                caps.Add($"{ReadName((uint)BitConverter.ToInt32(b, CBound.BoundFrameA) & Memory.PhysAddrMask)} c({BitConverter.ToSingle(b, CBound.BoundCentre):F1},{BitConverter.ToSingle(b, CBound.BoundCentre + 4):F1},{BitConverter.ToSingle(b, CBound.BoundCentre + 8):F1}) r({BitConverter.ToSingle(b, CBound.BoundRadii):F1},{BitConverter.ToSingle(b, CBound.BoundRadii + 4):F1},{BitConverter.ToSingle(b, CBound.BoundRadii + 8):F1})");
                bnd = (uint)BitConverter.ToInt32(b, CBound.BoundNext) & Memory.PhysAddrMask;
            }
            Log("cape watch: capsules " + (caps.Count == 0 ? "none" : string.Join(" | ", caps)));
            // how far each particle is from the rest shape the engine is pulling it to (+0x7550 = LW(anchor) × rest, refreshed every step)
            byte[] tg = Memory.ReadBytesBatch(_capeObj + CCloth.ClothTarget, _capeHang * CCloth.ClothParticleStride);   // column 0
            if (tg != null)
            {
                float Sag(int b)
                {
                    int t = b * CCloth.ClothParticleStride, c = Cur(0, b);
                    float dx = BitConverter.ToSingle(tg, t)     - BitConverter.ToSingle(o, c);
                    float dy = BitConverter.ToSingle(tg, t + 4) - BitConverter.ToSingle(o, c + 4);
                    float dz = BitConverter.ToSingle(tg, t + 8) - BitConverter.ToSingle(o, c + 8);
                    return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }
                int hem = bEnd * CCloth.ClothParticleStride;
                Log($"cape watch: column 0 off the rest shape by b1 {Sag(1):F2} b3 {Sag(3):F2} b{bEnd} {Sag(bEnd):F2} | b{bEnd} target ({BitConverter.ToSingle(tg, hem):F1},{BitConverter.ToSingle(tg, hem + 4):F1},{BitConverter.ToSingle(tg, hem + 8):F1})");
            }
            byte[] lw = Memory.ReadBytesBatch(Memory.ToMmu(anchor) + CFrameVu1.WorldMatrix, 0x40);
            string rows = lw == null ? "?" : string.Join(" | ", new[] { 0, 1, 2, 3 }.Select(r => $"({BitConverter.ToSingle(lw, r * 16):F2},{BitConverter.ToSingle(lw, r * 16 + 4):F2},{BitConverter.ToSingle(lw, r * 16 + 8):F2},{BitConverter.ToSingle(lw, r * 16 + 12):F2})"));
            Log($"cape watch: anchor LW rows {rows} | cat at ({Memory.ReadFloat(s + CCharacter.CharPos):F1},{Memory.ReadFloat(s + CCharacter.CharPos + 4):F1},{Memory.ReadFloat(s + CCharacter.CharPos + 8):F1}) yaw {Memory.ReadFloat(s + CCharacter.CharRotY):F2} scale {Memory.ReadFloat(s + CCharacter.CharScale):F2} opacity {Memory.ReadFloat(s + CCharacter.NpcOpacity):F0}");
        }

        private const float CapeWindLift = 2.6f;         // how far the hem flies off the back
        private const float CapeWindEase = 1.6f;         // profile along the hang: > 1 keeps the shoulders down, flies the tail
        private const float CapeTearSpan = 20f;          // collar corner to mid-hem, per unit of cat scale; past this the cloth is torn and gets reseated
        private static int _capeTearLog;
        private static int  _capeHemParticle;                                // the hem's middle, from the cloth's own dimensions
        internal static byte[] _capeRest;                                     // the rest shape as baked — the wind's baseline
        private static float _capeSpan;                                      // collar to hem along the cape, measured from that shape
        private const float CapeRippleAmp = 0.65f;       // units of swell at the crest
        private const float CapeRippleReach = 0.5f;      // fraction down the cape where the swell reaches full
        private const float CapeRippleRows = 10.0f;      // rows per wavelength — one swell visible at a time
        private const float CapeRippleSeconds = 0.5f;    // one crest, collar to hem
        private static double _ripplePhase;
        private static int _capeWide, _capeHang;      // the cloth's own dimensions: across the back × down the cape
        /// <summary>The wind: the cloth's REST SHAPE is rewritten each tick as the blown shape — every row carried back along
        /// the cape and lifted off the back, eased along the hang, with a travelling ripple on top — rather than a force being
        /// applied, which a pinned sheet answers by buckling. Rows draw in as they rise so the sheet never has to lengthen.
        /// All in the anchor's frame (+x toward the collar, −y off the back); gravity stays zero. Reseats the cloth if it has
        /// been left behind by a teleport.</summary>
        internal static void BreezeCape()
        {
            if (_capeObj == 0 || _capeRest == null) return;
            if (_capeWide <= 0 || _capeWide > 16 || _capeHang <= 1 || _capeHang > 16) return;   // the engine's grid is 16 × 16
            byte[] cur = Memory.ReadBytesBatch(_capeObj + CCloth.ClothCur + _capeHemParticle, 12);
            byte[] pin = Memory.ReadBytesBatch(_capeObj + CCloth.ClothCur, 12);       // the pinned corner: the sheet's own anchor end
            if (cur != null && pin != null)
            {
                float sx = BitConverter.ToSingle(cur, 0) - BitConverter.ToSingle(pin, 0);
                float sy = BitConverter.ToSingle(cur, 4) - BitConverter.ToSingle(pin, 4);
                float sz = BitConverter.ToSingle(cur, 8) - BitConverter.ToSingle(pin, 8);
                float span = (float)Math.Sqrt(sx * sx + sy * sy + sz * sz);
                if (span > CapeTearSpan * CatScale || float.IsNaN(span))
                {
                    if (--_capeTearLog <= 0) { _capeTearLog = 60; Log($"cape: torn — {span:F0} units from collar to hem; reseating the cloth"); }
                    ReseedCape();
                    return;
                }
            }
            StiffenCape();
            _ripplePhase += 2 * Math.PI * (TickMs / 1000.0) / CapeRippleSeconds;
            byte[] rest = (byte[])_capeRest.Clone();
            for (int b = 1; b < _capeHang; b++)                                       // b = 0 is the pinned collar edge: leave it
            {
                float t = (float)b / (_capeHang - 1);
                float f = (float)Math.Pow(t, CapeWindEase);                            // nothing at the collar, everything at the hem
                float swell = CapeRippleAmp * Math.Min(1f, t / CapeRippleReach);
                float lift = CapeWindLift * f + swell * (float)Math.Sin(_ripplePhase - b * 2 * Math.PI / CapeRippleRows);
                float d = _capeSpan * t;                                               // how far down the cape this row sits
                float trim = d - (float)Math.Sqrt(Math.Max(0f, d * d - lift * lift));  // …and how much it must draw in to rise that far
                for (int a = 0; a < _capeWide; a++)
                {
                    int o = a * CCloth.ClothColumnStride + b * CCloth.ClothParticleStride;
                    BitConverter.GetBytes(BitConverter.ToSingle(_capeRest, o) + trim).CopyTo(rest, o);          // +x = back toward the collar
                    BitConverter.GetBytes(BitConverter.ToSingle(_capeRest, o + 4) - lift).CopyTo(rest, o + 4);  // −y = up off the back
                }
            }
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothRest, rest);
        }

        private const float CapeSpringSide = 0.42f;      // across the cape: holds the width, and with it the authored flare
        private const float CapeSpringAlong = 0.12f;     // along it: low, so the wind can lift the sheet and carry a wave down it
        private const float CapeSpringUp = 0.16f;        // vertical: between the two — it fights the lift, but also the sagging
        /// <summary>The cat's facing as a GUARANTEED unit vector — the cave leaves it (0, 0) with no target in range, and
        /// <see cref="StiffenCape"/> mixes by the squares of these, which only sums correctly when they are normalised.</summary>
        private static void Facing(out float dx, out float dz)
        {
            float l = (float)Math.Sqrt(_dirX * _dirX + _dirY * _dirY);
            if (l > 1e-3f) { dx = _dirX / l; dz = _dirY / l; } else { dx = 0f; dz = 1f; }
        }

        /// <summary>Rebuild the cloth's spring (CCloth +0xE0/+0xE4/+0xE8, a per-axis vector) from the cat's facing: stiff
        /// across the cape, slack fore-and-aft, mixed by the squares of the facing so the pair rotates through the diagonals.
        /// The engine's axes are WORLD axes, so a constant would only be right while the cat faced one way.</summary>
        private static void StiffenCape()
        {
            Facing(out float fdx, out float fdz);
            float fx = fdx * fdx, fz = fdz * fdz;                                      // normalised, so fx + fz = 1 always
            byte[] k = new byte[12];
            BitConverter.GetBytes(CapeSpringAlong * fx + CapeSpringSide * fz).CopyTo(k, 0);
            BitConverter.GetBytes(CapeSpringUp).CopyTo(k, 4);
            BitConverter.GetBytes(CapeSpringAlong * fz + CapeSpringSide * fx).CopyTo(k, 8);
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothK, k);
        }

        /// <summary>Put the whole cloth exactly on its rest shape at the cat's new place. Called when the copy teleports — the
        /// bind to the pellet's birth frame — where the anchor jumps the length of the room in one step.
        ///
        /// The engine's own guard (Step teleports the sheet when the anchor's centroid moves more than 10 units) does not cover
        /// this: the pinned collar is re-pinned to the anchor on every constraint pass, so the instant it snaps to the new
        /// position while the rest of the sheet is still at the old one, the cape is stretched and the constraints tear it
        /// apart. So place every particle here — current AND previous = LW(anchor) × rest, velocities zeroed, and the mark the
        /// teleport test compares against moved to match: the state Clear__6CCloth builds.</summary>
        internal static void ReseedCape()
        {
            if (_capeObj == 0 || _capeRest == null) return;
            uint anchor = Memory.ReadGuestPtr(_capeObj + CCloth.ClothAttach);
            byte[] lw = Memory.IsValidGuest(anchor) ? Memory.ReadBytesBatch(Memory.ToMmu(anchor) + CFrameVu1.WorldMatrix, 0x40) : null;
            if (lw == null) { Log("cape: cannot reseed — the anchor's matrix did not read"); return; }
            float[] m = new float[16];
            for (int i = 0; i < 16; i++) m[i] = BitConverter.ToSingle(lw, i * 4);
            // row-vector convention, as everywhere in this engine: world = x·row0 + y·row1 + z·row2 + row3
            void Place(byte[] dst, int o, float x, float y, float z)
            {
                BitConverter.GetBytes(x * m[0] + y * m[4] + z * m[8] + m[12]).CopyTo(dst, o);
                BitConverter.GetBytes(x * m[1] + y * m[5] + z * m[9] + m[13]).CopyTo(dst, o + 4);
                BitConverter.GetBytes(x * m[2] + y * m[6] + z * m[10] + m[14]).CopyTo(dst, o + 8);
                BitConverter.GetBytes(1f).CopyTo(dst, o + 12);
            }
            byte[] cur = new byte[_capeWide * CCloth.ClothColumnStride];
            for (int a = 0; a < _capeWide; a++)
                for (int b = 0; b < _capeHang; b++)
                {
                    int o = a * CCloth.ClothColumnStride + b * CCloth.ClothParticleStride;
                    Place(cur, o, BitConverter.ToSingle(_capeRest, o), BitConverter.ToSingle(_capeRest, o + 4), BitConverter.ToSingle(_capeRest, o + 8));
                }
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothCur, cur);
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothPrev, cur);                  // no history: nothing to whip back toward
            Memory.WriteBytesBatch(_capeObj + CCloth.ClothVel, new byte[_capeWide * CCloth.ClothColumnStride]);
            byte[] c = Memory.ReadBytesBatch(_capeObj + CCloth.ClothAnchorLocal, 12);  // and the mark the teleport test compares to
            if (c != null)
            {
                byte[] w = new byte[12];
                float lx = BitConverter.ToSingle(c, 0), ly = BitConverter.ToSingle(c, 4), lz = BitConverter.ToSingle(c, 8);
                BitConverter.GetBytes(lx * m[0] + ly * m[4] + lz * m[8] + m[12]).CopyTo(w, 0);
                BitConverter.GetBytes(lx * m[1] + ly * m[5] + lz * m[9] + m[13]).CopyTo(w, 4);
                BitConverter.GetBytes(lx * m[2] + ly * m[6] + lz * m[10] + m[14]).CopyTo(w, 8);
                Memory.WriteBytesBatch(_capeObj + CCloth.ClothAnchorWorld, w);
            }
        }
    }
}
