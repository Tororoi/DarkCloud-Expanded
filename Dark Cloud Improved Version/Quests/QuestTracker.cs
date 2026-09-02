using System.Text;

namespace Dark_Cloud_Improved_Version
{
    static class QuestTracker
    {
        // Every address whose value affects the quest display. Polled to detect any change.
        private static readonly int[] watchedAddresses =
        {
            // Monster: available flags
            0x21CE4474, 0x21CE4476, 0x21CE4478, 0x21CE447A,
            // Monster: quest statuses
            0x21CE4402, 0x21CE4407, 0x21CE440C, 0x21CE4411,
            // Monster: kill counters
            0x21CE4405, 0x21CE440A, 0x21CE440F, 0x21CE4414,
            // Fishing: available flags
            0x21CE4475, 0x21CE4477, 0x21CE4479, 0x21CE447B,
            // Fishing: quest statuses
            0x21CE4416, 0x21CE441E, 0x21CE4427, 0x21CE4431,
            // Fishing: fish counters
            0x21CE441A, 0x21CE4422, 0x21CE442B, 0x21CE4435,
            // Item hunt flags
            0x21CE4451, 0x21CE4452, 0x21CE4453, 0x21CE4454,
            // Mayor quest state
            0x21CE4464, 0x21CE4468,
        };

        private static byte[] previousSnapshot;

