using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Big Bang "Detonate" (docs/big-bang.md): Solar Harvest and Solar Flash from the Sun Sword line, a
    /// whirlwind that IS the falloff blast at Toan's feet, and a guard charge that — locked on — hangs a judgement blade
    /// (<see cref="JudgementBlade"/>) over the target and drops it; the landing is the flash, the blast, every enemy
    /// turned to it and the weapon-HP bill. While the blade is held the game's explosions cannot hurt him
    /// (<see cref="ExplosionImmunity"/>). Dungeon only.</summary>
    internal static class BigBang
    {
        // ── Big Bang "Detonate" ────────────────────────────────────────────────────────────
        internal const int   TickMs           = 30;
        // THE WHIRLWIND is the detonation: as the spin begins, the SAME blast the dropped blade makes goes off at Toan's
        // feet (PlantFalloff — the falloff steps, elementless, the kick, every enemy turned to it), and the spin's own hit
        // sphere is taken out of reach so nothing is struck twice. (The lunge and the combo are ordinary swings; the
        // judgement blade is the other blast.)
        private const float  WhirlNoHit       = -1000f; // the whirl's own hit radius while it is armed: no enemy is inside it
        // ── the whirl's model ────────────────────────────────────────────────────────────
        // Toan's whirlwind visual IS the main-character effect instance — the same instance BorrowedShots borrows
        // for Xiao (hers sits idle holding an unused mgan01, his holds c01_fuusya). Seeding it with
        // `dun/effect/explosion.chr` therefore REPLACES the whirl rather than adding to it, and the engine fires it on
        // the spin by itself. No ISO patch: CustomConfig names any container on the disc.
        //
        // The container is a full pack — explosion.cfg (VERTEX_ANIME, BODY_SIZE 17,7,60, the same as c01_fuusya's),
        // explosion.mds (root frame "null3"), explosion.mot (one motion, KEY 5-30 at speed 0.2) and explosion.img.
        // Only the MUZZLE phase names a motion, matching the shape of the whirl's own config (c01_fuusya is "muzzle
        // motion 0, nothing after"); a motion on any other phase plays the explosion again at that phase.
        private const int    ExplosionTemplate = 5;     // a stock config's shape; the name and motions are replaced
        private const string ExplosionName     = "explosion";
        private const float  ExplosionScale    = 1.0f;  // the size it was authored at
        // Its root frame, as CFrameVu1.Name reads it: "null" then "3". The fuusya path keys on "kiru" and checks the
        // NEXT frame for "fkiri", so it cannot validate this model — hence the separate scale pass below, and
        // Weapons.WhirlScaleStandDown while this is the model in the instance.
        private const uint   ExplosionRootWord = 0x6C6C756E, ExplosionRootTail = 0x00000033;
        private static BorrowedEffect _explosion;
        private static readonly float[] _burstBind = new float[9];
        private static bool _burstBindRead;
        // ── the whirl's hit sphere is a data word ────────────────────────────────────────
        // ToanKey_Play built its two charge-attack hit radii as baked immediates; the ISO patch
        // (ElfToanMeleePatches.PatchChargeHitRadius) turned them into the data words CodeCaves.ChargeHitRadius, so the
        // whirl's word is what ArmSwing writes out of reach while the charge is up (the blast at his feet is the whirl's
        // only hit). The lunge's word is held at its vanilla 6.

        private const float  BlastWhp         = 20f;    // weapon HP a blast costs — the whirlwind's or the dropped blade's — before Endurance scales it (WeaponWhp: the engine's own drain takes it, and breaks the blade at 0)
        // THE BLADE ON A REGULAR CHARGE. Toan's charge meter runs 1.0 → 3.0 (lunge at 1.5, whirlwind at 2.5), and
        // the blade whitens across it exactly as it does for a guard charge — the same tint, driven by the meter
        // instead of by held time. It stands aside while SunSword.FlashArmed: Solar Flash owns the blade then, and
        // two ramps fighting over one mesh would only flicker.
        private const float  ChargeMeterFloor = 1.0f;
        // The whirl's hit radius is ARMED from the moment the meter reaches whirlwind range, so the spin's own hit is
        // out of reach before its first damage frame.
        private const float  WhirlThreshold   = 2.5f;   // the meter at which the charge becomes a whirlwind

        // ⚠ SHARED ELF WORDS, held only while the charge is up: the whirl's hit radius (CodeCaves.ChargeHitRadius) is
        // every charge attack's, and RestoreSwing puts the stock figure back on the spend, a dropped charge, a swap and
        // a floor change. The blast's own kick rides its hit entries (PlantFalloff), not the shared kick words.
        // The landing's VISUAL is explosion.chr (the whirlwind's own container) burst through the borrowed-shot instance
        // at the hit point; until it is entered on a floor, the thrown-gem ICE burst from the always-resident Maseki pool
        // stands in, scaled up and slowed to fit. The scale is visual only: it does not change what the blast HITS (that
        // is the falloff's hit entries).
        // ── what an auto-guarded explosion feels like ────────────────────────────────────────
        // The cave (ElfDamagePatches.PatchAutoGuardMatch) makes the engine forget the hit entirely, which is what
        // keeps Toan's charge alive, and ticks a counter; the mod answers it here (AnswerAutoGuard) with the controller
        // shove the engine itself uses on a hit (its own are motor 1 at 0xE6/22 frames for a knockdown, 0xDC/12 for a
        // lighter one — this sits under both) and the guard clang. No flinch.
        private const int    GuardRumble      = 0xC0, GuardRumbleFrames = 10;
        private const ushort GuardSe          = 0xA2;   // the engine's own guard-clang SE
        private static int   _guardSignal = -1;

        /// <summary>Big Bang as the judgement blade's owner (<see cref="JudgementBlade"/>): every enemy turned to watch it
        /// fall, the room darkened along the whole fall, and <see cref="LandBigBang"/> where it lands.</summary>
        internal static readonly JudgementBlade.JudgementOwner BigBangOwner = new JudgementBlade.JudgementOwner
        { WeaponId = Items.bigbang, Glow = ToanGlowBakes.BlueName, Profile = SunSword.BigBangFlash, Redirect = true, RampWholeFall = true, Land = LandBigBang };

        // ⚠ The ISO patch this ability depends on, as the patched instruction reads: `lui $2,0x01FB`
        // (ElfToanMeleePatches.PatchChargeHitRadius). Checked once per floor: without it the radius words are never
        // read and the charge attacks keep their stock 6 and 12, so the whirl connects on top of the blast.
        private const long   LungeRadiusInsn  = 0x20241AC0;
        private const uint   LungeRadiusPatched = 0x3C0201FB, LungeRadiusVanillaInsn = 0x3C0240C0;
        private const float  BurstScale       = 10.0f;
        private const float  BurstSpeed       = 0.6f;
        private const int    BurstElement     = MasekiEffect.Ice;    // the ANIMATION only — the fallback when explosion.chr is not entered
        private const float  BurstMul         = 1.5f;                // the blast's explosion.chr, over the whirl's ExplosionScale
        private sealed class BlastState
        {
            public byte floor = 0xFF;
            public bool bladeLogged;                    // this floor's blade state has been written to the log once
            public bool crushing;                       // the guard break is currently driven on
            public bool tinted;                         // the blade is carrying this ability's charge tint
            public bool swingArmed;                     // the whirl's own hit radius is overridden (out of reach)
            public int  chargeAction;                   // the whirlwind that has already been billed (0 = none)
            public bool patchChecked;                   // the radius patch has been verified this floor
            public int  hp = -1;                        // Toan's HP as of the last tick, for the damage probe
        }

        /// <summary>
        /// Ability Name: Detonate (Big Bang)
        /// Big Bang's WHIRLWIND is an explosion. The blade whitens as the meter fills, and the level-2 charge it
        /// becomes is the blast — the very blast the dropped judgement blade makes, at his feet: the falloff steps of
        /// the weapon's attack by distance (<see cref="BlastFalloff.Falloff"/>), no element so no resistance blunts it, guards crushed,
        /// everything thrown clear and turned to it; its model IS explosion.chr, and the spin's own hit is put out of
        /// reach so nothing is struck twice. The blade pays <see cref="BlastWhp"/> weapon HP per blast (whirlwind or
        /// drop) — the flash alone SunSword.FlashWhp. The lunge and the combo are ordinary swings. Dungeon only.
        ///
        /// While the blade is held, explosions cannot hurt Toan: the four shot configs that ARE the explosions are
        /// given a reaction the player's damage handler does not act on (see ExplosionImmunity.ExplosionCfgs).
        ///
        /// Big Bang's GUARD charge is Solar Flash, inherited from the Sun Sword it grows out of and struck at twice
        /// that sword's share (SunSword.BigBangFlash) — so the two charges are different abilities on one blade.
        ///
        /// ⚠ Nothing here detects a hit: the blast is PlantFalloff's hit entries, and the whirl's own sphere is a data
        /// word written out of reach. Every write this loop makes is idempotent, so no part of the ability depends on
        /// when a tick lands.
        /// </summary>
        public static void DetonateEffect()
        {
            var st = new BlastState();
            Memory.WriteInt(CodeCaves.NameHide, 0);                // the name-plate gate open, whatever a past run left
            while (Player.Weapon.GetCurrentWeaponId() == Items.bigbang && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                try { Tick(st); }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[BigBang] Detonate tick error: " + ex.Message); }
            }
            Reset(st);
        }

        /// <summary>DIAGNOSTIC, once per floor: what the engine has for the sword in his hand — the weapon object, its
        /// model root, the blade visual and the vtable it draws through, the object's opacity and dim, and whether the
        /// blade copy's chara slot was left registered — the reference for a blade that fails to draw on entering a floor
        /// (docs/big-bang.md). False until the weapon object exists.</summary>
        private static bool LogBlade()
        {
            uint obj = Memory.ReadGuestPtr(EquippedWeapon.WeaponObjGlobal);
            if (!Memory.IsValidGuest(obj)) return false;
            long o = Memory.ToMmu(obj);
            uint root = Memory.ReadGuestPtr(o + 0xBC);
            if (!Memory.IsValidGuest(root)) return false;
            uint vis = 0, vt = 0;
            for (uint n = root; Memory.IsValidGuest(n) && vis == 0; n = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.RootChild))
                vis = Memory.ReadGuestPtr(Memory.ToMmu(n) + CFrameVu1.GeomPtr);
            if (vis != 0) vt = Memory.ReadGuestPtr(Memory.ToMmu(vis) + CVisualMDT.VisVtable);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[BigBang] blade: obj 0x{obj:X} root 0x{root:X} visual 0x{vis:X} vtable 0x{vt:X}"
                + $" opacity {Memory.ReadFloat(o + CCharacter.NpcOpacity):F0} dim {Memory.ReadFloat(o + CCharacter.DimFactor):F2}"
                + $" | copy slot reg {Memory.ReadInt(DungeonCharaDraw.CharaRegistry + 3 * 4)} active {Memory.ReadInt(DungeonCharaDraw.CharaArray + 3 * DungeonCharaDraw.CharaStride + DungeonCharaDraw.CharaActive)} prop {BladeProp.Active}");
            return true;
        }

        private static void Tick(BlastState st)
        {
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != st.floor) { if (st.floor != 0xFF) Reset(st); st.floor = floor; st.bladeLogged = false; EnemyFacing._yawConv = -1; }
            if (DebugDiagnostics.Enabled && !st.bladeLogged) st.bladeLogged = LogBlade();
            ToanLockOn.HoldReach("[BigBang] ");                                       // the Cross Hinder's reach, inherited
            EnemyFacing.FaceTick();

            BlastFalloff.ExpireShells();
            EnemyFacing.ReleaseRedirectWhenDue();

            if (Player.CheckDunIsPausedOrMenu()) return;
            if (Player.CurrentCharacterNum() != Player.ToanId) { ClearTint(st); RestoreSwing(st); return; }

            if (ExplosionSeeded) MaintainExplosionScale();
            ExplosionImmunity.ArmImmunity();
            AnswerAutoGuard();
            JudgementBlade.JudgementTick(BigBangOwner);
            if (!st.patchChecked)
            {
                st.patchChecked = true;
                uint insn = Memory.ReadUInt(LungeRadiusInsn);
                if (insn != LungeRadiusPatched)
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[BigBang] ⚠ the charge-radius ISO patch is NOT applied (0x{insn:X8}"
                        + (insn == LungeRadiusVanillaInsn ? ", still vanilla" : "") +
                        ") — the charge attacks keep their stock 6 / 12 reach and the blast will do nothing");
                else
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"[BigBang] charge radii are data: lunge {Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusLunge):F0}"
                        + $", whirl {Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusWhirl):F0}");
            }

            if (DebugDiagnostics.Enabled) ProbeDamage(st);
            int   action = Memory.ReadInt(PlayerAction.ChargeActionState);
            float meter  = Memory.ReadFloat(PlayerAction.ChargeMeter);
            bool  whirl  = action == PlayerAction.ActionWhirlwind;

            // The blade whitens across the regular charge exactly as it does across a guard charge — the same tint,
            // driven by the meter instead of by held time. It stands aside while Solar Flash owns the blade.
            if (action == PlayerAction.ActionWindup && !SunSword.FlashArmed)
            {
                float k = (meter - ChargeMeterFloor) / (PlayerAction.ChargeMeterCap - ChargeMeterFloor);
                SolarBlade.Set(Math.Min(1f, Math.Max(0f, k)), SolarBlade.BigBangModel,
                               SolarBlade.BigBangBladeFrame, SolarBlade.BigBangGlowFrame);
                st.tinted = true;
            }
            else if (st.tinted && !whirl) ClearTint(st);

            // Armed from the moment the meter reaches whirlwind range, so the spin's own hit is already out of reach
            // before its first damage frame. Nothing below depends on WHEN a tick lands: every write is the same value.
            if (!SunSword.FlashArmed && (whirl || (action == PlayerAction.ActionWindup && meter >= WhirlThreshold)))
                ArmSwing(st);
            else
                RestoreSwing(st);

            // Guards are crushed while the whirl spins: a blocked spin would eat the detonation.
            if (whirl != st.crushing) { GuardGate.NobodyBlocks(whirl); st.crushing = whirl; }

            // The blast, once per whirlwind, as the spin begins: the dropped blade's blast at his feet, and its bill.
            if (whirl && action != st.chargeAction)
            {
                st.chargeAction = action;
                float x = Memory.ReadFloat(Addresses.dunPositionX), h = Memory.ReadFloat(Addresses.dunPositionZ), y = Memory.ReadFloat(Addresses.dunPositionY);
                BlastFalloff.LastBlast = (x, h, y);
                BlastFalloff.PlantFalloff(x, h, y);
                EnemyFacing.TurnEnemiesToward(x, y);
                DrainWhp();
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"[BigBang] whirlwind blast at ({x:F0},{h:F0},{y:F0}); the spin's own hit at {Memory.ReadFloat(CodeCaves.ChargeHitRadius + CodeCaves.ChargeRadiusWhirl):F0}, flash {SunSword.FlashArmed}");
            }
            else if (!whirl) st.chargeAction = 0;
        }

        /// <summary>The whirl's own hit sphere put out of reach (the blast at his feet is the whirl's damage; a spin that
        /// also struck would hit an enemy twice). The lunge's word stays at its vanilla 6.</summary>
        private static void ArmSwing(BlastState st)
        {
            st.swingArmed = true;
            Weapons.SetChargeHitRadii(CodeCaves.LungeRadiusVanilla, WhirlNoHit);
        }

        /// <summary>The stock 6 / 12 back. The words are global and every weapon's charge reads them.</summary>
        private static void RestoreSwing(BlastState st)
        {
            if (!st.swingArmed) return;
            Weapons.SeedChargeHitRadii();
            st.swingArmed = false;
        }

        /// <summary>The effect this weapon wants in the main-character instance: explosion.chr, in place of Toan's
        /// whirlwind, whenever Big Bang is the blade in his hands. Handed to BorrowedShots.Start as a provider, so
        /// the cave re-enters it whenever the floor loader has refilled the instance — nothing per tick.
        ///
        /// Every phase radius is zeroed: the effect is the VISUAL only, and a phase with a radius plants a damage
        /// entry of its own every frame, which would double up on the spheres this ability plants itself.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (Player.CurrentCharacterNum() != Player.ToanId) return null;
            if (Player.Weapon.GetCurrentWeaponId() != Items.bigbang) return null;
            if (_explosion == null)
            {
                _explosion = BorrowedShots.CustomConfig(ExplosionTemplate, ExplosionName,
                                                        muzzleMotion: 0, flyMotion: -1, impactMotion: -1, expireMotion: -1);
                if (_explosion == null) return null;
                for (int phase = 0; phase < 4; phase++) BorrowedShots.SetPhaseRadius(_explosion, phase, 0f);
            }
            return _explosion;
        }

        /// <summary>True while the instance actually holds explosion.chr — which is when the stock whirl scaler must
        /// stand down, since the model it validates ("kiru" + "fkiri") is not the one loaded.</summary>
        internal static bool ExplosionSeeded => _explosion != null && BorrowedShots.Entered(_explosion);

        /// <summary>The blast's VISUAL at a point: explosion.chr — the whirlwind's own container, played where it
        /// stands at the whirl's <see cref="ExplosionScale"/> with no damage (the blast's damage is the hit entries).
        /// The ice gem burst stands in until the config is entered on this floor.</summary>
        private static void Burst(float x, float h, float y)
        {
            if (ExplosionSeeded && BorrowedShots.Burst(_explosion, x, h, y, 0, ExplosionScale))
            {
                _burstSlot = Memory.ReadInt(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffLastIdx);   // the sub-shot it took
                MaintainExplosionScale();                                                                  // its size before its first frame
                return;
            }
            GemBurst.Show(BurstElement, x, h, y, BurstScale, damage: 0, speedMult: BurstSpeed);
        }
        private static int _burstSlot = -1;
        /// <summary>The blast's sub-shot is still playing — its model is drawn at <see cref="BurstMul"/> meanwhile.</summary>
        private static bool BurstLive(int slot) =>
            slot == _burstSlot && slot >= 0
            && Memory.ReadUShort(ShotEffectPack.CharaMainEffect + ShotEffectPack.OffActive + slot * 2) != 0;

        /// <summary>Hold explosion.chr at <see cref="ExplosionScale"/>. Same approach as the stock whirl scaler and
        /// for the same reason — a VERTEX_ANIME mesh is only transformed by its ROOT frame's local matrix, so the
        /// 3x3 at +0x1D0 is scaled and the translation row left anchored — but validated on this model's own root
        /// name. Every pool slot is kept scaled, not just the live one: a spin can activate any of them, and the
        /// idle copies sit at the origin where writing costs nothing.</summary>
        private static void MaintainExplosionScale()
        {
            if (!Player.CheckDunIsWalkingMode()) return;          // models are reallocated in menus and on transitions
            for (int slot = 0; slot < ShotEffectPool.EffectSlotCount; slot++)
            {
                uint ptr = Memory.ReadGuestPtr(ShotEffectPool.MainCharaEffectBase
                                               + ShotEffectPool.EffectSlotModelOff + (long)slot * ShotEffectPool.EffectSlotStride);
                if (!Memory.IsValidGuest(ptr)) continue;
                long root = Memory.ToMmu(ptr);
                if (Memory.ReadUInt(root + CFrameVu1.Name) != ExplosionRootWord) continue;
                if (Memory.ReadUInt(root + CFrameVu1.Name + 4) != ExplosionRootTail) continue;
                if (!_burstBindRead)
                {
                    for (int k = 0; k < 9; k++) _burstBind[k] = Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[k]);
                    if (Math.Abs(_burstBind[0]) < 0.05f || Math.Abs(_burstBind[0]) > 4.0f) return;   // already scaled, or a bad read
                    _burstBindRead = true;
                }
                float scale = ExplosionScale * (BurstLive(slot) ? BurstMul : 1f);   // the whirl's size, or the blast's while it plays
                if (Math.Abs(Memory.ReadFloat(root + ShotEffectPool.CFrameLocal3x3[0]) - _burstBind[0] * scale) <= 0.01f) continue;
                for (int k = 0; k < 9; k++)
                    Memory.WriteFloat(root + ShotEffectPool.CFrameLocal3x3[k], _burstBind[k] * scale);
                Memory.WriteInt(root + CFrameVu1.WorldCacheA, 0);
            }
        }

        /// <summary>DIAGNOSTIC. When Toan loses HP, report every sphere in the pool that can hurt him, with the
        /// fields that decide what it does. An explosion that still damages him while its config reads reaction 5
        /// is either not the entry we changed — the reaction is taken from the config at plant time, so a live entry
        /// shows what really arrived — or not an entry at all, in which case nothing here will be holding the damage
        /// and the HP is being taken by code that never touches the collision pool.</summary>
        private static void ProbeDamage(BlastState st)
        {
            int hp = Memory.ReadShort(DngStatusData.Base + 0x12 + Player.ToanId * 2);
            int was = st.hp; st.hp = hp;
            if (was < 0 || hp >= was) return;

            long pool = CollisionPool.Resolve();
            var seen = new List<string>();
            if (pool != 0)
                for (int i = 0; i < CollisionPool.Entries; i++)
                {
                    if (!CollisionPool.IsActive(pool, i)) continue;
                    long e = pool + (long)i * CollisionPool.Stride;
                    int mask = Memory.ReadInt(e + CollisionPool.Mask);
                    if ((mask & (int)CollisionPool.HurtsPlayerMask) == 0) continue;
                    seen.Add($"#{i} dmg {Memory.ReadInt(e + 0x34)} react {Memory.ReadInt(e + 0x4C)}"
                             + $" attr 0x{Memory.ReadInt(e + CollisionPool.Element):X} owner {Memory.ReadInt(e + CollisionPool.Owner)}"
                             + $" class {Memory.ReadFloat(e + CollisionPool.EntryClass):F0} r {Memory.ReadFloat(e + CollisionPool.Radius):F0}");
                }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] probe: HP {was} → {hp} (−{was - hp}); player-hurting spheres live: "
                + (seen.Count == 0 ? "NONE — the damage did not come through the collision pool" : string.Join(" | ", seen)));
        }

        /// <summary>Big Bang's landing: the blast, every enemy turned to look, the weapon-HP bill. The white-out and the
        /// burst on the SAME frame, from here: Solar Flash's own tick runs the rest of the flash (the light hit, the
        /// blinding, the blade and glow) once TakeDropLanded hands it the landing — and that is after the hit entries
        /// and the turns below, well past a frame — so the lighting write goes out now, and Solar Flash is told it is
        /// lit (a second Flash there restores the floor's light and whites it again).</summary>
        private static void LandBigBang(int slot, float x, float h, float y)
        {
            SunSword.BigBangFlash.ArmLighting();
            SolarLighting.Flash();
            Burst(x, h, y);
            BlastFalloff.PlantFalloff(x, h, y, guardBreak: true);      // the judgement blade's landing crushes any guard (the ISO's guard gate)
            EnemyFacing.TurnEnemiesToward(x, y);
            DrainWhp();
        }

        /// <summary>Answer a hit the cave swallowed: rumble and the guard clang. The cave only ticks a counter —
        /// everything the player actually feels is here, where it can be tuned without touching MIPS.
        /// The count is compared, never zeroed, so two ticks between polls still read as one answer and nothing is
        /// lost if the mod starts mid-floor.</summary>
        private static void AnswerAutoGuard()
        {
            int n = Memory.ReadInt(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardCount);
            int was = _guardSignal; _guardSignal = n;
            if (was < 0 || n == was) return;

            GamePad.Rumble(GuardRumble, GuardRumbleFrames);
            SeSeq.Play(GuardSe, 90);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"[BigBang] explosion guarded at ({Memory.ReadFloat(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardX):F0},"
                + $"{Memory.ReadFloat(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardH):F0},"
                + $"{Memory.ReadFloat(CodeCaves.AutoGuardSignal + CodeCaves.AutoGuardY):F0})");
        }

        /// <summary>The charge tint off the blade — but only ours. Solar Flash drives the same mesh from its own
        /// thread, so a tint it is holding is left alone.</summary>
        private static void ClearTint(BlastState st)
        {
            if (!st.tinted) return;
            st.tinted = false;
            if (!SunSword.FlashArmed) SolarBlade.Clear();
        }

        /// <summary>Weapon HP for a blast: <see cref="BlastWhp"/>, taken by the engine's own drain (<see cref="WeaponWhp"/>),
        /// which breaks the blade at 0 the engine's way.</summary>
        private static void DrainWhp() => WeaponWhp.Drain(Items.bigbang, BlastWhp, "[BigBang] detonation ");

        private static void Reset(BlastState st)
        {
            ClearTint(st);
            RestoreSwing(st);          // stats, kick constants and the charge radii
            ExplosionImmunity.RestoreImmunity();   // ⚠ shared ELF data: never leave the explosions inert
            JudgementBlade.ReleaseJudgement();
            ToanLockOn.ReleaseReach(); EnemyFacing._faceHold = 0; EnemyFacing.ReleaseRedirect();
            BlastFalloff._shells.WithdrawAll();                                  // the blast's entries withdrawn, their marks cleared, as ExpireShells does
            if (st.crushing) { GuardGate.NobodyBlocks(false); st.crushing = false; }
            st.chargeAction = 0;
        }
    }
}
