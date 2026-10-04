using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Forwarders for the names three files outside the Weapons split still use; each points at the member's home, and
    /// this file goes once they are repointed. Town/TownCharacter.cs and ModWindow.cs: the empty-SynthSphere listener thread
    /// (<see cref="WeaponSynthSphereLevel"/>). IsoPatch/ElfWeaponPatches.cs: four <see cref="WeaponTable"/> constants.
    /// Weapons/Xiao/HeavensCloudSphere.cs: the two equipped-record reads (<see cref="WeaponModelFrames"/>).</summary>
    public class Weapons
    {
        public static Thread weaponsMenuListener
        {
            get => WeaponSynthSphereLevel.Listener;
            set => WeaponSynthSphereLevel.Listener = value;
        }
        public static void WeaponListenForSynthSphere() => WeaponSynthSphereLevel.Listen();

        public const int buildup      = WeaponTable.BuildUp;
        public const int weaponoffset = WeaponTable.Stride;
        public const int xiaooffset   = WeaponTable.XiaoOffset;
        public const int woodenid     = WeaponTable.WoodenSlingshotId;

        internal static long EquippedRecord() => WeaponModelFrames.EquippedRecord();
        internal static int SelectedElementBits(long rec) => WeaponModelFrames.SelectedElementBits(rec);
    }
}
