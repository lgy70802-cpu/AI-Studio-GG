const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
(async () => {
  const browser = await chromium.launch({ channel: process.env.BROWSER_CHANNEL || 'msedge', headless: true });
  try {
    for (const file of ['index.html', 'preview.html', 'release/Aquarium-V1.1/浏览器预览.html', 'release/Aquarium-V1.1/com.mirabox.streamdock.aquarium.mPanelPlugin/index.html']) {
      const page = await browser.newPage({ viewport: { width: 1600, height: 800 } });
      const errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto(pathToFileURL(path.join(root, file)).href);
      await page.waitForFunction(() => window.aquarium?.renderer.ready);
      assert.notEqual(await page.evaluate(() => getComputedStyle(document.body).cursor), 'none');
      assert.equal(await page.locator('#startup-error').isVisible(), false);
      const first = await page.evaluate(() => aquarium.scene.time);
      await page.waitForFunction(start => aquarium.scene.time > start, first, { timeout: 5000 });
      assert.ok(await page.evaluate(() => {
        const data = aquarium.canvas.getContext('2d').getImageData(0, 0, aquarium.canvas.width, aquarium.canvas.height).data;
        let bright = 0;
        for (let i = 0; i < data.length; i += 128) if (data[i] > 70) bright++;
        return bright > 1000;
      }), 'Canvas must contain the aquarium, not just a dark fill');
      assert.deepEqual(errors, []);
      await page.close();
      console.log('PASS animated image and visible pointer:', file);
    }
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
