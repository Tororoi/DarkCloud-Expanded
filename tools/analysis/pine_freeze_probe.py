#!/usr/bin/env python3
"""Read the ally-swap diagnostic state out of a FROZEN game over PINE.

PCSX2's PINE server serves ONE client at a time, and the mod app holds it — so:
  1. leave PCSX2 running with the game frozen,
  2. QUIT the mod app (frees the PINE slot),
  3. run:  python3 tools/analysis/pine_freeze_probe.py

Prints the ReadInfo cfg-command breadcrumb (which .chr load command hung — see
AllySwapPrototype.CfgCmdNames), the CDataAlloc2 overflow mailbox, the character
arena fills, and the town mode/event state. All reads, nothing written.
"""
import socket, struct, os, sys

CFG_CMDS = ["VERTEX_ANIME","SHADOW_VERTEX_ANIME","MODEL","SHADOW_MODEL","MOTION","SHADOW_MOTION",
            "KEY","KEY_START","MOTION_END","CLOTH","BODY_SIZE","ALLOC_MDT","ALLOC_DBUFF",
            "ALLOC_SHADOW_MDT","ALLOC_SHADOW_DBUFF","IMG","IMG_END","FOOT","EVENT"]

def connect():
    cands = [os.path.join(os.environ.get('TMPDIR', '/tmp'), 'pcsx2.sock'), '/tmp/pcsx2.sock']
    path = next((p for p in cands if os.path.exists(p)), None)
    if not path:
        sys.exit("PINE socket not found (is PCSX2 running?)")
    s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    s.settimeout(3)
    try:
        s.connect(path)
    except ConnectionRefusedError:
        sys.exit("PINE refused — PCSX2 not running, or the mod app still holds the connection")
    return s

def read32(s, addr):
    s.sendall(struct.pack('<IBI', 9, 2, addr & 0x1FFFFFFF))   # opcode 2 = MsgRead32
    hdr = b''
    while len(hdr) < 4: hdr += s.recv(4 - len(hdr))
    size = struct.unpack('<I', hdr)[0]
    body = b''
    while len(body) < size - 4: body += s.recv(size - 4 - len(body))
    return struct.unpack('<I', body[1:5])[0]

def main():
    s = connect()
    def p32(label, addr, fmt=lambda v: f"0x{v:08X} ({v})"):
        v = read32(s, addr)
        print(f"  {label:<38} {fmt(v)}")
        return v

    print("frozen-state probe:")
    cmd = read32(s, 0x01F10078)
    name = CFG_CMDS[cmd] if 0 <= cmd < len(CFG_CMDS) else "?"
    print(f"  cfg-cmd breadcrumb (0x01F10078)        [{cmd}] {name}   <-- the hanging .chr load command")
    p32("alloc-overflow arena (0x01F10070)", 0x01F10070)
    p32("alloc-overflow size  (0x01F10074)", 0x01F10074)
    for label, addr in [("geometry arena used/cap (0x01D3A060)", 0x01D3A068),
                        ("", 0x01D3A06C),
                        ("texture arena used/cap  (0x01D3A080)", 0x01D3A088),
                        ("", 0x01D3A08C)]:
        v = read32(s, addr)
        print(f"  {label:<38} {v} blk = {v * 16 // 1024}K" if label else f"  {'':<38} cap {v} blk = {v * 16 // 1024}K")
    p32("GameMode (0x002A1F50)", 0x002A1F50)
    p32("start_event_no (0x002A28C0)", 0x002A28C0)
    p32("mode byte (0x002A2534)", 0x002A2534)
    s.close()

if __name__ == '__main__':
    main()
