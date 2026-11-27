const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {connect}=require('./tools-theme-common.cjs');
(async()=>{
 const {browser,page,req}=await connect(),out=path.join(__dirname,'artifacts/theme-qa'),checks=[];
 let snapshot=await req('library-load');const devices=(await req('devices')).devices,d69=devices.find(d=>d.model==='D69');
 for(const d of devices){await req('configure',{serial:d.serial,portrait:false,flipped:false});await req('apply-media',{serial:d.serial,themeId:'qa-live-theme'});}
 for(const d of devices){await req('configure',{serial:d.serial,portrait:true,flipped:true});let current=(await req('devices')).devices.find(x=>x.serial===d.serial);assert(current.playing&&current.orientation==='portrait'&&current.flipped);fs.writeFileSync(path.join(out,d.model+'-portrait.jpg'),Buffer.from(current.preview.split(',')[1],'base64'));await req('configure',{serial:d.serial,portrait:false,flipped:false});}
 checks.push('dual portrait/flip and landscape USB first frames');
 await req('configure',{serial:d69.serial,playing:false});let paused=(await req('devices')).devices;await page.waitForTimeout(2400);let later=(await req('devices')).devices;
 assert.equal(paused.find(d=>d.model==='D69').preview,later.find(d=>d.model==='D69').preview);assert.notEqual(paused.find(d=>d.model==='D92').preview,later.find(d=>d.model==='D92').preview);
 await req('configure',{serial:d69.serial,playing:true});await page.waitForTimeout(2000);assert((await req('devices')).devices.find(d=>d.model==='D69').playing);checks.push('pause holds D69 preview while D92 clock updates; resume');
 const theme=structuredClone(snapshot.themes.find(t=>t.id==='qa-live-theme'));theme.id='qa-clock-styles';theme.displayName='验收_时钟样式';
 for(const style of ['digital','flip','minimal','analog']){
  theme.editorProject.master.find(e=>e.kind==='clock').style=style;
  snapshot.themes=snapshot.themes.filter(t=>t.id!==theme.id);snapshot.themes.push(structuredClone(theme));await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(300);await req('apply-media',{serial:d69.serial,themeId:theme.id});await page.waitForTimeout(1000);
  const current=(await req('devices')).devices.find(d=>d.model==='D69');assert(current.playing);fs.writeFileSync(path.join(out,'style-'+style+'.jpg'),Buffer.from(current.preview.split(',')[1],'base64'));
 }checks.push('digital/flip/minimal/analog original styles USB output');
 assert.equal(browser.contexts()[0].pages().filter(p=>p.url().includes('device-render')).length,2);checks.push('repeated changes release previous renderer pages');
 const photo=snapshot.themes.find(t=>t.category==='壁纸'&&t.inLibrary!==false&&!t.deletedAt),video=snapshot.themes.find(t=>t.category==='视频');
 for(const d of devices){const id='qa-theme-mixed-'+d.model;snapshot.playlists=snapshot.playlists.filter(p=>p.id!==id);snapshot.playlists.push({id,name:'验收_'+d.model+'主题图片视频混播',deviceId:d.serial,orientation:'landscape',mood:'专注',shuffle:false,defaultDurationSeconds:10,items:[{themeId:'qa-live-theme',durationSeconds:10},{themeId:photo.id,durationSeconds:10},{themeId:video.id,durationSeconds:10}]});}
 const due=new Date();due.setMinutes(due.getMinutes()+1);due.setSeconds(0,0);const time=String(due.getHours()).padStart(2,'0')+':'+String(due.getMinutes()).padStart(2,'0');
 snapshot.schedules=snapshot.schedules.filter(r=>!r.id.startsWith('qa-theme-schedule-'));
 for(const d of devices)snapshot.schedules.push({id:'qa-theme-schedule-'+d.model,title:'验收_主题定时_'+d.model,time,deviceId:d.serial,orientation:'landscape',repeatMode:'once',days:'仅一次',actionType:d.model==='D69'?'theme':'playlist',themeId:'qa-live-theme',playlistId:'qa-theme-mixed-'+d.model,enabled:true});
 await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(500);
 for(const d of devices)await req('playlist-start',{serial:d.serial,playlistId:'qa-theme-mixed-'+d.model});
 await req('schedule-enable',{enabled:true});console.log('ORIENTATION_PAUSE_STYLES_PASSED; AUTOMATIC_DUE '+due.toISOString());
 while(Date.now()<due.getTime()+4500)await page.waitForTimeout(1000);
 const state=await req('devices');for(const d of devices)assert(state.scheduling.history.some(h=>h.ruleId==='qa-theme-schedule-'+d.model&&!h.manual&&h.status==='成功'));
 await req('schedule-enable',{enabled:false});snapshot=await req('library-load');snapshot.schedules=snapshot.schedules.map(r=>r.id.startsWith('qa-theme-schedule-')?{...r,enabled:false}:r);await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(500);
 for(const d of devices)await req('playlist-start',{serial:d.serial,playlistId:'qa-theme-mixed-'+d.model});
 checks.push('real-minute automatic standalone clock D69 and mixed playlist D92, first-frame receipts');
 fs.writeFileSync(path.join(out,'runtime.json'),JSON.stringify({checks,scheduling:state.scheduling},null,2));console.log('THEME_AUTOMATIC_SCHEDULE_AND_MIXED_START_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
