export class WaterLight {
  constructor(image, config) { this.image = image; this.config = config; }
  draw(ctx, time) {
    if (!this.config.caustics || !this.image) return;
    const w = this.config.designWidth, h = this.config.designHeight;
    const scale = 1.015 + Math.sin(time * 0.12) * 0.015;
    const tile = 512 * scale;
    const x = (time * 2) % tile, y = (time * 0.5) % tile;
    ctx.save(); ctx.globalCompositeOperation = 'screen'; ctx.globalAlpha = 0.085;
    // Texture already contains an alpha mask; draw only in the upper water column.
    ctx.beginPath(); ctx.rect(w * 0.045, h * 0.085, w * 0.91, h * 0.48); ctx.clip();
    for (let px = x - tile; px < w; px += tile)
      ctx.drawImage(this.image, px, y - tile * 0.5, tile, tile * 0.7);
    ctx.restore();
  }
}
