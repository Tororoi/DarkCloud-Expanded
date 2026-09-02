# Town Event-Label Blacklist

Every label listed in a per-area table below is **already in use** — a system handler, a live vanilla event, or a label this mod occupies at runtime. **Do not reuse any of them** for a new custom event in that area. Free labels are in the Spare Pool. Areas are ordered by `gedit` code.

> Derived **offline** from the extracted retail ISO (`gedit/<area>/event.stb`), which never changes. Area names are decoded from each area's `gedit/<code>/mapinfo.cfg` header (Shift-JIS). Regenerate with `tools/lib/extract_scene_mesh.py` if a new fishing area is added.

## Area index

`gedit` code and MapNo are two names for the same thing: `EditInit` builds the folder as `e%02d` for MapNo < 9 (MapNo+1) and `s%02d` for MapNo > 10 (MapNo−10). The five georama regions (`e01`–`e05`) are the build-rule scenes dispatched by `CEditGround::RequestCheck` (0=Norune, 1=Matataki, 2=Queens, 3=Muska Lacka, 4=Yellow Drops).

| `gedit` | MapNo | In-game name | JP (mapinfo.cfg) |
|------|------:|--------------|------------------|
| `e01` | 0 | Norune Village | ノルン村 |
| `e02` | 1 | Matataki Village | マタタギの里 |
| `e03` | 2 | Queens | クイーンズ |
| `e04` | 3 | Muska Lacka | ムスカラッカ |
| `e05` | 4 | Factory | ファクトリー |
| `s01` | 11 | Goro's House | ゴローの家 |
| `s03` | 13 | Spirit Tree | 精霊の樹 |
| `s04` | 14 | Brownboo Village | ブラウンプーの村 |
| `s09` | 19 | East Harbor | イーストハーバー |
| `s11` | 21 | Georama edit scene | エディット |
| `s12` | 22 | Georama edit scene | エディット |
| `s13` | 23 | Yellow Drops | イエロードロップ |
| `s14` | 24 | Dark Heaven Castle (distant) | ダークヘブン城（遠距離） |
| `s16` | 26 | King Seeda's Room | シーダ王の部屋 |
| `s17` | 27 | Georama edit scene | エディット |
| `s25` | 35 | Sunken Ship — Entrance | エントランス（沈没船） |
| `s28` | 38 | Dark Heaven Castle — Entrance | エントランス（ダークヘブン城） |
| `s29` | 39 | Sunken Ship | 沈没船 |
| `s30` | 40 | Dark Heaven Castle (near) | ダークヘブン城（近距離） |
| `s31` | 41 | Robot — Interior | ロボット（内観） |
| `s32` | 42 | Georama edit scene | エディット |
| `s33` | 43 | East Harbor | イーストハーバー |
| `s36` | 46 | (unnamed scene) | — |
| `s37` | 47 | Cloud Passage | 雲の通路 |
| `s38` | 48 | Georama edit scene | エディット |
| `s39` | 49 | King Seeda's Room (final-event) | 最終イベント用（シーダ王の部屋） |

## How labels work

Each town's `event.stb` holds a table of `{labelId, codeOffset}` entries; `codeOffset` points at the label's 56-byte **funcdata** (entry PC, local count, arg count), and the label's code follows it. Code reaches other labels/code three ways: **`_NEXT_EVENT` (cmd 5)** and **`_FADEOUT_TO_EVENT` (cmd 17)** dispatch a label by *id*; **`CALL_FUNC` (cmd 19)** calls a *funcdata by codeBase-relative offset* (a subroutine — args on the stack, return on the stack, may `YIELD`) and returns. A label is **in use** if it is a low-numbered system/town-event handler, is dispatched by 5/17 anywhere in the town script, is a `CALL_FUNC` target, or is occupied by this mod.

