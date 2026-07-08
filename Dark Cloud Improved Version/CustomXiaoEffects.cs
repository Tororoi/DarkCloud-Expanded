using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    public class CustomXiaoEffects
    {

        // ── Angel Gear ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Triggers Angel Gear effect: Applies the Heal regeneration effect to all allies
        /// </summary>
        public static void AngelGearEffect()
        {
            //Initialize variables
            ushort HpValueAdd = 1;
            ushort Delay = 5000;
            ushort XiaoHp = 0;
            ushort XiaoMaxHp = 0;
            bool isHealXiao = false;

            //Run while Angel Gear is equipped and Player is in valid state
            while (Player.Weapon.GetCurrentWeaponId() == Items.angelgear &&
                    !Player.CheckDunIsInteracting() &&
                    !Player.CheckDunIsOpeningChest() &&
                    !Player.CheckDunIsPaused() &&
                    Player.CheckDunIsWalkingMode())
            {
                //Fetch HP values for characters
                ushort ToanHp = Player.Toan.GetHp();
                ushort ToanMaxHp = Player.Toan.GetMaxHp();
                ushort GoroHp = Player.Goro.GetHp();
                ushort GoroMaxHp = Player.Goro.GetMaxHp();
                ushort RubyHp = Player.Ruby.GetHp();
                ushort RubyMaxHp = Player.Ruby.GetMaxHp();
                ushort UngagaHp = Player.Ungaga.GetHp();
                ushort UngagaMaxHp = Player.Ungaga.GetMaxHp();
                ushort OsmondHp = Player.Osmond.GetHp();
                ushort OsmondMaxHp = Player.Osmond.GetMaxHp();

                //Check for the Heal special attribute on the weapon
                if (Player.Weapon.GetCurrentWeaponSpecial2() % 16 < 8 ||
                    Player.Weapon.GetCurrentWeaponSpecial2() % 16 > 11)
                {
                    isHealXiao = true;
                    XiaoHp = Player.Xiao.GetHp();
                    XiaoMaxHp = Player.Xiao.GetMaxHp();
                }

                //Add the HP value to the characters current HP
                if (ToanHp < ToanMaxHp && ToanHp > 0) Player.Toan.SetHp((ushort)(ToanHp + HpValueAdd));
                //Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Toan HP add: " + (ToanHp + HpValueAdd));
                if (GoroHp < GoroMaxHp && GoroHp > 0) Player.Goro.SetHp((ushort)(GoroHp + HpValueAdd));
                if (RubyHp < RubyMaxHp && RubyHp > 0) Player.Ruby.SetHp((ushort)(RubyHp + HpValueAdd));
                if (UngagaHp < UngagaMaxHp && UngagaHp > 0) Player.Ungaga.SetHp((ushort)(UngagaHp + HpValueAdd));
                if (OsmondHp < OsmondMaxHp && OsmondHp > 0) Player.Osmond.SetHp((ushort)(OsmondHp + HpValueAdd));

                //Only affect Xiao if Angel Gear does not have the Heal attribute already
                if (isHealXiao && XiaoHp < XiaoMaxHp && XiaoHp > 0) Player.Xiao.SetHp((ushort)(XiaoHp + HpValueAdd));

                //Wait in between additions
                Thread.Sleep(Delay);
            }
        }

        // ── Super Steve "Sphere Inheritance" ───────────────────────────────────────────────
        /// <summary>
        /// Super Steve (Xiao's ultimate slingshot) inherits the custom effect of every weapon
        /// whose SynthSphere is attached to it. A weapon Status-Broken into a SynthSphere records
        /// its SOURCE weapon id at the attach-entry's +0x02 (SetStatusBreak, ELF 0x2368D0), and
        /// attaching copies the whole 0x20-byte entry into the weapon record's ATTACH_LIST
        /// (rec+0x28+slot*0x20), so the source id survives in-record and can be read straight off
        /// Super Steve's six slots — no stat-fingerprinting needed.
        ///
        /// Enemy-side abilities reuse the Toan drivers verbatim (they only touch enemy data, so
        /// they work for whoever is active): a 7th Heaven sphere grants Divine Guard AND Guard
        /// Crush (mirroring the 7th Heaven → Dark Cloud lineage), a Dark Cloud sphere grants Guard
        /// Crush. Player-body abilities (Quick Draw, Moonlit Focus, Heaven's Cloud, the curses,
        /// Defensive Legacy, …) need Xiao-specific adaptations and are added incrementally.
        /// </summary>
        private const int AgasDefenseBoost = 15;   // Defensive Legacy — mirrors CustomToanEffects.AgasSwordEffect
        private static bool _ssAgasApplied;        // whether the +15 defense is currently on Xiao

        public static void SuperSteveEffect()
        {
            var ssSun = new CustomToanEffects.SunHarvestState(EnemyAddresses.FloorSlots.Count);
            var xiaoCurse = new CustomToanEffects.CurseAddrs(Player.Xiao.status, Player.Xiao.statusTimer, Player.Xiao.hp);
            var ssEvilcise = new CustomToanEffects.CurseState();
            var ssManeater = new CustomToanEffects.CurseState();
            while (Player.InDungeonFloor())
            {
                int ch = Player.CurrentCharacterNum();
                if (ch != Player.XiaoId) break;
                int equipSlot = Memory.ReadByte(WeaponCollision.AbsRollover.UserStatusBase +
                                                WeaponCollision.AbsRollover.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) break;
                long rec = WeaponCollision.AbsRollover.RecordAddr(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) break;

                HashSet<int> spheres = SuperSteveSphereSources(rec);
                bool active = !Player.CheckDunIsPaused();

                // Divine Guard + Guard Crush (7th Heaven carries Dark Cloud's Guard Crush by lineage).
                bool divineGuard = spheres.Contains(Items.seventhheaven);
                bool guardCrush  = divineGuard || spheres.Contains(Items.darkcloud);
                CustomToanEffects.SeventhHeavenSoftenAttacks(active && divineGuard);
                CustomToanEffects.DarkCloudDriveGuards(active && guardCrush);

                // Defensive Legacy (Aga's Sword): +15 defense to Xiao, balanced add/remove.
                bool wantAgas = active && spheres.Contains(Items.agassword);
                if (wantAgas && !_ssAgasApplied)
                { Player.Xiao.SetDefense(Player.Xiao.GetDefense() + AgasDefenseBoost); _ssAgasApplied = true; }
                else if (!wantAgas && _ssAgasApplied)
                { Player.Xiao.SetDefense(Player.Xiao.GetDefense() - AgasDefenseBoost); _ssAgasApplied = false; }

                // Hero's Courage (Brave Ark): clear Freeze/Poison/Curse/Goo from Xiao each tick.
                if (active && spheres.Contains(Items.braveark))
                {
                    const ushort resistMask = ToanState.StatusFreeze | ToanState.StatusPoison |
                                              ToanState.StatusCurse  | ToanState.StatusGoo;
                    ushort status = Memory.ReadUShort(Player.Xiao.status);
                    if ((status & resistMask) != 0)
                        Memory.WriteUShort(Player.Xiao.status, (ushort)(status & ~resistMask));
                }

                // Bone Rapier: enable the bone-door bypass while its sphere is attached (the Xiao
                // dispatcher no longer force-clears it for Super Steve, so this owns the flag).
                CustomToanEffects.BoneRapierEffect(active && spheres.Contains(Items.bonerapier));

                // Solar Harvest (Sun Sword / Big Bang): ~1% of the floor's enemies drop a Sun attachment.
                bool hasSun = spheres.Contains(Items.sunsword) || spheres.Contains(Items.bigbang);
                CustomToanEffects.SunHarvestDrive(hasSun, ssSun);

                // Curses (full inherit): curse Xiao. Not gated on pause — mirrors the Toan loops.
                CustomToanEffects.EvilciseDrive(spheres.Contains(Items.evilcise), xiaoCurse, ssEvilcise);
                CustomToanEffects.ManeaterDrive(spheres.Contains(Items.maneater), xiaoCurse, rec, ssManeater);

                // Moonlit Focus (Tsukikage / Heaven's Cloud sphere): 2× pellet speed.
                bool hasMoonlit = spheres.Contains(Items.tsukikage) || spheres.Contains(Items.heavenscloud);
                MoonlitFocusDrive(active && hasMoonlit);

                Thread.Sleep(16);
            }

            // Restore everything on unequip / character-switch / dungeon exit (no-ops if not driven).
            CustomToanEffects.SeventhHeavenSoftenAttacks(false);
            CustomToanEffects.DarkCloudDriveGuards(false);
            CustomToanEffects.BoneRapierEffect(false);
            CustomToanEffects.SunHarvestDrive(false, ssSun);
            CustomToanEffects.EvilciseDrive(false, xiaoCurse, ssEvilcise);
            CustomToanEffects.ManeaterDrive(false, xiaoCurse, 0, ssManeater);
            MoonlitFocusDrive(false);
            if (_ssAgasApplied)
            { Player.Xiao.SetDefense(Player.Xiao.GetDefense() - AgasDefenseBoost); _ssAgasApplied = false; }
        }

        // ── Super Steve → Quick Draw (Small Sword / Tsukikage / Heaven's Cloud sphere) ─────────
        private const float XiaoShotFireFrame  = 251.0f;  // inside the (251,252) pellet-release window (shoot motion idx 13)
        private static bool _xqReleaseArmed;              // edge latch so one X-release = one instant shot
        private const ushort XiaoBattleSpeed = 350;   // effective Speed in Super Steve's BATTLE copy (past 99) → keeps the rate-of-fire gauge from being the bottleneck

        /// <summary>
        /// Tight-poll companion to <see cref="SuperSteveEffect"/>: the Quick Draw inheritance for Xiao.
        /// Xiao (controlled) shares Toan's shot plumbing — motion-frame cursor = AnimFrameCursor
        /// (0x21EA2010), shot action-state = ChargeActionState (0x21DC4494). Her shot is three c04b
        /// motions: draw (0xB, frames 240→251), a zero-speed "nocked" HOLD parked on 250 (0xC) that lasts
        /// until the fire input releases, then shoot (0xD) — the pellet leaves at frame 251.
        ///
        /// Vanilla feel is preserved: hold X to nock/aim, the shot fires on RELEASE. Quick Draw makes
        /// that release instant. Two levers, both gated on an attached Small Sword / Tsukikage / Heaven's
        /// Cloud sphere:
        ///   1. Rate-of-fire: the between-shots gauge (0x21DC44C8) fills at effectiveSpeed/30 and gates
        ///      the next shot at 100; we write Super Steve's BATTLE-copy speed (+8) past the game's 99
        ///      cap so it refills near-instantly. Menu/inventory speed is untouched (still reads 99).
        ///   2. Instant release: the moment the engine's release flag (0x21DC4498, the real X-release —
        ///      never forced) is set during the draw/hold, we jump the state straight to shoot (0xD) and
        ///      drop the cursor onto the shoot-motion start (251.0) so the pellet crosses the (251,252)
        ///      fire window exactly once. Edge-latched (<see cref="_xqReleaseArmed"/>) so one release =
        ///      one pellet. (251.5 sat inside the window and double-fired; 251.0 = the clean boundary.)
        /// The draw motion itself plays at a fixed KEY rate that speed never scales — only the gauge does
        /// (motionDrive, dun 0x1DB7450) — which is why the fix is gauge-speed + release-jump, not a
        /// motion-rate override (that steps over the 1-frame-wide fire window and desyncs).
        /// </summary>
        public static void XiaoQuickDrawEffect()
        {
            while (Player.InDungeonFloor())
            {
                int ch = Player.CurrentCharacterNum();
                if (ch != Player.XiaoId) break;
                int equipSlot = Memory.ReadByte(WeaponCollision.AbsRollover.UserStatusBase +
                                                WeaponCollision.AbsRollover.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) break;
                long rec = WeaponCollision.AbsRollover.RecordAddr(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) break;

                // Speed the rate-of-fire GAUGE: it fills at effectiveSpeed/30 (motionDrive, dun 0x1DB7450)
                // and gates the next shot at 100, so combat's effective Speed (post-clamp, at BATTLE-copy
                // +8) is the fill rate. WeaponAllValueSet caps it at 99 when it rebuilds the record; writing
                // past that here (BATTLE copy only, so the menu still reads Super Steve's real 99) removes
                // the between-shots wait. Only touch it when the battle weapon really is Super Steve.
                bool battleIsSteve = Memory.ReadUShort(WeaponCollision.BattleWeaponRecord) == Items.supersteve;
                long battleSpeedAddr = WeaponCollision.BattleWeaponRecord + WeaponCollision.EffSpeedOffset;
                bool boosted = !Player.CheckDunIsPaused() && battleIsSteve && SuperSteveHasQuickDrawSphere(rec);
                if (boosted && Memory.ReadUShort(battleSpeedAddr) != XiaoBattleSpeed)
                    Memory.WriteUShort(battleSpeedAddr, XiaoBattleSpeed);

                // Instant shot on X RELEASE: while drawing (0xB) or holding (0xC), the moment the fire
                // input is released (engine sets XiaoShotReleaseFlag), jump straight to the shoot state
                // (0xD) and drop the cursor into the fire window (251), so the pellet leaves at once no
                // matter how far the draw got. Holding still aims (nothing happens until release); a tap
                // fires almost instantly. Edge-triggered so exactly one pellet fires per release.
                int shotState = Memory.ReadInt(WeaponCollision.ChargeActionState);
                bool inDrawOrHold = shotState == WeaponCollision.XiaoShotDraw ||
                                    shotState == WeaponCollision.XiaoShotHold;
                bool released = inDrawOrHold && Memory.ReadInt(WeaponCollision.XiaoShotReleaseFlag) != 0;
                if (boosted && released && !_xqReleaseArmed)
                {
                    Memory.WriteInt(WeaponCollision.XiaoShotReleaseFlag, 1);
                    Memory.WriteInt(WeaponCollision.ChargeActionState, WeaponCollision.XiaoShotShoot);
                    Memory.WriteFloat(WeaponCollision.AnimFrameCursor, XiaoShotFireFrame);
                }
                _xqReleaseArmed = released;

                // Tight poll: latency here directly delays the shot (mirrors Toan's SmallSwordEffect).
                Thread.Sleep(8);
            }
        }

        /// <summary>True if a Small Sword / Tsukikage / Heaven's Cloud SynthSphere (the Quick Draw
        /// lineage) is attached to the weapon record at <paramref name="rec"/>.</summary>
        private static bool SuperSteveHasQuickDrawSphere(long rec)
        {
            for (int slot = 0; slot < WeaponCollision.WeaponAttachSlotCount; slot++)
            {
                long entry = rec + WeaponCollision.WeaponAttachSlot0Offset +
                             slot * WeaponCollision.WeaponAttachSlotStride;
                if (Memory.ReadUShort(entry) != WeaponCollision.AttachBoard.SynthSphereId) continue;
                int src = Memory.ReadUShort(entry + WeaponCollision.AttachBoard.EntrySourceId);
                if (src == Items.smallsword || src == Items.tsukikage || src == Items.heavenscloud) return true;
            }
            return false;
        }

        // ── Super Steve → Moonlit Focus (Tsukikage / Heaven's Cloud sphere) ────────────────────
        private static readonly bool[] _ssShotDoubled = new bool[WeaponCollision.PlayerShotPool.SlotCount];
        private static bool _ssMoonlitLogged;   // one-shot confirm log for the pool base

        /// <summary>Moonlit Focus for Xiao: doubles each newly-spawned pellet's velocity once (→ 2× shot
        /// speed). Reads the player shot pool (<see cref="WeaponCollision.PlayerShotPool"/>): for every
        /// slot that just became active (flag 0→1) it multiplies the +0x1C0 velocity vec by 2. Per-slot
        /// latch so a shot is doubled exactly once; cleared when the slot frees.</summary>
        private static void MoonlitFocusDrive(bool active)
        {
            long poolBase = (uint)Memory.ReadInt(WeaponCollision.PlayerShotPool.BasePtr);
            if (poolBase == 0 || poolBase >= 0x02000000) return;   // pool not allocated / bad pointer
            for (int i = 0; i < WeaponCollision.PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(WeaponCollision.PlayerShotPool.FlagAddr(poolBase, i)) != 0;
                if (active && live && !_ssShotDoubled[i])
                {
                    long vel = WeaponCollision.PlayerShotPool.VelAddr(poolBase, i);
                    for (int c = 0; c < 3; c++)
                        Memory.WriteFloat(vel + c * 4, Memory.ReadFloat(vel + c * 4) * 2f);
                    _ssShotDoubled[i] = true;
                    if (!_ssMoonlitLogged)
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"[Xiao Moonlit] poolBase=0x{poolBase:X} doubled slot {i}");
                        _ssMoonlitLogged = true;
                    }
                }
                else if (!live)
                {
                    _ssShotDoubled[i] = false;
                }
            }
        }

        /// <summary>Source weapon ids of every SynthSphere attached to the weapon record at
        /// <paramref name="rec"/> (reads the six in-record ATTACH_LIST slots at rec+0x28).</summary>
        private static HashSet<int> SuperSteveSphereSources(long rec)
        {
            var set = new HashSet<int>();
            for (int slot = 0; slot < WeaponCollision.WeaponAttachSlotCount; slot++)
            {
                long entry = rec + WeaponCollision.WeaponAttachSlot0Offset +
                             slot * WeaponCollision.WeaponAttachSlotStride;
                if (Memory.ReadUShort(entry) != WeaponCollision.AttachBoard.SynthSphereId) continue;
                int src = Memory.ReadUShort(entry + WeaponCollision.AttachBoard.EntrySourceId);
                if (src > 0 && src != 0xFFFF) set.Add(src);
            }
            return set;
        }

    }
}
