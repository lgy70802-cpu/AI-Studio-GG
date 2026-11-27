const fs = require('fs');
const path = require('path');
const http = require('http');
const assert = require('assert/strict');
const { chromium } = require('playwright');
const root = path.resolve(__dirname);
const output = path.join(root, 'artifacts/library-qa');
const dist = path.join(root, 'd-studio/dist');
fs.mkdirSync(output,{recursive:true});
if (!fs.existsSync(path.join(output,'验收_日落.jpg')))
  fs.copyFileSync(path.join(root,'d-studio/src/assets/images/theme_sunset_skyline_1795333384132.jpg'),path.join(output,'验收_日落.jpg'));
fs.writeFileSync(path.join(output,'损坏图片.png'),'invalid image fixture');
fs.copyFileSync(path.join(root,'d-studio/src/assets/images/theme_miniature_bedroom_1795333386709.jpg'),path.join(output,'验收_第二张.jpg'));
const media = path.join(process.env.LOCALAPPDATA, 'MiraBoxStudio.Next/Library');
const mime = {'.html':'text/html', '.js':'text/javascript', '.css':'text/css', '.png':'image/png', '.jpg':'image/jpeg'};
const server = http.createServer((req, res) => {
  const name = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
  const file = path.resolve(dist, '.' + (name === '/' ? '/index.html' : name));
  if (!file.startsWith(dist + path.sep) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
  res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
});
(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await chromium.launch({channel:'msedge', headless:true});
  const results = [];
  try {
    for (const layout of [{width:1440,height:900,dpr:1},{width:960,height:800,dpr:1.25},{width:720,height:800,dpr:1.5}]) {
      const context = await browser.newContext({viewport:{width:layout.width,height:layout.height},deviceScaleFactor:layout.dpr});
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.route('https://media.studio.local/**', route => {
        const file = path.join(media, path.basename(new URL(route.request().url()).pathname));
        return route.fulfill({body:fs.readFileSync(file),contentType:mime[path.extname(file)] || 'image/png'});
      });
      await page.addInitScript(() => {
        window.qaSnapshot = JSON.parse(sessionStorage.getItem('qa-snapshot') || 'null');
        window.qaDevice = {id:'QA-D69',serial:'QA-D69',model:'D69',name:'D69 · QA',status:'已连接',nativeWidth:1280,nativeHeight:480,resolution:'1280 × 480',orientation:'landscape',flipped:false,brightness:null,sleepMinutes:0,refreshRate:'—',firmware:'—'};
        const listeners = [];
        window.chrome = {...window.chrome, webview:{
          addEventListener(type, listener) { listeners.push(listener); },
          postMessage(message) {
            let result;
            if (message.command === 'devices') result = {selected:'QA-D69',restore:true,devices:[window.qaDevice]};
            if (message.command === 'library-load') result = window.qaSnapshot;
            if (message.command === 'library-save') { window.qaSnapshot = structuredClone(message.snapshot); sessionStorage.setItem('qa-snapshot', JSON.stringify(window.qaSnapshot)); result = window.qaSnapshot.themes; }
            if (message.command === 'apply') { Object.assign(window.qaDevice,{themeId:message.themeId,themeName:message.themeName,preview:message.image,playing:true,wantsPlaying:true}); result = true; }
            setTimeout(() => listeners.forEach(listener => listener({data:{id:message.id,result}})),0);
          }
        }};
      });
      await page.goto('http://127.0.0.1:' + server.address().port);
      if (await page.getByRole('button',{name:'打开菜单',exact:true}).isVisible())
        await page.getByRole('button',{name:'打开菜单',exact:true}).click();
      await page.getByRole('button',{name:'资源库',exact:true}).click();
      await page.locator('input[type=file]').setInputFiles([
        path.join(output,'验收_日落.jpg'), path.join(output,'损坏图片.png')
      ]);
      await page.waitForFunction(() => window.qaSnapshot?.themes.some(t=>t.source === 'local'));
      const drop = await page.evaluateHandle(base64 => {
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const transfer = new DataTransfer();
        transfer.items.add(new File([bytes], '拖放验收.jpg', {type:'image/jpeg'}));
        return transfer;
      }, fs.readFileSync(path.join(output,'验收_日落.jpg')).toString('base64'));
      await page.getByRole('region',{name:'资源库',exact:true}).dispatchEvent('drop', {dataTransfer:drop});
      await page.waitForTimeout(200);
      assert.equal(await page.evaluate(()=>window.qaSnapshot.themes.filter(t=>t.source==='local').length),1);
      const id = await page.evaluate(() => window.qaSnapshot.themes.find(t=>t.source==='local').id);
      const search = page.getByRole('textbox',{name:'搜索主题与壁纸'});
      await search.fill('验收_');
      const rename = page.getByRole('button',{name:'重命名',exact:true});
      await rename.click();
      await page.getByRole('textbox').last().fill('验收_UI已改名');
      await page.getByRole('button',{name:'保存名称',exact:true}).click();
      await page.getByRole('button',{name:'收藏',exact:true}).click();
      await page.waitForFunction(id => window.qaSnapshot.themes.find(t=>t.id===id)?.isFavorite, id);
      await page.getByRole('button',{name:'应用到 D69',exact:true}).click();
      await page.waitForFunction(id => window.qaDevice.themeId === id, id);
      await page.getByRole('button',{name:'移出资源库',exact:true}).click();
      await page.waitForFunction(id => window.qaSnapshot.themes.find(t=>t.id===id)?.deletedAt, id);
      assert.equal(await page.evaluate(()=>window.qaDevice.playing),true);
      await page.getByRole('button',{name:/^回收站/}).click();
      await page.getByRole('button',{name:'恢复',exact:true}).click();
      await page.waitForFunction(id => !window.qaSnapshot.themes.find(t=>t.id===id)?.deletedAt, id);
      await page.getByRole('button',{name:'关闭回收站'}).click();
      await page.getByRole('button',{name:'本地导入',exact:true}).click();
      await page.getByRole('button',{name:'新添加',exact:true}).click();
      await page.getByRole('button',{name:'仅看收藏',exact:true}).click();
      await page.locator('input[type=file]').setInputFiles(path.join(output,'验收_日落.jpg'));
      await page.waitForTimeout(300);
      assert.equal(await page.evaluate(()=>window.qaSnapshot.themes.filter(t=>t.source==='local').length),1);
      await page.reload();
      if (await page.getByRole('button',{name:'打开菜单',exact:true}).isVisible())
        await page.getByRole('button',{name:'打开菜单',exact:true}).click();
      await page.getByRole('button',{name:'资源库',exact:true}).click();
      assert.equal(await search.inputValue(),'验收_');
      assert.equal(await page.getByRole('button',{name:'仅看收藏',exact:true}).getAttribute('aria-pressed'),'true');
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
      assert.equal(overflow,false,'viewport horizontal overflow');
      await page.screenshot({path:path.join(output,'library-'+layout.width+'-'+layout.dpr+'.png')});
      await page.getByRole('button',{name:'仅看收藏',exact:true}).click();
      await page.locator('input[type=file]').setInputFiles(path.join(output,'验收_第二张.jpg'));
      await page.waitForFunction(()=>window.qaSnapshot.themes.filter(t=>t.source==='local').length===2);
      await page.getByRole('button',{name:'批量管理',exact:true}).click();
      await page.getByRole('button',{name:'全选当前',exact:true}).click();
      await page.getByRole('button',{name:'批量收藏',exact:true}).click();
      await page.waitForFunction(()=>window.qaSnapshot.themes.filter(t=>t.source==='local'&&t.isFavorite).length===2);
      await page.getByRole('button',{name:'移出资源库',exact:true}).click();
      await page.waitForFunction(()=>window.qaSnapshot.themes.filter(t=>t.source==='local'&&t.deletedAt).length===2);
      await page.getByRole('button',{name:/^回收站/}).click();
      await page.getByRole('button',{name:'全部恢复',exact:true}).click();
      await page.waitForFunction(()=>window.qaSnapshot.themes.filter(t=>t.source==='local'&&!t.deletedAt&&t.inLibrary).length===2);
      assert.deepEqual(errors,[]);
      results.push({...layout,import:true,rename:true,favorite:true,apply:true,trash:true,restore:true,batchFavorite:true,batchTrashRestore:true,horizontalOverflow:overflow,pageErrors:errors});
      await context.close();
    }
    fs.writeFileSync(path.join(output,'ui-results.json'),JSON.stringify(results,null,2));
    console.log(JSON.stringify(results,null,2));
  } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); server.close(); process.exitCode=1; });