**Two sources of hijackable labels.** (1) *Native orphans* — real, fully-authored event scripts that ship in a town's vanilla `event.stb` but that nothing ever dispatches (cut content). The mod overwrites their bytecode and renumbers the existing table slot. Each town was authored separately, so their count and size differ per area, and in several towns they are too few/small to hold the full minigame. (2) *ISO-baked labels* — for the three custom-fishing towns only (`e03`, `s13`, `s04`), `IsoPatcher.ExtendStb` **grows the label table** during ISO patching: it appends **exactly four** labels carrying the mod's FINAL ids — menu `9600`, ENTER `400`, QUIT `133`, BAIT `134` (plus a size-terminator) — past the original code, each sized to hold its one script in a single label: `0x800 / 0xA00 / 0x500 / 0x300` (measured `Need()` + margin). The three towns have no native `133/134/400/9600`, so these ids are collision-free. The installer **claims each by id** (`ClaimLabel`/`FindLabelById`) and writes straight in — no renumber, and nothing spans two labels. Each carries a correct funcdata entry-PC (`codeOff + 0x38 − codeBase` — a zero there would run the wrong PC and hang the town). This is what removed the old "no room for the quit label" failure. The native-orphan pool is now only a **fallback** for an unpatched disc (or a spot added to a town with no baked labels), where the installer renames orphans the old way.

### ID conventions

| Range | Meaning |
|-------|---------|
| `1`–`199` | Engine/system handlers. `128` = area boot, `150` = common handler. |
| `200`–`260` | Town event slots — NPC / building-door / quest events. |
| `256` | Town master / init script; also hosts vanilla fishing. See below. |
| `300` | A live town sub-event in most areas. In use everywhere. |
| `301`–`310` | 300-block sub-events. `301`–`307` and `310` are dispatched by **no** town — native-orphan fallback pool. |
| `400`,`133`,`134`,`9600` | The mod's four fishing labels, ISO-baked with these final ids into `e03`/`s13`/`s04` (see above): `400`=ENTER (named by a type-3 event point), `133`=QUIT + `134`=BAIT (engine-requested by number), `9600`=menu subroutine (`CALL_FUNC` target, never dispatched by id). |

### Label `256` in detail

`256` (= `0x100`) is the engine's **default/fallback event label** (`DefaultFishingLabel`) and, in every town, the **master / init script** (villager & scene setup) — a multi-entry state machine, so it is the largest label in most towns. Towns with retail fishing host the whole minigame *inside* 256: the fishing sign is a **type-3 event point** whose label is 256, and 256's code runs `_LOAD_FISHING_DATA` / `_GOTO_FISHING` plus `_INIT_FISH` / `_EXIT_FISHING` / the bait menu. So the vanilla fishing-**enter** number is not fixed — it is whatever the sign names (256 in Norune). **This mod never reuses 256** (overwriting it black-screens the town); it uses a fresh enter label `400`.

## Labels this mod adds (custom fishing)

A custom fishing spot installs **four** labels. In the three ISO-patched towns each is baked with its final id and claimed directly; on an unpatched disc they fall back to renamed native orphans:

| Label | Role | Source |
|------:|------|--------|
| `9600` | Shared **menu-select** subroutine — `menu(msgId, count) → line`. Both the entry and quit menus `CALL_FUNC` it, exactly as vanilla's `0x4bd4` is called from Norune's label 11 and 133. | ISO-baked label `9600` (fallback: a renamed orphan). |
| `400` | Fishing **ENTER** — a fresh type-3 event point points at it. | ISO-baked label `400` (fallback: a renamed orphan). |
| `133` | Fishing **QUIT** — engine requests this number. | ISO-baked label `133` (fallback: a renamed orphan). |
| `134` | Fishing **BAIT** menu — engine-requested number. | ISO-baked label `134` (fallback: a renamed orphan). |

The menu subroutine is parked in its own label so the entry/quit scripts share one implementation and stay smaller — the same reason vanilla factors it out. Its id (`9600`) is never dispatched by number; it is only reached via `CALL_FUNC`.

*Retired* = a spare whose code an overwriting script spanned into; its id is set to a dead value (`9000+`) so the engine can never dispatch into the middle of the mod's bytecode. `133`/`134` are collision-free (no fishing town has native 133/134).

## Spare label pool (free for custom events)

Every area's 300-block labels dispatched by **no** town. `†` marks a label that is the **last** in its town's table — free, but its byte budget is unbounded offline, so size it by hand.

