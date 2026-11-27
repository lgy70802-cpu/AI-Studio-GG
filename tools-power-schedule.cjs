const fs=require('fs'),path=require('path'),assert=require('assert/strict');const{connect}=require('./tools-theme-common.cjs');
(async()=>{
 const{browser,page,req}=await connect(),snapshot=await req('library-load'),devices=(await req('devices')).devices;
 const original=await req('desktop-preferences');await req('desktop-autostart',{enabled:!original.autoStart});assert.equal((await req('desktop-preferences')).autoStart,!original.autoStart);await req('desktop-autostart',{enabled:original.autoStart});
 snapshot.schedules=snapshot.schedules.filter(r=>!r.id.startsWith('qa-power-'));
 for(const d of devices)for(const actionType of ['sleep','wake'])snapshot.schedules.push({id:'qa-power-'+d.model+'-'+actionType,title:'验收_软件画面_'+actionType,deviceId:d.serial,orientation:'landscape',time:'23:59',repeatMode:'daily',days:'每天',actionType,enabled:false});
 await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(500);
 for(const d of devices){await req('schedule-run',{ruleId:'qa-power-'+d.model+'-sleep'});await page.waitForTimeout(1300);assert((await req('devices')).devices.find(x=>x.serial===d.serial).standby);await req('schedule-run',{ruleId:'qa-power-'+d.model+'-wake'});await page.waitForTimeout(500);assert((await req('devices')).devices.find(x=>x.serial===d.serial).playing);}
 fs.writeFileSync(path.join(__dirname,'artifacts/reliability-qa/power-schedules.json'),JSON.stringify({autostartRoundtrip:true,originalAutostartRestored:true,softwareSleepWakeSchedules:true,history:(await req('devices')).scheduling.history.filter(h=>h.ruleId.startsWith('qa-power-'))},null,2));console.log('AUTOSTART_SOFTWARE_POWER_SCHEDULES_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
