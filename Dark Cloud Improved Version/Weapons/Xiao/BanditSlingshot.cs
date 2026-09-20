using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Bandit Slingshot — a steal that lands on an enemy with a projectile takes the projectile too: from then until she
    /// steals another or the floor ends, every pellet Xiao fires is that enemy's shot — its config as the species table
    /// holds it, flags and all (the Black Dragon's ball keeps its Freeze), the victim mask alone turned on enemies — at
    /// <see cref="DamageMult"/>× the shot's attack and with the shot's own element. The steal is the weapon's native
    /// Steal ability (a 10 % roll per hit in CheckDmg); the game spawns the stolen item as a CStealItem that flies to
    /// Xiao, and the item's "acquired" notice gets a second line: "[enemy]'s projectile is now yours".
    ///
    /// The proc is seen in the CStealItem pool (<see cref="StealItemPool"/>, eight slots: a slot's state leaving −1),
    /// the victim is the living enemy nearest the slot's spawn point, and its projectile is the primary shot config of
    /// its species (the static table's +0x68; none = no projectile, the item alone). The config is entered into the
    /// main-character effect instance by <see cref="BorrowedShots"/> (the provider <see cref="WantedShot"/>) and fired
    /// in the pellet's place the tick the pellet appears, as Dragon's Y does.
    ///
    /// The notice is the game's own: the item's arrival calls ItemGetMes → SetSystemMes(10 / 20 / 30 by item id range) →
    /// MakeMesWin from the notices bank (meswin/system_ae.bin in <see cref="NoticeBank"/>). The ISO bake relocates those
    /// three templates to <see cref="NoticeReserveWords"/>-word blocks at the bank's end (MesTextBaker.RelocateMes), so the
    /// app can rewrite the one the item will use — the template's words, a line break, the enemy line — while the item
    /// is in flight, and put the template back afterwards.
    /// </summary>
    internal static class BanditSlingshot
    {
        private const string Tag = "[BanditSlingshot] ";
        private const float DamageMult = 2f;
        private const float VictimRange = 40f;              // the steal item spawns at the enemy: the nearest living one within this
        private static readonly TimeSpan NoticeHold  = TimeSpan.FromSeconds(8);   // the template stays rewritten at most this long after the steal (the item flies ~1–2 s, the window follows)
        private static readonly TimeSpan NoticeShown = TimeSpan.FromSeconds(3);   // …and this long after the item arrives (the window shows for 2 s)

        // The CStealItem pool: eight flying items — +0x10 + i×0x10 position (x, h, y), +0xD0 + i×4 state (−1 free, 0 rising,
        // 1 flying, 2 arrived), +0x134 + i×4 the item id.
        private const long StealItemPool = 0x21EA8300;
        private const int  StealSlots = 8, StealPos = 0x10, StealState = 0xD0, StealItem = 0x134;

        /// <summary>The notices bank in EE RAM (main BSS `mes_data`, 48,000 B; the file is 34,406 B) and the file baked.</summary>
        internal const long   NoticeBank = 0x21CFCE00;
        internal const string NoticeFile = "meswin/system_ae.bin";
        /// <summary>The "acquired" templates ItemGetMes picks by item id (≤ 0x50 → 10, ≤ 0x90 → 20, ≤ 0x100 → 30, else 10) and
        /// the words each relocated block holds.</summary>
        internal static readonly int[] NoticeIds = { 10, 20, 30 };
        internal const int NoticeReserveWords = 80;

        private static readonly int[]  _stealState = new int[StealSlots];
        private static readonly bool[] _seen = new bool[PlayerShotPool.SlotCount];
        private static int    _floor = -1;
        private static int    _stolenCfg = -1;
        private static string _stolenName;
        private static BorrowedEffect _effect;
        private static long     _noticeAddr;
        private static ushort[] _noticeVanilla;
        private static DateTime _noticeRestoreAt = DateTime.MaxValue;
        private static bool _nativeWarned;

        /// <summary>Drive every tick while the Bandit Slingshot is equipped; <paramref name="active"/> false holds everything.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            if (!_nativeWarned && (uint)Memory.ReadInt(DunPatches.CatFollowHookAddrMmu) != DunPatches.CatFollowHookNew)
            { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the borrowed-shots cave is not in this ISO — stolen projectiles cannot be fired (re-patch the ISO)"); }
            int floor = Memory.ReadUShort(Addresses.checkFloor);
            if (floor != _floor) { Forget(); _floor = floor; for (int i = 0; i < StealSlots; i++) _stealState[i] = -1; }
            WatchSteals();
            if (_noticeRestoreAt <= GameClock.Now) RestoreNotice();
            if (_effect != null) FirePellets();
        }

        private static void WatchSteals()
        {
            for (int i = 0; i < StealSlots; i++)
            {
                int st = Memory.ReadInt(StealItemPool + StealState + i * 4);
                if (st != -1 && _stealState[i] == -1)
                {
                    long p = StealItemPool + StealPos + i * 0x10;
                    OnSteal(Memory.ReadFloat(p), Memory.ReadFloat(p + 8), Memory.ReadInt(StealItemPool + StealItem + i * 4));
                }
                else if (st == -1 && _stealState[i] != -1 && _noticeVanilla != null)
                    _noticeRestoreAt = GameClock.Now + NoticeShown;          // the item arrived: its window is up; the template goes back after it
                _stealState[i] = st;
            }
        }

        /// <summary>A steal landed: the nearest living enemy's projectile is hers, and the item's notice says so.</summary>
        private static void OnSteal(float x, float y, int item)
        {
            int victim = -1; float best = VictimRange * VictimRange;
            for (int i = 0; i < EnemyAddresses.FloorSlots.Count; i++)
            {
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.RenderStatus)) <= 0) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)) <= 0) continue;
                long pos = EnemyAddresses.MainMonstorUnit.Base + EnemyAddresses.CharObjects.ArrayOffset + i * EnemyAddresses.CharObjects.Stride + EnemyAddresses.CharObjects.PosOffset;
                float dx = Memory.ReadFloat(pos) - x, dy = Memory.ReadFloat(pos + 8) - y, d = dx * dx + dy * dy;
                if (d < best) { best = d; victim = i; }
            }
            if (victim < 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"a steal landed (item {item}) but no living enemy stands near its spawn point"); return; }
            ushort id = Memory.ReadUShort(EnemyAddresses.FloorSlots.SlotAddr(victim, EnemySlotOffsets.EnemySpeciesId));
            if (!EnemySpecies.Defaults.TryGetValue(id, out var species) || species.TableIndex == null)
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"stole item {item} from species {id}, which the mod does not know"); return; }
            long row = 0x20000000L + EnemySpeciesTable.TableBase + (long)species.TableIndex.Value * EnemySpeciesTable.Stride;
            int cfg = Projectile(Memory.ReadUShort(row + EnemySpeciesTable.PrimaryBstIndex));
            if (cfg < 0) cfg = Projectile(Memory.ReadUShort(row + EnemySpeciesTable.SecondaryBstIndex));   // a self-burst primary: its other shot, if it flies
            if (cfg < 0)
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"stole item {item} from {species.Name}: it has no projectile"); return; }
            var fx = BorrowedShots.TableConfig(cfg, keepFlags: true);
            if (fx == null) return;
            _stolenCfg = cfg; _stolenName = species.Name; _effect = fx;
            Array.Clear(_seen, 0, _seen.Length);
            WriteNotice(item, species.Name);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{species.Name}'s projectile is hers: {fx.Name} (config {cfg}, flags 0x{BitConverter.ToInt32(fx.Cfg, ShotEffectPack.CfgFlags):X}) until another steal or the floor ends");
        }

        /// <summary>The self-detonations (the zibaku family: the enemy bursts where it stands — nothing that flies): never
        /// taken; a species whose primary is one falls back to its secondary shot.</summary>
        private static readonly int[] SelfBurst = { 16, 17, 18 };   // zibaku_f2, zibaku_r2, zibaku_t2

        /// <summary>A shot config index the slingshot may take: −1 for none (0xFFFF), a self-burst, or an index past the table.</summary>
        private static int Projectile(int cfg) =>
            cfg == 0xFFFF || cfg >= ShotEffectPack.CfgCount || Array.IndexOf(SelfBurst, cfg) >= 0 ? -1 : cfg;

        /// <summary>Every pellet the tick it appears: the stolen shot from its position with its velocity, at
        /// <see cref="DamageMult"/>× the attack, and the pellet gives way.</summary>
        private static void FirePellets()
        {
            long pool = (uint)Memory.ReadInt(PlayerShotPool.BasePtr);
            if (!Memory.IsValidGuest(pool)) return;
            for (int i = 0; i < PlayerShotPool.SlotCount; i++)
            {
                bool live = Memory.ReadInt(PlayerShotPool.FlagAddr(pool, i)) != 0;
                if (live && !_seen[i])
                {
                    _seen[i] = true;
                    long pa = PlayerShotPool.PosAddr(pool, i), va = PlayerShotPool.VelAddr(pool, i);
                    int damage = (int)(Memory.ReadInt(PlayerShotPool.DamageAddr(pool, i)) * DamageMult);
                    if (BorrowedShots.Fire(_effect, Memory.ReadFloat(pa), Memory.ReadFloat(pa + 4), Memory.ReadFloat(pa + 8),
                                           Memory.ReadFloat(va), Memory.ReadFloat(va + 4), Memory.ReadFloat(va + 8),
                                           damage, Memory.ReadInt(PlayerShotPool.LifetimeAddr(pool, i))))
                        Memory.WriteInt(PlayerShotPool.FlagAddr(pool, i), 0);
                }
                else if (!live) _seen[i] = false;
            }
        }

        // ── the notice ──────────────────────────────────────────────────────────────────────────────────────────
        private static int NoticeIdFor(int item) => item > 0x100 ? 10 : item > 0x90 ? 30 : item > 0x50 ? 20 : 10;   // ItemGetMes's choice

        private static void WriteNotice(int item, string enemy)
        {
            RestoreNotice();
            long addr = NoticeAddress(NoticeIdFor(item));
            if (addr == 0) return;
            var vanilla = new List<ushort>();
            for (int i = 0; i < NoticeReserveWords; i++) { ushort w = Memory.ReadUShort(addr + i * 2); vanilla.Add(w); if (w == 0xFF01) break; }
            if (vanilla.Count == 0 || vanilla[vanilla.Count - 1] != 0xFF01)
            { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the notice template is not where the bake put it — no message line (re-patch the ISO)"); return; }
            ushort[] line;
            try { line = WeaponDescriptions.Encode(enemy + "'s projectile is now yours"); }
            catch (ArgumentException) { line = WeaponDescriptions.Encode("Its projectile is now yours"); }
            var words = new List<ushort>(vanilla); words.RemoveAt(words.Count - 1);
            words.Add(0xFF00); words.AddRange(line); words.Add(0xFF01);
            if (words.Count > NoticeReserveWords) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the notice line does not fit its block"); return; }
            var bytes = new byte[words.Count * 2];
            for (int i = 0; i < words.Count; i++) { bytes[i * 2] = (byte)words[i]; bytes[i * 2 + 1] = (byte)(words[i] >> 8); }
            Memory.WriteBytesBatch(addr, bytes);
            _noticeAddr = addr; _noticeVanilla = vanilla.ToArray(); _noticeRestoreAt = GameClock.Now + NoticeHold;
        }

        private static void RestoreNotice()
        {
            if (_noticeVanilla == null) return;
            var bytes = new byte[_noticeVanilla.Length * 2];
            for (int i = 0; i < _noticeVanilla.Length; i++) { bytes[i * 2] = (byte)_noticeVanilla[i]; bytes[i * 2 + 1] = (byte)(_noticeVanilla[i] >> 8); }
            Memory.WriteBytesBatch(_noticeAddr, bytes);
            _noticeVanilla = null; _noticeAddr = 0; _noticeRestoreAt = DateTime.MaxValue;
        }

        /// <summary>MMU address of notice <paramref name="id"/>'s text in the bank (0 when the bank is not readable).</summary>
        private static long NoticeAddress(int id)
        {
            int count = Memory.ReadUShort(NoticeBank);
            if (count <= 0 || count > 1024) return 0;
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(NoticeBank + 4 + i * 4) == id) return NoticeBank + 2 * (count + Memory.ReadUShort(NoticeBank + 6 + i * 4) + 1);
            return 0;
        }

        /// <summary>The stolen projectile, for BorrowedShots — while the Bandit Slingshot is out and something is stolen.</summary>
        internal static BorrowedEffect WantedShot()
        {
            if (_effect == null || Player.CurrentCharacterNum() != Player.XiaoId) return null;
            int slot = Memory.ReadByte(DngStatusData.Base + DngStatusData.EquipSlotArrayOffset + Player.XiaoId);
            if ((uint)slot > 9) return null;
            return Memory.ReadUShort(DngStatusData.WeaponRecord(Player.XiaoId, slot)) == Items.banditslingshot ? _effect : null;
        }

        private static void Forget()
        {
            if (_effect != null) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{_stolenName}'s projectile is hers no longer");
            _stolenCfg = -1; _stolenName = null; _effect = null;
            RestoreNotice();
        }

        /// <summary>The weapon or the floor went: the stolen projectile is dropped and the notice put back.</summary>
        internal static void Stop() { Forget(); _floor = -1; }
    }
}
