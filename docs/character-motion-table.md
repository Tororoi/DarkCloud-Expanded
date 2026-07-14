# Character motion table

Per-character animation / motion lists for the **playable characters** (in-dungeon body models
`dun\mainchara\cNN*.chr`), decoded from each `.chr` `info.cfg` KEY block in `data.dat` — same format as
`enemy-motion-table.md`. Each `KEY <start>,<end>,<speed>, //<name(Shift-JIS)>` is one motion; **Idx** is the
motion-table index. Sections are labelled by MODEL CODE (definitive); the character name is INFERRED from the
weapon-specific motions (noted per header) — treat non-Toan names as best-effort.

**Speed** is the third KEY value — the motion's default per-frame playback rate (the baked "KEY speed"). It is
exactly the value the animation-speed knob **`CharacterMotion.MotionSpeedOverride` (`0x21EA2980`)** REPLACES
when set positive (`Step__CCharacter`; −1.0 = use this KEY speed), so this column is the per-motion baseline
for the weapon-speed-scaled-animation feature (`CharacterAddresses.cs`, `player-motion-speed` memory). Rows
with no name (frames like `0-0`/`1-1`) are unused/placeholder slots kept for index alignment.

### Toan `c01d`

*Motions: c01d.chr @ data.dat 0x19363000 — sword; has 攻撃(1)/(2) + full 溜め攻撃 (charge→whirlwind) chain — the feature's target character*

> NOTE: the source KEY block has one line **commented out** (`// KEY 540, 546, 0.4 //26投げ開始`,
> throw-start). The loader skips it, so it is NOT a motion slot — the table below matches the RUNTIME
> ids (verified against `ToanKey_Play`: first combo swing = motion id 0x24 = **36** 連続攻戸1, end frame
> read from `DAT_01ea2064 + id*0x10 + 4`). Toan is the only playable character with a commented-out KEY.

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.2 | 立ち | idle |
| 1 | 70–90 | 0.65 | 走り | run |
| 2 | 35–55 | 0.5 | 歩き | walk |
| 3 | 191–196 | 0.2 | 溜め攻撃loop2 | charge attackloop2 |
| 4 | 285–291 | 0.4 | ダメ−ジ | damage |
| 5 | 300–300 | 0 | 溜め攻撃loop3 | charge attackloop3 |
| 6 | 295–335 | 0.3 | ダメージ大 | damage (big) |
| 7 | 315–335 | 0.3 | 起き上がり | get up |
| 8 | 210–215 | 0.8 | 立ち＞ガード | idle＞guard |
| 9 | 216–224 | 0.3 | ガードループ | guard (loop) |
| 10 | 224–230 | 0.8 | ガード＞立ち | guard＞idle |
| 11 | 116–130 | 0.4 | 攻撃(1) | attack(1) |
| 12 | 136–150 | 0.5 | 攻撃(2) | attack(2) |
| 13 | 155–160 | 0.4 | 立ち＞溜め | idle＞charge |
| 14 | 160–170 | 0.4 | 溜めループ | charge (loop) |
| 15 | 180–191 | 0.4 | 溜め攻撃start | charge attackstart |
| 16 | 191–192 | 0 | 溜め攻撃loop1 | charge attackloop1 |
| 17 | 196–206 | 0.4 | 溜め攻撃end | charge attackend |
| 18 | 100–110 | 0.5 | 攻撃構え | attack ready |
| 19 | 340–360 | 0.6 | 攻撃態勢（右） | attack stance(right) |
| 20 | 370–390 | 0.6 | 攻撃態勢（左） | attack stance(left) |
| 21 | 400–420 | 0.6 | 攻撃態勢（前） | attack stance(fwd) |
| 22 | 430–450 | 0.6 | 攻撃態勢（後ろ） | attack stance(back) |
| 23 | 460–480 | 0.2 | やられ | downed |
| 24 | 715–750 | 0.4 | 技２ 24やられ | skill２ 24downed |
| 25 | 196–196 | 0 | 溜め攻撃loop3 | charge attackloop3 |
| 26 | 540–546 | 0.4 | 投げ開始 | throw (start) |
| 27 | 546–546 | 0 | 投げ停止 | throw (stop) |
| 28 | 546–566 | 0.4 | 投げる | throw |
| 29 | 620–655 | 0.25 | 飲む | drink |
| 30 | 690–710 | 0.65 | ダメージ受け走り | run (hit) |
| 31 | 756–766 | 0.3 | 溜め移動 | charge move |
| 32 | 660–680 | 0.5 | ダメだし | (dmg-out) |
| 33 | 570–590 | 0.3 | 落下モーション | fall motion |
| 34 | 778–788 | 0.3 | ガード移動 | guard move |
| 35 | 792–802 | 0.3 | Gget In | Gget In |
| 36 | 805–815 | 0.3 | Gget Loop | Gget Loop |
| 37 | 820–830 | 0.3 | 連続攻撃1 | combo attack1 |
| 38 | 830–838 | 0.3 | 連続攻撃2 | combo attack2 |
| 39 | 838–847 | 0.3 | 連続攻撃1 | combo attack1 |
| 40 | 847–857 | 0.3 | 連続攻撃2 | combo attack2 |
| 41 | 856–884 | 0.3 | 連続攻撃3 | combo attack3 |

