import { fillTabularText, tabularTextWidth } from './canvas-text';

export function drawHeaderClock(c: CanvasRenderingContext2D, right: number, baseline: number, compact = false, now = new Date()) {
  const weekdays = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六'];
  const date = `${now.getMonth() + 1}月${now.getDate()}日 ${weekdays[now.getDay()]}`;
  const time = [now.getHours(), now.getMinutes(), now.getSeconds()].map((v) => String(v).padStart(2, '0')).join(':');
  const timeSize = compact ? 20 : 29;
  const dateSize = compact ? 13 : 17;

  c.save();
  c.textAlign = 'right';
  c.textBaseline = 'alphabetic';
  c.fillStyle = '#eef3fb';
  c.font = `600 ${timeSize}px "ManropeTabular","Microsoft YaHei",sans-serif`;
  fillTabularText(c, time, right, baseline);
  const timeWidth = tabularTextWidth(c, time);
  c.fillStyle = '#91a0b5';
  c.font = `400 ${dateSize}px "ManropeTabular","Microsoft YaHei",sans-serif`;
  fillTabularText(c, date, right - timeWidth - (compact ? 10 : 18), baseline - (compact ? 2 : 3));
  c.restore();
}
