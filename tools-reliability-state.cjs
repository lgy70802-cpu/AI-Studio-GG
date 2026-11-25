const fs=require('fs'),path=require('path'),assert=require('assert/strict');const{connect}=require('./tools-theme-common.cjs');
(async()=>{
 const mode=process.argv[2],{browser,page,req}=await connect(),out=path.join(__dirname,'artifacts/reliability-qa');
 if(mode==='prepare'){
   const snapshot=await req('library-load');await req('library-save',{snapshot});await req('schedule-enable',{enabled:false});
   for(const d of (await req('devices')).devices)await req('playlist-start',{serial:d.serial,playlistId:'qa-reliability-list-'+d.model});
   const state=await req('devices');fs.writeFileSync(path.join(out,'before-abnormal-exit.json'),JSON.stringify({...state,devices:state.devices.map(({preview,...d})=>d)},null,2));
 }else{
   await page.waitForTimeout(1500);const state=await req('devices');
   assert(state.devices.every(d=>d.playing&&d.playlistRunning));assert.equal(state.scheduling.enabled,false);
   for(const d of state.devices){assert.equal(d.playlistId,'qa-reliability-list-'+d.model);if(mode==='missing')assert(!['qa-composite-theme'].includes(d.themeId)&&d.videoDuration==null,'missing video/theme not skipped');}
   fs.writeFileSync(path.join(out,mode+'.json'),JSON.stringify({...state,devices:state.devices.map(({preview,...d})=>d)},null,2));
 }
 console.log('RELIABILITY_'+mode.toUpperCase()+'_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
