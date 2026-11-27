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
      window.numericFonts = [];
      const original = CanvasRenderingContext2D.prototype.fillText;
      CanvasRenderingContext2D.prototype.fillText = function (value, ...args) {
        if (/\d/.test(String(value))) window.numericFonts.push(this.font);
        return original.call(this, value, ...args);
      };
    });
    await page.goto(pathToFileURL(path.resolve('preview.html')).href);
    await page.waitForFunction(() => document.fonts.check('16px ManropeTabular'), {}, { timeout: 10000 });
    await page.waitForFunction(() => window.stockScene?.quotes.length > 0 && window.numericFonts.length > 20, {}, { timeout: 25000 });
    const result = await page.evaluate(async () => {
      const c = document.createElement('canvas').getContext('2d');
      c.font = '600 64px ManropeTabular';
      return {
        widths: [...'0123456789'].map((digit) => window.tabularTextWidth(c, digit)),
        fonts: window.numericFonts
      };
    });
    assert.equal(new Set(result.widths).size, 1, JSON.stringify(result.widths));
    assert(result.fonts.every((font) => font.includes('ManropeTabular')), result.fonts.find((font) => !font.includes('ManropeTabular')));
    assert.deepEqual(errors, []);
    await page.locator('canvas').screenshot({ path: 'd92-manrope-tabular.png' });
    console.log(`PASS 0–9 have one exact advance width (${result.widths[0]}px at 64px); ${result.fonts.length} numeric draws use ManropeTabular`);
  } finally {
    await browser.close();
  }
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
