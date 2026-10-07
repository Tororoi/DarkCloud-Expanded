using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb Gemron (EnemySpecies.BombGemron) in play. The species is the disc's: its record (SpeciesRows), model, script and
    /// name (ModSpeciesBakes), its thrown bomb and its blast item-bomb shots (ElfSpeciesPatches.PatchBombConfigs). Its fuse spark is the
    /// machine-gun hit flash in one of the pool's slots 12–15, run by the flash's own draw once a frame (tools/stubs/flash_slot.s): kept
    /// alight, its three cells in turn 4 frames each, a guard-spark burst asked for every 15 frames, put out the frame the death motion
    /// (11) passes its blast (122) or the self-destruct motion (14: the guard's return reversed, then its loop) its final pose (217), and placed where the monster draw captured this unit's wick this frame (tools/stubs/fuse_capture.s —
    /// every Gemron shares one frame tree, posed for each unit in turn, so only the draw's own moment has this unit's pose). Every
    /// timer is the game's; this only
    ///  · pins a Gemron to a flash slot (CodeCaves.FlashPinTable: its body bone and model block; the slot's size 3 and full alpha in
    ///    CodeCaves.FlashSizeTable / FlashAlphaTable) and unpins it — back to Osmond's 5.0 and 0x80 — when it is gone;
    ///  · writes the fuse point: the wick's tip, and during the death motion or the self-destruct's guard loop the point the frame has reached along the wick
    ///    (<see cref="Fuse"/>, in the body bone's frame) — a function of the engine's frame, not of time;
    ///  · gives the big bomb's visual its private vtable, so it reddens as the fuse burns (<see cref="ArmTint"/>);
    ///  · answers each burst the draw asks for with the guard spark (CheckDmg's burst, written whole into the hit-mark pool's last
    ///    entry: 20 marks at twice the size, scatter 0.75), each pin into its own hit-mark entry, at the point the draw placed the flash
    ///    this frame, and puts that burst out when the Gemron is spent.</summary>
    internal static class BombGemron
    {
        private const string Tag = "[BombGemron] ";
        private const int DeathMotion = 11, DeathLoopMotion = 12;
        private const float FuseStart = 105f, BlastFrame = 122f;                       // the death motion's fuse
        private const int SelfDestructMotion = ModSpeciesBakes.SelfDestructMotion;
        private const float SelfDestructFuseStart = ModSpeciesBakes.SelfDestructFuseStart, SelfDestructBlast = ModSpeciesBakes.SelfDestructEnd - 1;   // _CHK_MOTION_FRM's last frame

        /// <summary>The fuse window of a last motion (the death, or the self-destruct), or null for any other.</summary>
        private static (float start, float blast)? FuseOf(int motion) =>
            motion == DeathMotion ? (FuseStart, BlastFrame) : motion == SelfDestructMotion ? (SelfDestructFuseStart, SelfDestructBlast) : ((float, float)?)null;
        private const string BodyBone = "tama1__m";                                    // the big bomb's bone
        private const float FlashSize = 3f;                                            // the flash's size (Osmond's stay 5.0)
        private const byte FlashAlpha = 0x80;                                          // its alpha in the additive pass: full, as Osmond's
        private const int FlashAlight = 17;                                            // a live timer for the first draw (the draw keeps it)
        private const int BurstMarks = 20;
        private const float BurstSpread = 0.75f, BurstSize = 2f, BurstShrink = 0.005f, BurstGravity = 0.02f, BurstSpeed = 1.3f;   // CheckDmg's guard burst, scatter 0.75, every mark (and its shrink) × 2
        private const float BurstFloorDrop = 6f;                                       // the marks bounce this far below the wick
        /// <summary>The big bomb's wick centreline, tip first, in the body bone's frame: the bake's bomb at 3.5 / 1.22 scale, aimed, spun
        /// and turned as ModSpeciesBakes places it (BodySpin, BodyPitch/Roll/Yaw). Must follow any change to those.</summary>
        private static readonly float[][] Fuse =
        {
            new[] { -4.502f, 0.4241f, 0.163f }, new[] { -4.7109f, 0.4496f, 0.1215f }, new[] { -4.7651f, 0.3937f, 0.651f },
            new[] { -4.9872f, 0.3606f, 1.1262f }, new[] { -5.2539f, 0.3325f, 1.5962f }, new[] { -5.3204f, 0.2763f, 2.1387f },
            new[] { -5.0037f, 0.194f, 2.5808f }, new[] { -4.4755f, 0.1267f, 2.7136f }, new[] { -3.9403f, 0.0833f, 2.633f },
            new[] { -3.4238f, 0.0494f, 2.4863f },
        };
        private const int Pins = 4;                                                      // flash slots 12–15: the four Gemrons nearest the player
        private const float KeepMargin = 15f;                                           // how much nearer an unlit Gemron must be to take a lit one's slot

        private static readonly int[] _pinOf = new int[EnemyAddresses.FloorSlots.Count];   // the unit's pin entry + 1 (0 = none)
        private static readonly int[] _unitOf = new int[Pins];                              // the pin's unit + 1
        private static readonly uint[] _bursts = new uint[Pins];                            // the bursts answered
        private static readonly bool[] _blown = new bool[EnemyAddresses.FloorSlots.Count]; // its blast has gone: no more sparks from it
        private static uint _tintVisual;                                                    // the bomb visual last armed (guest), 0 = none
        private static readonly bool[] _sawDeath = new bool[EnemyAddresses.FloorSlots.Count]; // its death motion has been seen playing
        private static readonly float[,] _burstAt = new float[Pins, 3];                    // where each pin's last burst went off
        private static readonly Random _rng = new();

        /// <summary>Once a dungeon tick; <paramref name="active"/> false while a load is on (the floor's units and pools are rebuilt), which
        /// lets every slot go. Menus, the character change and the player's knockdowns keep them: this only writes data the draw reads.</summary>
        internal static void Tick(bool active)
        {
            if (!active) { Release(); return; }
            // Every live Gemron checked for its end first; then the four nearest the player hold the flash slots — a lit one keeps its
            // slot unless an unlit one is nearer by more than KeepMargin (so two at a similar range do not trade it back and forth).
            float px = Memory.ReadFloat(PlayerAddresses.DunPositionX), py = Memory.ReadFloat(PlayerAddresses.DunPositionY);
            var live = new System.Collections.Generic.List<(int unit, float score)>();
            bool tinted = false;
            for (int unit = 0; unit < EnemyAddresses.FloorSlots.Count; unit++)
            {
                if (!IsBombGemron(unit)) { _blown[unit] = false; _sawDeath[unit] = false; Unpin(unit); continue; }
                if (!tinted) { ArmTint(unit); tinted = true; }                         // the species' one bomb visual
                if (_blown[unit] || Spent(unit)) continue;
                float dx = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(unit, EnemySlotOffsets.LocationX)) - px;
                float dy = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(unit, EnemySlotOffsets.LocationY)) - py;
                live.Add((unit, MathF.Sqrt(dx * dx + dy * dy) - (_pinOf[unit] > 0 ? KeepMargin : 0f)));
            }
            live.Sort((a, b) => a.score.CompareTo(b.score));
            for (int k = Pins; k < live.Count; k++) Unpin(live[k].unit);              // the far ones give their slots up first
            for (int k = 0; k < Math.Min(Pins, live.Count); k++) Drive(live[k].unit);
        }

        /// <summary>The big bomb's visual — the tama1__m frame's, one object every Bomb Gemron on the floor draws — given a private copy
        /// of its class vtable (CodeCaves.BombTintVtable) whose two DrawVu1 slots enter tools/stubs/bomb_tint.s (DeadChainCave.BombTint),
        /// the stock targets in CodeCaves.BombTintStock: the cave reddens the ambient for that one draw by the drawn unit's fuse, from the
        /// engine's own frame. Data writes only, the vtable pointer last; a new floor's visual is armed afresh.</summary>
        private static void ArmTint(int unit)
        {
            if (_tintVisual != 0 && Memory.ReadGuestPtr(Memory.ToMmu(_tintVisual) + CVisualMDT.VisVtable) == CodeCaves.BombTintVtableGuest) return;
            long chara = Model(unit) - CCharacter.CharScale;
            uint root = Memory.ReadGuestPtr(chara + CCharacter.CharModel);
            uint bone = Memory.IsValidGuest(root) ? BombCarrier.NodeNamed(root, BodyBone) : 0;
            if (bone == 0) return;
            uint vis = Memory.ReadGuestPtr(Memory.ToMmu(bone) + CFrameVu1.GeomPtr);
            if (!Memory.IsValidGuest(vis)) return;
            long visM = Memory.ToMmu(vis);
            uint vt = Memory.ReadGuestPtr(visM + CVisualMDT.VisVtable);
            if (vt != CodeCaves.BombTintVtableGuest)
            {
                if (vt != CVisualMDT.Vu1Vtable && vt != CVisualMDT.RigidVtable)
                {
                    if (_tintVisual != vis) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"the bomb visual 0x{vis:X} has vtable 0x{vt:X}, neither CVisualMDTVu1 nor CVisualVu1 — no fuse tint");
                    _tintVisual = vis; return;
                }
                byte[] tbl = Memory.ReadBytesBatch(Memory.ToMmu(vt), CVisualMDT.Vu1VtableBytes);
                if (tbl == null) return;
                Memory.WriteBytesBatch(CodeCaves.BombTintStock, tbl.AsSpan(CVisualMDT.Vu1VtableDrawSlot, 8).ToArray());   // slot 6, slot 7
                BitConverter.GetBytes(DeadChainCave.BombTint).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot);
                BitConverter.GetBytes(DeadChainCave.BombTint + 0x0Cu).CopyTo(tbl, CVisualMDT.Vu1VtableDrawSlot + 4);
                Memory.WriteBytesBatch(CodeCaves.BombTintVtable, tbl);
                Memory.WriteUInt(visM + CVisualMDT.VisVtable, CodeCaves.BombTintVtableGuest);   // last: the next draw takes it
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"bomb visual 0x{vis:X} (class vtable 0x{vt:X}) draws through bomb_tint (0x{DeadChainCave.BombTint:X})");
            }
            _tintVisual = vis;
        }

        private static bool IsBombGemron(int unit)
        {
            long a = EnemyAddresses.FloorSlots.SlotAddr(unit, 0);
            return Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) > 0 && Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId) == EnemySpecies.BombGemron.Id;
        }

        private static long Model(int unit) => ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride;
        private static long PinEntry(int pin) => CodeCaves.FlashPinTable + (long)pin * CodeCaves.FlashPinStride;
        private static long Capture(int pin) => CodeCaves.FlashCapture + (long)pin * CodeCaves.FlashPinStride;
        /// <summary>The pin's own hit-mark burst entry (12–15, beside its flash slot); CheckDmg takes the entries in turn, so a hit can reuse it.</summary>
        private static long BurstEntry(int pin) => PlayerAction.HitMarkBurst + (long)(FlashSlotOf(pin)) * PlayerAction.HitMarkBurstStride;
        private static int FlashSlotOf(int pin) => CodeCaves.FlashPinFirstSlot + pin;
        private static long FlashPos(int pin) => PlayerAction.MachineGunFlash + (long)FlashSlotOf(pin) * 16;
        private static long FlashTimer(int pin) => PlayerAction.MachineGunFlash + PlayerAction.MachineGunFlashTimer + (long)FlashSlotOf(pin) * 4;

        /// <summary>Whether the unit's fuse is done (it is then marked blown, its slot and last burst put out).</summary>
        private static bool Spent(int unit)
        {
            long model = Model(unit);
            int motion = Memory.ReadInt(model + ModelScaleOffsets.PlayingMotionId);
            var fuse = FuseOf(motion);
            float frame = fuse != null ? Memory.ReadFloat(model + ModelScaleOffsets.PlayingMotionFrame) : 0f;
            int pin = _pinOf[unit] - 1;
            // Spent, from what the engine holds (so a blast this missed — paused under a knockdown, a menu — still counts). Not its HP:
            // that is gone when the death motion starts, and the fuse burns down that motion to the blast at 122. The death and the
            // self-destruct motions are the Gemron's last (its script plays 11 only to die, 14 only to blow itself up), so once one
            // is seen, leaving it is the end too.
            if (fuse != null) _sawDeath[unit] = true;
            string spent =
                pin >= 0 && Memory.ReadUInt(PinEntry(pin) + CodeCaves.FlashPinFrame) == 0 ? "the draw put it out at the blast"
                : fuse != null && frame >= fuse.Value.blast ? $"motion {motion} is at frame {frame:0.#}"
                : _sawDeath[unit] && fuse == null ? $"its last motion has ended (now motion {motion})"
                : motion == DeathLoopMotion ? "the death loop plays"
                : Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(unit, EnemySlotOffsets.OpacityFadeStep)) > 0f ? "its fade-out has begun"
                : null;
            if (spent == null) return false;
            Blown(unit, pin, spent);
            return true;
        }

        /// <summary>A unit among the nearest: lit if it is not, its fuse point written, its burst requests answered.</summary>
        private static void Drive(int unit)
        {
            long model = Model(unit);
            var fuse = FuseOf(Memory.ReadInt(model + ModelScaleOffsets.PlayingMotionId));
            float frame = fuse != null ? Memory.ReadFloat(model + ModelScaleOffsets.PlayingMotionFrame) : 0f;
            int pin = _pinOf[unit] - 1;
            float t = fuse is (float lo, float hi) && frame >= lo ? Math.Min(1f, (frame - lo) / (hi - lo)) : 0f;
            float s = t * (Fuse.Length - 1); int i = Math.Min(Fuse.Length - 2, (int)s); float f = s - i;
            float lx = Fuse[i][0] + (Fuse[i + 1][0] - Fuse[i][0]) * f, ly = Fuse[i][1] + (Fuse[i + 1][1] - Fuse[i][1]) * f, lz = Fuse[i][2] + (Fuse[i + 1][2] - Fuse[i][2]) * f;
            if (pin < 0) { Pin(unit, lx, ly, lz); return; }
            long e = PinEntry(pin);
            if (Memory.ReadInt(FlashTimer(pin)) < 0) Memory.WriteInt(FlashTimer(pin), FlashAlight);   // the engine cleared the pool (its own reset): alight again
            var p = new byte[12];
            BitConverter.GetBytes(lx).CopyTo(p, 0); BitConverter.GetBytes(ly).CopyTo(p, 4); BitConverter.GetBytes(lz).CopyTo(p, 8);
            Memory.WriteBytesBatch(e + CodeCaves.FlashPinPoint, p);
            uint asked = Memory.ReadUInt(e + CodeCaves.FlashPinBursts);
            if (asked != _bursts[pin])
            {
                _bursts[pin] = asked;
                long pos = FlashPos(pin);
                float x = Memory.ReadFloat(pos), h = Memory.ReadFloat(pos + 4), y = Memory.ReadFloat(pos + 8);
                if (!float.IsNaN(x + h + y) && !(x == 0f && h == 0f && y == 0f)) Burst(pin, x, h, y, h - BurstFloorDrop);
            }
        }

        /// <summary>The unit pinned to a free flash slot at its body bone, the slot drawn at the Gemron's size and alpha.</summary>
        private static void Pin(int unit, float lx, float ly, float lz)
        {
            long chara = Model(unit) - CCharacter.CharScale;                           // the model table sits at CCharacter + 0x90
            uint root = Memory.ReadGuestPtr(chara + CCharacter.CharModel);
            uint bone = Memory.IsValidGuest(root) ? BombCarrier.NodeNamed(root, BodyBone) : 0;
            if (bone == 0) return;
            int pin = Array.IndexOf(_unitOf, 0);
            if (pin < 0) return;                                                       // four lit (the far one gives its slot up first)
            var e = new byte[CodeCaves.FlashPinStride];
            BitConverter.GetBytes(bone).CopyTo(e, CodeCaves.FlashPinFrame);
            BitConverter.GetBytes((uint)(Model(unit) - 0x20000000L)).CopyTo(e, CodeCaves.FlashPinModel);
            BitConverter.GetBytes(lx).CopyTo(e, CodeCaves.FlashPinPoint); BitConverter.GetBytes(ly).CopyTo(e, CodeCaves.FlashPinPoint + 4); BitConverter.GetBytes(lz).CopyTo(e, CodeCaves.FlashPinPoint + 8);
            int slot = FlashSlotOf(pin);
            Memory.WriteFloat(CodeCaves.FlashSizeTable + slot * 4, FlashSize);
            Memory.WriteByte(CodeCaves.FlashAlphaTable + slot, FlashAlpha);
            Memory.WriteBytesBatch(Capture(pin), new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF });   // no capture yet: unseen until the monster draw takes one
            Memory.WriteBytesBatch(PinEntry(pin), e);
            Memory.WriteInt(FlashTimer(pin), FlashAlight);                             // last: the draw takes it from here
            _pinOf[unit] = pin + 1; _unitOf[pin] = unit + 1; _bursts[pin] = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"unit {unit}'s fuse lit (flash slot {slot})");
        }

        private static void Blown(int unit, int pin, string why)
        {
            _blown[unit] = true;
            if (pin >= 0) Unpin(unit);                                                 // (its burst put out with it)
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"unit {unit}'s fuse spent: {why}{(pin >= 0 ? $" (flash slot {FlashSlotOf(pin)} out)" : " (it was not lit)")}");
        }

        /// <summary>The unit's flash slot let go: its burst and flash out, unpinned, Osmond's size and alpha back.</summary>
        private static void Unpin(int unit)
        {
            int pin = _pinOf[unit] - 1;
            if (pin < 0) return;
            EndBurst(pin);
            int slot = FlashSlotOf(pin);
            Memory.WriteBytesBatch(PinEntry(pin), new byte[CodeCaves.FlashPinStride]);
            Memory.WriteInt(FlashTimer(pin), -1);
            Memory.WriteFloat(CodeCaves.FlashSizeTable + slot * 4, CodeCaves.FlashSizeVanilla);
            Memory.WriteByte(CodeCaves.FlashAlphaTable + slot, CodeCaves.FlashAlphaVanilla);
            _pinOf[unit] = 0; _unitOf[pin] = 0;
        }

        /// <summary>CheckDmg's guard burst (kind 2) written whole into the pin's own burst entry: every mark a little off the point,
        /// scattered about no direction (the engine's own burst uses one axis of its direction for all three, so a guard spark is a pure
        /// scatter too), a random size, the parameters CheckDmg passes — the marks and their shrink at <see cref="BurstSize"/>.</summary>
        private static void Burst(int pin, float x, float h, float y, float floorY)
        {
            long at = BurstEntry(pin);
            byte[] e = Memory.ReadBytesBatch(at, PlayerAction.HitMarkBurstStride);
            if (e == null) return;                                                     // (the object's own header — vtable, mass … — kept)
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            void I(int o, int v) => BitConverter.GetBytes(v).CopyTo(e, o);
            F(PlayerAction.HitMarkBurstPos, x); F(PlayerAction.HitMarkBurstPos + 4, h); F(PlayerAction.HitMarkBurstPos + 8, y); F(PlayerAction.HitMarkBurstPos + 12, 1f);
            F(PlayerAction.HitMarkBurstShrink, BurstShrink * BurstSize); F(PlayerAction.HitMarkBurstSpread, BurstSpread);
            F(PlayerAction.HitMarkBurstGravity, BurstGravity); F(PlayerAction.HitMarkBurstSpeed, BurstSpeed); F(PlayerAction.HitMarkBurstFloor, floorY);
            for (int i = 0; i < PlayerAction.HitMarkBurstMarks; i++)
            {
                int o = PlayerAction.HitMarkBurstOffset + i * 16, v = PlayerAction.HitMarkBurstVelocity + i * 16;
                F(o, R() - 0.5f); F(o + 4, R() - 0.5f); F(o + 8, R() - 0.5f); F(o + 12, 1f);
                F(v, BurstSpread * R() - BurstSpread / 2f); F(v + 4, BurstSpread * R() - BurstSpread / 2f); F(v + 8, BurstSpread * R() - BurstSpread / 2f); F(v + 12, 1f);
                F(PlayerAction.HitMarkBurstSize + i * 4, (0.1f + 1.2f * R()) * BurstSize);
                I(PlayerAction.HitMarkBurstUsed + i * 4, i < BurstMarks ? 1 : 0);
            }
            I(PlayerAction.HitMarkBurstKind, PlayerAction.HitMarkGuard);
            I(PlayerAction.HitMarkBurstCount, BurstMarks);
            Memory.WriteBytesBatch(at, e);
            _burstAt[pin, 0] = x; _burstAt[pin, 1] = h; _burstAt[pin, 2] = y;
        }
        private static float R() => (float)_rng.NextDouble();

        /// <summary>The pin's burst put out (its marks stop drawing) when its entry holds a guard burst — ours, unless a guarded hit took
        /// the entry since (CheckDmg takes the entries in turn); a hit's own burst there is left alone.</summary>
        private static void EndBurst(int pin)
        {
            long at = BurstEntry(pin);
            int kind = Memory.ReadInt(at + PlayerAction.HitMarkBurstKind), count = Memory.ReadInt(at + PlayerAction.HitMarkBurstCount);
            bool ours = kind == PlayerAction.HitMarkGuard;
            if (ours && count > 0) Memory.WriteInt(at + PlayerAction.HitMarkBurstCount, 0);
            if (count <= 0) return;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"burst entry {FlashSlotOf(pin)}: {(ours ? $"guard burst put out ({count} marks)" : $"taken by a hit (kind {kind}), left")}"
                + $" — last burst at ({_burstAt[pin, 0]:0.#},{_burstAt[pin, 2]:0.#}), entry at ({Memory.ReadFloat(at + PlayerAction.HitMarkBurstPos):0.#},{Memory.ReadFloat(at + PlayerAction.HitMarkBurstPos + 8):0.#})");
        }

        /// <summary>Every flash slot let go (a menu, a character change, a load: the trees and pools are rebuilt).</summary>
        private static void Release()
        {
            for (int unit = 0; unit < EnemyAddresses.FloorSlots.Count; unit++) Unpin(unit);
        }

        /// <summary>A new floor: the slots are new enemies.</summary>
        internal static void Reset()
        {
            Release();
            Array.Clear(_blown, 0, _blown.Length);
            Array.Clear(_sawDeath, 0, _sawDeath.Length);
            _tintVisual = 0;
        }
    }
}
