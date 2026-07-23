#!/usr/bin/env python3
"""Phase-1 primitive: absorb the trailing DMMY. padding into DATA.DAT (see ps2iso.absorb_dummy).

Gains ~63 MB of free tail space in DATA.DAT for new/redirected archive entries — no rebuild, no shift,
same ISO size. Standalone here for the Phase-1 boot test; the full patcher lives in make_patched_copy.py.

  python3 tools/iso_patch/absorb_dummy.py --dryrun            # validate against $DC1_ISO, no write
  python3 tools/iso_patch/absorb_dummy.py  /path/to/out.iso   # write a patched COPY, then boot it
"""
import os, sys, shutil
import ps2iso

DC1_ISO = os.environ.get("DC1_ISO")
if not DC1_ISO: raise SystemExit("Set $DC1_ISO to your Dark Cloud (USA) ISO (see .env.sample)")

with open(DC1_ISO, "rb") as f:
    recs = ps2iso.parse_root(f)
    dat = recs["DATA.DAT"]
    old_sz = dat["size"]

print(f"source ISO : {DC1_ISO}")
print(f"DATA.DAT   : dir-record @ {dat['rec_off']:#x}, size {old_sz:,}")

if "--dryrun" in sys.argv:
    dm = recs["DMMY."]
    dummy_sectors = (dm["size"] + ps2iso.SECTOR - 1)//ps2iso.SECTOR
    new_sz = old_sz + dummy_sectors*ps2iso.SECTOR
    print(f"absorb ->  : new size {new_sz:,}  (+{(new_sz-old_sz)/1e6:.1f} MB free at archive offset {old_sz:#x})")
    print("dry run — no file written.")
    sys.exit(0)

out = next((a for a in sys.argv[1:] if not a.startswith("--")), None)
if not out: raise SystemExit("give an output path for the patched copy, or use --dryrun")
print(f"copying -> {out} ...")
shutil.copyfile(DC1_ISO, out)
with open(out, "r+b") as f:
    recs = ps2iso.parse_root(f)
    free_off, free_bytes = ps2iso.absorb_dummy(f, recs)
    recs2 = ps2iso.parse_root(f)
print(f"done. DATA.DAT now {recs2['DATA.DAT']['size']:,} bytes "
      f"(+{free_bytes/1e6:.1f} MB free @ offset {free_off:#x}). Boot {out} — must run identically to stock.")
