const fs=require('fs'),path=require('path'),assert=require('assert/strict');const {connect}=require('./tools-theme-common.cjs');
(async()=>{
 const {browser,page,req}=await connect(),out=path.join(__dirname,'artifacts/theme-qa'),ui=JSON.parse(fs.readFileSync(path.join(out,'editor-ui.json'))),theme=(await req('library-load')).themes.find(t=>t.id===ui.savedId);
 await page.getByRole('button',{name:'创作中心',exact:true}).click();await page.getByLabel('搜索我的作品').fill(theme.displayName);await page.locator('div.flex.flex-col.overflow-hidden.rounded-2xl').filter({has:page.getByText(theme.displayName,{exact:true})}).getByRole('button',{name:'编辑',exact:true}).click();if(await page.getByRole('button',{name:'忽略草稿',exact:true}).isVisible())await page.getByRole('button',{name:'忽略草稿',exact:true}).click();
 const name=page.getByLabel('主题名称',{exact:true});await name.fill('验收_正在提交的版本');await page.getByRole('button',{name:'应用',exact:true}).click();await name.fill('验收_提交期间的新修改');await page.waitForTimeout(2000);
 assert.equal((await req('library-load')).themes.find(t=>t.id===theme.id).displayName,'验收_正在提交的版本');assert.equal(await name.inputValue(),'验收_提交期间的新修改');assert(!(await name.locator('..').innerText()).includes('已同步'));
 await name.fill(theme.displayName);await page.getByRole('button',{name:'保存',exact:true}).click();await page.waitForTimeout(1000);assert.equal((await req('library-load')).themes.find(t=>t.id===theme.id).displayName,theme.displayName);
 fs.writeFileSync(path.join(out,'save-concurrent.json'),JSON.stringify({submittedVersionStored:true,newEditsRetained:true,newEditsRemainDirty:true,nextSaveSucceeded:true},null,2));console.log('SAVE_CONCURRENT_EDITS_REMAIN_DIRTY_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
