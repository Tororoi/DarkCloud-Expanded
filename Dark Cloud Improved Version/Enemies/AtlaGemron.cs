using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>The Atla Gemron (EnemySpecies.AtlaGemron) in play. The species is the disc's: its record (SpeciesRows), model, script,
    /// name and shot (AtlaGemronBake, ElfSpeciesPatches.PatchAtlaShot). In play:
    ///  • the forehead Atlamillia draws tinted (eye_tint.s's Atlamillia entries, through a private vtable: <see cref="Lens"/>);
    ///  • as one dies, a real atla is spawned where it will come to rest (CDungeonMap::SetAtraBoll through NativeCall, under a sentinel
    ///    parts entry and a name channel: AtlaNameChannels; on a floor whose eight atla places are taken, the nearest atla still on the
    ///    floor is taken over instead — its own content, no drop — or, when all eight are collected, a collected one's place is reused by
    ///    data writes) and a bounce entry filled (CodeCaves.AtlaBounce): tools/stubs/atla_draw.s keeps it
    ///    out of the draw until the Gemron's death reaches the swap (AtlaGemronBake.Swap, where the held atla leaves the Gemron's draw),
    ///    then bounces it from the held atla's pose to a real atla's over the Gemron's own death frames. The held atla's pose on the swap is
    ///    foreseen from the unit's place, turn and scale and the bake's chain on that frame (the recipe), and refreshed every tick until
    ///    then. Its pickup radius is 0 until it has settled (the cave sets it back);
    ///  • collected, it hands over what it rolled: 5 % an Atlamillia Sword (CDngStatusData::GetItem), 25 % an Atlamillia SynthSphere, else
    ///    the SynthSphere of a random weapon (any but the Atlamillia), every base value (attack, endurance, speed, magic, elements and
    ///    antis) a sixth, rounded up, with all its abilities (tools/stubs/atla_collect.s frees its parts entry, which it has no floor slot
    ///    for). A floor without an atla model hands it over at once. An atla left on the floor is lost with it.</summary>
    internal static class AtlaGemron
    {
        private const string Tag = "[AtlaGemron] ";
        private static readonly NodeDrawHook Lens = new("the Atla Gemron's Atlamillia", "eye_tint", DeadChainCave.AtlamilliaTint,
            CodeCaves.AtlamilliaTintStock, CodeCaves.AtlamilliaTintVtable, CodeCaves.AtlamilliaTintVtableGuest, slot7: 0x10);
        private const string LensNode = "renzu__m";                                  // AtlaGemronBake: Toan's lens record, its name kept

        private const uint SetAtraBoll = 0x001C8080, GetItem = 0x001BE060;
        private const int DeathMotion = 11, DeathStart = 200;                       // AtlaGemronBake: the death's key and its copy's first frame
        private const int BlockMotion = 0xBD8, BlockFrame = 0x260;                  // a unit's model block: its playing motion, its frame
        private const int MapAtra = 0xBC80, AtraStride = 0x20, AtraNo = 0x10, AtraUsed = 0x14, MapAtraNum = 0xBD80, MapAtraModel = 0xBD84, AtraMax = 8;
        private const int AtraPhase = 0x18;
        private const int MapEvents = 0x8D58, EventStride = 0x50, EventCount = 48, EventKind = 0x00, EventPlaced = 0x04, EventPos = 0x08, EventRadius = 0x1C, EventIndex = 0x20;
        private const int EventAtra = 3;
        private const float PickupRadius = 13f;                                     // SetAtraBoll's
        private const int GalleryOfTime = 5, DemonShaft = 6;

        private enum Drop { Sword, AtlaSphere, Sphere }

        /// <summary>A dying Atla Gemron's atla and drop. Kept (Entry -1 once handed over, or for an atla taken over: no drop of its own) until
        /// the unit leaves its death and the bounce is over, so one death gives one atla.</summary>
        private sealed class Pending
        {
            public int Unit;                    // the dying unit, -1 once it has left its death
            public long Map;                    // the CDungeonMap (PCSX2 address)
            public int Atra = -1, Event = -1, Bounce = -1;
            public int Dungeon, Entry, Sentinel, Channel;
            public Drop Kind;
            public int WeaponId;
            public string Name;
        }

        private static readonly List<Pending> _pending = new List<Pending>();
        private static readonly Random _rng = new Random();
        private static JsonElement _rc;
        private static bool _rcLoaded;

        /// <summary>Once a dungeon tick; <paramref name="active"/> false while a load is on (the floor's units and pools are rebuilt).</summary>
        internal static void Tick(bool active)
        {
            if (!active) return;
            AtlaNameChannels.Prepare();
            AtlaNameChannels.ReleaseCooled();
            bool tinted = false;
            for (int unit = 0; unit < EnemyAddresses.FloorSlots.Count; unit++)
            {
                if (!IsAtlaGemron(unit)) continue;
                if (!tinted) { ArmTint(unit); tinted = true; }
                long block = ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride;
                if (Memory.ReadInt(block + BlockMotion) != DeathMotion) continue;
                float frame = Memory.ReadFloat(block + BlockFrame);
                if (frame < DeathStart) continue;
                Pending p = _pending.Find(x => x.Unit == unit);
                if (p == null) Spawn(unit, block, frame < AtlaGemronBake.Swap);
                else if (frame < AtlaGemronBake.Swap) Refresh(p);
            }
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending p = _pending[i];
                if (p.Unit >= 0 && !Dying(p.Unit)) p.Unit = -1;                       // its slot may hold another Gemron later
                if (p.Bounce >= 0 && Memory.ReadInt(Bounce(p.Bounce) + CodeCaves.AtlaBounceIndex) != p.Atra) Settle(p);
                if (p.Entry < 0) { if (p.Unit < 0 && p.Bounce < 0) _pending.RemoveAt(i); continue; }   // nothing to hand over: kept while it dies
                if (Memory.ReadInt(AtlaSystem.PartsEntryAddr(p.Dungeon, p.Entry)) == p.Sentinel) continue;   // still on the floor
                AtlaNameChannels.Cool(p.Channel, p.Dungeon, p.Sentinel);
                Deliver(p.Kind, p.WeaponId, "collected");
                p.Entry = -1;
            }
        }

        private static bool Dying(int unit) =>
            IsAtlaGemron(unit) && Memory.ReadInt(ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride + BlockMotion) == DeathMotion;

        /// <summary>A new floor: its visuals are other objects, and an atla left behind is gone (its parts entry, name channel and
        /// bounce freed).</summary>
        internal static void Reset()
        {
            Lens.Reset();
            foreach (Pending p in _pending)
            {
                if (p.Entry < 0) continue;
                long ea = AtlaSystem.PartsEntryAddr(p.Dungeon, p.Entry);
                if (Memory.ReadInt(ea) == p.Sentinel) { Memory.WriteInt(ea, -1); Memory.WriteInt(ea + 8, 0); }
                AtlaNameChannels.Release(p.Channel, p.Dungeon, p.Sentinel);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{p.Name} left on the floor — lost");
            }
            _pending.Clear();
            for (int b = 0; b < CodeCaves.AtlaBounceCount; b++) Memory.WriteInt(Bounce(b) + CodeCaves.AtlaBounceIndex, -1);
        }

        private static bool IsAtlaGemron(int unit)
        {
            long a = EnemyAddresses.FloorSlots.SlotAddr(unit, 0);
            return Memory.ReadInt(a + EnemySlotOffsets.RenderStatus) > 0 && Memory.ReadUShort(a + EnemySlotOffsets.EnemySpeciesId) == EnemySpecies.AtlaGemron.Id;
        }

        private static long Bounce(int b) => CodeCaves.AtlaBounce + (long)b * CodeCaves.AtlaBounceStride;

        private static void ArmTint(int unit) => Lens.Arm(unit, LensNode);

        // ───────────────────────────── the drop ─────────────────────────────
        private static void Spawn(int unit, long block, bool bounce)
        {
            var p = new Pending { Unit = unit };
            int r = _rng.Next(100);
            p.Kind = r < 5 ? Drop.Sword : r < 30 ? Drop.AtlaSphere : Drop.Sphere;
            p.WeaponId = p.Kind == Drop.Sphere ? RandomWeapon() : Items.atlamilliasword;
            p.Name = p.Kind == Drop.Sword ? Items.ItemNameTbl[p.WeaponId] : Items.ItemNameTbl[p.WeaponId] + " SynthSphere";
            p.Map = DungeonAddresses.Map.Deref(Memory.ReadUInt(DungeonAddresses.Map.NowDngMapPtr));
            p.Dungeon = Memory.ReadByte(Addresses.checkDungeon);
            if (p.Dungeon == DemonShaft) p.Dungeon = GalleryOfTime;                     // the atla tables hold six dungeons: atla_dungeon.s collects
                                                                                         // Demon Shaft's atlas through Gallery of Time's
            string why = p.Map == 0 ? "no dungeon map" : p.Dungeon < 0 || p.Dungeon >= AtlaSystem.DungeonCount ? $"dungeon {p.Dungeon}"
                       : Memory.ReadUInt(p.Map + MapAtraModel) == 0 ? "no atla model on this floor" : null;
            int reuse = -1;
            if (why == null && Memory.ReadInt(p.Map + MapAtraNum) >= AtraMax)
            {
                int take = NearestAtla(p.Map, unit);                                     // every place taken: the nearest atla still here comes to it
                if (take >= 0) { TakeOver(p, unit, block, take, bounce); return; }
                for (int i = 0; i < AtraMax && reuse < 0; i++)                           // all eight collected: a collected one's place
                    if (Memory.ReadInt(p.Map + MapAtra + (long)i * AtraStride + AtraUsed) == 0) reuse = i;
                if (reuse < 0) why = "no atla place on this floor";
            }
            if (why == null && !Enumerable.Range(0, EventCount).Any(e => Memory.ReadInt(EventAddr(p.Map, e) + EventKind) == -1)) why = "no free map event";
            if (why == null)
            {
                p.Entry = -1;
                for (int e = AtlaSystem.PartsCount - 1; e >= 0 && p.Entry < 0; e--)
                    if (Memory.ReadInt(AtlaSystem.PartsEntryAddr(p.Dungeon, e)) == -1) p.Entry = e;
                if (p.Entry < 0) why = "no free parts entry";
            }
            if (why == null && (p.Sentinel = AtlaNameChannels.TakeSentinel(p.Dungeon)) < 0) why = "every sentinel part id busy";
            if (why != null)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no atla ({why}) — {p.Name} handed over at once");
                HandOver(p);
                return;
            }
            p.Channel = AtlaNameChannels.Claim(p.Dungeon, p.Sentinel, "\n" + p.Name);
            long ea = AtlaSystem.PartsEntryAddr(p.Dungeon, p.Entry);
            Memory.WriteInt(ea, p.Sentinel); Memory.WriteInt(ea + 4, -1); Memory.WriteInt(ea + 8, 1);
            var pose = Predict(unit);
            p.Atra = reuse >= 0 ? reuse : Memory.ReadInt(p.Map + MapAtraNum);
            if (bounce)
                for (int b = 0; b < CodeCaves.AtlaBounceCount && p.Bounce < 0; b++)
                    if (Memory.ReadInt(Bounce(b) + CodeCaves.AtlaBounceIndex) < 0) p.Bounce = b;
            if (p.Bounce >= 0)
            {
                long e = Bounce(p.Bounce);
                Memory.WriteUInt(e + CodeCaves.AtlaBounceMap, (uint)(p.Map & Memory.PhysAddrMask));
                Memory.WriteUInt(e + CodeCaves.AtlaBounceBlock, (uint)(block & Memory.PhysAddrMask));
                WritePose(e, pose);
                Memory.WriteInt(e + CodeCaves.AtlaBounceIndex, p.Atra);                // last: the cave keeps it out of the draw from here
            }
            bool called;
            if (reuse >= 0) called = Place(p, pose.Rest);
            else
            {
                for (int k = 0; k < 3; k++) Memory.WriteFloat(CodeCaves.AtlaSpawnPos + k * 4, (float)pose.Rest[k]);
                Memory.WriteFloat(CodeCaves.AtlaSpawnPos + 12, 1f);
                called = NativeCall.Invoke(SetAtraBoll, out _, (uint)(p.Map & Memory.PhysAddrMask), CodeCaves.AtlaSpawnPosGuest, (uint)p.Entry, timeoutMs: 500);
            }
            long atra = p.Map + MapAtra + (long)p.Atra * AtraStride;
            if (!called || Memory.ReadInt(atra + AtraUsed) != 1 || Memory.ReadInt(atra + AtraNo) != p.Entry)
            {
                if (p.Bounce >= 0) Memory.WriteInt(Bounce(p.Bounce) + CodeCaves.AtlaBounceIndex, -1);
                Memory.WriteInt(ea, -1); Memory.WriteInt(ea + 8, 0);
                AtlaNameChannels.Release(p.Channel, p.Dungeon, p.Sentinel);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"SetAtraBoll {(called ? "left no atla" : "not taken")}" + (bounce ? " — trying again" : $" — {p.Name} handed over at once"));
                p.Bounce = -1;
                if (!bounce) HandOver(p);
                return;
            }
            for (int ev = 0; ev < EventCount && p.Event < 0; ev++)
            {
                long a = EventAddr(p.Map, ev);
                if (Memory.ReadInt(a + EventKind) == EventAtra && Memory.ReadInt(a + EventIndex) == p.Atra) p.Event = ev;
            }
            if (p.Bounce >= 0 && p.Event >= 0) Memory.WriteFloat(EventAddr(p.Map, p.Event) + EventRadius, 0f);   // not to be picked up mid-air
            _pending.Add(p);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"atla {p.Atra} for unit {unit}: {p.Name} (parts entry {p.Entry}, sentinel {p.Sentinel}, channel {p.Channel}" +
                (p.Bounce >= 0 ? $", bouncing from frame {AtlaGemronBake.Swap})" : ", at rest)"));
        }

        /// <summary>A collected atla's place given this atla by data writes (SetAtraBoll only appends): the atla, then its event, kind last.</summary>
        private static bool Place(Pending p, double[] rest)
        {
            int ev = Enumerable.Range(0, EventCount).FirstOrDefault(e => Memory.ReadInt(EventAddr(p.Map, e) + EventKind) == -1, -1);
            if (ev < 0) return false;
            long atra = p.Map + MapAtra + (long)p.Atra * AtraStride, a = EventAddr(p.Map, ev);
            for (int k = 0; k < 3; k++) Memory.WriteFloat(atra + k * 4, (float)rest[k]);
            Memory.WriteFloat(atra + 12, 1f);
            Memory.WriteInt(atra + AtraNo, p.Entry); Memory.WriteFloat(atra + AtraPhase, 0f);
            Memory.WriteInt(atra + AtraUsed, 1);
            Memory.WriteInt(a + EventPlaced, 0);
            for (int k = 0; k < 3; k++) Memory.WriteFloat(a + EventPos + k * 4, (float)rest[k]);
            Memory.WriteFloat(a + EventPos + 12, 1f);
            Memory.WriteFloat(a + EventRadius, p.Bounce >= 0 ? 0f : PickupRadius);
            Memory.WriteInt(a + EventIndex, p.Atra);
            Memory.WriteInt(a + EventKind, EventAtra);
            return true;
        }

        /// <summary>The nearest atla still on the floor (drawn, not collected), or -1.</summary>
        private static int NearestAtla(long map, int unit)
        {
            double[] pos = Read3(ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride - CCharacter.CharScale + CCharacter.CharPos);
            int best = -1; double bd = double.MaxValue;
            int num = Math.Min(AtraMax, Memory.ReadInt(map + MapAtraNum));
            for (int i = 0; i < num; i++)
            {
                long atra = map + MapAtra + (long)i * AtraStride;
                if (Memory.ReadInt(atra + AtraUsed) == 0) continue;
                double[] q = Read3(atra);
                double d = (q[0] - pos[0]) * (q[0] - pos[0]) + (q[2] - pos[2]) * (q[2] - pos[2]);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>A floor whose atla places are all taken: atla <paramref name="take"/> (its own content: collected the vanilla way) comes to
        /// the dying Gemron and bounces out of its grip in the drop's stead.</summary>
        private static void TakeOver(Pending p, int unit, long block, int take, bool bounce)
        {
            p.Entry = -1; p.Atra = take;
            for (int ev = 0; ev < EventCount && p.Event < 0; ev++)
            {
                long a = EventAddr(p.Map, ev);
                if (Memory.ReadInt(a + EventKind) == EventAtra && Memory.ReadInt(a + EventIndex) == take) p.Event = ev;
            }
            var pose = Predict(unit);
            if (bounce)
                for (int b = 0; b < CodeCaves.AtlaBounceCount && p.Bounce < 0; b++)
                    if (Memory.ReadInt(Bounce(b) + CodeCaves.AtlaBounceIndex) < 0) p.Bounce = b;
            if (p.Bounce >= 0)
            {
                long e = Bounce(p.Bounce);
                Memory.WriteUInt(e + CodeCaves.AtlaBounceMap, (uint)(p.Map & Memory.PhysAddrMask));
                Memory.WriteUInt(e + CodeCaves.AtlaBounceBlock, (uint)(block & Memory.PhysAddrMask));
                WritePose(e, pose);
                Memory.WriteInt(e + CodeCaves.AtlaBounceIndex, take);
                if (p.Event >= 0) Memory.WriteFloat(EventAddr(p.Map, p.Event) + EventRadius, 0f);
            }
            long atra = p.Map + MapAtra + (long)take * AtraStride;
            for (int k = 0; k < 3; k++) Memory.WriteFloat(atra + k * 4, (float)pose.Rest[k]);
            Memory.WriteFloat(atra + AtraPhase, 0f);
            if (p.Event >= 0) for (int k = 0; k < 3; k++) Memory.WriteFloat(EventAddr(p.Map, p.Event) + EventPos + k * 4, (float)pose.Rest[k]);
            _pending.Add(p);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"every atla place taken: atla {take} (its own content) taken over by unit {unit}" +
                (p.Bounce >= 0 ? $", bouncing from frame {AtlaGemronBake.Swap}" : ", at rest"));
        }

        /// <summary>No atla: the drop now, the death marked done.</summary>
        private static void HandOver(Pending p)
        {
            p.Entry = -1;
            _pending.Add(p);
            Deliver(p.Kind, p.WeaponId, "at once");
        }

        /// <summary>The foreseen pose, every tick until the swap (the unit may still be sliding).</summary>
        private static void Refresh(Pending p)
        {
            if (p.Bounce < 0) return;
            var pose = Predict(p.Unit);
            WritePose(Bounce(p.Bounce), pose);
            long atra = p.Map + MapAtra + (long)p.Atra * AtraStride;
            for (int k = 0; k < 3; k++) Memory.WriteFloat(atra + k * 4, (float)pose.Rest[k]);
            if (p.Event >= 0) for (int k = 0; k < 3; k++) Memory.WriteFloat(EventAddr(p.Map, p.Event) + EventPos + k * 4, (float)pose.Rest[k]);
        }

        /// <summary>The bounce is over (the cave freed its entry): the atla can be picked up.</summary>
        private static void Settle(Pending p)
        {
            if (p.Event >= 0)
            {
                long a = EventAddr(p.Map, p.Event);
                if (Memory.ReadInt(a + EventKind) == EventAtra && Memory.ReadInt(a + EventIndex) == p.Atra) Memory.WriteFloat(a + EventRadius, PickupRadius);
            }
            p.Bounce = -1;
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"atla {p.Atra} settled" + (p.Event >= 0 ? $" (event {p.Event}, radius {Memory.ReadFloat(EventAddr(p.Map, p.Event) + EventRadius)})" : " (no event found)"));
        }

        private static long EventAddr(long map, int ev) => map + MapEvents + (long)ev * EventStride;

        private static int RandomWeapon()
        {
            var pool = new List<int>();
            for (int id = WeaponList.FirstItemId; id < WeaponList.FirstItemId + WeaponList.Count; id++)
            {
                string n = Items.ItemNameTbl[id];
                if (id == Items.atlamilliasword || n.Contains("(broken)") || n.IndexOf("glitch", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.StartsWith("weapon No.") || n == "Empty Slot") continue;
                pool.Add(id);
            }
            return pool[_rng.Next(pool.Count)];
        }

        /// <summary>The drop into the player's hands: the sword by the game's own pickup routine, a sphere onto the attachment board.</summary>
        private static void Deliver(Drop kind, int weaponId, string how)
        {
            if (kind == Drop.Sword)
            {
                bool called = NativeCall.Invoke(GetItem, out uint slot, (uint)(DngStatusData.Base - 0x20000000L), (uint)weaponId, 0u, timeoutMs: 1500);
                if (called && (int)slot >= 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"Atlamillia Sword given ({how}, weapon slot {(int)slot})"); return; }
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"no room for the Atlamillia Sword ({(called ? "GetItem found no free slot" : "GetItem not taken")}) — its SynthSphere instead");
            }
            byte[] sphere = Sphere(weaponId);
            if (sphere == null) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"weapon {weaponId}'s base values unreadable — no sphere"); return; }
            for (int b = 0; b < AttachBoard.ScanCount; b++)
            {
                long entry = AttachBoard.Base + (long)b * AttachBoard.Stride;
                if (Memory.ReadUShort(entry) > 0x50) continue;
                Memory.WriteByteArray(entry, sphere);
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{Items.ItemNameTbl[weaponId]} SynthSphere delivered to the attachment board ({how})");
                return;
            }
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"attachment board full — {Items.ItemNameTbl[weaponId]} SynthSphere lost");
        }

        /// <summary>The weapon's SynthSphere at a sixth of its base values (WeaponList), each rounded up, with all its abilities.</summary>
        private static byte[] Sphere(int weaponId)
        {
            byte[] w = Memory.ReadBytesBatch(WeaponList.EntryAddr(weaponId), WeaponList.Stride);
            if (w == null) return null;
            int Sixth(int offset) => (int)Math.Ceiling(BitConverter.ToInt16(w, offset) / 6.0);
            var s = new byte[AttachBoard.Stride];
            BitConverter.GetBytes((ushort)AttachBoard.SynthSphereId).CopyTo(s, AttachBoard.EntryItemId);
            BitConverter.GetBytes((ushort)weaponId).CopyTo(s, AttachBoard.EntrySourceId);
            BitConverter.GetBytes(BitConverter.ToUInt16(w, WeaponList.Effect1)).CopyTo(s, AttachBoard.EntryFlags);   // Effect1 | Effect2 << 8
            int[] stats = { WeaponList.Attack, WeaponList.Endurance, WeaponList.Speed, WeaponList.Magic };
            for (int i = 0; i < 4; i++) BitConverter.GetBytes((short)Sixth(stats[i])).CopyTo(s, AttachBoard.EntryStats + i * 2);
            for (int i = 0; i < 5; i++) s[AttachBoard.EntryElements + i] = (byte)(sbyte)Sixth(WeaponList.Fire + i * 2);
            for (int i = 0; i < 10; i++) s[AttachBoard.EntryAntis + i] = (byte)(sbyte)Sixth(WeaponList.DinoSlayer + i * 2);
            return s;
        }

        // ───────────────────────────── the bounce ─────────────────────────────
        private sealed class Pose
        {
            public double[] Rest, Q0, Q1, K0, K1;
            public double Omega, InvSin, Scale;
        }

        private static void WritePose(long e, Pose pose)
        {
            Memory.WriteFloat(e + CodeCaves.AtlaBounceOmega, (float)pose.Omega);
            Memory.WriteFloat(e + CodeCaves.AtlaBounceInvSin, (float)pose.InvSin);
            for (int k = 0; k < 4; k++) { Memory.WriteFloat(e + CodeCaves.AtlaBounceQ0 + k * 4, (float)pose.Q0[k]); Memory.WriteFloat(e + CodeCaves.AtlaBounceQ1 + k * 4, (float)pose.Q1[k]); }
            Memory.WriteFloat(e + CodeCaves.AtlaBounceScale, (float)pose.Scale);
            for (int k = 0; k < 3; k++) { Memory.WriteFloat(e + CodeCaves.AtlaBounceK0 + k * 4, (float)pose.K0[k]); Memory.WriteFloat(e + CodeCaves.AtlaBounceK1 + k * 4, (float)pose.K1[k]); }
        }

        /// <summary>The held atla's world pose on the swap frame, foreseen: the unit's root frame (CCharacter::Draw: its scale on the rows,
        /// its Euler turn X then Y then Z, its place added — CFrame::GetLWMatrix) round the bake's root matrix, the graft's chain on it;
        /// then where a real atla rests (the sphere's centre over the held one's, on the unit's floor) and the bounce's numbers.</summary>
        private static Pose Predict(int unit)
        {
            if (!_rcLoaded) { _rc = AtlaGemronBake.Numbers; _rcLoaded = true; }
            long chara = ModelScaleOffsets.ModelBase + (long)unit * ModelScaleOffsets.ModelStride - CCharacter.CharScale;
            double[] pos = Read3(chara + CCharacter.CharPos), rot = Read3(chara + CCharacter.CharRot), scl = Read3(chara + CCharacter.CharScale);
            double[][] L0 = AtlaGemronBake.Matrix(_rc.GetProperty("swapRoot")), G = AtlaGemronBake.Matrix(_rc.GetProperty("swapGraft"));
            double g = _rc.GetProperty("g").GetDouble();
            double[] c = AtlaGemronBake.Vector(_rc.GetProperty("atlaCentre")), T1 = AtlaGemronBake.Vector(_rc.GetProperty("atlaRootT"));
            double[][] R1 = AtlaGemronBake.Matrix(_rc.GetProperty("atlaRootRows"));
            double[] q1 = AtlaGemronBake.Vector(_rc.GetProperty("atlaRootQuat"));
            var N = new double[4][];
            for (int r = 0; r < 3; r++) N[r] = L0[r].Select(x => x * scl[r]).ToArray();
            N[3] = (double[])L0[3].Clone();
            N = Mul(N, Rot(0, rot[0])); N = Mul(N, Rot(1, rot[1])); N = Mul(N, Rot(2, rot[2]));
            for (int k = 0; k < 3; k++) N[3][k] += pos[k];
            double[][] W = Mul(G, N);
            double[] len = Enumerable.Range(0, 3).Select(r => Math.Sqrt(W[r][0] * W[r][0] + W[r][1] * W[r][1] + W[r][2] * W[r][2])).ToArray();
            double[][] R0 = Enumerable.Range(0, 3).Select(r => new[] { W[r][0] / len[r], W[r][1] / len[r], W[r][2] / len[r] }).ToArray();
            double sc = (len[0] + len[1] + len[2]) / 3 * g;
            double[] S = Enumerable.Range(0, 3).Select(j => g * (c[0] * W[0][j] + c[1] * W[1][j] + c[2] * W[2][j]) + W[3][j]).ToArray();
            double[] cR0 = Enumerable.Range(0, 3).Select(j => c[0] * R0[0][j] + c[1] * R0[1][j] + c[2] * R0[2][j]).ToArray();
            double[] cR1 = Enumerable.Range(0, 3).Select(j => c[0] * R1[0][j] + c[1] * R1[1][j] + c[2] * R1[2][j]).ToArray();
            double[] rest = { S[0] - cR1[0], pos[1], S[2] - cR1[2] };
            double[] q0 = QuatOfRows(R0);
            double dot = q0[0] * q1[0] + q0[1] * q1[1] + q0[2] * q1[2] + q0[3] * q1[3];
            if (dot < 0) { q0 = q0.Select(x => -x).ToArray(); dot = -dot; }                  // the shorter way round
            double omega = Math.Max(1e-4, Math.Acos(Math.Min(1.0, dot)));
            return new Pose
            {
                Rest = rest, Q0 = q0, Q1 = q1, Omega = omega, InvSin = 1.0 / Math.Sin(omega), Scale = sc,
                K0 = Enumerable.Range(0, 3).Select(j => S[j] - sc * cR0[j] - T1[j]).ToArray(),
                K1 = rest,
            };
        }

        private static double[] Read3(long a) => new double[] { Memory.ReadFloat(a), Memory.ReadFloat(a + 4), Memory.ReadFloat(a + 8) };

        /// <summary>sceVu0RotMatrixX/Y/Z's rotation (row-vector rows; the frame's matrix is multiplied by it on the right).</summary>
        private static double[][] Rot(int axis, double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return axis == 0 ? new[] { new[] { 1.0, 0, 0, 0 }, new[] { 0, c, s, 0 }, new[] { 0, -s, c, 0 }, new[] { 0.0, 0, 0, 1 } }
                 : axis == 1 ? new[] { new[] { c, 0, -s, 0 }, new[] { 0.0, 1, 0, 0 }, new[] { s, 0, c, 0 }, new[] { 0.0, 0, 0, 1 } }
                 : new[] { new[] { c, s, 0, 0 }, new[] { -s, c, 0, 0 }, new[] { 0.0, 0, 1, 0 }, new[] { 0.0, 0, 0, 1 } };
        }

        private static double[][] Mul(double[][] a, double[][] b)
        {
            var o = new double[4][];
            for (int r = 0; r < 4; r++) { o[r] = new double[4]; for (int q = 0; q < 4; q++) for (int k = 0; k < 4; k++) o[r][q] += a[r][k] * b[k][q]; }
            return o;
        }

        /// <summary>(w, x, y, z) from which QuatToMat (CFrame::SetTransMatrix(float *)) builds the row-vector rotation M
        /// (atla_gemron.quat_of_rows).</summary>
        private static double[] QuatOfRows(double[][] M)
        {
            double tr = M[0][0] + M[1][1] + M[2][2], s;
            if (tr > 0) { s = 2 * Math.Sqrt(1 + tr); return new[] { s / 4, (M[1][2] - M[2][1]) / s, (M[2][0] - M[0][2]) / s, (M[0][1] - M[1][0]) / s }; }
            if (M[0][0] >= M[1][1] && M[0][0] >= M[2][2]) { s = 2 * Math.Sqrt(1 + M[0][0] - M[1][1] - M[2][2]); return new[] { (M[1][2] - M[2][1]) / s, s / 4, (M[1][0] + M[0][1]) / s, (M[2][0] + M[0][2]) / s }; }
            if (M[1][1] >= M[2][2]) { s = 2 * Math.Sqrt(1 + M[1][1] - M[0][0] - M[2][2]); return new[] { (M[2][0] - M[0][2]) / s, (M[1][0] + M[0][1]) / s, s / 4, (M[2][1] + M[1][2]) / s }; }
            s = 2 * Math.Sqrt(1 + M[2][2] - M[0][0] - M[1][1]); return new[] { (M[0][1] - M[1][0]) / s, (M[2][0] + M[0][2]) / s, (M[2][1] + M[1][2]) / s, s / 4 };
        }
    }
}