| Area | `gedit` | MapNo | Spare pool | Consumed by mod fishing | Still free |
|------|---------|------:|------------|-------------------------|-----------|
| Norune Village | `e01` | 0 | **none** | — | **none** |
| Matataki Village | `e02` | 1 | 301 | — | 301 |
| Queens | `e03` | 2 | 301–305 (native fallback) **+ baked 9600/400/133/134** | baked 9600/400/133/134 | 301–305 |
| Muska Lacka | `e04` | 3 | 301, 302, 303 | — | 301, 302, 303 |
| Factory | `e05` | 4 | 301, 302, 303 | — | 301, 302, 303 |
| Goro's House | `s01` | 11 | 301 | — | 301 |
| Spirit Tree | `s03` | 13 | **none** | — | **none** |
| Brownboo Village | `s04` | 14 | 301–307 (native fallback) **+ baked 9600/400/133/134** | baked 9600/400/133/134 | 301–307 |
| East Harbor | `s09` | 19 | 301, 302, 303, 304, 305 | — | 301, 302, 303, 304, 305 |
| Georama edit scene | `s11` | 21 | **none** | — | **none** |
| Georama edit scene | `s12` | 22 | **none** | — | **none** |
| Yellow Drops | `s13` | 23 | 301–305, 310 (native fallback) **+ baked 9600/400/133/134** | baked 9600/400/133/134 | 301–305, 310 |
| Dark Heaven Castle (distant) | `s14` | 24 | **none** | — | **none** |
| King Seeda's Room | `s16` | 26 | 301† | — | 301† |
| Georama edit scene | `s17` | 27 | **none** | — | **none** |
| Sunken Ship — Entrance | `s25` | 35 | **none** | — | **none** |
| Dark Heaven Castle — Entrance | `s28` | 38 | 301, 302 | — | 301, 302 |
| Sunken Ship | `s29` | 39 | 301 | — | 301 |
| Dark Heaven Castle (near) | `s30` | 40 | 301, 302 | — | 301, 302 |
| Robot — Interior | `s31` | 41 | **none** | — | **none** |
| Georama edit scene | `s32` | 42 | 301, 302, 303† | — | 301, 302, 303† |
| East Harbor | `s33` | 43 | **none** | — | **none** |
| (unnamed scene) | `s36` | 46 | 301† | — | 301† |
| Cloud Passage | `s37` | 47 | 301† | — | 301† |
| Georama edit scene | `s38` | 48 | **none** | — | **none** |
| King Seeda's Room (final-event) | `s39` | 49 | **none** | — | **none** |

## Per-area blacklist (labels in use — do not reuse)

`obs:` tags are a heuristic read of the label's bytecode — best-effort, for readability only.


### Norune Village — `gedit/e01` (MapNo 0)

*ノルン村 · georama-edit scene*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 1152 | vanilla | Engine/system handler. _(obs: map warp, opens world map)_ |
| 11 | 1752 | vanilla | Engine/system handler. _(obs: fish-ranking board, opens world map, party/ally change)_ |
| 102 | 10152 | vanilla | Engine/system handler. |
| 128 | 3348 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 130 | 4512 | vanilla | Engine/system handler. |
| 131 | 992 | vanilla | Engine/system handler. |
| 132 | 576 | vanilla | Engine/system handler. |
| 133 | 1092 | vanilla | Engine/system handler. _(obs: item menu)_ |
| 134 | 1664 | vanilla | Engine/system handler. _(obs: enter-building door)_ |
| 150 | 884 | vanilla | Common system handler. |
| 200 | 1068 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 201 | 1036 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 202 | 1052 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 203 | 1068 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 204 | 1084 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 205 | 1100 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 206 | 1116 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 207 | 1656 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: item menu)_ |
| 208 | — | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 256 | 28448 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |
| 300 | 27168 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Matataki Village — `gedit/e02` (MapNo 1)

