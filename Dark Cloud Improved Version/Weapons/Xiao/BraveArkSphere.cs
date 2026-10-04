using System;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>Super Steve with a Brave Ark sphere — "Hero's Courage" on Xiao: Freeze, Poison, Curse and Goo are cleared from
    /// her status word every tick while the sphere is attached. Stateless, so there is nothing to restore when it goes.
    /// Mirrors <see cref="BraveArk.HerosCourageEffect"/>, whose loop is written against ToanState.Status; a wielder parameter
    /// there (the character's status address) would let it serve both. Driven from Super Steve's sphere dispatch.</summary>
    internal static class BraveArkSphere
    {
        private const string Tag = "[BraveArkSphere] ";

        /// <summary>Hero's Courage (Brave Ark): while <paramref name="active"/>, clear Freeze/Poison/Curse/Goo
        /// from Xiao's status word. Stateless (nothing to restore), so it just no-ops when inactive.</summary>
        internal static void Drive(bool active)
        {
            if (!active) return;
            const ushort resistMask = ToanState.StatusFreeze | ToanState.StatusPoison |
                                      ToanState.StatusCurse  | ToanState.StatusGoo;
            ushort status = Memory.ReadUShort(Player.Xiao.status);
            if ((status & resistMask) != 0)
                Memory.WriteUShort(Player.Xiao.status, (ushort)(status & ~resistMask));
        }
    }
}
