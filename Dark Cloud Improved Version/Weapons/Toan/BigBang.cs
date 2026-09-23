using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Big Bang — Solar Harvest from the Sun Sword line, and a guard-charged blade whose next landed hit
    /// detonates (Detonate).</summary>
    internal static class BigBang
    {
        // ── Big Bang "Detonate" ────────────────────────────────────────────────────────────
        private const int    TickMs           = 30;
        private const float  BlastRadius      = 30f;    // the blast reaches this far from the hit point
        private const float  DamageFraction   = 3.0f;   // the blast's base damage, as a multiple of the weapon's attack
        private const float  PerEnemyRadius   = 25f;    // each victim gets its OWN sphere, centred on it (see PlantBlast)
        private const int    HitLifeTicks     = 5;      // the planted spheres are withdrawn after this many ticks
        private const int    MutekiClearMax   = 30;     // belt-and-braces at plant time; a waking mimic (100) or a
                                                        // dying monster (1000) is never touched
        // The knockback is the ENGINE'S, driven entirely by data on the collision entry — nothing of ours is written
        // per tick, so nothing can race the frame the hit resolves on. Kick TYPE 2 (entry +0x98) is the radial one:
        // CheckDmg takes the kick ORIGIN (+0x80, the blast centre), computes normalize(enemy − origin) into the
        // enemy's launch direction, and writes force = +0x90 × that enemy's KnockbackMult and decay = +0x94 × the
        // same — so direction, per-species resistance and the rooted-plant exclusion all come out right for free.
        // Distance ≈ force²/(2·decay): 3.5 at 0.12 carries a normal enemy ~50 units, clear of a 30-unit blast.
        private const float  KickStrength     = 3.5f;
        private const float  KickDecay        = 0.12f;  // vanilla melee is 1.2 at 0.2, roughly 3.6 units
        private const int    KickTypeRadial   = 2;      // +0x98 = 2: thrown away from the kick origin
        // Toan is caught in his own blast: 100 before his defence, and the reaction that knocks him down. How far
        // that carries him is the knockdown clip's own root motion — the engine has no lever on it (see
        // PlayerCollision.KnockPush and PlayerAction.MotionRangeTablePtr, where both dead ends are recorded).
        private const int    SelfDamage       = 100;    // BtCheckDamageProc subtracts his defence and clamps at 0
        private const int    SelfReaction     = 3;      // knockdown — the reaction Rockanoff's melee carries
        // What the detonation costs the blade, in ordinary swings' worth of weapon HP. The engine's own drain for
        // the charge attack that set it off lands on top of this.
        private const float  WhpHits          = 5f;
        // THE BLADE ON A REGULAR CHARGE. Toan's charge meter runs 1.0 → 3.0 (lunge at 1.5, whirlwind at 2.5), and
        // the blade whitens across it exactly as it does for a guard charge — the same tint, driven by the meter
        // instead of by held time. It stands aside while SunSword.FlashArmed: Solar Flash owns the blade then, and
        // two ramps fighting over one mesh would only flicker.
        private const float  ChargeMeterFloor = 1.0f;
        // WHAT COUNTS AS A HIT: the engine's own HIT MARK — see HitLanded. The detonation rides the LEVEL-1 CHARGE
        // ATTACK (the lunge, PlayerAction.ActionLunge), so the blast goes off where that lunge connects.
        private const float  StruckRange      = 20f;    // a mark further than this from an enemy's centre is not its hit
        private const float  MarkSanity       = 60f;    // a mark further than this from Toan is not his swing's — ignore it
        // The explosion is the thrown-gem FIRE burst, spawned at the hit point by plain field writes into the
        // always-resident Maseki pool. It is authored as a small thrown-gem puff, so it is scaled up hard and
        // slowed down to stop the animation snapping at that size. Scale is the sub-slot's own CObject scale, not a
        // radius: it does not change what the blast HITS (that is BlastRadius).
        private const float  BurstScale       = 6.0f;
        private const float  BurstSpeed       = 0.6f;
        private const int    BurstElement     = MasekiEffect.Fire;   // the ANIMATION only
        private sealed class BlastState
        {
            public byte floor = 0xFF;
            public readonly List<(int slot, int ticks)> planted = new List<(int, int)>();
            public readonly List<(int slot, int hp0)>   victims = new List<(int, int)>();
            public bool crushing;                       // the guard break is currently driven on
            public bool tinted;                         // the blade is carrying this ability's charge tint
        }

        /// <summary>
        /// Ability Name: Detonate (Big Bang)
        /// Big Bang's CHARGE ATTACK detonates. Charging the attack whitens the blade as the meter fills, and when the
        /// level-1 charge — the lunge — lands, a fire burst erupts at the point of contact: every living enemy within
        /// <see cref="BlastRadius"/> of it, except the one the lunge itself hit, takes <see cref="DamageFraction"/> ×
        /// the weapon's attack as base damage through the normal formula, carrying NO element so no resistance blunts
        /// it, through any guard, and with a heavy shove outward. Toan is caught in it too — <see cref="SelfDamage"/>
        /// before his defence and the knockdown that throws him back. The blade pays <see cref="WhpHits"/> swings'
        /// worth of weapon HP for it. Dungeon only.
        ///
        /// Big Bang's GUARD charge is Solar Flash, inherited from the Sun Sword it grows out of and struck at twice
        /// that sword's share (SunSword.BigBangFlash) — so the two charges are different abilities on one blade.
        ///
        /// The blast damage is a sphere per victim in the engine's own collision pool — the same entries CheckDmg
        /// tests Toan's swings against — never a direct HP write, which produces a corpse that walks through walls.
        /// </summary>
        public static void DetonateEffect()
        {
            var st = new BlastState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.bigbang && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                try { Tick(st); }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] Detonate tick error: " + ex.Message); }
            }
            Reset(st);
        }

        private static void Tick(BlastState st)
        {
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != st.floor) { if (st.floor != 0xFF) Reset(st); st.floor = floor; }

            ExpireHits(st);
            if (Player.CheckDunIsPausedOrMenu()) return;
            if (Player.CurrentCharacterNum() != Player.ToanId) { ClearTint(st); return; }

            // Polled EVERY tick, not only during a charge, so the counter baseline never goes stale.
            bool hit = HitLanded(out float hx, out float hh, out float hy);
            int   action = Memory.ReadInt(PlayerAction.ChargeActionState);

            if (action == PlayerAction.ActionWindup && !SunSword.FlashArmed)
            {
                float meter = Memory.ReadFloat(PlayerAction.ChargeMeter);
                float k = (meter - ChargeMeterFloor) / (PlayerAction.ChargeMeterCap - ChargeMeterFloor);
                SolarBlade.Set(Math.Min(1f, Math.Max(0f, k)), SolarBlade.BigBangModel,
                               SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame);
                st.tinted = true;
            }
            else if (st.tinted && action != PlayerAction.ActionLunge && action != PlayerAction.ActionWhirlwind)
            {
                ClearTint(st);                                       // the charge was spent or dropped
            }

            if (hit && action == PlayerAction.ActionLunge) Detonate(st, hx, hh, hy);
        }

        /// <summary>The charge tint off the blade — but only ours. Solar Flash drives the same mesh from its own
        /// thread, so a tint it is holding is left alone.</summary>
        private static void ClearTint(BlastState st)
        {
            if (!st.tinted) return;
            st.tinted = false;
            if (!SunSword.FlashArmed) SolarBlade.Clear();
        }
        private static int _lastMark = -1;   // the hit counter as of the previous tick (−1 = no baseline yet)
        private static int _lastLife = -1;   // …and the current mark's countdown, which a BLOCKED hit resets
        /// <summary>The blast carries NO element (entry +0x50 = 0). CheckDmg's element step is wrapped in
        /// `if (col.attr != 0)`, so an elementless hit skips the whole branch: no element bonus, and — the point —
        /// no multiply by the monster's resistance percent. A detonation is not fire or ice, and a species that
        /// shrugs off every element takes it in full, defence being the only thing between it and the damage.</summary>
        private const uint ElementNone = 0;

        /// <summary>Did a hit just land, and where? Both kinds of landed hit leave the engine's own hit MARK behind,
        /// and they are told apart by what they do to the counter:
        ///
        /// • A hit that DAMAGES stamps the struck body part's position at the counter's index and then advances it, so
        ///   a counter that moved means the hit is at index (counter − 1). The stamp happens before the damage is
        ///   computed, so this fires on a hit resisted to nothing exactly as on one that hurts.
        /// • A hit that is BLOCKED takes an earlier branch that re-stamps the CURRENT index and leaves the counter
        ///   alone. All it disturbs is that mark's life (<see cref="PlayerAction.HitPointMarkLife"/>), which otherwise
        ///   only ever counts DOWN — one per frame, from 16 — so a life that has risen since the last look is a
        ///   blocked hit and nothing else. Catching it is what lets a guarded enemy set the charge off, instead of the
        ///   blade having to break its guard first.
        ///
        /// More than one hit can land between polls; the most recent one is the blast's origin. A mark further than
        /// <see cref="MarkSanity"/> from Toan is rejected rather than trusted, so a stale or unrelated entry cannot
        /// throw the detonation across the floor.</summary>
        private static bool HitLanded(out float x, out float h, out float y)
        {
            x = h = y = 0f;
            int n = PlayerAction.HitPointMarkCount;
            int now = Memory.ReadInt(PlayerAction.HitSparkCounter);
            int cur = ((now % n) + n) % n;
            int life = Memory.ReadInt(PlayerAction.HitPointMark + (long)cur * PlayerAction.HitPointMarkStride
                                      + PlayerAction.HitPointMarkLife);
            int was = _lastMark, wasLife = _lastLife;
            _lastMark = now; _lastLife = life;
            if (was < 0) return false;                              // no baseline yet

            int idx;
            if (now != was)       idx = ((now - 1) % n + n) % n;    // damaged: stamped, then the counter moved on
            else if (life > wasLife) idx = cur;                     // blocked: the current mark was stamped again
            else return false;
            long e = PlayerAction.HitPointMark + (long)idx * PlayerAction.HitPointMarkStride;
            x = Memory.ReadFloat(e); h = Memory.ReadFloat(e + 4); y = Memory.ReadFloat(e + 8);

            float dx = x - Memory.ReadFloat(Addresses.dunPositionX);
            float dy = y - Memory.ReadFloat(Addresses.dunPositionY);
            float dh = h - Memory.ReadFloat(Addresses.dunPositionZ);
            return dx * dx + dy * dy + dh * dh <= MarkSanity * MarkSanity;
        }

        /// <summary>The blast at the hit point: the fire burst where the struck enemy stands, the damage around it,
        /// and Toan thrown out of it.</summary>
        private static void Detonate(BlastState st, float x, float h, float y)
        {
            // The blow direction: from the blast back toward Toan, so he is thrown out of it and the spin leaves him
            // facing the way he swung (unitBlowActionRot takes atan2(x, z) − π of this and SETS his facing).
            float px = Memory.ReadFloat(Addresses.dunPositionX), py = Memory.ReadFloat(Addresses.dunPositionY);
            float bx = px - x, bz = py - y;
            float blen = (float)Math.Sqrt(bx * bx + bz * bz);
            if (blen < 1e-3f)                                            // standing inside it: fall back to his facing
            {
                float yaw = PlayerFacing();
                bx = -(float)Math.Sin(yaw); bz = -(float)Math.Cos(yaw); blen = 1f;
            }
            bx /= blen; bz /= blen;

            ushort attack = Player.Weapon.GetCurrentWeaponAttack();
            if (!SunSword.FlashArmed) SolarBlade.Clear();                // the charge is spent; the blade's colour back
            st.tinted = false;
            // Visual only: damage 0 suppresses the burst's own collision sphere outright, so it cannot register hits
            // of its own and cascade. The blast's damage is planted below.
            GemBurst.Show(BurstElement, x, h, y, BurstScale, damage: 0, speedMult: BurstSpeed);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] detonation at the hit mark ({x:F0},{h:F0},{y:F0}) r={BlastRadius:F0}; blow dir ({bx:F2},{bz:F2})");
            PlantBlast(st, x, h, y, attack, StruckSlot(x, h, y));
            PlantSelfHit(st, x, h, y, bx, bz);
            DrainWhp();
        }


        /// <summary>The enemy the blade just hit: the live one nearest the hit mark, which is stamped ON the struck
        /// body part, so the nearest enemy to it IS that enemy. Returns −1 when nothing is close enough to be it —
        /// then the blast covers the whole radius as before.</summary>
        private static int StruckSlot(float x, float h, float y)
        {
            int best = -1; float bestD = StruckRange * StruckRange;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                float ex = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX));
                float ey = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY));
                float eh = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationZ));
                float dx = ex - x, dy = ey - y, dh = eh - h;
                float d = dx * dx + dy * dy + dh * dh;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>Is the enemy in slot <paramref name="s"/> inside the blast? A TRUE sphere — height counts, so an
        /// enemy on a ledge above the hit point is not caught. This matches how the engine tests Toan against the
        /// self-hit sphere (CheckHitUser compares the distance against the entry's radius, then the vertical bands),
        /// so both sides of the blast agree on who is in it.</summary>
        private static bool InBlast(int s, float x, float h, float y)
        {
            float ex = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX));
            float ey = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY));
            float eh = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationZ));
            float dx = ex - x, dy = ey - y, dh = eh - h;
            return dx * dx + dy * dy + dh * dh <= BlastRadius * BlastRadius;
        }

        /// <summary>Toan's facing, read the way the engine reads it: getCharacterVector (main 0x1D41A0) takes the
        /// MODEL ROOT frame's euler Y and rotates the constant (0,0,1) by it, so forward is (sin yaw, 0, cos yaw).
        /// GetRotation__6CFrame only trusts that cache while <see cref="CFrameVu1.EulerValid"/> is 0, so the CObject
        /// euler at CCharacter+0x64 is the fallback. ⚠ The two are DIFFERENT fields: they differ by the model's bind
        /// rotation.</summary>
        private static float PlayerFacing()
        {
            uint root = Memory.ReadGuestPtr(CCharacter.Base + CCharacter.CharModel);
            if (Memory.IsValidGuest(root))
            {
                long m = Memory.ToMmu(root);
                if (Memory.ReadInt(m + CFrameVu1.EulerValid) == 0) return Memory.ReadFloat(m + CFrameVu1.EulerY);
            }
            return Memory.ReadFloat(CCharacter.Base + CCharacter.CharRotY);
        }

        /// <summary>A player-attack sphere ON EACH ENEMY within <see cref="BlastRadius"/> of the hit point: base =
        /// <see cref="DamageFraction"/> × the weapon's attack, no element (<see cref="ElementNone"/>), and a kick
        /// originating at the blast so each one is shoved outward from it.
        ///
        /// ⚠ NOT one big sphere. A collision entry is CONSUMED by the first victim the engine matches it against, so a
        /// single blast-sized sphere damages exactly one enemy and leaves the rest untouched. One small sphere centred
        /// on each enemy hits all of them, and the pool holds 96 entries against at most 16 enemies.</summary>
        private static void PlantBlast(BlastState st, float x, float h, float y, ushort attack, int skip)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int atk = attack > 0 ? attack : Player.Weapon.GetCurrentWeaponAttack();
            int baseDmg = Math.Max(1, (int)Math.Round(atk * DamageFraction));
            int hit = 0, missed = 0;
            st.victims.Clear();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (s == skip) continue;                               // the blade hit this one; it has its damage already
                if (!Enemies.IsLive(s)) continue;
                if (!InBlast(s, x, h, y)) continue;                    // a TRUE sphere: height counts
                float ex = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX));
                float ey = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY));
                float eh = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationZ));
                int slot = CollisionPool.TakeFreeSlot(pool);
                if (slot < 0) { missed++; continue; }
                byte[] e = CollisionPool.PlayerHitEntry(ex, eh, ey, PerEnemyRadius, baseDmg, ElementNone);
                void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
                F(0x80, x); F(0x84, h); F(0x88, y);                    // the kick ORIGIN: the blast, so they fly away from it
                F(0x90, KickStrength); F(0x94, KickDecay);
                BitConverter.GetBytes(KickTypeRadial).CopyTo(e, 0x98);
                CollisionPool.Plant(pool, slot, e);
                st.planted.Add((slot, HitLifeTicks));
                st.victims.Add((s, Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp))));
                hit++;
            }
            HoldVictimsHittable(st);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] {hit} enemies struck for base {baseDmg}"
                + (missed > 0 ? $" ({missed} missed — pool full)" : ""));
        }

        /// <summary>Clear any hit-stun still standing as the spheres go in — a species whose damage label sets a
        /// longer one than the usual 9 frames would otherwise outlast the delay. ONE SHOT, at plant time only: the
        /// invincibility each victim is granted by the blast's own hit is what stops a second helping, so nothing
        /// here may run again afterwards. Anything above <see cref="MutekiClearMax"/> is not a hit's stun and is
        /// left alone.</summary>
        private static void HoldVictimsHittable(BlastState st)
        {
            foreach (var (slot, _) in st.victims)
            {
                long t = EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.HitStunTimer);
                int stun = Memory.ReadInt(t);
                if (stun > 0 && stun <= MutekiClearMax) Memory.WriteInt(t, 0);
            }
        }

        /// <summary>Toan's own share of the blast: one sphere that hurts the PLAYER, centred on the explosion, with the
        /// knockdown reaction — so the engine spins him to the blow direction, plays the big-damage stagger and stuns
        /// him, exactly as a Rockanoff hit does. The damage handler ignores further hits while a reaction is running,
        /// so the entry cannot re-hit him.
        ///
        /// ⚠ The direction is the entry's BLOW DIRECTION (<see cref="CollisionPool.BlowDir"/>), NOT the kick words at
        /// +0x80..+0x98. BtCheckDamageProc copies +0x20 into a scratch and hands it to unitBlowActionRot; it never
        /// reads the kick words, which belong to the enemy path. Both builders default that vector to (1,0,0), a
        /// fixed WORLD bearing — which is why leaving it threw him the same way on the map every time.</summary>
        private static void PlantSelfHit(BlastState st, float x, float h, float y, float dirX, float dirZ)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int slot = CollisionPool.TakeFreeSlot(pool);
            if (slot < 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] no pool entry for the self-hit — Toan keeps his feet"); return; }
            byte[] e = CollisionPool.PlayerHurtEntry(x, h, y, BlastRadius, SelfDamage, SelfReaction, dirX, 0f, dirZ);
            CollisionPool.Plant(pool, slot, e);
            st.planted.Add((slot, HitLifeTicks));
        }

        /// <summary>The blade pays for the detonation in weapon HP: <see cref="WhpHits"/> ordinary swings' worth, by
        /// the engine's own formula — `(1.5 − 0.01 × Endurance) × factor`, halved by Durable and doubled by Fragile
        /// (BattleSubWeaponDmg 0x1B5D90). Endurance comes from the BATTLE record, where attachments have already been
        /// folded in; WHP itself lives on the INVENTORY record (+0x10 of the equipped bag slot), which is the copy the
        /// engine drains and the menu shows.
        ///
        /// ⚠ Floored at 1, never 0. Everything that happens at zero WHP — the auto-consumed Repair Powder, the
        /// warnings, the weapon breaking back to its base form — lives inside that native function, which a write here
        /// does not call. Leaving 1 keeps the blade whole and lets the next ordinary hit take it to zero through the
        /// engine's own path, with all of that intact. A weapon at Endurance 150 pays nothing, exactly as its swings
        /// cost nothing.</summary>
        private static void DrainWhp()
        {
            int bag = Memory.ReadByte(DngStatusData.EquippedSlotAddr(Player.ToanId));
            if (bag < 0 || bag >= DngStatusData.MaxWeaponSlots) return;
            long rec = DngStatusData.WeaponRecord(Player.ToanId, bag);
            if (Memory.ReadUShort(rec) != Items.bigbang) return;             // not the blade we just spent

            float factor = WhpHits;
            int flags = Memory.ReadUShort(rec + WeaponHave.AbilityFlagsOffset);
            if ((flags & WeaponHave.DurableFlag) != 0) factor *= 0.5f;
            if ((flags & WeaponHave.FragileFlag) != 0) factor *= 2f;
            int endurance = Memory.ReadShort(WeaponHave.BattleWeaponRecord + WeaponHave.EffEnduranceOffset);
            float drain = (1.5f - 0.01f * endurance) * factor;
            if (drain <= 0f) return;

            long whpAddr = rec + WeaponHave.InventoryWeaponWhpOffset;
            float whp = Memory.ReadFloat(whpAddr);
            float left = Math.Max(1f, whp - drain);
            Memory.WriteFloat(whpAddr, left);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] detonation cost {whp - left:F1} WHP ({whp:F0} → {left:F0})");
        }

        /// <summary>The engine withdraws its own swing spheres when the swing ends; ours are withdrawn here.</summary>
        private static void ExpireHits(BlastState st)
        {
            // Guards are crushed while the charge is PRIMED and while the blast's spheres are live. Primed, because
            // a guarding enemy inside the blast is hurt like any other. The charge attack itself does not need it:
            // a blocked hit still stamps a mark, HitLanded reads that stamp, and the detonation it sets off is where
            // the damage lives.
            //
            // ⚠ It MUST be driven off again — the flags stay zeroed until something restores them, and leaving them
            // so would disarm every enemy on the floor. Reset restores, and this drops it the tick the last sphere
            // expires.
            bool want = st.planted.Count > 0;
            if (want != st.crushing) { GuardBreak.Drive(want); st.crushing = want; }
            if (st.planted.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = st.planted.Count - 1; i >= 0; i--)
            {
                var (slot, ticks) = st.planted[i];
                if (--ticks > 0) { st.planted[i] = (slot, ticks); continue; }
                if (pool != 0) CollisionPool.Deactivate(pool, slot);
                st.planted.RemoveAt(i);
            }
            if (st.planted.Count == 0 && st.victims.Count > 0)
            {
                var dealt = new List<string>();
                foreach (var (slot, hp0) in st.victims)
                    dealt.Add($"{slot}:{hp0 - Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp))}");
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    "[BigBang] blast damage by slot — " + string.Join(", ", dealt));
                st.victims.Clear();
            }
        }

        private static void Reset(BlastState st)
        {
            ClearTint(st);
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in st.planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            st.planted.Clear();
            st.victims.Clear();
            if (st.crushing) { GuardBreak.Drive(false); st.crushing = false; }   // never leave the floor disarmed
        }
    }
}
