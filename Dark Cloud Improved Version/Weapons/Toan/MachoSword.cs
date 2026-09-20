using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Macho Sword — while owned, every weapon's ABS keeps filling past its max and the overflow carries into the next level (the ABS-rollover EE-code patches included).</summary>
    internal static class MachoSword
    {
        // ── Macho Sword "Overtraining" (ABS rollover) ──────────────────────────────────────
        /// <summary>Rollover ceiling: current ABS may grow to this multiple of the weapon's max. This is the
        /// MOD's policy, not something the game does (vanilla is inert above max — see
        /// <see cref="AbsRewards"/>), so it lives with the feature rather than in an addresses
        /// file.</summary>
        private const int RolloverFactor = 2;

        private static DateTime _machoNextTick = DateTime.MinValue;

        private static DateTime _machoNextOwnershipCheck = DateTime.MinValue;

        private static DateTime _machoNextCarryPass = DateTime.MinValue;

        private static bool _machoOwned;

        // per-enemy-slot HP latch for kill detection
        private static readonly int[] _machoPrevHp = new int[EnemyAddresses.FloorSlots.Count];

        // shadow accounting for the ACTIVE character's equipped weapon record
        private static long _machoShadowRecord;   // record the shadow belongs to (0 = unsynced)

        private static int _machoShadowId;

        private static int _machoTrueAbs;

        private static DateTime _machoLastGrantAt = DateTime.MinValue;

        // level-up carry: last-seen state + pending carries, one per bag record (6 chars × 10 slots)
        private struct MachoSnap { public ushort Id; public short Level; public short Abs; public short RolledAbs; public DateTime RolledAt; }

        private struct MachoCarry { public bool Active; public ushort Id; public int Carry; public int GuardPasses; }

        private static readonly MachoSnap[,] _machoSnaps = new MachoSnap[6, 10];

        private static readonly MachoCarry[,] _machoCarries = new MachoCarry[6, 10];

        private static bool _machoSnapsPrimed;

        // fast carry path: armed at the menu level-up CONFIRM (flow kind 0), fires the instant
        // the engine's post-level abs reset lands — during the ~2s animation, so the gauge
        // never redraws with 0
        private static long _machoFlowRec;        // record being leveled by the menu flow (0 = disarmed)

        private static ushort _machoFlowId;

        private static short _machoFlowPrevLevel;

        private static int _machoFlowCarry;

        private static DateTime _machoFlowArmedAt;

        private static Thread _machoStrikeThread; // tight-poll payout thread, alive only while armed

        private static bool _machoBankMigrated;

        private static string MachoBankFile => System.IO.Path.Combine(AppContext.BaseDirectory, "MachoRolloverBank.txt");

        /// <summary>
        /// Ability Name: Overtraining (Macho Sword) — replaces the old Shadow Boxing effect.
        /// PASSIVE: as long as a Macho Sword is OWNED (equipped, in Toan's bag, or in storage —
        /// it does not need to be in hand), every weapon's ABS keeps absorbing past its max, up
        /// to <see cref="RolloverFactor"/>× (98/100 + a 6-ABS kill →
        /// 104/100, cap 200/100), and the rolled-over surplus carries into the next level
        /// (level up at 116/100 → the new level starts at 16 ABS).
        ///
        /// The rolled value lives IN the record's abs field — the menu gauge number reads
        /// "116/100" for real — backed by two tiny EE-code display patches (applied from
        /// ApplyNewChanges at in-game entry, while the menu code is still cold — see
        /// <see cref="ApplyAbsCodePatches"/>): the weapon menu's number clamp is split off its
        /// gauge math (number raw, bar still pinned at full), and the battle HUD's abs bar gets
        /// a min(abs, max) folded into its width computation so it no longer overdraws. On menu
        /// level-up the engine zeroes abs on the message-dismiss press (SetLevelUpWeaponData —
        /// same input that redraws the gauge), so the level-up carry is delivered by a strike
        /// thread armed at the confirm (<see cref="MachoFastCarryTick"/>): the 0 is on screen
        /// for at most a frame before the carried surplus replaces it.
        ///
        /// Kill accounting: the engine's grant (CMonstorUnit::Step death block) is SKIPPED
        /// entirely once abs ≥ max, so above max this watcher replicates it — per-kill detection
        /// via the enemy slots' HP&gt;0→≤0 latch (which fires ~a death animation BEFORE the
        /// engine's own grant on the slot-release frame), the engine's own eligibility (killer ==
        /// active character, no default weapons, no monster-transform drain mode) and modifiers
        /// (back-floor ×2, slot-flag-0x2000 ×1.2). Writing the rolled value ≥ max immediately
        /// also neutralizes the engine's pending grant for the same kill (its abs &lt; max gate
        /// fails), so nothing double-counts. Save-safe: abs is an ordinary save field and a save
        /// carrying 116/100 is benign even without the mod — the engine treats abs ≥ max as
        /// "level-up ready" and simply stops absorbing. Known cosmetics: the item-menu panel
        /// clamps its abs number to max, and the vanilla "ABS MAX" popup doesn't fire on the
        /// crossing kill (this watcher preempts the engine's clamp path).
        /// </summary>
        public static void MachoSwordEffect()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _machoNextTick) return;
            _machoNextTick = now.AddMilliseconds(100);

            if (!_machoBankMigrated) MachoMigrateBankFile();

            // Level-up carry watchers. They run in every mode (weapons level up from menus
            // anywhere) and regardless of Macho ownership, so leftover rolled weapons still
            // carry after the sword is sold. The fast path arms a strike thread at the menu
            // confirm; the 500ms pass is the backstop for level-ups the fast path missed.
            MachoFastCarryTick(now);
            if (now >= _machoNextCarryPass)
            {
                _machoNextCarryPass = now.AddMilliseconds(500);
                MachoCarryPass(now);
            }

            if (now >= _machoNextOwnershipCheck)
            {
                _machoNextOwnershipCheck = now.AddSeconds(2);
                _machoOwned = PlayerOwnsMachoSword();
            }
            // Rollover accumulation (kill ABS past max) is the Macho-owned part of the ability,
            // and only matters in live dungeon play.
            if (!_machoOwned || !Player.InDungeonFloor())
            {
                _machoShadowRecord = 0;
                return;
            }
            MachoKillRolloverTick();
        }

        private static void MachoKillRolloverTick()
        {
            // Resolve the ACTIVE character's equipped inventory record — kill ABS lands there.
            int ch = Player.CurrentCharacterNum();
            if ((uint)ch > 5) { _machoShadowRecord = 0; return; }
            int equipSlot = Memory.ReadByte(DngStatusData.Base +
                                            DngStatusData.EquipSlotArrayOffset + ch);
            if ((uint)equipSlot > 9) { _machoShadowRecord = 0; return; }
            long rec = DngStatusData.WeaponRecord(ch, equipSlot);
            int id = Memory.ReadUShort(rec);
            int max = MachoMaxExp(id, Memory.ReadShort(rec + WeaponHave.InventoryWeaponLevelOffset));
            int cur = Memory.ReadShort(rec + WeaponHave.InventoryWeaponAbsOffset);

            if (rec != _machoShadowRecord || id != _machoShadowId)
            {
                // Equipped weapon changed — adopt its stored abs as the truth and re-latch the
                // HP array so pre-existing corpses can't read as fresh kills.
                _machoShadowRecord = rec;
                _machoShadowId = id;
                _machoTrueAbs = cur;
                for (int s = 0; s < _machoPrevHp.Length; s++) _machoPrevHp[s] = 0;
                return;
            }

            bool drainMode = Memory.ReadInt(DngStatusData.Base +
                                            DngStatusData.TransformStateOffset) == 10;
            bool eligible = max > 0 && !drainMode && !MachoIsAbslessWeapon(id);

            // 1) Kills first — so a fast engine grant that already landed isn't counted twice.
            int grants = MachoCollectKillGrants(ch, eligible);
            if (grants > 0)
            {
                int cap = RolloverFactor * max;
                _machoTrueAbs = Math.Min(_machoTrueAbs + grants, cap);
                _machoLastGrantAt = GameClock.Now;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"Macho Sword: +{grants} ABS (kill) — weapon {id} at {_machoTrueAbs}/{max}");
            }
            // 2) Sync up to engine activity (its own below-max grants).
            if (cur > _machoTrueAbs) _machoTrueAbs = cur;
            // 3) Adopt engine reductions (level-up reset, transform-drain kills) — but NOT
            //    while a detected kill's engine grant may still be in flight (grants land on
            //    the slot-release frame, up to a few seconds after our HP-latch detection;
            //    adopting down in that window loses the in-flight amount if the NEXT kill
            //    crosses max). The carry watcher re-applies level-up carries on its own.
            else if (cur < _machoTrueAbs && grants == 0 &&
                     GameClock.Now - _machoLastGrantAt > TimeSpan.FromSeconds(4))
                _machoTrueAbs = cur;

            // 4) Materialize the rollover. Only ever needed above the vanilla max; a value ≥ max
            //    also makes the engine's pending grant for these kills skip itself natively.
            if (_machoTrueAbs > max && _machoTrueAbs > cur)
                Memory.WriteUShort(rec + WeaponHave.InventoryWeaponAbsOffset, (ushort)_machoTrueAbs);
        }

        // One pass over the 16 enemy slots: HP > 0 → ≤ 0 crossings are deaths. Returns the summed
        // engine-equivalent ABS grant of this tick's eligible kills.
        private static int MachoCollectKillGrants(int activeChar, bool eligible)
        {
            int total = 0;
            bool backFloor = Memory.ReadInt(AbsRewards.BackFloorDoubleAddr) != 0;
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                int hp = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Hp));
                int prev = _machoPrevHp[s];
                _machoPrevHp[s] = hp;
                if (prev <= 0 || hp > 0) continue;   // no death crossing on this slot
                // Still in its death animation (RenderStatus ≥ 1): the engine's own grant (on the
                // -1 release frame) hasn't run yet, so our write below always lands first.
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.RenderStatus)) < 1) continue;
                if (!eligible) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.KillerCharId)) != activeChar) continue;
                int give = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(s, EnemySlotOffsets.Abs));
                if (give <= 0) continue;
                if (backFloor) give <<= 1;
                if ((Memory.ReadInt(AbsRewards.SlotStatusFlagsAddr(s)) &
                     AbsRewards.AbsBonusFlag) != 0)
                    give = (int)(give * AbsRewards.AbsBonusMult);
                total += give;
            }
            return total;
        }

        // The fast carry path. The engine commits level++/abs=0 on the message-DISMISS press —
        // the same input that redraws the gauge (pinned by the LvUpDiag trace 2026-07-06:
        // SetLevelUpWeaponData zeroes abs, then WeaponMenuSelect's phase-7 handler bumps the
        // level, all in one frame). A reactive write can therefore never fully win; this path
        // arms at the CONFIRM (flow kind 0, set by SetLevelUpValue ~2s before the commit, while
        // the record still holds the rolled abs) and hands the payout to a dedicated STRIKE
        // THREAD polling the record every few ms — the carry lands within a frame of the
        // commit, keeping the 0 on screen for at most a single frame. This tick remains the
        // arm/disarm bookkeeper and the payout backstop should the thread die.
        private static void MachoFastCarryTick(DateTime now)
        {
            if (_machoFlowRec == 0)
            {
                if (Memory.ReadShort(WeaponMenu.LevelUpFlowKind) != WeaponMenu.FlowKindLevelUp) return;
                int ch = Memory.ReadByte(WeaponMenu.MenuSelectedCharacter);
                int slot = Memory.ReadByte(WeaponMenu.MenuSelectedWeaponSlot);
                if ((uint)ch > 5 || (uint)slot > 9) return;
                long rec = DngStatusData.WeaponRecord(ch, slot);
                ushort id = Memory.ReadUShort(rec);
                short level = Memory.ReadShort(rec + WeaponHave.InventoryWeaponLevelOffset);
                int max = MachoMaxExp(id, level);
                if (max <= 0) return;                 // not a real weapon record
                int abs = Memory.ReadShort(rec + WeaponHave.InventoryWeaponAbsOffset);
                if (abs <= max) return;               // no rollover on the leveling weapon — vanilla handles it
                _machoFlowId = id;
                _machoFlowPrevLevel = level;
                _machoFlowCarry = Math.Min(abs, RolloverFactor * max) - max;
                _machoFlowArmedAt = now;
                _machoFlowRec = rec;                  // set last — arms the strike thread's loop
                if (_machoStrikeThread == null || !_machoStrikeThread.IsAlive)
                {
                    _machoStrikeThread = new Thread(MachoCarryStrikeLoop) { IsBackground = true };
                    _machoStrikeThread.Start();
                }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"Macho Sword: level-up confirmed on weapon {id} with {abs}/{max} ABS — carry {_machoFlowCarry} armed (strike thread up)");
            }
            else
            {
                ushort id = Memory.ReadUShort(_machoFlowRec);
                short level = Memory.ReadShort(_machoFlowRec + WeaponHave.InventoryWeaponLevelOffset);
                short abs = Memory.ReadShort(_machoFlowRec + WeaponHave.InventoryWeaponAbsOffset);
                if (id != _machoFlowId || now - _machoFlowArmedAt > TimeSpan.FromSeconds(15))
                {
                    _machoFlowRec = 0;   // weapon changed under us, or the flow never committed
                }
                else if (level > _machoFlowPrevLevel && abs < _machoFlowCarry)
                {
                    // Backstop payout (the strike thread normally beats this by ~100ms).
                    Memory.WriteUShort(_machoFlowRec + WeaponHave.InventoryWeaponAbsOffset, (ushort)_machoFlowCarry);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                        $"Macho Sword: fast carry (backstop) — weapon {id} starts its new level at {_machoFlowCarry} ABS");
                    _machoFlowRec = 0;
                }
                else if (level > _machoFlowPrevLevel && abs >= _machoFlowCarry)
                {
                    _machoFlowRec = 0;   // strike thread already paid out (or engine kept enough)
                }
            }
        }

        // Strike thread: lives only while a rolled level-up is in flight. Polls the armed record
        // every few ms so the carry write lands within a frame of the engine's dismissal commit —
        // the tightest reaction PINE allows without patching the commit itself (the abs reset is
        // a hardcoded `sh $zero` with no data operand, and the user prefers the flash-then-carry
        // presentation over NOPing it).
        private static void MachoCarryStrikeLoop()
        {
            try
            {
                while (true)
                {
                    long rec = _machoFlowRec;
                    if (rec == 0) return;                              // disarmed (paid out / timeout / changed)
                    if (Memory.ReadUShort(rec) != _machoFlowId) { Thread.Sleep(4); continue; } // let the tick disarm
                    short level = Memory.ReadShort(rec + WeaponHave.InventoryWeaponLevelOffset);
                    if (level > _machoFlowPrevLevel)
                    {
                        short abs = Memory.ReadShort(rec + WeaponHave.InventoryWeaponAbsOffset);
                        if (abs < _machoFlowCarry)
                            Memory.WriteUShort(rec + WeaponHave.InventoryWeaponAbsOffset, (ushort)_machoFlowCarry);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Macho Sword: strike carry — weapon {_machoFlowId} starts its new level at {_machoFlowCarry} ABS");
                        _machoFlowRec = 0;
                        return;
                    }
                    Thread.Sleep(4);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Macho Sword: strike thread error — " + ex.Message);
            }
        }

        // Watches all 60 bag records for level-ups and carries the rolled-over surplus into the
        // new level: level up at 116/100 → the new level's gauge starts at 16. In practice the
        // engine has already committed level++ AND its abs reset by the time the level bump is
        // visible here (play-tested: the menu showed 0/newMax while the old stability-wait
        // version held back), so the carry is written IMMEDIATELY on detection — no visible
        // "0 then suddenly 16" gap. Because the engine's post-level abs write location is
        // still unknown (NOT in SetLevelUpValue or the CWeaponLevelUp flow states), a short
        // guard window follows: if something re-zeroes the field below the carry within ~5s,
        // it is re-applied. A path that natively leaves MORE than the carry (e.g. Powerup
        // Powder keeping the rolled value) is never overwritten — the write only ever raises.
        private static void MachoCarryPass(DateTime now)
        {
            for (int ch = 0; ch < 6; ch++)
            {
                for (int slot = 0; slot < 10; slot++)
                {
                    long rec = DngStatusData.WeaponRecord(ch, slot);
                    ushort id = Memory.ReadUShort(rec);
                    bool validWeapon = id >= 257 && id <= 376;
                    short level = validWeapon ? Memory.ReadShort(rec + WeaponHave.InventoryWeaponLevelOffset) : (short)0;
                    short abs = validWeapon ? Memory.ReadShort(rec + WeaponHave.InventoryWeaponAbsOffset) : (short)0;

                    MachoSnap prev = _machoSnaps[ch, slot];
                    ref MachoCarry pend = ref _machoCarries[ch, slot];

                    if (pend.Active)
                    {
                        if (!validWeapon || id != pend.Id)
                            pend.Active = false;                       // weapon gone/changed — drop the guard
                        else
                        {
                            if (abs < pend.Carry)                      // a late engine reset undid the carry — re-apply
                            {
                                Memory.WriteUShort(rec + WeaponHave.InventoryWeaponAbsOffset, (ushort)pend.Carry);
                                abs = (short)pend.Carry;
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                    $"Macho Sword: level-up carry re-applied — weapon {id} at {pend.Carry} ABS");
                            }
                            if (++pend.GuardPasses >= 10) pend.Active = false;   // ~5s guard window
                        }
                    }
                    else if (_machoSnapsPrimed && validWeapon && id == prev.Id && level > prev.Level)
                    {
                        int prevMax = MachoMaxExp(id, prev.Level);
                        int rolled = prev.Abs;
                        if (prev.RolledAbs > rolled && (now - prev.RolledAt) < TimeSpan.FromSeconds(8))
                            rolled = prev.RolledAbs;
                        if (prevMax > 0)
                        {
                            int expected = Math.Max(0, Math.Min(rolled, RolloverFactor * prevMax) - prevMax);
                            // Settle only the cases that are OURS to settle: a commit that
                            // zeroed a rollover below its carry (the normal case), or one that
                            // left the rolled value fully in place. A path that natively kept a
                            // below-max abs (e.g. an item-menu Powerup Powder) is left alone.
                            bool resetSkipped = abs == rolled && rolled >= prevMax;
                            if (abs != expected && (resetSkipped || abs < expected))
                            {
                                Memory.WriteUShort(rec + WeaponHave.InventoryWeaponAbsOffset, (ushort)expected);
                                abs = (short)expected;
                                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                    $"Macho Sword: weapon {id} leveled with {rolled}/{prevMax} ABS — new level starts at {expected}");
                            }
                            if (expected > 0)
                                pend = new MachoCarry { Active = true, Id = id, Carry = expected, GuardPasses = 0 };
                        }
                    }

                    MachoSnap snap = new MachoSnap { Id = id, Level = level, Abs = abs, RolledAbs = prev.RolledAbs, RolledAt = prev.RolledAt };
                    if (validWeapon && id == prev.Id && abs > MachoMaxExp(id, level)) { snap.RolledAbs = abs; snap.RolledAt = now; }
                    else if (id != prev.Id) { snap.RolledAbs = 0; snap.RolledAt = DateTime.MinValue; }
                    _machoSnaps[ch, slot] = snap;
                }
            }
            _machoSnapsPrimed = true;
        }

        // One-time migration from the short-lived "banked surplus" build: its sidecar file held
        // rollover OUTSIDE the records — fold any surviving surplus back into the matching
        // record's abs field, then delete the file.
        private static void MachoMigrateBankFile()
        {
            _machoBankMigrated = true;
            try
            {
                if (!System.IO.File.Exists(MachoBankFile)) return;
                foreach (string line in System.IO.File.ReadAllLines(MachoBankFile))
                {
                    string[] p = line.Split(',');
                    if (p.Length != 5) continue;
                    int ch = int.Parse(p[0]); int slot = int.Parse(p[1]);
                    int id = int.Parse(p[2]); int surplus = int.Parse(p[4]);
                    if ((uint)ch > 5 || (uint)slot > 9 || surplus <= 0) continue;
                    long rec = DngStatusData.WeaponRecord(ch, slot);
                    if (Memory.ReadUShort(rec) != id) continue;
                    int max = MachoMaxExp(id, Memory.ReadShort(rec + WeaponHave.InventoryWeaponLevelOffset));
                    if (max <= 0) continue;
                    int abs = Memory.ReadShort(rec + WeaponHave.InventoryWeaponAbsOffset);
                    int merged = Math.Min(abs + surplus, RolloverFactor * max);
                    if (merged > abs)
                    {
                        Memory.WriteUShort(rec + WeaponHave.InventoryWeaponAbsOffset, (ushort)merged);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Macho Sword: migrated {surplus} banked ABS back into weapon {id} ({merged}/{max})");
                    }
                }
                System.IO.File.Delete(MachoBankFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Macho Sword: bank migration failed — " + ex.Message);
            }
        }

        // ── ABS rollover EE-code patches ─────────────────────────────────────────────────────
        // Surgical patches (display + the level-up commit) for rolled records (abs > max); all verified
        // against the SCUS_971.11 disassembly and rewritten IN PLACE (no code cave):
        //  • DrawWepDamageDraw (0x1F8D30) — the weapon-menu WHP/ABS panel. Vanilla clamps abs
        //    into $s2 once and feeds BOTH the gauge width and the number. Patch: NOP the clamp
        //    (por $s2,$s1 @+0x98) so the final DrawMenuNumber($s2) draws the RAW abs ("116/100"),
        //    and rebuild the width block (@+0xB4, 10 words) to fold min($s2,$s1) into its own
        //    99*abs/max computation — the div-by-zero guard it replaces is redundant (max ≥ 1 is
        //    guaranteed by the preceding bgtz fallback). The full-gauge blink compare (bne
        //    $s1,$s2 @+0x174) becomes abs >= max (slt+bnez) so rolled weapons blink too.
        //  • topStatusInfo (0x1B04F0) — the battle-HUD abs bar. Vanilla computes fill =
        //    abs*(len/max) with NO clamp (the overdraw seen in play-testing). Patch: min(abs,max)
        //    before the mtc1, using the blez delay slot + two FPU-hazard nops as free slots (an
        //    integer op fills a COP1 scheduling gap exactly as well as a nop).
        // ⚠ PINE writes to EE CODE pages crash PCSX2 when the target code is HOT (recompiled
        // blocks live). The floor-load window did NOT save these two functions (crashed on
        // floor load, 2026-07-06 — by then the menu/HUD draw code had already run, unlike the
        // ToanKey case that window was verified on), so this is applied from
        // MainMenuThread.ApplyNewChanges at in-game entry instead, while the menu/HUD draw
        // code is still cold for the session. Original words are read-verified first: a
        // mismatch (different game build / emulator state) logs once and never writes.
        private struct AbsCodePatch { public long Addr; public uint[] Orig; public uint[] New; }

        private static readonly AbsCodePatch[] _absDisplayPatches =
        {
            // DrawWepDamageDraw: NOP the abs clamp (por $s2, $s1 — taken when abs > max)
            new AbsCodePatch { Addr = 0x201F8DC8, Orig = new uint[] { 0x72209628 }, New = new uint[] { 0x00000000 } },
            // DrawWepDamageDraw: gauge width = 99*min(abs,max)/max (replaces 99*s2/s1 + the
            // redundant div-by-zero guard: slt/move/movn prepend, bnez/nop/break dropped)
            new AbsCodePatch
            {
                Addr = 0x201F8DE4,
                Orig = new uint[] { 0x00121140, 0x00521821, 0x00031080, 0x00431023, 0x0051001A,
                                    0x16200002, 0x00000000, 0x000001CD, 0x00001012, 0xAFA200A0 },
                New  = new uint[] { 0x0232082A,   // slt  $at, $s1, $s2      (at = max < abs)
                                    0x02401821,   // move $v1, $s2
                                    0x0221180B,   // movn $v1, $s1, $at      (v1 = min(abs, max))
                                    0x00031140,   // sll  $v0, $v1, 5
                                    0x00431821,   // addu $v1, $v0, $v1      (33×)
                                    0x00031080,   // sll  $v0, $v1, 2
                                    0x00431023,   // subu $v0, $v0, $v1      (99×)
                                    0x0051001A,   // div  $zero, $v0, $s1
                                    0x00001012,   // mflo $v0                (LO access interlocks)
                                    0xAFA200A0 }, // sw   $v0, 0xa0($sp)
            },
            // DrawWepDamageDraw: full-gauge blink at abs >= max instead of abs == max
            new AbsCodePatch
            {
                Addr = 0x201F8EA4,
                Orig = new uint[] { 0x1632001C, 0x00000000 },  // bne $s1, $s2, +0x1C; nop
                New  = new uint[] { 0x0251082A, 0x1420001B },  // slt $at, $s2, $s1; bnez $at, +0x1B
            },
            // topStatusInfo: clamp the HUD abs-bar fill to the bar length (min(abs,max) before mtc1)
            new AbsCodePatch { Addr = 0x201B0BC8, Orig = new uint[] { 0x00000000 }, New = new uint[] { 0x8FA200B8 } }, // lw   $v0, 0xb8($sp)  (max; blez delay slot)
            new AbsCodePatch { Addr = 0x201B0BD0, Orig = new uint[] { 0x00000000 }, New = new uint[] { 0x0043082A } }, // slt  $at, $v0, $v1   (fills the mtc1→cvt gap)
            new AbsCodePatch { Addr = 0x201B0BD8, Orig = new uint[] { 0x8FA200B8 }, New = new uint[] { 0x0041180B } }, // movn $v1, $v0, $at   (replaces the lw moved up)
        };

        private static bool _absPatchMismatchLogged;

        /// <summary>Applies the ABS display patches. Called from ApplyNewChanges (in-game
        /// entry), while the target menu/HUD draw code is still cold for the session — writing
        /// HOT EE code crashes PCSX2 (floor-load application crashed in testing). Idempotent:
        /// each patch is skipped when already applied and refused (log once) when the original
        /// words don't match this build.</summary>
        public static void ApplyAbsCodePatches()
        {
            foreach (AbsCodePatch p in _absDisplayPatches)
            {
                bool applied = true, pristine = true;
                for (int i = 0; i < p.Orig.Length; i++)
                {
                    uint w = (uint)Memory.ReadInt(p.Addr + i * 4L);
                    if (w != p.New[i]) applied = false;
                    if (w != p.Orig[i]) pristine = false;
                }
                if (applied) continue;                 // this emulator session already has it
                if (!pristine)
                {
                    if (!_absPatchMismatchLogged)
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Macho Sword: ABS display patch @0x{p.Addr:X8} found unexpected code — not patching");
                    _absPatchMismatchLogged = true;
                    continue;
                }
                for (int i = 0; i < p.New.Length; i++)
                    Memory.WriteUInt(p.Addr + i * 4L, p.New[i]);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"Macho Sword: ABS display patch applied @0x{p.Addr:X8} ({p.New.Length} words)");
            }
        }

        // C# replica of GetWeaponMaxExp (ELF 0x232080): live static-table base (+0x30, SIGNED
        // char) + level × step (+0x32, short), clamped to 1..999 with the engine's own ≤0 → 99
        // fallback. Reads the live table so any modded base values are honored. Returns 0 for
        // ids outside the weapon table.
        private static int MachoMaxExp(int weaponId, int level)
        {
            int row = weaponId - Weapons.daggerid;
            if (row < 0 || row > 118) return 0;
            int baseAbs = (sbyte)Memory.ReadByte(Weapons.abs + row * Weapons.weaponoffset);
            int step = Memory.ReadShort(Weapons.absadd + row * Weapons.weaponoffset);
            long max = baseAbs + (long)level * step;
            if (max > 999) max = 999;
            if (max < 1) max = 99;
            return (int)max;
        }

        // The only weapons the engine denies ABS are each character's BROKEN starting weapon — the
        // "default weapon" in MenuDefaultWeaponNo (the id−1 broken forms: broken Dagger 257, broken
        // Wooden Slingshot 299, broken Mallet 314, broken Gold Ring 331, broken Fighting Stick 347,
        // broken Machine Gun 363). CheckDefaultWeapon (0x1BE4B0) gates the death-block grant on
        // equipped == defWeapon[char], and defWeapon holds the BROKEN id. So INTACT defaults (Dagger,
        // Mallet, …) earn ABS and roll over like any weapon — only the broken forms are excluded.
        // (The Serpent Sword's flag-gated no-ABS state is not replicated; it self-limits anyway.)
        private static bool MachoIsAbslessWeapon(int id)
        {
            return id == Item.DaggerBroken.Id
                || id == Item.WoodenSlingshotBroken.Id
                || id == Item.MalletBroken.Id
                || id == Item.GoldRingBroken.Id
                || id == Item.FightingStickBroken.Id
                || id == Item.MachineGunBroken.Id;
        }

        private static bool PlayerOwnsMachoSword()
        {
            if (Player.Weapon.GetCurrentWeaponId() == Items.machosword)
                return true;

            for (int i = 0; i < 10; i++)
                if (Memory.ReadUShort(Addresses.firstBagWeapon + (0xF8 * i)) == Items.machosword)
                    return true;

            for (int i = 0; i < 30; i++)
                if (Memory.ReadUShort(Addresses.firstStorageWeapon + (0xF8 * i)) == Items.machosword)
                    return true;

            return false;
        }
    }
}
