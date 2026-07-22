import os
import struct

dat = open(os.path.join(DC1_DATA_DIR, "data.dat"),'rb')

import json

# Extracted Dark Cloud disc dir; required — see .env.sample.
DC1_DATA_DIR = os.environ.get("DC1_DATA_DIR")
if not DC1_DATA_DIR: raise SystemExit("Set $DC1_DATA_DIR to your extracted Dark Cloud disc dir (see .env.sample)")
CMD = {int(k):v for k,v in json.load(open('/tmp/stbcmds.json')).items()}
def u32(s,o): return struct.unpack('<I',s[o:o+4])[0]

def dis(stb, name):
    dat.seek(stb); s = dat.read(0x12000)
    cb, tbl, cnt = u32(s,8), u32(s,0xc), u32(s,0x10)
    print(f"\n========== {name}.stb  codeBase=0x{cb:X} labels={cnt} ==========")
    labs = [(u32(s,tbl+i*8), u32(s,tbl+i*8+4)) for i in range(cnt)]
    bo = sorted(labs, key=lambda x:x[1]); offs=[c for _,c in bo]
    print("labels:", [(lid,hex(co)) for lid,co in labs])
    for idx,(lid,co) in enumerate(bo):
        start = co + 8                                   # grid is offset +8 from the table codeOff
        end   = (offs[idx+1] if idx+1<len(offs) else min(len(s), co+0xA00))
        print(f"\n--- label {lid}  (code 0x{start:X} .. 0x{end:X}) ---")
        o=start; stk=[]
        while o+12 <= end:
            op,a1,a2 = u32(s,o),u32(s,o+4),u32(s,o+8); t=f"  +0x{o:04X}: "
            if   op==1:  stk.append(a1); print(f"{t}push   {a1} (0x{a1:X})")
            elif op==3:  stk.append(a2); print(f"{t}push   {a2} (0x{a2:X}) [t{a1}]")
            elif op==21:
                cid=stk[0] if stk else None; args=stk[1:]
                nm=CMD.get(cid, f"cmd_0x{cid:X}" if cid is not None else "cmd_?")
                print(f"{t}EXT    {nm}({', '.join(map(str,args))})"); stk=[]
            elif op==4:  print(f"{t}JMP    0x{a1:X}"); stk=[]
            elif op==17: print(f"{t}BR_FALSE 0x{a1:X}"); stk=[]
            elif op==18: print(f"{t}BR_TRUE  0x{a1:X}"); stk=[]
            elif op==15: print(f"{t}RET"); stk=[]
            elif op==16: print(f"{t}pop")
            elif op==23: print(f"{t}YIELD")
            elif op in (19,27): print(f"{t}call_func {a1} {a2}"); stk=[]
            elif op==0:  pass
            else: print(f"{t}op{op} a1={a1}(0x{a1:X}) a2={a2}")
            o+=12

dis(0x1ae92800,'c15a')
dis(0x1aede800,'c15b')
dat.close()
