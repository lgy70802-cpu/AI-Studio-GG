"""Generate synthetic header fixtures using only the vendor's header-building function.

No device object is created, and no HID entrypoint is called. This ABI is valid
only for the SHA-256 below; run as a disposable subprocess, not in the app.
"""
import argparse
import ctypes as c
import hashlib
import json
import os
from pathlib import Path
import random
import struct

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
root = Path(r'D:\Program Files (x86)\D92\MiraBoxCraft')
expected = '533926144e928d5c3e2c0012b52b086922bcd9c884f14228042fb3208782c397'
if hashlib.sha256((root/'SDLibrary1.dll').read_bytes()).hexdigest() != expected:
    raise RuntimeError('Unrecognized vendor DLL; do not reuse ABI assumptions')

with os.add_dll_directory(str(root)):
    qt = c.WinDLL(str(root/'Qt6Core.dll'))
    lib = c.WinDLL(str(root/'SDLibrary1.dll'))

def function(lib, name, restype, *argtypes):
    f = getattr(lib, name)
    f.restype, f.argtypes = restype, argtypes
    return f

construct = function(qt, '??0QByteArray@@QEAA@_JD@Z', c.c_void_p, c.c_void_p, c.c_int64, c.c_char)
destroy = function(qt, '??1QByteArray@@QEAA@XZ', None, c.c_void_p)
size = function(qt, '?size@QByteArray@@QEBA_JXZ', c.c_int64, c.c_void_p)
const_data = function(qt, '?constData@QByteArray@@QEBAPEBDXZ', c.c_void_p, c.c_void_p)
build = function(lib, '?getSecondaryScreenPicInfo@SDDevice@@MEAA?AVQByteArray@@AEBUImageStruct@@@Z',
                 c.c_void_p, c.c_void_p, c.c_void_p, c.c_void_p)

def vendor_header(kind, length, field12, width, height, x, y):
    # ImageStruct layout inferred from the complete function disassembly.
    image = c.create_string_buffer(0x90)
    address = c.addressof(image)
    struct.pack_into('<i', image, 0, kind)
    struct.pack_into('<B', image, 0x2c, field12)
    struct.pack_into('<iiii', image, 0x48, x, y, x+width-1, y+height-1)
    construct(address+0x30, length, b'\0')
    out = c.create_string_buffer(24)
    built = False
    try:
        # Audited function never dereferences this; it only builds a QByteArray.
        build(None, c.addressof(out), address)
        built = True
        return c.string_at(const_data(c.addressof(out)), size(c.addressof(out)))
    finally:
        if built:
            destroy(c.addressof(out))
        destroy(address+0x30)

rng = random.Random(929)
cases = [(16, 123, 0, 1920, 462, 0, 0), (19, 1025, 7, 462, 1920, 3, 11),
         (16, 14547, 177, 0, 0, 0, 0)]  # Full-frame geometry observed on D92.
for _ in range(30):
    cases.append((rng.choice([16,19]),rng.randrange(0,100000),rng.randrange(256),
                  rng.randrange(1,65536),rng.randrange(1,65536),rng.randrange(65536),rng.randrange(65536)))
fixtures=[]
for kind,length,field12,width,height,x,y in cases:
    actual=vendor_header(kind,length,field12,width,height,x,y)
    predicted=bytearray(32)
    predicted[:8]=b'CRT\0\0DRA'
    struct.pack_into('>IBHHHH',predicted,8,length+32,field12,width,height,x,y)
    if actual!=predicted:
        raise AssertionError((kind,actual.hex(),predicted.hex()))
    fixtures.append(dict(kind=kind,payloadLength=length,field12=field12,width=width,height=height,x=x,y=y,headerHex=actual.hex()))
args.output.write_text(json.dumps(dict(vendorSha256=expected,cases=fixtures),indent=2),encoding='utf-8')
print(json.dumps(dict(result='pass',vendorHeaderComparisons=len(fixtures),deviceCommandsSent=0,output=str(args.output))))
