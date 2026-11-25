const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{const id=crypto.randomUUID(),fn=e=>{if(e.data.id!==id)return;window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command,...payload});setTimeout(()=>reject(Error('timeout '+command)),60000);}),{command,payload});
 await page.getByRole('textbox',{name:'搜索主题与壁纸'}).fill('');
 await page.getByRole('button',{name:'资源库',exact:true}).click();
 await page.locator('input[type=file]').setInputFiles(path.join(__dirname,'artifacts/video-qa/验收_视频.mp4'));
 let snapshot,video;
 for(let i=0;i<25;i++){await page.waitForTimeout(1000);snapshot=await req('library-load');video=snapshot.themes.find(t=>t.category==='视频');if(video)break;}
 assert(video,'video import failed');
 assert(video.videoUrl&&video.thumbnailUrl&&video.durationSeconds>0);
 console.log('VIDEO_IMPORTED '+JSON.stringify({id:video.id,duration:video.durationSeconds,width:video.sourceWidth,height:video.sourceHeight}));
 await page.getByRole('textbox',{name:'搜索主题与壁纸'}).fill(video.displayName);
 await page.getByRole('button',{name:'大图预览',exact:true}).click();
 const preview=page.getByLabel('视频预览');
 await preview.waitFor({state:'visible'});
 await page.waitForFunction(()=>document.querySelector('video')?.currentTime>0.1);
 await page.screenshot({path:path.join(__dirname,'artifacts/video-qa/preview.png')});
 await page.keyboard.press('Escape');
 const close=page.getByRole('button',{name:/关闭.*预览/});if(await close.count())await close.first().click();
 const devices=(await req('devices')).devices;
 for(const d of devices){await req('configure',{serial:d.serial,portrait:false,flipped:false});await req('apply-media',{serial:d.serial,themeId:video.id});}
 await page.waitForTimeout(3500);
 const before=(await req('devices')).devices;
 console.log('BOTH_VIDEO_STARTED '+JSON.stringify(before.map(d=>({serial:d.serial,position:d.videoPosition,fps:d.videoFps}))));
 assert(before.every(d=>d.playing&&d.videoPosition>=0));
 const d69=before.find(d=>d.model==='D69');
 await req('configure',{serial:d69.serial,playing:false});
 await page.waitForTimeout(500);
 const paused=(await req('devices')).devices.find(d=>d.serial===d69.serial).videoPosition;
 await page.waitForTimeout(2000);
 assert(Math.abs((await req('devices')).devices.find(d=>d.serial===d69.serial).videoPosition-paused)<0.2,'video pause timeline drift');
 await req('configure',{serial:d69.serial,playing:true});await page.waitForTimeout(1500);
 const resumed=(await req('devices')).devices.find(d=>d.serial===d69.serial).videoPosition;
 assert(resumed!==paused,'video failed to resume');
 const photo=snapshot.themes.find(t=>t.category==='壁纸'&&t.inLibrary!==false&&!t.deletedAt);
 for(const d of devices){
  const id='qa-video-mixed-'+d.model;snapshot.playlists=snapshot.playlists.filter(p=>p.id!==id);
  snapshot.playlists.push({id,name:'验收_'+d.model+'图片视频混播',deviceId:d.serial,orientation:'landscape',mood:'专注',shuffle:false,defaultDurationSeconds:10,items:[{themeId:video.id,durationSeconds:10},{themeId:photo.id,durationSeconds:10}]});
 }
 await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(1000);
 for(const d of devices)await req('playlist-start',{serial:d.serial,playlistId:'qa-video-mixed-'+d.model});
 fs.writeFileSync(path.join(__dirname,'artifacts/video-qa/initial-results.json'),JSON.stringify({video,preview:true,bothVideo:true,pauseResume:true},null,2));
 console.log('PAUSE_PREVIEW_MIXED_START_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
