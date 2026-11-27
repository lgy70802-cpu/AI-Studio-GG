const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { connect } = require('./tools-theme-common.cjs');

(async () => {
  const { browser, page, req } = await connect(Number(process.argv[2] || 9228));
  const results = [];
  try {
    const initial = await req('devices');
    assert(initial.devices.every(d => !d.wantsPlaying), 'requires a paused test workspace');
    await page.getByRole('button', { name: '资源库', exact: true }).click();
    const png = await page.evaluate(() => {
      const canvas = document.createElement('canvas'); canvas.width = 1920; canvas.height = 462;
      const context = canvas.getContext('2d'); context.fillStyle = '#c0eaff'; context.fillRect(0, 0, 1920, 462);
      context.fillStyle = '#5e45f8'; context.fillRect(0, 0, 500, 462);
      return canvas.toDataURL().split(',')[1];
    });
    await page.locator('input[type=file]').setInputFiles({ name: 'release-wallpaper.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') });
    await page.waitForTimeout(700);
    await page.locator('input[type=file]').setInputFiles(path.join(__dirname, 'tests/fixtures/color-bars.mp4'));
    const videoHash = createHash('sha256').update(fs.readFileSync(path.join(__dirname, 'tests/fixtures/color-bars.mp4'))).digest('hex');
    let snapshot, video, photo;
    for (let i = 0; i < 20; i++) {
      await page.waitForTimeout(500);
      snapshot = await req('library-load');
      video = snapshot.themes.find(t => t.category === '视频' && t.contentHash === videoHash && !t.deletedAt);
      photo = snapshot.themes.find(t => t.displayName === 'release-wallpaper' && !t.deletedAt);
      if (video && photo) break;
    }
    assert(photo?.sourceWidth === 1920 && video?.durationSeconds > 0, 'real image/video import');
    results.push('image/video imported with native metadata');
    const devices = initial.devices.filter(d => d.status === '已连接');
    assert(devices.length, 'real MiraBox required');
    for (const device of devices) {
      await req('apply-media', { serial: device.serial, themeId: photo.id });
      let state = (await req('devices')).devices.find(d => d.serial === device.serial);
      assert(state.playing && state.themeId === photo.id);
      await req('apply-media', { serial: device.serial, themeId: video.id });
      await page.waitForTimeout(1800);
      state = (await req('devices')).devices.find(d => d.serial === device.serial);
      assert(state.playing && state.videoPosition > 0 && state.frames > 0);
      await req('configure', { serial: device.serial, playing: false });
      const paused = (await req('devices')).devices.find(d => d.serial === device.serial).videoPosition;
      await page.waitForTimeout(700);
      state = (await req('devices')).devices.find(d => d.serial === device.serial);
      assert(Math.abs(state.videoPosition - paused) < 0.2);
      await req('configure', { serial: device.serial, playing: true });
      await req('apply-media', { serial: device.serial, themeId: 'release-qa-editor' });
      state = (await req('devices')).devices.find(d => d.serial === device.serial);
      assert(state.playing && state.themeId === 'release-qa-editor');
      await req('configure', { serial: device.serial, playing: false });
      results.push(`${device.model}: real image/video/theme USB output and pause/resume`);
    }
    fs.writeFileSync(path.join(__dirname, 'artifacts/release-qa/media.json'), JSON.stringify({ results, video: { id: video.id, seconds: video.durationSeconds }, photo: photo.id }, null, 2));
    results.forEach(result => console.log('PASS', result));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
