const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  try {
    const page = await browser.newPage();
    await page.goto('http://127.0.0.1:3000/?component-preview');
    await page.getByRole('button', { name: '示例 11:11:11' }).click();
    const bounds = () => page.locator('.pixbuddy-flip-clock').evaluateAll(nodes => nodes.map(node => {
      const rectangle = node.getBoundingClientRect();
      return [rectangle.x, rectangle.y, rectangle.width, rectangle.height];
    }));
    const initialBounds = await bounds();
    assert.equal(initialBounds.length, 4);
    await page.getByRole('button', { name: '示例 20:59:59' }).click();
    assert.deepEqual(await bounds(), initialBounds, 'digits keep the same bounds');
    assert(await page.locator('[data-preview-spec]').evaluateAll(nodes => nodes.every(node => {
      const canvas = node.getBoundingClientRect();
      const clock = node.querySelector('.pixbuddy-flip-clock').getBoundingClientRect();
      return clock.left >= canvas.left && clock.top >= canvas.top &&
        clock.right <= canvas.right + 0.1 && clock.bottom <= canvas.bottom + 0.1;
    })), 'clock fits each device canvas');
    await page.getByLabel('显示秒数').uncheck();
    assert.equal(await page.locator('.pixbuddy-flip-card').count(), 16);
    await page.getByLabel('24小时制').uncheck();
    assert.equal(await page.locator('.pixbuddy-flip-period').first().innerText(), 'PM');
    await page.getByRole('button', { name: '保存配置' }).click();
    await page.reload();
    assert.equal(await page.getByLabel('显示秒数').isChecked(), false);
    assert.equal(await page.getByLabel('24小时制').isChecked(), false);
    await page.clock.setFixedTime(new Date(2026, 10, 28, 0, 0, 0));
    await page.waitForTimeout(1200);
    assert.equal(await page.locator('.pixbuddy-flip-clock').first().getAttribute('aria-label'), '12:00 AM');
    await page.getByLabel('24小时制').check();
    assert.equal(await page.locator('.pixbuddy-flip-clock').first().getAttribute('aria-label'), '00:00');
    await page.emulateMedia({ reducedMotion: 'reduce' });
    assert(await page.locator('.pixbuddy-flip-digit').first().evaluate(node =>
      getComputedStyle(node).animationName === 'none'), 'reduced motion disables animation');
    console.log('PASS four device sizes, stable digits, bounds, seconds, hour format, saved configuration and reduced motion');
  } finally {
    await browser.close();
  }
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
