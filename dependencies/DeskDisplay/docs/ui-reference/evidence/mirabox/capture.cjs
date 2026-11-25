const {chromium}=require(process.env.TEMP+'/d92-prototype-qa/node_modules/playwright');
const fs=require('fs');
(async()=>{
 const browser=await chromium.launch({channel:'msedge'});
 try {
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 const out='E:/Codex/MLB/MLB-DeskDisplay/docs/ui-reference/evidence/mirabox';
 fs.mkdirSync(out,{recursive:true});
 await page.goto('http://127.0.0.1:4318');
 for(const [id,label] of Object.entries({overview:'首页',discover:'在线资源',library:'资源库',creator:'创作中心',playlists:'轮播列表',plugins:'插件中心',devices:'设备管理',account:'个人中心',settings:'系统设置'})){
  await page.locator('aside').getByRole('button',{name:label,exact:true}).first().click();
  await page.screenshot({path:out+'/'+id+'.png',animations:'disabled'});
 }
 await page.locator('aside').getByRole('button',{name:'创作中心',exact:true}).click();
 await page.getByRole('button',{name:'编辑主题',exact:true}).first().click();
 await page.screenshot({path:out+'/editor.png'});
 await page.locator('aside').getByRole('button',{name:'首页',exact:true}).click();
 const scenarios=page.locator('header select').first();
 for(const id of ['single92','single69','dual','mixed','empty']){
  await scenarios.selectOption(id);
  await page.screenshot({path:out+'/overview-'+id+'.png'});
 }
 fs.writeFileSync(out+'/runtime.json',JSON.stringify({viewport:[1440,1000],errors},null,2));
 if(errors.length)throw Error(errors.join('\n'));
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});

