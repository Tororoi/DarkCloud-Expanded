# Chest & Clown Loot Tables (vanilla, extracted from SCUS_971.11)

Reference for the stock chest/floor loot pools. The Chest Randomizer overwrites live chest
slots at runtime, so this is documentation only. Raw memory layout + write helpers:
`ItemAddresses.ItemPutLists` (chest pools), `ItemAddresses.ItemDropRateLists` (rates),
and `PieroItemList` (clown). Floor->group: exact-floor override (word0==floor+1), else
threshold T per dungeon = [8, 9, 9, 9, 8, 12, 50] (floor<T = early/256, floor>=T = late/255).

`Rate` = ItemSetRateList rarity 0-100 (50 = baseline; **higher = RARER**). The picker accepts an
item when its rate < a random 0-99 roll, so `P(picked) ~= (100 - rate)/100` (rate 90 ~= 10%).
`x` = occurrence count in the raw list (duplicates raise selection chance).

## 0. Divine Beast Cave

### Front floors

**floors 1-8** (53 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 2 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Cheese | 155 | 50 | 2 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 2 |
| Escape Powder | 175 | 50 | 2 |
| Repair Powder | 177 | 50 | 4 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Tram Oil | 224 | 50 | 3 |
| Baselard | 259 | 50 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 60 | 1 |
| Kitchen Knife | 265 | 60 | 1 |
| Shamshir | 270 | 60 | 1 |
| Bone Rapier | 290 | 60 | 1 |

**floors 9+ (except 11)** (57 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 2 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Cheese | 155 | 50 | 2 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Tram Oil | 224 | 50 | 3 |
| Baselard | 259 | 50 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 60 | 1 |
| Kitchen Knife | 265 | 60 | 1 |
| Shamshir | 270 | 60 | 1 |
| Bone Rapier | 290 | 60 | 1 |
| Steel Slingshot | 301 | 50 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Bone Slingshot | 304 | 50 | 1 |

**floor 11** (57 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 3 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Cheese | 155 | 50 | 2 |
| Bomb | 159 | 50 | 3 |
| Fire Gem | 161 | 50 | 3 |
| Ice Gem | 162 | 50 | 3 |
| Thunder Gem | 163 | 50 | 3 |
| Wind Gem | 164 | 50 | 3 |
| Holy Gem | 165 | 50 | 3 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 3 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Tram Oil | 224 | 50 | 1 |
| Baselard | 259 | 50 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 60 | 1 |
| Kitchen Knife | 265 | 60 | 1 |
| Shamshir | 270 | 60 | 1 |
| Bone Rapier | 290 | 60 | 1 |
| Steel Slingshot | 301 | 50 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Bone Slingshot | 304 | 50 | 1 |

### Back floors

**all floors** (52 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Garnet | 95 | 90 | 1 |
| Amethyst | 96 | 80 | 1 |
| Aquamarine | 97 | 90 | 1 |
| Diamond | 98 | 80 | 1 |
| Emerald | 99 | 80 | 1 |
| Pearl | 100 | 80 | 1 |
| Ruby | 101 | 80 | 1 |
| Peridot | 102 | 85 | 1 |
| Sapphire | 103 | 80 | 1 |
| Opal | 104 | 80 | 1 |
| Topaz | 105 | 85 | 1 |
| Turquoise | 106 | 80 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Sea Killer | 113 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Plant Buster | 115 | 50 | 1 |
| Beast Buster | 116 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Metal Breaker | 118 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Powerup Powder | 178 | 90 | 1 |
| Dran's Feather | 235 | 50 | 1 |

### Clown (Piero)

**Big box** (19 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Anti-Freeze Amulet | 132 | 50 | 2 |
| Anti-Curse Amulet | 133 | 50 | 2 |
| Anti-Goo Amulet | 134 | 50 | 2 |
| Antidote Amulet | 135 | 50 | 2 |
| Tasty Water | 146 | 50 | 2 |
| Premium Water | 147 | 50 | 2 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 2 |
| Cheese | 155 | 50 | 2 |
| Baselard | 259 | 50 | 2 |
| Gladius | 260 | 50 | 2 |
| Crysknife | 262 | 60 | 2 |
| Kitchen Knife | 265 | 60 | 2 |
| Shamshir | 270 | 60 | 2 |
| Small Sword | 283 | 65 | 2 |
| Sand Breaker | 284 | 70 | 2 |
| Chopper | 286 | 50 | 2 |
| Bone Rapier | 290 | 60 | 2 |
| Sax | 291 | 60 | 2 |

**Small box** (28 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Attack | 91 | 50 | 2 |
| Endurance | 92 | 50 | 2 |
| Speed | 93 | 50 | 2 |
| Magic | 94 | 50 | 2 |
| Garnet | 95 | 90 | 2 |
| Amethyst | 96 | 80 | 2 |
| Aquamarine | 97 | 90 | 2 |
| Diamond | 98 | 80 | 2 |
| Emerald | 99 | 80 | 2 |
| Pearl | 100 | 80 | 2 |
| Ruby | 101 | 80 | 2 |
| Peridot | 102 | 85 | 2 |
| Sapphire | 103 | 80 | 2 |
| Opal | 104 | 80 | 2 |
| Topaz | 105 | 85 | 2 |
| Turquoise | 106 | 80 | 2 |
| Carrot | 186 | 50 | 2 |
| Potato cake | 187 | 50 | 2 |
| Minon | 188 | 50 | 2 |
| Battan | 189 | 50 | 2 |
| Petite Fish | 190 | 50 | 2 |
| Mimi | 197 | 50 | 2 |
| Prickly | 199 | 50 | 2 |
| Tram Oil | 224 | 50 | 2 |
| Steel Slingshot | 301 | 50 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 50 | 1 |

## 1. Wise Owl Forest

### Front floors

**floors 1-9** (65 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 2 |
| Antidote Drink | 151 | 50 | 2 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Stone | 160 | 50 | 2 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 2 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Sun Dew | 225 | 50 | 6 |
| Dran's Feather | 235 | 50 | 1 |
| Baselard | 259 | 60 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 60 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Shamshir | 270 | 60 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Bone Rapier | 290 | 60 | 1 |
| Sax | 291 | 60 | 1 |
| Steel Slingshot | 301 | 60 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 60 | 1 |

**floors 10+** (68 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 2 |
| Antidote Drink | 151 | 50 | 2 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 2 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Sun Dew | 225 | 50 | 4 |
| Dran's Feather | 235 | 50 | 1 |
| Baselard | 259 | 60 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 60 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Shamshir | 270 | 60 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Bone Rapier | 290 | 60 | 1 |
| Sax | 291 | 60 | 1 |
| Steel Slingshot | 301 | 60 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 60 | 1 |
| Steel Hammer | 316 | 50 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Turtle Shell | 319 | 60 | 1 |
| Big Bucks Hammer | 320 | 65 | 1 |

### Back floors

**all floors** (52 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Garnet | 95 | 80 | 1 |
| Amethyst | 96 | 80 | 1 |
| Aquamarine | 97 | 80 | 1 |
| Diamond | 98 | 80 | 1 |
| Emerald | 99 | 80 | 1 |
| Pearl | 100 | 80 | 1 |
| Ruby | 101 | 80 | 1 |
| Peridot | 102 | 80 | 1 |
| Sapphire | 103 | 80 | 1 |
| Opal | 104 | 80 | 1 |
| Topaz | 105 | 80 | 1 |
| Turquoise | 106 | 80 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Sea Killer | 113 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Plant Buster | 115 | 50 | 1 |
| Beast Buster | 116 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Metal Breaker | 118 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Powerup Powder | 178 | 90 | 1 |
| Dran's Feather | 235 | 50 | 1 |

### Clown (Piero)

**Big box** (25 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Anti-Freeze Amulet | 132 | 50 | 2 |
| Anti-Curse Amulet | 133 | 50 | 2 |
| Anti-Goo Amulet | 134 | 50 | 2 |
| Antidote Amulet | 135 | 50 | 2 |
| Tasty Water | 146 | 50 | 2 |
| Premium Water | 147 | 50 | 2 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 2 |
| Cheese | 155 | 50 | 2 |
| Baselard | 259 | 60 | 2 |
| Wise Owl Sword | 261 | 95 | 1 |
| Crysknife | 262 | 60 | 2 |
| Buster Sword | 264 | 70 | 2 |
| Tsukikage | 266 | 95 | 2 |
| Shamshir | 270 | 60 | 2 |
| Small Sword | 283 | 65 | 2 |
| Sand Breaker | 284 | 70 | 2 |
| Chopper | 286 | 50 | 2 |
| Bone Rapier | 290 | 60 | 2 |
| Sax | 291 | 60 | 2 |
| Steel Hammer | 316 | 50 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Turtle Shell | 319 | 60 | 1 |
| Big Bucks Hammer | 320 | 65 | 1 |
| Trial Hammer | 328 | 95 | 1 |

**Small box** (28 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Attack | 91 | 50 | 2 |
| Endurance | 92 | 50 | 2 |
| Speed | 93 | 50 | 2 |
| Magic | 94 | 50 | 2 |
| Garnet | 95 | 80 | 2 |
| Amethyst | 96 | 80 | 2 |
| Aquamarine | 97 | 80 | 2 |
| Diamond | 98 | 80 | 2 |
| Emerald | 99 | 80 | 2 |
| Pearl | 100 | 80 | 2 |
| Ruby | 101 | 80 | 2 |
| Peridot | 102 | 80 | 2 |
| Sapphire | 103 | 80 | 2 |
| Opal | 104 | 80 | 2 |
| Topaz | 105 | 80 | 2 |
| Turquoise | 106 | 80 | 2 |
| Carrot | 186 | 50 | 2 |
| Potato cake | 187 | 50 | 2 |
| Minon | 188 | 50 | 2 |
| Battan | 189 | 50 | 2 |
| Petite Fish | 190 | 50 | 2 |
| Mimi | 197 | 50 | 2 |
| Prickly | 199 | 50 | 2 |
| Sun Dew | 225 | 50 | 2 |
| Steel Slingshot | 301 | 60 | 2 |
| Bandit Slingshot | 302 | 60 | 2 |
| Steve | 303 | 65 | 2 |
| Bone Slingshot | 304 | 60 | 2 |

## 2. Shipwreck

### Front floors

**floors 1-9** (74 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 2 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Flapping Fish | 226 | 50 | 2 |
| Dran's Feather | 235 | 50 | 1 |
| Ice Block | 245 | 50 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 50 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Kitchen Knife | 265 | 50 | 1 |
| Shamshir | 270 | 65 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Bone Rapier | 290 | 50 | 1 |
| Sax | 291 | 50 | 1 |
| Steel Slingshot | 301 | 65 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 65 | 1 |
| Hardshooter | 305 | 80 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 80 | 1 |
| Turtle Shell | 319 | 50 | 1 |
| Frozen Tuna | 321 | 70 | 1 |
| Gaia Hammer | 322 | 90 | 1 |
| Last Judgement | 323 | 95 | 1 |
| Trial Hammer | 328 | 95 | 1 |

**floors 10+** (79 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 2 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Flapping Fish | 226 | 50 | 2 |
| Dran's Feather | 235 | 50 | 1 |
| Ice Block | 245 | 50 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 50 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Kitchen Knife | 265 | 50 | 1 |
| Shamshir | 270 | 65 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Bone Rapier | 290 | 50 | 1 |
| Sax | 291 | 50 | 1 |
| Steel Slingshot | 301 | 65 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 65 | 1 |
| Hardshooter | 305 | 80 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 80 | 1 |
| Turtle Shell | 319 | 50 | 1 |
| Frozen Tuna | 321 | 70 | 1 |
| Gaia Hammer | 322 | 90 | 1 |
| Last Judgement | 323 | 95 | 1 |
| Trial Hammer | 328 | 95 | 1 |
| Bandit's Ring | 333 | 50 | 1 |
| Crystal Ring | 334 | 50 | 1 |
| Platinum Ring | 335 | 60 | 1 |
| Pocklekul | 343 | 80 | 1 |
| Thorn Armlet | 344 | 95 | 1 |

### Back floors

**all floors** (52 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Garnet | 95 | 80 | 1 |
| Amethyst | 96 | 80 | 1 |
| Aquamarine | 97 | 80 | 1 |
| Diamond | 98 | 80 | 1 |
| Emerald | 99 | 80 | 1 |
| Pearl | 100 | 80 | 1 |
| Ruby | 101 | 80 | 1 |
| Peridot | 102 | 80 | 1 |
| Sapphire | 103 | 80 | 1 |
| Opal | 104 | 80 | 1 |
| Topaz | 105 | 80 | 1 |
| Turquoise | 106 | 80 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Sea Killer | 113 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Plant Buster | 115 | 50 | 1 |
| Beast Buster | 116 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Metal Breaker | 118 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Powerup Powder | 178 | 90 | 1 |
| Dran's Feather | 235 | 50 | 1 |

### Clown (Piero)

**Big box** (27 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Anti-Freeze Amulet | 132 | 50 | 2 |
| Anti-Curse Amulet | 133 | 50 | 2 |
| Anti-Goo Amulet | 134 | 50 | 2 |
| Antidote Amulet | 135 | 50 | 2 |
| Tasty Water | 146 | 50 | 2 |
| Premium Water | 147 | 50 | 2 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 2 |
| Cheese | 155 | 50 | 2 |
| Wise Owl Sword | 261 | 95 | 2 |
| Crysknife | 262 | 50 | 2 |
| Buster Sword | 264 | 70 | 2 |
| Tsukikage | 266 | 95 | 2 |
| Shamshir | 270 | 65 | 2 |
| Small Sword | 283 | 65 | 2 |
| Sand Breaker | 284 | 70 | 2 |
| Chopper | 286 | 50 | 2 |
| Bone Rapier | 290 | 50 | 2 |
| Sax | 291 | 50 | 2 |
| Steel Hammer | 316 | 60 | 2 |
| Magical Hammer | 317 | 50 | 2 |
| Battle Ax | 318 | 80 | 2 |
| Turtle Shell | 319 | 50 | 2 |
| Big Bucks Hammer | 320 | 50 | 2 |
| Gaia Hammer | 322 | 90 | 2 |
| Last Judgement | 323 | 95 | 2 |
| Trial Hammer | 328 | 95 | 2 |

**Small box** (35 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Attack | 91 | 50 | 2 |
| Endurance | 92 | 50 | 2 |
| Speed | 93 | 50 | 2 |
| Magic | 94 | 50 | 2 |
| Garnet | 95 | 80 | 2 |
| Amethyst | 96 | 80 | 2 |
| Aquamarine | 97 | 80 | 2 |
| Diamond | 98 | 80 | 2 |
| Emerald | 99 | 80 | 2 |
| Pearl | 100 | 80 | 2 |
| Ruby | 101 | 80 | 2 |
| Peridot | 102 | 80 | 2 |
| Sapphire | 103 | 80 | 2 |
| Opal | 104 | 80 | 2 |
| Topaz | 105 | 80 | 2 |
| Turquoise | 106 | 80 | 2 |
| Carrot | 186 | 50 | 2 |
| Potato cake | 187 | 50 | 2 |
| Minon | 188 | 50 | 2 |
| Battan | 189 | 50 | 2 |
| Petite Fish | 190 | 50 | 2 |
| Mimi | 197 | 50 | 2 |
| Prickly | 199 | 50 | 2 |
| Sun Dew | 225 | 50 | 2 |
| Steel Slingshot | 301 | 65 | 2 |
| Bandit Slingshot | 302 | 60 | 2 |
| Steve | 303 | 65 | 2 |
| Bone Slingshot | 304 | 65 | 2 |
| Hardshooter | 305 | 80 | 2 |
| Bandit's Ring | 333 | 50 | 1 |
| Crystal Ring | 334 | 50 | 1 |
| Platinum Ring | 335 | 60 | 1 |
| Fairy's Ring | 337 | 80 | 1 |
| Pocklekul | 343 | 80 | 1 |
| Thorn Armlet | 344 | 95 | 1 |

## 3. Sun & Moon Temple

### Front floors

**floors 1-9** (80 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Secret Path Key | 228 | 50 | 3 |
| Dran's Feather | 235 | 50 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 50 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Shamshir | 270 | 70 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Choora | 287 | 70 | 1 |
| Bone Rapier | 290 | 50 | 1 |
| Sax | 291 | 50 | 1 |
| Steel Slingshot | 301 | 70 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 70 | 1 |
| Hardshooter | 305 | 80 | 1 |
| Double Impact | 306 | 90 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 50 | 1 |
| Frozen Tuna | 321 | 70 | 1 |
| Gaia Hammer | 322 | 90 | 1 |
| Last Judgement | 323 | 95 | 1 |
| Bandit's Ring | 333 | 65 | 1 |
| Crystal Ring | 334 | 65 | 1 |
| Platinum Ring | 335 | 65 | 1 |
| Goddess Ring | 336 | 80 | 1 |
| Fairy's Ring | 337 | 80 | 1 |
| Pocklekul | 343 | 80 | 1 |
| Thorn Armlet | 344 | 90 | 1 |

**floors 10+** (84 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Secret Path Key | 228 | 50 | 3 |
| Dran's Feather | 235 | 50 | 1 |
| Gladius | 260 | 50 | 1 |
| Crysknife | 262 | 50 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Shamshir | 270 | 70 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Choora | 287 | 70 | 1 |
| Bone Rapier | 290 | 50 | 1 |
| Sax | 291 | 50 | 1 |
| Steel Slingshot | 301 | 70 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 70 | 1 |
| Hardshooter | 305 | 80 | 1 |
| Double Impact | 306 | 90 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 50 | 1 |
| Frozen Tuna | 321 | 70 | 1 |
| Gaia Hammer | 322 | 90 | 1 |
| Last Judgement | 323 | 95 | 1 |
| Bandit's Ring | 333 | 65 | 1 |
| Crystal Ring | 334 | 65 | 1 |
| Platinum Ring | 335 | 65 | 1 |
| Goddess Ring | 336 | 80 | 1 |
| Fairy's Ring | 337 | 80 | 1 |
| Pocklekul | 343 | 80 | 1 |
| Thorn Armlet | 344 | 90 | 1 |
| Javelin | 349 | 65 | 1 |
| Halberd | 350 | 65 | 1 |
| DeSanga | 351 | 60 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |

### Back floors

**all floors** (52 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Garnet | 95 | 75 | 1 |
| Amethyst | 96 | 75 | 1 |
| Aquamarine | 97 | 75 | 1 |
| Diamond | 98 | 75 | 1 |
| Emerald | 99 | 75 | 1 |
| Pearl | 100 | 75 | 1 |
| Ruby | 101 | 75 | 1 |
| Peridot | 102 | 75 | 1 |
| Sapphire | 103 | 75 | 1 |
| Opal | 104 | 75 | 1 |
| Topaz | 105 | 75 | 1 |
| Turquoise | 106 | 75 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Sea Killer | 113 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Plant Buster | 115 | 50 | 1 |
| Beast Buster | 116 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Metal Breaker | 118 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Powerup Powder | 178 | 90 | 1 |
| Dran's Feather | 235 | 50 | 1 |

### Clown (Piero)

**Big box** (32 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Anti-Freeze Amulet | 132 | 50 | 2 |
| Anti-Curse Amulet | 133 | 50 | 2 |
| Anti-Goo Amulet | 134 | 50 | 2 |
| Antidote Amulet | 135 | 50 | 2 |
| Tasty Water | 146 | 50 | 2 |
| Premium Water | 147 | 50 | 2 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 2 |
| Cheese | 155 | 50 | 2 |
| Wise Owl Sword | 261 | 95 | 2 |
| Crysknife | 262 | 50 | 2 |
| Buster Sword | 264 | 70 | 2 |
| Tsukikage | 266 | 95 | 2 |
| Shamshir | 270 | 70 | 2 |
| Small Sword | 283 | 65 | 2 |
| Sand Breaker | 284 | 70 | 2 |
| Chopper | 286 | 50 | 2 |
| Choora | 287 | 70 | 2 |
| Bone Rapier | 290 | 50 | 2 |
| Sax | 291 | 50 | 2 |
| Steel Hammer | 316 | 65 | 2 |
| Magical Hammer | 317 | 50 | 2 |
| Battle Ax | 318 | 50 | 2 |
| Turtle Shell | 319 | 50 | 2 |
| Big Bucks Hammer | 320 | 50 | 2 |
| Gaia Hammer | 322 | 90 | 2 |
| Last Judgement | 323 | 95 | 2 |
| Trial Hammer | 328 | 90 | 2 |
| Javelin | 349 | 65 | 1 |
| Halberd | 350 | 65 | 1 |
| DeSanga | 351 | 60 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |

**Small box** (35 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Attack | 91 | 50 | 2 |
| Endurance | 92 | 50 | 2 |
| Speed | 93 | 50 | 2 |
| Magic | 94 | 50 | 2 |
| Garnet | 95 | 75 | 2 |
| Amethyst | 96 | 75 | 2 |
| Aquamarine | 97 | 75 | 2 |
| Diamond | 98 | 75 | 2 |
| Emerald | 99 | 75 | 2 |
| Pearl | 100 | 75 | 2 |
| Ruby | 101 | 75 | 2 |
| Peridot | 102 | 75 | 2 |
| Sapphire | 103 | 75 | 2 |
| Opal | 104 | 75 | 2 |
| Topaz | 105 | 75 | 2 |
| Turquoise | 106 | 75 | 2 |
| Carrot | 186 | 50 | 2 |
| Potato cake | 187 | 50 | 2 |
| Minon | 188 | 50 | 2 |
| Battan | 189 | 50 | 2 |
| Petite Fish | 190 | 50 | 2 |
| Mimi | 197 | 50 | 2 |
| Prickly | 199 | 50 | 2 |
| Sun Dew | 225 | 50 | 2 |
| Steel Slingshot | 301 | 70 | 2 |
| Bandit Slingshot | 302 | 60 | 2 |
| Steve | 303 | 65 | 2 |
| Bone Slingshot | 304 | 70 | 2 |
| Hardshooter | 305 | 80 | 2 |
| Bandit's Ring | 333 | 65 | 1 |
| Crystal Ring | 334 | 65 | 1 |
| Platinum Ring | 335 | 65 | 1 |
| Fairy's Ring | 337 | 80 | 1 |
| Pocklekul | 343 | 80 | 1 |
| Thorn Armlet | 344 | 90 | 1 |

## 4. Moon Sea

### Front floors

**floors 1-8** (87 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Bravery Launch | 229 | 50 | 3 |
| Dran's Feather | 235 | 50 | 1 |
| Crysknife | 262 | 50 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Shamshir | 270 | 75 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Choora | 287 | 75 | 1 |
| Bone Rapier | 290 | 50 | 1 |
| Sax | 291 | 50 | 1 |
| Steel Slingshot | 301 | 75 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 75 | 1 |
| Hardshooter | 305 | 80 | 1 |
| Double Impact | 306 | 90 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 50 | 2 |
| Big Bucks Hammer | 320 | 50 | 1 |
| Frozen Tuna | 321 | 70 | 1 |
| Gaia Hammer | 322 | 90 | 1 |
| Last Judgement | 323 | 95 | 1 |
| Bandit's Ring | 333 | 70 | 1 |
| Crystal Ring | 334 | 70 | 1 |
| Platinum Ring | 335 | 70 | 1 |
| Goddess Ring | 336 | 75 | 1 |
| Fairy's Ring | 337 | 75 | 1 |
| Destruction Ring | 338 | 80 | 1 |
| Javelin | 349 | 70 | 1 |
| Halberd | 350 | 70 | 1 |
| DeSanga | 351 | 60 | 1 |
| Scorpion | 352 | 70 | 1 |
| Partisan | 353 | 80 | 1 |
| Terra Sword | 355 | 90 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |
| Cactus | 360 | 70 | 1 |

**floors 9+** (91 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Regular Water | 145 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Bravery Launch | 229 | 50 | 3 |
| Dran's Feather | 235 | 50 | 1 |
| Crysknife | 262 | 50 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 70 | 1 |
| Shamshir | 270 | 75 | 1 |
| Small Sword | 283 | 65 | 1 |
| Sand Breaker | 284 | 70 | 1 |
| Chopper | 286 | 50 | 1 |
| Choora | 287 | 75 | 1 |
| Bone Rapier | 290 | 50 | 1 |
| Sax | 291 | 50 | 1 |
| Steel Slingshot | 301 | 75 | 1 |
| Bandit Slingshot | 302 | 60 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 75 | 1 |
| Hardshooter | 305 | 80 | 1 |
| Double Impact | 306 | 90 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 50 | 2 |
| Big Bucks Hammer | 320 | 50 | 1 |
| Frozen Tuna | 321 | 70 | 1 |
| Gaia Hammer | 322 | 90 | 1 |
| Last Judgement | 323 | 95 | 1 |
| Bandit's Ring | 333 | 70 | 1 |
| Crystal Ring | 334 | 70 | 1 |
| Platinum Ring | 335 | 70 | 1 |
| Goddess Ring | 336 | 75 | 1 |
| Fairy's Ring | 337 | 75 | 1 |
| Destruction Ring | 338 | 80 | 1 |
| Javelin | 349 | 70 | 1 |
| Halberd | 350 | 70 | 1 |
| DeSanga | 351 | 60 | 1 |
| Scorpion | 352 | 70 | 1 |
| Partisan | 353 | 80 | 1 |
| Terra Sword | 355 | 90 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |
| Cactus | 360 | 70 | 1 |
| Jackal | 365 | 85 | 1 |
| Blessing Gun | 368 | 50 | 1 |
| Snail | 374 | 80 | 1 |
| Swallow | 375 | 90 | 1 |

### Back floors

**all floors** (52 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Garnet | 95 | 70 | 1 |
| Amethyst | 96 | 70 | 1 |
| Aquamarine | 97 | 70 | 1 |
| Diamond | 98 | 70 | 1 |
| Emerald | 99 | 70 | 1 |
| Pearl | 100 | 70 | 1 |
| Ruby | 101 | 70 | 1 |
| Peridot | 102 | 70 | 1 |
| Sapphire | 103 | 70 | 1 |
| Opal | 104 | 70 | 1 |
| Topaz | 105 | 70 | 1 |
| Turquoise | 106 | 70 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Sea Killer | 113 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Plant Buster | 115 | 50 | 1 |
| Beast Buster | 116 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Metal Breaker | 118 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Powerup Powder | 178 | 90 | 1 |
| Dran's Feather | 235 | 50 | 1 |

### Clown (Piero)

**Big box** (33 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Anti-Freeze Amulet | 132 | 50 | 2 |
| Anti-Curse Amulet | 133 | 50 | 2 |
| Anti-Goo Amulet | 134 | 50 | 2 |
| Antidote Amulet | 135 | 50 | 2 |
| Tasty Water | 146 | 50 | 2 |
| Premium Water | 147 | 50 | 2 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 2 |
| Cheese | 155 | 50 | 2 |
| Wise Owl Sword | 261 | 95 | 2 |
| Crysknife | 262 | 50 | 2 |
| Buster Sword | 264 | 70 | 2 |
| Tsukikage | 266 | 95 | 2 |
| Shamshir | 270 | 75 | 2 |
| Small Sword | 283 | 65 | 2 |
| Sand Breaker | 284 | 70 | 2 |
| Chopper | 286 | 50 | 2 |
| Choora | 287 | 75 | 2 |
| Bone Rapier | 290 | 50 | 2 |
| Sax | 291 | 50 | 2 |
| Steel Hammer | 316 | 70 | 2 |
| Magical Hammer | 317 | 50 | 2 |
| Battle Ax | 318 | 50 | 2 |
| Turtle Shell | 319 | 50 | 2 |
| Big Bucks Hammer | 320 | 50 | 2 |
| Gaia Hammer | 322 | 90 | 2 |
| Last Judgement | 323 | 95 | 2 |
| Trial Hammer | 328 | 90 | 2 |
| Javelin | 349 | 70 | 2 |
| Halberd | 350 | 70 | 2 |
| DeSanga | 351 | 60 | 2 |
| 5 Foot Nail | 359 | 60 | 2 |
| Cactus | 360 | 70 | 2 |

**Small box** (41 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Attack | 91 | 50 | 2 |
| Endurance | 92 | 50 | 2 |
| Speed | 93 | 50 | 2 |
| Magic | 94 | 50 | 2 |
| Garnet | 95 | 70 | 2 |
| Amethyst | 96 | 70 | 2 |
| Aquamarine | 97 | 70 | 2 |
| Diamond | 98 | 70 | 2 |
| Emerald | 99 | 70 | 2 |
| Pearl | 100 | 70 | 2 |
| Ruby | 101 | 70 | 2 |
| Peridot | 102 | 70 | 2 |
| Sapphire | 103 | 70 | 2 |
| Opal | 104 | 70 | 2 |
| Topaz | 105 | 70 | 2 |
| Turquoise | 106 | 70 | 2 |
| Carrot | 186 | 50 | 2 |
| Potato cake | 187 | 50 | 2 |
| Minon | 188 | 50 | 2 |
| Battan | 189 | 50 | 2 |
| Petite Fish | 190 | 50 | 2 |
| Mimi | 197 | 50 | 2 |
| Prickly | 199 | 50 | 2 |
| Sun Dew | 225 | 50 | 2 |
| Steel Slingshot | 301 | 75 | 2 |
| Bandit Slingshot | 302 | 60 | 2 |
| Steve | 303 | 65 | 2 |
| Bone Slingshot | 304 | 75 | 2 |
| Hardshooter | 305 | 80 | 2 |
| Bandit's Ring | 333 | 70 | 2 |
| Crystal Ring | 334 | 70 | 2 |
| Platinum Ring | 335 | 70 | 2 |
| Fairy's Ring | 337 | 75 | 2 |
| Pocklekul | 343 | 80 | 2 |
| Thorn Armlet | 344 | 90 | 2 |
| Jackal | 365 | 85 | 1 |
| Blessing Gun | 368 | 50 | 1 |
| Skunk | 369 | 85 | 1 |
| G Crusher | 370 | 85 | 1 |
| Snail | 374 | 80 | 1 |
| Swallow | 375 | 90 | 1 |

## 5. Gallery of Time

### Front floors

**all floors** (91 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Flapping Duster | 230 | 50 | 3 |
| Dran's Feather | 235 | 50 | 1 |
| Crysknife | 262 | 95 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 95 | 1 |
| Lamb's Sword | 272 | 95 | 1 |
| Brave Ark | 274 | 98 | 1 |
| Aga's Sword | 281 | 80 | 1 |
| Drain Seeker | 285 | 50 | 1 |
| Maneater | 289 | 50 | 1 |
| 7 Branch Sword | 292 | 50 | 1 |
| Cross Hinder | 294 | 50 | 1 |
| Steel Slingshot | 301 | 80 | 1 |
| Bandit Slingshot | 302 | 95 | 1 |
| Steve | 303 | 65 | 1 |
| Hardshooter | 305 | 95 | 1 |
| Double Impact | 306 | 95 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 95 | 1 |
| Gaia Hammer | 322 | 85 | 1 |
| Last Judgement | 323 | 90 | 1 |
| Bandit's Ring | 333 | 75 | 1 |
| Crystal Ring | 334 | 75 | 1 |
| Platinum Ring | 335 | 75 | 1 |
| Goddess Ring | 336 | 75 | 1 |
| Fairy's Ring | 337 | 75 | 1 |
| Destruction Ring | 338 | 80 | 1 |
| Satan's Ring | 339 | 85 | 1 |
| Javelin | 349 | 75 | 1 |
| Halberd | 350 | 75 | 1 |
| DeSanga | 351 | 60 | 1 |
| Scorpion | 352 | 70 | 1 |
| Partisan | 353 | 80 | 1 |
| Terra Sword | 355 | 90 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |
| Cactus | 360 | 70 | 1 |
| Jackal | 365 | 85 | 1 |
| Blessing Gun | 368 | 50 | 1 |
| Skunk | 369 | 85 | 1 |
| G Crusher | 370 | 85 | 1 |
| Hexa Blaster | 371 | 90 | 1 |
| Snail | 374 | 80 | 1 |
| Swallow | 375 | 85 | 1 |

### Back floors

**all floors** (97 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Garnet | 95 | 70 | 1 |
| Amethyst | 96 | 70 | 1 |
| Aquamarine | 97 | 70 | 1 |
| Diamond | 98 | 70 | 1 |
| Emerald | 99 | 70 | 1 |
| Pearl | 100 | 70 | 1 |
| Ruby | 101 | 70 | 1 |
| Peridot | 102 | 70 | 1 |
| Sapphire | 103 | 70 | 1 |
| Opal | 104 | 70 | 1 |
| Topaz | 105 | 70 | 1 |
| Turquoise | 106 | 70 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Sea Killer | 113 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Plant Buster | 115 | 50 | 1 |
| Beast Buster | 116 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Metal Breaker | 118 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Powerup Powder | 178 | 90 | 1 |
| Dran's Feather | 235 | 50 | 1 |
| Crysknife | 262 | 95 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 95 | 1 |
| Shamshir | 270 | 95 | 1 |
| Lamb's Sword | 272 | 95 | 1 |
| Brave Ark | 274 | 98 | 1 |
| Aga's Sword | 281 | 80 | 1 |
| Drain Seeker | 285 | 50 | 1 |
| Maneater | 289 | 50 | 1 |
| 7 Branch Sword | 292 | 50 | 1 |
| Cross Hinder | 294 | 50 | 1 |
| Steel Slingshot | 301 | 80 | 1 |
| Bandit Slingshot | 302 | 95 | 1 |
| Steve | 303 | 65 | 1 |
| Bone Slingshot | 304 | 95 | 1 |
| Hardshooter | 305 | 95 | 1 |
| Double Impact | 306 | 95 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 95 | 1 |
| Frozen Tuna | 321 | 95 | 1 |
| Gaia Hammer | 322 | 85 | 1 |
| Last Judgement | 323 | 90 | 1 |
| Bandit's Ring | 333 | 75 | 1 |
| Crystal Ring | 334 | 75 | 1 |
| Platinum Ring | 335 | 75 | 1 |
| Goddess Ring | 336 | 75 | 1 |
| Fairy's Ring | 337 | 75 | 1 |
| Destruction Ring | 338 | 80 | 1 |
| Satan's Ring | 339 | 85 | 1 |
| Javelin | 349 | 75 | 1 |
| Halberd | 350 | 75 | 1 |
| DeSanga | 351 | 60 | 1 |
| Scorpion | 352 | 70 | 1 |
| Partisan | 353 | 80 | 1 |
| Terra Sword | 355 | 90 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |
| Cactus | 360 | 70 | 1 |
| Jackal | 365 | 85 | 1 |
| Blessing Gun | 368 | 50 | 1 |
| Skunk | 369 | 85 | 1 |
| G Crusher | 370 | 85 | 1 |
| Hexa Blaster | 371 | 90 | 1 |
| Snail | 374 | 80 | 1 |
| Swallow | 375 | 85 | 1 |

### Clown (Piero)

**Big box** (33 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Anti-Freeze Amulet | 132 | 50 | 2 |
| Anti-Curse Amulet | 133 | 50 | 2 |
| Anti-Goo Amulet | 134 | 50 | 2 |
| Antidote Amulet | 135 | 50 | 2 |
| Tasty Water | 146 | 50 | 2 |
| Premium Water | 147 | 50 | 2 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 2 |
| Cheese | 155 | 50 | 2 |
| Wise Owl Sword | 261 | 80 | 2 |
| Crysknife | 262 | 95 | 2 |
| Buster Sword | 264 | 95 | 2 |
| Tsukikage | 266 | 95 | 2 |
| Shamshir | 270 | 95 | 2 |
| Small Sword | 283 | 95 | 2 |
| Sand Breaker | 284 | 70 | 2 |
| Chopper | 286 | 95 | 2 |
| Choora | 287 | 95 | 2 |
| Bone Rapier | 290 | 50 | 2 |
| Sax | 291 | 95 | 2 |
| Steel Hammer | 316 | 95 | 2 |
| Magical Hammer | 317 | 50 | 2 |
| Battle Ax | 318 | 50 | 2 |
| Turtle Shell | 319 | 95 | 2 |
| Big Bucks Hammer | 320 | 95 | 2 |
| Gaia Hammer | 322 | 85 | 2 |
| Last Judgement | 323 | 90 | 2 |
| Trial Hammer | 328 | 85 | 2 |
| Javelin | 349 | 75 | 2 |
| Halberd | 350 | 75 | 2 |
| DeSanga | 351 | 60 | 2 |
| 5 Foot Nail | 359 | 60 | 2 |
| Cactus | 360 | 70 | 2 |

**Small box** (42 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Attack | 91 | 50 | 2 |
| Endurance | 92 | 50 | 2 |
| Speed | 93 | 50 | 2 |
| Magic | 94 | 50 | 2 |
| Garnet | 95 | 70 | 2 |
| Amethyst | 96 | 70 | 2 |
| Aquamarine | 97 | 70 | 2 |
| Diamond | 98 | 70 | 2 |
| Emerald | 99 | 70 | 2 |
| Pearl | 100 | 70 | 2 |
| Ruby | 101 | 70 | 2 |
| Peridot | 102 | 70 | 2 |
| Sapphire | 103 | 70 | 2 |
| Opal | 104 | 70 | 2 |
| Topaz | 105 | 70 | 2 |
| Turquoise | 106 | 70 | 2 |
| Carrot | 186 | 50 | 2 |
| Potato cake | 187 | 50 | 2 |
| Minon | 188 | 50 | 2 |
| Battan | 189 | 50 | 2 |
| Petite Fish | 190 | 50 | 2 |
| Mimi | 197 | 50 | 2 |
| Prickly | 199 | 50 | 2 |
| Sun Dew | 225 | 50 | 2 |
| Steel Slingshot | 301 | 80 | 2 |
| Bandit Slingshot | 302 | 95 | 2 |
| Steve | 303 | 65 | 2 |
| Bone Slingshot | 304 | 95 | 2 |
| Hardshooter | 305 | 95 | 2 |
| Bandit's Ring | 333 | 75 | 2 |
| Crystal Ring | 334 | 75 | 2 |
| Platinum Ring | 335 | 75 | 2 |
| Fairy's Ring | 337 | 75 | 2 |
| Pocklekul | 343 | 80 | 2 |
| Thorn Armlet | 344 | 85 | 2 |
| Jackal | 365 | 85 | 2 |
| Blessing Gun | 368 | 50 | 2 |
| Skunk | 369 | 85 | 2 |
| G Crusher | 370 | 85 | 2 |
| Hexa Blaster | 371 | 90 | 2 |
| Snail | 374 | 80 | 2 |
| Swallow | 375 | 85 | 2 |

## 6. Demon Shaft

### Front floors

**floors 1-50** (88 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Crystal Eyeball | 231 | 50 | 5 |
| Dran's Feather | 235 | 50 | 1 |
| Crysknife | 262 | 95 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 95 | 1 |
| Lamb's Sword | 272 | 95 | 1 |
| Brave Ark | 274 | 98 | 1 |
| Aga's Sword | 281 | 80 | 1 |
| Drain Seeker | 285 | 50 | 1 |
| Maneater | 289 | 50 | 1 |
| 7 Branch Sword | 292 | 50 | 1 |
| Cross Hinder | 294 | 50 | 1 |
| Steel Slingshot | 301 | 80 | 1 |
| Bandit Slingshot | 302 | 95 | 1 |
| Steve | 303 | 65 | 1 |
| Hardshooter | 305 | 95 | 1 |
| Double Impact | 306 | 95 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 95 | 1 |
| Gaia Hammer | 322 | 85 | 1 |
| Last Judgement | 323 | 90 | 1 |
| Bandit's Ring | 333 | 75 | 1 |
| Crystal Ring | 334 | 75 | 1 |
| Platinum Ring | 335 | 75 | 1 |
| Goddess Ring | 336 | 75 | 1 |
| Fairy's Ring | 337 | 75 | 1 |
| Destruction Ring | 338 | 80 | 1 |
| Satan's Ring | 339 | 85 | 1 |
| Javelin | 349 | 75 | 1 |
| Halberd | 350 | 75 | 1 |
| DeSanga | 351 | 60 | 1 |
| Scorpion | 352 | 70 | 1 |
| Partisan | 353 | 80 | 1 |
| Terra Sword | 355 | 90 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |
| Cactus | 360 | 70 | 1 |
| Jackal | 365 | 85 | 1 |
| Blessing Gun | 368 | 50 | 1 |
| Skunk | 369 | 85 | 1 |
| G Crusher | 370 | 85 | 1 |
| Hexa Blaster | 371 | 90 | 1 |
| Snail | 374 | 80 | 1 |
| Swallow | 375 | 85 | 1 |

**floors 51+** (89 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Anti-Freeze Amulet | 132 | 50 | 1 |
| Anti-Curse Amulet | 133 | 50 | 1 |
| Anti-Goo Amulet | 134 | 50 | 1 |
| Antidote Amulet | 135 | 50 | 1 |
| Tasty Water | 146 | 50 | 1 |
| Premium Water | 147 | 50 | 1 |
| Premium Chicken | 149 | 50 | 1 |
| Stamina Drink | 150 | 50 | 1 |
| Antidote Drink | 151 | 50 | 1 |
| Holy Water | 152 | 50 | 1 |
| Soap | 153 | 50 | 1 |
| Mighty Healing | 154 | 50 | 1 |
| Cheese | 155 | 50 | 1 |
| Bomb | 159 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Throbbing Cherry | 166 | 50 | 1 |
| Gooey Peach | 167 | 50 | 1 |
| Bomb Nuts | 168 | 50 | 1 |
| Poisonous Apple | 169 | 50 | 1 |
| Mellow Banana | 170 | 50 | 1 |
| Stand-in Powder | 174 | 50 | 1 |
| Escape Powder | 175 | 50 | 1 |
| Revival Powder | 176 | 50 | 1 |
| Repair Powder | 177 | 50 | 2 |
| Powerup Powder | 178 | 90 | 1 |
| Treasure Key | 181 | 50 | 1 |
| Carrot | 186 | 50 | 1 |
| Potato cake | 187 | 50 | 1 |
| Minon | 188 | 50 | 1 |
| Battan | 189 | 50 | 1 |
| Petite Fish | 190 | 50 | 1 |
| Evy | 193 | 50 | 1 |
| Mimi | 197 | 50 | 1 |
| Prickly | 199 | 50 | 1 |
| Crystal Eyeball | 231 | 50 | 4 |
| Dran's Feather | 235 | 50 | 1 |
| Crysknife | 262 | 95 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Buster Sword | 264 | 95 | 1 |
| Lamb's Sword | 272 | 95 | 1 |
| Brave Ark | 274 | 98 | 1 |
| Aga's Sword | 281 | 80 | 1 |
| Drain Seeker | 285 | 50 | 1 |
| Maneater | 289 | 50 | 1 |
| 7 Branch Sword | 292 | 50 | 1 |
| Cross Hinder | 294 | 50 | 1 |
| Steel Slingshot | 301 | 80 | 1 |
| Bandit Slingshot | 302 | 95 | 1 |
| Steve | 303 | 65 | 1 |
| Hardshooter | 305 | 95 | 1 |
| Double Impact | 306 | 95 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 95 | 1 |
| Gaia Hammer | 322 | 85 | 1 |
| Last Judgement | 323 | 90 | 1 |
| Bandit's Ring | 333 | 75 | 1 |
| Crystal Ring | 334 | 75 | 1 |
| Platinum Ring | 335 | 75 | 1 |
| Goddess Ring | 336 | 75 | 1 |
| Fairy's Ring | 337 | 75 | 1 |
| Destruction Ring | 338 | 80 | 1 |
| Satan's Ring | 339 | 85 | 1 |
| Javelin | 349 | 75 | 1 |
| Halberd | 350 | 75 | 1 |
| DeSanga | 351 | 60 | 1 |
| Scorpion | 352 | 70 | 1 |
| Partisan | 353 | 80 | 1 |
| Terra Sword | 355 | 90 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |
| Cactus | 360 | 70 | 1 |
| Jackal | 365 | 85 | 1 |
| Blessing Gun | 368 | 50 | 1 |
| Skunk | 369 | 85 | 1 |
| G Crusher | 370 | 85 | 1 |
| Hexa Blaster | 371 | 90 | 1 |
| Snail | 374 | 80 | 1 |
| Swallow | 375 | 85 | 1 |

### Back floors

**all floors** (73 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Fire | 81 | 50 | 1 |
| Ice | 82 | 50 | 1 |
| Thunder | 83 | 50 | 1 |
| Wind | 84 | 50 | 1 |
| Holy | 85 | 50 | 1 |
| Attack | 91 | 50 | 1 |
| Endurance | 92 | 50 | 1 |
| Speed | 93 | 50 | 1 |
| Magic | 94 | 50 | 1 |
| Garnet | 95 | 70 | 1 |
| Amethyst | 96 | 70 | 1 |
| Aquamarine | 97 | 70 | 1 |
| Diamond | 98 | 70 | 1 |
| Emerald | 99 | 70 | 1 |
| Pearl | 100 | 70 | 1 |
| Ruby | 101 | 70 | 1 |
| Peridot | 102 | 70 | 1 |
| Sapphire | 103 | 70 | 1 |
| Opal | 104 | 70 | 1 |
| Topaz | 105 | 70 | 1 |
| Turquoise | 106 | 70 | 1 |
| Dinoslayer | 111 | 50 | 1 |
| Undead Buster | 112 | 50 | 1 |
| Sea Killer | 113 | 50 | 1 |
| Stone Breaker | 114 | 50 | 1 |
| Plant Buster | 115 | 50 | 1 |
| Beast Buster | 116 | 50 | 1 |
| Sky Hunter | 117 | 50 | 1 |
| Metal Breaker | 118 | 50 | 1 |
| Mimic Breaker | 119 | 50 | 1 |
| Mage Slayer | 120 | 50 | 1 |
| Fire Gem | 161 | 50 | 1 |
| Ice Gem | 162 | 50 | 1 |
| Thunder Gem | 163 | 50 | 1 |
| Wind Gem | 164 | 50 | 1 |
| Holy Gem | 165 | 50 | 1 |
| Powerup Powder | 178 | 90 | 1 |
| Antique Sword | 263 | 65 | 1 |
| Lamb's Sword | 272 | 95 | 1 |
| Brave Ark | 274 | 98 | 1 |
| Aga's Sword | 281 | 80 | 1 |
| Drain Seeker | 285 | 50 | 1 |
| Maneater | 289 | 50 | 1 |
| 7 Branch Sword | 292 | 50 | 1 |
| Cross Hinder | 294 | 50 | 1 |
| Steve | 303 | 65 | 1 |
| Magical Hammer | 317 | 50 | 1 |
| Battle Ax | 318 | 50 | 1 |
| Turtle Shell | 319 | 95 | 1 |
| Gaia Hammer | 322 | 85 | 1 |
| Last Judgement | 323 | 90 | 1 |
| Bandit's Ring | 333 | 75 | 1 |
| Crystal Ring | 334 | 75 | 1 |
| Platinum Ring | 335 | 75 | 1 |
| Goddess Ring | 336 | 75 | 1 |
| Fairy's Ring | 337 | 75 | 1 |
| Destruction Ring | 338 | 80 | 1 |
| Satan's Ring | 339 | 85 | 1 |
| Javelin | 349 | 75 | 1 |
| Halberd | 350 | 75 | 1 |
| DeSanga | 351 | 60 | 1 |
| Scorpion | 352 | 70 | 1 |
| Partisan | 353 | 80 | 1 |
| Terra Sword | 355 | 90 | 1 |
| 5 Foot Nail | 359 | 60 | 1 |
| Cactus | 360 | 70 | 1 |
| Jackal | 365 | 85 | 1 |
| Blessing Gun | 368 | 50 | 1 |
| Skunk | 369 | 85 | 1 |
| G Crusher | 370 | 85 | 1 |
| Hexa Blaster | 371 | 90 | 1 |
| Snail | 374 | 80 | 1 |
| Swallow | 375 | 85 | 1 |

### Clown (Piero)

**Big box** (33 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Anti-Freeze Amulet | 132 | 50 | 2 |
| Anti-Curse Amulet | 133 | 50 | 2 |
| Anti-Goo Amulet | 134 | 50 | 2 |
| Antidote Amulet | 135 | 50 | 2 |
| Tasty Water | 146 | 50 | 2 |
| Premium Water | 147 | 50 | 2 |
| Bread | 148 | 50 | 2 |
| Premium Chicken | 149 | 50 | 2 |
| Cheese | 155 | 50 | 2 |
| Wise Owl Sword | 261 | 80 | 2 |
| Crysknife | 262 | 95 | 2 |
| Buster Sword | 264 | 95 | 2 |
| Tsukikage | 266 | 95 | 2 |
| Shamshir | 270 | 95 | 2 |
| Small Sword | 283 | 95 | 2 |
| Sand Breaker | 284 | 70 | 2 |
| Chopper | 286 | 95 | 2 |
| Choora | 287 | 95 | 2 |
| Bone Rapier | 290 | 50 | 2 |
| Sax | 291 | 95 | 2 |
| Steel Hammer | 316 | 95 | 2 |
| Magical Hammer | 317 | 50 | 2 |
| Battle Ax | 318 | 50 | 2 |
| Turtle Shell | 319 | 95 | 2 |
| Big Bucks Hammer | 320 | 95 | 2 |
| Gaia Hammer | 322 | 85 | 2 |
| Last Judgement | 323 | 90 | 2 |
| Trial Hammer | 328 | 85 | 2 |
| Javelin | 349 | 75 | 2 |
| Halberd | 350 | 75 | 2 |
| DeSanga | 351 | 60 | 2 |
| 5 Foot Nail | 359 | 60 | 2 |
| Cactus | 360 | 70 | 2 |

**Small box** (42 items)

| Item | ID | Rate | x |
|---|---|---|---|
| Attack | 91 | 50 | 2 |
| Endurance | 92 | 50 | 2 |
| Speed | 93 | 50 | 2 |
| Magic | 94 | 50 | 2 |
| Garnet | 95 | 70 | 2 |
| Amethyst | 96 | 70 | 2 |
| Aquamarine | 97 | 70 | 2 |
| Diamond | 98 | 70 | 2 |
| Emerald | 99 | 70 | 2 |
| Pearl | 100 | 70 | 2 |
| Ruby | 101 | 70 | 2 |
| Peridot | 102 | 70 | 2 |
| Sapphire | 103 | 70 | 2 |
| Opal | 104 | 70 | 2 |
| Topaz | 105 | 70 | 2 |
| Turquoise | 106 | 70 | 2 |
| Carrot | 186 | 50 | 2 |
| Potato cake | 187 | 50 | 2 |
| Minon | 188 | 50 | 2 |
| Battan | 189 | 50 | 2 |
| Petite Fish | 190 | 50 | 2 |
| Mimi | 197 | 50 | 2 |
| Prickly | 199 | 50 | 2 |
| Sun Dew | 225 | 50 | 2 |
| Steel Slingshot | 301 | 80 | 2 |
| Bandit Slingshot | 302 | 95 | 2 |
| Steve | 303 | 65 | 2 |
| Bone Slingshot | 304 | 95 | 2 |
| Hardshooter | 305 | 95 | 2 |
| Bandit's Ring | 333 | 75 | 2 |
| Crystal Ring | 334 | 75 | 2 |
| Platinum Ring | 335 | 75 | 2 |
| Fairy's Ring | 337 | 75 | 2 |
| Pocklekul | 343 | 80 | 2 |
| Thorn Armlet | 344 | 85 | 2 |
| Jackal | 365 | 85 | 2 |
| Blessing Gun | 368 | 50 | 2 |
| Skunk | 369 | 85 | 2 |
| G Crusher | 370 | 85 | 2 |
| Hexa Blaster | 371 | 90 | 2 |
| Snail | 374 | 80 | 2 |
| Swallow | 375 | 85 | 2 |