        // Returns true (and updates the snapshot) if any watched address changed since last call.
        // Always returns true on the first call so the UI is populated immediately.
        public static bool HasStateChanged()
        {
            var current = new byte[watchedAddresses.Length];
            for (int i = 0; i < watchedAddresses.Length; i++)
                current[i] = Memory.ReadByte(watchedAddresses[i]);

            if (previousSnapshot == null)
            {
                previousSnapshot = current;
                return true;
            }

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != previousSnapshot[i])
                {
                    previousSnapshot = current;
                    return true;
                }
            }
            return false;
        }


        private static readonly string[] dungeonNames =
        {
            "Divine Beast Cave", "Wise Owl Forest", "Shipwreck",
            "Sun & Moon Temple", "Moon Sea", "Gallery of Time", "Demon Shaft"
        };

        private static readonly string[][] dungeonEnemies =
        {
            new[] { "Master Jackets", "Dashers", "Mimics", "Dragons" },
            new[] { "Fliflis", "Earth Diggers", "Mimics", "Werewolves" },
            new[] { "Gunnys", "Gyons", "Mimics", "Pirate's Chariots" },
            new[] { "Golems", "Dunes", "Mimics", "Blue Dragons" },
            new[] { "Moon Diggers", "Space Gyons", "Mimics", "Crescent Barons" },
            new[] { "Rash Dashers", "Jokers", "Mimics", "Alexanders" },
            new[] { "Master Jackets", "Dashers", "Mimics", "Dragons" },
        };

        // Fish name arrays per fishing NPC (index matches fishing NPC order: Pike, Pao-Pond, Sam, Devia)
        private static readonly string[][] fishByNPC =
        {
            new[] { "Nilers", "Gummies", "Nonkies", "Gobblers" },
            new[] { "Baku Bakus", "Gobblers", "Tartons", "Umadakaras" },
            new[] { "Bobos", "Kajis", "Piccolys", "Bons", "Hamahamas" },
            new[] { "Negies", "Dens", "Heelas", "Mardan Garayans", "Baron Garayan" },
        };

        private static readonly string[] matatakiWaterfallFish =
            { "Baku Bakus", "Nonkies", "Gummies", "Mardan Garayan", "Baron Garayan" };

        public static string GetActiveQuestsSummary()
        {
            var sb = new StringBuilder();
            bool hasAny = false;

            AppendMonsterQuests(sb, ref hasAny);
            AppendFishingQuests(sb, ref hasAny);
            AppendItemQuests(sb, ref hasAny);
            AppendMayorQuest(sb, ref hasAny);

            return hasAny ? sb.ToString().TrimEnd() : "No active sidequests.";
        }

        private static void AppendMonsterQuests(StringBuilder sb, ref bool hasAny)
        {
            // (name, area, availableFlag, statusAddr, dungIDAddr, enemyNameAddr, counterAddr)
            (string name, string area, int availFlag, int statusAddr, int dungIDAddr, int enemyNameAddr, int counterAddr)[] quests =
            {
                ("Macho",       "Norune Village",   0x21CE4474, 0x21CE4402, 0x21CE4403, 0x21CE4404, 0x21CE4405),
                ("Gob",         "Norune Village",   0x21CE4476, 0x21CE4407, 0x21CE4408, 0x21CE4409, 0x21CE440A),
                ("Jake",        "Norune Village",   0x21CE4478, 0x21CE440C, 0x21CE440D, 0x21CE440E, 0x21CE440F),
                ("Chief Bonka", "Brownboo Village", 0x21CE447A, 0x21CE4411, 0x21CE4412, 0x21CE4413, 0x21CE4414),
            };

            bool headerWritten = false;
            foreach (var q in quests)
            {
                if (Memory.ReadByte(q.availFlag) != 1) continue;
                if (Memory.ReadByte(q.statusAddr) != 1) continue;

                if (!headerWritten) { sb.AppendLine("[Monster Slaying]"); headerWritten = true; }

                int dungID = Memory.ReadByte(q.dungIDAddr);
                int enemyIdx = Memory.ReadByte(q.enemyNameAddr);
                int kills = Memory.ReadByte(q.counterAddr);
                string dungeon = dungID < dungeonNames.Length ? dungeonNames[dungID] : "?";
                string enemy = (dungID < dungeonEnemies.Length && enemyIdx < dungeonEnemies[dungID].Length)
                    ? dungeonEnemies[dungID][enemyIdx] : "?";

                sb.AppendLine($"{q.name}  ({q.area})");
                sb.AppendLine($"  Defeat {enemy} in {dungeon}  ({kills} kills left)");
                hasAny = true;
            }
            if (headerWritten) sb.AppendLine();
        }

        private static void AppendFishingQuests(StringBuilder sb, ref bool hasAny)
        {
            // (name, area, defaultLocation, availFlag, statusAddr, typeAddr, fishNameAddr, remainingAddr, origCountAddr, minAddr, maxAddr, matatakiLocAddr)
            (string name, string area, string location, int availFlag, int statusAddr, int typeAddr, int fishNameAddr, int remainingAddr, int origCountAddr, int minAddr, int maxAddr, int matatakiLocAddr)[] quests =
            {
                ("Pike",  "Norune Village",   "Norune Pond", 0x21CE4475, 0x21CE4416, 0x21CE4417, 0x21CE4418, 0x21CE441A, 0x21CE441D, 0x21CE441B, 0x21CE441C, -1),
                ("Pao",   "Matataki Village", "Matataki",    0x21CE4477, 0x21CE441E, 0x21CE441F, 0x21CE4420, 0x21CE4422, 0x21CE4425, 0x21CE4423, 0x21CE4424, 0x21CE4426),
                ("Sam",   "Queens",           "Queens Sea",  0x21CE4479, 0x21CE4427, 0x21CE4428, 0x21CE4429, 0x21CE442B, 0x21CE442E, 0x21CE442C, 0x21CE442D, -1),
                ("Devia", "Muska Lacka",      "the Oasis",   0x21CE447B, 0x21CE4431, 0x21CE4432, 0x21CE4433, 0x21CE4435, 0x21CE4438, 0x21CE4436, 0x21CE4437, -1),
            };

            bool headerWritten = false;
            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                if (Memory.ReadByte(q.availFlag) != 1) continue;
                if (Memory.ReadByte(q.statusAddr) != 1) continue;

                if (!headerWritten) { sb.AppendLine("[Fishing]"); headerWritten = true; }

                int questType = Memory.ReadByte(q.typeAddr);
                sb.AppendLine($"{q.name}  ({q.area})");

                if (questType == 0)
                {
                    int fishIdx = Memory.ReadByte(q.fishNameAddr);
                    int remaining = Memory.ReadByte(q.remainingAddr);
                    int total = Memory.ReadByte(q.origCountAddr);
                    int caught = total - remaining;
                    string location = q.location;
                    string fish;

                    if (i == 1 && q.matatakiLocAddr != -1)
                    {
                        int matatakiLoc = Memory.ReadByte(q.matatakiLocAddr);
                        if (matatakiLoc == 1)
                        {
                            location = "Matataki Waterfall";
                            fish = fishIdx < matatakiWaterfallFish.Length ? matatakiWaterfallFish[fishIdx] : "?";
                        }
                        else
                        {
                            location = matatakiLoc == 2 ? "Matataki Village" : "Matataki Pond";
                            fish = fishIdx < fishByNPC[i].Length ? fishByNPC[i][fishIdx] : "?";
                        }
                    }
                    else
                    {
                        fish = fishIdx < fishByNPC[i].Length ? fishByNPC[i][fishIdx] : "?";
                    }

                    sb.AppendLine($"  Catch {total} {fish} at {location}  ({caught}/{total})");
                }
                else
                {
                    int minSize = Memory.ReadByte(q.minAddr);
                    int maxSize = Memory.ReadByte(q.maxAddr);
                    sb.AppendLine($"  Catch any fish {minSize}-{maxSize} cm at {q.location}");
                }

                hasAny = true;
            }
            if (headerWritten) sb.AppendLine();
        }

        private static void AppendItemQuests(StringBuilder sb, ref bool hasAny)
        {
            // (name, area, flagAddr, itemName) — flagAddr == 1 means quest is active
            (string name, string area, int flagAddr, string item)[] quests =
            {
                ("Laura", "Divine Beast Cave",  0x21CE4451, "Medusa Powder"),
                ("Ro",    "Wise Owl Forest",    0x21CE4452, "Warp Powder"),
                ("Phil",  "Shipwreck",          0x21CE4453, "Queen's Ring"),
                ("Zabo",  "Sun & Moon Temple",  0x21CE4454, "Hardening Powder"),
            };

            bool headerWritten = false;
            foreach (var q in quests)
            {
                if (Memory.ReadByte(q.flagAddr) != 1) continue;

                if (!headerWritten) { sb.AppendLine("[Item Hunt]"); headerWritten = true; }

                sb.AppendLine($"{q.name}  ({q.area} area)");
                sb.AppendLine($"  Find {q.item} in {q.area} backfloors");
                hasAny = true;
            }
            if (headerWritten) sb.AppendLine();
        }

        private static void AppendMayorQuest(StringBuilder sb, ref bool hasAny)
        {
            if (Memory.ReadByte(0x21CE4464) != 2) return;
            if (Memory.ReadByte(0x21CE4468) != 1) return;

            sb.AppendLine("[Special]");
            sb.AppendLine("Mayor  (Brownboo Village)");
            sb.AppendLine("  Complete special dungeon challenge");
            hasAny = true;
        }
    }
}
