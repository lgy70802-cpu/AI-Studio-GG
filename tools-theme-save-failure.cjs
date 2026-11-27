const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {connect}=require('./tools-theme-common.cjs');
(async()=>{
 const {browser,page,req}=await connect(),out=path.join(__dirname,'artifacts/theme-qa'),ui=JSON.parse(fs.readFileSync(path.join(out,'editor-ui.json'))),theme=(await req('library-load')).themes.find(t=>t.id===ui.savedId);
 await page.reload();await page.waitForTimeout(500);await page.getByRole('button',{name:'创作中心',exact:true}).click();await page.getByLabel('搜索我的作品').fill(theme.displayName);await page.locator('div.flex.flex-col.overflow-hidden.rounded-2xl').filter({has:page.getByText(theme.displayName,{exact:true})}).getByRole('button',{name:'编辑',exact:true}).click();
 if(await page.getByRole('button',{name:'忽略草稿',exact:true}).isVisible())await page.getByRole('button',{name:'忽略草稿',exact:true}).click();
 const project=structuredClone(theme.editorProject);project.name='验收_保存失败不清除草稿';project.master.find(e=>e.id==='image-ui').src='blob:expired';const file=path.join(out,'invalid-project.json');fs.writeFileSync(file,JSON.stringify(project));
 await page.locator('input[type=file][accept=".json,.d92theme.json"]').setInputFiles(file);await page.waitForTimeout(300);const index=path.join(process.env.LOCALAPPDATA,'MiraBoxStudio.Next/library-workspace.json'),before=fs.readFileSync(index,'utf8');
 await page.getByRole('button',{name:'保存',exact:true}).click();await page.waitForTimeout(1200);assert.equal(fs.readFileSync(index,'utf8'),before);
 assert(!(await page.getByLabel('主题名称',{exact:true}).locator('..').innerText()).includes('已同步'));
 await page.screenshot({path:path.join(out,'save-failure.png')});
 fs.writeFileSync(file,JSON.stringify(theme.editorProject));await page.locator('input[type=file][accept=".json,.d92theme.json"]').setInputFiles(file);await page.waitForTimeout(200);await page.getByRole('button',{name:'保存',exact:true}).click();await page.waitForTimeout(1000);assert.equal((await req('library-load')).themes.find(t=>t.id===theme.id).displayName,theme.displayName);
 fs.writeFileSync(path.join(out,'save-failure.json'),JSON.stringify({failedSaveRetainsIndex:true,failedSaveRetainsDirtyDraft:true,validRetry:true},null,2));
 await page.reload();await page.waitForTimeout(500);await page.getByRole('button',{name:'首页',exact:true}).click();console.log('SAVE_FAILURE_DRAFT_INDEX_RETRY_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
