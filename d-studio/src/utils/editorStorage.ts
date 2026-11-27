import { EditorElement } from '../types/editor';
import { genEditorId } from '../data/editorCatalog';

const CLIPBOARD_STORAGE_KEY = 'd92-te-clipboard-v1';
const STYLE_CLIPBOARD_STORAGE_KEY = 'd92-te-style-clipboard-v1';
const CUSTOM_ASSETS_STORAGE_KEY = 'd92-te-custom-assets-v1';

export interface StyleClipboardPayload {
  sourceKind: EditorElement['kind'];
  color?: string;
  alpha?: number;
  weight?: number;
  font?: string;
  ls?: number;
  fx?: EditorElement['fx'];
  lh?: number;
  align?: '' | 'left' | 'center' | 'right';
  g1?: string;
  g2?: string;
  gAngle?: number;
  radius?: number;
  bw?: number;
  strokeColor?: string;
  fillMode?: 'solid' | 'gradient';
  color2?: string;
  gradAngle?: number;
  backdropBlur?: number;
  shadow?: {
    color: string;
    alpha?: number;
    blur: number;
    x: number;
    y: number;
  };
  blur?: number;
  fit?: 'fill' | 'cover' | 'contain';
  brightness?: number;
  contrast?: number;
  saturate?: number;
  feather?: {
    mode: 'all' | 'left' | 'right' | 'top' | 'bottom' | 'horizontal' | 'vertical';
    strength: number;
  };
  accentColor?: string;
  bgOpacity?: number;
  style?: 'digital' | 'flip' | 'minimal' | 'analog';
  dial?: 'tick' | 'dot' | 'num' | 'roman' | 'bare';
  hands?: 'line' | 'round' | 'taper';
  ring?: boolean;
  accent?: string;
}

export interface CustomAssetPreset {
  id: string;
  name: string;
  createdAt: number;
  elements: EditorElement[];
}

export function loadPersistedClipboard(): EditorElement[] {
  try {
    const raw = localStorage.getItem(CLIPBOARD_STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as EditorElement[]) : [];
  } catch {
    return [];
  }
}

export function savePersistedClipboard(elements: EditorElement[]): boolean {
  try {
    localStorage.setItem(CLIPBOARD_STORAGE_KEY, JSON.stringify(elements));
    return true;
  } catch {
    return false;
  }
}

