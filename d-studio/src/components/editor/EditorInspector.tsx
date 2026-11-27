import React, { useEffect, useRef, useState } from 'react';
import {
  AlignCenterHorizontal,
  AlignCenterVertical,
  AlignEndHorizontal,
  AlignEndVertical,
  AlignHorizontalSpaceAround,
  AlignStartHorizontal,
  AlignStartVertical,
  AlignVerticalSpaceAround,
  ArrowDown,
  ArrowDownToLine,
  ArrowUp,
  ArrowUpToLine,
  BookmarkPlus,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardPaste,
  Copy,
  Edit3,
  Eye,
  EyeOff,
  Film,
  FlipHorizontal2,
  FlipVertical2,
  Group,
  Layers,
  Link2,
  Link2Off,
  Lock,
  Move,
  Paintbrush,
  RotateCcw,
  RotateCw,
  Trash2,
  Ungroup,
  Unlock,
  Upload,
  X,
} from 'lucide-react';
import {
  BUILTIN_BG_STYLES,
  EDITOR_FONTS,
  EDITOR_TIMEZONES,
  PLUGIN_CAPABILITY_REGISTRY,
  WIDGET_CATALOG,
  computeFit100BoxPct,
  computeNaturalBoxPct,
  readImageDimensions,
  readVideoDimensions,
} from '../../data/editorCatalog';
import {
  AnchorPoint,
  BgConfig,
  ClockElement,
  DateElement,
  DatePartKind,
  EditorElement,
  EditorElementBase,
  EditorThemeProject,
  ImageElement,
  ImageFeatherMode,
  MotionElement,
  PluginElement,
  ShapeElement,
  TextElement,
  VideoElement,
  WidgetElement,
} from '../../types/editor';
import { getLayoutElements, isElementHidden } from './EditorArtworkRenderer';

const ANCHORS: AnchorPoint[] = ['tl', 'tc', 'tr', 'ml', 'mc', 'mr', 'bl', 'bc', 'br'];
const QUICK_COLORS = [
  '#ffffff',
  '#1A172B',
  '#5E45F8',
  '#38BDF8',
  '#10B981',
  '#F59E0B',
  '#EF4444',
  '#F472B6',
];

export type InspectorAlignMode =
  | 'l'
  | 'hc'
  | 'r'
  | 't'
  | 'vc'
  | 'b'
  | 'c'
  | AnchorPoint;

const SLIDER_CLS =
  'h-1 w-0 min-w-0 flex-1 cursor-pointer appearance-none rounded-full bg-[#DFDCF0] accent-[#5E45F8] [&::-webkit-slider-thumb]:h-3.5 [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:border-[1.5px] [&::-webkit-slider-thumb]:border-[#5E45F8] [&::-webkit-slider-thumb]:bg-white [&::-webkit-slider-thumb]:shadow-xs';

interface InlineSliderProps {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  displayValue?: string;
  onChange: (val: number) => void;
}

const InlineSlider: React.FC<InlineSliderProps> = ({
  label,
  value,
  min,
  max,
  step = 1,
  displayValue,
  onChange,
}) => (
  <div className="flex h-6 w-full min-w-0 items-center gap-2">
    <span className="w-[62px] shrink-0 truncate text-[11px] text-[#56526B]" title={label}>
      {label}
    </span>
    <input
      type="range"
      min={min}
      max={max}
      step={step}
      value={value}
      onChange={(e) => onChange(Number(e.target.value))}
      aria-label={label}
      className={SLIDER_CLS}
    />
    <span className="w-9 shrink-0 text-right font-mono-tabular text-[11.5px] font-medium text-[#1A172B]">
      {displayValue ?? String(value)}
    </span>
  </div>
);

interface ScrubNumberInputProps {
  value: number;
  suffix: string;
  min?: number;
  max?: number;
  decimals?: number;
  dragRatio?: number;
  ariaLabel?: string;
  onChange: (val: number, recordHistory?: boolean) => void;
}

const ScrubNumberInput: React.FC<ScrubNumberInputProps> = ({
  value,
  suffix,
  min = -9999,
  max = 9999,
  decimals = 0,
  dragRatio = 1.5,
  ariaLabel,
  onChange,
}) => {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const factor = Math.pow(10, decimals);
  const formatVal = (v: number) => {
    const rounded = Math.round(v * factor) / factor;
    return decimals > 0 ? String(Number(rounded.toFixed(decimals))) : String(rounded);
  };
  const [draft, setDraft] = useState(() => formatVal(value));
  const [focused, setFocused] = useState(false);
  const [scrubbing, setScrubbing] = useState(false);

  useEffect(() => {
    if (!focused) {
      setDraft(formatVal(value));
    }
  }, [value, focused, decimals]);

  const clampVal = (v: number) => {
    const clamped = Math.max(min, Math.min(max, v));
    return Math.round(clamped * factor) / factor;
  };

  const handlePointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    if (e.button !== 0) return;
    // Allow normal text selection if user is already typing inside the focused input
    if (focused && e.target === inputRef.current) return;

    const startX = e.clientX;
    const startVal = value;
    let didDrag = false;
    let lastVal = startVal;

    const onMove = (ev: PointerEvent) => {
      const dx = ev.clientX - startX;
      if (!didDrag && Math.abs(dx) > 3) {
        didDrag = true;
        setScrubbing(true);
        document.body.style.cursor = 'ew-resize';
        inputRef.current?.blur();
      }
      if (didDrag) {
        ev.preventDefault();
        const speed = ev.shiftKey ? dragRatio * 4 : ev.altKey ? dragRatio * 0.25 : dragRatio;
        const nextVal = clampVal(startVal + dx * speed);
        if (nextVal !== lastVal) {
          lastVal = nextVal;
          setDraft(formatVal(nextVal));
          onChange(nextVal, false);
        }
      }
    };

    const onUp = () => {
      window.removeEventListener('pointermove', onMove);
      window.removeEventListener('pointerup', onUp);
      document.body.style.cursor = '';
      setScrubbing(false);
      if (didDrag) {
        onChange(lastVal, true);
      } else {
        inputRef.current?.focus();
        inputRef.current?.select();
      }
    };

    window.addEventListener('pointermove', onMove);
    window.addEventListener('pointerup', onUp);
  };

  const commitDraft = () => {
    const parsed = Number(draft.trim());
    if (!Number.isNaN(parsed) && draft.trim() !== '') {
      const clamped = clampVal(parsed);
      setDraft(formatVal(clamped));
      onChange(clamped, true);
    } else {
      setDraft(formatVal(value));
    }
  };

  return (
    <div
      onPointerDown={handlePointerDown}
      title="按住左右拖动快速调节，点击直接输入数值"
      className={`flex h-8 w-0 min-w-0 flex-1 items-center rounded-xl border px-2.5 transition-all select-none cursor-ew-resize ${
        scrubbing || focused
          ? 'border-[#5E45F8] bg-white ring-2 ring-[#5E45F8]/12'
          : 'border-[#E6E4F2] bg-[#F8F7FC] hover:border-[#C5BDF5] hover:bg-white'
      }`}
    >
      <input
        ref={inputRef}
        type="text"
        size={1}
        inputMode="decimal"
        aria-label={ariaLabel}
        value={focused ? draft : formatVal(value)}
        onFocus={() => {
          setFocused(true);
          setDraft(formatVal(value));
        }}
        onBlur={() => {
          setFocused(false);
          commitDraft();
        }}
        onChange={(e) => {
          const raw = e.target.value;
          setDraft(raw);
          const parsed = Number(raw);
          if (!Number.isNaN(parsed) && raw.trim() !== '' && raw !== '-' && !raw.endsWith('.')) {
            onChange(clampVal(parsed), false);
          }
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            commitDraft();
            inputRef.current?.blur();
          } else if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
            e.preventDefault();
            const baseStep = decimals > 0 ? 1 / factor : 1;
            const delta = (e.key === 'ArrowUp' ? 1 : -1) * (e.shiftKey ? baseStep * 10 : baseStep);
            const nextVal = clampVal(value + delta);
            setDraft(formatVal(nextVal));
            onChange(nextVal, true);
          }
        }}
        className="w-0 min-w-0 flex-1 bg-transparent font-mono-tabular text-[12px] font-semibold text-[#1A172B] outline-none"
      />
      <span className="ml-1 shrink-0 cursor-ew-resize text-[11px] font-medium text-[#928DA8]">
        {suffix}
      </span>
    </div>
  );
};

interface VisualFontSelectProps {
  value: string;
  onChange: (fontId: string) => void;
  ariaLabel?: string;
}

