// Isolated real API/PostgreSQL acceptance. Never reads or replaces the developer .env.
import { randomBytes, randomUUID, createHash } from 'node:crypto';
import { spawn } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import { createWriteStream } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';
import net from 'node:net';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
function option(name) { const index = process.argv.indexOf(name); return index < 0 ? undefined : process.argv[index + 1]; }
const admin = resolve(option('--admin-root') || resolve(root, '../d92-studio-admin'));
const client = option('--client');
if (!client) throw Error('Supply --client <built DeskDisplay.DisplayTests.exe>');
const output = resolve(root, 'artifacts', 'studio-pipeline-' + randomUUID()); await mkdir(output, { recursive: true });
const require = createRequire(resolve(admin, 'package.json')); const sharp = require('sharp');
const { default: EmbeddedPostgres } = await import(pathToFileURL(resolve(admin, '.test-postgres/node_modules/embedded-postgres/dist/index.js')));
const { default: binariesFor } = await import(pathToFileURL(resolve(admin, '.test-postgres/node_modules/embedded-postgres/dist/binary.js')));
async function freePort() { const server = net.createServer(); await new Promise((ok, fail) => { server.once('error', fail); server.listen(0, '127.0.0.1', ok); }); const port = server.address().port; await new Promise(ok => server.close(ok)); return port; }
const port = await freePort(), apiPort = await freePort();
const password = randomBytes(24).toString('hex'), adminPassword = randomBytes(24).toString('hex');
const databaseDir = resolve(output, 'postgres');
const pg = new EmbeddedPostgres({ databaseDir, user: 'pipeline', password, port, persistent: true, authMethod: 'scram-sha-256', onLog() {}, onError() {} });
const binaries = await binariesFor();
const origin = `http://127.0.0.1:${apiPort}`, server = origin;
const env = { ...process.env, DATABASE_URL: `postgresql://pipeline:${password}@127.0.0.1:${port}/pipeline?schema=public`, SEED_ADMIN_EMAIL: 'pipeline@example.test', SEED_ADMIN_PASSWORD: adminPassword, API_PORT: String(apiPort), ADMIN_ORIGIN: origin, NODE_ENV: 'development', STORAGE_ROOT: resolve(output, 'private-storage') };
function run(exe, args, extra = {}) {
  return new Promise((ok, fail) => {
    const child = spawn(exe, args, { cwd: admin, env: { ...env, ...extra }, windowsHide: true });
    child.stdout.on('data', data => process.stdout.write(data.toString().replaceAll(password, '[redacted]').replaceAll(adminPassword, '[redacted]')));
    child.stderr.on('data', data => process.stderr.write(data.toString().replaceAll(password, '[redacted]').replaceAll(adminPassword, '[redacted]')));
    child.once('error', fail); child.once('exit', code => code === 0 ? ok() : fail(Error('Verification subprocess failed: ' + code)));
  });
}
let started = false, api, cookie;
async function request(path, body, method = 'POST') {
  const res = await fetch(server + '/api/v1/admin/' + path, { method, headers: { Origin: origin, ...(cookie ? { Cookie: cookie } : {}), ...(body instanceof FormData ? {} : { 'Content-Type': 'application/json' }) }, body: body instanceof FormData ? body : JSON.stringify(body) });
  if (!res.ok) throw Error(`Admin ${method} ${path}: ${res.status}`); return res;
}
try {
  await pg.initialise(); await run(binaries.pg_ctl, ['start', '-D', databaseDir, '-l', resolve(output, 'postgres.log'), '-o', `-p ${port} -h 127.0.0.1`, '-w']); started = true;
  const db = pg.getPgClient('postgres', '127.0.0.1'); await db.connect(); try { await db.query('CREATE DATABASE pipeline'); } finally { await db.end(); }
  console.log('Isolated PostgreSQL ready; applying committed migrations.');
  await run(process.execPath, ['node_modules/prisma/build/index.js', 'migrate', 'deploy', '--schema', 'packages/database/prisma/schema.prisma']);
  await run(process.execPath, ['--import', 'tsx', 'packages/database/src/seed.ts']);
  await run(process.execPath, ['node_modules/typescript/bin/tsc', '-p', 'packages/shared/tsconfig.json']);
  await run(process.execPath, ['node_modules/typescript/bin/tsc', '-p', 'apps/api/tsconfig.json']);
  const log = createWriteStream(resolve(output, 'api.log')); api = spawn(process.execPath, ['apps/api/dist/main.js'], { cwd: admin, env, windowsHide: true }); api.stdout.pipe(log); api.stderr.pipe(log);
  let ready = false;
  for (let i = 0; i < 50; i++) { try { ready = (await fetch(server + '/api/v1/health/ready')).ok; } catch {} if (ready) break; await new Promise(ok => setTimeout(ok, 300)); }
  if (!ready) throw Error('Isolated API did not become ready');
  const login = await request('auth/login', { email: env.SEED_ADMIN_EMAIL, password: adminPassword }); cookie = login.headers.get('set-cookie')?.split(';')[0]; if (!cookie) throw Error('Admin cookie missing');
  const resource = await (await request('resources', { name: '[隔离验收] Web → Windows → Theme', slug: 'pipeline-' + randomUUID(), type: 'STATIC_WALLPAPER', accessPolicy: 'FREE', description: 'Real generated PNG for isolated cross-stack acceptance.' })).json();
  const png = await sharp(Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="1920" height="462"><rect width="1920" height="462" fill="#203F68"/><circle cx="1500" cy="231" r="180" fill="#4C85F6"/><text x="60" y="250" font-size="68" fill="white">D92 STUDIO PIPELINE</text></svg>')).png().toBuffer();
  const sha = createHash('sha256').update(png).digest('hex');
  const form = new FormData(); form.set('version', '1.0.0'); form.set('file', new Blob([png], { type: 'image/png' }), 'pipeline.png');
  await request(`resources/${resource.id}/package`, form); await request(`resources/${resource.id}/publish`, {});
  const descriptor = resolve(output, 'pipeline.json'); await writeFile(descriptor, JSON.stringify({ serverUrl: server, resourceId: resource.id, expectedSha256: sha, outputDirectory: output }, null, 2));
  console.log('Real management API uploaded and published PNG. Running Windows client/render checks.');
  await run(resolve(client), [], { DESKDISPLAY_TEST_SUITE: 'studio-pipeline', DESKDISPLAY_PIPELINE_DESCRIPTOR: descriptor });
  console.log('PIPELINE_EVIDENCE=' + output);
} finally {
  if (cookie && api?.exitCode === null) await request('auth/logout', {}).catch(() => {});
  if (api && api.exitCode === null) { api.kill(); await new Promise(ok => api.once('exit', ok)); }
  if (started) await run(binaries.pg_ctl, ['stop', '-D', databaseDir, '-m', 'fast', '-w']);
}
