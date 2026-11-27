import { DeviceInfo, ThemeItem } from './themes';
import {
  BgConfig,
  BgStylePreset,
  CLIENT_ENGINE_VERSION,
  ClockElement,
  DateElement,
  EditorElement,
  EditorLayout,
  EditorRailTab,
  EditorThemeProject,
  MotionElement,
  PluginCapabilityDescriptor,
  ShapeElement,
  THEME_SCHEMA_VERSION,
  TextElement,
  WidgetCapabilityDescriptor,
  WidgetElement,
} from '../types/editor';

export const EDITOR_RAIL_TABS: { id: EditorRailTab; label: string }[] = [
  { id: 'add', label: '添加' },
  { id: 'templates', label: '模板' },
  { id: 'assets', label: '素材' },
  { id: 'text', label: '文字' },
  { id: 'images', label: '图片' },
  { id: 'time', label: '时间' },
  { id: 'motion', label: '动态' },
  { id: 'widgets', label: '组件' },
  { id: 'plugins', label: '插件' },
  { id: 'background', label: '背景' },
  { id: 'layers', label: '图层' },
];

export interface DeviceSpecOption {
  id: string;
  label: string;
  deviceModel: string;
  size: [number, number];
}

export const EDITOR_DEVICE_SPECS: DeviceSpecOption[] = [
  { id: 'd92-land', label: 'D92 横屏', deviceModel: 'D92 UltraWide', size: [1920, 462] },
  { id: 'd92-port', label: 'D92 竖屏', deviceModel: 'D92 UltraWide', size: [462, 1920] },
  { id: 'd69-land', label: 'D69 横屏', deviceModel: 'D69 Studio', size: [1280, 480] },
  { id: 'd69-port', label: 'D69 竖屏', deviceModel: 'D69 Studio', size: [480, 1280] },
];

export function availableEditorSpecs(devices: DeviceInfo[]): DeviceSpecOption[] {
  const onlineModels = new Set(devices.filter(device => device.status === '已连接').map(device => device.model));
  return onlineModels.size === 0 ? EDITOR_DEVICE_SPECS : EDITOR_DEVICE_SPECS.filter(spec =>
    [...onlineModels].some(model => spec.deviceModel.startsWith(model))
  );
}

export function specLabelFromSize(size: [number, number]): string {
  const found = EDITOR_DEVICE_SPECS.find(
    (s) => s.size[0] === size[0] && s.size[1] === size[1]
  );
  return found ? found.label : size[0] < size[1] ? '自定义竖屏' : '自定义横屏';
}

export const EDITOR_FONTS: {
  id: string;
  label: string;
  css: string;
  license: string;
}[] = [
  { id: '', label: '系统默认黑体', css: '', license: '系统内置免授权' },
  {
    id: 'hei',
    label: '苹方 / 微软雅黑',
    css: "'PingFang SC','Microsoft YaHei','Noto Sans SC',sans-serif",
    license: '系统渲染回退安全',
  },
  {
    id: 'round',
    label: '圆润体',
    css: "'Varela Round','Arial Rounded MT Bold','PingFang SC',sans-serif",
    license: 'SIL OFL / 内置打包',
  },
  {
    id: 'tech',
    label: '数字科技体',
    css: "'Orbitron','Share Tech Mono',Consolas,monospace",
    license: 'SIL OFL 1.1',
  },
  {
    id: 'mono',
    label: '等宽终端体',
    css: "'JetBrains Mono','Share Tech Mono','SF Mono',Consolas,monospace",
    license: 'SIL OFL 1.1',
  },
  {
    id: 'cond',
    label: '紧凑窄体',
    css: "'Bebas Neue',Oswald,'Arial Narrow',Impact,sans-serif",
    license: 'SIL OFL 1.1',
  },
  {
    id: 'serif',
    label: '衬线优雅体',
    css: "'Playfair Display',Georgia,'Songti SC','Noto Serif SC',serif",
    license: 'SIL OFL 1.1',
  },
  {
    id: 'song',
    label: '人文宋体',
    css: "'Songti SC',SimSun,'Noto Serif SC',serif",
    license: '跨平台衬线映射',
  },
  {
    id: 'kai',
    label: '书法楷体',
    css: "'Kaiti SC',KaiTi,STKaiti,serif",
    license: '跨平台手写楷体映射',
  },
];

export const FONT_CSS_MAP: Record<string, string> = Object.fromEntries(
  EDITOR_FONTS.map((f) => [f.id, f.css])
);

export const EDITOR_TIMEZONES: {
  id: '' | 'JP' | 'GB' | 'US';
  label: string;
  offsetHours: number;
  iana?: string;
}[] = [
  { id: '', label: '本地时间', offsetHours: 0 },
  { id: 'JP', label: '东京 (UTC+9)', offsetHours: 1, iana: 'Asia/Tokyo' },
  { id: 'GB', label: '伦敦 (UTC+0)', offsetHours: -8, iana: 'Europe/London' },
  { id: 'US', label: '纽约 (UTC-5)', offsetHours: -12, iana: 'America/New_York' },
];

export const BUILTIN_BG_STYLES: {
  id: BgStylePreset;
  label: string;
  css: string;
  isLight?: boolean;
}[] = [
  {
    id: 'paper',
    label: '宣纸暖白',
    css: 'linear-gradient(135deg, #f6f2e8 0%, #eae3d2 100%)',
    isLight: true,
  },
  {
    id: 'ink',
    label: '深墨 Slate',
    css: 'linear-gradient(135deg, #1b2230 0%, #273247 55%, #161d29 100%)',
  },
  {
    id: 'night',
    label: '星夜蓝调',
    css: 'radial-gradient(circle at 25% 20%, rgba(56,189,248,0.22), transparent 45%), linear-gradient(145deg, #090e1c 0%, #131c36 55%, #070b14 100%)',
  },
  {
    id: 'mountain',
    label: '远山云岚',
    css: 'radial-gradient(circle at 75% 20%, rgba(148,163,184,0.32), transparent 50%), linear-gradient(160deg, #1e293b 0%, #334155 48%, #0f172a 100%)',
  },
  {
    id: 'ocean',
    label: '深海青澄',
    css: 'radial-gradient(circle at 80% 75%, rgba(6,182,212,0.28), transparent 48%), linear-gradient(135deg, #0b2239 0%, #0e4766 55%, #07192c 100%)',
  },
  {
    id: 'dusk',
    label: '日落晚霞',
    css: 'radial-gradient(circle at 78% 25%, rgba(251,146,60,0.42), transparent 45%), linear-gradient(135deg, #271a3f 0%, #5c2b5e 52%, #a84852 100%)',
  },
  {
    id: 'aurora',
    label: '极光科技',
    css: 'radial-gradient(circle at 20% 25%, rgba(37,99,235,0.42), transparent 48%), radial-gradient(circle at 82% 72%, rgba(16,185,129,0.28), transparent 48%), linear-gradient(135deg, #0b132b 0%, #162447 55%, #0d1b2a 100%)',
  },
  {
    id: 'grid-tech',
    label: '工程网格',
    css: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)',
  },
];

export const BUILTIN_WALLPAPERS: {
  id: string;
  name: string;
  url: string;
  tags: string[];
}[] = [
  {
    id: 'wp-crystal',
    name: '冰晶流光宫殿',
    url: '/src/assets/images/theme_crystal_ice_palace_1795333384131.jpg',
    tags: ['科幻', '冷色'],
  },
  {
    id: 'wp-bear',
    name: '暖木桌搭小熊',
    url: '/src/assets/images/theme_cozy_bear_desk_1795333385881.jpg',
    tags: ['桌面', '治愈'],
  },
  {
    id: 'wp-sakura',
    name: '绯樱玫瑰金',
    url: '/src/assets/images/theme_sakura_rose_gold_1795333385651.jpg',
    tags: ['樱花', '柔光'],
  },
  {
    id: 'wp-icecat',
    name: '雪夜星猫物语',
    url: '/src/assets/images/theme_ice_cat_pastel_1795333386016.jpg',
    tags: ['萌宠', '治愈'],
  },
  {
    id: 'wp-bedroom',
    name: '微缩暖光空间',
    url: '/src/assets/images/theme_miniature_bedroom_1795333386709.jpg',
    tags: ['暖色', '微缩'],
  },
  {
    id: 'wp-sunset',
    name: '日落天际航线',
    url: '/src/assets/images/theme_sunset_skyline_1795333384132.jpg',
    tags: ['日落', '风景'],
  },
];

let uidCounter = 100;
export function genEditorId(prefix = 'el'): string {
  uidCounter += 1;
  return `${prefix}-${Date.now().toString(36)}-${uidCounter.toString(36)}`;
}

