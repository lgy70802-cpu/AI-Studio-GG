import { surface } from './assetLoader.js';
// Scene-specific silhouettes, authored in the reference image's 2048 x 493 space.
// Pixel refinement preserves openings and the gaps between coral branches.
const BUILDINGS = [
  [[374,348],[378,243],[388,243],[388,196],[397,196],[398,141],[449,141],[458,148],[458,196],[466,200],[466,243],[477,247],[477,298],[507,299],[529,323],[581,356],[594,416],[374,415]],
  [[484,298],[509,301],[539,335],[576,357],[610,345],[652,315],[689,299],[725,284],[753,277],[788,252],[813,232],[830,215],[851,205],[994,205],[1005,226],[1030,253],[1066,277],[1085,285],[1218,291],[1248,309],[1276,338],[1353,405],[1353,437],[486,437]],
  [[1238,301],[1260,273],[1285,250],[1314,230],[1330,218],[1404,218],[1405,190],[1420,178],[1456,164],[1478,169],[1498,190],[1514,190],[1514,244],[1529,244],[1536,267],[1552,278],[1565,302],[1588,341],[1589,418],[1340,432]]
];
const REEFS = [
  [[107,342],[187,307],[232,233],[285,232],[317,239],[366,291],[380,433],[105,433]],
  [[481,260],[510,213],[526,169],[563,164],[605,216],[650,222],[680,334],[626,360],[483,338]],
  [[695,194],[745,159],[788,167],[818,221],[778,280],[716,279]],
  [[1109,253],[1154,225],[1204,249],[1215,205],[1250,171],[1292,174],[1330,217],[1324,291],[1110,299]],
  [[1554,222],[1587,165],[1633,173],[1655,221],[1692,222],[1720,268],[1738,217],[1760,218],[1792,288],[1794,429],[1554,429]],
  [[1782,324],[1838,286],[1880,275],[1915,317],[1965,386],[1965,442],[1782,442]]
];
const FRONT_REEFS = [
  [[110,348],[156,320],[204,328],[226,357],[260,334],[302,354],[343,324],[381,348],[441,340],[462,412],[388,437],[111,433]],
  [[616,380],[651,350],[680,369],[727,352],[763,368],[790,351],[823,410],[621,434]],
  [[876,383],[898,339],[929,328],[967,368],[1001,377],[1014,425],[887,428]],
  [[1350,391],[1395,355],[1433,338],[1472,368],[1498,416],[1350,440]],
  [[1608,358],[1652,320],[1690,320],[1731,355],[1773,347],[1801,368],[1847,337],[1880,332],[1912,371],[1968,391],[1968,440],[1608,440]]
];
function depthLayer(image, polygons) {
  const canvas = surface(image.width, image.height), ctx = canvas.getContext('2d', { willReadFrequently: true });
  ctx.save(); ctx.scale(image.width / 2048, image.height / 493); ctx.fillStyle = 'white';
  for (const polygon of polygons) {
    ctx.beginPath(); ctx.moveTo(...polygon[0]);
    for (let i = 1; i < polygon.length; i++) ctx.lineTo(...polygon[i]);
    ctx.closePath(); ctx.fill();
  }
  ctx.restore();
  const mask = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
  ctx.clearRect(0, 0, canvas.width, canvas.height); ctx.drawImage(image, 0, 0);
  const pixels = ctx.getImageData(0, 0, canvas.width, canvas.height), data = pixels.data;
  for (let i = 0; i < data.length; i += 4) {
    if (!mask[i + 3]) { data[i + 3] = 0; continue; }
    const red = data[i], green = data[i + 1], blue = data[i + 2];
    // White stone and lit coral are brighter than the dark water/arch interiors.
    const stone = (Math.min(green, blue) - 62) / 22;
    const coral = Math.min((red - 108) / 28, (red - green - 24) / 22, (green - 40) / 18);
    let alpha = Math.max(0, Math.min(1, Math.max(stone, coral)));
    alpha = alpha * alpha * (3 - 2 * alpha);
    data[i + 3] = Math.round(mask[i + 3] * alpha);
  }
  ctx.putImageData(pixels, 0, 0);
  return canvas;
}
export class Occlusion {
  constructor(image, config) {
    this.enabled = !!image && config.occlusion && config.background === config.occlusionBackground;
    if (!this.enabled) return;
    this.back = depthLayer(image, [...BUILDINGS, ...REEFS, ...FRONT_REEFS]);
    this.front = depthLayer(image, FRONT_REEFS);
    const fit = (config.backgroundFit === 'cover' ? Math.max : Math.min)(config.designWidth / image.width, config.designHeight / image.height);
    this.width = image.width * fit; this.height = image.height * fit;
    this.x = (config.designWidth - this.width) / 2; this.y = (config.designHeight - this.height) / 2;
  }
  draw(ctx, depth) {
    if (this.enabled) ctx.drawImage(this[depth], this.x, this.y, this.width, this.height);
  }
  dispose() { for (const image of [this.back, this.front]) if (image) image.width = image.height = 1; this.back = this.front = null; }
}
