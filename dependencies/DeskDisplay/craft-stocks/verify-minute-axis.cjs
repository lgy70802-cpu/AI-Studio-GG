const { chromium } = require('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const assert = require('node:assert/strict');

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage();
    await page.goto('http://127.0.0.1:5186/preview.html');
    const result = await page.evaluate(async () => {
      const { drawIntradayChart, tradingMinutePosition } = await import('/src/chart.ts');
      const axis = ['0930', '1005', '1030', '1130', '1300', '1400', '1500'].map(tradingMinutePosition);
      const canvas = document.createElement('canvas');
      canvas.width = 1000;
      canvas.height = 300;
      const context = canvas.getContext('2d');
      const original = {
        beginPath: context.beginPath.bind(context),
        moveTo: context.moveTo.bind(context),
        lineTo: context.lineTo.bind(context),
        stroke: context.stroke.bind(context)
      };
      let path = [];
      const strokes = [];
      context.beginPath = function () { path = []; return original.beginPath(); };
      context.moveTo = function (x, y) { path.push([x, y]); return original.moveTo(x, y); };
      context.lineTo = function (x, y) { path.push([x, y]); return original.lineTo(x, y); };
      context.stroke = function () { strokes.push({ color: this.strokeStyle, path: [...path] }); return original.stroke(); };
      drawIntradayChart(context, [14.65, 14.71], ['0930', '1005'], 14.67, {}, 0, 0, 1000, 300);
      const priceStroke = strokes.find((item) => item.color === '#3b8cff');
      return { axis, curveEndX: priceStroke?.path.at(-1)?.[0] };
    });
    assert.deepEqual(result.axis, [0, 35, 60, 120, 120, 180, 240]);
    assert(result.curveEndX > 195 && result.curveEndX < 205, `10:05 endpoint should be near x=201, got ${result.curveEndX}`);
    console.log('PASS timestamp axis: 10:05 curve stops at its real trading-time position; lunch interval is collapsed');
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
