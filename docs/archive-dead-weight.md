# Dead weight in `data.dat` — expendable space for asset patches

Research conducted 2026-07-14 by cross-referencing `data.hed` / `data.hd2` against the ELF's string
table and every `.cfg` / `.ini` / `.scr` / `.txt` in the archive.

## Why this exists

Adding an asset (a model, a texture) normally means growing a file, which means the ISO's filesystem
has to be re-authored. That is avoidable: if a patched file keeps its **exact byte length**, nothing
in the ISO9660/UDF layer changes and the patch degenerates to *writing bytes at known LBAs*. No
remastering, no repack, a tiny xdelta, and near-zero chance of corrupting the image.

To do that you need somewhere to put the new bytes — a file already on the disc that nothing reads.
This is the inventory of those.

> **This still produces a patched ISO.** Size-neutral means we don't have to *rebuild* the
> filesystem; it does **not** mean the ISO is left alone. Always patch a copy and keep a pristine
> master.

## Method, and its limits

A file is flagged when its name appears **nowhere** in the ELF's strings, nor in any config or text
file in the archive.

The engine loads strictly by path, so a path that is never spelled anywhere is very likely never
loaded. But "very likely" is not "proven": paths can be **constructed at runtime** (`rmdat/rmdat%d.pak`,
`gedit/%s/scene.scn`), which is why a naive sweep flags ~3,000 files and 850 MB — almost all of them
false positives. Everything below is restricted to categories with an *independent* reason to believe
they are dead, not just an absent string.

**Two known false positives in the tables below**, listed for honesty: `check\ankfont.img` and
`noda_w\fconv.bin` show "in ELF: yes" only because a *different*, live file shares their basename
(`ankfont` as a texture name; `gedit\e01\fconv.bin`). That is a basename collision, not evidence
these copies are used.

**Verify before you overwrite.** These are strong candidates, not certainties.

---

## A. Backup copies — 29 files, 8.86 MB

The convention is obvious: a trailing `_` (or `__`) on an otherwise-normal name. 26 of the 29 have a
live twin still present in the archive, which is what makes this category convincing.

