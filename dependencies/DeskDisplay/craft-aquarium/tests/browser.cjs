const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const assert = require('node:assert/strict');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const root = path.resolve(__dirname,'..');
(async()=>{
  const browser = await chromium.launch({ headless:true,channel:'msedge',args:['--js-flags=--expose-gc'] });
  try {
    const page = await browser.newPage({viewport:{width:2557,height:615}}),errors=[];
    page.on('pageerror',error=>errors.push(error.message));
    await page.goto(pathToFileURL(path.join(root,'preview.html')).href);
    await page.waitForFunction(()=>window.aquarium?.renderer.ready);
    assert.deepEqual(await page.evaluate(()=>[aquarium.scene.fish.length,aquarium.scene.bubbles.items.length]),[8,24]);
    await page.waitForTimeout(3200);
    const fps = await page.evaluate(()=>aquarium.fps);
    console.log('Preview measured FPS:',fps);
    assert.ok(fps>=23 && fps<=32, `Unexpected frame rate ${fps}`);
    await page.screenshot({path:path.join(root,'preview-d92.png')});
    const motion=await page.evaluate(()=>({time:aquarium.scene.time,x:aquarium.scene.fish[0].x}));
    await page.waitForTimeout(300);
    assert.notEqual(await page.evaluate(()=>aquarium.scene.fish[0].x),motion.x);
    await page.evaluate(()=>{
      window.testHidden=true;
      Object.defineProperty(document,'hidden',{configurable:true,get:()=>window.testHidden});
      document.dispatchEvent(new Event('visibilitychange'));
    });
    const paused=await page.evaluate(()=>aquarium.scene.time);
    await page.waitForTimeout(400);assert.equal(await page.evaluate(()=>aquarium.scene.time),paused);
    await page.evaluate(()=>{window.testHidden=false;document.dispatchEvent(new Event('visibilitychange'));});
    await page.waitForTimeout(300);const resumed=await page.evaluate(()=>aquarium.scene.time);assert.ok(resumed>paused&&resumed-paused<0.4);
    for (const [width,height] of [[1280,720],[615,1000],[1920,462],[2557,615]]) {
      await page.setViewportSize({width,height}); await page.waitForTimeout(100);
      assert.deepEqual(await page.evaluate(()=>[aquarium.canvas.width,aquarium.canvas.height]),[width,height]);
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth||document.documentElement.scrollHeight>innerHeight),false);
    }
    // Compare retained JS heap after GC across a minute, not transient allocation peaks.
    const client=await page.context().newCDPSession(page);await client.send('HeapProfiler.collectGarbage');
    const before=(await client.send('Runtime.getHeapUsage')).usedSize;
    const sampleDuration = process.env.QUICK_TEST ? 1000 : 60000;
    await page.waitForTimeout(sampleDuration);
    await client.send('HeapProfiler.collectGarbage');const after=(await client.send('Runtime.getHeapUsage')).usedSize;
    console.log(`${sampleDuration/1000}s retained JS heap bytes:`,{before,after,delta:after-before});
    assert.ok(after-before<2*1024*1024);
    for (const target of [24,60]) {
      await page.evaluate(fps=>{aquarium.config.maxFPS=fps;aquarium.frames=0;aquarium.sampleSeconds=0;},target);
      await page.waitForTimeout(2400);
      const measured=await page.evaluate(()=>aquarium.fps);console.log(`${target} FPS target:`,measured);
      assert.ok(measured>=target*0.8 && measured<=target+2);
    }
    await page.evaluate(()=>aquarium.dispose());assert.equal(await page.evaluate(()=>aquarium.running||aquarium.raf!==0),false);
    const host = await browser.newPage({viewport:{width:1280,height:720}});
    host.on('pageerror',error=>{errors.push(error.message);console.log('Host error:',error.message);});
    host.on('console',message=>console.log('Host:',message.text()));
    await host.addInitScript(()=>{
      // Reproduce Craft's invisible host: rAF never fires, although the device is active.
      Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});
      window.requestAnimationFrame=()=>0;
      window.packets=[];
      class FakeSocket {
        static OPEN=1;
        constructor(){this.readyState=1;this.bufferedAmount=0;window.fakeSocket=this;setTimeout(()=>this.onopen?.(),0);}
        send(data){
          if(typeof data==='string'){
            const message=JSON.parse(data);
            if(message.event!=='setImage'){window.registration=message;return;}
            window.lastImage=message.payload.image;
            window.packets.push({header:{event:message.event,context:message.context,payload:{target:message.payload.target,type:message.payload.image.split(';')[0].slice(5)}},bytes:data.length});
            if(window.packets.length>10)window.packets.shift();window.packetCount=(window.packetCount||0)+1;return;
          }
          const length=new DataView(data).getUint32(0,false);
          const header=JSON.parse(new TextDecoder().decode(new Uint8Array(data,4,length)));
          window.packets.push({header,bytes:data.byteLength});if(window.packets.length>10)window.packets.shift();window.packetCount=(window.packetCount||0)+1;
        }
        close(){this.readyState=3;this.onclose?.();}
      }
      window.WebSocket=FakeSocket;
    });
    await host.goto(pathToFileURL(path.join(root,'release/Aquarium-V1.1/com.mirabox.streamdock.aquarium.mPanelPlugin/index.html')).href);
    await host.evaluate(()=>connectSDSocket('12345','test-plugin','registerPlugin','{}'));
    await host.evaluate(()=>aquariumHost.handle({event:'willAppear',context:'tank',action:'com.mirabox.streamdock.aquarium.action1',payload:{settings:{ActionGeometry:{width:2557,height:615}}}}));
    await host.waitForFunction(()=>window.packets.length>2, null, {polling:100}).catch(async error=>{
      console.log(await host.evaluate(()=>({hidden:document.hidden,instances:aquariumHost.instances.size,ready:fakeSocket.readyState,entry:[...aquariumHost.instances.values()].map(x=>({busy:x.busy,time:x.aquarium?.scene.time,running:x.aquarium?.running}))})));
      throw error;
    });
    assert.deepEqual(await host.evaluate(()=>[aquariumHost.instances.get('tank').aquarium.canvas.width,aquariumHost.instances.get('tank').aquarium.canvas.height]),[2557,615]);
    assert.equal(await host.evaluate(()=>packets[0].header.event),'setImage');
    assert.equal(await host.evaluate(()=>packets[0].header.payload.type),'image/jpeg');
    assert.equal(await host.evaluate(()=>packets[0].header.payload.target),0);
    assert.equal(await host.evaluate(()=>document.hidden),true);
    assert.ok(await host.evaluate(()=>aquariumHost.instances.get('tank').aquarium.scene.time)>0);
    await host.evaluate(()=>document.dispatchEvent(new Event('visibilitychange')));
    const hiddenCount=await host.evaluate(()=>packetCount);
    await host.waitForFunction(count=>packetCount>count,hiddenCount,{polling:100});
    assert.ok(await host.evaluate(async()=>{
      const image=new Image();image.src=lastImage;await image.decode();
      const canvas=document.createElement('canvas');canvas.width=64;canvas.height=16;
      const ctx=canvas.getContext('2d');ctx.drawImage(image,0,0,64,16);
      return ctx.getImageData(0,0,64,16).data.some((value,index)=>index%4!==3&&value>80);
    }),'Encoded host frame must decode to a non-black aquarium');
    await host.evaluate(()=>aquariumHost.handle({event:'didReceiveSettings',context:'tank',payload:{settings:{ActionGeometry:{width:1280,height:720}}}}));
    assert.deepEqual(await host.evaluate(()=>[aquariumHost.instances.get('tank').aquarium.canvas.width,aquariumHost.instances.get('tank').aquarium.canvas.height]),[1280,720]);
    await host.evaluate(()=>{fakeSocket.bufferedAmount=2*1024*1024;});await host.waitForTimeout(100);
    const blocked=await host.evaluate(()=>packetCount);await host.waitForTimeout(300);assert.equal(await host.evaluate(()=>packetCount),blocked);
    await host.evaluate(()=>{fakeSocket.bufferedAmount=0;aquariumHost.handle({event:'willDisappear',context:'tank'});});
    assert.equal(await host.evaluate(()=>aquariumHost.instances.size),0);
    assert.equal(await host.evaluate(()=>aquariumHost.clock.active),false);
    await host.evaluate(()=>aquariumHost.handle({event:'willAppear',context:'again',payload:{settings:{}}}));
    await host.waitForFunction(()=>aquariumHost.instances.get('again')?.aquarium,null,{polling:100});
    await host.evaluate(()=>aquariumHost.dispose());assert.equal(await host.evaluate(()=>aquariumHost.instances.size),0);
    assert.deepEqual(errors,[]);
    console.log('PASS preview + hidden Craft host without rAF, JSON image decoding, lifecycle, worker cleanup and backpressure.');
  } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
