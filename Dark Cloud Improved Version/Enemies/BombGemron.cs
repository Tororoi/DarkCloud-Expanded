using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Bomb Gemron (EnemySpecies.BombGemron) in play. The species is the disc's: its record (SpeciesRows), model, script and
    /// name (ModSpeciesBakes), its thrown bomb and its blast item-bomb shots (ElfSpeciesPatches.PatchBombConfigs). Its fuse spark is the
    /// machine-gun hit flash in one of the pool's slots 12–15, run by the flash's own draw once a frame (tools/stubs/flash_slot.s): kept
    /// alight, its three cells in turn 4 frames each, placed through the body bone's world matrix at the fuse point, a guard-spark burst
    /// asked for every 15 frames, and put out the frame the death motion (11) passes its blast (122). Every timer is the game's; this
    /// only
    ///  · pins a Gemron to a flash slot (CodeCaves.FlashPinTable: its body bone and model block; the slot's size 3 and alpha 0x30 in
    ///    CodeCaves.FlashSizeTable / FlashAlphaTable) and unpins it — back to Osmond's 5.0 and 0x80 — when it is gone;
    ///  · writes the fuse point: the wick's tip, and during the death motion the point the motion's frame has reached along the wick
    ///    (<see cref="Fuse"/>, in the body bone's frame) — a function of the engine's frame, not of time;
    ///  · answers each burst the draw asks for with the guard spark (CheckDmg's burst, written whole into the hit-mark pool's last
    ///    entry: 20 marks at twice the size, scatter 0.75) at the point the draw placed the flash this frame, and puts it out when the
    ///    draw reports the blast.</summary>
    internal static class BombGemron
    {
        private const string Tag = "[BombGemron] ";
        private const int DeathMotion = 11, DeathLoopMotion = 12;
        private const float FuseStart = 105f, BlastFrame = 122f;
        private const string BodyBone = "tama1__m";                                    // the big bomb's bone
        private const float FlashSize = 3f;                                            // the flash's size (Osmond's stay 5.0)
        private const byte FlashAlpha = 0x30;                                          // its alpha in the additive pass, ≈ 38 % (Osmond's 0x80)
        private const int FlashAlight = 17;                                            // a live timer for the first draw (the draw keeps it)
        private const int BurstMarks = 20;
        private const float BurstSpread = 0.75f, BurstSize = 2f, BurstShrink = 0.005f, BurstGravity = 0.02f, BurstSpeed = 1.3f;   // CheckDmg's guard burst, scatter 0.75, every mark (and its shrink) × 2
        private const float BurstFloorDrop = 6f;                                       // the marks bounce this far below the wick
        /// <summary>The big bomb's wick centreline, tip first, in the body bone's frame (the bake's bomb at 3.5 / 1.22 scale, turned as
        /// the preview page shows it; game_data's albino_gemron.py prints it).</summary>
        private static readonly float[][] Fuse =
        {
            new[] { -1.8859f, -4.0466f, 0.7365f }, new[] { -1.9521f, -4.2506f, 0.7291f }, new[] { -2.2054f, -4.1255f, 1.1838f },
            new[] { -2.5026f, -4.1713f, 1.6148f }, new[] { -2.8156f, -4.2594f, 2.0474f }, new[] { -3.0795f, -4.1413f, 2.5146f },
            new[] { -3.1451f, -3.7073f, 2.846f },  new[] { -2.9902f, -3.1825f, 2.888f },  new[] { -2.7392f, -2.7216f, 2.7487f },
            new[] { -2.4669f, -2.2995f, 2.556f },
        };
        private const int Pins = 4;

        private static readonly int[] _pinOf = new int[EnemyAddresses.FloorSlots.Count];   // the unit's pin entry + 1 (0 = none)
        private static readonly int[] _unitOf = new int[Pins];                              // the pin's unit + 1
        private static readonly uint[] _bursts = new uint[Pins];                            // the bursts answered
        private static readonly bool[] _blown = new bool[EnemyAddresses.FloorSlots.Count]; // its blast has gone: no more sparks from it
        private static readonly float[] _burstAt = new float[3];                           // where our last burst went off
        private static readonly Random _rng = new();

        /// <summary>Once a dungeon tick; <paramref name="active"/> only in the walking mode with nothing open and no load on (the trees
        /// and pools are rebuilt under a menu, the character change and a floor load).</summary>
        internal static void Tick(bool active)
        {
            if (!active) { Release(); return; }
            for (int unit = 0; unit < EnemyAddresses.FloorSlots.Count; unit++)
            {
                if (!IsBombGemron(unit)) { _blown[unit] = false; Unpin(unit); continue; }
                if (_blown[unit]) continue;
                Drive(unit);
            }
        }

        private static bool IsBombGemron(int unit)
        {
            long a = EnemyAddresses.FloorSlots.SlotAddr(unit, 0);
            return Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) > 0 && Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId) == EnemySpecies.BombGemron.Id;
        }

        private static long Model(int unit) => ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride;
        private static long PinEntry(int pin) => CodeCaves.FlashPinTable + (long)pin * CodeCaves.FlashPinStride;
        private static int FlashSlotOf(int pin) => CodeCaves.FlashPinFirstSlot + pin;
        private static long FlashPos(int pin) => PlayerAction.MachineGunFlash + (long)FlashSlotOf(pin) * 16;
        private static long FlashTimer(int pin) => PlayerAction.MachineGunFlash + PlayerAction.MachineGunFlashTimer + (long)FlashSlotOf(pin) * 4;

        private static void Drive(int unit)
        {
            long model = Model(unit);
            int motion = Memory.ReadInt(model + ModelScaleOffsets.PlayingMotionId);
            bool dying = motion == DeathMotion;
            float frame = dying ? Memory.ReadFloat(model + ModelScaleOffsets.PlayingMotionFrame) : 0f;
            int pin = _pinOf[unit] - 1;
            // Spent, from what the engine holds (so a blast this missed — paused under a knockdown, a menu — still counts): the draw
            // put it out, its death motion is past the blast, the death loop plays, or the fade-out has begun.
            bool spent = (pin >= 0 && Memory.ReadUInt(PinEntry(pin) + CodeCaves.FlashPinFrame) == 0)
                         || (dying && frame >= BlastFrame) || motion == DeathLoopMotion
                         || Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(unit, EnemySlotOffsets.OpacityFadeStep)) > 0f;
            if (spent) { Blown(unit, pin); return; }
            float t = dying && frame >= FuseStart ? (frame - FuseStart) / (BlastFrame - FuseStart) : 0f;
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
                if (!float.IsNaN(x + h + y) && !(x == 0f && h == 0f && y == 0f)) Burst(x, h, y, h - BurstFloorDrop);
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
            if (pin < 0) return;                                                       // four Gemrons already lit
            var e = new byte[CodeCaves.FlashPinStride];
            BitConverter.GetBytes(bone).CopyTo(e, CodeCaves.FlashPinFrame);
            BitConverter.GetBytes((uint)(Model(unit) - 0x20000000L)).CopyTo(e, CodeCaves.FlashPinModel);
            BitConverter.GetBytes(lx).CopyTo(e, CodeCaves.FlashPinPoint); BitConverter.GetBytes(ly).CopyTo(e, CodeCaves.FlashPinPoint + 4); BitConverter.GetBytes(lz).CopyTo(e, CodeCaves.FlashPinPoint + 8);
            int slot = FlashSlotOf(pin);
            Memory.WriteFloat(CodeCaves.FlashSizeTable + slot * 4, FlashSize);
            Memory.WriteByte(CodeCaves.FlashAlphaTable + slot, FlashAlpha);
            Memory.WriteBytesBatch(PinEntry(pin), e);
            Memory.WriteInt(FlashTimer(pin), FlashAlight);                             // last: the draw takes it from here
            _pinOf[unit] = pin + 1; _unitOf[pin] = unit + 1; _bursts[pin] = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"unit {unit}'s fuse lit (flash slot {slot})");
        }

        private static void Blown(int unit, int pin)
        {
            _blown[unit] = true;
            EndBurst();
            if (pin >= 0) Unpin(unit);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"unit {unit}'s fuse spent");
        }

        /// <summary>The unit's flash slot let go: out, unpinned, Osmond's size and alpha back.</summary>
        private static void Unpin(int unit)
        {
            int pin = _pinOf[unit] - 1;
            if (pin < 0) return;
            int slot = FlashSlotOf(pin);
            Memory.WriteBytesBatch(PinEntry(pin), new byte[CodeCaves.FlashPinStride]);
            Memory.WriteInt(FlashTimer(pin), -1);
            Memory.WriteFloat(CodeCaves.FlashSizeTable + slot * 4, CodeCaves.FlashSizeVanilla);
            Memory.WriteByte(CodeCaves.FlashAlphaTable + slot, CodeCaves.FlashAlphaVanilla);
            _pinOf[unit] = 0; _unitOf[pin] = 0;
        }

        /// <summary>CheckDmg's guard burst (kind 2) written whole into the burst pool's last entry: every mark a little off the point,
        /// scattered about no direction (the engine's own burst uses one axis of its direction for all three, so a guard spark is a pure
        /// scatter too), a random size, the parameters CheckDmg passes — the marks and their shrink at <see cref="BurstSize"/>.</summary>
        private static void Burst(float x, float h, float y, float floorY)
        {
            long at = PlayerAction.HitMarkBurst + (long)(PlayerAction.HitPointMarkCount - 1) * PlayerAction.HitMarkBurstStride;
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
            _burstAt[0] = x; _burstAt[1] = h; _burstAt[2] = y;
        }
        private static float R() => (float)_rng.NextDouble();

        /// <summary>Our last burst put out (its marks stop drawing), if the pool's entry is still ours (CheckDmg takes the entries in turn).</summary>
        private static void EndBurst()
        {
            long at = PlayerAction.HitMarkBurst + (long)(PlayerAction.HitPointMarkCount - 1) * PlayerAction.HitMarkBurstStride;
            if (Memory.ReadFloat(at + PlayerAction.HitMarkBurstPos) != _burstAt[0] || Memory.ReadFloat(at + PlayerAction.HitMarkBurstPos + 8) != _burstAt[2]) return;
            Memory.WriteInt(at + PlayerAction.HitMarkBurstCount, 0);
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
        }
    }
}
