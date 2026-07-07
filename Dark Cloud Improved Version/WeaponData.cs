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
        internal int    MesEntry;            // allmenu.mes entry ordinal (Nth 0xFF01 terminator; -1 = no entry).
                                             // Locate by ORDINAL, never byte offset (offsets vary per pak copy).
        internal string ModDescription;      // in-game menu description override ('\n' = line break; ~24 chars/line,
                                             // 2-3 lines). Placeholder = the vanilla text (docs/weapon-descriptions.md);
                                             // edit to describe the weapon's custom effect. null = leave untouched.
    }

    /// <summary>Static repository of <see cref="WeaponData"/> for TOAN's weapons (swords), keyed by item id.</summary>
    internal static class ToanWeapons
    {
        internal static readonly WeaponData[] All =
        {
            new WeaponData { Id = 257, Code = "c01w00", Name = "Dagger (broken)",  Dcol1 =   4.920f, CommenuCombatScale = 1.000f,
                             MesEntry = 204, ModDescription = "Broken dagger due to\nhard use." },
            new WeaponData { Id = 258, Code = "c01w01", Name = "Dagger",           Dcol1 =   5.808f, CommenuCombatScale = 1.000f,
                             MesEntry = 205, ModDescription = "Fairly easy to use.\ndefault weapon." },
            new WeaponData { Id = 259, Code = "c01w02", Name = "Baselard",         Dcol1 =   8.083f, CommenuCombatScale = 1.000f,
                             MesEntry = 206, ModDescription = "A big single-edged\nsword with long reach.\nEasy to use." },
            new WeaponData { Id = 260, Code = "c01w03", Name = "Gladius",          Dcol1 =   7.108f, CommenuCombatScale = 1.000f,
                             MesEntry = 207, ModDescription = "Light-wt double-edged\nsword. Fairly high\nattack power." },
            new WeaponData { Id = 261, Code = "c01w04", Name = "Wise Owl Sword",   Dcol1 =   7.998f, CommenuCombatScale = 1.000f,
                             MesEntry = 208, ModDescription = "Wise Owl knows when\nhis favorites are near." },
            new WeaponData { Id = 262, Code = "c01w05", Name = "Crysknife",        Dcol1 =   6.990f, CommenuCombatScale = 1.000f,
                             MesEntry = 209, ModDescription = "Stylet with magical\npower." },
            new WeaponData { Id = 263, Code = "c01w06", Name = "Antique Sword",    Dcol1 =   6.941f, CommenuCombatScale = 1.000f,
                             MesEntry = 210, ModDescription = "Sword of antiquity\nmade a long time ago." },
            new WeaponData { Id = 264, Code = "c01w07", Name = "Buster Sword",     Dcol1 =   8.160f, CommenuCombatScale = 1.000f,
                             MesEntry = 211, ModDescription = "Double-edged powerful\nsword." },
            new WeaponData { Id = 265, Code = "c01w08", Name = "Kitchen Knife",    Dcol1 =   4.187f, CommenuCombatScale = 1.000f,
                             MesEntry = 212, ModDescription = "Low attack power but\ncauses major damage\nto water monsters." },
            new WeaponData { Id = 266, Code = "c01w09", Name = "Tsukikage",        Dcol1 =   9.603f, CommenuCombatScale = 1.000f,
                             MesEntry = 213, ModDescription = "Sword with sacred\npower of moon beam." },
            new WeaponData { Id = 267, Code = "c01w10", Name = "Sun Sword",        Dcol1 =  10.074f, CommenuCombatScale = 1.000f,
                             MesEntry = 214, ModDescription = "Legendary sword with\npower to cut through\ndarkness." },
            new WeaponData { Id = 268, Code = "c01w11", Name = "Serpent Sword",    Dcol1 =   9.012f, CommenuCombatScale = 1.000f,
                             MesEntry = 215, ModDescription = "Sword with absolute\npower over snakes.\nExtremely rare." },
            new WeaponData { Id = 269, Code = "c01w12", Name = "Macho Sword",      Dcol1 =   9.382f, CommenuCombatScale = 1.000f,
                             MesEntry = 216, ModDescription = "Sword with muscle power.\nTrain past any limit." },
            new WeaponData { Id = 270, Code = "c01w13", Name = "Shamshir",         Dcol1 =   7.985f, CommenuCombatScale = 1.000f,
                             MesEntry = 217, ModDescription = "Characterized by its\ncurved edge.Light and\neasy to use." },
            new WeaponData { Id = 271, Code = "c01w14", Name = "Heaven's Cloud",   Dcol1 =   9.205f, CommenuCombatScale = 0.868f,
                             MesEntry = 218, ModDescription = "The storm gathers\nbefore it breaks." }, // shrunk display model
            new WeaponData { Id = 272, Code = "c01w15", Name = "Lamb's Sword",     Dcol1 =  10.015f, CommenuCombatScale = 1.000f,
                             MesEntry = 219, ModDescription = "Timid.Sword like a lamb.\nAppears to be weak but..." }, // combat .mds has no dcol1 bone
            new WeaponData { Id = 273, Code = "c01w16", Name = "Dark Cloud",       Dcol1 =  10.355f, CommenuCombatScale = 1.000f,
                             MesEntry = 220, ModDescription = "Magical Sword with\npower of darkness." },
            new WeaponData { Id = 274, Code = "c01w17", Name = "Brave Ark",        Dcol1 =   8.492f, CommenuCombatScale = 1.000f,
                             MesEntry = 221, ModDescription = "Sealed against\nevery affliction." },
            new WeaponData { Id = 275, Code = "c01w18", Name = "Big Bang",         Dcol1 =  11.378f, CommenuCombatScale = 1.000f,
                             MesEntry = 222, ModDescription = "Star energy is condensed\nand creates sword's\nenergy." },
            new WeaponData { Id = 276, Code = "c01w19", Name = "Atlamillia Sword", Dcol1 =   9.937f, CommenuCombatScale = 0.861f,
                             MesEntry = 223, ModDescription = "Sword that inherited\nAtlamillia,the legendary\ngem." }, // shrunk display model
            new WeaponData { Id = 277, Code = "c01w20", Name = "weapon No.277",    Dcol1 =   null,   CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null }, // no dcol1 frame
            new WeaponData { Id = 278, Code = "c01w21", Name = "Mardan Eins",      Dcol1 =   8.995f, CommenuCombatScale = 1.000f,
                             MesEntry = 453, ModDescription = "Sword dwelled in by\nLucky MardanGarayan." },
            new WeaponData { Id = 279, Code = "c01w22", Name = "Mardan Twei",      Dcol1 =  10.365f, CommenuCombatScale = 1.000f,
                             MesEntry = 454, ModDescription = "Sword dwelled in by\nLucky Mardan Garayan." },
            new WeaponData { Id = 280, Code = "c01w23", Name = "Arise Mardan",     Dcol1 =  11.798f, CommenuCombatScale = 1.000f,
                             MesEntry = 455, ModDescription = "Sword dwelled in by\nLucky MardanGarayan." },
            new WeaponData { Id = 281, Code = "c01w24", Name = "Aga's Sword",      Dcol1 =  10.037f, CommenuCombatScale = 1.000f,
                             MesEntry = 456, ModDescription = "The Hero Aga's will\nguards you." },
            new WeaponData { Id = 282, Code = "c01w25", Name = "Evilcise",         Dcol1 =   9.437f, CommenuCombatScale = 1.000f,
                             MesEntry = 457, ModDescription = "Cursed sword\nharboring jealous spirit." },
            new WeaponData { Id = 283, Code = "c01w26", Name = "Small Sword",      Dcol1 =   6.612f, CommenuCombatScale = 1.000f,
                             MesEntry = 458, ModDescription = "Light and easy to use.\nDraws faster than\nthe eye can see." },
            new WeaponData { Id = 284, Code = "c01w27", Name = "Sand Breaker",     Dcol1 =   9.992f, CommenuCombatScale = 1.000f,
                             MesEntry = 459, ModDescription = "Made of desert sand.\nAbsorbs water." },
            new WeaponData { Id = 285, Code = "c01w28", Name = "Drain Seeker",     Dcol1 =  11.494f, CommenuCombatScale = 1.000f,
                             MesEntry = 460, ModDescription = "Feared magic sword.\nAbsorb health power." },
            new WeaponData { Id = 286, Code = "c01w29", Name = "Chopper",          Dcol1 =   8.378f, CommenuCombatScale = 1.000f,
                             MesEntry = 461, ModDescription = "Light and easy to use\ndagger." },
            new WeaponData { Id = 287, Code = "c01w30", Name = "Choora",           Dcol1 = -11.120f, CommenuCombatScale = 1.000f,
                             MesEntry = 462, ModDescription = "A dagger with pretty\npattern on it." }, // mirrored model (Dcol1 negative)
            new WeaponData { Id = 288, Code = "c01w31", Name = "Claymore",         Dcol1 =  10.384f, CommenuCombatScale = 1.000f,
                             MesEntry = 463, ModDescription = "Big & double-handed.\nHeavy & powerful." },
            new WeaponData { Id = 289, Code = "c01w32", Name = "Maneater",         Dcol1 =   7.760f, CommenuCombatScale = 1.000f,
                             MesEntry = 464, ModDescription = "Fearful sword that\neats human souls." },
            new WeaponData { Id = 290, Code = "c01w33", Name = "Bone Rapier",      Dcol1 =  10.400f, CommenuCombatScale = 1.000f,
                             MesEntry = 465, ModDescription = "Made of bone.\nAbility unknown." },
            new WeaponData { Id = 291, Code = "c01w34", Name = "Sax",              Dcol1 =  10.412f, CommenuCombatScale = 1.000f,
                             MesEntry = 466, ModDescription = "Easy to use\nsingle-handed sword." },
            new WeaponData { Id = 292, Code = "c01w35", Name = "7 Branch Sword",   Dcol1 =  11.191f, CommenuCombatScale = 1.000f,
                             MesEntry = 467, ModDescription = "Ancient sword with\nmysterious power." },
            new WeaponData { Id = 293, Code = "c01w36", Name = "Dusack",           Dcol1 =  10.705f, CommenuCombatScale = 1.000f,
                             MesEntry = 468, ModDescription = "Unique single-hand\nsword. Cuts well." },
            new WeaponData { Id = 294, Code = "c01w37", Name = "Cross Hinder",     Dcol1 =  10.370f, CommenuCombatScale = 1.000f,
                             MesEntry = 469, ModDescription = "Very powerful double\nhand treasure sword." },
            new WeaponData { Id = 295, Code = "c01w38", Name = "7th Heaven",       Dcol1 =  10.232f, CommenuCombatScale = 1.000f,
                             MesEntry = 480, ModDescription = "Beautiful sword.\nTears the heavens." },
            new WeaponData { Id = 296, Code = "c01w39", Name = "Sword Of Zeus",    Dcol1 =  11.581f, CommenuCombatScale = 1.000f,
                             MesEntry = 481, ModDescription = "Zeus' swords with\nunlimited ability." },
            new WeaponData { Id = 297, Code = "c01w40", Name = "Chronicle Sword",  Dcol1 =   9.437f, CommenuCombatScale = 0.662f,
                             MesEntry = 482, ModDescription = "Worlds crumble\nbefore its might." }, // shrunk display model
            new WeaponData { Id = 298, Code = "c01w41", Name = "Chronicle 2",      Dcol1 =   9.437f, CommenuCombatScale = 0.662f,
                             MesEntry = 483, ModDescription = "Proof of subjugation.\nDemon Shaft." }, // shrunk display model
        };

        private static readonly Dictionary<int, WeaponData> ById;

        static ToanWeapons()
        {
            ById = new Dictionary<int, WeaponData>(All.Length);
            foreach (WeaponData w in All) ById[w.Id] = w;
        }

        /// <summary>Look up a weapon's static data by item id.</summary>
        internal static bool TryGetValue(int id, out WeaponData data) => ById.TryGetValue(id, out data);
    }

    /// <summary>Static repository of <see cref="WeaponData"/> for Xiao's weapons (slingshots), keyed by item id.
    /// Dcol1 = commenu-model dcol1 local-Z where the model has one (extracted from data.dat; null = none/no file);
    /// CommenuCombatScale is NOT yet measured for this character (1.0 assumed). Glitched ids have MesEntry = -1.</summary>
    internal static class XiaoWeapons
    {
        internal static readonly WeaponData[] All =
        {
            new WeaponData { Id = 299, Code = "c04w00", Name = "Wooden Slingshot (broken)", Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 224, ModDescription = "Broken wooden slingshot\ndue to hard use." },
            new WeaponData { Id = 300, Code = "c04w01", Name = "Wooden Slingshot",          Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 225, ModDescription = "Slingshot made by\ncarving wood.Default\nweapon." },
            new WeaponData { Id = 301, Code = "c04w02", Name = "Steel Slingshot",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 226, ModDescription = "Slingshot made of\nsteel.Looks heavy." },
            new WeaponData { Id = 302, Code = "c04w03", Name = "Bandit Slingshot",          Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 227, ModDescription = "Sometimes you can steal\nitems from monsters with\nthis." },
            new WeaponData { Id = 303, Code = "c04w04", Name = "Steve",                     Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 228, ModDescription = "Mysterious Talking\nSlingshot.Nobody knows\nits true color." },
            new WeaponData { Id = 304, Code = "c04w05", Name = "Bone Slingshot",            Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 229, ModDescription = "Slingshot made of bone.\nA bit fragile." },
            new WeaponData { Id = 305, Code = "c04w06", Name = "Hardshooter",               Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 230, ModDescription = "Launches powerful shot\nbut a bit fragile." },
            new WeaponData { Id = 306, Code = "c04w07", Name = "Double Impact",             Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 231, ModDescription = "Allows shooting two\nbullets at the same\ntime." },
            new WeaponData { Id = 307, Code = "c04w08", Name = "Dragon's Y",                Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 232, ModDescription = "Slingshot with dragon's\npower." },
            new WeaponData { Id = 308, Code = "c04w09", Name = "Divine Beast Title",        Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 233, ModDescription = "Having this gives you\nthe title of Divine\nBeast." },
            new WeaponData { Id = 309, Code = "c04w10", Name = "Angel Shooter",             Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 234, ModDescription = "Angel's slingshot.Looks\ncute but ultra powerful." },
            new WeaponData { Id = 310, Code = "c04w11", Name = "Flamingo",                  Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 470, ModDescription = "Unique slingshot\nshaped like a bird." },
            new WeaponData { Id = 311, Code = "c04w12", Name = "Matador",                   Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 471, ModDescription = "Slingshot made of ox\nhorn. Very powerful." },
            new WeaponData { Id = 312, Code = "c04w13", Name = "Super Steve",               Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 484, ModDescription = "Powerful mysterious\nSlingshot, Steve." },
            new WeaponData { Id = 313, Code = "c04w14", Name = "Angel Gear",                Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 485, ModDescription = "Blesses allies,\nslowly healing them." },
        };

        private static readonly Dictionary<int, WeaponData> ById;

        static XiaoWeapons()
        {
            ById = new Dictionary<int, WeaponData>(All.Length);
            foreach (WeaponData w in All) ById[w.Id] = w;
        }

        /// <summary>Look up a weapon's static data by item id.</summary>
        internal static bool TryGetValue(int id, out WeaponData data) => ById.TryGetValue(id, out data);
    }

    /// <summary>Static repository of <see cref="WeaponData"/> for Goro's weapons (hammers/axes), keyed by item id.
    /// Dcol1 = commenu-model dcol1 local-Z where the model has one (extracted from data.dat; null = none/no file);
    /// CommenuCombatScale is NOT yet measured for this character (1.0 assumed). Glitched ids have MesEntry = -1.</summary>
    internal static class GoroWeapons
    {
        internal static readonly WeaponData[] All =
        {
            new WeaponData { Id = 314, Code = "c06w00", Name = "Mallet (broken)",           Dcol1 =  -8.081f, CommenuCombatScale = 1.000f,
                             MesEntry = 236, ModDescription = "Broken Mallet due to\nhard use." },
            new WeaponData { Id = 315, Code = "c06w01", Name = "Mallet",                    Dcol1 =   8.471f, CommenuCombatScale = 1.000f,
                             MesEntry = 237, ModDescription = "Light-weight and easy\nto use.Default weapon." },
            new WeaponData { Id = 316, Code = "c06w02", Name = "Steel Hammer",              Dcol1 =  10.453f, CommenuCombatScale = 1.000f,
                             MesEntry = 238, ModDescription = "Hammer made of steel.\nA little heavy but\npowerful." },
            new WeaponData { Id = 317, Code = "c06w03", Name = "Magical Hammer",            Dcol1 =   7.775f, CommenuCombatScale = 1.000f,
                             MesEntry = 239, ModDescription = "Hammer with magical\npower.Makes attribute\nattack effective." },
            new WeaponData { Id = 318, Code = "c06w04", Name = "Battle Ax",                 Dcol1 =  10.984f, CommenuCombatScale = 1.000f,
                             MesEntry = 240, ModDescription = "Cut any enemy into half\nwith single stroke.\nA Hunter's must." },
            new WeaponData { Id = 319, Code = "c06w05", Name = "Turtle Shell",              Dcol1 =  12.024f, CommenuCombatScale = 1.000f,
                             MesEntry = 241, ModDescription = "Hammer made of turtle\nshell." },
            new WeaponData { Id = 320, Code = "c06w06", Name = "Big Bucks Hammer",          Dcol1 =   9.126f, CommenuCombatScale = 1.000f,
                             MesEntry = 242, ModDescription = "Magical hammer.Stroking\nit makes you rich." },
            new WeaponData { Id = 321, Code = "c06w07", Name = "Frozen Tuna",               Dcol1 =  10.847f, CommenuCombatScale = 1.000f,
                             MesEntry = 243, ModDescription = "Feeds you as it breaks.\nPlenty of leftovers." },
            new WeaponData { Id = 322, Code = "c06w08", Name = "Gaia Hammer",               Dcol1 =   2.930f, CommenuCombatScale = 1.000f,
                             MesEntry = 244, ModDescription = "Strongest hammer with\nthe power of Gaia." },
            new WeaponData { Id = 323, Code = "c06w09", Name = "Last Judgement",            Dcol1 =   9.799f, CommenuCombatScale = 1.000f,
                             MesEntry = 245, ModDescription = "Helluva strong hammer\nloved by the gate keeper\nof hell." },
            new WeaponData { Id = 324, Code = "c06w10", Name = "Tall Hammer",               Dcol1 =   9.364f, CommenuCombatScale = 1.000f,
                             MesEntry = 246, ModDescription = "Its curse shrinks\nenemies with every\nblow." },
            new WeaponData { Id = 325, Code = "c06w11", Name = "Satan's Ax",                Dcol1 =  11.272f, CommenuCombatScale = 1.000f,
                             MesEntry = 247, ModDescription = "Ax used by dreadful\nSatan of hell." },
            new WeaponData { Id = 326, Code = "c06w12", Name = "glitched weapon",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null },
            new WeaponData { Id = 327, Code = "c06w13", Name = "Plate Hammer",              Dcol1 =   9.586f, CommenuCombatScale = 1.000f,
                             MesEntry = 472, ModDescription = "Hammer made of steel\nplate. Light." },
            new WeaponData { Id = 328, Code = "c06w14", Name = "Trial Hammer",              Dcol1 =   9.327f, CommenuCombatScale = 1.000f,
                             MesEntry = 473, ModDescription = "Blood & sweat hammer\nw/warriors' soul." },
            new WeaponData { Id = 329, Code = "c06w15", Name = "Inferno",                   Dcol1 =   9.799f, CommenuCombatScale = 1.000f,
                             MesEntry = 486, ModDescription = "Great Hammer with\nHell's fire." },
            new WeaponData { Id = 330, Code = "c06w16", Name = "glitched weapon",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null },
        };

        private static readonly Dictionary<int, WeaponData> ById;

        static GoroWeapons()
        {
            ById = new Dictionary<int, WeaponData>(All.Length);
            foreach (WeaponData w in All) ById[w.Id] = w;
        }

        /// <summary>Look up a weapon's static data by item id.</summary>
        internal static bool TryGetValue(int id, out WeaponData data) => ById.TryGetValue(id, out data);
    }

    /// <summary>Static repository of <see cref="WeaponData"/> for Ruby's weapons (rings/armlets), keyed by item id.
    /// Dcol1 = commenu-model dcol1 local-Z where the model has one (extracted from data.dat; null = none/no file);
    /// CommenuCombatScale is NOT yet measured for this character (1.0 assumed). Glitched ids have MesEntry = -1.</summary>
    internal static class RubyWeapons
    {
        internal static readonly WeaponData[] All =
        {
            new WeaponData { Id = 331, Code = "c05w00", Name = "Gold Ring (broken)",        Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 249, ModDescription = "Broken ring due to\nhard use." },
            new WeaponData { Id = 332, Code = "c05w01", Name = "Gold Ring",                 Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 250, ModDescription = "Expensive golden armlet.\nDefault weapon." },
            new WeaponData { Id = 333, Code = "c05w02", Name = "Bandit's Ring",             Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 251, ModDescription = "Nice armlet that steals\nitems from enemy during\nattack." },
            new WeaponData { Id = 334, Code = "c05w03", Name = "Crystal Ring",              Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 252, ModDescription = "Expensive ring made of\nprecious crystal." },
            new WeaponData { Id = 335, Code = "c05w04", Name = "Platinum Ring",             Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 253, ModDescription = "Durable armlet with\npretty strong magical\npower." },
            new WeaponData { Id = 336, Code = "c05w05", Name = "Goddess Ring",              Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 254, ModDescription = "Armlet blessed by\ngoddess." },
            new WeaponData { Id = 337, Code = "c05w06", Name = "Fairy's Ring",              Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 255, ModDescription = "Legendary armlet made\nby fairies." },
            new WeaponData { Id = 338, Code = "c05w07", Name = "Destruction Ring",          Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 256, ModDescription = "Armlet of darkness with\npower that can destroy\neverything." },
            new WeaponData { Id = 339, Code = "c05w08", Name = "Satan's Ring",              Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 257, ModDescription = "Ring with fearful spell\nmade by Satan." },
            new WeaponData { Id = 340, Code = "c05w09", Name = "Athena's Armlet",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 258, ModDescription = "Armlet with sacred\npower of goddess Athena." },
            new WeaponData { Id = 341, Code = "c05w10", Name = "Mobius Ring",               Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 259, ModDescription = "The longer the charge,\nthe greater the\ndamage." },
            new WeaponData { Id = 342, Code = "c05w11", Name = "Glitched Weapon",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null },
            new WeaponData { Id = 343, Code = "c05w12", Name = "Pocklekul",                 Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 474, ModDescription = "Bracelet that honors\ndwarf of forest." },
            new WeaponData { Id = 344, Code = "c05w13", Name = "Thorn Armlet",              Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 475, ModDescription = "Bracelet made of\nwoven thorn." },
            new WeaponData { Id = 345, Code = "c05w14", Name = "Secret Armlet",             Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 487, ModDescription = "Turns magic circles\nin your favor." },
            new WeaponData { Id = 346, Code = "c05w15", Name = "glitched weapon",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null },
        };

        private static readonly Dictionary<int, WeaponData> ById;

        static RubyWeapons()
        {
            ById = new Dictionary<int, WeaponData>(All.Length);
            foreach (WeaponData w in All) ById[w.Id] = w;
        }

        /// <summary>Look up a weapon's static data by item id.</summary>
        internal static bool TryGetValue(int id, out WeaponData data) => ById.TryGetValue(id, out data);
    }

    /// <summary>Static repository of <see cref="WeaponData"/> for Ungaga's weapons (spears/poles), keyed by item id.
    /// Dcol1 = commenu-model dcol1 local-Z where the model has one (extracted from data.dat; null = none/no file);
    /// CommenuCombatScale is NOT yet measured for this character (1.0 assumed). Glitched ids have MesEntry = -1.</summary>
    internal static class UngagaWeapons
    {
        internal static readonly WeaponData[] All =
        {
            new WeaponData { Id = 347, Code = "c10w00", Name = "Fighting Stick (broken)",   Dcol1 =   2.668f, CommenuCombatScale = 1.000f,
                             MesEntry = 261, ModDescription = "Broken Fighting Stick\ndue to hard use." },
            new WeaponData { Id = 348, Code = "c10w01", Name = "Fighting Stick",            Dcol1 =   2.213f, CommenuCombatScale = 1.000f,
                             MesEntry = 262, ModDescription = "A stick used in combat\nby desert warriors.\nDefault weapon." },
            new WeaponData { Id = 349, Code = "c10w02", Name = "Javelin",                   Dcol1 =   7.688f, CommenuCombatScale = 1.000f,
                             MesEntry = 263, ModDescription = "Light-weight and easy\nto use lance." },
            new WeaponData { Id = 350, Code = "c10w03", Name = "Halberd",                   Dcol1 =   5.626f, CommenuCombatScale = 1.000f,
                             MesEntry = 264, ModDescription = "Powerful lance with\naxe blade." },
            new WeaponData { Id = 351, Code = "c10w04", Name = "DeSanga",                   Dcol1 =   7.465f, CommenuCombatScale = 1.000f,
                             MesEntry = 265, ModDescription = "Light-weight and easy\nto use forked lance." },
            new WeaponData { Id = 352, Code = "c10w05", Name = "Scorpion",                  Dcol1 =   8.128f, CommenuCombatScale = 1.000f,
                             MesEntry = 266, ModDescription = "Lance with poisoned\nblade.Puts enemy in\npoisoned state." },
            new WeaponData { Id = 353, Code = "c10w06", Name = "Partisan",                  Dcol1 =   9.121f, CommenuCombatScale = 1.000f,
                             MesEntry = 267, ModDescription = "Lance with sharp and\nthick blade." },
            new WeaponData { Id = 354, Code = "c10w07", Name = "Mirage",                    Dcol1 =   7.806f, CommenuCombatScale = 1.000f,
                             MesEntry = 268, ModDescription = "Legendary lance made\nby ancient desert\npeople." },
            new WeaponData { Id = 355, Code = "c10w08", Name = "Terra Sword",               Dcol1 =   7.434f, CommenuCombatScale = 1.000f,
                             MesEntry = 269, ModDescription = "Strongest lance made\nby fairies of Terra." },
            new WeaponData { Id = 356, Code = "c10w09", Name = "Hercules' Wrath",           Dcol1 =   6.306f, CommenuCombatScale = 1.000f,
                             MesEntry = 270, ModDescription = "Legendary lance with\ndreadful destructive\npower." },
            new WeaponData { Id = 357, Code = "c10w10", Name = "Babel's Spear",             Dcol1 =   6.161f, CommenuCombatScale = 1.000f,
                             MesEntry = 271, ModDescription = "Strikes may stop\ntime for all foes." },
            new WeaponData { Id = 358, Code = "c10w11", Name = "glitched weapon",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null },
            new WeaponData { Id = 359, Code = "c10w12", Name = "5 Foot Nail",               Dcol1 =   0.089f, CommenuCombatScale = 1.000f,
                             MesEntry = 476, ModDescription = "Cursed lance. A stab\nmay finish off enemy." },
            new WeaponData { Id = 360, Code = "c10w13", Name = "Cactus",                    Dcol1 =   5.070f, CommenuCombatScale = 1.000f,
                             MesEntry = 477, ModDescription = "An oasis\nin your hands." },
            new WeaponData { Id = 361, Code = "c10w14", Name = "glitched weapon",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null },
            new WeaponData { Id = 362, Code = "c10w15", Name = "Glitched Weapon",           Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = -1, ModDescription = null },
        };

        private static readonly Dictionary<int, WeaponData> ById;

        static UngagaWeapons()
        {
            ById = new Dictionary<int, WeaponData>(All.Length);
            foreach (WeaponData w in All) ById[w.Id] = w;
        }

        /// <summary>Look up a weapon's static data by item id.</summary>
        internal static bool TryGetValue(int id, out WeaponData data) => ById.TryGetValue(id, out data);
    }

    /// <summary>Static repository of <see cref="WeaponData"/> for Osmond's weapons (guns), keyed by item id.
    /// Dcol1 = commenu-model dcol1 local-Z where the model has one (extracted from data.dat; null = none/no file);
    /// CommenuCombatScale is NOT yet measured for this character (1.0 assumed). Glitched ids have MesEntry = -1.</summary>
    internal static class OsmondWeapons
    {
        internal static readonly WeaponData[] All =
        {
            new WeaponData { Id = 363, Code = "c18w00", Name = "Machine Gun (broken)",      Dcol1 =   1.856f, CommenuCombatScale = 1.000f,
                             MesEntry = 273, ModDescription = "Broken machine gun\ndue to hard use." },
            new WeaponData { Id = 364, Code = "c18w01", Name = "Machine Gun",               Dcol1 =   0.789f, CommenuCombatScale = 1.000f,
                             MesEntry = 274, ModDescription = "A powerful blazing gun.\nDefault weapon." },
            new WeaponData { Id = 365, Code = "c18w02", Name = "Jackal",                    Dcol1 = null   , CommenuCombatScale = 1.000f,
                             MesEntry = 275, ModDescription = "Upgraded machine gun." },
            new WeaponData { Id = 366, Code = "c18w03", Name = "Launcher",                  Dcol1 =   1.413f, CommenuCombatScale = 1.000f,
                             MesEntry = 276, ModDescription = "Max. of four missiles\ncan be consecutively\nlaunched." },
            new WeaponData { Id = 367, Code = "c18w04", Name = "Launcher V2",               Dcol1 =   1.553f, CommenuCombatScale = 1.000f,
                             MesEntry = 277, ModDescription = "Max. of 8 missiles can be\nconsecutively launched." },
            new WeaponData { Id = 368, Code = "c18w05", Name = "Blessing Gun",              Dcol1 =   1.590f, CommenuCombatScale = 1.000f,
                             MesEntry = 278, ModDescription = "Device emits magical\nattribute.Attacks\nvarious ways." },
            new WeaponData { Id = 369, Code = "c18w06", Name = "Skunk",                     Dcol1 =   2.170f, CommenuCombatScale = 1.000f,
                             MesEntry = 279, ModDescription = "Upgraded blessing gun.\nSpell emitting device." },
            new WeaponData { Id = 370, Code = "c18w07", Name = "G Crusher",                 Dcol1 =   1.840f, CommenuCombatScale = 1.000f,
                             MesEntry = 280, ModDescription = "Emits powerful shot.\nGreat destructive power." },
            new WeaponData { Id = 371, Code = "c18w08", Name = "Hexa Blaster",              Dcol1 =   3.125f, CommenuCombatScale = 1.000f,
                             MesEntry = 281, ModDescription = "Powerful beam cannon\nthat emits magical\nenergy." },
            new WeaponData { Id = 372, Code = "c18w09", Name = "Star Breaker",              Dcol1 =   2.064f, CommenuCombatScale = 1.000f,
                             MesEntry = 282, ModDescription = "Kills may break off\nan empty SynthSphere." },
            new WeaponData { Id = 373, Code = "c18w10", Name = "Supernova",                 Dcol1 =   2.018f, CommenuCombatScale = 1.000f,
                             MesEntry = 283, ModDescription = "Each hit may unleash\na random affliction." },
            new WeaponData { Id = 374, Code = "c18w11", Name = "Snail",                     Dcol1 =   1.840f, CommenuCombatScale = 1.000f,
                             MesEntry = 478, ModDescription = "Leaves foes stuck\nin its trail." },
            new WeaponData { Id = 375, Code = "c18w12", Name = "Swallow",                   Dcol1 =  -0.284f, CommenuCombatScale = 1.000f,
                             MesEntry = 479, ModDescription = "Enhanced machine gun.\nLight & easy to use." },
        };

        private static readonly Dictionary<int, WeaponData> ById;

        static OsmondWeapons()
        {
            ById = new Dictionary<int, WeaponData>(All.Length);
            foreach (WeaponData w in All) ById[w.Id] = w;
        }

        /// <summary>Look up a weapon's static data by item id.</summary>
        internal static bool TryGetValue(int id, out WeaponData data) => ById.TryGetValue(id, out data);
    }
}
