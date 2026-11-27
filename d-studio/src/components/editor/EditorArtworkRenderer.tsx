import { FlipClock } from '../widgets/FlipClock';
import React, { useEffect, useState } from 'react';
import {
  AlertTriangle,
  BarChart3,
  CheckSquare,
  CloudSun,
  Cpu,
  Fish,
  Image as ImageIcon,
  Music2,
  Sparkles,
  StickyNote,
  Timer,
  TrendingUp,
} from 'lucide-react';
import {
  BUILTIN_BG_STYLES,
  EDITOR_TIMEZONES,
  FONT_CSS_MAP,
  PLUGIN_CAPABILITY_REGISTRY,
} from '../../data/editorCatalog';
import {
  CraftAquariumWidget,
  CraftLyricsWidget,
  CraftNixieClockWidget,
  CraftStocksWidget,
  NixieColorHue,
} from '../crafts/CraftWidgets';
import {
  BonsaiSpecies,
  BonsaiSunlight,
  CassetteShellColor,
  CraftBonsaiWidget,
  CraftCampfireWidget,
  CraftCassetteWidget,
  CraftCountdownWidget,
  CraftCyberPetWidget,
  CraftFlipClockWidget,
  CraftNetSpeedWidget,
  CraftParcelWidget,
  CraftVuMeterWidget,
  CraftZodiacWidget,
  CyberPetMood,
  CyberPetSpecies,
  FlipShellStyle,
  MilestoneMode,
  NetUnitMode,
  ParcelStage,
  VuBacklightHue,
  VuSensitivity,
  ZodiacSignKey,
} from '../crafts/ExtendedCraftWidgets';
import {
  AnchorPoint,
  BgConfig,
  ClockElement,
  DateElement,
  EditorElement,
  EditorElementBase,
  EditorThemeProject,
  ImageFeatherMode,
  MotionElement,
  PluginElement,
  ShapeElement,
  TextElement,
  WidgetElement,
} from '../../types/editor';

export const FX_ANCHOR: Record<'l' | 'c' | 'r', number> = { l: 0, c: 50, r: 100 };
export const FY_ANCHOR: Record<'t' | 'm' | 'b', number> = { t: 0, m: 50, b: 100 };

export function resolveAnchorPercentages(anchor: AnchorPoint): [number, number] {
  const vt = anchor[0] as 't' | 'm' | 'b';
  const hz = (anchor[1] as 'l' | 'c' | 'r') || 'c';
  return [FX_ANCHOR[hz] ?? 50, FY_ANCHOR[vt] ?? 50];
}

export function getLayoutElements(project: EditorThemeProject, layoutIndex: number): EditorElement[] {
  const layout = project.layouts[layoutIndex];
  if (layoutIndex === 0 || !layout || layout.follow) {
    return project.master;
  }
  return layout.elements ?? project.master;
}

export function isElementHidden(
  project: EditorThemeProject,
  layoutIndex: number,
  el: EditorElement
): boolean {
  const layout = project.layouts[layoutIndex];
  if (layoutIndex > 0 && layout && layout.follow && layout.vis && el.id in layout.vis) {
    return Boolean(layout.vis[el.id]);
  }
  return Boolean(el.hide);
}

function pad2(n: number): string {
  return String(n).padStart(2, '0');
}

function getTimeParts(tz: ClockElement['tz'], live: boolean, now: Date) {
  const zone = EDITOR_TIMEZONES.find((z) => z.id === tz) ?? EDITOR_TIMEZONES[0];
  if (live) {
    try {
      const parts = Object.fromEntries(
        new Intl.DateTimeFormat('en-GB', {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
          hourCycle: 'h23',
          timeZone: zone.iana,
        })
          .formatToParts(now)
          .map((x) => [x.type, x.value])
      );
      return {
        h: Number(parts.hour ?? now.getHours()),
        m: Number(parts.minute ?? now.getMinutes()),
        s: Number(parts.second ?? now.getSeconds()),
      };
    } catch {
      return { h: now.getHours(), m: now.getMinutes(), s: now.getSeconds() };
    }
  }
  return {
    h: ((10 + zone.offsetHours) % 24 + 24) % 24,
    m: 24,
    s: 36,
  };
}

function getDateText(e: DateElement, live: boolean, now: Date): string {
  const d = live ? now : new Date(2026, 9, 30);
  const w = d.getDay();
  const CN = ['日', '一', '二', '三', '四', '五', '六'];
  const EN = ['SUNDAY', 'MONDAY', 'TUESDAY', 'WEDNESDAY', 'THURSDAY', 'FRIDAY', 'SATURDAY'];
  const MO = [
    'JANUARY',
    'FEBRUARY',
    'MARCH',
    'APRIL',
    'MAY',
    'JUNE',
    'JULY',
    'AUGUST',
    'SEPTEMBER',
    'OCTOBER',
    'NOVEMBER',
    'DECEMBER',
  ];

  if (e.part === 'weekday') {
    return e.fmt === 1 || e.fmt === 3 ? EN[w] : `星期${CN[w]}`;
  }
  if (e.part === 'ampm') {
    const h = live ? now.getHours() : 10;
    return h >= 12 ? 'PM · 下午' : 'AM · 上午';
  }
  if (e.part === 'ymd') {
    return `${d.getFullYear()}.${pad2(d.getMonth() + 1)}.${pad2(d.getDate())}`;
  }

  const formats = [
    `${d.getMonth() + 1}月${d.getDate()}日 周${CN[w]}`,
    `${EN[w]} · ${MO[d.getMonth()]} ${d.getDate()}`,
    `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`,
    `${pad2(d.getMonth() + 1)}.${pad2(d.getDate())} ${EN[w].slice(0, 3)}`,
  ];
  return formats[e.fmt || 0] ?? formats[0];
}

