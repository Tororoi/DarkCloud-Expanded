# Orphaned enemy models

Models that EXIST in `data.dat` (`dun\monstor\*.chr`) but are referenced by **no record** in the ELF
species table (`MonstorTable`, 167 records × 0x9C @ file 0x17FC00 / RAM 0x0027FB00).

**Why this list is the interesting one.** The engine reaches a species *only* through a species-table
record. `BtLoadMonstor` reads the floor roster's `+4` field and hands it straight to `SetupBaseModel`,
which does `record = &MonstorTable + index * 0x9C` — so the roster stores a **TableIndex, not an EID**, and
there is no EID→record lookup anywhere in the spawn path. The record then names, independently:

| field | selects |
|---|---|
| `+0x00` ModelCode | `dun/monstor/<code>.chr` — the model |
| `+0x40` ScriptCode | `dun/monstor/<code>.stb` — the behaviour script |
| `+0x10/+0x20/+0x30` | up to 3 extra sub-models (multi-part bosses) |
| `+0x50…` | HP, resistances, drops, body size |

Because model and script are chosen **separately**, a record fully defines an enemy — and the game already
exploits this: records 141/142/152/153/163/164 pair model `e109a`/`e110a` with *different* scripts
(`e147a`, `e148a`, `e156a`, `e157a`, `e165a`, `e166a`). That is the Demon-Shaft "Enhanced" pattern, and it
is the template for adding a species: **existing model + existing script + new stats = a new enemy.**

(Those `e147a`-style `.chr` files DO exist as models too, but they are **duplicate containers** — identical
`info.cfg`, i.e. the same `e109a.mds` mesh and `e109a01.img` texture as the model the record actually names.
So the Enhanced mimics look the same and only *behave* differently. They are not extra appearances.)

A model with no record is therefore unreachable no matter what the spawn roster says. These are the enemies
the game built and never shipped.

## Adding one

1. **A record.** All 167 are occupied and index 167 is already other data, so you cannot simply append.
   Either repurpose an existing TableIndex at runtime (the mod already writes species-table fields — see
   `Enemies.EnableEnemyDrops`), or relocate the table into a code cave and patch the `lui`/`addiu` that
   forms `&MonstorTable` (an in-place edit at the cold window).
2. **A model.** Present — that is what this table lists.
3. **A script.** The gap for most of them. A `.stb` drives animation by **motion INDEX**, so a borrowed
   script only works if the motion tables line up. The *Script* column below says whether that is free.

> **Do NOT infer a model code from an EID.** Model codes do not reliably track enemy ids
> (e.g. `e55a` is Living Armor, id 55 — but the numbering is not a rule). Always confirm against the
> species record or the model's own `info.cfg` title.

## THE RESTORATION SCOPE

Of everything below, this is the whole list of **enemies the game built and never used**:

| Enemy | Where | Needs |
|---|---|---|
| **Bat** `e04a` (18 motions: hang / rise / dive-slam / 3 deaths) | `dun\monstor` | a species record + a script |
| **Bat** `e13a` (18 motions, simpler) | `dun\monstor` | a species record + a script |
| **Killer Snake** `e54a`/`e54b` (EID 54) | packed in `gedit\s97\chara\e116.chr` | a name alias + a record + a script |

**That is the whole list.** In particular there are NO extra appearances hiding in the orphans: `e07a__`,
`e56a_`, `e20a_` and the six `e147a`-style mimic files all name the SAME mesh and texture as an enemy that
already exists (`e07a.mds`, `e56a.mds`, `e20a.mds`, `e109a.mds`, `e110a.mds`), so they would look identical
in game. They are alternate BUILDS of the same asset, not alternate skins — adding one gains nothing.

Everything else in this document is an NPC, a boss sub-part, a prop, an effect, or a scene copy of an enemy
that already has a species record. **It is not restorable as an enemy because it is not an enemy.**

## Summary

| | count |
|---|---|
| species-table records | 167 |
| distinct models the table references | 159 |
| models shipped in `data.dat` | 176 |
| **orphaned (shipped, never referenced)** | **17** |

## The orphans

A `.chr` names its mesh and texture in its `info.cfg` (`MODEL "x.mds"`, `IMG "x01.img"`). If an orphan
names the SAME mesh and texture as an in-table model, it is not a new appearance no matter how the file
differs — it would look identical in game. That check is what the Verdict column reports.

| Model | Title (JP) | Motions | Own `.stb`? | Shares a rig with (in-table) | Verdict |
|---|---|---|---|---|---|
| `c17_beem_s` | — | 3 | yes | — | **ready** — model + its own script |
| `c17_hikari` | — | 1 | yes | — | **ready** — model + its own script |
| `c17_syougeki` | — | 3 | yes | — | **ready** — model + its own script |
| `c23_beem_s` | — | 3 | yes | — | **ready** — model + its own script |
| `e04a` | コウモリ | 18 | no | — | needs a script — no rig match |
| `e07a__` | ワーウルフ | 19 | no | `e07a`, `e125a`, `e56a` | same mesh+texture as `e07a` (a different BUILD, not a different look) |
| `e13a` | コウモリ | 18 | no | — | needs a script — no rig match |
| `e147a` | ミミック | 20 | yes | `e109a`, `e35a`, `e37a`, `e39a`, `e79a`, `e81a`, `e83a` | **duplicate container of `e109a`** — same asset, same size. No new appearance. |
| `e148a` | ビビック | 23 | yes | `e110a`, `e34a`, `e36a`, `e38a`, `e78a`, `e80a`, `e82a` | **duplicate container of `e110a`** — same asset, same size. No new appearance. |
| `e156a` | ミミック | 20 | yes | `e109a`, `e35a`, `e37a`, `e39a`, `e79a`, `e81a`, `e83a` | **duplicate container of `e109a`** — same asset, same size. No new appearance. |
| `e157a` | ビビック | 23 | yes | `e110a`, `e34a`, `e36a`, `e38a`, `e78a`, `e80a`, `e82a` | **duplicate container of `e110a`** — same asset, same size. No new appearance. |
| `e165a` | ミミック | 20 | yes | `e109a`, `e35a`, `e37a`, `e39a`, `e79a`, `e81a`, `e83a` | **duplicate container of `e109a`** — same asset, same size. No new appearance. |
| `e166a` | ビビック | 23 | yes | `e110a`, `e34a`, `e36a`, `e38a`, `e78a`, `e80a`, `e82a` | **duplicate container of `e110a`** — same asset, same size. No new appearance. |
| `e20a_` | 笑いポックル7 | 19 | no | `e17a`, `e20a`, `e62a` | same mesh+texture as `e20a` (a different BUILD, not a different look) |
| `e56a_` | シルバーウルフ | 19 | no | `e07a`, `e125a`, `e56a` | **duplicate container of `e125a`** — same asset, same size. No new appearance. |
| `e84a` | 氷の矢 | 7 | yes | — | **ready** — model + its own script |
| `i_tatumaki` | 竜巻地雷 | 3 | no | — | needs a script — no rig match |

## Motion tables

### `c17_beem_s` — (untitled)

*`dun\monstor\c17_beem_s.chr` @ data.dat 0x1B20B800 (59,344 B) · own `.stb`: **yes***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–28 | 0.5 | 発動 | 発動 |
| 1 | 28–48 | 0.5 | ループ | ループ |
| 2 | 48–70 | 0.5 | 消滅 | 消滅 |

### `c17_hikari` — (untitled)

*`dun\monstor\c17_hikari.chr` @ data.dat 0x1B21B000 (65,568 B) · own `.stb`: **yes***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 30–40 | 0.2 | — | — |

### `c17_syougeki` — (untitled)

*`dun\monstor\c17_syougeki.chr` @ data.dat 0x1B24D800 (68,880 B) · own `.stb`: **yes***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–40 | 0.35 | 発生 | 発生 |
| 1 | 40–50 | 0.35 | ループ | ループ |
| 2 | 50–70 | 0.35 | 消滅 | 消滅 |

### `c23_beem_s` — (untitled)

*`dun\monstor\c23_beem_s.chr` @ data.dat 0x20851000 (59,344 B) · own `.stb`: **yes***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–28 | 0.5 | 発動 | 発動 |
| 1 | 28–48 | 0.5 | ループ | ループ |
| 2 | 48–70 | 0.5 | 消滅 | 消滅 |

### `e04a` — コウモリ