### Xiao `c04b`

*Motions: c04b.chr @ data.dat 0x195D5800 — ranged: 構え引き (draw/nock) + 撃ち (shoot) → slingshot*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 70–90 | 0.7 | 走り | run |
| 2 | 30–50 | 0.7 | 歩き | walk |
| 3 | 1–1 | 1 | — | — |
| 4 | 310–325 | 0.4 | 通常ダメージ | damage |
| 5 | 1–1 | 1 | — | — |
| 6 | 330–373 | 0.4 | ダメージ大 | damage (big) |
| 7 | 346–373 | 0.4 | 起き上がり | get up |
| 8 | 285–290 | 0.7 | ガード入り | guard (enter) |
| 9 | 290–300 | 0.3 | ガードﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 10 | 300–305 | 0.7 | ガード戻り | guard (return) |
| 11 | 240–251 | 0.7 | 構え引き | draw |
| 12 | 250–250 | 0 | 構え引き（ル−プ） | draw(loop) |
| 13 | 251–255 | 0.7 | 撃ち | shoot |
| 14 | 1–1 | 1 | — | — |
| 15 | 1–1 | 1 | — | — |
| 16 | 1–1 | 1 | — | — |
| 17 | 1–1 | 1 | — | — |
| 18 | 100–110 | 0.3 | 攻撃構え | attack ready |
| 19 | 180–200 | 0.5 | 攻撃態勢（右） | attack stance(right) |
| 20 | 210–230 | 0.5 | 攻撃態勢（左） | attack stance(left) |
| 21 | 120–140 | 0.5 | 攻撃態勢（前） | attack stance(fwd) |
| 22 | 150–170 | 0.5 | 攻撃態勢（後ろ） | attack stance(back) |
| 23 | 420–440 | 0.3 | やられ | downed |
| 24 | 440–441 | 0 | やられ（ル−プ） | downed(loop) |
| 25 | 445–460 | 0.3 | — | — |
| 26 | 450–450 | 0 | アイテム投げ開始 | item throw開始 |
| 27 | 450–460 | 0.4 | アイテム投げ | item throw |
| 28 | 465–490 | 0.25 | 飲む | drink |
| 29 | 690–710 | 0.65 | ダメージ受け走り | run (hit) |
| 30 | 756–766 | 0.3 | 溜め移動 | charge move |
| 31 | 491–520 | 0.4 | ダメだし | (dmg-out) |
| 32 | 375–395 | 0.3 | 落下モーション | fall motion |
| 33 | 530–540 | 0.3 | ガード移動 | guard move |
| 34 | 545–555 | 0.3 | Gget In | Gget In |
| 35 | 558–568 | 0.3 | Gget Loop | Gget Loop |
| 36 | 1–1 | 0 | — | — |
| 37 | 1–1 | 0 | — | — |
| 38 | 1–1 | 0 | — | — |
| 39 | 1–1 | 0 | — | — |
| 40 | 1–1 | 0 | — | — |
| 41 | 1–1 | 0 | — | — |
| 42 | 1–1 | 0 | — | — |
| 43 | 1–1 | 0 | — | — |
| 44 | 1–1 | 0 | — | — |
| 45 | 1–1 | 0 | — | — |
| 46 | 1–1 | 0 | — | — |
| 47 | 10–20 | 0.7 | 引き | 引き |
| 48 | 40–50 | 0.4 | 溜め | charge |
| 49 | 20–20 | 0 | 溜め | charge |
| 50 | 20–30 | 0.7 | 撃ち | shoot |

