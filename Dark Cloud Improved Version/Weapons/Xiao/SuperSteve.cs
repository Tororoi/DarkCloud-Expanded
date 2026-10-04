using System;
using System.Threading;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Super Steve's SynthSphere inheritance — the DISPATCHER. Reads which weapon's SynthSphere is attached to Super Steve
    /// (<see cref="AttachedSphere"/>, <see cref="Wields"/>) and pulses that weapon's ability each tick from
    /// <see cref="SphereInheritanceEffect"/>, which also tears everything down when the weapon, the character or the floor goes.
    /// It owns the sphere's presentation on her: the attached weapon's icon over Steve on the dungeon HUD
    /// (<see cref="DriveSphereIcon"/>) and, for a slingshot's sphere, that slingshot's pellet sprite (<see cref="DriveSphereSprite"/>).
    /// It also carries the facts about Super Steve's dungeon rig the abilities share (<see cref="WeaponModel"/>, <see cref="GlowDisc"/>,
    /// <see cref="GlowGoldRow"/>, <see cref="GlowSize"/>).
    ///
    /// Enemy-side abilities reuse the other characters' weapon classes' drivers directly (they only touch enemy data). A
    /// sphere's XIAO adaptation of another weapon's ability is its own class under Weapons/Xiao, named after the weapon of
    /// origin: <c>&lt;Weapon&gt;Shot</c> when the adaptation is a shot (SolarShot, BigBangShot, ZeusShot), <c>&lt;Weapon&gt;Sphere</c>
    /// otherwise (SmallSwordSphere = Quick Draw, TsukikageSphere = Moonlit Focus, HeavensCloudSphere = the two-stage charge →
    /// wind-gem blast, MobiusRingSphere = Ruby's ramp, AgasSwordSphere = Defensive Legacy, BraveArkSphere = Hero's Courage).
    /// </summary>
    internal static class SuperSteve
    {
        // ── Super Steve's dungeon rig, shared by the sphere abilities: the model whitened or tinted as "the weapon", the one
        //    glow disc resident while Xiao is the active character, and how it is painted and sized ──
        internal const string GlowDisc    = "catglowp";   // the cat's disc: the one glow disc resident while Xiao is the active character
        internal const int    GlowGoldRow = 9;            // the glow cave's ONE-based palette row: 1–5 the elements, 6 none, 7 the Divine Beast blue, 8 the Angel Shooter white, 9 the Angel Gear gold — the Sun Sword's colour
        internal const string WeaponModel = "c04w13";     // Super Steve's dungeon rig (item 312 = c04w13.chr): what SolarBlade whitens
        internal const float  GlowSize = 0.75f;           // the disc wider than the ×10 stone (45 units at 1.0 — 0.4 sat behind the 20-unit sprite): on the pouch while primed at the same size, so it looks the same when it rides the pellet

        /// <summary>The source weapon id of the single SynthSphere attached to Super Steve's record at
        /// <paramref name="rec"/> (0 if none). A weapon holds at most one SynthSphere (id 0x5A), so the first
        /// one found is the only one — its source id (attach-entry +0x02) selects the inherited effect.</summary>
        internal static int AttachedSphere(long rec)
        {
            for (int slot = 0; slot < WeaponHave.WeaponAttachSlotCount; slot++)
            {
                long entry = rec + WeaponHave.WeaponAttachSlot0Offset +
                             slot * WeaponHave.WeaponAttachSlotStride;
                if (Memory.ReadUShort(entry) == AttachBoard.SynthSphereId)
                    return Memory.ReadUShort(entry + AttachBoard.EntrySourceId);
            }
            return 0;
        }

        /// <summary>Xiao is the active character with Super Steve equipped and a SynthSphere of <paramref name="weaponId"/> attached
        /// (the battle record's) — the sphere's source weapon's effect is hers.</summary>
        internal static bool Wields(int weaponId)
            => Player.CurrentCharacterNum() == Player.XiaoId
            && Player.Weapon.GetCurrentWeaponId() == Items.supersteve
            && AttachedSphere(WeaponHave.BattleWeaponRecord) == weaponId;

        // ── the attached sphere's icon, over Steve on the dungeon HUD ──
        private const int SsIconX = 29, SsIconY = 367, SsIconSize = 20;   // just above Steve's raised hands: his icon (the equipped weapon's) is at (29, 388), 32 × 32
        private static int _ssIconSphere = -1;                            // the sphere the icon is on for; -1 = nothing written yet
        private static bool _ssIconWarned;

        // ── …and its pellet, when the sphere came from a slingshot ──
        private static int _spriteSphere = -1;   // the sphere the pellet sprite was last set for (−1 = never)
        private static bool _spriteWarned;

        /// <summary>Super Steve's pellet drawn as the sphere weapon's when the sphere came from a slingshot (the pellet sprite is
        /// a per-weapon cell of basefx01, so a slingshot's sphere brings its pellet): Mailbox.PelletSpriteId = that weapon's id,
        /// read by DebugIfCave.PelletSprite at every pellet draw; 0 (vanilla) for any other sphere, or none.</summary>
        internal static void DriveSphereSprite(int sphere)
        {
            if (sphere == _spriteSphere) return;
            bool slingshot = sphere >= Items.woodenslingshot && sphere <= Items.angelgear && sphere != Items.supersteve;
            uint hook = (uint)Memory.ReadInt(0x20000000L + 0x001ABC74);
            if (slingshot && hook != Jal(DebugIfCave.PelletSprite) && hook != Jal(DebugInfoCave.PelletSprite))
            {
                if (!_spriteWarned) { _spriteWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SuperSteve] pellet-sprite hook not in this ISO — the pellet stays Super Steve's (re-patch the ISO)"); }
                return;
            }
            Memory.WriteInt(Mailbox.PelletSpriteId, slingshot ? sphere : 0);
            _spriteSphere = sphere;
            if (slingshot) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SuperSteve] pellet sprite: the sphere weapon's (item {sphere})");
        }
        private static uint Jal(uint target) => 0x0C000000u | ((target >> 2) & 0x03FFFFFF);

        /// <summary>Switch the sphere icon on with its screen placement (the copy cave finds the icon itself). Written
        /// only when the sphere changes; 0 clears it. Nothing is drawn when the ISO lacks the hook.</summary>
        internal static void DriveSphereIcon(int sphere)
        {
            if (sphere == _ssIconSphere) return;
            if ((uint)Memory.ReadInt(DunPatches.SsIconHookAddrMmu) != DunPatches.SsIconHookNew)
            {
                if (!_ssIconWarned) { _ssIconWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SuperSteve] sphere-icon hook not in this ISO — no icon (re-patch the ISO)"); }
                return;
            }
            _ssIconSphere = sphere;
            long rec = ItemAddresses.ComItemInfo.RecordAddr(sphere);
            if (sphere == 0 || rec < 0) { Memory.WriteInt(Mailbox.SsIconOn, 0); return; }
            int cls  = Memory.ReadUShort(rec + ItemAddresses.ComItemInfo.ClassOffset);
            int icon = Memory.ReadUShort(rec + ItemAddresses.ComItemInfo.SubIndexOffset);
            Memory.WriteInt(Mailbox.SsIconX, SsIconX);
            Memory.WriteInt(Mailbox.SsIconY, SsIconY);
            Memory.WriteInt(Mailbox.SsIconSize, SsIconSize);
            Memory.WriteInt(Mailbox.SsIconOn, 1);                                       // on LAST
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SuperSteve] sphere icon: weapon {sphere} (class {cls}, icon {icon}) → wepicon cell ({(icon & 7) * 32},{(icon >> 3) * 32}), drawn at ({SsIconX},{SsIconY}) size {SsIconSize}; manager has {SheetsRegistered()}; counters draw {Memory.ReadInt(Mailbox.SsIconDiagDraws)} copy calls {Memory.ReadInt(Mailbox.SsIconDiagCopyCalls)} sheet seen {Memory.ReadInt(Mailbox.SsIconDiagSheetSeen)} copies {Memory.ReadInt(Mailbox.SsIconDiagCopies)}");
        }

        /// <summary>Which of the sheets the icon cave can draw from are registered right now — the one failure it cannot report.</summary>
        private static string SheetsRegistered()
        {
            int count = Math.Min(TextureManager.MaxEntries, Memory.ReadInt(TextureManager.Base + TextureManager.Count));
            byte[] table = count > 0 ? Memory.ReadBytesBatch(TextureManager.Base + TextureManager.Entries, count * TextureManager.EntryStride) : null;
            if (table == null) return "(table unreadable)";
            var found = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
            {
                int o = i * TextureManager.EntryStride + TextureManager.EntryName, len = 0;
                while (len < 16 && table[o + len] != 0) len++;
                string nm = System.Text.Encoding.ASCII.GetString(table, o, len);
                if (nm == "wepicon" || nm == "itemicon" || nm == "itempack") found.Add(nm);
            }
            return found.Count == 0 ? "none of wepicon/itemicon/itempack" : string.Join("+", found);
        }

        // ── Super Steve "Sphere Inheritance" ───────────────────────────────────────────────
        /// <summary>
        /// Super Steve (Xiao's ultimate slingshot) inherits the custom effect of the weapon whose SynthSphere
        /// is attached to it. A weapon Status-Broken into a SynthSphere records its SOURCE weapon id at the
        /// attach-entry's +0x02 (SetStatusBreak, ELF 0x2368D0), and attaching copies the whole 0x20-byte entry
        /// into the weapon record's ATTACH_LIST, so the source id survives in-record and can be read straight
        /// off the record — no stat-fingerprinting needed.
        ///
        /// This is the master dispatch loop: read the single attached sphere, then pulse each ability's driver
        /// with <c>active &amp;&amp; sphere == Items.X</c>. Enemy-side abilities reuse the other characters' weapon
        /// classes' drivers verbatim (they only touch enemy data); the Xiao adaptations are their own classes under
        /// Weapons/Xiao (see the class summary). Every driver is pulsed each tick (enabled or not) so it
        /// self-restores the instant the sphere is swapped; the ones that hold a primed charge or a resident copy
        /// are also stopped explicitly on the swap, and all of them in the teardown at the end.
        ///
        /// NOT dispatched here: MIRAGE (Mirage / Hercules' Wrath spheres). It owns a thread and a state machine
        /// (guard charge → decoy → clone → shimmer), so it gates itself in <see cref="Mirage"/> rather than being
        /// pulsed per-tick like the stateless abilities below. Nothing to add here when its sphere is attached.
        ///
        /// NOT every weapon's ability transfers. Excluded by design:
        ///   • Macho Sword, Wise Owl Sword, Chronicle 2 — rely on weapon ownership
        ///   • Buster Sword, 7 Branch Sword - modify upgrading / status-breaks (the 7 Branch sphere still hands over Swift Strikes)
        /// </summary>
        public static void SphereInheritanceEffect()
        {
            var ssSun = new SunSword.SunHarvestState(EnemyAddresses.FloorSlots.Count);
            var xiaoCurse = new CurseAddrs(Player.Xiao.status, Player.Xiao.statusTimer, Player.Xiao.hp);
            var ssEvilcise = new CurseState();
            var ssManeater = new CurseState();
            var xiaoTuna = new CustomGoroEffects.FrozenTunaWielder(Player.XiaoId, Player.Xiao.hp, Player.Xiao.maxHP,
                                                                   Player.Xiao.status, Player.Xiao.statusTimer);
            var ssTuna = new CustomGoroEffects.FrozenTunaState();
            var ssTallHammer = new CustomGoroEffects.TallHammerState();
            var ssCactus = new CustomUngagaEffects.CactusState();
            var ssSnail = new CustomOsmondEffects.SnailState();
            var ssStarBreaker = new CustomOsmondEffects.StarBreakerState();
            int lastSphere = 0;   // the sphere last seen: Charging Bull keeps a resident copy that must go when its sphere does
            int errors = 0;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SuperSteve] sphere dispatch up");
            while (Player.InDungeonFloor())
            {
                int ch = Player.CurrentCharacterNum();
                if (ch != Player.XiaoId) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SuperSteve] dispatch down: character {ch}"); break; }
                int equipSlot = Memory.ReadByte(DngStatusData.Base +
                                                DngStatusData.EquipSlotArrayOffset + ch);
                if ((uint)equipSlot > 9) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SuperSteve] dispatch down: equip slot {equipSlot}"); break; }
                long rec = DngStatusData.WeaponRecord(ch, equipSlot);
                if (Memory.ReadUShort(rec) != Items.supersteve) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SuperSteve] dispatch down: weapon {Memory.ReadUShort(rec)} in slot {equipSlot}"); break; }

                int sphere = SuperSteve.AttachedSphere(rec);
                bool active = !Player.CheckDunIsPaused();
                if (sphere != lastSphere) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SuperSteve] sphere {lastSphere} → {sphere}");
                try {

                // Toan Effects
                // Divine Guard (7th Heaven) + Guard Crush (Dark Cloud; 7th Heaven inherits Guard Crush by lineage).
                SeventhHeaven.SeventhHeavenSoftenAttacks(active && sphere == Items.seventhheaven);
                GuardGate.NobodyBlocks(active && (sphere == Items.seventhheaven || sphere == Items.darkcloud || SunSword.BlindRunning));   // …and while a Solar Shot's blinding holds the floor

                // Defensive Legacy (Aga's Sword): +15 Xiao defense.
                AgasSwordSphere.Drive(active && sphere == Items.agassword);

                // The attached sphere's weapon icon over Steve on the dungeon HUD.
                SuperSteve.DriveSphereIcon(sphere);

                // …and its pellet, when the sphere came from a slingshot.
                SuperSteve.DriveSphereSprite(sphere);

                // Hero's Courage (Brave Ark): clear Freeze/Poison/Curse/Goo each tick.
                BraveArkSphere.Drive(active && sphere == Items.braveark);

                // Bone Rapier: bone-door bypass (the Xiao dispatcher no longer force-clears it, so this owns it).
                BoneRapier.SkeletonKeyEffect(active && (sphere == Items.bonerapier || sphere == Items.boneslingshot));

                // Solar Harvest (Sun Sword / Big Bang): ~1% of the floor's enemies drop a Sun attachment.
                SunSword.SunHarvestDrive(sphere == Items.sunsword || sphere == Items.bigbang || sphere == Items.swordofzeus, ssSun);

                // Solar Shot (Sun Sword): a 5 s guard charge, and the next pellet carries the Sun Sword's flash to where it lands.
                if (lastSphere == Items.sunsword && sphere != Items.sunsword) SolarShot.Stop();
                SolarShot.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.sunsword);

                // Detonate (Big Bang): the guard charge hangs a bomb over the locked target for her shot to drop, or makes the next
                // pellet a bomb; Big Bang's blast and flash where it lands; explosions cannot hurt her.
                if (lastSphere == Items.bigbang && sphere != Items.bigbang) BigBangShot.Stop();
                BigBangShot.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.bigbang);

                // Lightning (Sword of Zeus): the guard charge primes the sword's bolts — the volley on a release with no lock; locked on, a
                // bolt on every pellet hit for five seconds; the shot charge's pellet calls the charge bolt down wherever it dies.
                if (lastSphere == Items.swordofzeus && sphere != Items.swordofzeus) ZeusShot.Stop();
                ZeusShot.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.swordofzeus);

                // Heavy Hand (Baselard) and the Claymore's throw: every pellet throws its enemy as the sword's hits do (the
                // Claymore's size stays with the sword).
                Baselard.DriveSphere(active && (sphere == Items.baselard || sphere == Items.claymore));

                // No Fool's Gold (Dusack / Brave Ark): mimics' wake guard held open for her pellets (Dark Cloud's and 7th Heaven's
                // Guard Crush above covers every guard, the wake included).
                Dusack.DriveSphere(active && Dusack.Grants(sphere));

                // Fine Fare (Sax / Dusack / 7 Branch Sword / Atlamillia Sword / Chronicle Sword): the floor's chest water, food, keys and
                // repair powder upgraded at the sphere's sword's form; the originals back when the sphere goes.
                Sax.DriveSphere(sphere, active);

                // The Halberd line's charge (Halberd / Scorpion / Mirage / Cactus / Hercules' Wrath / Terra Sword / Babel's Spear): a
                // 0.5 s held shot fires a pellet at the sphere's form — bigger, faster, 1.5× the attack.
                HalberdLineCharge.DriveSphere(sphere, active);

                // Swift Strikes (Shamshir / Dusack / 7 Branch Sword / Atlamillia Sword / Chronicle Sword) and the Partisan's quick combo:
                // her draw plays ×1.6 faster and her shoot at the fastest step that still fires.
                Shamshir.DriveSphere(active && (Shamshir.Grants(sphere) || sphere == Items.partisan));

                // Curses (full inherit): curse Xiao. Not pause-gated — mirrors the Toan loops.
                Evilcise.Drive(sphere == Items.evilcise, xiaoCurse, ssEvilcise);
                Maneater.Drive(sphere == Items.maneater, xiaoCurse, rec, ssManeater);

                // Quick Draw (Small Sword / Tsukikage / Heaven's Cloud): instant fire-on-release + rate-of-fire.
                SmallSwordSphere.Drive(active && (sphere == Items.smallsword || sphere == Items.tsukikage || sphere == Items.heavenscloud));

                // Moonlit Focus (Tsukikage / Heaven's Cloud): ×2 shot speed.
                TsukikageSphere.Drive(active && (sphere == Items.tsukikage || sphere == Items.heavenscloud));

                // Heaven's Cloud (Heaven's Cloud): the two-stage charge → grow the pellet, flash, wind burst on impact.
                HeavensCloudSphere.Drive(active && sphere == Items.heavenscloud);

                // A charged shot's weapon HP (Heaven's Cloud / Mobius Ring / the cat arm it): the word returns to 1.0 once fired.
                ChargedShotWhp.Tick();

                // Xiao Effects

                // Angel Gear: slow party-wide HP regen.
                AngelGear.DriveAngelGear(active && sphere == Items.angelgear);                  // the Angel Gear's own party heal

                // Lock-on speed (Dragon's Y / Divine Beast Title / Angel Shooter / Angel Gear): ×1.3 movement while locked on.
                DragonsY.LockOnSpeedDrive(active && DragonsY.LockOnSpeedGrants(sphere));

                // Lock-on reach (Flamingo / Dragon's Y / Divine Beast Title / Angel Shooter / Angel Gear — and Big Bang, whose sword has it): enemies locked from twice as far.
                Flamingo.Drive(active && (Flamingo.GrantsReach(sphere) || sphere == Items.crosshinder || sphere == Items.bigbang || sphere == Items.swordofzeus));   // the Cross Hinder's reach, and Big Bang's and the Sword of Zeus's inherited from it

                // Dragon's Y: the charged shot — the Gemron ball of Super Steve's own selected element.
                DragonsY.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.dragonsy);

                // Bandit Slingshot / Bandit's Ring: a steal takes the enemy's projectile; every pellet is that shot at 2× the attack.
                BanditSlingshot.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && (sphere == Items.banditslingshot || sphere == Items.banditsring));

                // Double Impact: every shot is two pellets, each at 0.75× the attack (their ricochets driven with them).
                DoubleImpact.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.doubleimpact);

                // Hardshooter: a pellet that lands on an enemy ricochets at the next one.
                if (lastSphere == Items.hardshooter && sphere != Items.hardshooter) Hardshooter.Stop();
                Hardshooter.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.hardshooter);

                // Steel Slingshot: half the WHP per shot while the weapon's WHP is low (the level-up bonus stays the Steel's own).
                if (lastSphere == Items.steelslingshot && sphere != Items.steelslingshot) SteelSlingshot.Stop();
                SteelSlingshot.Drive(active && sphere == Items.steelslingshot);

                // Matador (Charging Bull): the charged pellet crushes guards and flies as a projection of the slingshot — Super
                // Steve's own model, since the copy is of the live weapon. A held copy goes with the sphere.
                if (lastSphere == Items.matador && sphere != Items.matador) Matador.Stop();
                Matador.Drive(active && !Player.CheckDunIsInteracting() && !Player.CheckDunIsOpeningChest() && sphere == Items.matador);
                lastSphere = sphere;

                // NOT pulsed here: Guardian Grace and the cat. Both have their own thread, alive for Super Steve's spheres as well
                // (AngelShooter.Carries / DivineBeastTitle.Wields), because the cat must never have two drivers.

                // Goro Effects

                // Cold Storage (Frozen Tuna): WHP losses bank a healing pool that drains after Xiao is hit;
                // on-hit 5% chance to stop all non-ice enemies at the price of freezing Xiao too.
                CustomGoroEffects.FrozenTunaDrive(active && sphere == Items.frozentuna, xiaoTuna, equipSlot, ssTuna);

                // Tall Hammer: shrinks enemies Xiao's pellets hit.
                CustomGoroEffects.TallHammerDrive(active && sphere == Items.tallhammer, Player.XiaoId, ssTallHammer);

                // Ruby Effects

                // Mobius Ring: holding the shot ramps damage ×1.5 per 1.5s (flash per step); the fired
                // pellet gets the ramped damage + a Ruby-ball-style size to match.
                MobiusRingSphere.Drive(active && sphere == Items.mobiusring);

                // Ungaga Effects

                // Absorb (Cactus): pellet hits restore Xiao's thirst scaled by damage (rock/metal/undead immune).
                CustomUngagaEffects.CactusDrive(active && sphere == Items.cactus, Player.XiaoId,
                                                Player.Xiao.thirst, Player.Xiao.thirstMax, ssCactus);

                // Osmond Effects

                // Snail: 5% chance on hit to inflict gooey on the struck enemy.
                CustomOsmondEffects.SnailDrive(active && sphere == Items.snail, Player.XiaoId, ssSnail);

                // Star Breaker: 2% chance on an enemy kill to receive an empty SynthSphere.
                CustomOsmondEffects.StarBreakerDrive(active && sphere == Items.starbreaker, ssStarBreaker);
                }
                catch (Exception ex)
                {   // one ability's fault must not take the whole dispatch down with it
                    if (errors++ < 5) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SuperSteve] tick error: " + ex.Message + "\n" + ex.StackTrace);
                }

                Thread.Sleep(16);
            }

            // Restore everything on unequip / character-switch / dungeon exit (no-ops if not driven).
            SeventhHeaven.SeventhHeavenSoftenAttacks(false);
            GuardGate.NobodyBlocks(false);
            BoneRapier.SkeletonKeyEffect(false);
            SunSword.SunHarvestDrive(false, ssSun);
            Evilcise.Drive(false, xiaoCurse, ssEvilcise);
            Maneater.Drive(false, xiaoCurse, 0, ssManeater);
            SmallSwordSphere.Drive(false);
            Shamshir.DriveSphere(false);
            Dusack.DriveSphere(false);
            HalberdLineCharge.DriveSphere(0, false);
            Sax.DriveSphere(0, false);
            TsukikageSphere.Drive(false);
            HeavensCloudSphere.Drive(false);   // resets the flash latches, hands the wind gem's collision radius back
            AgasSwordSphere.Drive(false);
            SuperSteve.DriveSphereIcon(0);
            SuperSteve.DriveSphereSprite(0);
            MobiusRingSphere.Drive(false);   // resets the damage ramp
            CustomGoroEffects.FrozenTunaDrive(false, xiaoTuna, 0, ssTuna);   // resets the healing pool
            DragonsY.LockOnSpeedStop();
            Flamingo.Stop();
            DragonsY.Stop();
            Matador.Stop();   // the resident slingshot copy too
            SolarShot.Stop();
            BigBangShot.Stop();
            DoubleImpact.Stop();
            BanditSlingshot.Stop();
            SteelSlingshot.Stop();
            Hardshooter.Stop();
        }

    }
}
