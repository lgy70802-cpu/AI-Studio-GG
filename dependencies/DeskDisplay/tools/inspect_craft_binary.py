"""Offline PE export/disassembly inspection. Never loads or calls vendor code."""
import argparse
import hashlib
import os
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(os.environ['LOCALAPPDATA']) / 'DeskDisplayBuild/analysis-python'))
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

parser = argparse.ArgumentParser()
parser.add_argument('--dll', default=r'D:\Program Files (x86)\D92\MiraBoxCraft\SDLibrary1.dll')
parser.add_argument('--match', default=r'getSecondaryScreenPicInfo|sendPicSizeCommand|sendPixmapEndCommand')
parser.add_argument('--rva', type=lambda v: int(v, 0))
parser.add_argument('--max-bytes', type=int, default=8192)
parser.add_argument('--span', action='store_true', help='Decode max-bytes including adjacent unwind fragments')
args = parser.parse_args()
data = Path(args.dll).read_bytes()
pe = pefile.PE(data=data)
base = pe.OPTIONAL_HEADER.ImageBase
symbols = {base + s.address: s.name.decode(errors='replace') for s in pe.DIRECTORY_ENTRY_EXPORT.symbols if s.name}
for module in getattr(pe, 'DIRECTORY_ENTRY_IMPORT', []):
    for s in module.imports:
        symbols[s.address] = module.dll.decode() + '!' + (s.name.decode() if s.name else str(s.ordinal))
functions = {e.struct.BeginAddress: e.struct.EndAddress for e in getattr(pe, 'DIRECTORY_ENTRY_EXCEPTION', [])}
dis = Cs(CS_ARCH_X86, CS_MODE_64)
dis.detail = True
targets = [(a-base,n) for a,n in symbols.items() if re.search(args.match,n) and a-base in functions]
if args.rva is not None:
    targets = [(args.rva, symbols.get(base+args.rva,'requested RVA'))]
print('SHA256', hashlib.sha256(data).hexdigest())
for rva,name in targets:
    end = rva+args.max_bytes if args.span else min(functions.get(rva, rva+args.max_bytes),rva+args.max_bytes)
    print('\nFUNCTION',hex(rva),hex(end),name)
    for ins in dis.disasm(pe.get_data(rva,end-rva),base+rva):
        notes=[]
        for operand in ins.operands:
            addr = None
            if operand.type == X86_OP_IMM and ins.mnemonic in ('call','jmp'):
                addr=operand.imm
            elif operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                addr=ins.address+ins.size+operand.mem.disp
            if addr is not None:
                if addr in symbols:
                    notes.append(symbols[addr])
                elif operand.type == X86_OP_MEM and base <= addr < base+pe.OPTIONAL_HEADER.SizeOfImage:
                    raw=pe.get_data(addr-base,40)
                    notes.append(hex(addr-base)+' '+repr(raw))
        print(f'{ins.address-base:08x}: {ins.mnemonic:7} {ins.op_str:32} '+ '; '.join(notes))
