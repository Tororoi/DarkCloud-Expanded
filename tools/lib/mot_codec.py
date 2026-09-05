#!/usr/bin/env python3
"""Dark Cloud .mot skeletal-animation codec — parse a character `.chr` pack, locate a `.mot`
sub-file, decode it into tracks/keyframes, edit or splice a named motion's frame range, and
rebuild a byte-valid `.mot` (and `.chr`) the game will load.

FORMAT (reverse-engineered from SCUS_971.11; see game_data/docs/mot-format.md for the full write-up):

  A `.chr` is a PACK: a flat, chained sequence of records, NO front index. Each record is
      0x00  name        up to 0x40 bytes, NUL-terminated, '\' or '/' path seps (basename matched)
      0x40  u32 dataOff  offset of the payload within the record (always 0x50 in practice)
      0x44  u32 size     payload size in bytes
      0x48  u32 stride   whole-record size = next record starts at (this + stride)
      0x4C  u32          runtime fixup slot (garbage on disk, dead on load) — preserved verbatim
      0x50  payload      (== record + dataOff)
  Iteration walks records by +0x48 until a record whose first byte is 0 (a zero terminator record,
  typically a trailing 0x50 bytes). This mirrors GetPackFile__FPUiPcPi @0x13f720.

  A `.mot` payload (record+0x50 .. record+0x50+size) is a pure sequence of TRACKS. Each track:
      0x00  u32 w0    bone / joint id
      0x04  u32 w1
      0x08  u32 w2    channel (0 = rotation-ish, 2 = a second channel; both use the same record)
      0x0C  u32 w3    always 0x20 (keyframe stride)
      0x10  u32 count number of keyframes in this track
      0x14  u32 cont  size of THIS track in bytes (0x20 + count*0x20); 0 marks the LAST track
      0x18  u32 w6    0x74700000 (constant tag)
      0x1C  u32 w7    0x747BFE95 (constant tag)
      0x20  keyframes: `count` records, 0x20 bytes each:
                +0x00  u32   frameIdx (frame number on the .mot's single timeline; ASCENDING, SPARSE)
                +0x04  u32   0
                +0x08  u32   0
                +0x0C  u32   0
                +0x10  f32[4] value (quaternion for rotation tracks; per-channel vector otherwise)
  The loader (CreateAnimeDataEX @0x149090, fed by CommandMOTION @0x13a230) walks this list, copying
  count*0x20 bytes per track and advancing by (0x20 + count*8*4) bytes; `cont` is its stop flag.

  Named motions ("run", "walk", ...) are NOT in the `.mot`. They live in the `.chr`'s cfg text as
  `KEY <start>,<end>,<speed>` lines (CommandKEY @0x13a660 -> a 0x10-byte MOTION_INFO table). Each KEY
  addresses a [start,end] window of the SAME frame timeline. Playback advances a float frame counter
  across [start,end] and interpolates each bone's track between its bracketing keyframes. So to make
  model B play model A's motion, copy A's keyframes in A's window into B's window, per matching bone.

  Companion files: `.bbp` = the memcpy'd header/bind block (param_3[1..2] of MOTION_FILE_INFO);
  `.wgt` = a SECOND track list in the identical format but vertex-morph (keyframe +0x00 = vertex
  index, +0x10 = weight; consumed by MotionProc2 @0x148860). Neither is needed to SPLICE a skeletal
  motion: the `.mot` is self-contained per-frame rotation data. (If a transplant looks wrong on the
  fingers/cloth, the `.wgt` morph is what differs — but the body run does not depend on it.)

This module is byte-exact: parse -> rebuild of an unmodified record equals the original.
"""
import os
import struct

DATA_OFF_STD = 0x50           # record payload offset (game always writes 0x50)
KF_SIZE = 0x20                # keyframe stride
TRK_HDR = 0x20               # track header size
TAG_W6 = 0x74700000
TAG_W7 = 0x747BFE95