### Xiao (CAT FORM — playable) `c04pcat`

*Motions: gedit\e01\chara\c04pcat.chr @ data.dat 0x270B3000 — シャオ猫; the model the mod loads to play as cat Xiao in town (TownCharacter.cs)*

> NOTE: **the playable cat has only these four motions — there is no run and no brace.** Every OTHER cat model
> in the game (see the variant table below) carries a richer set, but their motion INDICES DO NOT MATCH this one:
> `c04pcat` orders them idle/walk/walk/sit, while all the others order them idle/sit/walk/brace/run. A motion id is
> only meaningful against the model that is actually loaded — id 1 is *walk* on the playable cat and *sit* on
> every other cat. Motion data is bound to the model (`MOTION 0, "c04pcat.mot"`), so ids are not portable.

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 60–80 | 0.3 | 歩き | walk |
| 2 | 60–80 | 0.3 | 歩き | walk |
| 3 | 30–40 | 0.1 | 座り | sit |

### Ruby `c05a`

*Motions: c05a.chr @ data.dat 0x198C0000 — caster: 魔法1/魔法2 + 溜め魔法 (charged magic)*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.15 | 立ち | idle |
| 1 | 60–80 | 0.55 | 走り | run |
| 2 | 30–50 | 0.25 | 歩き | walk |
| 3 | 0–0 | 0 | — | — |
| 4 | 255–265 | 0.2 | ダメージ | damage |
| 5 | 0–0 | 0 | — | — |
| 6 | 270–305 | 0.3 | ダメージ大 | damage (big) |
| 7 | 285–305 | 0.3 | 起き上がり | get up |
| 8 | 230–235 | 0.2 | 立ち＞ガード | idle＞guard |
| 9 | 235–245 | 0.1 | ガードループ | guard (loop) |
| 10 | 245–250 | 0.2 | ガード＞立ち | guard＞idle |
| 11 | 120–140 | 0.5 | 魔法１ | magic１ |
| 12 | 150–170 | 0.5 | 魔法2 | magic2 |
| 13 | 180–185 | 0.4 | 立ち＞溜め | idle＞charge |
| 14 | 185–195 | 0.1 | 溜めループ | charge (loop) |
| 15 | 195–200 | 0.4 | 溜め＞立ち | charge＞idle |
| 16 | 205–225 | 0.4 | 溜め魔法 | charged magic |
| 17 | 0–0 | 0 | — | — |
| 18 | 90–110 | 0.15 | 攻撃待機 | attack idle |
| 19 | 450–470 | 0.45 | Ｚ左 | Ｚ左 |
| 20 | 480–500 | 0.45 | Ｚ右 | Ｚ右 |
| 21 | 510–530 | 0.45 | Ｚ前 | Ｚ前 |
| 22 | 540–560 | 0.45 | Ｚ後 | Ｚ後 |
| 23 | 310–330 | 0.2 | 死亡 | death |
| 24 | 330–330 | 0 | 死亡ループ | deathloop |
| 25 | 370–390 | 0.2 | — | — |
| 26 | 341–341 | 0 | 投げ停止 | throw (stop) |
| 27 | 341–360 | 0.35 | 投げ | throw |
| 28 | 400–425 | 0.2 | 飲み | drink |
| 29 | 400–425 | 0.2 | — | — |
| 30 | 400–425 | 0.2 | — | — |
| 31 | 605–625 | 0.2 | ダメだし | (dmg-out) |
| 32 | 605–625 | 0.2 | — | — |
| 33 | 563–572 | 0.2 | — | — |
| 34 | 575–585 | 0.3 | Gkey In | Gkey In |
| 35 | 590–600 | 0.3 | Gkey Loop | Gkey Loop |
| 36 | 10–20 | 0.15 | 立ち | idle |
| 37 | 60–80 | 0.55 | 走り | run |
| 38 | 30–50 | 0.25 | 歩き | walk |
| 39 | 0–0 | 0 | — | — |
| 40 | 255–265 | 0.2 | ダメージ | damage |
| 41 | 0–0 | 0 | — | — |
| 42 | 270–285 | 0.2 | ダメージ大 | damage (big) |
| 43 | 285–305 | 0.2 | 起き上がり | get up |
| 44 | 230–235 | 0.2 | 立ち＞ガード | idle＞guard |
| 45 | 235–245 | 0.1 | ガードループ | guard (loop) |
| 46 | 245–250 | 0.2 | ガード＞立ち | guard＞idle |
| 47 | 120–140 | 0.3 | 魔法１ | magic１ |
| 48 | 150–170 | 0.2 | 魔法2 | magic2 |
| 49 | 180–185 | 0.3 | 立ち＞溜め | idle＞charge |
| 50 | 185–195 | 0.1 | 溜めループ | charge (loop) |
| 51 | 195–200 | 0.3 | 溜め＞立ち | charge＞idle |
| 52 | 205–225 | 0.3 | 溜め魔法 | charged magic |
| 53 | 1–10 | 0.17 | — | — |
| 54 | 10–20 | 0.4 | — | — |
| 55 | 20–40 | 0.3 | — | — |

