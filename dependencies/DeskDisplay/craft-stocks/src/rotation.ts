import type { Settings } from './stocks';
export type Screen = { kind: 'list' | 'market' | 'detail'; page: number; seconds: number };
export function rotation(s: Settings, pages: number): Screen[] {
  const seconds = (v?: number) => Math.max(5, Math.min(120, Number(v) || Number(s.pageSeconds) || 12));
  const lists = Array.from({ length: Math.max(1, pages) }, (_, page) => ({ kind: 'list' as const, page, seconds: seconds(s.listSeconds) }));
  const market: Screen = { kind: 'market', page: 0, seconds: seconds(s.marketSeconds) };
  const detail: Screen = { kind: 'detail', page: 0, seconds: seconds(s.detailSeconds) };
  if (s.mode === 'market') return [market];
  if (s.mode === 'detail') return [detail];
  if (s.mode !== 'rotate') return lists;
  const plan = [...(s.rotateList !== false ? lists : []), ...(s.rotateMarket !== false ? [market] : []), ...(s.rotateDetail !== false ? [detail] : [])];
  return plan.length ? plan : lists; // A blank selection always falls back to a useful screen.
}
