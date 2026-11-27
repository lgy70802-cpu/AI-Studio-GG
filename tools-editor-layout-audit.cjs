const fs = require('fs'), path = require('path'), assert = require('assert/strict');
const { connect } = require('./tools-theme-common.cjs');
(async () => {
  const { browser, page, req } = await connect(Number(process.argv[2] || 9228));
  const session = await page.context().newCDPSession(page);
  try {
    assert((await req('devices')).devices.every(d => !d.wantsPlaying), 'use a paused audit workspace');
    const snapshot = await req('library-load');
    const theme = snapshot.themes.find(t => t.id === 'custom-qa-ui-created') || snapshot.themes.find(t => t.editorProject && t.inLibrary !== false && !t.deletedAt);
    assert(theme, 'requires a saved editor project');
    await page.getByRole('button', { name: '创作中心', exact: true }).click();
    await page.getByLabel('搜索我的作品').fill(theme.displayName);
    await page.locator('div.flex.flex-col.overflow-hidden.rounded-2xl').filter({ has: page.getByText(theme.displayName, { exact: true }) }).first().getByRole('button', { name: '编辑', exact: true }).click();
    const discard = page.getByRole('button', { name: '忽略草稿', exact: true });
    if (await discard.isVisible()) await discard.click();
    const out = path.join(__dirname, 'artifacts/reliability-qa'), results = [];
    fs.mkdirSync(out, { recursive: true });
    for (const width of [720, 1024, 1440]) {
      await session.send('Emulation.setDeviceMetricsOverride', { width, height: 1000, deviceScaleFactor: 1, mobile: false });
      await page.waitForTimeout(500);
      await page.evaluate(() => document.fonts.ready);
      const controls = [];
      for (const name of ['保存', '应用', '设备预览']) {
        const buttons = page.getByRole('button', { name, exact: true });
        let visible = 0;
        for (const button of await buttons.all()) {
          if (!await button.isVisible()) continue;
          const box = await button.boundingBox();
          assert(box && box.x >= 0 && box.x + box.width <= width + 1 && box.y >= 0 && box.y + box.height <= 1001, `${name} outside ${width}px viewport`);
          controls.push({ name, ...box }); visible++;
        }
        assert(visible > 0, name + ' not visible');
      }
      results.push({ width, controls });
      await page.screenshot({ path: path.join(out, `editor-local-fonts-${width}.png`) });
    }
    fs.writeFileSync(path.join(out, 'editor-local-fonts-layout.json'), JSON.stringify(results, null, 2));
    console.log('EDITOR_SAVE_APPLY_PREVIEW_VISIBLE_AT_ALL_3_WIDTHS');
  } finally {
    await session.send('Emulation.clearDeviceMetricsOverride').catch(() => {});
    await browser.close();
  }
})().catch(e => { console.error(e); process.exit(1); });