# ---------------------------------------------------------------------------- pack (.chr) level

class Record:
    """One sub-file inside a .chr pack. `raw` is the whole record (header + payload)."""
    __slots__ = ('name', 'data_off', 'size', 'stride', 'raw')

    def __init__(self, name, data_off, size, stride, raw):
        self.name, self.data_off, self.size, self.stride, self.raw = name, data_off, size, stride, raw

    @property
    def payload(self):
        return self.raw[self.data_off:self.data_off + self.size]

    def __repr__(self):
        return f"<Record {self.name!r} dataOff=0x{self.data_off:X} size=0x{self.size:X} stride=0x{self.stride:X}>"


class Pack:
    """A .chr: a chained list of Records plus any trailing terminator bytes."""
    __slots__ = ('records', 'trailer')

    def __init__(self, records, trailer):
        self.records, self.trailer = records, trailer

    @classmethod
    def parse(cls, blob):
        recs = []
        p = 0
        n = len(blob)
        while p < n and blob[p] != 0:
            name = blob[p:p + 0x40].split(b'\x00')[0].decode('latin1')
            data_off, size, stride = struct.unpack_from('<III', blob, p + 0x40)
            if stride == 0:
                break
            recs.append(Record(name, data_off, size, stride, blob[p:p + stride]))
            p += stride
        return cls(recs, blob[p:])

    def find(self, name):
        """Case-insensitive basename match, like GetPackFile."""
        base = name.replace('\\', '/').rsplit('/', 1)[-1].lower()
        for r in self.records:
            if r.name.lower() == base:
                return r
        return None

    def replace_payload(self, name, new_payload):
        """Swap a record's payload; fix its size/stride header words. No absolute offsets in a pack,
        so every following record is untouched (they chain by their own stride)."""
        r = self.find(name)
        if r is None:
            raise KeyError(name)
        head = bytearray(r.raw[:r.data_off])
        new_stride = r.data_off + len(new_payload)
        struct.pack_into('<II', head, 0x44, len(new_payload), new_stride)   # size @0x44, stride @0x48
        r.raw = bytes(head) + new_payload
        r.size = len(new_payload)
        r.stride = new_stride

    def rebuild(self):
        return b''.join(r.raw for r in self.records) + self.trailer


# ---------------------------------------------------------------------------- motion (.mot) level

class Keyframe:
    __slots__ = ('raw',)

    def __init__(self, raw):
        assert len(raw) == KF_SIZE
        self.raw = bytearray(raw)

    @property
    def frame(self):
        return struct.unpack_from('<I', self.raw, 0)[0]

    @frame.setter
    def frame(self, v):
        struct.pack_into('<I', self.raw, 0, v)

    @property
    def value(self):
        return struct.unpack_from('<4f', self.raw, 0x10)

    def copy(self):
        return Keyframe(bytes(self.raw))

    def __repr__(self):
        v = self.value
        return f"<KF f={self.frame} ({v[0]:.4f},{v[1]:.4f},{v[2]:.4f},{v[3]:.4f})>"


class Track:
    __slots__ = ('w0', 'w1', 'w2', 'w3', 'w6', 'w7', 'keyframes')

    def __init__(self, w0, w1, w2, w3, w6, w7, keyframes):
        self.w0, self.w1, self.w2, self.w3 = w0, w1, w2, w3
        self.w6, self.w7 = w6, w7
        self.keyframes = keyframes

    @property
    def bone(self):
        return self.w0

    @property
    def chan(self):
        return self.w2

    @property
    def key(self):
        """Identity used to match a bone/channel between two motions."""
        return (self.w0, self.w2)

    def frames_in(self, lo, hi):
        return [kf for kf in self.keyframes if lo <= kf.frame <= hi]

    def build(self, is_last):
        count = len(self.keyframes)
        cont = 0 if is_last else (TRK_HDR + count * KF_SIZE)
        out = bytearray(struct.pack('<8I', self.w0, self.w1, self.w2, self.w3,
                                    count, cont, self.w6, self.w7))
        for kf in self.keyframes:
            out += kf.raw
        return bytes(out)

    def __repr__(self):
        f = self.keyframes
        rng = f"{f[0].frame}..{f[-1].frame}" if f else "-"
        return f"<Track bone={self.w0} chan={self.w2} keys={len(f)} frames[{rng}]>"