### Goro `c06b`

*Motions: c06b.chr @ data.dat 0x19AB9800 — melee: 攻撃 + ため攻撃 (charge) chain*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.2 | 立ち | idle |
| 1 | 60–80 | 0.5 | 走り | run |
| 2 | 30–50 | 0.5 | 歩き | walk |
| 3 | 10–20 | 0.5 | — | — |
| 4 | 200–210 | 0.4 | 通常ダメージ | damage |
| 5 | 10–20 | 0.5 | — | — |
| 6 | 210–238 | 0.3 | ダメージ大:吹っ飛び | damage (big):吹っ飛び |
| 7 | 225–238 | 0.23 | 置きあがり | get up |
| 8 | 181–194 | 0.6 | ガ−ド | ガ−ド |
| 9 | 140–150 | 0.4 | ガードループ | guard (loop) |
| 10 | 194–200 | 0.6 | ガード解除 | guard (release) |
| 11 | 90–117 | 0.4 | 攻撃 | attack |
| 12 | 118–122 | 0.3 | ため攻撃 | charge attack |
| 13 | 123–127 | 0.3 | ため攻撃LOOP | charge attackLOOP |
| 14 | 453–498 | 0.4 | ため攻撃はなつ | charge attackはなつ |
| 15 | 440–450 | 0.5 | — | — |
| 16 | 460–470 | 0.5 | — | — |
| 17 | 0–0 | 0.1 | — | — |
| 18 | 10–20 | 0.2 | 攻撃態勢 | attack stance |
| 19 | 335–345 | 0.5 | 右後退 | step right |
| 20 | 355–365 | 0.5 | 左後退 | step left |
| 21 | 30–50 | 0.5 | 前進 | step fwd |
| 22 | 305–325 | 0.5 | 後退 | step back |
| 23 | 240–256 | 0.25 | やられ | downed |
| 24 | 256–257 | 0 | やられloop | downedloop |
| 25 | 260–275 | 0.3 | — | — |
| 26 | 264–264 | 0 | 投げ停止 | throw (stop) |
| 27 | 264–275 | 0.3 | 投げ開始 | throw (start) |
| 28 | 277–302 | 0.2 | 飲む | drink |
| 29 | 60–80 | 0.5 | ダメージ受け走り | run (hit) |
| 30 | 155–165 | 0.3 | 溜め移動 | charge move |
| 31 | 421–450 | 0.5 | ダメだし | (dmg-out) |
| 32 | 0–0 | 0 | NONE | NONE |
| 33 | 550–560 | 0.2 | ガード移動 | guard move |
| 34 | 515–525 | 0.3 | GGet In | GGet In |
| 35 | 530–540 | 0.3 | GGet Loop | GGet Loop |
| 36 | 90–108 | 0.35 | New Action FullFrame | New Action FullFrame |

