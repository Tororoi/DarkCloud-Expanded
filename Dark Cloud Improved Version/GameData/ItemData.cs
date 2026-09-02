using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dark_Cloud_Improved_Version
{
    /// <summary>
    /// CONFIRMED engine item class, from <c>ComItemInfo</c> +0x0 (see
    /// <see cref="ItemAddresses.ComItemInfo"/>). Coarse 3-way grouping of item IDs 81-376.
    /// </summary>
    internal enum ItemClass
    {
        Attach = 0,  // element/stat spheres, gems, slayer attachments (IDs 81-131)
        Item   = 1,  // usable / consumable / key items / materials (IDs 132-256)
        Weapon = 2,  // swords, slingshots, hammers, rings, spears, guns (IDs 257-376)
    }

    /// <summary>
    /// Finer human-facing item grouping. The ELF only stores the coarse <see cref="ItemClass"/>;
    /// these are MAPPED by ID range + asset-code prefix (Attach class) + the known bait set.
    /// Best-effort / hand-curatable, not a direct engine field.
    /// </summary>
    internal enum ItemCategory
    {
        Unknown = 0,
        Element,      // weapon element spheres (atfire/atice/atthunde/atwind/atholy)
        StatSphere,   // Attack/Endurance/Speed/Magic spheres (atpower/atdamage/atspeed/atmagic)
        Gem,          // birthstone gems + Sun (juel*/taiyou)
        Attachment,   // slayer / build-up attachments (zat*: Dinoslayer..Mage Slayer)
        Amulet,       // status-prevention amulets (132-135)
        Consumable,   // food / drink / recovery
        Throwable,    // bombs, stones, element gems thrown in dungeons
        Powder,       // powders (Medusa, Warp, Repair, ...)
        Bait,         // fishing bait (see BaitDetectionRadiusTable)
        KeyItem,      // quest / progression items
        Material,     // synthesis / crafting materials (reserved; not currently assigned)
        Weapon,       // weapons (ItemClass.Weapon)
        Special,      // unique / event / utility items (Pocket, Gourd, Fishing Rod, Maps, ...)
    }

    /// <summary>
    /// Per-item usage / behaviour flags. UNCONFIRMED — no per-item flag table exists in the ELF
    /// (item behaviour is driven by ID-range checks in code, e.g. ItemGetMes at 81/145/...).
    /// Reserved for hand-curated data if needed.
    /// </summary>
    [System.Flags]
    internal enum ItemFlags
    {
        None = 0,
        UsableInDungeon = 1 << 0,
        Sellable        = 1 << 1,
        KeyItem         = 1 << 2,
    }

    /// <summary>
    /// Known data for a single item, mirroring the EnemyData / FishData convention.
    /// CONFIRMED fields are sourced from ELF static tables (item research session):
    ///   <see cref="Id"/>      - item ID (81-376).
    ///   <see cref="Name"/>    - display name (Items.ItemNameTbl).
    ///   <see cref="Code"/>    - internal asset/model code from ITEM_NAME_TBL_NEW (IDs 81-255 only;
    ///                           null for dummies and weapons 256+). e.g. "atfire", "juelgnet".
    ///   <see cref="ValueBuy"/> / <see cref="ValueSell"/> - default prices from PriceList
    ///                           (ItemAddresses.ItemPriceTable; buy @+0, sell @+2, index = Id-81).
    ///   <see cref="Class"/>   - engine 3-way class from ComItemInfo +0x0.
    ///   <see cref="Category"/> - finer grouping, MAPPED (see <see cref="ItemCategory"/>).
    /// Per-dungeon DROP RATES are NOT stored here (they vary by dungeon) - see
    /// <see cref="ItemAddresses.ItemDropRateLists"/> (ItemSetRateList, index = Id-1).
    /// UNCONFIRMED (no ELF table; null): <see cref="MaxStack"/>, <see cref="Flags"/>.
    /// </summary>
    internal struct ItemData
    {
        internal ushort Id;
        internal string Name;
        internal string Code;        // ITEM_NAME_TBL_NEW asset code (IDs 81-255); null otherwise

        internal ushort? ValueBuy;   // PriceList +0x0 (confirmed)
        internal ushort? ValueSell;  // PriceList +0x2 (confirmed)

        internal ItemClass?    Class;     // ComItemInfo +0x0 (confirmed)
        internal ItemCategory? Category;  // mapped (id-range + code prefix)

        internal byte?      MaxStack;  // unconfirmed (no ELF table)
        internal ItemFlags? Flags;     // unconfirmed (no ELF table)
    }

    /// <summary>
    /// Static repository of per-item <see cref="ItemData"/> records. Each item is a named field, so
    /// callers can reference it directly: <c>Item.Prickly.Id</c>, <c>Item.Fire.ValueSell</c>, etc.
    /// (Dummy / unnamed IDs use the fallback name <c>Item&lt;id&gt;</c>, e.g. <c>Item.Item86</c>.)
    /// Confirmed fields (Code, ValueBuy/Sell, Class) come from the ELF static tables; Category is a
    /// mapped best-effort. See the item-static-tables research note.
    ///
    /// <see cref="All"/> / the ID lookup are built once via reflection over the static fields, so
    /// adding or editing an item only means touching its field declaration.
    /// </summary>
    internal static class Item
    {
        internal static readonly ItemData Fire = new ItemData
        {
            Id = 81, Name = "Fire", Code = "atfire",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Element,
        };

        internal static readonly ItemData Ice = new ItemData
        {
            Id = 82, Name = "Ice", Code = "atice",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Element,
        };

        internal static readonly ItemData Thunder = new ItemData
        {
            Id = 83, Name = "Thunder", Code = "atthunde",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Element,
        };

        internal static readonly ItemData Wind = new ItemData
        {
            Id = 84, Name = "Wind", Code = "atwind",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Element,
        };

        internal static readonly ItemData Holy = new ItemData
        {
            Id = 85, Name = "Holy", Code = "atholy",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Element,
        };

        internal static readonly ItemData Item86 = new ItemData
        {
            Id = 86, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item87 = new ItemData
        {
            Id = 87, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item88 = new ItemData
        {
            Id = 88, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item89 = new ItemData
        {
            Id = 89, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item90 = new ItemData
        {
            Id = 90, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Attack = new ItemData
        {
            Id = 91, Name = "Attack", Code = "atpower",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.StatSphere,
        };

        internal static readonly ItemData Endurance = new ItemData
        {
            Id = 92, Name = "Endurance", Code = "atdamage",
            ValueBuy = 260, ValueSell = 130,
            Class = ItemClass.Attach, Category = ItemCategory.StatSphere,
        };

        internal static readonly ItemData Speed = new ItemData
        {
            Id = 93, Name = "Speed", Code = "atspeed",
            ValueBuy = 260, ValueSell = 130,
            Class = ItemClass.Attach, Category = ItemCategory.StatSphere,
        };

        internal static readonly ItemData Magic = new ItemData
        {
            Id = 94, Name = "Magic", Code = "atmagic",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.StatSphere,
        };

        internal static readonly ItemData Garnet = new ItemData
        {
            Id = 95, Name = "Garnet", Code = "juelgnet",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Amethyst = new ItemData
        {
            Id = 96, Name = "Amethyst", Code = "juelamst",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Aquamarine = new ItemData
        {
            Id = 97, Name = "Aquamarine", Code = "juelaqua",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Diamond = new ItemData
        {
            Id = 98, Name = "Diamond", Code = "jueldaia",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Emerald = new ItemData
        {
            Id = 99, Name = "Emerald", Code = "juelemrd",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Pearl = new ItemData
        {
            Id = 100, Name = "Pearl", Code = "juelparl",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Ruby = new ItemData
        {
            Id = 101, Name = "Ruby", Code = "juelruby",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Peridot = new ItemData
        {
            Id = 102, Name = "Peridot", Code = "juelprdt",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Sapphire = new ItemData
        {
            Id = 103, Name = "Sapphire", Code = "juelsphr",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Opal = new ItemData
        {
            Id = 104, Name = "Opal", Code = "juelopal",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Topaz = new ItemData
        {
            Id = 105, Name = "Topaz", Code = "jueltpaz",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Turquoise = new ItemData
        {
            Id = 106, Name = "Turquoise", Code = "jueltrqu",
            ValueBuy = 3000, ValueSell = 600,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Sun = new ItemData
        {
            Id = 107, Name = "Sun", Code = "taiyou",
            ValueBuy = 5000, ValueSell = 1000,
            Class = ItemClass.Attach, Category = ItemCategory.Gem,
        };

        internal static readonly ItemData Item108 = new ItemData
        {
            Id = 108, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item109 = new ItemData
        {
            Id = 109, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item110 = new ItemData
        {
            Id = 110, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Dinoslayer = new ItemData
        {
            Id = 111, Name = "Dinoslayer", Code = "zatdino",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData UndeadBuster = new ItemData
        {
            Id = 112, Name = "Undead Buster", Code = "zatunded",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData SeaKiller = new ItemData
        {
            Id = 113, Name = "Sea Killer", Code = "zatsea",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData StoneBreaker = new ItemData
        {
            Id = 114, Name = "Stone Breaker", Code = "zatstorn",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData PlantBuster = new ItemData
        {
            Id = 115, Name = "Plant Buster", Code = "zatplant",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData BeastBuster = new ItemData
        {
            Id = 116, Name = "Beast Buster", Code = "zatbeast",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData SkyHunter = new ItemData
        {
            Id = 117, Name = "Sky Hunter", Code = "zatsky",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData MetalBreaker = new ItemData
        {
            Id = 118, Name = "Metal Breaker", Code = "zatmetal",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData MimicBreaker = new ItemData
        {
            Id = 119, Name = "Mimic Breaker", Code = "zatmimic",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData MageSlayer = new ItemData
        {
            Id = 120, Name = "Mage Slayer", Code = "zatmaji",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Attach, Category = ItemCategory.Attachment,
        };

        internal static readonly ItemData Item121 = new ItemData
        {
            Id = 121, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item122 = new ItemData
        {
            Id = 122, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item123 = new ItemData
        {
            Id = 123, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item124 = new ItemData
        {
            Id = 124, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item125 = new ItemData
        {
            Id = 125, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item126 = new ItemData
        {
            Id = 126, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item127 = new ItemData
        {
            Id = 127, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item128 = new ItemData
        {
            Id = 128, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item129 = new ItemData
        {
            Id = 129, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item130 = new ItemData
        {
            Id = 130, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item131 = new ItemData
        {
            Id = 131, Name = "Unknown",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Attach, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData AntiFreezeAmulet = new ItemData
        {
            Id = 132, Name = "Anti-Freeze Amulet", Code = "mayokest",
            ValueBuy = 400, ValueSell = 200,
            Class = ItemClass.Item, Category = ItemCategory.Amulet,
        };

        internal static readonly ItemData AntiCurseAmulet = new ItemData
        {
            Id = 133, Name = "Anti-Curse Amulet", Code = "mayokenr",
            ValueBuy = 440, ValueSell = 220,
            Class = ItemClass.Item, Category = ItemCategory.Amulet,
        };

        internal static readonly ItemData AntiGooAmulet = new ItemData
        {
            Id = 134, Name = "Anti-Goo Amulet", Code = "mayokenb",
            ValueBuy = 380, ValueSell = 190,
            Class = ItemClass.Item, Category = ItemCategory.Amulet,
        };

        internal static readonly ItemData AntidoteAmulet = new ItemData
        {
            Id = 135, Name = "Antidote Amulet", Code = "mayokedk",
            ValueBuy = 400, ValueSell = 200,
            Class = ItemClass.Item, Category = ItemCategory.Amulet,
        };

        internal static readonly ItemData FluffyDoughnut = new ItemData
        {
            Id = 136, Name = "Fluffy Doughnut", Code = "dounut",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData FishCandy = new ItemData
        {
            Id = 137, Name = "Fish Candy", Code = "skanacnd",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData GrassCake = new ItemData
        {
            Id = 138, Name = "Grass Cake", Code = "kusamoti",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData WitchParfait = new ItemData
        {
            Id = 139, Name = "Witch Parfait", Code = "majopafe",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData ScorpionJerky = new ItemData
        {
            Id = 140, Name = "Scorpion Jerky", Code = "sasorijk",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData CarrotCookie = new ItemData
        {
            Id = 141, Name = "Carrot Cookie", Code = "ninjncki",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData Item142 = new ItemData
        {
            Id = 142, Name = "black square 142",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item143 = new ItemData
        {
            Id = 143, Name = "black square 143",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item144 = new ItemData
        {
            Id = 144, Name = "black square 144",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData RegularWater = new ItemData
        {
            Id = 145, Name = "Regular Water", Code = "mizufutu",
            ValueBuy = 10, ValueSell = 5,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData TastyWater = new ItemData
        {
            Id = 146, Name = "Tasty Water", Code = "mizuoisi",
            ValueBuy = 30, ValueSell = 15,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData PremiumWater = new ItemData
        {
            Id = 147, Name = "Premium Water", Code = "mizugoku",
            ValueBuy = 60, ValueSell = 30,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData Bread = new ItemData
        {
            Id = 148, Name = "Bread", Code = "pan",
            ValueBuy = 20, ValueSell = 10,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData PremiumChicken = new ItemData
        {
            Id = 149, Name = "Premium Chicken", Code = "chicken",
            ValueBuy = 130, ValueSell = 65,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData StaminaDrink = new ItemData
        {
            Id = 150, Name = "Stamina Drink", Code = "bin2drnk",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData AntidoteDrink = new ItemData
        {
            Id = 151, Name = "Antidote Drink", Code = "dokukesi",
            ValueBuy = 80, ValueSell = 40,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData HolyWater = new ItemData
        {
            Id = 152, Name = "Holy Water", Code = "seisui",
            ValueBuy = 120, ValueSell = 60,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData Soap = new ItemData
        {
            Id = 153, Name = "Soap", Code = "sekken",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData MightyHealing = new ItemData
        {
            Id = 154, Name = "Mighty Healing", Code = "mityheal",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData Cheese = new ItemData
        {
            Id = 155, Name = "Cheese", Code = "cheese",
            ValueBuy = 60, ValueSell = 30,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData Item156 = new ItemData
        {
            Id = 156, Name = "black square 156",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item157 = new ItemData
        {
            Id = 157, Name = "black square 157",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item158 = new ItemData
        {
            Id = 158, Name = "black square 158",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Bomb = new ItemData
        {
            Id = 159, Name = "Bomb", Code = "bakudan",
            ValueBuy = 80, ValueSell = 40,
            Class = ItemClass.Item, Category = ItemCategory.Throwable,
        };

        internal static readonly ItemData Stone = new ItemData
        {
            Id = 160, Name = "Stone", Code = "ishi",
            ValueBuy = 4, ValueSell = 2,
            Class = ItemClass.Item, Category = ItemCategory.Throwable,
        };

        internal static readonly ItemData FireGem = new ItemData
        {
            Id = 161, Name = "Fire Gem", Code = "masekifi",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Throwable,
        };

        internal static readonly ItemData IceGem = new ItemData
        {
            Id = 162, Name = "Ice Gem", Code = "masekiic",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Throwable,
        };

        internal static readonly ItemData ThunderGem = new ItemData
        {
            Id = 163, Name = "Thunder Gem", Code = "masekitd",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Throwable,
        };

        internal static readonly ItemData WindGem = new ItemData
        {
            Id = 164, Name = "Wind Gem", Code = "masekiwd",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Throwable,
        };

        internal static readonly ItemData HolyGem = new ItemData
        {
            Id = 165, Name = "Holy Gem", Code = "masekiho",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Throwable,
        };

        internal static readonly ItemData ThrobbingCherry = new ItemData
        {
            Id = 166, Name = "Throbbing Cherry", Code = "isiranbo",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData GooeyPeach = new ItemData
        {
            Id = 167, Name = "Gooey Peach", Code = "nebapeac",
            ValueBuy = 80, ValueSell = 40,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData BombNuts = new ItemData
        {
            Id = 168, Name = "Bomb Nuts", Code = "bomnuts",
            ValueBuy = 90, ValueSell = 45,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData PoisonousApple = new ItemData
        {
            Id = 169, Name = "Poisonous Apple", Code = "dokuring",
            ValueBuy = 120, ValueSell = 60,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData MellowBanana = new ItemData
        {
            Id = 170, Name = "Mellow Banana", Code = "banana",
            ValueBuy = 80, ValueSell = 40,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData MedusaPowder = new ItemData
        {
            Id = 171, Name = "Medusa Powder", Code = "konamedu",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData HardeningPowder = new ItemData
        {
            Id = 172, Name = "Hardening Powder", Code = "konakata",
            ValueBuy = 43, ValueSell = 22,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData WarpPowder = new ItemData
        {
            Id = 173, Name = "Warp Powder", Code = "konawarp",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData StandInPowder = new ItemData
        {
            Id = 174, Name = "Stand-in Powder", Code = "konakawa",
            ValueBuy = 50, ValueSell = 25,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData EscapePowder = new ItemData
        {
            Id = 175, Name = "Escape Powder", Code = "konaexit",
            ValueBuy = 20, ValueSell = 10,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData RevivalPowder = new ItemData
        {
            Id = 176, Name = "Revival Powder", Code = "konafuka",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData RepairPowder = new ItemData
        {
            Id = 177, Name = "Repair Powder", Code = "konarepe",
            ValueBuy = 20, ValueSell = 10,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData PowerupPowder = new ItemData
        {
            Id = 178, Name = "Powerup Powder", Code = "konalvup",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData Pocket = new ItemData
        {
            Id = 179, Name = "Pocket", Code = "pocket",
            ValueBuy = 100, ValueSell = 50,
            Class = ItemClass.Item, Category = ItemCategory.Special,
        };

        internal static readonly ItemData FruitOfEden = new ItemData
        {
            Id = 180, Name = "Fruit of Eden", Code = "edenfurt",
            ValueBuy = 800, ValueSell = 400,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData TreasureKey = new ItemData
        {
            Id = 181, Name = "Treasure Key", Code = "takaraky",
            ValueBuy = 800, ValueSell = 400,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Gourd = new ItemData
        {
            Id = 182, Name = "Gourd", Code = "hyoutan",
            ValueBuy = 500, ValueSell = 250,
            Class = ItemClass.Item, Category = ItemCategory.Special,
        };

        internal static readonly ItemData AutoRepairPowder = new ItemData
        {
            Id = 183, Name = "Auto Repair Powder", Code = "konarepe_at",
            ValueBuy = 200, ValueSell = 100,
            Class = ItemClass.Item, Category = ItemCategory.Powder,
        };

        internal static readonly ItemData Item184 = new ItemData
        {
            Id = 184, Name = "black square 184",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData FishingRod = new ItemData
        {
            Id = 185, Name = "Fishing Rod", Code = "turisao",
            ValueBuy = 500, ValueSell = 250,
            Class = ItemClass.Item, Category = ItemCategory.Special,
        };

        internal static readonly ItemData Carrot = new ItemData
        {
            Id = 186, Name = "Carrot", Code = "ninzin",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData PotatoCake = new ItemData
        {
            Id = 187, Name = "Potato cake", Code = "imodango",
            ValueBuy = 450, ValueSell = 225,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData Minon = new ItemData
        {
            Id = 188, Name = "Minon", Code = "minon",
            ValueBuy = 400, ValueSell = 200,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData Battan = new ItemData
        {
            Id = 189, Name = "Battan", Code = "battan",
            ValueBuy = 420, ValueSell = 210,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData PetiteFish = new ItemData
        {
            Id = 190, Name = "Petite Fish", Code = "puti",
            ValueBuy = 380, ValueSell = 190,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData SavingBook = new ItemData
        {
            Id = 191, Name = "Saving Book", Code = "savesyo",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData GoldBullion = new ItemData
        {
            Id = 192, Name = "Gold Bullion",
            ValueBuy = 1000, ValueSell = 1000,
            Class = ItemClass.Item, Category = ItemCategory.Special,
        };

        internal static readonly ItemData Evy = new ItemData
        {
            Id = 193, Name = "Evy", Code = "ebi",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData Item194 = new ItemData
        {
            Id = 194, Name = "black square 194",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData DranSCrest = new ItemData
        {
            Id = 195, Name = "Dran's Crest", Code = "gkeydran",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData ShinyStone = new ItemData
        {
            Id = 196, Name = "Shiny Stone", Code = "hikaruis",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Mimi = new ItemData
        {
            Id = 197, Name = "Mimi", Code = "gkeymimi",
            ValueBuy = 400, ValueSell = 200,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData RedBerry = new ItemData
        {
            Id = 198, Name = "Red Berry", Code = "akaikimi",
            ValueBuy = 400, ValueSell = 200,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Prickly = new ItemData
        {
            Id = 199, Name = "Prickly", Code = "togecchi",
            ValueBuy = 400, ValueSell = 200,
            Class = ItemClass.Item, Category = ItemCategory.Bait,
        };

        internal static readonly ItemData Candy = new ItemData
        {
            Id = 200, Name = "Candy", Code = "candy",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Hook = new ItemData
        {
            Id = 201, Name = "Hook", Code = "hook",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData KingSSlate = new ItemData
        {
            Id = 202, Name = "King's Slate", Code = "oukenost",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData GunPowder = new ItemData
        {
            Id = 203, Name = "Gun Powder", Code = "kayaku",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData ClockHands = new ItemData
        {
            Id = 204, Name = "Clock Hands", Code = "tokeisin",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData PointyChestnut = new ItemData
        {
            Id = 205, Name = "Pointy Chestnut", Code = "tongrmrn",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData BlackKnightCrest = new ItemData
        {
            Id = 206, Name = "Black Knight Crest", Code = "gkey_ds",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData HornedKey = new ItemData
        {
            Id = 207, Name = "Horned Key", Code = "keytuno",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData MoonGrassSeed = new ItemData
        {
            Id = 208, Name = "Moon Grass Seed", Code = "mikazuki",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData MusicBoxKey = new ItemData
        {
            Id = 209, Name = "Music Box Key", Code = "orgelnej",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData SunSignet = new ItemData
        {
            Id = 210, Name = "Sun Signet", Code = "taiysirs",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData MoonSignet = new ItemData
        {
            Id = 211, Name = "Moon Signet", Code = "tukisirs",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData AdmissionTicket = new ItemData
        {
            Id = 212, Name = "Admission Ticket", Code = "ticket",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Item213 = new ItemData
        {
            Id = 213, Name = "black square 213",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item214 = new ItemData
        {
            Id = 214, Name = "black square 214",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item215 = new ItemData
        {
            Id = 215, Name = "black square 215",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData BoneKey = new ItemData
        {
            Id = 216, Name = "Bone Key", Code = "keyhone",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData MoustacheKey = new ItemData
        {
            Id = 217, Name = "Moustache Key", Code = "keyhige",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData ShipcabinKey = new ItemData
        {
            Id = 218, Name = "Shipcabin Key", Code = "keysenst",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData StoneKey = new ItemData
        {
            Id = 219, Name = "Stone Key", Code = "keyishi",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Handle = new ItemData
        {
            Id = 220, Name = "Handle", Code = "handol",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData PitchdarkKey = new ItemData
        {
            Id = 221, Name = "Pitchdark Key", Code = "keysikok",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData SilverKey = new ItemData
        {
            Id = 222, Name = "Silver Key", Code = "fkey_ds",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Item223 = new ItemData
        {
            Id = 223, Name = "black square 223",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData TramOil = new ItemData
        {
            Id = 224, Name = "Tram Oil", Code = "torokoil",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData SunDew = new ItemData
        {
            Id = 225, Name = "Sun Dew", Code = "taiysizk",
            ValueBuy = 300, ValueSell = 150,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData FlappingFish = new ItemData
        {
            Id = 226, Name = "Flapping Fish", Code = "pitisakn",
            ValueBuy = 180, ValueSell = 90,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData RottenFish = new ItemData
        {
            Id = 227, Name = "Rotten Fish", Code = "kusasakn",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData SecretPathKey = new ItemData
        {
            Id = 228, Name = "Secret Path Key", Code = "keyhitug",
            ValueBuy = 500, ValueSell = 250,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData BraveryLaunch = new ItemData
        {
            Id = 229, Name = "Bravery Launch", Code = "yuukiita",
            ValueBuy = 800, ValueSell = 400,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData FlappingDuster = new ItemData
        {
            Id = 230, Name = "Flapping Duster", Code = "patapata",
            ValueBuy = 800, ValueSell = 400,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData CrystalEyeball = new ItemData
        {
            Id = 231, Name = "Crystal Eyeball", Code = "key_eye",
            ValueBuy = 1000, ValueSell = 500,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Item232 = new ItemData
        {
            Id = 232, Name = "black square 232",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Map = new ItemData
        {
            Id = 233, Name = "Map", Code = "map",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Special,
        };

        internal static readonly ItemData MagicalCrystal = new ItemData
        {
            Id = 234, Name = "Magical Crystal", Code = "masuisyo",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData DranSFeather = new ItemData
        {
            Id = 235, Name = "Dran's Feather", Code = "dorahane",
            ValueBuy = 50, ValueSell = 25,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData CaveKey = new ItemData
        {
            Id = 236, Name = "Cave Key", Code = "keydokut",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData ChangingPotion = new ItemData
        {
            Id = 237, Name = "Changing Potion", Code = "hengemiz",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData WorldMap = new ItemData
        {
            Id = 238, Name = "World Map", Code = "worldmap",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Special,
        };

        internal static readonly ItemData BonePendant = new ItemData
        {
            Id = 239, Name = "Bone Pendant", Code = "honepend",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData OddToneFlute = new ItemData
        {
            Id = 240, Name = "Odd Tone Flute", Code = "tukifue",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData MagicalLamp = new ItemData
        {
            Id = 241, Name = "Magical Lamp", Code = "mahoranp",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData MoonOrb = new ItemData
        {
            Id = 242, Name = "Moon Orb", Code = "tukinoob",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData ShellRing = new ItemData
        {
            Id = 243, Name = "Shell Ring", Code = "kairing",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData SearchWarrant = new ItemData
        {
            Id = 244, Name = "Search Warrant", Code = "sosareij",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData IceBlock = new ItemData
        {
            Id = 245, Name = "Ice Block", Code = "icebig",
            ValueBuy = 20, ValueSell = 10,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData SmallIce = new ItemData
        {
            Id = 246, Name = "Small Ice", Code = "icemid",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData TinyIce = new ItemData
        {
            Id = 247, Name = "Tiny Ice", Code = "icesml",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData FlameKey = new ItemData
        {
            Id = 248, Name = "Flame Key", Code = "keyhonoo",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData HunterSEarring = new ItemData
        {
            Id = 249, Name = "Hunter's Earring", Code = "hantmimi",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData OintmentLeaf = new ItemData
        {
            Id = 250, Name = "Ointment Leaf", Code = "kouyaku",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Consumable,
        };

        internal static readonly ItemData Foundation = new ItemData
        {
            Id = 251, Name = "Foundation", Code = "fundtion",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData ClayDoll = new ItemData
        {
            Id = 252, Name = "Clay Doll", Code = "haniwa",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData Manual = new ItemData
        {
            Id = 253, Name = "Manual", Code = "manual",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData SunSphere = new ItemData
        {
            Id = 254, Name = "Sun Sphere", Code = "pezutama",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.KeyItem,
        };

        internal static readonly ItemData AlmightyPass = new ItemData
        {
            Id = 255, Name = "Almighty Pass",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData Item256 = new ItemData
        {
            Id = 256, Name = "black square 256",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Item, Category = ItemCategory.Unknown,
        };

        internal static readonly ItemData DaggerBroken = new ItemData
        {
            Id = 257, Name = "Dagger (broken)",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Dagger = new ItemData
        {
            Id = 258, Name = "Dagger",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Baselard = new ItemData
        {
            Id = 259, Name = "Baselard",
            ValueBuy = 300, ValueSell = 75,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Gladius = new ItemData
        {
            Id = 260, Name = "Gladius",
            ValueBuy = 500, ValueSell = 125,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData WiseOwlSword = new ItemData
        {
            Id = 261, Name = "Wise Owl Sword",
            ValueBuy = 2500, ValueSell = 625,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Crysknife = new ItemData
        {
            Id = 262, Name = "Crysknife",
            ValueBuy = 700, ValueSell = 175,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData AntiqueSword = new ItemData
        {
            Id = 263, Name = "Antique Sword",
            ValueBuy = 800, ValueSell = 200,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BusterSword = new ItemData
        {
            Id = 264, Name = "Buster Sword",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData KitchenKnife = new ItemData
        {
            Id = 265, Name = "Kitchen Knife",
            ValueBuy = 400, ValueSell = 100,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Tsukikage = new ItemData
        {
            Id = 266, Name = "Tsukikage",
            ValueBuy = 2000, ValueSell = 500,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SunSword = new ItemData
        {
            Id = 267, Name = "Sun Sword",
            ValueBuy = 3000, ValueSell = 750,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SerpentSword = new ItemData
        {
            Id = 268, Name = "Serpent Sword",
            ValueBuy = 2000, ValueSell = 500,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MachoSword = new ItemData
        {
            Id = 269, Name = "Macho Sword",
            ValueBuy = 2400, ValueSell = 600,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Shamshir = new ItemData
        {
            Id = 270, Name = "Shamshir",
            ValueBuy = 500, ValueSell = 125,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData HeavenSCloud = new ItemData
        {
            Id = 271, Name = "Heaven's Cloud",
            ValueBuy = 3000, ValueSell = 750,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData LambSSword = new ItemData
        {
            Id = 272, Name = "Lamb's Sword",
            ValueBuy = 3000, ValueSell = 750,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData DarkCloud = new ItemData
        {
            Id = 273, Name = "Dark Cloud",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BraveArk = new ItemData
        {
            Id = 274, Name = "Brave Ark",
            ValueBuy = 3000, ValueSell = 750,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BigBang = new ItemData
        {
            Id = 275, Name = "Big Bang",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData AtlamilliaSword = new ItemData
        {
            Id = 276, Name = "Atlamillia Sword",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData WeaponNo277 = new ItemData
        {
            Id = 277, Name = "weapon No.277",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MardanEins = new ItemData
        {
            Id = 278, Name = "Mardan Eins",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MardanTwei = new ItemData
        {
            Id = 279, Name = "Mardan Twei",
            ValueBuy = 2, ValueSell = 0,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData AriseMardan = new ItemData
        {
            Id = 280, Name = "Arise Mardan",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData AgaSSword = new ItemData
        {
            Id = 281, Name = "Aga's Sword",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Evilcise = new ItemData
        {
            Id = 282, Name = "Evilcise",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SmallSword = new ItemData
        {
            Id = 283, Name = "Small Sword",
            ValueBuy = 800, ValueSell = 200,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SandBreaker = new ItemData
        {
            Id = 284, Name = "Sand Breaker",
            ValueBuy = 920, ValueSell = 230,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData DrainSeeker = new ItemData
        {
            Id = 285, Name = "Drain Seeker",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Chopper = new ItemData
        {
            Id = 286, Name = "Chopper",
            ValueBuy = 900, ValueSell = 225,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Choora = new ItemData
        {
            Id = 287, Name = "Choora",
            ValueBuy = 990, ValueSell = 248,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Claymore = new ItemData
        {
            Id = 288, Name = "Claymore",
            ValueBuy = 2500, ValueSell = 625,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Maneater = new ItemData
        {
            Id = 289, Name = "Maneater",
            ValueBuy = 1500, ValueSell = 375,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BoneRapier = new ItemData
        {
            Id = 290, Name = "Bone Rapier",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Sax = new ItemData
        {
            Id = 291, Name = "Sax",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData _7BranchSword = new ItemData
        {
            Id = 292, Name = "7 Branch Sword",
            ValueBuy = 2000, ValueSell = 500,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Dusack = new ItemData
        {
            Id = 293, Name = "Dusack",
            ValueBuy = 940, ValueSell = 235,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData CrossHinder = new ItemData
        {
            Id = 294, Name = "Cross Hinder",
            ValueBuy = 1500, ValueSell = 375,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData _7thHeaven = new ItemData
        {
            Id = 295, Name = "7th Heaven",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SwordOfZeus = new ItemData
        {
            Id = 296, Name = "Sword Of Zeus",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData ChronicleSword = new ItemData
        {
            Id = 297, Name = "Chronicle Sword",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Chronicle2 = new ItemData
        {
            Id = 298, Name = "Chronicle 2",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData WoodenSlingshotBroken = new ItemData
        {
            Id = 299, Name = "Wooden Slingshot (broken)",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData WoodenSlingshot = new ItemData
        {
            Id = 300, Name = "Wooden Slingshot",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SteelSlingshot = new ItemData
        {
            Id = 301, Name = "Steel Slingshot",
            ValueBuy = 360, ValueSell = 90,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BanditSlingshot = new ItemData
        {
            Id = 302, Name = "Bandit Slingshot",
            ValueBuy = 600, ValueSell = 150,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Steve = new ItemData
        {
            Id = 303, Name = "Steve",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BoneSlingshot = new ItemData
        {
            Id = 304, Name = "Bone Slingshot",
            ValueBuy = 400, ValueSell = 100,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Hardshooter = new ItemData
        {
            Id = 305, Name = "Hardshooter",
            ValueBuy = 900, ValueSell = 225,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData DoubleImpact = new ItemData
        {
            Id = 306, Name = "Double Impact",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData DragonSY = new ItemData
        {
            Id = 307, Name = "Dragon's Y",
            ValueBuy = 1200, ValueSell = 300,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData DivineBeastTitle = new ItemData
        {
            Id = 308, Name = "Divine Beast Title",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData AngelShooter = new ItemData
        {
            Id = 309, Name = "Angel Shooter",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Flamingo = new ItemData
        {
            Id = 310, Name = "Flamingo",
            ValueBuy = 500, ValueSell = 125,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Matador = new ItemData
        {
            Id = 311, Name = "Matador",
            ValueBuy = 600, ValueSell = 150,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SuperSteve = new ItemData
        {
            Id = 312, Name = "Super Steve",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData AngelGear = new ItemData
        {
            Id = 313, Name = "Angel Gear",
            ValueBuy = 900, ValueSell = 225,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MalletBroken = new ItemData
        {
            Id = 314, Name = "Mallet (broken)",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Mallet = new ItemData
        {
            Id = 315, Name = "Mallet",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SteelHammer = new ItemData
        {
            Id = 316, Name = "Steel Hammer",
            ValueBuy = 500, ValueSell = 125,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MagicalHammer = new ItemData
        {
            Id = 317, Name = "Magical Hammer",
            ValueBuy = 700, ValueSell = 175,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BattleAx = new ItemData
        {
            Id = 318, Name = "Battle Ax",
            ValueBuy = 800, ValueSell = 200,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData TurtleShell = new ItemData
        {
            Id = 319, Name = "Turtle Shell",
            ValueBuy = 850, ValueSell = 212,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BigBucksHammer = new ItemData
        {
            Id = 320, Name = "Big Bucks Hammer",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData FrozenTuna = new ItemData
        {
            Id = 321, Name = "Frozen Tuna",
            ValueBuy = 400, ValueSell = 100,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData GaiaHammer = new ItemData
        {
            Id = 322, Name = "Gaia Hammer",
            ValueBuy = 900, ValueSell = 225,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData LastJudgement = new ItemData
        {
            Id = 323, Name = "Last Judgement",
            ValueBuy = 1500, ValueSell = 375,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData TallHammer = new ItemData
        {
            Id = 324, Name = "Tall Hammer",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SatanSAx = new ItemData
        {
            Id = 325, Name = "Satan's Ax",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item326 = new ItemData
        {
            Id = 326, Name = "glitched weapon",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData PlateHammer = new ItemData
        {
            Id = 327, Name = "Plate Hammer",
            ValueBuy = 850, ValueSell = 212,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData TrialHammer = new ItemData
        {
            Id = 328, Name = "Trial Hammer",
            ValueBuy = 200, ValueSell = 50,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Inferno = new ItemData
        {
            Id = 329, Name = "Inferno",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item330 = new ItemData
        {
            Id = 330, Name = "glitched weapon",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData GoldRingBroken = new ItemData
        {
            Id = 331, Name = "Gold Ring (broken)",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData GoldRing = new ItemData
        {
            Id = 332, Name = "Gold Ring",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BanditSRing = new ItemData
        {
            Id = 333, Name = "Bandit's Ring",
            ValueBuy = 400, ValueSell = 100,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData CrystalRing = new ItemData
        {
            Id = 334, Name = "Crystal Ring",
            ValueBuy = 700, ValueSell = 175,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData PlatinumRing = new ItemData
        {
            Id = 335, Name = "Platinum Ring",
            ValueBuy = 600, ValueSell = 150,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData GoddessRing = new ItemData
        {
            Id = 336, Name = "Goddess Ring",
            ValueBuy = 720, ValueSell = 180,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData FairySRing = new ItemData
        {
            Id = 337, Name = "Fairy's Ring",
            ValueBuy = 800, ValueSell = 200,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData DestructionRing = new ItemData
        {
            Id = 338, Name = "Destruction Ring",
            ValueBuy = 1200, ValueSell = 300,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SatanSRing = new ItemData
        {
            Id = 339, Name = "Satan's Ring",
            ValueBuy = 1400, ValueSell = 350,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData AthenaSArmlet = new ItemData
        {
            Id = 340, Name = "Athena's Armlet",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MobiusRing = new ItemData
        {
            Id = 341, Name = "Mobius Ring",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item342 = new ItemData
        {
            Id = 342, Name = "Glitched Weapon",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Pocklekul = new ItemData
        {
            Id = 343, Name = "Pocklekul",
            ValueBuy = 400, ValueSell = 100,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData ThornArmlet = new ItemData
        {
            Id = 344, Name = "Thorn Armlet",
            ValueBuy = 300, ValueSell = 75,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData SecretArmlet = new ItemData
        {
            Id = 345, Name = "Secret Armlet",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item346 = new ItemData
        {
            Id = 346, Name = "glitched weapon",
            ValueBuy = 720, ValueSell = 180,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData FightingStickBroken = new ItemData
        {
            Id = 347, Name = "Fighting Stick (broken)",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData FightingStick = new ItemData
        {
            Id = 348, Name = "Fighting Stick",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Javelin = new ItemData
        {
            Id = 349, Name = "Javelin",
            ValueBuy = 800, ValueSell = 200,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Halberd = new ItemData
        {
            Id = 350, Name = "Halberd",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData DeSanga = new ItemData
        {
            Id = 351, Name = "DeSanga",
            ValueBuy = 1200, ValueSell = 300,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Scorpion = new ItemData
        {
            Id = 352, Name = "Scorpion",
            ValueBuy = 1500, ValueSell = 375,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Partisan = new ItemData
        {
            Id = 353, Name = "Partisan",
            ValueBuy = 1700, ValueSell = 425,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Mirage = new ItemData
        {
            Id = 354, Name = "Mirage",
            ValueBuy = 2000, ValueSell = 500,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData TerraSword = new ItemData
        {
            Id = 355, Name = "Terra Sword",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData HerculesWrath = new ItemData
        {
            Id = 356, Name = "Hercules' Wrath",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BabelSSpear = new ItemData
        {
            Id = 357, Name = "Babel's Spear",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item358 = new ItemData
        {
            Id = 358, Name = "glitched weapon",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData _5FootNail = new ItemData
        {
            Id = 359, Name = "5 Foot Nail",
            ValueBuy = 300, ValueSell = 75,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Cactus = new ItemData
        {
            Id = 360, Name = "Cactus",
            ValueBuy = 1800, ValueSell = 450,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item361 = new ItemData
        {
            Id = 361, Name = "glitched weapon",
            ValueBuy = 800, ValueSell = 200,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item362 = new ItemData
        {
            Id = 362, Name = "Glitched Weapon",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MachineGunBroken = new ItemData
        {
            Id = 363, Name = "Machine Gun (broken)",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData MachineGun = new ItemData
        {
            Id = 364, Name = "Machine Gun",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Jackal = new ItemData
        {
            Id = 365, Name = "Jackal",
            ValueBuy = 3000, ValueSell = 750,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Launcher = new ItemData
        {
            Id = 366, Name = "Launcher",
            ValueBuy = 3200, ValueSell = 800,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData LauncherV2 = new ItemData
        {
            Id = 367, Name = "Launcher V2",
            ValueBuy = 3300, ValueSell = 825,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData BlessingGun = new ItemData
        {
            Id = 368, Name = "Blessing Gun",
            ValueBuy = 3400, ValueSell = 850,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Skunk = new ItemData
        {
            Id = 369, Name = "Skunk",
            ValueBuy = 4000, ValueSell = 1000,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData GCrusher = new ItemData
        {
            Id = 370, Name = "G Crusher",
            ValueBuy = 5500, ValueSell = 1375,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData HexaBlaster = new ItemData
        {
            Id = 371, Name = "Hexa Blaster",
            ValueBuy = 6500, ValueSell = 1625,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData StarBreaker = new ItemData
        {
            Id = 372, Name = "Star Breaker",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Supernova = new ItemData
        {
            Id = 373, Name = "Supernova",
            ValueBuy = 2, ValueSell = 1,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Snail = new ItemData
        {
            Id = 374, Name = "Snail",
            ValueBuy = 1500, ValueSell = 375,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Swallow = new ItemData
        {
            Id = 375, Name = "Swallow",
            ValueBuy = 3400, ValueSell = 850,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        internal static readonly ItemData Item376 = new ItemData
        {
            Id = 376, Name = "Empty Slot",
            ValueBuy = 1000, ValueSell = 250,
            Class = ItemClass.Weapon, Category = ItemCategory.Weapon,
        };

        // ---- Lookup (built once from the named fields above, no parallel list to maintain) ----
        private static readonly Dictionary<ushort, ItemData> _byId;

        /// <summary>All item records, ordered by ID.</summary>
        internal static readonly IReadOnlyList<ItemData> All;

        static Item()
        {
            List<ItemData> all = typeof(Item)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(ItemData))
                .Select(f => (ItemData)f.GetValue(null))
                .OrderBy(d => d.Id)
                .ToList();
            All = all;
            _byId = new Dictionary<ushort, ItemData>(all.Count);
            foreach (ItemData item in all) _byId[item.Id] = item;
        }

        /// <summary>Looks up an item by ID. Returns false if the ID has no entry.</summary>
        internal static bool TryGetValue(ushort itemId, out ItemData data) =>
            _byId.TryGetValue(itemId, out data);

        /// <summary>Display name for an item ID, or "Unknown(N)" if the ID has no entry.</summary>
        internal static string GetName(ushort itemId) =>
            TryGetValue(itemId, out ItemData item) ? item.Name : $"Unknown({itemId})";
    }

    /// <summary>
    /// Item selection pools for the Chest Randomizer, keyed by item ID. The value is a RARITY weight
    /// 0-100, HIGHER = RARER (50 = baseline). NON-weapon items are seeded from the real game rate
    /// (ItemSetRateList, averaged across dungeons). WEAPONS are ranked by power = max attack +
    /// max magic/3 (WeaponList +0x44/+0x46) RELATIVE TO THE OWNING CHARACTER (ownership +0xA), convex
    /// curve to rarity 40-98 so each character's top weapon is ~98. Osmond is split into two sub-lines
    /// (elemental vs slayer; Machine Gun line = slayer, Launcher line = elemental) so the rapid-fire
    /// machine-gun line is rare per type despite lower raw stats. Chronicle 2 is pinned 99 (exceptional
    /// single rarest). All values are starting points - tune freely. Big pools are weapons only.
    /// </summary>
    internal static class ChestPools
    {
        /// <summary>Small (regular) chest pool, front floors - non-weapon items. Value = RARITY 0-100 (higher = rarer; 50 = baseline), seeded from avg game rate. Tune freely.</summary>
        internal static readonly Dictionary<ushort, int> ChestSmallFront = new()
        {
            { Item.Fire.Id, 50 }, { Item.Ice.Id, 50 }, { Item.Thunder.Id, 50 }, { Item.Wind.Id, 50 },
            { Item.Holy.Id, 50 }, { Item.Attack.Id, 50 }, { Item.Endurance.Id, 50 }, { Item.Speed.Id, 50 },
            { Item.Magic.Id, 50 }, { Item.Garnet.Id, 76 }, { Item.Amethyst.Id, 75 }, { Item.Aquamarine.Id, 76 },
            { Item.Diamond.Id, 75 }, { Item.Emerald.Id, 75 }, { Item.Pearl.Id, 75 }, { Item.Ruby.Id, 75 },
            { Item.Peridot.Id, 76 }, { Item.Sapphire.Id, 75 }, { Item.Opal.Id, 75 }, { Item.Topaz.Id, 76 },
            { Item.Turquoise.Id, 75 }, { Item.Sun.Id, 90 }, { Item.Dinoslayer.Id, 50 }, { Item.UndeadBuster.Id, 50 },
            { Item.SeaKiller.Id, 50 }, { Item.StoneBreaker.Id, 50 }, { Item.PlantBuster.Id, 50 }, { Item.BeastBuster.Id, 50 },
            { Item.SkyHunter.Id, 50 }, { Item.MetalBreaker.Id, 50 }, { Item.MimicBreaker.Id, 50 }, { Item.MageSlayer.Id, 50 },
            { Item.AntiFreezeAmulet.Id, 50 }, { Item.AntiCurseAmulet.Id, 50 }, { Item.AntiGooAmulet.Id, 50 }, { Item.AntidoteAmulet.Id, 50 },
            { Item.FluffyDoughnut.Id, 50 }, { Item.FishCandy.Id, 50 }, { Item.GrassCake.Id, 50 }, { Item.WitchParfait.Id, 50 },
            { Item.ScorpionJerky.Id, 50 }, { Item.CarrotCookie.Id, 50 }, { Item.RegularWater.Id, 50 }, { Item.TastyWater.Id, 50 },
            { Item.PremiumWater.Id, 50 }, { Item.Bread.Id, 50 }, { Item.PremiumChicken.Id, 50 }, { Item.StaminaDrink.Id, 50 },
            { Item.AntidoteDrink.Id, 50 }, { Item.HolyWater.Id, 50 }, { Item.Soap.Id, 50 }, { Item.MightyHealing.Id, 50 },
            { Item.Cheese.Id, 50 }, { Item.Bomb.Id, 50 }, { Item.Stone.Id, 50 }, { Item.FireGem.Id, 50 },
            { Item.IceGem.Id, 50 }, { Item.ThunderGem.Id, 50 }, { Item.WindGem.Id, 50 }, { Item.HolyGem.Id, 50 },
            { Item.ThrobbingCherry.Id, 50 }, { Item.GooeyPeach.Id, 50 }, { Item.BombNuts.Id, 50 }, { Item.PoisonousApple.Id, 50 },
            { Item.MellowBanana.Id, 50 }, { Item.MedusaPowder.Id, 50 }, { Item.HardeningPowder.Id, 50 }, { Item.WarpPowder.Id, 50 },
            { Item.StandInPowder.Id, 50 }, { Item.EscapePowder.Id, 50 }, { Item.RevivalPowder.Id, 50 }, { Item.RepairPowder.Id, 50 },
            { Item.PowerupPowder.Id, 90 }, { Item.FruitOfEden.Id, 50 }, { Item.AutoRepairPowder.Id, 50 }, { Item.Carrot.Id, 50 },
            { Item.PotatoCake.Id, 50 }, { Item.Minon.Id, 50 }, { Item.Battan.Id, 50 }, { Item.PetiteFish.Id, 50 },
            { Item.Evy.Id, 50 }, { Item.Mimi.Id, 50 }, { Item.Prickly.Id, 50 }, { Item.SunDew.Id, 50 },
            { Item.OintmentLeaf.Id, 50 },
        };

        /// <summary>Small (regular) chest pool, back floors. Starts identical to ChestSmallFront; tune independently.</summary>
        internal static readonly Dictionary<ushort, int> ChestSmallBack = new()
        {
            { Item.Fire.Id, 50 }, { Item.Ice.Id, 50 }, { Item.Thunder.Id, 50 }, { Item.Wind.Id, 50 },
            { Item.Holy.Id, 50 }, { Item.Attack.Id, 50 }, { Item.Endurance.Id, 50 }, { Item.Speed.Id, 50 },
            { Item.Magic.Id, 50 }, { Item.Garnet.Id, 76 }, { Item.Amethyst.Id, 75 }, { Item.Aquamarine.Id, 76 },
            { Item.Diamond.Id, 75 }, { Item.Emerald.Id, 75 }, { Item.Pearl.Id, 75 }, { Item.Ruby.Id, 75 },
            { Item.Peridot.Id, 76 }, { Item.Sapphire.Id, 75 }, { Item.Opal.Id, 75 }, { Item.Topaz.Id, 76 },
            { Item.Turquoise.Id, 75 }, { Item.Sun.Id, 90 }, { Item.Dinoslayer.Id, 50 }, { Item.UndeadBuster.Id, 50 },
            { Item.SeaKiller.Id, 50 }, { Item.StoneBreaker.Id, 50 }, { Item.PlantBuster.Id, 50 }, { Item.BeastBuster.Id, 50 },
            { Item.SkyHunter.Id, 50 }, { Item.MetalBreaker.Id, 50 }, { Item.MimicBreaker.Id, 50 }, { Item.MageSlayer.Id, 50 },
            { Item.AntiFreezeAmulet.Id, 50 }, { Item.AntiCurseAmulet.Id, 50 }, { Item.AntiGooAmulet.Id, 50 }, { Item.AntidoteAmulet.Id, 50 },
            { Item.FluffyDoughnut.Id, 50 }, { Item.FishCandy.Id, 50 }, { Item.GrassCake.Id, 50 }, { Item.WitchParfait.Id, 50 },
            { Item.ScorpionJerky.Id, 50 }, { Item.CarrotCookie.Id, 50 }, { Item.RegularWater.Id, 50 }, { Item.TastyWater.Id, 50 },
            { Item.PremiumWater.Id, 50 }, { Item.Bread.Id, 50 }, { Item.PremiumChicken.Id, 50 }, { Item.StaminaDrink.Id, 50 },
            { Item.AntidoteDrink.Id, 50 }, { Item.HolyWater.Id, 50 }, { Item.Soap.Id, 50 }, { Item.MightyHealing.Id, 50 },
            { Item.Cheese.Id, 50 }, { Item.Bomb.Id, 50 }, { Item.Stone.Id, 50 }, { Item.FireGem.Id, 50 },
            { Item.IceGem.Id, 50 }, { Item.ThunderGem.Id, 50 }, { Item.WindGem.Id, 50 }, { Item.HolyGem.Id, 50 },
            { Item.ThrobbingCherry.Id, 50 }, { Item.GooeyPeach.Id, 50 }, { Item.BombNuts.Id, 50 }, { Item.PoisonousApple.Id, 50 },
            { Item.MellowBanana.Id, 50 }, { Item.MedusaPowder.Id, 50 }, { Item.HardeningPowder.Id, 50 }, { Item.WarpPowder.Id, 50 },
            { Item.StandInPowder.Id, 50 }, { Item.EscapePowder.Id, 50 }, { Item.RevivalPowder.Id, 50 }, { Item.RepairPowder.Id, 50 },
            { Item.PowerupPowder.Id, 90 }, { Item.FruitOfEden.Id, 50 }, { Item.AutoRepairPowder.Id, 50 }, { Item.Carrot.Id, 50 },
            { Item.PotatoCake.Id, 50 }, { Item.Minon.Id, 50 }, { Item.Battan.Id, 50 }, { Item.PetiteFish.Id, 50 },
            { Item.Evy.Id, 50 }, { Item.Mimi.Id, 50 }, { Item.Prickly.Id, 50 }, { Item.SunDew.Id, 50 },
            { Item.OintmentLeaf.Id, 50 },
        };

        /// <summary>Big (large) chest pool, front floors - weapons only. RARITY = per-character rank of max attack + max magic/3 (top of each line ~98); Osmond split into elemental/slayer sub-lines; Chronicle 2 pinned 99.</summary>
        internal static readonly Dictionary<ushort, int> ChestBigFront = new()
        {
            { Item.Baselard.Id, 41 }, { Item.Gladius.Id, 42 }, { Item.WiseOwlSword.Id, 44 }, { Item.Crysknife.Id, 43 },
            { Item.AntiqueSword.Id, 62 }, { Item.BusterSword.Id, 47 }, { Item.KitchenKnife.Id, 41 }, { Item.Tsukikage.Id, 58 },
            { Item.SunSword.Id, 78 }, { Item.SerpentSword.Id, 56 }, { Item.MachoSword.Id, 95 }, { Item.Shamshir.Id, 45 },
            { Item.HeavenSCloud.Id, 82 }, { Item.LambSSword.Id, 65 }, { Item.DarkCloud.Id, 85 }, { Item.BraveArk.Id, 69 },
            { Item.BigBang.Id, 90 }, { Item.AtlamilliaSword.Id, 87 }, { Item.MardanEins.Id, 97 }, { Item.MardanTwei.Id, 98 },
            { Item.AriseMardan.Id, 99 }, { Item.AgaSSword.Id, 74 }, { Item.Evilcise.Id, 67 }, { Item.SmallSword.Id, 51 },
            { Item.SandBreaker.Id, 52 }, { Item.DrainSeeker.Id, 76 }, { Item.Chopper.Id, 50 }, { Item.Choora.Id, 61 },
            { Item.Claymore.Id, 53 }, { Item.Maneater.Id, 70 }, { Item.BoneRapier.Id, 46 }, { Item.Sax.Id, 48 },
            { Item._7BranchSword.Id, 72 }, { Item.Dusack.Id, 64 }, { Item.CrossHinder.Id, 80 }, { Item._7thHeaven.Id, 94 },
            { Item.SwordOfZeus.Id, 92 }, { Item.ChronicleSword.Id, 98 }, { Item.Chronicle2.Id, 99 },
            { Item.SteelSlingshot.Id, 43 }, { Item.BanditSlingshot.Id, 49 }, { Item.Steve.Id, 56 }, { Item.BoneSlingshot.Id, 46 },
            { Item.Hardshooter.Id, 61 }, { Item.DoubleImpact.Id, 65 }, { Item.DragonSY.Id, 75 }, { Item.DivineBeastTitle.Id, 86 },
            { Item.AngelShooter.Id, 92 }, { Item.Flamingo.Id, 52 }, { Item.Matador.Id, 70 }, { Item.SuperSteve.Id, 80 },
            { Item.AngelGear.Id, 98 },
            { Item.SteelHammer.Id, 46 }, { Item.MagicalHammer.Id, 70 }, { Item.BattleAx.Id, 65 }, { Item.TurtleShell.Id, 49 },
            { Item.BigBucksHammer.Id, 52 }, { Item.FrozenTuna.Id, 43 }, { Item.GaiaHammer.Id, 80 }, { Item.LastJudgement.Id, 75 },
            { Item.TallHammer.Id, 92 }, { Item.SatanSAx.Id, 86 }, { Item.PlateHammer.Id, 61 }, { Item.TrialHammer.Id, 56 },
            { Item.Inferno.Id, 98 },
            { Item.BanditSRing.Id, 43 }, { Item.CrystalRing.Id, 63 }, { Item.PlatinumRing.Id, 46 }, { Item.GoddessRing.Id, 73 },
            { Item.FairySRing.Id, 58 }, { Item.DestructionRing.Id, 68 }, { Item.SatanSRing.Id, 79 }, { Item.AthenaSArmlet.Id, 91 },
            { Item.MobiusRing.Id, 85 },  { Item.Pocklekul.Id, 54 }, { Item.ThornArmlet.Id, 50 }, { Item.SecretArmlet.Id, 98 },
            { Item.Javelin.Id, 51 }, { Item.Halberd.Id, 44 }, { Item.DeSanga.Id, 66 }, { Item.Scorpion.Id, 61 },
            { Item.Partisan.Id, 56 }, { Item.Mirage.Id, 72 }, { Item.TerraSword.Id, 84 }, { Item.HerculesWrath.Id, 91 },
            { Item.BabelSSpear.Id, 98 }, { Item._5FootNail.Id, 47 }, { Item.Cactus.Id, 78 },
            { Item.Jackal.Id, 55 }, { Item.BlessingGun.Id, 44 }, { Item.Skunk.Id, 72 }, { Item.GCrusher.Id, 82 },
            { Item.HexaBlaster.Id, 84 }, { Item.StarBreaker.Id, 98 }, { Item.Supernova.Id, 98 }, { Item.Snail.Id, 61 }, { Item.Swallow.Id, 67 },
        };

        /// <summary>Big (large) chest pool, back floors. Starts identical to ChestBigFront; tune independently.</summary>
        internal static readonly Dictionary<ushort, int> ChestBigBack = new()
        {
            { Item.Baselard.Id, 41 }, { Item.Gladius.Id, 42 }, { Item.WiseOwlSword.Id, 44 }, { Item.Crysknife.Id, 43 },
            { Item.AntiqueSword.Id, 62 }, { Item.BusterSword.Id, 47 }, { Item.KitchenKnife.Id, 41 }, { Item.Tsukikage.Id, 58 },
            { Item.SunSword.Id, 78 }, { Item.SerpentSword.Id, 56 }, { Item.MachoSword.Id, 95 }, { Item.Shamshir.Id, 45 },
            { Item.HeavenSCloud.Id, 82 }, { Item.LambSSword.Id, 65 }, { Item.DarkCloud.Id, 85 }, { Item.BraveArk.Id, 69 },
            { Item.BigBang.Id, 90 }, { Item.AtlamilliaSword.Id, 87 }, { Item.MardanEins.Id, 97 }, { Item.MardanTwei.Id, 98 },
            { Item.AriseMardan.Id, 99 }, { Item.AgaSSword.Id, 74 }, { Item.Evilcise.Id, 67 }, { Item.SmallSword.Id, 51 },
            { Item.SandBreaker.Id, 52 }, { Item.DrainSeeker.Id, 76 }, { Item.Chopper.Id, 50 }, { Item.Choora.Id, 61 },
            { Item.Claymore.Id, 53 }, { Item.Maneater.Id, 70 }, { Item.BoneRapier.Id, 46 }, { Item.Sax.Id, 48 },
            { Item._7BranchSword.Id, 72 }, { Item.Dusack.Id, 64 }, { Item.CrossHinder.Id, 80 }, { Item._7thHeaven.Id, 94 },
            { Item.SwordOfZeus.Id, 92 }, { Item.ChronicleSword.Id, 98 }, { Item.Chronicle2.Id, 99 },
            { Item.SteelSlingshot.Id, 43 }, { Item.BanditSlingshot.Id, 49 }, { Item.Steve.Id, 56 }, { Item.BoneSlingshot.Id, 46 },
            { Item.Hardshooter.Id, 61 }, { Item.DoubleImpact.Id, 65 }, { Item.DragonSY.Id, 75 }, { Item.DivineBeastTitle.Id, 86 },
            { Item.AngelShooter.Id, 92 }, { Item.Flamingo.Id, 52 }, { Item.Matador.Id, 70 }, { Item.SuperSteve.Id, 80 },
            { Item.AngelGear.Id, 98 },
            { Item.SteelHammer.Id, 46 }, { Item.MagicalHammer.Id, 70 }, { Item.BattleAx.Id, 65 }, { Item.TurtleShell.Id, 49 },
            { Item.BigBucksHammer.Id, 52 }, { Item.FrozenTuna.Id, 43 }, { Item.GaiaHammer.Id, 80 }, { Item.LastJudgement.Id, 75 },
            { Item.TallHammer.Id, 92 }, { Item.SatanSAx.Id, 86 }, { Item.PlateHammer.Id, 61 }, { Item.TrialHammer.Id, 56 },
            { Item.Inferno.Id, 98 },
            { Item.BanditSRing.Id, 43 }, { Item.CrystalRing.Id, 63 }, { Item.PlatinumRing.Id, 46 }, { Item.GoddessRing.Id, 73 },
            { Item.FairySRing.Id, 58 }, { Item.DestructionRing.Id, 68 }, { Item.SatanSRing.Id, 79 }, { Item.AthenaSArmlet.Id, 91 },
            { Item.MobiusRing.Id, 85 },  { Item.Pocklekul.Id, 54 }, { Item.ThornArmlet.Id, 50 }, { Item.SecretArmlet.Id, 98 },
            { Item.Javelin.Id, 51 }, { Item.Halberd.Id, 44 }, { Item.DeSanga.Id, 66 }, { Item.Scorpion.Id, 61 },
            { Item.Partisan.Id, 56 }, { Item.Mirage.Id, 72 }, { Item.TerraSword.Id, 84 }, { Item.HerculesWrath.Id, 91 },
            { Item.BabelSSpear.Id, 98 }, { Item._5FootNail.Id, 47 }, { Item.Cactus.Id, 78 },
            { Item.Jackal.Id, 55 }, { Item.BlessingGun.Id, 44 }, { Item.Skunk.Id, 72 }, { Item.GCrusher.Id, 82 },
            { Item.HexaBlaster.Id, 84 }, { Item.StarBreaker.Id, 98 }, { Item.Supernova.Id, 98 }, { Item.Snail.Id, 61 }, { Item.Swallow.Id, 67 },
        };

        /// <summary>Clown big box pool - mix of mid weapons + consumables + amulets (vanilla PieroItemList). Weapons by per-character stat rank; others by game rate.</summary>
        internal static readonly Dictionary<ushort, int> ClownBig = new()
        {
            { Item.AntiFreezeAmulet.Id, 50 }, { Item.AntiCurseAmulet.Id, 50 }, { Item.AntiGooAmulet.Id, 50 }, { Item.AntidoteAmulet.Id, 50 },
            { Item.TastyWater.Id, 50 }, { Item.PremiumWater.Id, 50 }, { Item.Bread.Id, 50 }, { Item.PremiumChicken.Id, 50 },
            { Item.Cheese.Id, 50 }, { Item.Baselard.Id, 41 }, { Item.Gladius.Id, 42 }, { Item.WiseOwlSword.Id, 44 },
            { Item.Crysknife.Id, 43 }, { Item.BusterSword.Id, 47 }, { Item.KitchenKnife.Id, 41 }, { Item.Tsukikage.Id, 58 },
            { Item.Shamshir.Id, 45 }, { Item.SmallSword.Id, 51 }, { Item.SandBreaker.Id, 52 }, { Item.Chopper.Id, 50 },
            { Item.Choora.Id, 61 }, { Item.BoneRapier.Id, 46 }, { Item.Sax.Id, 48 }, { Item.SteelHammer.Id, 46 },
            { Item.MagicalHammer.Id, 70 }, { Item.BattleAx.Id, 65 }, { Item.TurtleShell.Id, 49 }, { Item.BigBucksHammer.Id, 52 },
            { Item.GaiaHammer.Id, 80 }, { Item.LastJudgement.Id, 75 }, { Item.TrialHammer.Id, 56 }, { Item.Javelin.Id, 51 },
            { Item.Halberd.Id, 44 }, { Item.DeSanga.Id, 66 }, { Item._5FootNail.Id, 47 }, { Item.Cactus.Id, 78 },
        };

        /// <summary>Clown small box pool - spheres/gems/cheap-weapon mix (vanilla PieroItemList) with BAIT limited to Carrot, Potato Cake, Poisonous Apple, Mimi. Weapons by per-character stat rank; others by game rate.</summary>
        internal static readonly Dictionary<ushort, int> ClownSmall = new()
        {
            { Item.Attack.Id, 50 }, { Item.Endurance.Id, 50 }, { Item.Speed.Id, 50 }, { Item.Magic.Id, 50 },
            { Item.Garnet.Id, 76 }, { Item.Amethyst.Id, 75 }, { Item.Aquamarine.Id, 76 }, { Item.Diamond.Id, 75 },
            { Item.Emerald.Id, 75 }, { Item.Pearl.Id, 75 }, { Item.Ruby.Id, 75 }, { Item.Peridot.Id, 76 },
            { Item.Sapphire.Id, 75 }, { Item.Opal.Id, 75 }, { Item.Topaz.Id, 76 }, { Item.Turquoise.Id, 75 },
            { Item.PoisonousApple.Id, 50 }, { Item.Carrot.Id, 50 }, { Item.PotatoCake.Id, 50 }, { Item.Mimi.Id, 50 },
            { Item.TramOil.Id, 50 }, { Item.SunDew.Id, 50 }, { Item.SteelSlingshot.Id, 43 }, { Item.BanditSlingshot.Id, 49 },
            { Item.Steve.Id, 56 }, { Item.BoneSlingshot.Id, 46 }, { Item.Hardshooter.Id, 61 }, { Item.BanditSRing.Id, 43 },
            { Item.CrystalRing.Id, 63 }, { Item.PlatinumRing.Id, 46 }, { Item.FairySRing.Id, 58 }, { Item.Pocklekul.Id, 54 },
            { Item.ThornArmlet.Id, 50 }, { Item.Jackal.Id, 55 }, { Item.BlessingGun.Id, 44 }, { Item.Skunk.Id, 72 },
            { Item.GCrusher.Id, 82 }, { Item.HexaBlaster.Id, 84 }, { Item.Snail.Id, 61 }, { Item.Swallow.Id, 67 },
        };
    }
}
