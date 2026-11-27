const { chromium } = require('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({headless:true, channel: "msedge"});const page=await browser.newPage({viewport:{width:1000,height:650}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5173/preview.html');await page.waitForSelector('canvas');await page.waitForTimeout(1200);
 const before=await page.locator('canvas').evaluate(c=>c.toDataURL());await page.waitForTimeout(1200);const after=await page.locator('canvas').evaluate(c=>c.toDataURL());if(before===after)throw Error('Clock did not tick');
 await page.fill('#text','Craft 开发体验');await page.screenshot({path:'preview.png'});
 const sizes=await page.evaluate(async()=>{const {drawClock}=await import('/src/render.ts');return [[240,600],[900,160],[1,1]].map(([w,h])=>{const c=document.createElement('canvas');drawClock(c,{ActionGeometry:{width:w,height:h}});return [c.width,c.height]})});
 const response=await page.request.get('http://127.0.0.1:5173/src/plugin/actions/action1.ts');if(response.status()!==200)throw Error('Action compilation failed');
 const pi=await page.request.get('http://127.0.0.1:5173/src/pages/actions/action1.vue');if(pi.status()!==200)throw Error('PI compilation failed');
 console.log(JSON.stringify({clockTicks:before!==after,sizes,errors}));await browser.close();if(errors.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1});