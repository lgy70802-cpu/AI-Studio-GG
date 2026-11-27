const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { createHash } = require('node:crypto');
const { connect } = require('./tools-theme-common.cjs');

(async () => {
  const out = path.join(__dirname, 'artifacts/update-qa');
  const workspace = path.join(out, 'workspace');
  const installer = path.join(__dirname, 'artifacts/release/0.2.3/MiraBoxStudioNext-0.2.3-win-x64-Setup.exe');
  const bytes = fs.statSync(installer).size;
  const digest = createHash('sha256').update(fs.readFileSync(installer)).digest('hex');
  let mode = 'latest', address;
  const server = http.createServer((request, response) => {
    if (request.url === '/update.json') {
      const corrupt = mode === 'corrupt';
      response.setHeader('Content-Type', 'application/json');
      response.end(JSON.stringify({ version: mode === 'latest' ? '0.2.2' : '0.2.3', notes: '更新功能覆盖升级验收',
        url: address + (corrupt ? '/corrupt.exe' : '/setup.exe'), sha256: corrupt ? '0'.repeat(64) : digest,
        bytes: corrupt ? 8 : bytes }));
    } else if (request.url === '/corrupt.exe') response.end('MZbadexe');
    else if (request.url === '/setup.exe') {
      response.setHeader('Content-Length', bytes); fs.createReadStream(installer).pipe(response);
    } else { response.statusCode = 404; response.end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  address = 'http://127.0.0.1:' + server.address().port;
  const checks = [];
  let connection;
  try {
    connection = await connect(9228);
    const { page, req, browser } = connection;
    assert.equal((await req('updates-status')).version, '0.2.2');
    assert((await req('devices')).devices.every(d => !d.wantsPlaying), 'requires isolated paused workspace');
    const before = createHash('sha256').update(fs.readFileSync(path.join(workspace, 'library-workspace.json'))).digest('hex');
    await page.getByRole('button', { name: '设置', exact: true }).click();
    await page.getByLabel('HTTPS 更新清单地址').fill(address + '/update.json');
    await page.getByRole('button', { name: '检查更新', exact: true }).click();
    await page.getByRole('status').filter({ hasText: '当前已是最新版本' }).waitFor();
    checks.push('same version reports latest');
    mode = 'corrupt';
    await page.getByRole('button', { name: '检查更新', exact: true }).click();
    await page.getByRole('button', { name: '备份并安装更新', exact: true }).waitFor();
    await page.getByRole('button', { name: '备份并安装更新', exact: true }).click();
    await page.getByRole('status').filter({ hasText: '校验失败' }).waitFor();
    assert.equal((await req('updates-status')).version, '0.2.2');
    checks.push('checksum failure leaves old application running');
    mode = 'valid';
    await page.getByRole('button', { name: '检查更新', exact: true }).click();
    await page.getByRole('button', { name: '备份并安装更新', exact: true }).waitFor();
    await page.evaluate(() => localStorage.setItem('d92-te-custom-assets-v1', JSON.stringify([{ id: 'update-qa', name: '更新前预设', elements: [] }])));
    const progress = [];
    page.on('console', message => {
      if (message.text().startsWith('UPDATE_QA ')) progress.push(JSON.parse(message.text().slice(10)));
    });
    await page.evaluate(() => window.addEventListener('studio-update-progress', event => console.log('UPDATE_QA ' + JSON.stringify(event.detail))));
    const exited = new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error('old application did not exit')), 180000);
      const finished = () => { clearTimeout(timer); resolve(); };
      page.once('close', finished);
      browser.once('disconnected', finished);
    });
    await page.getByRole('button', { name: '备份并安装更新', exact: true }).click();
    await exited;
    connection = null;
    let restarted;
    for (let attempt = 0; attempt < 120; attempt++) {
      await new Promise(resolve => setTimeout(resolve, 1000));
      try {
        const candidate = await connect(9228);
        if ((await candidate.req('updates-status')).version === '0.2.3') { restarted = candidate; break; }
        await candidate.browser.close();
      } catch { }
    }
    assert(restarted, 'new application must restart after installer success');
    connection = restarted;
    assert.equal(createHash('sha256').update(fs.readFileSync(path.join(workspace, 'library-workspace.json'))).digest('hex'), before);
    const presets = await restarted.page.evaluate(() => JSON.parse(localStorage.getItem('d92-te-custom-assets-v1') || '[]'));
    assert(presets.some(p => p.id === 'update-qa'));
    const backups = path.join(workspace, 'Updates/Backups');
    const backup = fs.readdirSync(backups).map(name => path.join(backups, name)).find(directory => fs.existsSync(path.join(directory, 'backup.complete')));
    assert(backup && fs.statSync(path.join(backup, 'previous-program.zip')).size > 0);
    assert(fs.existsSync(path.join(backup, 'install.log')));
    checks.push('0.2.2 downloads and verifies real 0.2.3 installer');
    checks.push('configuration and previous program backed up');
    checks.push('same installation directory upgraded and automatically restarted');
    checks.push('library and custom presets preserved');
    await restarted.page.getByRole('button', { name: '设置', exact: true }).click();
    await restarted.page.getByRole('button', { name: '检查更新', exact: true }).click();
    await restarted.page.getByRole('status').filter({ hasText: '当前已是最新版本' }).waitFor();
    checks.push('updated application recognizes latest version');
    fs.writeFileSync(path.join(out, 'update-flow.json'), JSON.stringify({ checks, backup, progress }, null, 2));
    checks.forEach(check => console.log('PASS', check));
  } finally {
    if (connection) await connection.browser.close();
    server.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
