import { drawUltrawide } from './ultrawide';
import { drawHeaderClock } from './header-clock';
import { fillTabularText } from './canvas-text';
import { rotation } from './rotation';
import { drawMarket } from './market';
import { AlertTracker, type Alert } from './alerts';
import { drawDetail } from './detail';
import { tradingMinutePosition } from './chart';
export interface Quote {
  symbol: string;
  name: string;
  price: number;
  percent: number;
  stamp: string;
  open?: number;
  high?: number;
  low?: number;
  previous?: number;
}
export interface Scene {
  quotes: Quote[];
  lines: Record<string, number[]>;
  lineTimes?: Record<string, string[]>;
  error: string;
  alert?: Alert;
}
export interface Settings {
  mode?: 'list' | 'market' | 'detail' | 'rotate';
  rotateList?: boolean;
  rotateMarket?: boolean;
  rotateDetail?: boolean;
  listSeconds?: number;
  marketSeconds?: number;
  detailSeconds?: number;
  focus?: string;
  fontScale?: number;
  sort?: 'manual' | 'gain' | 'loss';
  alertEnabled?: boolean;
  alertAbove?: number;
  alertBelow?: number;
  symbols?: string;
  interval?: number;
  pageSeconds?: number;
  greenUp?: boolean;
  ActionGeometry?: { width: number; height: number };
}
export const defaults = {
  symbols: 'sh600519,sz300750,sh600036,sh688981',
  interval: 10,
  pageSeconds: 12,
  greenUp: false,
  mode: 'list' as const,
  focus: '',
  fontScale: 1,
  sort: 'manual' as const,
  alertEnabled: false
};
export const indices = ['sh000001', 'sz399001', 'sz399006'];
export function normalizeSymbol(input: string) {
  const code = input.trim().toLowerCase();
  if (/^6\d{5}$/.test(code)) return 'sh' + code;
  if (/^[03]\d{5}$/.test(code)) return 'sz' + code;
  return code;
}
export function symbols(input: string) {
  const a = input
    .toLowerCase()
    .split(/[\s,，;；]+/)
    .filter(Boolean)
    .map(normalizeSymbol);
  if (!a.length || a.length > 20 || a.some((s) => !/^(sh|sz|bj)\d{6}$/.test(s))) throw Error('请输入 1–20 个股票代码，如 600519、300750；其他市场请带前缀');
  return [...new Set(a)];
}
export function parseQuotes(text: string): Quote[] {
  const out: Quote[] = [];
  for (const m of text.matchAll(/v_((?:sh|sz|bj)\d{6})="([^"]*)"/g)) {
    const f = m[2].split('~'),
      price = Number(f[3]),
      percent = Number(f[32]);
    if (f.length < 35 || !f[1] || !/^\d{14}$/.test(f[30]) || !Number.isFinite(price) || price <= 0 || !f[32] || !Number.isFinite(percent)) continue;
    out.push({
      symbol: m[1],
      name: f[1],
      price,
      percent,
      stamp: f[30],
      open: Number(f[5]) || undefined,
      high: Number(f[33]) || undefined,
      low: Number(f[34]) || undefined,
      previous: Number(f[4]) || undefined
    });
  }
  return out;
}
async function request(url: string, signal: AbortSignal) {
  const r = await fetch(url, { signal, cache: 'no-store' });
  if (!r.ok) throw Error(`行情请求失败 ${r.status}`);
  return r;
}
export async function getQuotes(codes: string[], signal: AbortSignal) {
  const r = await request('https://qt.gtimg.cn/q=' + codes.join(','), signal);
  const q = parseQuotes(new TextDecoder('gbk').decode(await r.arrayBuffer()));
  if (!q.length) throw Error('未收到有效行情');
  return q;
}
export async function minute(code: string, stamp: string, signal: AbortSignal) {
  const r = await request('https://web.ifzq.gtimg.cn/appstock/app/minute/query?code=' + code, signal);
  const b = await r.json(),
    d = b.data?.[code]?.data;
  if (String(d?.date).replaceAll('-', '') !== stamp.slice(0, 8)) return { values: [], times: [] };
  const values: number[] = [],
    times: string[] = [];
  for (const row of d.data ?? []) {
    const [time, rawPrice] = String(row).trim().split(/\s+/),
      price = Number(rawPrice);
    if (!/^\d{4}$/.test(time) || tradingMinutePosition(time) === undefined || !Number.isFinite(price) || price <= 0) continue;
    values.push(price);
    times.push(time);
  }
  return { values, times };
}
export function createFeed(settings: Settings, changed: (scene: Scene) => void, tracker = new AlertTracker()) {
  const scene: Scene = { quotes: [], lines: {}, lineTimes: {}, error: '正在连接行情…' };
  let stopped = false,
    busy = false,
    last = 0,
    historyTime = 0,
    controller: AbortController | undefined,
    codes: string[],
    focusCode = '';
  try {
    codes = symbols(settings.symbols ?? defaults.symbols);
    if (settings.focus) {
      const focus = symbols(settings.focus);
      if (focus.length !== 1) throw Error('聚焦代码只能填写一只股票');
      focusCode = focus[0];
      codes = [...new Set([...codes, ...focus])];
    }
  } catch (e) {
    scene.error = (e as Error).message;
    changed(scene);
    return { tick: () => {}, stop: () => {} };
  }
  async function tick() {
    if (stopped || busy || Date.now() - last < Math.max(10, Math.min(300, Number(settings.interval) || 10)) * 1000) return;
    busy = true;
    last = Date.now();
    controller = new AbortController();
    const timer = setTimeout(() => controller?.abort(), 12000);
    try {
      const result = await getQuotes([...new Set([...indices, ...codes])], controller.signal);
      if (stopped) return;
      const old = scene.quotes;
      scene.quotes = result;
      for (const id of Object.keys(scene.lines)) {
        if (!result.some((q) => q.symbol === id && q.stamp.slice(0, 8) === old.find((o) => o.symbol === id)?.stamp.slice(0, 8))) {
          delete scene.lines[id];
          delete scene.lineTimes?.[id];
        }
      }
      scene.alert = tracker.update(
        result.find((q) => q.symbol === (focusCode || codes[0])),
        settings
      );
      const missing = codes.filter((s) => !result.some((q) => q.symbol === s));
      scene.error = missing.length ? '无行情：' + missing.join(', ') : '';
      changed(scene);
      if (Date.now() - historyTime >= 60000) {
        historyTime = Date.now();
        const historyCodes = [...new Set([...(settings.mode === 'market' || (settings.mode === 'rotate' && settings.rotateMarket !== false) ? ['sh000001'] : []), ...codes])];
        for (let i = 0; i < historyCodes.length && !stopped; i += 4) {
          await Promise.allSettled(
            historyCodes.slice(i, i + 4).map(async (id) => {
              const q = result.find((q) => q.symbol === id);
              if (q) {
                const series = await minute(id, q.stamp, controller!.signal);
                if (!stopped) {
                  scene.lines[id] = series.values;
                  scene.lineTimes![id] = series.times;
                }
              }
            })
          );
          if (!stopped) changed(scene);
        }
      }
    } catch (e) {
      if (!stopped) {
        scene.error = '连接异常 · 保留上次行情';
        console.warn(JSON.stringify({ event: 'stocks_fetch_failed', message: String(e) }));
        changed(scene);
      }
    } finally {
      clearTimeout(timer);
      busy = false;
    }
  }
  return {
    tick,
    stop() {
      stopped = true;
      controller?.abort();
    }
  };
}
export function draw(canvas: HTMLCanvasElement, scene: Scene, s: Settings, page = 0) {
  s = { ...s, focus: normalizeSymbol(s.focus ?? '') };
  const W = Math.max(1, Math.min(4096, Number(s.ActionGeometry?.width) || 1280)),
    H = Math.max(1, Math.min(4096, Number(s.ActionGeometry?.height) || 720));
  if (canvas.width !== W) canvas.width = W;
  if (canvas.height !== H) canvas.height = H;
  const c = canvas.getContext('2d')!;
  c.fillStyle = '#0b1018';
  c.fillRect(0, 0, W, H);
  let selection: string[] = [];
  try {
    selection = symbols(s.symbols ?? defaults.symbols);
  } catch {}
  const plan = rotation(s, Math.ceil(selection.length / 4));
  const active = plan[page % plan.length];
  if (W / H >= 3) {
    drawUltrawide(canvas, scene, s, active, selection);
    return;
  }
  if (active.kind === 'market') {
    drawMarket(canvas, scene, s);
    return;
  }
  if (active.kind === 'detail') {
    drawDetail(canvas, scene, s, s.focus || selection[0] || '');
    return;
  }
  page = active.page;
  const narrow = W / H < 1.2,
    bw = narrow ? 640 : 1280,
    bh = narrow ? 960 : 720,
    k = Math.min(W / bw, H / bh);
  c.save();
  c.translate((W - bw * k) / 2, (H - bh * k) / 2);
  c.scale(k, k);
  const color = (n: number) => (n === 0 ? '#abb6c7' : n > 0 !== !!s.greenUp ? '#ff646d' : '#3bd6a0');
  function text(t: string, x: number, y: number, size = 22, col = '#eef3fb', align: CanvasTextAlign = 'left') {
    c.fillStyle = col;
    c.font = `${size >= 30 ? 600 : 400} ${size * Math.max(0.85, Math.min(1.15, Number(s.fontScale) || 1))}px "ManropeTabular","Microsoft YaHei",sans-serif`;
    c.textAlign = align;
    fillTabularText(c, t, x, y);
  }
  function card(x: number, y: number, w: number, h: number) {
    c.fillStyle = '#141d29';
    c.beginPath();
    c.roundRect(x, y, w, h, 12);
    c.fill();
  }
  const fmt = (n: number) => n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
    pct = (n: number) => (n > 0 ? '+' : '') + n.toFixed(2) + '%';
  text('自选行情', 28, 64, 42);
  text('A 股 · 自选列表', 30, 98, 20, '#91a0b5');
  drawHeaderClock(c, bw - 28, 59, narrow);
  const iw = (bw - 76) / 3;
  indices.forEach((id, i) => {
    const q = scene.quotes.find((q) => q.symbol === id),
      x = 28 + i * (iw + 10);
    card(x, 126, iw, 112);
    text(q?.name ?? ['上证指数', '深证成指', '创业板指'][i], x + 14, 158, narrow ? 17 : 22);
    text(q ? fmt(q.price) : '—', x + 14, 195, narrow ? 23 : 32, q ? color(q.percent) : '#91a0b5');
    text(q ? pct(q.percent) : '等待行情', x + 14, 222, 17, q ? color(q.percent) : '#91a0b5');
  });
  let codes: string[] = [];
  try {
    codes = symbols(s.symbols ?? defaults.symbols);
  } catch {}
  if (s.sort === 'gain' || s.sort === 'loss') {
    const sign = s.sort === 'gain' ? -1 : 1;
    codes.sort((a, b) => {
      const x = scene.quotes.find((q) => q.symbol === a)?.percent,
        y = scene.quotes.find((q) => q.symbol === b)?.percent;
      return x === undefined ? 1 : y === undefined ? -1 : sign * (x - y);
    });
  }
  const count = Math.max(1, Math.ceil(codes.length / 4)),
    visible = codes.slice((page % count) * 4, (page % count) * 4 + 4),
    rh = narrow ? 133 : 84;
  visible.forEach((id, i) => {
    const q = scene.quotes.find((q) => q.symbol === id),
      y = 260 + i * (rh + 10);
    card(28, y, bw - 56, rh);
    text(q?.name ?? id, 46, y + 35, 26);
    text(id, 46, y + 63, 17, '#91a0b5');
    text(q ? fmt(q.price) : '—', bw - 185, y + 38, 30, '#eef3fb', 'right');
    text(q ? pct(q.percent) : '暂无行情', bw - 46, y + 38, 24, q ? color(q.percent) : '#91a0b5', 'right');
    const line = scene.lines[id],
      x = narrow ? 48 : 355,
      lw = narrow ? bw - 96 : 360,
      ly = narrow ? y + 86 : y + 18,
      lh = narrow ? 25 : 45;
    if (line?.length > 1) {
      const lo = Math.min(...line),
        range = Math.max(...line) - lo || 1;
      c.beginPath();
      line.forEach((v, j) => {
        const position = tradingMinutePosition(scene.lineTimes?.[id]?.[j]),
          px = x + (position === undefined ? j / (line.length - 1) : position / 240) * lw,
          py = ly + lh - ((v - lo) / range) * lh;
        j ? c.lineTo(px, py) : c.moveTo(px, py);
      });
      c.strokeStyle = color(q?.percent ?? 0);
      c.lineWidth = 2;
      c.stroke();
    } else text('分时暂不可用', x, ly + 22, 16, '#64758b');
  });
  const stamp = scene.quotes.map((q) => q.stamp).sort()[0],
    time = stamp ? `${stamp.slice(0, 4)}-${stamp.slice(4, 6)}-${stamp.slice(6, 8)} ${stamp.slice(8, 10)}:${stamp.slice(10, 12)}` : '—';
  text('行情时间 ' + time, 28, bh - 48, narrow ? 17 : 19, '#91a0b5');
  text(
    scene.error || (scene.alert ? `${scene.alert.symbol} ${scene.alert.direction} ${scene.alert.price.toFixed(2)} · 价格提醒` : '行情可能延迟 · 仅供参考'),
    28,
    bh - 20,
    16,
    scene.error || scene.alert ? '#f0ba55' : '#64758b'
  );
  text(`${(page % count) + 1} / ${count}`, bw - 28, bh - 20, 18, '#91a0b5', 'right');
  c.restore();
}

export function screenSeconds(s: Settings, page: number) {
  let count = 1;
  try {
    count = Math.ceil(symbols(s.symbols ?? defaults.symbols).length / 4);
  } catch {}
  const plan = rotation(s, count);
  return plan[page % plan.length].seconds;
}
