const fs = require('fs'), path = require('path'), assert = require('assert/strict');
const { connect } = require('./tools-theme-common.cjs');
(async () => {
  const { browser, page, req } = await connect(Number(process.argv[2] || 9228));
  try {
    assert((await req('devices')).devices.every(d => !d.wantsPlaying), 'use a paused audit workspace');
    const session = await page.context().newCDPSession(page);
    await session.send('Network.enable');
    await session.send('Network.setCacheDisabled', { cacheDisabled: true });
    await session.send('Network.setBlockedURLs', { urls: ['*fonts.googleapis.com*', '*fonts.gstatic.com*'] });
    await page.reload();
    await page.getByRole('button', { name: '首页', exact: true }).click();
    const result = await page.evaluate(async () => {
      const checks = [];
      for (const [family, weights] of [['Plus Jakarta Sans', [400, 500, 600, 700, 800]], ['JetBrains Mono', [500, 600, 700]]]) {
        for (const weight of weights) {
          const faces = await document.fonts.load(`${weight} 20px "${family}"`, 'AV0123');
          checks.push({ family, weight, faces: faces.length, loaded: faces.length > 0 && faces.every(f => f.status === 'loaded') });
        }
      }
      await document.fonts.ready;
      const resources = performance.getEntriesByType('resource').map(r => r.name);
      return { checks, localFontResources: resources.filter(r => r.includes('/fonts/')), externalFontResources: resources.filter(r => /fonts\.(googleapis|gstatic)\.com/.test(r)) };
    });
    assert(result.checks.every(c => c.loaded), 'a prototype font weight did not load locally');
    assert(result.localFontResources.some(r => r.endsWith('.woff2')));
    assert.equal(result.externalFontResources.length, 0);
    const out = path.join(__dirname, 'artifacts/reliability-qa');
    fs.mkdirSync(out, { recursive: true });
    await page.screenshot({ path: path.join(out, 'fonts-offline-home.png') });
    fs.writeFileSync(path.join(out, 'fonts-offline.json'), JSON.stringify({ externalFontsBlocked: true, ...result }, null, 2));
    await session.send('Network.setBlockedURLs', { urls: [] });
    await session.send('Network.setCacheDisabled', { cacheDisabled: false });
    await session.detach();
    console.log('ALL_8_PROTOTYPE_FONT_WEIGHTS_LOADED_WITH_GOOGLE_FONTS_BLOCKED');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