*マタタギの里 · georama-edit scene*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 904 | vanilla | Engine/system handler. _(obs: map warp, opens world map)_ |
| 2 | 496 | vanilla | Engine/system handler. _(obs: item menu, map warp)_ |
| 3 | 472 | vanilla | Engine/system handler. _(obs: map warp)_ |
| 4 | 616 | vanilla | Engine/system handler. _(obs: item menu, map warp, opens world map)_ |
| 11 | 468 | vanilla | Engine/system handler. _(obs: opens world map)_ |
| 12 | 2160 | vanilla | Engine/system handler. _(obs: fish-ranking board, opens world map, party/ally change)_ |
| 13 | 552 | vanilla | Engine/system handler. _(obs: opens world map)_ |
| 14 | 432 | vanilla | Engine/system handler. |
| 128 | 2528 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 131 | 812 | vanilla | Engine/system handler. _(obs: item menu, map warp)_ |
| 132 | 13288 | vanilla | Engine/system handler. |
| 133 | 35824 | vanilla | Engine/system handler. |
| 134 | 5748 | vanilla | Engine/system handler. |
| 150 | 1172 | vanilla | Common system handler. |
| 200 | 708 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 201 | 724 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 202 | 740 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 203 | 992 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 204 | 756 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 205 | 820 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 206 | 836 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 207 | 864 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 214 | — | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 256 | 6968 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |


### Queens — `gedit/e03` (MapNo 2)

*クイーンズ · georama-edit scene · custom fishing spot*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 1212 | vanilla | Engine/system handler. _(obs: map warp, opens world map)_ |
| 2 | 804 | vanilla | Engine/system handler. _(obs: item menu, map warp)_ |
| 3 | 2220 | vanilla | Engine/system handler. _(obs: item menu, map warp, reloads villagers)_ |
| 11 | 2096 | vanilla | Engine/system handler. _(obs: enter-building door)_ |
| 12 | — | vanilla | Engine/system handler. |
| 128 | 1728 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 131 | 676 | vanilla | Engine/system handler. |
| 132 | 672 | vanilla | Engine/system handler. |
| 150 | 612 | vanilla | Common system handler. |
| 200 | 772 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 201 | 808 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 202 | 772 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 203 | 740 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 204 | 864 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 205 | 772 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 206 | 788 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 207 | 804 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 208 | 924 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door, item menu, map warp)_ |
| 209 | 820 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 256 | 58420 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |
| 300 | 636 | vanilla | Town sub-event, dispatched locally via `_FADEOUT_TO_EVENT`. |
| 9600 | — | mod | Custom fishing shared **menu** subroutine — `CALL_FUNC` target for the entry/quit menus. |
| 400 | — | mod | Custom fishing **ENTER**. |
| 133 | — | mod | Custom fishing **QUIT**. |
| 134 | — | mod | Custom fishing **BAIT** menu. |

> Mod runtime consumption: four ISO-baked labels — `9600` (menu), `400` (ENTER), `133` (QUIT), `134` (BAIT) — each claimed by id and written straight in (no renumber, no spanning). The native pool (301–305) is untouched here; it is only the fallback for an unpatched disc.


### Muska Lacka — `gedit/e04` (MapNo 3)

*ムスカラッカ · georama-edit scene*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 844 | vanilla | Engine/system handler. _(obs: map warp, opens world map)_ |
| 2 | 1624 | vanilla | Engine/system handler. _(obs: enter-building door, item menu, map warp, reloads villagers)_ |
| 11 | 780 | vanilla | Engine/system handler. |
| 12 | 1944 | vanilla | Engine/system handler. _(obs: fish-ranking board, opens world map, party/ally change)_ |
| 13 | 1936 | vanilla | Engine/system handler. _(obs: enter-building door, opens world map)_ |
| 128 | 1312 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 131 | 800 | vanilla | Engine/system handler. |
| 132 | 44644 | vanilla | Engine/system handler. |
| 133 | 1284 | vanilla | Engine/system handler. _(obs: item menu)_ |
| 134 | 3448 | vanilla | Engine/system handler. |
| 150 | 724 | vanilla | Common system handler. |
| 200 | 804 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 201 | 816 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 202 | 1444 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 203 | 884 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 204 | 900 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 205 | 1108 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 206 | 940 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 207 | 932 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: enter-building door)_ |
| 208 | 872 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 209 | 872 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 210 | 872 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 211 | — | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 256 | 6272 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |
| 300 | 820 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Factory — `gedit/e05` (MapNo 4)

