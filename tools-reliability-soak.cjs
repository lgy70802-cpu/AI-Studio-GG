const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');const{connect}=require('./tools-theme-common.cjs');
(async()=>{
 const seconds=Number(process.argv[2]||7200),tag=process.argv[3]||'';
 assert(Number.isFinite(seconds)&&seconds>0,'duration must be positive');assert(/^[a-zA-Z0-9_-]*$/.test(tag),'invalid run tag');
 const{browser,page,req}=await connect(),out=path.join(__dirname,'artifacts/reliability-qa',tag);fs.mkdirSync(out,{recursive:true});
 const samples=[],problems=[],started=performance.now(),at=new Date().toISOString(),badSince=new Map(),frozenSince=new Map();let maxRenderers=0,lastSave=0,maxGap=0,lastAt=0;
 const index=await req('library-load');
 for(const d of (await req('devices')).devices){await req('device-power',{serial:d.serial,brightness:100,sleepMinutes:0,action:'wake'});await req('playlist-start',{serial:d.serial,playlistId:'qa-reliability-list-'+d.model});}
 const log=path.join(out,'long-soak.jsonl');fs.writeFileSync(log,'');
 console.log('LONG_SOAK_STARTED '+at+' target='+seconds+'s');
 try{
 while((performance.now()-started)/1000<seconds){
   const state=await req('devices'),elapsed=(performance.now()-started)/1000,renderers=browser.contexts()[0].pages().filter(p=>p.url().includes('device-render')).length;
   maxRenderers=Math.max(maxRenderers,renderers);maxGap=Math.max(maxGap,elapsed-lastAt);lastAt=elapsed;
   const entries=state.devices.map(({preview,...d})=>({...d,previewHash:crypto.createHash('sha256').update(preview||'').digest('hex')}));
   const sample={elapsed,renderers,devices:entries};samples.push(sample);fs.appendFileSync(log,JSON.stringify(sample)+'\n');
   for(const d of entries){
     const bad=d.status!=='已连接'||!d.wantsPlaying||!d.playlistRunning||!d.playing;
     if(bad){if(!badSince.has(d.serial))badSince.set(d.serial,elapsed);if(elapsed-badSince.get(d.serial)>20)throw Error('persistent output failure '+d.model+' '+d.outputStatus);}else badSince.delete(d.serial);
     const previous=samples.length>1?samples.at(-2).devices.find(x=>x.serial===d.serial):null;
     const dynamic=index.themes.find(t=>t.id===d.themeId)?.category!=='壁纸';
     const sameDynamic = d.videoDuration != null ? previous?.videoPosition === d.videoPosition && (d.frames <= previous.frames || (d.videoFps ?? 0) < 2) : previous?.previewHash === d.previewHash;
     if(dynamic&&previous?.themeId===d.themeId&&sameDynamic&&d.playing){if(!frozenSince.has(d.serial))frozenSince.set(d.serial,elapsed);if(elapsed-frozenSince.get(d.serial)>15)throw Error('frozen dynamic content '+d.model);}else frozenSince.delete(d.serial);
   }
   if(renderers>4)throw Error('background renderer pages accumulated');
   if(elapsed-lastSave>60){lastSave=elapsed;fs.writeFileSync(path.join(out,'long-progress.json'),JSON.stringify({at,elapsed,samples:samples.length,maxRenderers,maxGap,last:sample},null,2));}
   await page.waitForTimeout(5000);
 }
 const summary={at,seconds:(performance.now()-started)/1000,samples:samples.length,maxRenderers,maxGap,devices:[]};assert(summary.maxGap<30,'measurement interrupted by a long scheduling gap');
 for(const d of samples[0].devices){const entries=samples.map(s=>s.devices.find(x=>x.serial===d.serial)),transitions=entries.slice(1).filter((x,i)=>x.themeId!==entries[i].themeId).length;assert(new Set(entries.map(x=>x.themeId)).size===3);assert(transitions>=Math.floor(seconds/90));const composite=entries.filter(x=>x.renderFps>0).map(x=>x.renderFps).sort((a,b)=>a-b),video=entries.filter(x=>x.videoFps>0).map(x=>x.videoFps).sort((a,b)=>a-b);summary.devices.push({model:d.model,transitions,compositeMedianFps:composite[Math.floor(composite.length/2)],videoMedianFps:video[Math.floor(video.length/2)],connectedSamples:entries.filter(x=>x.status==='已连接').length,playingSamples:entries.filter(x=>x.playing).length,statuses:[...new Set(entries.map(x=>x.outputStatus))]});}
 fs.writeFileSync(path.join(out,'long-soak.json'),JSON.stringify(summary,null,2));console.log('LONG_SOAK_PASSED '+JSON.stringify(summary));
 }catch(error){problems.push({at:new Date().toISOString(),elapsed:(performance.now()-started)/1000,error:error.message});fs.writeFileSync(path.join(out,'long-soak-failure.json'),JSON.stringify(problems,null,2));throw error;}
 finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
