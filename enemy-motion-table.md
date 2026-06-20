# Enemy motion table

Per-species animation / motion lists, decoded from each enemy model's `<code>.chr` `info.cfg` KEY
block inside `data.dat`. Each `KEY <start>,<end>,<speed>, //<name(Shift-JIS)> <idx>` is one motion;
`idx` is the motion-table index passed to `_SET_MOTION`. **死亡** (`shibou`) marks the death/collapse
motion used by `EnemyModelInjector.BossScriptPatcher.CollapseMotion`. Locate a `.chr` via `data.hed`
(filename @ `index*80`) → `data.hd2` (entry @ `16 + index*32`: `[+0]`=data.dat offset, `[+4]`=size,
`[+8]`=sector). See the `datadat-index-and-chr-motions` note. Sorted by `TableIndex`.

### 0 — Master Jacket `e01a`

*Motions: e01a.chr @ data.dat 0x1b260000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–305 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–105 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 115–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 50–70 | 攻撃1 | attack1 |
| 14 | 50–61 | 振りかぶり | wind-up |
| 15 | 61–70 | 振り下ろし | downswing |
| 16 | 75–95 | 攻撃2 | attack2 |
| 17 | 75–95 | ダミー | (dummy) |
| 18 | 75–95 | ダミー | (dummy) |
| 19 | 280–295 | 飛び込み | lunge |

### 1 — Skeleton Soldier `e03a`

*Motions: e03a.chr @ data.dat 0x1b370800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–305 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–105 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 115–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 50–70 | 攻撃1 | attack1 |
| 14 | 50–70 | ダミー | (dummy) |
| 15 | 50–70 | ダミー | (dummy) |
| 16 | 75–95 | 攻撃2 | attack2 |
| 17 | 75–95 | ダミー | (dummy) |
| 18 | 75–95 | ダミー | (dummy) |

### 2 — Statue `e05a`

*Motions: e05a.chr @ data.dat 0x1b4c6000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 30–50 | ダミー | (dummy) |
| 3 | 30–50 | ダミー | (dummy) |
| 4 | 30–50 | ダミー | (dummy) |
| 5 | 10–20 | ダミー | (dummy) |
| 6 | 10–20 | ダミー | (dummy) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 10–20 | ダミー | (dummy) |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 180–210 | 死亡 | death |
| 12 | 60–80 | 攻撃1 | attack1 |
| 13 | 60–80 | ダミー | (dummy) |
| 14 | 60–80 | ダミー | (dummy) |
| 15 | 90–120 | 攻撃2 | attack2 |
| 16 | 90–104 | ジャンプ | jump |
| 17 | 104–120 | アタック | アタック |

### 3 — Dasher `e06a`

