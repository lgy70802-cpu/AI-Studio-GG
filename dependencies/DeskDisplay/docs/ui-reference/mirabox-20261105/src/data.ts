import { Device, Resource, Playlist, FitResult } from './types';

export const SPECS: [number, number][] = [
  [1920, 462],
  [462, 1920],
  [1280, 480],
  [480, 1280],
];

export function specName(s: [number, number]): string {
  const m: Record<string, string> = {
    '1920x462': 'D92 横屏',
    '462x1920': 'D92 竖屏',
    '1280x480': 'D69 横屏',
    '480x1280': 'D69 竖屏',
    '1920x480': '1920×480 横屏',
    '480x1920': '1920×480 竖屏',
  };
  return m[`${s[0]}x${s[1]}`] || `${s[0] > s[1] ? '横屏 ' : '竖屏 '}${s[0]} × ${s[1]}`;
}

export function dims(d: { w: number; h: number; rot: number }): [number, number] {
  return d.rot % 180 !== 0 ? [d.h, d.w] : [d.w, d.h];
}

export function sizeText(s: [number, number]): string {
  return `${s[0]} × ${s[1]}`;
}

export function fit(r: Resource, d?: { w: number; h: number; rot: number }): FitResult {
  if (!d) return { kind: 'blue', text: '选择设备查看适配', loss: null, index: 0 };
  const ds = dims(d);
  if (!r.sizes.length) return { kind: '', text: '尺寸未知', loss: null, index: 0 };
  const exactIndex = r.sizes.findIndex((s) => s[0] === ds[0] && s[1] === ds[1]);
  const losses = r.sizes.map((s) => 1 - Math.min(s[0] / s[1], ds[0] / ds[1]) / Math.max(s[0] / s[1], ds[0] / ds[1]));
  const loss = Math.min(...losses);
  const index = losses.indexOf(loss);
  if (exactIndex !== -1) {
    return {
      kind: 'ok',
      text: r.type === '主题' || r.type === '时钟' ? '专属布局' : '适合',
      loss: 0,
      index: exactIndex,
    };
  }
  if (loss <= 0.01) return { kind: 'ok', text: '适合', loss, index };
  if (loss <= 0.05) return { kind: 'blue', text: `轻微留边 ${(loss * 100).toFixed(1)}%`, loss, index };
  return { kind: 'warn', text: `明显留边 ${Math.round(loss * 100)}%`, loss, index };
}

export function fitWords(r: Resource, d: { w: number; h: number; rot: number }): string {
  const f = fit(r, d);
  if (f.loss === null) return '尺寸未知';
  if (f.loss <= 0.01) return '完整适配';
  const s = r.sizes[f.index || 0];
  const ds = dims(d);
  const pos = s[0] / s[1] > ds[0] / ds[1] ? '上下留边' : '左右留边';
  return `${pos}（约 ${Math.round(f.loss * 100)}%）`;
}

export const MOODS = [
  { k: 'focus', n: '专注', g: '◎', c: '#4a6cf7' },
  { k: 'morning', n: '清晨', g: '☀', c: '#e08a00' },
  { k: 'night', n: '夜晚', g: '☾', c: '#6d5bd0' },
  { k: 'relax', n: '放松', g: '♪', c: '#10a37f' },
  { k: 'idea', n: '灵感', g: '✦', c: '#e0559c' },
  { k: 'love', n: '喜爱', g: '♥', c: '#ef4444' },
];

export const CATS: Record<string, Record<string, string[]>> = {
  壁纸: {
    风景: ['山水', '海岸', '星空'],
    抽象: ['渐变', '几何'],
    极简: [],
    城市: ['夜景'],
  },
  主题: {
    效率: ['日历', '待办'],
    监控: ['性能', '网络'],
    氛围: [],
  },
  时钟: {
    数字: ['翻页', '大字'],
    模拟: [],
    极简: [],
  },
  视频: {
    自然: ['云海', '极光'],
    科幻: [],
    动画: [],
  },
};

export const BASE_DEVICES: Device[] = [
  {
    id: 'a',
    name: '我的 D92',
    model: 'D92',
    w: 1920,
    h: 462,
    rot: 0,
    online: true,
    content: 1,
    running: true,
    serial: 'D92-7F3A',
    fit: 'contain',
  },
  {
    id: 'b',
    name: '右侧 D92',
    model: 'D92',
    w: 1920,
    h: 462,
    rot: 90,
    online: true,
    content: 15,
    running: true,
    serial: 'D92-2C81',
    fit: 'contain',
  },
  {
    id: 'c',
    name: '桌面 D69',
    model: 'D69',
    w: 1280,
    h: 480,
    rot: 0,
    online: true,
    content: 2,
    running: true,
    serial: 'D69-A410',
    fit: 'contain',
  },
];

