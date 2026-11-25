const { chromium } = require('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const assert = require('node:assert/strict');

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 1968, height: 980 } });
    const errors = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await page.addInitScript(() => {
      window.labels = [];
      const original = CanvasRenderingContext2D.prototype.fillText;
      CanvasRenderingContext2D.prototype.fillText = function (value, ...args) {
        window.labels.push(String(value));
        return original.call(this, value, ...args);
      };
    });
    await page.goto(pathToFileURL(path.resolve('preview.html')).href);
    await page.waitForFunction(() => window.stockScene?.quotes.length > 0, {}, { timeout: 25000 });
    for (const mode of ['list', 'market', 'detail']) {
      await page.selectOption('#mode', mode);
      await page.click('#apply');
      await page.waitForTimeout(1100);
      const labels = await page.evaluate(() => window.labels.splice(0).join(''));
      assert(/\d{1,2}月\d{1,2}日 星期[一二三四五六日]/.test(labels), `${mode}: missing date`);
      assert(/\d{2}:\d{2}:\d{2}/.test(labels), `${mode}: missing time`);
    }
    const before = await page.locator('canvas').evaluate((canvas) => canvas.toDataURL());
    await page.waitForTimeout(1200);
    const after = await page.locator('canvas').evaluate((canvas) => canvas.toDataURL());
    assert.notEqual(before, after, 'clock did not tick');
    await page.locator('canvas').screenshot({ path: 'd92-header-clock.png' });
    assert.deepEqual(errors, []);
    console.log('PASS date, Chinese weekday, HH:mm:ss on all three pages; clock advances every second; no errors');
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
