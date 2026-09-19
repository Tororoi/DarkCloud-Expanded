#!/usr/bin/env python3
"""Regenerate the binaries the mod EMBEDS, so they always match the sources they are generated from.

Two generators, both of which used to be manual steps a person had to remember:
  tools/stubs/build_ee_stubs.py   — every EE cave stub (.s -> Resources/isoPatch/*.bin)
  build_cat_pack.glow_palettes()  — the six element glow ramps (GLOW_ELEMENTS -> catGlowPalettes.bin)

WHY THE BUILD DOES THIS. Editing a source and forgetting its generator ships stale bytes, and the failure is SILENT: the
glow palette kept its previous colours through an entire patch-and-test cycle that way, because the
cave faithfully paints whatever the tables hold and every diagnostic reports success. The csproj runs this before each
build so the embedded resources cannot drift from the code that defines them.

Absent prerequisites are reported and SKIPPED, never fatal — a checkout without the extracted disc still builds, and
build_cat_pack.refuse_if_palette_blob_stale refuses the ISO bake as the backstop. Only a generator that actually FAILS
is fatal, because that means a source no longer assembles.
"""
import os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
RES  = os.path.join(REPO, "Dark Cloud Improved Version", "Resources", "isoPatch")
TAG  = "[resources]"


def stubs():
    """Assemble every EE cave stub. build_ee_stubs writes a .bin only when the bytes actually change."""
    script = os.path.join(HERE, "stubs", "build_ee_stubs.py")
    if not os.path.exists(script):
        print(f"{TAG} no stub assembler — skipped"); return
    r = subprocess.run([sys.executable, script], capture_output=True, text=True)
    out = r.stdout + r.stderr
    if "No module named" in out:
        # No assembler on this machine. The committed .bins are still valid, so BUILD rather than block — this check
        # has to come first: a missing module also exits non-zero, which would otherwise hit the fatal path below.
        print(f"{TAG} assembler unavailable (keystone not installed) — stubs left as committed"); return
    if r.returncode != 0:
        # A stub that fails to ASSEMBLE is a real error: its .bin on disk is then older than the .s it came from.
        print(out)
        raise SystemExit(f"{TAG} stub assembly FAILED — a .s no longer assembles (see above)")
    wrote = [l.split()[1] for l in r.stdout.splitlines() if l.strip().startswith("WROTE")]
    print(f"{TAG} stubs: {'regenerated ' + ', '.join(wrote) if wrote else 'all up to date'}")


def palettes():
    """Rebuild the glow ramp tables from GLOW_ELEMENTS, writing only when they differ."""
    dc = os.environ.get("DC1_DATA_DIR") or os.path.join(REPO, "dc_extracted")
    dest = os.path.join(RES, "catGlowPalettes.bin")
    if not os.path.isdir(dc):
        print(f"{TAG} no extracted disc at {dc} — glow palettes left as committed"); return
    sys.path[:0] = [os.path.join(HERE, "iso_patch"), os.path.join(HERE, "lib"), os.path.join(HERE, "analysis")]
    try:
        import build_cat_pack as B
    except Exception as e:
        print(f"{TAG} cannot import build_cat_pack ({e}) — glow palettes left as committed"); return
    try:
        _, glow = B.mc.load_pack(B.GLOW_SRC, dc)
        blob = B.glow_palettes(B.Bank(glow.find("fire.img").payload).block("lightling"))
    except SystemExit as e:
        raise SystemExit(f"{TAG} glow palettes REFUSED: {e}")        # e.g. two elements sharing a state colour
    except Exception as e:
        print(f"{TAG} could not read the glow source ({e}) — palettes left as committed"); return
    old = open(dest, "rb").read() if os.path.exists(dest) else None
    if old == blob:
        print(f"{TAG} glow palettes: up to date")
    else:
        open(dest, "wb").write(blob)
        # ⚠ Name EVERY row, including the three weapon looks — a message that cannot name what it changed is worse than
        # none, and this listed "()" the day rows 6-8 were added because the name list stopped at the six elements.
        names = ["Fire", "Ice", "Thunder", "Wind", "Holy", "None",
                 "DivineBeastTitle", "AngelShooter", "AngelGear"]
        rows = len(blob) // 512
        if old is None:
            what = f"all {rows} rows"
        elif len(old) != len(blob):
            what = f"table resized, {len(old) // 512} -> {rows} rows"
        else:
            changed = [names[i] if i < len(names) else f"row {i}" for i in range(rows)
                       if old[i * 512:(i + 1) * 512] != blob[i * 512:(i + 1) * 512]]
            what = ", ".join(changed) if changed else "content identical, rewritten"
        print(f"{TAG} glow palettes: REGENERATED from GLOW_ROWS ({what})")


if __name__ == "__main__":
    stubs()
    palettes()
