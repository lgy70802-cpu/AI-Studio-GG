// Development-only atlas slicing and procedural caustics; no runtime dependencies.
const fs = require('node:fs');
const path = require('node:path');
const { createCanvas, loadImage } = require('../../craft-stocks/node_modules/@napi-rs/canvas');
(async () => {
  const root = path.resolve(__dirname, '..');
  const atlas = await loadImage(path.join(root, 'assets/fish/atlas.png'));
  const bounds = [[580,0,370,271],[580,270,370,150],[580,418,370,182],[620,602,320,199],[570,803,400,220]];
  for (let i = 0; i < bounds.length; i++) {
    const [x,y,w,h] = bounds[i], canvas = createCanvas(w,h), ctx = canvas.getContext('2d');
    ctx.drawImage(atlas,x,y,w,h,0,0,w,h);
    fs.writeFileSync(path.join(root,`assets/fish/fish0${i+1}.png`),canvas.toBuffer('image/png'));
  }
  const canvas = createCanvas(512,512), ctx = canvas.getContext('2d');
  ctx.strokeStyle = 'rgba(255,236,224,0.65)'; ctx.lineWidth = 1.5;
  // Periodic curves tile seamlessly at their 512px boundaries.
  for (let row = -1; row < 10; row++) {
    ctx.beginPath();
    for (let x = 0; x <= 512; x += 4) {
      const y = row * 58 + Math.sin(x / 512 * Math.PI * 4 + row * 1.7) * 18 + Math.sin(x / 512 * Math.PI * 8) * 6;
      if (!x) ctx.moveTo(x,y); else ctx.lineTo(x,y);
    }
    ctx.stroke();
  }
  ctx.globalCompositeOperation='destination-in';
  const g=ctx.createLinearGradient(0,0,0,512);g.addColorStop(0,'transparent');g.addColorStop(0.38,'white');g.addColorStop(0.7,'#ffffff80');g.addColorStop(1,'transparent');
  ctx.fillStyle=g;ctx.fillRect(0,0,512,512);
  fs.writeFileSync(path.join(root,'assets/effects/caustics.png'),canvas.toBuffer('image/png'));
  console.log('Prepared five alpha PNG sprites and caustics texture.');
})();