*`dun\monstor\e04a.chr` @ data.dat 0x1B47F000 (290,448 B) · own `.stb`: **NO***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.2 | 立ち | idle |
| 1 | 40–60 | 0.2 | 歩き | walk |
| 2 | 30–50 | 0.2 | ダミー | (dummy / unused) |
| 3 | 30–50 | 0.2 | ダミー | (dummy / unused) |
| 4 | 30–50 | 0.2 | ダミー | (dummy / unused) |
| 5 | 140–150 | 0.2 | ぶら下がり | hang |
| 6 | 10–20 | 0.2 | ダミー | (dummy / unused) |
| 7 | 10–20 | 0.2 | ダミー | (dummy / unused) |
| 8 | 10–20 | 0.2 | ダミー | (dummy / unused) |
| 9 | 125–130 | 0.2 | 死亡３ 着対死亡 | death３ death (hits ground) |
| 10 | 115–125 | 0.2 | 死亡２ 落下 | death２ fall |
| 11 | 110–115 | 0.25 | 死亡 のけぞり | death recoil |
| 12 | 70–80 | 0.2 | 攻撃1 上昇 | attack1 rise |
| 13 | 80–100 | 0.2 | 攻撃2 降下体当たり | attack2 dive slam |
| 14 | 60–80 | 0.2 | ダミー | (dummy / unused) |
| 15 | 220–240 | 0.2 | 攻撃2 | attack2 |
| 16 | 220–240 | 0.2 | ダミー | (dummy / unused) |
| 17 | 220–240 | 0.2 | ダミー | (dummy / unused) |

### `e07a__` — ワーウルフ

*`dun\monstor\e07a__.chr` @ data.dat 0x203DC800 (939,008 B) · own `.stb`: **NO** · **same mesh+texture as `e07a`** — not a new appearance · motion table **identical to** `e07a`, `e125a`, `e56a` — it can run `e07a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.2 | 立ち | idle |
| 1 | 25–45 | 0.2 | 歩き | walk |
| 2 | 90–110 | 0.25 | バックステップ | back step |
| 3 | 190–210 | 0.25 | 右ステップ | right step |
| 4 | 215–235 | 0.25 | 左ステップ | left step |
| 5 | 240–250 | 0.25 | ガード入り | guard (enter) |
| 6 | 250–260 | 0.25 | ガードループ | guard (loop) |
| 7 | 260–270 | 0.25 | ガード戻り | guard (return) |
| 8 | 115–125 | 0.2 | ダメージ1 | damage1 |
| 9 | 10–20 | 0.2 | ダミー | (dummy / unused) |
| 10 | 10–20 | 0.2 | ダミー | (dummy / unused) |
| 11 | 130–160 | 0.2 | 死亡 | death |
| 12 | 160–160 | 0 | 死亡ループ | death (loop) |
| 13 | 50–70 | 0.25 | 攻撃1 | attack1 |
| 14 | 50–70 | 0.25 | ダミー | (dummy / unused) |
| 15 | 50–70 | 0.25 | ダミー | (dummy / unused) |
| 16 | 70–90 | 0.25 | 攻撃2 | attack2 |
| 17 | 70–90 | 0.25 | ダミー | (dummy / unused) |
| 18 | 70–90 | 0.25 | ダミー | (dummy / unused) |

### `e13a` — コウモリ

*`dun\monstor\e13a.chr` @ data.dat 0x1B8F4800 (455,520 B) · own `.stb`: **NO***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 140–150 | 0.1 | ぶら下がり | hang |
| 1 | 40–60 | 0.4 | はばたき | flap |
| 2 | 40–60 | 0.4 | ダミー | (dummy / unused) |
| 3 | 40–60 | 0.4 | ダミー | (dummy / unused) |
| 4 | 40–60 | 0.4 | ダミー | (dummy / unused) |
| 5 | 140–150 | 0.1 | ダミー | (dummy / unused) |
| 6 | 140–150 | 0.1 | ダミー | (dummy / unused) |
| 7 | 140–150 | 0.1 | ダミー | (dummy / unused) |
| 8 | 140–150 | 0.1 | ダミー | (dummy / unused) |
| 9 | 140–150 | 0.1 | ダミー | (dummy / unused) |
| 10 | 140–150 | 0.1 | ダミー | (dummy / unused) |
| 11 | 110–134 | 0.2 | 死亡 | death |
| 12 | 70–100 | 0.3 | 攻撃1 | attack1 |
| 13 | 70–100 | 0.3 | ダミー | (dummy / unused) |
| 14 | 70–100 | 0.3 | ダミー | (dummy / unused) |
| 15 | 70–100 | 0.3 | ダミー | (dummy / unused) |
| 16 | 70–100 | 0.3 | ダミー | (dummy / unused) |
| 17 | 70–100 | 0.3 | ダミー | (dummy / unused) |

### `e147a` — ミミック

*`dun\monstor\e147a.chr` @ data.dat 0x1FA37800 (298,720 B) · own `.stb`: **yes** · **same mesh+texture as `e109a`** — not a new appearance (byte-size identical: a duplicate container) · motion table **identical to** `e109a`, `e35a`, `e37a`, `e39a`, `e79a`, `e81a`, `e83a` — it can run `e109a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 40–50 | 0.2 | 立ち | idle |
| 1 | 60–70 | 0.25 | 移動ループ前 | 移動ループ前 |
| 2 | 80–90 | 0.25 | 移動ループ後 | 移動ループ後 |
| 3 | 100–110 | 0.25 | 移動ループ右 | 移動ループ右 |
| 4 | 120–130 | 0.25 | 移動ループ左 | 移動ループ左 |
| 5 | 170–175 | 0.3 | ガード入り | guard (enter) |
| 6 | 175–185 | 0.3 | ガードループ | guard (loop) |
| 7 | 185–190 | 0.3 | ガード戻り | guard (return) |
| 8 | 200–207 | 0.3 | ダメージ | damage |
| 9 | 40–50 | 0.2 | ダミー | (dummy / unused) |
| 10 | 40–50 | 0.2 | ダミー | (dummy / unused) |
| 11 | 220–235 | 0.3 | 死亡 | death |
| 12 | 140–160 | 0.2 | 攻撃1 | attack1 |
| 13 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 14 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 15 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 16 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 17 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 18 | 10–28 | 0.25 | 出現 | 出現 |
| 19 | 10–10 | 0 | まちかまえ | まちかまえ |

### `e148a` — ビビック

*`dun\monstor\e148a.chr` @ data.dat 0x1FA86800 (491,328 B) · own `.stb`: **yes** · **same mesh+texture as `e110a`** — not a new appearance (byte-size identical: a duplicate container) · motion table **identical to** `e110a`, `e34a`, `e36a`, `e38a`, `e78a`, `e80a`, `e82a` — it can run `e110a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 35–45 | 0.2 | 立ち | idle |
| 1 | 55–65 | 0.2 | 移動ループ前 | 移動ループ前 |
| 2 | 75–85 | 0.2 | 移動ループ後 | 移動ループ後 |
| 3 | 95–105 | 0.2 | 移動ループ右 | 移動ループ右 |
| 4 | 115–125 | 0.2 | 移動ループ左 | 移動ループ左 |
| 5 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 6 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 7 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 8 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 9 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 10 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 11 | 200–220 | 0.2 | 死亡 | death |
| 12 | 135–152 | 0.2 | 攻撃1 | attack1 |
| 13 | 135–152 | 0.2 | ダミー | (dummy / unused) |
| 14 | 135–152 | 0.2 | ダミー | (dummy / unused) |
| 15 | 160–174 | 0.2 | 攻撃2 | attack2 |
| 16 | 160–174 | 0.2 | ダミー | (dummy / unused) |
| 17 | 160–174 | 0.2 | ダミー | (dummy / unused) |
| 18 | 180–194 | 0.2 | 攻撃3 | attack3 |
| 19 | 180–194 | 0.2 | ダミー | (dummy / unused) |
| 20 | 180–194 | 0.2 | ダミー | (dummy / unused) |
| 21 | 10–27 | 0.25 | 出現 | 出現 |
| 22 | 10–10 | 0 | 待ち構え | 待ち構え |

### `e156a` — ミミック

*`dun\monstor\e156a.chr` @ data.dat 0x1FE52800 (298,720 B) · own `.stb`: **yes** · **same mesh+texture as `e109a`** — not a new appearance (byte-size identical: a duplicate container) · motion table **identical to** `e109a`, `e35a`, `e37a`, `e39a`, `e79a`, `e81a`, `e83a` — it can run `e109a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 40–50 | 0.2 | 立ち | idle |
| 1 | 60–70 | 0.25 | 移動ループ前 | 移動ループ前 |
| 2 | 80–90 | 0.25 | 移動ループ後 | 移動ループ後 |
| 3 | 100–110 | 0.25 | 移動ループ右 | 移動ループ右 |
| 4 | 120–130 | 0.25 | 移動ループ左 | 移動ループ左 |
| 5 | 170–175 | 0.3 | ガード入り | guard (enter) |
| 6 | 175–185 | 0.3 | ガードループ | guard (loop) |
| 7 | 185–190 | 0.3 | ガード戻り | guard (return) |
| 8 | 200–207 | 0.3 | ダメージ | damage |
| 9 | 40–50 | 0.2 | ダミー | (dummy / unused) |
| 10 | 40–50 | 0.2 | ダミー | (dummy / unused) |
| 11 | 220–235 | 0.3 | 死亡 | death |
| 12 | 140–160 | 0.2 | 攻撃1 | attack1 |
| 13 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 14 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 15 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 16 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 17 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 18 | 10–28 | 0.25 | 出現 | 出現 |
| 19 | 10–10 | 0 | まちかまえ | まちかまえ |