export function readImageDimensions(src: string): Promise<{ w: number; h: number }> {
  return new Promise((resolve) => {
    if (!src) {
      resolve({ w: 800, h: 450 });
      return;
    }
    const img = new Image();
    img.onload = () => resolve({ w: img.naturalWidth || 800, h: img.naturalHeight || 450 });
    img.onerror = () => resolve({ w: 800, h: 450 });
    img.src = src;
  });
}

export function readVideoDimensions(src: string): Promise<{ w: number; h: number; dur: number }> {
  return new Promise((resolve) => {
    if (!src) {
      resolve({ w: 1280, h: 720, dur: 0 });
      return;
    }
    const video = document.createElement('video');
    let settled = false;
    const finish = (w: number, h: number, dur: number) => {
      if (settled) return;
      settled = true;
      window.clearTimeout(timer);
      video.onloadedmetadata = null;
      video.onerror = null;
      resolve({ w, h, dur });
    };
    const timer = window.setTimeout(() => finish(1280, 720, 0), 3000);
    video.preload = 'metadata';
    video.muted = true;
    video.playsInline = true;
    video.onloadedmetadata = () => {
      finish(
        video.videoWidth || 1280,
        video.videoHeight || 720,
        Number.isFinite(video.duration) ? Math.round(video.duration) : 0
      );
    };
    video.onerror = () => finish(1280, 720, 0);
    video.src = src;
  });
}

export function computeNaturalBoxPct(nw: number, nh: number, cw: number, ch: number) {
  const safeNw = Math.max(1, nw);
  const safeNh = Math.max(1, nh);
  const safeCw = Math.max(1, cw);
  const safeCh = Math.max(1, ch);
  const w = +((safeNw / safeCw) * 100).toFixed(2);
  const h = +((safeNh / safeCh) * 100).toFixed(2);
  const ar = +(safeNw / safeNh).toFixed(4);
  const size = +Math.max(2, (safeNh * 100) / Math.min(safeCw, safeCh)).toFixed(1);
  return { w, h, ar, size };
}

export function computeFit100BoxPct(nw: number, nh: number, cw: number, ch: number) {
  const safeNw = Math.max(1, nw);
  const safeNh = Math.max(1, nh);
  const safeCw = Math.max(1, cw);
  const safeCh = Math.max(1, ch);
  const scale = Math.min(safeCw / safeNw, safeCh / safeNh);
  const dispW = safeNw * scale;
  const dispH = safeNh * scale;
  const w = +((dispW / safeCw) * 100).toFixed(2);
  const h = +((dispH / safeCh) * 100).toFixed(2);
  const ar = +(safeNw / safeNh).toFixed(4);
  const size = +Math.max(2, (dispH * 100) / Math.min(safeCw, safeCh)).toFixed(1);
  return { w, h, ar, size };
}

export function computeImportMediaBox(nw: number, nh: number, cw: number, ch: number) {
  const exceeded = nw > cw || nh > ch;
  const box = exceeded
    ? computeFit100BoxPct(nw, nh, cw, ch)
    : computeNaturalBoxPct(nw, nh, cw, ch);
  return {
    ...box,
    fit: 'contain' as const,
    exceeded,
  };
}

const REFLOW_FX: Record<'l' | 'c' | 'r', number> = { l: 0, c: 50, r: 100 };
const REFLOW_FY: Record<'t' | 'm' | 'b', number> = { t: 0, m: 50, b: 100 };

