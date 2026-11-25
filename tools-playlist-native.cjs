const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
const root=__dirname, out=path.join(root,'artifacts/playlist-qa');
fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9227');
 const page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 if(!page) throw Error('studio page missing');
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{
   const id=crypto.randomUUID(),bridge=window.chrome.webview;
   const handler=event=>{if(event.data.id!==id)return;bridge.removeEventListener('message',handler);event.data.error?reject(Error(event.data.error)):resolve(event.data.result);};
   bridge.addEventListener('message',handler);bridge.postMessage({id,command,...payload});
   setTimeout(()=>{bridge.removeEventListener('message',handler);reject(Error('bridge timeout '+command));},30000);
 }),{command,payload});
 const devices=()=>req('devices');
 let snapshot=await req('library-load');
 const state=await devices();
 const d69=state.devices.find(d=>d.model==='D69'),d92=state.devices.find(d=>d.model==='D92');
 assert(d69&&d92,'D69 and D92 required');
 const pictures=snapshot.themes.filter(t=>t.category==='壁纸'&&t.inLibrary!==false&&!t.deletedAt&&t.imageUrl).slice(0,3);
 assert.equal(pictures.length,3);
 const ids={};
 for(const d of [d69,d92]){
   await req('configure',{serial:d.serial,portrait:false,flipped:false});
   const id='qa-playlist-'+d.model;
   ids[d.model]=id;
   const list={id,name:'验收_'+d.model+'壁纸轮播',deviceId:d.serial,orientation:'landscape',mood:'专注',shuffle:false,defaultDurationSeconds:10,
     items:pictures.map(t=>({themeId:t.id,durationSeconds:10}))};
   snapshot.playlists=snapshot.playlists.filter(p=>p.id!==id);snapshot.playlists.push(list);
 }
 await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(1500);
 for(const d of [d69,d92])await req('playlist-start',{serial:d.serial,playlistId:ids[d.model]});
 console.log('BOTH_STARTED');
 await page.getByRole('button',{name:'资源库',exact:true}).click();
 await page.waitForTimeout(2200);
 await req('playlist-pause',{serial:d69.serial,playlistId:ids.D69});
 const a=(await devices()).devices.find(d=>d.serial===d69.serial);
 const otherBefore=(await devices()).devices.find(d=>d.serial===d92.serial);
 await page.waitForTimeout(12000);
 const after=(await devices()).devices;
 const b=after.find(d=>d.serial===d69.serial),otherAfter=after.find(d=>d.serial===d92.serial);
 assert.equal(a.playlistRemaining,b.playlistRemaining,'paused countdown frozen');
 assert.equal(a.themeId,b.themeId,'paused image retained');
 assert.notEqual(otherBefore.themeId,otherAfter.themeId,'other device continued');
 console.log('PAUSE_AND_INDEPENDENCE_PASSED');
 await req('playlist-start',{serial:d69.serial,playlistId:ids.D69});
 const prev=(await devices()).devices.find(d=>d.serial===d69.serial).themeId;
 await req('playlist-step',{serial:d69.serial,playlistId:ids.D69,direction:1});
 assert.notEqual((await devices()).devices.find(d=>d.serial===d69.serial).themeId,prev);
 await req('playlist-step',{serial:d69.serial,playlistId:ids.D69,direction:-1});
 assert.equal((await devices()).devices.find(d=>d.serial===d69.serial).themeId,prev);
 await req('configure',{serial:d69.serial,playing:false});
 const paused=(await devices()).devices.find(d=>d.serial===d69.serial).playlistRemaining;
 await page.waitForTimeout(2500);
 assert.equal((await devices()).devices.find(d=>d.serial===d69.serial).playlistRemaining,paused);
 await req('configure',{serial:d69.serial,playing:true});
 snapshot=await req('library-load');
 snapshot.playlists.find(p=>p.id===ids.D69).shuffle=true;
 await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(1000);
 let last=(await devices()).devices.find(d=>d.serial===d69.serial).themeId;
 for(let i=0;i<4;i++){
   await req('playlist-step',{serial:d69.serial,playlistId:ids.D69,direction:1});
   const next=(await devices()).devices.find(d=>d.serial===d69.serial).themeId;
   assert.notEqual(next,last,'random immediate repeat');last=next;
 }
 console.log('RANDOM_AND_STEPPING_PASSED');
 snapshot=await req('library-load');
 const removed=snapshot.themes.find(t=>t.id===last);
 removed.deletedAt=Date.now();removed.inLibrary=false;
 await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(1700);
 assert.notEqual((await devices()).devices.find(d=>d.serial===d69.serial).themeId,last,'trash current skipped');
 snapshot=await req('library-load');const restore=snapshot.themes.find(t=>t.id===last);delete restore.deletedAt;restore.inLibrary=true;
 await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(1000);
 const result={at:new Date().toISOString(),devices:(await devices()).devices.map(d=>({serial:d.serial,playing:d.playing,playlistId:d.playlistId,remaining:d.playlistRemaining,frames:d.frames})),
   pause:true,independence:true,backgroundAfterPageChange:true,random:true,nextPrevious:true,devicePause:true,trashSkipRestore:true,ids};
 fs.writeFileSync(path.join(out,'native-results.json'),JSON.stringify(result,null,2));
 console.log(JSON.stringify(result));
 await req('select',{serial:d69.serial});await page.reload();await page.waitForTimeout(1200);
 await page.getByRole('button',{name:'轮播列表',exact:true}).click();
 await page.screenshot({path:path.join(out,'playlist-native.png')});
 await browser.close();
})().catch(error=>{console.error(error);process.exitCode=1;});
