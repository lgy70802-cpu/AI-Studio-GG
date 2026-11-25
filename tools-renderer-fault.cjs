const fs=require('fs'),path=require('path'),assert=require('assert/strict');const{connect}=require('./tools-theme-common.cjs');
(async()=>{
 let {browser,page,req}=await connect();const before=(await req('devices')).devices;
 const mainMode=process.argv[2]==='main';const target=browser.contexts()[0].pages().find(p=>mainMode?p.url().includes('studio.local')&&!p.url().includes('device-render'):p.url().includes('device-render'));assert(target);
 const cdp=await target.context().newCDPSession(target);const at=Date.now();
 await Promise.race([cdp.send('Page.crash').catch(()=>{}),new Promise(resolve=>setTimeout(resolve,2500))]);await browser.close();
 let state,healthy=false;
 for(let i=0;i<45;i++){
   await new Promise(resolve=>setTimeout(resolve,1000));
   try {({browser,page,req}=await connect());state=await req('devices');healthy=state.devices.every(d=>d.playing&&!/失败|中断/.test(d.outputStatus))&&browser.contexts()[0].pages().filter(p=>p.url().includes('device-render')).length===2;if(healthy)break;await browser.close();}catch{}
 }
 assert(healthy,'renderer crash did not recover');const first=state.devices;await page.waitForTimeout(2300);state=await req('devices');
 for(const d of state.devices){assert.notEqual(d.preview,first.find(x=>x.serial===d.serial).preview);assert.equal(d.themeId,before.find(x=>x.serial===d.serial).themeId);}
 fs.writeFileSync(path.join(__dirname,'artifacts/reliability-qa/'+(mainMode?'main-ui-fault.json':'renderer-fault.json')),JSON.stringify({forcedRendererCrash:true,recovered:true,seconds:(Date.now()-at)/1000,devices:state.devices.map(({preview,...d})=>d)},null,2));console.log('FORCED_RENDERER_CRASH_RECOVERY_PASSED '+((Date.now()-at)/1000).toFixed(1)+'s');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
