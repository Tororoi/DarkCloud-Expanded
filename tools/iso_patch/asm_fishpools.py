#!/usr/bin/env python3
"""
Mini MIPS (R5900) assembler for the rewritten FishingLoadFish species-selection region.

Rewrites [0x1a8a48, 0x1a8d44) in place: reproduces native areas 0-4 at identical
distributions (with the two requested vanilla edits: area2 Gummy->Niler, area3
Piccoly->Gobbler), and adds custom areas 5/6/7. Equal-weight pools use a rand%N ->
byte-table lookup so adding fish later costs one table byte + bumping N.

Registers: v0/v1/at/a0 scratch; s0=time divisor; s2=slot (preserve); s3=species out;
s6=area (preserve). rand=0x1046f8 rnd=0x123cb0 (unused here) convergence=0x1a8d44.
"""
import struct, sys

REGION_LO = 0x1a8a48
REGION_HI = 0x1a8d44          # convergence (exclusive end of our region)
RAND      = 0x1046f8
CONV      = 0x1a8d44

R = {'zero':0,'at':1,'v0':2,'v1':3,'a0':4,'a1':5,'a2':6,'a3':7,
     's0':16,'s1':17,'s2':18,'s3':19,'s6':22,'ra':31}

class Asm:
    def __init__(self, base):
        self.base = base
        self.items = []          # (kind, ...) ; kind='i' insn thunk, 'd' data word, 'L' label
    # ---- encoders (return 32-bit word given current pc for branches) ----
    def _reg(self,r): return R[r] if isinstance(r,str) else r
    def emit(self, fn):  self.items.append(('i', fn))
    def word(self, w):   self.items.append(('d', w & 0xffffffff))
    def label(self, name): self.items.append(('L', name))

    # instruction helpers append thunks (pc,labels)->word
    def I(self, op, rs, rt, imm):
        rs=self._reg(rs); rt=self._reg(rt)
        self.emit(lambda pc,L,op=op,rs=rs,rt=rt,imm=imm:
                  (op<<26)|(rs<<21)|(rt<<16)|(imm&0xffff))
    def Rr(self, funct, rs, rt, rd, sh=0):
        rs=self._reg(rs); rt=self._reg(rt); rd=self._reg(rd)
        self.emit(lambda pc,L,funct=funct,rs=rs,rt=rt,rd=rd,sh=sh:
                  (rs<<21)|(rt<<16)|(rd<<11)|(sh<<6)|funct)
    def addiu(self,rt,rs,imm): self.I(0x09,rs,rt,imm)
    def li(self,rt,imm):
        if 0 <= imm <= 0xffff and imm>0x7fff:   # need zero-extended small? use ori
            self.I(0x0d,'zero',rt,imm)          # ori rt,zero,imm
        else:
            self.I(0x09,'zero',rt,imm)          # addiu rt,zero,imm (sign-extended)
    def ori(self,rt,rs,imm): self.I(0x0d,rs,rt,imm)
    def lui(self,rt,imm):    self.I(0x0f,'zero',rt,imm)
    def addu(self,rd,rs,rt): self.Rr(0x21,rs,rt,rd)
    def div(self,rs,rt):     self.emit(lambda pc,L,rs=self._reg(rs),rt=self._reg(rt): (rs<<21)|(rt<<16)|0x1a)
    def mfhi(self,rd):       self.Rr(0x10,'zero','zero',rd)
    def mflo(self,rd):       self.Rr(0x12,'zero','zero',rd)
    def movz(self,rd,rs,rt): self.Rr(0x0a,rs,rt,rd)
    def slti(self,rt,rs,imm):self.I(0x0a,rs,rt,imm)
    def lbu(self,rt,off,rs): self.I(0x24,rs,rt,off)
    def nop(self):           self.word(0)
    def _branch(self,op,rs,rt,label):
        rs=self._reg(rs); rt=self._reg(rt)
        def fn(pc,L,op=op,rs=rs,rt=rt,label=label):
            off=(L[label]-(pc+4))>>2
            return (op<<26)|(rs<<21)|(rt<<16)|(off&0xffff)
        self.emit(fn)
    def beq(self,rs,rt,label): self._branch(0x04,rs,rt,label)
    def bne(self,rs,rt,label): self._branch(0x05,rs,rt,label)
    def beqz(self,rs,label):   self._branch(0x04,rs,'zero',label)
    def bnez(self,rs,label):   self._branch(0x05,rs,'zero',label)
    def b(self,label):         self._branch(0x04,'zero','zero',label)
    def jal(self,target):
        self.emit(lambda pc,L,t=target: (0x03<<26)|((t>>2)&0x3ffffff))
    def babs(self,target):     # unconditional branch to absolute VA (via label trick)
        def fn(pc,L,t=target):
            off=(t-(pc+4))>>2
            return (0x04<<26)|(off&0xffff)
        self.emit(fn)

    def assemble(self):
        # pass 1: assign VAs
        labels={}; va=self.base; seq=[]
        for it in self.items:
            if it[0]=='L': labels[it[1]]=va
            else: seq.append((va,it)); va+=4
        end=va
        # pass 2: encode
        words=[]
        for va,it in seq:
            if it[0]=='d': words.append(it[1])
            else: words.append(it[1](va,labels) & 0xffffffff)
        return words, labels, end

