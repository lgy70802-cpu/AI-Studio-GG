// Design coordinates, not a hardware resolution. Every target scales independently.
export const aquariumConfig = {
  designWidth: 2557, designHeight: 615,
  background: './assets/background/aquarium-bg.jpg',
  backgroundFit: 'contain',
  fishImages: [1, 2, 3, 4, 5].map(n => `./assets/fish/fish0${n}.png`),
  causticsImage: './assets/effects/caustics.png',
  maxFPS: 30, maxPixelRatio: 1, maxRenderPixels: 4000000,
  fishCount: { back: 2, mid: 4, front: 2 },
  fishSpeed: 1, spawnFromEdges: false,
  naturalMotion: true, warmFishLight: true,
  // This authored depth profile matches the supplied rose ruins background only.
  occlusion: true, occlusionBackground: './assets/background/aquarium-bg.jpg',
  bubbles: true, bubbleCount: 24, caustics: true, lightRays: true,
  debug: false,
  // Craft frame transport is independently bounded. No accumulating frame queue.
  outputFPS: 30, outputQuality: 0.88, outputFormat: 'image/jpeg', outputTransport: 'json'
};

export function resolveConfig(overrides = {}) {
  const c = { ...aquariumConfig, ...overrides,
    fishCount: { ...aquariumConfig.fishCount, ...overrides.fishCount } };
  const bounded = (v, fallback, lo, hi) => Number.isFinite(Number(v)) ? Math.min(hi, Math.max(lo, Number(v))) : fallback;
  c.maxFPS = [24, 30, 60].includes(c.maxFPS) ? c.maxFPS : 30;
  c.designWidth = bounded(c.designWidth, 2557, 320, 8192);
  c.designHeight = bounded(c.designHeight, 615, 180, 8192);
  c.fishSpeed = bounded(c.fishSpeed, 1, 0, 3);
  c.bubbleCount = Math.round(bounded(c.bubbleCount, 24, 0, 60));
  c.maxPixelRatio = bounded(c.maxPixelRatio, 1, 0.5, 2);
  c.maxRenderPixels = bounded(c.maxRenderPixels, 4000000, 100000, 16000000);
  c.outputFPS = bounded(c.outputFPS, 30, 1, c.maxFPS);
  c.outputQuality = bounded(c.outputQuality, 0.88, 0.1, 1);
  for (const depth of ['back', 'mid', 'front']) c.fishCount[depth] = Math.round(bounded(c.fishCount[depth], 0, 0, 16));
  c.backgroundFit = c.backgroundFit === 'cover' ? 'cover' : 'contain';
  return c;
}