### Ungaga `c10b`

*Motions: c10b.chr @ data.dat 0x19D26800 — 攻撃1/2/3 + 溜め (charge) — polearm*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.15 | 立ち | idle |
| 1 | 60–80 | 0.55 | 走り | run |
| 2 | 30–50 | 0.27 | 歩き | walk |
| 3 | 30–50 | 0.27 | — | — |
| 4 | 285–290 | 0.2 | ダメージ | damage |
| 5 | 285–290 | 0.2 | — | — |
| 6 | 295–334 | 0.3 | ダメージ大 | damage (big) |
| 7 | 315–334 | 0.3 | 起き上がり | get up |
| 8 | 210–215 | 0.3 | 構え＞ガード | ready＞guard |
| 9 | 215–225 | 0.2 | ガードループ | guard (loop) |
| 10 | 225–230 | 0.3 | ガード＞構え | guard＞ready |
| 11 | 103–121 | 0.4 | 攻撃１ | attack１ |
| 12 | 122–141 | 0.33 | 攻撃２ | attack２ |
| 13 | 155–160 | 0.2 | 構え＞溜め | ready＞charge |
| 14 | 160–170 | 0.7 | 溜めループ | charge (loop) |
| 15 | 171–175 | 0.15 | 溜め＞構え | charge＞ready |
| 16 | 160–170 | 0.7 | 溜め攻撃無し（変わりに溜めループ） | charge attack無し（変わりにcharge (loop)） |
| 17 | 0–0 | 0 | — | — |
| 18 | 90–100 | 0.15 | 攻撃構え | attack ready |
| 19 | 340–360 | 0.3 | 右移動 | move right |
| 20 | 370–390 | 0.3 | 左移動 | move left |
| 21 | 400–420 | 0.3 | 前移動 | move fwd |
| 22 | 430–450 | 0.3 | 後ろ移動 | move back |
| 23 | 463–480 | 0.2 | 死亡 | death |
| 24 | 463–480 | 0.2 | 死亡 | death |
| 25 | 180–206 | 0.25 | — | — |
| 26 | 186–186 | 0 | 投げ停止 | throw (stop) |
| 27 | 186–206 | 0.22 | 投げ | throw |
| 28 | 590–625 | 0.25 | 飲み | drink |
| 29 | 490–530 | 0.22 | NGポーズ | NGポーズ |
| 30 | 490–530 | 0.22 | — | — |
| 31 | 490–530 | 0.22 | ダメだし | (dmg-out) |
| 32 | 490–530 | 0.22 | — | — |
| 33 | 238–248 | 0.22 | ガード移動 | guard move |
| 34 | 630–640 | 0.3 | Gkey In | Gkey In |
| 35 | 643–653 | 0.3 | Gkey Loop | Gkey Loop |
| 36 | 670–714 | 0.3 | FullFrame attack | FullFrame attack |
| 37 | 670–678 | 0.28 | 攻撃１ | attack１ |
| 38 | 678–694 | 0.32 | 攻撃２ | attack２ |
| 39 | 695–714 | 0.32 | 攻撃３ | attack３ |