function shadowToRgba(color: string, alphaPct = 55): string {
  const a = Math.max(0, Math.min(1, alphaPct / 100));
  const clean = color.trim().replace('#', '');
  if (/^[0-9a-fA-F]{6}$/.test(clean)) {
    const r = parseInt(clean.slice(0, 2), 16);
    const g = parseInt(clean.slice(2, 4), 16);
    const b = parseInt(clean.slice(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${a.toFixed(2)})`;
  }
  if (/^[0-9a-fA-F]{3}$/.test(clean)) {
    const r = parseInt(clean[0] + clean[0], 16);
    const g = parseInt(clean[1] + clean[1], 16);
    const b = parseInt(clean[2] + clean[2], 16);
    return `rgba(${r}, ${g}, ${b}, ${a.toFixed(2)})`;
  }
  return color;
}

function buildBoxShadowCss(shadow?: EditorElementBase['shadow']): string | undefined {
  if (!shadow) return undefined;
  if (shadow.blur <= 0 && shadow.x === 0 && shadow.y === 0) return undefined;
  const rgba = shadowToRgba(shadow.color || '#000000', shadow.alpha ?? 55);
  return `${shadow.x}cqmin ${shadow.y}cqmin ${shadow.blur}cqmin ${rgba}`;
}

function buildDropShadowCss(shadow?: EditorElementBase['shadow']): string | undefined {
  if (!shadow) return undefined;
  if (shadow.blur <= 0 && shadow.x === 0 && shadow.y === 0) return undefined;
  const rgba = shadowToRgba(shadow.color || '#000000', shadow.alpha ?? 55);
  return `drop-shadow(${shadow.x}cqmin ${shadow.y}cqmin ${(shadow.blur * 0.6).toFixed(2)}cqmin ${rgba})`;
}

function buildFeatherStyle(feather?: { mode: ImageFeatherMode; strength: number }): React.CSSProperties {
  const fStrength = feather?.strength ?? 0;
  if (!feather || fStrength <= 0) return {};
  const s = Math.max(1, Math.min(60, fStrength));
  const mode = feather.mode || 'all';
  if (mode === 'all') {
    const hGrad = `linear-gradient(to right, transparent 0%, black ${s}%, black ${100 - s}%, transparent 100%)`;
    const vGrad = `linear-gradient(to bottom, transparent 0%, black ${s}%, black ${100 - s}%, transparent 100%)`;
    return {
      maskImage: `${hGrad}, ${vGrad}`,
      WebkitMaskImage: `${hGrad}, ${vGrad}`,
      maskComposite: 'intersect',
      WebkitMaskComposite: 'source-in',
    };
  }
  const grad =
    mode === 'left'
      ? `linear-gradient(to right, transparent 0%, black ${s}%, black 100%)`
      : mode === 'right'
        ? `linear-gradient(to left, transparent 0%, black ${s}%, black 100%)`
        : mode === 'top'
          ? `linear-gradient(to bottom, transparent 0%, black ${s}%, black 100%)`
          : mode === 'bottom'
            ? `linear-gradient(to top, transparent 0%, black ${s}%, black 100%)`
            : mode === 'horizontal'
              ? `linear-gradient(to right, transparent 0%, black ${s}%, black ${100 - s}%, transparent 100%)`
              : `linear-gradient(to bottom, transparent 0%, black ${s}%, black ${100 - s}%, transparent 100%)`;
  return {
    maskImage: grad,
    WebkitMaskImage: grad,
  };
}

function getTextEffectStyle(el: {
  fx?: string;
  color: string;
  g1?: string;
  g2?: string;
  gAngle?: number;
}): React.CSSProperties {
  if (el.fx === 'glow') {
    return { textShadow: '0 0 0.2em currentColor, 0 0 0.55em currentColor' };
  }
  if (el.fx === 'shadow') {
    return { textShadow: '0 0.06em 0.18em rgba(0,0,0,0.65)' };
  }
  if (el.fx === 'outline') {
    return {
      color: 'transparent',
      WebkitTextStroke: `0.035em ${el.color}`,
    };
  }
  if (el.fx === 'gradient') {
    return {
      backgroundImage: `linear-gradient(${el.gAngle ?? 120}deg, ${el.g1 || '#60A5FA'}, ${el.g2 || '#F472B6'})`,
      WebkitBackgroundClip: 'text',
      WebkitTextFillColor: 'transparent',
    };
  }
  return {};
}

const AnalogClockSvg: React.FC<{ el: ClockElement; live: boolean; now: Date }> = ({
  el,
  live,
  now,
}) => {
  const { h, m, s } = getTimeParts(el.tz, live, now);
  const dial = el.dial || 'tick';
  const hands = el.hands || 'line';
  const acc = el.accent || '#ff6a4d';
  const ha = ((h % 12) + m / 60) * 30;
  const ma = (m + s / 60) * 6;
  const sa = s * 6;

  const filterStyle: React.CSSProperties =
    el.fx === 'glow'
      ? { filter: 'drop-shadow(0 0 0.08em currentColor) drop-shadow(0 0 0.2em currentColor)' }
      : el.fx === 'shadow'
        ? { filter: 'drop-shadow(0 0.05em 0.1em rgba(0,0,0,0.6))' }
        : {};

  const renderHand = (angle: number, len: number, w: number, kind: string) => {
    if (kind === 'taper') {
      const d = `M${50 - w * 0.9} 54 L${50 + w * 0.9} 54 L${50 + w * 0.35} ${50 - len} L${50 - w * 0.35} ${50 - len} Z`;
      return <path d={d} fill="currentColor" transform={`rotate(${angle} 50 50)`} />;
    }
    return (
      <line
        x1="50"
        y1={kind === 'round' ? 50 : 54}
        x2="50"
        y2={50 - len}
        stroke="currentColor"
        strokeWidth={w}
        strokeLinecap="round"
        transform={`rotate(${angle} 50 50)`}
      />
    );
  };

  const hw = hands === 'round' ? 4.6 : 3.2;
  const mw = hands === 'round' ? 3.2 : 2.2;
  const romans = ['XII', 'I', 'II', 'III', 'IIII', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI'];

  return (
    <div className="flex items-center justify-center leading-none">
      <svg
        viewBox="0 0 100 100"
        style={{ width: '2.5em', height: '2.5em', ...filterStyle }}
        aria-hidden="true"
      >
        {el.ring !== false && dial !== 'bare' && (
          <circle cx="50" cy="50" r="48" fill="none" stroke="currentColor" strokeWidth="1.4" opacity="0.85" />
        )}
        {dial === 'tick' &&
          Array.from({ length: 60 }, (_, i) => {
            const big = i % 5 === 0;
            return (
              <line
                key={i}
                x1="50"
                y1="5"
                x2="50"
                y2={big ? 12 : 8}
                stroke="currentColor"
                strokeWidth={big ? 2.2 : 0.9}
                opacity={big ? 1 : 0.55}
                transform={`rotate(${i * 6} 50 50)`}
              />
            );
          })}
        {dial === 'dot' &&
          Array.from({ length: 12 }, (_, i) => (
            <circle
              key={i}
              cx="50"
              cy="9"
              r={i % 3 === 0 ? 2.8 : 1.8}
              fill="currentColor"
              transform={`rotate(${i * 30} 50 50)`}
            />
          ))}
        {dial === 'num' &&
          Array.from({ length: 12 }, (_, idx) => {
            const num = idx + 1;
            const a = (num * 30 * Math.PI) / 180;
            return (
              <text
                key={num}
                x={(50 + 36 * Math.sin(a)).toFixed(2)}
                y={(50 - 36 * Math.cos(a)).toFixed(2)}
                textAnchor="middle"
                dominantBaseline="central"
                fontSize="11"
                fontWeight="600"
                fill="currentColor"
              >
                {num}
              </text>
            );
          })}
        {dial === 'roman' &&
          romans.map((label, i) => {
            const a = (i * 30 * Math.PI) / 180;
            return (
              <text
                key={i}
                x={(50 + 36 * Math.sin(a)).toFixed(2)}
                y={(50 - 36 * Math.cos(a)).toFixed(2)}
                textAnchor="middle"
                dominantBaseline="central"
                fontSize={label.length > 2 ? 7 : 9}
                fill="currentColor"
              >
                {label}
              </text>
            );
          })}
        {dial === 'bare' &&
          Array.from({ length: 4 }, (_, i) => (
            <line
              key={i}
              x1="50"
              y1="6"
              x2="50"
              y2={i % 2 ? 11 : 13}
              stroke="currentColor"
              strokeWidth="2"
              transform={`rotate(${i * 90} 50 50)`}
            />
          ))}

        {renderHand(ha, hands === 'taper' ? 24 : 22, hands === 'taper' ? 2.4 : hw, hands)}
        {renderHand(ma, hands === 'taper' ? 36 : 34, hands === 'taper' ? 1.8 : mw, hands)}
        {el.sec ? (
          <g transform={`rotate(${sa} 50 50)`}>
            <line x1="50" y1="58" x2="50" y2="10" stroke={acc} strokeWidth="1" strokeLinecap="round" />
            <circle cx="50" cy="50" r="2" fill={acc} />
          </g>
        ) : (
          <circle cx="50" cy="50" r="2.4" fill="currentColor" />
        )}
      </svg>
    </div>
  );
};

const ShapeNode: React.FC<{ el: ShapeElement }> = ({ el }) => {
  const sh = el.sh || 'rect';
  const c = el.color;
  const bw = el.bw || 0.8;
  const dropFilter = buildDropShadowCss(el.shadow);

  if (sh === 'dash') {
    return (
      <div
        className="h-full w-full"
        style={{
          background: `linear-gradient(90deg, ${c} 62%, transparent 62%)`,
          backgroundSize: '5% 100%',
          filter: dropFilter,
        }}
      />
    );
  }
  if (sh === 'dots') {
    return (
      <div
        className="h-full w-full"
        style={{
          background: `radial-gradient(circle closest-side at 50% 50%, ${c} 0 92%, transparent 100%)`,
          backgroundSize: `${(el.h * 2.2).toFixed(2)}cqh 100%`,
          backgroundRepeat: 'repeat-x',
          backgroundPosition: 'center',
          filter: dropFilter,
        }}
      />
    );
  }
  if (sh === 'double') {
    return (
      <div
        className="h-full w-full"
        style={{
          background: `linear-gradient(to bottom, ${c} 0 32%, transparent 32% 68%, ${c} 68% 100%)`,
          filter: dropFilter,
        }}
      />
    );
  }
  if (sh === 'ring') {
    return (
      <div
        className="box-border h-full w-full rounded-full"
        style={{ border: `${bw}cqmin solid ${c}`, filter: dropFilter }}
      />
    );
  }
  if (sh === 'brackets') {
    const a = `${(bw * 9).toFixed(2)}cqmin`;
    const b = `${bw}cqmin solid ${c}`;
    return (
      <div className="relative h-full w-full" style={{ filter: dropFilter }}>
        <i className="absolute left-0 top-0" style={{ width: a, height: a, borderLeft: b, borderTop: b }} />
        <i className="absolute right-0 top-0" style={{ width: a, height: a, borderRight: b, borderTop: b }} />
        <i className="absolute left-0 bottom-0" style={{ width: a, height: a, borderLeft: b, borderBottom: b }} />
        <i className="absolute right-0 bottom-0" style={{ width: a, height: a, borderRight: b, borderBottom: b }} />
      </div>
    );
  }
  if (sh === 'dotline') {
    const d = `${(bw * 3.4).toFixed(2)}cqmin`;
    return (
      <div className="flex h-full w-full items-center" style={{ gap: '1.4cqmin', filter: dropFilter }}>
        <i className="h-full flex-1" style={{ background: c }} />
        <i className="shrink-0 rounded-full" style={{ width: d, height: d, background: c }} />
        <i className="h-full flex-1" style={{ background: c }} />
      </div>
    );
  }

  const strokeBorder = el.bw && el.bw > 0 ? `${el.bw}cqmin solid ${el.strokeColor || '#ffffff'}` : undefined;
  const isGrad = el.fillMode === 'gradient';
  const fillStyle: React.CSSProperties = isGrad
    ? {
        backgroundImage: `linear-gradient(${el.gradAngle ?? 135}deg, ${c}, ${el.color2 || '#38BDF8'})`,
      }
    : { backgroundColor: c };
  const backdropStyle: React.CSSProperties =
    el.backdropBlur && el.backdropBlur > 0
      ? {
          backdropFilter: `blur(${el.backdropBlur}px)`,
          WebkitBackdropFilter: `blur(${el.backdropBlur}px)`,
        }
      : {};
  const boxShadow = buildBoxShadowCss(el.shadow);

  if (sh === 'circle') {
    return (
      <div
        className="box-border h-full w-full"
        style={{
          ...fillStyle,
          ...backdropStyle,
          borderRadius: '50%',
          border: strokeBorder,
          boxShadow,
        }}
      />
    );
  }

  return (
    <div
      className="box-border h-full w-full"
      style={{
        ...fillStyle,
        ...backdropStyle,
        borderRadius: el.radius >= 50 ? '9999px' : `${el.radius}cqmin`,
        border: strokeBorder,
        boxShadow,
      }}
    />
  );
};

const MotionNode: React.FC<{ el: MotionElement; playMotion: boolean }> = ({ el, playMotion }) => {
  const dur = (3.4 / Math.max(0.4, el.speed || 1)).toFixed(2);
  const c1 = el.color || '#60A5FA';
  const c2 = el.secondaryColor || '#38BDF8';
  const count = Math.max(4, Math.min(18, Math.round((el.intensity || 60) / 8)));

  if (el.effect === 'gradient-flow') {
    return (
      <div
        className="h-full w-full rounded-[3cqmin]"
        style={{
          background: `linear-gradient(120deg, ${c1}55, ${c2}55, ${c1}35)`,
          backgroundSize: '200% 200%',
          animation: playMotion ? `teGradFlow ${dur}s ease infinite` : undefined,
        }}
      />
    );
  }

  if (el.effect === 'breathe') {
    return (
      <div className="flex h-full w-full items-center justify-center overflow-hidden">
        <div
          className="h-[72%] w-[72%] rounded-full blur-[2cqmin]"
          style={{
            background: `radial-gradient(circle, ${c1}66 0%, ${c2}22 58%, transparent 74%)`,
            animation: playMotion ? `teBreathe ${dur}s ease-in-out infinite` : undefined,
          }}
        />
      </div>
    );
  }

  if (el.effect === 'rotate') {
    return (
      <div className="flex h-full w-full items-center justify-center overflow-hidden">
        <div
          className="h-[76%] max-h-full max-w-full aspect-square rounded-full border-2 border-dashed"
          style={{
            borderColor: c1,
            animation: playMotion ? `teOrbitSpin ${(Number(dur) * 3.5).toFixed(2)}s linear infinite` : undefined,
          }}
        />
      </div>
    );
  }

  if (el.effect === 'fade') {
    return (
      <div
        className="h-full w-full"
        style={{
          background: `radial-gradient(ellipse at center, transparent 40%, ${c1}44 100%)`,
          animation: playMotion ? `teBreathe ${dur}s ease-in-out infinite` : undefined,
        }}
      />
    );
  }

  if (el.effect === 'gif-sticker') {
    return (
      <div className="flex h-full w-full items-center justify-center">
        <div
          className="flex items-center gap-[1.4cqmin] rounded-full border px-[2.8cqmin] py-[1.2cqmin]"
          style={{
            borderColor: `${c1}88`,
            backgroundColor: `${c1}22`,
            color: c1,
            animation: playMotion ? `teBreathe ${dur}s ease-in-out infinite` : undefined,
          }}
        >
          <Sparkles className="h-[4.2cqmin] w-[4.2cqmin]" />
          <span className="font-bold tracking-wider" style={{ fontSize: '3.2cqmin' }}>
            LIVE FX
          </span>
        </div>
      </div>
    );
  }

  if (el.effect === 'meteor') {
    const streakCount = Math.max(3, Math.min(9, Math.round(count / 2)));
    return (
      <div className="relative h-full w-full overflow-hidden">
        {Array.from({ length: streakCount }, (_, idx) => {
          const top = `${((idx * 29 + 6) % 72) - 5}%`;
          const left = `${((idx * 37 + 12) % 78) - 8}%`;
          const len = 18 + (idx % 3) * 8;
          const delay = `${((idx * 0.55) % 2.6).toFixed(2)}s`;
          const stepDur = (Number(dur) * 0.85 + (idx % 3) * 0.45).toFixed(2);
          return (
            <div
              key={idx}
              className="absolute flex items-center justify-end rounded-full"
              style={{
                top,
                left,
                width: `${len}cqw`,
                height: '0.9cqmin',
                background: `linear-gradient(90deg, transparent 0%, ${c2}66 55%, ${c1} 100%)`,
                transform: 'rotate(-28deg)',
                animation: playMotion ? `teMeteor ${stepDur}s ease-in ${delay} infinite` : undefined,
              }}
            >
              <span
                className="rounded-full"
                style={{
                  width: '1.6cqmin',
                  height: '1.6cqmin',
                  backgroundColor: c1,
                  boxShadow: `0 0 2.5cqmin ${c1}, 0 0 4.5cqmin ${c2}`,
                }}
              />
            </div>
          );
        })}
      </div>
    );
  }

  if (el.effect === 'matrix-rain') {
    const colCount = Math.max(6, Math.min(18, count + 2));
    return (
      <div className="relative h-full w-full overflow-hidden">
        {Array.from({ length: colCount }, (_, idx) => {
          const left = `${((idx * 100) / colCount + 2).toFixed(1)}%`;
          const hPct = 28 + (idx % 4) * 12;
          const delay = `${((idx * 0.37) % 2.4).toFixed(2)}s`;
          const stepDur = (Number(dur) * 0.78 + (idx % 4) * 0.38).toFixed(2);
          return (
            <div
              key={idx}
              className="absolute top-0 flex flex-col items-center justify-end rounded-full"
              style={{
                left,
                width: '0.85cqmin',
                height: `${hPct}%`,
                background: `linear-gradient(180deg, transparent 0%, ${c2}55 58%, ${c1} 100%)`,
                animation: playMotion ? `teRainDrop ${stepDur}s linear ${delay} infinite` : undefined,
              }}
            >
              <span
                className="rounded-full"
                style={{
                  width: '1.35cqmin',
                  height: '1.35cqmin',
                  backgroundColor: '#FFFFFF',
                  boxShadow: `0 0 2cqmin ${c1}`,
                }}
              />
            </div>
          );
        })}
      </div>
    );
  }

  if (el.effect === 'scanline') {
    return (
      <div
        className="relative h-full w-full overflow-hidden"
        style={{
          backgroundImage: `repeating-linear-gradient(0deg, transparent, transparent 3px, ${c2}1c 3px, ${c2}1c 4px)`,
        }}
      >
        <div
          className="absolute inset-x-0"
          style={{
            top: '44%',
            height: '2.2cqmin',
            background: `linear-gradient(180deg, transparent 0%, ${c1}ee 50%, transparent 100%)`,
            boxShadow: `0 0 4cqmin ${c1}, 0 0 8cqmin ${c2}`,
            animation: playMotion ? `teScanSweep ${(Number(dur) * 1.1).toFixed(2)}s linear infinite` : undefined,
          }}
        />
      </div>
    );
  }

  if (el.effect === 'ripple') {
    const ringCount = 3;
    return (
      <div className="relative flex h-full w-full items-center justify-center overflow-hidden">
        {Array.from({ length: ringCount }, (_, idx) => {
          const delay = `${((Number(dur) / ringCount) * idx).toFixed(2)}s`;
          const pct = 52 + idx * 14;
          return (
            <div
              key={idx}
              className="absolute aspect-square rounded-full border-[0.7cqmin]"
              style={{
                height: `${pct}%`,
                maxWidth: `${pct}%`,
                borderColor: idx % 2 === 0 ? c1 : c2,
                boxShadow: `0 0 2.5cqmin ${c1}66`,
                animation: playMotion ? `teRippleOut ${dur}s ease-out ${delay} infinite` : undefined,
              }}
            />
          );
        })}
        <span
          className="h-[2.4cqmin] w-[2.4cqmin] rounded-full"
          style={{ backgroundColor: c1, boxShadow: `0 0 3cqmin ${c1}` }}
        />
      </div>
    );
  }

  if (el.effect === 'aurora-ribbon') {
    return (
      <div className="relative h-full w-full overflow-hidden">
        <div
          className="absolute -inset-x-[20%] top-[12%] h-[42%] rounded-full blur-[3.2cqmin]"
          style={{
            background: `linear-gradient(90deg, transparent 5%, ${c1}88 38%, ${c2}99 68%, transparent 95%)`,
            transform: 'rotate(-8deg)',
            animation: playMotion ? `teWaveSlide ${(Number(dur) * 1.4).toFixed(2)}s ease-in-out infinite` : undefined,
          }}
        />
        <div
          className="absolute -inset-x-[15%] bottom-[10%] h-[36%] rounded-full blur-[3.6cqmin]"
          style={{
            background: `linear-gradient(90deg, transparent 5%, ${c2}77 35%, ${c1}88 70%, transparent 95%)`,
            transform: 'rotate(-5deg)',
            animation: playMotion
              ? `teWaveSlide ${(Number(dur) * 1.7).toFixed(2)}s ease-in-out 0.8s infinite reverse`
              : undefined,
          }}
        />
      </div>
    );
  }

  if (el.effect === 'starfield') {
    const starCount = Math.max(8, Math.min(22, count + 5));
    return (
      <div className="relative h-full w-full overflow-hidden">
        {Array.from({ length: starCount }, (_, idx) => {
          const left = `${((idx * 41 + 9) % 90) + 5}%`;
          const top = `${((idx * 61 + 13) % 84) + 8}%`;
          const sizeCq = 1.1 + (idx % 3) * 0.75;
          const delay = `${((idx * 0.31) % 2.5).toFixed(2)}s`;
          const starColor = idx % 3 === 0 ? c2 : c1;
          return (
            <span
              key={idx}
              className="absolute rounded-full"
              style={{
                left,
                top,
                width: `${sizeCq}cqmin`,
                height: `${sizeCq}cqmin`,
                backgroundColor: starColor,
                boxShadow: `0 0 ${sizeCq * 2.2}cqmin ${starColor}`,
                animation: playMotion
                  ? `teTwinkle ${(Number(dur) * 0.75 + (idx % 3) * 0.45).toFixed(2)}s ease-in-out ${delay} infinite`
                  : undefined,
              }}
            />
          );
        })}
      </div>
    );
  }

  if (el.effect === 'firefly') {
    const flyCount = Math.max(5, Math.min(14, count));
    return (
      <div className="relative h-full w-full overflow-hidden">
        {Array.from({ length: flyCount }, (_, idx) => {
          const left = `${((idx * 47 + 15) % 86) + 7}%`;
          const top = `${((idx * 59 + 11) % 78) + 11}%`;
          const sizeCq = 2.0 + (idx % 3) * 0.9;
          const delay = `${((idx * 0.49) % 2.9).toFixed(2)}s`;
          const orbColor = idx % 2 === 0 ? c1 : c2;
          return (
            <span
              key={idx}
              className="absolute rounded-full"
              style={{
                left,
                top,
                width: `${sizeCq}cqmin`,
                height: `${sizeCq}cqmin`,
                background: `radial-gradient(circle, #FFFFFF 0%, ${orbColor} 55%, transparent 100%)`,
                boxShadow: `0 0 ${sizeCq * 2.1}cqmin ${orbColor}`,
                animation: playMotion
                  ? `teFirefly ${(Number(dur) * 1.15 + (idx % 3) * 0.55).toFixed(2)}s ease-in-out ${delay} infinite`
                  : undefined,
              }}
            />
          );
        })}
      </div>
    );
  }

  if (el.effect === 'prism-beams') {
    return (
      <div className="relative h-full w-full overflow-hidden">
        <div
          className="absolute -inset-y-[25%] left-[12%] w-[26%]"
          style={{
            background: `linear-gradient(90deg, transparent 0%, ${c1}55 42%, ${c2}66 58%, transparent 100%)`,
            transform: 'skewX(-22deg)',
            animation: playMotion ? `teBeamSweep ${(Number(dur) * 1.35).toFixed(2)}s ease-in-out infinite` : undefined,
          }}
        />
        <div
          className="absolute -inset-y-[25%] left-[52%] w-[16%]"
          style={{
            background: `linear-gradient(90deg, transparent 0%, ${c2}44 50%, transparent 100%)`,
            transform: 'skewX(-22deg)',
            animation: playMotion ? `teBeamSweep ${(Number(dur) * 1.35).toFixed(2)}s ease-in-out 0.9s infinite` : undefined,
          }}
        />
      </div>
    );
  }

  // particles / snow / float
  const isSnow = el.effect === 'snow';
  const animName = isSnow ? 'teSnowFall' : 'teFloatUp';
  return (
    <div className="relative h-full w-full overflow-hidden">
      {Array.from({ length: count }, (_, idx) => {
        const left = `${((idx * 37 + 11) % 92) + 4}%`;
        const top = `${((idx * 53 + 7) % 86) + 6}%`;
        const sizeCq = isSnow ? 1.5 + (idx % 3) * 0.8 : 1.4 + (idx % 3) * 1.1;
        const delay = `${((idx * 0.43) % 2.8).toFixed(2)}s`;
        return (
          <span
            key={idx}
            className="absolute rounded-full"
            style={{
              left,
              top: isSnow ? '-10%' : top,
              width: `${sizeCq}cqmin`,
              height: `${sizeCq}cqmin`,
              backgroundColor: idx % 2 === 0 ? c1 : c2,
              boxShadow: `0 0 ${sizeCq * 1.4}cqmin ${c1}`,
              animation: playMotion ? `${animName} ${(Number(dur) + (idx % 3) * 0.7).toFixed(2)}s linear ${delay} infinite` : undefined,
            }}
          />
        );
      })}
    </div>
  );
};

