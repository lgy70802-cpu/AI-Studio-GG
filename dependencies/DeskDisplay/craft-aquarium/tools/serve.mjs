import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const mime = { '.js':'text/javascript', '.html':'text/html', '.png':'image/png', '.jpg':'image/jpeg' };
http.createServer(async (req,res) => {
  try {
    const file = path.resolve(root, '.' + decodeURIComponent(new URL(req.url,'http://localhost').pathname).replace(/\/$/,'/index.html'));
    if (!file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
    const content = await fs.readFile(file); res.writeHead(200,{'Content-Type':mime[path.extname(file)] || 'application/octet-stream'}).end(content);
  } catch { res.writeHead(404).end(); }
}).listen(5188,'127.0.0.1',()=>console.log('Aquarium: http://127.0.0.1:5188'));
