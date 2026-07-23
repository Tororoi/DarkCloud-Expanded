#!/usr/bin/env python3
"""Reusable ISO9660 / DC-archive primitives for the Dark Cloud ISO-patch pipeline.

Everything here operates IN-PLACE on an already-open file handle and preserves the ISO's sector layout —
no file is ever grown or shifted. The two levers we rely on:

  * absorb_dummy()  — extend DATA.DAT's directory-record size to swallow the contiguous trailing DMMY.
    padding file (the game never reads it), yielding ~63 MB of free tail space for new archive entries.
  * rename_root_file() / replace_in_file() — same-length in-place edits (used to change the boot serial).

No side effects at import — callers open the ISO and pass the handle in.
"""
import struct

SECTOR = 2048

def _u32le(b, o): return struct.unpack("<I", b[o:o+4])[0]

def parse_root(f):
    """Return {UPPERNAME: record-dict} for every entry in the root directory.
    record = dict(rec_off, ext, size, flags, name(bytes incl ;ver), name_off, name_len, is_dir)."""
    f.seek(16*SECTOR); pvd = f.read(SECTOR)
    if not (pvd[0] == 1 and pvd[1:6] == b"CD001"):
        raise ValueError("not a 2048-byte ISO9660 image (no PVD at sector 16)")
    root_lba  = _u32le(pvd, 158)   # root dir record: extent LBA at PVD+156+2
    root_size = _u32le(pvd, 166)   #                  data length at PVD+156+10
    f.seek(root_lba*SECTOR); d = f.read(root_size)
    recs, pos = {}, 0
    while pos + 33 <= len(d):
        ln = d[pos]
        if ln == 0:                          # padding to next sector
            pos = (pos//SECTOR + 1)*SECTOR; continue
        ext, size, flags, nlen = _u32le(d, pos+2), _u32le(d, pos+10), d[pos+25], d[pos+32]
        name = d[pos+33:pos+33+nlen]
        key = name.split(b';')[0].decode('latin1').upper()
        recs[key] = dict(rec_off=root_lba*SECTOR + pos, ext=ext, size=size, flags=flags,
                         name=name, name_off=root_lba*SECTOR + pos + 33, name_len=nlen,
                         is_dir=bool(flags & 2))
        pos += ln
    return recs

def set_file_size(f, rec, new_size):
    """Overwrite a directory record's both-endian data-length field (ISO9660 stores LE then BE)."""
    f.seek(rec['rec_off'] + 10); f.write(struct.pack("<I", new_size))
    f.seek(rec['rec_off'] + 14); f.write(struct.pack(">I", new_size))

def rename_root_file(f, rec, new_full_name: bytes):
    """Rename a file in the root directory. Length-preserving only (keeps every other record in place)."""
    if len(new_full_name) != rec['name_len']:
        raise ValueError(f"rename must preserve length ({rec['name_len']} bytes), got {len(new_full_name)}")
    f.seek(rec['name_off']); f.write(new_full_name)

def read_file(f, rec) -> bytes:
    f.seek(rec['ext']*SECTOR); return f.read(rec['size'])

def replace_in_file(f, rec, old: bytes, new: bytes, expect=None):
    """In-place same-length byte replacement inside a file's data (file size unchanged)."""
    if len(old) != len(new):
        raise ValueError("replace_in_file requires equal-length old/new")
    data = read_file(f, rec)
    n = data.count(old)
    if expect is not None and n != expect:
        raise ValueError(f"expected {expect} occurrence(s) of {old!r}, found {n}")
    if n == 0:
        raise ValueError(f"{old!r} not present in {rec['name']!r}")
    f.seek(rec['ext']*SECTOR); f.write(data.replace(old, new))
    return n

# ── DC archive (DATA.HED names / DATA.HD2 index / DATA.DAT blobs) ─────────────────────────────────
def archive_find(hed: bytes, name: str):
    """Index of `name` in DATA.HED (80-byte path slots), or None. Names use backslashes on disc."""
    want = name.replace('/', '\\')
    for i in range(len(hed)//80):
        n = hed[i*80:i*80+80].split(b'\x00')[0].decode('latin1')
        if n == want or n.replace('\\', '/') == name:
            return i
    return None

# ── PAK container (GetPackFile 0x13F720): interleaved [name@0, dataOff@+0x40, size@+0x44, stride@+0x48] ─
_PAK_HDR = 0x50
_PAK_ALIGN = 0x40

def pak_terminator(pak: bytes) -> int:
    """Offset of the empty-name entry that ends the directory (where GetPackFile stops)."""
    p = 0
    while p < len(pak):
        if pak[p] == 0: return p
        _off, _size, stride = struct.unpack_from('<III', pak, p + 0x40)
        p += stride
    return len(pak)

def pak_build_entry(name: str, data: bytes) -> bytes:
    stride = (_PAK_HDR + len(data) + _PAK_ALIGN - 1) & ~(_PAK_ALIGN - 1)
    e = bytearray(stride)
    nb = name.encode('latin1')
    e[0:len(nb)] = nb                                   # rest zero -> NUL-terminated + clean padding
    struct.pack_into('<III', e, 0x40, _PAK_HDR, len(data), stride)   # dataOff, size, stride
    e[_PAK_HDR:_PAK_HDR + len(data)] = data
    return bytes(e)

def pak_append(pak: bytes, additions) -> bytes:
    """Insert (name, data) sub-files before the terminator. Existing entries are untouched (self-relative),
    and the preceding entry's stride already points at the terminator offset, i.e. at our first new entry."""
    t = pak_terminator(pak)
    out = bytearray(pak[:t])
    for name, data in additions:
        out += pak_build_entry(name, data)
    out += pak[t:]                                      # the empty-name terminator (+ any trailing)
    return bytes(out)

def pak_prepend(pak: bytes, additions) -> bytes:
    """Put (name, data) sub-files at the FRONT (entries are self-relative, GetPackFile searches by name, so
    order is free). Keeps our data near offset 0 — immune to any load-buffer truncation of the tail."""
    out = bytearray()
    for name, data in additions:
        out += pak_build_entry(name, data)
    out += pak                                          # original entries + terminator, unchanged
    return bytes(out)

def absorb_dummy(f, recs, host="DATA.DAT", dummy="DMMY."):
    """Extend `host` to cover the contiguous trailing `dummy` file. Returns (free_offset, free_bytes)
    where free_offset is the archive-relative offset (bytes from host start) of the new free tail region."""
    h, dm = recs[host], recs[dummy]
    if h['ext']*SECTOR + h['size'] != dm['ext']*SECTOR:
        raise ValueError(f"{host} and {dummy} are not contiguous — absorb not applicable")
    dummy_sectors = (dm['size'] + SECTOR - 1)//SECTOR
    free_off  = h['size']
    new_size  = h['size'] + dummy_sectors*SECTOR
    set_file_size(f, h, new_size)
    return free_off, new_size - free_off