const WidgetNode: React.FC<{ el: WidgetElement; tickPhase: number; now: Date }> = ({
  el,
  tickPhase,
  now,
}) => {
  const accent = el.accentColor || '#2563EB';
  const bgAlpha = Math.min(0.94, Math.max(0.25, (el.bgOpacity ?? 78) / 100));
  const boxStyle: React.CSSProperties = {
    backgroundColor: `rgba(15, 23, 42, ${bgAlpha})`,
    borderColor: `${accent}55`,
    boxShadow: buildBoxShadowCss(el.shadow),
  };

  if (el.widgetType === 'progress') {
    const val = Math.max(0, Math.min(100, el.value ?? 72));
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-center rounded-[2.6cqmin] border px-[3.2cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-center justify-between" style={{ fontSize: '3.2cqmin' }}>
          <span className="font-semibold text-white/90">{el.label || '今日专注进度'}</span>
          <span className="font-mono-tabular font-bold" style={{ color: accent }}>
            {val}%
          </span>
        </div>
        <div className="mt-[1.6cqmin] h-[2.2cqmin] w-full overflow-hidden rounded-full bg-white/15">
          <div className="h-full rounded-full transition-all" style={{ width: `${val}%`, backgroundColor: accent }} />
        </div>
      </div>
    );
  }

  if (el.widgetType === 'ring-progress') {
    const val = Math.max(0, Math.min(100, el.value ?? 78));
    const dash = 2 * Math.PI * 36;
    const offset = dash * (1 - val / 100);
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col items-center justify-center rounded-[3cqmin] border p-[2cqmin] text-white backdrop-blur-md"
      >
        <svg viewBox="0 0 90 90" className="h-[68%] w-[68%]">
          <circle cx="45" cy="45" r="36" fill="none" stroke="rgba(255,255,255,0.14)" strokeWidth="8" />
          <circle
            cx="45"
            cy="45"
            r="36"
            fill="none"
            stroke={accent}
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray={dash}
            strokeDashoffset={offset}
            transform="rotate(-90 45 45)"
          />
          <text
            x="45"
            y="47"
            textAnchor="middle"
            dominantBaseline="central"
            fill="#fff"
            fontSize="18"
            fontWeight="700"
          >
            {val}%
          </text>
        </svg>
        <div className="truncate font-semibold text-white/80" style={{ fontSize: '2.6cqmin' }}>
          {el.label || '达成率'}
        </div>
      </div>
    );
  }

  if (el.widgetType === 'countdown') {
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full items-center justify-between rounded-[2.6cqmin] border px-[3.2cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div className="min-w-0">
          <div className="truncate text-white/70" style={{ fontSize: '2.8cqmin' }}>
            {el.label || '里程碑倒计时'}
          </div>
          <div className="font-mono-tabular font-bold" style={{ fontSize: '5.4cqmin', color: accent }}>
            {el.targetLabel || '12 天'}
          </div>
        </div>
        <Timer className="h-[6.2cqmin] w-[6.2cqmin] shrink-0" style={{ color: accent }} />
      </div>
    );
  }

  if (el.widgetType === 'date-card') {
    const weeks = ['周日', '周一', '周二', '周三', '周四', '周五', '周六'];
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col items-center justify-center rounded-[2.6cqmin] border p-[2cqmin] text-white backdrop-blur-md"
      >
        <span className="font-semibold uppercase tracking-wider" style={{ fontSize: '2.6cqmin', color: accent }}>
          {now.getFullYear()}年{now.getMonth() + 1}月
        </span>
        <span className="font-mono-tabular font-extrabold leading-none" style={{ fontSize: '9cqmin' }}>
          {pad2(now.getDate())}
        </span>
        <span className="text-white/75" style={{ fontSize: '2.8cqmin' }}>
          {weeks[now.getDay()]}
        </span>
      </div>
    );
  }

  if (el.widgetType === 'weather') {
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full items-center gap-[2.4cqmin] rounded-[2.6cqmin] border px-[3cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <CloudSun className="h-[7.5cqmin] w-[7.5cqmin] shrink-0" style={{ color: accent }} />
        <div className="min-w-0">
          <div className="flex items-baseline gap-[1.5cqmin]">
            <span className="truncate font-bold" style={{ fontSize: '3.6cqmin' }}>
              {el.city || '上海'}
            </span>
            <span className="font-mono-tabular font-bold" style={{ fontSize: '4.4cqmin', color: accent }}>
              22°C
            </span>
          </div>
          <div className="truncate text-white/75" style={{ fontSize: '2.7cqmin' }}>
            晴转多云 · 空气优 32
          </div>
        </div>
      </div>
    );
  }

  if (el.widgetType === 'audio-spectrum') {
    const barTotal = Math.max(8, Math.min(24, el.barCount ?? 16));
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-between rounded-[2.6cqmin] border px-[2.8cqmin] py-[1.8cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-center gap-[1.2cqmin] text-white/85" style={{ fontSize: '2.7cqmin' }}>
          <Music2 className="h-[3.2cqmin] w-[3.2cqmin]" style={{ color: accent }} />
          <span className="truncate font-semibold">{el.label || '音频频谱律动'}</span>
        </div>
        <div className="flex flex-1 items-end gap-[0.8cqmin] pt-[1.2cqmin]">
          {Array.from({ length: barTotal }, (_, i) => {
            const pct = Math.max(20, Math.round(Math.abs(Math.sin((i + 1) * 0.65 + tickPhase * 0.7)) * 100));
            return (
              <span
                key={i}
                className="flex-1 rounded-full transition-all duration-200"
                style={{ height: `${pct}%`, backgroundColor: accent, opacity: 0.6 + (pct / 100) * 0.4 }}
              />
            );
          })}
        </div>
      </div>
    );
  }

  if (el.widgetType === 'mini-chart') {
    const vals = [38, 62, 54, 84, 71, 92, 66];
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-between rounded-[2.6cqmin] border px-[2.8cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-center justify-between" style={{ fontSize: '2.7cqmin' }}>
          <span className="flex items-center gap-[1cqmin] font-semibold text-white/85">
            <BarChart3 className="h-[3.2cqmin] w-[3.2cqmin]" style={{ color: accent }} />
            {el.label || '7日趋势'}
          </span>
          <span className="font-mono-tabular font-bold" style={{ color: accent }}>
            +14%
          </span>
        </div>
        <div className="flex flex-1 items-end gap-[1.2cqmin] pt-[1.4cqmin]">
          {vals.map((v, i) => (
            <div key={i} className="flex h-full flex-1 items-end rounded-sm bg-white/10">
              <div
                className="w-full rounded-sm"
                style={{ height: `${v}%`, backgroundColor: i === 5 ? accent : `${accent}99` }}
              />
            </div>
          ))}
        </div>
      </div>
    );
  }

  if (el.widgetType === 'clock-widget') {
    const rawHours = now.getHours();
    const use24 = el.use24Hour !== false;
    const displayHours = use24 ? rawHours : rawHours % 12 || 12;
    const ampm = rawHours >= 12 ? 'PM' : 'AM';
    const timeMain = `${pad2(displayHours)}:${pad2(now.getMinutes())}`;
    const secondsStr = pad2(now.getSeconds());
    const weeks = ['周日', '周一', '周二', '周三', '周四', '周五', '周六'];
    const dateLine = `${pad2(now.getMonth() + 1)}月${pad2(now.getDate())}日 · ${weeks[now.getDay()]}`;
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-center rounded-[2.6cqmin] border px-[3.2cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-baseline gap-[1cqmin]">
          <span className="font-mono-tabular font-bold tracking-tight" style={{ fontSize: '5.8cqmin' }}>
            {timeMain}
          </span>
          {el.showSeconds !== false && (
            <span className="font-mono-tabular font-bold" style={{ fontSize: '3.6cqmin', color: accent }}>
              :{secondsStr}
            </span>
          )}
          {!use24 && (
            <span className="font-semibold text-white/70" style={{ fontSize: '2.5cqmin' }}>
              {ampm}
            </span>
          )}
        </div>
        {el.showDate !== false && (
          <div className="mt-[0.8cqmin] truncate font-mono-tabular font-medium text-white/75" style={{ fontSize: '2.5cqmin' }}>
            {dateLine}
          </div>
        )}
      </div>
    );
  }

  if (el.widgetType === 'telemetry-bar') {
    const step = Math.max(1, el.refreshSeconds ?? 2);
    const phase = Math.floor(tickPhase / step);
    const cpuLoad = 28 + ((phase * 7) % 19);
    const gpuLoad = 41 + ((phase * 11) % 23);
    const cpuTemp = 48 + (phase % 5);
    const memPct = 62;
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-center gap-[1.3cqmin] rounded-[2.6cqmin] border px-[3cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-center justify-between gap-[1.5cqmin]" style={{ fontSize: '2.6cqmin' }}>
          <span className="flex items-center gap-[1cqmin] font-semibold text-white/90">
            <Cpu className="h-[3.1cqmin] w-[3.1cqmin]" style={{ color: accent }} />
            <span className="truncate">{el.label || '性能监控'}</span>
          </span>
          {el.showTemp !== false && <span className="font-mono-tabular text-white/75">{cpuTemp}°C</span>}
        </div>
        <div className="flex items-center gap-[1.5cqmin]" style={{ fontSize: '2.4cqmin' }}>
          <span className="w-[5.8cqmin] shrink-0 font-mono-tabular text-white/70">CPU</span>
          <div className="h-[1.5cqmin] flex-1 overflow-hidden rounded-full bg-white/15">
            <div className="h-full rounded-full transition-all duration-300" style={{ width: `${cpuLoad}%`, backgroundColor: accent }} />
          </div>
          <span className="w-[6.5cqmin] text-right font-mono-tabular font-semibold">{cpuLoad}%</span>
        </div>
        <div className="flex items-center gap-[1.5cqmin]" style={{ fontSize: '2.4cqmin' }}>
          <span className="w-[5.8cqmin] shrink-0 font-mono-tabular text-white/70">GPU</span>
          <div className="h-[1.5cqmin] flex-1 overflow-hidden rounded-full bg-white/15">
            <div className="h-full rounded-full transition-all duration-300" style={{ width: `${gpuLoad}%`, backgroundColor: accent }} />
          </div>
          <span className="w-[6.5cqmin] text-right font-mono-tabular font-semibold">{gpuLoad}%</span>
        </div>
        {el.showMemory !== false && (
          <div className="flex items-center justify-between pt-[0.3cqmin] font-mono-tabular text-white/70" style={{ fontSize: '2.2cqmin' }}>
            <span>内存占用</span>
            <span>{memPct}% · 19.8/32G</span>
          </div>
        )}
      </div>
    );
  }

  if (el.widgetType === 'audio-rhythm') {
    const barTotal = Math.max(8, Math.min(28, el.barCount ?? 20));
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-between rounded-[2.6cqmin] border px-[2.8cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        {el.showTrackName !== false && (
          <div className="flex items-center gap-[1.2cqmin] text-white/85" style={{ fontSize: '2.6cqmin' }}>
            <Music2 className="h-[3.1cqmin] w-[3.1cqmin] shrink-0" style={{ color: accent }} />
            <span className="truncate font-semibold">{el.trackName || el.label || 'Midnight Lo-Fi · 小屏宝PixBuddy'}</span>
          </div>
        )}
        <div className="flex flex-1 items-end gap-[0.65cqmin] pt-[1.1cqmin]">
          {Array.from({ length: barTotal }, (_, i) => {
            const pct = Math.max(22, Math.round(Math.abs(Math.sin((i + 1) * 0.68 + tickPhase * 0.85)) * 100));
            return (
              <span
                key={i}
                className="flex-1 rounded-full transition-all duration-200"
                style={{ height: `${pct}%`, backgroundColor: accent, opacity: 0.65 + (pct / 100) * 0.35 }}
              />
            );
          })}
        </div>
      </div>
    );
  }

  if (el.widgetType === 'pomodoro-widget') {
    const mins = Math.max(1, el.focusMinutes ?? 25);
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full items-center gap-[2.4cqmin] rounded-[2.6cqmin] border px-[3cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div
          className="flex h-[8.5cqmin] w-[8.5cqmin] shrink-0 items-center justify-center rounded-[2cqmin]"
          style={{ backgroundColor: `${accent}28`, color: accent }}
        >
          <Timer className="h-[4.6cqmin] w-[4.6cqmin]" />
        </div>
        <div className="min-w-0">
          <div className="flex items-center gap-[1cqmin] text-white/75" style={{ fontSize: '2.4cqmin' }}>
            <span className="truncate">{el.label || '专注计时'}</span>
            <span aria-hidden="true">·</span>
            <span style={{ color: accent }}>就绪</span>
          </div>
          <div className="font-mono-tabular font-bold leading-tight" style={{ fontSize: '5.2cqmin' }}>
            {pad2(mins)}:00
          </div>
        </div>
      </div>
    );
  }

  if (el.widgetType === 'weather-capsule') {
    const city = (el.city ?? '上海').trim() || '上海';
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full items-center gap-[2.4cqmin] rounded-[2.6cqmin] border px-[3cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <CloudSun className="h-[7.2cqmin] w-[7.2cqmin] shrink-0" style={{ color: accent }} />
        <div className="min-w-0">
          <div className="flex items-baseline gap-[1.4cqmin]">
            <span className="truncate font-bold" style={{ fontSize: '3.4cqmin' }}>
              {city}
            </span>
            <span className="font-mono-tabular font-bold" style={{ fontSize: '4.2cqmin', color: accent }}>
              22°C
            </span>
          </div>
          <div className="truncate text-white/75" style={{ fontSize: '2.5cqmin' }}>
            多云转晴{el.showAqi !== false ? ' · 空气优 32' : ''}
          </div>
        </div>
      </div>
    );
  }

  if (el.widgetType === 'sticky-note') {
    const note = (el.noteText ?? '完成 D92 视觉方案评审 · 19:00').trim() || '保持专注';
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-center rounded-[2.6cqmin] border px-[3cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-center gap-[1.2cqmin] font-semibold text-white/80" style={{ fontSize: '2.5cqmin' }}>
          <StickyNote className="h-[3.1cqmin] w-[3.1cqmin] shrink-0" style={{ color: accent }} />
          <span className="truncate">{el.label || '桌面便签'}</span>
        </div>
        <div className="mt-[1cqmin] line-clamp-2 font-medium leading-snug text-white/95" style={{ fontSize: '2.9cqmin' }}>
          {note}
        </div>
      </div>
    );
  }

  // sysmon default
  const cpu = 28 + ((tickPhase * 7) % 19);
  const gpu = 42 + ((tickPhase * 11) % 24);
  const ram = 62;
  return (
    <div
      style={boxStyle}
      className="flex h-full w-full flex-col justify-center gap-[1.6cqmin] rounded-[2.6cqmin] border px-[3cqmin] py-[2.2cqmin] text-white backdrop-blur-md"
    >
      <div className="flex items-center justify-between" style={{ fontSize: '2.8cqmin' }}>
        <span className="flex items-center gap-[1cqmin] font-semibold">
          <Cpu className="h-[3.2cqmin] w-[3.2cqmin]" style={{ color: accent }} />
          硬件监测
        </span>
        <span className="font-mono-tabular text-white/75">49°C</span>
      </div>
      {[
        { k: 'CPU', v: cpu },
        { k: 'GPU', v: gpu },
        { k: 'RAM', v: ram },
      ].map((row) => (
        <div key={row.k} className="flex items-center gap-[1.6cqmin]" style={{ fontSize: '2.5cqmin' }}>
          <span className="w-[6cqmin] font-mono-tabular text-white/70">{row.k}</span>
          <div className="h-[1.5cqmin] flex-1 overflow-hidden rounded-full bg-white/15">
            <div className="h-full rounded-full transition-all" style={{ width: `${row.v}%`, backgroundColor: accent }} />
          </div>
          <span className="w-[7cqmin] text-right font-mono-tabular font-semibold">{row.v}%</span>
        </div>
      ))}
    </div>
  );
};