class Mot:
    """Decoded .mot record: `head` = the pack-record header (0x00..data_off, incl name + size words),
    then a list of Tracks. `rebuild()` regenerates a byte-valid record."""
    __slots__ = ('head', 'data_off', 'tracks')

    def __init__(self, head, data_off, tracks):
        self.head, self.data_off, self.tracks = head, data_off, tracks

    @classmethod
    def from_record(cls, rec):
        blob = rec.raw
        data_off = rec.data_off
        head = bytes(blob[:data_off])
        p = data_off
        end = data_off + rec.size
        tracks = []
        while p < end:
            w0, w1, w2, w3, count, cont, w6, w7 = struct.unpack_from('<8I', blob, p)
            ko = p + TRK_HDR
            kfs = [Keyframe(blob[ko + i * KF_SIZE:ko + (i + 1) * KF_SIZE]) for i in range(count)]
            tracks.append(Track(w0, w1, w2, w3, w6, w7, kfs))
            p = ko + count * KF_SIZE
            if cont == 0:
                break
        return cls(head, data_off, tracks)

    @classmethod
    def from_pack(cls, pack, mot_name):
        rec = pack.find(mot_name)
        if rec is None:
            raise KeyError(mot_name)
        return cls.from_record(rec)

    def track_by(self, bone, chan):
        for t in self.tracks:
            if t.w0 == bone and t.w2 == chan:
                return t
        return None

    def build_payload(self):
        out = bytearray()
        for i, t in enumerate(self.tracks):
            out += t.build(i == len(self.tracks) - 1)
        return bytes(out)

    def rebuild(self):
        """Regenerate the full pack record (header + payload), fixing size/stride words."""
        payload = self.build_payload()
        head = bytearray(self.head)
        struct.pack_into('<II', head, 0x44, len(payload), self.data_off + len(payload))
        return bytes(head) + payload


# ---------------------------------------------------------------------------- splice

def splice_motion(dest, src, src_lo, src_hi, dest_lo, dest_hi, clear_unmatched=False):
    """Transplant by RAW bone id (w0,w2). *** Only correct when src and dest share an IDENTICAL .mds
    frame table (same joints in the same order), because `w0` is a positional node index, not a stable
    joint id. *** For different models of a character (the real transplant case) use
    `splice_motion_by_joint`, which remaps `w0` through joint names. This by-id path is kept for the
    same-rig case and for the round-trip test.

    For every bone/channel present in BOTH motions, `dest`'s keyframes whose frame is in
    [dest_lo,dest_hi] are removed and replaced by `src`'s keyframes from [src_lo,src_hi], shifted by
    delta = dest_lo - src_lo. Keyframes outside the window (other motions) are preserved; the merged
    list stays frame-ascending.

    CONSTRAINT: designed for EQUAL-LENGTH windows ((src_hi-src_lo)==(dest_hi-dest_lo)), the real case
    (both Ungaga runs are frames 60-80). With unequal lengths, source frames are shifted by delta and
    any that land outside [dest_lo,dest_hi] are dropped — which can leave the tail of the window
    without keys; resample first if you need that. Bones only in `src` (extra battle-only joints) are
    ignored; bones only in `dest` keep their originals unless `clear_unmatched` deletes their window.

    Returns a report dict.
    """
    if (src_hi - src_lo) != (dest_hi - dest_lo):
        # allowed, but warn via report
        pass
    delta = dest_lo - src_lo
    src_index = {t.key: t for t in src.tracks}
    matched, unmatched, src_only = [], [], set(src_index)
    for dt in dest.tracks:
        st = src_index.get(dt.key)
        kept = [kf for kf in dt.keyframes if not (dest_lo <= kf.frame <= dest_hi)]
        if st is None:
            if clear_unmatched:
                dt.keyframes = sorted(kept, key=lambda k: k.frame)
            unmatched.append(dt.key)
            continue
        src_only.discard(dt.key)
        new = []
        for kf in st.frames_in(src_lo, src_hi):
            c = kf.copy()
            c.frame = kf.frame + delta
            if dest_lo <= c.frame <= dest_hi:
                new.append(c)
        dt.keyframes = sorted(kept + new, key=lambda k: k.frame)
        matched.append((dt.key, len(new)))
    return {
        'delta': delta,
        'matched': matched,
        'dest_only_bones': unmatched,
        'src_only_bones': sorted(src_only),
        'equal_length': (src_hi - src_lo) == (dest_hi - dest_lo),
    }


