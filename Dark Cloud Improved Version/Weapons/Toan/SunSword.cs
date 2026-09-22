using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Sun Sword — every enemy killed has a chance to drop a Sun attachment (Big Bang inherits), and a guard-charged
    /// blade whose next swing blinds the room (Solar Flash; helpers in SunSword/).</summary>
    internal static class SunSword
    {
        private static readonly Random random = new Random();

        // ── Sun Sword "Solar Flash" ────────────────────────────────────────────────────────
        private const int    TickMs               = 30;
        private const double ChargeSeconds        = 1.5;    // guard held this long primes the blade
        internal const float FlashRadius          = 300f;   // the hit and the blinding reach this far from Toan
        private const float  FlashDamageFraction  = 0.25f;  // the hit's base damage, as a fraction of the weapon's attack
        private const float  KickStrength = 2.0f, KickDecay = 0.3f;   // the hit's shove, sized like Toan's heavier combo hits
        private const int    KickTypeMelee        = 2;      // +0x98: the melee-style reaction (flinch + shove)
        private const int    HitLifeTicks         = 3;      // the planted sphere is withdrawn after this many ticks
        private const float  FlashPulseSpeed      = 90f;    // Toan's own white pulse at the flash (the change effect's rate)
        internal const double BlindSeconds        = 5.0;    // how long the flash holds the floor
        private  const double WakeSeconds         = 1.3;    // …of which this much is the guard coming back down
        private static DateTime _blindUntil;
        private const float  PrimedTint           = 45f;    // the slight white Toan keeps while the charge is held, per channel (the tint is an ambient ADD)
        private const ushort FlashSe              = 0;      // sound effect at the flash (SeSeq id; 0 = none)
        private const float  Combo1Hit = 825f, Combo2Hit = 835f, Combo3Hit = 843f, Combo4Hit = 852f, Combo5Hit = 870f;   // frame cursor at which each combo swing comes forward (docs/character-motion-table.md clips 37-41)

        private enum Phase { Idle, Charging, Primed, Windup }
        private sealed class SolarState
        {
            public Phase phase;
            public DateTime holdStart;
            public byte floor = 0xFF;
            public readonly List<(int slot, int ticks)> planted = new List<(int, int)>();
        }

        /// <summary>
        /// Ability Name: Solar Flash (Sun Sword)
        /// Hold guard and the blade whitens over <see cref="ChargeSeconds"/>; at full it is PRIMED (the charge-complete
        /// pulse marks it) and stays so, guard or not. The next attack carries the charge: as the swing comes forward the
        /// blade returns to its own colour and the dungeon flashes blinding white, easing back over a second
        /// (<see cref="SolarLighting"/>). Every enemy within <see cref="FlashRadius"/> takes a light hit — a quarter of the
        /// weapon's attack through the normal formula, with the sword's element and a melee stagger — and is then blinded
        /// for <see cref="BlindSeconds"/>: its OWN script holds its guard and cancels its movement, frame by frame
        /// (<see cref="SolarScript"/>). The blade tint is <see cref="SolarBlade"/>. Dungeon only; a sidekick out or a floor
        /// change drops the charge.
        /// </summary>
        public static void SolarFlashEffect()
        {
            var st = new SolarState();
            while (Player.Weapon.GetCurrentWeaponId() == Items.sunsword && Player.InDungeonFloor())
            {
                Thread.Sleep(TickMs);
                try { SolarTick(st); }
                catch (Exception ex) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SunSword] Solar Flash tick error: " + ex.Message); }
            }
            SolarReset(st);
        }

        private static void SolarTick(SolarState st)
        {
            byte floor = Memory.ReadByte(Addresses.checkFloor);
            if (floor != st.floor) { if (st.floor != 0xFF) SolarReset(st); st.floor = floor; }

            SolarLighting.Tick();
            SolarBlind();
            ExpireHits(st);
            if (Player.CheckDunIsPausedOrMenu()) return;
            if (Player.CurrentCharacterNum() != Player.ToanId)
            {
                if (st.phase != Phase.Idle) { SolarBlade.Clear(); ChargeTint.Clear(); st.phase = Phase.Idle; }
                return;
            }

            int action = Memory.ReadInt(PlayerAction.ChargeActionState);
            switch (st.phase)
            {
                case Phase.Idle:
                    if (GuardWatch.IsGuarding()) { st.phase = Phase.Charging; st.holdStart = GameClock.Now; }
                    break;

                case Phase.Charging:
                {
                    if (!GuardWatch.IsGuarding()) { st.phase = Phase.Idle; SolarBlade.Set(0f); ChargeTint.Clear(); break; }
                    double held = (GameClock.Now - st.holdStart).TotalSeconds;
                    SolarBlade.Set((float)(held / ChargeSeconds));
                    ChargeTint.Ramp(ChargeSeconds - held);
                    if (held >= ChargeSeconds)
                    {
                        st.phase = Phase.Primed;
                        ChargeTint.Clear();                                  // the cyan build-up ends; the white hold below takes over
                        SolarBlade.PaintPeak(true);                          // the blade's own gold goes near-white, all at once
                        SolarGlow.Show();                                    // …and Toan takes a white glow of his own
                        Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SunSword] Solar Flash primed");
                    }
                    break;
                }

                case Phase.Primed:
                    SolarBlade.Set(1f);                                   // re-asserted each tick: a rebuilt model gets it back
                    SolarBlade.PaintPeak(true);
                    SolarGlow.Show();
                    HoldPrimedTint();
                    if (IsAttack(action)) st.phase = Phase.Windup;
                    break;

                case Phase.Windup:
                {
                    SolarBlade.Set(1f);
                    HoldPrimedTint();
                    if (!IsAttack(action)) { st.phase = Phase.Primed; break; }     // the swing was cancelled: still primed
                    bool forward = action == PlayerAction.ActionWhirlwind || action == PlayerAction.ActionLunge
                                || Memory.ReadFloat(PlayerAction.AnimFrameCursor) >= ComboHitFrame(action);
                    if (!forward) break;
                    Flash(st);
                    st.phase = Phase.Idle;
                    break;
                }
            }
        }

        /// <summary>The slight white Toan carries while the charge is held: the same ambient-add field the charge ramp uses,
        /// re-asserted each tick so a status tint or a character swap cannot leave it stuck on.</summary>
        private static void HoldPrimedTint() =>
            Memory.WriteVec3(CCharacter.Base + CCharacter.CharaTint, PrimedTint, PrimedTint, PrimedTint);

        /// <summary>The blinding's clock. The behaviour itself is the enemies' own scripts (SolarScript); this only decides
        /// when they lower their guard and when they get their AI back.</summary>
        private static void SolarBlind()
        {
            if (_blindUntil == default) return;
            double left = (_blindUntil - GameClock.Now).TotalSeconds;
            if (left <= 0) { SolarScript.End(); _blindUntil = default; }
            else if (left <= WakeSeconds) SolarScript.Wake();
        }

        private static bool IsAttack(int action) =>
            (action >= PlayerAction.ActionComboFirst && action <= PlayerAction.ActionComboLast)
            || action == PlayerAction.ActionLunge || action == PlayerAction.ActionWhirlwind;

        private static float ComboHitFrame(int action) => action switch
        {
            PlayerAction.ActionComboFirst     => Combo1Hit,
            PlayerAction.ActionComboFirst + 1 => Combo2Hit,
            PlayerAction.ActionComboFirst + 2 => Combo3Hit,
            PlayerAction.ActionComboFirst + 3 => Combo4Hit,
            _                                 => Combo5Hit,
        };

        /// <summary>The flash itself: blade back to normal, the light to white, Toan's pulse, the hit, the blinding.</summary>
        private static void Flash(SolarState st)
        {
            float px = Memory.ReadFloat(Addresses.dunPositionX), ph = Memory.ReadFloat(Addresses.dunPositionZ), py = Memory.ReadFloat(Addresses.dunPositionY);
            SolarBlade.Clear();                                          // tint off, and the blade's own palette back
            ChargeTint.Clear();                                          // …and the white Toan was holding
            SolarGlow.Hide();
            SolarLighting.Flash();
            Player.FlashActiveCharacter(255f, 255f, 255f, FlashPulseSpeed, 1);
            if (FlashSe != 0) SeSeq.Play(FlashSe, 90);
            PlantFlashHit(st, px, ph, py);
            SolarScript.Begin();           // the enemies' OWN scripts hold the guard from here
            _blindUntil = GameClock.Now.AddSeconds(BlindSeconds);
        }

        /// <summary>One player-attack sphere at Toan, <see cref="FlashRadius"/> wide (CollisionPool: the same entry CheckDmg
        /// tests his sword swings against): base = attack × <see cref="FlashDamageFraction"/>, the sword's selected element as
        /// a pure bit, and a melee kick from his position so every enemy it reaches flinches and is shoved outward.</summary>
        private static void PlantFlashHit(SolarState st, float x, float h, float y)
        {
            long pool = CollisionPool.Resolve();
            if (pool == 0) return;
            int slot = CollisionPool.TakeFreeSlot(pool);
            if (slot < 0) { Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "[SunSword] no free collision entry — the flash hit is lost"); return; }
            float attack = Memory.ReadShort(WeaponHave.BattleWeaponRecord + 0x04);
            int baseDmg = Math.Max(1, (int)Math.Round(attack * FlashDamageFraction));
            uint elem = (uint)Weapons.SelectedElementBits(Weapons.EquippedRecord()) & 0x1F;
            uint attr = (elem != 0 && (elem & (elem - 1)) == 0) ? elem : 0u;
            byte[] e = CollisionPool.PlayerHitEntry(x, h, y, FlashRadius, baseDmg, attr);
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x80, x); F(0x84, h); F(0x88, y);                        // kick origin: Toan
            F(0x90, KickStrength); F(0x94, KickDecay);
            BitConverter.GetBytes(KickTypeMelee).CopyTo(e, 0x98);
            CollisionPool.Plant(pool, slot, e);
            st.planted.Add((slot, HitLifeTicks));
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + $"[SunSword] flash hit at ({x:F0},{h:F0},{y:F0}) r={FlashRadius:F0}: base {baseDmg}, attr 0x{attr:X} → entry {slot}");
        }

        /// <summary>The engine withdraws its own swing spheres when the swing ends; ours is withdrawn here.</summary>
        private static void ExpireHits(SolarState st)
        {
            if (st.planted.Count == 0) return;
            long pool = CollisionPool.Resolve();
            for (int i = st.planted.Count - 1; i >= 0; i--)
            {
                var (slot, ticks) = st.planted[i];
                if (--ticks > 0) { st.planted[i] = (slot, ticks); continue; }
                if (pool != 0) CollisionPool.Deactivate(pool, slot);
                st.planted.RemoveAt(i);
            }
        }

        private static void SolarReset(SolarState st)
        {
            SolarBlade.Clear();
            SolarGlow.Hide();
            ChargeTint.Clear();
            SolarLighting.Restore();
            SolarScript.End(); _blindUntil = default;
            long pool = CollisionPool.Resolve();
            foreach (var (slot, _) in st.planted) if (pool != 0) CollisionPool.Deactivate(pool, slot);
            st.planted.Clear();
            st.phase = Phase.Idle;
        }

        // ── Sun Sword "Solar Harvest" ──────────────────────────────────────────────────────
        /// <summary>
        /// Ability Name: Solar Harvest (Sun Sword, Big Bang)
        /// While the Sun Sword — or its evolution, Big Bang — is wielded, each enemy on the floor
        /// has a 1% chance (rolled once per slot per floor) to drop a Sun attachment instead of
        /// its regular drop when killed. (Big Bang shares this effect for lineage/visual-design
        /// reasons; it will additionally get its own unique effect later.)
        /// Implemented by pre-staging the engine's guaranteed-drop field (<see
        /// cref="EnemySlotOffsets.ForceItemDrop"/> — the same mechanism dungeon keys and miniboss
        /// loot use, consumed by the death-drop block in CMonstorUnit::Step, ELF 0x1DF4C0) while
        /// the sword is in hand, and un-staging when it isn't (unequip, character switch), so
        /// there is no race against the killing blow. Slots already carrying a forced drop
        /// (dungeon key, miniboss loot, mimic key) are never touched. Engine caveat: the drop
        /// path runs items through a small de-dupe set, so a second Sun proc on the same floor
        /// may be swallowed.
        /// </summary>
        public static void SolarHarvestEffect()
        {
            var st = new SunHarvestState(EnemyAddresses.FloorSlots.Count);
            while (Player.InDungeonFloor())
            {
                byte toanSlot = Memory.ReadByte(WeaponHave.InventoryEquipSlotAddr);
                ushort equippedId = Memory.ReadUShort(WeaponHave.InventoryWeaponSlot0Id +
                        toanSlot * WeaponHave.InventoryWeaponSlotStride);
                if (equippedId != Items.sunsword && equippedId != Items.bigbang)
                    break;

                // Kills while a sidekick is out aren't Sun Sword kills — winners keep their win
                // (state is preserved) and are re-staged when Toan takes over again.
                SunHarvestDrive(Player.CurrentCharacterNum() == Player.ToanId, st);
                Thread.Sleep(250);
            }
            SunHarvestDrive(false, st);   // unequipped / left the floor: revert anything still staged
        }

        // Solar Harvest core — shared with Super Steve (an attached Sun Sword / Big Bang sphere).
        // Per-caller state (no shared statics) so the Toan thread and the Xiao thread never fight
        // over the staged slots. `active` = the Sun wielder is currently the acting character.
        internal sealed class SunHarvestState
        {
            public byte LastFloor = 0xFF;
            public readonly bool[] Rolled, Winner, Staged;
            public readonly ushort[] OriginalDrop;
            public SunHarvestState(int n)
            { Rolled = new bool[n]; Winner = new bool[n]; Staged = new bool[n]; OriginalDrop = new ushort[n]; }
        }

        internal static void SunHarvestDrive(bool active, SunHarvestState st)
        {
            const int procPercent = 1;
            int slotCount = EnemyAddresses.FloorSlots.Count;

            byte currentFloor = Memory.ReadByte(Addresses.checkFloor);
            if (currentFloor != st.LastFloor)
            {
                // New floor: the slot array was reinitialized, so forget everything WITHOUT
                // restoring (writing stale values into fresh slots would corrupt them).
                st.LastFloor = currentFloor;
                Array.Clear(st.Rolled, 0, slotCount);
                Array.Clear(st.Winner, 0, slotCount);
                Array.Clear(st.Staged, 0, slotCount);
            }

            if (!active) { SunUnstageAll(st); return; }

            for (int i = 0; i < slotCount; i++)
            {
                if (Enemies.GetFloorEnemyId(i) == 0) continue;
                if (Memory.ReadInt(EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.Hp)) <= 0) continue;

                int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                ushort dropVal = Memory.ReadUShort(dropAddr);

                if (!st.Rolled[i])
                {
                    st.Rolled[i] = true;
                    // Slots that already carry a forced drop (key/miniboss/mimic) are off-limits
                    if (dropVal != 0 && dropVal != 65535) continue;
                    st.Winner[i] = random.Next(100) < procPercent;
                    if (st.Winner[i]) st.OriginalDrop[i] = dropVal;
                }
                if (!st.Winner[i] || st.Staged[i]) continue;

                // Re-check occupancy — a key could have been assigned here after our roll
                if (dropVal != 0 && dropVal != 65535 && dropVal != Items.sun)
                { st.Winner[i] = false; continue; }
                Memory.WriteUShort(dropAddr, (ushort)Items.sun);
                st.Staged[i] = true;
            }
        }

        private static void SunUnstageAll(SunHarvestState st)
        {
            for (int i = 0; i < st.Staged.Length; i++)
            {
                if (!st.Staged[i]) continue;
                st.Staged[i] = false;
                int dropAddr = EnemyAddresses.FloorSlots.SlotAddr(i, EnemySlotOffsets.ForceItemDrop);
                if (Memory.ReadUShort(dropAddr) == Items.sun)   // still ours → restore
                    Memory.WriteUShort(dropAddr, st.OriginalDrop[i]);
            }
        }
    }
}
