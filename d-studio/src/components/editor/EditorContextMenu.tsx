import React, { useEffect, useLayoutEffect, useRef, useState } from 'react';
import {
  Activity,
  AlignCenterHorizontal,
  AlignCenterVertical,
  ArrowDownToLine,
  ArrowUpToLine,
  BookmarkPlus,
  Check,
  ChevronRight,
  ClipboardPaste,
  Clock,
  Copy,
  Edit3,
  Eye,
  EyeOff,
  FlipHorizontal2,
  FlipVertical2,
  FolderOpen,
  Group,
  Image as ImageIcon,
  Layers,
  Lock,
  Maximize2,
  Minimize2,
  Move,
  Paintbrush,
  Pause,
  Play,
  Plus,
  RotateCcw,
  RotateCw,
  Ruler,
  Shapes,
  Shield,
  Sparkles,
  Trash2,
  Type,
  Ungroup,
  Unlock,
  Upload,
  Wand2,
} from 'lucide-react';
import {
  ClockElement,
  ClockStyleKind,
  DateElement,
  EditorElement,
  EditorThemeProject,
  ImageElement,
  ShapeElement,
  TextElement,
  VideoElement,
} from '../../types/editor';
import { isElementHidden } from './EditorArtworkRenderer';
import type { InspectorAlignMode } from './EditorInspector';

export interface ContextMenuState {
  x: number;
  y: number;
  target: 'element' | 'canvas' | 'layout';
  layoutIdx?: number;
}

interface EditorContextMenuProps {
  menu: ContextMenuState;
  project: EditorThemeProject;
  layoutIndex: number;
  layoutElements: EditorElement[];
  selectedIds: string[];
  selectedElements: EditorElement[];
  clipboardCount: number;
  hasStyleClipboard: boolean;
  playMotion: boolean;
  showSafeZone: boolean;
  showGridRulers: boolean;
  onClose: () => void;
  onCopy: () => void;
  onPaste: () => void;
  onDuplicate: () => void;
  onDelete: () => void;
  onReorderLayer: (op: 'top' | 'up' | 'down' | 'bottom') => void;
  onAlignSelection: (mode: InspectorAlignMode, scope?: 'selection' | 'canvas') => void;
  onDistributeSelection: (axis: 'h' | 'v') => void;
  onToggleLock: () => void;
  onToggleHide: () => void;
  onGroupSelection: () => void;
  onUngroupSelection: () => void;
  onBulkScale: (factor: number) => void;
  onBulkRotate: (deltaDeg: number) => void;
  onResetRotate: () => void;
  onFlipSelection: (axis: 'x' | 'y') => void;
  onCopyStyle: () => void;
  onPasteStyle: () => void;
  onSaveAsAsset: () => void;
  onSelectSameKind: () => void;
  onRenameLayer: (id: string) => void;
  onEditText: (id: string) => void;
  onMutateSingle: (fn: (el: EditorElement) => EditorElement, msg?: string) => void;
  onReplaceImageLocal: () => void;
  onReplaceImageLibrary: () => void;
  onApplyMediaNaturalSize: (el: ImageElement | VideoElement) => void;
  onApplyMediaFit100: (el: ImageElement | VideoElement) => void;
  onApplyMotionFullBleed: () => void;
  onTogglePlayMotion: () => void;
  onResetWidgetOrPluginDefaultSize: (el: EditorElement) => void;
  onSelectAllVisible: () => void;
  onQuickAdd: (kind: 'text' | 'clock' | 'date' | 'shape' | 'widget') => void;
  onTriggerLocalUploadEl: () => void;
  onEnterBgAdj: () => void;
  onToggleSafeZone: () => void;
  onToggleGridRulers: () => void;
  onSwitchLayout: (idx: number) => void;
  onToggleLayoutFollow: (idx: number) => void;
  onSmartReflowLayout: (idx: number) => void;
  onDeleteLayout: (idx: number) => void;
}

const ITEM_CLS =
  'flex w-full items-center justify-between gap-2 rounded-xl px-2.5 py-1.5 text-left text-[12px] font-medium text-[#1A172B] transition-colors hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:cursor-not-allowed disabled:text-[#B8B2CE] disabled:hover:bg-transparent disabled:hover:text-[#B8B2CE]';

const SUB_ITEM_CLS =
  'flex w-full items-center justify-between gap-2 rounded-xl px-2.5 py-1.5 text-left text-[12px] font-medium text-[#1A172B] transition-colors hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:cursor-not-allowed disabled:text-[#B8B2CE] disabled:hover:bg-transparent disabled:hover:text-[#B8B2CE]';

interface SubmenuWrapperProps {
  id: string;
  activeSubmenu: string | null;
  onOpen: (id: string) => void;
  onScheduleClose: () => void;
  onCancelClose: () => void;
  triggerLabel: string;
  triggerIcon: React.ReactNode;
  disabled?: boolean;
  widthCls?: string;
  children: React.ReactNode;
}

