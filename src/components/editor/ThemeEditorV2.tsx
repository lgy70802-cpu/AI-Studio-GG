import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Activity,
  AlignCenterVertical,
  AlignEndVertical,
  AlignHorizontalSpaceAround,
  AlignStartVertical,
  AlignVerticalSpaceAround,
  ArrowLeft,
  Check,
  ChevronRight,
  Clock,
  Copy,
  Download,
  Eye,
  EyeOff,
  FolderPlus,
  Group,
  HelpCircle,
  Image as ImageIcon,
  Layers,
  Lock,
  MonitorPlay,
  MoreHorizontal,
  Palette,
  Play,
  Plus,
  Puzzle,
  Redo2,
  RotateCw,
  Ruler,
  Save,
  Shield,
  Sparkles,
  Trash2,
  Type,
  Undo2,
  Ungroup,
  Unlock,
  Upload,
  WifiOff,
  X,
  ZoomIn,
  ZoomOut,
} from 'lucide-react';
import { ContentCategory, DeviceInfo, ThemeItem } from '../../data/themes';
import {
  EDITOR_DEVICE_SPECS,
  EDITOR_RAIL_TABS,
  computeImportMediaBox,
  genEditorId,
  makeElement,
  readImageDimensions,
  readVideoDimensions,
  specLabelFromSize,
} from '../../data/editorCatalog';
import { cloudResourceAdapter } from '../../services/cloudResourceAdapter';
import {
  BgConfig,
  EditorElement,
  EditorRailTab,
  EditorThemeProject,
  ImageElement,
  THEME_SCHEMA_VERSION,
  TextElement,
  VideoElement,
} from '../../types/editor';
import { CaptureModal } from './CaptureModal';
import {
  EditorArtworkRenderer,
  FX_ANCHOR,
  FY_ANCHOR,
  getLayoutElements,
  isElementHidden,
  resolveAnchorPercentages,
} from './EditorArtworkRenderer';
import { EditorInspector, InspectorAlignMode } from './EditorInspector';
import { EditorLeftPanel } from './EditorLeftPanel';

interface ThemeEditorV2Props {
  initialProject: EditorThemeProject;
  sourceThemeId?: string;
  activeDevice: DeviceInfo;
  themes: ThemeItem[];
  onSaveOnly: (theme: ThemeItem, project: EditorThemeProject) => void;
  onSaveAndApply: (theme: ThemeItem, project: EditorThemeProject) => void;
  onStartTempPreview: (theme: ThemeItem) => void;
  onExitEditor: () => void;
  onNotify: (msg: string) => void;
}

interface BoxRect {
  left: number;
  top: number;
  width: number;
  height: number;
  cx: number;
  cy: number;
}

type DragSession =
  | {
      type: 'move';
      startX: number;
      startY: number;
      canvasW: number;
      canvasH: number;
      ids: string[];
      origins: Record<string, { x: number; y: number }>;
      origBoxPct?: { l: number; cx: number; r: number; t: number; cy: number; b: number };
      siblingTargets?: { v: number[]; h: number[] };
      moved: boolean;
    }
  | {
      type: 'resize';
      handle: 'tl' | 'tc' | 'tr' | 'ml' | 'mr' | 'bl' | 'bc' | 'br';
      startX: number;
      startY: number;
      canvasW: number;
      canvasH: number;
      elId: string;
      origBox: BoxRect;
      origEl: EditorElement;
      moved: boolean;
    }
  | {
      type: 'rotate';
      cx: number;
      cy: number;
      startAngle: number;
      elId: string;
      origRot: number;
      moved: boolean;
    }
  | {
      type: 'marquee';
      startClientX: number;
      startClientY: number;
      curClientX: number;
      curClientY: number;
    }
  | {
      type: 'pan';
      startX: number;
      startY: number;
      origScrollLeft: number;
      origScrollTop: number;
    }
  | {
      type: 'bg-drag';
      startX: number;
      startY: number;
      canvasW: number;
      canvasH: number;
      origTx: number;
      origTy: number;
      moved: boolean;
    };

function cloneProj(p: EditorThemeProject): EditorThemeProject {
  return structuredClone(p);
}

function detachIfFollowing(draft: EditorThemeProject, layIdx: number): boolean {
  const lay = draft.layouts[layIdx];
  if (layIdx > 0 && lay && lay.follow) {
    lay.follow = false;
    lay.elements = cloneProj(draft).master.map((el) =>
      lay.vis?.[el.id] ? { ...el, hide: true } : el
    );
    delete lay.vis;
    return true;
  }
  return false;
}

function readFileAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : '');
    reader.onerror = () => reject(new Error('文件读取失败'));
    reader.readAsDataURL(file);
  });
}

function renderRailIcon(id: EditorRailTab) {
  switch (id) {
    case 'add':
      return <Plus className="h-4 w-4" />;
    case 'templates':
      return <Sparkles className="h-4 w-4" />;
    case 'assets':
      return <Copy className="h-4 w-4" />;
    case 'text':
      return <Type className="h-4 w-4" />;
    case 'images':
      return <ImageIcon className="h-4 w-4" />;
    case 'time':
      return <Clock className="h-4 w-4" />;
    case 'motion':
      return <Activity className="h-4 w-4" />;
    case 'widgets':
      return <FolderPlus className="h-4 w-4" />;
    case 'plugins':
      return <Puzzle className="h-4 w-4" />;
    case 'background':
      return <Palette className="h-4 w-4" />;
    case 'layers':
      return <Layers className="h-4 w-4" />;
  }
}