export function reflowElementsForSize(
  elements: EditorElement[],
  fromSize: [number, number],
  toSize: [number, number]
): EditorElement[] {
  const [fromW, fromH] = fromSize;
  const [toW, toH] = toSize;
  const fromPortrait = fromW < fromH;
  const toPortrait = toW < toH;
  const toMinDim = Math.min(toW, toH);

  const cloned = structuredClone(elements);
  if (fromW === toW && fromH === toH) return cloned;

  const isFullBleedOverlay = (el: EditorElement) =>
    el.kind === 'motion' && el.w >= 90 && el.h >= 90;

  // Same orientation with different dimensions: preserve relative layout & clamp safely
  if (fromPortrait === toPortrait) {
    const scaleW = fromW / Math.max(1, toW);
    const scaleH = fromH / Math.max(1, toH);
    return cloned.map((el) => {
      if (isFullBleedOverlay(el)) {
        return { ...el, a: 'mc', x: 0, y: 0, w: 100, h: 100 };
      }
      if (el.kind === 'image' || el.kind === 'video') {
        const nw = el.sourceMeta?.naturalWidth || Math.round(((el.w ?? 25) * fromW) / 100);
        const nh = el.sourceMeta?.naturalHeight || Math.round(((el.h ?? 30) * fromH) / 100);
        const curPxW = ((el.w ?? 25) * fromW) / 100;
        const curPxH = ((el.h ?? 30) * fromH) / 100;
        const box =
          curPxW > toW || curPxH > toH
            ? computeFit100BoxPct(nw, nh, toW, toH)
            : computeNaturalBoxPct(curPxW, curPxH, toW, toH);
        return { ...el, w: box.w, h: box.h, ar: box.ar, size: box.size };
      }
      if ('w' in el && typeof el.w === 'number' && 'h' in el && typeof el.h === 'number') {
        return {
          ...el,
          w: +Math.max(0.4, Math.min(96, el.w * scaleW)).toFixed(2),
          h: +Math.max(0.4, Math.min(96, el.h * scaleH)).toFixed(2),
        } as EditorElement;
      }
      return el;
    });
  }

  const getAbsCenter = (el: EditorElement) => {
    const fx = REFLOW_FX[el.a[1] as 'l' | 'c' | 'r'] ?? 50;
    const fy = REFLOW_FY[el.a[0] as 't' | 'm' | 'b'] ?? 50;
    return { absX: fx + el.x, absY: fy + el.y };
  };

  const fgIndices: number[] = [];
  cloned.forEach((el, idx) => {
    if (!el.hide && !isFullBleedOverlay(el)) {
      fgIndices.push(idx);
    }
  });

  if (!fromPortrait && toPortrait) {
    // Landscape -> Portrait: stack left-to-right visual groups vertically
    fgIndices.sort((ia, ib) => {
      const ca = getAbsCenter(cloned[ia]);
      const cb = getAbsCenter(cloned[ib]);
      const bucketA = Math.round(ca.absX / 20);
      const bucketB = Math.round(cb.absX / 20);
      if (bucketA !== bucketB) return bucketA - bucketB;
      return ca.absY - cb.absY;
    });

    const weights = fgIndices.map((idx) => {
      const el = cloned[idx];
      if (el.kind === 'widget' || el.kind === 'plugin') return 1.5;
      if (el.kind === 'image' || el.kind === 'video') return 1.4;
      if (el.kind === 'shape' && el.h < 4) return 0.45;
      return 1;
    });
    const totalW = weights.reduce((s, w) => s + w, 0) || 1;
    const topBand = fgIndices.length <= 2 ? 26 : 12;
    const bottomBand = fgIndices.length <= 2 ? 74 : 88;
    const span = bottomBand - topBand;

    let acc = 0;
    const ySlots = new Map<number, number>();
    fgIndices.forEach((idx, i) => {
      const w = weights[i];
      const centerRatio = (acc + w / 2) / totalW;
      acc += w;
      ySlots.set(idx, topBand + centerRatio * span);
    });

    return cloned.map((el, idx) => {
      if (isFullBleedOverlay(el)) {
        return { ...el, a: 'mc', x: 0, y: 0, w: 100, h: 100 };
      }
      if (el.hide) return el;
      const targetY = ySlots.get(idx) ?? 50;
      const basePos = { a: 'mc' as const, x: 0, y: +(targetY - 50).toFixed(2) };

      if (el.kind === 'clock') {
        return {
          ...el,
          ...basePos,
          size: +Math.min(el.size, el.style === 'analog' ? 28 : 25).toFixed(1),
        };
      }
      if (el.kind === 'date') {
        return {
          ...el,
          ...basePos,
          size: +Math.min(el.size, 7.5).toFixed(1),
        };
      }
      if (el.kind === 'text') {
        return {
          ...el,
          ...basePos,
          size: +Math.min(el.size, 9.5).toFixed(1),
          align: 'center',
          ...(el.w != null ? { w: Math.min(86, Math.max(48, Math.round(el.w * 1.8))) } : {}),
        };
      }
      if (el.kind === 'widget' || el.kind === 'plugin') {
        return {
          ...el,
          ...basePos,
          w: 84,
          h: +Math.max(15, Math.min(32, el.h * 0.42)).toFixed(1),
        } as EditorElement;
      }
      if (el.kind === 'image' || el.kind === 'video') {
        const nw = el.sourceMeta?.naturalWidth || Math.round(((el.w ?? 25) * fromW) / 100);
        const nh = el.sourceMeta?.naturalHeight || Math.round(((el.h ?? 30) * fromH) / 100);
        const box = computeFit100BoxPct(nw, nh, toW * 0.84, toH * 0.34);
        const finalW = +((box.w * 0.84)).toFixed(2);
        const finalH = +((box.h * 0.34)).toFixed(2);
        return {
          ...el,
          ...basePos,
          w: finalW,
          h: finalH,
          ar: box.ar,
          size: +Math.max(2, (finalH * toH) / toMinDim).toFixed(1),
        };
      }
      if (el.kind === 'shape') {
        const isThinVert = el.h > el.w * 4;
        const isThinHoriz = el.w > el.h * 3;
        if (isThinVert || isThinHoriz) {
          return { ...el, ...basePos, w: 68, h: 0.4 };
        }
        return {
          ...el,
          ...basePos,
          w: +Math.min(84, Math.max(16, el.w * 1.4)).toFixed(1),
          h: +Math.min(28, Math.max(6, el.h * 0.35)).toFixed(1),
        };
      }
      return { ...el, ...basePos };
    });
  }

  // Portrait -> Landscape: distribute top-to-bottom groups across horizontal columns
  fgIndices.sort((ia, ib) => {
    const ca = getAbsCenter(cloned[ia]);
    const cb = getAbsCenter(cloned[ib]);
    if (Math.abs(ca.absY - cb.absY) > 6) return ca.absY - cb.absY;
    return ca.absX - cb.absX;
  });

  // Group clock + adjacent date/text together so they stack neatly in the same horizontal column
  const cols: number[][] = [];
  fgIndices.forEach((idx) => {
    const el = cloned[idx];
    const lastCol = cols[cols.length - 1];
    if (
      lastCol &&
      lastCol.length < 2 &&
      (el.kind === 'date' || (el.kind === 'text' && cloned[lastCol[0]]?.kind === 'clock'))
    ) {
      lastCol.push(idx);
    } else {
      cols.push([idx]);
    }
  });

  const colCount = Math.max(1, cols.length);
  const posByIdx = new Map<number, { x: number; y: number }>();
  cols.forEach((colItems, cIdx) => {
    const colX =
      colCount === 1 ? 0 : -30 + (cIdx / (colCount - 1)) * 60;
    const rowCount = colItems.length;
    colItems.forEach((elIdx, rIdx) => {
      const rowY =
        rowCount === 1 ? 0 : rIdx === 0 ? -10 : 20;
      posByIdx.set(elIdx, { x: +colX.toFixed(2), y: rowY });
    });
  });

  return cloned.map((el, idx) => {
    if (isFullBleedOverlay(el)) {
      return { ...el, a: 'mc', x: 0, y: 0, w: 100, h: 100 };
    }
    if (el.hide) return el;
    const pos = posByIdx.get(idx) ?? { x: 0, y: 0 };
    const basePos = { a: 'mc' as const, x: pos.x, y: pos.y };

    if (el.kind === 'clock') {
      return {
        ...el,
        ...basePos,
        size: +Math.max(24, Math.min(38, el.size * 1.2)).toFixed(1),
      };
    }
    if (el.kind === 'date') {
      return {
        ...el,
        ...basePos,
        size: +Math.max(6, Math.min(10, el.size * 1.15)).toFixed(1),
      };
    }
    if (el.kind === 'text') {
      return {
        ...el,
        ...basePos,
        size: +Math.max(6, Math.min(13, el.size * 1.15)).toFixed(1),
        ...(el.w != null ? { w: Math.min(42, Math.max(18, Math.round(el.w * 0.45))) } : {}),
      };
    }
    if (el.kind === 'widget' || el.kind === 'plugin') {
      return {
        ...el,
        ...basePos,
        w: +Math.min(36, Math.max(24, el.w * 0.36)).toFixed(1),
        h: +Math.min(78, Math.max(48, el.h * 2.2)).toFixed(1),
      } as EditorElement;
    }
    if (el.kind === 'image' || el.kind === 'video') {
      const nw = el.sourceMeta?.naturalWidth || Math.round(((el.w ?? 40) * fromW) / 100);
      const nh = el.sourceMeta?.naturalHeight || Math.round(((el.h ?? 25) * fromH) / 100);
      const box = computeFit100BoxPct(nw, nh, toW * 0.34, toH * 0.8);
      const finalW = +((box.w * 0.34)).toFixed(2);
      const finalH = +((box.h * 0.8)).toFixed(2);
      return {
        ...el,
        ...basePos,
        w: finalW,
        h: finalH,
        ar: box.ar,
        size: +Math.max(2, (finalH * toH) / toMinDim).toFixed(1),
      };
    }
    if (el.kind === 'shape') {
      const isThinHoriz = el.w > el.h * 3;
      if (isThinHoriz) {
        return { ...el, ...basePos, w: 28, h: 1.5 };
      }
      return {
        ...el,
        ...basePos,
        w: +Math.min(36, Math.max(8, el.w * 0.35)).toFixed(1),
        h: +Math.min(76, Math.max(12, el.h * 2.2)).toFixed(1),
      };
    }
    return { ...el, ...basePos };
  });
}

export function makeElement<K extends EditorElement['kind']>(
  kind: K,
  overrides: Partial<Extract<EditorElement, { kind: K }>> = {}
): Extract<EditorElement, { kind: K }> {
  const base = {
    id: genEditorId(kind.slice(0, 3)),
    a: 'mc' as const,
    x: 0,
    y: 0,
    size: 30,
    color: '#ffffff',
    alpha: 1,
    weight: 600,
  };

  if (kind === 'clock') {
    return {
      ...base,
      kind: 'clock',
      style: 'digital',
      part: 'hms',
      h24: true,
      sec: false,
      tz: '',
      ...overrides,
    } as Extract<EditorElement, { kind: K }>;
  }
  if (kind === 'date') {
    return {
      ...base,
      kind: 'date',
      fmt: 0,
      part: 'full',
      size: 9,
      weight: 500,
      ...overrides,
    } as Extract<EditorElement, { kind: K }>;
  }
  if (kind === 'text') {
    return {
      ...base,
      kind: 'text',
      text: '你好，桌面',
      size: 12,
      align: '',
      ...overrides,
    } as Extract<EditorElement, { kind: K }>;
  }
  if (kind === 'image') {
    return {
      ...base,
      kind: 'image',
      src: '',
      size: 36,
      ar: 16 / 9,
      fit: 'cover',
      radius: 0,
      blur: 0,
      ...overrides,
    } as Extract<EditorElement, { kind: K }>;
  }
  if (kind === 'video') {
    return {
      ...base,
      kind: 'video',
      src: '',
      poster: '',
      size: 40,
      ar: 16 / 9,
      dur: 0,
      fit: 'cover',
      radius: 0,
      blur: 0,
      ...overrides,
    } as Extract<EditorElement, { kind: K }>;
  }
  if (kind === 'motion') {
    return {
      ...base,
      kind: 'motion',
      effect: 'particles',
      w: 100,
      h: 100,
      size: 16,
      speed: 1,
      intensity: 65,
      secondaryColor: '#38BDF8',
      performanceCost: 'medium',
      requiresGpu: true,
      ...overrides,
    } as Extract<EditorElement, { kind: K }>;
  }
  if (kind === 'widget') {
    return {
      ...base,
      kind: 'widget',
      widgetType: 'sysmon',
      w: 28,
      h: 34,
      size: 14,
      widgetVersion: '2.0.0',
      dataSource: 'system.telemetry',
      accentColor: '#2563EB',
      bgOpacity: 76,
      label: '系统监控',
      value: 68,
      ...overrides,
    } as Extract<EditorElement, { kind: K }>;
  }
  if (kind === 'plugin') {
    return {
      ...base,
      kind: 'plugin',
      pluginId: 'cap-music-info',
      pluginName: '音乐信息卡片',
      pluginVersion: '2.0.0',
      capabilityKey: 'music-info',
      w: 30,
      h: 34,
      size: 14,
      accentColor: '#2563EB',
      bgOpacity: 78,
      config: { track: 'Midnight Lo-Fi', artist: 'Studio Session' },
      ...overrides,
    } as unknown as Extract<EditorElement, { kind: K }>;
  }
  return {
    ...base,
    kind: 'shape',
    sh: 'rect',
    w: 30,
    h: 10,
    radius: 12,
    size: 0,
    ...overrides,
  } as Extract<EditorElement, { kind: K }>;
}

