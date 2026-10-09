#!/usr/bin/env python3
"""Disassemble Dark Cloud monster scripts (.stb).

    python3 tools/analysis/stbdis.py path/to/e86a.stb [more.stb ...]
    python3 tools/analysis/stbdis.py data.dat@0x1ae92800          # a script still inside data.dat
    python3 tools/analysis/stbdis.py --cmds event_cmds.json x.stb  # other command map (town/fishing event VM)

Layout (docs/stb-vm-and-ice-queen.md): header word 2 = codeBase (0x60), words 3/4 = label table offset/count;
each label-table entry is {id, header offset}; a function header is 56 bytes whose first word is the
codeBase-relative entry PC. Cells are 12 bytes (op, a1, a2). Subroutine calls (op 19/27) carry the callee's
header offset in a2, also codeBase-relative. Branch targets (op 16/17/18, a1) are codeBase-relative too.

The listing folds the value stack into expressions so an EXT line reads like a call, e.g.
`_SET_MOTION(13, 0.3f, 2|4)`; push-var operands are shown as vN (a2 = scope: 1 int local, 8 float local).
Only cells reachable from the labels are listed; functions are found by following calls.
"""
import os
import struct
import sys

# Battle-script external commands (ext_func_info @ ELF 0x2918A0, 91 pairs) by id.
CMD = {
    10:"_GET_DISTANCE", 11:"_GET_POSITION", 12:"_SET_ROTATION", 13:"_CHK_ROTATION", 14:"_CHK_MOVE",
    15:"_CHK_USER_INNER_PRODUCT", 30:"_GET_VECTOR", 31:"_GET_DIRECTION", 32:"_SET_MOVE", 33:"_CHK_MOVE_INFO",
    34:"_SET_MOVE_CANSEL", 35:"_SET_ROT_CANSEL", 36:"_SET_POSITION", 100:"_STATUS_SET_FALL",
    101:"_STATUS_SET_MUTEKI", 102:"_STATUS_SET_ALPHA", 103:"_STATUS_CHK_ALPHA", 104:"_STATUS_SET_DEAD",
    105:"_STATUS_SET_PALLET", 106:"_STATUS_SET_CLIPLEVEL", 107:"_STATUS_SET_EVENT", 108:"_STATUS_SET_COL_OFF",
    109:"_STATUS_GET_LIFE_RATE", 110:"_STATUS_GET_USER_VECTOR", 111:"_RUN_SCRIPT", 112:"_STATUS_GET_HEIGHT",
    113:"_STATUS_GET_HITDMG_VOL", 114:"_STATUS_GET_MOTION_ID", 115:"_STATUS_GET_DMG_ID",
    116:"_STATUS_SET_LOCKON_DIST", 117:"_STATUS_SET_SHADOW_LEN", 118:"_STATUS_SET_LOCKON_TRG",
    130:"_SET_BODY_COL", 131:"_SET_DMG_COL", 132:"_SET_DMG_PARA", 133:"_SET_SHOT", 134:"_SET_BODY_COL_PARA",
    136:"_SET_MOV_COL", 140:"_SET_SND_FRM", 141:"_SET_SND_NOW", 142:"_STOP_SND_NOW", 180:"_GET_RAND",
    181:"_GET_RANDF", 182:"_SIN_DEG", 183:"_COS_DEG", 200:"_SET_MOTION", 201:"_CHK_MOTION_FRM",
    202:"_GET_MOTION_FRM", 203:"_SET_MOTION_FRM", 204:"_SET_STATUS_CHANGE", 205:"_SET_TEX_ANIME_SW",
    206:"_SET_COLLISION_WIDTH", 207:"_GET_NEAR_MONSTER", 208:"_GET_STATUS_BIN2", 209:"_SET_BIN2",
    210:"_GET_CHR_ID", 211:"_GET_COL_HIT_ID", 212:"_GET_SCRIPT_ID", 213:"_GET_MONSTOR_POS",
    214:"_GET_MONSTOR_FRM", 215:"_SET_MONSTOR_POS", 216:"_SET_MONSTOR_MOVE", 217:"_SET_MONSTOR_LINK_MOVE",
    218:"_SET_MONSTOR_MOVE_CANSEL", 219:"_SET_MONSTOR_MOTION", 220:"_SET_GLOBAL_INT", 221:"_GET_GLOBAL_INT",
    222:"_GET_OBJ_POS", 223:"_SET_ROTATION_X", 224:"_SET_MOTION_CHANGE_STEP", 225:"_GET_MONSTOR_VECTOR",
    226:"_SET_LOCKON_DIST", 227:"_SET_LOCKON_SW", 228:"_STATUS_SET_LIFE", 229:"_SET_SHOT2", 230:"_SET_LOOP_SND",
    231:"_STOP_LOOP_SND", 232:"_DEL_LOOP_SND", 240:"_BOSS_FADE_OUT", 241:"_CHEKC_FADE_OUT", 242:"_SET_GRAVITY",
    244:"_SET_GUARD_FRAME", 245:"_GUARD_SEARCH", 246:"_GET_MOVE_VEC", 247:"_PUSH_IGLOBAL", 248:"_POP_IGLOBAL",
    249:"_GET_USER_STATUS", 250:"_SET_REFERENCE", 251:"_DEL_REFERENCE", 252:"_LOOKAT", 253:"_SET_SHADOW_FLAG",
}
CMP = {40: "==", 41: "!=", 42: "<", 43: "<=", 44: ">", 45: ">="}      # RS_CMP_* (runscript.hpp)
BINOP = {6: "+", 7: "-", 8: "*", 9: "/", 24: "&", 25: "|"}
HEADER = 0x38                                                       # function header size