const PluginNode: React.FC<{ el: PluginElement; tickPhase: number }> = ({ el, tickPhase }) => {
  const cap = PLUGIN_CAPABILITY_REGISTRY.find((c) => c.key === el.capabilityKey);
  const supported = cap ? cap.supportedInRuntime : true;
  const accent = el.accentColor || '#2563EB';
  const bgAlpha = Math.min(0.95, Math.max(0.3, (el.bgOpacity ?? 80) / 100));
  const boxStyle: React.CSSProperties = {
    backgroundColor: `rgba(15, 23, 42, ${bgAlpha})`,
    borderColor: supported ? `${accent}66` : 'rgba(245, 158, 11, 0.7)',
    boxShadow: buildBoxShadowCss(el.shadow),
  };

  if (!supported) {
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col items-center justify-center rounded-[2.6cqmin] border border-dashed p-[2.2cqmin] text-center text-amber-200 backdrop-blur-md"
      >
        <AlertTriangle className="h-[5.5cqmin] w-[5.5cqmin] text-amber-400" />
        <div className="mt-[1cqmin] font-bold" style={{ fontSize: '2.9cqmin' }}>
          {el.pluginName} · 能力不兼容
        </div>
        <div className="mt-[0.4cqmin] line-clamp-2 text-amber-100/75" style={{ fontSize: '2.2cqmin' }}>
          需客户端 v{cap?.minClientVersion}（已安全拦截，不会静默崩溃）
        </div>
      </div>
    );
  }

  if (el.capabilityKey === 'music-info') {
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full items-center gap-[2.4cqmin] rounded-[2.6cqmin] border px-[3cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div
          className="flex h-[9cqmin] w-[9cqmin] shrink-0 items-center justify-center rounded-[2cqmin]"
          style={{ backgroundColor: `${accent}33`, color: accent }}
        >
          <Music2 className="h-[4.8cqmin] w-[4.8cqmin]" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate font-bold" style={{ fontSize: '3.4cqmin' }}>
            {String(el.config.track || 'Midnight Lo-Fi')}
          </div>
          <div className="truncate text-white/70" style={{ fontSize: '2.5cqmin' }}>
            {String(el.config.artist || 'Studio Acoustics')}
          </div>
          <div className="mt-[1.2cqmin] h-[1.2cqmin] w-full overflow-hidden rounded-full bg-white/15">
            <div
              className="h-full rounded-full"
              style={{ width: `${30 + ((tickPhase * 2) % 65)}%`, backgroundColor: accent }}
            />
          </div>
        </div>
      </div>
    );
  }

  if (el.capabilityKey === 'lyrics-sync') {
    return (
      <div
        style={boxStyle}
        className="h-full w-full overflow-hidden rounded-[2.6cqmin] border"
      >
        <CraftLyricsWidget
          songTitle={String(el.config.songTitle || 'Midnight City')}
          artist={String(el.config.artist || 'M83')}
          spectrumBars={Number(el.config.spectrumBars) || 20}
          glowColor={accent}
          compact={true}
          interactive={false}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'quick-notes') {
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-start rounded-[2.6cqmin] border px-[2.8cqmin] py-[2.2cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-center gap-[1.2cqmin] font-bold" style={{ fontSize: '2.9cqmin', color: accent }}>
          <StickyNote className="h-[3.2cqmin] w-[3.2cqmin]" />
          <span>{String(el.config.title || '电子记事便签')}</span>
        </div>
        <div
          className="mt-[1.2cqmin] whitespace-pre-line leading-snug text-white/85"
          style={{ fontSize: '2.6cqmin' }}
        >
          {String(el.config.content || '1. 保持桌搭整洁\n2. 专注关键目标')}
        </div>
      </div>
    );
  }

  if (el.capabilityKey === 'stock-ticker') {
    return (
      <div
        style={boxStyle}
        className="h-full w-full overflow-hidden rounded-[2.6cqmin] border"
      >
        <CraftStocksWidget
          selectedSymbol={String(el.config.symbol || 'NVDA')}
          accentColor={accent}
          compact={true}
          interactive={false}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'todo-list') {
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col justify-center gap-[1cqmin] rounded-[2.6cqmin] border px-[2.8cqmin] py-[2cqmin] text-white backdrop-blur-md"
      >
        <div className="flex items-center gap-[1.2cqmin] font-bold" style={{ fontSize: '2.8cqmin', color: accent }}>
          <CheckSquare className="h-[3.2cqmin] w-[3.2cqmin]" />
          <span>今日待办</span>
        </div>
        {[el.config.item1, el.config.item2, el.config.item3].filter(Boolean).map((it, idx) => (
          <div key={idx} className="flex items-center gap-[1.2cqmin] truncate text-white/85" style={{ fontSize: '2.5cqmin' }}>
            <span className="h-[1.6cqmin] w-[1.6cqmin] rounded-xs border border-white/50 shrink-0" />
            <span className="truncate">{String(it)}</span>
          </div>
        ))}
      </div>
    );
  }

  if (el.capabilityKey === 'desktop-aquarium') {
    return (
      <div
        style={boxStyle}
        className="h-full w-full overflow-hidden rounded-[2.6cqmin] border"
      >
        <CraftAquariumWidget
          fishCount={Number(el.config.fishCount) || 7}
          causticsIntensity={Number(el.config.causticsIntensity) || 0.6}
          bubbleSpeed={Number(el.config.bubbleSpeed) || 1.0}
          showClock={el.config.showClock !== false}
          compact={true}
          interactive={false}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'nixie-clock') {
    return (
      <div
        style={boxStyle}
        className="h-full w-full overflow-hidden rounded-[2.6cqmin] border"
      >
        <CraftNixieClockWidget
          colorHue={(String(el.config.colorHue || 'amber') as NixieColorHue) || 'amber'}
          showSeconds={el.config.showSeconds !== false}
          showDate={el.config.showDate !== false}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'cyber-pet') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftCyberPetWidget
          petName={String(el.config.petName || 'Mochi')}
          species={(String(el.config.petSpecies || 'mochi') as CyberPetSpecies) || 'mochi'}
          mood={(String(el.config.mood || 'happy') as CyberPetMood) || 'happy'}
          accentColor={accent}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'campfire') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftCampfireWidget
          flameIntensity={(String(el.config.flameIntensity || 'medium') as 'low' | 'medium' | 'high') || 'medium'}
          emberDensity={(String(el.config.emberDensity || 'medium') as 'low' | 'medium' | 'high') || 'medium'}
          showSoundHint={el.config.showSoundHint !== false}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'zen-bonsai') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftBonsaiWidget
          species={(String(el.config.bonsaiSpecies || 'pine') as BonsaiSpecies) || 'pine'}
          growthDays={Number(el.config.growthDays) || 28}
          sunlightTone={(String(el.config.sunlightTone || 'warm') as BonsaiSunlight) || 'warm'}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'flip-clock') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftFlipClockWidget
          shellStyle={(String(el.config.flipShellStyle || 'obsidian') as FlipShellStyle) || 'obsidian'}
          showSeconds={el.config.showSeconds !== false}
          showDate={el.config.showDate !== false}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'vu-meter') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftVuMeterWidget
          backlightHue={(String(el.config.vuBacklight || 'warm-amber') as VuBacklightHue) || 'warm-amber'}
          sensitivity={(String(el.config.vuSensitivity || 'balanced') as VuSensitivity) || 'balanced'}
          deckLabel={String(el.config.deckLabel || 'MASTER STEREO')}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'cassette-deck') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftCassetteWidget
          trackTitle={String(el.config.songTitle || 'City Pop Tape #04')}
          artistLabel={String(el.config.artist || 'SIDE A · 90MIN')}
          shellColor={(String(el.config.cassetteShell || 'smoke') as CassetteShellColor) || 'smoke'}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'zodiac-oracle') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftZodiacWidget
          sign={(String(el.config.zodiacSign || 'aquarius') as ZodiacSignKey) || 'aquarius'}
          showConstellationGlow={el.config.showConstellationGlow !== false}
          accentColor={accent}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'parcel-tracker') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftParcelWidget
          parcelName={String(el.config.parcelName || '客制化机械键盘套件')}
          carrier={String(el.config.carrier || '顺丰速运')}
          stage={(String(el.config.parcelStage || 'delivering') as ParcelStage) || 'delivering'}
          accentColor={accent}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'milestone-days') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftCountdownWidget
          milestoneTitle={String(el.config.milestoneTitle || '年度版本发布')}
          targetDays={Number(el.config.targetDays ?? 18)}
          countMode={(String(el.config.countMode || 'countdown') as MilestoneMode) || 'countdown'}
          accentColor={accent}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'net-radar') {
    return (
      <div style={boxStyle} className="h-full w-full overflow-hidden rounded-[2.6cqmin] border">
        <CraftNetSpeedWidget
          unitMode={(String(el.config.unitMode || 'Mbps') as NetUnitMode) || 'Mbps'}
          refreshSeconds={Number(el.config.refreshSeconds) || 2}
          showPingChart={el.config.showPingChart !== false}
          accentColor={accent}
          compact={true}
        />
      </div>
    );
  }

  if (el.capabilityKey === 'photo-album') {
    return (
      <div
        style={boxStyle}
        className="flex h-full w-full flex-col items-center justify-center rounded-[2.6cqmin] border p-[2cqmin] text-white backdrop-blur-md"
      >
        <ImageIcon className="h-[6.5cqmin] w-[6.5cqmin]" style={{ color: accent }} />
        <div className="mt-[1cqmin] truncate font-semibold" style={{ fontSize: '2.8cqmin' }}>
          {String(el.config.caption || '电子相册')}
        </div>
      </div>
    );
  }

  // pomodoro / countdown-timer / system-monitor fallback
  return (
    <div
      style={boxStyle}
      className="flex h-full w-full items-center gap-[2.2cqmin] rounded-[2.6cqmin] border px-[3cqmin] py-[2cqmin] text-white backdrop-blur-md"
    >
      <Timer className="h-[6cqmin] w-[6cqmin] shrink-0" style={{ color: accent }} />
      <div className="min-w-0">
        <div className="truncate text-white/75" style={{ fontSize: '2.6cqmin' }}>
          {el.pluginName}
        </div>
        <div className="font-mono-tabular font-bold" style={{ fontSize: '4.8cqmin' }}>
          {el.capabilityKey === 'pomodoro'
            ? `${String(el.config.focusMinutes || 25)}:00`
            : el.capabilityKey === 'countdown-timer'
              ? `${String(el.config.minutes || 45)}:00`
              : 'CPU 32% · 49°C'}
        </div>
      </div>
    </div>
  );
};

