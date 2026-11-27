const assert = require('node:assert/strict');
const fs = require('node:fs');
const { connect } = require('./tools-theme-common.cjs');

(async () => {
  const { browser, page, req } = await connect(Number(process.argv[2] || 9230));
  const results = [];
  const check = (value, description) => {
    assert(value, description);
    results.push(description);
    console.log('PASS', description);
  };
  async function openExisting() {
    await page.reload();
    await page.getByRole('button', { name: '创作中心', exact: true }).click();
    await page.getByLabel('搜索我的作品').fill('验收_竖屏默认布局');
    await page.getByRole('button', { name: '编辑', exact: true }).click();
    if (await page.getByRole('button', { name: '忽略草稿', exact: true }).count()) {
      await page.getByRole('button', { name: '忽略草稿', exact: true }).click();
    }
  }
  try {
    await openExisting();
    await page.getByLabel('主题名称', { exact: true }).fill('验收_明确丢弃');
    await page.waitForTimeout(1800);
    check(await page.evaluate(() => Boolean(localStorage.getItem('d92-te-draft-qa-portrait-layout'))), 'draft written before discard');
    await page.getByRole('button', { name: '返回', exact: true }).click();
    await page.getByRole('button', { name: '丢弃退出', exact: true }).click();
    check(await page.evaluate(() => !localStorage.getItem('d92-te-draft-qa-portrait-layout')), 'discard removes draft');
    await page.getByLabel('搜索我的作品').fill('验收_竖屏默认布局');
    await page.getByRole('button', { name: '编辑', exact: true }).click();
    check(await page.getByRole('button', { name: '恢复草稿继续编辑', exact: true }).count() === 0, 'discarded draft does not reappear');

    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const before = await page.getByLabel('主题名称', { exact: true }).inputValue();
    for (const invalid of [
      { bg: { kind: 'color', color: '#ffffff' }, master: [] },
      { bg: { kind: 'color' }, master: [], layouts: [{ size: [0, 1280] }] },
      { bg: { kind: 'color' }, master: [], layouts: [{ size: [480, 1280], elements: {} }] },
    ]) {
      await page.locator('input[type=file][accept=".json,.d92theme.json"]').setInputFiles({
        name: 'invalid.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(invalid)),
      });
      await page.waitForTimeout(100);
      check(await page.getByLabel('主题名称', { exact: true }).inputValue() === before, 'invalid JSON preserves current editor');
    }
    check(errors.length === 0, 'invalid JSON causes no React exception');

    await page.getByText('标题文字', { exact: true }).click();
    await page.waitForTimeout(1800);
    await page.getByRole('button', { name: '撤销', exact: true }).click();
    await page.getByRole('button', { name: '返回', exact: true }).click();
    check(await page.getByRole('button', { name: '保存并退出', exact: true }).count() === 0, 'undo to saved content exits without false unsaved prompt');
    check(await page.evaluate(() => !localStorage.getItem('d92-te-draft-qa-portrait-layout')), 'undo to saved content clears obsolete autosave');
    await openExisting();
    const specs = page.locator('select:visible').filter({ has: page.locator('option[value="462x1920"]') });
    const layoutBefore = await specs.inputValue();
    await specs.selectOption('462x1920');
    await page.getByRole('button', { name: '撤销', exact: true }).click();
    check(await specs.inputValue() === layoutBefore, 'undo added layout restores previous portrait layout');
    await page.getByRole('button', { name: '重做', exact: true }).click();
    check(await specs.inputValue() === '462x1920', 'redo restores added layout selection');

    const lib = await req('library-load');
    const base = structuredClone(lib.themes.find(theme => theme.source === 'local' && theme.imageUrl));
    const dimensions = [[480, 1280], [720, 1920], [1280, 480]];
    const names = ['筛选验收_当前', '筛选验收_同向', '筛选验收_横屏'];
    lib.themes = lib.themes.filter(theme => !theme.id.startsWith('qa-filter-'));
    dimensions.forEach(([w, h], index) => lib.themes.unshift({ ...base,
      id: 'qa-filter-' + index, name: names[index], displayName: names[index], inLibrary: true,
      sourceWidth: w, sourceHeight: h, resolution: `${w}×${h}`, orientation: w < h ? 'portrait' : 'landscape',
    }));
    await req('library-save', { snapshot: lib });
    await page.reload();
    await page.getByRole('button', { name: '资源库', exact: true }).click();
    const hasCard = async name => await page.getByTitle(new RegExp('点击应用「' + name + '」')).count() > 0;
    check(await hasCard(names[0]) && !await hasCard(names[1]) && !await hasCard(names[2]), 'current device size only');
    await page.getByRole('button', { name: '显示同向', exact: true }).click();
    check(await hasCard(names[0]) && await hasCard(names[1]) && !await hasCard(names[2]), 'same orientation includes other sizes');
    await page.getByRole('button', { name: '显示全部', exact: true }).click();
    check(await hasCard(names[0]) && await hasCard(names[1]) && await hasCard(names[2]), 'all includes opposite orientation');
    await page.getByRole('button', { name: '显示当前', exact: true }).click();
    check(await hasCard(names[0]) && !await hasCard(names[1]) && !await hasCard(names[2]), 'filter returns to current size');

    const card = page.getByTitle(/点击应用「筛选验收_当前」/).locator('..');
    await card.hover();
    await card.getByLabel('在主题编辑器打开', { exact: true }).click();
    if (await page.getByRole('button', { name: '忽略草稿', exact: true }).count())
      await page.getByRole('button', { name: '忽略草稿', exact: true }).click();
    await page.getByLabel('主题名称', { exact: true }).fill('验收_提交时版本');
    await page.evaluate(() => {
      const bridge = window.chrome.webview;
      const original = bridge.postMessage.bind(bridge);
      let delayed = false;
      bridge.postMessage = message => {
        if (!delayed && message.command === 'library-save' && message.snapshot?.themes?.some(theme => theme.displayName === '验收_提交时版本')) {
          delayed = true;
          setTimeout(() => original(message), 2200);
        } else original(message);
      };
      window.restoreAuditBridge = () => { bridge.postMessage = original; };
    });
    await page.getByRole('button', { name: '保存', exact: true }).click();
    await page.getByLabel('主题名称', { exact: true }).fill('验收_保存期间的新修改');
    await page.waitForTimeout(3000);
    check(await page.getByLabel('主题名称', { exact: true }).inputValue() === '验收_保存期间的新修改', 'first save preserves concurrent edits');
    await page.evaluate(() => window.restoreAuditBridge());
    await page.getByRole('button', { name: '保存', exact: true }).click();
    await page.waitForTimeout(600);
    check((await req('library-load')).themes.some(theme => theme.displayName === '验收_保存期间的新修改'), 'concurrent edits can subsequently be saved');
    await page.locator('input[type=file][accept=".json,.d92theme.json"]').setInputFiles({
      name: 'compatible.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({
        name: '验收_兼容结构导入', bg: { kind: 'color', color: '#ffffff' },
        master: [{ id: 'minimal-text', kind: 'text', a: 'mc', text: '兼容导入' }],
        layouts: [{ size: [480, 1280], follow: false }],
      })),
    });
    check(await page.getByLabel('主题名称', { exact: true }).inputValue() === '验收_兼容结构导入', 'valid older JSON gets default metadata');
    await page.locator('[data-el-id="minimal-text"]').first().click();
    await page.keyboard.press('Delete');
    check(await page.locator('[data-el-id="minimal-text"]').count() === 0 && errors.length === 0, 'imported layers can be selected and deleted safely');
    fs.writeFileSync('artifacts/editor-regressions-result.json', JSON.stringify(results, null, 2));
    console.log(`${results.length} editor and filter checks passed`);
  } finally {
    await page.evaluate(() => window.chrome.webview.postMessage({ id: 'editor-regression-close', command: 'window-control', action: 'close' })).catch(() => {});
    await browser.close().catch(() => {});
  }
})().catch(error => { console.error(error); process.exit(1); });
