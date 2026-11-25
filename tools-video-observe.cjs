const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const samples=[],start=Date.now(),seconds=Number(process.argv[2]||300);
 while(Date.now()-start<seconds*1000){
  const state=await page.evaluate(()=>new Promise((resolve,reject)=>{const id=crypto.randomUUID(),timer=setTimeout(()=>{window.chrome.webview.removeEventListener('message',fn);reject(Error('devices timeout'));},15000),fn=e=>{if(e.data.id!==id)return;clearTimeout(timer);window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command:'devices'});}));
  samples.push({elapsed:(Date.now()-start)/1000,devices:state.devices.map(({preview,...d})=>d)});await new Promise(r=>setTimeout(r,2000));
 }
 const summary={seconds:(Date.now()-start)/1000,samples:samples.length,devices:[]};
 for(const d of samples[0].devices){const entries=samples.map(s=>s.devices.find(x=>x.serial===d.serial));assert(entries.every(x=>x.status==='已连接'&&x.playlistRunning));assert(entries.filter(x=>x.playing).length/entries.length>0.85);const rates=entries.filter(x=>x.videoFps>0).map(x=>x.videoFps).sort((a,b)=>a-b);assert(rates.length>10);const transitions=entries.slice(1).filter((x,i)=>x.themeId!==entries[i].themeId).length;assert(transitions>20);summary.devices.push({model:d.model,serial:d.serial,videoSamples:rates.length,medianFps:rates[Math.floor(rates.length/2)],transitions,statuses:[...new Set(entries.map(x=>x.outputStatus))]});}
 fs.writeFileSync(path.join(__dirname,'artifacts/video-qa/extended.json'),JSON.stringify({summary,samples},null,2));console.log(JSON.stringify(summary));await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
