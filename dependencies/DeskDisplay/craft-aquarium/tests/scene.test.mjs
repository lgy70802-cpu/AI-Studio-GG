import test from 'node:test';
import assert from 'node:assert/strict';
import { resolveConfig } from '../src/aquariumConfig.js';
import { Scene } from '../src/Scene.js';
import { Fish } from '../src/Fish.js';
const assets = { fish: [{ width:370, height:200 }], caustics:null };
function rng() { let seed = 101; return () => { seed = (1664525 * seed + 1013904223) >>> 0; return seed / 4294967296; }; }
test('eight-hour simulated run: bounded positions, stable object pools, every fish turns', () => {
  const scene = new Scene(resolveConfig(), assets, rng());
  const fish = [...scene.fish], bubbles = [...scene.bubbles.items], turns = new Set();
  for (let i = 0; i < 8 * 3600 * 30; i++) {
    scene.update(1/30);
    for (const f of fish) {
      assert.ok(Number.isFinite(f.x) && f.x >= f.margin && f.x <= f.right);
      assert.ok(Math.abs(f.y - f.baseY) <= f.amplitude + 0.001);
      if (f.state === 'turning') turns.add(f.id);
    }
  }
  assert.equal(turns.size,8); assert.equal(scene.fish.length,8); assert.equal(scene.bubbles.items.length,24);
  fish.forEach((f,i)=>assert.equal(scene.fish[i],f)); bubbles.forEach((b,i)=>assert.equal(scene.bubbles.items[i],b));
});
test('movement remains consistent across 24/30/60 fps', () => {
  const positions = [24,30,60].map(fps => {
    const fish = new Fish(2,'mid',assets.fish[0],resolveConfig(),()=>0.5);
    for (let i=0;i<fps*4;i++) fish.update(1/fps,(i+1)/fps);
    return fish.x;
  });
  assert.ok(Math.max(...positions)-Math.min(...positions)<0.01);
});
test('edge entry completes and gradual turn changes facing through zero', () => {
  const f = new Fish(1,'front',assets.fish[0],resolveConfig({spawnFromEdges:true}),()=>0.75);
  for(let i=0;i<1000;i++)f.update(1/30,i/30);
  assert.equal(f.entering,false);
  f.x=f.right-1;f.direction=1;f.state='swimming';
  let minFacing=1, stopped=false;
  for(let i=0;i<30;i++){f.update(1/60,i/60);minFacing=Math.min(minFacing,Math.abs(f.facing));if(f.speed===0)stopped=true;}
  assert.ok(minFacing<0.15);assert.ok(stopped);
  for(let i=0;i<30;i++)f.update(1/60,i/60);
  assert.equal(f.direction,-1);
});
test('optional layers and invalid configuration are bounded', () => {
  const c=resolveConfig({fishCount:{back:2},bubbles:false,lightRays:false,maxFPS:500,fishSpeed:NaN,bubbleCount:Infinity});
  assert.equal(c.maxFPS,30);assert.equal(c.fishSpeed,1);
  const s=new Scene(c,assets,rng());assert.equal(s.fish.length,8);assert.equal(s.bubbles.items.length,0);assert.equal(s.rays.length,0);
});
test('natural routes change height smoothly and hover resumes swimming', () => {
  const f = new Fish(3,'mid',assets.fish[0],resolveConfig(),rng());
  let low=f.baseY,high=f.baseY,hover=false,resumed=false,previous=f.baseY;
  for(let i=0;i<180*30;i++) {
    f.update(1/30,(i+1)/30);
    low=Math.min(low,f.baseY);high=Math.max(high,f.baseY);
    assert.ok(f.baseY>=f.lowY && f.baseY<=f.highY);
    assert.ok(Math.abs(f.baseY-previous)<0.5,'No abrupt vertical jumps'); previous=f.baseY;
    assert.ok(Math.abs(f.rotation)<=0.111);
    if(f.state==='hovering')hover=true;
    if(hover&&f.state==='swimming')resumed=true;
  }
  assert.ok(high-low>20);assert.ok(hover);assert.ok(resumed);
});
test('natural motion disabled preserves the original swimming height', () => {
  const f=new Fish(2,'mid',assets.fish[0],resolveConfig({naturalMotion:false}),rng());
  const initial=f.baseY;
  for(let i=0;i<3600;i++)f.update(1/30,i/30);
  assert.equal(f.baseY,initial);assert.notEqual(f.state,'hovering');
});