const SubmenuTrigger: React.FC<SubmenuWrapperProps> = ({
  id,
  activeSubmenu,
  onOpen,
  onScheduleClose,
  onCancelClose,
  triggerLabel,
  triggerIcon,
  disabled = false,
  widthCls = 'w-44',
  children,
}) => {
  const rowRef = useRef<HTMLDivElement | null>(null);
  const subRef = useRef<HTMLDivElement | null>(null);
  const isOpen = activeSubmenu === id && !disabled;
  const [placement, setPlacement] = useState<{ flipLeft: boolean; offsetTop: number }>({
    flipLeft: false,
    offsetTop: 0,
  });

  useLayoutEffect(() => {
    if (!isOpen || !rowRef.current || !subRef.current) return;
    const rowRect = rowRef.current.getBoundingClientRect();
    const subRect = subRef.current.getBoundingClientRect();
    const flipLeft = rowRect.right + subRect.width + 12 > window.innerWidth;
    let offsetTop = -6;
    const projectedBottom = rowRect.top + offsetTop + subRect.height;
    if (projectedBottom > window.innerHeight - 8) {
      offsetTop = Math.max(
        -rowRect.top + 8,
        window.innerHeight - 8 - rowRect.top - subRect.height
      );
    }
    setPlacement({ flipLeft, offsetTop });
  }, [isOpen]);

  return (
    <div
      ref={rowRef}
      className="relative"
      onMouseEnter={() => {
        if (disabled) return;
        onCancelClose();
        onOpen(id);
      }}
      onMouseLeave={onScheduleClose}
    >
      <button
        type="button"
        disabled={disabled}
        onClick={() => {
          if (!disabled) onOpen(isOpen ? '' : id);
        }}
        className={`flex w-full items-center justify-between gap-2 rounded-xl px-2.5 py-1.5 text-left text-[12px] font-medium transition-colors ${
          disabled
            ? 'cursor-not-allowed text-[#B8B2CE]'
            : isOpen
              ? 'bg-[#EFEBFD] text-[#5E45F8]'
              : 'text-[#1A172B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]'
        }`}
      >
        <span className="flex min-w-0 items-center gap-2">
          <span className={disabled ? 'text-[#B8B2CE]' : isOpen ? 'text-[#5E45F8]' : 'text-[#6B6880]'}>
            {triggerIcon}
          </span>
          <span className="truncate">{triggerLabel}</span>
        </span>
        <ChevronRight className={`h-3.5 w-3.5 shrink-0 ${disabled ? 'text-[#B8B2CE]' : 'text-[#8E89A6]'}`} />
      </button>

      {isOpen && (
        <div
          ref={subRef}
          onMouseEnter={onCancelClose}
          onMouseLeave={onScheduleClose}
          style={{ top: `${placement.offsetTop}px` }}
          className={`absolute z-50 ${widthCls} ${
            placement.flipLeft ? 'right-full mr-1.5' : 'left-full ml-1.5'
          } rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 shadow-[0_18px_46px_rgba(24,10,68,0.18)] backdrop-blur-xl`}
        >
          {/* Invisible hover bridge so diagonal cursor motion stays smooth */}
          <div
            aria-hidden="true"
            className={`absolute top-0 bottom-0 w-3 ${
              placement.flipLeft ? '-right-2.5' : '-left-2.5'
            }`}
          />
          {children}
        </div>
      )}
    </div>
  );
};