*ファクトリー · georama-edit scene (Yellow Drops region)*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 2 | 176 | vanilla | Engine/system handler. |
| 3 | 176 | vanilla | Engine/system handler. |
| 4 | 176 | vanilla | Engine/system handler. |
| 5 | 176 | vanilla | Engine/system handler. |
| 128 | 31308 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 131 | 728 | vanilla | Engine/system handler. _(obs: item menu, leave building, map warp)_ |
| 150 | 596 | vanilla | Common system handler. |
| 200 | 824 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 203 | 788 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 204 | 728 | vanilla | Town event slot — NPC / building-door / quest (fixed number). _(obs: item menu, leave building, map warp)_ |
| 209 | 788 | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 210 | — | vanilla | Town event slot — NPC / building-door / quest (fixed number). |
| 256 | 3628 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |


### Goro's House — `gedit/s01` (MapNo 11)

*ゴローの家 · Matataki region*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 1540 | vanilla | Engine/system handler. _(obs: map warp)_ |
| 11 | 1120 | vanilla | Engine/system handler. |
| 12 | 1432 | vanilla | Engine/system handler. _(obs: item menu)_ |
| 128 | — | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 628 | vanilla | Common system handler. |
| 256 | 73260 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |


### Spirit Tree — `gedit/s03` (MapNo 13)

*精霊の樹 · Matataki / Wise Owl Forest region*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 43024 | vanilla | Engine/system handler. |
| 11 | 1944 | vanilla | Engine/system handler. _(obs: item menu)_ |
| 128 | — | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 660 | vanilla | Common system handler. |
| 300 | 732 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Brownboo Village — `gedit/s04` (MapNo 14)

*ブラウンプーの村 · custom fishing spot*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 50440 | vanilla | Engine/system handler. |
| 11 | — | vanilla | Engine/system handler. |
| 128 | 1692 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 1772 | vanilla | Common system handler. |
| 256 | 1860 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |
| 300 | 900 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |
| 9600 | — | mod | Custom fishing shared **menu** subroutine — `CALL_FUNC` target for the entry/quit menus. |
| 400 | — | mod | Custom fishing **ENTER**. |
| 133 | — | mod | Custom fishing **QUIT**. |
| 134 | — | mod | Custom fishing **BAIT** menu. |

> Mod runtime consumption: four ISO-baked labels — `9600` (menu), `400` (ENTER), `133` (QUIT), `134` (BAIT) — each claimed by id and written straight in (no renumber, no spanning). The native pool (301–307) is untouched here; it is only the fallback for an unpatched disc.


### East Harbor — `gedit/s09` (MapNo 19)

*イーストハーバー · Queens region*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 400 | vanilla | Engine/system handler. _(obs: map warp)_ |
| 2 | 876 | vanilla | Engine/system handler. _(obs: map warp)_ |
| 11 | — | vanilla | Engine/system handler. |
| 12 | 1480 | vanilla | Engine/system handler. _(obs: fish-ranking board, opens world map, party/ally change)_ |
| 128 | 1672 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 133 | 43232 | vanilla | Engine/system handler. |
| 134 | 1288 | vanilla | Engine/system handler. _(obs: enter-building door)_ |
| 150 | 580 | vanilla | Common system handler. |
| 300 | 580 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Georama edit scene — `gedit/s11` (MapNo 21)

*エディット*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 436 | vanilla | Common system handler. |
| 300 | — | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Georama edit scene — `gedit/s12` (MapNo 22)

*エディット*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 420 | vanilla | Common system handler. |
| 300 | — | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Yellow Drops — `gedit/s13` (MapNo 23)

*イエロードロップ · custom fishing spot*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 796 | vanilla | Engine/system handler. _(obs: item menu, map warp)_ |
| 2 | 176 | vanilla | Engine/system handler. |
| 3 | 176 | vanilla | Engine/system handler. |
| 4 | 176 | vanilla | Engine/system handler. |
| 5 | 42580 | vanilla | Engine/system handler. |
| 12 | 2792 | vanilla | Engine/system handler. _(obs: item menu)_ |
| 128 | — | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 644 | vanilla | Common system handler. |
| 256 | 3196 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |
| 300 | 644 | vanilla | Town sub-event, dispatched locally via `_NEXT_EVENT`. |
| 9600 | — | mod | Custom fishing shared **menu** subroutine — `CALL_FUNC` target for the entry/quit menus. |
| 400 | — | mod | Custom fishing **ENTER**. |
| 133 | — | mod | Custom fishing **QUIT**. |
| 134 | — | mod | Custom fishing **BAIT** menu. |