### Osmond `c18a`

*Motions: c18a.chr @ data.dat 0x19EB9800 — 撃つ (shoot) → gun*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.15 | 立ち | idle |
| 1 | 30–50 | 0.2 | 走り | run |
| 2 | 30–50 | 0.2 | 歩き | walk |
| 3 | 0–0 | 0 | — | — |
| 4 | 155–165 | 0.35 | ダメージ | damage |
| 5 | 0–0 | 0 | — | — |
| 6 | 170–214 | 0.35 | ダメージ大 | damage (big) |
| 7 | 195–214 | 0.3 | 起き上がり | get up |
| 8 | 120–125 | 0.2 | 立ち〜ガード | idle〜guard |
| 9 | 130–140 | 0.2 | ガードループ | guard (loop) |
| 10 | 145–150 | 0.2 | ガード〜立ち | guard〜idle |
| 11 | 80–92 | 0.55 | 攻撃1 | attack1 |
| 12 | 100–110 | 0.3 | 攻撃2 | attack2 |
| 13 | 0–0 | 0 | — | — |
| 14 | 0–0 | 0 | — | — |
| 15 | 0–0 | 0 | — | — |
| 16 | 0–0 | 0 | — | — |
| 17 | 0–0 | 0 | — | — |
| 18 | 60–70 | 0.2 | 構え | ready |
| 19 | 225–235 | 0.2 | 右移動 | move right |
| 20 | 240–250 | 0.2 | 左移動 | move left |
| 21 | 255–265 | 0.2 | 前進 | step fwd |
| 22 | 270–280 | 0.2 | 後退 | step back |
| 23 | 290–306 | 0.2 | 死に | death |
| 24 | 290–306 | 0.2 | 死に | death |
| 25 | 315–328 | 0.2 | — | — |
| 26 | 320–320 | 0 | 投げ停止 | throw (stop) |
| 27 | 320–328 | 0.2 | 投げ | throw |
| 28 | 360–385 | 0.2 | 飲み | drink |
| 29 | 390–412 | 0.2 | だめ | だめ |
| 30 | 390–412 | 0.2 | だめ | だめ |
| 31 | 390–412 | 0.2 | — | — |
| 32 | 130–140 | 0.2 | — | — |
| 33 | 415–425 | 0.3 | Gkey In | Gkey In |
| 34 | 428–438 | 0.3 | Gkey Loop | Gkey Loop |
| 35 | 1–4 | 0.9 | — | — |
| 36 | 5–15 | 0.9 | 撃つ | shoot |

### (unused/NPC) `c07a`

*Motions: c07a.chr @ data.dat 0x19C64000 — only idle/run/walk present*

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.5 | 立ち | idle |
| 1 | 60–80 | 0.5 | 走り | run |
| 2 | 30–50 | 0.2 | 歩き | walk |

### Cat-form variants (NOT playable — reference only)

Every other `c04*cat` model in `data.dat`. They are listed because they show what cat animations
EXIST — but a motion id only means anything against the model that is actually loaded, and these
orderings differ from the playable `c04pcat`. To use one of these motions you would have to load
that model, not just ask for the index.

**`gedit\e01\chara\c04cat.chr`** @ 0x2705F000 — town NPC cat

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 30–40 | 0.1 | 座り | sit |
| 2 | 60–80 | 0.3 | 歩き | walk |
| 3 | 95–105 | 0.1 | 身構え | brace / ready |
| 4 | 120–136 | 0.5 | 走り | run |

**`gedit\s86\chara\c04cat.chr`** @ 0x51CAA800 — scene s86 — adds jump (踏み切り / 飛びＬ / 着地)

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 30–40 | 0.1 | 座り | sit |
| 2 | 60–80 | 0.3 | 歩き | walk |
| 3 | 95–105 | 0.1 | 身構え | brace / ready |
| 4 | 120–136 | 0.5 | 走り | run |
| 5 | 190–204 | 0.5 | 踏み切り | take off (jump) |
| 6 | 205–214 | 0.1 | 飛びＬ | leap |
| 7 | 215–227 | 0.36 | 着地 | land |

