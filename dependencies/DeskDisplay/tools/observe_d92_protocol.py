"""Bounded diagnostic instrumentation: observe D92 metadata, never alter arguments.

Requires matching vendor binary and an explicitly supplied running Craft PID.
Only retains frame headers and up to 8 image signature bytes, never full images.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import time

sys.path.insert(0, str(Path(os.environ['LOCALAPPDATA']) / 'DeskDisplayBuild/analysis-python'))
import frida

parser = argparse.ArgumentParser()
parser.add_argument('--pid', type=int, required=True)
parser.add_argument('--seconds', type=int, default=8, choices=range(1, 31))
args = parser.parse_args()
expected = '533926144e928d5c3e2c0012b52b086922bcd9c884f14228042fb3208782c397'
session = frida.attach(args.pid)
script = None
try:
    probe = session.create_script("rpc.exports = { path() { return Process.getModuleByName('SDLibrary1.dll').path; } };")
    probe.load()
    path = probe.exports_sync.path()
    probe.unload()
    if hashlib.sha256(Path(path).read_bytes()).hexdigest() != expected:
        raise RuntimeError('Vendor DLL version differs; offsets must be revalidated')
    script = session.create_script(r"""
const mod = Process.getModuleByName('SDLibrary1.dll');
let frames=0, reports=0, headerLogs=0, sampleLogs=0, results=0;
function isD92(p) { return p.add(0xc8).readU32()===0x5548 && p.add(0xcc).readU32()===0x1011; }
function hex(p,n) { return Array.from(new Uint8Array(p.readByteArray(n))).map(v=>v.toString(16).padStart(2,'0')).join(''); }
function jpegInfo(p,n) {
 const b=new Uint8Array(p.readByteArray(Math.min(n,65536)));
 if(b[0]!==255 || b[1]!==216) return null;
 for(let i=2;i+4<b.length;) {
  if(b[i++]!==255) break;
  while(b[i]===255) i++;
  const marker=b[i++]; if(marker===218 || marker===217) break;
  const length=(b[i]<<8)|b[i+1]; if(length<2 || i+length>b.length) break;
  if([192,193,194].includes(marker)) return {marker,precision:b[i+2],height:(b[i+3]<<8)|b[i+4],width:(b[i+5]<<8)|b[i+6],components:Array.from(b.slice(i+8,i+length))};
  i+=length;
 }
 return null;
}
Interceptor.attach(mod.base.add(0x284e0), {
 onEnter(a) {
  if (!isD92(a[0])) return;
  frames++;
  if (frames>6) return;
  const s=a[2], data=s.add(0x38).readPointer(), size=s.add(0x40).readS64().toNumber();
  send({kind:'frame',type:s.readU32(),field12:s.add(0x2c).readU8(),bytes:size,jpeg:jpegInfo(data,size),
   rect:[s.add(0x48).readS32(),s.add(0x4c).readS32(),s.add(0x50).readS32(),s.add(0x54).readS32()],
   signature:size>0?hex(data,Math.min(8,size)):''});
 }
});
Interceptor.attach(mod.base.add(0x31fb0), {
 onEnter(a) {
  this.tracked=false;
  if (!isD92(a[0])) return;
  reports++;
  const size=a[0].add(0xc0).readS32()+1;
  if (size<4 || size>4096) return;
  const prefix=hex(a[1],4);
  if ((prefix.substring(2)==='435254' || prefix.substring(2)==='4d5f56') && headerLogs<8) {
   headerLogs++; this.tracked=true;
   send({kind:'header',length:size,hex:hex(a[1],Math.min(41,size)),callers:Thread.backtrace(this.context,Backtracer.ACCURATE).filter(p=>p.compare(mod.base)>=0 && p.compare(mod.base.add(mod.size))<0).slice(0,6).map(p=>p.sub(mod.base).toString())});
  } else if(sampleLogs<3) {
   sampleLogs++; send({kind:'report-prefix',length:size,hex:hex(a[1],Math.min(8,size))});
  }
 },
 onLeave(ret) { if(this.tracked) { results++; send({kind:'write-result',success:ret.toInt32()!==0}); } }
});
rpc.exports={stats(){return {frames,reports,headerLogs,results};}};
send({kind:'attached',module:mod.name});
""")
    script.on('message', lambda message, data: print(json.dumps(message, ensure_ascii=False), flush=True))
    script.load()
    time.sleep(args.seconds)
    print(json.dumps({'kind': 'totals', **script.exports_sync.stats()}), flush=True)
finally:
    try:
        if script is not None:
            script.unload()
    finally:
        session.detach()
