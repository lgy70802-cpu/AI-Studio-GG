const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const assert = require('node:assert/strict');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const fs = require('node:fs');
const root=path.resolve(__dirname,'..');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try {
  const page=await browser.newPage({viewport:{width:2557,height:615}});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(root,'preview.html')).href);
  await page.waitForFunction(()=>window.aquarium?.renderer.ready);
  await page.evaluate(()=>aquarium.pause());
  const report=await page.evaluate(()=>{
    const fish=aquarium.scene.fish.find(f=>f.depth==='mid');
    const first=fish.spriteFrames[0],other=fish.spriteFrames[10];
    const a=first.getContext('2d').getImageData(0,0,first.width,first.height).data;
    const b=other.getContext('2d').getImageData(0,0,other.width,other.height).data;
    let tailDifference=0,headDifference=0;
    for(let i=0;i<a.length;i+=4){
      const x=(i/4)%first.width;
      const difference=Math.abs(a[i+3]-b[i+3]);
      if(x<first.width*0.65)tailDifference+=difference;
      if(x>first.width*0.8)headDifference+=difference;
    }
    const mask=aquarium.renderer.occlusion;
    const sample=(layer,x,y)=>layer.getContext('2d').getImageData(Math.round(x/2048*layer.width),Math.round(y/493*layer.height),1,1).data[3];
    const wall=sample(mask.back,875,285),openWater=sample(mask.back,900,120),arch=sample(mask.back,940,285);
    const frontWall=sample(mask.front,875,285),frontReef=sample(mask.front,180,370);
    let cacheBytes=0;
    for(const f of aquarium.scene.fish)for(const frame of f.spriteFrames)cacheBytes+=frame.width*frame.height*4;
    return{tailDifference,headDifference,wall,openWater,arch,frontWall,frontReef,cacheBytes};
  });
  assert.ok(report.tailDifference>1000);assert.equal(report.headDifference,0);
  assert.ok(report.wall>230);assert.equal(report.openWater,0);assert.equal(report.arch,0);
  assert.equal(report.frontWall,0);assert.ok(report.frontReef>200);
  assert.ok(report.cacheBytes<32*1024*1024);
  // Actual compositing: the same diagnostic fish is hidden by stone only in BACK.
  const layers=await page.evaluate(()=>{
    const s=aquarium.scene,c=aquarium.canvas,ctx=c.getContext('2d');
    for(const f of s.fish)f.opacity=0;
    const f=s.fish[0];f.opacity=1;f.x=875/2048*s.config.designWidth;f.y=285/493*s.config.designHeight;
    f.draw=function(ctx){ctx.fillStyle='#00ff00';ctx.fillRect(this.x-6,this.y-6,12,12);};
    const samples=[];
    for(const depth of ['back','mid','front']){
      f.depth=depth;aquarium.renderer.draw();
      samples.push(Array.from(ctx.getImageData(Math.round(f.x),Math.round(f.y),1,1).data));
    }
    return samples;
  });
  assert.ok(layers[0][1]<220);assert.ok(layers[1][1]>240&&layers[1][0]<10);assert.ok(layers[2][1]>240);
  // Reload to recover the real scene after the isolated compositing probe.
  await page.reload();await page.waitForFunction(()=>window.aquarium?.renderer.ready);
  await page.waitForTimeout(2000);
  await page.screenshot({path:path.join(root,'preview-v1.1.png')});
  const contact=await page.evaluate(()=>{
    const f=aquarium.scene.fish[4],c=document.createElement('canvas');c.width=1000;c.height=240;const x=c.getContext('2d');
    x.fillStyle='#29171e';x.fillRect(0,0,c.width,c.height);
    for(let i=0;i<5;i++){
      const sprite=f.spriteFrames[i*4],height=184*sprite.height/sprite.width;
      x.drawImage(sprite,i*200+8,(240-height)/2,184,height);
    }
    return c.toDataURL();
  });
  fs.writeFileSync(path.join(root,'fin-phases-v1.1.png'),Buffer.from(contact.split(',')[1],'base64'));
  // Fixed caches survive ordinary updates, and disappear on disposal.
  assert.equal(await page.evaluate(()=>{
    const a=aquarium.scene.fish[0].spriteFrames;
    for(let i=0;i<3600;i++)aquarium.scene.update(1/30);
    return a===aquarium.scene.fish[0].spriteFrames;
  }),true);
  await page.evaluate(()=>{window.retainedFish=aquarium.scene.fish[0];aquarium.dispose();});
  assert.equal(await page.evaluate(()=>retainedFish.spriteFrames===null&&retainedFish.blendCanvas===null),true);
  assert.deepEqual(errors,[]);console.log('PASS fin/head isolation, stone/arch/coral masks, compositing, bounded caches, disposal',report);
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
