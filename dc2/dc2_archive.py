#!/usr/bin/env python3
r"""Dark Cloud 2 (SCUS_972.13, USA v2.00) archive reader/extractor.

DC2 packs its assets in DATA.DAT, indexed by DATA.HD2 (DATA.HD3 is a parallel 16-byte index we don't
need). Unlike DC1 (data.hed name list + data.hd2 offsets), DC2 folds names into HD2:

  DATA.HD2 = N file entries (32 bytes each) followed by a C-string name table.
    entry: u32 nameOff (absolute offset into HD2, into the name table);
           u32 pad[3];
           u32 dataOffset (byte offset into DATA.DAT);
           u32 size (bytes);
           u32 idx;   (running sector-ish index)
           u32 type;  (asset class)
  The name table begins at entry[0].nameOff, so N = entry[0].nameOff // 32. Names use '\' separators.

DATA.HD2/HD3 are extracted to ROMs/dc2_extracted; DATA.DAT is read IN PLACE from the ISO (it's 1.5 GB —
no need to copy it). DATA.DAT sits at ISO LBA 2828 (v2.00 USA), so a file at dataOffset is at ISO byte
2828*2048 + dataOffset. Extracted individual files land in dc2_extracted. Usage:
  python3 dc2/dc2_archive.py list [substr]        # list files (optionally filtered)
  python3 dc2/dc2_archive.py extract <path> [out] # extract one file (path uses '/' or '\')
"""
import struct, sys, os

DC2_DIR = "/Users/thomascantwell/ROMs/dc2_extracted"
DC2_ISO = "/Users/thomascantwell/ROMs/Dark Cloud 2 (USA) (v2.00).iso"
DATA_DAT_LBA = 2828   # DATA.DAT's start sector in the ISO (from the ISO-9660 root dir)

def _load_index():
    hd2 = open(os.path.join(DC2_DIR, "DATA.HD2"), "rb").read()
    namebase = struct.unpack_from("<I", hd2, 0)[0]
    n = namebase // 32
    files = []  # (name, dataOffset, size, type)
    for i in range(n):
        nameOff, _, _, _, dataOff, size, idx, typ = struct.unpack_from("<8I", hd2, i * 32)
        if nameOff == 0:
            continue
        end = hd2.index(0, nameOff)
        name = hd2[nameOff:end].decode("latin1", "replace").replace("\\", "/")
        files.append((name, dataOff, size, typ))
    return files

def list_files(substr=None):
    for name, off, size, typ in _load_index():
        if substr is None or substr.lower() in name.lower():
            print(f"  {name:40} off={off:12} size={size:9} type={typ}")

def read_file(path):
    """Return the bytes of one DATA.DAT file by its archive path ('/' or '\\' separators).
    Read straight from the ISO at 2828*2048 + dataOffset (DATA.DAT is not copied out)."""
    want = path.replace("\\", "/").lower()
    for name, off, size, typ in _load_index():
        if name.lower() == want:
            with open(DC2_ISO, "rb") as f:
                f.seek(DATA_DAT_LBA * 2048 + off); return f.read(size)
    raise KeyError(f"{path} not found in DATA.HD2")

def extract_file(path, out=None):
    data = read_file(path)
    out = out or os.path.join(DC2_DIR, os.path.basename(path.replace("\\", "/")))
    os.makedirs(os.path.dirname(out), exist_ok=True) if os.path.dirname(out) else None
    open(out, "wb").write(data)
    print(f"extracted {path} ({len(data)} bytes) -> {out}")

if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] == "list":
        list_files(sys.argv[2] if len(sys.argv) > 2 else None)
    elif sys.argv[1] == "extract":
        extract_file(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
    else:
        print(__doc__)
