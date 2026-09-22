using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// The CCollisionData pool (NowColData): the hit spheres the engine tests every frame — enemy swings that hurt the
    /// player, and the player's own attacks that CMonstorUnit::CheckDmg (0x1D9F10) tests each enemy against. A feature
    /// lands a hit by planting an entry here and letting the engine find it: <see cref="PlayerHitEntry"/> builds the
    /// player-attack form, the caller overlays anything of its own, and <see cref="Plant"/> writes it with the active
    /// flag set LAST so a half-written entry is never tested.
    /// </summary>
    internal static class CollisionPool
    {
        internal const long Pointer   = 0x202A35E0;   // NowColData: native pointer to the pool
        internal const int  Entries   = 96;
        internal const int  Stride    = 0xA0;
        internal const int  ActiveOff = 0x3C00;       // int per entry, after the entries

        // entry fields
        internal const int  EntryClass = 0x38;        // float: 0 for a melee-style sphere; a SHOT's impact sphere is non-zero
        internal const int  Radius     = 0x3C;
        internal const int  Mask       = 0x48;        // bit 0 = hurts the player
        internal const int  Element    = 0x50;        // ONE pure element bit, or 0 — a status bit here misroutes CheckDmg's element branch
        internal const int  BlowDir    = 0x20;        // vec3 — the direction the PLAYER path throws its victim. Set__CCollisionData
                                                      // defaults it to (1,0,0): BtCheckDamageProc copies it to a scratch and hands
                                                      // that to unitBlowActionRot, which takes atan2(x, z) − π and SETS the player's
                                                      // facing from it. Left at the default, every hit throws him the same way in
                                                      // WORLD space, which reads as a random direction relative to him.
        internal const int  Owner      = 0x58;        // enemy swings: slot*5+200
        internal const int  GateA      = 0x70, GateB = 0x74;   // the entry is open to CheckHitUser while these are equal
        internal const uint HurtsPlayerMask = 1;

        private const long BattleWeaponStats = WeaponHave.BattleWeaponRecord + 0x1C;   // anti-category bytes (entry +0x64 points here)
        private const long BattleWeaponFlags = WeaponHave.BattleWeaponRecord + 0xEE;   // ability flags (entry +0x6C)

        /// <summary>The pool's MMU address, or 0 when it is not allocated (out of a dungeon floor).</summary>
        internal static long Resolve()
        {
            long p = Memory.ReadInt(Pointer);
            return p > 0 ? Memory.ToMmu(p) : 0;
        }

        /// <summary>The highest free entry, or −1: the engine's own spheres fill from 0, so planting from the top keeps clear
        /// of them.</summary>
        internal static int TakeFreeSlot(long pool)
        {
            for (int i = Entries - 1; i >= 0; i--)
                if (Memory.ReadInt(pool + ActiveOff + i * 4) == 0) return i;
            return -1;
        }

        internal static bool IsActive(long pool, int slot) => Memory.ReadInt(pool + ActiveOff + slot * 4) != 0;
        internal static void Deactivate(long pool, int slot) => Memory.WriteInt(pool + ActiveOff + slot * 4, 0);

        /// <summary>Write an entry and only then mark it active.</summary>
        internal static void Plant(long pool, int slot, byte[] entry)
        {
            Memory.WriteBytesBatch(pool + slot * Stride, entry);
            Memory.WriteInt(pool + ActiveOff + slot * 4, 1);
        }

        /// <summary>A player-attack sphere at (x, h, y): the form CheckDmg accepts as one of the player's own hits — damage
        /// <paramref name="baseDmg"/> before the enemy's defence, the equipped weapon's stats and ability flags, and
        /// <paramref name="attr"/> as its element bit (0 = none). Kick words (+0x80..+0x98) are left zero for the caller.</summary>
        internal static byte[] PlayerHitEntry(float x, float h, float y, float radius, int baseDmg, uint attr)
        {
            var e = new byte[Stride];
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            void I(int o, int v)   => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x00, x); F(0x04, h); F(0x08, y); F(0x0C, 1f);
            F(0x1C, 1f); F(0x20, 1f);
            I(0x34, baseDmg); I(EntryClass, 0); F(Radius, radius);
            I(0x44, 1); I(Mask, 2); I(0x4C, 2); I(Element, (int)attr); I(0x54, 0);
            I(Owner, 1); I(0x5C, -1); I(0x60, 0);
            I(0x64, (int)(BattleWeaponStats - 0x20000000)); I(0x68, -1); I(0x6C, Memory.ReadShort(BattleWeaponFlags));
            I(GateA, 0); I(GateB, 0); F(0x8C, 1f);
            return e;
        }

        /// <summary>A sphere at (x, h, y) that hurts the PLAYER — the form BtCheckDamageProc (dun 0x1DBAFD0) accepts as
        /// an enemy's attack. <paramref name="baseDmg"/> is the damage BEFORE the player's defence: the handler
        /// subtracts it and clamps at zero, so the HP lost is exactly baseDmg − defence.
        ///
        /// <paramref name="reaction"/> (+0x4C) is the one field governing guardability and knockover: 2 guardable
        /// knockback, 3 unguardable knockdown (damage, <c>unitBlowActionRot</c> spins the player to the blow
        /// direction, the big-damage stagger motion, ~160-frame stun), 4 light flinch. <see cref="Owner"/> is left −1
        /// so no enemy slot is credited — the handler only runs its attacker-specific work when it is not −1. The
        /// status-flag word (+0x50) is 0: no ailment rides along.
        ///
        /// <paramref name="dirX"/>/<paramref name="dirY"/>/<paramref name="dirZ"/> is the BLOW DIRECTION
        /// (<see cref="BlowDir"/>) — the one thing that steers where the victim is thrown. ⚠ NOT the kick words at
        /// +0x80..+0x98: those are the ENEMY path's, written with the attacker's POSITION, and the player path never
        /// reads them. Pass the direction pointing from the blow toward the player.</summary>
        internal static byte[] PlayerHurtEntry(float x, float h, float y, float radius, int baseDmg, int reaction,
                                               float dirX, float dirY, float dirZ)
        {
            var e = new byte[Stride];
            void F(int o, float v) => BitConverter.GetBytes(v).CopyTo(e, o);
            void I(int o, int v)   => BitConverter.GetBytes(v).CopyTo(e, o);
            F(0x00, x); F(0x04, h); F(0x08, y); F(0x0C, 1f);
            F(0x1C, 1f);
            F(BlowDir, dirX); F(BlowDir + 4, dirY); F(BlowDir + 8, dirZ);
            I(0x34, baseDmg); I(EntryClass, 0); F(Radius, radius);
            I(0x44, 2); I(Mask, (int)HurtsPlayerMask); I(0x4C, reaction); I(Element, 0); I(0x54, 0);
            I(Owner, -1); I(0x5C, -1); I(0x60, -1);
            I(0x64, 0); I(0x68, -1); I(0x6C, 1);
            I(GateA, 0); I(GateB, 0); F(0x8C, 1f);
            return e;
        }
    }
}