export interface BuiltinTemplateSpec {
  id: string;
  name: string;
  description: string;
  category: '主题' | '动态时钟';
  subCategory: string;
  tags: string[];
  accentHex: string;
  bg: Partial<BgConfig>;
  buildElements: (portrait: boolean) => EditorElement[];
}

export const BUILTIN_TEMPLATES: BuiltinTemplateSpec[] = [
  {
    id: 'tpl-minimal-paper',
    name: '极简时钟 · 宣纸',
    description: '高对比浅色系极简排版，适合桌面日常办公',
    category: '动态时钟',
    subCategory: '极简',
    tags: ['内置', '浅色', '时钟'],
    accentHex: '#4A463C',
    bg: { kind: 'style', style: 'paper' },
    buildElements: (p) => [
      makeElement('clock', {
        style: 'minimal',
        size: p ? 28 : 34,
        color: '#4a463c',
        weight: 300,
      }) as ClockElement,
      makeElement('date', {
        a: 'bc',
        y: p ? -12 : -10,
        size: p ? 5.2 : 7,
        color: '#4a463c',
        alpha: 0.72,
      }) as DateElement,
    ],
  },
  {
    id: 'tpl-left-clock-right-date',
    name: '左时右日 · 深墨',
    description: 'D92 超宽屏经典左右分布结构，信息一目了然',
    category: '动态时钟',
    subCategory: '效率',
    tags: ['内置', 'D92', '商务'],
    accentHex: '#2563EB',
    bg: { kind: 'style', style: 'ink' },
    buildElements: (p) =>
      p
        ? [
            makeElement('clock', { a: 'mc', y: -8, size: 28 }) as ClockElement,
            makeElement('date', { a: 'mc', y: 12, size: 6, fmt: 1, alpha: 0.8 }) as DateElement,
          ]
        : [
            makeElement('clock', { a: 'ml', x: 6, size: 42 }) as ClockElement,
            makeElement('date', { a: 'mr', x: -6, size: 9, fmt: 1, alpha: 0.8 }) as DateElement,
          ],
  },
  {
    id: 'tpl-flip-night',
    name: '翻页时钟 · 星夜',
    description: '经典复古机械翻页卡片数字时钟',
    category: '动态时钟',
    subCategory: '机械翻页',
    tags: ['内置', '翻页', '夜览'],
    accentHex: '#38BDF8',
    bg: { kind: 'style', style: 'night' },
    buildElements: (p) => [
      makeElement('clock', {
        style: 'flip',
        size: p ? 26 : 34,
        color: '#e8f1ff',
      }) as ClockElement,
    ],
  },
  {
    id: 'tpl-big-date',
    name: '大字日期 · 远山',
    description: '醒目英文短日期配激励标语，专注工作流',
    category: '主题',
    subCategory: '桌面语录',
    tags: ['内置', '大字', '极简'],
    accentHex: '#64748B',
    bg: { kind: 'style', style: 'mountain', dim: 15 },
    buildElements: (p) => [
      makeElement('date', {
        fmt: 3,
        size: p ? 16 : 32,
        weight: 700,
      }) as DateElement,
      makeElement('text', {
        text: '今天也要保持专注与热爱',
        a: 'bc',
        y: p ? -12 : -9,
        size: p ? 5.2 : 6.5,
        weight: 500,
        alpha: 0.85,
      }) as TextElement,
    ],
  },
  {
    id: 'tpl-dual-timezone',
    name: '双时区协同',
    description: '本地时间与东京时间并列展示，跨时区协作必备',
    category: '动态时钟',
    subCategory: '双时区',
    tags: ['内置', '跨区', '商务'],
    accentHex: '#0EA5E9',
    bg: { kind: 'gradient', g1: '#0f3a5f', g2: '#2a7fa6', angle: 120 },
    buildElements: (p) =>
      p
        ? [
            makeElement('clock', { a: 'mc', y: -12, size: 22 }) as ClockElement,
            makeElement('clock', { a: 'mc', y: 12, size: 22, tz: 'JP' }) as ClockElement,
          ]
        : [
            makeElement('clock', { a: 'ml', x: 11, y: 5, size: 32 }) as ClockElement,
            makeElement('text', {
              text: '本地 · BEIJING',
              a: 'ml',
              x: 11,
              y: -22,
              size: 5.8,
              alpha: 0.78,
              weight: 500,
            }) as TextElement,
            makeElement('clock', { a: 'mr', x: -11, y: 5, size: 32, tz: 'JP' }) as ClockElement,
            makeElement('text', {
              text: '东京 · TOKYO',
              a: 'mr',
              x: -11,
              y: -22,
              size: 5.8,
              alpha: 0.78,
              weight: 500,
            }) as TextElement,
          ],
  },
  {
    id: 'tpl-sysmon-hud',
    name: '硬件监控工作台',
    description: '融合实时时间、粒子律动与 CPU/GPU 硬件性能条',
    category: '主题',
    subCategory: '系统监控',
    tags: ['内置', '性能', '组件'],
    accentHex: '#2563EB',
    bg: { kind: 'style', style: 'aurora', dim: 10 },
    buildElements: (p) => [
      makeElement('motion', {
        effect: 'particles',
        w: 100,
        h: 100,
        color: '#60A5FA',
        secondaryColor: '#38BDF8',
        alpha: 0.5,
        speed: 1,
        intensity: 60,
        performanceCost: 'medium',
      }) as MotionElement,
      makeElement('clock', {
        a: p ? 'tc' : 'ml',
        x: p ? 0 : 6,
        y: p ? 10 : -8,
        size: p ? 24 : 32,
        font: 'tech',
      }) as ClockElement,
      makeElement('date', {
        a: p ? 'tc' : 'ml',
        x: p ? 0 : 6,
        y: p ? 24 : 18,
        size: p ? 5.2 : 7,
        fmt: 1,
        alpha: 0.8,
      }) as DateElement,
      makeElement('widget', {
        widgetType: 'sysmon',
        a: p ? 'bc' : 'mr',
        x: p ? 0 : -6,
        y: p ? -10 : 0,
        w: p ? 80 : 32,
        h: p ? 24 : 68,
        accentColor: '#38BDF8',
        bgOpacity: 74,
      }) as WidgetElement,
    ],
  },
  {
    id: 'tpl-bottom-bar',
    name: '底部信息胶囊条',
    description: '深海渐变搭配半透明信息栏，兼容风景壁纸叠层',
    category: '主题',
    subCategory: '桌面装饰',
    tags: ['内置', '壁纸兼容', '信息条'],
    accentHex: '#06B6D4',
    bg: { kind: 'style', style: 'ocean', dim: 10 },
    buildElements: (p) => [
      makeElement('shape', {
        a: 'bc',
        y: -6,
        w: 94,
        h: p ? 18 : 34,
        radius: 28,
        color: '#000000',
        alpha: 0.34,
      }) as ShapeElement,
      makeElement('clock', {
        a: 'bl',
        x: 8,
        y: p ? -9 : -12,
        size: p ? 16 : 20,
      }) as ClockElement,
      makeElement('date', {
        a: 'br',
        x: -8,
        y: p ? -10 : -14,
        size: p ? 5 : 8,
      }) as DateElement,
    ],
  },
  {
    id: 'tpl-dusk-greeting',
    name: '晚霞问候语录',
    description: '日落晚霞渐变背景，适合桌面伴侣与氛围展示',
    category: '主题',
    subCategory: '桌面语录',
    tags: ['内置', '渐变', '治愈'],
    accentHex: '#F59E0B',
    bg: { kind: 'gradient', g1: '#2b1b4a', g2: '#f2a65a', angle: 160 },
    buildElements: (p) => [
      makeElement('text', {
        text: '早上好 · Studio',
        a: 'tl',
        x: 5,
        y: p ? 8 : 12,
        size: p ? 9 : 14,
        weight: 700,
      }) as TextElement,
      makeElement('clock', {
        a: 'bl',
        x: 5,
        y: -8,
        size: p ? 24 : 34,
        style: 'digital',
      }) as ClockElement,
      makeElement('date', {
        a: 'br',
        x: -5,
        y: -10,
        size: p ? 5 : 8,
        alpha: 0.85,
      }) as DateElement,
    ],
  },
];

export type AssetCategoryFilter =
  | '全部'
  | '收藏'
  | '最近'
  | '数字'
  | '表盘'
  | '日期'
  | '组合'
  | '文字'
  | '装饰';

export interface AssetPresetItem {
  id: string;
  name: string;
  category: Exclude<AssetCategoryFilter, '全部' | '收藏' | '最近'>;
  keywords: string[];
  build: (portrait: boolean) => EditorElement[];
}

