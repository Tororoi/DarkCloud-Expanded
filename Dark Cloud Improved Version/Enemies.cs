using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    class EnemySlots
    {
        public static Dictionary<ushort, string> GetNormalEnemies()
        {
            return Enemies.NormalEnemies;
        }

        public static Dictionary<ushort, string> GetFlyingEnemies()
        {
            return Enemies.FlyingEnemies;
        }

        public static Dictionary<ushort, string> GetOverseasEnemies()
        {
            return Enemies.OverseasEnemies;
        }

        /// <summary>
        /// Returns the display name for an enemy species ID, searching all lists.
        /// Returns "Unknown" if not found.
        /// </summary>
        public static string GetEnemyName(ushort typeId)
        {
            if (Enemies.NormalEnemies.TryGetValue(typeId, out string name))   return name;
            if (Enemies.FlyingEnemies.TryGetValue(typeId, out name))          return name;
            if (Enemies.BossEnemies.TryGetValue(typeId, out name))            return name;
            if (Enemies.OverseasEnemies.TryGetValue(typeId, out name))        return name;
            return "Unknown";
        }

        public static Dictionary<ushort, string> GetBossEnemies()
        {
            return Enemies.BossEnemies;
        }

        /// <summary>
        /// Returns the id of the given enemy number on the dungeon floor.
        /// </summary>
        public static ushort GetFloorEnemyId(int enemyFloorNum)
        {
            if (enemyFloorNum < 0 || enemyFloorNum > 15)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "GetFloorEnemyId() input fell outside of range: " + enemyFloorNum);
                return ushort.MinValue;
            }

            return Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(enemyFloorNum, EnemySlotOffsets.EnemySpeciesId));
        }

        /// <summary>
        /// Returns a list of all the enemy ids currently on the dungeon floor.
        /// </summary>
        public static List<ushort> GetFloorEnemiesIds()
        {
            List<ushort> Ids = new List<ushort>();

            for (int i = 0; i < 15; i++)
            {
                Ids.Add(Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId)));
            }

            return Ids;
        }

        /// <summary>
        /// Returns true if the given enemy number on the dungeon floor is holding the dungeon key.
        /// </summary>
        public static bool EnemyHasKey(int enemyNumber, byte dungeon)
        {
            return Dungeon.GetDungeonGateKey(dungeon).Contains(Memory.ReadByte(EnemyAddresses.FloorSlots.SlotAddr(enemyNumber, EnemySlotOffsets.ForceItemDrop)));
        }

        // ── Research tools ──────────────────────────────────────────────────────

        /// <summary>
        /// Set true before entering a WOF floor to overwrite up to 5 active enemy slots with
        /// EnemySpeciesId=54 (Killer Snake) on the next CheckSpawns call. Clears itself after one use.
        /// </summary>
        internal static bool ForceKillerSnakeActive = true;

        /// <summary>
        /// Overwrites EnemySpeciesId in up to 5 active non-miniboss slots to 54 (Killer Snake),
        /// then calls DumpEnemySlot on each so the full slot layout is logged. Invoke from a WOF
        /// floor to observe what the engine does with eid=54 in a live slot.
        /// </summary>
        internal static void ForceKillerSnakeSlots()
        {
            Console.WriteLine("[ForceKS] Overwriting up to 5 active slots → EnemySpeciesId=54...");
            int replaced = 0;
            for (int i = 0; i < 16 && replaced < 5; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                int status = Memory.ReadInt(slotBase + EnemySlotOffsets.RenderStatus);
                if (status <= 0) continue;
                if (MiniBoss.miniBossEnemyNumbers.Contains(i)) continue;

                ushort oldId = Memory.ReadUShort(slotBase + EnemySlotOffsets.EnemySpeciesId);
                Memory.WriteUShort(slotBase + EnemySlotOffsets.EnemySpeciesId, 54);
                Console.WriteLine($"[ForceKS] slot={i} id={oldId}→54");
                replaced++;
            }
            Console.WriteLine($"[ForceKS] {replaced} slot(s) set to eid=54. Full dumps follow:");
            for (int i = 0; i < 16; i++)
            {
                int status = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus));
                if (status > 0 && !MiniBoss.miniBossEnemyNumbers.Contains(i))
                    DumpEnemySlot(i);
            }
        }

        private static readonly Dictionary<int, int[]> _enemySlotSnapshots = new Dictionary<int, int[]>();

        internal static void DumpEnemySlot(int slotIndex)
        {
            int slotBase = EnemyAddresses.FloorSlots.SlotAddr(slotIndex, 0);
            ushort nameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(slotIndex, EnemySlotOffsets.EnemySpeciesId));
            string name = GetEnemyName(nameId);
            bool isMiniboss = MiniBoss.miniBossEnemyNumbers.Contains(slotIndex);
            string tag = isMiniboss ? " [MINIBOSS - DATA INVALID FOR DATABASE]" : "";
            Console.WriteLine($"[EnemyDump] slot={slotIndex} {name} (id={nameId}) base=0x{slotBase:X8}{tag}");
            if (isMiniboss) return;
            for (int off = 0; off < 0x190; off += 4)
            {
                int raw = Memory.ReadInt(slotBase + off);
                float asFloat = Memory.ReadFloat(slotBase + off);
                Console.WriteLine($"[EnemyDump] slot={slotIndex} +0x{off:X3}  raw=0x{raw:X8}  int={raw,-12}  float={asFloat:F4}");
            }
        }

        internal static void DumpAllActiveEnemySlots()
        {
            for (int i = 0; i < 16; i++)
            {
                int status = Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus));
                if (status != -1 && status != 0)
                    DumpEnemySlot(i);
            }
        }

        /// <summary>
        /// Logs a concise summary of all known database fields for each active non-miniboss slot.
        /// Use this instead of DumpAllActiveEnemySlots for ongoing database population.
        /// </summary>
        internal static void LogEnemySpawns()
        {
            byte dungeon = Memory.ReadByte(Addresses.checkDungeon);
            byte floor   = Memory.ReadByte(Addresses.checkFloor);
            string dungeonName = Dungeons.TryGetValue(dungeon, out DungeonData dd) ? dd.Name : $"dungeon{dungeon}";
            Console.WriteLine($"[EnemyInfo] {dungeonName} (id={dungeon}) floor={floor + 1}");

            for (int i = 0; i < 16; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;
                if (MiniBoss.miniBossEnemyNumbers.Contains(i)) continue;

                ushort nameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                string name = GetEnemyName(nameId);

                int maxHp      = Memory.ReadInt(slotBase + EnemySlotOffsets.MaxHp);
                int abs        = Memory.ReadInt(slotBase + EnemySlotOffsets.Abs);
                int minGold    = Memory.ReadInt(slotBase + EnemySlotOffsets.MinGoldDrop);
                int dropChance = Memory.ReadInt(slotBase + EnemySlotOffsets.DropChance);

                int p1 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack1);
                int p2 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack2);
                int p3 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack3);
                ushort res1 = (ushort)(p1 & 0xFFFF); ushort fire = (ushort)(p1 >> 16);
                ushort ice  = (ushort)(p2 & 0xFFFF); ushort thun = (ushort)(p2 >> 16);
                ushort wind = (ushort)(p3 & 0xFFFF); ushort holy = (ushort)(p3 >> 16);

                float scale    = Memory.ReadFloat(slotBase + EnemySlotOffsets.EntityScale);
                int unk090     = Memory.ReadInt(slotBase + EnemySlotOffsets.Unk090);
                ushort u90a    = (ushort)(unk090 & 0xFFFF);
                ushort u90b    = (ushort)(unk090 >> 16);
                ushort stealId = (ushort)(Memory.ReadInt(slotBase + EnemySlotOffsets.StealItemId) & 0xFFFF);
                int itemResRaw = Memory.ReadInt(slotBase + EnemySlotOffsets.ItemResistance);
                ushort irA     = (ushort)(itemResRaw & 0xFFFF);
                ushort irB     = (ushort)(itemResRaw >> 16);
                float reticleW = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleWidth);
                float reticleH = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleHeight);

                int   scaleBase = ModelScaleOffsets.ModelBase + ModelScaleOffsets.ModelStride * i;
                float unk020    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.Unk020);
                float unk024    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.Unk024);
                float unk028    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.Unk028);
                int   dataSize  = Memory.ReadInt  (scaleBase + ModelScaleOffsets.DataSize);
                int   animCount = Memory.ReadInt  (scaleBase + ModelScaleOffsets.AnimCount);

                Console.WriteLine($"[EnemyInfo] slot={i} {name} (id={nameId}) hp={maxHp} abs={abs} gold={minGold} drop={dropChance}% | type={res1} fire={fire} ice={ice} thun={thun} wind={wind} holy={holy}");
                Console.WriteLine($"[EnemyInfo] slot={i} {name} scale={scale:F1} 090={u90a}/{u90b} steal=0x{stealId:X3} itemRes={irA}/{irB} reticle={reticleW:F2}x{reticleH:F2} | unk020={unk020:F1} unk024={unk024:F1} unk028={unk028:F1} dataSize={dataSize} animCount={animCount}");
            }
        }

        /// <summary>
        /// Dumps the model/render scale table (separate from enemy slots).
        /// Base 0x21E18530, stride 0x3510 — mirrors the width/height/depth used by MiniBoss scaling.
        /// </summary>
        internal static void DumpModelScaleTable()
        {
            const int modelBase   = 0x21E18530;
            const int modelStride = 0x3510;
            for (int i = 0; i < 16; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;

                ushort nameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                string name = GetEnemyName(nameId);
                bool isMiniboss = MiniBoss.miniBossEnemyNumbers.Contains(i);
                string tag = isMiniboss ? " [MINIBOSS]" : "";

                int scaleBase = modelBase + modelStride * i;
                Console.WriteLine($"[ModelScale] slot={i} {name} (id={nameId}) base=0x{scaleBase:X8}{tag}");
                for (int off = 0; off < 0x3510; off += 4)
                {
                    int raw = Memory.ReadInt(scaleBase + off);
                    float asFloat = Memory.ReadFloat(scaleBase + off);
                    Console.WriteLine($"[ModelScale] slot={i} +0x{off:X3}  raw=0x{raw:X8}  int={raw,-12}  float={asFloat:F4}");
                }
            }
        }

        private static readonly HashSet<int> _exhaustedSlots = new HashSet<int>();
        private static readonly HashSet<int> _activationLoggedSlots = [];

        internal static void ResetPollState()
        {
            _enemySlotSnapshots.Clear();
            _exhaustedSlots.Clear();
            _activationLoggedSlots.Clear();
            _flashTimerSnapshot.Clear();
        }

        internal static void PollEnemyDynamics()
        {
            const int wordCount = 0x190 / 4;
            const float maxDistance = 50.0f;

            bool anyChanges = false;

            for (int i = 0; i < 16; i++)
            {
                if (_exhaustedSlots.Contains(i)) continue;

                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;

                float dist = Memory.ReadFloat(slotBase + EnemySlotOffsets.DistanceToPlayer);
                if (dist > maxDistance) continue;

                int[] current = new int[wordCount];
                for (int w = 0; w < wordCount; w++)
                    current[w] = Memory.ReadInt(slotBase + w * 4);

                int hp = Memory.ReadInt(slotBase + EnemySlotOffsets.Hp);
                if (hp <= 0)
                    _exhaustedSlots.Add(i);

                if (status == 2 && !_activationLoggedSlots.Contains(i))
                {
                    _activationLoggedSlots.Add(i);
                    ushort actNameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    string actName   = GetEnemyName(actNameId);
                    string actMbTag  = MiniBoss.miniBossEnemyNumbers.Contains(i) ? " [MINIBOSS]" : "";
                    float  F(int w)  => BitConverter.ToSingle(BitConverter.GetBytes(current[w]), 0);
                    int    speciesPtr   = current[0x04C / 4];
                    float  moveBlend = F(0x080 / 4);
                    float  facingX   = F(0x060 / 4);
                    float  facingY   = F(0x064 / 4);
                    float  facingZ   = F(0x068 / 4);
                    float  targetX   = F(0x070 / 4);
                    float  targetZ   = F(0x074 / 4);
                    float  targetY   = F(0x078 / 4);
                    int    aiState   = current[0x0EC / 4];
                    float  aiSpeed   = F(0x0F0 / 4);
                    float  rangeX    = F(0x140 / 4);
                    float  rangeY    = F(0x144 / 4);
                    float  rangeZ    = F(0x148 / 4);
                    float  aggro     = F(0x14C / 4);
                    Console.WriteLine($"[EnemyActivation] slot={i} {actName}{actMbTag} speciesPtr=0x{speciesPtr:X8} moveBlend={moveBlend:F2} facing=({facingX:F4},{facingY:F4},{facingZ:F4})");
                    Console.WriteLine($"[EnemyActivation] slot={i} {actName}{actMbTag} target=({targetX:F1},{targetZ:F1},{targetY:F1}) aiState=0x{aiState:X8} aiSpeed={aiSpeed:F4} aggroRange={aggro:F1} behaviorRange=({rangeX:F1},{rangeY:F1},{rangeZ:F1})");
                }

                if (_enemySlotSnapshots.TryGetValue(i, out int[] prev))
                {
                    ushort nameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    string name = GetEnemyName(nameId);
                    bool isMiniboss = MiniBoss.miniBossEnemyNumbers.Contains(i);
                    string mbTag = isMiniboss ? " [MINIBOSS]" : "";
                    for (int w = 0; w < wordCount; w++)
                    {
                        if (current[w] != prev[w])
                        {
                            if (!anyChanges)
                            {
                                anyChanges = true;
                                float px = Memory.ReadFloat(Player.dunPositionX);
                                float py = Memory.ReadFloat(Player.dunPositionY);
                                float pz = Memory.ReadFloat(Player.dunPositionZ);
                                int charId = Memory.ReadByte(Player.currentCharacter);
                                string charName = Player.GetCharacterName(charId) ?? "Unknown";
                                ushort charHp = Player.Toan.GetHp();
                                int lastDmg = Memory.ReadInt(Player.mostRecentDamage);
                                int dmgSrc = Memory.ReadInt(Player.damageSource);
                                int animId = Memory.ReadInt(Player.animationId);
                                Console.WriteLine($"[PlayerState] {charName} hp={charHp} pos=({px:F1},{py:F1},{pz:F1}) anim={animId} lastDmg={lastDmg} dmgSrc={dmgSrc}");
                            }
                            int off = w * 4;
                            float asFloat = BitConverter.ToSingle(BitConverter.GetBytes(current[w]), 0);
                            Console.WriteLine($"[EnemyPoll] slot={i} {name}{mbTag} +0x{off:X3}  0x{prev[w]:X8} -> 0x{current[w]:X8}  (int:{current[w]}  float:{asFloat:F4})");
                        }
                    }
                }
                _enemySlotSnapshots[i] = current;
            }
        }

        private static DateTime _lastSlotWriteTime = DateTime.MinValue;
        private static int _slotWriteCycle = 0;

        private static readonly float[] _decayRateTestValues = [ 0.0f, 0.1f, 0.5f, 1.0f, 5.0f ];

        internal static void ApplyTestModifications()
        {
            for (int i = 0; i < 16; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;
                if (MiniBoss.miniBossEnemyNumbers.Contains(i)) continue;

                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorRed,   255.0f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorGreen, 255.0f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorBlue,  255.0f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashDecayRate,  0.016f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashTimer,      1.0f);
                Memory.WriteInt(slotBase + EnemySlotOffsets.FlashActivation,   1);

                ushort nameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                string name = GetEnemyName(nameId);
                Console.WriteLine($"[TestMod] slot={i} {name}  flash set: decayRate=0.016 timer=1.0");
            }
        }

        private static readonly Dictionary<int, float> _flashTimerSnapshot = [];

        private static DateTime _lastFlashTriggerTime = DateTime.MinValue;

        internal static void MonitorFlashTimer()
        {
            bool triggerFlash = (DateTime.UtcNow - _lastFlashTriggerTime).TotalSeconds >= 3.0;
            if (triggerFlash) _lastFlashTriggerTime = DateTime.UtcNow;

            for (int i = 0; i < 16; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;

                if (triggerFlash)
                {
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorRed,   255.0f);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorGreen, 255.0f);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorBlue,  255.0f);
                    // FlashTimer is engine-owned and always reset to 1.0; FlashDecayRate controls duration: 1.0/rate frames.
                    // At 30fps: 0.016 ≈ 2s, 0.08 ≈ 12 frames (natural hit flash).
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashDecayRate, 0.016f);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashTimer,        1.0f);
                    Memory.WriteInt(slotBase + EnemySlotOffsets.FlashActivation,   1);
                    _flashTimerSnapshot[i] = 1.0f;
                    ushort nameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    string name = GetEnemyName(nameId);
                    Console.WriteLine($"[FlashTimer] slot={i} {name}  flash set: decayRate=0.016 timer=1.0");
                    continue;
                }

                float duration = Memory.ReadFloat(slotBase + EnemySlotOffsets.FlashDecayRate);
                float timer    = Memory.ReadFloat(slotBase + EnemySlotOffsets.FlashTimer);
                int   active   = Memory.ReadInt(slotBase + EnemySlotOffsets.FlashActivation);

                _flashTimerSnapshot.TryGetValue(i, out float prevTimer);
                if (Math.Abs(timer - prevTimer) > 0.0001f)
                {
                    ushort nameId = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    string name = GetEnemyName(nameId);
                    Console.WriteLine($"[FlashTimer] slot={i} {name}  active={active}  decayRate={duration:F4}  flashTimer={timer:F4}");
                    _flashTimerSnapshot[i] = timer;
                }
            }
        }

        /// <summary>
        /// Writes Gyon's model code into Dasher's species table entry (both ModelCode and
        /// ModelCodeCopy) so the engine loads Gyon's mesh for any Dasher spawn.
        /// </summary>
        internal static void RedirectDasherToGyonModel()
        {
            byte[] code  = System.Text.Encoding.ASCII.GetBytes(Enemies.MinotaurJoe.ModelCode);
            int entityId = Enemies.MinotaurJoe.Id;
            // float entityScale = Memory.ReadFloat(EnemySpeciesTable.FieldAddress(Enemies.Dran.TableIndex.Value, EnemySpeciesTable.EntityScale));
            int modelAddr     = EnemySpeciesTable.FieldAddress(Enemies.Dasher.TableIndex.Value, EnemySpeciesTable.ModelCode);
            int copyAddr = EnemySpeciesTable.FieldAddress(Enemies.Dasher.TableIndex.Value, EnemySpeciesTable.ModelCodeCopy);
            int entityScaleAddr = EnemySpeciesTable.FieldAddress(Enemies.Dasher.TableIndex.Value, EnemySpeciesTable.EntityScale);
            int entityIdAddr = EnemySpeciesTable.FieldAddress(Enemies.Dasher.TableIndex.Value, EnemySpeciesTable.EnemySpeciesId);
            Memory.WriteInt(entityIdAddr, entityId);
            Memory.WriteByteArray(modelAddr,     code);
            Memory.WriteByteArray(copyAddr, code);
            // Memory.WriteFloat(entityScaleAddr, entityScale);
            Console.WriteLine($"[ModelRedirect] Dasher species table model → {Enemies.Dran.ModelCode} (was {Enemies.Dasher.ModelCode})");
        }

        /// <summary>
        /// Corrects the world position of any boss-model slot that was displaced by the
        /// model-redirect spawn logic (which sets position to the boss arena origin instead
        /// of a floor spawn point). Targets any slot whose species ID appears in
        /// <see cref="Enemies.BossEnemies"/>, since <see cref="RedirectDasherToGyonModel"/>
        /// now writes the redirected boss's own ID into the patched species table entry.
        ///
        /// Teleports displaced slots to a random active chest position — chests are guaranteed
        /// to be on walkable tiles, making them a reliable source of valid floor coordinates
        /// without needing the tile map. Falls back to the player's dungeon position if no
        /// active chests are found on the current floor.
        ///
        /// The Z (elevation) component is sourced from the mean of non-boss active slots, or
        /// the player's Z, since the chest table does not store elevation.
        ///
        /// Call this a few frames after floor entry, after enemy slots have settled.
        /// </summary>
        internal static void FixModelRedirectSpawnPositions()
        {
            const float displacedThreshold = 300.0f;

            // Collect active chest positions — guaranteed walkable floor locations.
            int rawCount = Memory.ReadInt(ChestAddresses.ChestSlots.CountAddr);
            int chestCount = Math.Min(rawCount, 16); // guard against corrupted count
            var activeChests = new List<(float x, float y)>();
            for (int c = 0; c < chestCount; c++)
            {
                int activeFlag = Memory.ReadInt(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.ActiveFlag));
                if (activeFlag != 1) continue;
                float cx = Memory.ReadFloat(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.WorldX));
                float cy = Memory.ReadFloat(ChestAddresses.ChestSlots.SlotAddr(c, ChestSlotOffsets.WorldY));
                activeChests.Add((cx, cy));
            }

            // Build reference elevation from non-boss, non-miniboss active slots.
            // The chest table has no Z/height, so we derive it from peers.
            float sumZ = 0;
            int zCount = 0;
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                if (Memory.ReadInt(slotBase) <= 0) continue;
                if (MiniBoss.miniBossEnemyNumbers.Contains(i)) continue;
                ushort id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                if (Enemies.BossEnemies.ContainsKey(id)) continue;
                sumZ += Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationZ);
                zCount++;
            }
            float refZ = zCount > 0
                ? sumZ / zCount
                : Memory.ReadFloat(Player.dunPositionZ);

            var rng = new Random();

            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                if (Memory.ReadInt(slotBase) <= 0) continue;
                ushort id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                if (!Enemies.BossEnemies.ContainsKey(id)) continue;

                float x = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationX);
                float y = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationY);

                float targetX, targetY;
                if (activeChests.Count > 0)
                {
                    var chest = activeChests[rng.Next(activeChests.Count)];
                    targetX = chest.x;
                    targetY = chest.y;
                }
                else
                {
                    targetX = Memory.ReadFloat(Player.dunPositionX);
                    targetY = Memory.ReadFloat(Player.dunPositionY);
                }

                float dx = x - targetX, dy = y - targetY;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist <= displacedThreshold) continue;

                Memory.WriteFloat(slotBase + EnemySlotOffsets.LocationX, targetX);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.LocationY, targetY);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.LocationZ, refZ);
                Console.WriteLine($"[ModelRedirect] slot={i} id={id} position corrected ({x:F1},{y:F1}) → ({targetX:F1},{targetY:F1}), dist was {dist:F1}");
            }
        }

        /// <summary>
        /// Scans candidate memory regions for walkable tile-map data and writes a hex dump
        /// to tilemap_search.txt alongside the player and enemy world positions for each floor.
        /// Pass 2 adds the unscanned 0x2094-0x2097 gap, post-enemy areas, and a pointer sniff
        /// of the dungeon variable block (0x202A355C-0x202A35FC) to follow any PS2-native
        /// RAM pointers into regions we haven't covered yet.
        /// </summary>
        internal static void LogFloorDataForTileMapSearch()
        {
            string logPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "tilemap_search.txt");

            using var w = System.IO.File.AppendText(logPath);

            byte dun = Memory.ReadByte(Addresses.checkDungeon);
            byte fl  = Memory.ReadByte(Addresses.checkFloor);
            float px = Memory.ReadFloat(Player.dunPositionX);
            float py = Memory.ReadFloat(Player.dunPositionY);
            float pz = Memory.ReadFloat(Player.dunPositionZ);

            w.WriteLine();
            w.WriteLine($"=== Dun={dun} Floor={fl} @ {DateTime.Now:HH:mm:ss} Player=({px:F2},{py:F2},{pz:F2}) ===");

            for (int i = 0; i < 16; i++)
            {
                int sBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                if (Memory.ReadInt(sBase) <= 0) continue;
                float ex = Memory.ReadFloat(sBase + EnemySlotOffsets.LocationX);
                float ey = Memory.ReadFloat(sBase + EnemySlotOffsets.LocationY);
                float ez = Memory.ReadFloat(sBase + EnemySlotOffsets.LocationZ);
                ushort eid = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                w.WriteLine($"  Slot[{i:D2}] species={eid:D3} X={ex:F2} Y={ey:F2} Z={ez:F2}");
            }

            // Pointer sniff: read every 4-byte word in the dungeon variable block.
            // Values in PS2 native RAM range (0x00100000–0x01FFFFFF) are pointers —
            // add 0x20000000 to get the PCSX2 address. These are the candidates most
            // likely to point at dynamically-allocated tile-map buffers.
            w.WriteLine("\n-- Pointer sniff 0x202A355C–0x202A35FC --");
            var ptrTargets = new List<uint>();
            for (int addr = 0x202A355C; addr <= 0x202A35FC; addr += 4)
            {
                uint v = (uint)Memory.ReadInt(addr);
                bool isNativePtr  = v is >= 0x00100000 and <= 0x01FFFFFF;
                bool isCachedPtr  = v is >= 0x80100000 and <= 0x81FFFFFF;
                if (isNativePtr || isCachedPtr)
                {
                    uint pcsx2 = (v & 0x01FFFFFFu) + 0x20000000u;
                    w.WriteLine($"  [0x{addr:X8}] = 0x{v:X8}  → PCSX2: 0x{pcsx2:X8}  <-- POINTER");
                    ptrTargets.Add(pcsx2);
                }
                else
                {
                    w.WriteLine($"  [0x{addr:X8}] = 0x{v:X8}");
                }
            }

            // Regions to scan — drop confirmed-useless areas (VU code at 201C7/201DC,
            // all-zero blocks at 20980/20990/21CD0). 0x21DD0000 gets 1024 B (64 rows)
            // to capture the full room-layout grid beyond offset 0x130.
            int[] regions = {
                // Dungeon AI event scripts (change per floor)
                0x20920000, 0x20928000,
                // Asset string table (changes per dungeon)
                0x20940000,
                // THE UNSCANNED GAP — most likely location for MPD tile/nav data
                0x20948000, 0x20950000, 0x20958000,
                0x20960000, 0x20968000, 0x20970000, 0x20978000,
                // Dialogue / item name text
                0x21CC0000,
                // Teleport cost table + room layout grid (64 rows = 1024 B)
                0x21DD0000,
                // Per-room chest spawn data
                0x21DE0000,
                // Enemy instance data + two pages immediately after
                0x21E16800, 0x21E18000, 0x21E20000,
            };

            // Also dump any pointer targets from the sniff that aren't already covered
            var covered = new HashSet<uint>();
            foreach (int r in regions) covered.Add((uint)r);
            foreach (uint pt in ptrTargets)
            {
                uint aligned = pt & 0xFFFF8000u; // round down to 32 KB boundary
                if (covered.Add(aligned) && covered.Add(aligned + 0x8000))
                    regions = [.. regions, (int)aligned];
            }

            foreach (int rBase in regions)
            {
                int rowCount = (rBase == 0x21DD0000) ? 64 : 32;
                int size = rowCount * 16;
                var buf = new byte[size];
                for (int row = 0; row < rowCount; row++)
                    for (int col = 0; col < 4; col++)
                    {
                        int v = Memory.ReadInt(rBase + row * 16 + col * 4);
                        buf[row * 16 + col * 4]     = (byte)(v & 0xFF);
                        buf[row * 16 + col * 4 + 1] = (byte)((v >> 8)  & 0xFF);
                        buf[row * 16 + col * 4 + 2] = (byte)((v >> 16) & 0xFF);
                        buf[row * 16 + col * 4 + 3] = (byte)((v >> 24) & 0xFF);
                    }

                int score = 0;
                for (int row = 0; row < rowCount; row++)
                {
                    bool small = true;
                    for (int col = 0; col < 16; col++)
                        if (buf[row * 16 + col] > 15) { small = false; break; }
                    if (small) score++;
                }

                HashSet<byte> distinct = [..buf];

                w.WriteLine($"\n-- 0x{rBase:X8} [tile-score={score}/{rowCount}, distinct={distinct.Count}] --");
                for (int row = 0; row < rowCount; row++)
                {
                    w.Write($"  {rBase + row * 16:X8}: ");
                    for (int col = 0; col < 16; col++)
                        w.Write($"{buf[row * 16 + col]:X2} ");
                    w.WriteLine();
                }
            }

            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[TileMap] Logged Dun={dun} Floor={fl} → {logPath}");
        }

        internal class Digger
        {
            public const int maxJumpDistance = 0x213F3D70;
        }
    }
}
