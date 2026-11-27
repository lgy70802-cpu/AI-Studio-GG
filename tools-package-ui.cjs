const fs=require('fs'),path=require('path'),assert=require('assert/strict');const{connect}=require('./tools-theme-common.cjs');
(async()=>{
 const out=path.join(__dirname,'artifacts/reliability-qa'),root=path.join(out,'migration-workspace'),file=path.join(out,'full-backup.mirabox'),{browser,page,req}=await connect(9228);
 const counts={};
 async function inspectThroughUi(){
   await page.getByRole('button',{name:'设置',exact:true}).click();
   const patched=await page.evaluate(file=>{const bridge=window.chrome.webview,send=bridge.postMessage.bind(bridge);const wrapped=message=>send(message.command==='package-inspect'?{...message,path:file}:message);bridge.postMessage=wrapped;return bridge.postMessage===wrapped;},file);assert(patched,'native picker test hook unavailable');
   await page.getByRole('button',{name:'导入或恢复备份包',exact:true}).click();await page.getByRole('dialog',{name:'备份包导入预览'}).waitFor();
 }
 await inspectThroughUi();await page.screenshot({path:path.join(out,'package-import-review.png')});await page.getByRole('button',{name:'合并导入，保留原内容',exact:true}).click();await page.waitForTimeout(2200);
 let snapshot=await req('library-load');counts.afterMerge=snapshot.themes.length;assert(snapshot.themes.some(t=>t.id==='qa-composite-theme'));
 await inspectThroughUi();await page.getByRole('button',{name:'恢复整份备份并暂停输出',exact:true}).click();await page.waitForTimeout(2500);
 snapshot=await req('library-load');counts.afterRestore=snapshot.themes.length;assert((await req('devices')).devices.every(d=>!d.wantsPlaying&&!d.playing));assert.equal((await req('devices')).scheduling.enabled,false);
 const theme=snapshot.themes.find(t=>t.id==='qa-composite-theme'),video=theme.editorProject.bg.vurl;assert(fs.existsSync(path.join(root,'Library',new URL(video).pathname.slice(1))));
 await page.getByRole('button',{name:'创作中心',exact:true}).click();await page.getByLabel('搜索我的作品').fill(theme.displayName);await page.locator('div.flex.flex-col.overflow-hidden.rounded-2xl').filter({has:page.getByText(theme.displayName,{exact:true})}).getByRole('button',{name:'编辑',exact:true}).click();
 assert.equal(await page.getByLabel('主题名称',{exact:true}).inputValue(),theme.displayName);await page.waitForTimeout(1500);assert(await page.locator('video').evaluateAll(videos=>videos.some(v=>v.readyState>=2&&!v.error)));
 await page.screenshot({path:path.join(out,'migrated-editor.png')});await page.reload();await page.waitForTimeout(600);await page.getByRole('button',{name:'设置',exact:true}).click();
 assert(await page.getByRole('switch',{name:'GPU 硬件渲染加速',exact:true}).isDisabled());assert(await page.getByRole('switch',{name:'丝滑淡入切换动效',exact:true}).isDisabled());
 const orphan=path.join(root,'Library','qa-unused.upload');fs.writeFileSync(orphan,Buffer.alloc(262144));await page.getByRole('button',{name:'立即清理',exact:true}).click();await page.waitForTimeout(500);assert(!fs.existsSync(orphan));assert(fs.existsSync(path.join(root,'Library',new URL(video).pathname.slice(1))));
 fs.writeFileSync(path.join(out,'package-ui.json'),JSON.stringify({nativeReview:true,merge:true,restorePaused:true,mediaCopied:true,videoDecodesInNewWorkspace:true,editorReopened:true,unsupportedSettingsDisabled:true,temporaryCleanupPreservesMedia:true,counts},null,2));console.log('PACKAGE_UI_MIGRATION_MERGE_RESTORE_PAUSE_EDITOR_CACHE_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
