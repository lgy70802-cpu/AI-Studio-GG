const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { connect } = require('./tools-theme-common.cjs');

(async () => {
  const { browser, page, req } = await connect(Number(process.argv[2] || 9228));
  const out = path.join(__dirname, 'artifacts/release-qa');
  fs.mkdirSync(out, { recursive: true });
  const results = [];
  const check = (value, name) => { assert(value, name); results.push(name); console.log('PASS', name); };
  try {
    check((await req('devices')).devices.every(d => !d.wantsPlaying), 'requires a paused test workspace');
    const window = await req('debug-window-state');
    const { bounds: b, workArea: a } = window;
    check(Math.abs((b.left + b.right) - (a.left + a.right)) <= 4 &&
      Math.abs((b.top + b.bottom) - (a.top + a.bottom)) <= 4, 'normal startup centered in work area');
    fs.writeFileSync(path.join(out, 'window.json'), JSON.stringify(window, null, 2));

    let snapshot = await req('library-load');
    const photo = snapshot.themes.find(t => !t.deletedAt && t.category === '壁纸' && t.imageUrl?.startsWith('https://media.studio.local/'));
    assert(photo);
    const id = 'release-qa-editor';
    const image = index => ({ id: `release-image-${index}`, kind: 'image', src: photo.imageUrl,
      a: 'mc', x: index * 10, y: 0, w: 50, h: 50, size: 50, ar: 2, fit: 'contain', alpha: 1 });
    snapshot.themes = snapshot.themes.filter(t => t.id !== id);
    snapshot.themes.unshift({ ...photo, id, name: '发布验收工程', displayName: '发布验收工程', category: '主题',
      source: 'custom', inLibrary: true, deletedAt: undefined, resolution: '1920×462', editorProject: {
        schema_version: '2.0.0', template_id: id, name: '发布验收工程', category: '主题',
        updated_at: Date.now(), bg: { kind: 'color', color: '#112233' }, master: [image(0), image(1)],
        layouts: [{ size: [1920, 462], follow: false }, { size: [462, 1920], follow: true }], locks: {}, names: {},
      } });
    await req('library-save', { snapshot });
    await page.reload();
    await page.getByRole('button', { name: '资源库', exact: true }).click();
    const card = page.locator('div.group').filter({ has: page.getByText('发布验收工程', { exact: true }) }).first();
    await card.getByRole('button', { name: '在主题编辑器打开', exact: true }).evaluate(e => e.click());
    await page.getByLabel('主题名称').waitFor();
    check(await page.locator('[data-te-canvas] [data-el-id]').count() === 2, 'editor loads both source layers');
    await page.evaluate(() => {
      const original = Storage.prototype.setItem;
      window.releaseOriginalStorage = original;
      Storage.prototype.setItem = function (key, value) {
        if (key.startsWith('d92-te-draft-')) throw new DOMException('QA quota failure', 'QuotaExceededError');
        return original.call(this, key, value);
      };
    });
    await page.getByLabel('主题名称').fill('发布验收_保存失败提醒');
    await page.getByRole('alert').filter({ hasText: '草稿保存失败' }).waitFor();
    check(true, 'autosave failure is visible');
    await page.evaluate(() => { Storage.prototype.setItem = window.releaseOriginalStorage; });
    await page.getByRole('button', { name: '保存', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: '草稿保存失败' }).waitFor({ state: 'hidden' });
    snapshot = await req('library-load');
    check(snapshot.themes.find(t => t.id === id).editorProject.master.length === 2, 'manual save preserves layers after autosave failure');
    await page.getByRole('button', { name: '返回', exact: true }).click();
    await page.getByRole('button', { name: '设置', exact: true }).click();
    const backup = path.join(out, 'release-full.mirabox');
    await page.evaluate(({ backup, url }) => {
      localStorage.setItem('d92-te-custom-assets-v1', JSON.stringify([{ id: 'release-preset', name: '迁移预设',
        elements: [{ id: 'preset-image', kind: 'image', src: url, a: 'mc', x: 0, y: 0, w: 100, h: 100 }] }]));
      const bridge = chrome.webview, send = bridge.postMessage.bind(bridge);
      window.releaseBridgeSend = send;
      bridge.postMessage = message => {
        if (message.command === 'package-export') window.releaseHeldExport = { ...message, path: backup };
        else send(message.command === 'package-inspect' ? { ...message, path: backup } : message);
      };
    }, { backup, url: photo.imageUrl });
    await page.clock.install();
    await page.getByRole('button', { name: '导出完整备份', exact: true }).click();
    await page.waitForFunction(() => Boolean(window.releaseHeldExport));
    await page.clock.fastForward(61000);
    check(await page.getByRole('button', { name: '导出完整备份', exact: true }).isDisabled(), 'backup remains pending after 61 seconds');
    await page.evaluate(() => { window.releaseBridgeSend(window.releaseHeldExport); });
    await page.getByRole('button', { name: '导出完整备份', exact: true }).waitFor();
    await page.waitForFunction(() => [...document.querySelectorAll('button')].some(b => b.textContent === '导出完整备份' && !b.disabled));
    check(fs.existsSync(backup), 'delayed backup completes successfully');
    await page.clock.resume();
    for (const mode of ['merge', 'restore']) {
      await page.evaluate(() => localStorage.setItem('d92-te-custom-assets-v1', JSON.stringify([
        { id: 'release-preset', name: '原有预设', elements: [] }
      ])));
      await page.getByRole('button', { name: '导入或恢复备份包', exact: true }).click();
      await page.getByRole('dialog', { name: '备份包导入预览' }).waitFor();
      await page.getByRole('button', { name: mode === 'merge' ? '合并导入，保留原内容' : '恢复整份备份并暂停输出', exact: true }).click();
      await page.waitForTimeout(1800);
      const presets = await page.evaluate(() => JSON.parse(localStorage.getItem('d92-te-custom-assets-v1') || '[]'));
      check(presets.some(p => p.name === '迁移预设') && (mode !== 'merge' || presets.some(p => p.name === '原有预设')),
        `${mode} imports custom presets with collision handling`);
      check((await req('devices')).devices.every(d => !d.wantsPlaying), `${mode} retains paused test outputs`);
      await page.getByRole('button', { name: '设置', exact: true }).click();
      await page.evaluate(backup => {
        const bridge = chrome.webview, send = bridge.postMessage.bind(bridge);
        bridge.postMessage = message => send(message.command === 'package-inspect' ? { ...message, path: backup } : message);
      }, backup);
    }
    fs.writeFileSync(path.join(out, 'release-ui.json'), JSON.stringify({ checks: results, window }, null, 2));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
