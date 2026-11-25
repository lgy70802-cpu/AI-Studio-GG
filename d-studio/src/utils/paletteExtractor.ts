import { BgConfig, BgStylePreset } from '../types/editor';

const STYLE_PALETTES: Record<BgStylePreset, string[]> = {
  paper: ['#4A463C', '#8C7A5B', '#D4A373', '#2B2721', '#E07A5F', '#F6F2E8'],
  ink: ['#FFFFFF', '#60A5FA', '#A78BFA', '#38BDF8', '#94A3B8', '#1E293B'],
  night: ['#E0F2FE', '#38BDF8', '#818CF8', '#F472B6', '#FFFFFF', '#131C36'],
  mountain: ['#F8FAFC', '#94A3B8', '#38BDF8', '#CBD5E1', '#F59E0B', '#1E293B'],
  ocean: ['#E0F7FA', '#22D3EE', '#38BDF8', '#34D399', '#FFFFFF', '#0E4766'],
  dusk: ['#FFF7ED', '#FB923C', '#F472B6', '#FBBF24', '#C084FC', '#5C2B5E'],
  aurora: ['#F0FDF4', '#34D399', '#38BDF8', '#818CF8', '#60A5FA', '#162447'],
  'grid-tech': ['#F8FAFC', '#38BDF8', '#6366F1', '#10B981', '#94A3B8', '#0F172A'],
};

function clamp255(n: number): number {
  return Math.max(0, Math.min(255, Math.round(n)));
}

function rgbToHex(r: number, g: number, b: number): string {
  return (
    '#' +
    [clamp255(r), clamp255(g), clamp255(b)]
      .map((x) => x.toString(16).padStart(2, '0'))
      .join('')
      .toUpperCase()
  );
}

function hexToRgb(hex: string): [number, number, number] | null {
  const clean = hex.replace('#', '').trim();
  if (clean.length === 3) {
    const r = parseInt(clean[0] + clean[0], 16);
    const g = parseInt(clean[1] + clean[1], 16);
    const b = parseInt(clean[2] + clean[2], 16);
    return Number.isNaN(r) ? null : [r, g, b];
  }
  if (clean.length === 6) {
    const r = parseInt(clean.slice(0, 2), 16);
    const g = parseInt(clean.slice(2, 4), 16);
    const b = parseInt(clean.slice(4, 6), 16);
    return Number.isNaN(r) ? null : [r, g, b];
  }
  return null;
}

function getLuminance(r: number, g: number, b: number): number {
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255;
}

function getSaturation(r: number, g: number, b: number): number {
  const max = Math.max(r, g, b) / 255;
  const min = Math.min(r, g, b) / 255;
  if (max === min) return 0;
  const l = (max + min) / 2;
  const d = max - min;
  return l > 0.5 ? d / (2 - max - min) : d / (max + min);
}

function colorDist(a: [number, number, number], b: [number, number, number]): number {
  return Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
}

function mixHex(h1: string, h2: string, t = 0.5): string {
  const a = hexToRgb(h1);
  const b = hexToRgb(h2);
  if (!a || !b) return h1;
  return rgbToHex(
    a[0] * (1 - t) + b[0] * t,
    a[1] * (1 - t) + b[1] * t,
    a[2] * (1 - t) + b[2] * t
  );
}

function lightenHex(hex: string, amt = 0.55): string {
  return mixHex(hex, '#FFFFFF', amt);
}

function darkenHex(hex: string, amt = 0.55): string {
  return mixHex(hex, '#0F172A', amt);
}

export function getFallbackPaletteFromBg(bg: BgConfig): string[] {
  if (bg.kind === 'style') {
    return STYLE_PALETTES[bg.style || 'ink'] ?? STYLE_PALETTES.ink;
  }

  if (bg.kind === 'color') {
    const base = bg.color || '#141026';
    const rgb = hexToRgb(base) ?? [20, 16, 38];
    const isLight = getLuminance(rgb[0], rgb[1], rgb[2]) > 0.58;
    const primaryText = isLight ? '#1A172B' : '#FFFFFF';
    const muted = isLight ? darkenHex(base, 0.55) : lightenHex(base, 0.65);
    const soft = isLight ? darkenHex(base, 0.35) : lightenHex(base, 0.42);
    return Array.from(
      new Set([primaryText, muted, soft, '#5E45F8', '#38BDF8', '#F59E0B'])
    ).slice(0, 6);
  }

  if (bg.kind === 'gradient') {
    const g1 = bg.g1 || '#180942';
    const g2 = bg.g2 || '#5E45F8';
    const mid = mixHex(g1, g2, 0.5);
    const rgb1 = hexToRgb(g1) ?? [24, 9, 66];
    const rgb2 = hexToRgb(g2) ?? [94, 69, 248];
    const avgLum = (getLuminance(...rgb1) + getLuminance(...rgb2)) / 2;
    const contrast = avgLum > 0.58 ? '#1A172B' : '#FFFFFF';
    const highlight = lightenHex(g2, 0.55);
    const soft1 = lightenHex(g1, 0.65);
    return Array.from(new Set([contrast, highlight, mid, g2, soft1, g1])).slice(0, 6);
  }

  return ['#FFFFFF', '#F8FAFC', '#38BDF8', '#A78BFA', '#F59E0B', '#1A172B'];
}

