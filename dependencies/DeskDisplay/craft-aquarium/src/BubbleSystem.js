export class BubbleSystem {
  constructor(config, random = Math.random) {
    this.width = config.designWidth; this.height = config.designHeight; this.random = random;
    this.items = Array.from({ length: config.bubbles ? config.bubbleCount : 0 }, (_, i) => ({
      x: 0, baseX: this.width * (i % 2 ? 0.91 : 0.08) + (random() - 0.5) * 90,
      y: random() * this.height, radius: 2 + random() * 6,
      speed: 10 + random() * 25, opacity: 0.2 + random() * 0.45,
      phase: random() * Math.PI * 2
    }));
  }
  update(dt, time) {
    for (const b of this.items) {
      b.y -= b.speed * dt;
      b.x = b.baseX + Math.sin(time * 0.65 + b.phase) * 7;
      if (b.y < -10) b.y = this.height + b.radius;
    }
  }
  draw(ctx) {
    for (const b of this.items) {
      const fade = Math.min(1, Math.max(0, (b.y - this.height * 0.09) / 55));
      ctx.globalAlpha = b.opacity * fade;
      const size = b.radius * 3;
      ctx.drawImage(b.radius > 6 ? this.softSprite : this.sprite, b.x - size / 2, b.y - size / 2, size, size);
    }
    ctx.globalAlpha = 1;
  }
}