> Mod runtime consumption: four ISO-baked labels — `9600` (menu), `400` (ENTER), `133` (QUIT), `134` (BAIT) — each claimed by id and written straight in (no renumber, no spanning). The native pool (301–305, 310) is untouched here; it is only the fallback for an unpatched disc.


### Dark Heaven Castle (distant) — `gedit/s14` (MapNo 24)

*ダークヘブン城（遠距離）*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 164 | vanilla | Common system handler. |
| 300 | — | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### King Seeda's Room — `gedit/s16` (MapNo 26)

*シーダ王の部屋*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 660 | vanilla | Common system handler. |
| 300 | 660 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Georama edit scene — `gedit/s17` (MapNo 27)

*エディット*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 300 | vanilla | Common system handler. |
| 300 | — | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Sunken Ship — Entrance — `gedit/s25` (MapNo 35)

*エントランス（沈没船） · Queens region*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 11 | 512 | vanilla | Engine/system handler. |
| 12 | 2828 | vanilla | Engine/system handler. _(obs: map warp)_ |
| 128 | — | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 356 | vanilla | Common system handler. |
| 300 | 380 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Dark Heaven Castle — Entrance — `gedit/s28` (MapNo 38)

*エントランス（ダークヘブン城）*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 1976 | vanilla | Engine/system handler. _(obs: item menu, map warp)_ |
| 11 | 1184 | vanilla | Engine/system handler. _(obs: enter-building door, map warp)_ |
| 128 | — | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 836 | vanilla | Common system handler. |
| 256 | 79212 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |
| 300 | 836 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Sunken Ship — `gedit/s29` (MapNo 39)

*沈没船 · Queens region*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 128 | — | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 388 | vanilla | Common system handler. |
| 300 | 388 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Dark Heaven Castle (near) — `gedit/s30` (MapNo 40)

*ダークヘブン城（近距離）*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 2780 | vanilla | Engine/system handler. _(obs: item menu, map warp)_ |
| 128 | — | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 692 | vanilla | Common system handler. |
| 300 | 776 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). _(obs: item menu)_ |


### Robot — Interior — `gedit/s31` (MapNo 41)

*ロボット（内観）*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 128 | 868 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 724 | vanilla | Common system handler. |
| 300 | — | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Georama edit scene — `gedit/s32` (MapNo 42)

*エディット*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 1 | 272 | vanilla | Engine/system handler. _(obs: map warp)_ |
| 2 | 42156 | vanilla | Engine/system handler. |
| 128 | 1508 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | 548 | vanilla | Common system handler. |
| 256 | 2500 | vanilla | Town master / init script — villager & scene setup on every entry; also hosts vanilla fishing (see above). |
| 300 | 572 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### East Harbor — `gedit/s33` (MapNo 43)

*イーストハーバー · Queens region*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 212 | vanilla | Common system handler. |
| 300 | — | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### (unnamed scene) — `gedit/s36` (MapNo 46)

*—*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 128 | 240 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 300 | 256 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). _(obs: enter-building door)_ |


### Cloud Passage — `gedit/s37` (MapNo 47)

*雲の通路*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 756 | vanilla | Common system handler. |
| 300 | 756 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### Georama edit scene — `gedit/s38` (MapNo 48)

*エディット*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 128 | 252 | vanilla | Area boot — camera reset, fade-in, player placement on entry. |
| 150 | — | vanilla | Common system handler. |
| 300 | 276 | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |


### King Seeda's Room (final-event) — `gedit/s39` (MapNo 49)

*最終イベント用（シーダ王の部屋）*

| Label | Size (B) | Origin | Nature |
|------:|--------:|--------|--------|
| 150 | 468 | vanilla | Common system handler. |
| 300 | — | vanilla | Live town sub-event (dispatched in Queens, Yellow Drops & most maps). |

