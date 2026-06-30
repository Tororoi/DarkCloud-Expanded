using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// Static per-weapon data for Toan's swords (character c01). <see cref="Dcol1"/> is the "dcol1" bone's
    /// local-matrix Z translation in the weapon's COMMENU model (<c>commenu\weapon\c01wNN.chr</c> → embedded
    /// .mds → bone "dcol1", matrix row 3) — the value the game loads and uses for the melee hit point (read
    /// live via <c>*(*NowWeapon+0xBC)</c>). <see cref="CommenuCombatScale"/> is that commenu dcol1 expressed
    /// relative to the in-combat model's dcol1 (<c>dun\item\main_wep\c01wNN.mds</c>): 1.0 = identical,
    /// &lt;1.0 = the display/commenu model is shrunk vs the combat model. Only the long swords differ; the
    /// shrink is non-uniform per bone (no single model scale) and bones are NOT renamed between models.
    /// Values extracted offline from data.dat. See docs/weapon-reach.md.
    /// </summary>
    internal struct WeaponData
    {
        internal int    Id;                  // in-game item id (257 = c01w00 .. 298 = c01w41)
        internal string Code;                // model code "c01wNN" (NN = Id - 257)
        internal string Name;                // weapon name (from ItemData)
        internal float? Dcol1;               // commenu-model dcol1 bone local-Z; null = no dcol1 frame
        internal float  CommenuCombatScale;  // commenu dcol1 / combat-model dcol1 (1.0 = same; <1 = commenu shrunk)
    }

    /// <summary>Static repository of <see cref="WeaponData"/> for Toan's weapons, keyed by item id.</summary>
    internal static class WeaponDb
    {
        internal static readonly WeaponData[] All =
        {
            new WeaponData { Id = 257, Code = "c01w00", Name = "Dagger (broken)",  Dcol1 =   4.920f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 258, Code = "c01w01", Name = "Dagger",           Dcol1 =   5.808f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 259, Code = "c01w02", Name = "Baselard",         Dcol1 =   8.083f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 260, Code = "c01w03", Name = "Gladius",          Dcol1 =   7.108f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 261, Code = "c01w04", Name = "Wise Owl Sword",   Dcol1 =   7.998f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 262, Code = "c01w05", Name = "Crysknife",        Dcol1 =   6.990f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 263, Code = "c01w06", Name = "Antique Sword",    Dcol1 =   6.941f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 264, Code = "c01w07", Name = "Buster Sword",     Dcol1 =   8.160f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 265, Code = "c01w08", Name = "Kitchen Knife",    Dcol1 =   4.187f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 266, Code = "c01w09", Name = "Tsukikage",        Dcol1 =   9.603f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 267, Code = "c01w10", Name = "Sun Sword",        Dcol1 =  10.074f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 268, Code = "c01w11", Name = "Serpent Sword",    Dcol1 =   9.012f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 269, Code = "c01w12", Name = "Macho Sword",      Dcol1 =   9.382f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 270, Code = "c01w13", Name = "Shamshir",         Dcol1 =   7.985f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 271, Code = "c01w14", Name = "Heaven's Cloud",   Dcol1 =   9.205f, CommenuCombatScale = 0.868f }, // shrunk display model
            new WeaponData { Id = 272, Code = "c01w15", Name = "Lamb's Sword",     Dcol1 =  10.015f, CommenuCombatScale = 1.000f }, // combat .mds has no dcol1 bone
            new WeaponData { Id = 273, Code = "c01w16", Name = "Dark Cloud",       Dcol1 =  10.355f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 274, Code = "c01w17", Name = "Brave Ark",        Dcol1 =   8.492f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 275, Code = "c01w18", Name = "Big Bang",         Dcol1 =  11.378f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 276, Code = "c01w19", Name = "Atlamillia Sword", Dcol1 =   9.937f, CommenuCombatScale = 0.861f }, // shrunk display model
            new WeaponData { Id = 277, Code = "c01w20", Name = "weapon No.277",    Dcol1 =   null,   CommenuCombatScale = 1.000f }, // no dcol1 frame
            new WeaponData { Id = 278, Code = "c01w21", Name = "Mardan Eins",      Dcol1 =   8.995f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 279, Code = "c01w22", Name = "Mardan Twei",      Dcol1 =  10.365f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 280, Code = "c01w23", Name = "Arise Mardan",     Dcol1 =  11.798f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 281, Code = "c01w24", Name = "Aga's Sword",      Dcol1 =  10.037f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 282, Code = "c01w25", Name = "Evilcise",         Dcol1 =   9.437f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 283, Code = "c01w26", Name = "Small Sword",      Dcol1 =   6.612f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 284, Code = "c01w27", Name = "Sand Breaker",     Dcol1 =   9.992f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 285, Code = "c01w28", Name = "Drain Seeker",     Dcol1 =  11.494f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 286, Code = "c01w29", Name = "Chopper",          Dcol1 =   8.378f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 287, Code = "c01w30", Name = "Choora",           Dcol1 = -11.120f, CommenuCombatScale = 1.000f }, // mirrored model (Dcol1 negative)
            new WeaponData { Id = 288, Code = "c01w31", Name = "Claymore",         Dcol1 =  10.384f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 289, Code = "c01w32", Name = "Maneater",         Dcol1 =   7.760f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 290, Code = "c01w33", Name = "Bone Rapier",      Dcol1 =  10.400f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 291, Code = "c01w34", Name = "Sax",              Dcol1 =  10.412f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 292, Code = "c01w35", Name = "7 Branch Sword",   Dcol1 =  11.191f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 293, Code = "c01w36", Name = "Dusack",           Dcol1 =  10.705f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 294, Code = "c01w37", Name = "Cross Hinder",     Dcol1 =  10.370f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 295, Code = "c01w38", Name = "7th Heaven",       Dcol1 =  10.232f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 296, Code = "c01w39", Name = "Sword Of Zeus",    Dcol1 =  11.581f, CommenuCombatScale = 1.000f },
            new WeaponData { Id = 297, Code = "c01w40", Name = "Chronicle Sword",  Dcol1 =   9.437f, CommenuCombatScale = 0.662f }, // shrunk display model
            new WeaponData { Id = 298, Code = "c01w41", Name = "Chronicle 2",      Dcol1 =   9.437f, CommenuCombatScale = 0.662f }, // shrunk display model
        };

        private static readonly Dictionary<int, WeaponData> ById;

        static WeaponDb()
        {
            ById = new Dictionary<int, WeaponData>(All.Length);
            foreach (WeaponData w in All) ById[w.Id] = w;
        }

        /// <summary>Look up a weapon's static data by item id.</summary>
        internal static bool TryGetValue(int id, out WeaponData data) => ById.TryGetValue(id, out data);
    }
}
