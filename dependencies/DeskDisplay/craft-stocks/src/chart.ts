import type { Settings } from './stocks';
import { fillTabularText } from './canvas-text';

/** Maps A-share trading time to elapsed trading minutes (09:30 = 0, 15:00 = 240). */
export function tradingMinutePosition(time: string | undefined) {
  if (!time) return undefined;
  const compact = time.replace(':', '');
  if (!/^\d{4}$/.test(compact)) return undefined;
  const hour = Number(compact.slice(0, 2)),
    minute = Number(compact.slice(2)),
    clockMinute = hour * 60 + minute;
  if (minute > 59) return undefined;
  if (clockMinute >= 570 && clockMinute <= 690) return clockMinute - 570;
  if (clockMinute >= 780 && clockMinute <= 900) return 120 + clockMinute - 780;
  return undefined;
}

export function drawIntradayChart(
  c: CanvasRenderingContext2D,
  values: number[] | undefined,
  times: string[] | undefined,
  previous: number | undefined,
  s: Settings,
  x: number,
  y: number,
  w: number,
  h: number
) {
  const muted = '#91a0b5';
  if (!values || values.length < 2 || !previous || previous <= 0) {
    c.fillStyle = muted;
    c.font = '20px "ManropeTabular","Microsoft YaHei",sans-serif';
    c.textAlign = 'center';
    fillTabularText(c, values?.length ? '等待更多分时采样' : '分时暂不可用', x + w / 2, y + h / 2);
    return;
  }

  const leftGutter = Math.min(76, Math.max(54, w * 0.08));
  const rightGutter = Math.min(70, Math.max(54, w * 0.075));
  const bottomGutter = 30;
  const px = x + leftGutter;
  const py = y + 4;
  const pw = w - leftGutter - rightGutter;
  const ph = h - bottomGutter - 8;
  const maxMove = Math.max(...values.map((v) => Math.abs(v / previous - 1)), 0.003);
  const pctSpan = (Math.ceil(maxMove * 1000) / 1000) * 1.12;
  const high = previous * (1 + pctSpan);
  const low = previous * (1 - pctSpan);
  const up = s.greenUp ? '#3bd6a0' : '#ff646d';
  const down = s.greenUp ? '#ff646d' : '#3bd6a0';
  const yFor = (v: number) => py + ((high - v) / (high - low)) * ph;
  const label = (value: string, tx: number, ty: number, color: string, align: CanvasTextAlign) => {
    c.fillStyle = color;
    c.font = '15px "ManropeTabular","Microsoft YaHei",sans-serif';
    c.textAlign = align;
    fillTabularText(c, value, tx, ty);
  };

  c.save();
  c.strokeStyle = '#2a3749';
  c.lineWidth = 1;
  for (let i = 0; i < 5; i++) {
    const ratio = i / 4;
    const gy = py + ph * ratio;
    const price = high - (high - low) * ratio;
    const percent = (price / previous - 1) * 100;
    c.beginPath();
    c.moveTo(px, gy);
    c.lineTo(px + pw, gy);
    c.stroke();
    const axisColor = i < 2 ? up : i > 2 ? down : muted;
    label(price.toFixed(2), px - 8, gy + 5, axisColor, 'right');
    label(`${percent > 0 ? '+' : ''}${percent.toFixed(2)}%`, px + pw + 8, gy + 5, axisColor, 'left');
  }

  const timeLabels = ['09:30', '10:30', '11:30/13:00', '14:00', '15:00'];
  timeLabels.forEach((time, i) => {
    const gx = px + (pw * i) / 4;
    c.strokeStyle = '#263347';
    c.beginPath();
    c.moveTo(gx, py);
    c.lineTo(gx, py + ph);
    c.stroke();
    label(time, gx, py + ph + 23, muted, i === 0 ? 'left' : i === 4 ? 'right' : 'center');
  });

  const base = yFor(previous);
  c.setLineDash([5, 5]);
  c.strokeStyle = '#d9a441';
  c.beginPath();
  c.moveTo(px, base);
  c.lineTo(px + pw, base);
  c.stroke();
  c.setLineDash([]);

  c.beginPath();
  values.forEach((v, i) => {
    const position = tradingMinutePosition(times?.[i]),
      vx = px + (position === undefined ? i / (values.length - 1) : position / 240) * pw;
    const vy = yFor(v);
    i ? c.lineTo(vx, vy) : c.moveTo(vx, vy);
  });
  c.strokeStyle = '#3b8cff';
  c.lineWidth = 2.2;
  c.stroke();
  c.restore();
}