const ht = (px: number, p: boolean) => +((px / (p ? 1920 : 462)) * 100).toFixed(2);

export const BUILTIN_ASSET_PRESETS: AssetPresetItem[] = [
  {
    id: 'ap-flip',
    name: '翻页时钟',
    category: '数字',
    keywords: ['flip', '翻页'],
    build: (p) => [makeElement('clock', { style: 'flip', size: p ? 26 : 36, color: '#e8f1ff' })],
  },
  {
    id: 'ap-minimal',
    name: '极简细体',
    category: '数字',
    keywords: ['细', '简约'],
    build: (p) => [
      makeElement('clock', { style: 'minimal', y: p ? -3 : -5, size: p ? 28 : 36, weight: 300 }),
      makeElement('date', { fmt: 1, y: p ? 13 : 21, size: p ? 4.6 : 6, alpha: 0.7 }),
    ],
  },
  {
    id: 'ap-neon',
    name: '霓虹科技',
    category: '数字',
    keywords: ['发光', '科技', 'neon'],
    build: (p) => [
      makeElement('clock', {
        font: 'tech',
        fx: 'glow',
        color: '#58f0ff',
        weight: 700,
        size: p ? 24 : 36,
        ls: 4,
        sec: !p,
      }),
    ],
  },
  {
    id: 'ap-retro',
    name: '复古等宽',
    category: '数字',
    keywords: ['终端', '复古', 'mono'],
    build: (p) => [
      makeElement('clock', {
        font: 'mono',
        fx: 'glow',
        color: '#7bd88f',
        weight: 400,
        size: p ? 26 : 40,
        ls: 6,
      }),
    ],
  },
  {
    id: 'ap-cond',
    name: '窄体大字',
    category: '数字',
    keywords: ['大字', '窄'],
    build: (p) => [
      makeElement('clock', { font: 'cond', weight: 400, size: p ? 40 : 60, ls: 4 }),
    ],
  },
  {
    id: 'ap-serif',
    name: '衬线优雅',
    category: '数字',
    keywords: ['衬线', '优雅', 'serif'],
    build: (p) => [
      makeElement('clock', {
        font: 'serif',
        weight: 400,
        color: '#f5f1e8',
        size: p ? 28 : 40,
        ls: 2,
        y: p ? -3 : -6,
      }),
      makeElement('date', {
        font: 'serif',
        fmt: 1,
        y: p ? 13 : 22,
        size: p ? 4.2 : 6,
        ls: 24,
        alpha: 0.75,
        color: '#f5f1e8',
        weight: 400,
      }),
    ],
  },
  {
    id: 'ap-outline',
    name: '描边空心',
    category: '数字',
    keywords: ['描边', '空心'],
    build: (p) => [makeElement('clock', { fx: 'outline', weight: 800, size: p ? 30 : 46 })],
  },
  {
    id: 'ap-dial-tick',
    name: '经典表盘',
    category: '表盘',
    keywords: ['指针', '刻度', 'analog'],
    build: (p) => [
      makeElement('clock', {
        style: 'analog',
        dial: 'tick',
        hands: 'line',
        sec: true,
        size: p ? 30 : 34,
      }),
    ],
  },
  {
    id: 'ap-dial-dot',
    name: '圆点表盘',
    category: '表盘',
    keywords: ['圆点', '指针'],
    build: (p) => [
      makeElement('clock', {
        style: 'analog',
        dial: 'dot',
        hands: 'round',
        size: p ? 30 : 34,
      }),
    ],
  },
  {
    id: 'ap-dial-roman',
    name: '罗马表盘',
    category: '表盘',
    keywords: ['罗马', '复古'],
    build: (p) => [
      makeElement('clock', {
        style: 'analog',
        dial: 'roman',
        hands: 'taper',
        sec: true,
        font: 'serif',
        color: '#f5f1e8',
        accent: '#d4a24c',
        size: p ? 30 : 34,
      }),
    ],
  },
  {
    id: 'ap-dial-bare',
    name: '极简表盘',
    category: '表盘',
    keywords: ['极简', '无刻度'],
    build: (p) => [
      makeElement('clock', {
        style: 'analog',
        dial: 'bare',
        hands: 'taper',
        ring: false,
        size: p ? 30 : 34,
      }),
    ],
  },
  {
    id: 'ap-dial-date',
    name: '表盘+日期',
    category: '表盘',
    keywords: ['日期', '组合'],
    build: (p) =>
      p
        ? [
            makeElement('clock', { style: 'analog', dial: 'tick', hands: 'line', y: -8, size: 26 }),
            makeElement('date', { fmt: 3, y: 20, size: 7, weight: 700 }),
          ]
        : [
            makeElement('clock', {
              style: 'analog',
              dial: 'tick',
              hands: 'line',
              sec: true,
              a: 'ml',
              x: 12,
              size: 32,
            }),
            makeElement('date', { fmt: 3, a: 'mr', x: -12, size: 10, weight: 700 }),
            makeElement('date', { fmt: 0, a: 'mr', x: -12, y: 14, size: 6, alpha: 0.75 }),
          ],
  },
  {
    id: 'ap-bigdate',
    name: '大字日期',
    category: '日期',
    keywords: ['日期'],
    build: (p) => [makeElement('date', { fmt: 3, size: p ? 15 : 32, weight: 700 })],
  },
  {
    id: 'ap-endate-bar',
    name: '英文日期条',
    category: '日期',
    keywords: ['英文'],
    build: (p) => [
      makeElement('date', { fmt: 1, y: -3, size: p ? 4.6 : 9, weight: 500 }),
      makeElement('shape', { y: p ? 5 : 12, w: p ? 40 : 24, h: ht(3, p), radius: 50, alpha: 0.8 }),
    ],
  },
  {
    id: 'ap-vdate',
    name: '竖排日期',
    category: '日期',
    keywords: ['竖排', '中文'],
    build: (p) => [makeElement('date', { vert: true, fmt: 0, size: p ? 9 : 7, ls: 20, weight: 700 })],
  },
  {
    id: 'ap-sdate',
    name: '衬线英文日期',
    category: '日期',
    keywords: ['衬线', '英文'],
    build: (p) => [makeElement('date', { font: 'serif', fmt: 1, size: p ? 5 : 11, ls: 28, weight: 400 })],
  },
  {
    id: 'ap-clk-date',
    name: '数字时钟+日期',
    category: '组合',
    keywords: ['日期'],
    build: (p) => [
      makeElement('clock', { y: p ? -4 : -6, size: p ? 30 : 40 }),
      makeElement('date', { y: p ? 14 : 22, size: p ? 7 : 8, alpha: 0.85 }),
    ],
  },
  {
    id: 'ap-lr-clk',
    name: '左时右日期',
    category: '组合',
    keywords: ['日期'],
    build: (p) =>
      p
        ? [
            makeElement('clock', { y: -4, size: 28 }),
            makeElement('date', { y: 13, size: 7 }),
          ]
        : [
            makeElement('clock', { a: 'ml', x: 8, size: 38 }),
            makeElement('date', { a: 'mr', x: -8, size: 8, fmt: 1, alpha: 0.85 }),
          ],
  },
  {
    id: 'ap-2tz',
    name: '双时区',
    category: '组合',
    keywords: ['时区', '东京'],
    build: (p) =>
      p
        ? [
            makeElement('clock', { y: -13, size: 22 }),
            makeElement('clock', { y: 13, size: 22, tz: 'JP' }),
          ]
        : [
            makeElement('clock', { a: 'ml', x: 10, y: 4, size: 30 }),
            makeElement('text', { text: '本地', a: 'ml', x: 10, y: -24, size: 6, alpha: 0.75, weight: 500 }),
            makeElement('clock', { a: 'mr', x: -10, y: 4, size: 30, tz: 'JP' }),
            makeElement('text', { text: '东京', a: 'mr', x: -10, y: -24, size: 6, alpha: 0.75, weight: 500 }),
          ],
  },
  {
    id: 'ap-greet',
    name: '问候+时钟',
    category: '组合',
    keywords: ['问候'],
    build: (p) => [
      makeElement('text', { text: '早上好', y: p ? -14 : -22, size: p ? 9 : 12, weight: 700 }),
      makeElement('clock', { y: p ? 2 : 6, size: p ? 26 : 32 }),
      makeElement('date', { y: p ? 19 : 27, size: p ? 6 : 7, alpha: 0.8 }),
    ],
  },
  {
    id: 'ap-dotline-clk',
    name: '时钟+圆点线',
    category: '组合',
    keywords: ['装饰', '分隔'],
    build: (p) => [
      makeElement('clock', { y: p ? -6 : -10, size: p ? 28 : 38 }),
      makeElement('shape', { sh: 'dotline', y: p ? 8 : 12, w: p ? 50 : 30, h: ht(3, p), bw: 0.8, alpha: 0.7 }),
      makeElement('date', { y: p ? 17 : 25, size: p ? 6 : 7.5, alpha: 0.85, ls: 12 }),
    ],
  },
  {
    id: 'ap-vsplit',
    name: '竖线分栏',
    category: '组合',
    keywords: ['竖排', '分栏'],
    build: (p) =>
      p
        ? [
            makeElement('clock', { y: -10, size: 28 }),
            makeElement('shape', { y: 5, w: 50, h: ht(3, p), alpha: 0.6 }),
            makeElement('date', { y: 15, size: 7, ls: 12 }),
          ]
        : [
            makeElement('clock', { a: 'ml', x: 10, size: 38 }),
            makeElement('shape', { x: 10, w: 0.25, h: 50, alpha: 0.6 }),
            makeElement('date', { a: 'mr', x: -12, size: 5.5, vert: true, ls: 20, weight: 500 }),
          ],
  },
  {
    id: 'ap-rect',
    name: '实心矩形',
    category: '装饰',
    keywords: ['矩形', '方块', '色块'],
    build: (p) => [makeElement('shape', { sh: 'rect', w: p ? 56 : 24, h: p ? 14 : 44, radius: 4, alpha: 0.85 })],
  },
  {
    id: 'ap-circle',
    name: '实心圆形',
    category: '装饰',
    keywords: ['圆', '椭圆', 'circle'],
    build: (p) => [makeElement('shape', { sh: 'circle', w: p ? 56 : 16, h: p ? 13.5 : 66, radius: 50, alpha: 0.9 })],
  },
  {
    id: 'ap-pill',
    name: '胶囊细条',
    category: '装饰',
    keywords: ['细条', '胶囊', '线条'],
    build: (p) => [makeElement('shape', { sh: 'rect', w: p ? 68 : 44, h: ht(8, p), radius: 50, alpha: 0.85 })],
  },
  {
    id: 'ap-dash',
    name: '虚线',
    category: '装饰',
    keywords: ['线条', 'dash'],
    build: (p) => [makeElement('shape', { sh: 'dash', w: p ? 60 : 50, h: ht(5, p), alpha: 0.85 })],
  },
  {
    id: 'ap-dots',
    name: '点线',
    category: '装饰',
    keywords: ['线条', '圆点'],
    build: (p) => [makeElement('shape', { sh: 'dots', w: p ? 60 : 50, h: ht(9, p), alpha: 0.85 })],
  },
  {
    id: 'ap-double',
    name: '双线',
    category: '装饰',
    keywords: ['线条'],
    build: (p) => [makeElement('shape', { sh: 'double', w: p ? 60 : 50, h: ht(12, p), alpha: 0.85 })],
  },
  {
    id: 'ap-ring',
    name: '圆环',
    category: '装饰',
    keywords: ['环', '圆'],
    build: (p) => [makeElement('shape', { sh: 'ring', w: p ? 80 : 19.3, h: p ? 19.3 : 80, bw: 1, alpha: 0.9 })],
  },
  {
    id: 'ap-brackets',
    name: '边框角',
    category: '装饰',
    keywords: ['边框', '角标'],
    build: (p) => [makeElement('shape', { sh: 'brackets', w: p ? 86 : 94, h: p ? 94 : 86, bw: 0.8, alpha: 0.8 })],
  },
  {
    id: 'ap-h1',
    name: '大标题',
    category: '文字',
    keywords: ['标题'],
    build: (p) => [makeElement('text', { text: '今日专注', size: p ? 10 : 20, weight: 700 })],
  },
  {
    id: 'ap-h1h2',
    name: '标题+副标题',
    category: '文字',
    keywords: ['副标题', '标题'],
    build: (p) => [
      makeElement('text', { text: '晨间计划', size: p ? 9 : 16, weight: 700, y: p ? -3 : -6 }),
      makeElement('text', { text: 'FOCUS · PLAN · DO', size: p ? 4.2 : 7, weight: 400, ls: 14, y: p ? 4 : 9, alpha: 0.75 }),
    ],
  },
  {
    id: 'ap-quote',
    name: '励志短句',
    category: '文字',
    keywords: ['短句', '两行'],
    build: (p) => [makeElement('text', { text: '保持热爱\n奔赴山海', size: p ? 6 : 10, weight: 300, ls: 10 })],
  },
  {
    id: 'ap-vpoem',
    name: '竖排诗句',
    category: '文字',
    keywords: ['诗', '竖排', '楷体'],
    build: (p) => [makeElement('text', { text: '山高自有客船行', vert: true, font: 'kai', size: p ? 6 : 9, ls: 20, weight: 400 })],
  },
  {
    id: 'ap-en-slogan',
    name: '英文标语',
    category: '文字',
    keywords: ['标语', '英文'],
    build: (p) => [makeElement('text', { text: 'STAY HUNGRY', font: 'cond', size: p ? 9 : 20, ls: 20, weight: 400 })],
  },
  {
    id: 'ap-serif-quote',
    name: '衬线引语',
    category: '文字',
    keywords: ['引语', '衬线'],
    build: (p) => [makeElement('text', { text: '“Less is more.”', font: 'serif', size: p ? 5 : 11, ls: 4, weight: 400 })],
  },
  {
    id: 'ap-neon-open',
    name: '霓虹标语',
    category: '文字',
    keywords: ['发光', '霓虹', 'neon'],
    build: (p) => [makeElement('text', { text: 'STUDIO LIVE', font: 'tech', fx: 'glow', color: '#ff7ac8', size: p ? 8.5 : 18, ls: 10, weight: 700 })],
  },
  {
    id: 'ap-badge',
    name: '标签徽章',
    category: '文字',
    keywords: ['标签', '徽章', '胶囊'],
    build: (p) => [
      makeElement('shape', { w: p ? 40 : 16, h: ht(48, p), radius: 50, alpha: 0.24 }),
      makeElement('text', { text: '进行中', size: p ? 6 : 7, weight: 600 }),
    ],
  },
];

