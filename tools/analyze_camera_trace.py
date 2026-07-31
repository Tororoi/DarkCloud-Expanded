#!/usr/bin/env python3
"""Analyze camera_trace.csv to test whether the pull-in/climb camera holds a straight vertical line
along a wall, or bows AWAY from the wall as it climbs (the "more padding at higher heights" hunch).

The native pull-in (IsoPatcher pullin.s) logs per frame, via scratch 0x2014BB00..:
    dist      eye horizontal distance from the camera reference (its orbit radius)
    h         camera height (the climb output)
    hitHoriz  horizontal distance from ref to where the collision ray STRUCK the wall (0 = ray missed)
    hitY      world height at which the ray struck the wall
    clr       hitHoriz - dist  (the eye's horizontal gap to the wall; should stay ≈ MARGIN)
    nClimbH   the climb-written height

Two independent readings:
  1. WALL CROSS-SECTION — bucket hitHoriz by hitY. A vertical wall gives constant hitHoriz across
     heights; hitHoriz rising with hitY means the wall itself slopes outward (so it genuinely recedes
     with height and the extra padding is real geometry, not a bug).
  2. EYE STRAIGHTNESS — is `clr` (eye→wall horizontal gap) constant, or does it grow with h? Growing
     clr with h = the eye bows away from the wall as it climbs (padding increases with height).

Usage:  python3 tools/analyze_camera_trace.py [path/to/camera_trace.csv]
        (defaults to the build-output copy under bin/Debug/net8.0/)
"""
import sys, os, csv, statistics

DEFAULT = os.path.join(os.path.dirname(__file__), "..", "Dark Cloud Improved Version",
                       "bin", "Debug", "net8.0", "camera_trace.csv")


def load(path):
    rows = []
    with open(path) as f:
        for r in csv.DictReader(f):
            try:
                rows.append({k: float(v) for k, v in r.items()})
            except ValueError:
                pass
    return rows


def corr(xs, ys):
    """Pearson r; None if degenerate."""
    n = len(xs)
    if n < 3:
        return None
    mx, my = sum(xs) / n, sum(ys) / n
    sx = sum((x - mx) ** 2 for x in xs) ** 0.5
    sy = sum((y - my) ** 2 for y in ys) ** 0.5
    if sx == 0 or sy == 0:
        return None
    return sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / (sx * sy)


def bucket(rows, key, val, lo, hi, step):
    out = {}
    b = lo
    while b < hi:
        pts = [r[val] for r in rows if b <= r[key] < b + step]
        if pts:
            out[(b, b + step)] = (statistics.mean(pts), len(pts))
        b += step
    return out


def stationary_segments(rows, move_tol=3.0, min_len=12):
    """Split into runs where the player (refx,refz) barely moves — i.e. wedged against one wall.
    Within such a run any change in hitHoriz is due to the camera (height/angle), not a different wall."""
    if not rows or "refx" not in rows[0]:
        return [rows]  # no ref logged (older capture) — treat whole thing as one segment
    segs, cur = [], [rows[0]]
    for r in rows[1:]:
        p = cur[-1]
        if abs(r["refx"] - p["refx"]) + abs(r["refz"] - p["refz"]) <= move_tol:
            cur.append(r)
        else:
            if len(cur) >= min_len:
                segs.append(cur)
            cur = [r]
    if len(cur) >= min_len:
        segs.append(cur)
    return segs


def report(hits, label):
    if len(hits) < 5:
        print(f"  ({label}: only {len(hits)} wall-hit frames — too few)\n")
        return
    hy = [r["hitY"] for r in hits]
    hh = [r["hitHoriz"] for r in hits]
    hgt = [r["h"] for r in hits]
    clr = [r["clr"] for r in hits]

    # 1) wall cross-section: hitHoriz vs hitY
    print("── WALL CROSS-SECTION (does the wall itself slope?) ──")
    print("   hitY range        mean hitHoriz   frames")
    for (a, b), (m, n) in sorted(bucket(hits, "hitY", "hitHoriz",
                                         min(hy), max(hy) + 1e-3, max((max(hy) - min(hy)) / 6, 1))
                                 .items()):
        print(f"   {a:6.1f}..{b:6.1f}   {m:9.2f}      {n}")
    r1 = corr(hy, hh)
    slope = (hh[-1] - hh[0]) / (hy[-1] - hy[0]) if hy[-1] != hy[0] else 0
    print(f"   corr(hitY, hitHoriz) = {r1: .2f}")
    if r1 is None or abs(r1) < 0.4:
        note = "hitHoriz ~independent of strike height → wall reads VERTICAL (padding shouldn't grow with height)"
    elif r1 > 0:
        note = "hitHoriz RISES with strike height → wall slopes outward (extra padding at height is real geometry)"
    else:
        note = ("hitHoriz FALLS with strike height → NOT a vertical wall at fixed aim. Almost always means the "
                "AIM ANGLE changed during the climb (a rotating maneuver), so this can't be read as wall shape — "
                "recapture climbing with the camera angle held fixed, or use the flat-ray diagnostic.")
    print(f"   → {note}\n")

    # 2) eye straightness: clr vs h
    print("── EYE STRAIGHTNESS (does the eye bow away from the wall as it climbs?) ──")
    print("   height range      mean clr(gap)   frames")
    for (a, b), (m, n) in sorted(bucket(hits, "h", "clr",
                                        min(hgt), max(hgt) + 1e-3, max((max(hgt) - min(hgt)) / 6, 1))
                                 .items()):
        print(f"   {a:6.1f}..{b:6.1f}   {m:9.2f}      {n}")
    r2 = corr(hgt, clr)
    print(f"   clr mean={statistics.mean(clr):.2f} stdev={statistics.pstdev(clr):.2f}  corr(h, clr) = {r2: .2f}")
    verdict = ("eye bows AWAY as it climbs (gap grows with height)" if r2 and r2 > 0.4
               else "eye holds a ~constant gap (straight vertical line)")
    print(f"   → {verdict}")


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    if not os.path.exists(path):
        sys.exit(f"no CSV at {path}")
    rows = load(path)
    hits = [r for r in rows if r.get("hitHoriz", 0) > 0.1]
    print(f"{len(rows)} frames, {len(hits)} with a wall hit\n")

    # Prefer the longest STATIONARY segment (player wedged against one wall) — that isolates a single
    # wall so hitHoriz changes are purely the camera's doing. Fall back to all frames if none is long.
    segs = stationary_segments(rows)
    seg_hits = [[r for r in s if r.get("hitHoriz", 0) > 0.1] for s in segs]
    seg_hits = [s for s in seg_hits if len(s) >= 8]
    if seg_hits:
        best = max(seg_hits, key=len)
        rx = [r["refx"] for r in best]; rz = [r["refz"] for r in best]
        print(f"Using longest stationary segment: {len(best)} wall-hit frames, "
              f"player drift ≈ {max(rx)-min(rx):.1f}×{max(rz)-min(rz):.1f} units\n")
        report(best, "stationary")
    else:
        print("No clean stationary segment (player kept moving) — analyzing ALL wall-hit frames; the wall\n"
              "cross-section will be smeared across different walls. For a clean read, stand still wedged\n"
              "against ONE wall and rotate the camera up/into it, then recapture.\n")
        report(hits, "all")


if __name__ == "__main__":
    main()
