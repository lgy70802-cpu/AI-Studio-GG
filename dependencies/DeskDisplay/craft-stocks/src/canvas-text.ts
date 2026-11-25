const cellCache = new Map<string, number>();

function digitCell(c: CanvasRenderingContext2D) {
  const key = c.font;
  let cell = cellCache.get(key);
  if (cell === undefined) {
    cell = Math.max(...'0123456789'.split('').map((digit) => c.measureText(digit).width));
    cellCache.set(key, cell);
  }
  return cell;
}

export function tabularTextWidth(c: CanvasRenderingContext2D, value: string) {
  const cell = digitCell(c);
  return [...value].reduce((width, char) => width + (/\d/.test(char) ? cell : c.measureText(char).width), 0);
}

export function fillTabularText(c: CanvasRenderingContext2D, value: string, x: number, y: number) {
  const cell = digitCell(c);
  const width = tabularTextWidth(c, value);
  const align = c.textAlign;
  let cursor = align === 'center' ? x - width / 2 : align === 'right' || align === 'end' ? x - width : x;
  c.save();
  c.textAlign = 'left';
  for (const char of value) {
    const actual = c.measureText(char).width;
    const advance = /\d/.test(char) ? cell : actual;
    c.fillText(char, cursor + (advance - actual) / 2, y);
    cursor += advance;
  }
  c.restore();
}
