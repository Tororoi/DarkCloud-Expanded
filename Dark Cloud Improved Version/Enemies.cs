using System;
using System.Collections.Generic;

namespace Dark_Cloud_Improved_Version
{
    class Enemies
    {
        public const int offset = 0x190;        //Offset between floor enemies
        public const int tableOffset = 0x9C;    //Offset between table enemies

        //Enemy ID values
        public const int masterjacket = 1;
        public const int skeletonsoldier = 3;
        public const int statue = 5;
        public const int dasher = 6;
        public const int werewolf = 7;
        public const int flifli = 8;
        public const int hornet = 9;
        public const int halloween = 10;
        public const int canibalplant = 11;
        public const int earthdigger = 12;
        public const int sunday = 14;
        public const int monday = 15;
        public const int tuesday = 16;
        public const int wednesday = 17;
        public const int friday = 18;
        public const int saturday = 19;
        public const int witchhellza = 21;
        public const int witchillza = 22;
        public const int gunny = 23;
        public const int gyon = 24;
        public const int pirateschariot = 25;
        public const int auntiemedu = 26;
        public const int captain = 27;
        public const int corcea = 28;
        public const int golem = 30;
        public const int mrblare = 31;
        public const int dune = 32;
        public const int titan = 33;
        public const int kingmimicdbc = 34; //Diving Beast Cave
        public const int mimicdbc = 35;     //Diving Beast Cave
        public const int kingmimicsmt = 36; //Sun & Moon Temple
        public const int mimicsmt = 37;     //Sun & Moon Temple
        public const int kingmimicms = 38;  //Moon Sea
        public const int mimicms = 39;      //Moon Sea
        public const int arthur = 40;
        public const int ghost = 42;
        public const int alexander = 43;
        public const int heart = 44;
        public const int club = 45;
        public const int diamond = 46;
        public const int spade = 47;
        public const int joker = 48;
        public const int bomberhead = 49;
        public const int mummy = 50;
        public const int lich = 51;
        public const int cursedancer = 52;
        public const int killersnake = 54;
        public const int livingarmor = 55;
        public const int whitefang = 56;
        public const int moonbug = 57;
        public const int phantom = 58;
        public const int dragon = 59;
        public const int cavebat = 60;
        public const int evilbat = 61;
        public const int hellpockle = 62;
        public const int rashdasher = 63;
        public const int steelgiant = 64;
        public const int blizzard = 65;
        public const int moondigger = 66;
        public const int darkflower = 67;
        public const int cursedrose = 68;
        public const int billy = 69;
        public const int vulcan = 70;
        public const int crabbyhermit = 71;
        public const int spacegyon = 72;
        public const int bluedragon = 73;
        public const int blackdragon = 74;
        public const int maskofprajna = 75;
        public const int crescentbaron = 76;
        public const int rockanoff = 77;
        public const int kingmimicwo = 78;  //Wise Owl
        public const int mimicwo = 79;      //Wise Owl
        public const int kingmimicsw = 80;  //Shipwreck
        public const int mimicsw = 81;      //Shipwreck
        public const int kingmimicgot = 82; //Gallery of Time
        public const int mimicgot = 83;     //Gallery of Time
        public const int icearrow = 84;
        public const int sam = 85;
        public const int gol = 90;
        public const int sil = 91;
        public const int dran = 112;
        public const int icequeen = 113;
        public const int masterutan = 114;
        public const int kingscurse = 115;
        public const int minotaurjoe = 116;
        public const int darkgenie = 117;
        public const int righthand = 119;
        public const int lefthand = 120;
        public const int blackknight = 221;
        public const int yammich = 301;
        public const int statuedog = 303;
        public const int opar = 304;
        public const int haleyholey = 305;
        public const int kingprickly = 306;
        public const int nikapous = 308;
        public const int mimicds = 309;     //Demon Shaft
        public const int kingmimicds = 310; //Demon Shaft
        public const int gemronfire = 311;
        public const int gemronice = 312;
        public const int gemronthunder = 313;
        public const int gemronwind = 314;
        public const int gemronholy = 315;
        public const int bishopq = 316;
        public const int gacious = 317;
        public const int silvergear = 318;
        public const int hornhead = 319;

