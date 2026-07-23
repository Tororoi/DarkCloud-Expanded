#!/usr/bin/env python3
"""Fishing-sign ISO patch — DATA layer (step 1a): bake the sign asset into the disc.

Programmatic + repeatable (the shape the mod's file-select will drive): everything is found by NAME and
parsed by STRUCTURE, so it works on any Dark Cloud (USA) disc. This stage:
  1. copies the source ISO to ~/ROMs/Patched ISOs (never touches the original),
  2. absorbs the trailing DMMY. dummy into DATA.DAT (free tail space, no rebuild),
  3. appends fishsign.mds + fishsign.img as sub-files to meswin/mes_tex.pak (a global, boot-loaded pak),
  4. writes the grown pak into the tail and redirects its DATA.HD2 index entry.

After this the disc still boots normally — the two sub-files are inert until the boot ELF patch (step 2)
asks for them. So this validates the archive surgery in isolation.

NOTE: the two assets are read from game_data/fishsign/ (carved from e04's own scene.scn/img.pak). The
shipped patcher will carve them from the user's ISO instead; the bytes are identical.

  python3 tools/iso_patch/build_sign_iso.py            # -> ~/ROMs/Patched ISOs/Dark Cloud - Expanded.iso
Requires $DC1_ISO (see .env.sample).
"""
import os, sys, struct, shutil
import ps2iso

SEC = 2048
HOST_PAK = "meswin/mes_tex.pak"
# Native scene sign (Brownboo s04): inject the kanban georama part + place it via mapinfo.
SCENE_SCN   = "gedit/s04/scene.scn"
MAPINFO_CFG = "gedit/s04/mapinfo.cfg"
SIGN_X, SIGN_Y, SIGN_Z, SIGN_RY = 212, 9, -61, 0     # world position + Y-rotation (0 = south)

# ── engine addresses (guest / ELF vaddr) ──
GetPackFile  = 0x0013F720
EnterIMGFile = 0x00132BA0
LoadFile     = 0x0013F360
SysTexMgr    = 0x01C75870
DETOUR_VA    = 0x00180D7C      # the `jal LoadFile` that loads esys_%d.pak (read_buffer still = mes_tex here)
REJOIN_VA    = 0x00180D84      # instruction after the detour + its delay slot
CAVE_VA      = 0x002A2314      # 108B, file-backed, CLEAN across 443 sessions (CodeCaveScanner)
STR_VA       = 0x002452B8      # 72B clean cave for the "fishsign.img" string
CAVE_LEN     = 0x6C

# ── MIPS encoders ──
zero,v0,a0,a1,a2,a3,t0,sp = 0,2,4,5,6,7,8,29
DIAG_VA = 0x01F80000     # scanner-CLEAN BSS (0x01F80000 0x31900) — cave writes GetPackFile's result here
def _lui(rt,i):    return 0x3C000000 | (rt<<16) | (i&0xFFFF)
def _ori(rt,rs,i): return 0x34000000 | (rs<<21) | (rt<<16) | (i&0xFFFF)
def _lw(rt,o,b):   return 0x8C000000 | (b<<21)  | (rt<<16) | (o&0xFFFF)
def _sw(rt,o,b):   return 0xAC000000 | (b<<21)  | (rt<<16) | (o&0xFFFF)
def _addiu(rt,rs,i): return 0x24000000 | (rs<<21) | (rt<<16) | (i&0xFFFF)
def _move(rd,rs):  return _ori(rd,rs,0)
def _jal(t):       return 0x0C000000 | ((t>>2)&0x03FFFFFF)
def _j(t):         return 0x08000000 | ((t>>2)&0x03FFFFFF)
NOP = 0

