using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>A shot effect an ability borrows: a BT_SHOT_EFFECT config and the archive container it names.</summary>
    internal sealed class BorrowedEffect
    {
        internal readonly byte[] Cfg;      // the BT_SHOT_EFFECT (0x70 B), as the provider built it
        internal readonly string Path;     // the container in the archive, e.g. dun/effect/f_boll_3.chr
        internal readonly bool   KeepFlags; // the config's whole flags word stays (its ailments too); else the element bits alone
        internal readonly long   Instance;  // the CSHOT_EFFECT it is entered in: CharaMainEffect, or the second instance (Ruby's stolen shot)
        internal BorrowedEffect(byte[] cfg, string path, bool keepFlags = false, long instance = ShotEffectPack.CharaMainEffect)
        { Cfg = cfg; Path = path; KeepFlags = keepFlags; Instance = instance; }
        internal string Name => BorrowedShots.Name(Cfg);
        internal bool Same(BorrowedEffect o) => o != null && Path == o.Path && Instance == o.Instance && Cfg.SequenceEqual(o.Cfg);
    }

    /// <summary>
    /// A shot effect borrowed by one of Xiao's abilities, available on every floor. A shot effect is a BT_SHOT_EFFECT
    /// config (<see cref="ShotEffectPack.CfgTable"/> holds the species' 34; the characters' wep_eff ones sit in dun.bin)
    /// naming a `.chr` container the game builds a CSHOT_EFFECT from. Xiao's live in the MAIN-CHARACTER effect instance
    /// (<see cref="ShotEffectPack.CharaMainEffect"/>) — the sixth CSHOT_EFFECT beside the monster pack, which the floor
    /// loader fills with the active character's own wep_eff effect and the dungeon loop steps and draws; Xiao's holds an
    /// unused mgan01 — so the pack's five slots stay the monsters'. Each ability hands <see cref="Start"/> a provider
    /// returning the <see cref="BorrowedEffect"/> it wants right now (null = none): one of the game's 34 configs
    /// (<see cref="TableConfig"/>) or one of the mod's own naming any container (<see cref="CustomConfig"/>); the first
    /// answer wins. <see cref="Seed"/> writes a copy of that config — victim mask switched from the player to ENEMIES, flags
    /// cut to the element bits — and the file's path into <see cref="CodeCaves.BorrowedShotBlock"/> with state 0;
    /// ElfCave.BorrowedShotsEnter, at the head of the per-frame step chain, re-enters the instance whenever the loader
    /// refilled it (a new floor) or the state asks (another config): it carves a region from the monster pool once per
    /// floor (<see cref="ReserveUnits"/>), empties the instance, clears its texture block, reads the file and enters the
    /// config with that region as allocator — a re-entry reuses the region whole, so switching configs mid-floor costs
    /// the pool nothing more. No room, or a failed entry, drops the block's magic until the mod seeds again (a new floor).
    /// <see cref="Fire"/> is the Set replica the Guardian Reflector re-fires with, for a XIAO-owned shot: the engine steps
    /// it, collides it with enemies (the mask) and plants its damage entry with her owner id, ability flags and
    /// anti-category bytes, so CheckDmg treats it exactly as one of her pellets — with the shot's own element. The step
    /// plants that entry every frame a phase has a radius, then keeps quiet for the sub-shot's reload frames; so the copy's
    /// FLYING radius is zeroed (the contact sweep still runs) and <see cref="Fire"/> sets the reload past any impact
    /// animation: the impact's first frame is the one plant — the whole explosion radius, every enemy inside it once — and
    /// a miss's expiry burst likewise.
    /// </summary>
    internal static class BorrowedShots
    {
        private const string Tag = "[BorrowedShots] ";
        private const byte   PlantReload = 120;   // frames of silence after a plant (a byte): longer than any impact animation
        internal const string EffectDir = "dun/effect/", WepEffDir = "dun/mainchara/wep_eff/";   // where the species' and the characters' containers live
        /// <summary>The region the cave carves from the monster pool per floor, in 16-byte units, for an effect not in
        /// <see cref="KnownUnits"/>: the largest effect measured so far (an old fireball, 30,468) with headroom. An effect that
        /// needs more than its reserve hangs the game in the allocator, so the log line after each entry reports the units
        /// used — add a new effect's figure to the table once seen.</summary>
        internal const int ReserveUnits = 32768;
        /// <summary>Units each container's entry took (deterministic per file; the log line after each entry), so the region
        /// reserved for it is only slightly larger than what it needs — a floor whose pool is nearly full can still fit it.</summary>
        private static readonly Dictionary<string, int> KnownUnits = new Dictionary<string, int>
        {
            ["t_boll"] = 19867, ["i_boll"] = 20540, ["f_boll_3"] = 21167, ["e115a_ex"] = 21235, ["b_boll"] = 21695, ["e114a_ex"] = 23822,
        };
        private const int ReserveMargin = 1024;
        /// <summary>The region an effect is given, in units. Every KNOWN effect asks for the same amount — the largest known need
        /// plus a margin — so switching between them reuses one region (the cave carves a fresh, larger region only when a
        /// config needs more than the region holds, leaving the old one behind); an unknown effect asks for
        /// <see cref="ReserveUnits"/>.</summary>
        internal static int ReserveFor(BorrowedEffect fx) => KnownUnits.ContainsKey(fx.Name) ? KnownUnits.Values.Max() + ReserveMargin : ReserveUnits;
        /// <summary>The monster-pool units the seeded effect will ask for on each floor (0 when none is seeded).</summary>
        internal static int Headroom => _seeded == null ? 0 : ReserveFor(_seeded);
        /// <summary>The most any known effect asks for — what the roster builders keep clear of every floor, since a floor is
        /// staged before the weapon carried onto it is known.</summary>
        internal static int MaxReserve => KnownUnits.Values.Max() + ReserveMargin;
        private static Func<BorrowedEffect>[] _wanted = Array.Empty<Func<BorrowedEffect>>();   // each ability's answer: the effect it wants, or null
        private static BorrowedEffect _seeded;    // the effect in the block, as the provider gave it (null = none)
        private static readonly Dictionary<string, BorrowedEffect> _effects = new Dictionary<string, BorrowedEffect>();   // TableConfig/CustomConfig results, by key
        private static int _lastState = -3;        // the state word last logged
        private static int _lastFloor = -1;        // checkFloor last seen in a floor: a change → magic restored (retry after a quiet floor)
        private static bool _wasInFloor;
        private static System.Threading.Thread _thread;

        /// <summary>Keep the block matched to what the abilities want, from app start — ready BEFORE a floor loads (the loader
        /// runs before any ability thread does). Each <paramref name="wanted"/> answers with an effect or null, reading
        /// whatever it keys on (an equipped weapon's inventory record is valid in town and dungeon alike); the first answer
        /// is seeded, none → cleared.</summary>
        internal static void Start(params Func<BorrowedEffect>[] wanted)
        {
            _wanted = wanted;
            if (_thread != null && _thread.IsAlive) return;
            _thread = new System.Threading.Thread(Loop) { IsBackground = true, Name = "BorrowedShots" };
            _thread.Start();
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    BorrowedEffect fx = null;
                    foreach (var want in _wanted) { fx = want(); if (fx != null) break; }
                    if (fx != null) Seed(fx); else Clear();
                    bool inFloor = Player.InDungeonFloor();
                    int floor = inFloor ? Memory.ReadUShort(Addresses.checkFloor) : -1;
                    if (_seeded != null && inFloor && (!_wasInFloor || floor != _lastFloor))
                    {   // a new floor: the cave sees the loader's refill by itself; this restores the magic a quiet floor dropped
                        Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState, 0);
                        Memory.WriteInt(CodeCaves.BorrowedShotBlock, (int)CodeCaves.BorrowedShotMagic);
                        _lastState = -3;
                    }
                    _wasInFloor = inFloor; _lastFloor = floor;
                    WatchDonorPools();
                    int state = Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState);
                    if (_seeded != null && inFloor && state != _lastState)
                    {
                        _lastState = state;
                        long alloc = CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotAlloc;
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + _seeded.Name + (state == 1
                            ? $" entered in the main-character effect: region {Memory.ReadInt(alloc + 8):N0} of {Memory.ReadInt(alloc + 12):N0} units used (base 0x{Memory.ReadUInt(alloc):X}); monster pool {Memory.ReadInt(DataPools.Monstor + DataPools.Used):N0} of {Memory.ReadInt(DataPools.Monstor + DataPools.Cap):N0}"
                            : state < 0 ? " NOT entered (no room in the monster pool, or the entry failed) — quiet until the next floor" : " (to be entered)"));
                        if (state == 1) DescribeEntry();                               // DIAGNOSTIC: what the loader was actually given
                        if (state < 0)
                        {   // what the cave saw: the pool it tried to carve from, the region it holds, the instance's config pointer
                            long pool = DataPools.Monstor;
                            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag
                                + $"  monster pool used {Memory.ReadInt(pool + DataPools.Used):N0} of cap {Memory.ReadInt(pool + DataPools.Cap):N0} units, reserve {Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotReserve):N0}; "
                                + $"region base 0x{Memory.ReadUInt(alloc):X} used {Memory.ReadInt(alloc + 8):N0} cap {Memory.ReadInt(alloc + 12):N0}; "
                                + $"instance cfg 0x{Memory.ReadUInt(_seeded.Instance):X} (ours 0x{CodeCaves.BorrowedShotBlockGuest + CodeCaves.BorrowedShotCfg:X}), live effect ptr 0x{Memory.ReadUInt(ShotEffectPack.MainEffectLivePtr):X}");
                        }
                    }
                }
                catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "tick failed: " + e.Message); }
                System.Threading.Thread.Sleep(250);
            }
        }

        // ── pool-growth survey: the peak of the two carves that might donate units to the map/monster pool ─────────────
        // The floor script's work allocator ("cash") is reset around its run, so a sample after the load reads 0; only a
        // poll that catches it mid-run shows the demand. Logged whenever a new peak appears.
        private static readonly Dictionary<uint, int> _cashPeak = new Dictionary<uint, int>();   // the scratch allocator is re-aimed (work buffer, monster pool, read buffer): a peak per base
        private static int _texturePeak;
        private static void WatchDonorPools()
        {
            int cash = Memory.ReadInt(DataPools.Cash + DataPools.Used), tex = Memory.ReadInt(DataPools.Texture + DataPools.Used);
            uint cashBase = Memory.ReadUInt(DataPools.Cash);
            if (cash < 4_000_000 && cash > (_cashPeak.TryGetValue(cashBase, out int peak) ? peak : 0))
            {
                _cashPeak[cashBase] = cash;
                string where = cashBase == Memory.ReadUInt(DataPools.P840) ? "the script work buffer" : cashBase == Memory.ReadUInt(DataPools.Monstor) ? "the monster pool" : "elsewhere";
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"[survey] scratch allocator peak {cash:N0} of {Memory.ReadInt(DataPools.Cash + DataPools.Cap):N0} units at base 0x{cashBase:X} ({where})");
            }
            if (tex > _texturePeak && tex < 4_000_000)
            {
                _texturePeak = tex;
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"[survey] texture-data pool peak {tex:N0} of {Memory.ReadInt(DataPools.Texture + DataPools.Cap):N0} units");
            }
        }

        private static void Clear()
        {
            if (_seeded == null) return;
            Memory.WriteInt(CodeCaves.BorrowedShotBlock, 0);         // no magic: the cave enters nothing (an entered effect stays until the next floor, unused)
            _seeded = null; _lastState = -3;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "block cleared (no ability wants a shot)");
        }

        /// <summary>Put the effect in the block unless it is there already, state 0: the cave enters it on its next frame in
        /// a floor (re-entering the instance if another config was in it, reusing the floor's region).</summary>
        private static void Seed(BorrowedEffect fx)
        {
            if (fx.Same(_seeded)) return;
            byte[] c = (byte[])fx.Cfg.Clone();
            BitConverter.GetBytes(2).CopyTo(c, ShotEffectPack.CfgVictimMask);                            // hurts enemies, not the player
            BitConverter.GetBytes(0f).CopyTo(c, ShotEffectPack.CfgRadiusFlying);                         // no planting in flight: the impact's first frame is the first plant
            int flags = BitConverter.ToInt32(c, ShotEffectPack.CfgFlags);
            if (!fx.KeepFlags) BitConverter.GetBytes(flags & ShotEffectPack.CfgElementBits).CopyTo(c, ShotEffectPack.CfgFlags);   // the element only — no ailment (the Black Dragon's shot carries Freeze)
            byte[] path = new byte[CodeCaves.BorrowedShotPathLen];
            Encoding.ASCII.GetBytes(fx.Path, 0, Math.Min(fx.Path.Length, path.Length - 1), path, 0);
            Memory.WriteInt(CodeCaves.BorrowedShotBlock, 0);                                               // quiet while the block changes
            Memory.WriteBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCfg, c);
            Memory.WriteBytesBatch(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotPath, path);
            Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotReserve, ReserveFor(fx));
            Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotInstance, (int)(fx.Instance - 0x20000000L));
            Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotMainFlag, fx.Instance == ShotEffectPack.CharaMainEffect ? 1 : 0);
            // The carve mark and the region are left as they are: the cave proves a region by its signature and the pool's counter,
            // so a floor's region outlives a Clear (a weapon switch) and the next effect re-enters it instead of carving another.
            Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState, 0);
            Memory.WriteInt(CodeCaves.BorrowedShotBlock, (int)CodeCaves.BorrowedShotMagic);
            _seeded = fx; _lastState = -3;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{fx.Name} ({fx.Path}) seeded, reserve {ReserveFor(fx):N0} units — entered on the next frame in a floor");
        }

        /// <summary>One of the game's 34 configs (<see cref="ShotEffectPack.CfgTable"/>) and its container under dun/effect, as
        /// the species loader uses it.</summary>
        internal static BorrowedEffect TableConfig(int index, bool keepFlags = false, long instance = ShotEffectPack.CharaMainEffect)
        {
            string key = "#" + index + (keepFlags ? "+" : "") + "@" + instance;
            if (_effects.TryGetValue(key, out var fx)) return fx;
            uint cfgAddr = Memory.ReadUInt(ShotEffectPack.CfgTable + index * 4);
            byte[] c = cfgAddr == 0 ? null : Memory.ReadBytesBatch(0x20000000L + cfgAddr, ShotEffectPack.CfgSize);
            if (c == null) return null;
            fx = new BorrowedEffect(c, EffectDir + Name(c) + ".chr", keepFlags, instance);
            _effects[key] = fx;
            return fx;
        }

        /// <summary>A character's wep_eff config as dun.bin holds it (Get_Main_EffectPtr's table: Toan's whirlwind is at
        /// <see cref="ShotEffectPack.WhirlwindCfg"/>), read once the overlay is resident — null until then (the overlay is not
        /// loaded in town, so the first floor after app start enters it a frame after the load instead of before).</summary>
        internal static BorrowedEffect DunConfig(long addr, string expectName)
        {
            string key = "@" + addr.ToString("X");
            if (_effects.TryGetValue(key, out var fx)) return fx;
            if (!Player.InDungeonFloor()) return null;
            byte[] c = Memory.ReadBytesBatch(addr, ShotEffectPack.CfgSize);
            if (c == null || Name(c) != expectName) return null;
            fx = new BorrowedEffect(c, WepEffDir + expectName + ".chr");
            _effects[key] = fx;
            return fx;
        }

        /// <summary>Set the radius the effect's phase <paramref name="phase"/> (0 muzzle, 1 flying, 2 impact, 3 expiry) plants
        /// damage with, on the copy the instance runs from — the step reads it live, so it can differ per shot.</summary>
        internal static void SetPhaseRadius(BorrowedEffect fx, int phase, float radius)
        {
            if (!Entered(fx)) return;
            Memory.WriteFloat(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCfg + ShotEffectPack.CfgRadiusMuzzle + phase * 4, radius);
        }

        /// <summary>Set the element bits the effect's damage carries (CfgFlags, element bits only) on the copy the instance
        /// runs from — the step reads it live, so a burst can hurt with the weapon's element rather than the effect's own.</summary>
        internal static void SetElement(BorrowedEffect fx, int elementBits)
        {
            if (!Entered(fx)) return;
            Memory.WriteInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotCfg + ShotEffectPack.CfgFlags, elementBits & ShotEffectPack.CfgElementBits);
        }

        /// <summary>Play the effect where it stands: a sub-shot in its MUZZLE phase at <paramref name="x"/>/<paramref name="h"/>/
        /// <paramref name="y"/> with no velocity, its muzzle motion once at <paramref name="scale"/>× size — Toan's whirlwind is
        /// exactly such an effect (muzzle radius 20, motion 0, nothing after). Its damage entry is Xiao's, planted once
        /// (the reload) with the muzzle radius set by <see cref="SetPhaseRadius"/>. False when not entered or all busy.</summary>
        internal static bool Burst(BorrowedEffect fx, float x, float h, float y, int damage, float scale)
        {
            if (!Entered(fx)) return false;
            byte[] cfg = fx.Cfg;
            long inst = ShotEffectPack.CharaMainEffect;
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return false;
            int j = -1;
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) == 0) { j = i; break; }
            if (j < 0) return false;
            long obj = inst + ShotEffectPack.OffObj + j * ShotEffectPack.ObjStride, dirA = inst + ShotEffectPack.OffDir + j * 0x10;
            int mot = BitConverter.ToInt16(cfg, ShotEffectPack.CfgMuzzleMotion);
            if (mot < 0) mot = 0;
            long ftab = Memory.ReadInt(obj + ShotEffectPack.ObjFrameTb);
            float startFrame = ftab > 0 ? Memory.ReadInt(ftab + 0x20000000 + mot * 0x10) : 1;
            long rec = WeaponHave.BattleWeaponRecord;
            Memory.WriteUShort(inst + ShotEffectPack.OffPhase + j * 2, 0);              // the muzzle phase: plays out, then the sub-shot ends
            Memory.WriteVec3  (obj + ShotEffectPack.ObjPos, x, h, y);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 12, 1f);
            Memory.WriteVec3  (obj + CCharacter.CharScale, scale, scale, scale);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotId, mot);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotFlag, 6);
            Memory.WriteFloat (obj + ShotEffectPack.ObjMotSpd, -1f);
            Memory.WriteFloat (obj + ShotEffectPack.ObjFrame, startFrame);
            Memory.WriteVec3  (dirA, 0f, 0f, 0f);
            Memory.WriteInt   (inst + ShotEffectPack.OffWait + j * 4, -1);              // no flight to time out
            Memory.WriteInt   (inst + ShotEffectPack.OffDamage + j * 4, damage);
            Memory.WriteInt   (inst + ShotEffectPack.OffUserCol + j * 4, 0);
            Memory.WriteUShort(inst + ShotEffectPack.OffOwner + j * 2, (ushort)Player.XiaoId);
            Memory.WriteUShort(inst + ShotEffectPack.OffAttr2 + j * 2, 0);
            Memory.WriteUShort(inst + ShotEffectPack.OffA060 + j * 2, 0xFFFF);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0B0 + j * 4, -1);
            Memory.WriteFloat (inst + ShotEffectPack.OffA0D0 + j * 4, -1f);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0F0 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffA110 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffWepFlags + j * 4, Memory.ReadUShort(rec + WeaponHave.AbilityFlagsOffset));
            Memory.WriteUInt  (inst + ShotEffectPack.OffAntiPtr + j * 4, (uint)(rec - 0x20000000 + WeaponHave.WeaponAntiOffset));
            Memory.WriteByte  (inst + ShotEffectPack.OffSndFlag + j, 0);
            Memory.WriteByte  (inst + ShotEffectPack.OffReload + j, PlantReload);
            Memory.WriteByte  (inst + ShotEffectPack.OffLatch + j, damage > 0 ? (byte)0 : PlantReload);   // no damage → never plants
            Memory.WriteInt   (inst + ShotEffectPack.OffLastIdx, j);
            Memory.WriteUShort(inst + ShotEffectPack.OffActive + j * 2, 1);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{fx.Name} burst from the main-character effect #{j}: damage {damage}, scale {scale:F2} at ({x:F0},{h:F0},{y:F0})");
            return true;
        }

        /// <summary>A config of the mod's own: the game's config <paramref name="templateIndex"/> (radii, wait, damage,
        /// element, reaction) naming the container <paramref name="dir"/><paramref name="name"/>.chr instead, with its phases'
        /// motions (the .chr's KEY ordinals; −1 = none) — any effect container (a species' under <see cref="EffectDir"/>, a
        /// character's under <see cref="WepEffDir"/>), whether or not the game itself uses it. The container's cfg record must
        /// be named <paramref name="name"/>.cfg: the loader asks for it by that name.</summary>
        internal static BorrowedEffect CustomConfig(int templateIndex, string name, short muzzleMotion, short flyMotion, short impactMotion, short expireMotion, string dir = EffectDir)
        {
            string key = $"{dir}{name}/{templateIndex}/{muzzleMotion},{flyMotion},{impactMotion},{expireMotion}";
            if (_effects.TryGetValue(key, out var fx)) return fx;
            BorrowedEffect t = TableConfig(templateIndex);
            if (t == null) return null;
            byte[] c = (byte[])t.Cfg.Clone();
            Array.Clear(c, ShotEffectPack.CfgName, ShotEffectPack.CfgNameLen);
            Encoding.ASCII.GetBytes(name, 0, Math.Min(name.Length, ShotEffectPack.CfgNameLen - 1), c, ShotEffectPack.CfgName);
            BitConverter.GetBytes(muzzleMotion).CopyTo(c, ShotEffectPack.CfgMuzzleMotion);
            BitConverter.GetBytes(flyMotion).CopyTo(c, ShotEffectPack.CfgFlyMotion);
            BitConverter.GetBytes(impactMotion).CopyTo(c, ShotEffectPack.CfgImpactMotion);
            BitConverter.GetBytes(expireMotion).CopyTo(c, ShotEffectPack.CfgExpireMotion);
            fx = new BorrowedEffect(c, dir + name + ".chr");
            _effects[key] = fx;
            return fx;
        }

        /// <summary>DIAGNOSTIC: the container the read buffer holds after an entry — its records by name (the pack
        /// format: name at +0, data offset +0x40, size +0x44, next record +0x48 — GetPackFile walks it by name) — and
        /// the instance's model root. An entry that carved nothing is either a file that never arrived (the buffer
        /// still holds the previous container) or a cfg the parser gave up on.</summary>
        private static void DescribeEntry()
        {
            try
            {
                uint buf = Memory.ReadGuestPtr(ShotEffectPack.ReadBufferPtr);
                var names = new System.Collections.Generic.List<string>();
                if (Memory.IsValidGuest(buf))
                {
                    long r = Memory.ToMmu(buf);
                    for (int i = 0; i < 8; i++)
                    {
                        byte[] nm = Memory.ReadBytesBatch(r, 32); if (nm == null || nm[0] == 0) break;
                        int n = Array.IndexOf(nm, (byte)0); if (n < 0) n = 32;
                        names.Add($"{Encoding.ASCII.GetString(nm, 0, n)}[{Memory.ReadInt(r + 0x44):N0}]");
                        int next = Memory.ReadInt(r + 0x48); if (next <= 0 || next > 0x100000) break;
                        r += next;
                    }
                }
                uint root = Memory.ReadGuestPtr(_seeded.Instance + 0xCC);
                string rootName = Memory.IsValidGuest(root) ? Encoding.ASCII.GetString(Memory.ReadBytesBatch(Memory.ToMmu(root) + CFrameVu1.Name, 8)).TrimEnd('\0') : "none";
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag +
                    $"read buffer 0x{buf:X} holds: {(names.Count == 0 ? "nothing readable" : string.Join(" ", names))}; instance model root 0x{root:X} `{rootName}`");
            }
            catch (Exception e) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "describe failed: " + e.Message); }
        }

        /// <summary>The effect file a config names.</summary>
        internal static string Name(byte[] cfg)
        {
            int n = Array.IndexOf(cfg, (byte)0, ShotEffectPack.CfgName, ShotEffectPack.CfgNameLen);
            return Encoding.ASCII.GetString(cfg, ShotEffectPack.CfgName, (n < 0 ? ShotEffectPack.CfgNameLen : n) - ShotEffectPack.CfgName);
        }

        /// <summary>Whether the effect is entered in the main-character instance right now (false also when another effect is
        /// seeded, or this floor had no room).</summary>
        internal static bool Entered(BorrowedEffect fx)
            => fx != null && fx.Same(_seeded) && Memory.ReadInt(CodeCaves.BorrowedShotBlock + CodeCaves.BorrowedShotState) == 1;

        /// <summary>Fire the effect's shot from <paramref name="x"/>/<paramref name="h"/>/<paramref name="y"/> with velocity
        /// <paramref name="vx"/>/<paramref name="vh"/>/<paramref name="vy"/> (units per frame), <paramref name="damage"/> base
        /// damage and <paramref name="life"/> frames of flight, as Xiao's. False when the effect is not entered on this floor
        /// or its sub-shots are all busy.</summary>
        internal static bool Fire(BorrowedEffect fx, float x, float h, float y, float vx, float vh, float vy, int damage, int life, int owner = Player.XiaoId)
        {
            if (!Entered(fx)) return false;
            byte[] cfg = fx.Cfg;
            long inst = fx.Instance;
            int count = Memory.ReadInt(inst + ShotEffectPack.OffCount);
            if (count < 1 || count > ShotEffectPack.SubShots) return false;
            int j = -1;
            for (int i = 0; i < count; i++)
                if (Memory.ReadUShort(inst + ShotEffectPack.OffActive + i * 2) == 0) { j = i; break; }
            if (j < 0) return false;
            long obj = inst + ShotEffectPack.OffObj + j * ShotEffectPack.ObjStride, dirA = inst + ShotEffectPack.OffDir + j * 0x10;
            int flyMot = BitConverter.ToInt16(cfg, ShotEffectPack.CfgFlyMotion);
            if (flyMot < 0) flyMot = 0;
            long ftab = Memory.ReadInt(obj + ShotEffectPack.ObjFrameTb);
            float startFrame = ftab > 0 ? Memory.ReadInt(ftab + 0x20000000 + flyMot * 0x10) : 1;
            long rec = WeaponHave.BattleWeaponRecord;
            // Set's own field order; active LAST. The owner (1 Xiao, 3 Ruby), collider 0 as a pellet's, the live weapon's flags and anti bytes.
            Memory.WriteUShort(inst + ShotEffectPack.OffPhase + j * 2, 1);              // flying, no muzzle
            Memory.WriteVec3  (obj + ShotEffectPack.ObjPos, x, h, y);
            Memory.WriteFloat (obj + ShotEffectPack.ObjPos + 12, 1f);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotId, flyMot);
            Memory.WriteInt   (obj + ShotEffectPack.ObjMotFlag, 4);
            Memory.WriteFloat (obj + ShotEffectPack.ObjMotSpd, -1f);
            Memory.WriteFloat (obj + ShotEffectPack.ObjFrame, startFrame);
            Memory.WriteVec3  (dirA, vx, vh, vy);
            Memory.WriteInt   (inst + ShotEffectPack.OffWait + j * 4, life);
            Memory.WriteInt   (inst + ShotEffectPack.OffDamage + j * 4, damage);
            Memory.WriteInt   (inst + ShotEffectPack.OffUserCol + j * 4, 0);
            Memory.WriteUShort(inst + ShotEffectPack.OffOwner + j * 2, (ushort)owner);
            Memory.WriteUShort(inst + ShotEffectPack.OffAttr2 + j * 2, 0);
            Memory.WriteUShort(inst + ShotEffectPack.OffA060 + j * 2, 0xFFFF);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0B0 + j * 4, -1);
            Memory.WriteFloat (inst + ShotEffectPack.OffA0D0 + j * 4, -1f);
            Memory.WriteInt   (inst + ShotEffectPack.OffA0F0 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffA110 + j * 4, -1);
            Memory.WriteInt   (inst + ShotEffectPack.OffWepFlags + j * 4, Memory.ReadUShort(rec + WeaponHave.AbilityFlagsOffset));
            Memory.WriteUInt  (inst + ShotEffectPack.OffAntiPtr + j * 4, (uint)(rec - 0x20000000 + WeaponHave.WeaponAntiOffset));
            Memory.WriteByte  (inst + ShotEffectPack.OffSndFlag + j, 0);
            Memory.WriteByte  (inst + ShotEffectPack.OffReload + j, PlantReload);
            Memory.WriteByte  (inst + ShotEffectPack.OffLatch + j, 0);                 // plants its damage
            Memory.WriteInt   (inst + ShotEffectPack.OffLastIdx, j);
            ShotEffects.FaceAlong(obj, vx, vh, vy);
            Memory.WriteUShort(inst + ShotEffectPack.OffActive + j * 2, 1);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{fx.Name} from {(inst == ShotEffectPack.CharaMainEffect ? "the main-character effect" : "the second instance")} #{j}: damage {damage}, {life} frames, v=({vx:F2},{vh:F2},{vy:F2})");
            return true;
        }
    }
}