        /// <summary>
        /// Returns a list of enemy ids that can drop dungeon keys.
        /// </summary>
        public static Dictionary<ushort, string> GetNormalEnemies()
        {
            return EnemyList.enemiesNormal;
        }

        /// <summary>
        /// Returns a list of enemy ids that cannot drop the dungeon keys.
        /// </summary>
        public static Dictionary<ushort, string> GetFlyingEnemies()
        {
            return EnemyList.enemiesFlying;
        }

        /// <summary>
        /// Returns a list of enemy ids that are not present in the original Japanese version of the game.
        /// </summary>
        public static Dictionary<ushort, string> GetOverseasEnemies()
        {
            return EnemyList.enemiesOverseas;
        }

        /// <summary>
        /// Returns the display name for an enemy type ID, searching all lists.
        /// Returns "Unknown" if not found.
        /// </summary>
        public static string GetEnemyName(ushort typeId)
        {
            if (EnemyList.enemiesNormal.TryGetValue(typeId, out string name))   return name;
            if (EnemyList.enemiesFlying.TryGetValue(typeId, out name))          return name;
            if (EnemyList.enemiesBoss.TryGetValue(typeId, out name))            return name;
            if (EnemyList.enemiesOverseas.TryGetValue(typeId, out name))        return name;
            return "Unknown";
        }

        /// <summary>
        /// Returns a list of boss enemy ids.
        /// </summary>
        public static Dictionary<ushort, string> GetBossEnemies()
        {
            return EnemyList.enemiesBoss;
        }

        /// <summary>
        /// Returns the id of the given enemy number on the dungeon floor.
        /// </summary>
        /// <param name="enemyFloorNum">The enemy number (0-15).</param>
        public static ushort GetFloorEnemyId(int enemyFloorNum)
        {
            if(enemyFloorNum < 0 || enemyFloorNum > 15)
            {
                Console.WriteLine(ReusableFunctions.GetDateTimeForLog() + "GetFloorEnemyId() input fell outside of range: " + enemyFloorNum);
                return ushort.MinValue;
            }

            return Memory.ReadUShort(Enemy0.enemyTypeId + (offset * enemyFloorNum));
        }

        /// <summary>
        /// Returns a list of all the enemy ids currently on the dungeon floor.
        /// </summary>
        public static List<ushort> GetFloorEnemiesIds()
        {
            List<ushort> Ids = new List<ushort>();

            for (int i = 0; i < 15; i++)
            {
                Ids.Add(Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i)));
            }

