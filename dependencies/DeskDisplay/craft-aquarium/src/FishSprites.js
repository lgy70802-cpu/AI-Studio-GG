function fishSurface(w, h) {
  const canvas = document.createElement('canvas'); canvas.width = Math.ceil(w); canvas.height = Math.ceil(h); return canvas;
}
export function prepareFishSprite(fish, config) {
  const density = 1.5, w = Math.ceil(fish.width * density), h = Math.ceil(fish.height * density);
  const source = fishSurface(w, h), ctx = source.getContext('2d');
  const tone = config.warmFishLight ? (fish.depth === 'back' ? 'saturate(0.78) brightness(0.89)' : 'saturate(0.86) brightness(0.98)') : '';
  ctx.filter = `${tone} ${fish.blur ? `blur(${fish.blur * density}px)` : ''}`.trim() || 'none';
  ctx.drawImage(fish.image, 2, 2, w - 4, h - 4); ctx.filter = 'none';
  if (config.warmFishLight) {
    ctx.globalCompositeOperation = 'source-atop';
    const light = ctx.createLinearGradient(0, 0, 0, h);
    light.addColorStop(0, 'rgba(255,220,205,0.12)');
    light.addColorStop(0.48, 'rgba(219,148,146,0.055)');
    light.addColorStop(1, 'rgba(53,25,37,0.17)');
    ctx.fillStyle = light; ctx.fillRect(0, 0, w, h);
  }
  fish.sprite = source;
  if (!config.naturalMotion) return;
  const count = 20, columns = 24, padX = w * 0.06, padY = h * 0.10;
  fish.spriteFrames = new Array(count);
  for (let frame = 0; frame < count; frame++) {
    const c = fishSurface(w * 1.12, h * 1.20), x = c.getContext('2d');
    const phase = frame / count * Math.PI * 2;
    for (let column = 0; column < columns; column++) {
      const left = Math.floor(column * w / columns), right = Math.floor((column + 1) * w / columns);
      const u = (left + right) / (2 * w), tail = Math.max(0, (0.72 - u) / 0.72);
      // Travelling bend increases toward the tail. Head/eye (last 28%) stay stable.
      const bend = Math.sin(phase - u * 2.4) * h * 0.046 * tail * tail;
      const fin = Math.sin(phase + u * 3.1 + 0.7) * h * 0.025 * tail;
      const top = padY + bend + fin, upper = padY + h * 0.32 + bend * 0.55;
      const lower = padY + h * 0.73 + bend * 0.55, bottom = padY + h + bend - fin;
      // A small overlap suppresses strip seams after filtering. All work is startup-only.
      const sw = Math.min(w - left, right - left + 0.4), dx = padX + left;
      x.drawImage(source, left, 0, sw, h * 0.32 + 0.5, dx, top, sw, (upper - top) * (1 + 0.5 / (h * 0.32)));
      x.drawImage(source, left, h * 0.32, sw, h * 0.41 + 0.5, dx, upper, sw, (lower - upper) * (1 + 0.5 / (h * 0.41)));
      x.drawImage(source, left, h * 0.73, sw, h * 0.27, dx, lower, sw, bottom - lower);
    }
    fish.spriteFrames[frame] = c;
  }
  fish.blendCanvas = fishSurface(w * 1.12, h * 1.20);
  fish.blendContext = fish.blendCanvas.getContext('2d');
}
