namespace Dark_Cloud_Improved_Version
{
    /// <summary>Who is wielding one of Ungaga's weapons: Ungaga with it in hand, or Xiao with Super Steve carrying its SynthSphere
    /// (<see cref="SuperSteve.Wields"/> — the sphere's source weapon selects the inherited effect). Read from the active
    /// character's equipped weapon each call.</summary>
    internal static class UngagaWeapon
    {
        /// <summary>Ungaga is the active character with <paramref name="weaponId"/> equipped.</summary>
        internal static bool Wields(int weaponId)
            => Player.CurrentCharacterNum() == Player.UngagaId && Player.Weapon.GetCurrentWeaponId() == weaponId;

        /// <summary>Ungaga with <paramref name="weaponId"/>, or Xiao with Super Steve and its sphere.</summary>
        internal static bool WieldsOrSphere(int weaponId)
        {
            int ch = Player.CurrentCharacterNum();
            if (ch == Player.UngagaId) return Player.Weapon.GetCurrentWeaponId() == weaponId;
            if (ch == Player.XiaoId) return SuperSteve.Wields(weaponId);
            return false;
        }

        /// <summary>Ungaga with any weapon of <paramref name="line"/>, or Xiao with Super Steve and a sphere of any of them.</summary>
        internal static bool WieldsOrSphere(int[] line)
        {
            int ch = Player.CurrentCharacterNum(), w = Player.Weapon.GetCurrentWeaponId();
            if (ch == Player.UngagaId) return System.Array.IndexOf(line, w) >= 0;
            if (ch == Player.XiaoId) return w == Items.supersteve && System.Array.IndexOf(line, SuperSteve.AttachedSphere(WeaponHave.BattleWeaponRecord)) >= 0;
            return false;
        }
    }
}
