import type { Scene, Settings } from './stocks';
import { drawIntradayChart } from './chart';
import { drawHeaderClock } from './header-clock';
import { fillTabularText } from './canvas-text';
export function drawDetail(canvas: HTMLCanvasElement, scene: Scene, s: Settings, code: string) {
  const c = canvas.getContext('2d')!,
    portrait = canvas.width / canvas.height < 1.2,
    W = portrait ? 640 : 1280,
    H = portrait ? 960 : 720;
  const k = Math.min(canvas.width / W, canvas.height / H);
  c.save();
  c.translate((canvas.width - W * k) / 2, (canvas.height - H * k) / 2);
  c.scale(k, k);
  const q = scene.quotes.find((q) => q.symbol === code),
    up = s.greenUp ? '#3bd6a0' : '#ff646d',
    down = s.greenUp ? '#ff646d' : '#3bd6a0',
    col = (q?.percent ?? 0) >= 0 ? up : down;
  const text = (t: string, x: number, y: number, size = 22, color = '#eef3fb', align: CanvasTextAlign = 'left') => {
    c.font = `${size >= 30 ? 600 : 400} ${size}px "ManropeTabular","Microsoft YaHei",sans-serif`;
    c.fillStyle = color;
    c.textAlign = align;
    fillTabularText(c, t, x, y);
  };
  const card = (x: number, y: number, w: number, h: number) => {
    c.fillStyle = '#141d29';
    c.beginPath();
    c.roundRect(x, y, w, h, 12);
    c.fill();
  };
  const fmt = (n?: number) => (n && Number.isFinite(n) ? n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '—');
  text('个股聚焦', 28, 60, 38);
  text(`${q?.name ?? '等待行情'} · ${code}`, 28, 97, 22, '#91a0b5');
  drawHeaderClock(c, W - 28, 57, portrait);
  text(fmt(q?.price), 28, portrait ? 174 : 195, portrait ? 64 : 88, col);
  text(q ? `${q.percent > 0 ? '+' : ''}${q.percent.toFixed(2)}%` : '—', 28, portrait ? 218 : 240, 32, col);
  const metrics = [
    ['今开', q?.open],
    ['最高', q?.high],
    ['最低', q?.low],
    ['昨收', q?.previous]
  ] as const;
  metrics.forEach(([label, value], i) => {
    const x = portrait ? 28 + (i % 2) * 298 : 730 + (i % 2) * 262,
      y = (portrait ? 242 : 120) + Math.floor(i / 2) * (portrait ? 80 : 72);
    card(x, y, portrait ? 286 : 250, portrait ? 70 : 64);
    text(label, x + 14, y + 23, 17, '#91a0b5');
    text(fmt(value), x + 14, y + 54, 26);
  });
  const y = portrait ? 418 : 280,
    h = portrait ? 350 : 285;
  card(28, y, W - 56, h);
  text('当日分时', 46, y + 34, 22);
  drawIntradayChart(c, scene.lines[code], scene.lineTimes?.[code], q?.previous, s, 42, y + 53, W - 84, h - 66);
  const alert = scene.alert,
    ay = portrait ? 790 : 584;
  card(28, ay, W - 56, 54);
  text(
    alert
      ? `${alert.symbol} ${alert.direction} ${fmt(alert.price)} · ${alert.stamp.slice(8, 10)}:${alert.stamp.slice(10, 12)}`
      : s.alertEnabled
        ? '价格提醒已开启 · 等待新的跨价行情'
        : '价格提醒未开启',
    46,
    ay + 34,
    portrait ? 18 : 22,
    alert ? '#f0ba55' : '#91a0b5'
  );
  const t = q?.stamp;
  text(t ? `行情时间 ${t.slice(0, 4)}-${t.slice(4, 6)}-${t.slice(6, 8)} ${t.slice(8, 10)}:${t.slice(10, 12)}` : '行情时间 —', 28, H - 47, 18, '#91a0b5');
  text(scene.error || '行情可能延迟 · 仅供参考', 28, H - 20, 16, scene.error ? '#f0ba55' : '#64758b');
  c.restore();
}
