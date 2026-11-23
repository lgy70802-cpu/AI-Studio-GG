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
  ChevronLeft,
  ChevronRight,
  Copy,
  Edit3,
  Eye,
  EyeOff,
  Film,
  Group,
  Layers,
  Link2,
  Link2Off,
  Lock,
  Move,
  RotateCcw,
  RotateCw,
  Trash2,
  Ungroup,
  Unlock,
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
  EditorThemeProject,
  ImageElement,
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

interface EditorInspectorProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  project: EditorThemeProject;
  layoutIndex: number;
  selectedElements: EditorElement[];
  adjBg: boolean;
  aspectLinked: boolean;
  onToggleAspectLink: () => void;
  onToggleAdjBg: (on: boolean) => void;
  onMutateBg: (fn: (b: BgConfig) => BgConfig, label?: string) => void;
  onResetBgTransform: () => void;
  onMutateSelected: (
    fn: (el: EditorElement) => EditorElement,
    label?: string,
    recordHistory?: boolean
  ) => void;
  onReplaceImageForSelection: () => void;
  onAlignSelection: (mode: InspectorAlignMode) => void;
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
  onToggleAspectLink,
  onToggleAdjBg,
  onMutateBg,
  onResetBgTransform,
  onMutateSelected,
  onReplaceImageForSelection,
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

      {/* Top Layer Action Bar + Layer Position Button (Reference Image 5) */}
      {!showBg && primarySelected && (
        <div ref={popoverRef} className="relative shrink-0 border-b border-[#E6E4F2] bg-[#FAF9FE] px-3 py-2">
          <div className="flex items-center justify-between gap-1">
            <div className="flex items-center gap-1">
              <button
                type="button"
                onClick={() => setLayerPosOpen((o) => !o)}
                title="图层位置与顺序"
                aria-label="图层位置与顺序"
                className={`flex h-8 w-8 items-center justify-center rounded-xl border transition-all ${
                  layerPosOpen
                    ? 'border-[#5E45F8] bg-[#EFEBFD] text-[#5E45F8] shadow-2xs'
                    : 'border-[#E6E4F2] bg-white text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]'
                }`}
              >
                <Layers className="h-4 w-4" />
              </button>
              <button
                type="button"
                onClick={() => onToggleLock(selectedElements.map((e) => e.id))}
                title={singleLocked ? '解锁图层' : '锁定图层'}
                aria-label={singleLocked ? '解锁图层' : '锁定图层'}
                className={`flex h-8 w-8 items-center justify-center rounded-xl border transition-colors ${
                  singleLocked
                    ? 'border-amber-300 bg-amber-50 text-amber-600'
                    : 'border-transparent bg-transparent text-[#56526B] hover:bg-white hover:text-[#5E45F8]'
                }`}
              >
                {singleLocked ? <Lock className="h-4 w-4" /> : <Unlock className="h-4 w-4" />}
              </button>
              <button
                type="button"
                onClick={() => onToggleHide(selectedElements.map((e) => e.id))}
                title={singleHidden ? '显示图层' : '隐藏图层'}
                aria-label={singleHidden ? '显示图层' : '隐藏图层'}
                className="flex h-8 w-8 items-center justify-center rounded-xl text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
              >
                {singleHidden ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
              <button
                type="button"
                onClick={onDuplicateSelection}
                title="创建副本 (Ctrl+D)"
                aria-label="创建副本"
                className="flex h-8 w-8 items-center justify-center rounded-xl text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
              >
                <Copy className="h-4 w-4" />
              </button>
              {single && (
                <button
                  type="button"
                  onClick={() => onRenameLayer(single.id)}
                  title="重命名图层"
                  aria-label="重命名图层"
                  className="flex h-8 w-8 items-center justify-center rounded-xl text-[#56526B] hover:bg-white hover:text-[#5E45F8]"
                >
                  <Edit3 className="h-4 w-4" />
                </button>
              )}
            </div>

            <button
              type="button"
              disabled={singleLocked}
              onClick={onDeleteSelection}
              title="删除图层 (Del)"
              aria-label="删除图层"
              className="flex h-8 w-8 items-center justify-center rounded-xl text-red-500 hover:bg-red-50 disabled:opacity-40"
            >
              <Trash2 className="h-4 w-4" />
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
                  onClick={() => onAlignSelection('hc')}
                  className="rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] py-2 text-[12px] font-semibold text-[#1A172B] transition-colors hover:border-[#5E45F8] hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:opacity-40"
                >
                  水平居中
                </button>
                <button
                  type="button"
                  disabled={singleLocked}
                  onClick={() => onAlignSelection('vc')}
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
                      onClick={() => onAlignSelection(pt)}
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
              <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                画布对齐与等距分布
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
                    title={label}
                    onClick={() => onAlignSelection(k)}
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
                    <div className="flex items-center justify-between">
                      <span className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                        图片设置与裁切
                      </span>
                      <button
                        type="button"
                        onClick={onReplaceImageForSelection}
                        className="rounded-lg border border-[#D9D2FF] bg-[#EFEBFD] px-2.5 py-0.5 text-[11px] font-semibold text-[#5E45F8] hover:bg-[#5E45F8] hover:text-white"
                      >
                        更换图片…
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
                    <div className="space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <div className="flex items-center justify-between text-[11px]">
                        <span className="font-semibold text-[#56526B]">框内缩放裁切</span>
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
                    </div>

                    {/* Color filter controls */}
                    <div className="space-y-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5">
                      <div className="flex items-center justify-between text-[11px]">
                        <span className="font-semibold text-[#56526B]">画面调色</span>
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
                    </div>
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
                  </div>
                );
              })()}

              {single.kind === 'shape' && (() => {
                const shapeEl = single as ShapeElement;
                const sh = shapeEl.sh || 'rect';
                const supportsStroke =
                  sh === 'rect' || sh === 'circle' || sh === 'ring' || sh === 'brackets' || sh === 'dotline';
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
                </div>
              )}

              {single.kind === 'plugin' && (
                <div className="space-y-2.5">
                  <div className="text-[11px] font-bold tracking-wider text-[#6B6880]">
                    插件声明式配置 · v{(single as PluginElement).pluginVersion}
                  </div>
                  {Object.entries((single as PluginElement).config).map(([k, val]) => (
                    <div key={k}>
                      <label className="text-[11px] text-[#56526B]">{k}</label>
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
                  ))}
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
                          <select
                            value={single.font || ''}
                            onChange={(e) =>
                              onMutateSelected((el) => ({ ...el, font: e.target.value }))
                            }
                            className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 text-[11.5px]"
                          >
                            {EDITOR_FONTS.map((f) => (
                              <option key={f.id} value={f.id}>
                                {f.label} ({f.license})
                              </option>
                            ))}
                          </select>
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
                              ? { g1: el.g1 || '#A78BFA', g2: el.g2 || '#5E45F8' }
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
                      <div className="flex items-center justify-between rounded-xl border border-[#D9D2FF] bg-[#EFEBFD]/55 px-2.5 py-2">
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

                        <div className="grid grid-cols-2 gap-1.5">
                          <button
                            type="button"
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
                            {single.kind === 'image' || single.kind === 'video' ? '按原尺寸显示' : '按默认尺寸'}
                          </button>

                          <button
                            type="button"
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
                            100% 比例显示
                          </button>
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