**`gedit\s87\chara\c04cat.chr`** @ 0x51FE5800 — scene s87 — same set as s86

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 30–40 | 0.1 | 座り | sit |
| 2 | 60–80 | 0.3 | 歩き | walk |
| 3 | 95–105 | 0.1 | 身構え | brace / ready |
| 4 | 120–136 | 0.5 | 走り | run |
| 5 | 190–204 | 0.5 | 踏み切り | take off (jump) |
| 6 | 205–214 | 0.1 | 飛びＬ | leap |
| 7 | 215–227 | 0.36 | 着地 | land |

**`gedit\e01\chara\e04c04cat.chr`** @ 0x27871000 — event e04 — adds float / head-tilt

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 30–40 | 0.1 | 座り | sit |
| 2 | 60–80 | 0.3 | 歩き | walk |
| 3 | 95–105 | 0.1 | 身構え | brace / ready |
| 4 | 120–136 | 0.5 | 走り | run |
| 5 | 160–190 | 0.16 | 浮かぶ | float |
| 6 | 191–201 | 0.16 | そのまま首傾げ | head tilt |
| 7 | 202–212 | 0.1 | 傾げﾙｰﾌﾟ | tilt (loop) |

**`gedit\s99\chara\e17c04cat.chr`** @ 0x55966000 — event e17 — adds look-at-Toan / turn

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 30–40 | 0.1 | 座り | sit |
| 2 | 60–80 | 0.3 | 歩き | walk |
| 3 | 95–105 | 0.1 | 身構え | brace / ready |
| 4 | 120–136 | 0.5 | 走り | run |
| 5 | 150–165 | 0.1 | トアン見る | look at Toan |
| 6 | 155–165 | 0.1 | 上見ﾙｰﾌﾟ | look up (loop) |
| 7 | 166–180 | 0.3 | 振り向き | turn around |

**`dun\d01\event\e613c04cat.chr`** @ 0x1789F800 — dungeon event e613 — its OWN ordering (idle/walk/run/gasp/turn)

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 25–45 | 0.3 | 歩き | walk |
| 2 | 50–66 | 0.5 | 走り | run |
| 3 | 70–80 | 0.3 | ハッ3 | gasp / startle3 |
| 4 | 85–95 | 0.13 | ハッﾙｰﾌﾟ | gasp (loop) |
| 5 | 100–110 | 0.3 | ハッ戻り（立ちへ） | gasp (return to idle)（idleへ） |
| 6 | 115–135 | 0.3 | ふりかえ | turn around |
| 7 | 140–150 | 0.13 | ふりかえﾙｰﾌﾟ | turn around (loop) |
| 8 | 155–165 | 0.3 | ふりかえ戻り（歩きへ）8 | turn back (to walk)（walkへ）8 |

**`dun\d01\event\e614c04cat.chr`** @ 0x1791D800 — dungeon event e614 — same as e613

| Idx | Frames | Speed | Name (JP) | Meaning |
|---|---|---|---|---|
| 0 | 10–20 | 0.1 | 立ち | idle |
| 1 | 25–45 | 0.3 | 歩き | walk |
| 2 | 50–66 | 0.5 | 走り | run |
| 3 | 70–80 | 0.3 | ハッ3 | gasp / startle3 |
| 4 | 85–95 | 0.13 | ハッﾙｰﾌﾟ | gasp (loop) |
| 5 | 100–110 | 0.3 | ハッ戻り（立ちへ） | gasp (return to idle)（idleへ） |
| 6 | 115–135 | 0.3 | ふりかえ | turn around |
| 7 | 140–150 | 0.13 | ふりかえﾙｰﾌﾟ | turn around (loop) |
| 8 | 155–165 | 0.3 | ふりかえ戻り（歩きへ）8 | turn back (to walk)（walkへ）8 |

