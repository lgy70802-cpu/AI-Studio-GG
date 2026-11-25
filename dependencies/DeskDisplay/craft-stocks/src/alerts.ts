import type { Quote, Settings } from './stocks';
export interface Alert {
  symbol: string;
  direction: string;
  price: number;
  stamp: string;
}
// One crossing per rule per source trading day; first observation establishes a baseline.
export class AlertTracker {
  private rules = new Map<string, { stamp: string; price: number; fired: boolean }>();
  latest?: Alert;
  private signature = '';
  update(q: Quote | undefined, s: Settings, now = Date.now()) {
    const signature = JSON.stringify([q?.symbol, s.alertEnabled, Number(s.alertAbove) || 0, Number(s.alertBelow) || 0]);
    if (signature !== this.signature) {
      this.rules.clear();
      this.latest = undefined;
      this.signature = signature;
    }
    if (!q || !s.alertEnabled) return undefined;
    const t = q.stamp,
      epoch = Date.parse(`${t.slice(0, 4)}-${t.slice(4, 6)}-${t.slice(6, 8)}T${t.slice(8, 10)}:${t.slice(10, 12)}:${t.slice(12, 14)}+08:00`);
    if (!Number.isFinite(epoch) || now - epoch > 300000 || epoch - now > 60000) return undefined;
    for (const [direction, threshold] of [
      ['上穿', Number(s.alertAbove)],
      ['下穿', Number(s.alertBelow)]
    ] as const) {
      if (!Number.isFinite(threshold) || threshold <= 0) continue;
      const key = `${q.symbol}:${direction}:${threshold}`,
        old = this.rules.get(key);
      if (old && old.stamp >= t) continue;
      const sameDay = old?.stamp.slice(0, 8) === t.slice(0, 8);
      const crossed = old && sameDay && (direction === '上穿' ? old.price < threshold && q.price >= threshold : old.price > threshold && q.price <= threshold);
      const fired = !!(sameDay && old?.fired);
      this.rules.set(key, { stamp: t, price: q.price, fired: fired || !!crossed });
      if (crossed && !fired) this.latest = { symbol: q.symbol, direction, price: threshold, stamp: t };
    }
    return this.latest?.stamp.slice(0, 8) === t.slice(0, 8) && this.latest.symbol === q.symbol ? this.latest : undefined;
  }
}
