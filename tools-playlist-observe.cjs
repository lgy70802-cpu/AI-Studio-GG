const fs=require('fs'),path=require('path');
const {chromium}=require('playwright');
(async()=>{
const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
const samples=[];
for(let i=0;i<45;i++){
 const state=await page.evaluate(()=>new Promise(resolve=>{const id=crypto.randomUUID();const handler=e=>{if(e.data.id===id){window.chrome.webview.removeEventListener('message',handler);resolve(e.data.result);}};window.chrome.webview.addEventListener('message',handler);window.chrome.webview.postMessage({id,command:'devices'});}));
 samples.push({at:new Date().toISOString(),devices:state.devices.map(d=>({serial:d.serial,status:d.status,remaining:d.playlistRemaining,theme:d.themeId,frames:d.frames,playing:d.playing}))});
 if(samples.at(-1).devices.some(d=>d.status==='离线'))console.log('OFFLINE_OBSERVED');
 await new Promise(r=>setTimeout(r,1000));
}
fs.writeFileSync(path.join(__dirname,'artifacts/playlist-qa/reconnect-observation.json'),JSON.stringify(samples,null,2));
console.log('observation saved');await browser.close();
})().catch(e=>{console.error(e);process.exitCode=1;});