export const TEXT_STYLE_PRESETS: {
  id: string;
  label: string;
  desc: string;
  overrides: Partial<TextElement>;
}[] = [
  { id: 'txt-h1', label: '大标题', desc: '醒目主视觉标题', overrides: { text: 'D92 STUDIO', size: 18, weight: 700 } },
  { id: 'txt-h2', label: '副标题', desc: '辅助说明 / 标签', overrides: { text: 'DESKTOP SECONDARY DISPLAY', size: 9, weight: 500, ls: 12 } },
  { id: 'txt-body', label: '正文文本', desc: '清晰易读的正文排版', overrides: { text: '保持工作台整洁有序，专注当下创造力。', size: 6.5, weight: 400 } },
  { id: 'txt-vert', label: '竖排古风', desc: 'vertical-rl 竖向排布', overrides: { text: '万物静观皆自得', vert: true, font: 'kai', size: 8, ls: 20, weight: 500 } },
  { id: 'txt-kai', label: '手写楷韵', desc: '楷体人文书卷感', overrides: { text: '清风徐来 · 水波不兴', font: 'kai', size: 11, weight: 400 } },
  { id: 'txt-outline', label: '描边空心', desc: '极简镂空外描边', overrides: { text: 'ULTRA WIDE', fx: 'outline', weight: 800, size: 22 } },
  { id: 'txt-glow', label: '霓虹发光', desc: '电子脉冲光晕', overrides: { text: 'ONLINE', font: 'tech', fx: 'glow', color: '#38BDF8', size: 18, weight: 700 } },
  { id: 'txt-shadow', label: '立体投影', desc: '强背景图清晰可读', overrides: { text: 'MORNING BRIEF', fx: 'shadow', weight: 700, size: 14 } },
  { id: 'txt-gradient', label: '渐变艺术字', desc: '蓝青极光渐变填充', overrides: { text: 'CREATIVE LAB', fx: 'gradient', g1: '#60A5FA', g2: '#F472B6', weight: 800, size: 16 } },
];

