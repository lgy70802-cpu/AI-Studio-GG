const fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:9227'),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{const id=crypto.randomUUID(),timer=setTimeout(()=>{window.chrome.webview.removeEventListener('message',fn);reject(Error('timeout '+command));},60000),fn=e=>{if(e.data.id!==id)return;clearTimeout(timer);window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command,...payload});}),{command,payload});
 const snapshot=await req('library-load'),devices=(await req('devices')).devices;
 const base={a:'mc',x:0,y:0,size:10,color:'#ffffff',alpha:100,weight:600};
 const elements=[{...base,id:'panel',kind:'shape',a:'tl',x:2,y:5,w:96,h:90,size:1,sh:'rect',radius:3,color:'#172640',alpha:70},
 { ...base,id:'clock',kind:'clock',style:'digital',part:'hms',h24:true,sec:true,tz:'',y:-8,size:20,font:'mono' },
 { ...base,id:'title',kind:'text',text:'MiraBox 实机主题验收',y:22,size:7 },
 { ...base,id:'date',kind:'date',fmt:0,part:'full',y:36,size:5 }];
 const project={template_id:'qa-clock-project',name:'验收_实时主题',description:'原型图层、日期与秒钟',category:'动态时钟',category_id:'clock',tags:[],version:'1.0.0',schema_version:'2.0.0',min_client_version:'2.4.1',required_capabilities:[],author:'验收',entitlement:'free',supported_devices:['D69','D92'],supported_resolutions:['1280x480','1920x462','480x1280','462x1920'],thumbnail_variants:{},bg:{kind:'gradient',g1:'#26385d',g2:'#56277b',angle:110,dim:0,fit:'cover'},master:elements,
 layouts:[{size:[1280,480],label:'D69 横屏',follow:false},{size:[1920,462],label:'D92 横屏',follow:false,elements:elements.map(e=>e.id==='title'?{...e,text:'D92 独立布局'}:e)}, {size:[480,1280],label:'D69 竖屏',follow:true},{size:[462,1920],label:'D92 竖屏',follow:true}],locks:{},names:{},asset_manifest:[],updated_at:Date.now(),sourceOrigin:'custom'};
 const theme={...snapshot.themes[0],id:'qa-live-theme',name:project.name,displayName:project.name,category:'动态时钟',source:'custom',inLibrary:true,desktopStatic:false,imageUrl:undefined,videoUrl:undefined,thumbnailUrl:undefined,clockStyle:'none',editorProject:project};
 snapshot.themes=snapshot.themes.filter(t=>t.id!==theme.id);snapshot.themes.unshift(theme);await req('library-save',{snapshot});await page.reload();await page.waitForTimeout(1000);
 for(const d of devices){await req('configure',{serial:d.serial,portrait:false,flipped:false});console.log('APPLY '+d.model);await req('apply-media',{serial:d.serial,themeId:theme.id});}
 const a=(await req('devices')).devices;await page.waitForTimeout(3500);const b=(await req('devices')).devices;
 const out=path.join(__dirname,'artifacts/theme-qa');fs.mkdirSync(out,{recursive:true});
 for(const d of devices){const before=a.find(x=>x.serial===d.serial),after=b.find(x=>x.serial===d.serial);assert(after.playing&&after.frames>before.frames);assert.notEqual(after.preview,before.preview,'clock frame frozen '+d.model);fs.writeFileSync(path.join(out,d.model+'-live.jpg'),Buffer.from(after.preview.split(',')[1],'base64'));}
 const renders=browser.contexts()[0].pages().filter(p=>p.url().includes('device-render'));assert.equal(renders.length,2);
 const texts=await Promise.all(renders.map(p=>p.locator('body').innerText()));assert(texts.some(t=>t.includes('D92 独立布局')));assert(texts.some(t=>t.includes('MiraBox 实机主题验收')));
 fs.writeFileSync(path.join(out,'native-initial.json'),JSON.stringify({checks:['dual USB first frames','seconds update','independent exact-resolution layouts'],devices:b.map(({preview,...d})=>d),texts,project},null,2));
 console.log('DUAL_THEME_SECONDS_LAYOUTS_PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});
