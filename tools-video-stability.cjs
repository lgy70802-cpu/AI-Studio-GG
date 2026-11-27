const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{const id=crypto.randomUUID();const timer=setTimeout(()=>{window.chrome.webview.removeEventListener('message',fn);reject(Error('timeout '+command));},60000);const fn=e=>{if(e.data.id!==id)return;clearTimeout(timer);window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command,...payload});}),{command,payload});
 const snapshot=await req('library-load'),video=snapshot.themes.find(t=>t.category==='视频'),devices=(await req('devices')).devices;
 const results={at:new Date().toISOString(),checks:[],samples:[]};
 for(const d of devices){await req('playlist-pause',{serial:d.serial});await req('apply-media',{serial:d.serial,themeId:video.id});await req('configure',{serial:d.serial,portrait:true,flipped:true});await page.waitForTimeout(1200);const live=(await req('devices')).devices.find(x=>x.serial===d.serial);assert(live.playing&&live.orientation==='portrait'&&live.flipped);await req('configure',{serial:d.serial,portrait:false,flipped:false});results.checks.push(d.model+' portrait/flip/native first frame');}
 await assert.rejects(req('video-begin',{name:'bad.exe',size:1}));
 await assert.rejects(req('video-begin',{name:'big.mp4',size:500*1024*1024+1}));
 let uploadId=await req('video-begin',{name:'incomplete.mp4',size:10});await req('video-chunk',{uploadId,data:'AQID'});await assert.rejects(req('video-finish',{uploadId}));
 uploadId=await req('video-begin',{name:'cancel.mp4',size:10});await req('video-cancel',{uploadId});await assert.rejects(req('video-finish',{uploadId}));
 results.checks.push('extension/size/truncated/cancel rejected and cleaned');
 snapshot.schedules=snapshot.schedules.filter(r=>!r.id.startsWith('qa-video-schedule-'));
 for(const d of devices)snapshot.schedules.push({id:'qa-video-schedule-'+d.model,title:'验收_视频混播_'+d.model,time:'23:59',deviceId:d.serial,orientation:'landscape',repeatMode:'daily',days:'每天',actionType:d.model==='D69'?'theme':'playlist',themeId:video.id,playlistId:'qa-video-mixed-'+d.model,enabled:false});
 await req('library-save',{snapshot});await req('schedule-enable',{enabled:false});await page.reload();await page.waitForTimeout(1200);
 for(const d of devices){const run=await req('schedule-run',{ruleId:'qa-video-schedule-'+d.model});assert(run.history.some(h=>h.ruleId==='qa-video-schedule-'+d.model&&h.manual&&h.status==='成功'));await req('playlist-start',{serial:d.serial,playlistId:'qa-video-mixed-'+d.model});}
 results.checks.push('manual schedules: standalone video D69 / mixed playlist D92, USB first frame');
 console.log('ORIENTATION_IMPORT_GUARDS_SCHEDULE_PASSED; START_180_SECONDS');
 const start=Date.now();while(Date.now()-start<180000){const state=await req('devices');results.samples.push({elapsed:(Date.now()-start)/1000,devices:state.devices.map(({preview,...d})=>d)});await new Promise(r=>setTimeout(r,2000));}
 for(const d of devices){const samples=results.samples.map(s=>s.devices.find(x=>x.serial===d.serial));assert(samples.every(x=>x.status==='已连接'));assert(samples.filter(x=>x.playing).length/samples.length>0.85);assert(new Set(samples.map(x=>x.themeId)).size===2,'no mixed transitions '+d.model);const moving=samples.filter(x=>x.videoFps>0);assert(moving.length>10,'insufficient video frames');results[d.model]={samples:samples.length,videoSamples:moving.length,videoFpsMedian:moving.map(x=>x.videoFps).sort((a,b)=>a-b)[Math.floor(moving.length/2)],transitions:samples.slice(1).filter((x,i)=>x.themeId!==samples[i].themeId).length,statuses:[...new Set(samples.map(x=>x.outputStatus))]};}
 fs.writeFileSync(path.join(__dirname,'artifacts/video-qa/stability.json'),JSON.stringify(results,null,2));console.log(JSON.stringify({checks:results.checks,D69:results.D69,D92:results.D92}));await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
