using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Flamingo — enemies can be locked on to from twice as far. The reach is the enemy's lock-on distance
    /// (<see cref="EnemySlotOffsets.LockOnDistance"/>, 120 by default) times the character's factor: SetNearLockOnTarget
    /// (acquire) and setTargetCursor (hold) read a six-float table by character id — Toan 1.2, Xiao 1.4, Goro 1.1, Ruby 1.5,
    /// Ungaga 1.0, Osmond 1.8 — which the ISO's dun.bin patch (DunPatches) moves from the overlay to
    /// <see cref="CodeCaves.LockOnFactorTable"/>, runtime data the PNACH seeds with the vanilla six while nobody owns it.
    /// While a granting weapon is equipped the driver owns the table and holds Xiao's factor at <see cref="XiaoFactor"/>; the
    /// vanilla 1.4 goes back when the weapon goes. The reach is the Flamingo's, inherited by Dragon's Y, Divine Beast
    /// Title, Angel Shooter and Angel Gear (<see cref="GrantsReach"/>), and by Super Steve carrying any of their
    /// SynthSpheres (<see cref="CustomXiaoEffects.SuperSteveEffect"/> drives it; the others share
    /// <see cref="CustomXiaoEffects.LockOnReachEffect"/>).
    ///
    /// Owning them (Xiao's bag or the storage) is a passive for fishing: every bait's notice radius — the distance at which a
    /// fish turns toward the hook, copied from <see cref="BaitDetectionRadiusTable"/> into each fish every frame — is
    /// <see cref="BaitNoticeBonus"/> units more per Flamingo owned, up to <see cref="BaitNoticeMaxOwned"/> of them, the bare
    /// hook's too. The table is written at each fishing session's start (<see cref="Fishing.OnSessionStart"/>): the game's
    /// figures plus the bonus, or the game's figures alone when none is owned (the table keeps whatever was last written).
    /// </summary>
    internal static class Flamingo
    {
        private const string Tag = "[Flamingo] ";
        private const float XiaoFactor = 2.8f;      // twice the vanilla 1.4
        private static readonly long XiaoEntry = CodeCaves.LockOnFactorTable + Player.XiaoId * 4;
        private static bool _held, _nativeWarned;
        private const float BaitNoticeBonus = 10f;   // per Flamingo owned …
        private const int   BaitNoticeMaxOwned = 3;  // … up to this many
        private const int   StorageSlots = 30;

        /// <summary>Whether a weapon carries the reach: the Flamingo and the four that inherit it (Dragon's Y, Divine Beast
        /// Title, Angel Shooter, Angel Gear — the lock-on speed's line). Also the test for a SynthSphere's source weapon on
        /// Super Steve.</summary>
        internal static bool GrantsReach(int weaponId)
            => weaponId == Items.flamingo || LockOnSpeed.Grants(weaponId);

        /// <summary>Drive every tick while a granting weapon (or sphere) is equipped; <paramref name="active"/> false releases.</summary>
        internal static void Drive(bool active)
        {
            if (!active) { Stop(); return; }
            if ((uint)Memory.ReadInt(DunPatches.LockOnTableHookAddrMmu) != DunPatches.LockOnTableWord0)
            {
                if (!_nativeWarned) { _nativeWarned = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "the lock-on table patch is not in this ISO — no extra reach (re-patch the ISO)"); }
                return;
            }
            if (Memory.ReadFloat(XiaoEntry) == XiaoFactor) return;
            Memory.WriteInt(CodeCaves.LockOnFactorTable + CodeCaves.LockOnFactorOwner, 1);   // ours: the PNACH stops re-seeding
            Memory.WriteFloat(XiaoEntry, XiaoFactor);
            if (!_held) { _held = true; Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"lock-on reach ×{XiaoFactor / CodeCaves.LockOnFactorVanilla[Player.XiaoId]:F1}"); }
        }

        /// <summary>How many Flamingos are in the inventory: Xiao's ten bag slots and the thirty storage slots.</summary>
        internal static int Owned()
        {
            int n = 0;
            for (int s = 0; s < DngStatusData.MaxWeaponSlots; s++)
                if (Memory.ReadUShort(DngStatusData.WeaponRecord(Player.XiaoId, s)) == Items.flamingo) n++;
            for (int s = 0; s < StorageSlots; s++)
                if (Memory.ReadUShort(Addresses.firstStorageWeapon + s * WeaponHave.InventoryWeaponSlotStride) == Items.flamingo) n++;
            return n;
        }

        /// <summary>The bait notice table for this fishing session: each entry's own figure, plus the bonus per Flamingo owned
        /// (up to <see cref="BaitNoticeMaxOwned"/>).</summary>
        internal static void ApplyBaitBonus()
        {
            int owned = Math.Min(Owned(), BaitNoticeMaxOwned);
            float bonus = owned * BaitNoticeBonus;
            foreach (var bait in BaitDetectionRadiusTable.All)
                Memory.WriteFloat(bait.Radius, bait.DefaultRadius + bonus);
            if (owned > 0) Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + $"{owned} owned: every bait notices from {bonus:F0} units further this session");
        }

        /// <summary>The weapon or the floor went: Xiao's vanilla factor back.</summary>
        internal static void Stop()
        {
            if (!_held) return;
            _held = false;
            if (Memory.ReadFloat(XiaoEntry) == XiaoFactor) Memory.WriteFloat(XiaoEntry, CodeCaves.LockOnFactorVanilla[Player.XiaoId]);
            Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + Tag + "lock-on reach released");
        }
    }
}