export const MOTION_PRESETS: {
  id: string;
  name: string;
  effect: MotionElement['effect'];
  desc: string;
  cost: 'low' | 'medium' | 'high';
  requiresGpu: boolean;
  supported: boolean;
  color: string;
  secondaryColor?: string;
}[] = [
  { id: 'mo-particles', name: '星尘悬浮粒子', effect: 'particles', desc: '轻量柔光漂浮粒子，全设备流畅支持', cost: 'low', requiresGpu: false, supported: true, color: '#60A5FA', secondaryColor: '#93C5FD' },
  { id: 'mo-snow', name: '冬日飘雪氛围', effect: 'snow', desc: '柔和雪花景深飘落效果', cost: 'medium', requiresGpu: true, supported: true, color: '#E0F2FE', secondaryColor: '#BAE6FD' },
  { id: 'mo-grad', name: '极光流光渐变', effect: 'gradient-flow', desc: '低功耗线性色带慢速呼吸流动', cost: 'low', requiresGpu: true, supported: true, color: '#2563EB', secondaryColor: '#06B6D4' },
  { id: 'mo-breathe', name: '柔光呼吸光环', effect: 'breathe', desc: '中心脉冲式呼吸光效', cost: 'low', requiresGpu: false, supported: true, color: '#3B82F6', secondaryColor: '#60A5FA' },
  { id: 'mo-float', name: '微重力漂浮波纹', effect: 'float', desc: '轻度上下浮动的空间几何光斑', cost: 'low', requiresGpu: false, supported: true, color: '#818CF8', secondaryColor: '#38BDF8' },
  { id: 'mo-rotate', name: '轨道星环旋转', effect: 'rotate', desc: '匀速几何外环旋转装饰', cost: 'low', requiresGpu: false, supported: true, color: '#38BDF8', secondaryColor: '#0EA5E9' },
  { id: 'mo-fade', name: '渐隐氛围薄雾', effect: 'fade', desc: '边缘柔光呼吸淡入淡出', cost: 'low', requiresGpu: false, supported: true, color: '#CBD5E1', secondaryColor: '#64748B' },
  { id: 'mo-gif', name: '动态脉冲徽章', effect: 'gif-sticker', desc: '模拟 GIF 帧循环动态图标装饰', cost: 'medium', requiresGpu: false, supported: true, color: '#F59E0B', secondaryColor: '#EF4444' },
];

export const WIDGET_CATALOG: WidgetCapabilityDescriptor[] = [
  {
    type: 'progress',
    name: '线性进度条',
    version: '2.0.0',
    description: '展示今日目标、年进度或渲染任务完成度',
    dataSource: 'local.progress / year.percent',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'low',
    editableParams: ['标题', '百分比', '强调色', '透明度'],
    defaultW: 28,
    defaultH: 20,
    defaultAccent: '#2563EB',
  },
  {
    type: 'ring-progress',
    name: '环形仪表进度',
    version: '2.0.0',
    description: 'SVG 圆环进度显示，适配窄屏与竖屏模块',
    dataSource: 'local.metric',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'low',
    editableParams: ['标签', '数值', '强调色'],
    defaultW: 18,
    defaultH: 48,
    defaultAccent: '#10B981',
  },
  {
    type: 'countdown',
    name: '目标倒计时牌',
    version: '2.0.0',
    description: '纪念日、项目上线或下班倒计时卡片',
    dataSource: 'system.clock',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'low',
    editableParams: ['事件名称', '剩余天数/时间', '强调色'],
    defaultW: 26,
    defaultH: 34,
    defaultAccent: '#F59E0B',
  },
  {
    type: 'date-card',
    name: '日历日程微卡片',
    version: '2.0.0',
    description: '绑定系统实时日期与周次的多格卡片',
    dataSource: 'system.calendar',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'low',
    editableParams: ['强调色', '透明度'],
    defaultW: 22,
    defaultH: 38,
    defaultAccent: '#2563EB',
  },
  {
    type: 'weather',
    name: '实时气象卡片',
    version: '2.1.0',
    description: '展示城市实时温度、天气图标与空气质量',
    dataSource: 'mock.weather.feed',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'low',
    editableParams: ['城市名称', '强调色', '透明度'],
    defaultW: 24,
    defaultH: 34,
    defaultAccent: '#0EA5E9',
  },
  {
    type: 'sysmon',
    name: 'CPU / GPU / RAM 监控',
    version: '2.2.0',
    description: '结构化硬件负载与温度监测面板',
    dataSource: 'mock.hardware.sensors',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'medium',
    editableParams: ['强调色', '透明度'],
    defaultW: 30,
    defaultH: 46,
    defaultAccent: '#38BDF8',
  },
  {
    type: 'audio-spectrum',
    name: '音频频谱柱状条',
    version: '2.0.0',
    description: '响应系统音频节拍的实时柱状光波频谱',
    dataSource: 'mock.audio.fft',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'medium',
    editableParams: ['柱条数量', '强调色', '透明度'],
    defaultW: 30,
    defaultH: 26,
    defaultAccent: '#2563EB',
  },
  {
    type: 'mini-chart',
    name: '小型数据趋势图',
    version: '2.0.0',
    description: '展示近 7 日专注时长或算力波动柱状/折线图',
    dataSource: 'local.analytics',
    supportedDevices: ['D92', 'D69'],
    performanceCost: 'low',
    editableParams: ['指标标题', '强调色', '透明度'],
    defaultW: 28,
    defaultH: 36,
    defaultAccent: '#6366F1',
  },
];

