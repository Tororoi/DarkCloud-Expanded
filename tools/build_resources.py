#!/usr/bin/env python3
"""Regenerate the binaries the mod EMBEDS, so they always match the sources they are generated from:
  tools/stubs/build_ee_stubs.py   — every EE cave stub (.s -> Resources/isoPatch/stubs/<group>/*.bin)

WHY THE BUILD DOES THIS. Editing a source and forgetting its generator ships stale bytes, and the failure is SILENT: the cave
faithfully runs whatever the .bin holds and every diagnostic reports success. The csproj runs this before each build so the
embedded resources cannot drift from the code that defines them.

An absent assembler is reported and SKIPPED, never fatal — the committed .bins still build. Only a generator that actually
FAILS is fatal, because that means a source no longer assembles. (The glow palettes are no longer embedded: the patch computes
them from the user's own disc, IsoPatch/GlowDisc.cs.)
"""
import os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
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


if __name__ == "__main__":
    stubs()
