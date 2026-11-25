const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{const id=crypto.randomUUID(),fn=e=>{if(e.data.id!==id)return;window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command,...payload});}),{command,payload});
 const snapshot=await req('library-load'),video=snapshot.themes.find(t=>t.category==='视频'),state=await req('devices'),d69=state.devices.find(d=>d.model==='D69');
 await req('playlist-pause',{serial:d69.serial,playlistId:'qa-video-mixed-D69'});
 const photo=snapshot.themes.find(t=>t.category==='壁纸'&&t.inLibrary!==false&&!t.deletedAt);
 await req('apply-media',{serial:d69.serial,themeId:photo.id});await req('select',{serial:d69.serial});await page.reload();await page.waitForTimeout(1200);
 await page.getByRole('button',{name:'资源库',exact:true}).click();await page.getByRole('textbox',{name:'搜索主题与壁纸'}).fill(video.displayName);
 await page.getByRole('button',{name:'应用到 D69',exact:true}).click();await page.waitForTimeout(2200);
 let current=(await req('devices')).devices.find(d=>d.serial===d69.serial);assert.equal(current.themeId,video.id);assert(current.playing&&current.videoDuration>0);
 await page.getByRole('textbox',{name:'搜索主题与壁纸'}).fill('');await page.getByRole('button',{name:'首页',exact:true}).click();
 await page.getByRole('button',{name:'暂停输出',exact:true}).click();await page.waitForTimeout(1200);current=(await req('devices')).devices.find(d=>d.serial===d69.serial);assert.equal(current.wantsPlaying,false);const position=current.videoPosition;
 await page.waitForTimeout(1500);assert(Math.abs((await req('devices')).devices.find(d=>d.serial===d69.serial).videoPosition-position)<0.2);
 await page.getByRole('button',{name:'恢复输出',exact:true}).click();await page.waitForTimeout(1200);assert((await req('devices')).devices.find(d=>d.serial===d69.serial).playing);
 await page.screenshot({path:path.join(__dirname,'artifacts/video-qa/home-video.png')});
 for(const d of state.devices){await req('playlist-start',{serial:d.serial,playlistId:'qa-video-mixed-'+d.model});if((await req('devices')).devices.find(x=>x.serial===d.serial).themeId!==video.id)await req('playlist-step',{serial:d.serial,direction:1});}
 const trashed=structuredClone(snapshot);const target=trashed.themes.find(t=>t.id===video.id);target.deletedAt=Date.now();target.inLibrary=false;
 try {await req('library-save',{snapshot:trashed});await page.reload();await page.waitForTimeout(3000);for(const d of (await req('devices')).devices){assert.notEqual(d.themeId,video.id);assert(d.playing&&d.playlistRunning);}}
 finally {await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(1000);}
 for(const d of state.devices){await req('playlist-step',{serial:d.serial,direction:1});assert.equal((await req('devices')).devices.find(x=>x.serial===d.serial).themeId,video.id);}
 await page.getByRole('textbox',{name:'搜索主题与壁纸'}).fill('');await page.getByRole('button',{name:'首页',exact:true}).click();
 fs.writeFileSync(path.join(__dirname,'artifacts/video-qa/ui-final.json'),JSON.stringify({uiApply:true,uiPauseResume:true,trashSkipBoth:true,restoredBoth:true},null,2));
 console.log('UI_APPLY_PAUSE_RESUME_TRASH_RESTORE_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
