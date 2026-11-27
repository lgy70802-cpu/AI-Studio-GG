import { surface } from './assetLoader.js';
import { Occlusion } from './Occlusion.js';
export class Renderer {
  constructor(canvas, scene, assets) {
    this.canvas = canvas; this.scene = scene; this.assets = assets;
    this.ctx = canvas.getContext('2d', { alpha: false });
    this.background = surface(1, 1);
    this.occlusion = new Occlusion(assets.background, scene.config);
  }
  resize(width, height, ratio = 1) {
    const c = this.scene.config;
    const scale = Math.min(ratio, Math.sqrt(c.maxRenderPixels / Math.max(1, width * height)));
    const w = Math.max(1, Math.round(width * scale)), h = Math.max(1, Math.round(height * scale));
    if (this.canvas.width === w && this.canvas.height === h && this.ready) return;
    this.canvas.width = this.background.width = w; this.canvas.height = this.background.height = h;
    const x = this.background.getContext('2d'); x.fillStyle = '#100b0e'; x.fillRect(0, 0, w, h);
    this.scale = (c.backgroundFit === 'cover' ? Math.max : Math.min)(w / c.designWidth, h / c.designHeight);
    this.left = (w - c.designWidth * this.scale) / 2; this.top = (h - c.designHeight * this.scale) / 2;
    const image = this.assets.background;
    if (image) {
      const fit = (c.backgroundFit === 'cover' ? Math.max : Math.min)(c.designWidth / image.width, c.designHeight / image.height);
      x.save(); x.translate(this.left, this.top); x.scale(this.scale, this.scale);
      x.beginPath(); x.rect(0, 0, c.designWidth, c.designHeight); x.clip();
      x.drawImage(image, (c.designWidth - image.width * fit) / 2, (c.designHeight - image.height * fit) / 2, image.width * fit, image.height * fit); x.restore();
    }
    this.ready = true;
  }
  draw(fps = 0) {
    if (!this.ready) return;
    const ctx = this.ctx, s = this.scene, c = s.config;
    ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.drawImage(this.background, 0, 0);
    ctx.save(); ctx.translate(this.left, this.top); ctx.scale(this.scale, this.scale);
    ctx.beginPath(); ctx.rect(c.designWidth * 0.045, c.designHeight * 0.085, c.designWidth * 0.91, c.designHeight * 0.815); ctx.clip();
    s.water.draw(ctx, s.time);
    for (const fish of s.fish) if (fish.depth === 'back') fish.draw(ctx, s.time);
    this.occlusion.draw(ctx, 'back');
    for (const fish of s.fish) if (fish.depth === 'mid') fish.draw(ctx, s.time);
    this.occlusion.draw(ctx, 'front');
    s.bubbles.draw(ctx);
    for (const fish of s.fish) if (fish.depth === 'front') fish.draw(ctx, s.time);
    for (const ray of s.rays) ray.draw(ctx, s.time);
    ctx.restore();
    if (c.debug) {
      ctx.fillStyle = '#100b0ed9'; ctx.fillRect(8, 8, 240, 32); ctx.font = '14px monospace'; ctx.fillStyle = '#fff';
      ctx.fillText(`${fps.toFixed(1)} FPS | fish ${s.fish.length} | bubbles ${s.bubbles.items.length}`, 16, 29);
    }
  }
  dispose() { this.background.width = this.background.height = 1; this.occlusion.dispose(); }
}