# ------------------------------------------------------------------- joint identity (.mds) + remap
#
# CORRECTNESS: a track's `w0` is a POSITIONAL index into the model's CFrame node array
# (AnimeDataInit @0x1493a0: the joint is `frame_base + w0*0x270`). That array is the `.mds` frame
# table: header +0x08 = frame count, table starts at 0x18, one 0x70-byte record per frame, name at
# record+0x00. So `w0` means "the Nth frame of THIS model" — it is NOT a stable joint id across
# models. Two models of the same character can have identical low nodes but diverge once one inserts
# extra frames (e.g. e323_2c10a inserts `r_handa`/`kon_1` at index ~40, shifting every later joint by
# +2). Splicing by raw `w0` then lands battle motion on the wrong joints. `splice_motion_by_joint`
# remaps `w0` through joint NAMES so each source track writes onto the physically correct dest joint.

def read_mds_frames(mds_payload):
    """Return the ordered list of CFrame node names for a `.mds` payload; index == a track's `w0`.
    (header +0x08 = frame count, +0x14 = record stride (0x70), table @0x18, name at record+0x00.)"""
    count = struct.unpack_from('<I', mds_payload, 0x08)[0]
    stride = struct.unpack_from('<I', mds_payload, 0x14)[0]
    names = []
    for i in range(count):
        p = 0x18 + i * stride
        names.append(mds_payload[p:p + 0x20].split(b'\x00')[0].decode('latin1', 'replace'))
    return names


def build_joint_remap(src_frames, dst_frames):
    """src `w0` -> dst `w0` by joint NAME. Frame 0 (model root; the name often differs, e.g.
    'c10a_2' vs 'c10a') is mapped positionally 0->0. Names absent in dst map to None (dropped)."""
    dst_of = {}
    for i, nm in enumerate(dst_frames):
        dst_of.setdefault(nm, i)          # first occurrence wins (names are unique in practice)
    remap = {}
    for i, nm in enumerate(src_frames):
        remap[i] = 0 if i == 0 else dst_of.get(nm)
    return remap