### `e157a` — ビビック

*`dun\monstor\e157a.chr` @ data.dat 0x1FEA1800 (491,328 B) · own `.stb`: **yes** · **same mesh+texture as `e110a`** — not a new appearance (byte-size identical: a duplicate container) · motion table **identical to** `e110a`, `e34a`, `e36a`, `e38a`, `e78a`, `e80a`, `e82a` — it can run `e110a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 35–45 | 0.2 | 立ち | idle |
| 1 | 55–65 | 0.2 | 移動ループ前 | 移動ループ前 |
| 2 | 75–85 | 0.2 | 移動ループ後 | 移動ループ後 |
| 3 | 95–105 | 0.2 | 移動ループ右 | 移動ループ右 |
| 4 | 115–125 | 0.2 | 移動ループ左 | 移動ループ左 |
| 5 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 6 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 7 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 8 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 9 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 10 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 11 | 200–220 | 0.2 | 死亡 | death |
| 12 | 135–152 | 0.2 | 攻撃1 | attack1 |
| 13 | 135–152 | 0.2 | ダミー | (dummy / unused) |
| 14 | 135–152 | 0.2 | ダミー | (dummy / unused) |
| 15 | 160–174 | 0.2 | 攻撃2 | attack2 |
| 16 | 160–174 | 0.2 | ダミー | (dummy / unused) |
| 17 | 160–174 | 0.2 | ダミー | (dummy / unused) |
| 18 | 180–194 | 0.2 | 攻撃3 | attack3 |
| 19 | 180–194 | 0.2 | ダミー | (dummy / unused) |
| 20 | 180–194 | 0.2 | ダミー | (dummy / unused) |
| 21 | 10–27 | 0.25 | 出現 | 出現 |
| 22 | 10–10 | 0 | 待ち構え | 待ち構え |

### `e165a` — ミミック

*`dun\monstor\e165a.chr` @ data.dat 0x202CB000 (298,720 B) · own `.stb`: **yes** · **same mesh+texture as `e109a`** — not a new appearance (byte-size identical: a duplicate container) · motion table **identical to** `e109a`, `e35a`, `e37a`, `e39a`, `e79a`, `e81a`, `e83a` — it can run `e109a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 40–50 | 0.2 | 立ち | idle |
| 1 | 60–70 | 0.25 | 移動ループ前 | 移動ループ前 |
| 2 | 80–90 | 0.25 | 移動ループ後 | 移動ループ後 |
| 3 | 100–110 | 0.25 | 移動ループ右 | 移動ループ右 |
| 4 | 120–130 | 0.25 | 移動ループ左 | 移動ループ左 |
| 5 | 170–175 | 0.3 | ガード入り | guard (enter) |
| 6 | 175–185 | 0.3 | ガードループ | guard (loop) |
| 7 | 185–190 | 0.3 | ガード戻り | guard (return) |
| 8 | 200–207 | 0.3 | ダメージ | damage |
| 9 | 40–50 | 0.2 | ダミー | (dummy / unused) |
| 10 | 40–50 | 0.2 | ダミー | (dummy / unused) |
| 11 | 220–235 | 0.3 | 死亡 | death |
| 12 | 140–160 | 0.2 | 攻撃1 | attack1 |
| 13 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 14 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 15 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 16 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 17 | 140–160 | 0.2 | ダミー | (dummy / unused) |
| 18 | 10–28 | 0.25 | 出現 | 出現 |
| 19 | 10–10 | 0 | まちかまえ | まちかまえ |

### `e166a` — ビビック

*`dun\monstor\e166a.chr` @ data.dat 0x2031A000 (491,328 B) · own `.stb`: **yes** · **same mesh+texture as `e110a`** — not a new appearance (byte-size identical: a duplicate container) · motion table **identical to** `e110a`, `e34a`, `e36a`, `e38a`, `e78a`, `e80a`, `e82a` — it can run `e110a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 35–45 | 0.2 | 立ち | idle |
| 1 | 55–65 | 0.2 | 移動ループ前 | 移動ループ前 |
| 2 | 75–85 | 0.2 | 移動ループ後 | 移動ループ後 |
| 3 | 95–105 | 0.2 | 移動ループ右 | 移動ループ右 |
| 4 | 115–125 | 0.2 | 移動ループ左 | 移動ループ左 |
| 5 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 6 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 7 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 8 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 9 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 10 | 35–45 | 0.2 | ダミー | (dummy / unused) |
| 11 | 200–220 | 0.2 | 死亡 | death |
| 12 | 135–152 | 0.2 | 攻撃1 | attack1 |
| 13 | 135–152 | 0.2 | ダミー | (dummy / unused) |
| 14 | 135–152 | 0.2 | ダミー | (dummy / unused) |
| 15 | 160–174 | 0.2 | 攻撃2 | attack2 |
| 16 | 160–174 | 0.2 | ダミー | (dummy / unused) |
| 17 | 160–174 | 0.2 | ダミー | (dummy / unused) |
| 18 | 180–194 | 0.2 | 攻撃3 | attack3 |
| 19 | 180–194 | 0.2 | ダミー | (dummy / unused) |
| 20 | 180–194 | 0.2 | ダミー | (dummy / unused) |
| 21 | 10–27 | 0.25 | 出現 | 出現 |
| 22 | 10–10 | 0 | 待ち構え | 待ち構え |

### `e20a_` — 笑いポックル7

*`dun\monstor\e20a_.chr` @ data.dat 0x1E1C9800 (494,784 B) · own `.stb`: **NO** · **same mesh+texture as `e20a`** — not a new appearance · motion table **identical to** `e17a`, `e20a`, `e62a` — it can run `e20a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.2 | 立ち | idle |
| 1 | 30–50 | 0.3 | 歩き | walk |
| 2 | 60–70 | 0.23 | バックステップ | back step |
| 3 | 80–90 | 0.23 | 右ステップ | right step |
| 4 | 100–110 | 0.23 | 左ステップ | left step |
| 5 | 120–125 | 0.25 | ガード入り | guard (enter) |
| 6 | 125–135 | 0.25 | ガードループ | guard (loop) |
| 7 | 135–140 | 0.25 | ガード戻り | guard (return) |
| 8 | 150–160 | 0.25 | ダメージ１ | damage１ |
| 9 | 170–190 | 0.25 | ダメージ２ | damage２ |
| 10 | 200–220 | 0.18 | 起き上がり | get up |
| 11 | 230–245 | 0.25 | 死亡 | death |
| 12 | 245–245 | 0 | 死亡ループ | death (loop) |
| 13 | 255–280 | 0.35 | 攻撃7（パンチ） | attack7（punch） |
| 14 | 255–280 | 0.35 | 攻撃7予備動作１ | attack7(windup)１ |
| 15 | 255–280 | 0.35 | 攻撃7予備動作２ | attack7(windup)２ |
| 16 | 290–302 | 0.35 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる）16 | attack8（steal/body slam。盗ったら早walkで逃げる）16 |
| 17 | 290–302 | 0.35 | 攻撃8予備動作１ | attack8(windup)１ |
| 18 | 290–302 | 0.35 | 攻撃8予備動作２ | attack8(windup)２ |