            return Ids;
        }

        /// <summary>
        /// Returns true if the given enemy number on the dungeon floor is holding the dungeon key.
        /// </summary>
        /// <param name="enemyNumber">The enemy spawn number.</param>
        /// <param name="dungeon">The dungeon the enemy belongs to.</param>
        /// <returns></returns>
        public static bool EnemyHasKey(int enemyNumber, byte dungeon)
        {
            return Dungeon.GetDungeonGateKey(dungeon).Contains(Memory.ReadByte(Enemy0.forceItemDrop + (offset * enemyNumber)));
        }

        internal class EnemyList
        {
            public static Dictionary<ushort, string> enemiesNormal = new Dictionary<ushort, string>()
            {
                { 1, "Master Jacket" },
                { 3, "Skeleton Soldier" },
                { 5, "Statue" },
                { 6, "Dasher" },
                { 7, "Werewolf" },
                { 8, "Fli Fli" },
                { 10, "Halloween" },
                { 11, "Cannibal Plant" },
                { 12, "Earth Digger" },
                { 14, "Sunday" },
                { 15, "Monday" },
                { 16, "Tuesday" },
                { 17, "Wednesday" },
                { 18, "Thursday" },
                { 19, "Friday" },
                { 20, "Saturday" },
                { 23, "Gunny" },
                { 24, "Gyon" },
                { 25, "Pirate's Chariot" }, //Longest name
                { 26, "Auntie Medu" },
                { 27, "Captain" },
                { 28, "Corcea" },
                { 30, "Golem" },
                { 31, "Mr. Blare" },
                { 32, "Dune" },
                { 33, "Titan" },
                { 34, "King Mimic (Divine Beast Cave)" },
                { 35, "Mimic (Divine Beast Cave)" },
                { 36, "King Mimic (Sun & Moon Temple)" },
                { 37, "Mimic (Sun & Moon Temple)" },
                { 38, "King Mimic (Moon Sea)" },
                { 39, "Mimic (Moon Sea)" },
                { 40, "Arthur" },
                { 43, "Alexander" },
                { 44, "Heart" },
                { 45, "Club" },
                { 46, "Diamond" },
                { 47, "Spade" },
                { 48, "Joker" },
                { 49, "Bomber Head" },
                { 50, "Mummy" },
                { 52, "Curse Dancer" },
                { 54, "Killer Snake" },
                { 55, "Living Armor" },
                { 56, "White Fang" },
                { 57, "Moon Bug" },
                { 59, "Dragon" },
                { 62, "Hell Pockle" },
                { 63, "Rash Dasher" },
                { 64, "Steel Giant" },
                { 65, "Blizzard" },
                { 66, "Moon Digger" },
                { 67, "Dark Flower" },
                { 68, "Cursed Rose" },
                { 69, "Billy" },
                { 70, "Vulcan" },
                { 71, "Crabby Hermit" },
                { 72, "Space Gyon" },
                { 73, "Blue Dragon" },
                { 74, "Black Dragon" },
                { 75, "Mask of Prajna" },
                { 76, "Crescent Baron" },
                { 77, "Rockanoff" },
                { 78, "King Mimic (Wise Owl)" },
                { 79, "Mimic (Wise Owl)" },
                { 80, "King Mimic (Shipwreck)" },
                { 81, "Mimic (Shipwreck)" },
                { 82, "King Mimic (Gallery of Time)" },
                { 83, "Mimic (Gallery of Time)" },
                { 85, "Sam" },
                { 301, "Yammich" },
                { 303, "Statue Dog" },
                { 304, "Opar" },
                { 305, "Haley Holey" },
                { 306, "King Prickly" },
                { 308, "Nikapous" },
                { 309, "Mimic (Demon Shaft)" },
                { 310, "King Mimic (Demon Shaft)" },
                { 311, "Gemron (Fire)" },
                { 312, "Gemron (Ice)" },
                { 313, "Gemron (Thunder)" },
                { 314, "Gemron (Wind)" },
                { 315, "Gemron (Holy)" },
                { 316, "Bishop Q" },
                { 317, "Gacious" },
                { 318, "Silver Gear" },
                { 319, "Horn Head" }
            };

            internal static Dictionary<ushort, string> enemiesFlying = new Dictionary<ushort, string>()
            {
                { 9, "Hornet" },
                { 21, "Witch Hellza" },
                { 22, "Witch Illza" },
                { 42, "Ghost" },
                { 51, "Lich" },
                { 58, "Phantom" },
                { 60, "Cave Bat" },
                { 61, "Evil Bat" },
                { 90, "Gol" },  //non-flying, but cannot drop an item
                { 91, "Sil" },  //non-flying, but cannot drop an item
            };

            internal static Dictionary<ushort, string> enemiesOverseas = new Dictionary<ushort, string>()
            {
                { 301, "Yammich" },
                { 303, "Statue Dog" },
                { 304, "Opar" },
                { 305, "Haley Holey" },
                { 306, "King Prickly" },
                { 308, "Nikapous" },
                { 309, "Mimic (Demon Shaft)" },
                { 310, "King Mimic (Demon Shaft)" },
                { 311, "Gemron (Fire)" },
                { 312, "Gemron (Ice)" },
                { 313, "Gemron (Thunder)" },
                { 314, "Gemron (Wind)" },
                { 315, "Gemron (Holy)" },
                { 316, "Bishop Q" },
                { 317, "Gacious" },
                { 318, "Silver Gear" },
                { 319, "Horn Head" },
            };

            internal static Dictionary<ushort, string> enemiesBoss = new Dictionary<ushort, string>()
            {
                { 84, "Ice Arrow" },
                { 112, "Dran" },
                { 113, "Ice Queen" },
                { 114, "Master Utan" },
                { 115, "King's Curse" },
                { 116, "Minotaur Joe" },
                { 117, "Dark Genie" },
                { 119, "Right Hand" },
                { 120, "Left Hand" },
                { 121, "Wine Keg" },
                { 221, "Black Knight" },
            };
        }

        internal class Enemy0
        {
            public const int visible = 0x21E16BA0;
            public const int freezeTimer = 0x21E16BA8;
            public const int poisonPeriod = 0x21E16BAC;
            public const int staminaTimer = 0x21E16BB0;
            public const int gooeyState = 0x21E16BB4;
            public const int maxHp = 0x21E16BC0;
            public const int hp = 0x21E16BC4;
            public const int drop = 0x21E16C40;
            public const int enemyTypeId = 0x21E16BE2;
            public const int minGoldDrop = 0x21E16BD4;      //Minimum value gold can drop
            public const int dropChance = 0x21E16BD8;       // 0 = 0% | 100 = 100%
            public const int forceItemDrop = 0x21E16C40;    //Default value is 65535 ...
                                                            //Turns into an item ID value once an item is dropped ...
                                                            //If value is changed before killed, it will drop that item, be it by weapon or throw kill
            public const int abs = 0x21E16C50;
            public const int stealItemId = 0x21E16C78;
            public const int itemResistance = 0x21E16C7C;   //0 = Immune | 100 = 100%
            public const int itemDropId = 0x21E16FA4;       //The item dropped by weapon kill
            public const int renderStatus = 0x21E16BA0;     //Determines the enemy status
                                                            // -1 = Not spawned
                                                            //  1 = Spawned but not rendered
                                                            //  2 = Spawned and being rendered

            public const int distanceToPlayer = 0x21E16BB8;

            // Elemental resistance packs — each int holds two ushorts (low, high)
            // Order: [Type, FireRes] / [IceRes, ThunderRes] / [WindRes, HolyRes]
            // Fire confirmed: high ushort of resistancePack1.
            // Ice confirmed: low ushort of resistancePack2.
            // Thunder confirmed: high ushort of resistancePack2.
            // Use EnemyResistances.Read/Write helpers for packed access.
            public const int resistancePack1 = 0x21E16BC8;
            public const int resistancePack2 = 0x21E16BCC;
            public const int resistancePack3 = 0x21E16BD0;

            // +0x044/+0x048: unknown floats — NOT attack power.
            // Inflating these causes enemies to refuse approach (stand-off distance?).
            public const int unk044 = 0x21E16BE4;
            public const int unk048 = 0x21E16BE8;

            // Speed multipliers (exact roles — movement vs. attack animation — TBD)
            public const int reticleWidth = 0x21E16CB0;
            public const int speedB = 0x21E16CB4;

            public const int flashColorRed = 0x21E16CD0;
            public const int flashColorGreen = 0x21E16CD4;
            public const int flashColorBlue = 0x21E16CD8;
            public const int flashActivation = 0x21E16D04;
            public const int flashDuration = 0x21E16D08;

            public const int locationCoordinateX = 0x21E16CA0; //I made it XZY order to match the 2D map-layout lines
            public const int locationCoordinateZ = 0x21E16CA4;
            public const int locationCoordinateY = 0x21E16CA8;

            //Add this to here later 21E17D74 (enemy 12 render distance default 300 float)
        }

        internal class Enemy1
        {
            const byte EnemyMultiplier = 1;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy2
        {
            const byte EnemyMultiplier = 2;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy3
        {
            const byte EnemyMultiplier = 3;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy4
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 4;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy5
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 5;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy6
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 6;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy7
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 7;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy8
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 8;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy9
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 9;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy10
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 10;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy11
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 11;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy12
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 12;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy13
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 13;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy14
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 14;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        internal class Enemy15
        {
            const int offset = 0x190;
            const byte EnemyMultiplier = 15;

            public const int visible = Enemy0.visible + (offset * EnemyMultiplier);
            public const int freezeTimer = Enemy0.freezeTimer + (offset * EnemyMultiplier);
            public const int poisonPeriod = Enemy0.poisonPeriod + (offset * EnemyMultiplier);
            public const int staminaTimer = Enemy0.staminaTimer + (offset * EnemyMultiplier);
            public const int gooeyState = Enemy0.gooeyState + (offset * EnemyMultiplier);
            public const int maxHp = Enemy0.maxHp + (offset * EnemyMultiplier);
            public const int hp = Enemy0.hp + (offset * EnemyMultiplier);
            public const int drop = Enemy0.drop + (offset * EnemyMultiplier);
            public const int enemyTypeId = Enemy0.enemyTypeId + (offset * EnemyMultiplier);
            public const int minGoldDrop = Enemy0.minGoldDrop + (offset * EnemyMultiplier);
            public const int dropChance = Enemy0.dropChance + (offset * EnemyMultiplier);
            public const int forceItemDrop = Enemy0.forceItemDrop + (offset * EnemyMultiplier);
            public const int abs = Enemy0.abs + (offset * EnemyMultiplier);
            public const int stealItemId = Enemy0.stealItemId + (offset * EnemyMultiplier);
            public const int itemResistance = Enemy0.itemResistance + (offset * EnemyMultiplier);
            public const int itemDropId = Enemy0.itemDropId + (offset * EnemyMultiplier);
            public const int renderStatus = Enemy0.renderStatus + (offset * EnemyMultiplier);
            public const int distanceToPlayer = Enemy0.distanceToPlayer + (offset * EnemyMultiplier);
            public const int flashColorRed = Enemy0.flashColorRed + (offset * EnemyMultiplier);
            public const int flashColorGreen = Enemy0.flashColorGreen + (offset * EnemyMultiplier);
            public const int flashColorBlue = Enemy0.flashColorBlue + (offset * EnemyMultiplier);
            public const int flashActivation = Enemy0.flashActivation + (offset * EnemyMultiplier);
            public const int flashDuration = Enemy0.flashDuration + (offset * EnemyMultiplier);
            public const int locationCoordinateX = Enemy0.locationCoordinateX + (offset * EnemyMultiplier);
            public const int locationCoordinateZ = Enemy0.locationCoordinateZ + (offset * EnemyMultiplier);
            public const int locationCoordinateY = Enemy0.locationCoordinateY + (offset * EnemyMultiplier);
        }

        // ── Research tools ──────────────────────────────────────────────────────
        private static readonly Dictionary<int, int[]> _enemySlotSnapshots = new Dictionary<int, int[]>();

        internal static void DumpEnemySlot(int slotIndex)
        {
            int slotBase = Enemy0.visible + (offset * slotIndex);
            ushort nameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * slotIndex));
            string name = GetEnemyName(nameId);
            bool isMiniboss = MiniBoss.miniBossEnemyNumbers.Contains(slotIndex);
            string tag = isMiniboss ? " [MINIBOSS - DATA INVALID FOR DATABASE]" : "";
            Console.WriteLine($"[EnemyDump] slot={slotIndex} {name} (id={nameId}) base=0x{slotBase:X8}{tag}");
            if (isMiniboss) return;
            for (int off = 0; off < 0x190; off += 4)
            {
                int raw = Memory.ReadInt(slotBase + off);
                float asFloat = Memory.ReadFloat(slotBase + off);
                Console.WriteLine($"[EnemyDump] slot={slotIndex} +0x{off:X3}  raw=0x{raw:X8}  int={raw,-12}  float={asFloat:F4}");
            }
        }

        internal static void DumpAllActiveEnemySlots()
        {
            for (int i = 0; i < 16; i++)
            {
                int status = Memory.ReadInt(Enemy0.renderStatus + (offset * i));
                if (status != -1 && status != 0)
                    DumpEnemySlot(i);
            }
        }

        /// <summary>
        /// Logs a concise summary of all known database fields for each active non-miniboss slot.
        /// Use this instead of DumpAllActiveEnemySlots for ongoing database population.
        /// </summary>
        internal static void LogEnemySpawns()
        {
            byte dungeon = Memory.ReadByte(Addresses.checkDungeon);
            byte floor   = Memory.ReadByte(Addresses.checkFloor);
            string dungeonName = DungeonDatabase.TryGetValue(dungeon, out DungeonData dd) ? dd.Name : $"dungeon{dungeon}";
            Console.WriteLine($"[EnemyInfo] {dungeonName} (id={dungeon}) floor={floor + 1}");

            for (int i = 0; i < 16; i++)
            {
                int slotBase = Enemy0.visible + (offset * i);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;
                if (MiniBoss.miniBossEnemyNumbers.Contains(i)) continue;

                ushort nameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i));
                string name = GetEnemyName(nameId);

                int maxHp      = Memory.ReadInt(slotBase + EnemySlotOffsets.MaxHp);
                int abs        = Memory.ReadInt(slotBase + EnemySlotOffsets.Abs);
                int minGold    = Memory.ReadInt(slotBase + EnemySlotOffsets.MinGoldDrop);
                int dropChance = Memory.ReadInt(slotBase + EnemySlotOffsets.DropChance);

                int p1 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack1);
                int p2 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack2);
                int p3 = Memory.ReadInt(slotBase + EnemySlotOffsets.ResistancePack3);
                ushort res1 = (ushort)(p1 & 0xFFFF); ushort fire = (ushort)(p1 >> 16);
                ushort ice  = (ushort)(p2 & 0xFFFF); ushort thun = (ushort)(p2 >> 16);
                ushort wind = (ushort)(p3 & 0xFFFF); ushort holy = (ushort)(p3 >> 16);

                float scale    = Memory.ReadFloat(slotBase + EnemySlotOffsets.EntityScale);
                int unk090     = Memory.ReadInt(slotBase + EnemySlotOffsets.Unk090);
                ushort u90a    = (ushort)(unk090 & 0xFFFF);
                ushort u90b    = (ushort)(unk090 >> 16);
                ushort stealId = (ushort)(Memory.ReadInt(slotBase + EnemySlotOffsets.StealItemId) & 0xFFFF);
                int itemResRaw = Memory.ReadInt(slotBase + EnemySlotOffsets.ItemResistance);
                ushort irA     = (ushort)(itemResRaw & 0xFFFF);
                ushort irB     = (ushort)(itemResRaw >> 16);
                float reticleW = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleWidth);
                float reticleH = Memory.ReadFloat(slotBase + EnemySlotOffsets.ReticleHeight);

                int   scaleBase = ModelScaleOffsets.ModelBase + ModelScaleOffsets.ModelStride * i;
                float unk020    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.Unk020);
                float unk024    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.Unk024);
                float unk028    = Memory.ReadFloat(scaleBase + ModelScaleOffsets.Unk028);
                int   dataSize  = Memory.ReadInt  (scaleBase + ModelScaleOffsets.DataSize);
                int   animCount = Memory.ReadInt  (scaleBase + ModelScaleOffsets.AnimCount);

                Console.WriteLine($"[EnemyInfo] slot={i} {name} (id={nameId}) hp={maxHp} abs={abs} gold={minGold} drop={dropChance}% | type={res1} fire={fire} ice={ice} thun={thun} wind={wind} holy={holy}");
                Console.WriteLine($"[EnemyInfo] slot={i} {name} scale={scale:F1} 090={u90a}/{u90b} steal=0x{stealId:X3} itemRes={irA}/{irB} reticle={reticleW:F2}x{reticleH:F2} | unk020={unk020:F1} unk024={unk024:F1} unk028={unk028:F1} dataSize={dataSize} animCount={animCount}");
            }
        }

        /// <summary>
        /// Dumps the model/render scale table (separate from enemy slots).
        /// Base 0x21E18530, stride 0x3510 — mirrors the width/height/depth used by MiniBoss scaling.
        /// </summary>
        internal static void DumpModelScaleTable()
        {
            const int modelBase   = 0x21E18530;
            const int modelStride = 0x3510;
            for (int i = 0; i < 16; i++)
            {
                int slotBase = Enemy0.visible + (offset * i);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;

                ushort nameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i));
                string name = GetEnemyName(nameId);
                bool isMiniboss = MiniBoss.miniBossEnemyNumbers.Contains(i);
                string tag = isMiniboss ? " [MINIBOSS]" : "";

                int scaleBase = modelBase + modelStride * i;
                Console.WriteLine($"[ModelScale] slot={i} {name} (id={nameId}) base=0x{scaleBase:X8}{tag}");
                for (int off = 0; off < 0x3510; off += 4)
                {
                    int raw = Memory.ReadInt(scaleBase + off);
                    float asFloat = Memory.ReadFloat(scaleBase + off);
                    Console.WriteLine($"[ModelScale] slot={i} +0x{off:X3}  raw=0x{raw:X8}  int={raw,-12}  float={asFloat:F4}");
                }
            }
        }

        private static readonly HashSet<int> _exhaustedSlots = new HashSet<int>();
        private static readonly HashSet<int> _activationLoggedSlots = [];

        internal static void ResetPollState()
        {
            _enemySlotSnapshots.Clear();
            _exhaustedSlots.Clear();
            _activationLoggedSlots.Clear();
            _flashTimerSnapshot.Clear();
        }

        internal static void PollEnemyDynamics()
        {
            const int wordCount = 0x190 / 4;
            const float maxDistance = 50.0f;

            bool anyChanges = false;

            for (int i = 0; i < 16; i++)
            {
                if (_exhaustedSlots.Contains(i)) continue;

                int slotBase = Enemy0.visible + (offset * i);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;

                float dist = Memory.ReadFloat(slotBase + EnemySlotOffsets.DistanceToPlayer);
                if (dist > maxDistance) continue;

                int[] current = new int[wordCount];
                for (int w = 0; w < wordCount; w++)
                    current[w] = Memory.ReadInt(slotBase + w * 4);

                int hp = Memory.ReadInt(slotBase + EnemySlotOffsets.Hp);
                if (hp <= 0)
                    _exhaustedSlots.Add(i);

                if (status == 2 && !_activationLoggedSlots.Contains(i))
                {
                    _activationLoggedSlots.Add(i);
                    ushort actNameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i));
                    string actName   = GetEnemyName(actNameId);
                    string actMbTag  = MiniBoss.miniBossEnemyNumbers.Contains(i) ? " [MINIBOSS]" : "";
                    float  F(int w)  => BitConverter.ToSingle(BitConverter.GetBytes(current[w]), 0);
                    int    typePtr   = current[0x04C / 4];
                    float  moveBlend = F(0x080 / 4);
                    float  facingX   = F(0x060 / 4);
                    float  facingY   = F(0x064 / 4);
                    float  facingZ   = F(0x068 / 4);
                    float  targetX   = F(0x070 / 4);
                    float  targetZ   = F(0x074 / 4);
                    float  targetY   = F(0x078 / 4);
                    int    aiState   = current[0x0EC / 4];
                    float  aiSpeed   = F(0x0F0 / 4);
                    float  rangeX    = F(0x140 / 4);
                    float  rangeY    = F(0x144 / 4);
                    float  rangeZ    = F(0x148 / 4);
                    float  aggro     = F(0x14C / 4);
                    Console.WriteLine($"[EnemyActivation] slot={i} {actName}{actMbTag} typePtr=0x{typePtr:X8} moveBlend={moveBlend:F2} facing=({facingX:F4},{facingY:F4},{facingZ:F4})");
                    Console.WriteLine($"[EnemyActivation] slot={i} {actName}{actMbTag} target=({targetX:F1},{targetZ:F1},{targetY:F1}) aiState=0x{aiState:X8} aiSpeed={aiSpeed:F4} aggroRange={aggro:F1} behaviorRange=({rangeX:F1},{rangeY:F1},{rangeZ:F1})");
                }

                if (_enemySlotSnapshots.TryGetValue(i, out int[] prev))
                {
                    ushort nameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i));
                    string name = GetEnemyName(nameId);
                    bool isMiniboss = MiniBoss.miniBossEnemyNumbers.Contains(i);
                    string mbTag = isMiniboss ? " [MINIBOSS]" : "";
                    for (int w = 0; w < wordCount; w++)
                    {
                        if (current[w] != prev[w])
                        {
                            if (!anyChanges)
                            {
                                anyChanges = true;
                                float px = Memory.ReadFloat(Player.dunPositionX);
                                float py = Memory.ReadFloat(Player.dunPositionY);
                                float pz = Memory.ReadFloat(Player.dunPositionZ);
                                int charId = Memory.ReadByte(Player.currentCharacter);
                                string charName = Player.GetCharacterName(charId) ?? "Unknown";
                                ushort charHp = Player.Toan.GetHp();
                                int lastDmg = Memory.ReadInt(Player.mostRecentDamage);
                                int dmgSrc = Memory.ReadInt(Player.damageSource);
                                int animId = Memory.ReadInt(Player.animationId);
                                Console.WriteLine($"[PlayerState] {charName} hp={charHp} pos=({px:F1},{py:F1},{pz:F1}) anim={animId} lastDmg={lastDmg} dmgSrc={dmgSrc}");
                            }
                            int off = w * 4;
                            float asFloat = BitConverter.ToSingle(BitConverter.GetBytes(current[w]), 0);
                            Console.WriteLine($"[EnemyPoll] slot={i} {name}{mbTag} +0x{off:X3}  0x{prev[w]:X8} -> 0x{current[w]:X8}  (int:{current[w]}  float:{asFloat:F4})");
                        }
                    }
                }
                _enemySlotSnapshots[i] = current;
            }
        }

        private static DateTime _lastSlotWriteTime = DateTime.MinValue;
        private static int _slotWriteCycle = 0;

        private static readonly float[] _decayRateTestValues = [ 0.0f, 0.1f, 0.5f, 1.0f, 5.0f ];

        internal static void ApplyTestModifications()
        {
            for (int i = 0; i < 16; i++)
            {
                int slotBase = Enemy0.visible + (offset * i);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;
                if (MiniBoss.miniBossEnemyNumbers.Contains(i)) continue;

                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorRed,   255.0f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorGreen, 255.0f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorBlue,  255.0f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashDecayRate,  0.016f);
                Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashTimer,      1.0f);
                Memory.WriteInt(slotBase + EnemySlotOffsets.FlashActivation,   1);

                ushort nameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i));
                string name = GetEnemyName(nameId);
                Console.WriteLine($"[TestMod] slot={i} {name}  flash set: decayRate=0.016 timer=1.0");
            }
        }

        private static readonly Dictionary<int, float> _flashTimerSnapshot = [];

        private static DateTime _lastFlashTriggerTime = DateTime.MinValue;

        internal static void MonitorFlashTimer()
        {
            bool triggerFlash = (DateTime.UtcNow - _lastFlashTriggerTime).TotalSeconds >= 3.0;
            if (triggerFlash) _lastFlashTriggerTime = DateTime.UtcNow;

            for (int i = 0; i < 16; i++)
            {
                int slotBase = Enemy0.visible + (offset * i);
                int status = Memory.ReadInt(slotBase);
                if (status == -1 || status == 0) continue;

                if (triggerFlash)
                {
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorRed,   255.0f);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorGreen, 255.0f);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashColorBlue,  255.0f);
                    // FlashTimer is engine-owned and always reset to 1.0; FlashDecayRate controls duration: 1.0/rate frames.
                    // At 30fps: 0.016 ≈ 2s, 0.08 ≈ 12 frames (natural hit flash).
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashDecayRate, 0.016f);
                    Memory.WriteFloat(slotBase + EnemySlotOffsets.FlashTimer,        1.0f);
                    Memory.WriteInt(slotBase + EnemySlotOffsets.FlashActivation,   1);
                    _flashTimerSnapshot[i] = 1.0f;
                    ushort nameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i));
                    string name = GetEnemyName(nameId);
                    Console.WriteLine($"[FlashTimer] slot={i} {name}  flash set: decayRate=0.016 timer=1.0");
                    continue;
                }

                float duration = Memory.ReadFloat(slotBase + EnemySlotOffsets.FlashDecayRate);
                float timer    = Memory.ReadFloat(slotBase + EnemySlotOffsets.FlashTimer);
                int   active   = Memory.ReadInt(slotBase + EnemySlotOffsets.FlashActivation);

                _flashTimerSnapshot.TryGetValue(i, out float prevTimer);
                if (Math.Abs(timer - prevTimer) > 0.0001f)
                {
                    ushort nameId = Memory.ReadUShort(Enemy0.enemyTypeId + (offset * i));
                    string name = GetEnemyName(nameId);
                    Console.WriteLine($"[FlashTimer] slot={i} {name}  active={active}  decayRate={duration:F4}  flashTimer={timer:F4}");
                    _flashTimerSnapshot[i] = timer;
                }
            }
        }

        internal class Digger
        {
            public const int maxJumpDistance = 0x213F3D70;

        }
    }
}