export function loadPersistedStyleClipboard(): StyleClipboardPayload | null {
  try {
    const raw = localStorage.getItem(STYLE_CLIPBOARD_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed && typeof parsed === 'object' ? (parsed as StyleClipboardPayload) : null;
  } catch {
    return null;
  }
}

export function savePersistedStyleClipboard(payload: StyleClipboardPayload | null): boolean {
  try {
    if (!payload) {
      localStorage.removeItem(STYLE_CLIPBOARD_STORAGE_KEY);
      return true;
    }
    localStorage.setItem(STYLE_CLIPBOARD_STORAGE_KEY, JSON.stringify(payload));
    return true;
  } catch {
    return false;
  }
}

export function extractStyleFromElement(el: EditorElement): StyleClipboardPayload {
  const payload: StyleClipboardPayload = {
    sourceKind: el.kind,
    color: el.color,
    alpha: el.alpha,
    weight: el.weight,
    font: el.font,
    ls: el.ls,
    fx: el.fx,
  };

  if (el.shadow) {
    payload.shadow = { ...el.shadow };
  }

  if (el.kind === 'text') {
    payload.lh = el.lh;
    payload.align = el.align;
    payload.g1 = el.g1;
    payload.g2 = el.g2;
    payload.gAngle = el.gAngle;
  } else if (el.kind === 'shape') {
    payload.radius = el.radius;
    payload.bw = el.bw;
    payload.strokeColor = el.strokeColor;
    payload.fillMode = el.fillMode;
    payload.color2 = el.color2;
    payload.gradAngle = el.gradAngle;
    payload.backdropBlur = el.backdropBlur;
  } else if (el.kind === 'image' || el.kind === 'video') {
    payload.radius = el.radius;
    payload.blur = el.blur;
    payload.fit = el.fit;
    payload.brightness = el.brightness;
    payload.contrast = el.contrast;
    payload.saturate = el.saturate;
    payload.feather = el.feather ? { ...el.feather } : undefined;
  } else if (el.kind === 'widget' || el.kind === 'plugin') {
    payload.accentColor = el.accentColor;
    payload.bgOpacity = el.bgOpacity;
  } else if (el.kind === 'clock') {
    payload.style = el.style;
    payload.dial = el.dial;
    payload.hands = el.hands;
    payload.ring = el.ring;
    payload.accent = el.accent;
  }

  return payload;
}

export function applyStyleToElement(
  el: EditorElement,
  style: StyleClipboardPayload
): EditorElement {
  const next = structuredClone(el);

  if (style.alpha != null) next.alpha = style.alpha;

  if (style.shadow !== undefined) {
    next.shadow = { ...style.shadow };
  }

  if (next.kind === 'widget' || next.kind === 'plugin') {
    if (style.accentColor) {
      next.accentColor = style.accentColor;
    } else if (style.color) {
      next.accentColor = style.color;
    }
    if (style.bgOpacity != null) {
      next.bgOpacity = style.bgOpacity;
    }
    if (style.color) {
      next.color = style.color;
    }
    return next;
  }

  if (style.color && next.kind !== 'image' && next.kind !== 'video') {
    next.color = style.color;
  }

  if (next.kind === 'text' || next.kind === 'clock' || next.kind === 'date') {
    if (style.weight != null) next.weight = style.weight;
    if (style.font !== undefined) next.font = style.font;
    if (style.ls !== undefined) next.ls = style.ls;
    if (style.fx !== undefined) {
      next.fx = next.kind !== 'text' && style.fx === 'gradient' ? '' : style.fx;
    }
  }

  if (next.kind === 'text') {
    if (style.lh !== undefined) next.lh = style.lh;
    if (style.align !== undefined) next.align = style.align;
    if (style.g1 !== undefined) next.g1 = style.g1;
    if (style.g2 !== undefined) next.g2 = style.g2;
    if (style.gAngle !== undefined) next.gAngle = style.gAngle;
  } else if (next.kind === 'shape') {
    if (style.radius != null) next.radius = style.radius;
    if (style.bw !== undefined) next.bw = style.bw;
    if (style.strokeColor !== undefined) next.strokeColor = style.strokeColor;
    if (style.fillMode !== undefined) next.fillMode = style.fillMode;
    if (style.color2 !== undefined) next.color2 = style.color2;
    if (style.gradAngle !== undefined) next.gradAngle = style.gradAngle;
    if (style.backdropBlur !== undefined) next.backdropBlur = style.backdropBlur;
  } else if (next.kind === 'image' || next.kind === 'video') {
    if (style.radius !== undefined) next.radius = style.radius;
    if (style.blur !== undefined) next.blur = style.blur;
    if (style.fit !== undefined) next.fit = style.fit;
    if (style.brightness !== undefined) next.brightness = style.brightness;
    if (style.contrast !== undefined) next.contrast = style.contrast;
    if (style.saturate !== undefined) next.saturate = style.saturate;
    if (style.feather !== undefined) next.feather = { ...style.feather };
  } else if (next.kind === 'clock' && style.sourceKind === 'clock') {
    if (style.style) next.style = style.style;
    if (style.dial) next.dial = style.dial;
    if (style.hands) next.hands = style.hands;
    if (style.ring !== undefined) next.ring = style.ring;
    if (style.accent !== undefined) next.accent = style.accent;
  }

  return next;
}

const FX_ANCHOR: Record<'l' | 'c' | 'r', number> = { l: 0, c: 50, r: 100 };
const FY_ANCHOR: Record<'t' | 'm' | 'b', number> = { t: 0, m: 50, b: 100 };

/**
 * Normalizes a selection of elements so their combined center aligns at (mc, 0, 0),
 * preserving relative offsets when inserted as a reusable asset preset.
 */
export function buildCenteredAssetElements(elements: EditorElement[]): EditorElement[] {
  if (elements.length === 0) return [];
  const cloned = structuredClone(elements);
  const absPositions = cloned.map((el) => {
    const fx = FX_ANCHOR[el.a[1] as 'l' | 'c' | 'r'] ?? 50;
    const fy = FY_ANCHOR[el.a[0] as 't' | 'm' | 'b'] ?? 50;
    return { absX: fx + el.x, absY: fy + el.y };
  });
  const avgX = absPositions.reduce((s, p) => s + p.absX, 0) / absPositions.length;
  const avgY = absPositions.reduce((s, p) => s + p.absY, 0) / absPositions.length;

  return cloned.map((el, idx) => ({
    ...el,
    a: 'mc',
    x: +(absPositions[idx].absX - avgX).toFixed(2),
    y: +(absPositions[idx].absY - avgY).toFixed(2),
    g: undefined,
    hide: false,
  }));
}

export function loadCustomAssets(): CustomAssetPreset[] {
  try {
    const raw = localStorage.getItem(CUSTOM_ASSETS_STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as CustomAssetPreset[]) : [];
  } catch {
    return [];
  }
}

export function saveCustomAsset(
  name: string,
  elements: EditorElement[]
): { ok: boolean; asset?: CustomAssetPreset; list: CustomAssetPreset[] } {
  const normalized = buildCenteredAssetElements(elements);
  const asset: CustomAssetPreset = {
    id: genEditorId('myasset'),
    name: name.trim() || '我的素材预设',
    createdAt: Date.now(),
    elements: normalized,
  };
  const existing = loadCustomAssets();
  const next = [asset, ...existing];
  try {
    localStorage.setItem(CUSTOM_ASSETS_STORAGE_KEY, JSON.stringify(next));
    return { ok: true, asset, list: next };
  } catch {
    return { ok: false, list: existing };
  }
}

export function deleteCustomAsset(id: string): CustomAssetPreset[] {
  const next = loadCustomAssets().filter((a) => a.id !== id);
  try {
    localStorage.setItem(CUSTOM_ASSETS_STORAGE_KEY, JSON.stringify(next));
  } catch {
    // ignore
  }
  return next;
}