def build_cave():
    """Reached by `j CAVE` replacing the esys `jal LoadFile` (ra not needed — we rejoin via `j REJOIN`).
    On entry a0=esys path (from 0x180d70), a1=read_buffer=mes_tex buffer (from 0x180d74). Register
    fishsign.img's textures into 0x1c75870, then re-run the original LoadFile(esys)."""
    w = [
        _addiu(sp, sp, -0x20),
        _sw(a0, 0x14, sp),                 # save esys path
        _sw(a1, 0x18, sp),                 # save mes_tex buffer ptr
        _move(a0, a1),                     # GetPackFile(buffer, "fishsign.img")
        _lui(a1, STR_VA >> 16),
        _ori(a1, a1, STR_VA & 0xFFFF),
        _addiu(a2, zero, 0),
        _jal(GetPackFile), NOP,            # v0 = fishsign.img bank in RAM (or 0 if not found)
        _lui(t0, DIAG_VA >> 16), _sw(v0, DIAG_VA & 0xFFFF, t0),   # DIAG: record GetPackFile's result
        _move(a1, v0),                     # EnterIMGFile(0x1c75870, bank, -1, 0, 0)
        _lui(a0, SysTexMgr >> 16),
        _ori(a0, a0, SysTexMgr & 0xFFFF),
        _addiu(a2, zero, -1),
        _addiu(a3, zero, 0),
        _addiu(t0, zero, 0),               # arg5=0 in REGISTER a4 (=t0/$8) — EE ABI passes args 5-8 in a4-a7,
                                           # NOT the stack; leaving a4 garbage made param_5!=0 -> registration skipped
        _jal(EnterIMGFile), NOP,
        _lw(a0, 0x14, sp),                 # original: LoadFile(esys path, buffer, 0)
        _lw(a1, 0x18, sp),
        _addiu(a2, zero, 0),
        _jal(LoadFile), NOP,
        _addiu(sp, sp, 0x20),
        _j(REJOIN_VA), NOP,
    ]
    b = b"".join(struct.pack("<I", x) for x in w)
    assert len(b) <= CAVE_LEN, f"cave {len(b)}B > {CAVE_LEN}B"
    return b

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(REPO, "game_data", "fishsign")

def align(x, a=SEC): return (x + a - 1) & ~(a - 1)

