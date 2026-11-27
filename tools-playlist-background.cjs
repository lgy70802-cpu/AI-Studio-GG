const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
const request=()=>page.evaluate(()=>new Promise(resolve=>{const id=crypto.randomUUID(),fn=e=>{if(e.data.id===id){window.chrome.webview.removeEventListener('message',fn);resolve(e.data.result);}};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command:'devices'});}));
const before=await request();
assert(before.devices.every(d=>d.playlistRunning&&d.playing));
await new Promise(r=>setTimeout(r,13000));
const after=await request();
for(const d of before.devices){
 const later=after.devices.find(x=>x.serial===d.serial);
 assert.equal(later.playlistId,d.playlistId);
 assert(later.playing&&later.playlistRunning);
 assert(later.frames>d.frames || later.themeId!==d.themeId,'minimized device failed to continue');
}
const result={restartRestored:true,minimizedPlayback:true,before:before.devices.map(d=>({serial:d.serial,theme:d.themeId,frames:d.frames})),after:after.devices.map(d=>({serial:d.serial,theme:d.themeId,frames:d.frames}))};
fs.writeFileSync(path.join(__dirname,'artifacts/playlist-qa/restart-minimized.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify(result));await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
