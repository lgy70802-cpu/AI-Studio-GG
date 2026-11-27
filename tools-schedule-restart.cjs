const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{const id=crypto.randomUUID(),fn=e=>{if(e.data.id!==id)return;window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command,...payload});}),{command,payload});
 const file=path.join(__dirname,'artifacts/schedule-qa/restart-check.json');
 const state=(await req('devices')).scheduling;
 if(process.argv[2]==='before'){
   fs.writeFileSync(file,JSON.stringify({enabled:state.enabled,historyIds:state.history.map(e=>e.id),completed:state.completed},null,2));
   console.log('restart baseline saved');
 }else{
   const baseline=JSON.parse(fs.readFileSync(file));
   assert.equal(state.enabled,baseline.enabled,'global setting lost');
   for(const id of baseline.completed) assert(state.completed.includes(id),'one-off completion lost');
   assert.deepEqual(state.history.map(e=>e.id),baseline.historyIds,'restart unexpectedly re-executed');
   const snapshot=await req('library-load');
   for(const rule of snapshot.schedules) if(rule.id.startsWith('qa-schedule-'))rule.enabled=false;
   await req('library-save',{snapshot});await req('schedule-enable',{enabled:false});
   for(const device of (await req('devices')).devices){
     const list=snapshot.playlists.find(p=>p.id==='qa-playlist-'+device.model);
     if(list)await req('playlist-start',{serial:device.serial,playlistId:list.id});
   }
   await page.reload();await page.waitForTimeout(1000);await page.getByRole('button',{name:'定时切换',exact:true}).click();
   await page.screenshot({path:path.join(__dirname,'artifacts/schedule-qa/schedule-final.png')});
   fs.writeFileSync(file,JSON.stringify({...baseline,restartPassed:true,globalPausedAfterAcceptance:true},null,2));console.log('RESTART_SETTING_HISTORY_ONCE_DEDUP_PASSED');
 }
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
