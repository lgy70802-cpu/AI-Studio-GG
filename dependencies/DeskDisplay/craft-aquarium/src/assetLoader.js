import { prepareFishSprite } from './FishSprites.js';
export function log(event, details = {}) { console.info(JSON.stringify({ component: 'aquarium', event, ...details })); }
function loadImage(src) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    const timer = setTimeout(() => { image.onload = image.onerror = null; image.src = ''; reject(new Error(`Asset timeout: ${src}`)); }, 15000);
    image.onload = () => { clearTimeout(timer); image.onload = image.onerror = null; resolve(image); };
    image.onerror = () => { clearTimeout(timer); image.onload = image.onerror = null; reject(new Error(`Cannot load asset: ${src}`)); };
    // Built file:// entry uses embedded bytes so Canvas export remains origin-clean.
    image.src = window.aquariumEmbeddedAssets?.[src] || src;
  });
}
export async function loadAssets(config) {
  const cache = new Map();
  const get = path => { if (!cache.has(path)) cache.set(path, loadImage(path)); return cache.get(path); };
  const optional = path => get(path).catch(error => { log('asset-unavailable', { path, message: error.message }); return null; });
  const [background, fish, caustics] = await Promise.all([
    optional(config.background), Promise.all(config.fishImages.map(optional)),
    config.caustics ? optional(config.causticsImage) : Promise.resolve(null)
  ]);
  const valid = fish.filter(Boolean);
  if (!valid.length) throw new Error('No fish assets could be loaded');
  return { background, fish: valid, caustics };
}
export function surface(width, height) {
  const canvas = document.createElement('canvas'); canvas.width = Math.ceil(width); canvas.height = Math.ceil(height); return canvas;
}
export function prepareSprites(scene) {
  for (const fish of scene.fish) {
    prepareFishSprite(fish, scene.config);
  }
  const bubble = surface(48, 48), b = bubble.getContext('2d');
  b.lineWidth = 1.2; b.strokeStyle = 'rgba(255,226,222,0.6)';
  b.beginPath(); b.arc(24, 24, 15, 0, Math.PI * 2); b.stroke();
  b.strokeStyle = 'rgba(255,245,240,0.95)'; b.lineWidth = 2;
  b.beginPath(); b.arc(24, 24, 12, 3.8, 4.8); b.stroke();
  scene.bubbles.sprite = bubble;
  const soft = surface(48, 48), s = soft.getContext('2d'); s.filter = 'blur(2px)'; s.drawImage(bubble, 0, 0);
  scene.bubbles.softSprite = soft;
  const ray = surface(128, 256), r = ray.getContext('2d');
  const gradient = r.createLinearGradient(0, 0, 0, 256);
  gradient.addColorStop(0, 'rgba(255,231,220,0.9)'); gradient.addColorStop(1, 'rgba(255,231,220,0)');
  r.fillStyle = gradient; r.beginPath(); r.moveTo(50, 0); r.lineTo(78, 0); r.lineTo(128, 256); r.lineTo(0, 256); r.fill();
  r.globalCompositeOperation = 'destination-in';
  const edge = r.createLinearGradient(0, 0, 128, 0);
  edge.addColorStop(0, 'transparent'); edge.addColorStop(0.5, 'white'); edge.addColorStop(1, 'transparent');
  r.fillStyle = edge; r.fillRect(0, 0, 128, 256);
  for (const light of scene.rays) light.sprite = ray;
}