def build():
    a=Asm(REGION_LO)
    # ---- entry: top-level rand draw, default species ----
    a.jal(RAND)
    a.addiu('s3','zero',-1)          # delay slot: default species -1
    # ---- dispatch on area ($s6). each 'li $v1,N' doubles as prev beq's delay ----
    a.li('v1',5);  a.beq('s6','v1','A5')
    a.li('v1',6);  a.beq('s6','v1','A6')
    a.li('v1',7);  a.beq('s6','v1','A7')
    a.li('v1',1);  a.beq('s6','v1','A1')
    a.li('v1',2);  a.beq('s6','v1','A2')
    a.li('v1',3);  a.beq('s6','v1','A3')
    a.li('v1',4);  a.beq('s6','v1','A4')
    a.nop()                          # delay of beq A4
    # fall through to A0

    def table_pick(label, tbl):
        # $v0 currently = rand; pick rand % len(tbl) -> byte table -> $s3
        n=len(tbl)
        a.label(label)
        a.li('v1',n)
        a.div('v0','v1')
        a.lui('at', 0)               # placeholder hi (patched via label below) -- use data-label addr
        # we can't know table VA yet; emit lui/ori referencing a label via thunks
        # replace: use dedicated thunks
        a.items.pop()                # remove placeholder lui
        tlab=label+'_T'
        a.emit(lambda pc,L,tl=tlab: (0x0f<<26)|(R['at']<<16)|((L[tl]>>16)&0xffff))   # lui at,hi
        a.emit(lambda pc,L,tl=tlab: (0x0d<<26)|(R['at']<<21)|(R['at']<<16)|(L[tl]&0xffff))  # ori at,at,lo
        a.mfhi('v0')                 # v0 = rand % n
        a.addu('at','at','v0')
        a.lbu('s3',0,'at')
        a.babs(CONV)
        a.nop()
        # table data (packed bytes, word-aligned)
        a.label(tlab)
        b=bytes(tbl)
        while len(b)%4: b+=b'\x00'
        for i in range(0,len(b),4):
            a.word(struct.unpack_from('<I',b,i)[0])

    # ---- A0 Norune: 4-way {Gobbler1, Nonky2, Gummy6, Niler7} ----
    table_pick('A0',[1,2,6,7])

    # ---- A1 Peanut Pond: rand%100 -> Gobbler1 35 / BakuBaku4 35 / Umadakara9 10 / Tarton10 20
    a.label('A1')
    a.li('v1',100); a.div('v0','v1'); a.nop(); a.nop(); a.mfhi('v0')
    a.addiu('s3','zero',1)                 # default band0 (<35) Gobbler
    a.slti('at','v0',35); a.bnez('at','_conv'); a.nop()
    a.addiu('s3','zero',4)                 # BakuBaku
    a.slti('at','v0',70); a.bnez('at','_conv'); a.nop()
    a.addiu('s3','zero',9)                 # Umadakara
    a.slti('at','v0',80); a.bnez('at','_conv'); a.nop()
    a.addiu('s3','zero',10)                # Tarton (else)
    a.babs(CONV); a.nop()

    # ---- A2 Matataki: rare(%s0) else rand%3 {Nonky2, BakuBaku4, Niler7}  (Gummy6->Niler7) ----
    a.label('A2')
    a.div('v0','s0'); a.nop(); a.nop(); a.mfhi('v1')
    a.beqz('v1','RARE'); a.nop()
    table_pick('A2C',[2,4,7])

    # ---- A3 East Harbor: 5-way equal {Bobo0, Kaiji3, Gobbler1, Bon12, Hama13} (Piccoly11->Gobbler1) ----
    table_pick('A3',[0,3,1,12,13])

    # ---- A4 Muska Lacka: rare(%s0) else rand%100 Negie14 40 / Den15 30 / Heela16 30 ----
    a.label('A4')
    a.div('v0','s0'); a.nop(); a.nop(); a.mfhi('v1')
    a.beqz('v1','RARE'); a.nop()
    a.li('v1',100); a.div('v0','v1'); a.nop(); a.nop(); a.mfhi('v0')
    a.addiu('s3','zero',14)                # Negie (<40)
    a.slti('at','v0',40); a.bnez('at','_conv'); a.nop()
    a.addiu('s3','zero',15)                # Den
    a.slti('at','v0',70); a.bnez('at','_conv'); a.nop()
    a.addiu('s3','zero',16)                # Heela (else)
    a.babs(CONV); a.nop()

    # ---- A5 Brownboo: rare(2% via %50) else rand%4 {Piccoly11,Piccoly11,Negie14,Gummy6} ----
    a.label('A5')
    a.li('v1',50); a.div('v0','v1'); a.nop(); a.nop(); a.mfhi('v1')
    a.beqz('v1','RARE'); a.nop()
    table_pick('A5C',[11,11,14,6])

    # ---- A6 Queens: 100% Bobo0 ----
    a.label('A6')
    a.addiu('s3','zero',0)
    a.babs(CONV); a.nop()

    # ---- A7 Yellow Drops: 4-way {Tarton10, Nonky2, Negie14, Bon12} ----
    table_pick('A7',[10,2,14,12])

    # ---- shared RARE tier: rand%5==0 -> Baron(0x11) else Mardan(5) ----
    a.label('RARE')
    a.jal(RAND); a.li('v1',5)              # delay: divisor 5
    a.div('v0','v1'); a.nop(); a.mfhi('v0')
    a.addiu('s3','zero',5)                 # Mardan default
    a.li('v1',0x11)                        # Baron
    a.movz('s3','v1','v0')                 # if rand%5==0 -> Baron
    a.babs(CONV); a.nop()

    # convergence target label (for _conv branches -> real CONV via babs would be cleaner,
    # but bnez needs a label; point _conv at a 1-instr trampoline)
    a.label('_conv')
    a.babs(CONV); a.nop()

    words,labels,end=a.assemble()
    # pad with nops out to REGION_HI so the whole region is clean (no stale vanilla bytes);
    # the pad IS the free headroom for future fish — new code overwrites the nops.
    pad=(REGION_HI-end)//4
    words+=[0]*pad
    return words,labels,end

if __name__=='__main__':
    words,labels,end=build()
    used=end-REGION_LO
    budget=REGION_HI-REGION_LO
    print("region [%08x,%08x) budget=%d bytes (%d words)"%(REGION_LO,REGION_HI,budget,budget//4))
    print("used   = %d bytes (%d words)"%(used,used//4))
    print("HEADROOM = %d bytes (%d words)"%(budget-used,(budget-used)//4))
    if used>budget: print("!!! OVERFLOW by %d bytes"%(used-budget))
    # branch range check
    for name,va in sorted(labels.items(),key=lambda x:x[1]):
        pass
    # dump
    if '-v' in sys.argv:
        va=REGION_LO
        inv={v:k for k,v in labels.items()}
        for w in words:
            tag=(" <%s>"%inv[va]) if va in inv else ""
            print("  %08x  %08x%s"%(va,w,tag)); va+=4
    # emit patch bytes as hex for integration
    blob=b''.join(struct.pack('<I',w) for w in words)
    print("BLOB_BYTES=%d"%len(blob))
    open('/tmp/fishpools.bin','wb').write(blob)
