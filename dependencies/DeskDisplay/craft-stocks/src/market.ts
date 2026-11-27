import type { Scene, Settings } from './stocks';
import { drawHeaderClock } from './header-clock';
import { fillTabularText } from './canvas-text';
import { tradingMinutePosition } from './chart';
export function drawMarket(canvas: HTMLCanvasElement, scene: Scene, s: Settings) {
  const c = canvas.getContext('2d')!,
    portrait = canvas.width / canvas.height < 1.2,
    W = portrait ? 640 : 1280,
    H = portrait ? 960 : 720,
    k = Math.min(canvas.width / W, canvas.height / H);
  c.save();
  c.translate((canvas.width - W * k) / 2, (canvas.height - H * k) / 2);
  c.scale(k, k);
  const text = (v: string, x: number, y: number, size = 22, color = '#eef3fb', align: CanvasTextAlign = 'left') => {
    c.font = `${size >= 28 ? 600 : 400} ${size}px "ManropeTabular","Microsoft YaHei",sans-serif`;
    c.fillStyle = color;
    c.textAlign = align;
    fillTabularText(c, v, x, y);
  };
  const card = (x: number, y: number, w: number, h: number) => {
    c.fillStyle = '#141d29';
    c.beginPath();
    c.roundRect(x, y, w, h, 12);
    c.fill();
  };
  const color = (n: number) => (n === 0 ? '#91a0b5' : n > 0 !== !!s.greenUp ? '#ff646d' : '#3bd6a0');
  const fmt = (v: number) => v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  text('市场总览', 28, 61, 40);
  text('A 股 · 指数与市场统计', 28, 97, 20, '#91a0b5');
  drawHeaderClock(c, W - 28, 58, portrait);
  const ids = ['sh000001', 'sz399001', 'sz399006'],
    iw = (W - 76) / 3;
  ids.forEach((id, i) => {
    const q = scene.quotes.find((q) => q.symbol === id),
      x = 28 + i * (iw + 10);
    card(x, 120, iw, 124);
    text(q?.name ?? ['上证指数', '深证成指', '创业板指'][i], x + 14, 151, portrait ? 18 : 22);
    text(q ? fmt(q.price) : '—', x + 14, 191, portrait ? 23 : 36);
    text(q ? `${q.percent > 0 ? '+' : ''}${q.percent.toFixed(2)}%` : '暂无行情', x + 14, 225, 21, q ? color(q.percent) : '#91a0b5');
  });
  const cx = 28,
    cy = 264,
    cw = portrait ? W - 56 : 770,
    ch = portrait ? 338 : 350;
  card(cx, cy, cw, ch);
  text('上证分时', cx + 18, cy + 36, 24);
  const line = scene.lines.sh000001,
    q = scene.quotes.find((q) => q.symbol === 'sh000001'),
    left = cx + 88,
    right = cx + cw - 22,
    top = cy + 68,
    bottom = cy + ch - 48;
  if (line?.length > 1) {
    const low = Math.min(...line),
      high = Math.max(...line),
      range = high - low || 1;
    for (let i = 0; i < 3; i++) {
      const y = top + ((bottom - top) * i) / 2;
      text(fmt(high - (range * i) / 2), left - 10, y + 6, 16, '#91a0b5', 'right');
      c.strokeStyle = '#283446';
      c.beginPath();
      c.moveTo(left, y);
      c.lineTo(right, y);
      c.stroke();
    }
    c.beginPath();
    line.forEach((v, i) => {
      const position = tradingMinutePosition(scene.lineTimes?.sh000001?.[i]),
        x = left + (position === undefined ? i / (line.length - 1) : position / 240) * (right - left),
        y = bottom - ((v - low) / range) * (bottom - top);
      i ? c.lineTo(x, y) : c.moveTo(x, y);
    });
    c.strokeStyle = color(q?.percent ?? 0);
    c.lineWidth = 2.5;
    c.stroke();
  } else text(line?.length === 1 ? '分时采样不足，等待更新' : '分时暂不可用', cx + cw / 2, cy + ch / 2, 22, '#91a0b5', 'center');
  text('当日已返回采样点', left, cy + ch - 16, 16, '#91a0b5');
  const x = portrait ? 28 : 818,
    y = portrait ? 622 : 264,
    w = portrait ? 584 : 434,
    h = portrait ? 210 : 350;
  card(x, y, w, h);
  text('市场统计', x + 18, y + 36, 24);
  text('上涨 / 下跌家数', x + 18, y + 78, 19, '#91a0b5');
  text('暂不可用', x + w - 18, y + 78, 21, '#91a0b5', 'right');
  text('两市成交额', x + 18, y + 126, 19, '#91a0b5');
  text('暂不可用', x + w - 18, y + 126, 21, '#91a0b5', 'right');
  text('暂无可靠统计数据', x + 18, y + h - 28, 17, '#73849c');
  const stamp = ids
    .map((id) => scene.quotes.find((q) => q.symbol === id)?.stamp)
    .filter(Boolean)
    .sort()[0];
  text(stamp ? `指数时间 ${stamp.slice(0, 4)}-${stamp.slice(4, 6)}-${stamp.slice(6, 8)} ${stamp.slice(8, 10)}:${stamp.slice(10, 12)}` : '指数时间 —', 28, H - 48, 18, '#91a0b5');
  text(
    scene.error || (scene.alert ? `${scene.alert.symbol} ${scene.alert.direction} ${scene.alert.price.toFixed(2)} · 价格提醒` : '行情可能延迟 · 仅供参考'),
    28,
    H - 20,
    16,
    scene.error || scene.alert ? '#f0ba55' : '#64758b'
  );
  c.restore();
}