export const ThemeEditorV2: React.FC<ThemeEditorV2Props> = ({
  initialProject,
  sourceThemeId,
  activeDevice,
  themes,
  onSaveOnly,
  onSaveAndApply,
  onStartTempPreview,
  onExitEditor,
  onNotify,
}) => {
  const [project, setProject] = useState<EditorThemeProject>(() => cloneProj(initialProject));
  const [layoutIndex, setLayoutIndex] = useState<number>(0);
  const [activeRailTab, setActiveRailTab] = useState<EditorRailTab>('templates');
  const [leftCollapsed, setLeftCollapsed] = useState<boolean>(false);
  const [rightCollapsed, setRightCollapsed] = useState<boolean>(false);

  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [adjBg, setAdjBg] = useState<boolean>(false);
  const [aspectLinked, setAspectLinked] = useState<boolean>(false);
  const lastAspectElIdRef = useRef<string | null>(null);
  const [editingTextId, setEditingTextId] = useState<string | null>(null);

  // Canvas preview & guide states
  const [liveTick, setLiveTick] = useState<boolean>(true);
  const [playMotion, setPlayMotion] = useState<boolean>(true);
  const [showSafeZone, setShowSafeZone] = useState<boolean>(true);
  const [showGridRulers, setShowGridRulers] = useState<boolean>(true);
  const [zoomMode, setZoomMode] = useState<'fit' | number>('fit');
  const [fitZoom, setFitZoom] = useState<number>(0.6);
  const [spacePressed, setSpacePressed] = useState<boolean>(false);

  // Undo/Redo history (up to 50 steps)
  const [history, setHistory] = useState<EditorThemeProject[]>(() => [cloneProj(initialProject)]);
  const [historyIdx, setHistoryIdx] = useState<number>(0);
  const [isDirty, setIsDirty] = useState<boolean>(false);
  const [draftSavedAt, setDraftSavedAt] = useState<string | null>(null);

  // Clipboard & Recent Local Imports
  const [clipboard, setClipboard] = useState<EditorElement[]>([]);
  const [styleClipboard, setStyleClipboard] = useState<Partial<EditorElement> | null>(null);
  const [recentImports, setRecentImports] = useState<
    { id: string; name: string; kind: 'image' | 'video'; src: string; w: number; h: number }[]
  >([]);

  // Measured boxes on canvas + Snap guidelines + Live rotation HUD
  const [selBox, setSelBox] = useState<BoxRect | null>(null);
  const [snapGuides, setSnapGuides] = useState<{ v: number[]; h: number[] }>({ v: [], h: [] });
  const [rotateHud, setRotateHud] = useState<{ deg: number; x: number; y: number } | null>(null);
  const [marqueeRect, setMarqueeRect] = useState<{ left: number; top: number; width: number; height: number } | null>(
    null
  );
  const [isDraggingFile, setIsDraggingFile] = useState<boolean>(false);

  // Modals & Popovers
  const [moreMenuOpen, setMoreMenuOpen] = useState<boolean>(false);
  const [captureMode, setCaptureMode] = useState<'shot' | 'rec' | null>(null);
  const [libraryPickerMode, setLibraryPickerMode] = useState<'el' | 'bg' | 'replace-el' | null>(null);
  const [renameTargetId, setRenameTargetId] = useState<string | null>(null);
  const [renameInput, setRenameInput] = useState<string>('');
  const [addLayoutOpen, setAddLayoutOpen] = useState<boolean>(false);
  const [customLayoutW, setCustomLayoutW] = useState<number>(1920);
  const [customLayoutH, setCustomLayoutH] = useState<number>(462);
  const [shortcutModalOpen, setShortcutModalOpen] = useState<boolean>(false);
  const [exitConfirmOpen, setExitConfirmOpen] = useState<boolean>(false);
  const [pendingVideoReplace, setPendingVideoReplace] = useState<{
    action: () => void;
    desc: string;
  } | null>(null);
  const [draftPromptProj, setDraftPromptProj] = useState<{
    project: EditorThemeProject;
    savedAt: number;
  } | null>(null);
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    target: 'element' | 'canvas' | 'layout';
    layoutIdx?: number;
  } | null>(null);
  const [contextSubmenu, setContextSubmenu] = useState<'z' | 'align' | 'transform' | 'add' | null>(
    null
  );

  const stageRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const dragRef = useRef<DragSession | null>(null);
  const fileUploadRef = useRef<HTMLInputElement | null>(null);
  const fileUploadModeRef = useRef<'el' | 'bg'>('el');
  const jsonImportRef = useRef<HTMLInputElement | null>(null);
  const detachNotifiedRef = useRef<boolean>(false);

  useEffect(() => {
    detachNotifiedRef.current = false;
  }, [layoutIndex]);

  const currentLayout = project.layouts[layoutIndex] ?? project.layouts[0];
  const canvasPxW = currentLayout?.size[0] ?? 1920;
  const canvasPxH = currentLayout?.size[1] ?? 462;
  const effectiveZoom = zoomMode === 'fit' ? fitZoom : zoomMode;
  const isOffline = cloudResourceAdapter.isOfflineSimulated();

  const draftStorageKey = `d92-te-draft-${sourceThemeId || project.template_id}`;

  // Check autosave draft on mount
  useEffect(() => {
    try {
      const raw = localStorage.getItem(draftStorageKey);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (
          parsed?.project?.template_id &&
          parsed?.savedAt &&
          parsed.savedAt > initialProject.updated_at + 1000
        ) {
          setDraftPromptProj({ project: parsed.project, savedAt: parsed.savedAt });
        }
      }
    } catch {
      // ignore invalid draft
    }
  }, [draftStorageKey, initialProject.updated_at]);

  // Autosave draft every 1.5s when dirty
  useEffect(() => {
    if (!isDirty) return;
    const t = window.setTimeout(() => {
      try {
        const savedAt = Date.now();
        localStorage.setItem(draftStorageKey, JSON.stringify({ project, savedAt }));
        const d = new Date(savedAt);
        setDraftSavedAt(
          `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:${String(
            d.getSeconds()
          ).padStart(2, '0')}`
        );
      } catch {
        // ignore quota errors
      }
    }, 1500);
    return () => window.clearTimeout(t);
  }, [isDirty, project, draftStorageKey]);

  // Fit canvas zoom calculation on resize / layout change
  useEffect(() => {
    const computeFit = () => {
      const stage = stageRef.current;
      if (!stage) return;
      const rect = stage.getBoundingClientRect();
      const padW = showGridRulers ? 72 : 48;
      const padH = showGridRulers ? 104 : 80;
      const availW = Math.max(240, rect.width - padW);
      const availH = Math.max(160, rect.height - padH);
      const z = Math.min(availW / canvasPxW, availH / canvasPxH, 1.5);
      setFitZoom(Math.max(0.18, Number(z.toFixed(3))));
    };
    computeFit();
    const ro = new ResizeObserver(computeFit);
    if (stageRef.current) ro.observe(stageRef.current);
    return () => ro.disconnect();
  }, [canvasPxW, canvasPxH, showGridRulers, leftCollapsed, rightCollapsed]);

  const commitProject = useCallback(
    (updater: (prev: EditorThemeProject) => EditorThemeProject, recordHistory = true) => {
      setProject((prev) => {
        const next = updater(cloneProj(prev));
        next.updated_at = Date.now();
        if (recordHistory) {
          setHistory((h) => {
            const sliced = h.slice(0, historyIdx + 1);
            const appended = [...sliced, cloneProj(next)].slice(-50);
            setHistoryIdx(appended.length - 1);
            return appended;
          });
        }
        return next;
      });
      setIsDirty(true);
    },
    [historyIdx]
  );

  const handleUndo = useCallback(() => {
    if (historyIdx <= 0) return;
    const nextIdx = historyIdx - 1;
    setHistoryIdx(nextIdx);
    setProject(cloneProj(history[nextIdx]));
    setSelectedIds([]);
    setIsDirty(true);
    onNotify('已撤销上一步操作');
  }, [history, historyIdx, onNotify]);

  const handleRedo = useCallback(() => {
    if (historyIdx >= history.length - 1) return;
    const nextIdx = historyIdx + 1;
    setHistoryIdx(nextIdx);
    setProject(cloneProj(history[nextIdx]));
    setSelectedIds([]);
    setIsDirty(true);
    onNotify('已重做操作');
  }, [history, historyIdx, onNotify]);

  // Helper to get & mutate elements of current layout (respecting master vs unfollowed layout)
  const layoutElements = useMemo(
    () => getLayoutElements(project, layoutIndex),
    [project, layoutIndex]
  );

  const selectedElements = useMemo(
    () => layoutElements.filter((el) => selectedIds.includes(el.id)),
    [layoutElements, selectedIds]
  );

  const mutateElementsInCurrentLayout = useCallback(
    (
      fn: (els: EditorElement[], p: EditorThemeProject) => EditorElement[],
      recordHistory = true
    ) => {
      let didDetach = false;
      commitProject((draft) => {
        didDetach = detachIfFollowing(draft, layoutIndex);
        const lay = draft.layouts[layoutIndex];
        if (layoutIndex === 0 || !lay) {
          draft.master = fn(draft.master, draft);
        } else {
          lay.elements = fn(lay.elements ?? cloneProj(draft).master, draft);
        }
        return draft;
      }, recordHistory);
      if (didDetach && !detachNotifiedRef.current) {
        detachNotifiedRef.current = true;
        onNotify('已自动脱离跟随，当前修改仅作用于当前规格画布');
      }
    },
    [commitProject, layoutIndex, onNotify]
  );

  // Measure DOM bounding box of selected elements relative to [data-te-canvas]
  const measureSelection = useCallback(() => {
    const canvasEl = canvasRef.current;
    if (!canvasEl || selectedIds.length === 0) {
      setSelBox(null);
      return;
    }
    const cRect = canvasEl.getBoundingClientRect();
    let minL = Infinity;
    let minT = Infinity;
    let maxR = -Infinity;
    let maxB = -Infinity;
    let found = 0;

    selectedIds.forEach((id) => {
      const node = canvasEl.querySelector(`[data-el-id="${id}"]`) as HTMLElement | null;
      if (!node) return;
      const r = node.getBoundingClientRect();
      minL = Math.min(minL, r.left - cRect.left);
      minT = Math.min(minT, r.top - cRect.top);
      maxR = Math.max(maxR, r.right - cRect.left);
      maxB = Math.max(maxB, r.bottom - cRect.top);
      found += 1;
    });

    if (!found) {
      setSelBox(null);
      return;
    }
    const rawW = maxR - minL;
    const rawH = maxB - minT;
    const width = Math.max(14, rawW);
    const height = Math.max(14, rawH);
    const left = minL - (width - rawW) / 2;
    const top = minT - (height - rawH) / 2;
    setSelBox({
      left,
      top,
      width,
      height,
      cx: left + width / 2,
      cy: top + height / 2,
    });
  }, [selectedIds]);

  useEffect(() => {
    const sid = selectedIds.length === 1 ? selectedIds[0] : null;
    if (sid !== lastAspectElIdRef.current) {
      lastAspectElIdRef.current = sid;
      if (sid) {
        const target = layoutElements.find((e) => e.id === sid);
        if (target) {
          setAspectLinked(target.kind === 'image' || target.kind === 'video');
        }
      }
    }
  }, [selectedIds, layoutElements]);

  useEffect(() => {
    const raf = window.requestAnimationFrame(measureSelection);
    return () => window.cancelAnimationFrame(raf);
  }, [measureSelection, project, layoutIndex, effectiveZoom, liveTick]);

  // Expand selection by group when clicking a grouped element
  const resolveGroupIds = useCallback(
    (id: string): string[] => {
      const target = layoutElements.find((el) => el.id === id);
      if (!target?.g) return [id];
      return layoutElements.filter((el) => el.g === target.g).map((el) => el.id);
    },
    [layoutElements]
  );

  // 1-Video limit check (V8.0 safeguard)
  const checkVideoLimit = useCallback(
    (incomingType: 'element' | 'bg', onProceed: (p: EditorThemeProject) => EditorThemeProject) => {
      const existingBgVideo = project.bg.kind === 'video';
      const existingElVideo = layoutElements.some((el) => el.kind === 'video');
      if (existingBgVideo || existingElVideo) {
        setPendingVideoReplace({
          desc:
            incomingType === 'bg'
              ? '当前画布已存在视频素材/背景，副屏单主题仅允许保留 1 路硬件视频解码流，是否替换原有视频？'
              : '画布已包含 1 路视频图层或视频背景，是否移除原有视频并插入新视频？',
          action: () => {
            commitProject((draft) => {
              if (draft.bg.kind === 'video') {
                draft.bg = { kind: 'style', style: 'ink', dim: 0, blur: 0, fit: 'cover' };
              }
              draft.master = draft.master.filter((el) => el.kind !== 'video');
              draft.layouts.forEach((l) => {
                if (l.elements) l.elements = l.elements.filter((el) => el.kind !== 'video');
              });
              return onProceed(draft);
            });
          },
        });
        return;
      }
      commitProject(onProceed);
    },
    [project.bg.kind, layoutElements, commitProject]
  );

  const handleInsertElements = useCallback(
    (els: EditorElement[], label?: string) => {
      const hasVideo = els.some((e) => e.kind === 'video');
      let didDetach = false;
      const doInsert = (draft: EditorThemeProject) => {
        didDetach = detachIfFollowing(draft, layoutIndex);
        const lay = draft.layouts[layoutIndex];
        const cloned = els.map((item) => ({ ...item, id: genEditorId(item.kind.slice(0, 3)) }));
        if (cloned.length > 1) {
          const gid = genEditorId('grp');
          cloned.forEach((c) => {
            c.g = gid;
          });
        }
        if (layoutIndex === 0 || !lay) {
          draft.master = [...draft.master, ...cloned];
        } else {
          lay.elements = [...(lay.elements ?? cloneProj(draft).master), ...cloned];
        }
        window.setTimeout(() => {
          setSelectedIds(cloned.map((c) => c.id));
          setAdjBg(false);
        }, 0);
        return draft;
      };

      if (hasVideo) {
        checkVideoLimit('element', doInsert);
      } else {
        commitProject(doInsert);
      }
      if (didDetach && !detachNotifiedRef.current) {
        detachNotifiedRef.current = true;
        onNotify('已自动脱离跟随，新元素仅加入当前规格画布');
      } else if (label) {
        onNotify(`已添加「${label}」`);
      }
    },
    [checkVideoLimit, commitProject, layoutIndex, onNotify]
  );

  // Import local image/video file into canvas or bg
  const importMediaFile = useCallback(
    async (file: File, mode: 'el' | 'bg') => {
      const isVideo = file.type.startsWith('video/');
      const isImage = file.type.startsWith('image/');
      if (!isVideo && !isImage) {
        onNotify('暂不支持该文件格式，请上传 PNG / JPG / GIF / WebP / MP4 / WebM');
        return;
      }

      if (isVideo) {
        const url = URL.createObjectURL(file);
        const dims = await readVideoDimensions(url);
        setRecentImports((prev) => [
          { id: genEditorId('imp'), name: file.name, kind: 'video', src: url, w: dims.w, h: dims.h },
          ...prev.slice(0, 11),
        ]);
        if (mode === 'bg') {
          checkVideoLimit('bg', (draft) => {
            draft.bg = {
              ...draft.bg,
              kind: 'video',
              vurl: url,
              vname: file.name,
              fit: 'cover',
              hide: false,
            };
            return draft;
          });
        } else {
          const box = computeImportMediaBox(dims.w, dims.h, canvasPxW, canvasPxH);
          const vidEl = makeElement('video', {
            src: url,
            w: box.w,
            h: box.h,
            size: box.size,
            ar: box.ar,
            fit: box.fit,
            dur: dims.dur,
            sourceMeta: { origin: 'local', name: file.name, naturalWidth: dims.w, naturalHeight: dims.h },
          }) as VideoElement;
          handleInsertElements(
            [vidEl],
            `${file.name} (${dims.w}×${dims.h}${box.exceeded ? ' · 100%比例适配' : ' · 原尺寸'})`
          );
        }
        return;
      }

      const dataUrl = await readFileAsDataUrl(file);
      const dims = await readImageDimensions(dataUrl);
      setRecentImports((prev) => [
        { id: genEditorId('imp'), name: file.name, kind: 'image', src: dataUrl, w: dims.w, h: dims.h },
        ...prev.slice(0, 11),
      ]);

      if (mode === 'bg') {
        commitProject((draft) => {
          draft.bg = {
            ...draft.bg,
            kind: 'image',
            url: dataUrl,
            fit: 'cover',
            hide: false,
          };
          draft.asset_manifest = [
            ...draft.asset_manifest,
            {
              assetId: genEditorId('ast'),
              kind: 'image',
              name: file.name,
              source: 'local',
              width: dims.w,
              height: dims.h,
            },
          ];
          return draft;
        });
        onNotify(`已设置背景图「${file.name}」(${dims.w}×${dims.h})`);
      } else {
        const box = computeImportMediaBox(dims.w, dims.h, canvasPxW, canvasPxH);
        const imgEl = makeElement('image', {
          src: dataUrl,
          w: box.w,
          h: box.h,
          ar: box.ar,
          size: box.size,
          fit: box.fit,
          sourceMeta: {
            origin: 'local',
            naturalWidth: dims.w,
            naturalHeight: dims.h,
            name: file.name,
          },
        }) as ImageElement;
        handleInsertElements(
          [imgEl],
          `${file.name} (${dims.w}×${dims.h}${box.exceeded ? ' · 100%比例适配' : ' · 原尺寸'})`
        );
      }
    },
    [canvasPxH, canvasPxW, checkVideoLimit, commitProject, handleInsertElements, onNotify]
  );

  // Layer operations
  const handleToggleLock = useCallback(
    (ids?: string[]) => {
      const targets = ids ?? selectedIds;
      if (targets.length === 0) return;
      commitProject((draft) => {
        const allLocked = targets.every((id) => draft.locks[id]);
        targets.forEach((id) => {
          if (allLocked) delete draft.locks[id];
          else draft.locks[id] = true;
        });
        return draft;
      });
    },
    [commitProject, selectedIds]
  );

  const handleToggleHide = useCallback(
    (ids?: string[]) => {
      const targets = ids ?? selectedIds;
      if (targets.length === 0) return;
      commitProject((draft) => {
        const lay = draft.layouts[layoutIndex];
        if (layoutIndex > 0 && lay && lay.follow) {
          lay.vis = lay.vis ?? {};
          const allHidden = targets.every((id) => lay.vis![id]);
          targets.forEach((id) => {
            if (allHidden) delete lay.vis![id];
            else lay.vis![id] = true;
          });
        } else {
          const list = layoutIndex === 0 || !lay?.elements ? draft.master : lay.elements;
          const allHidden = targets.every((id) => list.find((e) => e.id === id)?.hide);
          list.forEach((el) => {
            if (targets.includes(el.id)) el.hide = !allHidden;
          });
        }
        return draft;
      });
    },
    [commitProject, layoutIndex, selectedIds]
  );

  const handleReorderLayer = useCallback(
    (op: 'top' | 'up' | 'down' | 'bottom', targetId?: string) => {
      const ids = targetId ? [targetId] : selectedIds;
      if (ids.length === 0) return;
      mutateElementsInCurrentLayout((els) => {
        const copy = [...els];
        const idx = copy.findIndex((e) => e.id === ids[0]);
        if (idx === -1) return els;
        const [item] = copy.splice(idx, 1);
        if (op === 'top') copy.push(item);
        else if (op === 'bottom') copy.unshift(item);
        else if (op === 'up') copy.splice(Math.min(copy.length, idx + 1), 0, item);
        else copy.splice(Math.max(0, idx - 1), 0, item);
        return copy;
      });
    },
    [mutateElementsInCurrentLayout, selectedIds]
  );

  const handleSetLayerIndex = useCallback(
    (targetIdx: number, targetId?: string) => {
      const ids = targetId ? [targetId] : selectedIds;
      if (ids.length === 0) return;
      mutateElementsInCurrentLayout((els) => {
        const copy = [...els];
        const idx = copy.findIndex((e) => e.id === ids[0]);
        if (idx === -1) return els;
        const clamped = Math.max(0, Math.min(copy.length - 1, targetIdx));
        if (clamped === idx) return els;
        const [item] = copy.splice(idx, 1);
        copy.splice(clamped, 0, item);
        return copy;
      });
    },
    [mutateElementsInCurrentLayout, selectedIds]
  );

  const handleDuplicateSelection = useCallback(() => {
    if (selectedElements.length === 0) return;
    const dup = selectedElements.map((el) => ({
      ...structuredClone(el),
      id: genEditorId(el.kind.slice(0, 3)),
      x: +(el.x + 2.5).toFixed(2),
      y: +(el.y + 3.5).toFixed(2),
      g: undefined,
    }));
    handleInsertElements(dup, `副本 (${dup.length}项)`);
  }, [selectedElements, handleInsertElements]);

  const handleDeleteSelection = useCallback(() => {
    if (selectedIds.length === 0) return;
    const idSet = new Set(selectedIds);
    mutateElementsInCurrentLayout((els, draft) => {
      selectedIds.forEach((id) => {
        delete draft.locks[id];
        delete draft.names[id];
      });
      return els.filter((e) => !idSet.has(e.id));
    });
    setSelectedIds([]);
    onNotify(`已删除 ${idSet.size} 个图层`);
  }, [mutateElementsInCurrentLayout, onNotify, selectedIds]);

  const handleGroupSelection = useCallback(() => {
    if (selectedIds.length < 2) return;
    const gid = genEditorId('grp');
    const idSet = new Set(selectedIds);
    mutateElementsInCurrentLayout((els) => els.map((e) => (idSet.has(e.id) ? { ...e, g: gid } : e)));
    onNotify(`已将 ${selectedIds.length} 个图层编组`);
  }, [mutateElementsInCurrentLayout, onNotify, selectedIds]);

  const handleUngroupSelection = useCallback(() => {
    const idSet = new Set(selectedIds);
    mutateElementsInCurrentLayout((els) =>
      els.map((e) => (idSet.has(e.id) ? { ...e, g: undefined } : e))
    );
    onNotify('已取消组合');
  }, [mutateElementsInCurrentLayout, onNotify, selectedIds]);

  // Align & Even Distribution (supports edge/center + 9-point anchor snap)
  const handleAlignSelection = useCallback(
    (mode: InspectorAlignMode) => {
      if (selectedIds.length === 0) return;
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          const next = { ...el };
          if (mode === 'l') {
            next.a = `${next.a[0]}l` as typeof next.a;
            next.x = 4;
          } else if (mode === 'hc') {
            next.a = `${next.a[0]}c` as typeof next.a;
            next.x = 0;
          } else if (mode === 'r') {
            next.a = `${next.a[0]}r` as typeof next.a;
            next.x = -4;
          } else if (mode === 't') {
            next.a = `t${next.a[1]}` as typeof next.a;
            next.y = 4;
          } else if (mode === 'vc') {
            next.a = `m${next.a[1]}` as typeof next.a;
            next.y = 0;
          } else if (mode === 'b') {
            next.a = `b${next.a[1]}` as typeof next.a;
            next.y = -4;
          } else if (mode === 'c' || mode === 'mc') {
            next.a = 'mc';
            next.x = 0;
            next.y = 0;
          } else {
            next.a = mode;
            next.x = mode[1] === 'l' ? 4 : mode[1] === 'r' ? -4 : 0;
            next.y = mode[0] === 't' ? 4 : mode[0] === 'b' ? -4 : 0;
          }
          return next;
        })
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  const handleDistributeSelection = useCallback(
    (axis: 'h' | 'v') => {
      if (selectedIds.length < 3) {
        onNotify('至少需要选中 3 个元素才能等距分布');
        return;
      }
      const selected = layoutElements.filter((e) => selectedIds.includes(e.id));
      const coords = selected.map((el) => {
        const fx = FX_ANCHOR[el.a[1] as 'l' | 'c' | 'r'] ?? 50;
        const fy = FY_ANCHOR[el.a[0] as 't' | 'm' | 'b'] ?? 50;
        return {
          id: el.id,
          absX: fx + el.x,
          absY: fy + el.y,
        };
      });
      coords.sort((a, b) => (axis === 'h' ? a.absX - b.absX : a.absY - b.absY));
      const first = axis === 'h' ? coords[0].absX : coords[0].absY;
      const last = axis === 'h' ? coords[coords.length - 1].absX : coords[coords.length - 1].absY;
      const step = (last - first) / (coords.length - 1);
      const targetPos = new Map(coords.map((c, idx) => [c.id, first + step * idx]));

      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          const val = targetPos.get(el.id);
          if (val === undefined || project.locks[el.id]) return el;
          const fx = FX_ANCHOR[el.a[1] as 'l' | 'c' | 'r'] ?? 50;
          const fy = FY_ANCHOR[el.a[0] as 't' | 'm' | 'b'] ?? 50;
          return axis === 'h'
            ? { ...el, x: +(val - fx).toFixed(2) }
            : { ...el, y: +(val - fy).toFixed(2) };
        })
      );
      onNotify(axis === 'h' ? '已完成水平等距分布' : '已完成垂直等距分布');
    },
    [layoutElements, mutateElementsInCurrentLayout, onNotify, project.locks, selectedIds]
  );

  const handleBulkScale = useCallback(
    (factor: number) => {
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          const next = { ...el, size: +Math.max(2, el.size * factor).toFixed(1) } as EditorElement & {
            w?: number;
            h?: number;
          };
          if (typeof next.w === 'number') {
            next.w = +Math.max(0.2, next.w * factor).toFixed(2);
          }
          if (typeof next.h === 'number') {
            next.h = +Math.max(0.2, next.h * factor).toFixed(2);
          }
          return next as EditorElement;
        })
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  const handleBulkRotate = useCallback(
    (deltaDeg: number) => {
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          return { ...el, rot: (((el.rot || 0) + deltaDeg) % 360 + 360) % 360 };
        })
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  // Keyboard shortcuts
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const activeTag = (document.activeElement?.tagName || '').toLowerCase();
      const isEditingInput =
        activeTag === 'input' || activeTag === 'textarea' || activeTag === 'select';

      if (e.code === 'Space' && !isEditingInput) {
        setSpacePressed(true);
      }

      if (editingTextId) {
        if (e.key === 'Escape') setEditingTextId(null);
        return;
      }

      if (isEditingInput) return;

      const meta = e.ctrlKey || e.metaKey;
      if (meta && e.key.toLowerCase() === 'z') {
        e.preventDefault();
        if (e.shiftKey) handleRedo();
        else handleUndo();
        return;
      }
      if (meta && e.key.toLowerCase() === 'y') {
        e.preventDefault();
        handleRedo();
        return;
      }
      if (meta && e.key.toLowerCase() === 'c' && selectedElements.length > 0) {
        e.preventDefault();
        setClipboard(structuredClone(selectedElements));
        onNotify(`已复制 ${selectedElements.length} 个图层`);
        return;
      }
      if (meta && e.key.toLowerCase() === 'v' && clipboard.length > 0) {
        e.preventDefault();
        const pasted = clipboard.map((c) => ({
          ...structuredClone(c),
          x: +(c.x + 2).toFixed(2),
          y: +(c.y + 3).toFixed(2),
          g: undefined,
        }));
        handleInsertElements(pasted, `粘贴 (${pasted.length}项)`);
        return;
      }
      if (meta && e.key.toLowerCase() === 'd' && selectedElements.length > 0) {
        e.preventDefault();
        handleDuplicateSelection();
        return;
      }
      if (meta && e.key.toLowerCase() === 'a') {
        e.preventDefault();
        setSelectedIds(
          layoutElements
            .filter((el) => !isElementHidden(project, layoutIndex, el))
            .map((el) => el.id)
        );
        return;
      }
      if (meta && e.key.toLowerCase() === 'g') {
        e.preventDefault();
        if (e.shiftKey) handleUngroupSelection();
        else handleGroupSelection();
        return;
      }
      if ((e.key === 'Delete' || e.key === 'Backspace') && selectedIds.length > 0) {
        e.preventDefault();
        handleDeleteSelection();
        return;
      }
      if (e.key === ']') {
        e.preventDefault();
        setRightCollapsed((c) => !c);
        return;
      }
      if (e.key === 'Escape') {
        setContextMenu(null);
        setMoreMenuOpen(false);
        setAdjBg(false);
        setSelectedIds([]);
        return;
      }
      if (
        ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key) &&
        selectedIds.length > 0
      ) {
        e.preventDefault();
        const step = e.shiftKey ? 2 : 0.5;
        const dx = e.key === 'ArrowLeft' ? -step : e.key === 'ArrowRight' ? step : 0;
        const dy = e.key === 'ArrowUp' ? -step : e.key === 'ArrowDown' ? step : 0;
        const idSet = new Set(selectedIds);
        mutateElementsInCurrentLayout((els) =>
          els.map((el) =>
            idSet.has(el.id) && !project.locks[el.id]
              ? { ...el, x: +(el.x + dx).toFixed(2), y: +(el.y + dy).toFixed(2) }
              : el
          )
        );
      }
    };

    const onKeyUp = (e: KeyboardEvent) => {
      if (e.code === 'Space') setSpacePressed(false);
    };

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
    };
  }, [
    clipboard,
    editingTextId,
    handleDeleteSelection,
    handleDuplicateSelection,
    handleGroupSelection,
    handleInsertElements,
    handleRedo,
    handleUndo,
    handleUngroupSelection,
    layoutElements,
    layoutIndex,
    mutateElementsInCurrentLayout,
    onNotify,
    project,
    selectedElements,
    selectedIds,
  ]);

  // Global Pointer Move & Pointer Up for smooth dragging, resizing, rotating, marquee & pan
  useEffect(() => {
    const onPointerMove = (e: PointerEvent) => {
      const drag = dragRef.current;
      if (!drag) return;

      if (drag.type === 'pan') {
        const st = stageRef.current;
        if (!st) return;
        st.scrollLeft = drag.origScrollLeft - (e.clientX - drag.startX);
        st.scrollTop = drag.origScrollTop - (e.clientY - drag.startY);
        return;
      }

      if (drag.type === 'bg-drag') {
        const dx = ((e.clientX - drag.startX) / drag.canvasW) * 100;
        const dy = ((e.clientY - drag.startY) / drag.canvasH) * 100;
        drag.moved = true;
        commitProject((draft) => {
          draft.bg.tx = +Math.max(-60, Math.min(60, drag.origTx + dx)).toFixed(1);
          draft.bg.ty = +Math.max(-60, Math.min(60, drag.origTy + dy)).toFixed(1);
          return draft;
        }, false);
        return;
      }

      if (drag.type === 'marquee') {
        drag.curClientX = e.clientX;
        drag.curClientY = e.clientY;
        const stageRect = stageRef.current?.getBoundingClientRect();
        if (stageRect) {
          const l = Math.min(drag.startClientX, drag.curClientX) - stageRect.left + (stageRef.current?.scrollLeft ?? 0);
          const t = Math.min(drag.startClientY, drag.curClientY) - stageRect.top + (stageRef.current?.scrollTop ?? 0);
          const w = Math.abs(drag.curClientX - drag.startClientX);
          const h = Math.abs(drag.curClientY - drag.startClientY);
          setMarqueeRect({ left: l, top: t, width: w, height: h });
        }
        return;
      }

      if (drag.type === 'move') {
        const dxPct = ((e.clientX - drag.startX) / drag.canvasW) * 100;
        const dyPct = ((e.clientY - drag.startY) / drag.canvasH) * 100;
        if (Math.abs(dxPct) > 0.1 || Math.abs(dyPct) > 0.1) drag.moved = true;

        let snapDx = 0;
        let snapDy = 0;
        const activeV: number[] = [];
        const activeH: number[] = [];

        if (!e.altKey && drag.ids.length === 1) {
          if (drag.origBoxPct && drag.siblingTargets) {
            const box = drag.origBoxPct;
            const curV = [box.l + dxPct, box.cx + dxPct, box.r + dxPct];
            const curH = [box.t + dyPct, box.cy + dyPct, box.b + dyPct];

            let bestVDist = 1.15;
            let bestVTgt: number | null = null;
            for (const val of curV) {
              for (const tgt of drag.siblingTargets.v) {
                const d = Math.abs(val - tgt);
                if (d < bestVDist) {
                  bestVDist = d;
                  snapDx = tgt - val;
                  bestVTgt = tgt;
                }
              }
            }
            if (bestVTgt !== null) activeV.push(bestVTgt);

            let bestHDist = 1.15;
            let bestHTgt: number | null = null;
            for (const val of curH) {
              for (const tgt of drag.siblingTargets.h) {
                const d = Math.abs(val - tgt);
                if (d < bestHDist) {
                  bestHDist = d;
                  snapDy = tgt - val;
                  bestHTgt = tgt;
                }
              }
            }
            if (bestHTgt !== null) activeH.push(bestHTgt);
          } else {
            const lead = layoutElements.find((el) => el.id === drag.ids[0]);
            const orig = drag.origins[drag.ids[0]];
            if (lead && orig) {
              const fx = FX_ANCHOR[lead.a[1] as 'l' | 'c' | 'r'] ?? 50;
              const fy = FY_ANCHOR[lead.a[0] as 't' | 'm' | 'b'] ?? 50;
              const curX = fx + orig.x + dxPct;
              const curY = fy + orig.y + dyPct;
              [4, 50, 96].forEach((target) => {
                if (Math.abs(curX - target) < 1.3) {
                  snapDx = target - curX;
                  activeV.push(target);
                }
                if (Math.abs(curY - target) < 1.3) {
                  snapDy = target - curY;
                  activeH.push(target);
                }
              });
            }
          }
        }
        setSnapGuides({ v: activeV, h: activeH });

        const idSet = new Set(drag.ids);
        mutateElementsInCurrentLayout(
          (els) =>
            els.map((el) => {
              if (!idSet.has(el.id)) return el;
              const o = drag.origins[el.id];
              if (!o) return el;
              return {
                ...el,
                x: +(o.x + dxPct + snapDx).toFixed(2),
                y: +(o.y + dyPct + snapDy).toFixed(2),
              };
            }),
          false
        );
        return;
      }

      if (drag.type === 'resize') {
        const dx = e.clientX - drag.startX;
        const dy = e.clientY - drag.startY;
        drag.moved = true;
        const h = drag.handle;
        const orig = drag.origEl;
        const rad = ((orig.rot || 0) * Math.PI) / 180;
        const cos = Math.cos(rad);
        const sin = Math.sin(rad);
        const localDx = dx * cos + dy * sin;
        const localDy = -dx * sin + dy * cos;
        const [fx, fy] = resolveAnchorPercentages(orig.a);

        const isBoxKind =
          orig.kind === 'shape' ||
          orig.kind === 'motion' ||
          orig.kind === 'widget' ||
          orig.kind === 'plugin' ||
          orig.kind === 'image' ||
          orig.kind === 'video';

        if (isBoxKind) {
          const minSize =
            orig.kind === 'shape'
              ? 0.2
              : orig.kind === 'image' || orig.kind === 'video'
                ? 0.5
                : orig.kind === 'motion'
                  ? 1
                  : 2;

          let baseW: number;
          let baseH: number;
          if ('w' in orig && typeof orig.w === 'number' && 'h' in orig && typeof orig.h === 'number') {
            baseW = orig.w;
            baseH = orig.h;
          } else {
            const minCanvasPx = Math.min(drag.canvasW, drag.canvasH);
            const pxH = (orig.size * minCanvasPx) / 100;
            const ar = 'ar' in orig && orig.ar ? orig.ar : 1;
            const pxW = pxH * ar;
            baseW = (pxW / drag.canvasW) * 100;
            baseH = (pxH / drag.canvasH) * 100;
          }

          const dw = ((h.includes('r') ? localDx : h.includes('l') ? -localDx : 0) / drag.canvasW) * 100;
          const dh = ((h.includes('b') ? localDy : h.includes('t') ? -localDy : 0) / drag.canvasH) * 100;
          const isCorner = h.length === 2;
          const lockRatio = isCorner && (e.shiftKey ? !aspectLinked : aspectLinked);

          let nextW = h.includes('l') || h.includes('r') ? Math.max(minSize, baseW + dw) : baseW;
          let nextH = h.includes('t') || h.includes('b') ? Math.max(minSize, baseH + dh) : baseH;

          if (lockRatio) {
            const normW = dw / Math.max(0.5, baseW);
            const normH = dh / Math.max(0.5, baseH);
            const deltaNorm = Math.abs(normW) >= Math.abs(normH) ? normW : normH;
            const minFactor = Math.max(minSize / Math.max(0.2, baseW), minSize / Math.max(0.2, baseH));
            const factor = Math.max(minFactor, 1 + deltaNorm);
            nextW = baseW * factor;
            nextH = baseH * factor;
          }

          nextW = +Math.min(1000, nextW).toFixed(2);
          nextH = +Math.min(1000, nextH).toFixed(2);

          let nextX = orig.x;
          let nextY = orig.y;
          if (Math.abs(orig.rot || 0) < 0.5) {
            if (h.includes('r')) nextX = orig.x + (fx / 100) * (nextW - baseW);
            else if (h.includes('l')) nextX = orig.x - ((100 - fx) / 100) * (nextW - baseW);
            if (h.includes('b')) nextY = orig.y + (fy / 100) * (nextH - baseH);
            else if (h.includes('t')) nextY = orig.y - ((100 - fy) / 100) * (nextH - baseH);
          }

          const syncedSize =
            orig.kind === 'image' || orig.kind === 'video'
              ? +Math.max(
                  2,
                  (nextH * drag.canvasH) / Math.max(1, Math.min(drag.canvasW, drag.canvasH))
                ).toFixed(1)
              : orig.size;

          mutateElementsInCurrentLayout(
            (els) =>
              els.map((el) =>
                el.id === drag.elId
                  ? ({
                      ...el,
                      w: nextW,
                      h: nextH,
                      x: +nextX.toFixed(2),
                      y: +nextY.toFixed(2),
                      size: syncedSize,
                    } as EditorElement)
                  : el
              ),
            false
          );
        } else if (orig.kind === 'text') {
          const isVert = Boolean(orig.vert);
          if (!isVert && (h === 'ml' || h === 'mr')) {
            const baseW =
              orig.w ?? +((drag.origBox.width / Math.max(1, drag.canvasW)) * 100).toFixed(2);
            const dw = ((h === 'mr' ? localDx : -localDx) / drag.canvasW) * 100;
            const nextW = +Math.max(3, Math.min(100, baseW + dw)).toFixed(2);
            let nextX = orig.x;
            if (Math.abs(orig.rot || 0) < 0.5) {
              nextX =
                h === 'mr'
                  ? orig.x + (fx / 100) * (nextW - baseW)
                  : orig.x - ((100 - fx) / 100) * (nextW - baseW);
            }
            mutateElementsInCurrentLayout(
              (els) =>
                els.map((el) =>
                  el.id === drag.elId
                    ? ({ ...el, w: nextW, x: +nextX.toFixed(2) } as TextElement)
                    : el
                ),
              false
            );
          } else if (isVert && (h === 'tc' || h === 'bc')) {
            const baseH =
              orig.h ?? +((drag.origBox.height / Math.max(1, drag.canvasH)) * 100).toFixed(2);
            const dh = ((h === 'bc' ? localDy : -localDy) / drag.canvasH) * 100;
            const nextH = +Math.max(4, Math.min(100, baseH + dh)).toFixed(2);
            let nextY = orig.y;
            if (Math.abs(orig.rot || 0) < 0.5) {
              nextY =
                h === 'bc'
                  ? orig.y + (fy / 100) * (nextH - baseH)
                  : orig.y - ((100 - fy) / 100) * (nextH - baseH);
            }
            mutateElementsInCurrentLayout(
              (els) =>
                els.map((el) =>
                  el.id === drag.elId
                    ? ({ ...el, h: nextH, y: +nextY.toFixed(2) } as TextElement)
                    : el
                ),
              false
            );
          } else {
            const sx = h.includes('r') ? localDx : h.includes('l') ? -localDx : 0;
            const sy = h.includes('b') ? localDy : h.includes('t') ? -localDy : 0;
            const normX = sx / Math.max(40, drag.origBox.width);
            const normY = sy / Math.max(24, drag.origBox.height);
            const normDelta = Math.abs(normX) >= Math.abs(normY) ? normX : normY;
            const factor = Math.max(0.25, 1 + normDelta);
            const nextSize = +Math.max(3, Math.min(95, orig.size * factor)).toFixed(1);
            const appliedFactor = nextSize / Math.max(1, orig.size);
            mutateElementsInCurrentLayout(
              (els) =>
                els.map((el) =>
                  el.id === drag.elId
                    ? ({
                        ...el,
                        size: nextSize,
                        ...(orig.w != null
                          ? { w: +Math.max(3, Math.min(100, orig.w * appliedFactor)).toFixed(2) }
                          : {}),
                        ...(orig.h != null
                          ? { h: +Math.max(4, Math.min(100, orig.h * appliedFactor)).toFixed(2) }
                          : {}),
                      } as TextElement)
                    : el
                ),
              false
            );
          }
        } else {
          const sx = h.includes('r') ? localDx : h.includes('l') ? -localDx : 0;
          const sy = h.includes('b') ? localDy : h.includes('t') ? -localDy : 0;
          const normX = sx / Math.max(40, drag.origBox.width);
          const normY = sy / Math.max(24, drag.origBox.height);
          const normDelta = Math.abs(normX) >= Math.abs(normY) ? normX : normY;
          const factor = Math.max(0.25, 1 + normDelta);
          const nextSize = +Math.max(3, Math.min(95, orig.size * factor)).toFixed(1);
          mutateElementsInCurrentLayout(
            (els) => els.map((el) => (el.id === drag.elId ? { ...el, size: nextSize } : el)),
            false
          );
        }
        return;
      }

      if (drag.type === 'rotate') {
        drag.moved = true;
        const curAngle = (Math.atan2(e.clientY - drag.cy, e.clientX - drag.cx) * 180) / Math.PI;
        let deg = (drag.origRot + (curAngle - drag.startAngle)) % 360;
        if (deg < 0) deg += 360;
        if (e.shiftKey) {
          deg = Math.round(deg / 15) * 15;
        } else {
          const snapTargets = [0, 90, 180, 270, 360];
          const near = snapTargets.find((s) => Math.abs(deg - s) < 3.5);
          if (near !== undefined) deg = near % 360;
        }
        const finalDeg = Math.round(deg);
        setRotateHud({ deg: finalDeg, x: e.clientX + 14, y: e.clientY - 12 });
        mutateElementsInCurrentLayout(
          (els) => els.map((el) => (el.id === drag.elId ? { ...el, rot: finalDeg } : el)),
          false
        );
      }
    };

    const onPointerUp = () => {
      const drag = dragRef.current;
      if (!drag) return;
      dragRef.current = null;
      setSnapGuides({ v: [], h: [] });
      setRotateHud(null);

      if (drag.type === 'marquee') {
        setMarqueeRect(null);
        const canvasEl = canvasRef.current;
        if (canvasEl && Math.abs(drag.curClientX - drag.startClientX) > 6) {
          const left = Math.min(drag.startClientX, drag.curClientX);
          const right = Math.max(drag.startClientX, drag.curClientX);
          const top = Math.min(drag.startClientY, drag.curClientY);
          const bottom = Math.max(drag.startClientY, drag.curClientY);
          const hits: string[] = [];
          canvasEl.querySelectorAll('[data-el-id]').forEach((node) => {
            const r = node.getBoundingClientRect();
            const overlaps = r.left <= right && r.right >= left && r.top <= bottom && r.bottom >= top;
            if (overlaps) {
              const id = node.getAttribute('data-el-id');
              if (id && !project.locks[id]) hits.push(id);
            }
          });
          setSelectedIds(hits);
          setAdjBg(false);
        }
        return;
      }

      if ('moved' in drag && drag.moved) {
        commitProject((p) => p, true);
      }
    };

    window.addEventListener('pointermove', onPointerMove);
    window.addEventListener('pointerup', onPointerUp);
    return () => {
      window.removeEventListener('pointermove', onPointerMove);
      window.removeEventListener('pointerup', onPointerUp);
    };
  }, [aspectLinked, commitProject, layoutElements, mutateElementsInCurrentLayout, project.locks]);

  const handlePointerDownElement = useCallback(
    (e: React.PointerEvent, id: string) => {
      if (e.button === 2) {
        const groupIds = resolveGroupIds(id);
        setSelectedIds(groupIds);
        setContextMenu({ x: e.clientX, y: e.clientY, target: 'element' });
        return;
      }
      if (spacePressed || e.button === 1) return;
      e.stopPropagation();
      setContextMenu(null);
      setMoreMenuOpen(false);
      setAdjBg(false);

      const groupIds = resolveGroupIds(id);
      let nextSelection: string[];
      if (e.shiftKey || e.metaKey || e.ctrlKey) {
        const already = selectedIds.includes(id);
        nextSelection = already
          ? selectedIds.filter((s) => !groupIds.includes(s))
          : Array.from(new Set([...selectedIds, ...groupIds]));
      } else {
        nextSelection = selectedIds.includes(id) ? selectedIds : groupIds;
      }
      setSelectedIds(nextSelection);

      if (nextSelection.some((sid) => project.locks[sid])) return;
      const canvasEl = canvasRef.current;
      if (!canvasEl) return;
      const cRect = canvasEl.getBoundingClientRect();
      const cw = Math.max(1, cRect.width);
      const ch = Math.max(1, cRect.height);
      const origins: Record<string, { x: number; y: number }> = {};
      layoutElements.forEach((el) => {
        if (nextSelection.includes(el.id)) origins[el.id] = { x: el.x, y: el.y };
      });

      let origBoxPct: { l: number; cx: number; r: number; t: number; cy: number; b: number } | undefined;
      let siblingTargets: { v: number[]; h: number[] } | undefined;
      if (nextSelection.length === 1) {
        const targetNode = canvasEl.querySelector(`[data-el-id="${nextSelection[0]}"]`) as HTMLElement | null;
        if (targetNode) {
          const tr = targetNode.getBoundingClientRect();
          origBoxPct = {
            l: ((tr.left - cRect.left) / cw) * 100,
            cx: ((tr.left + tr.width / 2 - cRect.left) / cw) * 100,
            r: ((tr.right - cRect.left) / cw) * 100,
            t: ((tr.top - cRect.top) / ch) * 100,
            cy: ((tr.top + tr.height / 2 - cRect.top) / ch) * 100,
            b: ((tr.bottom - cRect.top) / ch) * 100,
          };
          const vSet = new Set<number>([4, 50, 96]);
          const hSet = new Set<number>([4, 50, 96]);
          canvasEl.querySelectorAll('[data-el-id]').forEach((node) => {
            const nid = node.getAttribute('data-el-id');
            if (!nid || nid === nextSelection[0]) return;
            const r = node.getBoundingClientRect();
            vSet.add(((r.left - cRect.left) / cw) * 100);
            vSet.add(((r.left + r.width / 2 - cRect.left) / cw) * 100);
            vSet.add(((r.right - cRect.left) / cw) * 100);
            hSet.add(((r.top - cRect.top) / ch) * 100);
            hSet.add(((r.top + r.height / 2 - cRect.top) / ch) * 100);
            hSet.add(((r.bottom - cRect.top) / ch) * 100);
          });
          siblingTargets = { v: Array.from(vSet), h: Array.from(hSet) };
        }
      }

      dragRef.current = {
        type: 'move',
        startX: e.clientX,
        startY: e.clientY,
        canvasW: cw,
        canvasH: ch,
        ids: nextSelection,
        origins,
        origBoxPct,
        siblingTargets,
        moved: false,
      };
    },
    [layoutElements, project.locks, resolveGroupIds, selectedIds, spacePressed]
  );

  const handleStagePointerDown = useCallback(
    (e: React.PointerEvent) => {
      setContextMenu(null);
      setMoreMenuOpen(false);

      if (spacePressed || e.button === 1) {
        const st = stageRef.current;
        if (!st) return;
        dragRef.current = {
          type: 'pan',
          startX: e.clientX,
          startY: e.clientY,
          origScrollLeft: st.scrollLeft,
          origScrollTop: st.scrollTop,
        };
        return;
      }

      if (e.button === 2) return;

      if (adjBg) {
        const cRect = canvasRef.current?.getBoundingClientRect();
        if (cRect) {
          dragRef.current = {
            type: 'bg-drag',
            startX: e.clientX,
            startY: e.clientY,
            canvasW: Math.max(1, cRect.width),
            canvasH: Math.max(1, cRect.height),
            origTx: project.bg.tx ?? 0,
            origTy: project.bg.ty ?? 0,
            moved: false,
          };
        }
        return;
      }

      if (editingTextId) setEditingTextId(null);
      setSelectedIds([]);
      dragRef.current = {
        type: 'marquee',
        startClientX: e.clientX,
        startClientY: e.clientY,
        curClientX: e.clientX,
        curClientY: e.clientY,
      };
    },
    [adjBg, editingTextId, project.bg.tx, project.bg.ty, spacePressed]
  );

  // Build ThemeItem output from current project
  const buildThemeItem = useCallback((): ThemeItem => {
    const now = Date.now();
    const isPortrait = canvasPxW < canvasPxH;
    const syncedProject: EditorThemeProject = {
      ...project,
      updated_at: now,
      supported_resolutions: Array.from(
        new Set(project.layouts.map((l) => `${l.size[0]}x${l.size[1]}`))
      ),
    };
    const existing = themes.find((t) => t.id === (sourceThemeId || project.template_id));
    return {
      id: existing?.source === 'custom' ? existing.id : `custom-${project.template_id}`,
      name: project.name || 'D92_未命名主题',
      displayName: project.name || 'D92_未命名主题',
      category: project.category,
      resolution: `${canvasPxW}×${canvasPxH}`,
      orientation: isPortrait ? 'portrait' : 'landscape',
      imageUrl: project.bg.kind === 'image' ? project.bg.url : undefined,
      clockStyle: 'none',
      author: project.author || '我的创作',
      source: 'custom',
      inLibrary: true,
      downloads: '本地自制 V2',
      downloadCount: existing?.downloadCount ?? 0,
      publishedAt: existing?.publishedAt ?? now,
      releasedLabel: '刚刚保存',
      addedAt: existing?.addedAt ?? now,
      addedLabel: existing?.addedLabel ?? '刚刚创建',
      lastUsedAt: now,
      useCount: (existing?.useCount ?? 0) + 1,
      durationMinutes: 15,
      inPlaylist: existing?.inPlaylist ?? true,
      isFavorite: existing?.isFavorite ?? true,
      isRecent: true,
      accentHex: '#5E45F8',
      editorProject: syncedProject,
    };
  }, [canvasPxH, canvasPxW, project, sourceThemeId, themes]);

  const handleSave = (applyNow: boolean) => {
    const out = buildThemeItem();
    try {
      localStorage.removeItem(draftStorageKey);
    } catch {
      // ignore
    }
    setIsDirty(false);
    setDraftSavedAt(null);
    if (applyNow) {
      onSaveAndApply(out, out.editorProject!);
    } else {
      onSaveOnly(out, out.editorProject!);
    }
  };

  const handleSwitchDeviceSpec = (specSize: [number, number]) => {
    const foundIdx = project.layouts.findIndex(
      (l) => l.size[0] === specSize[0] && l.size[1] === specSize[1]
    );
    if (foundIdx >= 0) {
      setLayoutIndex(foundIdx);
      setSelectedIds([]);
      onNotify(`已切换编辑画布至 ${specLabelFromSize(specSize)} (${specSize[0]}×${specSize[1]})`);
      return;
    }
    commitProject((draft) => {
      draft.layouts.push({
        size: specSize,
        label: specLabelFromSize(specSize),
        follow: true,
      });
      return draft;
    });
    setLayoutIndex(project.layouts.length);
    setSelectedIds([]);
    onNotify(`已自动新增 ${specLabelFromSize(specSize)} 跟随画布`);
  };

  const singleSelected = selectedElements.length === 1 ? selectedElements[0] : null;
  const singleSelectedLocked = singleSelected ? Boolean(project.locks[singleSelected.id]) : false;

  return (
    <div
      className="flex h-full w-full flex-col overflow-hidden bg-[#F5F4FA] text-[#1A172B] select-none"
      onClick={() => {
        if (contextMenu) setContextMenu(null);
        if (moreMenuOpen) setMoreMenuOpen(false);
      }}
    >
      {/* Hidden local file inputs */}
      <input
        ref={fileUploadRef}
        type="file"
        accept="image/*,video/mp4,video/webm"
        className="hidden"
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) void importMediaFile(f, fileUploadModeRef.current);
          e.target.value = '';
        }}
      />
      <input
        ref={jsonImportRef}
        type="file"
        accept=".json,.d92theme.json"
        className="hidden"
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (!f) return;
          const reader = new FileReader();
          reader.onload = () => {
            try {
              const parsed = JSON.parse(String(reader.result || '{}'));
              if (!parsed.bg || !Array.isArray(parsed.master)) {
                onNotify('JSON 格式校验失败：缺少有效的背景或图层结构');
                return;
              }
              commitProject(() => ({
                ...parsed,
                template_id: parsed.template_id || genEditorId('imp'),
                schema_version: parsed.schema_version || THEME_SCHEMA_VERSION,
              }));
              setLayoutIndex(0);
              setSelectedIds([]);
              onNotify(`已导入工程文件「${parsed.name || f.name}」`);
            } catch {
              onNotify('工程 JSON 解析失败');
            }
          };
          reader.readAsText(f);
          e.target.value = '';
        }}
      />

      {/* ==================== ZONE A: TOP TOOLBAR (56px) ==================== */}
      <header className="relative z-30 flex h-14 shrink-0 items-center justify-between gap-2 border-b border-[#E6E4F2] bg-white/92 px-3 backdrop-blur-xl shadow-[0_2px_12px_rgba(30,20,80,0.04)]">
        {/* Left: Return, Title, Category, Dirty Status */}
        <div className="flex min-w-0 items-center gap-2">
          <button
            type="button"
            onClick={() => {
              if (isDirty) setExitConfirmOpen(true);
              else onExitEditor();
            }}
            className="flex h-8 items-center gap-1 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8] whitespace-nowrap"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            <span>返回创作中心</span>
          </button>

          <input
            type="text"
            value={project.name}
            onChange={(e) =>
              commitProject((d) => {
                d.name = e.target.value;
                return d;
              }, false)
            }
            aria-label="主题名称"
            className="w-36 sm:w-48 truncate rounded-xl border border-transparent bg-[#F5F4FA] px-2.5 py-1 text-[13px] font-bold text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
          />

          <select
            value={project.category === '动态时钟' ? '动态时钟' : '主题'}
            onChange={(e) =>
              commitProject((d) => {
                d.category = e.target.value as ContentCategory;
                return d;
              })
            }
            aria-label="主题分类"
            className="hidden md:block rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11.5px] font-semibold text-[#56526B] focus:border-[#5E45F8] focus:outline-none"
          >
            <option value="主题">桌面主题</option>
            <option value="动态时钟">动态时钟</option>
          </select>

          <span className="hidden xl:inline-flex items-center gap-1 rounded-lg bg-[#F5F4FA] px-2 py-0.5 text-[10.5px] font-medium text-[#6B6880] whitespace-nowrap">
            {isDirty
              ? draftSavedAt
                ? `已自动保存 ${draftSavedAt}`
                : '未保存'
              : '已同步'}
          </span>
        </div>

        {/* Center: Undo/Redo, Device Spec Selector, Preview Toggles, Zoom */}
        <div className="hidden lg:flex items-center gap-1.5">
          <div className="flex items-center rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-0.5">
            <button
              type="button"
              disabled={historyIdx <= 0}
              onClick={handleUndo}
              title="撤销 (Ctrl+Z)"
              aria-label="撤销"
              className="rounded-lg p-1.5 text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
            >
              <Undo2 className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              disabled={historyIdx >= history.length - 1}
              onClick={handleRedo}
              title="重做 (Ctrl+Y)"
              aria-label="重做"
              className="rounded-lg p-1.5 text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
            >
              <Redo2 className="h-3.5 w-3.5" />
            </button>
          </div>

          <select
            value={`${canvasPxW}x${canvasPxH}`}
            onChange={(e) => {
              const [w, h] = e.target.value.split('x').map(Number);
              if (w && h) handleSwitchDeviceSpec([w, h]);
            }}
            aria-label="切换设备画布规格"
            className="rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1 text-[11.5px] font-semibold text-[#1A172B] focus:border-[#5E45F8] focus:outline-none"
          >
            {EDITOR_DEVICE_SPECS.map((s) => (
              <option key={s.id} value={`${s.size[0]}x${s.size[1]}`}>
                {s.label} · {s.size[0]}×{s.size[1]}
              </option>
            ))}
            {!EDITOR_DEVICE_SPECS.some(
              (s) => s.size[0] === canvasPxW && s.size[1] === canvasPxH
            ) && (
              <option value={`${canvasPxW}x${canvasPxH}`}>
                自定义 · {canvasPxW}×{canvasPxH}
              </option>
            )}
          </select>

          <div className="flex items-center gap-0.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-0.5">
            <button
              type="button"
              onClick={() => setLiveTick((v) => !v)}
              title="切换实时时间走时预览"
              className={`flex items-center gap-1 rounded-lg px-2 py-1 text-[11px] font-semibold whitespace-nowrap ${
                liveTick ? 'bg-[#5E45F8] text-white' : 'text-[#6B6880] hover:bg-white'
              }`}
            >
              <Clock className="h-3 w-3" />
              <span>实时</span>
            </button>
            <button
              type="button"
              onClick={() => setPlayMotion((v) => !v)}
              title="切换动态与视频预览"
              className={`flex items-center gap-1 rounded-lg px-2 py-1 text-[11px] font-semibold whitespace-nowrap ${
                playMotion ? 'bg-[#5E45F8] text-white' : 'text-[#6B6880] hover:bg-white'
              }`}
            >
              <Play className="h-3 w-3" />
              <span>动态</span>
            </button>
            <button
              type="button"
              onClick={() => setShowSafeZone((v) => !v)}
              title="显示/隐藏 4% 安全边距线"
              className={`rounded-lg p-1.5 ${
                showSafeZone ? 'bg-[#EFEBFD] text-[#5E45F8]' : 'text-[#6B6880] hover:bg-white'
              }`}
              aria-label="安全边距"
            >
              <Shield className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              onClick={() => setShowGridRulers((v) => !v)}
              title="显示/隐藏标尺与参考网格"
              className={`rounded-lg p-1.5 ${
                showGridRulers ? 'bg-[#EFEBFD] text-[#5E45F8]' : 'text-[#6B6880] hover:bg-white'
              }`}
              aria-label="标尺与网格"
            >
              <Ruler className="h-3.5 w-3.5" />
            </button>
          </div>

          {/* Zoom Controls */}
          <div className="flex items-center gap-0.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-1 py-0.5">
            <button
              type="button"
              onClick={() => setZoomMode(Math.max(0.25, +(effectiveZoom - 0.1).toFixed(2)))}
              aria-label="缩小画布"
              className="rounded-lg p-1 text-[#6B6880] hover:bg-white hover:text-[#5E45F8]"
            >
              <ZoomOut className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              onClick={() => setZoomMode('fit')}
              title="恢复适应屏幕比例"
              className="px-1.5 font-mono-tabular text-[11px] font-bold text-[#1A172B] hover:text-[#5E45F8] whitespace-nowrap"
            >
              {Math.round(effectiveZoom * 100)}%
            </button>
            <button
              type="button"
              onClick={() => setZoomMode(Math.min(2, +(effectiveZoom + 0.1).toFixed(2)))}
              aria-label="放大画布"
              className="rounded-lg p-1 text-[#6B6880] hover:bg-white hover:text-[#5E45F8]"
            >
              <ZoomIn className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>

        {/* Right: Temp Device Preview, Save, Save & Apply, More */}
        <div className="flex items-center gap-1.5">
          <button
            type="button"
            onClick={() => {
              const previewItem = buildThemeItem();
              onStartTempPreview(previewItem);
            }}
            title={`在 ${activeDevice.name} 临时预览 15 秒，结束自动恢复原内容`}
            className="flex h-8 items-center gap-1.5 rounded-xl border border-[#D9D2FF] bg-[#EFEBFD] px-2.5 text-[12px] font-semibold text-[#5E45F8] hover:bg-[#E5DDFD] whitespace-nowrap"
          >
            <MonitorPlay className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">设备临时预览</span>
          </button>

          <button
            type="button"
            onClick={() => handleSave(false)}
            className="flex h-8 items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white px-2.5 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8] whitespace-nowrap"
          >
            <Save className="h-3.5 w-3.5" />
            <span>保存</span>
          </button>

          <button
            type="button"
            onClick={() => handleSave(true)}
            className="flex h-8 items-center gap-1 rounded-xl bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-3 text-[12px] font-bold text-white shadow-[0_4px_14px_rgba(94,69,248,0.28)] hover:brightness-105 whitespace-nowrap"
          >
            <Check className="h-3.5 w-3.5" />
            <span>保存并使用</span>
          </button>

          <div className="relative" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              onClick={() => setMoreMenuOpen((o) => !o)}
              aria-label="更多编辑器操作"
              className="flex h-8 w-8 items-center justify-center rounded-xl border border-[#E6E4F2] bg-white text-[#6B6880] hover:border-[#5E45F8] hover:text-[#5E45F8]"
            >
              <MoreHorizontal className="h-4 w-4" />
            </button>
            {moreMenuOpen && (
              <div className="absolute right-0 top-10 z-50 w-48 rounded-2xl border border-[#E6E4F2] bg-white/98 p-1.5 shadow-[0_16px_40px_rgba(24,10,68,0.18)] backdrop-blur-xl">
                <button
                  type="button"
                  onClick={() => {
                    setMoreMenuOpen(false);
                    const blob = new Blob([JSON.stringify(project, null, 2)], {
                      type: 'application/json',
                    });
                    const a = document.createElement('a');
                    a.href = URL.createObjectURL(blob);
                    a.download = `${project.name || 'd92-theme'}.d92theme.json`;
                    a.click();
                    onNotify('已导出版本化主题工程 JSON');
                  }}
                  className="flex w-full items-center gap-2 rounded-xl px-2.5 py-2 text-left text-[12px] font-medium text-[#1A172B] hover:bg-[#EFEBFD]"
                >
                  <Download className="h-3.5 w-3.5 text-[#5E45F8]" />
                  <span>导出 .d92theme.json</span>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setMoreMenuOpen(false);
                    jsonImportRef.current?.click();
                  }}
                  className="flex w-full items-center gap-2 rounded-xl px-2.5 py-2 text-left text-[12px] font-medium text-[#1A172B] hover:bg-[#EFEBFD]"
                >
                  <Upload className="h-3.5 w-3.5 text-[#5E45F8]" />
                  <span>导入工程 JSON</span>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setMoreMenuOpen(false);
                    setShortcutModalOpen(true);
                  }}
                  className="flex w-full items-center gap-2 rounded-xl px-2.5 py-2 text-left text-[12px] font-medium text-[#1A172B] hover:bg-[#EFEBFD]"
                >
                  <HelpCircle className="h-3.5 w-3.5 text-[#5E45F8]" />
                  <span>快捷键与操作指南</span>
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* ==================== MAIN 4 WORKSPACE REGIONS ==================== */}
      <div className="flex min-h-0 flex-1 overflow-hidden">
        {/* ZONE B: Left 1st-Level Navigation Rail (64px) */}
        <nav
          aria-label="主题编辑器一级工具栏"
          className="flex w-[64px] shrink-0 flex-col items-center gap-1 overflow-y-auto border-r border-[#E6E4F2] bg-white/92 py-2 backdrop-blur-xl studio-scroll"
        >
          {EDITOR_RAIL_TABS.map((tab) => {
            const active = activeRailTab === tab.id && !leftCollapsed;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => {
                  if (activeRailTab === tab.id) {
                    setLeftCollapsed((c) => !c);
                  } else {
                    setActiveRailTab(tab.id);
                    setLeftCollapsed(false);
                  }
                }}
                className={`flex w-[52px] shrink-0 flex-col items-center justify-center gap-0.5 rounded-xl py-1.5 transition-all ${
                  active
                    ? 'bg-gradient-to-b from-[#6A52FF] to-[#563CEB] text-white shadow-[0_6px_14px_rgba(94,69,248,0.28)]'
                    : 'text-[#6B6880] hover:bg-[#EFEBFD]/70 hover:text-[#1A172B]'
                }`}
              >
                {renderRailIcon(tab.id)}
                <span className="text-[10.5px] font-semibold leading-tight">{tab.label}</span>
              </button>
            );
          })}
        </nav>

        {/* ZONE C: Left Secondary Panel (300px, collapsible) */}
        {!leftCollapsed && (
          <EditorLeftPanel
            activeTab={activeRailTab}
            project={project}
            layoutIndex={layoutIndex}
            selectedIds={selectedIds}
            themes={themes}
            recentImports={recentImports}
            onClose={() => setLeftCollapsed(true)}
            onSwitchTab={(t) => setActiveRailTab(t)}
            onInsertElements={handleInsertElements}
            onApplyTemplateProject={(nextProj, asCopy, msg) => {
              commitProject((prev) => ({
                ...nextProj,
                template_id: asCopy ? genEditorId('tpl') : prev.template_id,
              }));
              setLayoutIndex(0);
              setSelectedIds([]);
              onNotify(msg);
            }}
            onMutateBg={(fn, msg) => {
              const previewBg = fn(project.bg);
              if (previewBg.kind === 'video' && project.bg.kind !== 'video') {
                checkVideoLimit('bg', (draft) => {
                  draft.bg = fn(draft.bg);
                  return draft;
                });
              } else {
                commitProject((draft) => {
                  draft.bg = fn(draft.bg);
                  return draft;
                });
              }
              if (msg) onNotify(msg);
            }}
            onTriggerLocalUpload={(mode) => {
              fileUploadModeRef.current = mode;
              fileUploadRef.current?.click();
            }}
            onOpenLibraryPicker={(mode) => setLibraryPickerMode(mode)}
            onStartCapture={(mode) => setCaptureMode(mode)}
            onSelectLayer={(id, multi) => {
              setAdjBg(false);
              setSelectedIds((prev) =>
                multi
                  ? prev.includes(id)
                    ? prev.filter((x) => x !== id)
                    : [...prev, id]
                  : [id]
              );
            }}
            onSelectBgLayer={() => {
              setSelectedIds([]);
              setActiveRailTab('background');
            }}
            onToggleLock={handleToggleLock}
            onToggleHide={handleToggleHide}
            onReorderLayer={handleReorderLayer}
            onRenameLayer={(id) => {
              setRenameTargetId(id);
              setRenameInput(project.names[id] || '');
            }}
            onDuplicateSelection={handleDuplicateSelection}
            onDeleteSelection={handleDeleteSelection}
            onToggleBgAdj={() => setAdjBg((v) => !v)}
            onNotify={onNotify}
          />
        )}

        {/* ZONE D: Central Canvas Stage */}
        <section
          className="relative flex min-w-0 flex-1 flex-col overflow-hidden bg-[#F1EFFA]"
          aria-label="中央可视化画布区"
        >
          {/* Optional Top Rulers + Stage Area */}
          <div
            ref={stageRef}
            onPointerDown={handleStagePointerDown}
            onWheel={(e) => {
              if (e.ctrlKey || e.metaKey) {
                e.preventDefault();
                const delta = e.deltaY < 0 ? 0.08 : -0.08;
                setZoomMode(Math.max(0.2, Math.min(2.5, +(effectiveZoom + delta).toFixed(2))));
              }
            }}
            onContextMenu={(e) => {
              e.preventDefault();
              setContextMenu({ x: e.clientX, y: e.clientY, target: 'canvas' });
            }}
            onDragOver={(e) => {
              e.preventDefault();
              setIsDraggingFile(true);
            }}
            onDragLeave={() => setIsDraggingFile(false)}
            onDrop={(e) => {
              e.preventDefault();
              setIsDraggingFile(false);
              const f = e.dataTransfer.files?.[0];
              if (f) void importMediaFile(f, 'el');
            }}
            className={`relative flex flex-1 overflow-auto p-8 studio-scroll ${
              spacePressed ? 'cursor-grab' : adjBg ? 'cursor-move' : 'cursor-default'
            }`}
          >
            {/* Stage Grid Background */}
            {showGridRulers && (
              <div
                className="pointer-events-none absolute inset-0 opacity-65"
                style={{
                  backgroundImage: 'radial-gradient(#D5CEE8 1px, transparent 1px)',
                  backgroundSize: '18px 18px',
                }}
              />
            )}

            {/* Drag-and-drop Overlay */}
            {isDraggingFile && (
              <div className="pointer-events-none fixed inset-0 z-50 flex items-center justify-center bg-[#5E45F8]/15 backdrop-blur-xs">
                <div className="rounded-2xl border-2 border-dashed border-[#5E45F8] bg-white/95 px-8 py-5 text-center shadow-2xl">
                  <Upload className="mx-auto h-7 w-7 text-[#5E45F8]" />
                  <div className="mt-1.5 text-[14px] font-bold text-[#1A172B]">
                    松开鼠标，立即导入图片/视频至画布
                  </div>
                </div>
              </div>
            )}

            {/* Exact Proportional Canvas Wrapper */}
            <div className="relative m-auto shrink-0">
              {/* Top & Left Pixel Rulers */}
              {showGridRulers && (
                <>
                  <div
                    className="pointer-events-none absolute -top-5 left-0 flex h-4 items-end justify-between border-b border-[#DAD6F0] px-1 font-mono-tabular text-[9px] text-[#6B6880]"
                    style={{ width: `${Math.round(canvasPxW * effectiveZoom)}px` }}
                  >
                    <span>0</span>
                    <span>{Math.round(canvasPxW * 0.25)}</span>
                    <span>{Math.round(canvasPxW * 0.5)}px</span>
                    <span>{Math.round(canvasPxW * 0.75)}</span>
                    <span>{canvasPxW}</span>
                  </div>
                  <div
                    className="pointer-events-none absolute -left-7 top-0 flex w-6 flex-col items-end justify-between border-r border-[#DAD6F0] pr-1 font-mono-tabular text-[9px] text-[#6B6880]"
                    style={{ height: `${Math.round(canvasPxH * effectiveZoom)}px` }}
                  >
                    <span>0</span>
                    <span>{Math.round(canvasPxH * 0.5)}</span>
                    <span>{canvasPxH}</span>
                  </div>
                </>
              )}

              {/* The Proportional Canvas */}
              <div
                ref={canvasRef}
                data-te-canvas="true"
                style={{
                  width: `${Math.round(canvasPxW * effectiveZoom)}px`,
                  height: `${Math.round(canvasPxH * effectiveZoom)}px`,
                }}
                className="relative overflow-hidden rounded-xl border border-[#DAD6F0] bg-[#0B061D] shadow-[0_24px_60px_rgba(24,10,68,0.26)]"
              >
                <EditorArtworkRenderer
                  project={project}
                  layoutIndex={layoutIndex}
                  liveTick={liveTick}
                  playMotion={playMotion}
                  interactive={true}
                  locks={project.locks}
                  editingTextId={editingTextId}
                  onPointerDownElement={handlePointerDownElement}
                  onDoubleClickElement={(_e, id) => {
                    const target = layoutElements.find((item) => item.id === id);
                    if (target?.kind === 'text' && !project.locks[id]) {
                      setEditingTextId(id);
                    }
                  }}
                  onChangeEditingText={(id, nextText) => {
                    mutateElementsInCurrentLayout(
                      (els) =>
                        els.map((item) =>
                          item.id === id && item.kind === 'text'
                            ? ({ ...item, text: nextText } as TextElement)
                            : item
                        ),
                      false
                    );
                  }}
                  onFinishEditingText={() => {
                    setEditingTextId(null);
                    commitProject((p) => p, true);
                  }}
                />

                {/* Safe Margin Guide (4%) */}
                {showSafeZone && (
                  <div className="pointer-events-none absolute inset-[4%] rounded border border-dashed border-violet-300/45" />
                )}

                {/* Snap Guidelines */}
                {snapGuides.v.map((pos, idx) => (
                  <div
                    key={`v-${pos}-${idx}`}
                    className="pointer-events-none absolute top-0 bottom-0 w-px bg-[#5E45F8] shadow-[0_0_6px_#5E45F8]"
                    style={{ left: `${pos}%` }}
                  />
                ))}
                {snapGuides.h.map((pos, idx) => (
                  <div
                    key={`h-${pos}-${idx}`}
                    className="pointer-events-none absolute left-0 right-0 h-px bg-[#5E45F8] shadow-[0_0_6px_#5E45F8]"
                    style={{ top: `${pos}%` }}
                  />
                ))}
              </div>

              {/* Selection Transform Overlay (Rendered above canvas at exact measured bounds) */}
              {selBox && !editingTextId && (
                <div
                  className="pointer-events-none absolute"
                  style={{
                    left: `${selBox.left}px`,
                    top: `${selBox.top}px`,
                    width: `${selBox.width}px`,
                    height: `${selBox.height}px`,
                    outline: singleSelectedLocked
                      ? '1.5px dashed #F59E0B'
                      : '1.5px solid #5E45F8',
                  }}
                >
                  {/* Floating Quick Actions Toolbar */}
                  <div
                    className="pointer-events-auto absolute -top-9 left-1/2 flex -translate-x-1/2 items-center gap-0.5 rounded-xl border border-[#E6E4F2] bg-white/95 px-1.5 py-0.5 shadow-[0_8px_24px_rgba(24,10,68,0.16)] backdrop-blur-md"
                    onPointerDown={(e) => e.stopPropagation()}
                  >
                    <button
                      type="button"
                      onClick={handleDuplicateSelection}
                      title="复制副本 (Ctrl+D)"
                      className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                    >
                      <Copy className="h-3.5 w-3.5" />
                    </button>
                    {selectedIds.length > 1 && (
                      <>
                        <button
                          type="button"
                          onClick={handleGroupSelection}
                          title="成组 (Ctrl+G)"
                          className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                        >
                          <Group className="h-3.5 w-3.5" />
                        </button>
                        <span className="mx-0.5 h-3.5 w-px bg-[#E6E4F2]" />
                        <button
                          type="button"
                          onClick={() => handleAlignSelection('l')}
                          title="左对齐"
                          className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                        >
                          <AlignStartVertical className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => handleAlignSelection('hc')}
                          title="水平居中"
                          className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                        >
                          <AlignCenterVertical className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => handleAlignSelection('r')}
                          title="右对齐"
                          className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                        >
                          <AlignEndVertical className="h-3.5 w-3.5" />
                        </button>
                        {selectedIds.length >= 3 && (
                          <>
                            <button
                              type="button"
                              onClick={() => handleDistributeSelection('h')}
                              title="水平等距分布"
                              className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                            >
                              <AlignHorizontalSpaceAround className="h-3.5 w-3.5" />
                            </button>
                            <button
                              type="button"
                              onClick={() => handleDistributeSelection('v')}
                              title="垂直等距分布"
                              className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                            >
                              <AlignVerticalSpaceAround className="h-3.5 w-3.5" />
                            </button>
                          </>
                        )}
                      </>
                    )}
                    {selectedElements.some((el) => el.g) && (
                      <button
                        type="button"
                        onClick={handleUngroupSelection}
                        title="取消成组 (Ctrl+Shift+G)"
                        className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        <Ungroup className="h-3.5 w-3.5" />
                      </button>
                    )}
                    <button
                      type="button"
                      onClick={() => handleToggleLock()}
                      title="锁定/解锁"
                      className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                    >
                      {singleSelectedLocked ? (
                        <Unlock className="h-3.5 w-3.5 text-amber-600" />
                      ) : (
                        <Lock className="h-3.5 w-3.5" />
                      )}
                    </button>
                    <button
                      type="button"
                      onClick={() => handleToggleHide()}
                      title="隐藏图层"
                      className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                    >
                      <EyeOff className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={handleDeleteSelection}
                      title="删除 (Delete)"
                      className="rounded-lg p-1 text-[#56526B] hover:bg-red-50 hover:text-red-600"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>

                  {/* Single-Element Resize Handles */}
                  {singleSelected && !singleSelectedLocked && (() => {
                    const isText = singleSelected.kind === 'text';
                    const isVertText = isText && Boolean(singleSelected.vert);
                    const supportsAllEdges =
                      singleSelected.kind === 'shape' ||
                      singleSelected.kind === 'image' ||
                      singleSelected.kind === 'video' ||
                      singleSelected.kind === 'motion' ||
                      singleSelected.kind === 'widget' ||
                      singleSelected.kind === 'plugin';
                    const showTopBottom = supportsAllEdges || isVertText;
                    const showLeftRight = supportsAllEdges || (isText && !isVertText);

                    const handles = [
                      { h: 'tl' as const, cls: 'z-10 -left-1.5 -top-1.5 h-2.5 w-2.5 rounded-xs cursor-nwse-resize', title: '缩放尺寸 (Shift 切换等比)' },
                      { h: 'tr' as const, cls: 'z-10 -right-1.5 -top-1.5 h-2.5 w-2.5 rounded-xs cursor-nesw-resize', title: '缩放尺寸 (Shift 切换等比)' },
                      { h: 'bl' as const, cls: 'z-10 -left-1.5 -bottom-1.5 h-2.5 w-2.5 rounded-xs cursor-nesw-resize', title: '缩放尺寸 (Shift 切换等比)' },
                      { h: 'br' as const, cls: 'z-10 -right-1.5 -bottom-1.5 h-2.5 w-2.5 rounded-xs cursor-nwse-resize', title: '缩放尺寸 (Shift 切换等比)' },
                      ...(showTopBottom
                        ? [
                            { h: 'tc' as const, cls: 'z-20 left-1/2 -top-1.5 h-2 w-4 -translate-x-1/2 rounded-full cursor-ns-resize', title: isVertText ? '调整竖排文本框高度' : '自由拉高 / 压扁成细条' },
                            { h: 'bc' as const, cls: 'z-20 left-1/2 -bottom-1.5 h-2 w-4 -translate-x-1/2 rounded-full cursor-ns-resize', title: isVertText ? '调整竖排文本框高度' : '自由拉高 / 压扁成细条' },
                          ]
                        : []),
                      ...(showLeftRight
                        ? [
                            { h: 'ml' as const, cls: 'z-20 -left-1.5 top-1/2 h-4 w-2 -translate-y-1/2 rounded-full cursor-ew-resize', title: isText ? '拉宽 / 拉窄文本框（自动换行）' : '自由拉宽 / 拉窄成细条' },
                            { h: 'mr' as const, cls: 'z-20 -right-1.5 top-1/2 h-4 w-2 -translate-y-1/2 rounded-full cursor-ew-resize', title: isText ? '拉宽 / 拉窄文本框（自动换行）' : '自由拉宽 / 拉窄成细条' },
                          ]
                        : []),
                    ];
                    return handles;
                  })().map((item) => (
                    <div
                      key={item.h}
                      title={item.title}
                      onPointerDown={(e) => {
                        e.stopPropagation();
                        const cRect = canvasRef.current?.getBoundingClientRect();
                        if (!cRect) return;
                        dragRef.current = {
                          type: 'resize',
                          handle: item.h,
                          startX: e.clientX,
                          startY: e.clientY,
                          canvasW: Math.max(1, cRect.width),
                          canvasH: Math.max(1, cRect.height),
                          elId: singleSelected.id,
                          origBox: selBox,
                          origEl: structuredClone(singleSelected),
                          moved: false,
                        };
                      }}
                      className={`pointer-events-auto absolute border border-[#5E45F8] bg-white shadow ${item.cls}`}
                    />
                  ))}
                  {singleSelected && !singleSelectedLocked && (
                    <>

                      {/* Rotation Handle */}
                      <div
                        onPointerDown={(e) => {
                          e.stopPropagation();
                          const cRect = canvasRef.current?.getBoundingClientRect();
                          if (!cRect) return;
                          const cx = cRect.left + selBox.cx;
                          const cy = cRect.top + selBox.cy;
                          dragRef.current = {
                            type: 'rotate',
                            cx,
                            cy,
                            startAngle: (Math.atan2(e.clientY - cy, e.clientX - cx) * 180) / Math.PI,
                            elId: singleSelected.id,
                            origRot: singleSelected.rot || 0,
                            moved: false,
                          };
                        }}
                        title="拖拽旋转 (按住 Shift 每 15° 吸附)"
                        className="pointer-events-auto absolute -bottom-7 left-1/2 flex h-5 w-5 -translate-x-1/2 cursor-grab items-center justify-center rounded-full border border-[#5E45F8] bg-white text-[#5E45F8] shadow"
                      >
                        <RotateCw className="h-3 w-3" />
                      </div>
                    </>
                  )}
                </div>
              )}

              {/* Background Adjust Active Pill */}
              {adjBg && (
                <div className="pointer-events-auto absolute left-1/2 top-3 flex -translate-x-1/2 items-center gap-2 rounded-full border border-[#5E45F8] bg-white/95 px-3.5 py-1 text-[11.5px] font-bold text-[#5E45F8] shadow-md backdrop-blur-md">
                  <span>正在画布内直接拖拽调整背景位置</span>
                  <button
                    type="button"
                    onClick={() => setAdjBg(false)}
                    className="rounded-full bg-[#5E45F8] px-2.5 py-0.5 text-[10.5px] text-white"
                  >
                    完成
                  </button>
                </div>
              )}
            </div>

            {/* Marquee Selection Box */}
            {marqueeRect && (
              <div
                className="pointer-events-none absolute border border-[#5E45F8] bg-[#5E45F8]/12"
                style={{
                  left: `${marqueeRect.left}px`,
                  top: `${marqueeRect.top}px`,
                  width: `${marqueeRect.width}px`,
                  height: `${marqueeRect.height}px`,
                }}
              />
            )}
          </div>

          {/* Multi-Layout Strip at Bottom of Stage */}
          <div className="flex h-10 shrink-0 items-center justify-between gap-2 border-t border-[#E6E4F2] bg-white/92 px-3 backdrop-blur-xl">
            <div className="flex min-w-0 items-center gap-1.5 overflow-x-auto">
              <span className="shrink-0 text-[11px] font-bold text-[#6B6880]">多规格布局:</span>
              {project.layouts.map((lay, idx) => {
                const active = idx === layoutIndex;
                return (
                  <button
                    key={`${lay.size[0]}x${lay.size[1]}-${idx}`}
                    type="button"
                    onClick={() => {
                      setLayoutIndex(idx);
                      setSelectedIds([]);
                      setAdjBg(false);
                    }}
                    onContextMenu={(e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      setContextMenu({
                        x: e.clientX,
                        y: e.clientY,
                        target: 'layout',
                        layoutIdx: idx,
                      });
                    }}
                    className={`flex shrink-0 items-center gap-1.5 rounded-xl border px-2.5 py-1 text-[11px] font-semibold transition-colors whitespace-nowrap ${
                      active
                        ? 'border-[#5E45F8] bg-[#5E45F8] text-white'
                        : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#1A172B] hover:border-[#5E45F8]'
                    }`}
                  >
                    <span>{lay.label}</span>
                    <span className="font-mono-tabular opacity-80">
                      {lay.size[0]}×{lay.size[1]}
                    </span>
                    <span
                      className={`rounded px-1 text-[9.5px] ${
                        active ? 'bg-white/20 text-white' : 'bg-[#EAE7F8] text-[#56526B]'
                      }`}
                    >
                      {idx === 0 ? '主布局' : lay.follow ? '跟随' : '独立'}
                    </span>
                  </button>
                );
              })}

              <button
                type="button"
                onClick={() => setAddLayoutOpen(true)}
                className="flex shrink-0 items-center gap-1 rounded-xl border border-dashed border-[#DAD6F0] px-2.5 py-1 text-[11px] font-semibold text-[#5E45F8] hover:border-[#5E45F8] hover:bg-[#EFEBFD] whitespace-nowrap"
              >
                <Plus className="h-3 w-3" />
                <span>添加尺寸</span>
              </button>
            </div>

            {layoutIndex > 0 && (
              <div className="flex shrink-0 items-center gap-1.5">
                {currentLayout?.follow ? (
                  <button
                    type="button"
                    onClick={() => {
                      commitProject((draft) => {
                        const target = draft.layouts[layoutIndex];
                        if (target) {
                          target.follow = false;
                          target.elements = cloneProj(draft).master.map((el) =>
                            target.vis?.[el.id] ? { ...el, hide: true } : el
                          );
                          delete target.vis;
                        }
                        return draft;
                      });
                      onNotify('已切换为独立定制布局，修改不再影响主布局');
                    }}
                    className="rounded-lg border border-[#E6E4F2] px-2 py-1 text-[11px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                  >
                    脱离跟随（独立编辑）
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => {
                      commitProject((draft) => {
                        const target = draft.layouts[layoutIndex];
                        if (target) {
                          target.follow = true;
                          delete target.elements;
                        }
                        return draft;
                      });
                      onNotify('已恢复跟随主布局');
                    }}
                    className="rounded-lg border border-[#D9D2FF] bg-[#EFEBFD] px-2 py-1 text-[11px] font-semibold text-[#5E45F8] hover:bg-[#E5DDFD]"
                  >
                    恢复跟随主布局
                  </button>
                )}

                <button
                  type="button"
                  onClick={() => {
                    commitProject((draft) => {
                      draft.layouts.splice(layoutIndex, 1);
                      return draft;
                    });
                    setLayoutIndex(0);
                    setSelectedIds([]);
                  }}
                  className="rounded-lg border border-red-200 px-2 py-1 text-[11px] font-semibold text-red-600 hover:bg-red-50"
                >
                  删除尺寸
                </button>
              </div>
            )}
          </div>
        </section>

        {/* ZONE E: Right Contextual Inspector (320px, collapsible) */}
        <EditorInspector
          collapsed={rightCollapsed}
          onToggleCollapse={() => setRightCollapsed((c) => !c)}
          project={project}
          layoutIndex={layoutIndex}
          selectedElements={selectedElements}
          adjBg={adjBg}
          aspectLinked={aspectLinked}
          onToggleAspectLink={() => setAspectLinked((v) => !v)}
          onToggleAdjBg={(on) => setAdjBg(on)}
          onMutateBg={(fn, msg) => {
            commitProject((draft) => {
              draft.bg = fn(draft.bg);
              return draft;
            });
            if (msg) onNotify(msg);
          }}
          onResetBgTransform={() => {
            commitProject((draft) => {
              draft.bg.tx = 0;
              draft.bg.ty = 0;
              draft.bg.rot = 0;
              draft.bg.sc = 1;
              return draft;
            });
            onNotify('已重置背景变换');
          }}
          onMutateSelected={(fn, msg, recordHistory = true) => {
            const idSet = new Set(selectedIds);
            mutateElementsInCurrentLayout(
              (els) => els.map((e) => (idSet.has(e.id) ? fn(e) : e)),
              recordHistory
            );
            if (msg) onNotify(msg);
          }}
          onReplaceImageForSelection={() => {
            if (singleSelected?.kind === 'video') {
              fileUploadModeRef.current = 'el';
              fileUploadRef.current?.click();
            } else {
              setLibraryPickerMode('replace-el');
            }
          }}
          onAlignSelection={handleAlignSelection}
          onDistributeSelection={handleDistributeSelection}
          onBulkScale={handleBulkScale}
          onBulkRotate={handleBulkRotate}
          onGroupSelection={handleGroupSelection}
          onUngroupSelection={handleUngroupSelection}
          onReorderLayer={(op) => handleReorderLayer(op)}
          onSetLayerIndex={(idx) => handleSetLayerIndex(idx)}
          onToggleLock={handleToggleLock}
          onToggleHide={handleToggleHide}
          onDuplicateSelection={handleDuplicateSelection}
          onDeleteSelection={handleDeleteSelection}
          onRenameLayer={(id) => {
            setRenameTargetId(id);
            setRenameInput(project.names[id] || '');
          }}
          onNotify={onNotify}
        />
      </div>

      {/* ==================== BOTTOM STATUS BAR (28px) ==================== */}
      <footer className="flex h-7 shrink-0 items-center justify-between border-t border-[#E6E4F2] bg-white/92 px-3 text-[11px] text-[#6B6880] backdrop-blur-xl">
        <div className="flex items-center gap-3">
          <span className="font-mono-tabular font-semibold text-[#1A172B]">
            {canvasPxW} × {canvasPxH} ({currentLayout?.label})
          </span>
          <span className="font-mono-tabular">缩放 {Math.round(effectiveZoom * 100)}%</span>
          <span>
            {selectedElements.length === 0
              ? `共 ${layoutElements.length} 个图层`
              : selectedElements.length === 1
                ? `已选中 1 项 · X:${selectedElements[0].x}% Y:${selectedElements[0].y}%`
                : `已多选 ${selectedElements.length} 项`}
          </span>
        </div>

        <div className="flex items-center gap-3">
          {isOffline && (
            <span className="inline-flex items-center gap-1 text-amber-700">
              <WifiOff className="h-3 w-3 text-amber-600" />
              <span>离线模式</span>
            </span>
          )}
        </div>
      </footer>

      {/* ==================== ROTATE LIVE HUD ==================== */}
      {rotateHud && (
        <div
          className="pointer-events-none fixed z-50 rounded-lg bg-[#1A172B] px-2 py-0.5 font-mono-tabular text-[11px] font-bold text-white shadow"
          style={{ left: `${rotateHud.x}px`, top: `${rotateHud.y}px` }}
        >
          {rotateHud.deg}°
        </div>
      )}

      {/* ==================== CONTEXT MENU (Reference Image 4) ==================== */}
      {contextMenu && (
        <div
          style={{
            left: `${Math.max(8, Math.min(window.innerWidth - 210, contextMenu.x))}px`,
            top: `${Math.max(8, Math.min(window.innerHeight - 360, contextMenu.y))}px`,
          }}
          className="fixed z-50 w-48 rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 text-[12px] text-[#1A172B] shadow-[0_18px_46px_rgba(24,10,68,0.18)] backdrop-blur-xl"
          onClick={(e) => e.stopPropagation()}
          onMouseLeave={() => setContextSubmenu(null)}
        >
          {contextMenu.target === 'element' ? (() => {
            const flipSubmenuLeft = contextMenu.x > window.innerWidth - 380;
            const submenuSideCls = flipSubmenuLeft ? 'right-full mr-1.5' : 'left-full ml-1.5';
            const allLocked =
              selectedIds.length > 0 && selectedIds.every((id) => Boolean(project.locks[id]));
            const anyHidden =
              selectedElements.length > 0 &&
              selectedElements.some((el) => isElementHidden(project, layoutIndex, el));
            const anyGrouped = selectedElements.some((el) => Boolean(el.g));

            return (
              <>
                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    setClipboard(structuredClone(selectedElements));
                    setContextMenu(null);
                    setContextSubmenu(null);
                    onNotify(`已复制 ${selectedElements.length} 个图层`);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  <span>复制</span>
                  <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+C</span>
                </button>
                <button
                  type="button"
                  disabled={clipboard.length === 0}
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    if (clipboard.length === 0) return;
                    const pasted = clipboard.map((c) => ({
                      ...structuredClone(c),
                      x: +(c.x + 2).toFixed(2),
                      y: +(c.y + 3).toFixed(2),
                      g: undefined,
                    }));
                    handleInsertElements(pasted, `粘贴 (${pasted.length}项)`);
                    setContextMenu(null);
                    setContextSubmenu(null);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <span>粘贴</span>
                  <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+V</span>
                </button>
                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    handleDuplicateSelection();
                    setContextMenu(null);
                    setContextSubmenu(null);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  <span>创建副本</span>
                  <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+D</span>
                </button>
                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    handleDeleteSelection();
                    setContextMenu(null);
                    setContextSubmenu(null);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 text-red-600 hover:bg-red-50"
                >
                  <span>删除</span>
                  <span className="font-mono-tabular text-[10px] text-red-400">Del</span>
                </button>

                <div className="my-1 h-px bg-[#E6E4F2]" />

                {/* Submenu: 层级 */}
                <div
                  className="relative"
                  onMouseEnter={() => setContextSubmenu('z')}
                >
                  <button
                    type="button"
                    onClick={() => setContextSubmenu((s) => (s === 'z' ? null : 'z'))}
                    className={`flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 ${
                      contextSubmenu === 'z' ? 'bg-[#EFEBFD] text-[#5E45F8]' : 'hover:bg-[#EFEBFD]'
                    }`}
                  >
                    <span>层级</span>
                    <ChevronRight className="h-3.5 w-3.5 text-[#8E89A6]" />
                  </button>
                  {contextSubmenu === 'z' && (
                    <div
                      className={`absolute top-0 ${submenuSideCls} w-36 rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 shadow-[0_16px_40px_rgba(24,10,68,0.16)] backdrop-blur-xl`}
                    >
                      {[
                        { op: 'top' as const, label: '置于顶层' },
                        { op: 'up' as const, label: '上移一层' },
                        { op: 'down' as const, label: '下移一层' },
                        { op: 'bottom' as const, label: '置于底层' },
                      ].map((item) => (
                        <button
                          key={item.op}
                          type="button"
                          onClick={() => {
                            handleReorderLayer(item.op);
                            setContextMenu(null);
                            setContextSubmenu(null);
                          }}
                          className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                        >
                          {item.label}
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                {/* Submenu: 对齐画布 */}
                <div
                  className="relative"
                  onMouseEnter={() => setContextSubmenu('align')}
                >
                  <button
                    type="button"
                    onClick={() => setContextSubmenu((s) => (s === 'align' ? null : 'align'))}
                    className={`flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 ${
                      contextSubmenu === 'align' ? 'bg-[#EFEBFD] text-[#5E45F8]' : 'hover:bg-[#EFEBFD]'
                    }`}
                  >
                    <span>对齐画布</span>
                    <ChevronRight className="h-3.5 w-3.5 text-[#8E89A6]" />
                  </button>
                  {contextSubmenu === 'align' && (
                    <div
                      className={`absolute top-0 ${submenuSideCls} w-36 rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 shadow-[0_16px_40px_rgba(24,10,68,0.16)] backdrop-blur-xl`}
                    >
                      {[
                        { mode: 'l' as const, label: '居左' },
                        { mode: 'hc' as const, label: '水平居中' },
                        { mode: 'r' as const, label: '居右' },
                        { mode: 't' as const, label: '居上' },
                        { mode: 'vc' as const, label: '垂直居中' },
                        { mode: 'b' as const, label: '居下' },
                        { mode: 'c' as const, label: '正中' },
                      ].map((item) => (
                        <button
                          key={item.mode}
                          type="button"
                          onClick={() => {
                            handleAlignSelection(item.mode);
                            setContextMenu(null);
                            setContextSubmenu(null);
                          }}
                          className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                        >
                          {item.label}
                        </button>
                      ))}
                      {selectedIds.length >= 3 && (
                        <>
                          <div className="my-1 h-px bg-[#E6E4F2]" />
                          <button
                            type="button"
                            onClick={() => {
                              handleDistributeSelection('h');
                              setContextMenu(null);
                              setContextSubmenu(null);
                            }}
                            className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                          >
                            水平等距分布
                          </button>
                          <button
                            type="button"
                            onClick={() => {
                              handleDistributeSelection('v');
                              setContextMenu(null);
                              setContextSubmenu(null);
                            }}
                            className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                          >
                            垂直等距分布
                          </button>
                        </>
                      )}
                    </div>
                  )}
                </div>

                <div className="my-1 h-px bg-[#E6E4F2]" />

                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    handleToggleLock();
                    setContextMenu(null);
                    setContextSubmenu(null);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  <span>{allLocked ? '解锁' : '锁定'}</span>
                </button>
                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    handleToggleHide();
                    setContextMenu(null);
                    setContextSubmenu(null);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  <span>{anyHidden ? '显示' : '隐藏'}</span>
                </button>

                {selectedIds.length > 1 && (
                  <button
                    type="button"
                    onMouseEnter={() => setContextSubmenu(null)}
                    onClick={() => {
                      handleGroupSelection();
                      setContextMenu(null);
                      setContextSubmenu(null);
                    }}
                    className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                  >
                    <span>组合图层</span>
                    <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+G</span>
                  </button>
                )}
                {anyGrouped && (
                  <button
                    type="button"
                    onMouseEnter={() => setContextSubmenu(null)}
                    onClick={() => {
                      handleUngroupSelection();
                      setContextMenu(null);
                      setContextSubmenu(null);
                    }}
                    className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                  >
                    <span>取消组合</span>
                    <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+Shift+G</span>
                  </button>
                )}

                {/* Submenu: 变换 */}
                <div
                  className="relative"
                  onMouseEnter={() => setContextSubmenu('transform')}
                >
                  <button
                    type="button"
                    onClick={() => setContextSubmenu((s) => (s === 'transform' ? null : 'transform'))}
                    className={`flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 ${
                      contextSubmenu === 'transform' ? 'bg-[#EFEBFD] text-[#5E45F8]' : 'hover:bg-[#EFEBFD]'
                    }`}
                  >
                    <span>变换</span>
                    <ChevronRight className="h-3.5 w-3.5 text-[#8E89A6]" />
                  </button>
                  {contextSubmenu === 'transform' && (
                    <div
                      className={`absolute top-0 ${submenuSideCls} w-40 rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 shadow-[0_16px_40px_rgba(24,10,68,0.16)] backdrop-blur-xl`}
                    >
                      <button
                        type="button"
                        onClick={() => {
                          handleBulkRotate(90);
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        顺时针旋转 90°
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          handleBulkRotate(-90);
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        逆时针旋转 90°
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          const idSet = new Set(selectedIds);
                          mutateElementsInCurrentLayout((els) =>
                            els.map((el) =>
                              idSet.has(el.id) && !project.locks[el.id] ? { ...el, rot: 0 } : el
                            )
                          );
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        重置旋转角度 (0°)
                      </button>
                      <div className="my-1 h-px bg-[#E6E4F2]" />
                      <button
                        type="button"
                        onClick={() => {
                          handleBulkScale(1.1);
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        等比放大 +10%
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          handleBulkScale(0.9);
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        等比缩小 -10%
                      </button>
                    </div>
                  )}
                </div>

                <div className="my-1 h-px bg-[#E6E4F2]" />

                {singleSelected && (
                  <>
                    <button
                      type="button"
                      onMouseEnter={() => setContextSubmenu(null)}
                      onClick={() => {
                        setStyleClipboard({
                          color: singleSelected.color,
                          alpha: singleSelected.alpha,
                          weight: singleSelected.weight,
                          font: singleSelected.font,
                          ls: singleSelected.ls,
                          fx: singleSelected.fx,
                        });
                        setContextMenu(null);
                        setContextSubmenu(null);
                        onNotify('已复制图层样式参数');
                      }}
                      className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                    >
                      复制样式
                    </button>
                    {styleClipboard && (
                      <button
                        type="button"
                        onMouseEnter={() => setContextSubmenu(null)}
                        onClick={() => {
                          const idSet = new Set(selectedIds);
                          mutateElementsInCurrentLayout((els) =>
                            els.map((el) =>
                              idSet.has(el.id) && !project.locks[el.id]
                                ? ({ ...el, ...styleClipboard } as EditorElement)
                                : el
                            )
                          );
                          setContextMenu(null);
                          setContextSubmenu(null);
                          onNotify('已粘贴样式到选中图层');
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        粘贴样式
                      </button>
                    )}
                    <button
                      type="button"
                      onMouseEnter={() => setContextSubmenu(null)}
                      onClick={() => {
                        const sameKindIds = layoutElements
                          .filter(
                            (el) =>
                              el.kind === singleSelected.kind &&
                              !isElementHidden(project, layoutIndex, el)
                          )
                          .map((el) => el.id);
                        setSelectedIds(sameKindIds);
                        setContextMenu(null);
                        setContextSubmenu(null);
                        onNotify(`已选中全部 ${sameKindIds.length} 个同类图层`);
                      }}
                      className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                    >
                      选择同类型图层
                    </button>
                    <button
                      type="button"
                      onMouseEnter={() => setContextSubmenu(null)}
                      onClick={() => {
                        setRenameTargetId(singleSelected.id);
                        setRenameInput(project.names[singleSelected.id] || '');
                        setContextMenu(null);
                        setContextSubmenu(null);
                      }}
                      className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                    >
                      重命名...
                    </button>
                  </>
                )}
              </>
            );
          })() : contextMenu.target === 'layout' && contextMenu.layoutIdx !== undefined ? (
            <>
              <button
                type="button"
                onClick={() => {
                  setLayoutIndex(contextMenu.layoutIdx!);
                  setContextMenu(null);
                }}
                className="flex w-full rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
              >
                切换到此尺寸画布
              </button>
              {contextMenu.layoutIdx > 0 && (
                <button
                  type="button"
                  onClick={() => {
                    const idx = contextMenu.layoutIdx!;
                    commitProject((draft) => {
                      const lay = draft.layouts[idx];
                      if (lay) {
                        lay.follow = !lay.follow;
                        if (!lay.follow && !lay.elements) lay.elements = cloneProj(draft).master;
                      }
                      return draft;
                    });
                    setContextMenu(null);
                  }}
                  className="flex w-full rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  切换跟随 / 独立编辑
                </button>
              )}
            </>
          ) : (() => {
            const flipSubmenuLeft = contextMenu.x > window.innerWidth - 360;
            const submenuSideCls = flipSubmenuLeft ? 'right-full mr-1.5' : 'left-full ml-1.5';
            return (
              <>
                <button
                  type="button"
                  disabled={clipboard.length === 0}
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    if (clipboard.length === 0) return;
                    const pasted = clipboard.map((c) => ({
                      ...structuredClone(c),
                      x: +(c.x + 2).toFixed(2),
                      y: +(c.y + 3).toFixed(2),
                      g: undefined,
                    }));
                    handleInsertElements(pasted, '粘贴图层');
                    setContextMenu(null);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <span>粘贴</span>
                  <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+V</span>
                </button>
                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    setSelectedIds(
                      layoutElements
                        .filter((el) => !isElementHidden(project, layoutIndex, el))
                        .map((el) => el.id)
                    );
                    setContextMenu(null);
                  }}
                  className="flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  <span>全选可见图层</span>
                  <span className="font-mono-tabular text-[10px] text-[#8E89A6]">Ctrl+A</span>
                </button>

                <div className="my-1 h-px bg-[#E6E4F2]" />

                <div
                  className="relative"
                  onMouseEnter={() => setContextSubmenu('add')}
                >
                  <button
                    type="button"
                    onClick={() => setContextSubmenu((s) => (s === 'add' ? null : 'add'))}
                    className={`flex w-full items-center justify-between rounded-xl px-2.5 py-1.5 ${
                      contextSubmenu === 'add' ? 'bg-[#EFEBFD] text-[#5E45F8]' : 'hover:bg-[#EFEBFD]'
                    }`}
                  >
                    <span>快速添加图层</span>
                    <ChevronRight className="h-3.5 w-3.5 text-[#8E89A6]" />
                  </button>
                  {contextSubmenu === 'add' && (
                    <div
                      className={`absolute top-0 ${submenuSideCls} w-40 rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 shadow-[0_16px_40px_rgba(24,10,68,0.16)] backdrop-blur-xl`}
                    >
                      <button
                        type="button"
                        onClick={() => {
                          handleInsertElements([makeElement('text', { text: '新建文本' })], '文字');
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        新建文本
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          handleInsertElements([makeElement('clock', { size: 32 })], '数字时钟');
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        数字时钟
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          handleInsertElements([makeElement('date', { size: 7 })], '日期星期');
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        日期星期
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          handleInsertElements(
                            [makeElement('shape', { w: 20, h: 20, radius: 12 })],
                            '几何形状'
                          );
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        几何形状
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          handleInsertElements(
                            [makeElement('widget', { widgetType: 'sysmon', w: 30, h: 52 })],
                            '硬件监控组件'
                          );
                          setContextMenu(null);
                          setContextSubmenu(null);
                        }}
                        className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        硬件监控组件
                      </button>
                    </div>
                  )}
                </div>

                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    setAdjBg(true);
                    setContextMenu(null);
                  }}
                  className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  进入背景直接调整
                </button>
                <button
                  type="button"
                  onMouseEnter={() => setContextSubmenu(null)}
                  onClick={() => {
                    setShowGridRulers((v) => !v);
                    setContextMenu(null);
                  }}
                  className="flex w-full rounded-xl px-2.5 py-1.5 text-left hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                >
                  {showGridRulers ? '隐藏标尺与网格' : '显示标尺与网格'}
                </button>
              </>
            );
          })()}
        </div>
      )}

      {/* ==================== MODALS ==================== */}

      {/* Draft Restore Modal */}
      {draftPromptProj && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/50 p-4 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-2xl backdrop-blur-xl">
            <h3 className="text-[16px] font-bold text-[#1A172B]">检测到本地自动保存草稿</h3>
            <p className="mt-1.5 text-[12.5px] text-[#6B6880]">
              系统发现于 {new Date(draftPromptProj.savedAt).toLocaleTimeString()} 保存的未提交编辑草稿「
              {draftPromptProj.project.name}」，是否恢复？
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => {
                  try {
                    localStorage.removeItem(draftStorageKey);
                  } catch {
                    // ignore
                  }
                  setDraftPromptProj(null);
                }}
                className="rounded-xl border border-[#E6E4F2] px-3.5 py-1.5 text-[12px] font-semibold text-[#6B6880]"
              >
                忽略草稿
              </button>
              <button
                type="button"
                onClick={() => {
                  commitProject(() => draftPromptProj.project);
                  setDraftPromptProj(null);
                  onNotify('已恢复自动保存草稿');
                }}
                className="rounded-xl bg-[#5E45F8] px-4 py-1.5 text-[12px] font-bold text-white hover:bg-[#4D34E6]"
              >
                恢复草稿继续编辑
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Exit Confirm Modal */}
      {exitConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/50 p-4 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-2xl backdrop-blur-xl">
            <h3 className="text-[16px] font-bold text-[#1A172B]">退出编辑器确认</h3>
            <p className="mt-1.5 text-[12.5px] text-[#6B6880]">
              当前主题「{project.name}」有未正式保存的更改，离开前是否保存到资源库？
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setExitConfirmOpen(false)}
                className="rounded-xl border border-[#E6E4F2] px-3 py-1.5 text-[12px] font-semibold text-[#6B6880]"
              >
                继续编辑
              </button>
              <button
                type="button"
                onClick={() => {
                  setExitConfirmOpen(false);
                  onExitEditor();
                }}
                className="rounded-xl border border-red-200 px-3 py-1.5 text-[12px] font-semibold text-red-600 hover:bg-red-50"
              >
                丢弃退出
              </button>
              <button
                type="button"
                onClick={() => {
                  setExitConfirmOpen(false);
                  handleSave(false);
                  onExitEditor();
                }}
                className="rounded-xl bg-[#5E45F8] px-4 py-1.5 text-[12px] font-bold text-white hover:bg-[#4D34E6]"
              >
                保存并退出
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 1-Video Limit Replace Modal */}
      {pendingVideoReplace && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/50 p-4 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-2xl backdrop-blur-xl">
            <h3 className="text-[16px] font-bold text-[#1A172B]">视频硬件解码通路上限提示</h3>
            <p className="mt-1.5 text-[12.5px] leading-relaxed text-[#6B6880]">
              {pendingVideoReplace.desc}
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setPendingVideoReplace(null)}
                className="rounded-xl border border-[#E6E4F2] px-3.5 py-1.5 text-[12px] font-semibold text-[#6B6880]"
              >
                取消
              </button>
              <button
                type="button"
                onClick={() => {
                  pendingVideoReplace.action();
                  setPendingVideoReplace(null);
                }}
                className="rounded-xl bg-[#5E45F8] px-4 py-1.5 text-[12px] font-bold text-white hover:bg-[#4D34E6]"
              >
                确认替换视频
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Layer Rename Modal */}
      {renameTargetId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/50 p-4 backdrop-blur-xs">
          <div className="w-full max-w-sm rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-2xl backdrop-blur-xl">
            <h3 className="text-[15px] font-bold text-[#1A172B]">重命名图层</h3>
            <input
              type="text"
              autoFocus
              value={renameInput}
              onChange={(e) => setRenameInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  commitProject((draft) => {
                    if (renameInput.trim()) draft.names[renameTargetId] = renameInput.trim();
                    else delete draft.names[renameTargetId];
                    return draft;
                  });
                  setRenameTargetId(null);
                }
              }}
              placeholder="输入自定义图层名称"
              className="mt-3 w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 text-[13px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
            />
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setRenameTargetId(null)}
                className="rounded-xl border border-[#E6E4F2] px-3 py-1.5 text-[12px] text-[#6B6880]"
              >
                取消
              </button>
              <button
                type="button"
                onClick={() => {
                  commitProject((draft) => {
                    if (renameInput.trim()) draft.names[renameTargetId] = renameInput.trim();
                    else delete draft.names[renameTargetId];
                    return draft;
                  });
                  setRenameTargetId(null);
                }}
                className="rounded-xl bg-[#5E45F8] px-4 py-1.5 text-[12px] font-bold text-white hover:bg-[#4D34E6]"
              >
                确定
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Library Picker Modal */}
      {libraryPickerMode && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/55 p-4 backdrop-blur-xs">
          <div className="flex max-h-[82vh] w-full max-w-3xl flex-col overflow-hidden rounded-[24px] border border-white/80 bg-white/95 shadow-2xl backdrop-blur-xl">
            <div className="flex items-center justify-between border-b border-[#E6E4F2] px-5 py-3.5">
              <div>
                <h3 className="text-[15px] font-bold text-[#1A172B]">
                  从我的资源库选取
                  {libraryPickerMode === 'bg'
                    ? '背景画面'
                    : libraryPickerMode === 'replace-el'
                      ? '替换选中图片'
                      : '图片素材'}
                </h3>
                <p className="text-[11.5px] text-[#6B6880]">
                  实时复用资源库中的内置壁纸与本地导入图片（保持原图引用关系）
                </p>
              </div>
              <button
                type="button"
                onClick={() => setLibraryPickerMode(null)}
                className="rounded-xl p-1.5 text-[#6B6880] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="grid flex-1 grid-cols-2 gap-3 overflow-y-auto p-5 sm:grid-cols-3 studio-scroll">
              {themes
                .filter((t) => Boolean(t.imageUrl))
                .map((t) => (
                  <button
                    key={t.id}
                    type="button"
                    onClick={async () => {
                      const url = t.imageUrl!;
                      setLibraryPickerMode(null);
                      if (libraryPickerMode === 'bg') {
                        commitProject((draft) => {
                          draft.bg = {
                            ...draft.bg,
                            kind: 'image',
                            url,
                            fit: 'cover',
                            hide: false,
                          };
                          return draft;
                        });
                        onNotify(`已从资源库引用背景「${t.displayName}」`);
                      } else if (libraryPickerMode === 'replace-el' && singleSelected?.kind === 'image') {
                        const dims = await readImageDimensions(url);
                        const box = computeImportMediaBox(dims.w, dims.h, canvasPxW, canvasPxH);
                        mutateElementsInCurrentLayout((els) =>
                          els.map((el) =>
                            el.id === singleSelected.id && el.kind === 'image'
                              ? ({
                                  ...el,
                                  src: url,
                                  w: box.w,
                                  h: box.h,
                                  ar: box.ar,
                                  size: box.size,
                                  fit: box.fit,
                                  sourceMeta: {
                                    origin: 'library',
                                    name: t.displayName,
                                    naturalWidth: dims.w,
                                    naturalHeight: dims.h,
                                  },
                                } as ImageElement)
                              : el
                          )
                        );
                        onNotify(`已替换为「${t.displayName}」`);
                      } else {
                        const dims = await readImageDimensions(url);
                        const box = computeImportMediaBox(dims.w, dims.h, canvasPxW, canvasPxH);
                        handleInsertElements(
                          [
                            makeElement('image', {
                              src: url,
                              w: box.w,
                              h: box.h,
                              size: box.size,
                              ar: box.ar,
                              fit: box.fit,
                              sourceMeta: {
                                origin: 'library',
                                name: t.displayName,
                                naturalWidth: dims.w,
                                naturalHeight: dims.h,
                              },
                            }),
                          ],
                          t.displayName
                        );
                      }
                    }}
                    className="group overflow-hidden rounded-2xl border border-[#E6E4F2] bg-[#F8F7FC] text-left transition-all hover:border-[#5E45F8] hover:shadow-md"
                  >
                    <div className="aspect-[16/9] w-full overflow-hidden bg-[#120733]">
                      <img
                        src={t.imageUrl}
                        alt={t.displayName}
                        className="h-full w-full object-cover transition-transform group-hover:scale-105"
                      />
                    </div>
                    <div className="p-2.5">
                      <div className="truncate text-[12px] font-bold text-[#1A172B]">
                        {t.displayName}
                      </div>
                      <div className="text-[10.5px] text-[#6B6880]">
                        {t.category} · {t.resolution}
                      </div>
                    </div>
                  </button>
                ))}
            </div>
          </div>
        </div>
      )}

      {/* Add Layout Spec Modal */}
      {addLayoutOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/50 p-4 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-2xl backdrop-blur-xl">
            <div className="flex items-center justify-between">
              <h3 className="text-[15px] font-bold text-[#1A172B]">新增适配设备规格画布</h3>
              <button
                type="button"
                onClick={() => setAddLayoutOpen(false)}
                className="rounded-xl p-1 text-[#6B6880] hover:bg-[#EFEBFD]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="mt-3 grid grid-cols-2 gap-2">
              {EDITOR_DEVICE_SPECS.map((s) => (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => {
                    handleSwitchDeviceSpec(s.size);
                    setAddLayoutOpen(false);
                  }}
                  className="rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5 text-left hover:border-[#5E45F8] hover:bg-[#EFEBFD]"
                >
                  <div className="text-[12px] font-bold text-[#1A172B]">{s.label}</div>
                  <div className="font-mono-tabular text-[11px] text-[#6B6880]">
                    {s.size[0]} × {s.size[1]}
                  </div>
                </button>
              ))}
            </div>

            <div className="mt-4 border-t border-[#E6E4F2] pt-3">
              <div className="text-[11.5px] font-bold text-[#6B6880]">自定义宽高分辨率 (px)</div>
              <div className="mt-2 flex items-center gap-2">
                <input
                  type="number"
                  min={200}
                  max={3840}
                  value={customLayoutW}
                  onChange={(e) => setCustomLayoutW(Number(e.target.value) || 1920)}
                  className="w-24 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 font-mono-tabular text-[12px]"
                />
                <span className="text-[#6B6880]">×</span>
                <input
                  type="number"
                  min={200}
                  max={3840}
                  value={customLayoutH}
                  onChange={(e) => setCustomLayoutH(Number(e.target.value) || 462)}
                  className="w-24 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-1.5 font-mono-tabular text-[12px]"
                />
                <button
                  type="button"
                  onClick={() => {
                    handleSwitchDeviceSpec([
                      Math.max(200, Math.min(3840, customLayoutW)),
                      Math.max(200, Math.min(3840, customLayoutH)),
                    ]);
                    setAddLayoutOpen(false);
                  }}
                  className="ml-auto rounded-xl bg-[#5E45F8] px-3 py-1.5 text-[12px] font-bold text-white hover:bg-[#4D34E6]"
                >
                  添加
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Shortcuts Help Modal */}
      {shortcutModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/50 p-4 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-2xl backdrop-blur-xl">
            <div className="flex items-center justify-between border-b border-[#E6E4F2] pb-2.5">
              <h3 className="text-[15px] font-bold text-[#1A172B]">主题编辑器快捷键清单</h3>
              <button
                type="button"
                onClick={() => setShortcutModalOpen(false)}
                className="rounded-xl p-1 text-[#6B6880] hover:bg-[#EFEBFD]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="mt-3 space-y-2 text-[12px]">
              {[
                ['Ctrl / Cmd + Z', '撤销上一步'],
                ['Ctrl / Cmd + Y / Shift+Z', '重做操作'],
                ['Ctrl / Cmd + C / V', '复制 / 粘贴选中图层'],
                ['Ctrl / Cmd + D', '快速原位创建副本'],
                ['Ctrl / Cmd + G / Shift+G', '编组 / 取消编组'],
                ['Ctrl / Cmd + A', '全选当前画布图层'],
                ['Ctrl / Cmd + 滚轮', '以居中视野顺滑缩放画布'],
                ['方向键 / Shift+方向键', '微调位置 0.5% / 2%'],
                ['Alt + 拖拽', '临时忽略智能吸附与安全边距'],
                ['Shift + 旋转拖拽', '按 15° 整数步进吸附旋转'],
                ['Space + 拖拽', '拖动画布视野平移'],
                ['] 键', '展开/收起右侧属性检查器'],
                ['双击文本图层', '直接在画布内编辑文字内容'],
              ].map(([k, v]) => (
                <div key={k} className="flex items-center justify-between border-b border-[#F1EFFA] pb-1.5">
                  <span className="font-mono-tabular font-semibold text-[#5E45F8]">{k}</span>
                  <span className="text-[#56526B]">{v}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Region Capture & Screen Recording Modal */}
      <CaptureModal
        mode={captureMode}
        defaultSize={[canvasPxW, canvasPxH]}
        onClose={() => setCaptureMode(null)}
        onCapturedImage={({ src, width, height, name }) => {
          setCaptureMode(null);
          const box = computeImportMediaBox(width, height, canvasPxW, canvasPxH);
          handleInsertElements(
            [
              makeElement('image', {
                src,
                w: box.w,
                h: box.h,
                ar: box.ar,
                size: box.size,
                fit: box.fit,
                sourceMeta: { origin: 'capture', name, naturalWidth: width, naturalHeight: height },
              }),
            ],
            name
          );
        }}
        onCapturedVideo={({ src, poster, width, height, dur, name }) => {
          setCaptureMode(null);
          const box = computeImportMediaBox(width, height, canvasPxW, canvasPxH);
          handleInsertElements(
            [
              makeElement('video', {
                src,
                poster,
                w: box.w,
                h: box.h,
                ar: box.ar,
                dur,
                size: box.size,
                fit: box.fit,
                sourceMeta: { origin: 'capture', name, naturalWidth: width, naturalHeight: height },
              }),
            ],
            name
          );
        }}
        onNotify={onNotify}
      />
    </div>
  );
};