export const PLUGIN_CAPABILITY_REGISTRY: PluginCapabilityDescriptor[] = [
  {
    id: 'cap-music-info',
    key: 'music-info',
    name: '音乐播放信息卡片',
    version: '2.1.0',
    category: '媒体联动',
    description: '读取系统 SMTC / MediaSession 正在播放的曲目、歌手与进度',
    requiredPermissions: ['media.session.read'],
    minClientVersion: '2.0.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '轻量',
    supportedInRuntime: true,
    defaultW: 30,
    defaultH: 36,
    defaultAccent: '#2563EB',
    defaultConfig: { track: 'Midnight Lo-Fi', artist: 'Studio Acoustics', showProgress: true },
    declarativeOnlyNote: '仅通过内置媒体桥接协议订阅元数据，禁止外部 JS 注入',
  },
  {
    id: 'cap-lyrics-sync',
    key: 'lyrics-sync',
    name: '桌面同步歌词条',
    version: '1.8.0',
    category: '媒体联动',
    description: '双行滚动高亮歌词条，适配副屏底部长条区域',
    requiredPermissions: ['media.session.read'],
    minClientVersion: '2.0.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '轻量',
    supportedInRuntime: true,
    defaultW: 48,
    defaultH: 26,
    defaultAccent: '#EC4899',
    defaultConfig: { currentLine: '星辰落入桌面极光', nextLine: '每一帧灵感都在此刻凝聚' },
    declarativeOnlyNote: '声明式歌词渲染模板',
  },
  {
    id: 'cap-quick-notes',
    key: 'quick-notes',
    name: '电子记事便签',
    version: '1.5.0',
    category: '效率工具',
    description: '常驻桌面备忘录与临时灵感记录板',
    requiredPermissions: ['storage.local'],
    minClientVersion: '2.0.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '轻量',
    supportedInRuntime: true,
    defaultW: 26,
    defaultH: 42,
    defaultAccent: '#F59E0B',
    defaultConfig: { title: '今日备忘', content: '1. 完成 D92 视觉走查\n2. 整理素材云端目录' },
    declarativeOnlyNote: '声明式本地持久化便签',
  },
  {
    id: 'cap-photo-album',
    key: 'photo-album',
    name: '电子相册轮播框',
    version: '1.9.0',
    category: '生活美学',
    description: '在主题内嵌独立小相框，自动轮换精选回忆照片',
    requiredPermissions: ['library.images.read'],
    minClientVersion: '2.1.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '标准',
    supportedInRuntime: true,
    defaultW: 24,
    defaultH: 52,
    defaultAccent: '#3B82F6',
    defaultConfig: { caption: 'Autumn Desk Setup', intervalSec: 15 },
    declarativeOnlyNote: '使用受控图像解码器渲染',
  },
  {
    id: 'cap-pomodoro',
    key: 'pomodoro',
    name: '番茄钟专注插件',
    version: '2.0.0',
    category: '效率工具',
    description: '25/5 分钟专业番茄工作法计时器，联动桌面专注状态',
    requiredPermissions: ['timer.local'],
    minClientVersion: '2.0.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '轻量',
    supportedInRuntime: true,
    defaultW: 26,
    defaultH: 34,
    defaultAccent: '#10B981',
    defaultConfig: { focusMinutes: 25, breakMinutes: 5 },
    declarativeOnlyNote: '复用客户端内置 Pomodoro 引擎',
  },
  {
    id: 'cap-stock-ticker',
    key: 'stock-ticker',
    name: '行情信息观察条',
    version: '1.4.0',
    category: '资讯行情',
    description: '实时跟踪自选指数、科技股涨跌幅（模拟演示行情源）',
    requiredPermissions: ['network.quotes.readonly'],
    minClientVersion: '2.2.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '轻量',
    supportedInRuntime: true,
    defaultW: 32,
    defaultH: 36,
    defaultAccent: '#EF4444',
    defaultConfig: { symbol: 'NVDA', price: '142.85', change: '+2.41%' },
    declarativeOnlyNote: '通过白名单行情适配通道拉取 JSON 数据',
  },
  {
    id: 'cap-sysmon',
    key: 'system-monitor',
    name: '深度系统监控插件',
    version: '2.3.0',
    category: '系统监控',
    description: '多传感器 CPU/GPU 功耗、风扇转速与网络上下行速率',
    requiredPermissions: ['system.sensors.read'],
    minClientVersion: '2.0.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '标准',
    supportedInRuntime: true,
    defaultW: 34,
    defaultH: 48,
    defaultAccent: '#0EA5E9',
    defaultConfig: { refreshSeconds: 2, showNet: true },
    declarativeOnlyNote: '由客户端原生传感器守护进程提供数据',
  },
  {
    id: 'cap-countdown',
    key: 'countdown-timer',
    name: '正向/倒向计时器',
    version: '1.3.0',
    category: '效率工具',
    description: '会议、演讲或摄影补光秒表计时器',
    requiredPermissions: ['timer.local'],
    minClientVersion: '2.0.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '轻量',
    supportedInRuntime: true,
    defaultW: 24,
    defaultH: 32,
    defaultAccent: '#6366F1',
    defaultConfig: { label: '冲刺倒计时', minutes: 45 },
    declarativeOnlyNote: '声明式时间组件',
  },
  {
    id: 'cap-todo',
    key: 'todo-list',
    name: '待办事项清单',
    version: '1.6.0',
    category: '效率工具',
    description: '高亮展示最多 3 条核心待办及完成进度',
    requiredPermissions: ['storage.local'],
    minClientVersion: '2.0.0',
    supportedDevices: ['D92', 'D69'],
    performanceTier: '轻量',
    supportedInRuntime: true,
    defaultW: 28,
    defaultH: 46,
    defaultAccent: '#2563EB',
    defaultConfig: { item1: '完成主题编辑器走查', item2: '同步多设备布局', item3: '导出备份 JSON' },
    declarativeOnlyNote: '声明式清单配置模板',
  },
  {
    id: 'cap-aquarium',
    key: 'desktop-aquarium',
    name: '动态桌面鱼缸',
    version: '1.1.0',
    category: '互动娱乐',
    description: '微生态热带鱼游动与气泡交互渲染层',
    requiredPermissions: ['gpu.shader.2d'],
    minClientVersion: '2.4.0',
    supportedDevices: ['D92'],
    performanceTier: '高性能',
    supportedInRuntime: true,
    defaultW: 36,
    defaultH: 54,
    defaultAccent: '#06B6D4',
    defaultConfig: { fishCount: 4, bubbleDensity: 'medium' },
    declarativeOnlyNote: '仅支持受信任渲染管线的 2D Canvas 插件包',
  },
  {
    id: 'cap-pet',
    key: 'desktop-pet',
    name: '萌宠互动伴侣',
    version: '1.0.0',
    category: '互动娱乐',
    description: '需要 v3.0 原生骨骼动画引擎支持的桌面交互萌宠',
    requiredPermissions: ['gpu.spine.runtime', 'hid.interaction'],
    minClientVersion: '3.0.0',
    supportedDevices: ['D92 Ultra (v3.0+)'],
    performanceTier: '高性能',
    supportedInRuntime: false,
    unsupportedReason: `当前客户端引擎 v${CLIENT_ENGINE_VERSION} 暂未内置 v3.0 骨骼动画运行时（仅保留能力注册与占位预览，不会静默崩溃）`,
    defaultW: 22,
    defaultH: 42,
    defaultAccent: '#F472B6',
    defaultConfig: { petName: 'Mochi', mood: 'happy' },
    declarativeOnlyNote: '高版本受控原生能力，需要随客户端安全升级下发',
  },
];

export function createBlankEditorProject(
  device: DeviceInfo,
  options?: {
    name?: string;
    category?: '主题' | '动态时钟';
    template?: BuiltinTemplateSpec;
  }
): EditorThemeProject {
  const isPortrait = device.orientation === 'portrait';
  const primarySize: [number, number] = isPortrait
    ? [device.nativeHeight, device.nativeWidth]
    : [device.nativeWidth, device.nativeHeight];
  const secondarySize: [number, number] = isPortrait
    ? [device.nativeWidth, device.nativeHeight]
    : [device.nativeHeight, device.nativeWidth];

  const tpl = options?.template;
  const master = tpl
    ? tpl.buildElements(isPortrait)
    : [
        makeElement('clock', { size: isPortrait ? 28 : 36 }) as ClockElement,
        makeElement('date', { a: 'bc', y: isPortrait ? -12 : -10, size: isPortrait ? 5.5 : 7, alpha: 0.78 }) as DateElement,
      ];

  const bg: BgConfig = {
    kind: 'style',
    style: 'ink',
    dim: 0,
    blur: 0,
    fit: 'cover',
    ...(tpl?.bg ?? {}),
  };

  const layouts: EditorLayout[] = [
    { size: primarySize, label: specLabelFromSize(primarySize), follow: false },
    { size: secondarySize, label: specLabelFromSize(secondarySize), follow: true },
  ];

  return {
    template_id: `custom-${Date.now().toString(36)}`,
    name: options?.name || tpl?.name || 'D92_新建创意主题',
    description: tpl?.description || '由 D92 Studio 主题编辑器 V2.0 创作',
    category: options?.category || tpl?.category || '主题',
    category_id: tpl?.subCategory || '自定义',
    tags: tpl?.tags || ['自定义'],
    version: '1.0.0',
    schema_version: THEME_SCHEMA_VERSION,
    min_client_version: '2.0.0',
    required_capabilities: [],
    author: '我的创作',
    entitlement: 'free',
    supported_devices: ['D92', 'D69'],
    supported_resolutions: [`${primarySize[0]}x${primarySize[1]}`, `${secondarySize[0]}x${secondarySize[1]}`],
    thumbnail_variants: {},
    bg,
    master,
    layouts,
    locks: {},
    names: {},
    asset_manifest: [],
    updated_at: Date.now(),
    sourceOrigin: tpl ? 'builtin' : 'custom',
  };
}

export function ensureEditorProjectFromTheme(
  theme: ThemeItem,
  device: DeviceInfo
): EditorThemeProject {
  if (theme.editorProject) {
    return { ...structuredClone(theme.editorProject), name: theme.displayName };
  }
  const base = createBlankEditorProject(device, {
    name: theme.displayName,
    category: theme.category === '动态时钟' ? '动态时钟' : '主题',
  });
  if (theme.source === 'local' && (theme.imageUrl || theme.videoUrl)) {
    const [cw, ch] = base.layouts[0].size;
    const width = theme.sourceWidth || cw;
    const height = theme.sourceHeight || ch;
    const box = computeImportMediaBox(width, height, cw, ch);
    const media = {
      w: box.w, h: box.h, ar: box.ar, size: box.size, fit: box.fit,
      sourceMeta: { origin: 'library' as const, naturalWidth: width, naturalHeight: height, name: theme.displayName },
    };
    base.master = theme.videoUrl
      ? [makeElement('video', { ...media, src: theme.videoUrl, poster: theme.thumbnailUrl ?? theme.imageUrl, dur: theme.durationSeconds ?? 0 })]
      : [makeElement('image', { ...media, src: theme.imageUrl! })];
    base.template_id = theme.id;
    return base;
  }
  if (theme.imageUrl) {
    base.bg = {
      kind: 'image',
      url: theme.imageUrl,
      dim: theme.clockStyle && theme.clockStyle !== 'none' ? 20 : 0,
      blur: 0,
      fit: 'cover',
    };
  } else if (theme.customArt === 'aurora-prism') {
    base.bg = { kind: 'style', style: 'aurora', dim: 0, blur: 0, fit: 'cover' };
  } else if (theme.customArt === 'wealth-calligraphy') {
    base.bg = { kind: 'gradient', g1: '#EAE2D6', g2: '#E6DDD0', angle: 90, dim: 0, blur: 0, fit: 'cover' };
  }

  if (theme.clockStyle === 'none') {
    base.master = [];
  } else if (theme.clockStyle === 'flip-wood') {
    base.master = [makeElement('clock', { style: 'flip', size: 32, color: '#FFF7ED' })];
  } else if (theme.clockStyle === 'minimal-warm') {
    base.master = [makeElement('clock', { style: 'minimal', size: 32, color: '#ffffff' })];
  }

  base.template_id = theme.id;
  return base;
}
