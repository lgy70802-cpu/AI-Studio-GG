import { resolveConfig } from './aquariumConfig.js';
import { loadAssets, prepareSprites, log } from './assetLoader.js';
import { Scene } from './Scene.js';
import { Renderer } from './Renderer.js';
export class Aquarium {
  static async create(canvas, overrides = {}, options = {}) {
    const config = resolveConfig(overrides), assets = options.assets || await loadAssets(config);
    return new Aquarium(canvas, config, assets, options);
  }
  constructor(canvas, config, assets, options) {
    this.config = config; this.canvas = canvas; this.options = options;
    this.scene = new Scene(config, assets); prepareSprites(this.scene);
    this.renderer = new Renderer(canvas, this.scene, assets);
    this.running = false; this.disposed = false; this.raf = 0; this.last = null; this.fps = 0;
    this.frames = 0; this.sampleSeconds = 0;
    this.frame = now => {
      if (!this.running) return;
      if (!options.externalClock) this.raf = requestAnimationFrame(this.frame);
      if (this.last === null) { this.last = now; return; }
      const elapsed = now - this.last, interval = 1000 / config.maxFPS;
      if (elapsed + 0.2 < interval) return;
      // Retain fractional scheduling remainder, while elapsed simulation time uses actual timestamps.
      const dt = Math.min((now - (this.lastRendered ?? this.last)) / 1000, 0.1);
      this.last = now - Math.max(0, (elapsed + 0.2) % interval - 0.2); this.lastRendered = now;
      this.scene.update(dt); this.frames++; this.sampleSeconds += dt;
      if (this.sampleSeconds >= 1) { this.fps = this.frames / this.sampleSeconds; this.frames = 0; this.sampleSeconds = 0; }
      this.renderer.draw(this.fps); this.options.onFrame?.(canvas, now);
    };
    this.visibility = () => { if (options.externalClock) return; if (document.hidden) this.pause(); else if (this.enabled) this.resume(); };
    this.onPageHide = () => this.pause();
    this.onPageShow = () => { if (this.enabled && !document.hidden) this.resume(); };
    document.addEventListener('visibilitychange', this.visibility);
    window.addEventListener('pagehide', this.onPageHide); window.addEventListener('pageshow', this.onPageShow);
    if (options.observeResize !== false) {
      this.observer = new ResizeObserver(entries => {
        const rect = entries[0].contentRect;
        this.resize(rect.width, rect.height, Math.min(devicePixelRatio || 1, config.maxPixelRatio));
      });
      this.observer.observe(canvas);
      const rect = canvas.getBoundingClientRect(); this.resize(rect.width, rect.height, 1);
    }
    this.enabled = true; if (options.externalClock || !document.hidden) this.resume();
    log('started', { fish: this.scene.fish.length, bubbles: this.scene.bubbles.items.length, maxFPS: config.maxFPS });
  }
  resize(width, height, ratio = 1) { this.renderer.resize(width, height, ratio); this.renderer.draw(this.fps); }
  resume() { if (this.disposed || this.running) return; this.running = true; this.last = this.lastRendered = null; if (!this.options.externalClock) this.raf = requestAnimationFrame(this.frame); }
  pause() { this.running = false; cancelAnimationFrame(this.raf); this.raf = 0; this.last = this.lastRendered = null; }
  dispose() {
    if (this.disposed) return;
    this.disposed = true; this.enabled = false; this.pause(); this.observer?.disconnect();
    document.removeEventListener('visibilitychange', this.visibility);
    window.removeEventListener('pagehide', this.onPageHide); window.removeEventListener('pageshow', this.onPageShow);
    this.renderer.dispose();
    for (const fish of this.scene.fish) {
      if (fish.spriteFrames) { for (const frame of fish.spriteFrames) frame.width = frame.height = 1; fish.spriteFrames = null; }
      if (fish.blendCanvas) fish.blendCanvas.width = fish.blendCanvas.height = 1;
      fish.blendCanvas = fish.blendContext = fish.sprite = null;
    }
    this.scene.fish.length = 0; this.scene.bubbles.items.length = 0; this.scene.rays.length = 0;
    this.options.onFrame = null; log('disposed');
  }
}