def splice_motion_by_joint(dest, src, src_frames, dst_frames,
                           src_lo, src_hi, dest_lo, dest_hi):
    """CORRECT transplant: for every SOURCE track with keyframes in [src_lo,src_hi], remap its bone
    `w0` to the dest joint of the SAME NAME (same channel), and replace that dest track's
    [dest_lo,dest_hi] window with the source keys (shifted by dest_lo-src_lo). Dest tracks that
    receive nothing keep their originals, so no joint is accidentally frozen. `src_frames`/`dst_frames`
    come from `read_mds_frames` of each model's `.mds`.

    Returns a report: written (src_key, dst_w0, jointname, nkeys), dropped_no_joint, dropped_no_track.
    """
    remap = build_joint_remap(src_frames, dst_frames)
    delta = dest_lo - src_lo
    dst_by = {(t.w0, t.w2): t for t in dest.tracks}
    written, no_joint, no_track = [], [], []
    for st in src.tracks:
        win = st.frames_in(src_lo, src_hi)
        if not win:
            continue
        nm = src_frames[st.w0] if st.w0 < len(src_frames) else '?'
        dw = remap.get(st.w0)
        if dw is None:
            no_joint.append((st.w0, st.w2, nm))
            continue
        dt = dst_by.get((dw, st.w2))
        if dt is None:
            no_track.append((st.w0, st.w2, nm, dw))
            continue
        kept = [kf for kf in dt.keyframes if not (dest_lo <= kf.frame <= dest_hi)]
        new = []
        for kf in win:
            c = kf.copy()
            c.frame = kf.frame + delta
            if dest_lo <= c.frame <= dest_hi:
                new.append(c)
        dt.keyframes = sorted(kept + new, key=lambda k: k.frame)
        written.append(((st.w0, st.w2), dw, nm, len(new)))
    return {
        'delta': delta,
        'written': written,
        'remapped': [w for w in written if w[0][0] != w[1]],
        'dropped_no_joint': no_joint,
        'dropped_no_track': no_track,
    }


# ---------------------------------------------------------------------------- data.dat access

def _dc_dir():
    d = os.environ.get('DC1_DATA_DIR')
    if not d:
        raise SystemExit("set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")
    return d