def main():
    src = os.environ.get("DC1_ISO")
    if not src: raise SystemExit("Set $DC1_ISO (see .env.sample)")
    outdir = os.path.join(os.path.expanduser("~/ROMs"), "Patched ISOs")
    os.makedirs(outdir, exist_ok=True)
    out = os.path.join(outdir, "Dark Cloud - Expanded.iso")

    mds = open(os.path.join(ASSETS, "kanban.mds"), "rb").read()
    img = open(os.path.join(ASSETS, "e01b24_bank.img"), "rb").read()   # IMG-magic bank holding e01b24
    print(f"assets: fishsign.mds {len(mds)}B, fishsign.img {len(img)}B")

    print(f"copying -> {out} ...")
    shutil.copyfile(src, out)

    with open(out, "r+b") as f:
        recs = ps2iso.parse_root(f)
        hed_r, hd2_r, dat_r = recs["DATA.HED"], recs["DATA.HD2"], recs["DATA.DAT"]
        dat_iso = dat_r["ext"] * SEC

        # 1) absorb the dummy -> free tail
        free_off, free_bytes = ps2iso.absorb_dummy(f, recs)
        print(f"absorbed dummy: +{free_bytes/1e6:.1f} MB free at DATA.DAT offset {free_off:#x}")

        # 2) locate mes_tex.pak in the archive index
        hed = ps2iso.read_file(f, hed_r)
        idx = ps2iso.archive_find(hed, HOST_PAK)
        if idx is None: raise SystemExit(f"{HOST_PAK} not in archive — not a Dark Cloud (USA) disc?")
        hd2_slot_iso = hd2_r["ext"] * SEC + 16 + idx * 32
        f.seek(hd2_slot_iso); pak_off, pak_size = struct.unpack("<II", f.read(8))
        print(f"{HOST_PAK}: archive idx {idx}, DATA.DAT off {pak_off:#x}, size {pak_size:,}")

        # 3) read it, append the two sub-files
        f.seek(dat_iso + pak_off); pak = f.read(pak_size)
        grown = ps2iso.pak_prepend(pak, [("fishsign.img", img)])  # only the texture; the MESH is now a native scene.scn part
        print(f"grown pak: {pak_size:,} -> {len(grown):,} (+{len(grown)-pak_size} B)")

        # 4) write to the tail (sector-aligned) + redirect the index entry
        tail_off = align(free_off)          # first free byte, already 2048-aligned
        if len(grown) > free_bytes: raise SystemExit("grown pak exceeds free tail")
        f.seek(dat_iso + tail_off); f.write(grown)
        # HD2 entry = [byte_off, byte_size, SECTOR, SECTOR_COUNT, ...]. CDRead seeks by the precomputed
        # sector[+8]/count[+0xc] (NOT the byte offset), so redirect BOTH the byte fields and the sector fields.
        new_sector = tail_off >> 11                       # tail_off is 2048-aligned; sector is DATA.DAT-relative
        new_count  = (len(grown) + SEC - 1) // SEC
        f.seek(hd2_slot_iso); f.write(struct.pack("<IIII", tail_off, len(grown), new_sector, new_count))
        print(f"redirected {HOST_PAK}: off {pak_off:#x} -> {tail_off:#x}, size {pak_size:,} -> {len(grown):,}, "
              f"sector -> {new_sector:#x}, count -> {new_count}")

        # 5) verify EXACTLY as the game reads it: via the sector[+8]/count[+0xc] fields
        f.seek(hd2_slot_iso); v_off, v_size, v_sec, v_cnt = struct.unpack("<IIII", f.read(16))
        assert v_sec == new_sector and v_cnt == new_count, "sector/count fields not written"
        f.seek(dat_iso + v_sec * SEC); v_pak = f.read(v_cnt * SEC)
        names = []
        p = 0
        while p < len(v_pak) and v_pak[p] != 0:
            names.append(v_pak[p:p+0x40].split(b"\x00")[0].decode("latin1"))
            p += struct.unpack_from("<III", v_pak, p+0x40)[2]
        print(f"verify: pak now holds {names}")
        assert "fishsign.img" in names, "texture sub-file missing after write"

        # ── NATIVE SCENE SIGN: inject the kanban as a georama part into Brownboo's scene.scn + place it via
        #    mapinfo.cfg. Draws with the town (area-load), NO mesh cave, NO runtime draw. Redirect both files
        #    into the tail after mes_tex.pak (advance the tail cursor, sector-aligned). ──
        import sign_scene
        tail = align(tail_off + len(grown))               # next free tail byte after the grown pak

        def redirect_file(name, new_data, verify_head=None):
            nonlocal tail
            i = ps2iso.archive_find(hed, name)
            if i is None: raise SystemExit(f"{name} not in archive")
            slot = hd2_r["ext"] * SEC + 16 + i * 32
            f.seek(slot); o0, s0 = struct.unpack("<II", f.read(8))
            if len(new_data) > free_off + free_bytes - tail: raise SystemExit("tail exhausted")
            f.seek(dat_iso + tail); f.write(new_data)
            sec, cnt = tail >> 11, (len(new_data) + SEC - 1) // SEC
            f.seek(slot); f.write(struct.pack("<IIII", tail, len(new_data), sec, cnt))
            print(f"redirected {name}: {s0:,} -> {len(new_data):,} B  @sector {sec:#x} count {cnt}")
            f.seek(dat_iso + sec * SEC); back = f.read(len(new_data))
            if verify_head is not None: assert back[:len(verify_head)] == verify_head, f"{name} readback bad"
            tail = align(tail + len(new_data))

        scn_i = ps2iso.archive_find(hed, SCENE_SCN)
        f.seek(hd2_r["ext"] * SEC + 16 + scn_i * 32); so, ss = struct.unpack("<II", f.read(8))
        f.seek(dat_iso + so); scene0 = f.read(ss)
        scene1 = sign_scene.build_injected_scene(scene0, mds)
        cfg_i = ps2iso.archive_find(hed, MAPINFO_CFG)
        f.seek(hd2_r["ext"] * SEC + 16 + cfg_i * 32); co, cs = struct.unpack("<II", f.read(8))
        f.seek(dat_iso + co); cfg0 = f.read(cs)
        cfg1 = sign_scene.build_injected_mapinfo(cfg0, SIGN_X, SIGN_Y, SIGN_Z, SIGN_RY)
        redirect_file(SCENE_SCN, scene1, b"SCN\x00")
        redirect_file(MAPINFO_CFG, cfg1)
        assert struct.unpack_from("<I", scene1, 4)[0] == struct.unpack_from("<I", scene0, 4)[0] + 1
        assert b"kanban" in scene1 and b'"kanban"' in cfg1
        print(f"native scene sign: kanban part + mapinfo GROUND @({SIGN_X},{SIGN_Y},{SIGN_Z})")

        # ── ELF patch: register fishsign.img's e01b24 into 0x1c75870 at boot ──
        elf_r = recs["SCUS_971.11"]
        elf_iso = elf_r["ext"] * SEC
        f.seek(elf_iso); ehdr = f.read(0x34)
        e_phoff = struct.unpack_from("<I", ehdr, 0x1c)[0]
        e_phent = struct.unpack_from("<H", ehdr, 0x2a)[0]
        e_phnum = struct.unpack_from("<H", ehdr, 0x2c)[0]
        p_off = p_va = None
        for i in range(e_phnum):
            f.seek(elf_iso + e_phoff + i*e_phent); typ, off, va, _pa, fsz, _m = struct.unpack("<IIIIII", f.read(24))
            if typ == 1 and fsz > 0 and va <= DETOUR_VA < va + fsz: p_off, p_va = off, va; break
        if p_off is None: raise SystemExit("no PT_LOAD covers the patch site")
        def elf_off(va): return elf_iso + p_off + (va - p_va)

        # RULE 3: verify pristine vanilla before patching
        f.seek(elf_off(DETOUR_VA)); orig = struct.unpack("<I", f.read(4))[0]
        f.seek(elf_off(DETOUR_VA + 4)); delay = struct.unpack("<I", f.read(4))[0]
        if orig != _jal(LoadFile) or delay != NOP:
            raise SystemExit(f"detour site not vanilla (got {orig:#010x}/{delay:#010x}) — wrong ISO/version")
        f.seek(elf_off(CAVE_VA)); cave_was = f.read(len(build_cave()))
        if any(cave_was): raise SystemExit("cave region not zero — unexpected ISO")

        cave = build_cave()
        f.seek(elf_off(STR_VA)); f.write(b"fishsign.img\x00")     # texture-bank sub-file name
        f.seek(elf_off(CAVE_VA)); f.write(cave)                    # the boot register stub
        f.seek(elf_off(DETOUR_VA)); f.write(struct.pack("<I", _j(CAVE_VA)))   # jal LoadFile -> j cave
        print(f"ELF patch: SCUS_971.11 @ISO {elf_iso:#x}; detour 0x{DETOUR_VA:x} -> cave 0x{CAVE_VA:x} ({len(cave)}B), str @0x{STR_VA:x}")

        # new PCSX2 game CRC = XOR of all 32-bit words in the patched ELF
        f.seek(elf_iso); pelf = f.read(elf_r["size"])
        crc = 0
        for i in range(len(pelf)//4): crc ^= struct.unpack_from("<I", pelf, i*4)[0]
        crc &= 0xFFFFFFFF

    reship_pnach(crc)
    print(f"\nDONE -> {out}\nELF CRC A5C05C78 -> {crc:08X}; pnach reshipped. Boot with the mod: e01b24Handle >= 0.")

def reship_pnach(crc, old="A5C05C78"):
    """Copy the mod's pnach to <newCRC>.pnach (CRC in gametitle only; patches are address-based) so PCSX2
    applies it to the patched disc. Updates every copy: PCSX2 cheats (deployed), repo, and build output."""
    import re, glob
    new = f"{crc:08X}"
    dests = [os.path.expanduser("~/Library/Application Support/PCSX2/cheats"),
             os.path.join(REPO, "Dark Cloud Improved Version", "Resources", "PNACH"),
             os.path.join(REPO, "Dark Cloud Improved Version", "bin", "Debug", "net8.0", "Resources", "PNACH")]
    norm = lambda t: re.sub(r'\[[0-9A-Fa-f]{8}\]', '[]', t)   # CRC-agnostic body, for identity matching
    for d in dests:
        src = os.path.join(d, old + ".pnach")
        if not os.path.exists(src): continue
        body = re.sub(r'\[' + old + r'\]', '[' + new + ']', open(src).read())
        # remove OUR own stale patched-CRC pnach (earlier cave versions). Content-matched so we NEVER touch
        # another game's cheat file that happens to share this shared PCSX2 cheats folder.
        for stale in glob.glob(os.path.join(d, "*.pnach")):
            nm = os.path.splitext(os.path.basename(stale))[0].upper()
            if not re.fullmatch(r'[0-9A-F]{8}', nm) or nm in (old, new): continue
            if norm(open(stale).read()) == norm(body):
                os.remove(stale); print(f"  pnach rm stale {os.path.basename(stale)}.pnach")
        open(os.path.join(d, new + ".pnach"), "w").write(body)
        print(f"  pnach -> {os.path.join(d, new + '.pnach')}")

if __name__ == "__main__":
    main()
