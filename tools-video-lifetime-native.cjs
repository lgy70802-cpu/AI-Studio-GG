const fs = require('fs'), path = require('path'), assert = require('assert/strict');
const { connect } = require('./tools-theme-common.cjs');
(async () => {
  const cycles = Number(process.argv[2] || 10);
  assert(Number.isInteger(cycles) && cycles >= 1 && cycles <= 100);
  const { browser, page, req } = await connect();
  try {
    const snapshot = await req('library-load'), devices = (await req('devices')).devices;
    const video = snapshot.themes.find(t => t.category === '视频' && t.inLibrary !== false && !t.deletedAt);
    const photo = snapshot.themes.find(t => t.category === '壁纸' && t.inLibrary !== false && !t.deletedAt);
    assert(video && photo && devices.length >= 2, 'requires managed video/photo and two real devices');
    const samples = [];
    await req('schedule-enable', { enabled: false });
    for (let cycle = 0; cycle < cycles; cycle++) {
      for (const d of devices) await req('apply-media', { serial: d.serial, themeId: video.id });
      await page.waitForTimeout(1100);
      const first = (await req('devices')).devices;
      await page.waitForTimeout(700);
      const later = (await req('devices')).devices;
      for (const d of later) {
        const before = first.find(x => x.serial === d.serial);
        assert(d.playing && d.frames > before.frames && d.videoDuration > 0, d.model + ' native video not advancing');
        assert.notEqual(d.videoPosition, before.videoPosition, d.model + ' decoder position frozen');
      }
      if (cycle === 0) {
        for (const d of devices) await req('configure', { serial: d.serial, playing: false });
        await page.waitForTimeout(500);
        const paused = (await req('devices')).devices;
        await page.waitForTimeout(700);
        for (const d of (await req('devices')).devices) assert.equal(d.videoPosition, paused.find(x => x.serial === d.serial).videoPosition);
        for (const d of devices) await req('configure', { serial: d.serial, playing: true });
      }
      samples.push({ cycle: cycle + 1, devices: later.map(({ preview, ...d }) => d) });
      for (const d of devices) await req('apply-media', { serial: d.serial, themeId: photo.id });
      for (const d of (await req('devices')).devices) assert(d.playing && d.themeId === photo.id);
      console.log('REAL_VIDEO_REUSE_CYCLE ' + (cycle + 1));
    }
    const out = path.join(__dirname, 'artifacts/reliability-qa');
    fs.mkdirSync(out, { recursive: true });
    fs.writeFileSync(path.join(out, 'video-reuse-native.json'), JSON.stringify({ cycles, twoDevices: true, nativePositionAdvances: true, pauseResume: true, samples }, null, 2));
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
