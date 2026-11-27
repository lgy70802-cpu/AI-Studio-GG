// Read-only browser audit of the supplied local prototype. No client or hardware calls.
const {chromium}=require(process.env.TEMP+'/d92-prototype-qa/node_modules/playwright');
const fs=require('fs'); const path=require('path');
(async()=>{const out=path.join(__dirname,'evidence');fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto('file:///'+path.join(__dirname,'D92_Studio_Prototype_V6.3.html').replaceAll('\\','/'));
 const log=[];
 for(const scenario of ['single92','single69','dual','mixed','empty']){
  await page.locator('#scenario').selectOption(scenario);
  await page.screenshot({path:path.join(out,`prototype-home-${scenario}.png`)});
  log.push({scenario,text:(await page.locator('main').innerText()).slice(0,3000)});
 }
 await page.locator('#scenario').selectOption('single92');
 const nav=await page.locator('button.nav').evaluateAll(xs=>xs.map(x=>({text:x.innerText,onclick:x.getAttribute('onclick')})));
 for(const item of nav){if(!item.onclick?.startsWith('go('))continue;
  const loc=page.locator(`button.nav[onclick="${item.onclick}"]`).first();await loc.click();
  const key=item.onclick.match(/go\('([^']+)'/)[1];
  await page.screenshot({path:path.join(out,`prototype-${key}.png`)});
  log.push({page:key,text:(await page.locator('main').innerText()).slice(0,5000),buttons:await page.locator('main button').allTextContents()});
 }
 for(const [route,label,key] of [['library','回收站 · 0','trash'],['playlists','＋ 新建列表','new-playlist'],['playlists','＋ 添加内容','add-content'],['creator','＋ 新建主题','new-theme'],['devices','＋ 连接新设备','connect']]){
  await page.locator(`button.nav[onclick="go('${route}')"]`).first().click();
  await page.getByRole('button',{name:label,exact:true}).click();
  await page.screenshot({path:path.join(out,`prototype-dialog-${key}.png`)});
  log.push({dialog:key,text:await page.locator('#dialog').innerText()});
  await page.evaluate(()=>closeModal());
 }
 await page.locator('button.nav[onclick="go(\'library\')"]').first().click();
 await page.locator('main button').filter({hasText:/^查看$/}).first().click({force:true});
 await page.screenshot({path:path.join(out,'prototype-dialog-detail.png')});log.push({dialog:'detail',text:await page.locator('#dialog').innerText()});
 await page.evaluate(()=>closeModal());
 await page.locator('button.nav[onclick="go(\'creator\')"]').first().click();
 await page.locator('main button').filter({hasText:/^编辑$/}).first().click({force:true});
 await page.screenshot({path:path.join(out,'prototype-editor.png')});log.push({page:'editor',text:await page.locator('main').innerText()});
 fs.writeFileSync(path.join(out,'prototype-audit.json'),JSON.stringify({viewport:{width:1440,height:1000},nav,log,errors},null,2));
 await browser.close();console.log(JSON.stringify({pages:log.length,errors}));
})();
