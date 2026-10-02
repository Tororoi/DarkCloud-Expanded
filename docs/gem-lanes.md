# Gem lanes: a second borrowed effect in idle gem slots

`Weapons/GemLanes.cs`. The thrown-gem effects are five resident `CSHOT_EFFECT` instances (`MasekiEffect`, 0x21E5B380 +
element × 0xA160 — Fire, Ice, Thunder, Wind, Holy), stepped and drawn by the dungeon loop every frame. Only the three
active-item slots can throw a gem, so at least two of the five are always idle. A LANE takes one idle slot and keeps an
ability's effect entered in it: two lanes × 8 sub-shots. Babel's Spear uses them for the stars over every confused enemy
(`ConfusionStars`) while its beam keeps the second main-character instance.

## Entering

The ISO loader cave (`borrowed_shots_enter.s`) serves one request block (`CodeCaves.BorrowedShotBlock`). A lane borrows it
from `BorrowedShots`' loop (`GemLanes.Tick` first in every pass; nothing else touches the block while a lane request is in
flight), and only while the block's own effect sits still — nothing seeded, or entered in the second instance with
`SecondEffectLive` 0 (that instance reads its config through the block while stepped; the main instance is stepped every
frame, so a main-instance effect never lends the block):

1. the block saved (all 0x2C0 B);
2. the lane's request written: the prepared config, the container, the gem slot as the instance, main flag 0, 8 sub-shots
   (+0x2BC — the cave's `Entry2` count, 6 for every other request), reserve 4,096 units, and its own region (zeroed the first
   time on a floor → the cave carves a fresh one; the saved one after), magic last;
3. on state 1: the block restored (it names its own instance again), the lane's config copied to `CodeCaves.GemLaneCfg` and
   the gem slot's config pointer (+0) pointed at it — so the block's config can change under it — and the region saved;
4. on −1 or no answer in 3 s: the block restored; the cave empties the slot before it tries, so its saved fields go back.

## Giving a slot back

Before a lane first takes a slot it saves the slot's own fields (the whole 0xA160-byte instance), read only while the slot is
genuinely the gem's (its config pointer is the static descriptor `MyEntryEffect_Maseki0e`, 0x21DC2230 + e × 0x70) and none of
its bursts is live. A lane's entry only re-points the slot — the gem's model, motions and textures stay in the maseki arena and
its texture block — so handing the slot back is writing those fields back: its stars let go first (`GemLanes.Releasing`:
`ConfusionStars` clears their follow entries, which write into it), every sub-shot off, the fields, the config pointer last.
No loader, no allocation, so it happens at once — the moment that gem is equipped, even while the beam plays. (Re-entering
`maseki_ex.chr` through the loader instead hung the game: a 4,096-unit region overflowed, and the allocator answers an
overflow with an endless loop.) An unwanted effect (2 s after its last `Want`) gives every slot back the same way. A lane that
lost its slot takes another idle one (Holy, Thunder, Fire, then Ice and Wind — `GemBurst`'s own users) when the block is next
free. The floor loader refills every gem slot on a new floor; a lane whose slot no longer points at its config copy starts
over. `GemBurst.Show` refuses a slot a lane holds.

## The stars

`ConfusionStars`: one sub-shot per confused live enemy (`BorrowedShots.BurstIn` into the first lane with a free sub-shot),
carried by the follow cave (`CodeCaves.FollowTable`, 16 entries, entry = enemy slot; `follow.s` walks the table every frame)
at the authored head height + 3, grown to 1.5× over 0.25 s and scaled with the spear's fade, its clip rewound before it ends
(re-armed if the engine retires it). Every phase radius zeroed: no damage.

A gem thrown from the item menu while its slot holds the stars (if the game allows it unequipped) would play the stars'
sub-shots — not guarded.