*Motions: e06a.chr @ data.dat 0x1b57c000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 120–140 | バックステップ | back step |
| 3 | 60–80 | 右ステップ | right step |
| 4 | 90–110 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 240–260 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 270–290 | 死亡 | death |
| 12 | 290 | 死亡ループ | death loop |
| 13 | 150–180 | 攻撃1(縦振り） | attack1(縦振り) |
| 14 | 150–180 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 150–180 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 190–215 | 攻撃２（走り頭突き入り） | attack２(run頭突き(enter)) |
| 17 | 215–223 | 攻撃２予備動作１（走りリピート） | attack２予備動作１(runリピート) |
| 18 | 223–230 | 攻撃２予備動作２ （走り頭突き終わり） | attack２予備動作２ (run頭突き終わり) |

### 4 — Werewolf `e07a`

*Motions: e07a.chr @ data.dat 0x1b5e7000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 90–110 | バックステップ | back step |
| 3 | 190–210 | 右ステップ | right step |
| 4 | 215–235 | 左ステップ | left step |
| 5 | 240–250 | ガード入り | guard (enter) |
| 6 | 250–260 | ガードループ | guard (loop) |
| 7 | 260–270 | ガード戻り | guard (return) |
| 8 | 115–125 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 130–160 | 死亡 | death |
| 12 | 160 | 死亡ループ | death loop |
| 13 | 50–70 | 攻撃1 | attack1 |
| 14 | 50–70 | ダミー | (dummy) |
| 15 | 50–70 | ダミー | (dummy) |
| 16 | 70–90 | 攻撃2 | attack2 |
| 17 | 70–90 | ダミー | (dummy) |
| 18 | 70–90 | ダミー | (dummy) |

### 5 — FliFli `e08a`

*Motions: e08a.chr @ data.dat 0x1b6b2000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 100–110 | バックステップ | back step |
| 3 | 60–70 | 右ステップ | right step |
| 4 | 80–90 | 左ステップ | left step |
| 5 | 115–120 | ガード入り | guard (enter) |
| 6 | 120–125 | ガードループ | guard (loop) |
| 7 | 125–130 | ガード戻り | guard (return) |
| 8 | 144–150 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 235 | 死亡ループ | death loop |
| 12 | 160–180 | 攻撃1 | attack1 |
| 13 | 160–180 | ダミー | (dummy) |
| 14 | 160–180 | ダミー | (dummy) |
| 15 | 190–200 | 攻撃2 | attack2 |
| 16 | 190–200 | ダミー | (dummy) |
| 17 | 190–200 | ダミー | (dummy) |

### 6 — Hornet `e09a`

*Motions: e09a.chr @ data.dat 0x1b6fd800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | ホバリング | ホバリング |
| 1 | 40–50 | 飛行 | flight |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 10–30 | ホバリング | ホバリング |
| 6 | 10–30 | ホバリング | ホバリング |
| 7 | 10–30 | ホバリング | ホバリング |
| 8 | 120–140 | ダメージ | damage |
| 9 | 10–30 | ホバリング | ホバリング |
| 10 | 10–30 | ホバリング | ホバリング |
| 11 | 150–170 | 死亡入り | death(enter) |
| 12 | 270–280 | 死亡落ちループ | death落ち(loop) |
| 13 | 290–310 | 死亡 | death |
| 14 | 310 | 死亡ループ | death loop |
| 15 | 180–210 | 攻撃1 | attack1 |
| 16 | 180–210 | 攻撃1 | attack1 |
| 17 | 180–210 | 攻撃1 | attack1 |
| 18 | 220–260 | 攻撃2 | attack2 |
| 19 | 220–260 | 攻撃2 | attack2 |
| 20 | 220–260 | 攻撃2 | attack2 |

### 7 — Halloween `e10a`

*Motions: e10a.chr @ data.dat 0x1b75f000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–45 | 前移動ループ | move loop (fwd) |
| 2 | 30–45 | ダミー | (dummy) |
| 3 | 110–130 | 右ステップ | right step |
| 4 | 140–160 | 左ステップ | left step |
| 5 | 170–180 | ガード入り | guard (enter) |
| 6 | 180–190 | ガードループ | guard (loop) |
| 7 | 190–200 | ガード戻り | guard (return) |
| 8 | 210–220 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 230–255 | 死亡 | death |
| 12 | 55–79 | 攻撃1 | attack1 |
| 13 | 265–285 | バックステップ | back step |
| 14 | 295–315 | 歩き | walk |
| 15 | 85–103 | 攻撃2 | attack2 |
| 16 | 85–103 | ダミー | (dummy) |
| 17 | 85–103 | ダミー | (dummy) |

### 8 — Cannibal Plant `e11a`

*Motions: e11a.chr @ data.dat 0x1b808800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 35–55 | ダメージ | damage |
| 2 | 60–80 | 死亡 | death |
| 3 | 80 | 死亡ループ | death loop |
| 4 | 85–120 | 攻撃葉 | attack葉 |
| 5 | 125–160 | 攻撃液前 | attack液前 |
| 6 | 165–200 | 攻撃液上 | attack液上 |

### 9 — Earth Digger `e12a`

*Motions: e12a.chr @ data.dat 0x1b87d800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 10–20 | ダミー | (dummy) |
| 3 | 10–20 | ダミー | (dummy) |
| 4 | 10–20 | ダミー | (dummy) |
| 5 | 60–70 | もぐり | もぐり |
| 6 | 70–80 | もぐりループ | もぐり(loop) |
| 7 | 80–90 | 出現 | appear |
| 8 | 100–110 | ダメージ | damage |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 180–200 | 死亡 | death |
| 12 | 200 | 死亡ループ | death loop |
| 13 | 120–140 | 攻撃1 | attack1 |
| 14 | 120–140 | ダミー | (dummy) |
| 15 | 120–140 | ダミー | (dummy) |
| 16 | 150–170 | 攻撃2 | attack2 |
| 17 | 150–170 | ダミー | (dummy) |
| 18 | 150–170 | ダミー | (dummy) |

### 10 — Sunday `e14a`

*Motions: e14a.chr @ data.dat 0x1b964000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–280 | 攻撃1（出刃包丁） | attack1(出刃包丁) |
| 14 | 255–280 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 255–280 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 285–297 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 285–297 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 285–297 | 攻撃8予備動作２ | attack8予備動作２ |

### 11 — Monday `e15a`

*Motions: e15a.chr @ data.dat 0x1b9e5000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–270 | 攻撃2（槍） | attack2(槍) |
| 14 | 255–270 | 攻撃2予備動作１ | attack2予備動作１ |
| 15 | 255–270 | 攻撃2予備動作２ | attack2予備動作２ |
| 16 | 280–292 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 280–292 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 280–292 | 攻撃8予備動作２ | attack8予備動作２ |

### 12 — Tuesday `e16a`

*Motions: e16a.chr @ data.dat 0x1ba63800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–275 | 攻撃3（吹き矢） | attack3(吹き矢) |
| 14 | 255–275 | 攻撃3予備動作１ | attack3予備動作１ |
| 15 | 255–275 | 攻撃3予備動作２ | attack3予備動作２ |
| 16 | 285–297 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 285–297 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 285–297 | 攻撃8予備動作２ | attack8予備動作２ |

### 13 — Wednesday `e17a`

*Motions: e17a.chr @ data.dat 0x1bae3000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–280 | 攻撃4（斧/飛んで前に出る） | attack4(斧/飛んで前に出る) |
| 14 | 255–280 | 攻撃4予備動作１ | attack4予備動作１ |
| 15 | 255–280 | 攻撃4予備動作２ | attack4予備動作２ |
| 16 | 290–302 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 290–302 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 290–302 | 攻撃8予備動作２ | attack8予備動作２ |

### 14 — Thursday `e18a`

*Motions: e18a.chr @ data.dat 0x1bb61000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–275 | 攻撃5（ランダム投げ） | attack5(ランダムthrow) |
| 14 | 255–275 | 攻撃5予備動作１ | attack5予備動作１ |
| 15 | 255–275 | 攻撃5予備動作２ | attack5予備動作２ |
| 16 | 285–297 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 285–297 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 285–297 | 攻撃8予備動作２ | attack8予備動作２ |

### 15 — Friday `e19a`

*Motions: e19a.chr @ data.dat 0x1bbe0000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–262 | 攻撃6（ジャンプキック） | attack6(jumpキック) |
| 14 | 262–271 | 攻撃6予備動作１ | attack6予備動作１ |
| 15 | 271–275 | 攻撃6予備動作２ | attack6予備動作２ |
| 16 | 285–297 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 285–297 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 285–297 | 攻撃8予備動作２ | attack8予備動作２ |

### 16 — Saturday `e20a`

*Motions: e20a.chr @ data.dat 0x1bc5d800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–280 | 攻撃7（パンチ） | attack7(パンチ) |
| 14 | 255–280 | 攻撃7予備動作１ | attack7予備動作１ |
| 15 | 255–280 | 攻撃7予備動作２ | attack7予備動作２ |
| 16 | 290–302 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 290–302 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 290–302 | 攻撃8予備動作２ | attack8予備動作２ |

### 17 — Witch Hellza `e21a`

*Motions: e21a.chr @ data.dat 0x1bcc9000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 移動ループ前 | move loop (fwd) |
| 2 | 50–60 | 移動ループ後 | move loop (back) |
| 3 | 70–80 | 移動ループ右 | move loop右 |
| 4 | 90–100 | 移動ループ左 | move loop左 |
| 5 | 165–170 | ガード入り | guard (enter) |
| 6 | 170–180 | ガードループ | guard (loop) |
| 7 | 180–185 | ガード戻り | guard (return) |
| 8 | 195–205 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 215–235 | 死亡 | death |
| 12 | 110–128 | 攻撃1 | attack1 |
| 13 | 110–128 | ダミー | (dummy) |
| 14 | 110–128 | ダミー | (dummy) |
| 15 | 110–128 | ダミー | (dummy) |
| 16 | 110–128 | ダミー | (dummy) |
| 17 | 110–128 | ダミー | (dummy) |

### 18 — Witch Illza `e22a`

*Motions: e22a.chr @ data.dat 0x1bd64000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 移動ループ前 | move loop (fwd) |
| 2 | 50–60 | 移動ループ後 | move loop (back) |
| 3 | 70–80 | 移動ループ右 | move loop右 |
| 4 | 90–100 | 移動ループ左 | move loop左 |
| 5 | 165–170 | ガード入り | guard (enter) |
| 6 | 170–180 | ガードループ | guard (loop) |
| 7 | 180–185 | ガード戻り | guard (return) |
| 8 | 195–205 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 215–235 | 死亡 | death |
| 12 | 110–130 | 攻撃1 | attack1 |
| 13 | 60–80 | ダミー | (dummy) |
| 14 | 60–80 | ダミー | (dummy) |
| 15 | 60–80 | ダミー | (dummy) |
| 16 | 60–80 | ダミー | (dummy) |
| 17 | 60–80 | ダミー | (dummy) |

### 19 — Gunny `e23a`

*Motions: e23a.chr @ data.dat 0x1bde9000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–42 | 歩き | walk |
| 2 | 70–80 | バックステップ | back step |
| 3 | 90–100 | 右ステップ | right step |
| 4 | 110–120 | 左ステップ | left step |
| 5 | 130–136 | ガード入り | guard (enter) |
| 6 | 136–142 | ガードループ | guard (loop) |
| 7 | 142–146 | ガード戻り | guard (return) |
| 8 | 150–170 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–190 | 死亡 | death |
| 12 | 190 | 死亡ループ | death loop |
| 13 | 200–215 | 攻撃1(はさみ） | attack1(はさみ) |
| 14 | 200–215 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 200–215 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 220–245 | 攻撃２（口泡） | attack２(口泡) |
| 17 | 220–245 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 220–245 | 攻撃２予備動作２ | attack２予備動作２ |

### 20 — Gyon `e24a`

*Motions: e24a.chr @ data.dat 0x1be46000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | バタアシ | バタアシ |
| 2 | 60–80 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 90–110 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 120–140 | 左ｽﾃｯﾌﾟ | left step |
| 5 | 150–160 | ｶﾞｰﾄﾞ | guard |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 190–200 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 210–225 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 235–260 | ﾀﾞﾒｰｼﾞ２ | damage２ |
| 10 | 270–290 | ﾀﾞﾒｰｼﾞ戻り | damage(return) |
| 11 | 300–325 | 死亡 | death |
| 12 | 325 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 335–355 | 攻撃１ | attack１ |
| 14 | 365–385 | 攻撃２ | attack２ |
| 15 | 390–410 | 攻撃3 | attack3 |

### 21 — Pirate's Chariot `e25a`

*Motions: e25a.chr @ data.dat 0x1bee3800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | 後退 | 後退 |
| 3 | 90–110 | 右旋回 | 右turn |
| 4 | 120–140 | 左旋回 | 左turn |
| 5 | 10–20 | ダミー | (dummy) |
| 6 | 10–20 | ダミー | (dummy) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 150–160 | ダメージ | damage |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 170–185 | 死亡 | death |
| 12 | 185 | 死亡ループ | death loop |
| 13 | 190–210 | 攻撃1 | attack1 |
| 14 | 190–210 | ダミー | (dummy) |
| 15 | 190–210 | ダミー | (dummy) |
| 16 | 250–280 | 攻撃2予備動作 | attack2予備動作 |
| 17 | 250–280 | 攻撃 | attack |
| 18 | 250–280 | 戻り | (return) |

### 22 — Auntie Medu `e26a`

*Motions: e26a.chr @ data.dat 0x1bf24800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | 左ｽﾃｯﾌﾟ | left step |
| 3 | 80–90 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 100–110 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 5 | 120–125 | ｶﾞｰﾄﾞ | guard |
| 6 | 125–135 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 135–140 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 150–160 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 165–170 | ﾀﾞﾒｰｼﾞ２ | damage２ |
| 10 | 175–180 | ﾀﾞﾒｰｼﾞ戻り | damage(return) |
| 11 | 185–195 | 死亡 | death |
| 12 | 195 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 210–230 | 攻撃１ | attack１ |
| 14 | 210–230 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 210–230 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 240–260 | 攻撃２ | attack２ |
| 17 | 240–260 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 240–260 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 23 — Captain `e27a`

*Motions: e27a.chr @ data.dat 0x1bfc4800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 歩き | walk |
| 1 | 30–50 | 歩き | walk |
| 2 | 115–125 | バックステップ | back step |
| 3 | 140–150 | 右ステップ | right step |
| 4 | 160–170 | 左ステップ | left step |
| 5 | 180–184 | ガード入り | guard (enter) |
| 6 | 184–188 | ガードループ | guard (loop) |
| 7 | 188–192 | ガード戻り | guard (return) |
| 8 | 200–210 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 235 | 死亡ループ | death loop |
| 13 | 60–81 | 攻撃1 | attack1 |
| 14 | 60–81 | ダミー | (dummy) |
| 15 | 60–81 | ダミー | (dummy) |
| 16 | 90–106 | 攻撃2 | attack2 |
| 17 | 90–106 | ダミー | (dummy) |
| 18 | 90–106 | ダミー | (dummy) |

### 24 — Corcea `e28a`

*Motions: e28a.chr @ data.dat 0x1c068000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 115–125 | バックステップ | back step |
| 3 | 140–150 | 右ステップ | right step |
| 4 | 160–170 | 左ステップ | left step |
| 5 | 180–184 | ガード入り | guard (enter) |
| 6 | 184–188 | ガードループ | guard (loop) |
| 7 | 188–192 | ガード戻り | guard (return) |
| 8 | 200–210 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 215–235 | 死亡 | death |
| 12 | 235 | 死亡ループ | death loop |
| 13 | 60–81 | 攻撃1 | attack1 |
| 14 | 60–81 | ダミー | (dummy) |
| 15 | 60–81 | ダミー | (dummy) |
| 16 | 90–106 | 攻撃2 | attack2 |
| 17 | 90–106 | ダミー | (dummy) |
| 18 | 90–106 | ダミー | (dummy) |

### 25 — Golem `e30a`

*Motions: e30a.chr @ data.dat 0x1c10d000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 26 — Mr. Blare `e31a`

*Motions: e31a.chr @ data.dat 0x1c1b3800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–130 | ガードループ | guard (loop) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 10–20 | ダミー | (dummy) |
| 9 | 220–234 | 死亡 | death |
| 10 | 234 | 死亡ループ | death loop |
| 11 | 160–185 | 攻撃1 | attack1 |
| 12 | 160–185 | ダミー | (dummy) |
| 13 | 160–185 | ダミー | (dummy) |
| 14 | 190–215 | 攻撃2 | attack2 |
| 15 | 190–215 | ダミー | (dummy) |
| 16 | 190–215 | ダミー | (dummy) |

### 27 — Dune `e32a`

*Motions: e32a.chr @ data.dat 0x1c217800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 70–90 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 100–120 | 左ｽﾃｯﾌﾟ | left step |
| 4 | 130–150 | 右ｽﾃｯﾌﾟ | right step |
| 5 | 160–180 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 190–200 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 210–230 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 240–270 | 死亡 | death |
| 9 | 270 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 10 | 280–310 | ｻﾝﾄﾞｱｯﾊﾟｰ | ｻﾝﾄﾞｱｯﾊﾟｰ |
| 11 | 320–340 | 爪 | 爪 |

### 28 — Titan `e33a`

*Motions: e33a.chr @ data.dat 0x1c2af000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 29 — King Mimic (Divine Beast Cave) `e34a`

*Motions: e34a.chr @ data.dat 0x1c354800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 30 — Mimic (Divine Beast Cave) `e35a`

*Motions: e35a.chr @ data.dat 0x1c3d0000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | 蓋閉じてる | 蓋閉じてる |

### 31 — King Mimic (Sun & Moon Temple) `e36a`

*Motions: e36a.chr @ data.dat 0x1c41e000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 32 — Mimic (Sun & Moon Temple) `e37a`

*Motions: e37a.chr @ data.dat 0x1c499800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | 待ち構え | 待ちstance |

### 33 — King Mimic (Moon Sea) `e38a`

*Motions: e38a.chr @ data.dat 0x1c4e7800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 34 — Mimic (Moon Sea) `e39a`

*Motions: e39a.chr @ data.dat 0x1c564000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | 待ち構え | 待ちstance |

### 35 — Arthur `e40a`

*Motions: e40a.chr @ data.dat 0x1c5b2800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 70–80 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 90–100 | 左ｽﾃｯﾌﾟ | left step |
| 5 | 110–120 | ｶﾞｰﾄﾞ | guard |
| 6 | 130–140 | ｶﾞｰﾄﾞループ | guard (loop) |
| 7 | 150–160 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 170–190 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 200–230 | 死亡 | death |
| 10 | 230 | 死亡ループ | death loop |
| 11 | 240–270 | 攻撃１ | attack１ |
| 12 | 280–310 | 攻撃２ | attack２ |
| 13 | 320–340 | 攻撃３ | attack３ |
| 14 | 350–360 | 攻撃３ループ | attack３(loop) |
| 15 | 370–390 | 攻撃３戻り | attack３(return) |
| 16 | 0–10 | 立ち | idle |

### 36 — Ghost `e42a`

*Motions: e42a.chr @ data.dat 0x1c61a800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | バックステップ | back step |
| 3 | 120–140 | 右ステップ | right step |
| 4 | 90–110 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 240–260 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 270–290 | 死亡 | death |
| 12 | 290 | 死亡 ループ | death (loop) |
| 13 | 150–190 | 攻撃1 | attack1 |
| 14 | 150–190 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 150–190 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 200–230 | 攻撃２ | attack２ |
| 17 | 200–230 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 200–215 | ワープ開始 | ワープ開始 |
| 19 | 215 | ワープ中（要らない？） | ワープ中(要らない？) |
| 20 | 215–230 | ワープ終了 | ワープ終了 |

### 37 — Alexander `e43a`

*Motions: e43a.chr @ data.dat 0x1c6a7800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | バックステップ | back step |
| 3 | 70–80 | 右ステップ | right step |
| 4 | 90–100 | 左ステップ | left step |
| 5 | 110–115 | ガード入り | guard (enter) |
| 6 | 115–125 | ガードループ | guard (loop) |
| 7 | 125–130 | ガード戻り | guard (return) |
| 8 | 140–150 | ダメージ１ | damage１ |
| 9 | 160–172 | ダメージ２ (吹っ飛び） | damage２ (吹っ飛び) |
| 10 | 10–20 | 起き上がり（歩きで移動） | get up(walkでmove) |
| 11 | 180–200 | 死亡 | death |
| 12 | 200 | 死亡ループ | death loop |
| 13 | 210–230 | 攻撃1（切り裂き） | attack1(切り裂き) |
| 14 | 210–230 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 210–230 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 240–255 | 攻撃２（ファイヤーボール） | attack２(ファイヤーボール) |
| 17 | 240–255 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 240–255 | 攻撃２予備動作２ | attack２予備動作２ |

### 38 — Heart `e44a`

*Motions: e44a.chr @ data.dat 0x1c6f6000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(ビーム292/hit） | attack1(beam292/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 39 — Club `e45a`

*Motions: e45a.chr @ data.dat 0x1c7a0800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(殴る929/hit） | attack1(殴る929/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 40 — Diamond `e46a`

*Motions: e46a.chr @ data.dat 0x1c84b800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(つき286/hit） | attack1(つき286/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 41 — Spade `e47a`

*Motions: e47a.chr @ data.dat 0x1c8f2000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(切る290/hit） | attack1(切る290/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 42 — Joker `e48a`

*Motions: e48a.chr @ data.dat 0x1c99b000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(裂く286/hit） | attack1(裂く286/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 43 — Bomber Head `e49a`

*Motions: e49a.chr @ data.dat 0x1ca46000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–45 | ジャンプ歩き | jumpwalk |
| 2 | 350–370 | バックステップ | back step |
| 3 | 110–130 | 右ステップ | right step |
| 4 | 140–160 | 左ステップ | left step |
| 5 | 170–180 | ガード入り | guard (enter) |
| 6 | 180–190 | ガードループ | guard (loop) |
| 7 | 190–200 | ガード戻り | guard (return) |
| 8 | 210–220 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 230–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–300 | 攻撃1(殴り） | attack1(殴り) |
| 14 | 270–300 | 攻撃1予備動作1 | attack1予備動作1 |
| 15 | 270–300 | 攻撃1予備動作2 | attack1予備動作2 |
| 16 | 310–321 | 攻撃2 （ジャンプ入り） | attack2 (jump(enter)) |
| 17 | 321–324 | 攻撃2予備動作1 （ジャンプ中） | attack2予備動作1 (jump中) |
| 18 | 324–335 | 攻撃2予備動作2 （着地） | attack2予備動作2 (landing) |
| 19 | 85–103 | 攻撃3 （投げ） | attack3 (throw) |
| 20 | 85–103 | 攻撃3予備動作1 | attack3予備動作1 |
| 21 | 85–103 | 攻撃3予備動作2 | attack3予備動作2 |
| 22 | 380–400 | 歩き | walk |

### 44 — Mummy `e50a`

*Motions: e50a.chr @ data.dat 0x1cbaa000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–310 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–105 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 115–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 360–385 | 攻撃1 （引っかき） | attack1 (claw) |
| 14 | 360–385 | ダミー | (dummy) |
| 15 | 360–385 | ダミー | (dummy) |
| 16 | 320–350 | 攻撃2  (ブレス） | attack2 (breath) |
| 17 | 320–350 | ダミー | (dummy) |
| 18 | 320–350 | ダミー | (dummy) |

### 45 — Lich `e51a`

*Motions: e51a.chr @ data.dat 0x1cdd4000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | バックステップ | back step |
| 3 | 120–140 | 右ステップ | right step |
| 4 | 90–110 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 240–260 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 270–290 | 死亡 | death |
| 12 | 290 | 死亡 ループ | death (loop) |
| 13 | 150–190 | 攻撃1 | attack1 |
| 14 | 150–190 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 150–190 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 200–230 | 攻撃２ | attack２ |
| 17 | 200–230 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 200–215 | ワープ開始 | ワープ開始 |
| 19 | 215 | ワープ中（要らない？） | ワープ中(要らない？) |
| 20 | 215–230 | ワープ終了 | ワープ終了 |

### 46 — Curse Dancer `e52a`

*Motions: e52a.chr @ data.dat 0x1cf09800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 70–92 | バックステップ | back step |
| 3 | 100–122 | 右ステップ | right step |
| 4 | 130–152 | 左ステップ | left step |
| 5 | 160–164 | ガード入り | guard (enter) |
| 6 | 164–172 | ガードループ | guard (loop) |
| 7 | 172–176 | ガード戻り | guard (return) |
| 8 | 185–205 | ダメージ1 | damage1 |
| 9 | 300–310 | ダメージ2 | damage2 |
| 10 | 320–340 | 起き上がり | get up |
| 11 | 210–228 | 死亡 | death |
| 12 | 229–230 | 死亡ループ | death loop |
| 13 | 240–262 | 攻撃1 | attack1 |
| 14 | 240–262 | 攻撃1予備動作1 | attack1予備動作1 |
| 15 | 240–262 | 攻撃1予備動作2 | attack1予備動作2 |
| 16 | 270–293 | 攻撃2 | attack2 |
| 17 | 270–293 | 攻撃1予備動作1 | attack1予備動作1 |
| 18 | 270–293 | 攻撃2予備動作1 | attack2予備動作1 |

### 47 — Living Armor `e55a`

*Motions: e55a.chr @ data.dat 0x1cfde800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 30–50 | バックステップ | back step |
| 3 | 30–50 | 右ステップ | right step |
| 4 | 30–50 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–202 | 死亡 | death |
| 12 | 202 | 死亡ループ | death loop |
| 13 | 60–80 | 攻撃1 (横） | attack1 (横) |
| 14 | 60–80 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 60–80 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 90–120 | 攻撃2 （ランス突き） | attack2 (ランス突き) |
| 17 | 90–120 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 90–120 | 攻撃２予備動作２ | attack２予備動作２ |

### 48 — White Fang `e56a`

*Motions: e56a.chr @ data.dat 0x1d098000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 90–110 | バックステップ | back step |
| 3 | 190–210 | 右ステップ | right step |
| 4 | 215–235 | 左ステップ | left step |
| 5 | 240–250 | ガード入り | guard (enter) |
| 6 | 250–260 | ガードループ | guard (loop) |
| 7 | 260–270 | ガード戻り | guard (return) |
| 8 | 115–125 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 130–160 | 死亡 | death |
| 12 | 160 | 死亡ループ | death loop |
| 13 | 50–70 | 攻撃1 | attack1 |
| 14 | 50–70 | ダミー | (dummy) |
| 15 | 50–70 | ダミー | (dummy) |
| 16 | 70–90 | 攻撃2 | attack2 |
| 17 | 70–90 | ダミー | (dummy) |
| 18 | 70–90 | ダミー | (dummy) |

### 49 — Moon Bug `e57a`

*Motions: e57a.chr @ data.dat 0x1d160000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–75 | ﾀﾞﾒｰｼﾞ | damage |
| 3 | 85–105 | 死亡 | death |
| 4 | 105 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 5 | 115–135 | 攻撃１ | attack１ |
| 6 | 145–155 | 攻撃１ﾙｰﾌﾟ | attack１ﾙｰﾌﾟ |
| 7 | 165–175 | 攻撃１戻り | attack１(return) |
| 8 | 185–235 | 攻撃２ | attack２ |

### 50 — Phantom `e58a`

*Motions: e58a.chr @ data.dat 0x1d1c2800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | ホバリング | ホバリング |
| 1 | 40–50 | 飛行 | flight |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 10–30 | ホバリング | ホバリング |
| 6 | 10–30 | ホバリング | ホバリング |
| 7 | 10–30 | ホバリング | ホバリング |
| 8 | 120–140 | ダメージ | damage |
| 9 | 10–30 | ホバリング | ホバリング |
| 10 | 10–30 | ホバリング | ホバリング |
| 11 | 150–170 | 死亡入り | death(enter) |
| 12 | 270–280 | 死亡落ちループ | death落ち(loop) |
| 13 | 290–310 | 死亡 | death |
| 14 | 310 | 死亡ループ | death loop |
| 15 | 180–210 | 攻撃1 | attack1 |
| 16 | 180–210 | 攻撃1 | attack1 |
| 17 | 180–210 | 攻撃1 | attack1 |
| 18 | 220–260 | 攻撃2 | attack2 |
| 19 | 220–260 | 攻撃2 | attack2 |
| 20 | 220–260 | 攻撃2 | attack2 |

### 51 — Dragon `e59a`

*Motions: e59a.chr @ data.dat 0x1d223800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 230–240 | ダメージ１ | damage１ |
| 9 | 150–160 | ダメージ２（後ろに下がる） | damage２(後ろに下がる) |
| 10 | 30–50 | 起き上がり(歩きで移動） | get up(walkでmove) |
| 11 | 250–260 | 死亡 | death |
| 12 | 260 | 死亡ループ | death loop |
| 13 | 310–350 | 攻撃1 （火の球） | attack1 (fireの球) |
| 14 | 310–350 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 310–350 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–300 | 攻撃２（頭突き） | attack２(頭突き) |
| 17 | 270–300 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–300 | 攻撃２予備動作２ | attack２予備動作２ |

### 52 — Cave Bat `e60a`

*Motions: e60a.chr @ data.dat 0x1d2e9800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 3 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 4 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 5 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 6 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 7 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 8 | 70–80 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 10 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 11 | 90–100 | 死亡始まり | death始まり |
| 12 | 100–105 | 落下ﾙｰﾌﾟ | 落下ﾙｰﾌﾟ |
| 13 | 105–115 | 死亡 | death |
| 14 | 115 | 死亡停止 | death停止 |
| 15 | 120–145 | 攻撃１ | attack１ |
| 16 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 17 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 150–171 | 攻撃2予備動作 | attack2予備動作 |
| 19 | 171–189 | 攻撃 | attack |
| 20 | 189–215 | 戻り | (return) |

### 53 — Evil Bat `e61a`

*Motions: e61a.chr @ data.dat 0x1d325000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 3 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 4 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 5 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 6 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 7 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 8 | 70–80 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 10 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 11 | 90–100 | 死亡始まり | death始まり |
| 12 | 100–105 | 落下ﾙｰﾌﾟ | 落下ﾙｰﾌﾟ |
| 13 | 105–115 | 死亡 | death |
| 14 | 115 | 死亡停止 | death停止 |
| 15 | 120–145 | 攻撃１ | attack１ |
| 16 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 17 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 150–171 | 攻撃2予備動作 | attack2予備動作 |
| 19 | 171–175 | 攻撃 | attack |
| 20 | 175–200 | 攻撃2予備動作 | attack2予備動作 |

### 54 — Hell Pockle `e62a`

*Motions: e62a.chr @ data.dat 0x1d360800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 150–160 | ダメージ１ | damage１ |
| 9 | 170–190 | ダメージ２ | damage２ |
| 10 | 200–220 | 起き上がり | get up |
| 11 | 230–245 | 死亡 | death |
| 12 | 245 | 死亡ループ | death loop |
| 13 | 255–280 | 攻撃4（斧/飛んで前に出る） | attack4(斧/飛んで前に出る) |
| 14 | 255–280 | 攻撃4予備動作１ | attack4予備動作１ |
| 15 | 255–280 | 攻撃4予備動作２ | attack4予備動作２ |
| 16 | 290–302 | 攻撃8（共通スティール/体当たり。盗ったら早歩きで逃げる） | attack8(共通スティール/体当たり。盗ったら早walkでfleeる) |
| 17 | 290–302 | 攻撃8予備動作１ | attack8予備動作１ |
| 18 | 290–302 | 攻撃8予備動作２ | attack8予備動作２ |

### 55 — Rash Dasher `e63a`

*Motions: e63a.chr @ data.dat 0x1d3e1800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 120–140 | バックステップ | back step |
| 3 | 60–80 | 右ステップ | right step |
| 4 | 90–110 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 240–260 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 270–290 | 死亡 | death |
| 12 | 290 | 死亡ループ | death loop |
| 13 | 150–180 | 攻撃1(縦振り） | attack1(縦振り) |
| 14 | 150–180 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 150–180 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 190–215 | 攻撃２（走り頭突き入り） | attack２(run頭突き(enter)) |
| 17 | 215–223 | 攻撃２予備動作１（走りリピート） | attack２予備動作１(runリピート) |
| 18 | 223–230 | 攻撃２予備動作２ （走り頭突き終わり） | attack２予備動作２ (run頭突き終わり) |

### 56 — Steel Giant `e64a`

*Motions: e64a.chr @ data.dat 0x1d461800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 57 — Blizzard `e65a`

*Motions: e65a.chr @ data.dat 0x1d506800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 58 — Moon Digger `e66a`

*Motions: e66a.chr @ data.dat 0x1d5a5000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 10–20 | ダミー | (dummy) |
| 3 | 10–20 | ダミー | (dummy) |
| 4 | 10–20 | ダミー | (dummy) |
| 5 | 60–70 | もぐり | もぐり |
| 6 | 70–80 | もぐりループ | もぐり(loop) |
| 7 | 80–90 | 出現 | appear |
| 8 | 100–110 | ダメージ | damage |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 180–200 | 死亡 | death |
| 12 | 200 | 死亡ループ | death loop |
| 13 | 120–140 | 攻撃1 | attack1 |
| 14 | 120–140 | ダミー | (dummy) |
| 15 | 120–140 | ダミー | (dummy) |
| 16 | 150–170 | 攻撃2 | attack2 |
| 17 | 150–170 | ダミー | (dummy) |
| 18 | 150–170 | ダミー | (dummy) |

### 59 — Dark Flower `e67a`

*Motions: e67a.chr @ data.dat 0x1d602000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 35–55 | ダメージ | damage |
| 2 | 60–80 | 死亡 | death |
| 3 | 80 | 死亡ループ | death loop |
| 4 | 85–120 | 攻撃葉 | attack葉 |
| 5 | 125–160 | 攻撃液前 | attack液前 |
| 6 | 165–200 | 攻撃液上 | attack液上 |

### 60 — Cursed Rose `e68a`

*Motions: e68a.chr @ data.dat 0x1d664000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 35–55 | ダメージ | damage |
| 2 | 60–80 | 死亡 | death |
| 3 | 80 | 死亡ループ | death loop |
| 4 | 85–120 | 攻撃葉 | attack葉 |
| 5 | 125–160 | 攻撃液前 | attack液前 |
| 6 | 165–200 | 攻撃液上 | attack液上 |

### 61 — Billy `e69a`

*Motions: e69a.chr @ data.dat 0x1d6bc000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–130 | ガードループ | guard (loop) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 10–20 | ダミー | (dummy) |
| 9 | 220–234 | 死亡 | death |
| 10 | 234–240 | 死亡ループ | death loop |
| 11 | 160–185 | 攻撃1 | attack1 |
| 12 | 160–185 | ダミー | (dummy) |
| 13 | 160–185 | ダミー | (dummy) |
| 14 | 190–215 | 攻撃2 | attack2 |
| 15 | 190–215 | ダミー | (dummy) |
| 16 | 190–215 | ダミー | (dummy) |

### 62 — Vulcan `e70a`

*Motions: e70a.chr @ data.dat 0x1d71b000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 70–90 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 100–120 | 左ｽﾃｯﾌﾟ | left step |
| 4 | 130–150 | 右ｽﾃｯﾌﾟ | right step |
| 5 | 160–180 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 190–200 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 210–230 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 240–270 | 死亡 | death |
| 9 | 270 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 10 | 280–310 | ｻﾝﾄﾞｱｯﾊﾟｰ | ｻﾝﾄﾞｱｯﾊﾟｰ |
| 11 | 320–340 | 爪 | 爪 |

### 63 — Crabby Hermit `e71a`

*Motions: e71a.chr @ data.dat 0x1d7b9800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 30–42 | 立ち〈ダミー） | idle〈(dummy)) |
| 1 | 30–42 | 歩き | walk |
| 2 | 70–80 | バックステップ | back step |
| 3 | 90–100 | 右ステップ | right step |
| 4 | 110–120 | 左ステップ | left step |
| 5 | 130–136 | ガード入り | guard (enter) |
| 6 | 136–142 | ガードループ | guard (loop) |
| 7 | 142–146 | ガード戻り | guard (return) |
| 8 | 150–170 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–190 | 死亡 | death |
| 12 | 190 | 死亡ループ | death loop |
| 13 | 200–215 | 攻撃1(はさみ） | attack1(はさみ) |
| 14 | 200–215 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 200–215 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 220–245 | 攻撃２（口泡） | attack２(口泡) |
| 17 | 220–245 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 220–245 | 攻撃２予備動作２ | attack２予備動作２ |
| 19 | 255–275 | 攻撃２（棘） | attack２(棘) |
| 20 | 255–275 | 攻撃２予備動作１ | attack２予備動作１ |
| 21 | 255–275 | 攻撃２予備動作２ | attack２予備動作２ |

### 64 — Space Gyon `e72a`

*Motions: e72a.chr @ data.dat 0x1d812800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | バタアシ | バタアシ |
| 2 | 60–80 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 90–110 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 120–140 | 左ｽﾃｯﾌﾟ | left step |
| 5 | 150–160 | ｶﾞｰﾄﾞ | guard |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 190–200 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 210–225 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 235–260 | ﾀﾞﾒｰｼﾞ２ | damage２ |
| 10 | 270–290 | ﾀﾞﾒｰｼﾞ戻り | damage(return) |
| 11 | 300–325 | 死亡 | death |
| 12 | 325 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 335–355 | 攻撃１ | attack１ |
| 14 | 365–385 | 攻撃２ | attack２ |
| 15 | 390–410 | 攻撃3 | attack3 |

### 65 — Blue Dragon `e73a`

*Motions: e73a.chr @ data.dat 0x1d8b0000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 230–240 | ダメージ１ | damage１ |
| 9 | 150–160 | ダメージ２（後ろに下がる） | damage２(後ろに下がる) |
| 10 | 30–50 | 起き上がり(歩きで移動） | get up(walkでmove) |
| 11 | 250–260 | 死亡 | death |
| 12 | 260 | 死亡ループ | death loop |
| 13 | 310–350 | 攻撃1 （火の球） | attack1 (fireの球) |
| 14 | 310–350 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 270–300 | 攻撃２（頭突き） | attack２(頭突き) |
| 16 | 270–300 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 270–300 | 攻撃２予備動作２ | attack２予備動作２ |

### 66 — Black Dragon `e74a`

*Motions: e74a.chr @ data.dat 0x1d9a5000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–135 | ガードループ | guard (loop) |
| 7 | 135–140 | ガード戻り | guard (return) |
| 8 | 230–240 | ダメージ１ | damage１ |
| 9 | 150–160 | ダメージ２（後ろに下がる） | damage２(後ろに下がる) |
| 10 | 30–50 | 起き上がり(歩きで移動） | get up(walkでmove) |
| 11 | 250–260 | 死亡 | death |
| 12 | 260 | 死亡ループ | death loop |
| 13 | 310–350 | 攻撃1 （火の球） | attack1 (fireの球) |
| 14 | 310–350 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 270–300 | 攻撃２（頭突き） | attack２(頭突き) |
| 16 | 270–300 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 270–300 | 攻撃２予備動作２ | attack２予備動作２ |

### 67 — Mask of Prajna `e75a`

*Motions: e75a.chr @ data.dat 0x1da6d800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | バックステップ | back step |
| 3 | 70–80 | 右ステップ | right step |
| 4 | 90–100 | 左ステップ | left step |
| 5 | 110–115 | ガード入り | guard (enter) |
| 6 | 115–125 | ガードループ | guard (loop) |
| 7 | 125–130 | ガード戻り | guard (return) |
| 8 | 140–150 | ダメージ１ | damage１ |
| 9 | 160–172 | ダメージ２ (吹っ飛び） | damage２ (吹っ飛び) |
| 10 | 10–20 | 起き上がり（歩きで移動） | get up(walkでmove) |
| 11 | 180–200 | 死亡 | death |
| 12 | 200 | 死亡ループ | death loop |
| 13 | 260–290 | 攻撃1（切り裂き） | attack1(切り裂き) |
| 14 | 260–290 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 260–290 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 240–255 | 攻撃２（ファイヤーボール） | attack２(ファイヤーボール) |
| 17 | 240–255 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 240–255 | 攻撃２予備動作２ | attack２予備動作２ |

### 68 — Crescent Baron `e76a`

*Motions: e76a.chr @ data.dat 0x1dadd800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | バック | バック |
| 3 | 90–110 | 右 | 右 |
| 4 | 120–140 | 左 | 左 |
| 5 | 150–155 | ガード（入り） | guard((enter)) |
| 6 | 155–165 | ガードループ | guard (loop) |
| 7 | 165–170 | ガード戻り | guard (return) |
| 8 | 180–190 | ダメージ1 | damage1 |
| 9 | 200–220 | ダメージ2 | damage2 |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 230–250 | 死亡 | death |
| 12 | 250 | 死亡ループ | death loop |
| 13 | 260–280 | 攻撃1 | attack1 |
| 14 | 260–280 | 攻撃1 | attack1 |
| 15 | 260–280 | 攻撃1 | attack1 |
| 16 | 290–310 | 攻撃2 | attack2 |
| 17 | 290–310 | 攻撃2 | attack2 |
| 18 | 290–310 | 攻撃2 | attack2 |
| 19 | 320–340 | 攻撃3 | attack3 |

### 69 — Rockanoff `e77a`

*Motions: e77a.chr @ data.dat 0x1db53000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | バック | バック |
| 3 | 70–80 | 右 | 右 |
| 4 | 90–100 | 左 | 左 |
| 5 | 10–20 | ダミー | (dummy) |
| 6 | 10–20 | ダミー | (dummy) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 110–120 | ダメージ1 | damage1 |
| 9 | 130–150 | ダメージ2 | damage2 |
| 10 | 150–160 | 起き上がり | get up |
| 11 | 170–190 | 死亡 | death |
| 12 | 190 | 死亡ループ | death loop |
| 13 | 200–210 | 攻撃1 | attack1 |
| 14 | 210–220 | 攻撃1 | attack1 |
| 15 | 220–230 | 攻撃1 | attack1 |
| 16 | 240–260 | 攻撃2 | attack2 |
| 17 | 240–260 | 攻撃2 | attack2 |
| 18 | 240–260 | 攻撃2 | attack2 |

### 70 — King Mimic (Wise Owl Forest) `e78a`

*Motions: e78a.chr @ data.dat 0x1db86800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 71 — Mimic (Wise Owl Forest) `e79a`

*Motions: e79a.chr @ data.dat 0x1dc01800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | 出現 | appear |

### 72 — King Mimic (Shipwreck) `e80a`

*Motions: e80a.chr @ data.dat 0x1dc4f800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 73 — Mimic (Shipwreck) `e81a`

*Motions: e81a.chr @ data.dat 0x1dccb000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | 待ち構え | 待ちstance |

### 74 — King Mimic (Gallery of Time) `e82a`

*Motions: e82a.chr @ data.dat 0x1dd19000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 75 — Mimic (Gallery of Time) `e83a`

*Motions: e83a.chr @ data.dat 0x1dd94800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | まちかまえ | まちstance |

### 76 — Ice Arrow `korinoya`

*Motions: kori.chr info.cfg @ data.dat 0x1e3a8800 — 2 (projectile; no death anim).*  
*NOTE: idx 76's real ModelCode is "korinoya" (STB c13_korinoya) — the flying, damaging ice-arrow*  
*PROJECTILE, not the static "kori" ice source (that's idx 102). The 4-char "kori" here was a truncation.*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 氷出現 | ice appear |
| 1 | 40–55 | 氷破裂 | ice burst |

### 77 — Sam `e86a`

*Motions: e86a.chr @ data.dat 0x1ddff000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–125 | ガード入り | guard (enter) |
| 6 | 125–130 | ガードループ | guard (loop) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 10–20 | ダミー | (dummy) |
| 9 | 220–234 | 死亡 | death |
| 10 | 234–240 | 死亡ループ | death loop |
| 11 | 160–185 | 攻撃1 | attack1 |
| 12 | 160–185 | ダミー | (dummy) |
| 13 | 160–185 | ダミー | (dummy) |
| 14 | 190–215 | 攻撃2 | attack2 |
| 15 | 190–215 | ダミー | (dummy) |
| 16 | 190–215 | ダミー | (dummy) |

### 78 — Dran `c12a`

*DBC boss. Motions: c12a.chr info.cfg @ data.dat 0x1a8d6000.*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 飛行 | flight |
| 1 | 35–55 | 着地 | landing (touches down @50) |
| 2 | 270–300 | 突進（離陸） | charge (takeoff) |
| 3 | 200–205 | 突進ループ | charge loop |
| 4 | 70–80 | 突進（着地） | charge (land @74) |
| 5 | 85–95 | ダメージ | damage |
| 6 | 100–120 | 死亡 | death ← collapse |
| 7 | 125–145 | 離陸 | takeoff |
| 8 | 175–196 | 火 | fire breath |
| 9 | 210–215 | 毛繕い（入り） | grooming (enter) |
| 10 | 215–225 | 毛繕いループ | grooming loop |
| 11 | 225–240 | 毛繕い（戻り） | grooming (return) |
| 12 | 245–265 | じたじた | squirm |

### 79 — Master Utan `c14a`

*WOF boss. Motions: c14a.chr info.cfg @ data.dat 0x1ad22000.*  
*NOTE: the .chr's own labels skip 11 (…10, 12, 13, 14, 15); Idx below is the sequential table index*  
*(the _SET_MOTION arg). So verify death = 13 (sequential) vs the .chr's "14" when wiring CollapseMotion.*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–75 | 走り | run |
| 3 | 80–96 | 通常攻撃 | normal attack |
| 4 | 110–163 | ため攻撃 | charge attack |
| 5 | 175–230 | 種マシンガン | seed machinegun |
| 6 | 240–257 | ダメージ左足入り | damage L-leg (enter) |
| 7 | 257–273 | フゥフゥループ | panting loop |
| 8 | 273–278 | ダメージ左足戻り | damage L-leg (return) |
| 9 | 285–302 | ダメージ右足入り | damage R-leg (enter) |
| 10 | 302–318 | フゥフゥループ | panting loop |
| 11 | 318–323 | ダメージ右足戻り | damage R-leg (return) |
| 12 | 335–349 | ダメージ | damage |
| 13 | 360–385 | 死亡 | death ← collapse |
| 14 | 400–420 | バックステップ | back step |

### 80 — Ice Queen `c13a`

*Motions: c13a.chr info.cfg @ data.dat 0x1a9f6800.*  
*Damage model (confirmed from the genuine fight 2026-06-18): IceRes=65486=0xFFCE=-50 → she ABSORBS ice*  
*(ice magic HEALS her). Weak to FIRE (150) and HOLY (120); resists thunder/wind (80). So she is damaged with*  
*fire/holy magic, never ice. Her companions (barrier/arrow/prison/meteor/tornado) are all ice-IMMUNE (IceRes=0)*  
*and fire-weak (200); reiki/Ice Aura alone is fully neutral. MaxHp 700.*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 50–70 | バックステップ | back step |
| 3 | 100–120 | 右ステップ | right step |
| 4 | 75–95 | 左ステップ | left step |
| 5 | 10–20 | ダミー | (dummy) |
| 6 | 10–20 | ダミー | (dummy) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 150–160 | ダメージ1 | damage 1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 165–185 | 死亡 | death ← collapse |
| 12 | 185 | 死亡ループ | death loop |
| 13 | 125–145 | 攻撃1 | attack 1 |
| 14 | 10–20 | ダミー | (dummy) |
| 15 | 10–20 | ダミー | (dummy) |
| 16 | 10–20 | 攻撃2 | attack 2 |
| 17 | 10–20 | ダミー | (dummy) |
| 18 | 10–20 | ダミー | (dummy) |
| 19 | 190–200 | 竜巻（入り） | tornado (enter) |
| 20 | 205–215 | 竜巻（ループ） | tornado (loop) |
| 21 | 216–230 | 竜巻（戻り） | tornado (return) |
| 22 | 240–270 | 氷落とし | ice drop |
| 23 | 280–313 | 機雷たつまき | mine tornado |
| 24 | 320–326 | バリア（入り） | barrier (enter) |
| 25 | 327–335 | バリア（ループ） | barrier (loop) |
| 26 | 336–340 | バリア（戻り） | barrier (return) |

### 81 — King's Curse Coffin `c15a`

*SMT boss. Motions: c15a.chr info.cfg @ data.dat 0x1ae5c000.*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 待機 | idle |
| 1 | 30–50 | 開く | open |
| 2 | 50–55 | 開きループ | open loop |
| 3 | 55–65 | 閉じる | close |
| 4 | 70–80 | ダメージ | damage |
| 5 | 90–110 | 死亡 | death ← collapse |
| 6 | 110–115 | 死亡ループ | death loop |

### 82 — King's Curse `c15b`

*Motions: c15b.chr info.cfg @ data.dat 0x1ae94000 — no "死亡" (transformation entity; no collapse).*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–95 | 攻撃 | attack |
| 3 | 115–135 | 出現 | appear |
| 4 | 140–145 | ノーマル〜黒玉 | normal → black-orb |
| 5 | 150–155 | 黒玉〜ノーマル | black-orb → normal |
| 6 | 160–165 | ノーマル〜棺桶 | normal → coffin |
| 7 | 180–185 | 黒玉〜棺桶 | black-orb → coffin |
| 8 | 190–200 | 黒丸ループ | black-orb loop |
| 9 | 165–175 | 棺桶ループ | coffin loop |

### 83 — Minotaur Joe `c16a`

*MS boss. Motions: c16a.chr info.cfg @ data.dat 0x1aee3000.*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | 走り | run |
| 3 | 140–170 | 飲む | drink |
| 4 | 210–260 | 雄たけび | roar ← BossScriptPatcher.RoarMotion |
| 5 | 180–202 | 攻撃1 | attack 1 |
| 6 | 270–294 | 攻撃2 | attack 2 |
| 7 | 90–105 | ダメージけつ | damage (rear) |
| 8 | 115–130 | ダメージ顔 | damage (face) |
| 9 | 300–330 | 死亡 | death ← collapse |
| 10 | 340–356 | バックステップ | back step |

### 84 — Dark Genie `c17a`

*GoT boss (final). Motions: c17a.chr info.cfg @ data.dat 0x1afed000.*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 待機 | idle |
| 1 | 30–38 | たたみ入り | wing-fold (enter) |
| 2 | 39–50 | たたみループ | wing-fold (loop) |
| 3 | 51–65 | たたみ戻り | wing-fold (return) |
| 4 | 75–81 | はばたき入り | wing-flap (enter) |
| 5 | 81–91 | はばたきループ | wing-flap (loop) |
| 6 | 91–97 | はばたき戻り | wing-flap (return) |
| 7 | 110–121 | 右攻撃 | right attack |
| 8 | 121–122 | ループ | loop |
| 9 | 122–130 | 戻り | return |
| 10 | 145–156 | 左攻撃 | left attack |
| 11 | 156–157 | ループ | loop |
| 12 | 157–165 | 戻り | return |
| 13 | 175–188 | ビーム入り | beam (enter) |
| 14 | 188–189 | ビームループ | beam (loop) |
| 15 | 188–197 | ビーム戻り | beam (return) |
| 16 | 210–222 | ダメージ目 | damage (eye) |
| 17 | 230–235 | ダメージ右手 | damage (R hand) |
| 18 | 245–250 | ダメージ左手 | damage (L hand) |
| 19 | 260–295 | 死亡 | death ← collapse |
| 20 | 292–295 | 死亡ループ | death loop |

### 85 — Dark Genie (form 2) `c17b`

*Motions: c17b.chr info.cfg @ data.dat 0x1b0d8800 — only 2 (no death anim; defeat is scripted).*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 9–10 | 攻撃のかまえ | attack stance |
| 1 | 20–30 | ダメージ | damage |

### 86 — Right Hand `c17c`

*Dark Genie hands. Motions: c17c.chr info.cfg @ data.dat 0x1b160800 — only 2 (no death anim).*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 9–10 | 攻撃のかまえ | attack stance |
| 1 | 20–30 | ダメージ | damage |

### 87 — Left Hand `c17_`

*Motions: ModelCode "c17_" has no own .chr (shares the hand model); see Right Hand (c17c) above.*  

### 89 — (DG companion c17_) `c17_`

*Motions: c17_beem.chr @ data.dat 0x1b1e9000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–50 | 発射 | launch |
| 1 | 60–68 | ループ | (loop) |
| 2 | 80–90 | 消滅 | despawn |

### 90 — (DG companion c17_) `c17_`

*Motions: c17_beem.chr @ data.dat 0x1b1e9000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–50 | 発射 | launch |
| 1 | 60–68 | ループ | (loop) |
| 2 | 80–90 | 消滅 | despawn |

### 91 — Wine Keg `e85a`

*Motions: e85a.chr info.cfg @ data.dat 0x1ddf5000 — 2 (object; no death anim).*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–15 | 回る | spin / roll |
| 1 | 20–25 | 落ちてる | lying fallen |

### 93 — (DG companion c17_) `c17_`

*Motions: c17_beem.chr @ data.dat 0x1b1e9000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–50 | 発射 | launch |
| 1 | 60–68 | ループ | (loop) |
| 2 | 80–90 | 消滅 | despawn |

### 94 — Gol `e90a`

*Motions: e90a.chr @ data.dat 0x1de72000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 95 — Sil `e91a`

*Motions: e91a.chr @ data.dat 0x1df13800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 96 — Yammich `e101`

*Motions: e101a.chr @ data.dat 0x1e730800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–15 | 立ち | idle |
| 1 | 5–15 | 歩き | walk |
| 2 | 20–35 | バックステップ | back step |
| 3 | 40–50 | 右ステップ | right step |
| 4 | 55–65 | 左ステップ | left step |
| 5 | 5–15 | ガード入り | guard (enter) |
| 6 | 5–15 | ガードループ | guard (loop) |
| 7 | 5–15 | ガード戻り | guard (return) |
| 8 | 130–140 | ダメージ1 | damage1 |
| 9 | 130–140 | ダメージ2 | damage2 |
| 10 | 130–140 | 起き上がり | get up |
| 11 | 145–165 | 死亡 | death |
| 12 | 165 | 死亡ループ | death loop |
| 13 | 75–90 | 攻撃１入り | attack１(enter) |
| 14 | 95–105 | 攻撃１ループ | attack１(loop) |
| 15 | 110–125 | 攻撃１戻り | attack１(return) |

### 97 — Statue Dog `e103`

*Motions: e103a.chr @ data.dat 0x1e15d000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–15 | 立ち | idle |
| 1 | 20–40 | 歩き | walk |
| 2 | 45–55 | バックステップ | back step |
| 3 | 60–70 | 右ステップ | right step |
| 4 | 75–85 | 左ステップ | left step |
| 5 | 90–100 | ガード入り | guard (enter) |
| 6 | 100 | ガードループ | guard (loop) |
| 7 | 105–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–160 | 死亡 | death |
| 10 | 160 | 死亡L | deathL |
| 11 | 165–180 | 攻撃1 | attack1 |

### 98 — Opar `e104`

*Motions: e104a.chr @ data.dat 0x1dfb5800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き(あしふみ） | walk(あしふみ) |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 60–70 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 80–100 | 死亡 | death |
| 12 | 100 | 死亡ループ | death loop |
| 13 | 110–120 | 攻撃1（エラからネバネバ液） | attack1(エラからネバネバ液) |
| 14 | 110–120 | 攻撃1予備動作１（エラからネバネバ液） | attack1予備動作１(エラからネバネバ液) |
| 15 | 110–120 | 攻撃1予備動作２（エラからネバネバ液） | attack1予備動作２(エラからネバネバ液) |
| 16 | 130–150 | 攻撃２（超重プレス） | attack２(超重プレス) |
| 17 | 160–180 | 攻撃２予備動作１（起き上がり） | attack２予備動作１(get up) |
| 18 | 160–180 | 攻撃２予備動作２（起き上がり） | attack２予備動作２(get up) |

### 99 — Haley Holey `e105`

*Motions: e105a.chr @ data.dat 0x1e789800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 220–240 | バックステップ | back step |
| 3 | 190–215 | 右ステップ | right step |
| 4 | 160–185 | 左ステップ | left step |
| 5 | 60–75 | もぐる | もぐる |
| 6 | 75–85 | もぐりるーぷ | もぐりるーぷ |
| 7 | 85–100 | 這い出る | 這い出る |
| 8 | 110–120 | ダメージ | damage |
| 9 | 110–120 | ダメージ | damage |
| 10 | 30–50 | ダミー | (dummy) |
| 11 | 130–150 | 死亡 | death |
| 12 | 150 | 死亡ループ | death loop |
| 13 | 250–265 | 攻撃予備動作1 | attack予備動作1 |
| 14 | 300–310 | 攻撃 | attack |
| 15 | 275–290 | 戻り | (return) |
| 16 | 265–275 | 攻撃2 | attack2 |

### 100 — King Prickly `e106`

*Motions: e106a.chr @ data.dat 0x1e242800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 10–30 | 歩き | walk |
| 2 | 10–30 | バックステップ | back step |
| 3 | 10–30 | 右ステップ | right step |
| 4 | 10–30 | 左ステップ | left step |
| 5 | 115–125 | ガード入り | guard (enter) |
| 6 | 115–125 | ガードループ | guard (loop) |
| 7 | 115–125 | ガード戻り | guard (return) |
| 8 | 135–145 | ダメージ１ | damage１ |
| 9 | 154–165 | ダメージ２ | damage２ |
| 10 | 175–190 | 起き上がり | get up |
| 11 | 10–30 | 死亡（消える） | death(消える) |
| 12 | 10–30 | 死亡ループ （消える） | death loop (消える) |
| 13 | 200–205 | 攻撃1 | attack1 |
| 14 | 205–225 | 攻撃1予備動作1 | attack1予備動作1 |
| 15 | 225–235 | 攻撃1予備動作2 | attack1予備動作2 |
| 16 | 245–250 | 攻撃2 | attack2 |
| 17 | 250–260 | 攻撃2予備動作1 | attack2予備動作1 |
| 18 | 260–265 | 攻撃2予備動作2 | attack2予備動作2 |
| 19 | 275–280 | 攻撃3 | attack3 |
| 20 | 280–300 | 攻撃3予備動作1 | attack3予備動作1 |
| 21 | 300–305 | 攻撃3予備動作2 | attack3予備動作2 |

### 102 — Ice Prison `kori`

*Motions: kori.chr @ data.dat 0x1e3a8800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 氷出現 | iceappear |
| 1 | 40–55 | 氷破裂 | ice破裂 |

### 105 — Gacious `e124`

*Motions: e124a.chr @ data.dat 0x1ec15000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–305 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–104 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 116–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 50–64 | 攻撃1 | attack1 |
| 14 | 64–90 | 攻撃11 | attack11 |
| 15 | 50–90 | ダミー | (dummy) |
| 16 | 310–330 | 攻撃2 | attack2 |
| 17 | 330–360 | 攻撃2 | attack2 |
| 18 | 310–360 | ダミー | (dummy) |

### 106 — Dark Genie (Final Form) `c23a`

*Motions: c23a.chr info.cfg.*  
*Idx  Frames    Name (JP)        Meaning*  
*0    5–20      基本立ち          basic idle*  
*1    26–30     肩開き            shoulder open*  
*2    30–40     開きループ        open loop*  
*3    40–44     肩閉じ            shoulder close*  
*4    51–70     左手ひっぱたき     left-hand slap*  
*5    76–94     右手ひっぱたき     right-hand slap*  
*6    100–107   口開け            mouth open*  
*7    107–115   溜                charge*  
*8    115–123   発射体制に        firing stance*  
*9    123–130   発射ループ        firing loop*  
*10   130–140   発射戻り          firing return*  

### 111 — Gemron (Fire) `e111`

*Motions: e111a.chr @ data.dat 0x1e860000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–35 | 歩き | walk |
| 2 | 40–50 | バックステップ | back step |
| 3 | 55–65 | 右ステップ | right step |
| 4 | 70–80 | 左ステップ | left step |
| 5 | 155–165 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 185–195 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 85–100 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 105–125 | 死亡 | death |
| 12 | 125 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 130–150 | 攻撃１ | attack１ |
| 14 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 130–150 | 攻撃２ | attack２ |
| 17 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 112 — Nikapous `e108`

*Motions: e108a.chr @ data.dat 0x1e028000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–35 | 歩き | walk |
| 2 | 10–20 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 3 | 10–20 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 4 | 10–20 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 5 | 40–80 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 85–95 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 100–110 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 115–130 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 135–155 | 死亡 | death |
| 12 | 155 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 160–190 | 攻撃１ | attack１ |
| 14 | 160–190 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 160–190 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 195–225 | 攻撃２ | attack２ |
| 17 | 195–225 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 195–225 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 113 — White Fang (Enhanced) `e125`

*Motions: e125a.chr @ data.dat 0x1ec7d000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 90–110 | バックステップ | back step |
| 3 | 190–210 | 右ステップ | right step |
| 4 | 215–235 | 左ステップ | left step |
| 5 | 240–250 | ガード入り | guard (enter) |
| 6 | 250–260 | ガードループ | guard (loop) |
| 7 | 260–270 | ガード戻り | guard (return) |
| 8 | 115–125 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 130–160 | 死亡 | death |
| 12 | 160 | 死亡ループ | death loop |
| 13 | 50–70 | 攻撃1 | attack1 |
| 14 | 50–70 | ダミー | (dummy) |
| 15 | 50–70 | ダミー | (dummy) |
| 16 | 70–90 | 攻撃2 | attack2 |
| 17 | 70–90 | ダミー | (dummy) |
| 18 | 70–90 | ダミー | (dummy) |

### 114 — Arthur (Enhanced) `e126`

*Motions: e126a.chr @ data.dat 0x1ed6b000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 70–80 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 90–100 | 左ｽﾃｯﾌﾟ | left step |
| 5 | 110–120 | ｶﾞｰﾄﾞ | guard |
| 6 | 130–140 | ｶﾞｰﾄﾞループ | guard (loop) |
| 7 | 150–160 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 170–190 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 200–230 | 死亡 | death |
| 10 | 230 | 死亡ループ | death loop |
| 11 | 240–270 | 攻撃１ | attack１ |
| 12 | 280–310 | 攻撃２ | attack２ |
| 13 | 320–340 | 攻撃３ | attack３ |
| 14 | 350–360 | 攻撃３ループ | attack３(loop) |
| 15 | 370–390 | 攻撃３戻り | attack３(return) |
| 16 | 0–10 | 立ち | idle |

### 115 — Sil (Enhanced) `e127`

*Motions: e127a.chr @ data.dat 0x1edd3000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 116 — Halloween (Enhanced) `e128`

*Motions: e128a.chr @ data.dat 0x1ee7a800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–45 | 前移動ループ | move loop (fwd) |
| 2 | 30–45 | ダミー | (dummy) |
| 3 | 110–130 | 右ステップ | right step |
| 4 | 140–160 | 左ステップ | left step |
| 5 | 170–180 | ガード入り | guard (enter) |
| 6 | 180–190 | ガードループ | guard (loop) |
| 7 | 190–200 | ガード戻り | guard (return) |
| 8 | 210–220 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 230–255 | 死亡 | death |
| 12 | 55–79 | 攻撃1 | attack1 |
| 13 | 265–285 | バックステップ | back step |
| 14 | 295–315 | 歩き | walk |
| 15 | 85–103 | 攻撃2 | attack2 |
| 16 | 85–103 | ダミー | (dummy) |
| 17 | 85–103 | ダミー | (dummy) |

### 117 — Master Jacket (Enhanced) `e129`

*Motions: e129a.chr @ data.dat 0x1ef24000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–305 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–105 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 115–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 50–70 | 攻撃1 | attack1 |
| 14 | 50–61 | 振りかぶり | wind-up |
| 15 | 61–70 | 振り下ろし | downswing |
| 16 | 75–95 | 攻撃2 | attack2 |
| 17 | 75–95 | ダミー | (dummy) |
| 18 | 75–95 | ダミー | (dummy) |
| 19 | 280–295 | 飛び込み | lunge |

### 118 — Vulcan (Enhanced) `e130`

*Motions: e130a.chr @ data.dat 0x1f034800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 70–90 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 100–120 | 左ｽﾃｯﾌﾟ | left step |
| 4 | 130–150 | 右ｽﾃｯﾌﾟ | right step |
| 5 | 160–180 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 190–200 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 210–230 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 240–270 | 死亡 | death |
| 9 | 270 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 10 | 280–310 | ｻﾝﾄﾞｱｯﾊﾟｰ | ｻﾝﾄﾞｱｯﾊﾟｰ |
| 11 | 320–340 | 爪 | 爪 |

### 119 — Mummy (Enhanced) `e131`

*Motions: e131a.chr @ data.dat 0x1f0d3000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–310 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–105 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 115–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 360–385 | 攻撃1 （引っかき） | attack1 (claw) |
| 14 | 360–385 | ダミー | (dummy) |
| 15 | 360–385 | ダミー | (dummy) |
| 16 | 320–350 | 攻撃2  (ブレス） | attack2 (breath) |
| 17 | 320–350 | ダミー | (dummy) |
| 18 | 320–350 | ダミー | (dummy) |

### 120 — Diamond (Enhanced) `e132`

*Motions: e132a.chr @ data.dat 0x1f218000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(つき286/hit） | attack1(つき286/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 121 — Gemron (Ice) `e112`

*Motions: e112a.chr @ data.dat 0x1e8ba000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–35 | 歩き | walk |
| 2 | 40–50 | バックステップ | back step |
| 3 | 55–65 | 右ステップ | right step |
| 4 | 70–80 | 左ステップ | left step |
| 5 | 155–165 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 185–195 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 85–100 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 105–125 | 死亡 | death |
| 12 | 125 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 130–150 | 攻撃１ | attack１ |
| 14 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 130–150 | 攻撃２ | attack２ |
| 17 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 122 — Horn Head `e119`

*Motions: e119a.chr @ data.dat 0x1eb74000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 280–290 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–105 | ガード入り | guard (enter) |
| 6 | 106–114 | ガードループ | guard (loop) |
| 7 | 115–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 300–325 | 攻撃1 | attack1 |
| 14 | 300–325 | ダミー | (dummy) |
| 15 | 300–325 | ダミー | (dummy) |
| 16 | 330–360 | 攻撃2 | attack2 |
| 17 | 330–360 | ダミー | (dummy) |
| 18 | 330–360 | ダミー | (dummy) |

### 123 — Auntie Medu (Enhanced) `e133`

*Motions: e133a.chr @ data.dat 0x1f2be800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | 左ｽﾃｯﾌﾟ | left step |
| 3 | 80–90 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 100–110 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 5 | 120–125 | ｶﾞｰﾄﾞ | guard |
| 6 | 125–135 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 135–140 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 150–160 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 165–170 | ﾀﾞﾒｰｼﾞ２ | damage２ |
| 10 | 175–180 | ﾀﾞﾒｰｼﾞ戻り | damage(return) |
| 11 | 185–195 | 死亡 | death |
| 12 | 195 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 210–230 | 攻撃１ | attack１ |
| 14 | 210–230 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 210–230 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 240–260 | 攻撃２ | attack２ |
| 17 | 240–260 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 240–260 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 124 — Rockanoff (Enhanced) `e134`

*Motions: e134a.chr @ data.dat 0x1f35e800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | バック | バック |
| 3 | 70–80 | 右 | 右 |
| 4 | 90–100 | 左 | 左 |
| 5 | 10–20 | ダミー | (dummy) |
| 6 | 10–20 | ダミー | (dummy) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 110–120 | ダメージ1 | damage1 |
| 9 | 130–150 | ダメージ2 | damage2 |
| 10 | 150–160 | 起き上がり | get up |
| 11 | 170–190 | 死亡 | death |
| 12 | 190 | 死亡ループ | death loop |
| 13 | 200–210 | 攻撃1 | attack1 |
| 14 | 210–220 | 攻撃1 | attack1 |
| 15 | 220–230 | 攻撃1 | attack1 |
| 16 | 240–260 | 攻撃2 | attack2 |
| 17 | 240–260 | 攻撃2 | attack2 |
| 18 | 240–260 | 攻撃2 | attack2 |

### 125 — Yammich (Enhanced) `e135`

*Motions: e135a.chr @ data.dat 0x1f392000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–15 | 立ち | idle |
| 1 | 5–15 | 歩き | walk |
| 2 | 20–35 | バックステップ | back step |
| 3 | 40–50 | 右ステップ | right step |
| 4 | 55–65 | 左ステップ | left step |
| 5 | 5–15 | ガード入り | guard (enter) |
| 6 | 5–15 | ガードループ | guard (loop) |
| 7 | 5–15 | ガード戻り | guard (return) |
| 8 | 130–140 | ダメージ1 | damage1 |
| 9 | 130–140 | ダメージ2 | damage2 |
| 10 | 130–140 | 起き上がり | get up |
| 11 | 145–165 | 死亡 | death |
| 12 | 165 | 死亡ループ | death loop |
| 13 | 75–90 | 攻撃１入り | attack１(enter) |
| 14 | 95–105 | 攻撃１ループ | attack１(loop) |
| 15 | 110–125 | 攻撃１戻り | attack１(return) |

### 126 — Witch Hellza (Enhanced) `e136`

*Motions: e136a.chr @ data.dat 0x1f3eb800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 移動ループ前 | move loop (fwd) |
| 2 | 50–60 | 移動ループ後 | move loop (back) |
| 3 | 70–80 | 移動ループ右 | move loop右 |
| 4 | 90–100 | 移動ループ左 | move loop左 |
| 5 | 165–170 | ガード入り | guard (enter) |
| 6 | 170–180 | ガードループ | guard (loop) |
| 7 | 180–185 | ガード戻り | guard (return) |
| 8 | 195–205 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 215–235 | 死亡 | death |
| 12 | 110–128 | 攻撃1 | attack1 |
| 13 | 110–128 | ダミー | (dummy) |
| 14 | 110–128 | ダミー | (dummy) |
| 15 | 110–128 | ダミー | (dummy) |
| 16 | 110–128 | ダミー | (dummy) |
| 17 | 110–128 | ダミー | (dummy) |

### 127 — Steel Giant (Enhanced) `e137`

*Motions: e137a.chr @ data.dat 0x1f486800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 128 — Club (Enhanced) `e138`

*Motions: e138a.chr @ data.dat 0x1f52b800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(殴る929/hit） | attack1(殴る929/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 129 — Corcea (Enhanced) `e139`

*Motions: e139a.chr @ data.dat 0x1f5d6800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 115–125 | バックステップ | back step |
| 3 | 140–150 | 右ステップ | right step |
| 4 | 160–170 | 左ステップ | left step |
| 5 | 180–184 | ガード入り | guard (enter) |
| 6 | 184–188 | ガードループ | guard (loop) |
| 7 | 188–192 | ガード戻り | guard (return) |
| 8 | 200–210 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 215–235 | 死亡 | death |
| 12 | 235 | 死亡ループ | death loop |
| 13 | 60–81 | 攻撃1 | attack1 |
| 14 | 60–81 | ダミー | (dummy) |
| 15 | 60–81 | ダミー | (dummy) |
| 16 | 90–106 | 攻撃2 | attack2 |
| 17 | 90–106 | ダミー | (dummy) |
| 18 | 90–106 | ダミー | (dummy) |

### 130 — Mimic (Demon Shaft) `e109`

*Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | まちかまえ | まちstance |

### 131 — King Mimic (Demon Shaft) `e110`

*Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 132 — Gemron (Thunder) `e113`

*Motions: e113a.chr @ data.dat 0x1e914000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–35 | 歩き | walk |
| 2 | 40–50 | バックステップ | back step |
| 3 | 55–65 | 右ステップ | right step |
| 4 | 70–80 | 左ステップ | left step |
| 5 | 155–165 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 185–195 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 85–100 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 105–125 | 死亡 | death |
| 12 | 125 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 130–150 | 攻撃１ | attack１ |
| 14 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 130–150 | 攻撃２ | attack２ |
| 17 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 133 — Bishop Q `e116`

*Motions: e116a.chr @ data.dat 0x1ea22000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | バックステップ | back step |
| 3 | 70–80 | 右ステップ | right step |
| 4 | 90–100 | 左ステップ | left step |
| 5 | 110–115 | ガード入り | guard (enter) |
| 6 | 115–125 | ガードループ | guard (loop) |
| 7 | 125–130 | ガード戻り | guard (return) |
| 8 | 140–153 | ダメージ１ | damage１ |
| 9 | 165–183 | ダメージ２ | damage２ |
| 10 | 225–243 | 死亡 | death |
| 11 | 243 | 死亡ループ | death loop |
| 12 | 255–285 | 攻撃1（近距離パンチ） | attack1(近距離パンチ) |
| 13 | 255–285 | 攻撃1予備動作２ | attack1予備動作２ |
| 14 | 295–322 | 攻撃２（魔法） | attack２(magic) |
| 15 | 295–322 | 攻撃２予備動作１ | attack２予備動作１ |
| 16 | 295–322 | 攻撃２予備動作２ | attack２予備動作２ |

### 134 — Cave Bat (Enhanced) `e140`

*Motions: e140a.chr @ data.dat 0x1f67a000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 3 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 4 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 5 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 6 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 7 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 8 | 70–80 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 10 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 11 | 90–100 | 死亡始まり | death始まり |
| 12 | 100–105 | 落下ﾙｰﾌﾟ | 落下ﾙｰﾌﾟ |
| 13 | 105–115 | 死亡 | death |
| 14 | 115 | 死亡停止 | death停止 |
| 15 | 120–145 | 攻撃１ | attack１ |
| 16 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 17 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 150–171 | 攻撃2予備動作 | attack2予備動作 |
| 19 | 171–189 | 攻撃 | attack |
| 20 | 189–215 | 戻り | (return) |

### 135 — Gol (Enhanced) `e141`

*Motions: e141a.chr @ data.dat 0x1f6b5800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 136 — Mask of Prajna (Enhanced) `e142`

*Motions: e142a.chr @ data.dat 0x1f75d000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | バックステップ | back step |
| 3 | 70–80 | 右ステップ | right step |
| 4 | 90–100 | 左ステップ | left step |
| 5 | 110–115 | ガード入り | guard (enter) |
| 6 | 115–125 | ガードループ | guard (loop) |
| 7 | 125–130 | ガード戻り | guard (return) |
| 8 | 140–150 | ダメージ１ | damage１ |
| 9 | 160–172 | ダメージ２ (吹っ飛び） | damage２ (吹っ飛び) |
| 10 | 10–20 | 起き上がり（歩きで移動） | get up(walkでmove) |
| 11 | 180–200 | 死亡 | death |
| 12 | 200 | 死亡ループ | death loop |
| 13 | 260–290 | 攻撃1（切り裂き） | attack1(切り裂き) |
| 14 | 260–290 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 260–290 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 240–255 | 攻撃２（ファイヤーボール） | attack２(ファイヤーボール) |
| 17 | 240–255 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 240–255 | 攻撃２予備動作２ | attack２予備動作２ |

### 137 — Gyon (Enhanced) `e143`

*Motions: e143a.chr @ data.dat 0x1f7cd000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | バタアシ | バタアシ |
| 2 | 60–80 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 90–110 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 120–140 | 左ｽﾃｯﾌﾟ | left step |
| 5 | 150–160 | ｶﾞｰﾄﾞ | guard |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 190–200 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 210–225 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 235–260 | ﾀﾞﾒｰｼﾞ２ | damage２ |
| 10 | 270–290 | ﾀﾞﾒｰｼﾞ戻り | damage(return) |
| 11 | 300–325 | 死亡 | death |
| 12 | 325 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 335–355 | 攻撃１ | attack１ |
| 14 | 365–385 | 攻撃２ | attack２ |
| 15 | 390–410 | 攻撃3 | attack3 |

### 138 — Spade (Enhanced) `e144`

*Motions: e144a.chr @ data.dat 0x1f86a800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(切る290/hit） | attack1(切る290/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 139 — Rash Dasher (Enhanced) `e145`

*Motions: e145a.chr @ data.dat 0x1f913800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 120–140 | バックステップ | back step |
| 3 | 60–80 | 右ステップ | right step |
| 4 | 90–110 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 240–260 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 270–290 | 死亡 | death |
| 12 | 290 | 死亡ループ | death loop |
| 13 | 150–180 | 攻撃1(縦振り） | attack1(縦振り) |
| 14 | 150–180 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 150–180 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 190–215 | 攻撃２（走り頭突き入り） | attack２(run頭突き(enter)) |
| 17 | 215–223 | 攻撃２予備動作１（走りリピート） | attack２予備動作１(runリピート) |
| 18 | 223–230 | 攻撃２予備動作２ （走り頭突き終わり） | attack２予備動作２ (run頭突き終わり) |

### 140 — Captain (Enhanced) `e146`

*Motions: e146a.chr @ data.dat 0x1f994000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 歩き | walk |
| 1 | 30–50 | 歩き | walk |
| 2 | 115–125 | バックステップ | back step |
| 3 | 140–150 | 右ステップ | right step |
| 4 | 160–170 | 左ステップ | left step |
| 5 | 180–184 | ガード入り | guard (enter) |
| 6 | 184–188 | ガードループ | guard (loop) |
| 7 | 188–192 | ガード戻り | guard (return) |
| 8 | 200–210 | ダメージ1 | damage1 |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 235 | 死亡ループ | death loop |
| 13 | 60–81 | 攻撃1 | attack1 |
| 14 | 60–81 | ダミー | (dummy) |
| 15 | 60–81 | ダミー | (dummy) |
| 16 | 90–106 | 攻撃2 | attack2 |
| 17 | 90–106 | ダミー | (dummy) |
| 18 | 90–106 | ダミー | (dummy) |

### 141 — Mimic (Demon Shaft) (Enhanced) `e109`

*Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | まちかまえ | まちstance |

### 142 — King Mimic (Demon Shaft) (Enhanced) `e110`

*Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 143 — Gemron (Wind) `e114`

*Motions: e114a.chr @ data.dat 0x1e96e000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–35 | 歩き | walk |
| 2 | 40–50 | バックステップ | back step |
| 3 | 55–65 | 右ステップ | right step |
| 4 | 70–80 | 左ステップ | left step |
| 5 | 155–165 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 185–195 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 85–100 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 105–125 | 死亡 | death |
| 12 | 125 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 130–150 | 攻撃１ | attack１ |
| 14 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 130–150 | 攻撃２ | attack２ |
| 17 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 144 — Silver Gear `e118`

*Motions: e118a.chr @ data.dat 0x1eab7800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–305 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–105 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 115–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 50–75 | 攻撃1 | attack1 |
| 14 | 50–75 | ダミー | (dummy) |
| 15 | 50–75 | ダミー | (dummy) |
| 16 | 50–60 | 攻撃2構え | attack2stance |
| 17 | 60–75 | 発射 | launch |
| 18 | 50–75 | ダミー | (dummy) |

### 145 — Alexnder (Enhanced) `e149`

*Motions: e149a.chr @ data.dat 0x1fb03800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–40 | 歩き | walk |
| 2 | 50–60 | バックステップ | back step |
| 3 | 70–80 | 右ステップ | right step |
| 4 | 90–100 | 左ステップ | left step |
| 5 | 110–115 | ガード入り | guard (enter) |
| 6 | 115–125 | ガードループ | guard (loop) |
| 7 | 125–130 | ガード戻り | guard (return) |
| 8 | 140–150 | ダメージ１ | damage１ |
| 9 | 160–172 | ダメージ２ (吹っ飛び） | damage２ (吹っ飛び) |
| 10 | 10–20 | 起き上がり（歩きで移動） | get up(walkでmove) |
| 11 | 180–200 | 死亡 | death |
| 12 | 200 | 死亡ループ | death loop |
| 13 | 210–230 | 攻撃1（切り裂き） | attack1(切り裂き) |
| 14 | 210–230 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 210–230 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 240–255 | 攻撃２（ファイヤーボール） | attack２(ファイヤーボール) |
| 17 | 240–255 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 240–255 | 攻撃２予備動作２ | attack２予備動作２ |

### 146 — Heart (Enhanced) `e150`

*Motions: e150a.chr @ data.dat 0x1fb54000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(ビーム292/hit） | attack1(beam292/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 147 — Bomber Head (Enhanced) `e151`

*Motions: e151a.chr @ data.dat 0x1fbfe800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–45 | ジャンプ歩き | jumpwalk |
| 2 | 350–370 | バックステップ | back step |
| 3 | 110–130 | 右ステップ | right step |
| 4 | 140–160 | 左ステップ | left step |
| 5 | 170–180 | ガード入り | guard (enter) |
| 6 | 180–190 | ガードループ | guard (loop) |
| 7 | 190–200 | ガード戻り | guard (return) |
| 8 | 210–220 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 230–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–300 | 攻撃1(殴り） | attack1(殴り) |
| 14 | 270–300 | 攻撃1予備動作1 | attack1予備動作1 |
| 15 | 270–300 | 攻撃1予備動作2 | attack1予備動作2 |
| 16 | 310–321 | 攻撃2 （ジャンプ入り） | attack2 (jump(enter)) |
| 17 | 321–324 | 攻撃2予備動作1 （ジャンプ中） | attack2予備動作1 (jump中) |
| 18 | 324–335 | 攻撃2予備動作2 （着地） | attack2予備動作2 (landing) |
| 19 | 85–103 | 攻撃3 （投げ） | attack3 (throw) |
| 20 | 85–103 | 攻撃3予備動作1 | attack3予備動作1 |
| 21 | 85–103 | 攻撃3予備動作2 | attack3予備動作2 |
| 22 | 380–400 | 歩き | walk |

### 148 — Crabby Hermit (Enhanced) `e152`

*Motions: e152a.chr @ data.dat 0x1fcc1000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 30–42 | 立ち〈ダミー） | idle〈(dummy)) |
| 1 | 30–42 | 歩き | walk |
| 2 | 70–80 | バックステップ | back step |
| 3 | 90–100 | 右ステップ | right step |
| 4 | 110–120 | 左ステップ | left step |
| 5 | 130–136 | ガード入り | guard (enter) |
| 6 | 136–142 | ガードループ | guard (loop) |
| 7 | 142–146 | ガード戻り | guard (return) |
| 8 | 150–170 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–190 | 死亡 | death |
| 12 | 190 | 死亡ループ | death loop |
| 13 | 200–215 | 攻撃1(はさみ） | attack1(はさみ) |
| 14 | 200–215 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 200–215 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 220–245 | 攻撃２（口泡） | attack２(口泡) |
| 17 | 220–245 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 220–245 | 攻撃２予備動作２ | attack２予備動作２ |
| 19 | 255–275 | 攻撃２（棘） | attack２(棘) |
| 20 | 255–275 | 攻撃２予備動作１ | attack２予備動作１ |
| 21 | 255–275 | 攻撃２予備動作２ | attack２予備動作２ |

### 149 — Cursed Rose (Enhanced) `e153`

*Motions: e153a.chr @ data.dat 0x1fd1a000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 35–55 | ダメージ | damage |
| 2 | 60–80 | 死亡 | death |
| 3 | 80 | 死亡ループ | death loop |
| 4 | 85–120 | 攻撃葉 | attack葉 |
| 5 | 125–160 | 攻撃液前 | attack液前 |
| 6 | 165–200 | 攻撃液上 | attack液上 |

### 150 — Pirate's Chariot (Enhanced) `e154`

*Motions: e154a.chr @ data.dat 0x1fd72800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | 後退 | 後退 |
| 3 | 90–110 | 右旋回 | 右turn |
| 4 | 120–140 | 左旋回 | 左turn |
| 5 | 10–20 | ダミー | (dummy) |
| 6 | 10–20 | ダミー | (dummy) |
| 7 | 10–20 | ダミー | (dummy) |
| 8 | 150–160 | ダメージ | damage |
| 9 | 10–20 | ダミー | (dummy) |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 170–185 | 死亡 | death |
| 12 | 185 | 死亡ループ | death loop |
| 13 | 190–210 | 攻撃1 | attack1 |
| 14 | 190–210 | ダミー | (dummy) |
| 15 | 190–210 | ダミー | (dummy) |
| 16 | 250–280 | 攻撃2予備動作 | attack2予備動作 |
| 17 | 250–280 | 攻撃 | attack |
| 18 | 250–280 | 戻り | (return) |

### 151 — Space Gyon (Enhanced) `e155`

*Motions: e155a.chr @ data.dat 0x1fdb3800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | バタアシ | バタアシ |
| 2 | 60–80 | ﾊﾞｯｸｽﾃｯﾌﾟ | back step |
| 3 | 90–110 | 右ｽﾃｯﾌﾟ | right step |
| 4 | 120–140 | 左ｽﾃｯﾌﾟ | left step |
| 5 | 150–160 | ｶﾞｰﾄﾞ | guard |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 190–200 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 210–225 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 235–260 | ﾀﾞﾒｰｼﾞ２ | damage２ |
| 10 | 270–290 | ﾀﾞﾒｰｼﾞ戻り | damage(return) |
| 11 | 300–325 | 死亡 | death |
| 12 | 325 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 335–355 | 攻撃１ | attack１ |
| 14 | 365–385 | 攻撃２ | attack２ |
| 15 | 390–410 | 攻撃3 | attack3 |

### 152 — Mimic (Demon Shaft) (Enhanced x2) `e109`

*Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | まちかまえ | まちstance |

### 153 — King Mimic (Demon Shaft) (Enhanced x2) `e110`

*Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 154 — Gemron (Holy) `e115`

*Motions: e115a.chr @ data.dat 0x1e9c8000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–35 | 歩き | walk |
| 2 | 40–50 | バックステップ | back step |
| 3 | 55–65 | 右ステップ | right step |
| 4 | 70–80 | 左ステップ | left step |
| 5 | 155–165 | ｶﾞｰﾄﾞ入り | guard(enter) |
| 6 | 170–180 | ｶﾞｰﾄﾞﾙｰﾌﾟ | guardﾙｰﾌﾟ |
| 7 | 185–195 | ｶﾞｰﾄﾞ戻り | guard (return) |
| 8 | 85–100 | ﾀﾞﾒｰｼﾞ | damage |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 105–125 | 死亡 | death |
| 12 | 125 | 死亡ﾙｰﾌﾟ | deathﾙｰﾌﾟ |
| 13 | 130–150 | 攻撃１ | attack１ |
| 14 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 15 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 16 | 130–150 | 攻撃２ | attack２ |
| 17 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 130–150 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |

### 155 — Gacious (Enhanced) `e117`

*Motions: e117a.chr @ data.dat 0x1e2d6800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 295–305 | バックステップ | back step |
| 3 | 230–250 | 右ステップ | right step |
| 4 | 255–275 | 左ステップ | left step |
| 5 | 100–104 | ガード入り | guard (enter) |
| 6 | 105–115 | ガードループ | guard (loop) |
| 7 | 116–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–165 | ダメージ2 | damage2 |
| 10 | 165–185 | 起き上がり | get up |
| 11 | 205–225 | 死亡 | death |
| 12 | 225 | 死亡ループ | death loop |
| 13 | 50–64 | 攻撃1 | attack1 |
| 14 | 64–90 | 攻撃11 | attack11 |
| 15 | 50–90 | ダミー | (dummy) |
| 16 | 310–330 | 攻撃2 | attack2 |
| 17 | 330–360 | 攻撃2 | attack2 |
| 18 | 310–360 | ダミー | (dummy) |

### 156 — Evil Bat (Enhanced) `e158`

*Motions: e158a.chr @ data.dat 0x1ff1e800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–30 | 立ち | idle |
| 1 | 40–60 | 歩き | walk |
| 2 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 3 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 4 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 5 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 6 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 7 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 8 | 70–80 | ﾀﾞﾒｰｼﾞ１ | damage１ |
| 9 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 10 | 40–60 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 11 | 90–100 | 死亡始まり | death始まり |
| 12 | 100–105 | 落下ﾙｰﾌﾟ | 落下ﾙｰﾌﾟ |
| 13 | 105–115 | 死亡 | death |
| 14 | 115 | 死亡停止 | death停止 |
| 15 | 120–145 | 攻撃１ | attack１ |
| 16 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 17 | 120–145 | ﾀﾞﾐｰ | ﾀﾞﾐｰ |
| 18 | 150–171 | 攻撃2予備動作 | attack2予備動作 |
| 19 | 171–175 | 攻撃 | attack |
| 20 | 175–200 | 攻撃2予備動作 | attack2予備動作 |

### 157 — Crescent Baron (Enhanced) `e159`

*Motions: e159a.chr @ data.dat 0x1ff5a000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | バック | バック |
| 3 | 90–110 | 右 | 右 |
| 4 | 120–140 | 左 | 左 |
| 5 | 150–155 | ガード（入り） | guard((enter)) |
| 6 | 155–165 | ガードループ | guard (loop) |
| 7 | 165–170 | ガード戻り | guard (return) |
| 8 | 180–190 | ダメージ1 | damage1 |
| 9 | 200–220 | ダメージ2 | damage2 |
| 10 | 10–20 | ダミー | (dummy) |
| 11 | 230–250 | 死亡 | death |
| 12 | 250 | 死亡ループ | death loop |
| 13 | 260–280 | 攻撃1 | attack1 |
| 14 | 260–280 | 攻撃1 | attack1 |
| 15 | 260–280 | 攻撃1 | attack1 |
| 16 | 290–310 | 攻撃2 | attack2 |
| 17 | 290–310 | 攻撃2 | attack2 |
| 18 | 290–310 | 攻撃2 | attack2 |
| 19 | 320–340 | 攻撃3 | attack3 |

### 158 — Statue Dog (Enhanced) `e160`

*Motions: e160a.chr @ data.dat 0x1ffcf800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 5–15 | 立ち | idle |
| 1 | 20–40 | 歩き | walk |
| 2 | 45–55 | バックステップ | back step |
| 3 | 60–70 | 右ステップ | right step |
| 4 | 75–85 | 左ステップ | left step |
| 5 | 90–100 | ガード入り | guard (enter) |
| 6 | 100 | ガードループ | guard (loop) |
| 7 | 105–120 | ガード戻り | guard (return) |
| 8 | 125–135 | ダメージ1 | damage1 |
| 9 | 140–160 | 死亡 | death |
| 10 | 160 | 死亡L | deathL |
| 11 | 165–180 | 攻撃1 | attack1 |

### 159 — Joker (Enhanced) `e161`

*Motions: e161a.chr @ data.dat 0x20023800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–70 | バックステップ | back step |
| 3 | 80–90 | 右ステップ | right step |
| 4 | 100–110 | 左ステップ | left step |
| 5 | 120–130 | ガード入り | guard (enter) |
| 6 | 130–140 | ガードループ | guard (loop) |
| 7 | 140–150 | ガード戻り | guard (return) |
| 8 | 160–175 | ダメージ１ | damage１ |
| 9 | 185–200 | ダメージ２ | damage２ |
| 10 | 210–225 | 起き上がり | get up |
| 11 | 235–255 | 死亡 | death |
| 12 | 255 | 死亡ループ | death loop |
| 13 | 270–305 | 攻撃1(裂く286/hit） | attack1(裂く286/hit) |
| 14 | 270–305 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 270–305 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 270–305 | 攻撃２ | attack２ |
| 17 | 270–305 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 270–305 | 攻撃２予備動作２ | attack２予備動作２ |

### 160 — Lich (Enhanced) `e162`

*Motions: e162a.chr @ data.dat 0x200ce800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 60–80 | バックステップ | back step |
| 3 | 120–140 | 右ステップ | right step |
| 4 | 90–110 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 240–260 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 270–290 | 死亡 | death |
| 12 | 290 | 死亡 ループ | death (loop) |
| 13 | 150–190 | 攻撃1 | attack1 |
| 14 | 150–190 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 150–190 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 200–230 | 攻撃２ | attack２ |
| 17 | 200–230 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 200–215 | ワープ開始 | ワープ開始 |
| 19 | 215 | ワープ中（要らない？） | ワープ中(要らない？) |
| 20 | 215–230 | ワープ終了 | ワープ終了 |

### 161 — Titan (Enhanced) `e163`

*Motions: e163a.chr @ data.dat 0x2016c000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–70 | 歩き | walk |
| 2 | 10–20 | バックステップ | back step |
| 3 | 10–20 | 右ステップ | right step |
| 4 | 10–20 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–210 | 死亡 | death |
| 12 | 80–125 | 攻撃1(パンチ） | attack1(パンチ) |
| 13 | 80–125 | 攻撃1予備動作１ | attack1予備動作１ |
| 14 | 80–125 | 攻撃1予備動作２ | attack1予備動作２ |
| 15 | 130–160 | 攻撃２（地面叩く） | attack２(地面叩く) |
| 16 | 130–160 | 攻撃２予備動作１ | attack２予備動作１ |
| 17 | 130–160 | 攻撃２予備動作２ | attack２予備動作２ |

### 162 — Living Armor (Enhanced) `e164`

*Motions: e164a.chr @ data.dat 0x20211800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 30–50 | バックステップ | back step |
| 3 | 30–50 | 右ステップ | right step |
| 4 | 30–50 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–202 | 死亡 | death |
| 12 | 202 | 死亡ループ | death loop |
| 13 | 60–80 | 攻撃1 (横） | attack1 (横) |
| 14 | 60–80 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 60–80 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 90–120 | 攻撃2 （ランス突き） | attack2 (ランス突き) |
| 17 | 90–120 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 90–120 | 攻撃２予備動作２ | attack２予備動作２ |

### 163 — Mimic (Demon Shaft) (Enhanced x3) `e109`

*Motions: e109a.chr @ data.dat 0x1e09c000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 40–50 | 立ち | idle |
| 1 | 60–70 | 移動ループ前 | move loop (fwd) |
| 2 | 80–90 | 移動ループ後 | move loop (back) |
| 3 | 100–110 | 移動ループ右 | move loop右 |
| 4 | 120–130 | 移動ループ左 | move loop左 |
| 5 | 170–175 | ガード入り | guard (enter) |
| 6 | 175–185 | ガードループ | guard (loop) |
| 7 | 185–190 | ガード戻り | guard (return) |
| 8 | 200–207 | ダメージ | damage |
| 9 | 40–50 | ダミー | (dummy) |
| 10 | 40–50 | ダミー | (dummy) |
| 11 | 220–235 | 死亡 | death |
| 12 | 140–160 | 攻撃1 | attack1 |
| 13 | 140–160 | ダミー | (dummy) |
| 14 | 140–160 | ダミー | (dummy) |
| 15 | 140–160 | ダミー | (dummy) |
| 16 | 140–160 | ダミー | (dummy) |
| 17 | 140–160 | ダミー | (dummy) |
| 18 | 10–28 | 出現 | appear |
| 19 | 10 | まちかまえ | まちstance |

### 164 — King Mimic (Demon Shaft) (Enhanced x3) `e110`

*Motions: e110a.chr @ data.dat 0x1e0e5000  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 35–45 | 立ち | idle |
| 1 | 55–65 | 移動ループ前 | move loop (fwd) |
| 2 | 75–85 | 移動ループ後 | move loop (back) |
| 3 | 95–105 | 移動ループ右 | move loop右 |
| 4 | 115–125 | 移動ループ左 | move loop左 |
| 5 | 35–45 | ダミー | (dummy) |
| 6 | 35–45 | ダミー | (dummy) |
| 7 | 35–45 | ダミー | (dummy) |
| 8 | 35–45 | ダミー | (dummy) |
| 9 | 35–45 | ダミー | (dummy) |
| 10 | 35–45 | ダミー | (dummy) |
| 11 | 200–220 | 死亡 | death |
| 12 | 135–152 | 攻撃1 | attack1 |
| 13 | 135–152 | ダミー | (dummy) |
| 14 | 135–152 | ダミー | (dummy) |
| 15 | 160–174 | 攻撃2 | attack2 |
| 16 | 160–174 | ダミー | (dummy) |
| 17 | 160–174 | ダミー | (dummy) |
| 18 | 180–194 | 攻撃3 | attack3 |
| 19 | 180–194 | ダミー | (dummy) |
| 20 | 180–194 | ダミー | (dummy) |
| 21 | 10–27 | 出現 | appear |
| 22 | 10 | 待ち構え | 待ちstance |

### 165 — Black Knight `c21a`

*Motions: c21a.chr info.cfg @ data.dat 0x1e3d5800. (c22a is the same list + motion 27; see below.)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 25–45 | 歩き | walk |
| 2 | 50–70 | 走り | run |
| 3 | 75–95 | バックステップ | back step |
| 4 | 100–120 | 右ステップ | right step |
| 5 | 125–145 | 左ステップ | left step |
| 6 | 150–160 | ダメージ１ | damage 1 |
| 7 | 265–285 | 死亡 | death ← collapse |
| 8 | 285 | 死亡ループ | death loop |
| 9 | 25–45 | 右歩き | right walk |
| 10 | 25–45 | 左歩き | left walk |
| 11 | 165–185 | 攻撃１（突進） | attack 1 (charge) |
| 12 | 190–225 | 攻撃２（円月輪） | attack 2 (chakram) |
| 13 | 230–260 | 攻撃３（カマイタチ） | attack 3 (wind-slash) |
| 14 | 290–310 | 死亡後のつなぎ | post-death link |
| 15 | 315–325 | 立ち | idle (form 2) |
| 16 | 330–350 | 歩き | walk (form 2) |
| 17 | 380–400 | 右ステップ | right step (form 2) |
| 18 | 405–425 | 左ステップ | left step (form 2) |
| 19 | 355–375 | バックステップ | back step (form 2) |
| 20 | 430–440 | ダメージ１ | damage 1 (form 2) |
| 21 | 535–540 | ガード入り | guard (enter) |
| 22 | 540–550 | ガードループ | guard (loop) |
| 23 | 550–555 | ガード戻り | guard (return) |
| 24 | 510–530 | 攻撃１（二刀流斬りその1） | attack 1 (dual-slash A) |
| 25 | 445–470 | 攻撃２（円月輪） | attack 2 (chakram) |
| 26 | 560–585 | 攻撃１（二刀流斬りその2） | attack 1 (dual-slash B) |

### 166 — Black Knight Mount `c22a`

*Motions: c22a.chr info.cfg @ data.dat 0x1e565800 — identical to c21a (0–26, death=7) plus:*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 27 | 595–645 | 攻撃３（強攻撃） | attack 3 (heavy attack) |

### — — Killer Snake 

*Motions: e55a.chr @ data.dat 0x1cfde800  (idx = _SET_MOTION; 死亡 = death)*  

| Idx | Frames | Name (JP) | Meaning |
|---|---|---|---|
| 0 | 10–20 | 立ち | idle |
| 1 | 30–50 | 歩き | walk |
| 2 | 30–50 | バックステップ | back step |
| 3 | 30–50 | 右ステップ | right step |
| 4 | 30–50 | 左ステップ | left step |
| 5 | 10–20 | ガード入り | guard (enter) |
| 6 | 10–20 | ガードループ | guard (loop) |
| 7 | 10–20 | ガード戻り | guard (return) |
| 8 | 10–20 | ダメージ１ | damage１ |
| 9 | 10–20 | ダメージ２ | damage２ |
| 10 | 10–20 | 起き上がり | get up |
| 11 | 180–202 | 死亡 | death |
| 12 | 202 | 死亡ループ | death loop |
| 13 | 60–80 | 攻撃1 (横） | attack1 (横) |
| 14 | 60–80 | 攻撃1予備動作１ | attack1予備動作１ |
| 15 | 60–80 | 攻撃1予備動作２ | attack1予備動作２ |
| 16 | 90–120 | 攻撃2 （ランス突き） | attack2 (ランス突き) |
| 17 | 90–120 | 攻撃２予備動作１ | attack２予備動作１ |
| 18 | 90–120 | 攻撃２予備動作２ | attack２予備動作２ |
