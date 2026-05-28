using System.Collections.Generic;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    public partial class MiniBoss
    {
        internal struct WeaponBoostData
        {
            public int WeaponId;
            public ushort Attack;
            public ushort Endurance;
            public ushort Speed;
            public ushort Magic;
            public ushort Whp;
            public byte Fire;
            public byte Ice;
            public byte Thunder;
            public byte Wind;
            public byte Holy;
            public byte AntiDragon;
            public byte AntiUndead;
            public byte AntiMarine;
            public byte AntiRock;
            public byte AntiPlant;
            public byte AntiBeast;
            public byte AntiSky;
            public byte AntiMimic;
            public byte AntiMage;
            public byte Special1;
            public byte Special2;
        }

        // =====================================================================
        // Standard loot tables (all dungeons)
        // =====================================================================

        static int[] attachmentsTableLucky =
        {
            Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond,
            Items.emerald, Items.pearl, Items.ruby, Items.peridot,
            Items.sapphire, Items.opal, Items.topaz, Items.turquoise,
        };

        static int[] attachmentsTableUnlucky =
        {
            // Elements
            Items.fire, Items.ice, Items.thunder, Items.wind, Items.holy,
            // Stats
            Items.attack, Items.endurance, Items.speed, Items.magic,
            // Anti-stats
            Items.dragonslayer, Items.undeadbuster, Items.seakiller, Items.stonebreaker,
            Items.plantbuster, Items.beastbuster, Items.skyhunter, Items.metalbreaker,
            Items.mimicbreaker, Items.mageslayer,
        };

        static int[] itemTableLucky   = { Items.poweruppowder, Items.autorepairpowder };
        static int[] itemTableUnlucky = { Items.staminadrink, Items.dransfeather };

        // =====================================================================
        // Dungeon 0: Divine Beast Cavern
        // =====================================================================

        static readonly Dictionary<ushort, int[]> dbcFlavorRareDrops = new Dictionary<ushort, int[]>
        {
            { 1,   new[] { Items.gladius  } },   // Master Jacket → Gladius
            { 303, new[] { Items.steve    } },   // Statue Dog    → Steve
            { 301, new[] { Items.evilcise } },   // Yammich       → Evilcise
        };

        static readonly Dictionary<ushort, int[]> dbcFlavorDrops = new Dictionary<ushort, int[]>
        {
            { 77,  new[] { Items.diamond                          } }, // Rockanoff        → Diamond
            { 59,  new[] { Items.ruby,        Items.garnet        } }, // Dragon           → Ruby or Garnet
            { 3,   new[] { Items.bonerapier,  Items.boneslingshot } }, // Skeleton Soldier → Bone Rapier or Bone Slingshot
            { 1,   new[] { Items.gladius                          } }, // Master Jacket    → Gladius
            { 5,   new[] { Items.steelslingshot                   } }, // Statue           → Steel Slingshot
            { 303, new[] { Items.turquoise                        } }, // Statue Dog       → Turquoise
            { 6,   new[] { Items.topaz                            } }, // Dasher           → Topaz
            { 304, new[] { Items.opal                             } }, // Opar             → Opal
            { 301, new[] { Items.amethyst                         } }, // Yammich          → Amethyst
            { 34,  new[] { Items.goldbullion, Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // King Mimic (DBC) → Gold Bar or any gem
            { 35,  new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise }                    }, // Mimic (DBC)      → any gem
        };

        static readonly Dictionary<int, WeaponBoostData> dbcWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            { Items.gladius, new WeaponBoostData { WeaponId = Items.gladius, Attack = 15, Holy = 50, AntiUndead = 50 } },
        };

        // =====================================================================
        // Dungeon 1: Wise Owl Forest
        // =====================================================================

        static readonly Dictionary<ushort, int[]> wofFlavorRareDrops = new Dictionary<ushort, int[]>
        {
            { 12,  new[] { Items.trialhammer     } },   // Earth Digger → Trial Hammer
            { 19,  new[] { Items.banditslingshot } },   // Friday       → Bandit Slingshot
            { 10,  new[] { Items.steve           } },   // Halloween    → Steve
            { 15,  new[] { Items.banditslingshot } },   // Monday       → Bandit Slingshot
            { 20,  new[] { Items.banditslingshot } },   // Saturday     → Bandit Slingshot
            { 14,  new[] { Items.banditslingshot } },   // Sunday       → Bandit Slingshot
            { 18,  new[] { Items.banditslingshot } },   // Thursday     → Bandit Slingshot
            { 16,  new[] { Items.banditslingshot } },   // Tuesday      → Bandit Slingshot
            { 17,  new[] { Items.banditslingshot } },   // Wednesday    → Bandit Slingshot
            { 7,   new[] { Items.lambsswordnormal} },   // Werewolf     → Lamb's Sword
        };

        static readonly Dictionary<ushort, int[]> wofFlavorDrops = new Dictionary<ushort, int[]>
        {
            { 12,  new[] { Items.platehammer       } }, // Earth Digger → Plate Hammer
            { 19,  new[] { Items.autorepairpowder  } }, // Friday       → Auto Repair Powder
            { 305, new[] { Items.sapphire, Items.flamingo } }, // Haley Holey → Sapphire or Flamingo
            { 306, new[] { Items.steelhammer       } }, // King Prickly → Steel Hammer
            { 15,  new[] { Items.autorepairpowder  } }, // Monday       → Auto Repair Powder
            { 20,  new[] { Items.autorepairpowder  } }, // Saturday     → Auto Repair Powder
            { 14,  new[] { Items.autorepairpowder  } }, // Sunday       → Auto Repair Powder
            { 18,  new[] { Items.autorepairpowder  } }, // Thursday     → Auto Repair Powder
            { 16,  new[] { Items.autorepairpowder  } }, // Tuesday      → Auto Repair Powder
            { 17,  new[] { Items.autorepairpowder  } }, // Wednesday    → Auto Repair Powder
            { 78,  new[] { Items.goldbullion, Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // King Mimic (WOF) → Gold Bar or any gem
            { 79,  new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise }                    }, // Mimic (WOF)      → any gem
        };

        static readonly Dictionary<int, WeaponBoostData> wofWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            { Items.banditslingshot,  new WeaponBoostData { WeaponId = Items.banditslingshot,  Attack = 35, Wind = 40, AntiMimic = 40 } },
            { Items.lambsswordnormal, new WeaponBoostData { WeaponId = Items.lambsswordnormal, Attack = 60, Magic = 30, Whp = 99 } },
        };

        // =====================================================================
        // Dungeon 2: Shipwreck
        // =====================================================================

        static readonly Dictionary<ushort, int[]> shipFlavorRareDrops = new Dictionary<ushort, int[]>
        {
            { 26, new[] { Items.serpentsword } },   // Auntie Medu    → Serpent Sword
            { 27, new[] { Items.goldbullion  } },   // Captain        → Gold Bar
            { 28, new[] { Items.dusack       } },   // Corcea         → Dusack
            { 75, new[] { Items.smallsword   } },   // Mask of Prajna → Small Sword
        };

        static readonly Dictionary<ushort, int[]> shipFlavorDrops = new Dictionary<ushort, int[]>
        {
            { 26, new[] { Items.turquoise,     Items.garnet         } }, // Auntie Medu    → Turquoise or Garnet
            { 27, new[] { Items.flamingo                            } }, // Captain        → Flamingo
            { 28, new[] { Items.chopper                             } }, // Corcea         → Chopper
            { 68, new[] { Items.thornarmlet                         } }, // Cursed Rose    → Thorn Armlet
            { 23, new[] { Items.pearl                               } }, // Gunny          → Pearl
            { 24, new[] { Items.frozentuna,    Items.aquamarine     } }, // Gyon           → Frozen Tuna or Aquamarine
            { 75, new[] { Items.amethyst                            } }, // Mask of Prajna → Amethyst
            { 25, new[] { Items.steelhammer,   Items.poweruppowder  } }, // Pirate's Chariot → Steel Hammer or Powerup Powder
            { 85, new[] { Items.crystalring                         } }, // Sam            → Crystal Ring
            { 80, new[] { Items.goldbullion, Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // King Mimic (Ship) → Gold Bar or any gem
            { 81, new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise }                    }, // Mimic (Ship)      → any gem
        };

        static readonly Dictionary<int, WeaponBoostData> shipWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            { Items.smallsword, new WeaponBoostData { WeaponId = Items.smallsword, Attack = 40, Thunder = 40, AntiMage = 40 } },
        };

        // =====================================================================
        // Dungeon 3: Sun & Moon
        // =====================================================================

        static readonly Dictionary<ushort, int[]> sunFlavorRareDrops = new Dictionary<ushort, int[]>
        {
            { 73, new[] { Items.dragonsy                       } }, // Blue Dragon → Dragon's Y
            { 32, new[] { Items.cactus                         } }, // Dune        → Cactus
            { 30, new[] { Items.sunsword,   Items.tsukikage    } }, // Golem       → Sun Sword or Tsukikage
            { 50, new[] { Items.claymore                       } }, // Mummy       → Claymore
            { 64, new[] { Items.platinumring                   } }, // Steel Giant → Platinum Ring
        };

        static readonly Dictionary<ushort, int[]> sunFlavorDrops = new Dictionary<ushort, int[]>
        {
            { 73, new[] { Items.amethyst, Items.aquamarine, Items.turquoise } }, // Blue Dragon   → Amethyst, Aquamarine, or Turquoise
            { 49, new[] { Items.poweruppowder                               } }, // Bomber Head   → Powerup Powder
            { 71, new[] { Items.opal                                        } }, // Crabby Hermit → Opal
            { 32, new[] { Items.poweruppowder                               } }, // Dune          → Powerup Powder
            { 30, new[] { Items.autorepairpowder                            } }, // Golem         → Auto Repair Powder
            { 31, new[] { Items.blessinggun                                 } }, // Mr. Blare     → Blessing Gun
            { 50, new[] { Items.revivalpowder, Items.peridot                } }, // Mummy         → Revival Powder or Peridot
            { 64, new[] { Items.opal,          Items.diamond                } }, // Steel Giant   → Opal or Diamond
            { 36, new[] { Items.goldbullion, Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // King Mimic (SMT) → Gold Bar or any gem
            { 37, new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise }                    }, // Mimic (SMT)      → any gem
        };

        static readonly Dictionary<int, WeaponBoostData> sunWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            { Items.platinumring, new WeaponBoostData { WeaponId = Items.platinumring, Attack = 30, Ice = 40, AntiRock = 40, Special2 = 0x02 } },
        };

        // =====================================================================
        // Dungeon 4: Moon Sea
        // =====================================================================

        static readonly Dictionary<ushort, int[]> moonFlavorRareDrops = new Dictionary<ushort, int[]>
        {
            { 76, new[] { Items.tsukikage, Items.mirage  } },  // Crescent Baron → Tsukikage or Mirage
            { 62, new[] { Items.satansring               } },  // Hell Pockle    → Satan's Ring
            { 66, new[] { Items.trialhammer              } },  // Moon Digger    → Trial Hammer
            { 72, new[] { Items.frozentuna               } },  // Space Gyon     → Frozen Tuna
            { 33, new[] { Items.gaiahammer               } },  // Titan          → Gaia Hammer
            { 70, new[] { Items.blessinggun              } },  // Vulcan         → Blessing Gun
            { 56, new[] { Items.desanga                  } },  // White Fang     → De Sanga
        };

        static readonly Dictionary<ushort, int[]> moonFlavorDrops = new Dictionary<ushort, int[]>
        {
            { 40, new[] { Items.swallow,      Items.fivefootnail                                                                                             } }, // Arthur         → Swallow or 5 Foot Nail
            { 76, new[] { Items.pearl                                                                                                                        } }, // Crescent Baron → Pearl
            { 62, new[] { Items.pocklekul                                                                                                                    } }, // Hell Pockle    → Pocklekul (+ Bandit boost)
            { 57, new[] { Items.poweruppowder                                                                                                                } }, // Moon Bug       → Powerup Powder
            { 66, new[] { Items.magicalhammer                                                                                                                } }, // Moon Digger    → Magical Hammer
            { 72, new[] { Items.javelin                                                                                                                      } }, // Space Gyon     → Javelin
            { 33, new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // Titan → any gem
            { 70, new[] { Items.peridot,     Items.garnet,     Items.topaz,   Items.diamond                                                                 } }, // Vulcan         → Peridot, Garnet, Topaz, or Diamond
            { 56, new[] { Items.topaz                                                                                                                        } }, // White Fang     → Topaz
            { 38, new[] { Items.goldbullion, Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // King Mimic (MS)  → Gold Bar or any gem
            { 39, new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise }                    }, // Mimic (MS)       → any gem
        };

        static readonly Dictionary<int, WeaponBoostData> moonWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            // Frozen Tuna: BigBucks (sp1 0x02) + Fragile (sp2 0x01)
            { Items.frozentuna,  new WeaponBoostData { WeaponId = Items.frozentuna,  Whp = 99, Endurance = 99, Ice = 99, AntiMarine = 99, Special1 = 0x02, Special2 = 0x01 } },
            // Blessing Gun: 50 atk, 30 mp, 40 fire, 40 anti-dragon, 20 anti-plant
            { Items.blessinggun, new WeaponBoostData { WeaponId = Items.blessinggun, Attack = 50, Magic = 30, Fire = 40, AntiDragon = 40, AntiPlant = 20 } },
            // De Sanga: Drain ability                                  Special2: 0x04 = Drain
            { Items.desanga,     new WeaponBoostData { WeaponId = Items.desanga,     Special2 = 0x04 } },
        };

        static readonly Dictionary<int, WeaponBoostData> moonFlavorWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            // Pocklekul: Bandit (Steal) ability                       Special1: 0x80 = Steal
            { Items.pocklekul, new WeaponBoostData { WeaponId = Items.pocklekul, Special1 = 0x80 } },
        };

        static readonly Dictionary<int, WeaponBoostData> emptyWeaponBoosts = new Dictionary<int, WeaponBoostData>();

        // =====================================================================
        // Dungeon 5: Gallery of Time
        // =====================================================================

        static readonly Dictionary<ushort, int[]> galFlavorRareDrops = new Dictionary<ushort, int[]>
        {
            { 74, new[] { Items.dragonsy      } },  // Black Dragon → Dragon's Y
            { 45, new[] { Items.cactus        } },  // Club         → Cactus
            { 67, new[] { Items.thornarmlet   } },  // Dark Flower  → Thorn Armlet
            { 46, new[] { Items.bigbang       } },  // Diamond      → Big Bang
            { 44, new[] { Items.angelshooter  } },  // Heart        → Angel Shooter
            { 48, new[] { Items.supersteve    } },  // Joker        → Super Steve
            { 47, new[] { Items.darkcloud     } },  // Spade        → Dark Cloud (weapon)
        };

        static readonly Dictionary<ushort, int[]> galFlavorDrops = new Dictionary<ushort, int[]>
        {
            { 43, new[] { Items.drainseeker                                  } }, // Alexander   → Drain Seeker
            { 69, new[] { Items.trialhammer                                  } }, // Billy       → Trial Hammer (+ Thunder boost)
            { 74, new[] { Items.peridot                                      } }, // Black Dragon → Peridot
            { 65, new[] { Items.aquamarine, Items.amethyst, Items.turquoise  } }, // Blizzard    → Aquamarine, Amethyst, or Turquoise
            { 45, new[] { Items.steelhammer                                  } }, // Club        → Steel Hammer
            { 52, new[] { Items.maneater,   Items.crosshinder                } }, // Curse Dancer → Maneater or Cross Hinder
            { 67, new[] { Items.thornarmlet, Items.sevenbranchsword          } }, // Dark Flower  → Thorn Armlet or 7 Branch Sword
            { 46, new[] { Items.diamond                                      } }, // Diamond     → Diamond (gem)
            { 44, new[] { Items.goddessring                                  } }, // Heart       → Goddess Ring
            { 48, new[] { Items.supersteve                                   } }, // Joker       → Super Steve (no boost on flavor)
            { 47, new[] { Items.braveark                                     } }, // Spade       → Brave Ark
            { 82, new[] { Items.goldbullion, Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // King Mimic (GoT) → Gold Bar or any gem
            { 83, new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise }                    }, // Mimic (GoT)      → any gem
        };

        static readonly Dictionary<int, WeaponBoostData> galWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            // Dragon's Y: 40 thunder, 40 anti-beast, 40 anti-sky (flying)
            { Items.dragonsy,     new WeaponBoostData { WeaponId = Items.dragonsy,     Thunder = 40, AntiBeast = 40, AntiSky = 40 } },
            // Cactus: 99 whp, 99 endurance, 50 wind, 40 anti-plant, 40 anti-marine, Critical
            { Items.cactus,       new WeaponBoostData { WeaponId = Items.cactus,       Whp = 99, Endurance = 99, Wind = 50, AntiPlant = 40, AntiMarine = 40, Special2 = 0x10 } },
            // Thorn Armlet: Abs Up                                    Special2: 0x20 = Abs Up
            { Items.thornarmlet,  new WeaponBoostData { WeaponId = Items.thornarmlet,  Special2 = 0x20 } },
            // Big Bang: 99 whp, Durable                               Special2: 0x02 = Durable
            { Items.bigbang,      new WeaponBoostData { WeaponId = Items.bigbang,      Whp = 99, Special2 = 0x02 } },
            // Angel Shooter: 99 whp, 40 fire, 40 thunder, 40 holy
            { Items.angelshooter, new WeaponBoostData { WeaponId = Items.angelshooter, Whp = 99, Fire = 40, Thunder = 40, Holy = 40 } },
            // Super Steve: 99 whp, 80 atk, 50 mp, Steal + Abs Up
            { Items.supersteve,   new WeaponBoostData { WeaponId = Items.supersteve,   Whp = 99, Attack = 80, Magic = 50, Special1 = 0x80, Special2 = 0x20 } },
            // Dark Cloud: 99 whp, 40 fire, 40 holy, 30 anti-mage, Stop + Drain
            { Items.darkcloud,    new WeaponBoostData { WeaponId = Items.darkcloud,    Whp = 99, Fire = 40, Holy = 40, AntiMage = 30, Special1 = 0x40, Special2 = 0x04 } },
        };

        static readonly Dictionary<int, WeaponBoostData> galFlavorWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            // Trial Hammer (Billy flavor): 50 thunder (Lightning)
            { Items.trialhammer, new WeaponBoostData { WeaponId = Items.trialhammer, Thunder = 50 } },
        };

        // =====================================================================
        // Dungeon 6: Demon Shaft
        // =====================================================================

        static readonly Dictionary<ushort, int[]> dsFlavorRareDrops = new Dictionary<ushort, int[]>
        {
            { 311, new[] { Items.satansring      } }, // Fire Gemron    → Satan's Ring
            { 308, new[] { Items.machosword      } }, // Nikapous        → Macho Sword
            { 319, new[] { Items.satansaxe       } }, // Hornhead        → Satan's Ax
            { 312, new[] { Items.crystalring     } }, // Ice Gemron      → Crystal Ring
            { 313, new[] { Items.trialhammer     } }, // Thunder Gemron  → Trial Hammer
            { 318, new[] { Items.destructionring } }, // Silver Gear     → Destruction Ring
            { 314, new[] { Items.heavenscloud    } }, // Wind Gemron     → Heaven's Cloud
            { 315, new[] { Items.goddessring     } }, // Holy Gemron     → Goddess Ring
        };

        static readonly Dictionary<ushort, int[]> dsFlavorDrops = new Dictionary<ushort, int[]>
        {
            { 311, new[] { Items.sun,    Items.ruby       } }, // Fire Gemron    → Sun or Ruby
            { 319, new[] { Items.matador, Items.bonerapier } }, // Hornhead      → Matador or Bone Rapier
            { 312, new[] { Items.sun,    Items.aquamarine } }, // Ice Gemron     → Sun or Aquamarine
            { 316, new[] { Items.flamingo                 } }, // Bishop Q       → Flamingo
            { 313, new[] { Items.sun,    Items.pearl      } }, // Thunder Gemron → Sun or Pearl
            { 318, new[] { Items.boneslingshot            } }, // Silver Gear    → Bone Slingshot
            { 314, new[] { Items.sun,    Items.sapphire   } }, // Wind Gemron    → Sun or Sapphire
            { 315, new[] { Items.sun,    Items.peridot    } }, // Holy Gemron    → Sun or Peridot
            { 310, new[] { Items.goldbullion, Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise } }, // King Mimic (DS)  → Gold Bar or any gem
            { 309, new[] { Items.garnet, Items.amethyst, Items.aquamarine, Items.diamond, Items.emerald, Items.pearl, Items.ruby, Items.peridot, Items.sapphire, Items.opal, Items.topaz, Items.turquoise }                    }, // Mimic (DS)       → any gem
        };

        static readonly Dictionary<int, WeaponBoostData> dsWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            // Crystal Ring: 50 ice
            { Items.crystalring,  new WeaponBoostData { WeaponId = Items.crystalring,  Ice = 50 } },
            // Trial Hammer: 50 thunder
            { Items.trialhammer,  new WeaponBoostData { WeaponId = Items.trialhammer,  Thunder = 50 } },
            // Heaven's Cloud: 50 wind
            { Items.heavenscloud, new WeaponBoostData { WeaponId = Items.heavenscloud, Wind = 50 } },
        };

        static readonly Dictionary<int, WeaponBoostData> dsFlavorWeaponBoosts = new Dictionary<int, WeaponBoostData>
        {
            // Bishop Q flavor Flamingo: AbsUp (sp2 0x20)
            { Items.flamingo, new WeaponBoostData { WeaponId = Items.flamingo, Special2 = 0x20 } },
        };

        // =====================================================================
        // Pending inventory boost state
        // =====================================================================

        static volatile bool pendingBoostActive = false;
        static WeaponBoostData pendingBoost;
        static Thread boostMonitorThread;

        public static void CancelPendingBoost() => pendingBoostActive = false;
    }
}