def read_subfile(subpath, dc_dir=None):
    """Read a file from data.dat by its data.hed path suffix (e.g. 'mainchara\\c10b.chr' or
    'e323_2c10a.chr'). Returns (full_hed_name, offset, bytes)."""
    dc = dc_dir or _dc_dir()
    hed = open(os.path.join(dc, 'data.hed'), 'rb').read()
    hd2 = open(os.path.join(dc, 'data.hd2'), 'rb').read()
    want = subpath.replace('/', '\\').lower()
    for i in range(len(hed) // 80):
        name = hed[i * 80:i * 80 + 80].split(b'\x00')[0].decode('latin1')
        if name.lower().endswith(want):
            off, size, _ = struct.unpack_from('<III', hd2, 16 + i * 32)
            with open(os.path.join(dc, 'data.dat'), 'rb') as f:
                f.seek(off)
                return name, off, f.read(size)
    raise KeyError(subpath)


def load_pack(subpath, dc_dir=None):
    name, off, blob = read_subfile(subpath, dc_dir)
    return name, Pack.parse(blob)


# ---------------------------------------------------------------------------- self-test / demo

def _selftest():
    print("=== .mot codec self-test ===")
    cases = [('dun\\mainchara\\c10b.chr', 'c10a.mot'),
             ('e323_2c10a.chr', 'e323_2c10a.mot')]
    packs = {}
    for sub, mot in cases:
        name, pack = load_pack(sub)
        packs[sub] = pack
        # (1) pack round-trip
        assert pack.rebuild() == read_subfile(sub)[2], f"pack round-trip FAILED for {sub}"
        rec = pack.find(mot)
        m = Mot.from_record(rec)
        # (2) mot record round-trip (header size/stride recomputed from tracks)
        rt = m.rebuild()
        ok = rt == rec.raw
        print(f"[{mot:16}] tracks={len(m.tracks):2}  keyframes={sum(len(t.keyframes) for t in m.tracks):5}"
              f"  frames=1..{max(kf.frame for t in m.tracks for kf in t.keyframes)}"
              f"  round-trip={'OK byte-exact' if ok else 'MISMATCH'}")
        assert ok, f"mot round-trip FAILED for {mot}"
    # (3) CORRECT transplant: c10a run (60-80) -> e323 run (60-80), remapped by joint NAME
    src_pack = packs[cases[0][0]]
    dst_pack = packs[cases[1][0]]
    src = Mot.from_record(src_pack.find('c10a.mot'))
    dst = Mot.from_record(dst_pack.find('e323_2c10a.mot'))
    src_frames = read_mds_frames(src_pack.find('c10a.mds').payload)
    dst_frames = read_mds_frames(dst_pack.find('e323_2c10a.mds').payload)
    print(f"[skeleton] c10a frames={len(src_frames)} e323 frames={len(dst_frames)}; "
          f"nodes 0-37 identical, e323 inserts extra joints at 38+ -> REMAP REQUIRED")

    # hip (root-motion node 'null84' = w0 1) BEFORE
    hip_bone = src_frames.index('null84')
    before_hip = {kf.frame: tuple(round(x, 4) for x in kf.value)
                  for kf in dst.track_by(hip_bone, 2).frames_in(60, 62)}

    before = dst.rebuild()
    rep = splice_motion_by_joint(dst, src, src_frames, dst_frames, 60, 80, 60, 80)
    spliced = dst.rebuild()
    print(f"[splice-by-joint 60-80] wrote {len(rep['written'])} tracks "
          f"({len(rep['remapped'])} needed index remap), "
          f"dropped_no_joint={len(rep['dropped_no_joint'])} dropped_no_track={len(rep['dropped_no_track'])}, "
          f"size {len(before)}->{len(spliced)} bytes")
    for (sw0, chan), dw0, nm, n in rep['remapped'][:6]:
        print(f"    remapped {nm:9} src w0={sw0} -> dst w0={dw0} ({n} keys)")

    # spliced mot must still parse and round-trip byte-exact
    rec2 = Record('e323_2c10a.mot', 0x50, len(spliced) - 0x50, len(spliced), spliced)
    m2 = Mot.from_record(rec2)
    assert m2.rebuild() == spliced, "spliced mot does not re-parse byte-exact"

    # (4) CORRECTNESS: the dest joints now hold c10a's values, on the RIGHT joints
    #   (a) hip translation (null84) carries c10a's run bob
    after_hip = {kf.frame: tuple(round(x, 4) for x in kf.value)
                 for kf in m2.track_by(hip_bone, 2).frames_in(60, 62)}
    src_hip = {kf.frame: tuple(round(x, 4) for x in kf.value)
               for kf in src.track_by(hip_bone, 2).frames_in(60, 62)}
    print(f"  hip null84 translate f60-62  DST before: {before_hip}")
    print(f"  hip null84 translate f60-62  SRC(c10a):  {src_hip}")
    print(f"  hip null84 translate f60-62  DST after:   {after_hip}")
    assert after_hip == src_hip and after_hip != before_hip, "hip did not take source values"
    #   (b) a REMAPPED arm joint (jnt16_1): source value must land on dest's jnt16_1, NOT dest w0==src w0
    j16_s = src_frames.index('jnt16_1')
    j16_d = dst_frames.index('jnt16_1')
    assert j16_d != j16_s, "expected jnt16_1 to be at a different index in dest"
    sv = {kf.frame: kf.value for kf in src.track_by(j16_s, 0).frames_in(60, 80)}
    dv = {kf.frame: kf.value for kf in m2.track_by(j16_d, 0).frames_in(60, 80)}
    assert sv == dv, "remapped jnt16_1 values did not land on the correct dest joint"
    print(f"  verified jnt16_1: c10a w0={j16_s} values landed on e323 w0={j16_d} (correct joint)")
    #   (c) no shared core joint went static: every source mover's dest joint varies
    static = []
    for st in src.tracks:
        win = st.frames_in(60, 80)
        if len(set(tuple(round(x, 4) for x in k.value) for k in win)) <= 1:
            continue
        dw = build_joint_remap(src_frames, dst_frames).get(st.w0)
        dt = m2.track_by(dw, st.w2) if dw is not None else None
        if dt is not None:
            vals = set(tuple(round(x, 4) for x in k.value) for k in dt.frames_in(60, 80))
            if len(vals) <= 1:
                static.append(src_frames[st.w0])
    print(f"  core movers that went static after splice: {static if static else 'NONE'}")
    assert not static, f"joints went static: {static}"
    print("ALL CHECKS PASSED")


if __name__ == '__main__':
    _selftest()