def u32(s, o):
    return struct.unpack_from("<I", s, o)[0]


def f32(v):
    return struct.unpack("<f", struct.pack("<I", v))[0]


def load(arg):
    """`file.stb` or `data.dat@0xOFFSET` (reads 0x12000 bytes from the archive)."""
    if "@" in arg:
        path, off = arg.rsplit("@", 1)
        with open(path, "rb") as f:
            f.seek(int(off, 0))
            return f.read(0x12000), f"{os.path.basename(path)}@{off}"
    with open(arg, "rb") as f:
        return f.read(), os.path.basename(arg)


class Dis:
    def __init__(self, s):
        self.s = s
        self.cb = u32(s, 8)
        tbl, cnt = u32(s, 0xC), u32(s, 0x10)
        self.labels = [(u32(s, tbl + i * 8), u32(s, tbl + i * 8 + 4)) for i in range(cnt)]

    def entry(self, hdr):
        return self.cb + u32(self.s, hdr)

    def string(self, off):
        o = self.cb + off
        return self.s[o:self.s.index(b"\0", o)].decode("latin1", "replace")

    def literal(self, kind, v):
        if kind == 2:
            return f"{f32(v):g}f"
        if kind == 3:
            return repr(self.string(v))
        return str(v)

    def functions(self):
        """{header offset: name} for the labels and every function reachable through calls."""
        found = {hdr: f"label {lid}" for lid, hdr in self.labels}
        todo = list(found)
        while todo:
            hdr = todo.pop()
            for _, op, _, a2 in self.cells(hdr):
                if op in (19, 27):
                    tgt = self.cb + a2
                    if tgt not in found:
                        found[tgt] = f"func 0x{tgt:X}"
                        todo.append(tgt)
        return found

    def cells(self, hdr):
        """Cells of one function: from its entry to the final RET (the one followed by a padding cell)."""
        o = self.entry(hdr)
        s = self.s
        while o + 12 <= len(s):
            op, a1, a2 = u32(s, o), u32(s, o + 4), u32(s, o + 8)
            yield o, op, a1, a2
            if op == 15 and (o + 24 > len(s) or u32(s, o + 12) == 0):
                return
            o += 12

    def listing(self, hdr, name):
        s = self.s
        out = [f"\n--- {name}  header 0x{hdr:X}  locals={u32(s, hdr + 8)} args={u32(s, hdr + 12)}  code 0x{self.entry(hdr):X}"]
        stk = []

        def pop(n=1):
            vals = stk[-n:] if n else []
            del stk[len(stk) - len(vals):]
            return vals + ["?"] * (n - len(vals))

        for o, op, a1, a2 in self.cells(hdr):
            t = f"  {o:06X}: "
            if op == 0:
                continue
            if op == 1:
                stk.append(f"v{a1}")
            elif op == 2:
                stk.append(f"&v{a1}")
            elif op == 3:
                stk.append(self.literal(a1, a2))
            elif op == 4:
                pop()
            elif op == 5:
                ref, val = pop(2)
                out.append(f"{t}{ref.lstrip('&')} = {val}")
                stk.append(ref.lstrip("&"))
            elif op in BINOP:
                a, b = pop(2)
                stk.append(f"({a} {BINOP[op]} {b})")
            elif op == 11:
                stk.append(f"-{pop()[0]}")
            elif op == 26:
                stk.append(f"!{pop()[0]}")
            elif op == 14:
                a, b = pop(2)
                stk.append(f"({a} {CMP.get(a1, f'cmp{a1}')} {b})")
            elif op == 21:
                ent = pop(a1) or ["?"]
                try:
                    cid = int(ent[0])
                except ValueError:
                    cid = None
                out.append(f"{t}{CMD.get(cid, f'cmd{ent[0]}')}({', '.join(ent[1:])})")
            elif op == 15:
                out.append(f"{t}RET {pop()[0] if stk else ''}")
            elif op == 16:
                out.append(f"{t}JMP 0x{self.cb + a1:X}")
            elif op in (17, 18):
                out.append(f"{t}{'BR_FALSE' if op == 17 else 'BR_TRUE'} ({pop()[0]}) -> 0x{self.cb + a1:X}")
            elif op in (19, 27):
                out.append(f"{t}CALL func 0x{self.cb + a2:X}")
                stk.append("ret")
            elif op == 22:
                out.append(f"{t}.L{a1}")
            elif op == 23:
                out.append(f"{t}YIELD")
            else:
                out.append(f"{t}op{op} a1={a1} a2={a2} ({f32(a2):g})")
        return out


def main(args):
    if args[:1] == ["--cmds"]:
        import json
        CMD.clear()
        CMD.update({int(k): v for k, v in json.load(open(args[1])).items()})
        args = args[2:]
    for arg in args:
        s, name = load(arg)
        d = Dis(s)
        print(f"\n########## {name}  codeBase=0x{d.cb:X}  labels={[(lid, hex(h)) for lid, h in d.labels]}")
        funcs = d.functions()
        for hdr in sorted(funcs):
            print("\n".join(d.listing(hdr, funcs[hdr])))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    main(sys.argv[1:])
