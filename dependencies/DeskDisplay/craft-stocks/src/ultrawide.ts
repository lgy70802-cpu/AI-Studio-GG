import type { Scene, Settings } from './stocks';
import type { Screen } from './rotation';
import { drawIntradayChart, tradingMinutePosition } from './chart';
import { drawHeaderClock } from './header-clock';
import { fillTabularText, tabularTextWidth } from './canvas-text';

// Keep the actual aspect ratio: a 462-unit height is a design grid, not a device constraint.
export function drawUltrawide(canvas: HTMLCanvasElement, scene: Scene, s: Settings, screen: Screen, codes: string[]) {
  const c = canvas.getContext('2d')!,
    H = 462,
    W = (canvas.width / canvas.height) * H,
    k = canvas.height / H;
  c.save();
  c.scale(k, k);
  const muted = '#91a0b5',
    white = '#eef3fb',
    amber = '#f0ba55';
  const color = (v: number) => (v === 0 ? muted : v > 0 !== !!s.greenUp ? '#ff646d' : '#3bd6a0');
  const fmt = (v?: number) => (v !== undefined && Number.isFinite(v) ? v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—');
  const pct = (v: number) => (v > 0 ? '+' : '') + v.toFixed(2) + '%';
  function text(t: string, x: number, y: number, size = 22, col = white, width = W - x - 22) {
    c.font = `${size >= 28 ? 600 : 400} ${size}px "ManropeTabular","Microsoft YaHei",sans-serif`;
    c.fillStyle = col;
    c.textAlign = 'left';
    while (tabularTextWidth(c, t) > width && t.length > 1) t = t.slice(0, -2) + '…';
    fillTabularText(c, t, x, y);
  }
  function card(x: number, y: number, w: number, h: number) {
    c.fillStyle = '#141d29';
    c.beginPath();
    c.roundRect(x, y, w, h, 12);
    c.fill();
  }
  function line(id: string, x: number, y: number, w: number, h: number) {
    const values = scene.lines[id],
      q = scene.quotes.find((q) => q.symbol === id);
    if (!values || values.length < 2) {
      text(values?.length ? '等待更多分时采样' : '分时暂不可用', x + 12, y + h / 2, 20, muted, w - 24);
      return;
    }
    const lo = Math.min(...values),
      hi = Math.max(...values),
      range = hi - lo || Math.max(1, hi * 0.01);
    for (let i = 0; i < 3; i++) {
      c.strokeStyle = '#263347';
      c.beginPath();
      c.moveTo(x, y + (i * h) / 2);
      c.lineTo(x + w, y + (i * h) / 2);
      c.stroke();
    }
    c.beginPath();
    values.forEach((v, i) => {
      const position = tradingMinutePosition(scene.lineTimes?.[id]?.[i]),
        px = x + (position === undefined ? i / (values.length - 1) : position / 240) * w,
        py = y + h * 0.9 - ((v - lo) / range) * h * 0.8;
      i ? c.lineTo(px, py) : c.moveTo(px, py);
    });
    c.strokeStyle = color(q?.percent ?? 0);
    c.lineWidth = 2.5;
    c.stroke();
  }
  const focus = s.focus || codes[0] || '',
    ids = ['sh000001', 'sz399001', 'sz399006'];
  text(screen.kind === 'list' ? '自选行情' : screen.kind === 'market' ? '市场总览' : '个股聚焦', 24, 49, 34);
  text('A 股 · 腾讯行情', 228, 47, 20, muted);
  drawHeaderClock(c, W - 24, 48);
  const foot = scene.error || (scene.alert ? `${scene.alert.symbol} ${scene.alert.direction} ${fmt(scene.alert.price)} · 价格提醒` : '行情可能延迟 · 仅供参考');
  const stamp = (screen.kind === 'detail' ? scene.quotes.filter((q) => q.symbol === focus) : scene.quotes).map((q) => q.stamp).sort()[0];
  text(foot, 24, H - 20, 18, scene.error || scene.alert ? amber : muted, W * 0.55);
  text(stamp ? `行情 ${stamp.slice(0, 4)}-${stamp.slice(4, 6)}-${stamp.slice(6, 8)} ${stamp.slice(8, 10)}:${stamp.slice(10, 12)}` : '等待行情', W - 380, H - 20, 18, muted, 356);
  function indexColumn(w: number) {
    ids.forEach((id, i) => {
      const q = scene.quotes.find((q) => q.symbol === id),
        y = 76 + i * 112;
      card(24, y, w, 100);
      text(q?.name ?? ['上证指数', '深证成指', '创业板指'][i], 40, y + 29, 20, muted, w - 32);
      text(fmt(q?.price), 40, y + 67, 32, white, w - 130);
      text(q ? pct(q.percent) : '—', 24 + w - 100, y + 68, 20, q ? color(q.percent) : muted, 88);
    });
  }
  if (screen.kind === 'list') {
    const side = Math.max(270, W * 0.165);
    indexColumn(side);
    const sorted = [...codes];
    if (s.sort === 'gain' || s.sort === 'loss')
      sorted.sort((a, b) => {
        const x = scene.quotes.find((q) => q.symbol === a)?.percent,
          y = scene.quotes.find((q) => q.symbol === b)?.percent;
        return x === undefined ? 1 : y === undefined ? -1 : (s.sort === 'gain' ? -1 : 1) * (x - y);
      });
    const x0 = side + 40,
      gap = 14,
      cw = (W - x0 - 24 - 3 * gap) / 4;
    text(`第 ${screen.page + 1} / ${Math.max(1, Math.ceil(codes.length / 4))} 页`, 410, 47, 20, muted, 150);
    sorted.slice(screen.page * 4, screen.page * 4 + 4).forEach((id, i) => {
      const x = x0 + i * (cw + gap),
        q = scene.quotes.find((q) => q.symbol === id);
      card(x, 76, cw, 324);
      const font = Math.max(0.85, Math.min(1.15, Number(s.fontScale) || 1));
      text(q?.name ?? id, x + 18, 114, 27 * font, white, cw - 36);
      text(id, x + 18, 144, 18, muted, cw - 36);
      text(fmt(q?.price), x + 18, 201, 44 * font, white, cw - 36);
      text(q ? pct(q.percent) : '暂无行情', x + 18, 240, 28 * font, q ? color(q.percent) : muted, cw - 36);
      line(id, x + 18, 270, cw - 36, 86);
      text('当日分时', x + 18, 382, 17, muted, cw - 36);
    });
  } else if (screen.kind === 'market') {
    const side = Math.max(280, W * 0.19);
    indexColumn(side);
    const x = side + 40,
      stats = Math.max(300, W * 0.22),
      chartW = W - x - stats - 40;
    card(x, 76, chartW, 324);
    text('上证分时', x + 18, 113, 25);
    line('sh000001', x + 20, 140, chartW - 40, 206);
    text('当日已返回采样点', x + 20, 380, 17, muted);
    const sx = x + chartW + 16;
    card(sx, 76, stats, 324);
    text('市场统计', sx + 20, 116, 26);
    text('上涨 / 下跌家数', sx + 20, 167, 20, muted);
    text('暂不可用', sx + 20, 206, 28, muted);
    text('两市成交额', sx + 20, 256, 20, muted);
    text('暂不可用', sx + 20, 295, 28, muted);
    text('暂无可靠统计数据', sx + 20, 378, 18, muted);
  } else {
    const q = scene.quotes.find((q) => q.symbol === focus),
      left = Math.max(330, W * 0.23),
      right = Math.max(260, W * 0.17),
      x = left + 40,
      cw = W - x - right - 40;
    card(24, 76, left, 324);
    text(q?.name ?? '等待行情', 44, 119, 30, white, left - 40);
    text(focus, 44, 151, 20, muted);
    text(fmt(q?.price), 44, 220, 56, q ? color(q.percent) : white, left - 40);
    text(q ? pct(q.percent) : '—', 44, 266, 34, q ? color(q.percent) : muted);
    text(
      scene.alert ? `${scene.alert.direction} ${fmt(scene.alert.price)} 已触发` : s.alertEnabled ? '价格提醒已开启' : '价格提醒未开启',
      44,
      373,
      21,
      scene.alert ? amber : muted,
      left - 40
    );
    card(x, 76, cw, 324);
    text('当日分时', x + 20, 114, 25);
    drawIntradayChart(c, scene.lines[focus], scene.lineTimes?.[focus], q?.previous, s, x + 12, 132, cw - 24, 250);
    const rx = x + cw + 16;
    card(rx, 76, right, 324);
    [
      ['今开', q?.open],
      ['最高', q?.high],
      ['最低', q?.low],
      ['昨收', q?.previous]
    ].forEach(([label, v], i) => {
      const y = 113 + i * 76;
      text(String(label), rx + 18, y, 19, muted);
      text(fmt(v as number | undefined), rx + 18, y + 34, 30, white, right - 36);
    });
  }
  c.restore();
}
