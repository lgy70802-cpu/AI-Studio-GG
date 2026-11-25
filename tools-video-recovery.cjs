const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const mode=process.argv[2],browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{const id=crypto.randomUUID(),timer=setTimeout(()=>{window.chrome.webview.removeEventListener('message',fn);reject(Error('timeout '+command));},60000),fn=e=>{if(e.data.id!==id)return;clearTimeout(timer);window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command,...payload});}),{command,payload});
 const video=(await req('library-load')).themes.find(t=>t.category==='视频');
 if(mode==='prepare'){
   for(const d of (await req('devices')).devices){await req('playlist-start',{serial:d.serial,playlistId:'qa-video-mixed-'+d.model});if((await req('devices')).devices.find(x=>x.serial===d.serial).themeId!==video.id)await req('playlist-step',{serial:d.serial,direction:1});}
 }else{
   await page.waitForTimeout(2500);
   const state=await req('devices');assert(state.restore);assert.equal(state.devices.length,2);
   for(const d of state.devices){assert(d.playing&&d.playlistRunning,d.model+' recovery failed '+d.outputStatus);if(mode==='missing')assert.notEqual(d.themeId,video.id,'missing video not skipped');}
   assert.equal(state.scheduling.enabled,false);
   fs.writeFileSync(path.join(__dirname,'artifacts/video-qa/'+mode+'.json'),JSON.stringify({...state,devices:state.devices.map(({preview,...d})=>d)},null,2));
 }
 console.log(mode.toUpperCase()+'_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
