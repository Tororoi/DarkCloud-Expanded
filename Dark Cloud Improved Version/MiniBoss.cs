using System;
using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public partial class MiniBoss
    {
        static Random rnd = new Random();

        public const int enemyZeroWidth = 0x21E18530;  //Enemy Width multiplier
        public const int enemyZeroHeight = 0x21E18534; //Enemy Height multiplier
        public const int enemyZeroDepth = 0x21E18538;  //Enemy Depth multiplier
        public const int scaleOffset = 0x3510;         //Offset for size
        public static List<int> miniBossEnemyNumbers = new List<int>();
        public static bool miniBossRolled = false;

        const float scaleSize = 1.5F;             //Miniboss model + damage-hitbox scale
        const float weaponHitboxFactor = 1.6F;    //Miniboss WEAPON (attack) hitbox scale — intentionally > scaleSize: the 1.5× model raises the weapon bone, so the attack sphere needs extra radius to still reach short characters
        const float attackRangeFactor = 1.3F;     //Miniboss attack/engage RANGE multiplier — how much farther out a miniboss commits to its attack (shared-STB _GET_DISTANCE threshold, applied per-species to the nearest miniboss)
        const float attackRangeFactorWide = 1.5F; //Wider attack-range factor for the long-reach species in _attackRangeWide (plants / chariot)
        // Species that use attackRangeFactorWide instead of attackRangeFactor. Keyed by species Id — enhanced variants
        // reuse the base Id (e.g. Cursed Rose + Cursed Rose (Enhanced) are both Id 68), so they're covered automatically.
        static readonly HashSet<ushort> _attackRangeWide = new HashSet<ushort>
        {
            Enemies.CursedRose.Id, Enemies.DarkFlower.Id, Enemies.CannibalPlant.Id,
            Enemies.Opar.Id, Enemies.KingPrickly.Id, Enemies.Rockanoff.Id,
            Enemies.PiratesChariot.Id,
        };
        const float minibossHpFactor = 3.0F;      //Miniboss max-HP multiplier (×3)
        const float minibossDefenseFactor = 1.5F; //Miniboss defense multiplier — DamageReduction + WeaponDefense (×1.5)
        const float minibossAttackFactor = 1.5F;  //Miniboss melee-damage multiplier (×1.5). Projectile damage is per-SPECIES (shared STB) so it is intentionally left unscaled.
        internal const float WalkAnimSyncFactor = 1.3F; //Slow a miniboss's WALK clip by this divisor so its (scaled-up, longer) strides plant the feet instead of skating. = scaleSize syncs foot-speed to ground-speed; lower = faster legs. Applied in HarderEnemies.ScaleAnimation, composing with the Faster-enemies anim multiplier.
        const float minibossReticleFactor = 1.5F; //Miniboss lock-on reticle size multiplier — visual marker (model is already scaleSize bigger)
        const int enemyABSMult = 4;               //Miniboss ABS multiplier
        const int enemyItemResistMulti = 10;      //Miniboss Item Resistance multiplier %
        const int enemyGoldMult = 4;              //Miniboss Gilda Loot multiplier
        const int enemyLootChance = 100;          //Miniboss Loot chance % (0 - 100)
        const bool forceAllMinibosses = true;    //TEST ONLY: promote EVERY eligible enemy to a miniboss (easier testing of miniboss changes); keep false for normal play

        public class MiniBossSnapshot
        {
            public int Slot;
            public ushort TypeId;
        }

        /// <summary>
        /// Picks and transforms enemies on the current floor into Champions (Minibosses).
        /// Each eligible enemy (non-zero ID, non-boss, not holding the floor key) has a
        /// 1-in-(15 minus ineligible count) chance of being chosen. All winners are transformed.
        /// </summary>
        public static bool MiniBossSpawn(byte dungeon = 255, byte floor = 255)
        {
            Thread.Sleep(200);

            miniBossEnemyNumbers.Clear();
            EnemyStatScaler.ResetSlotProjectile();   // drop last floor's per-slot projectile-scale cache

            // Count all enemies ineligible to become a miniboss (ID 0, boss, or holds the floor key)
            int ineligibleCount = 0;
            List<ushort> allIds = EnemySlots.GetFloorEnemiesIds();
            for (int i = 0; i < allIds.Count; i++)
            {
                ushort id = allIds[i];
                ushort dropVal = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop));
                if (id == 0 || Enemies.BossEnemies.ContainsKey(id) || (dropVal != 0 && dropVal != 65535))
                    ineligibleCount++;
            }

            int denominator = Math.Max(1, 15 - ineligibleCount);

            // Roll 1-in-denominator for each eligible enemy; all winners become minibosses
            List<int> winners = new List<int>();
            for (int i = 0; i < allIds.Count; i++)
            {
                ushort id = allIds[i];
                if (id == 0) continue;
                if (Enemies.BossEnemies.ContainsKey(id)) continue;   // never promote a boss/boss-companion (e.g. Ice Queen 113, IceArrow 84) to a miniboss
                ushort dropVal = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop));
                if (dropVal != 0 && dropVal != 65535) continue;
                if (forceAllMinibosses || rnd.Next(denominator) == 0)
                    winners.Add(i);
            }

            if (winners.Count == 0)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Failed to roll for Mini Boss!");
                miniBossRolled = false;
                return false;
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "\nIneligible count: " + ineligibleCount +
                "\nDenominator (1-in-N): " + denominator +
                "\nWinners: " + winners.Count + "\n");

            foreach (int slot in winners)
                ApplyMiniBossToSlot(slot, dungeon, floor);

            miniBossRolled = true;
            return true;
        }

        /// <summary>
        /// Per-tick: maintain miniboss PROJECTILE scaling (×<see cref="minibossAttackFactor"/>). HP/defense/melee are
        /// one-shot at spawn, but projectile damage's live source is the per-slot ShotDmgCache field that _SET_SHOT
        /// rewrites on every shot, so it has to be re-applied each tick (see EnemyStatScaler.MaintainSlotProjectile).
        /// Only the (few) miniboss slots are touched — never all enemies. No-op when no minibosses are rolled.
        /// </summary>
        public static void MaintainProjectileScale()
        {
            if (!miniBossRolled) return;
            foreach (int slot in miniBossEnemyNumbers)
                EnemyStatScaler.MaintainSlotProjectile(slot, minibossAttackFactor);
        }

        // Miniboss WALK-ANIMATION SYNC: a scaled-up miniboss has longer visual strides, but its walk clip plays at the
        // normal rate while the body moves at normal speed → the feet "skate". Per-slot MOVE speed can't be raised (the
        // engine rewrites it every frame — see EnemyAddresses.MoveControl), so we sync from the other side by SLOWING the
        // walk clip ×(1/WalkAnimSyncFactor). Animation speed IS holdable per-slot (set once per motion, not per frame),
        // so this lives in HarderEnemies.ScaleAnimation — the single PlayingMotionSpeed writer — where it composes
        // multiplicatively with the Faster-enemies animation multiplier (see WalkAnimSyncFactor above).

        // Species whose motion idx 1 isn't a literal walk but a FLYING/hover loop — slowing it looks wrong (no feet to
        // skate), so exclude them from the stride-sync. Keyed by species Id (enhanced variants share the base Id).
        static readonly HashSet<ushort> _walkSyncExclude = new HashSet<ushort>
        {
            Enemies.CaveBat.Id, Enemies.EvilBat.Id,
        };
        internal static bool WalkSyncExcluded(ushort speciesId) => _walkSyncExclude.Contains(speciesId);

        // ── Miniboss ATTACK RANGE (engage/commit distance) ────────────────────────────────────────────────
        // The AI decides when to attack by comparing the live _GET_DISTANCE (cmd 10) result against a float literal
        // baked in the species' loaded STB, re-read every frame. That literal is SHARED by every enemy of the species,
        // so we can't give one enemy its own reach — BUT only the enemy NEAREST the player drives the engaged-attack
        // decision. So each tick, for every species that has a miniboss on the floor, we find its nearest live enemy
        // and set the shared threshold to match THAT enemy's status: nearest is a miniboss → thresholds ×attackRangeFactor
        // (longer reach, matching its bigger body/attack hitbox); nearest is a regular → exact vanilla literals (regulars
        // feel untouched). The STB is only rewritten when the nearest's status flips (dedup). Caveat: while scaled, farther
        // same-species enemies briefly share the longer reach. Self-reverts on floor reload (STB reloads vanilla + the
        // per-floor cache clear below); no snapshot/restore needed.
        private const int FnGetDistance = 10;   // _GET_DISTANCE STB command
        private static readonly Dictionary<int, List<(int off, int origBits)>> _arThresholds = new(); // per loaded STB: _GET_DISTANCE literals (byte offset + vanilla IEEE-754 bits)
        private static readonly Dictionary<int, bool> _arScaled = new();                               // per loaded STB: currently scaled (miniboss reach) vs vanilla
        private static int _arLastFloorKey = -1;

        /// <summary>
        /// Per-tick: maintain miniboss ATTACK RANGE for every miniboss species on the floor, via the shared-STB
        /// "nearest enemy" trick above. No-op when no minibosses are rolled; reverts for free on floor change.
        /// </summary>
        public static void MaintainAttackRange()
        {
            if (!Player.InDungeonFloor()) { _arThresholds.Clear(); _arScaled.Clear(); _arLastFloorKey = -1; return; }

            int floorKey = (Memory.ReadByte(Addresses.checkDungeon) << 8) | Memory.ReadByte(Addresses.checkFloor);
            if (floorKey != _arLastFloorKey) { _arThresholds.Clear(); _arScaled.Clear(); _arLastFloorKey = floorKey; }  // STB reloads fresh each floor

            if (!miniBossRolled || miniBossEnemyNumbers.Count == 0) return;

            // Species that currently have at least one miniboss on the floor.
            HashSet<ushort> mbSpecies = new HashSet<ushort>();
            foreach (int slot in miniBossEnemyNumbers)
            {
                ushort sid = EnemySlots.GetFloorEnemyId(slot);
                if (sid != 0) mbSpecies.Add(sid);
            }
            if (mbSpecies.Count == 0) return;

            // One pass over the slots: the nearest live enemy of each miniboss-species.
            Dictionary<ushort, int>   nearestSlot = new Dictionary<ushort, int>();
            Dictionary<ushort, float> nearestDist = new Dictionary<ushort, float>();
            for (int s = 0; s < EnemyAddresses.FloorSlots.Count; s++)
            {
                ushort id = EnemySlots.GetFloorEnemyId(s);
                if (id == 0 || !mbSpecies.Contains(id)) continue;
                float dist = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(s, 0) + EnemySlotOffsets.DistanceToPlayer);
                if (!nearestDist.TryGetValue(id, out float cur) || dist < cur) { nearestDist[id] = dist; nearestSlot[id] = s; }
            }

            foreach (int slot in nearestSlot.Values)
                ApplyNearestRange(slot);
        }

        // Set the (shared) STB _GET_DISTANCE thresholds to scaled iff this nearest slot is a miniboss; rewrite only on flip.
        private static void ApplyNearestRange(int nearestSlot)
        {
            int stbNative = Memory.ReadInt(CRunScript.StbPtrAddr(nearestSlot));
            if (stbNative == 0 || stbNative == -1) return;

            bool wantScaled = miniBossEnemyNumbers.Contains(nearestSlot);
            bool isScaled   = _arScaled.TryGetValue(stbNative, out bool cur) && cur;
            if (isScaled == wantScaled) return;   // nearest's status unchanged → nothing to do

            // Long-reach species get a wider factor than the default.
            float factor = _attackRangeWide.Contains(EnemySlots.GetFloorEnemyId(nearestSlot))
                ? attackRangeFactorWide : attackRangeFactor;

            long stb = Memory.ToMmu(stbNative);
            foreach (var (off, origBits) in AttackRangeThresholds(stbNative))
            {
                int bits = wantScaled
                    ? BitConverter.SingleToInt32Bits(BitConverter.Int32BitsToSingle(origBits) * factor)
                    : origBits;
                Memory.WriteInt(stb + off, bits);
            }
            _arScaled[stbNative] = wantScaled;
        }

        // Walk a loaded STB once to record each _GET_DISTANCE threshold literal (byte offset + floor-fresh vanilla bits).
        private static List<(int off, int origBits)> AttackRangeThresholds(int stbNative)
        {
            if (_arThresholds.TryGetValue(stbNative, out var list)) return list;
            list = new List<(int, int)>();
            _arThresholds[stbNative] = list;

            long stb = Memory.ToMmu(stbNative);
            if (Memory.ReadInt(stb) != StbVm.Magic) return list;
            byte[] d = Memory.ReadByteArray(stb, 0xC000);
            if (d == null || d.Length < 0x10) return list;
            int Word(int i) => d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24);
            int code = Word(StbVm.CodeSectionOff);
            if (code < StbVm.LabelTableOff || code >= d.Length) return list;

            for (int x = code; x + StbVm.InstrSize <= d.Length; x += 4)
            {
                if (Word(x) != StbVm.OpExt || Word(x + StbVm.OperandB) != 0) continue;   // op21 ext-call
                int argc = Word(x + StbVm.OperandA);
                if (argc < 1 || argc > 10) continue;
                int fp = x - argc * StbVm.InstrSize;                                     // first pushed arg = funcId
                if (fp < code || Word(fp) != StbVm.OpPush3 || Word(fp + StbVm.OperandA) != StbVm.TypeInt) continue;
                if (Word(fp + StbVm.OperandB) != FnGetDistance) continue;

                // threshold = first op3 FLOAT literal within ~4 records after the call (skip variable comparisons)
                for (int k = 1; k <= 4; k++)
                {
                    int r = x + k * StbVm.InstrSize;
                    if (r + StbVm.InstrSize > d.Length) break;
                    if (Word(r) == StbVm.OpPush3 && Word(r + StbVm.OperandA) == StbVm.TypeFloat)
                    {
                        int litOff = r + StbVm.OperandB;
                        if (BitConverter.Int32BitsToSingle(Word(litOff)) > 0f)
                            list.Add((litOff, Word(litOff)));
                        break;
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Applies miniboss stat multipliers and rolls loot for a single enemy slot.
        /// Also registers the slot in miniBossEnemyNumbers.
        /// </summary>
        private static void ApplyMiniBossToSlot(int slot, byte dungeon, byte floor)
        {
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                "\nApplying miniboss to slot: " + slot +
                "\nEnemy ID: " + EnemySlots.GetFloorEnemyId(slot) + "\n");

            // Eligibility filtering should prevent a key-holder from ever being chosen.
            // This block should be unreachable — log a warning if it fires.
            if (EnemySlots.EnemyHasKey(slot, dungeon))
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WARNING] Miniboss ApplyMiniBossToSlot: slot " + slot + " holds a key — this should not happen with the current eligibility filter!");

            int startAbs    = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Abs));
            int startGold   = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.MinGoldDrop));
            ushort enemySpeciesId = EnemySlots.GetFloorEnemyId(slot);

            Memory.WriteFloat(enemyZeroWidth  + (scaleOffset * slot), scaleSize);
            Memory.WriteFloat(enemyZeroHeight + (scaleOffset * slot), scaleSize);
            Memory.WriteFloat(enemyZeroDepth  + (scaleOffset * slot), scaleSize);

            // Grow the DAMAGE HITBOX by the same factor as the visual scale, so the bigger miniboss body is hittable
            // across its new size. These are the per-bone _SET_BODY_COL sphere radii (see EnemyAddresses.BodyCollision):
            // CheckDmg rebuilds the live collision from them each frame, so scaling them once here persists. Each
            // enemy uses 1+ body parts; unused parts read 0 and are skipped. Default visual scale is 1.0, so scaleSize
            // is the growth factor (e.g. Skeleton radius 6.0 → 9.0 at 1.5×).
            for (int bodyPart = 0; bodyPart < BodyCollision.MaxBodyParts; bodyPart++)
            {
                long radiusAddr = BodyCollision.RadiusAddr(slot, bodyPart);
                float radius = Memory.ReadFloat(radiusAddr);
                if (radius > 0f) Memory.WriteFloat(radiusAddr, radius * scaleSize);
            }

            // Grow the WEAPON (attack) hitbox too. This is a SEPARATE per-slot array from the body hitbox above
            // (see EnemyAddresses.AttackCollision), so the body loop never touched it: at 1.5× model scale the weapon
            // bone rides higher while its attack sphere stayed default-size, overshooting short characters (only tall
            // ones like Ungaga got hit). Scale every occupied attack sphere by weaponHitboxFactor (> scaleSize) to
            // reach back down. Guarded to a plausible radius range so a bad read can't write garbage.
            for (int sphere = 0; sphere < AttackCollision.MaxSpheres; sphere++)
            {
                long rAddr = AttackCollision.RadiusAddr(slot, sphere);
                float r = Memory.ReadFloat(rAddr);
                if (r > 0f && r < 200f)
                    Memory.WriteFloat(rAddr, r * weaponHitboxFactor);
            }

            // Visual marker: enlarge the lock-on reticle so a targeted miniboss reads as special (its model is already
            // scaleSize bigger). One-shot like the hitbox; if the engine rewrites these per-frame it won't stick and
            // we'll move it to per-tick upkeep.
            float reticleW = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.ReticleWidth));
            float reticleH = Memory.ReadFloat(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.ReticleHeight));
            if (reticleW > 0f) Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.ReticleWidth),  reticleW * minibossReticleFactor);
            if (reticleH > 0f) Memory.WriteFloat(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.ReticleHeight), reticleH * minibossReticleFactor);
            // Stat buffs via the shared EnemyStatScaler pipeline: max HP ×3, defense ×1.5, melee damage ×1.5.
            // (Projectile damage is per-species/shared-STB — see EnemyStatScaler.ScaleProjectile — so it's left
            // unscaled to avoid buffing every same-species enemy on the floor.)
            EnemyStatScaler.ScaleHp(slot, minibossHpFactor);
            EnemyStatScaler.ScaleDefense(slot, minibossDefenseFactor, minibossDefenseFactor);
            if (Enemies.Defaults.TryGetValue(enemySpeciesId, out EnemyDefaults def))
                EnemyStatScaler.ScaleMelee(slot, def.MeleeDamage, minibossAttackFactor);

            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Abs),          startAbs * enemyABSMult);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.ItemResistance), enemyItemResistMulti);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.MinGoldDrop),  startGold * enemyGoldMult);
            Memory.WriteInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.DropChance),   enemyLootChance);

            // EnemySlots.EnableEnemyDrops patches the static species table on game load, so every spawn — minibosses
            // included — inherits the death-drop flag. See EnemySpeciesTable.StealFlag.)

            int[] weaponTable  = CustomChests.GetDungeonWeaponsTable(dungeon, floor);

            if (!TryApplyFlavorLoot(enemySpeciesId, dungeon, slot))
            {
                if (rnd.Next(100) < 35)
                {
                    WriteLootItem(Dungeon.GetDungeonBackFloorKey(dungeon), slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with backfloor key!");
                }
                else if (rnd.Next(100) < 15)
                {
                    WriteLootItem(weaponTable[rnd.Next(weaponTable.Length)], slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with weapon!");
                }
                else if (rnd.Next(100) < 80)
                {
                    if (rnd.Next(100) < 60) WriteLootItem(MiniBossLootTables.attachmentsTableLucky[rnd.Next(MiniBossLootTables.attachmentsTableLucky.Length)], slot);
                    else                    WriteLootItem(MiniBossLootTables.attachmentsTableUnlucky[rnd.Next(MiniBossLootTables.attachmentsTableUnlucky.Length)], slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with attachment!");
                }
                else
                {
                    if (rnd.Next(100) < 60) WriteLootItem(MiniBossLootTables.itemTableLucky[rnd.Next(MiniBossLootTables.itemTableLucky.Length)], slot);
                    else                    WriteLootItem(MiniBossLootTables.itemTableUnlucky[rnd.Next(MiniBossLootTables.itemTableUnlucky.Length)], slot);
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with item!");
                }
            }

            miniBossEnemyNumbers.Add(slot);
        }

        /// <summary>
        /// Snapshots all currently alive minibosses, clears miniboss state, and returns the snapshot.
        /// Returns an empty list (never null) if no minibosses are alive.
        /// </summary>
        public static List<MiniBossSnapshot> TakeSnapshot()
        {
            var snap = new List<MiniBossSnapshot>();
            foreach (int slot in miniBossEnemyNumbers)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(slot, EnemySlotOffsets.Hp)) > 0)
                    snap.Add(new MiniBossSnapshot { Slot = slot, TypeId = EnemySlots.GetFloorEnemyId(slot) });
            }
            miniBossRolled = false;
            miniBossEnemyNumbers.Clear();
            return snap;
        }

        /// <summary>
        /// Restores minibosses from a previous snapshot, re-applying stats to each alive entry.
        /// If a restored enemy was assigned the key during respawn, the key is transferred to
        /// another non-miniboss, non-key enemy before applying stats.
        /// </summary>
        public static void RestoreFromSnapshot(List<MiniBossSnapshot> snapshot, byte dungeon, byte floor)
        {
            if (snapshot == null || snapshot.Count == 0) return;

            // Build the set of slots being restored so key transfer avoids them
            var restoredSlots = new HashSet<int>();
            foreach (var entry in snapshot)
            {
                if (EnemySlots.GetFloorEnemyId(entry.Slot) == entry.TypeId)
                    restoredSlots.Add(entry.Slot);
            }

            foreach (var entry in snapshot)
            {
                if (EnemySlots.GetFloorEnemyId(entry.Slot) != entry.TypeId)
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WARNING] Miniboss restore: type mismatch at slot " + entry.Slot + ", skipping.");
                    continue;
                }

                // If the respawned enemy was assigned the key, transfer it away before applying stats
                if (EnemySlots.EnemyHasKey(entry.Slot, dungeon))
                {
                    Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss restore: key conflict at slot " + entry.Slot + ", transferring key.");
                    ushort keyId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(entry.Slot, EnemySlotOffsets.ForceItemDrop));
                    int newSlot = -1;
                    for (int i = 0; i < 15; i++)
                    {
                        if (restoredSlots.Contains(i) || i == entry.Slot) continue;
                        if (EnemySlots.GetFloorEnemyId(i) == 0) continue;
                        if (EnemySlots.EnemyHasKey(i, dungeon)) continue;
                        newSlot = i;
                        break;
                    }
                    if (newSlot >= 0)
                    {
                        Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(entry.Slot, EnemySlotOffsets.ForceItemDrop), 0);
                        Memory.WriteUShort(EnemyAddresses.FloorSlots.SlotAddr(newSlot, EnemySlotOffsets.ForceItemDrop), keyId);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Key transferred to slot " + newSlot);
                    }
                    else
                    {
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[WARNING] Miniboss restore: no valid slot found for key transfer!");
                    }
                }

                ApplyMiniBossToSlot(entry.Slot, dungeon, floor);
            }

            if (miniBossEnemyNumbers.Count > 0)
                miniBossRolled = true;
        }

        /// <summary>
        /// Rolls for and applies a dungeon-specific flavor loot to the miniboss.
        /// Checks flavorRare (5%) first, then flavorLoot (30%).
        /// Returns true if special loot was applied.
        /// </summary>
        private static bool TryApplyFlavorLoot(ushort enemySpeciesId, byte dungeon, int enemyNum)
        {
            Dictionary<ushort, int[]> rareLoot;
            Dictionary<ushort, int[]> flavorLoot;
            Dictionary<int, MiniBossLootTables.WeaponBoostData> rareBoosts;
            Dictionary<int, MiniBossLootTables.WeaponBoostData> flavorBoosts;

            switch (dungeon)
            {
                case 0: rareLoot = MiniBossLootTables.dbcFlavorRareLoot;  flavorLoot = MiniBossLootTables.dbcFlavorLoot;  rareBoosts = MiniBossLootTables.dbcWeaponBoosts;  flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 1: rareLoot = MiniBossLootTables.wofFlavorRareLoot;  flavorLoot = MiniBossLootTables.wofFlavorLoot;  rareBoosts = MiniBossLootTables.wofWeaponBoosts;  flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 2: rareLoot = MiniBossLootTables.shipFlavorRareLoot; flavorLoot = MiniBossLootTables.shipFlavorLoot; rareBoosts = MiniBossLootTables.shipWeaponBoosts; flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 3: rareLoot = MiniBossLootTables.sunFlavorRareLoot;  flavorLoot = MiniBossLootTables.sunFlavorLoot;  rareBoosts = MiniBossLootTables.sunWeaponBoosts;  flavorBoosts = MiniBossLootTables.emptyWeaponBoosts;        break;
                case 4: rareLoot = MiniBossLootTables.moonFlavorRareLoot; flavorLoot = MiniBossLootTables.moonFlavorLoot; rareBoosts = MiniBossLootTables.moonWeaponBoosts; flavorBoosts = MiniBossLootTables.moonFlavorWeaponBoosts;   break;
                case 5: rareLoot = MiniBossLootTables.galFlavorRareLoot;  flavorLoot = MiniBossLootTables.galFlavorLoot;  rareBoosts = MiniBossLootTables.galWeaponBoosts;  flavorBoosts = MiniBossLootTables.galFlavorWeaponBoosts;    break;
                case 6: rareLoot = MiniBossLootTables.dsFlavorRareLoot;   flavorLoot = MiniBossLootTables.dsFlavorLoot;   rareBoosts = MiniBossLootTables.dsWeaponBoosts;   flavorBoosts = MiniBossLootTables.dsFlavorWeaponBoosts;      break;
                default: return false;
            }

            if (rareLoot.TryGetValue(enemySpeciesId, out int[] rarePool) && rnd.Next(100) < 5)
            {
                int rareItem = rarePool[rnd.Next(rarePool.Length)];
                WriteLootItem(rareItem, enemyNum);
                if (rareBoosts.TryGetValue(rareItem, out MiniBossLootTables.WeaponBoostData boost))
                    StartInventoryBoostMonitor(boost, enemyNum);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with flavor rare loot!");
                return true;
            }

            if (flavorLoot.TryGetValue(enemySpeciesId, out int[] flavorPool) && rnd.Next(100) < 30)
            {
                int flavorItem = flavorPool[rnd.Next(flavorPool.Length)];
                WriteLootItem(flavorItem, enemyNum);
                if (flavorBoosts.TryGetValue(flavorItem, out MiniBossLootTables.WeaponBoostData flavorBoost))
                    StartInventoryBoostMonitor(flavorBoost, enemyNum);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Miniboss rolled with flavor loot!");
                return true;
            }

            return false;
        }

        // Weapons start at ID 257 in Items.cs; they require a 32-bit write.
        private static void WriteLootItem(int itemId, int enemyNum)
        {
            int lootAddress = EnemyAddresses.FloorSlots.SlotAddr(enemyNum, EnemySlotOffsets.ForceItemDrop);
            if (itemId >= 257)
                Memory.WriteInt(lootAddress, itemId);
            else
                Memory.WriteUShort(lootAddress, (ushort)itemId);
        }

        private static void StartInventoryBoostMonitor(MiniBossLootTables.WeaponBoostData boost, int enemyNum)
        {
            MiniBossLootTables.pendingBoost = boost;
            MiniBossLootTables.pendingBoostActive = true;
            MiniBossLootTables.boostMonitorThread = new Thread(() => InventoryBoostMonitorLoop(enemyNum)) { IsBackground = true };
            MiniBossLootTables.boostMonitorThread.Start();
        }

        private static void InventoryBoostMonitorLoop(int enemyNum)
        {
            const int weaponOffset     = 0xF8;
            const int attackOffset     = 0x04;
            const int enduranceOffset  = 0x06;
            const int speedOffset      = 0x08;
            const int magicOffset      = 0x0A;
            const int whpMaxOffset     = 0x0C;
            const int whpOffset        = 0x10;
            const int fireOffset       = 0x17;
            const int iceOffset        = 0x18;
            const int thunderOffset    = 0x19;
            const int windOffset       = 0x1A;
            const int holyOffset       = 0x1B;
            const int antiDragonOffset = 0x1C;
            const int antiUndeadOffset = 0x1D;
            const int antiMarineOffset = 0x1E;
            const int antiRockOffset   = 0x1F;
            const int antiPlantOffset  = 0x20;
            const int antiBeastOffset  = 0x21;
            const int antiSkyOffset    = 0x22;
            const int antiMimicOffset  = 0x24;
            const int antiMageOffset   = 0x25;
            const int special1Offset   = 0xEE;
            const int special2Offset   = 0xEF;
            const int toanSlots = 10;

            // Phase 1: wait for the miniboss to die so any weapon already in
            // inventory (e.g. from a chest) is present in the snapshot we take next.
            int hpAddress = EnemyAddresses.FloorSlots.SlotAddr(enemyNum, EnemySlotOffsets.Hp);
            int elapsed = 0;
            while (MiniBossLootTables.pendingBoostActive && Memory.ReadInt(hpAddress) > 0 && elapsed < 300000)
            {
                Thread.Sleep(250);
                elapsed += 250;
            }

            if (!MiniBossLootTables.pendingBoostActive) return;

            if (elapsed >= 300000)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "Weapon boost monitor timed out waiting for miniboss death.");
                MiniBossLootTables.pendingBoostActive = false;
                return;
            }

            // Phase 2: snapshot inventory now (post-kill), then watch until the
            // dropped weapon is picked up or the floor changes (MiniBossLootTables.pendingBoostActive → false).
            ushort[] snapshot = new ushort[toanSlots];
            for (int i = 0; i < toanSlots; i++)
                snapshot[i] = Memory.ReadUShort(Addresses.firstBagWeapon + (weaponOffset * i));

            while (MiniBossLootTables.pendingBoostActive)
            {
                Thread.Sleep(250);

                for (int i = 0; i < toanSlots; i++)
                {
                    int slotBase = Addresses.firstBagWeapon + (weaponOffset * i);
                    ushort current = Memory.ReadUShort(slotBase);
                    if (current == MiniBossLootTables.pendingBoost.WeaponId && snapshot[i] != MiniBossLootTables.pendingBoost.WeaponId)
                    {
                        if (MiniBossLootTables.pendingBoost.Attack > 0)     Memory.WriteUShort(slotBase + attackOffset,    MiniBossLootTables.pendingBoost.Attack);
                        if (MiniBossLootTables.pendingBoost.Endurance > 0)  Memory.WriteUShort(slotBase + enduranceOffset, MiniBossLootTables.pendingBoost.Endurance);
                        if (MiniBossLootTables.pendingBoost.Speed > 0)      Memory.WriteUShort(slotBase + speedOffset,     MiniBossLootTables.pendingBoost.Speed);
                        if (MiniBossLootTables.pendingBoost.Magic > 0)      Memory.WriteUShort(slotBase + magicOffset,     MiniBossLootTables.pendingBoost.Magic);
                        if (MiniBossLootTables.pendingBoost.Whp > 0)      { Memory.WriteUShort(slotBase + whpMaxOffset,    MiniBossLootTables.pendingBoost.Whp); Memory.WriteUShort(slotBase + whpOffset, MiniBossLootTables.pendingBoost.Whp); }
                        if (MiniBossLootTables.pendingBoost.Fire > 0)       Memory.WriteByte(slotBase + fireOffset,        MiniBossLootTables.pendingBoost.Fire);
                        if (MiniBossLootTables.pendingBoost.Ice > 0)        Memory.WriteByte(slotBase + iceOffset,         MiniBossLootTables.pendingBoost.Ice);
                        if (MiniBossLootTables.pendingBoost.Thunder > 0)    Memory.WriteByte(slotBase + thunderOffset,     MiniBossLootTables.pendingBoost.Thunder);
                        if (MiniBossLootTables.pendingBoost.Wind > 0)       Memory.WriteByte(slotBase + windOffset,        MiniBossLootTables.pendingBoost.Wind);
                        if (MiniBossLootTables.pendingBoost.Holy > 0)       Memory.WriteByte(slotBase + holyOffset,        MiniBossLootTables.pendingBoost.Holy);
                        if (MiniBossLootTables.pendingBoost.AntiDragon > 0) Memory.WriteByte(slotBase + antiDragonOffset,  MiniBossLootTables.pendingBoost.AntiDragon);
                        if (MiniBossLootTables.pendingBoost.AntiUndead > 0) Memory.WriteByte(slotBase + antiUndeadOffset,  MiniBossLootTables.pendingBoost.AntiUndead);
                        if (MiniBossLootTables.pendingBoost.AntiMarine > 0) Memory.WriteByte(slotBase + antiMarineOffset,  MiniBossLootTables.pendingBoost.AntiMarine);
                        if (MiniBossLootTables.pendingBoost.AntiRock > 0)   Memory.WriteByte(slotBase + antiRockOffset,    MiniBossLootTables.pendingBoost.AntiRock);
                        if (MiniBossLootTables.pendingBoost.AntiPlant > 0)  Memory.WriteByte(slotBase + antiPlantOffset,   MiniBossLootTables.pendingBoost.AntiPlant);
                        if (MiniBossLootTables.pendingBoost.AntiBeast > 0)  Memory.WriteByte(slotBase + antiBeastOffset,   MiniBossLootTables.pendingBoost.AntiBeast);
                        if (MiniBossLootTables.pendingBoost.AntiSky > 0)    Memory.WriteByte(slotBase + antiSkyOffset,     MiniBossLootTables.pendingBoost.AntiSky);
                        if (MiniBossLootTables.pendingBoost.AntiMimic > 0)  Memory.WriteByte(slotBase + antiMimicOffset,   MiniBossLootTables.pendingBoost.AntiMimic);
                        if (MiniBossLootTables.pendingBoost.AntiMage > 0)   Memory.WriteByte(slotBase + antiMageOffset,    MiniBossLootTables.pendingBoost.AntiMage);
                        if (MiniBossLootTables.pendingBoost.Special1 > 0)   Memory.WriteByte(slotBase + special1Offset,    MiniBossLootTables.pendingBoost.Special1);
                        if (MiniBossLootTables.pendingBoost.Special2 > 0)   Memory.WriteByte(slotBase + special2Offset,    MiniBossLootTables.pendingBoost.Special2);
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() +
                            $"Weapon boost applied to bag slot {i}:" +
                            (MiniBossLootTables.pendingBoost.Attack > 0     ? $" atk={MiniBossLootTables.pendingBoost.Attack}"              : "") +
                            (MiniBossLootTables.pendingBoost.Endurance > 0  ? $" end={MiniBossLootTables.pendingBoost.Endurance}"           : "") +
                            (MiniBossLootTables.pendingBoost.Speed > 0      ? $" spd={MiniBossLootTables.pendingBoost.Speed}"               : "") +
                            (MiniBossLootTables.pendingBoost.Magic > 0      ? $" mp={MiniBossLootTables.pendingBoost.Magic}"                : "") +
                            (MiniBossLootTables.pendingBoost.Whp > 0        ? $" whp={MiniBossLootTables.pendingBoost.Whp}"                 : "") +
                            (MiniBossLootTables.pendingBoost.Fire > 0       ? $" fire={MiniBossLootTables.pendingBoost.Fire}"               : "") +
                            (MiniBossLootTables.pendingBoost.Ice > 0        ? $" ice={MiniBossLootTables.pendingBoost.Ice}"                 : "") +
                            (MiniBossLootTables.pendingBoost.Thunder > 0    ? $" thunder={MiniBossLootTables.pendingBoost.Thunder}"         : "") +
                            (MiniBossLootTables.pendingBoost.Wind > 0       ? $" wind={MiniBossLootTables.pendingBoost.Wind}"               : "") +
                            (MiniBossLootTables.pendingBoost.Holy > 0       ? $" holy={MiniBossLootTables.pendingBoost.Holy}"               : "") +
                            (MiniBossLootTables.pendingBoost.AntiDragon > 0 ? $" aDragon={MiniBossLootTables.pendingBoost.AntiDragon}"      : "") +
                            (MiniBossLootTables.pendingBoost.AntiUndead > 0 ? $" aUndead={MiniBossLootTables.pendingBoost.AntiUndead}"      : "") +
                            (MiniBossLootTables.pendingBoost.AntiMarine > 0 ? $" aMarine={MiniBossLootTables.pendingBoost.AntiMarine}"      : "") +
                            (MiniBossLootTables.pendingBoost.AntiRock > 0   ? $" aRock={MiniBossLootTables.pendingBoost.AntiRock}"          : "") +
                            (MiniBossLootTables.pendingBoost.AntiPlant > 0  ? $" aPlant={MiniBossLootTables.pendingBoost.AntiPlant}"        : "") +
                            (MiniBossLootTables.pendingBoost.AntiBeast > 0  ? $" aBeast={MiniBossLootTables.pendingBoost.AntiBeast}"        : "") +
                            (MiniBossLootTables.pendingBoost.AntiSky > 0    ? $" aSky={MiniBossLootTables.pendingBoost.AntiSky}"            : "") +
                            (MiniBossLootTables.pendingBoost.AntiMimic > 0  ? $" aMimic={MiniBossLootTables.pendingBoost.AntiMimic}"        : "") +
                            (MiniBossLootTables.pendingBoost.AntiMage > 0   ? $" aMage={MiniBossLootTables.pendingBoost.AntiMage}"          : "") +
                            (MiniBossLootTables.pendingBoost.Special1 > 0   ? $" sp1=0x{MiniBossLootTables.pendingBoost.Special1:X2}"       : "") +
                            (MiniBossLootTables.pendingBoost.Special2 > 0   ? $" sp2=0x{MiniBossLootTables.pendingBoost.Special2:X2}"       : ""));
                        MiniBossLootTables.pendingBoostActive = false;
                        return;
                    }
                }
            }
        }
    }
}