| file | size | sector | live twin? |
|---|---|---|---|
| `dun\monstor\e50a.chr_` | 1,305,136 | 235806 | yes |
| `dun\pack\main00n.pac_` | 1,242,448 | 309747 | yes |
| `dun\monstor\e74a.chr_` | 959,968 | 265933 | yes |
| `dun\monstor\e59a.chr_` | 953,872 | 265467 | yes |
| `dun\monstor\e52a.chr_` | 882,048 | 264580 | yes |
| `dun\pack\teximg.pac_` | 716,544 | 311414 | yes |
| `dun\monstor\e49a.chr_` | 661,472 | 235007 | yes |
| `dun\monstor\e51a.chr_` | 622,224 | 236760 | yes |
| `gedit\system\esys.pak_` | 503,584 | 709417 | yes |
| `commenu\a_fre\kgetoan2.img__` | 182,512 | 39935 | yes |
| `data.hd2_` | 122,176 | 48 | — (a backup of the archive's own index!) |
| `dun\effect\i_boll.chr_` | 122,160 | 198263 | yes |
| `dun\effect\f_boll_3.chr_` | 108,720 | 198209 | yes |
| `chara\iget.chr_` | 85,936 | 2280 | yes |
| `meswin\system_1.mes__` | 47,906 | 727908 | yes |
| `dun\effect\g_wave1.chr_` | 39,408 | 196627 | yes |
| `meswin\system_a_2.mes__` | 33,994 | 728813 | yes |
| `img\nowload.img_` … `img_6\nowload.img_` (7×) | 32,896 each | 713045 … 727567 | yes |
| `dun\item\main_data\skanacnd.mds_` | 12,704 | 204421 | yes |
| `gedit\e01\gdata0.edt_` | 1,048 | 315896 | yes |
| `gdata_e3.edt_` | 632 | 1 | no |
| `gedit\s88\06{Xi_[Nwuéj_` | 0 | 672009 | no — a mangled Shift-JIS name, zero bytes |

None of these 29 names appears anywhere in the ELF.

## B. `check\` — old UI mockups, 10 files, 967 KB

An abandoned menu/HUD prototype folder, including a `check\old\` sub-folder (mockups of the mockups).

**These files use an obsolete format.** `check\old\exitmenu.img` carries the magic **`IMG\0`**, whereas
every live town texture bank is **`IM2\0`** (see docs/custom-fishing-spot.md §8 for the IM2 layout).
That is a much stronger argument for this folder being dead than a missing string: it is written in a
format the shipped town-texture path does not read.

| file | size | sector |
|---|---|---|
| `check\old\preview.img` | 287,872 | 3727 |
| `check\old\window.img` | 287,872 | 3914 |
| `check\button.img` | 84,208 | 3602 |
| `check\topframe.img` | 66,688 | 3669 |
| `check\old\tx01.img` | 66,688 | 3881 |
| `check\ankfont.img` | 49,280 | 3577 |
| `check\opennext.img` | 49,280 | 3644 |
| `check\old\hp.img` | 32,096 | 3711 |
| `check\old\sphere.mds` | 25,600 | 3868 |
| **`check\old\exitmenu.img`** | **17,536** | **3702** |

## C. `noda_w\` — a developer's working directory, 14 files, 575 KB

Someone's actual work folder, shipped on the retail disc. It contains `ReadBuff.txt`, `ConvBuff.txt`,
a `fconvtest.bin`, six translation-workflow `.mes` files — and a **Windows `.lnk` shortcut** with a
Shift-JIS name (`e01の…ショートカット`, "shortcut to e01").

| file | size | sector |
|---|---|---|
| `noda_w\fconv.bin` | 80,192 | 729115 |
| `noda_w\dun00_eng.mes` | 48,040 | 728930 |
| `noda_w\dun00_eng3/4/6.mes` | 47,508 each | 728978 / 729002 / 729050 |
| `noda_w\dun00_eng5.mes` | 47,214 | 729026 |
| `noda_w\dun00_eng2.mes` | 47,196 | 728954 |
| `noda_w\editmenu.bin` | 45,858 | 729092 |
| `noda_w\e01…ショートカット.lnk` | 36,692 | 729074 |
| `noda_w\system14.bin` | 35,338 | 729194 |
| `noda_w\system.bin` | 35,316 | 729176 |
| `noda_w\fconvtest.bin` | 31,838 | 729155 |
| `noda_w\ConvBuff.txt` | 14,598 | 728922 |
| `noda_w\ReadBuff.txt` | 9,921 | 729171 |

## D. Loose junk at the archive root — 211 KB

| file | size | sector | note |
|---|---|---|---|
| `tmp.txt` | 82,580 | 7 | a temp file, shipped |
| `data.hd2_` | 122,176 | 48 | (also in table A) |
| `test4.stb` | 5,144 | 4 | |
| `test.stb` | 648 | 3 | |
| `gdata_e3.edt_` | 632 | 1 | |
| `mapeditor.ini` | 107 | 2 | the level editor's config |

`gdata0.edt` (72 B, sector 0) is **live** — `EditInit` reads it. Do not touch it.

---

## Total

Roughly **10.6 MB** of high-confidence dead weight, and the low sector numbers of the root junk
(sectors 0–48) put some of it right at the front of the archive.

## Recommended victims for the fishing-sign patch

| new asset | size | suggested host | host size | fit |
|---|---|---|---|---|
| `fishsign.img` | 17,536 | `check\old\exitmenu.img` | 17,536 | exact |
| `fishsign.mds` | 2,160 | `test4.stb` | 5,144 | comfortable |

> **The size match is arithmetic, not a coincidence, and means nothing.** Any bank holding exactly one
> 128x128 8bpp texture with a 256-colour CLUT weighs 17,536 bytes: image 16,384 + CLUT 1,024 + TIM2
> header 0x10 + picture header 0x30 = a 17,472-byte payload, plus a 0x10 bank header and one 0x30
> entry. `exitmenu.img` is an exit-menu *button graphic* (and in the older `IMG\0` format at that);
> it has no relationship to the fishing sign beyond both being single 128x128 textures. It is a good
> host because it is dead and the right size — not because it is special.

The host only has to be **at least** as large as the payload; any of the dead files above will do.

The patch is then:

1. Overwrite the host files' bytes inside `data.dat` (no length change).
2. Patch each host's `data.hd2` entry **size** field to the new payload's length (fixed-size 32-byte
   records — edited in place, `data.hd2` does not change length).
3. Point the consumer at the host path — for the model, `LoadGroundData`'s hardcoded string at
   `0x2029B010`; for the texture, a `GRD_IMG` line added to the town's `mapinfo.cfg` by overwriting
   an equal-length run of comment text (the cfg is 13 KB of text, densely commented in Japanese).

Every file keeps its exact length, so the ISO's filesystem is never touched.

## Caveats

- **Verify each victim before overwriting.** Absence of a string is strong evidence, not proof.
- Keep the pristine ISO. The patcher should take a clean image and emit a new one, refusing input
  whose hash does not match a known-good dump.
- This is USA (`SCUS_971.11`) only — `data.hd2` indices differ by region.