export const INITIAL_RESOURCES: Resource[] = [
  { id: 1, name: '留白时钟', type: '主题', style: 'paper', sizes: [[1920, 462], [1280, 480], [462, 1920]], local: true, cat: ['氛围', ''], hot: 96, added: 10, text: '10:24' },
  { id: 2, name: '系统脉搏', type: '主题', style: 'ink', sizes: [[1280, 480]], local: true, cat: ['监控', '性能'], hot: 74, added: 12, text: 'CPU 24%' },
  { id: 3, name: '日落航线', type: '壁纸', style: 'sun', sizes: [[1920, 462]], local: true, online: true, cat: ['风景', '海岸'], hot: 90, added: 8 },
  { id: 4, name: '纵向日历', type: '主题', style: 'aurora', sizes: [[462, 1920]], local: true, cat: ['效率', '日历'], hot: 65, added: 14, text: '10:24' },
  { id: 5, name: '薄雾山谷', type: '壁纸', style: 'mountain', sizes: [[1920, 480]], local: true, online: true, cat: ['风景', '山水'], hot: 81, added: 6 },
  { id: 6, name: '粉色翻页', type: '主题', style: 'pink', sizes: [[1920, 480]], local: true, cat: ['氛围', ''], hot: 58, added: 16, text: '10:24' },
  { id: 7, name: '极光缓缓', type: '视频', style: 'aurora', sizes: [[1920, 462]], local: true, cat: ['自然', '极光'], hot: 99, added: 18 },
  { id: 8, name: '旅行旧照片', type: '壁纸', style: 'mountain', sizes: [], local: true, cat: ['风景', ''], hot: 20, added: 20 },
  { id: 9, name: '夜航时钟', type: '时钟', style: 'ink', sizes: [[1920, 462], [1280, 480]], local: false, online: true, cat: ['数字', '翻页'], hot: 77, added: 22, text: '10:24' },
  { id: 10, name: '海岸晨光', type: '壁纸', style: 'sun', sizes: [[1280, 480]], local: false, online: true, cat: ['风景', '海岸'], hot: 69, added: 24 },
  { id: 11, name: '青山入梦', type: '壁纸', style: 'mountain', sizes: [[480, 1280]], local: false, online: true, cat: ['风景', '山水'], hot: 55, added: 26 },
  { id: 12, name: '静谧蓝调', type: '壁纸', style: 'aurora', sizes: [[1920, 480]], local: false, online: true, cat: ['抽象', '渐变'], hot: 63, added: 28 },
  { id: 13, name: '深夜工作台', type: '主题', style: 'ink', sizes: [[1920, 462]], local: true, cat: ['效率', ''], hot: 48, added: 30, text: '23:45' },
  { id: 14, name: '山水长卷', type: '壁纸', style: 'mountain', sizes: [[1920, 462]], local: true, cat: ['风景', '山水'], hot: 72, added: 32 },
  { id: 15, name: '竖屏留白', type: '主题', style: 'paper', sizes: [[462, 1920]], local: true, cat: ['氛围', ''], hot: 40, added: 34, text: '10:24' },
  { id: 16, name: '海岸晨雾', type: '壁纸', style: 'ocean', sizes: [[1920, 462]], local: true, cat: ['风景', '海岸'], hot: 88, added: 41 },
  { id: 17, name: '星河长夜', type: '壁纸', style: 'night', sizes: [[1920, 462]], local: true, cat: ['风景', '星空'], hot: 93, added: 44 },
  { id: 18, name: '几何山脊', type: '壁纸', style: 'mint', sizes: [[1920, 462]], local: true, cat: ['抽象', '几何'], hot: 61, added: 38 },
  { id: 19, name: '薄荷渐变', type: '壁纸', style: 'mint', sizes: [[1280, 480]], local: true, cat: ['抽象', '渐变'], hot: 57, added: 46 },
  { id: 20, name: '留白纸感', type: '壁纸', style: 'paper', sizes: [[1920, 480]], local: true, cat: ['极简', ''], hot: 49, added: 40 },
  { id: 21, name: '霓虹街巷', type: '壁纸', style: 'neon', sizes: [[1920, 462]], local: true, cat: ['城市', '夜景'], hot: 85, added: 50 },
  { id: 22, name: '竖版云海', type: '壁纸', style: 'ice', sizes: [[462, 1920]], local: true, cat: ['风景', '山水'], hot: 79, added: 52 },
  { id: 23, name: '暮色竖幅', type: '壁纸', style: 'dusk', sizes: [[462, 1920]], local: true, cat: ['风景', '海岸'], hot: 66, added: 48 },
  { id: 24, name: '纸页日历', type: '主题', style: 'paper', sizes: [[1920, 462], [462, 1920]], local: true, cat: ['效率', '日历'], hot: 83, added: 54, text: 'OCT 30' },
  { id: 25, name: '待办清单', type: '主题', style: 'mono', sizes: [[1920, 462]], local: true, cat: ['效率', '待办'], hot: 52, added: 42, text: '3 项待办' },
  { id: 26, name: '网络脉搏', type: '主题', style: 'ink', sizes: [[1280, 480]], local: true, cat: ['监控', '网络'], hot: 60, added: 47, text: '↓ 12 MB/s' },
  { id: 27, name: '性能仪表', type: '主题', style: 'neon', sizes: [[1920, 462], [1280, 480]], local: true, cat: ['监控', '性能'], hot: 91, added: 53, text: 'GPU 41%' },
  { id: 28, name: '极简翻页钟', type: '时钟', style: 'mono', sizes: [[1920, 462], [462, 1920]], local: true, cat: ['极简', ''], hot: 76, added: 51, text: '10:24' },
  { id: 29, name: '大字数字钟', type: '时钟', style: 'ember', sizes: [[1920, 462]], local: true, cat: ['数字', '大字'], hot: 68, added: 45, text: '10:24' },
  { id: 30, name: '翻页黄昏', type: '时钟', style: 'dusk', sizes: [[1920, 462], [1280, 480]], local: true, cat: ['数字', '翻页'], hot: 87, added: 55, text: '18:30' },
  { id: 31, name: '圆盘表盘', type: '时钟', style: 'sand', sizes: [[1280, 480]], local: true, cat: ['模拟', ''], hot: 45, added: 43, text: '3:45' },
  { id: 32, name: '云海延时', type: '视频', style: 'ice', sizes: [[1920, 462]], local: true, cat: ['自然', '云海'], hot: 80, added: 49 },
  { id: 33, name: '极光流动', type: '视频', style: 'aurora', sizes: [[1920, 462]], local: true, cat: ['自然', '极光'], hot: 94, added: 56 },
  { id: 34, name: '赛博雨夜', type: '视频', style: 'neon', sizes: [[1920, 462], [1280, 480]], local: true, cat: ['科幻', ''], hot: 89, added: 57 },
  { id: 35, name: '粒子漫游', type: '视频', style: 'plum', sizes: [[1920, 462]], local: true, cat: ['动画', ''], hot: 54, added: 39 },
  { id: 36, name: '春日樱花', type: '壁纸', style: 'pink', sizes: [[1920, 462]], local: false, online: true, cat: ['风景', '山水'], hot: 97, added: 58 },
  { id: 37, name: '深海幽蓝', type: '壁纸', style: 'ocean', sizes: [[1920, 462]], local: false, online: true, cat: ['风景', '海岸'], hot: 71, added: 59 },
  { id: 38, name: '极简灰度', type: '壁纸', style: 'mono', sizes: [[1920, 462]], local: false, online: true, cat: ['极简', ''], hot: 62, added: 60 },
  { id: 39, name: '熔岩流光', type: '壁纸', style: 'ember', sizes: [[1280, 480]], local: false, online: true, cat: ['抽象', '渐变'], hot: 84, added: 57 },
  { id: 40, name: '竖版瀑布', type: '壁纸', style: 'forest', sizes: [[462, 1920]], local: false, online: true, cat: ['风景', '山水'], hot: 75, added: 56 },
  { id: 41, name: '月相时钟', type: '时钟', style: 'night', sizes: [[1920, 462]], local: false, online: true, cat: ['数字', '翻页'], hot: 90, added: 58, text: '23:08' },
  { id: 42, name: '复古指针', type: '时钟', style: 'sand', sizes: [[1920, 462], [1280, 480]], local: false, online: true, cat: ['模拟', ''], hot: 64, added: 55, text: '10:10' },
  { id: 43, name: '终端黑客', type: '主题', style: 'neon', sizes: [[1920, 462]], local: false, online: true, cat: ['监控', '性能'], hot: 82, added: 60, text: 'root@d92' },
  { id: 44, name: '周计划板', type: '主题', style: 'paper', sizes: [[1920, 462], [462, 1920]], local: false, online: true, cat: ['效率', '待办'], hot: 59, added: 59, text: 'W44' },
  { id: 45, name: '星尘穿梭', type: '视频', style: 'night', sizes: [[1920, 462]], local: false, online: true, cat: ['科幻', ''], hot: 86, added: 60 },
];

export const INITIAL_PLAYLISTS: Playlist[] = [
  {
    name: '日常桌面',
    mood: 'morning',
    items: [
      { id: 1, sec: 60 },
      { id: 3, sec: 30 },
      { id: 7, sec: 30 },
    ],
    device: 'a',
    playing: true,
    at: 0,
    dev0: 'a',
  },
  {
    name: '竖屏精选',
    mood: 'idea',
    items: [{ id: 4, sec: 60 }, { id: 22, sec: 30 }],
    device: 'b',
    playing: false,
    at: 0,
    dev0: 'b',
  },
  {
    name: '夜晚时光',
    mood: 'night',
    items: [
      { id: 17, sec: 60 },
      { id: 21, sec: 30 },
      { id: 34, sec: 30 },
      { id: 33, sec: 30 },
    ],
    device: 'a',
    playing: false,
    at: 0,
    dev0: 'a',
  },
];
