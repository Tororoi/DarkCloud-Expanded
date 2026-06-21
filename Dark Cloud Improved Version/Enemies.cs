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
                int defStats   = Memory.ReadInt(slotBase + EnemySlotOffsets.DefenseStats);
                ushort u90a    = (ushort)(defStats & 0xFFFF);   // DamageReduction
                ushort u90b    = (ushort)(defStats >> 16);      // WeaponDefense
                ushort stealId = (ushort)(Memory.ReadInt(slotBase + EnemySlotOffsets.StealItemId) & 0xFFFF);
                int itemResRaw = Memory.ReadInt(slotBase + EnemySlotOffsets.ItemResistance);
                ushort irA     = (ushort)(itemResRaw & 0xFFFF);
                ushort irB     = (ushort)(itemResRaw >> 16);
                float reticleW = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleWidth);
                float reticleH = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleHeight);

                int   scaleBase = ModelScaleOffsets.ModelBase + ModelScaleOffsets.ModelStride * i;
                float bodyWidth    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.BodyWidth);
                float bodyHeight    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.BodyHeight);
                float bodyDepth    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.BodyDepth);
                int   dataSize  = Memory.ReadInt  (scaleBase + ModelScaleOffsets.DataSize);
                int   animCount = Memory.ReadInt  (scaleBase + ModelScaleOffsets.AnimCount);

                Console.WriteLine($"[EnemyInfo] slot={i} {name} (id={nameId}) hp={maxHp} abs={abs} gold={minGold} drop={dropChance}% | type={res1} fire={fire} ice={ice} thun={thun} wind={wind} holy={holy}");
                Console.WriteLine($"[EnemyInfo] slot={i} {name} scale={scale:F1} def={u90a}/{u90b} steal=0x{stealId:X3} itemRes={irA}/{irB} reticle={reticleW:F2}x{reticleH:F2} | bodyWidth={bodyWidth:F1} bodyHeight={bodyHeight:F1} bodyDepth={bodyDepth:F1} dataSize={dataSize} animCount={animCount}");
            }
        }

        /// <summary>
        /// Readable per-slot dump for boss fights (esp. Ice Queen) — like <see cref="LogEnemySpawns"/> but tuned for
        /// live combat: current/max HP, collision scale, lock-on reticle, world position, playing motion, and the
        /// resistance packs (scale: 0=immune, &lt;100=resistant, 100=neutral, &gt;100=weak — so the shield's magic
        /// immunity and the Queen's physical immunity are visible). Dumps EVERY live slot incl. eid-0 sub-entities,
        /// no miniboss filter. <paramref name="tag"/> labels the snapshot.
        /// </summary>
        internal static void BossInfo(string tag)
        {
            Console.WriteLine($"[BossInfo] === {tag} ===");
            for (int i = 0; i < 16; i++)
            {
                int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                if (Memory.ReadInt(slotBase) == -1) continue;

                ushort eid  = Memory.ReadUShort(slotBase + EnemySlotOffsets.EnemySpeciesId);
                string name = GetEnemyName(eid);
                int hp      = Memory.ReadInt(slotBase + EnemySlotOffsets.Hp);
                int maxHp   = Memory.ReadInt(slotBase + EnemySlotOffsets.MaxHp);
                float scale = Memory.ReadFloat(slotBase + EnemySlotOffsets.EntityScale);
                float rw    = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleWidth);
                float rh    = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleHeight);
                float x     = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationX);
                float y     = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationY);
                int mot     = Memory.ReadInt(EnemyAddresses.CharObjects.CharAddr(i) + 0xC68);   // playing motion id

                int p1 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack1);
                int p2 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack2);
                int p3 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack3);
                ushort res1 = (ushort)(p1 & 0xFFFF), fire = (ushort)(p1 >> 16);
                ushort ice  = (ushort)(p2 & 0xFFFF), thun = (ushort)(p2 >> 16);
                ushort wind = (ushort)(p3 & 0xFFFF), holy = (ushort)(p3 >> 16);

                Console.WriteLine($"[BossInfo] slot={i,2} {name} (id={eid}) hp={hp}/{maxHp} scale={scale:F1} reticle={rw:F2}x{rh:F2} pos=({x:F0},{y:F0}) mot={mot} | type={res1} fire={fire} ice={ice} thun={thun} wind={wind} holy={holy} | {SlotStbId(i)}");
            }
        }

        /// <summary>
        /// Identify which companion script a slot is running, by decoding its STB code-offset. The slot's CRunScript
        /// (0x21E4D5A0 + slot*0x48) holds the live instruction pointer at +0x30; that points INTO the loaded STB, so
        /// we scan back to the "STB\0" header and read codeOff @+0x54. codeOff is the companion's signature: korinoya
        /// 0x7D0, baria 0x874, kori/i_meteo 0x5EC, i_tatumaki/reiki 0x3AC, Ice Queen 0x2914. This is how we verify the
        /// eid-0 companions (kori/i_meteo/i_tatumaki) are in their correct native slots.
        /// </summary>
        private static string SlotStbId(int slot)
        {
            const uint StbMagic = 0x00425453u;   // "STB\0"
            uint ipRaw = Memory.ReadUInt(0x21E4D5A0 + slot * 0x48 + 0x30);
            if (ipRaw == 0) return "no-script";
            long ip = ipRaw < Memory.Pcsx2Base ? ipRaw + Memory.Pcsx2Base : ipRaw;
            const int win = 0x8000;                                   // STBs are well under 32 KB
            long start = System.Math.Max(Memory.Pcsx2Base, ip - win);
            int nWords = (int)((ip - start) / 4);
            if (nWords <= 1) return "?";
            uint[] w = Memory.ReadUIntBatch(start, nWords);
            long nearestStb = -1; uint nearestCo = 0;
            for (int i = w.Length - 1; i >= 0; i--)                   // nearest STB header below the IP, with a known codeOff
            {
                if (w[i] != StbMagic) continue;
                long stb = start + (long)i * 4;
                uint co = (uint)Memory.ReadInt(stb + 0x54);
                if (nearestStb < 0) { nearestStb = stb; nearestCo = co; }   // remember the nearest header for the raw fallback
                string nm = co switch
                {
                    0x2914 => "IceQueen", 0x7D0 => "korinoya", 0x874 => "baria",
                    0x5EC => "kori/i_meteo", 0x3AC => "i_tatumaki/reiki",
                    0x778 => "KingsCurseCoffin(c15a)", 0x2FDC => "phase(c15b)", _ => null
                };
                if (nm != null) return $"codeOff=0x{co:X}({nm}) stb=0x{stb:X8}";
            }
            // No recognized signature — report the nearest header's RAW codeOff + address so unmapped scripts (e.g. the
            // King's Curse phase entity whose runtime codeOff we're trying to confirm) are still identifiable.
            return nearestStb >= 0 ? $"codeOff=0x{nearestCo:X}(?) stb=0x{nearestStb:X8}" : "?";
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
            _modelDumpFired = false;
            _lastTeleportDebugTime = DateTime.MinValue;
            _teleportPhase2Pending = false;
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

        private static readonly Dictionary<int, int[]> _speciesTableSnapshots = [];

        /// <summary>
        /// Overwrites target's species table entry with source's model codes and all data
        /// fields so the engine loads source's mesh and stats for any target spawn.
        /// Snapshots the original record on first call so RestoreRedirectedEnemies() can
        /// undo every change when the player leaves the dungeon.
        /// </summary>
        internal static void RedirectEnemyModel(EnemyDefaults target, EnemyDefaults source)
        {
            int targetIdx  = target.TableIndex.Value;
            int recordAddr = EnemySpeciesTable.RecordAddress(targetIdx);

            // Guard: skip if redirect already applied (fires every frame during floor selection screen).
            if (Memory.ReadUShort(recordAddr + EnemySpeciesTable.EnemySpeciesId) == source.Id) return;

            // Snapshot original record before first redirect so RestoreRedirectedEnemies() can
            // write it back verbatim. Read as raw int words — covers all field types correctly.
            if (!_speciesTableSnapshots.ContainsKey(targetIdx))
            {
                const int wordCount = EnemySpeciesTable.Stride / 4;
                int[] snapshot = new int[wordCount];
                for (int w = 0; w < wordCount; w++)
                    snapshot[w] = Memory.ReadInt(recordAddr + w * 4);
                _speciesTableSnapshots[targetIdx] = snapshot;
            }

            int srcAddr = EnemySpeciesTable.RecordAddress(source.TableIndex.Value);

            // ModelCode (0x000): 4-char ASCII mesh identifier (e.g. "m_ut", "e52a").
            // Writing source's code here tells the engine to load source's visual mesh for target
            // spawns. Writing only this field avoids the boss-spawn-at-(0,0) origin bug because
            // the AI dispatch still reads ModelCodeCopy and loads the original behavior script.
            // However the AI will act like the original enemy (e.g. Dasher AI with MasterUtan mesh).
            byte[] modelCode = System.Text.Encoding.ASCII.GetBytes(source.ModelCode);
            Memory.WriteByteArray(recordAddr + EnemySpeciesTable.ModelCode, modelCode);

            // ModelCodeCopy (0x040): duplicate of ModelCode consumed by the boss AI dispatch system.
            // The engine reads this field to select which c16a/stb behavior-script block to load for
            // spawned instances. Writing source's code here activates source's full boss AI and attack
            // patterns, but also triggers boss spawn logic that places the initial position at the
            // boss arena origin (0,0). Call FixModelRedirectSpawnPositions() after floor entry to
            // correct displaced slots when this field is written.
            Memory.WriteByteArray(recordAddr + EnemySpeciesTable.ModelCodeCopy, modelCode);

            // ── Data fields (0x050–0x09B) ─────────────────────────────────────────────────────
            // Memory.WriteInt   (recordAddr + EnemySpeciesTable.MaxHp,          Memory.ReadInt   (srcAddr + EnemySpeciesTable.MaxHp));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.Category,       Memory.ReadUShort(srcAddr + EnemySpeciesTable.Category));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.FireRes,        Memory.ReadUShort(srcAddr + EnemySpeciesTable.FireRes));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.IceRes,         Memory.ReadUShort(srcAddr + EnemySpeciesTable.IceRes));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.ThunderRes,     Memory.ReadUShort(srcAddr + EnemySpeciesTable.ThunderRes));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.WindRes,        Memory.ReadUShort(srcAddr + EnemySpeciesTable.WindRes));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.HolyRes,        Memory.ReadUShort(srcAddr + EnemySpeciesTable.HolyRes));
            Memory.WriteFloat (recordAddr + EnemySpeciesTable.EntityScale,    Memory.ReadFloat (srcAddr + EnemySpeciesTable.EntityScale));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.DamageReduction, Memory.ReadUShort(srcAddr + EnemySpeciesTable.DamageReduction));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.WeaponDefense,   Memory.ReadUShort(srcAddr + EnemySpeciesTable.WeaponDefense));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.PrimaryBstIndex,   Memory.ReadUShort(srcAddr + EnemySpeciesTable.PrimaryBstIndex));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.SecondaryBstIndex, Memory.ReadUShort(srcAddr + EnemySpeciesTable.SecondaryBstIndex));
            Memory.WriteInt   (recordAddr + EnemySpeciesTable.Abs,            Memory.ReadInt   (srcAddr + EnemySpeciesTable.Abs));
            // Memory.WriteInt   (recordAddr + EnemySpeciesTable.MinGoldDrop,    Memory.ReadInt   (srcAddr + EnemySpeciesTable.MinGoldDrop));
            // Memory.WriteInt   (recordAddr + EnemySpeciesTable.DropChance,     Memory.ReadInt   (srcAddr + EnemySpeciesTable.DropChance));
            // Memory.WriteInt   (recordAddr + EnemySpeciesTable.SpawnCap,       Memory.ReadInt   (srcAddr + EnemySpeciesTable.SpawnCap));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.EnemySpeciesId, Memory.ReadUShort(srcAddr + EnemySpeciesTable.EnemySpeciesId));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.StealItemId,    Memory.ReadUShort(srcAddr + EnemySpeciesTable.StealItemId));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.StealFlag,      Memory.ReadUShort(srcAddr + EnemySpeciesTable.StealFlag));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.ItemResA,       Memory.ReadUShort(srcAddr + EnemySpeciesTable.ItemResA));
            Memory.WriteUShort(recordAddr + EnemySpeciesTable.ItemResB,       Memory.ReadUShort(srcAddr + EnemySpeciesTable.ItemResB));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.AttackPower,    Memory.ReadUShort(srcAddr + EnemySpeciesTable.AttackPower));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.ElemAtkFire,    Memory.ReadUShort(srcAddr + EnemySpeciesTable.ElemAtkFire));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.ElemAtkIce,     Memory.ReadUShort(srcAddr + EnemySpeciesTable.ElemAtkIce));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.ElemAtkThunder, Memory.ReadUShort(srcAddr + EnemySpeciesTable.ElemAtkThunder));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.ElemAtkWind,    Memory.ReadUShort(srcAddr + EnemySpeciesTable.ElemAtkWind));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.ElemAtkHoly,    Memory.ReadUShort(srcAddr + EnemySpeciesTable.ElemAtkHoly));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.ElemAtkDark,    Memory.ReadUShort(srcAddr + EnemySpeciesTable.ElemAtkDark));
            // Memory.WriteUShort(recordAddr + EnemySpeciesTable.Unk042,         Memory.ReadUShort(srcAddr + EnemySpeciesTable.Unk042));
            // Memory.WriteFloat (recordAddr + EnemySpeciesTable.Unk098,         Memory.ReadFloat (srcAddr + EnemySpeciesTable.Unk098));

            Console.WriteLine($"[ModelRedirect] {target.Name} species table → {source.Name}");
        }

        /// <summary>
        /// Restores all species table entries modified by RedirectEnemyModel to their original
        /// values. Call when the player leaves the dungeon.
        /// </summary>
        internal static void RestoreRedirectedEnemies()
        {
            if (_speciesTableSnapshots.Count == 0) return;
            foreach (var kvp in _speciesTableSnapshots)
            {
                int recordAddr = EnemySpeciesTable.RecordAddress(kvp.Key);
                for (int w = 0; w < kvp.Value.Length; w++)
                    Memory.WriteInt(recordAddr + w * 4, kvp.Value[w]);
                Console.WriteLine($"[ModelRedirect] Restored species table index {kvp.Key}");
            }
            _speciesTableSnapshots.Clear();
        }

        /// <summary>
        /// Corrects the world position of any boss-model slot that was displaced by the
        /// model-redirect spawn logic (which sets position to the boss arena origin instead
        /// of a floor spawn point). Targets any slot whose species ID appears in
        /// <see cref="Enemies.BossEnemies"/>, since <see cref="RedirectEnemyModel"/>
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
            float playerZ = Memory.ReadFloat(Player.dunPositionZ);
            float avgZ    = zCount > 0 ? sumZ / zCount : 0f;
            // At floor entry all peers may have Z=0 (not yet placed by engine), so fall back to
            // player Z — player is at stable floor height by the time this correction runs.
            float refZ = avgZ >= 1.0f ? avgZ : playerZ;

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

        // --- DEBUG: freeze-then-teleport all active enemies to player every 3 s ---
        // Phase 1 (t=0s): freeze all enemies (FreezeTimer=300) and log pre-freeze position.
        // Phase 2 (t=1s): write position while frozen; log before/after to see if write sticks.
        // If position changes and holds for 1s, LocationX/Y/Z is writable when movement AI is paused.
        // If position snaps back immediately, the engine overwrites even frozen enemies.
        private static DateTime _lastTeleportDebugTime = DateTime.MinValue;
        private static bool _teleportPhase2Pending = false;
        internal static void TeleportEnemiesDebug()
        {
            double elapsed = (DateTime.UtcNow - _lastTeleportDebugTime).TotalSeconds;

            if (!_teleportPhase2Pending && elapsed < 3.0) return;

            float px = Memory.ReadFloat(Player.dunPositionX);
            float py = Memory.ReadFloat(Player.dunPositionY);
            float pz = Memory.ReadFloat(Player.dunPositionZ);

            if (!_teleportPhase2Pending)
            {
                // Phase 1: freeze all active enemies and snapshot their positions.
                _lastTeleportDebugTime = DateTime.UtcNow;
                _teleportPhase2Pending = true;
                for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                {
                    int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                    if (Memory.ReadInt(slotBase) <= 0) continue;
                    float x = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationX);
                    float y = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationY);
                    float z = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationZ);
                    Memory.WriteInt(slotBase + EnemySlotOffsets.FreezeTimer, 300);
                    ushort id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    Console.WriteLine($"[TeleportDebug] FREEZE slot={i} id={id} pos=({x:F1},{y:F1},{z:F1}) frozen");
                }
            }
            else if (elapsed >= 1.0)
            {
                // Phase 2: write position while frozen; refresh freeze so it doesn't expire mid-test.
                _teleportPhase2Pending = false;
                for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
                {
                    int slotBase = EnemyAddresses.FloorSlots.SlotAddr(i, 0);
                    if (Memory.ReadInt(slotBase) <= 0) continue;
                    float xBefore = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationX);
                    float yBefore = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationY);
                    float zBefore = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationZ);
                    Memory.WriteInt(slotBase + EnemySlotOffsets.FreezeTimer, 300);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.LocationX, px);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.LocationY, py);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.LocationZ, pz);
                    float xAfter = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationX);
                    float yAfter = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationY);
                    ushort id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.EnemySpeciesId));
                    Console.WriteLine($"[TeleportDebug] WRITE slot={i} id={id} before=({xBefore:F1},{yBefore:F1}) after=({xAfter:F1},{yAfter:F1}) target=({px:F1},{py:F1})");
                }
            }
        }
        // --- DEBUG: one-shot model table scan for slot 0 render position ---
        // Fires once when slot 0's slot position is non-zero and stable.
        // Scans the first 0x400 bytes of the model table entry for slot 0 as floats,
        // printing any that are within 5.0 units of the known slot X or Y coordinate.
        // The matching offsets are candidates for the true render/transform position.
        private static bool _modelDumpFired = false;
        internal static void DumpModelTableForRenderPosition()
        {
            if (_modelDumpFired) return;
            int slotBase = EnemyAddresses.FloorSlots.SlotAddr(0, 0);
            if (Memory.ReadInt(slotBase) <= 0) return;
            float slotX = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationX);
            float slotY = Memory.ReadFloat(slotBase + EnemySlotOffsets.LocationY);
            if (Math.Abs(slotX) < 10.0f && Math.Abs(slotY) < 10.0f) return;

            _modelDumpFired = true;
            int modelBase = ModelScaleOffsets.ModelBase + 0 * ModelScaleOffsets.ModelStride;
            int stride    = ModelScaleOffsets.ModelStride; // 0x3510

            // Scan the full model table entry as floats.
            // Print any value in [50, 3000] — the world-coordinate range seen in this dungeon.
            // Also flag values within 5 units of the slot X or Y (written position).
            Console.WriteLine($"[ModelDump] slot=0 slotPos=({slotX:F1},{slotY:F1}) scanning 0x{modelBase:X8}+0x{stride:X4} for world-range floats");
            for (int off = 0; off < stride; off += 4)
            {
                float val = Memory.ReadFloat(modelBase + off);
                if (float.IsNaN(val) || float.IsInfinity(val)) continue;
                if (val < 50.0f || val > 3000.0f) continue;
                bool nearX = Math.Abs(val - slotX) < 5.0f;
                bool nearY = Math.Abs(val - slotY) < 5.0f;
                Console.WriteLine($"[ModelDump]   +0x{off:X4} = {val:F2}{(nearX ? " ~X" : "")}{(nearY ? " ~Y" : "")}");
            }
            Console.WriteLine("[ModelDump] scan complete");
        }
        // --- END DEBUG ---

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
                    uint pcsx2 = (v & 0x01FFFFFFu) + (uint)Memory.Pcsx2Base;
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
