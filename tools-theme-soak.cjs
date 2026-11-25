const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {connect}=require('./tools-theme-common.cjs');
(async()=>{
 const {browser,page,req}=await connect(),samples=[],seconds=Number(process.argv[2]||180),start=Date.now();
 while(Date.now()-start<seconds*1000){const state=await req('devices'),renderers=browser.contexts()[0].pages().filter(p=>p.url().includes('device-render')).length;samples.push({seconds:(Date.now()-start)/1000,renderers,devices:state.devices.map(({preview,...d})=>d)});await page.waitForTimeout(2000);}
 const summary={seconds:(Date.now()-start)/1000,samples:samples.length,maxRenderers:Math.max(...samples.map(s=>s.renderers)),devices:[]};assert(summary.maxRenderers<=4,'renderer pages leaked');
 for(const d of samples[0].devices){const list=samples.map(s=>s.devices.find(x=>x.serial===d.serial));assert(list.every(x=>x.status==='已连接'&&x.playlistRunning));assert(new Set(list.map(d=>d.themeId)).size===3,'three media kinds not visited');const transitions=list.slice(1).filter((d,i)=>d.themeId!==list[i].themeId).length;assert(transitions>=10);const fps=list.filter(d=>d.videoFps>0).map(d=>d.videoFps).sort((a,b)=>a-b);summary.devices.push({model:d.model,transitions,videoMedianFps:fps[Math.floor(fps.length/2)],statuses:[...new Set(list.map(x=>x.outputStatus))]});}
 fs.writeFileSync(path.join(__dirname,'artifacts/theme-qa/soak.json'),JSON.stringify({summary,samples},null,2));console.log(JSON.stringify(summary));await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
