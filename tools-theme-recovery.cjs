const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {connect}=require('./tools-theme-common.cjs');
(async()=>{
 const mode=process.argv[2],{browser,page,req}=await connect(),out=path.join(__dirname,'artifacts/theme-qa'),ui=JSON.parse(fs.readFileSync(path.join(out,'editor-ui.json')));
 const snapshot=await req('library-load'),theme=snapshot.themes.find(t=>t.id===ui.savedId);assert(theme?.editorProject);
 if(mode==='prepare'){
  for(const d of (await req('devices')).devices){const list=snapshot.playlists.find(p=>p.id==='qa-theme-mixed-'+d.model);list.items[0]={themeId:theme.id,durationSeconds:30};}
  await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(500);
  for(const d of (await req('devices')).devices){await req('playlist-start',{serial:d.serial,playlistId:'qa-theme-mixed-'+d.model});for(let i=0;i<3;i++){if((await req('devices')).devices.find(x=>x.serial===d.serial).themeId===theme.id)break;await req('playlist-step',{serial:d.serial,direction:1});}}
  const before=await req('devices');fs.writeFileSync(path.join(out,'before-restart.json'),JSON.stringify({...before,devices:before.devices.map(({preview,...d})=>d)},null,2));
  const preview={...structuredClone(theme),id:'qa-unsaved-preview-restart',displayName:'验收_重启不保留临时预览'};await req('theme-preview',{serial:before.devices.find(d=>d.model==='D69').serial,theme:preview});
  console.log('PREPARED_MIXED_WITH_TEMPORARY_PREVIEW');
 }else{
  await page.waitForTimeout(1500);const first=await req('devices');assert.equal(first.scheduling.enabled,false);
  for(const d of first.devices){assert.equal(d.themeId,theme.id);assert(d.playing&&d.playlistRunning);assert(d.playlistRemaining>0&&d.playlistRemaining<=30);}
  await page.waitForTimeout(2200);const later=await req('devices');for(const d of later.devices)assert.notEqual(d.preview,first.devices.find(x=>x.serial===d.serial).preview,'restart clock frozen');
  await page.getByRole('button',{name:'创作中心',exact:true}).click();await page.getByLabel('搜索我的作品').fill(theme.displayName);await page.locator('div.flex.flex-col.overflow-hidden.rounded-2xl').filter({has:page.getByText(theme.displayName,{exact:true})}).getByRole('button',{name:'编辑',exact:true}).click();if(await page.getByRole('button',{name:'忽略草稿',exact:true}).isVisible())await page.getByRole('button',{name:'忽略草稿',exact:true}).click();assert.equal(await page.getByLabel('主题名称',{exact:true}).inputValue(),theme.displayName);
  assert((await req('library-load')).themes.find(t=>t.id===theme.id).editorProject.master.find(e=>e.id==='image-ui').src.startsWith('https://media.studio.local/'));
  assert.equal(await page.locator('[data-el-id="image-ui"]').count(),1);
  await page.screenshot({path:path.join(out,'editor-after-restart.png')});
  fs.writeFileSync(path.join(out,'restart.json'),JSON.stringify({normalRestart:true,temporaryNotPersisted:true,mixedRemainingRestored:true,clockUpdates:true,managedImage:true,editorReopened:true,devices:later.devices.map(({preview,...d})=>d)},null,2));console.log('RESTART_EDITOR_IMAGE_CLOCK_MIXED_PREVIEW_RECOVERY_PASSED');
 }
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
