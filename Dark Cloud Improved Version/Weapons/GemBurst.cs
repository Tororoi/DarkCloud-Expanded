using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Fire the game's THROWN-GEM elemental burst (Fire/Ice/Thunder/Wind/Holy) anywhere in the world, at any
    /// size — as a pure visual, or with real damage.
    ///
    /// This is a general capability, not a feature: the burst belongs to no weapon or character, so any effect
    /// that wants an elemental impact can call <see cref="Show"/>. It works by replicating, as plain memory
    /// writes, exactly what the vanilla thrown-gem path (Step__14CMainItemModel → Set__12CSHOT_EFFECT) writes
    /// into a resident <c>MasekiEffect</c> slot — the same "feed the game data it already interprets" approach
    /// the item-Bomb pool uses for Big Bang. No game function is called and no code is injected.
    ///
    /// The pool's layout, residency and gating are documented on <see cref="MasekiEffect"/>. The two constraints
    /// that shape the API: <see cref="MasekiEffect.SubSlots"/> (3) bursts per element may be live at once, and a
    /// burst is FIRE-AND-FORGET — the engine clears its active flag when the lifetime expires, so there is
    /// nothing to tear down and no state for callers to hold.
    /// </summary>
    internal static class GemBurst
    {
        // Vanilla's own values for a thrown gem (Step__14CMainItemModel). Lifetime is in engine ticks.
        private const int  VanillaLifetime = 10;
        private const int  VanillaWait     = 5;    // re-hit wait
        private const bool Silent          = true; // the caller's own effect already makes noise

        /// <summary>Spawn an elemental burst at a world point. <paramref name="element"/> is one of the
        /// <see cref="MasekiEffect"/> element constants; <paramref name="scale"/> is 1.0 for a normal thrown-gem
        /// burst and larger to grow it.
        ///
        /// <paramref name="damage"/> defaults to 0 = a PURELY VISUAL burst, which suppresses the burst's collision
        /// sphere outright (see <see cref="MasekiEffect.HitCooldownOff"/>) rather than merely asking for zero
        /// damage — the damage formula floors at 1 HP, and worse, a colliding burst registers as a HIT, which any
        /// caller triggering off the engine's hit ring would then react to by spawning another burst, and cascade.
        ///
        /// Returns false if the pool isn't loaded (not in a dungeon yet) or all three of that element's slots are
        /// still busy — both are normal, transient conditions, so callers can simply skip the visual.</summary>
        internal static bool Show(int element, float x, float y, float z, float scale,
                                  int damage = 0, float colRadius = 0f, float speedMult = 1f,
                                  int elementBits = -1)
        {
            long slot = MasekiEffect.Slot(element);
            long desc = MasekiEffect.Desc(element);

            // A damaging burst resolves its damage through the ENGINE (its own collision sphere → CheckDmg), which
            // is the only way guards, elements, death, drops and de-targeting all behave. Widening the sphere is
            // how it damages an AREA. The radius lives on the shared descriptor, so snapshot the original once and
            // hand it back in Restore().
            if (damage > 0 && colRadius > 0f)
            {
                long r = desc + MasekiEffect.ColRadiusOffset;
                if (!_origRadius.ContainsKey(element)) _origRadius[element] = Memory.ReadFloat(r);
                Memory.WriteFloat(r, colRadius);
            }

            // The element the burst's DAMAGE carries, independent of which element's ANIMATION is playing — so an
            // effect can look like wind and hurt like whatever the weapon is actually charged with. -1 leaves the
            // element the descriptor's own (a wind burst deals wind damage).
            if (elementBits >= 0)
            {
                long b = desc + MasekiEffect.ElementBitsOffset;
                if (!_origElement.ContainsKey(element)) _origElement[element] = Memory.ReadInt(b);
                Memory.WriteInt(b, elementBits);
            }
            bool loaded = MasekiEffect.Loaded(element);
            int  i      = loaded ? FreeSubSlot(slot) : -1;

            if (!loaded || i < 0) return false;    // not in a dungeon yet, or all 3 bursts still live

            long sub = MasekiEffect.SubChar(slot, i);

            // ── the sub-slot's CCharacter: where it is, how big, which animation ──
            // Draw__10CCharacter re-composes the model's frame from these EVERY draw, so writing the CObject
            // fields is enough — the CFrame matrix never has to be touched.
            Memory.WriteFloat(sub + CCharacter.CharPos,      x);
            Memory.WriteFloat(sub + CCharacter.CharPos + 4,  y);
            Memory.WriteFloat(sub + CCharacter.CharPos + 8,  z);
            Memory.WriteFloat(sub + CCharacter.CharPos + 12, 1f);
            Memory.WriteFloat(sub + CCharacter.CharRot,      0f);
            Memory.WriteFloat(sub + CCharacter.CharRot + 4,  0f);
            Memory.WriteFloat(sub + CCharacter.CharRot + 8,  0f);
            Memory.WriteFloat(sub + CCharacter.CharScale,     scale);   // the size knob (per sub-slot)
            Memory.WriteFloat(sub + CCharacter.CharScale + 4, scale);
            Memory.WriteFloat(sub + CCharacter.CharScale + 8, scale);

            // Motion: the element's animation index lives in the descriptor (all five elements share one .chr).
            //
            // speedMult scales the burst's play rate (a scaled-up burst reads better slowed down — the animation
            // was authored for a small thrown-gem puff, so at 5x the stock rate makes a room-sized gust snap).
            // The engine's override at +0xC60 is an ABSOLUTE rate, not a multiplier, and a motion's own rate is
            // baked per-motion and is NOT 1.0 in general — so read THIS motion's real KEY step and scale that.
            // 1.0 leaves the override at -1 = "use the motion's own rate", i.e. touches nothing.
            short motionIdx = (short)Memory.ReadUShort(desc + MasekiEffect.MotionIdxOffset);
            float rate = CharacterMotion.MotionSpeedUseKey;
            if (speedMult != 1f)
            {
                float key = MotionEntry(sub, motionIdx, CCharacter.MotionEntryStep);
                if (key > 0f) rate = key * speedMult;
            }
            Memory.WriteFloat(sub + CharacterMotion.MotionSpeedOffset, rate);
            Memory.WriteInt  (sub + CCharacter.MotionFlags, 6);
            Memory.WriteInt  (sub + CCharacter.MotionId, motionIdx);
            Memory.WriteFloat(sub + CCharacter.MotionFrame, MotionEntry(sub, motionIdx, CCharacter.MotionEntryStart));

            // ── the per-sub-slot state arrays on the element slot ──
            long vel = slot + MasekiEffect.VelOffset + i * MasekiEffect.VelStride;   // a gem burst is stationary
            Memory.WriteFloat(vel, 0f);
            Memory.WriteFloat(vel + 4, 0f);
            Memory.WriteFloat(vel + 8, 0f);
            Memory.WriteFloat(vel + 12, 1f);

            // Re-seed the burst's state machine. Reusing a sub-slot without clearing the phase leaves it in the
            // state the PREVIOUS burst died in, and Step then never reaches its expire branch: the active flag is
            // never cleared, the slot leaks, and once all three leak the effect silently stops rendering forever.
            Memory.WriteUShort(slot + MasekiEffect.PhaseCountOff  + i * 2, 0);
            Memory.WriteInt  (slot + MasekiEffect.PhaseTimerOff   + i * 4, Memory.ReadInt(desc + MasekiEffect.PhaseSeedOffset));
            Memory.WriteUShort(slot + MasekiEffect.PhaseOffset    + i * 2, 0);
            Memory.WriteInt  (slot + MasekiEffect.DamageOffset    + i * 4, damage);
            Memory.WriteInt  (slot + MasekiEffect.WepStatusOffset + i * 4, 1);
            Memory.WriteUShort(slot + MasekiEffect.UserIdOffset   + i * 2, 0xFFFF);
            Memory.WriteUShort(slot + MasekiEffect.UserId2Offset  + i * 2, 0xFFFF);
            Memory.WriteInt  (slot + MasekiEffect.Unk070Offset    + i * 4, -1);
            Memory.WriteInt  (slot + MasekiEffect.LoopOffset      + i * 4, -1);
            Memory.WriteFloat(slot + MasekiEffect.RandomRateOff   + i * 4, -1f);   // <0 = no positional jitter
            Memory.WriteInt  (slot + MasekiEffect.LifeTimeOffset  + i * 4, VanillaLifetime);
            Memory.WriteInt  (slot + MasekiEffect.EnemyAttrOffset + i * 4, -1);
            Memory.WriteByte (slot + MasekiEffect.NoSoundOffset   + i, (byte)(Silent ? 1 : 0));
            Memory.WriteByte (slot + MasekiEffect.WaitOffset      + i, VanillaWait);
            // damage 0 => make it truly non-colliding, not just harmless (a 0-damage sphere still chips 1 HP and
            // still counts as a hit). Vanilla's 0 lets the sphere spawn on the next frame.
            Memory.WriteByte (slot + MasekiEffect.HitCooldownOff  + i, damage > 0 ? (byte)0 : MasekiEffect.NoCollide);
            Memory.WriteInt  (slot + MasekiEffect.Unk148Offset, 0);
            Memory.WriteInt  (slot + MasekiEffect.LastEnteredOff, i);

            // ACTIVE LAST — Step/Draw pick the burst up on the very next frame, so every field above must
            // already be in place or the first frame renders a half-built effect.
            Memory.WriteUShort(slot + MasekiEffect.ActiveOffset + i * 2, 1);
            return true;
        }

        private static readonly System.Collections.Generic.Dictionary<int, float> _origRadius =
            new System.Collections.Generic.Dictionary<int, float>();
        private static readonly System.Collections.Generic.Dictionary<int, int> _origElement =
            new System.Collections.Generic.Dictionary<int, int>();

        /// <summary>Hand the element's descriptor back to the game — collision radius and damage element both. The
        /// descriptor is SHARED with real thrown gems, so any caller that changed it must restore it once its
        /// effect is done (e.g. on unequip), or a thrown gem inherits the mod's radius and element.</summary>
        internal static void Restore(int element)
        {
            bool loaded = MasekiEffect.Loaded(element);
            if (_origRadius.TryGetValue(element, out float r))
            {
                if (loaded) Memory.WriteFloat(MasekiEffect.Desc(element) + MasekiEffect.ColRadiusOffset, r);
                _origRadius.Remove(element);
            }
            if (_origElement.TryGetValue(element, out int b))
            {
                if (loaded) Memory.WriteInt(MasekiEffect.Desc(element) + MasekiEffect.ElementBitsOffset, b);
                _origElement.Remove(element);
            }
        }

        /// <summary>First sub-slot of this element whose active flag is clear, or -1 if all three are live.
        /// The engine clears the flag itself when a burst expires, so slots recycle without our help.</summary>
        private static int FreeSubSlot(long slot)
        {
            for (int i = 0; i < MasekiEffect.SubSlots; i++)
                if (Memory.ReadUShort(slot + MasekiEffect.ActiveOffset + i * 2) == 0) return i;
            return -1;
        }

        /// <summary>A field of the motion's own Mot_List entry (start frame, end frame, or its KEY play rate).
        /// The vanilla spawn seeds the animation from the entry's start frame rather than 0, so a burst begins on
        /// the right key; the KEY rate is what a speed multiplier has to scale. Returns 0 if the list is absent.</summary>
        private static float MotionEntry(long sub, short motionIdx, int field)
        {
            long list = (uint)Memory.ReadInt(sub + CCharacter.MotionList);
            if (!Memory.IsValidGuest(list)) return 0f;
            long entry = Memory.ToMmu((int)list) + motionIdx * CCharacter.MotionEntryStride + field;
            return field == CCharacter.MotionEntryStep ? Memory.ReadFloat(entry) : Memory.ReadInt(entry);
        }
    }
}