const VisualFontSelect: React.FC<VisualFontSelectProps> = ({
  value,
  onChange,
  ariaLabel = '选择字体',
}) => {
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement | null>(null);
  const current = EDITOR_FONTS.find((f) => f.id === value) ?? EDITOR_FONTS[0];

  useEffect(() => {
    if (!open) return;
    const handleOutside = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    window.addEventListener('mousedown', handleOutside);
    window.addEventListener('keydown', handleEsc);
    return () => {
      window.removeEventListener('mousedown', handleOutside);
      window.removeEventListener('keydown', handleEsc);
    };
  }, [open]);

  return (
    <div ref={wrapRef} className="relative mt-1">
      <button
        type="button"
        aria-label={ariaLabel}
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center justify-between gap-2 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-left transition-colors hover:border-[#C5BDF5] hover:bg-white"
      >
        <div className="min-w-0 flex-1">
          <div
            className="truncate text-[12px] font-semibold text-[#1A172B]"
            style={{ fontFamily: current.css || undefined }}
          >
            {current.label}
          </div>
          <div className="truncate text-[10px] text-[#7D7893]">{current.license}</div>
        </div>
        <div className="flex shrink-0 items-center gap-1.5">
          <span
            className="font-mono-tabular text-[11px] font-semibold text-[#5E45F8]"
            style={{ fontFamily: current.css || undefined }}
          >
            Aa 12
          </span>
          <ChevronDown
            className={`h-3.5 w-3.5 text-[#6B6880] transition-transform ${open ? 'rotate-180' : ''}`}
          />
        </div>
      </button>

      {open && (
        <div className="studio-scroll absolute left-0 right-0 top-full z-40 mt-1 max-h-60 overflow-y-auto rounded-xl border border-[#E6E4F2] bg-white p-1 shadow-[0_14px_36px_rgba(24,10,68,0.18)]">
          {EDITOR_FONTS.map((f) => {
            const active = f.id === value;
            return (
              <button
                key={f.id || 'default'}
                type="button"
                onClick={() => {
                  onChange(f.id);
                  setOpen(false);
                }}
                className={`flex w-full items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-left transition-colors ${
                  active
                    ? 'bg-[#EFEBFD] text-[#5E45F8]'
                    : 'text-[#1A172B] hover:bg-[#F5F4FA]'
                }`}
              >
                <div className="min-w-0 flex-1">
                  <div
                    className="truncate text-[12.5px] font-semibold"
                    style={{ fontFamily: f.css || undefined }}
                  >
                    {f.label}
                  </div>
                  <div className="truncate text-[9.5px] text-[#7D7893]">{f.license}</div>
                </div>
                <div className="flex shrink-0 items-center gap-1.5">
                  <span
                    className="text-[12px] opacity-80"
                    style={{ fontFamily: f.css || undefined }}
                  >
                    Aa 10:24
                  </span>
                  {active && <Check className="h-3.5 w-3.5 text-[#5E45F8]" />}
                </div>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
};

interface ShadowControlsProps {
  shadow?: EditorElementBase['shadow'];
  extractedPalette?: string[];
  onChange: (next?: EditorElementBase['shadow']) => void;
}

const ShadowControls: React.FC<ShadowControlsProps> = ({
  shadow,
  extractedPalette = [],
  onChange,
}) => (
  <div className="space-y-2 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
    <div className="flex items-center justify-between text-[11px]">
      <span className="font-semibold text-[#56526B]">投影阴影</span>
      {shadow ? (
        <button
          type="button"
          onClick={() => onChange(undefined)}
          className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
        >
          关闭投影
        </button>
      ) : (
        <button
          type="button"
          onClick={() => onChange({ color: '#000000', alpha: 55, blur: 2.4, x: 0, y: 0.8 })}
          className="rounded-md border border-[#D9D2FF] bg-white px-2 py-0.5 text-[10.5px] font-semibold text-[#5E45F8] hover:bg-[#EFEBFD]"
        >
          + 开启投影
        </button>
      )}
    </div>
    {shadow && (
      <div className="space-y-1.5">
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-1.5">
            <input
              type="color"
              value={shadow.color || '#000000'}
              aria-label="投影颜色"
              onChange={(e) => onChange({ ...shadow, color: e.target.value })}
              className="h-6 w-8 cursor-pointer rounded border border-[#E6E4F2]"
            />
            <span className="font-mono-tabular text-[10.5px] text-[#6B6880]">
              {(shadow.color || '#000000').toUpperCase()}
            </span>
          </div>
          {extractedPalette.length > 0 && (
            <div className="flex items-center gap-1">
              {extractedPalette.slice(0, 4).map((c) => (
                <button
                  key={c}
                  type="button"
                  title={`使用推荐色 ${c}`}
                  onClick={() => onChange({ ...shadow, color: c })}
                  className="h-4 w-4 rounded-full border border-white shadow-xs"
                  style={{ backgroundColor: c }}
                />
              ))}
            </div>
          )}
        </div>
        <InlineSlider
          label="投影浓度"
          min={10}
          max={100}
          value={shadow.alpha ?? 55}
          displayValue={`${shadow.alpha ?? 55}%`}
          onChange={(val) => onChange({ ...shadow, alpha: val })}
        />
        <InlineSlider
          label="模糊半径"
          min={0}
          max={8}
          step={0.2}
          value={shadow.blur}
          displayValue={`${shadow.blur.toFixed(1)}`}
          onChange={(val) => onChange({ ...shadow, blur: val })}
        />
        <InlineSlider
          label="水平偏移"
          min={-4}
          max={4}
          step={0.2}
          value={shadow.x}
          displayValue={`${shadow.x.toFixed(1)}`}
          onChange={(val) => onChange({ ...shadow, x: val })}
        />
        <InlineSlider
          label="垂直偏移"
          min={-4}
          max={4}
          step={0.2}
          value={shadow.y}
          displayValue={`${shadow.y.toFixed(1)}`}
          onChange={(val) => onChange({ ...shadow, y: val })}
        />
      </div>
    )}
  </div>
);

type PluginFieldMeta =
  | { label: string; type: 'text' | 'textarea' | 'boolean' }
  | { label: string; type: 'number'; min: number; max: number; step?: number; unit?: string }
  | { label: string; type: 'select'; options: { value: string; label: string }[] };

const PLUGIN_CONFIG_FIELD_META: Record<string, PluginFieldMeta> = {
  track: { label: '曲目名称', type: 'text' },
  artist: { label: '歌手 / 艺人', type: 'text' },
  showProgress: { label: '显示播放进度条', type: 'boolean' },
  currentLine: { label: '当前高亮歌词', type: 'text' },
  nextLine: { label: '下一句预览歌词', type: 'text' },
  title: { label: '便签标题', type: 'text' },
  content: { label: '便签正文内容', type: 'textarea' },
  caption: { label: '相册标题', type: 'text' },
  intervalSec: { label: '轮播间隔', type: 'number', min: 3, max: 120, step: 1, unit: '秒' },
  focusMinutes: { label: '专注时长', type: 'number', min: 5, max: 90, step: 5, unit: '分' },
  breakMinutes: { label: '休息时长', type: 'number', min: 1, max: 30, step: 1, unit: '分' },
  symbol: { label: '股票 / 指数代码', type: 'text' },
  price: { label: '展示价格', type: 'text' },
  change: { label: '涨跌幅文案', type: 'text' },
  refreshSeconds: { label: '刷新频率', type: 'number', min: 1, max: 30, step: 1, unit: '秒' },
  showNet: { label: '显示网络速率', type: 'boolean' },
  label: { label: '计时器标题', type: 'text' },
  minutes: { label: '倒计时长', type: 'number', min: 1, max: 180, step: 1, unit: '分' },
  item1: { label: '待办事项 1', type: 'text' },
  item2: { label: '待办事项 2', type: 'text' },
  item3: { label: '待办事项 3', type: 'text' },
  songTitle: { label: '曲目标题', type: 'text' },
  spectrumBars: { label: '频谱柱数量', type: 'number', min: 8, max: 32, step: 2, unit: '条' },
  fishCount: { label: '热带鱼数量', type: 'number', min: 1, max: 14, step: 1, unit: '条' },
  causticsIntensity: { label: '水流焦散强度', type: 'number', min: 0.2, max: 1.0, step: 0.1 },
  bubbleSpeed: { label: '气泡速率', type: 'number', min: 0.5, max: 2.0, step: 0.1, unit: 'x' },
  showClock: { label: '显示悬浮时钟', type: 'boolean' },
  colorHue: {
    label: '辉光管灯丝色温',
    type: 'select',
    options: [
      { value: 'amber', label: '经典琥珀' },
      { value: 'cyan', label: '赛博青蓝' },
      { value: 'green', label: '荧光翠绿' },
      { value: 'white', label: '纯净冷白' },
    ],
  },
  showSeconds: { label: '显示秒数管', type: 'boolean' },
  showDate: { label: '显示底部日期', type: 'boolean' },
  bubbleDensity: {
    label: '气泡密度',
    type: 'select',
    options: [
      { value: 'low', label: '轻柔' },
      { value: 'medium', label: '适中' },
      { value: 'high', label: '丰富' },
    ],
  },
  petName: { label: '萌宠昵称', type: 'text' },
  petSpecies: {
    label: '宠物形态',
    type: 'select',
    options: [
      { value: 'mochi', label: '赛博猫耳' },
      { value: 'pixel-bot', label: '像素机甲' },
      { value: 'shiba', label: '柴犬伙伴' },
    ],
  },
  flameIntensity: {
    label: '火焰强度',
    type: 'select',
    options: [
      { value: 'low', label: '微火' },
      { value: 'medium', label: '适中' },
      { value: 'high', label: '旺火' },
    ],
  },
  emberDensity: {
    label: '余烬密度',
    type: 'select',
    options: [
      { value: 'low', label: '稀疏' },
      { value: 'medium', label: '适中' },
      { value: 'high', label: '密集' },
    ],
  },
  showSoundHint: { label: '显示白噪音提示', type: 'boolean' },
  bonsaiSpecies: {
    label: '盆栽品种',
    type: 'select',
    options: [
      { value: 'pine', label: '青松微景' },
      { value: 'succulent', label: '玉露多肉' },
      { value: 'sakura', label: '春樱盆景' },
    ],
  },
  growthDays: { label: '养护天数', type: 'number', min: 1, max: 999, step: 1, unit: '天' },
  sunlightTone: {
    label: '光照色温',
    type: 'select',
    options: [
      { value: 'warm', label: '晨光暖阳' },
      { value: 'cool', label: '月夜冷光' },
      { value: 'mist', label: '森林微雾' },
    ],
  },
  flipShellStyle: {
    label: '机械外壳材质',
    type: 'select',
    options: [
      { value: 'obsidian', label: '曜石炭黑' },
      { value: 'warmwood', label: '暖木胡桃' },
      { value: 'titanium', label: '钛银拉丝' },
    ],
  },
  vuBacklight: {
    label: '表盘背光色',
    type: 'select',
    options: [
      { value: 'warm-amber', label: '经典暖黄' },
      { value: 'emerald', label: '示波翠绿' },
      { value: 'ice-blue', label: '冰蓝冷光' },
    ],
  },
  vuSensitivity: {
    label: '指针灵敏度',
    type: 'select',
    options: [
      { value: 'smooth', label: '舒缓' },
      { value: 'balanced', label: '均衡' },
      { value: 'punchy', label: '灵敏' },
    ],
  },
  deckLabel: { label: '电平表标签', type: 'text' },
  cassetteShell: {
    label: '磁带外壳色',
    type: 'select',
    options: [
      { value: 'smoke', label: '烟熏透黑' },
      { value: 'amber', label: '琥珀复古' },
      { value: 'indigo', label: '靛蓝透明' },
    ],
  },
  zodiacSign: {
    label: '星座选择',
    type: 'select',
    options: [
      { value: 'aries', label: '白羊座' },
      { value: 'taurus', label: '金牛座' },
      { value: 'gemini', label: '双子座' },
      { value: 'cancer', label: '巨蟹座' },
      { value: 'leo', label: '狮子座' },
      { value: 'virgo', label: '处女座' },
      { value: 'libra', label: '天秤座' },
      { value: 'scorpio', label: '天蝎座' },
      { value: 'sagittarius', label: '射手座' },
      { value: 'capricorn', label: '摩羯座' },
      { value: 'aquarius', label: '水瓶座' },
      { value: 'pisces', label: '双鱼座' },
    ],
  },
  showConstellationGlow: { label: '显示星图光晕', type: 'boolean' },
  parcelName: { label: '包裹备注名称', type: 'text' },
  carrier: { label: '承运商', type: 'text' },
  parcelStage: {
    label: '物流进度节点',
    type: 'select',
    options: [
      { value: 'collected', label: '已揽收' },
      { value: 'transit', label: '干线运输' },
      { value: 'delivering', label: '派送中' },
      { value: 'delivered', label: '已签收' },
    ],
  },
  milestoneTitle: { label: '纪念日标题', type: 'text' },
  targetDays: { label: '目标天数', type: 'number', min: 0, max: 9999, step: 1, unit: '天' },
  countMode: {
    label: '计数方式',
    type: 'select',
    options: [
      { value: 'countdown', label: '倒数天数' },
      { value: 'countup', label: '正数纪念' },
    ],
  },
  unitMode: {
    label: '速率单位',
    type: 'select',
    options: [
      { value: 'Mbps', label: 'Mbps' },
      { value: 'MB/s', label: 'MB/s' },
    ],
  },
  showPingChart: { label: '显示带宽波形图', type: 'boolean' },
  mood: {
    label: '情绪状态',
    type: 'select',
    options: [
      { value: 'happy', label: '开心活跃' },
      { value: 'chill', label: '慵懒打盹' },
      { value: 'focus', label: '陪伴专注' },
    ],
  },
};

interface EditorInspectorProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  project: EditorThemeProject;
  layoutIndex: number;
  selectedElements: EditorElement[];
  adjBg: boolean;
  aspectLinked: boolean;
  extractedPalette?: string[];
  alignScope?: 'selection' | 'canvas';
  hasStyleClipboard?: boolean;
  onChangeAlignScope?: (scope: 'selection' | 'canvas') => void;
  onToggleAspectLink: () => void;
  onToggleAdjBg: (on: boolean) => void;
  onMutateBg: (fn: (b: BgConfig) => BgConfig, label?: string) => void;
  onResetBgTransform: () => void;
  onMutateSelected: (
    fn: (el: EditorElement) => EditorElement,
    label?: string,
    recordHistory?: boolean
  ) => void;
  onBatchColor?: (hex: string) => void;
  onBatchAlpha?: (alpha: number) => void;
  onBatchFont?: (font: string) => void;
  onBatchFontSize?: (sizeCqmin: number) => void;
  onBatchRadius?: (radius: number) => void;
  onCopyStyle?: () => void;
  onPasteStyle?: () => void;
  onSaveAsAsset?: () => void;
  onReplaceBgImage?: () => void;
  onUploadBgLocal?: () => void;
  onReplaceImageForSelection: () => void;
  onReplaceImageLocal?: () => void;
  onFlipSelection?: (axis: 'x' | 'y') => void;
  onAlignSelection: (mode: InspectorAlignMode, scope?: 'selection' | 'canvas') => void;
  onDistributeSelection: (axis: 'h' | 'v') => void;
  onBulkScale: (factor: number) => void;
  onBulkRotate: (deltaDeg: number) => void;
  onGroupSelection: () => void;
  onUngroupSelection: () => void;
  onReorderLayer: (op: 'top' | 'up' | 'down' | 'bottom') => void;
  onSetLayerIndex: (targetIdx: number) => void;
  onToggleLock: (ids?: string[]) => void;
  onToggleHide: (ids?: string[]) => void;
  onDuplicateSelection: () => void;
  onDeleteSelection: () => void;
  onRenameLayer: (id: string) => void;
  onNotify?: (msg: string) => void;
}

export const EditorInspector: React.FC<EditorInspectorProps> = ({
  collapsed,
  onToggleCollapse,
  project,
  layoutIndex,
  selectedElements,
  adjBg,
  aspectLinked,
  extractedPalette = [],
  alignScope = 'selection',
  hasStyleClipboard = false,
  onChangeAlignScope,
  onToggleAspectLink,
  onToggleAdjBg,
  onMutateBg,
  onResetBgTransform,
  onMutateSelected,
  onBatchColor,
  onBatchAlpha,
  onBatchFont,
  onBatchFontSize,
  onBatchRadius,
  onCopyStyle,
  onPasteStyle,
  onSaveAsAsset,
  onReplaceBgImage,
  onUploadBgLocal,
  onReplaceImageForSelection,
  onReplaceImageLocal,
  onFlipSelection,
  onAlignSelection,
  onDistributeSelection,
  onBulkScale,
  onBulkRotate,
  onGroupSelection,
  onUngroupSelection,
  onReorderLayer,
  onSetLayerIndex,
  onToggleLock,
  onToggleHide,
  onDuplicateSelection,
  onDeleteSelection,
  onRenameLayer,
  onNotify,
}) => {
  const [layerPosOpen, setLayerPosOpen] = useState(false);
  const popoverRef = useRef<HTMLDivElement | null>(null);

  const layoutSize = project.layouts[layoutIndex]?.size ?? [1920, 462];
  const minDim = Math.min(layoutSize[0], layoutSize[1]);
  const isFollowLayout = layoutIndex > 0 && Boolean(project.layouts[layoutIndex]?.follow);
  const layoutElements = getLayoutElements(project, layoutIndex);

  useEffect(() => {
    if (selectedElements.length === 0 || adjBg) {
      setLayerPosOpen(false);
    }
  }, [selectedElements.length, adjBg]);

  useEffect(() => {
    if (!layerPosOpen) return;
    const handleOutside = (e: MouseEvent) => {
      if (popoverRef.current && !popoverRef.current.contains(e.target as Node)) {
        setLayerPosOpen(false);
      }
    };
    window.addEventListener('mousedown', handleOutside);
    return () => window.removeEventListener('mousedown', handleOutside);
  }, [layerPosOpen]);

  if (collapsed) {
    return (
      <aside
        aria-label="属性检查器（已收起）"
        className="flex h-full w-10 shrink-0 flex-col items-center gap-2 border-l border-[#E6E4F2] bg-white/92 py-2.5 backdrop-blur-xl"
      >
        <button
          type="button"
          onClick={onToggleCollapse}
          title="展开右侧属性栏 (])"
          aria-label="展开右侧属性栏"
          className="rounded-lg p-1.5 text-[#6B6880] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <span className="mt-2 text-[10px] font-bold tracking-widest text-[#6B6880] [writing-mode:vertical-rl]">
          属性检查器
        </span>
      </aside>
    );
  }

  const single = selectedElements.length === 1 ? selectedElements[0] : null;
  const multi = selectedElements.length > 1;
  const showBg = adjBg || selectedElements.length === 0;
  const singleLocked = single ? Boolean(project.locks[single.id]) : false;
  const singleHidden = single ? isElementHidden(project, layoutIndex, single) : false;

  const primarySelected = single ?? (selectedElements.length > 0 ? selectedElements[0] : null);
  const currentLayerIdx = primarySelected
    ? layoutElements.findIndex((e) => e.id === primarySelected.id)
    : -1;
  const totalLayers = layoutElements.length;

  const anchorBaseX = single
    ? single.a[1] === 'l'
      ? 0
      : single.a[1] === 'r'
        ? 100
        : 50
    : 50;
  const anchorBaseY = single
    ? single.a[0] === 't'
      ? 0
      : single.a[0] === 'b'
        ? 100
        : 50
    : 50;

  const pxX = single ? Math.round(((anchorBaseX + single.x) * layoutSize[0]) / 100) : 0;
  const pxY = single ? Math.round(((anchorBaseY + single.y) * layoutSize[1]) / 100) : 0;

  const kindLabel = single
    ? single.kind === 'clock'
      ? '时钟'
      : single.kind === 'date'
        ? '日期'
        : single.kind === 'text'
          ? '文字'
          : single.kind === 'image'
            ? '图片'
            : single.kind === 'video'
              ? '视频'
              : single.kind === 'shape'
                ? '形状'
                : single.kind === 'motion'
                  ? '动态'
                  : single.kind === 'widget'
                    ? '组件'
                    : '插件'
    : '';

  return (
    <aside
      aria-label="上下文属性检查器"
      className="relative flex h-full w-[320px] shrink-0 flex-col border-l border-[#E6E4F2] bg-white/92 text-[#1A172B] backdrop-blur-xl"
    >
      {/* Inspector Header */}
      <div className="flex h-11 shrink-0 items-center justify-between border-b border-[#E6E4F2] px-3.5">
        <span className="truncate text-[13px] font-bold text-[#1A172B]">
          {showBg
            ? adjBg
              ? '背景交互调整中'
              : '画布与背景属性'
            : multi
              ? `多选组合属性 (${selectedElements.length} 项)`
              : `图层 · ${project.names[single!.id] || kindLabel}`}
        </span>
        <button
          type="button"
          onClick={onToggleCollapse}
          title="收起属性栏 (])"
          aria-label="收起属性栏"
          className="rounded-lg p-1 text-[#6B6880] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>

      {/* Top Layer Action Bar + Layer Position Button */}
      {!showBg && primarySelected && (
        <div ref={popoverRef} className="relative shrink-0 border-b border-[#E6E4F2] bg-[#FAF9FE] px-2.5 py-2">
          <div className="flex items-center justify-between gap-0.5">
            <div className="flex items-center gap-0.5">
              <button
                type="button"
                onClick={() => setLayerPosOpen((o) => !o)}
                title="图层位置与顺序"
                aria-label="图层位置与顺序"
                className={`flex h-7 w-7 items-center justify-center rounded-lg border transition-all ${
                  layerPosOpen
                    ? 'border-[#5E45F8] bg-[#EFEBFD] text-[#5E45F8] shadow-2xs'
                    : 'border-[#E6E4F2] bg-white text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]'
                }`}
              >
                <Layers className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                onClick={() => onToggleLock(selectedElements.map((e) => e.id))}
                title={singleLocked ? '解锁图层' : '锁定图层'}
                aria-label={singleLocked ? '解锁图层' : '锁定图层'}
                className={`flex h-7 w-7 items-center justify-center rounded-lg border transition-colors ${
                  singleLocked
                    ? 'border-amber-300 bg-amber-50 text-amber-600'
                    : 'border-transparent bg-transparent text-[#56526B] hover:bg-white hover:text-[#5E45F8]'
                }`}
              >
                {singleLocked ? <Lock className="h-3.5 w-3.5" /> : <Unlock className="h-3.5 w-3.5" />}
              </button>
              <button
                type="button"
                onClick={() => onToggleHide(selectedElements.map((e) => e.id))}
                title={singleHidden ? '显示图层' : '隐藏图层'}
                aria-label={singleHidden ? '显示图层' : '隐藏图层'}
                className="flex h-7 w-7 items-center justify-center rounded-lg text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
              >
                {singleHidden ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
              </button>
              <button
                type="button"
                onClick={onDuplicateSelection}
                title="创建副本 (Ctrl+D)"
                aria-label="创建副本"
                className="flex h-7 w-7 items-center justify-center rounded-lg text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
              >
                <Copy className="h-3.5 w-3.5" />
              </button>
              {single && (
                <>
                  <button
                    type="button"
                    onClick={() => onRenameLayer(single.id)}
                    title="重命名图层"
                    aria-label="重命名图层"
                    className="flex h-7 w-7 items-center justify-center rounded-lg text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
                  >
                    <Edit3 className="h-3.5 w-3.5" />
                  </button>
                  {onCopyStyle && (
                    <button
                      type="button"
                      onClick={onCopyStyle}
                      title="复制样式（跨主题持久生效）"
                      aria-label="复制样式"
                      className="flex h-7 w-7 items-center justify-center rounded-lg text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
                    >
                      <Paintbrush className="h-3.5 w-3.5" />
                    </button>
                  )}
                </>
              )}
              {onPasteStyle && (
                <button
                  type="button"
                  disabled={!hasStyleClipboard || singleLocked}
                  onClick={onPasteStyle}
                  title={hasStyleClipboard ? '粘贴样式到当前选中图层' : '暂无已复制样式'}
                  aria-label="粘贴样式"
                  className="flex h-7 w-7 items-center justify-center rounded-lg text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
                >
                  <ClipboardPaste className="h-3.5 w-3.5" />
                </button>
              )}
              {onSaveAsAsset && (
                <button
                  type="button"
                  onClick={onSaveAsAsset}
                  title="存为我的素材预设"
                  aria-label="存为我的素材预设"
                  className="flex h-7 w-7 items-center justify-center rounded-lg text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
                >
                  <BookmarkPlus className="h-3.5 w-3.5" />
                </button>
              )}
            </div>

            <button
              type="button"
              disabled={singleLocked}
              onClick={onDeleteSelection}
              title="删除图层 (Del)"
              aria-label="删除图层"
              className="flex h-7 w-7 items-center justify-center rounded-lg text-red-500 hover:bg-red-50 disabled:opacity-40"
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>

          {/* Floating Layer Position & Order Popover (Reference Image 5) */}
          {layerPosOpen && (
            <div className="absolute right-3 top-11 z-40 w-[264px] rounded-2xl border border-[#E6E4F2] bg-white/96 p-4 shadow-[0_20px_50px_rgba(24,10,68,0.18)] backdrop-blur-xl">
              <div className="flex items-center justify-between">
                <span className="text-[13px] font-bold text-[#1A172B]">图层位置</span>
                <button
                  type="button"
                  onClick={() => setLayerPosOpen(false)}
                  aria-label="关闭图层位置弹窗"
                  className="rounded-lg p-1 text-[#6B6880] hover:bg-[#F5F4FA]"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </div>

              {/* Horizontal & Vertical Center Pills */}
              <div className="mt-3 grid grid-cols-2 gap-2">
                <button
                  type="button"
                  disabled={singleLocked}
                  onClick={() => onAlignSelection('hc', 'canvas')}
                  className="rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] py-2 text-[12px] font-semibold text-[#1A172B] transition-colors hover:border-[#5E45F8] hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:opacity-40"
                >
                  水平居中
                </button>
                <button
                  type="button"
                  disabled={singleLocked}
                  onClick={() => onAlignSelection('vc', 'canvas')}
                  className="rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] py-2 text-[12px] font-semibold text-[#1A172B] transition-colors hover:border-[#5E45F8] hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:opacity-40"
                >
                  垂直居中
                </button>
              </div>

              {/* 9-Point Visual Alignment Pad */}
              <div className="group/pad mx-auto mt-3 grid h-[108px] w-[114px] grid-cols-3 grid-rows-3 place-items-center rounded-2xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                {ANCHORS.map((pt) => {
                  const isCardinal =
                    pt === 'tc' || pt === 'ml' || pt === 'mc' || pt === 'mr' || pt === 'bc';
                  const isActive =
                    single &&
                    single.a === pt &&
                    Math.abs(single.x) <= 4.5 &&
                    Math.abs(single.y) <= 4.5;
                  return (
                    <button
                      key={pt}
                      type="button"
                      disabled={singleLocked}
                      title={`画布对齐：${pt.toUpperCase()}`}
                      onClick={() => onAlignSelection(pt, 'canvas')}
                      className={`flex h-6 w-6 items-center justify-center rounded-full transition-all disabled:opacity-40 ${
                        isCardinal
                          ? 'opacity-100'
                          : 'opacity-25 group-hover/pad:opacity-100'
                      } hover:bg-[#EFEBFD]`}
                    >
                      <span
                        className={`block rounded-full transition-all ${
                          isActive
                            ? 'h-2.5 w-2.5 bg-[#5E45F8] ring-4 ring-[#5E45F8]/20'
                            : 'h-1.5 w-1.5 bg-[#A39DBA]'
                        }`}
                      />
                    </button>
                  );
                })}
              </div>

              {/* Layer Z-Order Section with Ticked Slider */}
              <div className="mt-3.5 border-t border-[#E6E4F2] pt-3">
                <div className="flex items-center justify-between">
                  <span className="text-[12.5px] font-bold text-[#1A172B]">图层顺序</span>
                  <span className="font-mono-tabular text-[11px] font-semibold text-[#6B6880]">
                    {currentLayerIdx >= 0 ? `${currentLayerIdx + 1} / ${totalLayers}` : '-'}
                  </span>
                </div>
                <div className="mt-2.5 flex items-center gap-2">
                  <button
                    type="button"
                    disabled={singleLocked || currentLayerIdx <= 0}
                    onClick={() => onReorderLayer('down')}
                    title="下移一层"
                    aria-label="下移一层"
                    className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8] disabled:opacity-35"
                  >
                    <ArrowDownToLine className="h-3.5 w-3.5" />
                  </button>
                  <div className="flex min-w-0 flex-1 flex-col items-center">
                    <input
                      type="range"
                      min={0}
                      max={Math.max(0, totalLayers - 1)}
                      step={1}
                      disabled={singleLocked || totalLayers <= 1 || currentLayerIdx < 0}
                      value={Math.max(0, currentLayerIdx)}
                      onChange={(e) => onSetLayerIndex(Number(e.target.value))}
                      aria-label="调整图层顺序"
                      className="h-1 w-full cursor-pointer appearance-none rounded-full bg-[#DFDCF0] accent-[#5E45F8] [&::-webkit-slider-thumb]:h-3.5 [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:border-[1.5px] [&::-webkit-slider-thumb]:border-[#5E45F8] [&::-webkit-slider-thumb]:bg-white [&::-webkit-slider-thumb]:shadow-xs"
                    />
                    <div className="mt-1 flex w-full justify-between px-1">
                      {Array.from({ length: Math.min(10, Math.max(2, totalLayers)) }).map((_, i) => (
                        <span key={i} className="h-1.5 w-px bg-[#CFCBE3]" />
                      ))}
                    </div>
                  </div>
                  <button
                    type="button"
                    disabled={singleLocked || currentLayerIdx < 0 || currentLayerIdx >= totalLayers - 1}
                    onClick={() => onReorderLayer('up')}
                    title="上移一层"
                    aria-label="上移一层"
                    className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8] disabled:opacity-35"
                  >
                    <ArrowUpToLine className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Scrollable Inspector Body */}
      <div className="min-w-0 flex-1 space-y-4 overflow-y-auto overflow-x-hidden p-3.5 text-[12px] studio-scroll">
        {/* BACKGROUND MODE */}
        {showBg && (
          <>
            <div className="space-y-2.5">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                  背景类型与填充
                </span>
                <button
                  type="button"
                  onClick={() => onMutateBg((b) => ({ ...b, hide: !b.hide }))}
                  className="flex items-center gap-1 rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-0.5 text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  {project.bg.hide ? <EyeOff className="h-3 w-3" /> : <Eye className="h-3 w-3" />}
                  <span>{project.bg.hide ? '已隐藏' : '显示中'}</span>
                </button>
              </div>

              <div className="grid grid-cols-4 gap-1 rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] p-1 text-[11px] font-semibold">
                {(
                  [
                    { id: 'style', label: '材质' },
                    { id: 'color', label: '纯色' },
                    { id: 'gradient', label: '渐变' },
                    { id: 'image', label: '图片' },
                  ] as const
                ).map((k) => (
                  <button
                    key={k.id}
                    type="button"
                    onClick={() =>
                      onMutateBg((b) => ({
                        ...b,
                        kind: k.id,
                        style: b.style || 'ink',
                        color: b.color || '#141026',
                        g1: b.g1 || '#180942',
                        g2: b.g2 || '#5E45F8',
                        hide: false,
                      }))
                    }
                    className={`rounded-lg py-1 transition-all ${
                      project.bg.kind === k.id
                        ? 'bg-white text-[#5E45F8] shadow-xs'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    {k.label}
                  </button>
                ))}
              </div>

              {project.bg.kind === 'video' && (
                <div className="flex items-center justify-between gap-2 rounded-xl border border-[#D9D2FF] bg-[#EFEBFD] px-3 py-2 text-[11.5px] text-[#4D34E6]">
                  <div className="flex min-w-0 items-center gap-1.5">
                    <Film className="h-3.5 w-3.5 shrink-0" />
                    <span className="truncate font-semibold">
                      视频背景 · {project.bg.vname || '动态视频流'}
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() =>
                      onMutateBg((b) => ({
                        ...b,
                        kind: 'style',
                        style: 'ink',
                        vurl: undefined,
                        vname: undefined,
                      }))
                    }
                    className="shrink-0 rounded-md bg-white px-2 py-0.5 text-[10.5px] font-semibold text-[#5E45F8] hover:bg-[#5E45F8] hover:text-white"
                  >
                    切回底色
                  </button>
                </div>
              )}

              {project.bg.kind === 'style' && (
                <div className="grid grid-cols-4 gap-1.5">
                  {BUILTIN_BG_STYLES.map((st) => (
                    <button
                      key={st.id}
                      type="button"
                      onClick={() => onMutateBg((b) => ({ ...b, style: st.id, url: '' }))}
                      title={st.label}
                      className={`h-7 rounded-lg border transition-all ${
                        project.bg.style === st.id
                          ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/25'
                          : 'border-[#E6E4F2] hover:border-[#DAD6F0]'
                      }`}
                      style={{ background: st.css }}
                    />
                  ))}
                </div>
              )}

              {project.bg.kind === 'color' && (
                <div className="flex items-center gap-2">
                  <input
                    type="color"
                    value={project.bg.color || '#141026'}
                    onChange={(e) => onMutateBg((b) => ({ ...b, color: e.target.value }))}
                    className="h-8 w-12 cursor-pointer rounded-lg border border-[#E6E4F2]"
                  />
                  <span className="font-mono-tabular text-[#6B6880]">
                    {project.bg.color || '#141026'}
                  </span>
                </div>
              )}

              {project.bg.kind === 'gradient' && (
                <div className="space-y-2">
                  <div className="flex items-center gap-3">
                    <label className="flex items-center gap-1.5 text-[11px] text-[#6B6880]">
                      起点
                      <input
                        type="color"
                        value={project.bg.g1 || '#180942'}
                        onChange={(e) => onMutateBg((b) => ({ ...b, g1: e.target.value }))}
                        className="h-7 w-9 cursor-pointer rounded-lg border border-[#E6E4F2]"
                      />
                    </label>
                    <label className="flex items-center gap-1.5 text-[11px] text-[#6B6880]">
                      终点
                      <input
                        type="color"
                        value={project.bg.g2 || '#5E45F8'}
                        onChange={(e) => onMutateBg((b) => ({ ...b, g2: e.target.value }))}
                        className="h-7 w-9 cursor-pointer rounded-lg border border-[#E6E4F2]"
                      />
                    </label>
                  </div>
                  <InlineSlider
                    label="渐变角度"
                    min={0}
                    max={360}
                    value={project.bg.angle ?? 135}
                    displayValue={`${project.bg.angle ?? 135}°`}
                    onChange={(val) => onMutateBg((b) => ({ ...b, angle: val }))}
                  />
                </div>
              )}
              {project.bg.kind === 'image' && (
                <div className="space-y-2 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                  <div className="flex items-center justify-between text-[11px]">
                    <span className="font-semibold text-[#56526B]">壁纸图片源</span>
                    <span className="truncate max-w-[140px] text-[10.5px] text-[#7D7893]">
                      {project.bg.url ? '已配置背景图' : '暂未选图'}
                    </span>
                  </div>
                  <div className="grid grid-cols-2 gap-1.5">
                    {onUploadBgLocal && (
                      <button
                        type="button"
                        onClick={onUploadBgLocal}
                        className="flex items-center justify-center gap-1 rounded-lg border border-[#D9D2FF] bg-[#EFEBFD] py-1.5 text-[11px] font-semibold text-[#5E45F8] hover:bg-[#5E45F8] hover:text-white"
                      >
                        <Upload className="h-3 w-3" />
                        <span>本地上传</span>
                      </button>
                    )}
                    {onReplaceBgImage && (
                      <button
                        type="button"
                        onClick={onReplaceBgImage}
                        className="flex items-center justify-center gap-1 rounded-lg border border-[#E6E4F2] bg-white py-1.5 text-[11px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                      >
                        <span>壁纸库选图</span>
                      </button>
                    )}
                  </div>
                </div>
              )}
            </div>

            <div className="space-y-2.5 border-t border-[#E6E4F2] pt-3">
              <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                明暗、模糊与适配
              </div>

              <InlineSlider
                label="暗角遮罩"
                min={0}
                max={80}
                value={project.bg.dim}
                displayValue={`${project.bg.dim}%`}
                onChange={(val) => onMutateBg((b) => ({ ...b, dim: val }))}
              />

              <InlineSlider
                label="高斯模糊"
                min={0}
                max={12}
                step={0.5}
                value={project.bg.blur}
                displayValue={`${project.bg.blur}px`}
                onChange={(val) => onMutateBg((b) => ({ ...b, blur: val }))}
              />

              <div className="space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                <div className="flex items-center justify-between text-[11px]">
                  <span className="font-semibold text-[#56526B]">背景调色</span>
                  <button
                    type="button"
                    onClick={() =>
                      onMutateBg((b) => ({ ...b, brightness: 100, contrast: 100, saturate: 100 }))
                    }
                    className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
                  >
                    重置调色
                  </button>
                </div>
                <InlineSlider
                  label="亮度"
                  min={50}
                  max={150}
                  value={project.bg.brightness ?? 100}
                  displayValue={`${project.bg.brightness ?? 100}%`}
                  onChange={(val) => onMutateBg((b) => ({ ...b, brightness: val }))}
                />
                <InlineSlider
                  label="对比度"
                  min={50}
                  max={150}
                  value={project.bg.contrast ?? 100}
                  displayValue={`${project.bg.contrast ?? 100}%`}
                  onChange={(val) => onMutateBg((b) => ({ ...b, contrast: val }))}
                />
                <InlineSlider
                  label="饱和度"
                  min={0}
                  max={200}
                  value={project.bg.saturate ?? 100}
                  displayValue={`${project.bg.saturate ?? 100}%`}
                  onChange={(val) => onMutateBg((b) => ({ ...b, saturate: val }))}
                />
              </div>

              <div className="flex items-center justify-between">
                <span className="text-[11px] font-medium text-[#56526B]">填充方式</span>
                <div className="flex rounded-lg border border-[#E6E4F2] bg-[#F5F4FA] p-0.5 text-[11px] font-semibold">
                  {(
                    [
                      { id: 'cover', label: '裁切铺满' },
                      { id: 'contain', label: '完整显示' },
                      { id: 'stretch', label: '拉伸' },
                    ] as const
                  ).map((f) => (
                    <button
                      key={f.id}
                      type="button"
                      onClick={() => onMutateBg((b) => ({ ...b, fit: f.id }))}
                      className={`rounded-md px-2 py-0.5 transition-colors ${
                        project.bg.fit === f.id
                          ? 'bg-white text-[#5E45F8] shadow-xs'
                          : 'text-[#6B6880] hover:text-[#1A172B]'
                      }`}
                    >
                      {f.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <div className="space-y-2.5 border-t border-[#E6E4F2] pt-3">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                  背景位置与缩放
                </span>
                <button
                  type="button"
                  onClick={onResetBgTransform}
                  className="text-[11px] font-semibold text-[#5E45F8] hover:underline"
                >
                  重置变换
                </button>
              </div>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={Boolean(project.bg.hide || project.bg.kind === 'color')}
                  onClick={() => onToggleAdjBg(!adjBg)}
                  className={`flex flex-1 items-center justify-center gap-1.5 rounded-xl py-1.5 text-[11.5px] font-semibold transition-all disabled:opacity-40 ${
                    adjBg
                      ? 'bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.28)]'
                      : 'border border-[#E6E4F2] bg-[#F8F7FC] text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]'
                  }`}
                >
                  <Move className="h-3.5 w-3.5" />
                  <span>{adjBg ? '完成背景拖拽' : '画布直接拖动调整'}</span>
                </button>
              </div>

              <InlineSlider
                label="背景缩放"
                min={50}
                max={250}
                value={Math.round((project.bg.sc || 1) * 100)}
                displayValue={`${Math.round((project.bg.sc || 1) * 100)}%`}
                onChange={(val) => onMutateBg((b) => ({ ...b, sc: val / 100 }))}
              />

              <InlineSlider
                label="背景旋转"
                min={-180}
                max={180}
                value={Math.round(project.bg.rot || 0)}
                displayValue={`${Math.round(project.bg.rot || 0)}°`}
                onChange={(val) => onMutateBg((b) => ({ ...b, rot: val }))}
              />
            </div>
          </>
        )}

        {/* MULTI-SELECTION MODE */}
        {multi && !adjBg && (
          <>
            <div className="flex flex-wrap gap-1.5">
              <button
                type="button"
                onClick={onGroupSelection}
                className="flex flex-1 items-center justify-center gap-1 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
              >
                <Group className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>成组 (Ctrl+G)</span>
              </button>
              <button
                type="button"
                onClick={onUngroupSelection}
                className="flex flex-1 items-center justify-center gap-1 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
              >
                <Ungroup className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>取消成组</span>
              </button>
            </div>

            {/* Alignment & Even Distribution */}
            <div className="space-y-2 border-t border-[#E6E4F2] pt-3">
              <div className="flex items-center justify-between gap-2">
                <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                  对齐与等距分布
                </span>
                <div className="flex rounded-lg border border-[#E6E4F2] bg-[#F5F4FA] p-0.5 text-[10.5px] font-semibold">
                  <button
                    type="button"
                    onClick={() => onChangeAlignScope?.('selection')}
                    className={`rounded-md px-2 py-0.5 transition-colors whitespace-nowrap ${
                      alignScope === 'selection'
                        ? 'bg-white text-[#5E45F8] shadow-xs'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    对齐选区
                  </button>
                  <button
                    type="button"
                    onClick={() => onChangeAlignScope?.('canvas')}
                    className={`rounded-md px-2 py-0.5 transition-colors whitespace-nowrap ${
                      alignScope === 'canvas'
                        ? 'bg-white text-[#5E45F8] shadow-xs'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    对齐画布
                  </button>
                </div>
              </div>
              <div className="grid grid-cols-6 gap-1">
                {[
                  { k: 'l' as const, label: '左对齐', Icon: AlignStartVertical },
                  { k: 'hc' as const, label: '水平居中', Icon: AlignCenterVertical },
                  { k: 'r' as const, label: '右对齐', Icon: AlignEndVertical },
                  { k: 't' as const, label: '顶对齐', Icon: AlignStartHorizontal },
                  { k: 'vc' as const, label: '垂直居中', Icon: AlignCenterHorizontal },
                  { k: 'b' as const, label: '底对齐', Icon: AlignEndHorizontal },
                ].map(({ k, label, Icon }) => (
                  <button
                    key={k}
                    type="button"
                    title={`${label}（${alignScope === 'selection' ? '互相对齐' : '相对画布'}）`}
                    onClick={() => onAlignSelection(k, alignScope)}
                    className="flex h-7 items-center justify-center rounded-lg border border-[#E6E4F2] bg-white text-[#56526B] hover:border-[#5E45F8] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                  >
                    <Icon className="h-3.5 w-3.5" />
                  </button>
                ))}
              </div>
              <div className="grid grid-cols-2 gap-1.5">
                <button
                  type="button"
                  onClick={() => onDistributeSelection('h')}
                  className="flex items-center justify-center gap-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 text-[11px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <AlignHorizontalSpaceAround className="h-3.5 w-3.5 text-[#5E45F8]" />
                  <span>水平等距分布</span>
                </button>
                <button
                  type="button"
                  onClick={() => onDistributeSelection('v')}
                  className="flex items-center justify-center gap-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 text-[11px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <AlignVerticalSpaceAround className="h-3.5 w-3.5 text-[#5E45F8]" />
                  <span>垂直等距分布</span>
                </button>
              </div>
            </div>

            {/* Batch Color & Alpha */}
            {(() => {
              const avgAlphaPct = Math.round(
                (selectedElements.reduce((sum, e) => sum + e.alpha, 0) /
                  Math.max(1, selectedElements.length)) *
                  100
              );
              const firstColored = selectedElements.find(
                (e) => e.kind !== 'image' && e.kind !== 'video'
              );
              const batchHex =
                firstColored?.kind === 'widget' || firstColored?.kind === 'plugin'
                  ? firstColored.accentColor
                  : firstColored?.color || '#FFFFFF';
              return (
                <div className="space-y-2 border-t border-[#E6E4F2] pt-3">
                  <div className="flex items-center justify-between">
                    <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                      批量改色与透明度
                    </span>
                    <span className="font-mono-tabular text-[10.5px] text-[#6B6880]">
                      {batchHex.toUpperCase()}
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-1.5">
                    <input
                      type="color"
                      value={batchHex}
                      aria-label="批量统一修改颜色"
                      onChange={(e) => onBatchColor?.(e.target.value)}
                      className="h-6 w-8 cursor-pointer rounded-md border border-[#E6E4F2]"
                    />
                    {QUICK_COLORS.map((c) => (
                      <button
                        key={c}
                        type="button"
                        title={`批量改色 ${c}`}
                        onClick={() => onBatchColor?.(c)}
                        className="h-5 w-5 rounded-full border border-[#DAD6F0] transition-transform hover:scale-110"
                        style={{ backgroundColor: c }}
                      />
                    ))}
                  </div>
                  {extractedPalette.length > 0 && (
                    <div className="flex items-center justify-between gap-2 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5">
                      <span className="shrink-0 text-[10.5px] font-semibold text-[#5E45F8]">
                        背景推荐色
                      </span>
                      <div className="flex items-center gap-1">
                        {extractedPalette.map((c) => (
                          <button
                            key={c}
                            type="button"
                            title={`应用背景推荐色 ${c}`}
                            onClick={() => onBatchColor?.(c)}
                            className="h-4.5 w-4.5 rounded-full border border-white shadow-xs transition-transform hover:scale-110"
                            style={{ backgroundColor: c }}
                          />
                        ))}
                      </div>
                    </div>
                  )}
                  <InlineSlider
                    label="批量透明"
                    min={10}
                    max={100}
                    step={1}
                    value={avgAlphaPct}
                    displayValue={`${avgAlphaPct}%`}
                    onChange={(val) => onBatchAlpha?.(val / 100)}
                  />
                </div>
              );
            })()}

            {/* Batch Typography & Radius for compatible multi-selection */}
            {(() => {
              const typoEls = selectedElements.filter(
                (e) =>
                  e.kind === 'text' ||
                  e.kind === 'date' ||
                  (e.kind === 'clock' && (e as ClockElement).style !== 'analog')
              );
              const radiusEls = selectedElements.filter(
                (e) =>
                  e.kind === 'image' ||
                  e.kind === 'video' ||
                  (e.kind === 'shape' && ((e as ShapeElement).sh || 'rect') === 'rect')
              );
              if (typoEls.length === 0 && radiusEls.length === 0) return null;

              const avgSizePx =
                typoEls.length > 0
                  ? Math.round(
                      ((typoEls.reduce((s, e) => s + e.size, 0) / typoEls.length) * minDim) / 100
                    )
                  : 24;
              const commonFont = typoEls[0]?.font || '';
              const avgRadius =
                radiusEls.length > 0
                  ? Math.round(
                      radiusEls.reduce(
                        (s, e) => s + ((e as ImageElement | VideoElement | ShapeElement).radius ?? 0),
                        0
                      ) / radiusEls.length
                    )
                  : 0;

              return (
                <div className="space-y-2 border-t border-[#E6E4F2] pt-3">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    批量排版与圆角
                  </div>
                  {typoEls.length > 0 && (
                    <>
                      <div>
                        <span className="text-[11px] text-[#56526B]">
                          统一字体 ({typoEls.length} 项文本)
                        </span>
                        <VisualFontSelect
                          value={commonFont}
                          ariaLabel="批量统一字体"
                          onChange={(fId) => onBatchFont?.(fId)}
                        />
                      </div>
                      <InlineSlider
                        label="统一字号"
                        min={12}
                        max={Math.round(minDim * 1.2)}
                        value={avgSizePx}
                        displayValue={`${avgSizePx}`}
                        onChange={(px) => onBatchFontSize?.(+((px * 100) / minDim).toFixed(1))}
                      />
                    </>
                  )}
                  {radiusEls.length > 0 && (
                    <InlineSlider
                      label="统一圆角"
                      min={0}
                      max={30}
                      value={avgRadius}
                      displayValue={String(avgRadius)}
                      onChange={(r) => onBatchRadius?.(r)}
                    />
                  )}
                </div>
              );
            })()}

            {/* Bulk Transform */}
            <div className="space-y-2 border-t border-[#E6E4F2] pt-3">
              <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                批量变换与层级
              </div>
              <div className="grid grid-cols-4 gap-1">
                <button
                  type="button"
                  onClick={() => onBulkScale(0.9)}
                  className="rounded-lg border border-[#E6E4F2] py-1 font-mono-tabular text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  -10%
                </button>
                <button
                  type="button"
                  onClick={() => onBulkScale(1.1)}
                  className="rounded-lg border border-[#E6E4F2] py-1 font-mono-tabular text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  +10%
                </button>
                <button
                  type="button"
                  onClick={() => onBulkRotate(-15)}
                  className="flex items-center justify-center gap-0.5 rounded-lg border border-[#E6E4F2] py-1 font-mono-tabular text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <RotateCcw className="h-3 w-3" /> 15°
                </button>
                <button
                  type="button"
                  onClick={() => onBulkRotate(15)}
                  className="flex items-center justify-center gap-0.5 rounded-lg border border-[#E6E4F2] py-1 font-mono-tabular text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <RotateCw className="h-3 w-3" /> 15°
                </button>
              </div>
              <div className="grid grid-cols-2 gap-1.5">
                <button
                  type="button"
                  onClick={() =>
                    onFlipSelection
                      ? onFlipSelection('x')
                      : onMutateSelected((el) => ({ ...el, sx: (el.sx ?? 1) * -1 }))
                  }
                  className="flex items-center justify-center gap-1 rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] py-1 text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  <FlipHorizontal2 className="h-3.5 w-3.5" />
                  <span>水平翻转</span>
                </button>
                <button
                  type="button"
                  onClick={() =>
                    onFlipSelection
                      ? onFlipSelection('y')
                      : onMutateSelected((el) => ({ ...el, sy: (el.sy ?? 1) * -1 }))
                  }
                  className="flex items-center justify-center gap-1 rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] py-1 text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  <FlipVertical2 className="h-3.5 w-3.5" />
                  <span>垂直翻转</span>
                </button>
              </div>
              <div className="grid grid-cols-4 gap-1">
                <button
                  type="button"
                  onClick={() => onReorderLayer('top')}
                  className="flex items-center justify-center gap-1 rounded-lg border border-[#E6E4F2] py-1 text-[11px] text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <ArrowUpToLine className="h-3 w-3" /> 置顶
                </button>
                <button
                  type="button"
                  onClick={() => onReorderLayer('up')}
                  className="flex items-center justify-center gap-1 rounded-lg border border-[#E6E4F2] py-1 text-[11px] text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <ArrowUp className="h-3 w-3" /> 上移
                </button>
                <button
                  type="button"
                  onClick={() => onReorderLayer('down')}
                  className="flex items-center justify-center gap-1 rounded-lg border border-[#E6E4F2] py-1 text-[11px] text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <ArrowDown className="h-3 w-3" /> 下移
                </button>
                <button
                  type="button"
                  onClick={() => onReorderLayer('bottom')}
                  className="flex items-center justify-center gap-1 rounded-lg border border-[#E6E4F2] py-1 text-[11px] text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <ArrowDownToLine className="h-3 w-3" /> 置底
                </button>
              </div>
            </div>
          </>
        )}

        {/* SINGLE ELEMENT MODE */}
        {single && !adjBg && (
          <>
            <fieldset disabled={singleLocked} className="w-full min-w-0 space-y-4 disabled:opacity-60">
              {/* CONTENT SECTION */}
              {single.kind === 'text' && (
                <div className="space-y-2.5">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    文字内容与排版
                  </div>
                  <textarea
                    rows={3}
                    value={(single as TextElement).text}
                    onChange={(e) =>
                      onMutateSelected((el) => ({ ...el, text: e.target.value } as EditorElement))
                    }
                    className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5 text-[12px] text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                  />
                  <div className="flex items-center justify-between">
                    <span className="text-[11px] text-[#56526B]">对齐方向</span>
                    <div className="flex rounded-lg border border-[#E6E4F2] bg-[#F5F4FA] p-0.5 text-[11px]">
                      {[
                        { id: 'left', label: '左' },
                        { id: 'center', label: '中' },
                        { id: 'right', label: '右' },
                      ].map((al) => (
                        <button
                          key={al.id}
                          type="button"
                          onClick={() =>
                            onMutateSelected((el) => ({ ...el, align: al.id } as EditorElement))
                          }
                          className={`rounded-md px-2.5 py-0.5 font-semibold transition-colors ${
                            (single as TextElement).align === al.id
                              ? 'bg-white text-[#5E45F8] shadow-xs'
                              : 'text-[#6B6880]'
                          }`}
                        >
                          {al.label}
                        </button>
                      ))}
                    </div>
                  </div>
                  <InlineSlider
                    label="行高倍数"
                    min={0.9}
                    max={2.4}
                    step={0.05}
                    value={(single as TextElement).lh ?? 1.25}
                    displayValue={((single as TextElement).lh ?? 1.25).toFixed(2)}
                    onChange={(val) =>
                      onMutateSelected((el) => ({ ...el, lh: val } as EditorElement))
                    }
                  />
                </div>
              )}

              {single.kind === 'clock' && (
                <div className="space-y-2.5">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    时钟样式与时区
                  </div>
                  <div className="grid grid-cols-4 gap-1 rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] p-1 text-[11px] font-semibold">
                    {[
                      { id: 'digital', label: '数字' },
                      { id: 'flip', label: '翻页' },
                      { id: 'minimal', label: '极简' },
                      { id: 'analog', label: '表盘' },
                    ].map((st) => (
                      <button
                        key={st.id}
                        type="button"
                        onClick={() =>
                          onMutateSelected((el) => ({ ...el, style: st.id } as EditorElement))
                        }
                        className={`rounded-lg py-1 transition-colors ${
                          (single as ClockElement).style === st.id
                            ? 'bg-white text-[#5E45F8] shadow-xs'
                            : 'text-[#6B6880]'
                        }`}
                      >
                        {st.label}
                      </button>
                    ))}
                  </div>

                  {(single as ClockElement).style === 'analog' ? (
                    <>
                      <div className="flex items-center justify-between">
                        <span className="text-[11px] text-[#56526B]">表盘刻度</span>
                        <select
                          value={(single as ClockElement).dial || 'tick'}
                          onChange={(e) =>
                            onMutateSelected((el) => ({ ...el, dial: e.target.value } as EditorElement))
                          }
                          className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11px]"
                        >
                          <option value="tick">精细刻度</option>
                          <option value="dot">圆点标记</option>
                          <option value="num">阿拉伯数字</option>
                          <option value="roman">罗马数字</option>
                          <option value="bare">极简无刻度</option>
                        </select>
                      </div>
                      <div className="flex items-center justify-between">
                        <span className="text-[11px] text-[#56526B]">指针形状</span>
                        <select
                          value={(single as ClockElement).hands || 'line'}
                          onChange={(e) =>
                            onMutateSelected((el) => ({ ...el, hands: e.target.value } as EditorElement))
                          }
                          className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11px]"
                        >
                          <option value="line">经典细线</option>
                          <option value="round">圆润粗针</option>
                          <option value="taper">剑形锥针</option>
                        </select>
                      </div>
                      <label className="flex items-center justify-between text-[11.5px]">
                        <span>显示外圈表壳</span>
                        <input
                          type="checkbox"
                          className="accent-[#5E45F8]"
                          checked={(single as ClockElement).ring !== false}
                          onChange={(e) =>
                            onMutateSelected((el) => ({ ...el, ring: e.target.checked } as EditorElement))
                          }
                        />
                      </label>
                      <div className="space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                        <div className="flex items-center justify-between">
                          <span className="text-[11px] font-semibold text-[#56526B]">秒针 / 点缀色</span>
                          <div className="flex items-center gap-1.5">
                            <span className="font-mono-tabular text-[10.5px] text-[#6B6880]">
                              {((single as ClockElement).accent || '#ff6a4d').toUpperCase()}
                            </span>
                            <input
                              type="color"
                              value={(single as ClockElement).accent || '#ff6a4d'}
                              aria-label="模拟时钟点缀色"
                              onChange={(e) =>
                                onMutateSelected((el) => ({ ...el, accent: e.target.value } as EditorElement))
                              }
                              className="h-6 w-8 cursor-pointer rounded border border-[#E6E4F2]"
                            />
                          </div>
                        </div>
                        {extractedPalette.length > 0 && (
                          <div className="flex items-center justify-between gap-2">
                            <span className="text-[10px] font-semibold text-[#5E45F8]">背景推荐色</span>
                            <div className="flex items-center gap-1">
                              {extractedPalette.map((c) => (
                                <button
                                  key={c}
                                  type="button"
                                  onClick={() =>
                                    onMutateSelected((el) => ({ ...el, accent: c } as EditorElement))
                                  }
                                  className="h-4 w-4 rounded-full border border-white shadow-xs"
                                  style={{ backgroundColor: c }}
                                />
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    </>
                  ) : (
                    <>
                      <div className="flex items-center justify-between">
                        <span className="text-[11px] text-[#56526B]">独立拆分模块</span>
                        <select
                          value={(single as ClockElement).part || 'hms'}
                          onChange={(e) =>
                            onMutateSelected((el) => ({ ...el, part: e.target.value } as EditorElement))
                          }
                          className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11px]"
                        >
                          <option value="hms">完整时分秒</option>
                          <option value="hm">时分组合</option>
                          <option value="hour">仅小时</option>
                          <option value="minute">仅分钟</option>
                          <option value="second">仅秒钟</option>
                        </select>
                      </div>
                      <label className="flex items-center justify-between text-[11.5px]">
                        <span>24 小时制</span>
                        <input
                          type="checkbox"
                          className="accent-[#5E45F8]"
                          checked={(single as ClockElement).h24}
                          onChange={(e) =>
                            onMutateSelected((el) => ({ ...el, h24: e.target.checked } as EditorElement))
                          }
                        />
                      </label>
                    </>
                  )}

                  <label className="flex items-center justify-between text-[11.5px]">
                    <span>显示秒数 / 秒针</span>
                    <input
                      type="checkbox"
                      className="accent-[#5E45F8]"
                      checked={(single as ClockElement).sec}
                      onChange={(e) =>
                        onMutateSelected((el) => ({ ...el, sec: e.target.checked } as EditorElement))
                      }
                    />
                  </label>

                  <div className="flex items-center justify-between">
                    <span className="text-[11px] text-[#56526B]">时区绑定</span>
                    <select
                      value={(single as ClockElement).tz}
                      onChange={(e) =>
                        onMutateSelected((el) => ({ ...el, tz: e.target.value } as EditorElement))
                      }
                      className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11px]"
                    >
                      {EDITOR_TIMEZONES.map((z) => (
                        <option key={z.id} value={z.id}>
                          {z.label}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              )}

              {single.kind === 'date' && (
                <div className="space-y-2.5">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    日期模块与格式
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-[11px] text-[#56526B]">拆分子项</span>
                    <select
                      value={(single as DateElement).part || 'full'}
                      onChange={(e) =>
                        onMutateSelected((el) => ({
                          ...el,
                          part: e.target.value as DatePartKind,
                        } as EditorElement))
                      }
                      className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11px]"
                    >
                      <option value="full">完整日期与星期</option>
                      <option value="ymd">仅年月日</option>
                      <option value="weekday">仅星期</option>
                      <option value="ampm">仅 AM / PM</option>
                    </select>
                  </div>
                  <select
                    value={(single as DateElement).fmt}
                    onChange={(e) =>
                      onMutateSelected((el) => ({
                        ...el,
                        fmt: Number(e.target.value) as 0 | 1 | 2 | 3,
                      } as EditorElement))
                    }
                    className="w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]"
                  >
                    <option value={0}>中文日期 (10月30日 周五)</option>
                    <option value={1}>英文完整 (FRIDAY · OCTOBER 30)</option>
                    <option value={2}>数字标准 (2026-10-30)</option>
                    <option value={3}>极简短格式 (10.30 FRI)</option>
                  </select>
                </div>
              )}

              {single.kind === 'image' && (() => {
                const imgEl = single as ImageElement;
                return (
                  <div className="space-y-2.5">
                    <div className="flex items-center justify-between gap-1.5">
                      <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                        图片设置与裁切
                      </span>
                      <div className="flex items-center gap-1">
                        {onReplaceImageLocal && (
                          <button
                            type="button"
                            onClick={onReplaceImageLocal}
                            className="rounded-lg border border-[#D9D2FF] bg-[#EFEBFD] px-2 py-0.5 text-[11px] font-semibold text-[#5E45F8] hover:bg-[#5E45F8] hover:text-white"
                          >
                            本地替换
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={onReplaceImageForSelection}
                          className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-0.5 text-[11px] font-semibold text-[#56526B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                        >
                          素材库
                        </button>
                      </div>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-[11px] text-[#56526B]">填充模式</span>
                      <div className="flex rounded-lg border border-[#E6E4F2] bg-[#F5F4FA] p-0.5 text-[11px] font-semibold">
                        {(
                          [
                            { id: 'cover', label: '裁切' },
                            { id: 'contain', label: '适应' },
                            { id: 'fill', label: '拉伸' },
                          ] as const
                        ).map((m) => (
                          <button
                            key={m.id}
                            type="button"
                            onClick={() =>
                              onMutateSelected((el) => ({ ...el, fit: m.id } as EditorElement))
                            }
                            className={`rounded-md px-2 py-0.5 transition-colors ${
                              (imgEl.fit || 'cover') === m.id
                                ? 'bg-white text-[#5E45F8] shadow-xs'
                                : 'text-[#6B6880]'
                            }`}
                          >
                            {m.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    <InlineSlider
                      label="圆角"
                      min={0}
                      max={30}
                      value={imgEl.radius ?? 0}
                      displayValue={String(imgEl.radius ?? 0)}
                      onChange={(val) =>
                        onMutateSelected((el) => ({ ...el, radius: val } as EditorElement))
                      }
                    />

                    <InlineSlider
                      label="高斯模糊"
                      min={0}
                      max={10}
                      step={0.5}
                      value={imgEl.blur ?? 0}
                      displayValue={`${imgEl.blur ?? 0}px`}
                      onChange={(val) =>
                        onMutateSelected((el) => ({ ...el, blur: val } as EditorElement))
                      }
                    />

                    {/* Crop scale & offset inside frame */}
                    <details className="group/section space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <summary className="flex cursor-pointer list-none items-center justify-between text-[11px] font-semibold text-[#56526B] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] [&::-webkit-details-marker]:hidden">
                        框内缩放裁切
                        <span className="text-[10.5px] text-[#5E45F8] group-open/section:hidden">+ 展开</span>
                        <span className="hidden text-[10.5px] text-[#5E45F8] group-open/section:inline">− 收起</span>
                      </summary>
                      <div className="flex items-center justify-between text-[11px]">

                        <button
                          type="button"
                          onClick={() =>
                            onMutateSelected((el) => ({
                              ...el,
                              cropScale: 1,
                              cropX: 0,
                              cropY: 0,
                            } as EditorElement))
                          }
                          className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
                        >
                          重置裁切
                        </button>
                      </div>
                      <InlineSlider
                        label="放大倍率"
                        min={100}
                        max={250}
                        step={5}
                        value={Math.round((imgEl.cropScale ?? 1) * 100)}
                        displayValue={`${Math.round((imgEl.cropScale ?? 1) * 100)}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, cropScale: val / 100 } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="水平偏移"
                        min={-40}
                        max={40}
                        value={imgEl.cropX ?? 0}
                        displayValue={`${imgEl.cropX ?? 0}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, cropX: val } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="垂直偏移"
                        min={-40}
                        max={40}
                        value={imgEl.cropY ?? 0}
                        displayValue={`${imgEl.cropY ?? 0}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, cropY: val } as EditorElement))
                        }
                      />
                    </details>

                    {/* Color filter controls */}
                    <details className="group/section space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <summary className="flex cursor-pointer list-none items-center justify-between text-[11px] font-semibold text-[#56526B] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] [&::-webkit-details-marker]:hidden">
                        画面调色
                        <span className="text-[10.5px] text-[#5E45F8] group-open/section:hidden">+ 展开</span>
                        <span className="hidden text-[10.5px] text-[#5E45F8] group-open/section:inline">− 收起</span>
                      </summary>
                      <div className="flex items-center justify-between text-[11px]">

                        <button
                          type="button"
                          onClick={() =>
                            onMutateSelected((el) => ({
                              ...el,
                              brightness: 100,
                              contrast: 100,
                              saturate: 100,
                            } as EditorElement))
                          }
                          className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
                        >
                          重置调色
                        </button>
                      </div>
                      <InlineSlider
                        label="亮度"
                        min={50}
                        max={150}
                        value={imgEl.brightness ?? 100}
                        displayValue={`${imgEl.brightness ?? 100}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, brightness: val } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="对比度"
                        min={50}
                        max={150}
                        value={imgEl.contrast ?? 100}
                        displayValue={`${imgEl.contrast ?? 100}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, contrast: val } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="饱和度"
                        min={0}
                        max={200}
                        value={imgEl.saturate ?? 100}
                        displayValue={`${imgEl.saturate ?? 100}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, saturate: val } as EditorElement))
                        }
                      />
                    </details>

                    {/* Image Edge Feathering Mask */}
                    <details className="group/section space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <summary className="flex cursor-pointer list-none items-center justify-between text-[11px] font-semibold text-[#56526B] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] [&::-webkit-details-marker]:hidden">
                        边缘羽化遮罩
                        <span className="text-[10.5px] text-[#5E45F8] group-open/section:hidden">+ 展开</span>
                        <span className="hidden text-[10.5px] text-[#5E45F8] group-open/section:inline">− 收起</span>
                      </summary>
                      <div className="flex items-center justify-between text-[11px]">

                        {imgEl.feather && imgEl.feather.strength > 0 && (
                          <button
                            type="button"
                            onClick={() =>
                              onMutateSelected((el) => ({ ...el, feather: undefined } as EditorElement))
                            }
                            className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
                          >
                            关闭羽化
                          </button>
                        )}
                      </div>
                      <div className="grid grid-cols-4 gap-1 text-[10.5px] font-semibold">
                        {(
                          [
                            { id: 'off' as const, label: '关闭' },
                            { id: 'all' as const, label: '四周' },
                            { id: 'left' as const, label: '左侧' },
                            { id: 'right' as const, label: '右侧' },
                            { id: 'top' as const, label: '顶部' },
                            { id: 'bottom' as const, label: '底部' },
                            { id: 'horizontal' as const, label: '左右' },
                            { id: 'vertical' as const, label: '上下' },
                          ] as const
                        ).map((opt) => {
                          const isActive =
                            opt.id === 'off'
                              ? !imgEl.feather || imgEl.feather.strength <= 0
                              : imgEl.feather?.mode === opt.id && imgEl.feather.strength > 0;
                          return (
                            <button
                              key={opt.id}
                              type="button"
                              onClick={() =>
                                onMutateSelected((el) => {
                                  if (opt.id === 'off') {
                                    return { ...el, feather: undefined } as EditorElement;
                                  }
                                  const prevStrength = (el as ImageElement).feather?.strength;
                                  return {
                                    ...el,
                                    feather: {
                                      mode: opt.id as ImageFeatherMode,
                                      strength: prevStrength && prevStrength > 0 ? prevStrength : 24,
                                    },
                                  } as EditorElement;
                                })
                              }
                              className={`rounded-lg border py-1 transition-colors whitespace-nowrap ${
                                isActive
                                  ? 'border-[#5E45F8] bg-[#EFEBFD] text-[#5E45F8]'
                                  : 'border-[#E6E4F2] bg-white text-[#6B6880] hover:border-[#C5BDF5] hover:text-[#1A172B]'
                              }`}
                            >
                              {opt.label}
                            </button>
                          );
                        })}
                      </div>
                      {imgEl.feather && imgEl.feather.strength > 0 && (
                        <InlineSlider
                          label="羽化范围"
                          min={4}
                          max={55}
                          step={1}
                          value={imgEl.feather.strength}
                          displayValue={`${imgEl.feather.strength}%`}
                          onChange={(val) =>
                            onMutateSelected((el) => {
                              const cur = el as ImageElement;
                              return {
                                ...cur,
                                feather: {
                                  mode: cur.feather?.mode ?? 'all',
                                  strength: val,
                                },
                              } as EditorElement;
                            })
                          }
                        />
                      )}
                    </details>

                    <ShadowControls
                      shadow={imgEl.shadow}
                      extractedPalette={extractedPalette}
                      onChange={(nextShadow) =>
                        onMutateSelected((el) => ({ ...el, shadow: nextShadow } as EditorElement))
                      }
                    />
                  </div>
                );
              })()}

              {single.kind === 'video' && (() => {
                const vidEl = single as VideoElement;
                return (
                  <div className="space-y-2.5">
                    <div className="flex items-center justify-between">
                      <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                        视频设置与画面裁切
                      </span>
                      <button
                        type="button"
                        onClick={onReplaceImageForSelection}
                        className="rounded-lg border border-[#D9D2FF] bg-[#EFEBFD] px-2.5 py-0.5 text-[11px] font-semibold text-[#5E45F8] hover:bg-[#5E45F8] hover:text-white"
                      >
                        导入新视频…
                      </button>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-[11px] text-[#56526B]">填充模式</span>
                      <div className="flex rounded-lg border border-[#E6E4F2] bg-[#F5F4FA] p-0.5 text-[11px] font-semibold">
                        {(
                          [
                            { id: 'cover', label: '裁切' },
                            { id: 'contain', label: '适应' },
                            { id: 'fill', label: '拉伸' },
                          ] as const
                        ).map((m) => (
                          <button
                            key={m.id}
                            type="button"
                            onClick={() =>
                              onMutateSelected((el) => ({ ...el, fit: m.id } as EditorElement))
                            }
                            className={`rounded-md px-2 py-0.5 transition-colors ${
                              (vidEl.fit || 'cover') === m.id
                                ? 'bg-white text-[#5E45F8] shadow-xs'
                                : 'text-[#6B6880]'
                            }`}
                          >
                            {m.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    <InlineSlider
                      label="圆角"
                      min={0}
                      max={30}
                      value={vidEl.radius ?? 0}
                      displayValue={String(vidEl.radius ?? 0)}
                      onChange={(val) =>
                        onMutateSelected((el) => ({ ...el, radius: val } as EditorElement))
                      }
                    />

                    <InlineSlider
                      label="高斯模糊"
                      min={0}
                      max={10}
                      step={0.5}
                      value={vidEl.blur ?? 0}
                      displayValue={`${vidEl.blur ?? 0}px`}
                      onChange={(val) =>
                        onMutateSelected((el) => ({ ...el, blur: val } as EditorElement))
                      }
                    />

                    <details className="group/section space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <summary className="flex cursor-pointer list-none items-center justify-between text-[11px] font-semibold text-[#56526B] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] [&::-webkit-details-marker]:hidden">
                        框内缩放裁切
                        <span className="text-[10.5px] text-[#5E45F8] group-open/section:hidden">+ 展开</span>
                        <span className="hidden text-[10.5px] text-[#5E45F8] group-open/section:inline">− 收起</span>
                      </summary>
                      <div className="flex items-center justify-between text-[11px]">

                        <button
                          type="button"
                          onClick={() =>
                            onMutateSelected((el) => ({
                              ...el,
                              cropScale: 1,
                              cropX: 0,
                              cropY: 0,
                            } as EditorElement))
                          }
                          className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
                        >
                          重置裁切
                        </button>
                      </div>
                      <InlineSlider
                        label="放大倍率"
                        min={100}
                        max={250}
                        step={5}
                        value={Math.round((vidEl.cropScale ?? 1) * 100)}
                        displayValue={`${Math.round((vidEl.cropScale ?? 1) * 100)}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, cropScale: val / 100 } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="水平偏移"
                        min={-40}
                        max={40}
                        value={vidEl.cropX ?? 0}
                        displayValue={`${vidEl.cropX ?? 0}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, cropX: val } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="垂直偏移"
                        min={-40}
                        max={40}
                        value={vidEl.cropY ?? 0}
                        displayValue={`${vidEl.cropY ?? 0}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, cropY: val } as EditorElement))
                        }
                      />
                    </details>

                    <details className="group/section space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <summary className="flex cursor-pointer list-none items-center justify-between text-[11px] font-semibold text-[#56526B] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] [&::-webkit-details-marker]:hidden">
                        画面调色
                        <span className="text-[10.5px] text-[#5E45F8] group-open/section:hidden">+ 展开</span>
                        <span className="hidden text-[10.5px] text-[#5E45F8] group-open/section:inline">− 收起</span>
                      </summary>
                      <div className="flex items-center justify-between text-[11px]">

                        <button
                          type="button"
                          onClick={() =>
                            onMutateSelected((el) => ({
                              ...el,
                              brightness: 100,
                              contrast: 100,
                              saturate: 100,
                            } as EditorElement))
                          }
                          className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
                        >
                          重置调色
                        </button>
                      </div>
                      <InlineSlider
                        label="亮度"
                        min={50}
                        max={150}
                        value={vidEl.brightness ?? 100}
                        displayValue={`${vidEl.brightness ?? 100}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, brightness: val } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="对比度"
                        min={50}
                        max={150}
                        value={vidEl.contrast ?? 100}
                        displayValue={`${vidEl.contrast ?? 100}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, contrast: val } as EditorElement))
                        }
                      />
                      <InlineSlider
                        label="饱和度"
                        min={0}
                        max={200}
                        value={vidEl.saturate ?? 100}
                        displayValue={`${vidEl.saturate ?? 100}%`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, saturate: val } as EditorElement))
                        }
                      />
                    </details>

                    <details className="group/section space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <summary className="flex cursor-pointer list-none items-center justify-between text-[11px] font-semibold text-[#56526B] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] [&::-webkit-details-marker]:hidden">
                        边缘羽化遮罩
                        <span className="text-[10.5px] text-[#5E45F8] group-open/section:hidden">+ 展开</span>
                        <span className="hidden text-[10.5px] text-[#5E45F8] group-open/section:inline">− 收起</span>
                      </summary>
                      <div className="flex items-center justify-between text-[11px]">

                        {vidEl.feather && vidEl.feather.strength > 0 && (
                          <button
                            type="button"
                            onClick={() =>
                              onMutateSelected((el) => ({ ...el, feather: undefined } as EditorElement))
                            }
                            className="text-[10.5px] font-semibold text-[#5E45F8] hover:underline"
                          >
                            关闭羽化
                          </button>
                        )}
                      </div>
                      <div className="grid grid-cols-4 gap-1 text-[10.5px] font-semibold">
                        {(
                          [
                            { id: 'off' as const, label: '关闭' },
                            { id: 'all' as const, label: '四周' },
                            { id: 'left' as const, label: '左侧' },
                            { id: 'right' as const, label: '右侧' },
                            { id: 'top' as const, label: '顶部' },
                            { id: 'bottom' as const, label: '底部' },
                            { id: 'horizontal' as const, label: '左右' },
                            { id: 'vertical' as const, label: '上下' },
                          ] as const
                        ).map((opt) => {
                          const isActive =
                            opt.id === 'off'
                              ? !vidEl.feather || vidEl.feather.strength <= 0
                              : vidEl.feather?.mode === opt.id && vidEl.feather.strength > 0;
                          return (
                            <button
                              key={opt.id}
                              type="button"
                              onClick={() =>
                                onMutateSelected((el) => {
                                  if (opt.id === 'off') {
                                    return { ...el, feather: undefined } as EditorElement;
                                  }
                                  const prevStrength = (el as VideoElement).feather?.strength;
                                  return {
                                    ...el,
                                    feather: {
                                      mode: opt.id as ImageFeatherMode,
                                      strength: prevStrength && prevStrength > 0 ? prevStrength : 24,
                                    },
                                  } as EditorElement;
                                })
                              }
                              className={`rounded-lg border py-1 transition-colors whitespace-nowrap ${
                                isActive
                                  ? 'border-[#5E45F8] bg-[#EFEBFD] text-[#5E45F8]'
                                  : 'border-[#E6E4F2] bg-white text-[#6B6880] hover:border-[#C5BDF5] hover:text-[#1A172B]'
                              }`}
                            >
                              {opt.label}
                            </button>
                          );
                        })}
                      </div>
                      {vidEl.feather && vidEl.feather.strength > 0 && (
                        <InlineSlider
                          label="羽化范围"
                          min={4}
                          max={55}
                          step={1}
                          value={vidEl.feather.strength}
                          displayValue={`${vidEl.feather.strength}%`}
                          onChange={(val) =>
                            onMutateSelected((el) => {
                              const cur = el as VideoElement;
                              return {
                                ...cur,
                                feather: {
                                  mode: cur.feather?.mode ?? 'all',
                                  strength: val,
                                },
                              } as EditorElement;
                            })
                          }
                        />
                      )}
                    </details>

                    <ShadowControls
                      shadow={vidEl.shadow}
                      extractedPalette={extractedPalette}
                      onChange={(nextShadow) =>
                        onMutateSelected((el) => ({ ...el, shadow: nextShadow } as EditorElement))
                      }
                    />
                  </div>
                );
              })()}

              {single.kind === 'shape' && (() => {
                const shapeEl = single as ShapeElement;
                const sh = shapeEl.sh || 'rect';
                const supportsStroke =
                  sh === 'rect' || sh === 'circle' || sh === 'ring' || sh === 'brackets' || sh === 'dotline';
                const supportsFillEffects = sh === 'rect' || sh === 'circle';
                return (
                  <div className="space-y-2.5">
                    <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                      形状样式
                    </div>
                    <select
                      value={sh}
                      onChange={(e) =>
                        onMutateSelected((el) => ({ ...el, sh: e.target.value } as EditorElement))
                      }
                      className="w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]"
                    >
                      <option value="rect">实心矩形 / 胶囊细条</option>
                      <option value="circle">实心圆形 / 椭圆</option>
                      <option value="ring">几何圆环 / 椭圆环</option>
                      <option value="dash">虚线分割</option>
                      <option value="dots">点线装饰</option>
                      <option value="double">双线饰条</option>
                      <option value="brackets">边框角标</option>
                      <option value="dotline">圆点分隔线</option>
                    </select>
                    {sh === 'rect' && (
                      <InlineSlider
                        label="圆角半径"
                        min={0}
                        max={50}
                        value={shapeEl.radius}
                        displayValue={shapeEl.radius >= 50 ? '胶囊' : String(shapeEl.radius)}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, radius: val } as EditorElement))
                        }
                      />
                    )}
                    {supportsFillEffects && (
                      <>
                        <div className="flex items-center justify-between">
                          <span className="text-[11px] text-[#56526B]">填充模式</span>
                          <div className="flex rounded-lg border border-[#E6E4F2] bg-[#F5F4FA] p-0.5 text-[11px] font-semibold">
                            {(
                              [
                                { id: 'solid', label: '纯色填充' },
                                { id: 'gradient', label: '双色渐变' },
                              ] as const
                            ).map((mode) => (
                              <button
                                key={mode.id}
                                type="button"
                                onClick={() =>
                                  onMutateSelected((el) => ({
                                    ...el,
                                    fillMode: mode.id,
                                    ...(mode.id === 'gradient' && !(el as ShapeElement).color2
                                      ? { color2: '#38BDF8', gradAngle: 135 }
                                      : {}),
                                  } as EditorElement))
                                }
                                className={`rounded-md px-2 py-0.5 transition-colors ${
                                  (shapeEl.fillMode || 'solid') === mode.id
                                    ? 'bg-white text-[#5E45F8] shadow-xs'
                                    : 'text-[#6B6880]'
                                }`}
                              >
                                {mode.label}
                              </button>
                            ))}
                          </div>
                        </div>

                        {shapeEl.fillMode === 'gradient' && (
                          <div className="space-y-2 rounded-xl border border-[#D9D2FF] bg-[#EFEBFD]/55 p-2.5">
                            <div className="flex items-center justify-between">
                              <span className="text-[11px] font-medium text-[#4D34E6]">渐变终点色</span>
                              <div className="flex items-center gap-1.5">
                                <span className="font-mono-tabular text-[10.5px] text-[#4D34E6]">
                                  {(shapeEl.color2 || '#38BDF8').toUpperCase()}
                                </span>
                                <input
                                  type="color"
                                  value={shapeEl.color2 || '#38BDF8'}
                                  aria-label="形状渐变终点色"
                                  onChange={(e) =>
                                    onMutateSelected((el) => ({
                                      ...el,
                                      color2: e.target.value,
                                    } as EditorElement))
                                  }
                                  className="h-6 w-8 cursor-pointer rounded border border-[#D9D2FF]"
                                />
                              </div>
                            </div>
                            {extractedPalette.length > 0 && (
                              <div className="flex items-center justify-between gap-2">
                                <span className="text-[10px] font-semibold text-[#5E45F8]">推荐终点色</span>
                                <div className="flex items-center gap-1">
                                  {extractedPalette.map((c) => (
                                    <button
                                      key={c}
                                      type="button"
                                      onClick={() =>
                                        onMutateSelected((el) => ({ ...el, color2: c } as EditorElement))
                                      }
                                      className="h-4 w-4 rounded-full border border-white shadow-xs"
                                      style={{ backgroundColor: c }}
                                    />
                                  ))}
                                </div>
                              </div>
                            )}
                            <InlineSlider
                              label="渐变角度"
                              min={0}
                              max={360}
                              value={shapeEl.gradAngle ?? 135}
                              displayValue={`${shapeEl.gradAngle ?? 135}°`}
                              onChange={(val) =>
                                onMutateSelected((el) => ({ ...el, gradAngle: val } as EditorElement))
                              }
                            />
                          </div>
                        )}

                        <InlineSlider
                          label="背景毛玻璃"
                          min={0}
                          max={24}
                          step={1}
                          value={shapeEl.backdropBlur ?? 0}
                          displayValue={`${shapeEl.backdropBlur ?? 0}px`}
                          onChange={(val) =>
                            onMutateSelected((el) => ({ ...el, backdropBlur: val } as EditorElement))
                          }
                        />
                      </>
                    )}
                    {supportsStroke && (
                      <>
                        <InlineSlider
                          label={sh === 'rect' || sh === 'circle' ? '描边粗细' : '线条粗细'}
                          min={sh === 'rect' || sh === 'circle' ? 0 : 0.2}
                          max={4}
                          step={0.1}
                          value={shapeEl.bw ?? (sh === 'rect' || sh === 'circle' ? 0 : 0.8)}
                          displayValue={(shapeEl.bw ?? (sh === 'rect' || sh === 'circle' ? 0 : 0.8)).toFixed(1)}
                          onChange={(val) =>
                            onMutateSelected((el) => ({
                              ...el,
                              bw: val,
                              ...(val > 0 && !(el as ShapeElement).strokeColor
                                ? { strokeColor: '#ffffff' }
                                : {}),
                            } as EditorElement))
                          }
                        />
                        {(sh === 'rect' || sh === 'circle') && (shapeEl.bw ?? 0) > 0 && (
                          <div className="flex items-center justify-between">
                            <span className="text-[11px] text-[#56526B]">描边颜色</span>
                            <div className="flex items-center gap-2">
                              <span className="font-mono-tabular text-[11px] text-[#6B6880]">
                                {shapeEl.strokeColor || '#ffffff'}
                              </span>
                              <input
                                type="color"
                                value={shapeEl.strokeColor || '#ffffff'}
                                onChange={(e) =>
                                  onMutateSelected((el) => ({
                                    ...el,
                                    strokeColor: e.target.value,
                                  } as EditorElement))
                                }
                                className="h-6 w-8 cursor-pointer rounded-md border border-[#E6E4F2]"
                              />
                            </div>
                          </div>
                        )}
                      </>
                    )}
                    <ShadowControls
                      shadow={shapeEl.shadow}
                      extractedPalette={extractedPalette}
                      onChange={(nextShadow) =>
                        onMutateSelected((el) => ({ ...el, shadow: nextShadow } as EditorElement))
                      }
                    />
                  </div>
                );
              })()}

              {single.kind === 'motion' && (
                <div className="space-y-2">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    动态参数 · 开销 {(single as MotionElement).performanceCost}
                  </div>
                  <InlineSlider
                    label="动画速率"
                    min={0.4}
                    max={2.2}
                    step={0.1}
                    value={(single as MotionElement).speed}
                    displayValue={`${(single as MotionElement).speed.toFixed(1)}x`}
                    onChange={(val) =>
                      onMutateSelected((el) => ({ ...el, speed: val } as EditorElement))
                    }
                  />
                  <InlineSlider
                    label="密度强度"
                    min={20}
                    max={100}
                    value={(single as MotionElement).intensity}
                    displayValue={`${(single as MotionElement).intensity}%`}
                    onChange={(val) =>
                      onMutateSelected((el) => ({ ...el, intensity: val } as EditorElement))
                    }
                  />
                </div>
              )}

              {single.kind === 'widget' && (
                <div className="space-y-2.5">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    组件参数 · v{(single as WidgetElement).widgetVersion}
                  </div>
                  <div>
                    <label className="text-[11px] text-[#56526B]">显示标题</label>
                    <input
                      type="text"
                      value={(single as WidgetElement).label || ''}
                      onChange={(e) =>
                        onMutateSelected((el) => ({ ...el, label: e.target.value } as EditorElement))
                      }
                      className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                    />
                  </div>
                  {((single as WidgetElement).widgetType === 'progress' ||
                    (single as WidgetElement).widgetType === 'ring-progress') && (
                    <InlineSlider
                      label="数值比例"
                      min={0}
                      max={100}
                      value={(single as WidgetElement).value ?? 72}
                      displayValue={`${(single as WidgetElement).value ?? 72}%`}
                      onChange={(val) =>
                        onMutateSelected((el) => ({ ...el, value: val } as EditorElement))
                      }
                    />
                  )}
                  {(single as WidgetElement).widgetType === 'countdown' && (
                    <div>
                      <label className="text-[11px] text-[#56526B]">目标倒计时文案</label>
                      <input
                        type="text"
                        value={(single as WidgetElement).targetLabel || '12 天'}
                        onChange={(e) =>
                          onMutateSelected((el) => ({
                            ...el,
                            targetLabel: e.target.value,
                          } as EditorElement))
                        }
                        className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                      />
                    </div>
                  )}
                  {(single as WidgetElement).widgetType === 'audio-spectrum' && (
                    <InlineSlider
                      label="频谱柱数"
                      min={8}
                      max={24}
                      step={2}
                      value={(single as WidgetElement).barCount ?? 16}
                      displayValue={`${(single as WidgetElement).barCount ?? 16} 根`}
                      onChange={(val) =>
                        onMutateSelected((el) => ({ ...el, barCount: val } as EditorElement))
                      }
                    />
                  )}
                  {(single as WidgetElement).widgetType === 'weather' && (
                    <div>
                      <label className="text-[11px] text-[#56526B]">城市名称</label>
                      <input
                        type="text"
                        value={(single as WidgetElement).city || '上海'}
                        onChange={(e) =>
                          onMutateSelected((el) => ({ ...el, city: e.target.value } as EditorElement))
                        }
                        className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                      />
                    </div>
                  )}
                  {(single as WidgetElement).widgetType === 'clock-widget' && (
                    <div className="space-y-1.5">
                      {[
                        { key: 'showSeconds' as const, label: '显示秒数' },
                        { key: 'showDate' as const, label: '显示日期星期' },
                        { key: 'use24Hour' as const, label: '24 小时制' },
                      ].map((opt) => {
                        const checked = (single as WidgetElement)[opt.key] !== false;
                        return (
                          <label
                            key={opt.key}
                            className="flex items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]"
                          >
                            <span className="text-[#56526B]">{opt.label}</span>
                            <input
                              type="checkbox"
                              className="accent-[#5E45F8]"
                              checked={checked}
                              onChange={(e) =>
                                onMutateSelected((el) => ({
                                  ...el,
                                  [opt.key]: e.target.checked,
                                } as EditorElement))
                              }
                            />
                          </label>
                        );
                      })}
                    </div>
                  )}
                  {(single as WidgetElement).widgetType === 'telemetry-bar' && (
                    <div className="space-y-2">
                      {[
                        { key: 'showTemp' as const, label: '显示核心温度 (°C)' },
                        { key: 'showMemory' as const, label: '显示内存占用' },
                      ].map((opt) => {
                        const checked = (single as WidgetElement)[opt.key] !== false;
                        return (
                          <label
                            key={opt.key}
                            className="flex items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]"
                          >
                            <span className="text-[#56526B]">{opt.label}</span>
                            <input
                              type="checkbox"
                              className="accent-[#5E45F8]"
                              checked={checked}
                              onChange={(e) =>
                                onMutateSelected((el) => ({
                                  ...el,
                                  [opt.key]: e.target.checked,
                                } as EditorElement))
                              }
                            />
                          </label>
                        );
                      })}
                      <div>
                        <span className="text-[11px] text-[#56526B]">采集刷新频率</span>
                        <div className="mt-1 grid grid-cols-3 gap-1">
                          {[1, 2, 5].map((sec) => (
                            <button
                              key={sec}
                              type="button"
                              onClick={() =>
                                onMutateSelected((el) => ({
                                  ...el,
                                  refreshSeconds: sec,
                                } as EditorElement))
                              }
                              className={`rounded-lg py-1 font-mono-tabular text-[11px] font-semibold ${
                                ((single as WidgetElement).refreshSeconds ?? 2) === sec
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {sec}s
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  )}
                  {(single as WidgetElement).widgetType === 'audio-rhythm' && (
                    <div className="space-y-2">
                      <label className="flex items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]">
                        <span className="text-[#56526B]">显示正在播放曲目</span>
                        <input
                          type="checkbox"
                          className="accent-[#5E45F8]"
                          checked={(single as WidgetElement).showTrackName !== false}
                          onChange={(e) =>
                            onMutateSelected((el) => ({
                              ...el,
                              showTrackName: e.target.checked,
                            } as EditorElement))
                          }
                        />
                      </label>
                      <div>
                        <label className="text-[11px] text-[#56526B]">曲目胶囊文案</label>
                        <input
                          type="text"
                          value={(single as WidgetElement).trackName ?? 'Midnight Lo-Fi · 小屏宝PixBuddy'}
                          onChange={(e) =>
                            onMutateSelected((el) => ({
                              ...el,
                              trackName: e.target.value,
                            } as EditorElement))
                          }
                          className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                      <InlineSlider
                        label="频谱柱数"
                        min={8}
                        max={28}
                        step={2}
                        value={(single as WidgetElement).barCount ?? 20}
                        displayValue={`${(single as WidgetElement).barCount ?? 20} 根`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, barCount: val } as EditorElement))
                        }
                      />
                    </div>
                  )}
                  {(single as WidgetElement).widgetType === 'pomodoro-widget' && (
                    <div className="space-y-2">
                      <div>
                        <span className="text-[11px] text-[#56526B]">专注时长</span>
                        <div className="mt-1 grid grid-cols-3 gap-1">
                          {[15, 25, 45].map((mins) => (
                            <button
                              key={mins}
                              type="button"
                              onClick={() =>
                                onMutateSelected((el) => ({
                                  ...el,
                                  focusMinutes: mins,
                                } as EditorElement))
                              }
                              className={`rounded-lg py-1 font-mono-tabular text-[11px] font-semibold ${
                                ((single as WidgetElement).focusMinutes ?? 25) === mins
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {mins} 分钟
                            </button>
                          ))}
                        </div>
                      </div>
                      <InlineSlider
                        label="休息时长"
                        min={3}
                        max={20}
                        value={(single as WidgetElement).breakMinutes ?? 5}
                        displayValue={`${(single as WidgetElement).breakMinutes ?? 5} 分钟`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({ ...el, breakMinutes: val } as EditorElement))
                        }
                      />
                    </div>
                  )}
                  {(single as WidgetElement).widgetType === 'weather-capsule' && (
                    <div className="space-y-2">
                      <div>
                        <label className="text-[11px] text-[#56526B]">监测城市</label>
                        <input
                          type="text"
                          value={(single as WidgetElement).city || '上海'}
                          onChange={(e) =>
                            onMutateSelected((el) => ({ ...el, city: e.target.value } as EditorElement))
                          }
                          className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                      <label className="flex items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]">
                        <span className="text-[#56526B]">显示空气质量 (AQI)</span>
                        <input
                          type="checkbox"
                          className="accent-[#5E45F8]"
                          checked={(single as WidgetElement).showAqi !== false}
                          onChange={(e) =>
                            onMutateSelected((el) => ({
                              ...el,
                              showAqi: e.target.checked,
                            } as EditorElement))
                          }
                        />
                      </label>
                    </div>
                  )}
                  {(single as WidgetElement).widgetType === 'sticky-note' && (
                    <div>
                      <label className="text-[11px] text-[#56526B]">副屏常驻便签文案</label>
                      <textarea
                        rows={3}
                        maxLength={80}
                        value={(single as WidgetElement).noteText ?? '完成 D92 视觉方案评审 · 19:00'}
                        onChange={(e) =>
                          onMutateSelected((el) => ({
                            ...el,
                            noteText: e.target.value,
                          } as EditorElement))
                        }
                        className="mt-1 w-full resize-none rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                      />
                    </div>
                  )}
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <span className="text-[11px] text-[#56526B]">主题色</span>
                      <input
                        type="color"
                        value={(single as WidgetElement).accentColor}
                        onChange={(e) =>
                          onMutateSelected((el) => ({ ...el, accentColor: e.target.value } as EditorElement))
                        }
                        className="h-7 w-10 cursor-pointer rounded-lg border border-[#E6E4F2]"
                      />
                    </div>
                    {extractedPalette.length > 0 && (
                      <div className="flex items-center justify-between gap-2 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5">
                        <span className="shrink-0 text-[10.5px] font-semibold text-[#5E45F8]">
                          背景推荐色
                        </span>
                        <div className="flex items-center gap-1">
                          {extractedPalette.map((c) => (
                            <button
                              key={c}
                              type="button"
                              onClick={() =>
                                onMutateSelected((el) => ({ ...el, accentColor: c } as EditorElement))
                              }
                              className={`h-4.5 w-4.5 rounded-full border ${
                                (single as WidgetElement).accentColor.toLowerCase() === c.toLowerCase()
                                  ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/30'
                                  : 'border-white shadow-xs'
                              }`}
                              style={{ backgroundColor: c }}
                            />
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                  <InlineSlider
                    label="卡片底色"
                    min={25}
                    max={95}
                    value={(single as WidgetElement).bgOpacity}
                    displayValue={`${(single as WidgetElement).bgOpacity}%`}
                    onChange={(val) =>
                      onMutateSelected((el) => ({ ...el, bgOpacity: val } as EditorElement))
                    }
                  />
                  <ShadowControls
                    shadow={(single as WidgetElement).shadow}
                    extractedPalette={extractedPalette}
                    onChange={(nextShadow) =>
                      onMutateSelected((el) => ({ ...el, shadow: nextShadow } as EditorElement))
                    }
                  />
                </div>
              )}

              {single.kind === 'plugin' && (
                <div className="space-y-2.5">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    插件参数配置 · v{(single as PluginElement).pluginVersion}
                  </div>
                  {Object.entries((single as PluginElement).config).map(([k, val]) => {
                    const meta = PLUGIN_CONFIG_FIELD_META[k];
                    const label = meta?.label ?? k;
                    if (meta?.type === 'boolean' || typeof val === 'boolean') {
                      return (
                        <label
                          key={k}
                          className="flex items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]"
                        >
                          <span className="text-[#56526B]">{label}</span>
                          <input
                            type="checkbox"
                            className="accent-[#5E45F8]"
                            checked={Boolean(val)}
                            onChange={(e) =>
                              onMutateSelected((el) => {
                                const pe = el as PluginElement;
                                return { ...pe, config: { ...pe.config, [k]: e.target.checked } };
                              })
                            }
                          />
                        </label>
                      );
                    }
                    if (meta?.type === 'number') {
                      const numVal = Number(val) || meta.min;
                      return (
                        <InlineSlider
                          key={k}
                          label={label}
                          min={meta.min}
                          max={meta.max}
                          step={meta.step ?? 1}
                          value={numVal}
                          displayValue={`${numVal}${meta.unit || ''}`}
                          onChange={(nextNum) =>
                            onMutateSelected((el) => {
                              const pe = el as PluginElement;
                              return { ...pe, config: { ...pe.config, [k]: nextNum } };
                            })
                          }
                        />
                      );
                    }
                    if (meta?.type === 'select') {
                      return (
                        <div key={k} className="flex items-center justify-between gap-2">
                          <span className="text-[11px] text-[#56526B]">{label}</span>
                          <select
                            value={String(val)}
                            onChange={(e) =>
                              onMutateSelected((el) => {
                                const pe = el as PluginElement;
                                return { ...pe, config: { ...pe.config, [k]: e.target.value } };
                              })
                            }
                            className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px]"
                          >
                            {meta.options.map((opt) => (
                              <option key={opt.value} value={opt.value}>
                                {opt.label}
                              </option>
                            ))}
                          </select>
                        </div>
                      );
                    }
                    if (meta?.type === 'textarea') {
                      return (
                        <div key={k}>
                          <label className="text-[11px] text-[#56526B]">{label}</label>
                          <textarea
                            rows={3}
                            value={String(val)}
                            onChange={(e) =>
                              onMutateSelected((el) => {
                                const pe = el as PluginElement;
                                return { ...pe, config: { ...pe.config, [k]: e.target.value } };
                              })
                            }
                            className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                          />
                        </div>
                      );
                    }
                    return (
                      <div key={k}>
                        <label className="text-[11px] text-[#56526B]">{label}</label>
                        <input
                          type="text"
                          value={String(val)}
                          onChange={(e) =>
                            onMutateSelected((el) => {
                              const pe = el as PluginElement;
                              return { ...pe, config: { ...pe.config, [k]: e.target.value } };
                            })
                          }
                          className="mt-0.5 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                    );
                  })}
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <span className="text-[11px] text-[#56526B]">强调色</span>
                      <input
                        type="color"
                        value={(single as PluginElement).accentColor}
                        onChange={(e) =>
                          onMutateSelected((el) => ({ ...el, accentColor: e.target.value } as EditorElement))
                        }
                        className="h-7 w-10 cursor-pointer rounded-lg border border-[#E6E4F2]"
                      />
                    </div>
                    {extractedPalette.length > 0 && (
                      <div className="flex items-center justify-between gap-2 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5">
                        <span className="shrink-0 text-[10.5px] font-semibold text-[#5E45F8]">
                          背景推荐色
                        </span>
                        <div className="flex items-center gap-1">
                          {extractedPalette.map((c) => (
                            <button
                              key={c}
                              type="button"
                              onClick={() =>
                                onMutateSelected((el) => ({ ...el, accentColor: c } as EditorElement))
                              }
                              className={`h-4.5 w-4.5 rounded-full border ${
                                (single as PluginElement).accentColor.toLowerCase() === c.toLowerCase()
                                  ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/30'
                                  : 'border-white shadow-xs'
                              }`}
                              style={{ backgroundColor: c }}
                            />
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                  <InlineSlider
                    label="卡片底色"
                    min={30}
                    max={95}
                    value={(single as PluginElement).bgOpacity ?? 80}
                    displayValue={`${(single as PluginElement).bgOpacity ?? 80}%`}
                    onChange={(val) =>
                      onMutateSelected((el) => ({ ...el, bgOpacity: val } as EditorElement))
                    }
                  />
                  <ShadowControls
                    shadow={(single as PluginElement).shadow}
                    extractedPalette={extractedPalette}
                    onChange={(nextShadow) =>
                      onMutateSelected((el) => ({ ...el, shadow: nextShadow } as EditorElement))
                    }
                  />
                </div>
              )}

              {/* APPEARANCE SECTION */}
              <div className="space-y-2.5 border-t border-[#E6E4F2] pt-3">
                <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                  外观样式
                </div>

                {(single.kind === 'text' || single.kind === 'date' || single.kind === 'clock') && (
                  <>
                    {!(single.kind === 'clock' && (single as ClockElement).style === 'analog') && (
                      <>
                        <div>
                          <label className="text-[11px] text-[#56526B]">字体 (跨设备安全映射)</label>
                          <VisualFontSelect
                            value={single.font || ''}
                            onChange={(fontId) =>
                              onMutateSelected((el) => ({ ...el, font: fontId }))
                            }
                          />
                        </div>

                        <div className="flex items-center justify-between">
                          <span className="text-[11px] text-[#56526B]">字重</span>
                          <div className="flex rounded-lg border border-[#E6E4F2] bg-[#F5F4FA] p-0.5 text-[11px]">
                            {[300, 500, 700].map((wt) => (
                              <button
                                key={wt}
                                type="button"
                                onClick={() => onMutateSelected((el) => ({ ...el, weight: wt }))}
                                className={`rounded-md px-2.5 py-0.5 font-semibold transition-colors ${
                                  single.weight === wt
                                    ? 'bg-white text-[#5E45F8] shadow-xs'
                                    : 'text-[#6B6880]'
                                }`}
                              >
                                {wt === 300 ? '细' : wt === 500 ? '常规' : '粗'}
                              </button>
                            ))}
                          </div>
                        </div>

                        <InlineSlider
                          label="字间距"
                          min={-5}
                          max={60}
                          value={single.ls ?? 0}
                          displayValue={`${single.ls ?? 0}%`}
                          onChange={(val) =>
                            onMutateSelected((el) => ({ ...el, ls: val }))
                          }
                        />
                      </>
                    )}

                    <div className="flex items-center justify-between">
                      <span className="text-[11px] text-[#56526B]">文字效果</span>
                      <select
                        value={single.fx || ''}
                        onChange={(e) =>
                          onMutateSelected((el) => ({
                            ...el,
                            fx: e.target.value as TextElement['fx'],
                            ...(e.target.value === 'gradient' && el.kind === 'text'
                              ? { g1: el.g1 || '#A78BFA', g2: el.g2 || '#5E45F8', gAngle: el.gAngle ?? 120 }
                              : {}),
                          }))
                        }
                        className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11px]"
                      >
                        <option value="">无效果</option>
                        <option value="glow">霓虹发光</option>
                        <option value="shadow">立体阴影</option>
                        <option value="outline">空心描边</option>
                        {single.kind === 'text' && <option value="gradient">极光渐变字</option>}
                      </select>
                    </div>

                    {single.kind === 'text' && single.fx === 'gradient' && (
                      <div className="space-y-2 rounded-xl border border-[#D9D2FF] bg-[#EFEBFD]/55 p-2.5">
                        <div className="flex items-center justify-between">
                          <label className="flex items-center gap-1.5 text-[11px] font-medium text-[#4D34E6]">
                            渐变起点
                            <input
                              type="color"
                              value={(single as TextElement).g1 || '#A78BFA'}
                              onChange={(e) =>
                                onMutateSelected((el) => ({
                                  ...el,
                                  g1: e.target.value,
                                } as EditorElement))
                              }
                              className="h-6 w-8 cursor-pointer rounded border border-[#D9D2FF]"
                            />
                          </label>
                          <label className="flex items-center gap-1.5 text-[11px] font-medium text-[#4D34E6]">
                            渐变终点
                            <input
                              type="color"
                              value={(single as TextElement).g2 || '#5E45F8'}
                              onChange={(e) =>
                                onMutateSelected((el) => ({
                                  ...el,
                                  g2: e.target.value,
                                } as EditorElement))
                              }
                              className="h-6 w-8 cursor-pointer rounded border border-[#D9D2FF]"
                            />
                          </label>
                        </div>
                        <InlineSlider
                          label="渐变方向"
                          min={0}
                          max={360}
                          value={(single as TextElement).gAngle ?? 120}
                          displayValue={`${(single as TextElement).gAngle ?? 120}°`}
                          onChange={(val) =>
                            onMutateSelected((el) => ({
                              ...el,
                              gAngle: val,
                            } as EditorElement))
                          }
                        />
                        <div className="grid grid-cols-5 gap-1">
                          {[0, 45, 90, 120, 180].map((deg) => (
                            <button
                              key={deg}
                              type="button"
                              onClick={() =>
                                onMutateSelected((el) => ({ ...el, gAngle: deg } as EditorElement))
                              }
                              className={`rounded-md border py-0.5 font-mono-tabular text-[10px] font-semibold ${
                                ((single as TextElement).gAngle ?? 120) === deg
                                  ? 'border-[#5E45F8] bg-white text-[#5E45F8]'
                                  : 'border-[#D9D2FF] text-[#4D34E6] hover:bg-white/80'
                              }`}
                            >
                              {deg}°
                            </button>
                          ))}
                        </div>
                      </div>
                    )}

                    {single.kind !== 'clock' && (
                      <label className="flex items-center justify-between text-[11.5px]">
                        <span>竖排排版</span>
                        <input
                          type="checkbox"
                          className="accent-[#5E45F8]"
                          checked={Boolean(single.vert)}
                          onChange={(e) =>
                            onMutateSelected((el) => ({ ...el, vert: e.target.checked }))
                          }
                        />
                      </label>
                    )}
                  </>
                )}

                {single.kind !== 'image' && single.kind !== 'video' && (
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between text-[11px] text-[#56526B]">
                      <span>主颜色</span>
                      <span className="font-mono-tabular">{single.color}</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <input
                        type="color"
                        value={single.color}
                        onChange={(e) => onMutateSelected((el) => ({ ...el, color: e.target.value }))}
                        className="h-6 w-8 cursor-pointer rounded-md border border-[#E6E4F2]"
                      />
                      {QUICK_COLORS.map((c) => (
                        <button
                          key={c}
                          type="button"
                          onClick={() => onMutateSelected((el) => ({ ...el, color: c }))}
                          className={`h-5 w-5 rounded-full border ${
                            single.color.toLowerCase() === c.toLowerCase()
                              ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/30'
                              : 'border-[#DAD6F0]'
                          }`}
                          style={{ backgroundColor: c }}
                        />
                      ))}
                    </div>
                    {extractedPalette.length > 0 && (
                      <div className="flex items-center justify-between gap-2 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5">
                        <span className="shrink-0 text-[10.5px] font-semibold text-[#5E45F8]">
                          背景推荐色
                        </span>
                        <div className="flex items-center gap-1">
                          {extractedPalette.map((c) => (
                            <button
                              key={c}
                              type="button"
                              title={`使用背景推荐色 ${c}`}
                              onClick={() => onMutateSelected((el) => ({ ...el, color: c }))}
                              className={`h-4.5 w-4.5 rounded-full border ${
                                single.color.toLowerCase() === c.toLowerCase()
                                  ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/30'
                                  : 'border-white shadow-xs'
                              }`}
                              style={{ backgroundColor: c }}
                            />
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* Single-line Opacity Slider (Reference Image 1) */}
                <InlineSlider
                  label="不透明度"
                  min={10}
                  max={100}
                  step={1}
                  value={Math.round(single.alpha * 100)}
                  displayValue={String(Math.round(single.alpha * 100))}
                  onChange={(val) =>
                    onMutateSelected((el) => ({ ...el, alpha: val / 100 }))
                  }
                />
              </div>

              {/* POSITION & SIZE SECTION */}
              {(() => {
                const isBoxElement =
                  single.kind === 'shape' ||
                  single.kind === 'motion' ||
                  single.kind === 'widget' ||
                  single.kind === 'plugin' ||
                  single.kind === 'image' ||
                  single.kind === 'video';

                const resolveBoxDims = (el: EditorElement): [number, number] => {
                  if ('w' in el && typeof el.w === 'number' && 'h' in el && typeof el.h === 'number') {
                    return [el.w, el.h];
                  }
                  const pxH = (el.size * minDim) / 100;
                  const ar = 'ar' in el && el.ar ? el.ar : 1;
                  const pxW = pxH * ar;
                  return [
                    +((pxW / layoutSize[0]) * 100).toFixed(1),
                    +((pxH / layoutSize[1]) * 100).toFixed(1),
                  ];
                };

                const [boxW, boxH] = isBoxElement ? resolveBoxDims(single) : [0, 0];
                const minBoxPct =
                  single.kind === 'shape'
                    ? 0.2
                    : single.kind === 'image' || single.kind === 'video'
                      ? 0.5
                      : single.kind === 'motion'
                        ? 1
                        : 2;

                return (
                  <div className="space-y-2.5 border-t border-[#E6E4F2] pt-3">
                    <div className="flex items-center justify-between">
                      <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                        位置、尺寸与旋转
                      </span>
                      {isBoxElement && (
                        <span className="font-mono-tabular text-[10.5px] font-medium text-[#8E89A6]">
                          {Math.round((boxW * layoutSize[0]) / 100)} × {Math.round((boxH * layoutSize[1]) / 100)} px
                        </span>
                      )}
                    </div>

                    {(single.kind === 'text' || single.kind === 'date' || single.kind === 'clock') && (
                      <InlineSlider
                        label="字号大小"
                        min={12}
                        max={Math.round(minDim * 1.2)}
                        value={Math.round((single.size * minDim) / 100)}
                        displayValue={`${Math.round((single.size * minDim) / 100)}`}
                        onChange={(val) =>
                          onMutateSelected((el) => ({
                            ...el,
                            size: +((val * 100) / minDim).toFixed(1),
                          }))
                        }
                      />
                    )}

                    {single.kind === 'text' && (() => {
                      const txtEl = single as TextElement;
                      const isVert = Boolean(txtEl.vert);
                      const activeDim = isVert ? txtEl.h : txtEl.w;
                      return (
                        <div className="flex w-full min-w-0 items-center gap-2">
                          <span className="w-[62px] shrink-0 text-[11px] font-medium text-[#56526B]">
                            {isVert ? '竖排框高' : '文本框宽'}
                          </span>
                          {activeDim != null ? (
                            <div className="flex min-w-0 flex-1 items-center gap-1.5">
                              <ScrubNumberInput
                                value={activeDim}
                                suffix={isVert ? 'H%' : 'W%'}
                                min={3}
                                max={100}
                                decimals={1}
                                dragRatio={0.25}
                                ariaLabel={isVert ? '竖排文本框高度' : '文本框宽度'}
                                onChange={(val, recordHistory = true) =>
                                  onMutateSelected(
                                    (el) => ({ ...el, [isVert ? 'h' : 'w']: val } as EditorElement),
                                    undefined,
                                    recordHistory
                                  )
                                }
                              />
                              <button
                                type="button"
                                onClick={() =>
                                  onMutateSelected((el) => ({
                                    ...el,
                                    [isVert ? 'h' : 'w']: undefined,
                                  } as EditorElement))
                                }
                                className="shrink-0 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1.5 text-[10.5px] font-semibold text-[#6B6880] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                              >
                                自适应
                              </button>
                            </div>
                          ) : (
                            <div className="flex min-w-0 flex-1 items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5">
                              <span className="text-[11px] text-[#6B6880]">自适应不折行</span>
                              <button
                                type="button"
                                onClick={() =>
                                  onMutateSelected((el) => ({
                                    ...el,
                                    [isVert ? 'h' : 'w']: isVert ? 60 : 40,
                                  } as EditorElement))
                                }
                                className="text-[11px] font-semibold text-[#5E45F8] hover:underline"
                              >
                                固定{isVert ? '高度' : '宽度'}
                              </button>
                            </div>
                          )}
                        </div>
                      );
                    })()}

                    {isBoxElement && (
                      <div className="space-y-2">
                        <div className="flex w-full min-w-0 items-center gap-2">
                          <button
                            type="button"
                            onClick={onToggleAspectLink}
                            title={aspectLinked ? '等比锁定中（点击解锁自由宽高）' : '自由宽高（点击锁定比例）'}
                            className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border ${
                              aspectLinked
                                ? 'border-[#5E45F8] bg-[#EFEBFD] text-[#5E45F8]'
                                : 'border-[#E6E4F2] text-[#6B6880] hover:border-[#C5BDF5]'
                            }`}
                          >
                            {aspectLinked ? <Link2 className="h-3.5 w-3.5" /> : <Link2Off className="h-3.5 w-3.5" />}
                          </button>
                          <ScrubNumberInput
                            value={boxW}
                            suffix="W%"
                            min={minBoxPct}
                            max={1000}
                            decimals={1}
                            dragRatio={0.25}
                            ariaLabel="宽度百分比"
                            onChange={(nw, recordHistory = true) =>
                              onMutateSelected(
                                (el) => {
                                  const [curW, curH] = resolveBoxDims(el);
                                  const ratio = curH / Math.max(0.2, curW);
                                  const nextH = aspectLinked
                                    ? Math.max(minBoxPct, Math.min(1000, +(nw * ratio).toFixed(1)))
                                    : curH;
                                  return {
                                    ...el,
                                    w: nw,
                                    h: nextH,
                                    ...(el.kind === 'image' || el.kind === 'video'
                                      ? { size: +Math.max(2, (nextH * layoutSize[1]) / minDim).toFixed(1) }
                                      : {}),
                                  } as EditorElement;
                                },
                                undefined,
                                recordHistory
                              )
                            }
                          />
                          <ScrubNumberInput
                            value={boxH}
                            suffix="H%"
                            min={minBoxPct}
                            max={1000}
                            decimals={1}
                            dragRatio={0.25}
                            ariaLabel="高度百分比"
                            onChange={(nh, recordHistory = true) =>
                              onMutateSelected(
                                (el) => {
                                  const [curW, curH] = resolveBoxDims(el);
                                  const ratio = curW / Math.max(0.2, curH);
                                  const nextW = aspectLinked
                                    ? Math.max(minBoxPct, Math.min(1000, +(nh * ratio).toFixed(1)))
                                    : curW;
                                  return {
                                    ...el,
                                    h: nh,
                                    w: nextW,
                                    ...(el.kind === 'image' || el.kind === 'video'
                                      ? { size: +Math.max(2, (nh * layoutSize[1]) / minDim).toFixed(1) }
                                      : {}),
                                  } as EditorElement;
                                },
                                undefined,
                                recordHistory
                              )
                            }
                          />
                        </div>

                        <div className={single.kind === 'image' || single.kind === 'video' ? 'grid grid-cols-3 gap-1.5' : 'grid grid-cols-2 gap-1.5'}>
                          <button
                            type="button"
                            title="按素材原始像素尺寸显示"
                            onClick={async () => {
                              if (single.kind === 'image' || single.kind === 'video') {
                                let nw = single.sourceMeta?.naturalWidth;
                                let nh = single.sourceMeta?.naturalHeight;
                                if (!nw || !nh) {
                                  if (single.kind === 'image' && single.src) {
                                    const dims = await readImageDimensions(single.src);
                                    nw = dims.w;
                                    nh = dims.h;
                                  } else if (single.kind === 'video' && single.src) {
                                    const dims = await readVideoDimensions(single.src);
                                    nw = dims.w;
                                    nh = dims.h;
                                  }
                                }
                                const resolvedW = nw || Math.max(1, Math.round((boxW * layoutSize[0]) / 100));
                                const resolvedH = nh || Math.max(1, Math.round((boxH * layoutSize[1]) / 100));
                                const box = computeNaturalBoxPct(
                                  resolvedW,
                                  resolvedH,
                                  layoutSize[0],
                                  layoutSize[1]
                                );
                                onMutateSelected((el) => {
                                  if (el.kind !== 'image' && el.kind !== 'video') return el;
                                  return {
                                    ...el,
                                    w: box.w,
                                    h: box.h,
                                    ar: box.ar,
                                    size: box.size,
                                    fit: 'contain',
                                    sourceMeta: {
                                      ...(el.sourceMeta ?? { origin: 'local' }),
                                      naturalWidth: resolvedW,
                                      naturalHeight: resolvedH,
                                    },
                                  } as EditorElement;
                                });
                                onNotify?.(`已按原尺寸显示 (${resolvedW}×${resolvedH}px)`);
                                return;
                              }

                              if (single.kind === 'motion') {
                                onMutateSelected(
                                  (el) => ({ ...el, w: 100, h: 100 } as EditorElement),
                                  '已恢复默认尺寸 (100% × 100%)'
                                );
                              } else if (single.kind === 'widget') {
                                const spec = WIDGET_CATALOG.find((w) => w.type === single.widgetType);
                                const dw = spec?.defaultW ?? 28;
                                const dh = spec?.defaultH ?? 34;
                                onMutateSelected(
                                  (el) => ({ ...el, w: dw, h: dh } as EditorElement),
                                  '已恢复组件默认尺寸'
                                );
                              } else if (single.kind === 'plugin') {
                                const cap = PLUGIN_CAPABILITY_REGISTRY.find(
                                  (c) => c.key === single.capabilityKey
                                );
                                const dw = cap?.defaultW ?? 30;
                                const dh = cap?.defaultH ?? 34;
                                onMutateSelected(
                                  (el) => ({ ...el, w: dw, h: dh } as EditorElement),
                                  '已恢复插件默认尺寸'
                                );
                              } else if (single.kind === 'shape') {
                                const sh = single.sh || 'rect';
                                const isSquareLike = sh === 'circle' || sh === 'ring' || sh === 'brackets';
                                const sqPx = Math.round(minDim * 0.4);
                                const dw = isSquareLike ? +((sqPx / layoutSize[0]) * 100).toFixed(2) : 30;
                                const dh = isSquareLike ? +((sqPx / layoutSize[1]) * 100).toFixed(2) : 10;
                                onMutateSelected(
                                  (el) => ({ ...el, w: dw, h: dh } as EditorElement),
                                  '已恢复形状默认尺寸'
                                );
                              }
                            }}
                            className="w-full min-w-0 truncate rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1.5 text-[11px] font-semibold text-[#1A172B] transition-colors hover:border-[#5E45F8] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                          >
                            {single.kind === 'image' || single.kind === 'video' ? '原始尺寸' : '按默认尺寸'}
                          </button>

                          <button
                            type="button"
                            title="等比缩放并居中，让完整内容放入画布"
                            onClick={async () => {
                              if (single.kind === 'image' || single.kind === 'video') {
                                let nw = single.sourceMeta?.naturalWidth;
                                let nh = single.sourceMeta?.naturalHeight;
                                if (!nw || !nh) {
                                  if (single.kind === 'image' && single.src) {
                                    const dims = await readImageDimensions(single.src);
                                    nw = dims.w;
                                    nh = dims.h;
                                  } else if (single.kind === 'video' && single.src) {
                                    const dims = await readVideoDimensions(single.src);
                                    nw = dims.w;
                                    nh = dims.h;
                                  }
                                }
                                const resolvedW = nw || Math.max(1, (boxW * layoutSize[0]) / 100);
                                const resolvedH = nh || Math.max(1, (boxH * layoutSize[1]) / 100);
                                const box = computeFit100BoxPct(
                                  resolvedW,
                                  resolvedH,
                                  layoutSize[0],
                                  layoutSize[1]
                                );
                                onMutateSelected((el) => {
                                  if (el.kind !== 'image' && el.kind !== 'video') return el;
                                  return {
                                    ...el,
                                    w: box.w,
                                    h: box.h,
                                    ar: box.ar,
                                    size: box.size,
                                    fit: 'contain',
                                    a: 'mc',
                                    x: 0,
                                    y: 0,
                                    sourceMeta: {
                                      ...(el.sourceMeta ?? { origin: 'local' }),
                                      naturalWidth: Math.round(resolvedW),
                                      naturalHeight: Math.round(resolvedH),
                                    },
                                  } as EditorElement;
                                });
                                onNotify?.('已按屏幕 100% 比例等比适配并居中');
                                return;
                              }

                              if (single.kind === 'motion') {
                                onMutateSelected(
                                  (el) => ({ ...el, w: 100, h: 100, a: 'mc', x: 0, y: 0 } as EditorElement),
                                  '已按 100% 屏幕铺满并居中'
                                );
                              } else {
                                const curPxW = Math.max(1, (boxW * layoutSize[0]) / 100);
                                const curPxH = Math.max(1, (boxH * layoutSize[1]) / 100);
                                const box = computeFit100BoxPct(
                                  curPxW,
                                  curPxH,
                                  layoutSize[0],
                                  layoutSize[1]
                                );
                                onMutateSelected(
                                  (el) => ({
                                    ...el,
                                    w: box.w,
                                    h: box.h,
                                    a: 'mc',
                                    x: 0,
                                    y: 0,
                                  } as EditorElement),
                                  '已按屏幕 100% 比例等比缩放并居中'
                                );
                              }
                            }}
                            className="w-full min-w-0 truncate rounded-xl border border-[#D9D2FF] bg-[#EFEBFD] px-2 py-1.5 text-[11px] font-semibold text-[#5E45F8] transition-colors hover:bg-[#5E45F8] hover:text-white"
                          >
                            完整显示
                          </button>
                          {(single.kind === 'image' || single.kind === 'video') && (
                            <button
                              type="button"
                              title="等比缩放至覆盖整个画布并居中，超出部分可拖动查看"
                              onClick={async () => {
                                try {
                                  let nw = single.sourceMeta?.naturalWidth;
                                  let nh = single.sourceMeta?.naturalHeight;
                                  if ((!nw || !nh) && single.src) {
                                    const dims = single.kind === 'image'
                                      ? await readImageDimensions(single.src)
                                      : await readVideoDimensions(single.src);
                                    nw = dims.w;
                                    nh = dims.h;
                                  }
                                  const sourceW = nw || Math.max(1, boxW * layoutSize[0] / 100);
                                  const sourceH = nh || Math.max(1, boxH * layoutSize[1] / 100);
                                  const scale = Math.max(layoutSize[0] / sourceW, layoutSize[1] / sourceH);
                                  const width = sourceW * scale;
                                  const height = sourceH * scale;
                                  onMutateSelected((el) => {
                                    if (el.id !== single.id || (el.kind !== 'image' && el.kind !== 'video')) return el;
                                    return {
                                      ...el, w: width / layoutSize[0] * 100,
                                      h: height / layoutSize[1] * 100,
                                      ar: sourceW / sourceH,
                                      size: height / minDim * 100,
                                      fit: 'contain', a: 'mc', x: 0, y: 0,
                                      cropScale: 1, cropX: 0, cropY: 0,
                                      sourceMeta: {
                                        ...(el.sourceMeta ?? { origin: 'local' }),
                                        naturalWidth: sourceW, naturalHeight: sourceH,
                                      },
                                    } as EditorElement;
                                  });
                                } catch {
                                  onNotify?.('无法读取素材尺寸，请检查文件后重试。');
                                }
                              }}
                              className="w-full min-w-0 truncate rounded-xl border border-[#D9D2FF] bg-[#EFEBFD] px-2 py-1.5 text-[11px] font-semibold text-[#5E45F8] transition-colors hover:border-[#5E45F8] hover:bg-[#E5DDFD]"
                            >
                              满屏显示
                            </button>
                          )}

                        </div>
                      </div>
                    )}

                {/* Scrubbable X / Y Position Row (Reference Images 2 & 3) */}
                <div className="flex w-full min-w-0 items-center gap-2">
                  <span className="w-[62px] shrink-0 text-[11px] font-medium text-[#56526B]">
                    位置
                  </span>
                  <div className="flex min-w-0 flex-1 items-center gap-1.5">
                    <ScrubNumberInput
                      value={pxX}
                      suffix="X"
                      min={-layoutSize[0]}
                      max={layoutSize[0] * 2}
                      dragRatio={1.5}
                      ariaLabel="横向坐标 X"
                      onChange={(targetPx, recordHistory = true) => {
                        const nextX = +((targetPx / layoutSize[0]) * 100 - anchorBaseX).toFixed(2);
                        onMutateSelected((el) => ({ ...el, x: nextX }), undefined, recordHistory);
                      }}
                    />
                    <ScrubNumberInput
                      value={pxY}
                      suffix="Y"
                      min={-layoutSize[1]}
                      max={layoutSize[1] * 2}
                      dragRatio={1.5}
                      ariaLabel="纵向坐标 Y"
                      onChange={(targetPx, recordHistory = true) => {
                        const nextY = +((targetPx / layoutSize[1]) * 100 - anchorBaseY).toFixed(2);
                        onMutateSelected((el) => ({ ...el, y: nextY }), undefined, recordHistory);
                      }}
                    />
                  </div>
                </div>

                {/* Single-line Rotation Slider */}
                <InlineSlider
                  label="旋转角度"
                  min={-180}
                  max={180}
                  value={Math.round(single.rot || 0)}
                  displayValue={`${Math.round(single.rot || 0)}°`}
                  onChange={(val) =>
                    onMutateSelected((el) => ({ ...el, rot: val }))
                  }
                />

                {/* Flip Horizontal / Vertical */}
                <div className="grid grid-cols-2 gap-1.5">
                  <button
                    type="button"
                    onClick={() =>
                      onFlipSelection
                        ? onFlipSelection('x')
                        : onMutateSelected((el) => ({ ...el, sx: (el.sx ?? 1) * -1 }))
                    }
                    className={`flex items-center justify-center gap-1.5 rounded-xl border py-1.5 text-[11px] font-semibold transition-colors ${
                      (single.sx ?? 1) < 0
                        ? 'border-[#5E45F8] bg-[#EFEBFD] text-[#5E45F8]'
                        : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]'
                    }`}
                  >
                    <FlipHorizontal2 className="h-3.5 w-3.5" />
                    <span>水平翻转</span>
                  </button>
                  <button
                    type="button"
                    onClick={() =>
                      onFlipSelection
                        ? onFlipSelection('y')
                        : onMutateSelected((el) => ({ ...el, sy: (el.sy ?? 1) * -1 }))
                    }
                    className={`flex items-center justify-center gap-1.5 rounded-xl border py-1.5 text-[11px] font-semibold transition-colors ${
                      (single.sy ?? 1) < 0
                        ? 'border-[#5E45F8] bg-[#EFEBFD] text-[#5E45F8]'
                        : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]'
                    }`}
                  >
                    <FlipVertical2 className="h-3.5 w-3.5" />
                    <span>垂直翻转</span>
                  </button>
                </div>

                {/* 9-Anchor Grid */}
                <div>
                  <div className="mb-1.5 flex items-center justify-between text-[11px] text-[#56526B]">
                    <span>跨规格跟随锚点</span>
                    <span className="font-mono-tabular uppercase text-[#5E45F8]">{single.a}</span>
                  </div>
                  <div className="grid w-24 grid-cols-3 gap-1">
                    {ANCHORS.map((a) => (
                      <button
                        key={a}
                        type="button"
                        onClick={() => onMutateSelected((el) => ({ ...el, a }))}
                        title={`锚点 ${a.toUpperCase()}`}
                        className={`h-6 rounded-md border text-[10px] font-bold transition-colors ${
                          single.a === a
                            ? 'border-[#5E45F8] bg-[#5E45F8] text-white'
                            : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880] hover:border-[#5E45F8]'
                        }`}
                      >
                        •
                      </button>
                    ))}
                  </div>
                </div>
                  </div>
                );
              })()}
            </fieldset>

            {isFollowLayout && (
              <div className="rounded-xl border border-[#D9D2FF] bg-[#EFEBFD]/70 p-2.5 text-[10.5px] leading-relaxed text-[#4D34E6]">
                当前跟随主布局，调整参数后将转为独立布局
              </div>
            )}
          </>
        )}
      </div>
    </aside>
  );
};
