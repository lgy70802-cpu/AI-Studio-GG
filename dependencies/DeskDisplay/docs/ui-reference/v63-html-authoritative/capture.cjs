const {chromium}=require(process.env.TEMP+'/d92-prototype-qa/node_modules/playwright');
const fs=require('fs');
(async()=>{
 const browser=await chromium.launch({channel:'msedge'});
 try {
  const page=await browser.newPage({viewport:{width:1440,height:1000},deviceScaleFactor:1});
  const out='E:/Codex/MLB/MLB-DeskDisplay/docs/ui-reference/evidence/html-v63';fs.mkdirSync(out,{recursive:true});
  await page.goto('file:///E:/Codex/MLB/MLB-DeskDisplay/docs/ui-reference/v63-html-authoritative/Prototype.html');
  for(const id of ['overview','discover','library','creator','playlists','plugins','devices','account','settings']){
   await page.evaluate(id=>go(id),id);await page.screenshot({path:out+'/'+id+'.png',animations:'disabled'});
  }
  for(const id of ['single92','single69','dual','mixed','empty']){
   await page.evaluate(id=>{scenario(id);go('overview')},id);await page.screenshot({path:out+'/home-'+id+'.png',animations:'disabled'});
  }
  fs.writeFileSync(out+'/capture.json',JSON.stringify({viewport:[1440,1000],deviceScaleFactor:1,source:'v63-html-authoritative/Prototype.html',kind:'HTML reference screenshots; not WPF'},null,2));
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