interface EditorArtworkRendererProps {
  paused?: boolean;
  project: EditorThemeProject;
  layoutIndex?: number;
  liveTick?: boolean;
  playMotion?: boolean;
  interactive?: boolean;
  locks?: Record<string, boolean>;
  editingTextId?: string | null;
  className?: string;
  onPointerDownElement?: (e: React.PointerEvent, id: string) => void;
  onDoubleClickElement?: (e: React.MouseEvent, id: string) => void;
  onChangeEditingText?: (id: string, nextText: string) => void;
  onFinishEditingText?: () => void;
}

export const EditorArtworkRenderer: React.FC<EditorArtworkRendererProps> = ({
  project,
  layoutIndex = 0,
  liveTick = true,
  playMotion = true,
  interactive = false,
  locks = {},
  editingTextId = null,
  className = '',
  onPointerDownElement,
  onDoubleClickElement,
  onChangeEditingText,
  onFinishEditingText,
  paused = false,
}) => {
  const [now, setNow] = useState(() => new Date());
  const [tickPhase, setTickPhase] = useState(1);

  useEffect(() => {
    if (paused || (!liveTick && !playMotion)) return;
    const timer = window.setInterval(() => {
      setNow(new Date());
      setTickPhase((p) => (p + 1) % 600);
    }, 1000);
    return () => window.clearInterval(timer);
  }, [liveTick, playMotion, paused]);

  const layout = project.layouts[layoutIndex] ?? project.layouts[0];
  const size = layout?.size ?? [1920, 462];
  const portrait = size[0] < size[1];
  const elements = getLayoutElements(project, layoutIndex).filter(
    (el) => !isElementHidden(project, layoutIndex, el)
  );
  const bg = project.bg;

  const bgStyleObj = BUILTIN_BG_STYLES.find((s) => s.id === (bg.style || 'ink'));
  const bgTransform = `translate(${bg.tx || 0}%, ${bg.ty || 0}%) rotate(${bg.rot || 0}deg) scale(${(
    (bg.sc || 1) * (bg.blur ? 1.12 : 1)
  ).toFixed(4)})`;
  const bgFilters: string[] = [];
  if (bg.blur) bgFilters.push(`blur(${bg.blur}cqmin)`);
  if (bg.brightness != null && bg.brightness !== 100) bgFilters.push(`brightness(${bg.brightness}%)`);
  if (bg.contrast != null && bg.contrast !== 100) bgFilters.push(`contrast(${bg.contrast}%)`);
  if (bg.saturate != null && bg.saturate !== 100) bgFilters.push(`saturate(${bg.saturate}%)`);
  const bgFilter = bgFilters.length > 0 ? bgFilters.join(' ') : undefined;

  return (
    <div
      className={`te-canvas-container relative h-full w-full overflow-hidden select-none ${
        bg.hide ? 'te-checkerboard' : 'bg-[#0E1726]'
      } ${className}`}
    >
      {/* Background Layer */}
      {!bg.hide && (
        <div
          className="pointer-events-none absolute inset-0 overflow-hidden"
          style={{ transform: bgTransform, filter: bgFilter }}
        >
          {bg.kind === 'color' && (
            <div className="h-full w-full" style={{ backgroundColor: bg.color || '#1b2230' }} />
          )}
          {bg.kind === 'gradient' && (
            <div
              className="h-full w-full"
              style={{
                background: `linear-gradient(${bg.angle ?? 135}deg, ${bg.g1 || '#2468e8'}, ${
                  bg.g2 || '#0b97c2'
                })`,
              }}
            />
          )}
          {bg.kind === 'video' && (
            <>
              {playMotion && bg.vurl ? (
                <video
                  src={bg.vurl}
                  muted
                  loop
                  autoPlay
                  playsInline
                  className="h-full w-full"
                  style={{
                    objectFit:
                      bg.fit === 'contain' ? 'contain' : bg.fit === 'stretch' ? 'fill' : 'cover',
                  }}
                />
              ) : (
                <div
                  className="h-full w-full bg-black bg-center bg-no-repeat"
                  style={{
                    backgroundImage: bg.poster ? `url('${bg.poster}')` : undefined,
                    backgroundSize:
                      bg.fit === 'contain' ? 'contain' : bg.fit === 'stretch' ? '100% 100%' : 'cover',
                  }}
                />
              )}
            </>
          )}
          {(bg.kind === 'style' || bg.kind === 'image') && (
            <div
              className="h-full w-full bg-center bg-no-repeat"
              style={{
                background: bg.url ? undefined : bgStyleObj?.css,
                backgroundImage: bg.url ? `url('${bg.url}')` : bgStyleObj?.css,
                backgroundSize:
                  bg.url
                    ? bg.fit === 'contain'
                      ? 'contain'
                      : bg.fit === 'stretch'
                        ? '100% 100%'
                        : 'cover'
                    : undefined,
              }}
            >
              {!bg.url && bg.style === 'grid-tech' && (
                <div
                  className="h-full w-full opacity-35"
                  style={{
                    backgroundImage:
                      'linear-gradient(rgba(148,163,184,0.22) 1px, transparent 1px), linear-gradient(90deg, rgba(148,163,184,0.22) 1px, transparent 1px)',
                    backgroundSize: '24px 24px',
                  }}
                />
              )}
            </div>
          )}
        </div>
      )}

      {/* Dim Scrim */}
      {!bg.hide && bg.dim > 0 && (
        <div
          className="pointer-events-none absolute inset-0"
          style={{ backgroundColor: `rgba(0,0,0,${bg.dim / 100})` }}
        />
      )}

      {/* Elements Layer */}
      {elements.map((el) => {
        const [fx, fy] = resolveAnchorPercentages(el.a);
        const hz = el.a[1] === 'l' ? 'l' : el.a[1] === 'r' ? 'r' : 'c';
        const locked = Boolean(locks[el.id]);
        const fontCss = el.font && FONT_CSS_MAP[el.font] ? FONT_CSS_MAP[el.font] : undefined;
        const posStyle: React.CSSProperties = {
          position: 'absolute',
          left: `calc(${fx}cqw + ${el.x}cqw)`,
          top: `calc(${fy}cqh + ${el.y}cqh)`,
          transform: `translate(-${fx}%, -${fy}%) rotate(${el.rot || 0}deg) scale(${el.sx || 1}, ${
            el.sy || 1
          })`,
          opacity: el.alpha,
          color: el.color,
          pointerEvents: interactive ? 'auto' : 'none',
          cursor: interactive ? (locked ? 'not-allowed' : 'move') : 'default',
        };

        const handlers = interactive
          ? {
              onPointerDown: (e: React.PointerEvent) => onPointerDownElement?.(e, el.id),
              onDoubleClick: (e: React.MouseEvent) => onDoubleClickElement?.(e, el.id),
            }
          : {};

        if (el.kind === 'clock') {
          const { h, m, s } = getTimeParts(el.tz, liveTick, now);
          const ap = h >= 12 ? 'PM' : 'AM';
          const hh = el.h24 ? pad2(h) : String(h % 12 || 12);
          const mm = pad2(m);
          const ss = pad2(s);
          const wt = el.style === 'minimal' ? 300 : el.weight;
          const ls = el.ls != null ? `${el.ls / 100}em` : el.style === 'minimal' ? '0.14em' : '0';

          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                fontSize: `${el.size}cqmin`,
                fontFamily: fontCss,
                fontWeight: wt,
                fontVariantNumeric: 'tabular-nums',
                letterSpacing: ls,
                lineHeight: 1.02,
                whiteSpace: 'nowrap',
                ...(el.style === 'analog' ? {} : getTextEffectStyle(el)),
              }}
              {...handlers}
            >
              {el.style === 'analog' ? (
                <AnalogClockSvg el={el} live={liveTick} now={now} />
              ) : el.part === 'hour' ? (
                <span className="font-mono-tabular">{hh}</span>
              ) : el.part === 'minute' ? (
                <span className="font-mono-tabular">{mm}</span>
              ) : el.part === 'second' ? (
                <span className="font-mono-tabular">{ss}</span>
              ) : el.style === 'flip' ? (
                <FlipClock hours={hh} minutes={mm} seconds={ss}
                  showSeconds={el.sec && el.part !== 'hm'} period={el.h24 ? undefined : ap}
                  portrait={portrait} animate={liveTick && !paused} />
              ) : (
                <div className={`flex items-baseline gap-[0.15em] ${portrait ? 'flex-col items-center leading-none' : ''}`}>
                  <span>
                    {hh}
                    {!portrait && (
                      <span className={el.style === 'minimal' ? 'mx-[0.06em] opacity-55' : ''}>
                        {el.style === 'minimal' ? '·' : ':'}
                      </span>
                    )}
                    {portrait ? <br /> : null}
                    {mm}
                  </span>
                  {!el.h24 && <span className="text-[0.34em] font-semibold opacity-80">{ap}</span>}
                  {el.sec && el.part !== 'hm' && (
                    <span className="text-[0.45em] opacity-75">{ss}</span>
                  )}
                </div>
              )}
            </div>
          );
        }

        if (el.kind === 'date') {
          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                fontSize: `${el.size}cqmin`,
                fontFamily: fontCss,
                fontWeight: el.weight,
                letterSpacing: `${el.ls != null ? el.ls / 100 : 0.04}em`,
                whiteSpace: 'nowrap',
                writingMode: el.vert ? 'vertical-rl' : undefined,
                textOrientation: el.vert ? 'upright' : undefined,
                ...getTextEffectStyle(el),
              }}
              {...handlers}
            >
              {getDateText(el, liveTick, now)}
            </div>
          );
        }

        if (el.kind === 'text') {
          const textAlign = el.align || (hz === 'l' ? 'left' : hz === 'r' ? 'right' : 'center');
          const isEditing = interactive && editingTextId === el.id;
          const isVert = Boolean(el.vert);
          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                width: !isVert && el.w != null ? `${el.w}cqw` : undefined,
                height: isVert && el.h != null ? `${el.h}cqh` : undefined,
                maxWidth: 'none',
                fontSize: `${el.size}cqmin`,
                fontFamily: fontCss,
                fontWeight: el.weight,
                letterSpacing: el.ls != null ? `${el.ls / 100}em` : undefined,
                lineHeight: el.lh ?? 1.25,
                textAlign,
                whiteSpace: 'pre-wrap',
                wordBreak: el.w != null || el.h != null ? 'break-word' : undefined,
                overflowWrap: el.w != null || el.h != null ? 'anywhere' : undefined,
                writingMode: isVert ? 'vertical-rl' : undefined,
                textOrientation: isVert ? 'upright' : undefined,
                ...getTextEffectStyle(el),
              }}
              {...handlers}
            >
              {isEditing ? (
                <textarea
                  autoFocus
                  value={el.text}
                  onChange={(e) => onChangeEditingText?.(el.id, e.target.value)}
                  onBlur={() => onFinishEditingText?.()}
                  onKeyDown={(e) => {
                    if (e.key === 'Escape') {
                      e.preventDefault();
                      onFinishEditingText?.();
                    }
                    e.stopPropagation();
                  }}
                  onPointerDown={(e) => e.stopPropagation()}
                  className={`${
                    !isVert && el.w != null
                      ? 'w-full'
                      : isVert && el.h != null
                        ? 'h-full'
                        : 'min-w-[4em]'
                  } resize-none rounded border border-[#5E45F8] bg-black/55 px-1.5 py-0.5 text-inherit outline-none`}
                  rows={Math.max(1, el.text.split('\n').length)}
                />
              ) : (
                el.text || '文本'
              )}
            </div>
          );
        }

        if (el.kind === 'image') {
          const filters: string[] = [];
          if (el.blur) filters.push(`blur(${el.blur}cqmin)`);
          if (el.brightness != null && el.brightness !== 100) {
            filters.push(`brightness(${el.brightness}%)`);
          }
          if (el.contrast != null && el.contrast !== 100) {
            filters.push(`contrast(${el.contrast}%)`);
          }
          if (el.saturate != null && el.saturate !== 100) {
            filters.push(`saturate(${el.saturate}%)`);
          }
          const cropScale = el.cropScale ?? 1;
          const cropX = el.cropX ?? 0;
          const cropY = el.cropY ?? 0;
          const hasCrop = cropScale !== 1 || cropX !== 0 || cropY !== 0;
          const featherStyle = buildFeatherStyle(el.feather);
          const hasFeather = (el.feather?.strength ?? 0) > 0;
          const boxShadow = buildBoxShadowCss(el.shadow);
          const dropShadow = buildDropShadowCss(el.shadow);
          const hasBox = el.w != null && el.h != null;
          const radiusCss = el.radius ? `${el.radius}cqmin` : undefined;

          const mediaInner = el.src ? (
            <img
              src={el.src}
              alt={el.sourceMeta?.name || ''}
              draggable={false}
              className="block h-full w-full"
              style={{
                objectFit: el.fit || 'cover',
                transform: hasCrop
                  ? `translate(${cropX}%, ${cropY}%) scale(${cropScale})`
                  : undefined,
                transformOrigin: 'center center',
              }}
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center rounded-[12%] border border-dashed border-current text-[0.7em]">
              ◆
            </div>
          );

          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                height: hasBox ? `${el.h}cqh` : `${el.size}cqmin`,
                width: hasBox ? `${el.w}cqw` : `${(el.size * (el.ar || 1)).toFixed(3)}cqmin`,
                maxWidth: 'none',
                borderRadius: hasFeather ? undefined : radiusCss,
                filter: hasFeather
                  ? dropShadow
                  : filters.length > 0
                    ? filters.join(' ')
                    : undefined,
                boxShadow: hasFeather ? undefined : boxShadow,
                overflow: hasFeather ? undefined : 'hidden',
              }}
              {...handlers}
            >
              {hasFeather ? (
                <div
                  className="h-full w-full overflow-hidden"
                  style={{
                    borderRadius: radiusCss,
                    filter: filters.length > 0 ? filters.join(' ') : undefined,
                    ...featherStyle,
                  }}
                >
                  {mediaInner}
                </div>
              ) : (
                mediaInner
              )}
            </div>
          );
        }

        if (el.kind === 'video') {
          const hasBox = el.w != null && el.h != null;
          const videoFit = el.fit || 'cover';
          const filters: string[] = [];
          if (el.blur) filters.push(`blur(${el.blur}cqmin)`);
          if (el.brightness != null && el.brightness !== 100) {
            filters.push(`brightness(${el.brightness}%)`);
          }
          if (el.contrast != null && el.contrast !== 100) {
            filters.push(`contrast(${el.contrast}%)`);
          }
          if (el.saturate != null && el.saturate !== 100) {
            filters.push(`saturate(${el.saturate}%)`);
          }
          const cropScale = el.cropScale ?? 1;
          const cropX = el.cropX ?? 0;
          const cropY = el.cropY ?? 0;
          const hasCrop = cropScale !== 1 || cropX !== 0 || cropY !== 0;
          const featherStyle = buildFeatherStyle(el.feather);
          const hasFeather = (el.feather?.strength ?? 0) > 0;
          const boxShadow = buildBoxShadowCss(el.shadow);
          const dropShadow = buildDropShadowCss(el.shadow);
          const radiusCss = el.radius ? `${el.radius}cqmin` : undefined;

          const videoInner = (
            <>
              {playMotion && el.src ? (
                <video
                  src={el.src}
                  muted
                  loop
                  autoPlay
                  playsInline
                  className="pointer-events-none block h-full w-full"
                  style={{
                    objectFit: videoFit,
                    transform: hasCrop
                      ? `translate(${cropX}%, ${cropY}%) scale(${cropScale})`
                      : undefined,
                    transformOrigin: 'center center',
                  }}
                />
              ) : (
                <img
                  src={el.poster || ''}
                  alt=""
                  draggable={false}
                  className="block h-full w-full"
                  style={{
                    objectFit: videoFit,
                    transform: hasCrop
                      ? `translate(${cropX}%, ${cropY}%) scale(${cropScale})`
                      : undefined,
                    transformOrigin: 'center center',
                  }}
                />
              )}
              {interactive && !playMotion && (
                <span className="pointer-events-none absolute left-1.5 top-1.5 rounded bg-black/70 px-1.5 py-0.5 text-[10px] font-semibold text-white">
                  ▶ 视频
                </span>
              )}
            </>
          );

          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                height: hasBox ? `${el.h}cqh` : `${el.size}cqmin`,
                width: hasBox ? `${el.w}cqw` : `${(el.size * (el.ar || 1)).toFixed(3)}cqmin`,
                maxWidth: 'none',
                borderRadius: hasFeather ? undefined : radiusCss,
                filter: hasFeather
                  ? dropShadow
                  : filters.length > 0
                    ? filters.join(' ')
                    : undefined,
                boxShadow: hasFeather ? undefined : boxShadow,
                overflow: hasFeather ? undefined : 'hidden',
              }}
              {...handlers}
            >
              {hasFeather ? (
                <div
                  className="relative h-full w-full overflow-hidden"
                  style={{
                    borderRadius: radiusCss,
                    filter: filters.length > 0 ? filters.join(' ') : undefined,
                    ...featherStyle,
                  }}
                >
                  {videoInner}
                </div>
              ) : (
                videoInner
              )}
            </div>
          );
        }

        if (el.kind === 'shape') {
          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                width: `${el.w}cqw`,
                height: `${el.h}cqh`,
                maxWidth: 'none',
              }}
              {...handlers}
            >
              <ShapeNode el={el} />
            </div>
          );
        }

        if (el.kind === 'motion') {
          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                width: `${el.w}cqw`,
                height: `${el.h}cqh`,
                maxWidth: 'none',
              }}
              {...handlers}
            >
              <MotionNode el={el} playMotion={playMotion} />
            </div>
          );
        }

        if (el.kind === 'widget') {
          return (
            <div
              key={el.id}
              data-el-id={el.id}
              style={{
                ...posStyle,
                width: `${el.w}cqw`,
                height: `${el.h}cqh`,
                maxWidth: 'none',
              }}
              {...handlers}
            >
              <WidgetNode el={el} tickPhase={tickPhase} now={now} />
            </div>
          );
        }

        // plugin node
        return (
          <div
            key={el.id}
            data-el-id={el.id}
            style={{
              ...posStyle,
              width: `${el.w}cqw`,
              height: `${el.h}cqh`,
              maxWidth: 'none',
            }}
            {...handlers}
          >
            <PluginNode el={el} tickPhase={tickPhase} />
          </div>
        );
      })}
    </div>
  );
};
