#!/usr/bin/env python3
"""Produce a patched COPY of the Dark Cloud ISO (never touches the original).

Applies, in-place on the copy (no rebuild / no shift / same ISO size):
  1. absorb DMMY. into DATA.DAT  -> ~63 MB free tail space for the actual patches (the fishing sign, etc.)

The boot serial is left UNCHANGED (SCUS_971.11), so PCSX2 still recognises the disc: correct Region
(NTSC-U) and Compatibility, the mod's CRC-keyed pnach still applies, and saves are shared with the
original. To tell the two ISOs apart in PCSX2, the copy is written as "<name>.iso" -> that shows in the
game list's File Title / filename column (the Title column stays the proper GameDB "Dark Cloud").

  python3 tools/iso_patch/make_patched_copy.py --dryrun
  python3 tools/iso_patch/make_patched_copy.py --name "Dark Cloud - Expanded"
Requires $DC1_ISO (see .env.sample).
"""
import os, sys, shutil, argparse
import ps2iso

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.dirname(os.environ.get("DC1_ISO", "") or "."),
                    help="directory to write the patched copy into (default: next to the source ISO)")
    ap.add_argument("--name", default="Dark Cloud - Expanded",
                    help="output filename (without .iso); shows in PCSX2's File Title column")
    ap.add_argument("--dryrun", action="store_true")
    a = ap.parse_args()

    src = os.environ.get("DC1_ISO")
    if not src: raise SystemExit("Set $DC1_ISO to your Dark Cloud (USA) ISO (see .env.sample)")

    with open(src, "rb") as f:
        recs = ps2iso.parse_root(f)
    dat, dm = recs.get("DATA.DAT"), recs.get("DMMY.")
    for nm, r in (("DATA.DAT", dat), ("DMMY.", dm)):
        if r is None: raise SystemExit(f"expected {nm} in the ISO root — is this a Dark Cloud (USA) disc?")

    dummy_sectors = (dm['size'] + ps2iso.SECTOR - 1)//ps2iso.SECTOR
    print(f"source ISO : {src}")
    print(f"serial     : SCUS-97111 unchanged (keeps Region/Compat + CRC-keyed pnach)")
    print(f"absorb     : DATA.DAT {dat['size']:,} -> {dat['size']+dummy_sectors*ps2iso.SECTOR:,} "
          f"(+{dummy_sectors*ps2iso.SECTOR/1e6:.1f} MB free)")
    print(f"output     : {os.path.join(a.out, a.name + '.iso')}")
    if a.dryrun:
        print("dry run — no file written.")
        return

    out = os.path.join(a.out, a.name + ".iso")
    print(f"copying -> {out} ...")
    shutil.copyfile(src, out)
    with open(out, "r+b") as f:
        recs = ps2iso.parse_root(f)
        free_off, free_bytes = ps2iso.absorb_dummy(f, recs)
        v = ps2iso.parse_root(f)
    print(f"done. DATA.DAT {v['DATA.DAT']['size']:,} bytes (+{free_bytes/1e6:.1f} MB free @ {free_off:#x}).")
    print(f"  PCSX2 Title 'Dark Cloud' (Region NTSC-U, Compat intact); File Title '{a.name}'.")

if __name__ == "__main__":
    main()
