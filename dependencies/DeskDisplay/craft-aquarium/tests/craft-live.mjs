// Read-only validation of the installed, already-running Craft plugin.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const port=process.env.CRAFT_DEBUG_PORT || '23518';
const targets=await(await fetch(`http://127.0.0.1:${port}/json/list`)).json();
let verified=0;
for(const target of targets.filter(t=>t.url.includes('com.mirabox.streamdock.aquarium.'))){
 const socket=new WebSocket(target.webSocketDebuggerUrl);
 await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject;});
 let id=0;
 const evaluate=expression=>new Promise((resolve,reject)=>{
   const request=++id,timeout=setTimeout(()=>reject(new Error('Craft inspection timeout')),10000);
   socket.onmessage=e=>{const m=JSON.parse(e.data);if(m.id===request){clearTimeout(timeout);if(m.result?.exceptionDetails||m.error)reject(new Error(JSON.stringify(m)));else resolve(m.result?.result?.value);}};
   socket.send(JSON.stringify({id:request,method:'Runtime.evaluate',params:{expression,returnByValue:true}}));
 });
 try{
  const query=`(()=>{const e=window.aquariumHost&&[...aquariumHost.instances.values()][0];if(!e?.aquarium)return null;const a=e.aquarium;return {hidden:document.hidden,time:a.scene.time,lastSent:e.lastSent,running:a.running,occlusion:a.renderer.occlusion.enabled,frames:a.scene.fish.map(f=>f.spriteFrames?.length),width:a.canvas.width,height:a.canvas.height,buffered:aquariumHost.socket.bufferedAmount};})()`;
  const before=await evaluate(query);if(!before)continue;
  assert.equal(before.running,true);assert.equal(before.occlusion,true);assert.deepEqual(before.frames,Array(8).fill(20));
  await new Promise(resolve=>setTimeout(resolve,2500));
  const after=await evaluate(query);assert.ok(after.time>before.time);assert.ok(after.lastSent>before.lastSent);
  const png=await evaluate(`([...aquariumHost.instances.values()][0].aquarium.canvas.toDataURL('image/png'))`);
  await fs.writeFile(path.join(root,'craft-v1.1-live.png'),Buffer.from(png.split(',')[1],'base64'));
  console.log('PASS actual Craft V1.1 hidden host: animated cached fins, depth masks, ongoing frame output',after);verified++;
 } finally {socket.close();}
}
assert.ok(verified>0,'No active aquarium component found in Craft');
