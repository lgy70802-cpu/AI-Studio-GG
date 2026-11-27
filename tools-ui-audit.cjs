const fs=require('fs'),path=require('path'),assert=require('assert/strict');const{connect}=require('./tools-theme-common.cjs');
(async()=>{
 const{browser,page,req}=await connect(9228),out=path.join(__dirname,'artifacts/reliability-qa/ui-audit');fs.mkdirSync(out,{recursive:true});
 const cdp=await page.context().newCDPSession(page),results=[];
 for(const section of ['首页','在线资源','资源库','创作中心','轮播列表','定时切换','设备管理','设置','插件中心']){
   await cdp.send('Emulation.clearDeviceMetricsOverride');await page.getByRole('button',{name:section,exact:true}).click();await page.waitForTimeout(200);
   if(['在线资源','插件中心'].includes(section))assert(await page.getByRole('status').filter({hasText:/示例|演示/}).count()>0);
   for(const width of [720,1024,1440]){
     await cdp.send('Emulation.setDeviceMetricsOverride',{width,height:900,deviceScaleFactor:width===1024?1.5:1,mobile:false});await page.waitForTimeout(150);
     const layout=await page.evaluate(()=>({width:innerWidth,documentWidth:document.documentElement.scrollWidth,height:innerHeight}));assert(layout.documentWidth<=layout.width+2,'global horizontal overflow '+section+' '+width);
     const buttons=await page.getByRole('button').evaluateAll(nodes=>nodes.filter(n=>n.getBoundingClientRect().width>0).map(n=>({text:n.getAttribute('aria-label')||n.getAttribute('title')||n.textContent.trim(),disabled:n.disabled})));
     results.push({section,width,deviceScaleFactor:width===1024?1.5:1,layout,buttons});await page.screenshot({path:path.join(out,section+'-'+width+'.png')});
   }
 }
 await cdp.send('Emulation.clearDeviceMetricsOverride');assert((await req('devices')).devices.every(d=>!d.wantsPlaying));
 fs.writeFileSync(path.join(out,'layout-controls.json'),JSON.stringify({checks:results.length,emulatedDpi:true,results},null,2));console.log('UI_LAYOUT_CONTROLS_AUDIT_PASSED '+results.length);await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
