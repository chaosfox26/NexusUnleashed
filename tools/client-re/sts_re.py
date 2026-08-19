#!/usr/bin/env python3
"""sts_re - our own clean client-binary analyzer (Starlight tooling).

Reverse-engineers Carbine's client (source #1) to pin protocol formats WITHOUT
touching any emulator/NF source. Built on capstone (MIT) + pefile (MIT); the
analysis code is ours. Used to derive the STS login message formats directly
from `StsConnLib64.MT.dll`.

Capabilities:
  strings  <dll>                    list printable strings + their VAs by section
  xrefs    <dll> <VA>[,<VA>...]     find code cross-refs (RIP-relative) to VAs
  fieldrefs<dll> <startVA> <endVA>  list every string a code range references
  disasm   <dll> <startVA> <endVA>  disassemble a range, annotating string refs

This is analysis of the CLIENT (a fact source), never of NF. No NF input.
"""
import sys, pefile, capstone

def load(path):
    pe = pefile.PE(path, fast_load=True)
    base = pe.OPTIONAL_HEADER.ImageBase
    text = None
    strmap = {}
    for s in pe.sections:
        nm = s.Name.rstrip(b"\x00")
        va = base + s.VirtualAddress
        data = s.get_data()
        if nm == b".text":
            text = (va, data)
        if nm in (b".rdata", b".data"):
            i = 0
            while i < len(data):
                j = i
                while j < len(data) and 32 <= data[j] < 127:
                    j += 1
                if j - i >= 2:
                    strmap[va + i] = data[i:j].decode("latin1")
                i = j + 1
    return pe, base, text, strmap

def md64():
    m = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    m.detail = True
    return m

def cmd_strings(path):
    pe, base, text, strmap = load(path)
    for va in sorted(strmap):
        print("0x%X\t%r" % (va, strmap[va]))

def rip_targets(insn):
    for op in insn.operands:
        if op.type == capstone.x86.X86_OP_MEM and op.mem.base == capstone.x86.X86_REG_RIP:
            yield insn.address + insn.size + op.mem.disp

def cmd_xrefs(path, vas):
    pe, base, (tva, tdata), strmap = load(path)
    want = set(vas)
    hits = {}
    for insn in md64().disasm(tdata, tva):
        for t in rip_targets(insn):
            if t in want:
                hits.setdefault(t, []).append(insn.address)
    for va in vas:
        print("0x%X (%s): %s" % (va, strmap.get(va, "?"),
              " ".join("0x%X" % a for a in hits.get(va, []))))

def cmd_fieldrefs(path, a, b):
    pe, base, (tva, tdata), strmap = load(path)
    for insn in md64().disasm(tdata[a - tva:b - tva], a):
        for t in rip_targets(insn):
            s = strmap.get(t)
            if s and len(s) >= 2 and (s[0].isalpha() or s[0] in "./"):
                print("0x%X  %-6s -> %r" % (insn.address, insn.mnemonic, s))

def cmd_disasm(path, a, b):
    pe, base, (tva, tdata), strmap = load(path)
    for insn in md64().disasm(tdata[a - tva:b - tva], a):
        ann = ""
        for t in rip_targets(insn):
            if t in strmap:
                ann = " ; -> %r" % strmap[t]
        if insn.mnemonic == "call":
            ann += " [CALL]"
        print("0x%X:  %-8s %s%s" % (insn.address, insn.mnemonic, insn.op_str, ann))

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__); sys.exit(2)
    cmd, path = sys.argv[1], sys.argv[2]
    if cmd == "strings":
        cmd_strings(path)
    elif cmd == "xrefs":
        cmd_xrefs(path, [int(x, 16) for x in sys.argv[3].split(",")])
    elif cmd == "fieldrefs":
        cmd_fieldrefs(path, int(sys.argv[3], 16), int(sys.argv[4], 16))
    elif cmd == "disasm":
        cmd_disasm(path, int(sys.argv[3], 16), int(sys.argv[4], 16))
    else:
        print("unknown command", cmd); sys.exit(2)
