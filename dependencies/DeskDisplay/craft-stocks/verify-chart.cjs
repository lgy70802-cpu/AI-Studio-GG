const { chromium } = require('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const assert = require('node:assert/strict');

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 1968, height: 1000 } });
    const errors = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await page.addInitScript(() => {
      window.chartLabels = [];
      const original = CanvasRenderingContext2D.prototype.fillText;
      CanvasRenderingContext2D.prototype.fillText = function (value, ...args) {
        window.chartLabels.push(String(value));
        return original.call(this, value, ...args);
      };
    });
    await page.goto(pathToFileURL(path.resolve('preview.html')).href);
    await page.selectOption('#mode', 'detail');
    await page.fill('#focus', '600519');
    await page.click('#apply');
    await page.waitForFunction(() => window.stockScene?.lines.sh600519?.length > 1, {}, { timeout: 25000 });
    await page.waitForTimeout(300);
    const labels = await page.evaluate(() => window.chartLabels.join(''));
    for (const expected of ['09:30', '10:30', '11:30/13:00', '14:00', '15:00', '0.00%']) assert(labels.includes(expected), `missing ${expected}`);
    assert(/\+\d+\.\d{2}%/.test(labels), 'missing positive percent axis');
    assert(/-\d+\.\d{2}%/.test(labels), 'missing negative percent axis');
    assert(/1,?2\d{2}\.\d{2}/.test(labels), 'missing price axis');
    assert.deepEqual(errors, []);
    await page.locator('canvas').screenshot({ path: 'd92-detail-dual-axis.png' });
    await page.click('#size');
    await page.waitForTimeout(100);
    await page.locator('canvas').screenshot({ path: 'detail-portrait-dual-axis.png' });
    console.log('PASS dual axes: left prices, right percentages, five time labels, D92 and portrait, no errors');
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
