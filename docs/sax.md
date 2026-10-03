# Sax — "Fine Fare"

While Toan carries the Sax (item 291) as his drawn weapon, every water in a dungeon floor's chests is Premium Water and
every food is Premium Chicken. Code: `Weapons/Toan/Sax.cs`, started from `WeaponThreads` (`case Items.sax`). No ISO
patch.

## Mechanism

Chest contents are settled once, when a floor is built: the engine fills the chest tables, then the mod's randomizer
overwrites them (`CustomChests.BasicChestRandomizer`, in the floor-entry block of `Dungeon.cs`) for the front map and the
back floor's map both. `Sax.OnFloorChestsReady()`, called right after, reads every placed item chest on both maps
(`ChestAddresses.ChestSlots`: 24 boxes × 0x40 at map+0xB680; the box's first word is its item id — or, for a mimic box,
the linked enemy slot index — with `ActiveFlag` at −0x20 (1 = placed, unopened) and `ChestSize` at +0x08 (1 = item
chest)) and plans what each qualifying one becomes, making the chance rolls there, once per chest per floor.

`Apply(rank)` writes the plan's rows the equipped sword carries (a chest still shut and still holding its original);
`Revert()` puts the originals back. They run when the floor is built with a qualifying sword out, when such a sword is
drawn (`FineFareEffect`, the thread `WeaponThreads` starts for each of the five), on a swap between two of the swords,
and when the sword is put away or another character with a plain weapon takes over. Nothing polls the chests.

| found | becomes |
|---|---|
| Regular Water (145), Tasty Water (146) | Premium Water (147) |
| Bread (148), Cheese (155) | Premium Chicken (149) |

The Dusack (293) inherits the ability and adds a row of its own (its `WeaponThreads` case starts the same thread):

| found | becomes |
|---|---|
| Treasure Key (181) | Gold Bullion (192) |

The 7 Branch Sword (292) inherits the Dusack's form and adds a chance row; the Atlamillia Sword (276) and Chronicle
Sword (297) inherit the 7 Branch Sword's form:

| found | becomes | odds |
|---|---|---|
| Repair Powder (177) | Auto Repair Powder (183) | 1 in 4, rolled once per chest when the floor is built |

Weapon chests and mimic boxes never match.

Super Steve with a sphere from any of the five swords gets that sword's form (`Sax.DriveSphere`, from the sphere dispatch;
the floor-build hook reads her sphere too), and the originals come back when the sphere goes or the dispatch ends.