export const EditorContextMenu: React.FC<EditorContextMenuProps> = ({
  menu,
  project,
  layoutIndex,
  layoutElements,
  selectedIds,
  selectedElements,
  clipboardCount,
  hasStyleClipboard,
  playMotion,
  showSafeZone,
  showGridRulers,
  onClose,
  onCopy,
  onPaste,
  onDuplicate,
  onDelete,
  onReorderLayer,
  onAlignSelection,
  onDistributeSelection,
  onToggleLock,
  onToggleHide,
  onGroupSelection,
  onUngroupSelection,
  onBulkScale,
  onBulkRotate,
  onResetRotate,
  onFlipSelection,
  onCopyStyle,
  onPasteStyle,
  onSaveAsAsset,
  onSelectSameKind,
  onRenameLayer,
  onEditText,
  onMutateSingle,
  onReplaceImageLocal,
  onReplaceImageLibrary,
  onApplyMediaNaturalSize,
  onApplyMediaFit100,
  onApplyMotionFullBleed,
  onTogglePlayMotion,
  onResetWidgetOrPluginDefaultSize,
  onSelectAllVisible,
  onQuickAdd,
  onTriggerLocalUploadEl,
  onEnterBgAdj,
  onToggleSafeZone,
  onToggleGridRulers,
  onSwitchLayout,
  onToggleLayoutFollow,
  onSmartReflowLayout,
  onDeleteLayout,
}) => {
  const menuRef = useRef<HTMLDivElement | null>(null);
  const [pos, setPos] = useState<{ left: number; top: number }>({ left: menu.x, top: menu.y });
  const [activeSubmenu, setActiveSubmenu] = useState<string | null>(null);
  const closeTimerRef = useRef<number | null>(null);

  const cancelSubClose = () => {
    if (closeTimerRef.current !== null) {
      window.clearTimeout(closeTimerRef.current);
      closeTimerRef.current = null;
    }
  };

  const scheduleSubClose = () => {
    cancelSubClose();
    closeTimerRef.current = window.setTimeout(() => {
      setActiveSubmenu(null);
    }, 80);
  };

  useEffect(() => {
    setActiveSubmenu(null);
    return () => cancelSubClose();
  }, [menu.x, menu.y, menu.target]);

  // Clamp main menu into viewport dynamically based on rendered bounding box
  useLayoutEffect(() => {
    const el = menuRef.current;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const clampedX = Math.max(8, Math.min(window.innerWidth - rect.width - 8, menu.x));
    const clampedY = Math.max(8, Math.min(window.innerHeight - rect.height - 8, menu.y));
    setPos({ left: clampedX, top: clampedY });
  }, [menu.x, menu.y, menu.target, selectedIds.length, layoutElements.length]);

  const singleSelected = selectedElements.length === 1 ? selectedElements[0] : null;
  const allLocked =
    selectedIds.length > 0 && selectedIds.every((id) => Boolean(project.locks[id]));
  const anyHidden =
    selectedElements.length > 0 &&
    selectedElements.some((el) => isElementHidden(project, layoutIndex, el));
  const anyGrouped = selectedElements.some((el) => Boolean(el.g));

  // Z-order boundary computation (bottom-to-top in layoutElements)
  const selectedIdSet = new Set(selectedIds);
  let minIdx = Infinity;
  let maxIdx = -Infinity;
  layoutElements.forEach((el, idx) => {
    if (selectedIdSet.has(el.id)) {
      if (idx < minIdx) minIdx = idx;
      if (idx > maxIdx) maxIdx = idx;
    }
  });
  const canMoveUp =
    !allLocked &&
    layoutElements.length > 1 &&
    maxIdx >= 0 &&
    maxIdx < layoutElements.length - 1;
  const canMoveDown =
    !allLocked &&
    layoutElements.length > 1 &&
    minIdx !== Infinity &&
    minIdx > 0;

  const runAndClose = (fn: () => void) => {
    fn();
    setActiveSubmenu(null);
    onClose();
  };

  return (
    <div
      ref={menuRef}
      role="menu"
      aria-label="编辑器右键快捷菜单"
      style={{ left: `${pos.left}px`, top: `${pos.top}px` }}
      onClick={(e) => e.stopPropagation()}
      onContextMenu={(e) => e.preventDefault()}
      onMouseLeave={scheduleSubClose}
      className="fixed z-50 w-52 rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 text-[12px] text-[#1A172B] shadow-[0_18px_46px_rgba(24,10,68,0.18)] backdrop-blur-xl"
    >
      {menu.target === 'element' && (
        <>
          {/* 1. Clipboard & Lifecycle */}
          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onCopy)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Copy className="h-3.5 w-3.5 text-[#6B6880]" />
              <span>复制</span>
            </span>
            <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+C</span>
          </button>

          <button
            type="button"
            disabled={clipboardCount === 0}
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onPaste)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Plus className="h-3.5 w-3.5 text-[#6B6880]" />
              <span>粘贴</span>
            </span>
            <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+V</span>
          </button>

          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onDuplicate)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Copy className="h-3.5 w-3.5 text-[#5E45F8]" />
              <span>创建副本</span>
            </span>
            <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+D</span>
          </button>

          <button
            type="button"
            disabled={allLocked}
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onDelete)}
            className={`flex w-full items-center justify-between gap-2 rounded-xl px-2.5 py-1.5 text-left text-[12px] font-medium transition-colors ${
              allLocked
                ? 'cursor-not-allowed text-[#B8B2CE]'
                : 'text-red-600 hover:bg-red-50'
            }`}
          >
            <span className="flex items-center gap-2">
              <Trash2 className="h-3.5 w-3.5" />
              <span>删除</span>
            </span>
            <span className={`font-mono-tabular text-[10px] ${allLocked ? 'text-[#B8B2CE]' : 'text-red-400'}`}>
              Del
            </span>
          </button>

          <div className="my-1 h-px bg-[#E6E4F2]" />

          {/* 2. Z-Order & Alignment */}
          <SubmenuTrigger
            id="z"
            activeSubmenu={activeSubmenu}
            onOpen={(id) => setActiveSubmenu(id || null)}
            onScheduleClose={scheduleSubClose}
            onCancelClose={cancelSubClose}
            triggerLabel="层级"
            triggerIcon={<Layers className="h-3.5 w-3.5" />}
            widthCls="w-40"
          >
            <button
              type="button"
              disabled={!canMoveUp}
              onClick={() => runAndClose(() => onReorderLayer('top'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <ArrowUpToLine className="h-3.5 w-3.5" />
                <span>置顶</span>
              </span>
            </button>
            <button
              type="button"
              disabled={!canMoveUp}
              onClick={() => runAndClose(() => onReorderLayer('up'))}
              className={SUB_ITEM_CLS}
            >
              <span>上移一层</span>
            </button>
            <button
              type="button"
              disabled={!canMoveDown}
              onClick={() => runAndClose(() => onReorderLayer('down'))}
              className={SUB_ITEM_CLS}
            >
              <span>下移一层</span>
            </button>
            <button
              type="button"
              disabled={!canMoveDown}
              onClick={() => runAndClose(() => onReorderLayer('bottom'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <ArrowDownToLine className="h-3.5 w-3.5" />
                <span>置底</span>
              </span>
            </button>
          </SubmenuTrigger>

          {selectedIds.length > 1 && (
            <SubmenuTrigger
              id="align-selection"
              activeSubmenu={activeSubmenu}
              onOpen={(id) => setActiveSubmenu(id || null)}
              onScheduleClose={scheduleSubClose}
              onCancelClose={cancelSubClose}
              triggerLabel="对齐选区（互相对齐）"
              triggerIcon={<AlignCenterHorizontal className="h-3.5 w-3.5" />}
              disabled={allLocked}
              widthCls="w-40"
            >
              {(
                [
                  { mode: 'l' as const, label: '左边缘对齐' },
                  { mode: 'hc' as const, label: '水平居中对齐' },
                  { mode: 'r' as const, label: '右边缘对齐' },
                  { mode: 't' as const, label: '顶边缘对齐' },
                  { mode: 'vc' as const, label: '垂直居中对齐' },
                  { mode: 'b' as const, label: '底边缘对齐' },
                  { mode: 'c' as const, label: '选区正中叠合' },
                ] as const
              ).map((item) => (
                <button
                  key={item.mode}
                  type="button"
                  onClick={() => runAndClose(() => onAlignSelection(item.mode, 'selection'))}
                  className={SUB_ITEM_CLS}
                >
                  <span>{item.label}</span>
                </button>
              ))}
              {selectedIds.length >= 3 && (
                <>
                  <div className="my-1 h-px bg-[#E6E4F2]" />
                  <button
                    type="button"
                    onClick={() => runAndClose(() => onDistributeSelection('h'))}
                    className={SUB_ITEM_CLS}
                  >
                    <span>水平等距分布</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => runAndClose(() => onDistributeSelection('v'))}
                    className={SUB_ITEM_CLS}
                  >
                    <span>垂直等距分布</span>
                  </button>
                </>
              )}
            </SubmenuTrigger>
          )}

          <SubmenuTrigger
            id="align"
            activeSubmenu={activeSubmenu}
            onOpen={(id) => setActiveSubmenu(id || null)}
            onScheduleClose={scheduleSubClose}
            onCancelClose={cancelSubClose}
            triggerLabel="对齐画布"
            triggerIcon={<AlignCenterVertical className="h-3.5 w-3.5" />}
            disabled={allLocked}
            widthCls="w-40"
          >
            {(
              [
                { mode: 'l' as const, label: '居左' },
                { mode: 'hc' as const, label: '水平居中' },
                { mode: 'r' as const, label: '居右' },
                { mode: 't' as const, label: '居上' },
                { mode: 'vc' as const, label: '垂直居中' },
                { mode: 'b' as const, label: '居下' },
                { mode: 'c' as const, label: '正中' },
              ] as const
            ).map((item) => (
              <button
                key={item.mode}
                type="button"
                onClick={() => runAndClose(() => onAlignSelection(item.mode, 'canvas'))}
                className={SUB_ITEM_CLS}
              >
                <span>{item.label}</span>
              </button>
            ))}
            {selectedIds.length === 1 ? null : selectedIds.length >= 3 ? (
              <>
                <div className="my-1 h-px bg-[#E6E4F2]" />
                <button
                  type="button"
                  onClick={() => runAndClose(() => onDistributeSelection('h'))}
                  className={SUB_ITEM_CLS}
                >
                  <span>水平等距分布</span>
                </button>
                <button
                  type="button"
                  onClick={() => runAndClose(() => onDistributeSelection('v'))}
                  className={SUB_ITEM_CLS}
                >
                  <span>垂直等距分布</span>
                </button>
              </>
            ) : null}
          </SubmenuTrigger>

          <div className="my-1 h-px bg-[#E6E4F2]" />

          {/* 3. Lock, Visibility, Grouping & Transform */}
          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onToggleLock)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              {allLocked ? (
                <Unlock className="h-3.5 w-3.5 text-amber-600" />
              ) : (
                <Lock className="h-3.5 w-3.5 text-[#6B6880]" />
              )}
              <span>{allLocked ? '解锁' : '锁定'}</span>
            </span>
          </button>

          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onToggleHide)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              {anyHidden ? (
                <Eye className="h-3.5 w-3.5 text-[#5E45F8]" />
              ) : (
                <EyeOff className="h-3.5 w-3.5 text-[#6B6880]" />
              )}
              <span>{anyHidden ? '显示' : '隐藏'}</span>
            </span>
          </button>

          {selectedIds.length > 1 && (
            <button
              type="button"
              onMouseEnter={() => {
                cancelSubClose();
                setActiveSubmenu(null);
              }}
              onClick={() => runAndClose(onGroupSelection)}
              className={ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Group className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>组合图层</span>
              </span>
              <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+G</span>
            </button>
          )}

          {anyGrouped && (
            <button
              type="button"
              onMouseEnter={() => {
                cancelSubClose();
                setActiveSubmenu(null);
              }}
              onClick={() => runAndClose(onUngroupSelection)}
              className={ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Ungroup className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>取消组合</span>
              </span>
              <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+Shift+G</span>
            </button>
          )}

          <SubmenuTrigger
            id="transform"
            activeSubmenu={activeSubmenu}
            onOpen={(id) => setActiveSubmenu(id || null)}
            onScheduleClose={scheduleSubClose}
            onCancelClose={cancelSubClose}
            triggerLabel="变换"
            triggerIcon={<RotateCw className="h-3.5 w-3.5" />}
            disabled={allLocked}
            widthCls="w-44"
          >
            <button
              type="button"
              onClick={() => runAndClose(() => onBulkScale(1.1))}
              className={SUB_ITEM_CLS}
            >
              <span>放大 10%</span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onBulkScale(0.9))}
              className={SUB_ITEM_CLS}
            >
              <span>缩小 10%</span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onBulkRotate(15))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <RotateCw className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>顺时针旋转 15°</span>
              </span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onBulkRotate(-15))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <RotateCcw className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>逆时针旋转 15°</span>
              </span>
            </button>

            <div className="my-1 h-px bg-[#E6E4F2]" />

            <button
              type="button"
              onClick={() => runAndClose(() => onBulkRotate(90))}
              className={SUB_ITEM_CLS}
            >
              <span>顺时针旋转 90°</span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onBulkRotate(-90))}
              className={SUB_ITEM_CLS}
            >
              <span>逆时针旋转 90°</span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(onResetRotate)}
              className={SUB_ITEM_CLS}
            >
              <span>重置旋转角度 (0°)</span>
            </button>

            <div className="my-1 h-px bg-[#E6E4F2]" />

            <button
              type="button"
              onClick={() => runAndClose(() => onFlipSelection('x'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <FlipHorizontal2 className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>水平翻转</span>
              </span>
              {singleSelected && (singleSelected.sx ?? 1) < 0 && (
                <Check className="h-3.5 w-3.5 text-[#5E45F8]" />
              )}
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onFlipSelection('y'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <FlipVertical2 className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>垂直翻转</span>
              </span>
              {singleSelected && (singleSelected.sy ?? 1) < 0 && (
                <Check className="h-3.5 w-3.5 text-[#5E45F8]" />
              )}
            </button>
          </SubmenuTrigger>

          {/* 4. Type-Specific Quick Actions (Single Element) */}
          {singleSelected && !allLocked && (
            <>
              <div className="my-1 h-px bg-[#E6E4F2]" />

              {singleSelected.kind === 'text' && (() => {
                const txt = singleSelected as TextElement;
                const isVert = Boolean(txt.vert);
                const hasFixedBox = isVert ? txt.h != null : txt.w != null;
                return (
                  <>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(() => onEditText(txt.id))}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Edit3 className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span>编辑文字内容</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() =>
                        runAndClose(() =>
                          onMutateSingle(
                            (el) => ({ ...el, vert: !el.vert }),
                            isVert ? '已切换为横排排版' : '已切换为竖排排版'
                          )
                        )
                      }
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Type className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>{isVert ? '切换为横排排版' : '切换为竖排排版'}</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() =>
                        runAndClose(() =>
                          onMutateSingle(
                            (el) =>
                              isVert
                                ? ({ ...el, h: hasFixedBox ? undefined : 60 } as TextElement)
                                : ({ ...el, w: hasFixedBox ? undefined : 40 } as TextElement),
                            hasFixedBox ? '已切换为自适应宽度' : '已固定文本框宽度'
                          )
                        )
                      }
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Maximize2 className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>{hasFixedBox ? '切换自适应不折行' : `固定${isVert ? '高度' : '宽度'}`}</span>
                      </span>
                    </button>
                  </>
                );
              })()}

              {singleSelected.kind === 'image' && (() => {
                const img = singleSelected as ImageElement;
                const hasCropOrFilter =
                  (img.cropScale && img.cropScale !== 1) ||
                  Boolean(img.cropX) ||
                  Boolean(img.cropY) ||
                  (img.brightness != null && img.brightness !== 100) ||
                  (img.contrast != null && img.contrast !== 100) ||
                  (img.saturate != null && img.saturate !== 100) ||
                  Boolean(img.feather && img.feather.strength > 0);
                return (
                  <>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(onReplaceImageLocal)}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Upload className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span>本地替换图片…</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(onReplaceImageLibrary)}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <FolderOpen className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>从素材库替换…</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(() => onApplyMediaNaturalSize(img))}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Minimize2 className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>按原尺寸显示</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(() => onApplyMediaFit100(img))}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Maximize2 className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span>100% 比例适配居中</span>
                      </span>
                    </button>
                    {hasCropOrFilter && (
                      <button
                        type="button"
                        onMouseEnter={() => {
                          cancelSubClose();
                          setActiveSubmenu(null);
                        }}
                        onClick={() =>
                          runAndClose(() =>
                            onMutateSingle(
                              (el) =>
                                ({
                                  ...el,
                                  cropScale: 1,
                                  cropX: 0,
                                  cropY: 0,
                                  brightness: 100,
                                  contrast: 100,
                                  saturate: 100,
                                  feather: undefined,
                                } as ImageElement),
                              '已重置图片裁切、调色与羽化'
                            )
                          )
                        }
                        className={ITEM_CLS}
                      >
                        <span className="flex items-center gap-2">
                          <RotateCcw className="h-3.5 w-3.5 text-[#6B6880]" />
                          <span>重置裁切与效果</span>
                        </span>
                      </button>
                    )}
                  </>
                );
              })()}

              {singleSelected.kind === 'video' && (() => {
                const vid = singleSelected as VideoElement;
                const isCover = (vid.fit || 'cover') === 'cover';
                return (
                  <>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(onReplaceImageLocal)}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Upload className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span>替换本地视频…</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(() => onApplyMediaNaturalSize(vid))}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Minimize2 className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>按原尺寸显示</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() => runAndClose(() => onApplyMediaFit100(vid))}
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Maximize2 className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span>100% 比例适配居中</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() =>
                        runAndClose(() =>
                          onMutateSingle(
                            (el) =>
                              ({
                                ...el,
                                fit: isCover ? 'contain' : 'cover',
                              } as VideoElement),
                            isCover ? '已切换为完整适应填充' : '已切换为裁切铺满'
                          )
                        )
                      }
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <ImageIcon className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>{isCover ? '切换为适应完整' : '切换为裁切铺满'}</span>
                      </span>
                    </button>
                  </>
                );
              })()}

              {singleSelected.kind === 'clock' && (() => {
                const clk = singleSelected as ClockElement;
                return (
                  <>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() =>
                        runAndClose(() =>
                          onMutateSingle(
                            (el) => ({ ...el, h24: !(el as ClockElement).h24 } as ClockElement),
                            clk.h24 ? '已切换为 12 小时制' : '已切换为 24 小时制'
                          )
                        )
                      }
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Clock className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span>{clk.h24 ? '切换为 12 小时制' : '切换为 24 小时制'}</span>
                      </span>
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() =>
                        runAndClose(() =>
                          onMutateSingle(
                            (el) => ({ ...el, sec: !(el as ClockElement).sec } as ClockElement),
                            clk.sec ? '已隐藏秒数' : '已显示秒数'
                          )
                        )
                      }
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Clock className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>{clk.sec ? '隐藏秒数显示' : '显示秒数走时'}</span>
                      </span>
                    </button>
                    <SubmenuTrigger
                      id="clock-style"
                      activeSubmenu={activeSubmenu}
                      onOpen={(id) => setActiveSubmenu(id || null)}
                      onScheduleClose={scheduleSubClose}
                      onCancelClose={cancelSubClose}
                      triggerLabel="切换时钟风格"
                      triggerIcon={<Sparkles className="h-3.5 w-3.5" />}
                      widthCls="w-40"
                    >
                      {(
                        [
                          { style: 'digital', label: '数字时钟' },
                          { style: 'flip', label: '翻页机械' },
                          { style: 'minimal', label: '极简细线' },
                          { style: 'analog', label: '模拟指针' },
                        ] as { style: ClockStyleKind; label: string }[]
                      ).map((opt) => (
                        <button
                          key={opt.style}
                          type="button"
                          onClick={() =>
                            runAndClose(() =>
                              onMutateSingle(
                                (el) => ({ ...el, style: opt.style } as ClockElement),
                                `已切换为「${opt.label}」`
                              )
                            )
                          }
                          className={SUB_ITEM_CLS}
                        >
                          <span>{opt.label}</span>
                          {clk.style === opt.style && (
                            <Check className="h-3.5 w-3.5 text-[#5E45F8]" />
                          )}
                        </button>
                      ))}
                    </SubmenuTrigger>
                  </>
                );
              })()}

              {singleSelected.kind === 'date' && (() => {
                const dt = singleSelected as DateElement;
                return (
                  <>
                    <SubmenuTrigger
                      id="date-fmt"
                      activeSubmenu={activeSubmenu}
                      onOpen={(id) => setActiveSubmenu(id || null)}
                      onScheduleClose={scheduleSubClose}
                      onCancelClose={cancelSubClose}
                      triggerLabel="切换日期格式"
                      triggerIcon={<Clock className="h-3.5 w-3.5" />}
                      widthCls="w-48"
                    >
                      {(
                        [
                          { fmt: 0, label: '中文日期 (10月30日 周五)' },
                          { fmt: 1, label: '英文完整 (FRIDAY · OCT 30)' },
                          { fmt: 2, label: '数字标准 (2026-10-30)' },
                          { fmt: 3, label: '极简短格式 (10.30 FRI)' },
                        ] as { fmt: 0 | 1 | 2 | 3; label: string }[]
                      ).map((opt) => (
                        <button
                          key={opt.fmt}
                          type="button"
                          onClick={() =>
                            runAndClose(() =>
                              onMutateSingle(
                                (el) => ({ ...el, fmt: opt.fmt } as DateElement),
                                `已切换日期格式：${opt.label}`
                              )
                            )
                          }
                          className={SUB_ITEM_CLS}
                        >
                          <span className="truncate">{opt.label}</span>
                          {dt.fmt === opt.fmt && (
                            <Check className="h-3.5 w-3.5 shrink-0 text-[#5E45F8]" />
                          )}
                        </button>
                      ))}
                    </SubmenuTrigger>
                    <button
                      type="button"
                      onMouseEnter={() => {
                        cancelSubClose();
                        setActiveSubmenu(null);
                      }}
                      onClick={() =>
                        runAndClose(() =>
                          onMutateSingle(
                            (el) => ({ ...el, vert: !el.vert }),
                            dt.vert ? '已切换为横排日期' : '已切换为竖排日期'
                          )
                        )
                      }
                      className={ITEM_CLS}
                    >
                      <span className="flex items-center gap-2">
                        <Type className="h-3.5 w-3.5 text-[#6B6880]" />
                        <span>{dt.vert ? '切换为横排排版' : '切换为竖排排版'}</span>
                      </span>
                    </button>
                  </>
                );
              })()}

              {singleSelected.kind === 'shape' && (() => {
                const shp = singleSelected as ShapeElement;
                const sh = shp.sh || 'rect';
                const isPill = shp.radius >= 50;
                const hasStroke = (shp.bw ?? 0) > 0;
                return (
                  <>
                    {sh === 'rect' && (
                      <button
                        type="button"
                        onMouseEnter={() => {
                          cancelSubClose();
                          setActiveSubmenu(null);
                        }}
                        onClick={() =>
                          runAndClose(() =>
                            onMutateSingle(
                              (el) =>
                                ({ ...el, radius: isPill ? 4 : 50 } as ShapeElement),
                              isPill ? '已切换为微圆角矩形' : '已切换为胶囊圆角'
                            )
                          )
                        }
                        className={ITEM_CLS}
                      >
                        <span className="flex items-center gap-2">
                          <Shapes className="h-3.5 w-3.5 text-[#5E45F8]" />
                          <span>{isPill ? '切换为矩形微圆角' : '切换为胶囊圆角'}</span>
                        </span>
                      </button>
                    )}
                    {(sh === 'rect' || sh === 'circle') && (
                      <button
                        type="button"
                        onMouseEnter={() => {
                          cancelSubClose();
                          setActiveSubmenu(null);
                        }}
                        onClick={() =>
                          runAndClose(() =>
                            onMutateSingle(
                              (el) =>
                                ({
                                  ...el,
                                  bw: hasStroke ? 0 : 1,
                                  strokeColor: (el as ShapeElement).strokeColor || '#ffffff',
                                } as ShapeElement),
                              hasStroke ? '已关闭边框描边' : '已开启边框描边'
                            )
                          )
                        }
                        className={ITEM_CLS}
                      >
                        <span className="flex items-center gap-2">
                          <Shapes className="h-3.5 w-3.5 text-[#6B6880]" />
                          <span>{hasStroke ? '关闭边框描边' : '开启边框描边'}</span>
                        </span>
                      </button>
                    )}
                  </>
                );
              })()}

              {singleSelected.kind === 'motion' && (
                <>
                  <button
                    type="button"
                    onMouseEnter={() => {
                      cancelSubClose();
                      setActiveSubmenu(null);
                    }}
                    onClick={() => runAndClose(onApplyMotionFullBleed)}
                    className={ITEM_CLS}
                  >
                    <span className="flex items-center gap-2">
                      <Maximize2 className="h-3.5 w-3.5 text-[#5E45F8]" />
                      <span>100% 铺满画布并居中</span>
                    </span>
                  </button>
                  <button
                    type="button"
                    onMouseEnter={() => {
                      cancelSubClose();
                      setActiveSubmenu(null);
                    }}
                    onClick={() => runAndClose(onTogglePlayMotion)}
                    className={ITEM_CLS}
                  >
                    <span className="flex items-center gap-2">
                      {playMotion ? (
                        <Pause className="h-3.5 w-3.5 text-[#6B6880]" />
                      ) : (
                        <Play className="h-3.5 w-3.5 text-[#5E45F8]" />
                      )}
                      <span>{playMotion ? '暂停画布动态预览' : '播放画布动态预览'}</span>
                    </span>
                  </button>
                </>
              )}

              {(singleSelected.kind === 'widget' || singleSelected.kind === 'plugin') && (
                <button
                  type="button"
                  onMouseEnter={() => {
                    cancelSubClose();
                    setActiveSubmenu(null);
                  }}
                  onClick={() => runAndClose(() => onResetWidgetOrPluginDefaultSize(singleSelected))}
                  className={ITEM_CLS}
                >
                  <span className="flex items-center gap-2">
                    <Activity className="h-3.5 w-3.5 text-[#5E45F8]" />
                    <span>恢复默认尺寸</span>
                  </span>
                </button>
              )}
            </>
          )}

          {/* 5. Style Clipboard, Same-Kind Selection & Rename */}
          <div className="my-1 h-px bg-[#E6E4F2]" />

          {singleSelected && (
            <button
              type="button"
              onMouseEnter={() => {
                cancelSubClose();
                setActiveSubmenu(null);
              }}
              onClick={() => runAndClose(onCopyStyle)}
              className={ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Paintbrush className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>复制样式</span>
              </span>
            </button>
          )}

          <button
            type="button"
            disabled={!hasStyleClipboard || allLocked}
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onPasteStyle)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <ClipboardPaste className="h-3.5 w-3.5 text-[#5E45F8]" />
              <span>粘贴样式</span>
            </span>
          </button>

          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onSaveAsAsset)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <BookmarkPlus className="h-3.5 w-3.5 text-[#5E45F8]" />
              <span>存为我的素材…</span>
            </span>
          </button>

          {singleSelected && (
            <>
              <button
                type="button"
                onMouseEnter={() => {
                  cancelSubClose();
                  setActiveSubmenu(null);
                }}
                onClick={() => runAndClose(onSelectSameKind)}
                className={ITEM_CLS}
              >
                <span className="flex items-center gap-2">
                  <Layers className="h-3.5 w-3.5 text-[#6B6880]" />
                  <span>选择同类型图层</span>
                </span>
              </button>
              <button
                type="button"
                onMouseEnter={() => {
                  cancelSubClose();
                  setActiveSubmenu(null);
                }}
                onClick={() => runAndClose(() => onRenameLayer(singleSelected.id))}
                className={ITEM_CLS}
              >
                <span className="flex items-center gap-2">
                  <Edit3 className="h-3.5 w-3.5 text-[#6B6880]" />
                  <span>重命名…</span>
                </span>
              </button>
            </>
          )}
        </>
      )}

      {menu.target === 'layout' && menu.layoutIdx !== undefined && (
        <>
          <button
            type="button"
            onClick={() => runAndClose(() => onSwitchLayout(menu.layoutIdx!))}
            className={ITEM_CLS}
          >
            <span>切换到此尺寸画布</span>
          </button>
          {menu.layoutIdx > 0 && (
            <>
              <button
                type="button"
                onClick={() => runAndClose(() => onToggleLayoutFollow(menu.layoutIdx!))}
                className={ITEM_CLS}
              >
                <span>
                  {project.layouts[menu.layoutIdx]?.follow
                    ? '脱离跟随（独立编辑）'
                    : '恢复跟随主布局'}
                </span>
              </button>
              <button
                type="button"
                onClick={() => runAndClose(() => onSmartReflowLayout(menu.layoutIdx!))}
                className={ITEM_CLS}
              >
                <span className="flex items-center gap-2">
                  <Wand2 className="h-3.5 w-3.5 text-[#5E45F8]" />
                  <span>基于主布局智能重排</span>
                </span>
              </button>
              <div className="my-1 h-px bg-[#E6E4F2]" />
              <button
                type="button"
                disabled={project.layouts.length <= 1}
                onClick={() => runAndClose(() => onDeleteLayout(menu.layoutIdx!))}
                className="flex w-full items-center gap-2 rounded-xl px-2.5 py-1.5 text-left text-[12px] font-medium text-red-600 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-40"
              >
                <Trash2 className="h-3.5 w-3.5" />
                <span>删除此尺寸</span>
              </button>
            </>
          )}
        </>
      )}

      {menu.target === 'canvas' && (
        <>
          <button
            type="button"
            disabled={clipboardCount === 0}
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onPaste)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Plus className="h-3.5 w-3.5 text-[#6B6880]" />
              <span>粘贴</span>
            </span>
            <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+V</span>
          </button>

          <button
            type="button"
            disabled={layoutElements.length === 0}
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onSelectAllVisible)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Layers className="h-3.5 w-3.5 text-[#6B6880]" />
              <span>全选可见图层</span>
            </span>
            <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+A</span>
          </button>

          <div className="my-1 h-px bg-[#E6E4F2]" />

          <SubmenuTrigger
            id="add"
            activeSubmenu={activeSubmenu}
            onOpen={(id) => setActiveSubmenu(id || null)}
            onScheduleClose={scheduleSubClose}
            onCancelClose={cancelSubClose}
            triggerLabel="快速添加图层"
            triggerIcon={<Plus className="h-3.5 w-3.5" />}
            widthCls="w-44"
          >
            <button
              type="button"
              onClick={() => runAndClose(() => onQuickAdd('text'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Type className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>新建文本</span>
              </span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onQuickAdd('clock'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Clock className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>数字时钟</span>
              </span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onQuickAdd('date'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Clock className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>日期星期</span>
              </span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onQuickAdd('shape'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Shapes className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>几何形状</span>
              </span>
            </button>
            <button
              type="button"
              onClick={() => runAndClose(() => onQuickAdd('widget'))}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Activity className="h-3.5 w-3.5 text-[#6B6880]" />
                <span>硬件监控组件</span>
              </span>
            </button>
            <div className="my-1 h-px bg-[#E6E4F2]" />
            <button
              type="button"
              onClick={() => runAndClose(onTriggerLocalUploadEl)}
              className={SUB_ITEM_CLS}
            >
              <span className="flex items-center gap-2">
                <Upload className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>导入本地图片/视频…</span>
              </span>
            </button>
          </SubmenuTrigger>

          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onEnterBgAdj)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Move className="h-3.5 w-3.5 text-[#6B6880]" />
              <span>进入背景直接调整</span>
            </span>
          </button>

          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onToggleSafeZone)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Shield className={`h-3.5 w-3.5 ${showSafeZone ? 'text-[#5E45F8]' : 'text-[#6B6880]'}`} />
              <span>{showSafeZone ? '隐藏安全边距' : '显示安全边距'}</span>
            </span>
          </button>

          <button
            type="button"
            onMouseEnter={() => {
              cancelSubClose();
              setActiveSubmenu(null);
            }}
            onClick={() => runAndClose(onToggleGridRulers)}
            className={ITEM_CLS}
          >
            <span className="flex items-center gap-2">
              <Ruler className={`h-3.5 w-3.5 ${showGridRulers ? 'text-[#5E45F8]' : 'text-[#6B6880]'}`} />
              <span>{showGridRulers ? '隐藏标尺与网格' : '显示标尺与网格'}</span>
            </span>
          </button>
        </>
      )}
    </div>
  );
};
