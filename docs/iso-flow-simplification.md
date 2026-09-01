# ISO-flow simplification pass (open design item)

Salvaged from the retired root `TODO.txt` (item [B]) when the `feat/custom-fishing` branch was
cleaned up — the rest of that file was a stale 2026-07 manual test script for the pre-ISO-bake
camera work.

Audit the mod for places the old "read the disc / bundle assets / hardcode a dev path" pattern
could be simplified now that:

- (a) original game data comes from the player's own ISO,
- (b) `game_data/` holds all non-distributable inputs, and
- (c) the ISO select+patch flow now exists (`IsoPatcher.Patch`, wired from `ModWindow`).

In particular revisit `GameDataFiles` + the Iso9660 reader: with a user-selected, validated ISO
path we know the exact disc up front and could patch/extract deterministically instead of the
current on-the-fly `~/ROMs` / app-dir / config search.

Runtime on-demand callers today: `HarderEnemyAI`, `WeaponTextureSwap`, `CustomToanEffects`.