### `e56a_` — シルバーウルフ

*`dun\monstor\e56a_.chr` @ data.dat 0x20599800 (932,544 B) · own `.stb`: **NO** · **same mesh+texture as `e125a`** — not a new appearance (byte-size identical: a duplicate container) · motion table **identical to** `e07a`, `e125a`, `e56a` — it can run `e56a.stb` unchanged*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.2 | 立ち | idle |
| 1 | 25–45 | 0.35 | 歩き | walk |
| 2 | 90–110 | 0.25 | バックステップ | back step |
| 3 | 190–210 | 0.25 | 右ステップ | right step |
| 4 | 215–235 | 0.25 | 左ステップ | left step |
| 5 | 240–250 | 0.25 | ガード入り | guard (enter) |
| 6 | 250–260 | 0.25 | ガードループ | guard (loop) |
| 7 | 260–270 | 0.25 | ガード戻り | guard (return) |
| 8 | 115–125 | 0.2 | ダメージ1 | damage1 |
| 9 | 10–20 | 0.2 | ダミー | (dummy / unused) |
| 10 | 10–20 | 0.2 | ダミー | (dummy / unused) |
| 11 | 130–160 | 0.2 | 死亡 | death |
| 12 | 160–160 | 0 | 死亡ループ | death (loop) |
| 13 | 50–70 | 0.25 | 攻撃1 | attack1 |
| 14 | 50–70 | 0.25 | ダミー | (dummy / unused) |
| 15 | 50–70 | 0.25 | ダミー | (dummy / unused) |
| 16 | 70–90 | 0.25 | 攻撃2 | attack2 |
| 17 | 70–90 | 0.25 | ダミー | (dummy / unused) |
| 18 | 70–90 | 0.25 | ダミー | (dummy / unused) |

### `e84a` — 氷の矢

*`dun\monstor\e84a.chr` @ data.dat 0x1DDE2800 (58,416 B) · own `.stb`: **yes***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–10 | 0 | 立ち | idle |
| 1 | 10–20 | 0.2 | 立ちループ | idleループ |
| 2 | 30–40 | 0.2 | 90度前傾 | 90度前傾 |
| 3 | 40–50 | 0.2 | 前傾ループ | 前傾ループ |
| 4 | 55–65 | 0.2 | 前傾破壊 | 前傾破壊 |
| 5 | 70–80 | 0.2 | 立ち破壊 | idle破壊 |
| 6 | 85–95 | 0.1 | 出現 | 出現 |

### `i_tatumaki` — 竜巻地雷

*`dun\monstor\i_tatumaki.chr` @ data.dat 0x1E38B800 (117,776 B) · own `.stb`: **NO***

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–30 | 0.3 | 柱出現 | 柱出現 |
| 1 | 40–50 | 0.5 | 竜巻 | 竜巻 |
| 2 | 60–75 | 0.3 | 竜巻消える3 | 竜巻消える3 |

## Models OUTSIDE `dun\monstor` — and which of them are ENEMIES