export function extractPaletteFromBg(bg: BgConfig): Promise<string[]> {
  if (bg.kind === 'style' || bg.kind === 'color' || bg.kind === 'gradient') {
    return Promise.resolve(getFallbackPaletteFromBg(bg));
  }

  const imgSrc = bg.kind === 'image' ? bg.url : bg.poster;
  if (!imgSrc) {
    return Promise.resolve(getFallbackPaletteFromBg(bg));
  }

  return new Promise((resolve) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    let done = false;
    const finish = (colors: string[]) => {
      if (done) return;
      done = true;
      resolve(colors);
    };

    const timer = window.setTimeout(() => finish(getFallbackPaletteFromBg(bg)), 1800);

    img.onload = () => {
      window.clearTimeout(timer);
      try {
        const size = 36;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        if (!ctx) {
          finish(getFallbackPaletteFromBg(bg));
          return;
        }
        ctx.drawImage(img, 0, 0, size, size);
        const data = ctx.getImageData(0, 0, size, size).data;

        const buckets = new Map<
          string,
          { rSum: number; gSum: number; bSum: number; count: number }
        >();
        let lumSum = 0;
        let pixelCount = 0;

        for (let i = 0; i < data.length; i += 4) {
          const a = data[i + 3];
          if (a < 120) continue;
          const r = data[i];
          const g = data[i + 1];
          const b = data[i + 2];
          lumSum += getLuminance(r, g, b);
          pixelCount += 1;
          const qr = Math.floor(r / 28);
          const qg = Math.floor(g / 28);
          const qb = Math.floor(b / 28);
          const key = `${qr}-${qg}-${qb}`;
          const prev = buckets.get(key);
          if (prev) {
            prev.rSum += r;
            prev.gSum += g;
            prev.bSum += b;
            prev.count += 1;
          } else {
            buckets.set(key, { rSum: r, gSum: g, bSum: b, count: 1 });
          }
        }

        if (pixelCount === 0 || buckets.size === 0) {
          finish(getFallbackPaletteFromBg(bg));
          return;
        }

        const candidates = Array.from(buckets.values()).map((b) => {
          const r = b.rSum / b.count;
          const g = b.gSum / b.count;
          const bl = b.bSum / b.count;
          const sat = getSaturation(r, g, bl);
          const score = b.count * (0.65 + sat * 1.65);
          return { rgb: [r, g, bl] as [number, number, number], score, sat };
        });

        candidates.sort((a, b) => b.score - a.score);

        const picked: [number, number, number][] = [];
        for (const c of candidates) {
          if (picked.every((p) => colorDist(p, c.rgb) > 46)) {
            picked.push(c.rgb);
          }
          if (picked.length >= 4) break;
        }

        const avgLum = lumSum / pixelCount;
        const primaryContrast = avgLum > 0.58 ? '#1A172B' : '#FFFFFF';
        const secondaryTint = picked[0]
          ? avgLum > 0.58
            ? darkenHex(rgbToHex(...picked[0]), 0.45)
            : lightenHex(rgbToHex(...picked[0]), 0.58)
          : '#E2E8F0';

        const finalPalette = Array.from(
          new Set([
            primaryContrast,
            secondaryTint,
            ...picked.map((p) => rgbToHex(...p)),
          ])
        ).slice(0, 6);

        finish(finalPalette.length > 0 ? finalPalette : getFallbackPaletteFromBg(bg));
      } catch {
        finish(getFallbackPaletteFromBg(bg));
      }
    };

    img.onerror = () => {
      window.clearTimeout(timer);
      finish(getFallbackPaletteFromBg(bg));
    };

    img.src = imgSrc;
  });
}
