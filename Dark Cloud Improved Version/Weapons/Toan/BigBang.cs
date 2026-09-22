using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Big Bang — Solar Harvest from the Sun Sword line, and a guard-charged blade whose next swing
    /// detonates (Detonate).</summary>
    internal static class BigBang
    {
        // ── Big Bang "Detonate" ────────────────────────────────────────────────────────────
        private const int    TickMs           = 30;
        private const double ChargeSeconds    = 1.5;    // guard held this long primes the blade (as Solar Flash's)
        private const float  BlastRadius      = 160f;   // the blast reaches this far from the sword
        private const float  DamageFraction   = 2.0f;   // the blast's base damage, as a multiple of the weapon's attack
        private const float  PerEnemyRadius   = 25f;    // each victim gets its OWN sphere, centred on it (see PlantBlast)
        private const int    HitLifeTicks     = 3;      // the planted spheres are withdrawn after this many ticks
        // The blast's shove — HEAVY. For scale: Toan's own combo hits use strength 1.2 with decay 0.2 (the ELF
        // constants ToanKey_Play hands SetKickBack), Goro's hammer swing 2.5 with a slow 0.1 that carries further,
        // and Super Steve's wind clamps at 6 because a flat 40 threw enemies clean off the map. So this is roughly
        // four times a sword hit, with a slower decay so they travel rather than just flinch.
        private const float  KickStrength = 5.0f, KickDecay = 0.12f;
        private const int    KickTypeMelee    = 2;      // +0x98: the melee-style reaction (flinch + shove)
        private const double PrimedSeconds    = 10.0;   // a charge left unused this long dissipates
        private const double DissipateSeconds = 0.5;    // …shrinking the glow away
        // Toan is caught in his own blast: 100 before his defence, and the reaction that launches him.
        private const int    SelfDamage       = 100;    // BtCheckDamageProc subtracts his defence and clamps at 0
        private const int    SelfReaction     = 3;      // knockdown — the reaction Rockanoff's melee carries
        // WHERE it goes off: one sword-length straight ahead of Toan. His position pushed along HIS FACING by the
        // weapon's melee reach (WeaponData.Dcol1 — Big Bang's is 11.378), lifted to about blade height so it is not
        // at his feet. The facing comes from the model root frame's euler, which is the field the engine's own
        // forward vector is built from (see PlayerFacing) — reading the CObject euler instead put the blast a bind
        // rotation away from ahead, which looked like it was going off on top of him.
        private const float  DefaultReach     = 11.4f;  // if the weapon table has no dcol1 for it
        private const float  BlastLift        = 8f;     // above his origin — roughly where the blade is
        private const float  PrimedTint       = 45f;    // the slight white Toan keeps while the charge is held, per channel (an ambient ADD)
        // WHEN it goes off: near the END of the opening swing. That clip runs frames 820-830 and its hit window is
        // 825-828, so this lets the swing almost finish. Only that swing arms the blast — the later combo swings, the
        // lunge and the whirlwind are all reached by holding an attack that BEGINS with this one, so the charge is
        // always spent before any of them can start.
        private const float  Swing1Late       = 827f;
        // The burst is authored as a small thrown-gem puff, so a blast-sized one is scaled up and slowed down.
        private const float  BurstScale       = 6.0f;
        private const float  BurstSpeed       = 0.6f;
        private const int    BurstElement     = MasekiEffect.Fire;   // the ANIMATION only — the damage carries the weapon's element
        // ── the shared effect slot ──────────────────────────────────────────────────────────
        // The dead `dun\effect\explosion.chr` — a large one-KEY burst no config in the game's 34-entry shot table
        // names, so nothing of the game's ever loads it. It already carries its own `explosion.cfg` record, so the
        // pack's loader takes it as-is (no ISO bake, unlike the underscore balls).
        //
        // It can only live in the MAIN-CHARACTER effect instance: the cave's Entry2 passes texture block 0x10 as an
        // IMMEDIATE, and that is the block that instance uses (a gem-pool slot is block 6, a monster-pack slot 0x26 —
        // either would need the stub changed and the ISO re-patched). That instance is also where Toan's whirlwind
        // (`c01_fuusya`) lives, and the swoosh renders from its sub-slots, so the slot is SHARED rather than taken:
        // the explosion is seeded when a guard charge STARTS, and the whirlwind is put back when that charge is
        // abandoned, when it lapses unused, and once the blast has finished animating. Restoring is lossless — the
        // whirlwind's own config already has victim mask 2, flying radius 0 and flags 0x8 (Wind alone), which is
        // exactly what Seed would force on it.
        //
        // Each swap costs a mid-floor disc read, which is why the seed happens as the charge begins rather than at the
        // swing: it has the charge's length to land. If it has not, the blast falls back to the thrown-gem burst.
        private const int    ExplosionTemplate = 16;                 // zibaku_f2: a self-detonation — bursts in place, nothing flies
        private const string ExplosionName     = "explosion";
        private const string WhirlwindName     = "c01_fuusya";       // the config's own name, as dun.bin holds it
        private const float  ExplosionScale    = 2.0f;               // CObject scale on the sub-shot, not the gem burst's sprite scale
        private const double VentMaxSeconds    = 3.0;                // backstop: never hold the slot longer than this after a blast
        private static volatile bool _wantExplosion;                 // read by WantedShot on the BorrowedShots thread

        private enum Phase { Idle, Charging, Primed, Windup, Venting, Dissipating }

        private sealed class BlastState
        {
            public Phase phase;
            public DateTime holdStart, primedAt, dissipateAt, ventAt;
            public byte floor = 0xFF;
            public readonly List<(int slot, int ticks)> planted = new List<(int, int)>();
        }

        /// <summary>
        /// Ability Name: Detonate (Big Bang)
        /// Hold guard and the blade charges over <see cref="ChargeSeconds"/>; at full it is PRIMED and stays so,
        /// guard or not, marked by the blue glow Toan carries (<see cref="ToanGlowBakes.BlueName"/>, the Divine Beast
        /// Title cat's ramp), the whitening blade and the slight white he holds. The next SWING spends it: near the
        /// end of that swing an explosion erupts at the tip of the sword's reach, and every living enemy within
        /// <see cref="BlastRadius"/> takes the weapon's full attack as base damage through the normal formula, with
        /// the sword's element and a melee stagger. Toan is caught in it too — <see cref="SelfDamage"/> before his
        /// defence, and the knockdown reaction that launches him back out. A charge left unused for
        /// <see cref="PrimedSeconds"/> dissipates. Dungeon only; a sidekick out or a floor change drops it.
        ///
        /// The blast damage is a sphere per victim in the engine's own collision pool — the same entries CheckDmg
        /// tests Toan's sword swings against — never a direct HP write, which produces a corpse that walks through
        /// walls. Firing on the SWING rather than on a landed hit is what keeps the charge from surviving into a
        /// whirlwind. A swing into empty air still detonates, and still costs him.
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
            if (Player.CurrentCharacterNum() != Player.ToanId)
            {
                if (st.phase != Phase.Idle) { Disarm(st); }
                return;
            }

            int action = Memory.ReadInt(PlayerAction.ChargeActionState);
            switch (st.phase)
            {
                case Phase.Idle:
                    if (GuardWatch.IsGuarding())
                    {
                        st.phase = Phase.Charging; st.holdStart = GameClock.Now;
                        _wantExplosion = true;                           // the swap starts now: it has the charge to land
                    }
                    break;

                case Phase.Charging:
                {
                    // Guard released before it primed: the glow goes with it, and the whirlwind gets its slot back.
                    if (!GuardWatch.IsGuarding()) { Disarm(st); break; }
                    double held = (GameClock.Now - st.holdStart).TotalSeconds;
                    SolarBlade.Set((float)(held / ChargeSeconds), SolarBlade.BigBangModel, SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame);
                    ChargeTint.Ramp(ChargeSeconds - held);
                    // The glow LEADS the charge: started a grow-time early, it is at full size exactly as it primes.
                    if (held >= ChargeSeconds - SolarGlow.GrowSeconds) { SolarGlow.Show(ToanGlowBakes.BlueName); SolarGlow.Tick(); }
                    if (held >= ChargeSeconds)
                    {
                        st.phase = Phase.Primed; st.primedAt = GameClock.Now;
                        ChargeTint.Clear();                              // the cyan build-up ends; the white hold takes over
                        SolarGlow.Show(ToanGlowBakes.BlueName);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] Detonate primed");
                    }
                    break;
                }

                case Phase.Primed:
                    SolarBlade.Set(1f, SolarBlade.BigBangModel, SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame);          // re-asserted each tick: a rebuilt model gets it back
                    SolarGlow.Show(ToanGlowBakes.BlueName); SolarGlow.Tick();
                    HoldPrimedTint(1f);
                    if (action == PlayerAction.ActionComboFirst) { st.phase = Phase.Windup; break; }
                    if ((GameClock.Now - st.primedAt).TotalSeconds >= PrimedSeconds)
                    {
                        st.phase = Phase.Dissipating; st.dissipateAt = GameClock.Now;
                        SolarGlow.Fade();
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] charge went unused — dissipating");
                    }
                    break;

                case Phase.Windup:
                {
                    SolarBlade.Set(1f, SolarBlade.BigBangModel, SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame);
                    SolarGlow.Tick();
                    HoldPrimedTint(1f);
                    // Cancelled, or the chain moved on before the poll caught the frame: still primed, and the next
                    // fresh opening swing arms it again.
                    if (action != PlayerAction.ActionComboFirst) { st.phase = Phase.Primed; break; }
                    if (Memory.ReadFloat(PlayerAction.AnimFrameCursor) < Swing1Late) break;   // let the swing almost finish
                    bool borrowed = Detonate(st);
                    if (borrowed) { st.phase = Phase.Venting; st.ventAt = GameClock.Now; }
                    else { _wantExplosion = false; st.phase = Phase.Idle; }                   // the gem burst ran: the slot is free now
                    break;
                }

                case Phase.Venting:
                    // The explosion is still playing out of the shared slot. Restoring the whirlwind re-enters the
                    // instance, which would cut the burst off mid-animation — so hold the seed until its sub-shots
                    // go quiet (the engine clears them itself), with a backstop in case one never activated.
                    if (BurstPlaying() && (GameClock.Now - st.ventAt).TotalSeconds < VentMaxSeconds) break;
                    _wantExplosion = false;
                    st.phase = Phase.Idle;
                    break;

                case Phase.Dissipating:
                {
                    // The charge lapses: the white bleeds out of the blade and Toan as the glow shrinks away, and the
                    // whirlwind takes its slot back.
                    double t = (GameClock.Now - st.dissipateAt).TotalSeconds / DissipateSeconds;
                    SolarGlow.Tick();
                    if (t >= 1.0) { Disarm(st); }
                    else { SolarBlade.Set((float)(1.0 - t), SolarBlade.BigBangModel, SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame); HoldPrimedTint((float)(1.0 - t)); }
                    break;
                }
            }
        }

        /// <summary>Everything off and the shared slot handed back: a charge abandoned, lapsed, or dropped because
        /// Toan is no longer the one out.</summary>
        private static void Disarm(BlastState st)
        {
            SolarBlade.Clear(); ChargeTint.Clear(); SolarGlow.Hide();
            _wantExplosion = false;
            st.phase = Phase.Idle;
        }

        /// <summary>Whether any sub-shot of the shared instance is still live — the explosion still animating.</summary>
        private static bool BurstPlaying()
        {
            long inst = ShotEffectPack.CharaMainEffect;
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return false;
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) != 0) return true;
            return false;
        }

        /// <summary>The blast at the sword's tip: the explosion there, the damage around it, and Toan thrown out of it.
        /// True when the borrowed `explosion` effect carried the visual (so the shared slot must be held until it has
        /// finished), false when the thrown-gem burst stood in for it.</summary>
        private static bool Detonate(BlastState st)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX);
            float ph = Memory.ReadFloat(Addresses.dunPositionZ);    // height
            float py = Memory.ReadFloat(Addresses.dunPositionY);    // the ground plane with X
            float yaw = PlayerFacing();
            float reach = BlastReach();
            float x = px + (float)Math.Sin(yaw) * reach;
            float y = py + (float)Math.Cos(yaw) * reach;
            float h = ph + BlastLift;
            // The blow direction: from the blast back toward Toan, so he is thrown out of it and the spin leaves him
            // facing the way he swung (unitBlowActionRot takes atan2(x, z) − π of this and SETS his facing).
            float bx = px - x, bz = py - y;
            float blen = (float)Math.Sqrt(bx * bx + bz * bz);
            if (blen < 1e-3f) { bx = -(float)Math.Sin(yaw); bz = -(float)Math.Cos(yaw); blen = 1f; }
            bx /= blen; bz /= blen;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] blast origin: Toan ({px:F0},{ph:F0},{py:F0}) yaw {yaw:F2} → ({x:F0},{h:F0},{y:F0}), {reach:F1} ahead; blow dir ({bx:F2},{bz:F2})");

            SolarBlade.Clear();                                                       // tint off, and the blade's own colour back
            ChargeTint.Clear();
            SolarGlow.Hide();                                                         // the charge is spent
            // The explosion, visual only — the blast's damage is planted below, so the burst never registers hits of
            // its own (a 0-damage burst plants no damage entry at all). The dead `explosion` effect when the cave has
            // it entered; the thrown-gem burst when it has not (a floor too tight to carve its region refuses it, or
            // the swap had not landed yet).
            var fx = WantedShot();
            bool borrowed = fx != null && BorrowedShots.Burst(fx, x, h, y, 0, ExplosionScale);
            if (!borrowed) GemBurst.Show(BurstElement, x, h, y, BurstScale, damage: 0, speedMult: BurstSpeed);
            PlantBlast(st, x, h, y);
            PlantSelfHit(st, x, h, y, bx, bz);
            return borrowed;
        }

        /// <summary>The shot effect the shared slot should hold right now: the dead `explosion` container while a
        /// charge is building, being held, or still going off; otherwise Toan's own whirlwind, so the regular sword
        /// charge keeps its swoosh. Null while Toan is not the active character or is not carrying Big Bang, so the
        /// block clears and Xiao's own borrowed shots have it back. BorrowedShots asks every tick.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.ToanId) return null;
            byte slot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
            if ((uint)slot > 9) return null;
            if (Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id + slot * WeaponHave.InventoryWeaponSlotStride) != Items.bigbang) return null;
            return _wantExplosion
                ? BorrowedShots.CustomConfig(ExplosionTemplate, ExplosionName, 0, -1, -1, -1)   // one KEY: a muzzle burst, nothing after
                : BorrowedShots.DunConfig(ShotEffectPack.WhirlwindCfg, WhirlwindName);
        }

        /// <summary>The equipped weapon's melee hit-point reach, from the static weapon table.</summary>
        private static float BlastReach()
        {
            foreach (var w in ToanWeapons.All)
                if (w.Id == Items.bigbang) return w.Dcol1 ?? DefaultReach;
            return DefaultReach;
        }

        /// <summary>Toan's facing, read the way the engine reads it: getCharacterVector (main 0x1D41A0) takes the
        /// MODEL ROOT frame's euler Y and rotates the constant (0,0,1) by it, so forward is (sin yaw, 0, cos yaw).
        /// GetRotation__6CFrame only trusts that cache while <see cref="CFrameVu1.EulerValid"/> is 0, so the CObject
        /// euler at CCharacter+0x64 is the fallback. ⚠ The two are DIFFERENT fields: they differ by the model's bind
        /// rotation, and using the CObject one put the blast nowhere near ahead of him.</summary>
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

        /// <summary>The slight white Toan carries while the charge is held: the same ambient-add field the charge ramp
        /// uses, re-asserted each tick so a status tint or a character swap cannot leave it stuck on.</summary>
        private static void HoldPrimedTint(float k) =>
            Memory.WriteVec3(CCharacter.Base + CCharacter.CharaTint, PrimedTint * k, PrimedTint * k, PrimedTint * k);

        /// <summary>A player-attack sphere ON EACH ENEMY within <see cref="BlastRadius"/> of the blast: base = the
        /// weapon's full attack, the sword's selected element as a pure bit, and a melee kick originating at the blast
        /// so each one is shoved outward from it.
        ///
        /// ⚠ NOT one big sphere. A collision entry is CONSUMED by the first victim the engine matches it against, so a
        /// single blast-sized sphere damages exactly one enemy and leaves the rest untouched. One small sphere centred
        /// on each enemy hits all of them, and the pool holds 96 entries against at most 16 enemies.</summary>
        private static void PlantBlast(BlastState st, float x, float h, float y)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int baseDmg = Math.Max(1, (int)Math.Round(Player.Weapon.GetCurrentWeaponAttack() * DamageFraction));
            uint elem = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
            uint attr = (elem != 0 && (elem & (elem - 1)) == 0) ? elem : 0u;
            int hit = 0, missed = 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                if (!Enemies.IsLive(s)) continue;
                float ex = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationX));
                float ey = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationY));
                float eh = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.LocationZ));
                if (Math.Sqrt((ex - x) * (ex - x) + (ey - y) * (ey - y)) > BlastRadius) continue;
                int slot = CollisionPool.TakeFreeSlot(pool);
                if (slot < 0) { missed++; continue; }
                byte[] e = CollisionPool.PlayerHitEntry(ex, eh, ey, PerEnemyRadius, baseDmg, attr);
                void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
                F(0x80, x); F(0x84, h); F(0x88, y);                    // kick origin is the blast: everyone is shoved AWAY from it
                F(0x90, KickStrength); F(0x94, KickDecay);
                BitConverter.GetBytes(KickTypeMelee).CopyTo(e, 0x98);
                CollisionPool.Plant(pool, slot, e);
                st.planted.Add((slot, HitLifeTicks));
                hit++;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] detonation at ({x:F0},{h:F0},{y:F0}) r={BlastRadius:F0}: {hit} enemies struck for base {baseDmg}, attr 0x{attr:X}"
                + (missed > 0 ? $" ({missed} missed — pool full)" : ""));
        }

        /// <summary>Toan's own share of the blast: one sphere that hurts the PLAYER, centred on the explosion, with the
        /// knockdown reaction — so the engine spins him to the blow direction, plays the big-damage stagger and stuns
        /// him, exactly as a Rockanoff hit does. The damage handler ignores further hits while a reaction is running,
        /// so the entry cannot re-hit him.
        ///
        /// ⚠ The direction is the entry's BLOW DIRECTION (<see cref="CollisionPool.BlowDir"/>), NOT the kick words at
        /// +0x80..+0x98. BtCheckDamageProc copies +0x20 into a scratch and hands it to unitBlowActionRot; it never
        /// reads the kick words, which belong to the enemy path. Both builders default that vector to (1,0,0), a
        /// fixed WORLD bearing — which is why every detonation threw him the same way on the map, and so looked
        /// random from behind him.</summary>
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

        /// <summary>The engine withdraws its own swing spheres when the swing ends; ours are withdrawn here.</summary>
        private static void ExpireHits(BlastState st)
        {
            if (st.planted.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = st.planted.Count - 1; i >= 0; i--)
            {
                var (slot, ticks) = st.planted[i];
                if (--ticks > 0) { st.planted[i] = (slot, ticks); continue; }
                if (pool != 0) CollisionPool.Deactivate(pool, slot);
                st.planted.RemoveAt(i);
            }
        }

        private static void Reset(BlastState st)
        {
            SolarBlade.Clear();
            ChargeTint.Clear();
            SolarGlow.Hide();
            _wantExplosion = false;
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in st.planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            st.planted.Clear();
            st.phase = Phase.Idle;
        }
    }
}
