import { Fish } from './Fish.js';
import { BubbleSystem } from './BubbleSystem.js';
import { WaterLight } from './WaterLight.js';
import { LightRay } from './LightRay.js';
export class Scene {
  constructor(config, assets, random = Math.random) {
    this.config = config; this.time = 0; this.fish = [];
    for (const depth of ['back', 'mid', 'front']) {
      for (let i = 0; i < config.fishCount[depth]; i++) {
        const id = this.fish.length;
        this.fish.push(new Fish(id, depth, assets.fish[id % assets.fish.length], config, random));
      }
    }
    this.bubbles = new BubbleSystem(config, random);
    this.water = new WaterLight(assets.caustics, config);
    this.rays = config.lightRays ? Array.from({ length: 4 }, (_, i) => new LightRay(i, config)) : [];
  }
  update(dt) {
    this.time += dt;
    for (const fish of this.fish) fish.update(dt, this.time, this.config.fishSpeed);
    this.bubbles.update(dt, this.time);
  }
}