The scene folders (`gedit\*\chara\`, `dun\d01\event\`) hold the **cutscene cast**: story NPCs, per-scene
copies of the party, boss sub-parts, props and effects. Only a handful are creatures the game means to
FIGHT, and that is the only part of this list that matters for restoring enemies.

The honest test is the **motion vocabulary** — anything meant to fight has attack / damage / death / guard /
battle motions; an NPC has idle, walk and talk. Of **326** distinct `e*`-coded models out here, only **18**
have any combat motion at all.

| Model | Title (JP) | Packs | Combat motions | What it actually is |
|---|---|---|---|---|
| `e101` | トアン | `e101c01d`, `e101c06a` | バトル | Toan/Goro cutscene battle poses — party, not an enemy |
| `e101eb` | ゴロー | `e101ebc01d`, `e101ebc06a` | バトル | Goro cutscene battle poses — party |
| `e116` | キラースネイク | `e116e54b`, `e116e54a`, `e116c01d` | バトル | **KILLER SNAKE — the one genuine unused enemy.** Meshes `e54a`/`e54b` = EID 54. |
| `e117_0` | トアン | `e117c01d`, `e117e54a`, `e117e54b` | バトル | another `s97` bundle packing the same `e54a`/`e54b` Killer Snake meshes |
| `e15c12a` | ドラン | `e15c12a` | ダメージ 死亡 | Dran (`c12a`) — a boss that ALREADY has a species record |
| `e17a` | 笑いポックル4 | `e17a` | 攻撃 ダメージ 死亡 ガード 起き上がり | 笑いポックル4 — already in the species table |
| `e17c01d` | イベント17トアン | `e17c01d` | のけぞり | Toan — party |
| `e17eb` | シーダ | `e17ebc07a`, `e17ebc01d` | バトル | Cedric (シーダ) — story NPC |
| `e23` | シーダ | `e23c07a`, `e23c01d` | のけぞり | Cedric (シーダ) — story NPC |
| `e24c12a` | ドラン | `e24c12a` | ダメージ 死亡 | Dran (`c12a`) — already in the species table |
| `e25a` | 大砲 | `e25a` | 攻撃 ダメージ 死亡 | 大砲 (cannon) — already in the species table |
| `e28e03a` | e28e03aスケルトン | `e28e03a` | 起き上がり | skeleton (`e03a`) — already in the species table |
| `e28eb` | — | `e28ebe01a`, `e28ebc01d` | バトル | event bundle (`e01a` skeleton + Toan) |
| `e306c15c` | 王の呪い | `e306c15c` | 攻撃 | King's Curse phase part (`c15c`) — the boss already exists |
| `e325` | e325エンガ | `e325p54a`, `e325c01d` | のけぞり | エンガ (`p54a`) — a story NPC from Ungaga's chapter, not an enemy |
| `e411drobo` | ロボ | `e411drobo` | ガード | ロボ — a scripted set-piece robot from the `s37`/`s40` event |
| `e502c17a` | 新魔人 | `e502c17a` | バトル | 新魔人 (`c17a`) — the boss already exists |
| `e503c17a` | 新魔人 | `e503c17a` | バトル | 新魔人 (`c17a`) — the boss already exists |

### The rest — every other `e*` model out there (no combat motions)

Documented for completeness. These are NPCs, boss sub-parts, props and effects — **none is an enemy** —
but this is the full inventory of what the scan found, 308 distinct models. *Packs* lists the characters
bundled inside each `.chr`, which is how the cutscene bundles reveal their real cast.

| Model | Title (JP) | Motions | Packs | First seen in |
|---|---|---|---|---|
| `e02` | 村長 | 13 | `e02c01d`, `e02p14a` | `gedit\e01\chara` |
| `e03` | 精霊王 | 20 | `e03c03c`, `e03c01d` | `gedit\e01\chara` |
| `e04` | — | 20 | `e04c04cat`, `e04c04b` | `gedit\e01\chara` |
| `e04c01d` | トアン | 8 | `e04c01d` | `gedit\e01\chara` |
| `e04c03c` | 精霊王 | 3 | `e04c03c` | `gedit\e01\chara` |
| `e04c04b` | シャオ | 12 | `e04c04b` | `gedit\e01\chara` |
| `e04c04cat` | — | 8 | `e04c04cat` | `gedit\e01\chara` |
| `e06` | — | 9 | `e06c04cat`, `e06c01d` | `gedit\e01\chara` |
| `e06p10a` | レネ | 5 | `p10a` | `gedit\e01\chara` |
| `e07` | — | 17 | `e07c01d`, `e07p01a`, `e07p07a` | `gedit\e01\chara` |
| `e08` | キャラクタ情報 | 12 | `e08p03a`, `e08c01d`, `e08p04a` | `gedit\e01\chara` |
| `e09` | — | 18 | `e09c01d`, `e09p09a` | `gedit\e01\chara` |
| `e10` | キャラクタ情報 | 10 | `e10p12a`, `e10c01d` | `gedit\e01\chara` |
| `e100` | — | 4 | `e100c01d` | `gedit\e02\chara` |
| `e102` | ゴロー | 12 | `e102c06a`, `e102c01d` | `gedit\s01\chara` |
| `e103` | ゴロー | 8 | `e103c01d`, `e103c06a` | `gedit\s01\chara` |
| `e105` | ゴロー | 25 | `e105c06b`, `e105p20a` | `gedit\s01\chara` |
| `e105ex` | e105ex | 2 | `e105ex` | `gedit\s01\chara` |
| `e105img` | — | 0 | — | `gedit\s01\chara` |
| `e106` | トアン | 2 | `e106c01d` | `gedit\s03\chara` |
| `e106zyu` | — | 6 | `e106zyu` | `gedit\s03\chara` |
| `e107` | — | 2 | `e107c01d` | `gedit\s03\chara` |
| `e108` | トアン | 16 | `e108c01d`, `e108w11` | `gedit\s03\chara` |
| `e108ex` | くしゃみ | 0 | — | `gedit\s03\chara` |
| `e108ex_2` | くしゃみ | 0 | — | `gedit\s03\chara` |
| `e109` | トアン | 2 | `e109c01d` | `gedit\s03\chara` |
| `e11` | — | 6 | `e11c01d` | `gedit\e01\chara` |
| `e110` | — | 6 | `e110c01d` | `gedit\s96\chara` |
| `e110ex` | e110ex | 2 | `e110ex_01`, `e110ex_02` | `gedit\s96\chara` |
| `e111` | トアン | 29 | `e111c01d`, `e111c14a`, `e111c14b`, `e111c14c`, `e111c14d` | `gedit\s96\chara` |
| `e111c14p` | — | 1 | `e111c14p` | `gedit\s96\chara` |
| `e112` | トアン | 38 | `e112c01d`, `e112c14a`, `e112c14b`, `e112c14c`, `e112c14d` | `gedit\s96\chara` |
| `e113c01d` | トアン | 3 | `e113c01d` | `gedit\s04\chara` |
| `e113p65a` | 長老 | 8 | `e113p65a` | `gedit\s04\chara` |
| `e113p66a` | ブラウン一般１ | 7 | `e113p66a` | `gedit\s04\chara` |
| `e113p67a` | ブラウン一般２（ジョスカ） | 7 | `e113p67a` | `gedit\s04\chara` |
| `e113p73a` | テオ | 7 | `e113p73a` | `gedit\s04\chara` |
| `e114a_ex` | — | 2 | `e114a_ex` | `dun\effect` |
| `e114c01d` | トアン | 16 | `e114c01d` | `gedit\s04\chara` |
| `e114c08a` | 魔神 | 1 | `e114c08a` | `gedit\s28\chara` |
| `e114c08b` | 魔神 | 3 | `e114c08b` | `gedit\s28\chara` |
| `e114c08c` | 魔神 | 3 | `e114c08c` | `gedit\s28\chara` |
| `e114ex` | 回る星 | 1 | `e114ex` | `gedit\s04\chara` |
| `e114kinomi` | 木の実 | 0 | — | `gedit\s04\chara` |
| `e114p65a` | 長老 | 11 | `e114p65a` | `gedit\s04\chara` |
| `e114p66a` | みみいっぽん | 9 | `e114p66a` | `gedit\s04\chara` |
| `e114p67a` | みみにほん | 10 | `e114p67a` | `gedit\s04\chara` |
| `e114p73a` | テオ | 19 | `e114p73a` | `gedit\s04\chara` |
| `e115` | トアン | 9 | `e115c01d`, `e115e54a` | `gedit\s97\chara` |
| `e115a_ex` | — | 2 | `e115a_ex` | `dun\effect` |
| `e117_1` | トアン | 8 | `e117c01d1` | `gedit\s97\chara` |
| `e12` | オルネットイベント用 | 19 | `e12p05a`, `e12p08a`, `e12c01d` | `gedit\e01\chara` |
| `e120` | パオ | 15 | `e120p24a`, `e120c01d` | `gedit\e02\chara` |
| `e121` | カカオ | 15 | `e121p28a` | `gedit\e02\chara` |
| `e122` | ブンブク | 12 | `e122p30a`, `e122p29a` | `gedit\e02\chara` |
| `e123` | モモ | 31 | `e123p23a`, `e123p26a`, `e123c01d` | `gedit\e02\chara` |
| `e124` | バロン | 13 | `e124p27a`, `e124c01d` | `gedit\e02\chara` |
| `e125` | くすくす | 14 | `e125p31a`, `e125c01d` | `gedit\e02\chara` |
| `e126` | ガブ | 22 | `e126p25a`, `e126c01d`, `e126w07` | `gedit\e02\chara` |
| `e127` | ロウ | 21 | `e127p21a`, `e127p22a` | `gedit\e02\chara` |
| `e127_01` | トアン | 7 | `e127c01d` | `gedit\e02\chara` |
| `e128` | ブンブク | 27 | `e128p30a`, `e128p29a`, `e128c01d` | `gedit\e02\chara` |
| `e129` | はぐれふくろう | 22 | `e129p32a`, `e129c01d` | `gedit\e02\chara` |
| `e13` | キャラクタ情報 | 14 | `e13p02a`, `e13p06a` | `gedit\e01\chara` |
| `e130` | トアン | 19 | `e130c01d`, `e130c06c` | `gedit\s03\chara` |
| `e130zyu` | — | 6 | `e130zyu` | `gedit\s03\chara` |
| `e131` | 笑いポックル7 | 12 | `e131e14a` | `gedit\e02\chara` |
| `e132` | トアン | 6 | `e132c01d` | `gedit\s23\chara` |
| `e15c01d` | e15トアン | 10 | `e15c01d` | `gedit\s98\chara` |
| `e15c03c` | 精霊王 | 1 | `e15c03c` | `gedit\s98\chara` |
| `e16` | ドラン＿ノルン村用 | 27 | `e16c01d`, `e16c12a`, `e16c12a` | `gedit\s98\chara` |
| `e17c04cat` | シャオ猫 | 8 | `e17c04cat` | `gedit\s99\chara` |
| `e17exkumo` | e17exkumo | 1 | `e17exkumo` | `gedit\s99\chara` |
| `e200` | — | 5 | `e200c01d` | `gedit\e03\chara` |
| `e201` | ランド | 38 | `e201p38a`, `e201c01d` | `gedit\s09\chara` |
| `e202` | — | 6 | `e202p`, `e202c01d`, `e202kame` | `gedit\s09\chara` |
| `e203` | — | 6 | `e203p`, `e202kame`, `e203c01d` | `gedit\s09\chara` |
| `e204c01d` | — | 4 | `e204c01d` | `gedit\e03\chara` |
| `e204c08a` | 魔神 | 10 | `e204c08a` | `gedit\s28\chara` |
| `e204c08b` | 魔神 | 5 | `e204c08b` | `gedit\s28\chara` |
| `e204c08c` | 魔神 | 5 | `e204c08c` | `gedit\s28\chara` |
| `e204c09a` | フラッグ | 6 | `e204c09a` | `gedit\s28\chara` |
| `e204p37a` | レイナ | 5 | `e204p37a` | `gedit\s33\chara` |
| `e204p45a` | ヤヤ | 8 | `e204p45a` | `gedit\e03\chara` |
| `e207` | — | 2 | `e207c01d` | `gedit\s09\chara` |
| `e207ex` | ピカー | 2 | `e207ex` | `gedit\s09\chara` |
| `e208c01d` | キャラクタ情報 | 6 | `e208c01d` | `gedit\s34\chara` |
| `e208c13a` | ｃ13ａ氷の女王 | 2 | `e208c13a` | `gedit\s34\chara` |
| `e208c19a` | サイア | 4 | `e208c19a` | `gedit\s34\chara` |
| `e208ex1` | e208ex1 | 2 | `e208ex1` | `gedit\s34\chara` |
| `e208ex2` | e208ex2 | 2 | `e208ex2` | `gedit\s34\chara` |
| `e209ball_b` | 命の玉 | 1 | `e209ball_b` | `gedit\s34\chara` |
| `e209c01d` | — | 4 | `e209c01d` | `gedit\s34\chara` |
| `e209c13a` | ｃ13ａ氷の女王 | 6 | `e209c13a` | `gedit\s34\chara` |
| `e209c19a` | サイア | 10 | `e209c19a` | `gedit\s34\chara` |
| `e209p38a` | ランド | 15 | `e209p38a` | `gedit\s34\chara` |
| `e209p77a` | 若ランド | 15 | `e209p77a` | `gedit\s34\chara` |
| `e211` | トアン | 52 | `e211c01d`, `e211p65a`, `e211p73a` | `gedit\s04\chara` |
| `e212` | レイナ | 8 | `e212p37a`, `e212c01d` | `gedit\e03\chara` |
| `e213c01d` | キャラクタ情報 | 6 | `e213c01d` | `gedit\s34\chara` |
| `e213c13a` | ｃ13ａ氷の女王 | 2 | `e213c13a` | `gedit\s34\chara` |
| `e213c19a` | サイア | 4 | `e213c19a` | `gedit\s34\chara` |
| `e213c19p` | — | 1 | `e213c19p` | `gedit\s34\chara` |
| `e214` | コルセア | 15 | `e214e28a`, `e214e27a` | `gedit\s95\chara` |
| `e21c01d` | トアン | 10 | `e21c01d` | `gedit\e01\chara` |
| `e21c12a` | ドラン | 10 | `e21c12a` | `gedit\e01\chara` |
| `e22` | イベント22トアン | 10 | `e22c01d`, `e22c07a` | `gedit\s99\chara` |
| `e220` | ルーティ | 9 | `e220p35a` | `gedit\e03\chara` |
| `e221c01d` | — | 4 | `e221c01d` | `gedit\e03\chara` |
| `e221p36a` | スージー | 20 | `e221p36a` | `gedit\e03\chara` |
| `e222` | レイナ | 8 | `e222p37a` | `gedit\e03\chara` |
| `e223` | ジャック | 32 | `e223p49a`, `e223c01d` | `gedit\e03\chara` |
| `e223c05a` | ルビー | 9 | `e223c05a` | `gedit\e03\chara` |
| `e224` | ジョーカー | 26 | `e224p41a`, `e224c01d` | `gedit\e03\chara` |
| `e226` | フィル | 14 | `e226p42a`, `e226c01d` | `gedit\e03\chara` |
| `e227` | e27バスカー | 19 | `e227c01d`, `e227p39a` | `gedit\e03\chara` |
| `e228c01d` | キャラクタ情報 | 18 | `e228c01d` | `gedit\e03\chara` |
| `e228c05a` | ルビー | 21 | `e228c05a` | `gedit\e03\chara` |
| `e228ex` | ボワン | 1 | `e228ex` | `gedit\e03\chara` |
| `e228lamp` | ランプ | 2 | `e228lamp` | `gedit\e03\chara` |
| `e228p33a` | キング | 42 | `e228p33a` | `gedit\e03\chara` |
| `e228p40a` | スチュー | 13 | `e228p40a` | `gedit\e03\chara` |
| `e228p43a` | ジェイク | 10 | `e228p43a` | `gedit\e03\chara` |
| `e229c01d` | — | 1 | `e229c01d` | `gedit\e03\chara` |
| `e229p34a` | サム | 12 | `e229p34a` | `gedit\e03\chara` |
| `e229p44a` | ワイルダー | 19 | `e229p44a` | `gedit\e03\chara` |
| `e232c01d` | e232トアン拡張 | 6 | `e232c01d` | `gedit\e03\chara` |
| `e232p34a` | e232p34aサム | 8 | `e232p34a` | `gedit\e03\chara` |
| `e232p44a` | e232p44aワイルダー | 14 | `e232p44a` | `gedit\e03\chara` |
| `e24c01d` | e24トアン | 10 | `e24c01d` | `gedit\s98\chara` |
| `e25` | キャラクタ情報 | 15 | `e25p02a`, `e25c01d` | `gedit\e01\chara` |
| `e26` | はだかクロード | 22 | `e26c01d`, `e26p81a` | `gedit\e01\chara` |
| `e28c01d` | e28c01dﾄｱﾝ拡張 | 11 | `e28c01d` | `dun\d01\event` |
| `e28c03c` | e28c03c精霊王 | 4 | `e28c03c` | `dun\d01\event` |
| `e28e01a` | e28e01aスケルトンナイト | 7 | `e28e01a` | `dun\d01\event` |
| `e28eff` | effect | 2 | `rm04ex` | `dun\d01\event` |
| `e28ibox2` | 宝箱 | 1 | `e28ibox` | `dun\d01\event` |
| `e29` | トアン | 14 | `e29c01d`, `e29c03c` | `gedit\s99\chara` |
| `e30` | 精霊王 | 9 | `e30c03c`, `e30c01d` | `gedit\s99\chara` |
| `e301` | テオ | 26 | `e301p73a`, `e301c10a`, `e301c01d` | `gedit\s32\chara` |
| `e302` | テオ | 12 | `e302p73a`, `e302c10a` | `gedit\s32\chara` |
| `e303` | ウンガガ | 18 | `e303c10a`, `e303p73a` | `gedit\s32\chara` |
| `e303ex` | 流れ星 | 1 | `e303ex` | `gedit\s32\chara` |
| `e304p46a` | ジブブ | 13 | `e304p46a` | `gedit\e04\chara` |
| `e305c01d` | トアン | 20 | `e305c01d` | `gedit\s11\chara` |
| `e305c03c` | 精霊王 | 11 | `e305c03c` | `gedit\s11\chara` |
| `e305ex` | — | 2 | `e305ex`, `e305ex2` | `gedit\s24\chara` |
| `e305jump` | 足場 | 10 | `e305ashiba`, `e305c01d`, `e305ashiba2` | `gedit\s24\chara` |
| `e305p48a` | ボンカ | 11 | `e305p48a` | `gedit\s12\chara` |
| `e305p50a` | ザーボ | 11 | `e305p50a` | `gedit\s12\chara` |
| `e305p56a` | グロン | 3 | `e305p56a` | `gedit\s12\chara` |
| `e305p72a` | 一般２ | 6 | `e305p72a` | `gedit\s11\chara` |
| `e305p73a` | テオ | 7 | `e305p73a` | `gedit\s04\chara` |
| `e305p74a` | サンバ | 19 | `e305p74a` | `gedit\s11\chara` |
| `e305p80a` | うさぎテオ | 7 | `e305p80a` | `gedit\s04\chara` |
| `e306` | トアン | 11 | `e306c15a`, `e306c01d`, `e306c03c` | `gedit\s92\chara` |
| `e306c15p` | — | 1 | `e306c15p` | `gedit\s92\chara` |
| `e31` | 精霊王 | 6 | `e31c03c`, `e31c01d` | `gedit\s99\chara` |
| `e311` | 足場 | 2 | `e311asiba`, `e311c01d` | `gedit\s24\chara` |
| `e312` | イベント専用／金 | 12 | `e312e90a`, `e312e91a` | `gedit\s93\chara` |
| `e32` | 精霊王 | 6 | `e32c01d`, `e32c03c` | `gedit\s99\chara` |
| `e320` | e320ウンガガ | 22 | `e320c01d`, `e320c10a`, `e320p48a` | `gedit\e04\chara` |
| `e321` | ジブブ | 31 | `e321p46a`, `e321c01d` | `gedit\e04\chara` |
| `e322` | ザーボ | 20 | `e322p50a`, `e322c01d` | `gedit\e04\chara` |
| `e323_0c01d` | — | 1 | `e323_0c01d` | `gedit\e04\chara` |
| `e323_0c10a` | e323_0ウンガガ | 6 | `e323_0c10a` | `gedit\e04\chara` |
| `e323_0p51a` | e323_0ミカラ | 12 | `e323_0p51a` | `gedit\e04\chara` |
| `e323_0p52a` | e323_0ナギタ | 29 | `e323_0p52a` | `gedit\e04\chara` |
| `e323_0p53a` | e323_0デビア | 9 | `e323_0p53a` | `gedit\e04\chara` |
| `e323_1c10a` | e323_1ウンガガ | 15 | `e323_1c10a` | `gedit\e04\chara` |
| `e323_1p51a` | e323_1ミカラ | 10 | `e323_1p51a` | `gedit\s17\chara` |
| `e323_1p52a` | e323_1ナギタ | 8 | `e323_1p52a` | `gedit\s17\chara` |
| `e323_2c01d` | — | 1 | `e323_2c01d` | `gedit\e04\chara` |
| `e323_2c10a` | e323_2ウンガガ | 10 | `e323_2c10a` | `gedit\e04\chara` |
| `e323_2p51a` | e323_2ミカラ | 7 | `e323_2p51a` | `gedit\e04\chara` |
| `e323_2p52a` | e323_2ナギタ | 7 | `e323_2p52a` | `gedit\e04\chara` |
| `e323_2p53a` | e323_2デビア | 8 | `e323_2p53a` | `gedit\e04\chara` |
| `e323_3c01d` | — | 7 | `e323_3c01d` | `gedit\e04\chara` |
| `e323_3p73a` | e323_3テオ | 15 | `e323_3p73a` | `gedit\e04\chara` |
| `e324c01d` | トアン | 3 | `e324c01d` | `gedit\e04\chara` |
| `e324p55a` | ブルーク | 10 | `e324p55a` | `gedit\e04\chara` |
| `e326` | グロン | 22 | `e326p56a`, `e326c01d`, `e326c10a` | `gedit\e04\chara` |
| `e327c01d` | トアン | 7 | `e327c01d` | `gedit\e04\chara` |
| `e327ex` | 327 | 1 | `e327ex` | `gedit\e04\chara` |
| `e327p57a` | トト | 31 | `e327p57a` | `gedit\e04\chara` |
| `e327p58a` | ゴーすけ | 6 | `e327p58a` | `gedit\e04\chara` |
| `e327p58b` | ゴーすけ | 3 | `e327p58b` | `gedit\e04\chara` |
| `e327tama` | 玉 | 1 | — | `gedit\e04\chara` |
| `e327w10` | — | 0 | — | `gedit\e04\chara` |
| `e329` | e329c01dトアン拡張 | 22 | `e329c01d`, `e329c10a`, `e329p48a` | `gedit\e04\chara` |
| `e401` | レダン | 43 | `e401p75a`, `e401c01d` | `gedit\s13\chara` |
| `e402c01d` | トアン | 17 | `e402c01d` | `gedit\s13\chara` |
| `e402c18a` | e402オズモンド | 46 | `e402c18a` | `gedit\s13\chara` |
| `e403c01d` | — | 11 | `e403c01d` | `gedit\e05\chara` |
| `e403c18a` | e403オズモンド | 21 | `e403c18a` | `gedit\e05\chara` |
| `e403p75a` | レダン | 5 | `e403p75a` | `gedit\e05\chara` |
| `e404p79a` | p79aダフヤン | 12 | `e404p79a` | `gedit\s90\chara` |
| `e405` | 司会 | 15 | `e405p76a`, `e405c16a` | `gedit\s90\chara` |
| `e405c01d` | — | 8 | `e405c01d` | `gedit\s90\chara` |
| `e405c16p` | — | 1 | `e405c16p` | `gedit\s90\chara` |
| `e405c18a` | e405オズモンド | 15 | `e405c18a` | `gedit\s90\chara` |
| `e405p` | — | 1 | `e405p` | `gedit\s90\chara` |
| `e406` | ミノタウロス | 13 | `e406c16a`, `e406p76a` | `gedit\s90\chara` |
| `e406c01d` | — | 11 | `e406c01d` | `gedit\s90\chara` |
| `e406c18a` | e406オズモンド | 15 | `e406c18a` | `gedit\s90\chara` |
| `e406ex1` | 紙ふぶき | 1 | `e406ex` | `gedit\s90\chara` |
| `e406ex2` | 紙ふぶき | 1 | `e406ex2` | `gedit\s90\chara` |
| `e406p` | — | 1 | `e406p` | `gedit\s90\chara` |
| `e407c01d` | — | 2 | `e407c01d` | `gedit\s13\chara` |
| `e407p71a` | e407一般村人１ | 19 | `e407p71a` | `gedit\s13\chara` |
| `e408c01d` | — | 1 | `e408c01d` | `gedit\s13\chara` |
| `e408c18a` | e408オズモンド | 29 | `e408c18a` | `gedit\s13\chara` |
| `e408p61a` | e408ブーン | 7 | `e408p61a` | `gedit\s13\chara` |
| `e408p62a` | e408ゴッチ | 5 | `e408p62a` | `gedit\s13\chara` |
| `e408p63a` | e408p63a | 6 | `e408p63a` | `gedit\s13\chara` |
| `e408p64a` | e408アムリオ | 9 | `e408p64a` | `gedit\s13\chara` |
| `e409c01d` | e409トアン拡張 | 7 | `e409c01d` | `gedit\s31\chara` |
| `e409c18a` | e409オズモンド | 14 | `e409c18a` | `gedit\s31\chara` |
| `e409doum` | — | 2 | `e409doum` | `gedit\s31\chara` |
| `e409p61a` | e409ブーン | 1 | `e409p61a` | `gedit\s31\chara` |
| `e409p62a` | e409ゴッチ | 1 | `e409p62a` | `gedit\s31\chara` |
| `e409p63a` | e409トマホン | 2 | `e409p63a` | `gedit\s31\chara` |
| `e409p64a` | e409アムリオ | 1 | `e409p64a` | `gedit\s31\chara` |
| `e409p74a` | e409aサンバ | 3 | `e409p74a` | `gedit\s31\chara` |
| `e409p75a` | e409レダン | 4 | `e409p75a` | `gedit\s31\chara` |
| `e409robo` | — | 11 | `robo` | `gedit\s31\chara` |
| `e410c03c` | シンバ | 2 | `e410c03c` | `gedit\s28\chara` |
| `e410c07a` | シーダ | 15 | `e410c07a` | `gedit\s28\chara` |
| `e411_1img` | — | 0 | — | `gedit\s40\chara` |
| `e411_pas1` | — | 1 | `e411_p4` | `gedit\s30\chara` |
| `e411_pas2` | — | 1 | `e411_p5` | `gedit\s30\chara` |
| `e411ac01d` | e411aトアン拡張 | 7 | `e411ac01d` | `gedit\s37\chara` |
| `e411ac18a` | e411ac18aオズモンド | 20 | `e411ac18a` | `gedit\s37\chara` |
| `e411ap61a` | e411ap61aブーン | 7 | `e411ap61a` | `gedit\s37\chara` |
| `e411ap62a` | e411ap62aゴッチ | 5 | `e411ap62a` | `gedit\s37\chara` |
| `e411ap63a` | e411ap63aトマホン | 4 | `e411ap63a` | `gedit\s37\chara` |
| `e411ap64a` | e411ap64aアムリオ | 1 | `e411ap64a` | `gedit\s37\chara` |
| `e411bc08a` | 魔神 | 20 | `e411bc08a` | `gedit\s30\chara` |
| `e411bc08up1` | 魔神･体1 | 10 | `e411bc08body1`, `e411bc08face1` | `gedit\s37\chara` |
| `e411bc08up2` | 魔神･体2 | 18 | `e411bc08body2`, `e411bc08face2` | `gedit\s37\chara` |
| `e411bc09a` | フラッグ | 25 | `e411bc09a` | `gedit\s30\chara` |
| `e411bfurag2` | フラッグ･ダミー | 20 | `e411bfurag2` | `gedit\s37\chara` |
| `e411bmouse` | ネズミ | 3 | `e411bmouse` | `gedit\s37\chara` |
| `e411cc01d` | — | 15 | `e411cc01d` | `gedit\s30\chara` |
| `e411cc12a` | ドラン | 9 | `e411cc12a` | `gedit\s30\chara` |
| `e411cc18a` | e403オズモンド | 12 | `e411cc18a` | `gedit\s30\chara` |
| `e411cut01` | — | 1 | `e411cut01` | `gedit\s37\chara` |
| `e411cut02` | — | 1 | `e411cut02` | `gedit\s37\chara` |
| `e411cut07` | — | 1 | `e411cut07` | `gedit\s37\chara` |
| `e411cut12` | — | 1 | `e411cut12` | `gedit\s30\chara` |
| `e411cut13` | — | 1 | `e411cut13` | `gedit\s30\chara` |
| `e411drobo30` | — | 2 | `e411cut30`, `e411robo30` | `gedit\s37\chara` |
| `e411e60a` | コウモリ | 1 | `e60a` | `gedit\s30\chara` |
| `e411e61a` | コウモリ赤 | 1 | `e61a` | `gedit\s30\chara` |
| `e411ex1` | e411ex1 | 3 | `e411ex1` | `gedit\s37\chara` |
| `e411ex2` | e411ex2 | 2 | `e411ex2` | `gedit\s37\chara` |
| `e411ex_c55` | e411ex_c55 | 3 | `e411ex_c55` | `gedit\s40\chara` |
| `e411ex_redeye` | 赤い目の光 | 1 | `e411ex_redeye` | `gedit\s30\chara` |
| `e411ex_redeye2` | 赤い目の光 | 1 | `e411ex_redeye` | `gedit\s30\chara` |
| `e411p` | — | 1 | `e411p` | `gedit\s37\chara` |
| `e411robo08` | — | 2 | `e411cut08`, `e411robo08` | `gedit\s30\chara` |
| `e412c01d` | — | 8 | `e412c01d` | `gedit\s90\chara` |
| `e413` | e413p64aアムリオ | 9 | `e413c01d`, `e413p64a` | `gedit\e05\chara` |
| `e414` | e414p62aゴッチ | 18 | `e414p62a`, `e414c18a` | `gedit\s13\chara` |
| `e415` | e415p63aトマホン | 15 | `e415p63a`, `e415c18a` | `gedit\e05\chara` |
| `e416` | e416c18aオズモンド | 23 | `e416c18a`, `e416p61a` | `gedit\s13\chara` |
| `e501c01d` | トアン | 25 | `e501c01d` | `gedit\s28\chara` |
| `e501c07a` | シーダ | 37 | `e501c07a` | `gedit\s28\chara` |
| `e501ex1` | e501ex1 | 4 | `e501ex1` | `gedit\s28\chara` |
| `e501ex2` | e501ex2 | 4 | `e501ex2` | `dun\d06\effect` |
| `e502c01d` | トアン | 25 | `e502c01d` | `gedit\s15\chara` |
| `e502c17p` | — | 1 | `e502c17p` | `gedit\s88\chara` |
| `e502ex1` | 502ex1 | 2 | `e502ex1` | `gedit\s16\chara` |
| `e502ex_01` | _01 | 3 | `e502ex_01` | `gedit\s16\chara` |
| `e502ex_02` | 502ex_02 | 6 | `e502ex_02` | `gedit\s16\chara` |
| `e502ex_hi` | 光 | 1 | `e502ex_hi` | `gedit\s16\chara` |
| `e502p` | — | 1 | `e502p` | `gedit\s16\chara` |
| `e502p68b` | シーダ王 | 52 | `e502p68b` | `gedit\s16\chara` |
| `e502p69a` | 姫･其の壱 | 16 | `e502p69a` | `gedit\s16\chara` |
| `e502p69b` | 姫・其の弐 | 1 | `e502p69b` | `gedit\s16\chara` |
| `e502p69c` | 姫・其の参 | 8 | `e502p69c` | `gedit\s16\chara` |
| `e502p70a` | 刺客 | 17 | `e502p70a` | `gedit\s16\chara` |
| `e502p78a` | 偽姫 | 23 | `e502p78a` | `gedit\s16\chara` |
| `e502seiken` | 聖剣 | 1 | `e502seiken` | `gedit\s16\chara` |
| `e503c01d` | キャラクタ情報 | 22 | `e503c01d` | `gedit\s16\chara` |
| `e503c01e` | キャラクタ情報 | 3 | `e503c01e` | `gedit\s88\chara` |
| `e503c03c` | 精霊王 | 10 | `e503c03c` | `gedit\s16\chara` |
| `e503ex_at03c` | — | 1 | `e503ex_at03` | `gedit\s88\chara` |
| `e503ex_at04` | アトラリミアの光4 | 3 | `e503ex_at04` | `gedit\s88\chara` |
| `e503ex_atall` | アトラリミアの光1235 | 11 | `e503ex_atall` | `gedit\s88\chara` |
| `e503ex_m` | 溶ける魔神 | 0 | — | `gedit\s88\chara` |
| `e503exkumo` | e17exkumo | 1 | `e503exkumo` | `gedit\s88\chara` |
| `e503p68b` | シーダ王 | 19 | `e503p68b` | `gedit\s16\chara` |
| `e503p69` | 姫a | 16 | `e503p69a`, `e503p69b` | `gedit\s16\chara` |
| `e504` | トアン | 15 | `e504c01d`, `e504c04cat` | `gedit\s41\chara` |
| `e505c17a` | — | 3 | `e505c17a` | `gedit\s88\chara` |
| `e505c23a` | — | 1 | `e505c23a` | `gedit\s77\chara` |
| `e506_ex` | — | 3 | `e506_ex` | `gedit\s77\chara` |
| `e506c23a` | — | 1 | `e506c23a` | `gedit\s77\chara` |
| `e507_ex` | — | 1 | `e507_ex` | `gedit\s78\chara` |
| `e507c21a` | — | 2 | `e507c21a` | `gedit\s78\chara` |
| `e507c22a` | e507c22a | 2 | `e507c22a` | `gedit\s78\chara` |
| `e508_ex` | — | 4 | `e508_ex` | `gedit\s78\chara` |
| `e508c01d` | トアン | 6 | `e508c01d` | `gedit\s78\chara` |
| `e508c21a` | — | 4 | `e508c21a` | `gedit\s78\chara` |
| `e613c01d` | トアン | 3 | `e613c01d` | `dun\d01\event` |
| `e613c04cat` | — | 9 | `e613c04cat` | `dun\d01\event` |
| `e614c04cat` | — | 9 | `e614c04cat` | `dun\d01\event` |

**Scope, stated plainly:** exactly **one** unused enemy lives outside `dun\monstor` — **Killer Snake**
(`e54a`/`e54b`, packed inside the `s97` cutscene bundles). Everything else with combat motions is either the
party striking battle poses, or a boss/enemy that already owns a species record. The remaining **308** models
have **no combat motion of any kind** — they are NPCs, boss sub-parts, props (`宝箱` chest, `ランプ` lamp,
`聖剣` holy sword, `足場` platform) and effects. They are documented here for completeness, but they are not
enemies and nothing about them is restorable as one.

## Killer Snake — it EXISTS

**`gedit\s97\chara\e116.chr`** — title **キラースネイク**. EID 54's model was built and never shipped to the
dungeon.

The file is a **cutscene bundle**, not one enemy — three packed characters, hence 3 `KEY_START` blocks of 8
motions each (`バトル 0`–`バトル 7`):

```
MODEL     e116e54b.mds
MOTION 0  e116e54b.mot     <- Killer Snake, variant b
MOTION 0  e116e54a.mot     <- Killer Snake, variant a
MOTION 1  e116c01d.mot     <- Toan
```

The meshes are named **`e54a` / `e54b`** — the species-table-style code for **EID 54**. So this was authored as
a normal dungeon enemy; only the cutscene copy survived. Its motions are a cutscene set (`バトル 0-7`), so it
would need a behaviour script written from scratch, not borrowed.

## Restoring one without touching the ISO

`SetupBaseModel` builds the model path from a FIXED format string — the record's ModelCode is the only
variable part, and the field is just 16 bytes, so a path cannot be smuggled into it:

```
dun/monstor/%s.chr     <- model     (fmt @ ELF 0x29CFC0)
dun/monstor/%s.stb     <- script    (fmt @ ELF 0x29CFF0)
```

**But the file index is a tree in RAM, and it is patchable.** `LoadFile` -> `SearchFile` (0x13E980) splits the
path on `/` and walks a `NAME_TREE` from the global **`tree` @ EE `0x202A24EC`**, matching each component
case-insensitively (`search_tree` 0x13EEF0). A node is:

| off | field |
|---|---|
| `+0x00` | `char*` name |
| `+0x04` | value — what `SearchFile` returns for a leaf (the file's archive entry) |
| `+0x08` | first child |
| `+0x0C` | next sibling |

So the mod can **alias a name onto existing file data**, with pure data writes and no ISO change:

1. Walk `tree` -> `dun` -> `monstor`.
2. Build a new node in a code cave: name string `"e54a.chr"`, value = the leaf value of
   `gedit/s97/chara/e116.chr`.
3. Splice it into `monstor`'s child list (write the new node into a sibling `+0x0C` chain).
4. Point a species record's ModelCode at `e54a`. The engine now loads the cutscene bundle's bytes when it
   asks for `dun/monstor/e54a.chr`.

This is the usual "feed the game data it already interprets" move: no injected code, and the engine does the
load itself.

**The limit of the approach:** it can only REDIRECT to data already on the disc. It cannot introduce new
bytes. So a trimmed, single-character `e54a.chr` (extracted out of the 1.3 MB three-character bundle) cannot
be added this way — that would need a real archive rebuild. The open question for Killer Snake is therefore
whether `LoadPackData3` copes with the multi-character bundle as-is (it reads the first `info.cfg` block), and
whether 1.3 MB fits the enemy model budget. Worth testing against a throwaway ISO before committing.

**The orphans already inside `dun\monstor` need none of this** — their files are already on the right path.
They need only a species record and a script, which is entirely RAM-side.

