using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>7 Branch Sword — no Status Break below +7; at +7 or higher the SynthSphere keeps 77% of the stats.</summary>
    internal static class SevenBranchSword
    {
        /// <summary>7 Branch Sword's status-break transfer factor — MOD POLICY. The vanilla float (which the game
        /// reads live) is <see cref="WeaponMenu.StatusBreakFactorDefault"/> = 0.6; 0.77 appears nowhere in the
        /// game and is our designed buff (77% transfer, thematically "7"). A designed value belongs with the
        /// feature, not in an addresses file.</summary>
        private const float StatusBreakFactorSeven = 0.77f;

        // ── 7 Branch Sword "Sevenfold Rite" ────────────────────────────────────────────────
        private static DateTime _sevenBranchNextTick = DateTime.MinValue;

        private static bool _sbFactorBoosted = false;

        private static readonly byte[][] _sbSnapshots = new byte[10][]; // last-seen 7BS bag records

        private static readonly bool[] _sbSphereSeen = new bool[AttachBoard.ScanCount];

        private static readonly DateTime[] _sbPendingSince = new DateTime[AttachBoard.ScanCount];

        private static bool _sbInitialized = false;

        // HandleSevenBranchBreak outcomes
        private const int SbPending = 0;   // sphere seen but its source sword hasn't vanished yet

        private const int SbKept    = 1;   // resolved: +7 or higher — sphere kept as-is (already 77%)

        private const int SbUndone  = 2;   // resolved: below +7 — break undone, sphere cleared

        /// <summary>
        /// Ability Name: Sevenfold Rite (7 Branch Sword)
        /// The 7 Branch Sword refuses Status Break below +7, and breaking it at +7 or higher
        /// transfers 77% of its stats into the SynthSphere instead of the normal 60%. Data-only
        /// (EE code pokes crash PCSX2 — see the warning in WeaponAddresses); no persistent stat
        /// is ever spoofed.
        ///  • 77% transfer: while the weapon menu is OPEN with a 7BS selected, the shared
        ///    stat-transfer factor float (0x2A1890) is set to 0.77 and restored to 0.6 otherwise.
        ///    SetStatusBreak reads it live at confirm, so the engine itself builds the
        ///    boosted sphere. The float is shared with cape/fishing-line physics (which
        ///    only run unpaused), so it's only ever 1.0 while a menu is up.
        ///  • Below-+7 refusal: the engine still lets the player pick Status Break below +7, so
        ///    it's refused after the fact. The break writes the sphere immediately but only
        ///    empties the bag slot 1-2s later (CWeaponLevelUp::Step case 5, in place, id -> 0xFFFF).
        ///    A new 7BS-sourced sphere is held PENDING until its source sword vanishes; then if
        ///    the source was below +7 the break is undone (restore the snapshot into the emptied
        ///    slot, clear the sphere, message). At +7+ nothing is needed — the float already made
        ///    the sphere 77%. The break animation still plays on a refused break (cosmetic).
        /// The Status Break hover hint explains the rule whenever a 7BS is selected (mes-data).
        /// </summary>
        public static void SevenfoldRiteEffect()
        {
            // The selection / factor / cursor-pin logic below runs EVERY call (main loop ~50ms)
            // so the confirm-cursor pin reacts fast enough to beat a Right+X input. Only the
            // heavier bag/board undo scan at the end is throttled to ~250ms.
            byte gameMode = Memory.ReadByte(Addresses.mode);
            bool menuOpen =
                (gameMode == 2 && Memory.ReadByte(Addresses.townMode) == 8) ||
                (gameMode == 3 && Memory.ReadByte(Addresses.dungeonMode) == 2) ||
                Player.CheckIsWorldMapMenu();

            bool selected = false;
            int selLevel = -1;
            if (Memory.ReadByte(WeaponMenu.MenuSelectedCharacter) == Player.ToanId)
            {
                byte sel = Memory.ReadByte(WeaponMenu.MenuSelectedWeaponSlot);
                if (sel < 10)
                {
                    long rec = WeaponHave.InventoryWeaponSlot0Id +
                        sel * WeaponHave.InventoryWeaponSlotStride;
                    if (Memory.ReadUShort(rec) == Items.sevenbranchsword)
                    {
                        selected = true;
                        selLevel = Memory.ReadUShort(rec + WeaponHave.InventoryWeaponLevelOffset);
                    }
                }
            }
            WeaponDescriptions.SetStatusBreakHint(selected);
            // Pre-swap the SynthSphere "acquired" message line to the "resists" text whenever a
            // below-+7 7BS is selected, so the popup after an interrupted break IS the refusal
            // message (proactive: applied long before a break can be confirmed; no timing race).
            WeaponDescriptions.SetBreakResultResists(selected && selLevel >= 0 && selLevel < 7);

            // Below +7: interrupt the break flow mid-animation, before it "completes". The
            // confirm and the break animation start normally, but while the CWeaponLevelUp flow
            // sits in its animation state (state 5 — lasts 1-2s, trivially caught at this poll
            // rate) we advance it straight to the wind-down state (6). That skips case 5
            // entirely: the sword is never deleted and the "SynthSphere created!" presentation
            // never fires — the flow just winds down and exits through its own native path.
            // The sphere (written to the board at confirm) is cleared here as well. At +7 and
            // above the flow is untouched.
            if (menuOpen && selected && selLevel >= 0 && selLevel < 7 &&
                Memory.ReadUShort(WeaponMenu.LevelUpFlowKind) == WeaponMenu.FlowKindStatusBreak &&
                Memory.ReadUShort(WeaponMenu.LevelUpFlowState) == WeaponMenu.BreakStateAnimation)
            {
                Memory.WriteUShort(WeaponMenu.LevelUpFlowState, WeaponMenu.BreakStateWindDown);

                // Erase the just-created 7BS sphere from the attachment board
                for (int i = 0; i < AttachBoard.ScanCount; i++)
                {
                    long entry = AttachBoard.Base + (long)i * AttachBoard.Stride;
                    if (Memory.ReadUShort(entry + AttachBoard.EntryItemId) ==
                            AttachBoard.SynthSphereId &&
                        Memory.ReadUShort(entry + AttachBoard.EntrySourceId) ==
                            Items.sevenbranchsword &&
                        !_sbSphereSeen[i])
                    {
                        Memory.WriteByteArray(entry, new byte[AttachBoard.Stride]);
                        break;
                    }
                }

                // Feedback comes from the flow's own message window: the "acquired" line was
                // pre-swapped to the "resists" text above, so the popup that follows explains
                // the refusal in-menu.
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"7 Branch Sword: status break at +{selLevel} interrupted mid-animation (needs +7)");
            }

            // 77% transfer factor (shared float — only ever boosted while a menu is open)
            bool boost = menuOpen && selected;
            if (boost != _sbFactorBoosted)
            {
                _sbFactorBoosted = boost;
                Memory.WriteFloat(WeaponMenu.StatusBreakFactorFloat,
                    boost ? StatusBreakFactorSeven
                          : WeaponMenu.StatusBreakFactorDefault);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    (boost ? "7 Branch Sword: status-break factor 0.6 -> 0.77"
                           : "7 Branch Sword: status-break factor restored to 0.6"));
            }

            // Undo watcher (backstop): throttle the heavy bag/board reads to ~250ms.
            if (DateTime.UtcNow < _sevenBranchNextTick) return;
            _sevenBranchNextTick = DateTime.UtcNow.AddMilliseconds(250);

            byte[] bag = Memory.ReadBytesBatch(WeaponHave.InventoryWeaponSlot0Id,
                10 * WeaponHave.InventoryWeaponSlotStride);
            byte[] board = Memory.ReadBytesBatch(AttachBoard.Base,
                AttachBoard.ScanCount * AttachBoard.Stride);
            if (bag == null || board == null ||
                bag.Length < 10 * WeaponHave.InventoryWeaponSlotStride ||
                board.Length < AttachBoard.ScanCount * AttachBoard.Stride)
                return;

            for (int idx = 0; idx < AttachBoard.ScanCount; idx++)
            {
                int off = idx * AttachBoard.Stride;
                bool isSevenSphere =
                    BitConverter.ToUInt16(board, off + AttachBoard.EntryItemId) ==
                        AttachBoard.SynthSphereId &&
                    BitConverter.ToUInt16(board, off + AttachBoard.EntrySourceId) ==
                        Items.sevenbranchsword;

                bool markSeen = isSevenSphere;
                if (isSevenSphere && !_sbSphereSeen[idx] && _sbInitialized)
                {
                    if (_sbPendingSince[idx] == DateTime.MinValue)
                        _sbPendingSince[idx] = DateTime.UtcNow;

                    int outcome = HandleSevenBranchBreak(idx, bag);
                    if (outcome == SbPending)
                    {
                        if (DateTime.UtcNow - _sbPendingSince[idx] < TimeSpan.FromSeconds(8))
                            markSeen = false;                       // not resolved — retry next tick
                        else
                        {
                            _sbPendingSince[idx] = DateTime.MinValue; // pre-existing sphere; stop trying
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                                "7 Branch Sword: sphere never matched a vanished sword — left untouched");
                        }
                    }
                    else
                    {
                        _sbPendingSince[idx] = DateTime.MinValue;
                        if (outcome == SbUndone) markSeen = false;    // sphere was cleared
                    }
                }
                else if (!isSevenSphere)
                {
                    _sbPendingSince[idx] = DateTime.MinValue;
                }
                _sbSphereSeen[idx] = markSeen;
            }
            _sbInitialized = true;

            for (int s = 0; s < 10; s++)
            {
                int off = s * WeaponHave.InventoryWeaponSlotStride;
                if (BitConverter.ToUInt16(bag, off) == Items.sevenbranchsword)
                {
                    byte[] snap = new byte[WeaponHave.InventoryWeaponSlotStride];
                    Array.Copy(bag, off, snap, 0, snap.Length);
                    _sbSnapshots[s] = snap;
                }
                else
                {
                    _sbSnapshots[s] = null;
                }
            }
        }

        /// <summary>
        /// Resolves a newly seen 7BS-sourced SynthSphere at board index <paramref name="idx"/>
        /// once its source sword has vanished from the bag: below +7 -> undo the break; +7 or
        /// higher -> keep it (the factor float already made it 77%). Returns
        /// <see cref="SbPending"/> while the source sword is still present.
        /// </summary>
        private static int HandleSevenBranchBreak(int idx, byte[] bag)
        {
            int donor = -1;
            for (int s = 0; s < 10; s++)
            {
                if (_sbSnapshots[s] != null &&
                    BitConverter.ToUInt16(bag, s * WeaponHave.InventoryWeaponSlotStride) !=
                        Items.sevenbranchsword)
                {
                    donor = s;
                    break;
                }
            }
            if (donor < 0) return SbPending;   // source sword still in the bag (animation running)

            byte[] snap = _sbSnapshots[donor];
            int level = BitConverter.ToUInt16(snap, WeaponHave.InventoryWeaponLevelOffset);
            if (level >= 7)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                    $"7 Branch Sword: status break at +{level} — 77% sphere (factor float)");
                return SbKept;
            }

            long sphereAddr = AttachBoard.Base + (long)idx * AttachBoard.Stride;
            long slotAddr = WeaponHave.InventoryWeaponSlot0Id +
                            donor * WeaponHave.InventoryWeaponSlotStride;
            Memory.WriteByteArray(slotAddr, snap);
            Memory.WriteByteArray(sphereAddr, new byte[AttachBoard.Stride]);
            Array.Copy(snap, 0, bag, donor * WeaponHave.InventoryWeaponSlotStride, snap.Length);
            DungeonMessages.DisplayMessage("The 7 Branch Sword resists!\nIt only breaks at +7 or above.", 2, 34, 5000);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                $"7 Branch Sword: status break at +{level} undone (needs +7)");
            return SbUndone;
        }
    }
}
