import { desktopMode, importVideo } from '../../services/desktopBridge';
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
  PanelLeft,
  PanelLeftOpen,
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
  Wand2,
  WifiOff,
  X,
  ZoomIn,
  ZoomOut,
} from 'lucide-react';
import type { SidebarMode } from '../Sidebar';
import { ContentCategory, DeviceInfo, ThemeItem } from '../../data/themes';
import {
  EDITOR_DEVICE_SPECS,
  EDITOR_RAIL_TABS,
  PLUGIN_CAPABILITY_REGISTRY,
  WIDGET_CATALOG,
  computeFit100BoxPct,
  computeImportMediaBox,
  computeNaturalBoxPct,
  genEditorId,
  makeElement,
  readImageDimensions,
  readVideoDimensions,
  reflowElementsForSize,
  specLabelFromSize,
} from '../../data/editorCatalog';
import { cloudResourceAdapter } from '../../services/cloudResourceAdapter';
import {
  BgConfig,
  ClockElement,
  EditorElement,
  EditorRailTab,
  EditorThemeProject,
  ImageElement,
  PluginElement,
  ShapeElement,
  THEME_SCHEMA_VERSION,
  TextElement,
  VideoElement,
  WidgetElement,
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
import { ContextMenuState, EditorContextMenu } from './EditorContextMenu';
import { EditorInspector, InspectorAlignMode } from './EditorInspector';
import { EditorLeftPanel } from './EditorLeftPanel';
import {
  CustomAssetPreset,
  StyleClipboardPayload,
  applyStyleToElement,
  deleteCustomAsset,
  extractStyleFromElement,
  loadCustomAssets,
  loadPersistedClipboard,
  loadPersistedStyleClipboard,
  saveCustomAsset,
  savePersistedClipboard,
  savePersistedStyleClipboard,
} from '../../utils/editorStorage';
import { extractPaletteFromBg, getFallbackPaletteFromBg } from '../../utils/paletteExtractor';

interface ThemeEditorV2Props {
  initialProject: EditorThemeProject;
  sourceThemeId?: string;
  activeDevice: DeviceInfo;
  themes: ThemeItem[];
  onSaveOnly: (theme: ThemeItem, project: EditorThemeProject) => void | Promise<boolean>;
  onSaveAndApply: (theme: ThemeItem, project: EditorThemeProject) => void | Promise<boolean>;
  onStartTempPreview: (theme: ThemeItem) => void;
  onExitEditor: () => void;
  onNotify: (msg: string) => void;
  sidebarMode?: SidebarMode;
  onChangeSidebarMode?: (mode: SidebarMode) => void;
}

interface BoxRect {
  left: number;
  top: number;
  width: number;
  height: number;
  cx: number;
  cy: number;
  rot: number;
  aabbTop: number;
  aabbBottom: number;
}

interface SiblingBoxPct {
  l: number;
  cx: number;
  r: number;
  t: number;
  cy: number;
  b: number;
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
      origBoxPct?: SiblingBoxPct;
      siblingTargets?: { v: number[]; h: number[] };
      siblingBoxes?: SiblingBoxPct[];
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
      type: 'multi-resize';
      handle: 'tl' | 'tr' | 'bl' | 'br';
      startX: number;
      startY: number;
      canvasW: number;
      canvasH: number;
      origBox: BoxRect;
      anchorPctX: number;
      anchorPctY: number;
      items: { origEl: EditorElement }[];
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
      type: 'multi-rotate';
      cx: number;
      cy: number;
      selLocalCx: number;
      selLocalCy: number;
      canvasW: number;
      canvasH: number;
      startAngle: number;
      items: { origEl: EditorElement; elCxPx: number; elCyPx: number }[];
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
  sidebarMode = 'hidden',
  onChangeSidebarMode,
}) => {
  const [project, setProject] = useState<EditorThemeProject>(() => cloneProj(initialProject));
  const latestProject = useRef(project);
  latestProject.current = project;
  const saving = useRef(false);
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

  // Clipboard, Custom Assets, Palette & Recent Local Imports
  const [clipboard, setClipboard] = useState<EditorElement[]>(() => loadPersistedClipboard());
  const [styleClipboard, setStyleClipboard] = useState<StyleClipboardPayload | null>(() =>
    loadPersistedStyleClipboard()
  );
  const [customAssets, setCustomAssets] = useState<CustomAssetPreset[]>(() => loadCustomAssets());
  const [extractedPalette, setExtractedPalette] = useState<string[]>(() =>
    getFallbackPaletteFromBg(initialProject.bg)
  );
  const [alignScope, setAlignScope] = useState<'selection' | 'canvas'>('selection');
  const [recentImports, setRecentImports] = useState<
    { id: string; name: string; kind: 'image' | 'video'; src: string; w: number; h: number }[]
  >([]);

  // Measured boxes on canvas + Snap guidelines + Equal-spacing guides + Live rotation HUD
  const [selBox, setSelBox] = useState<BoxRect | null>(null);
  const [snapGuides, setSnapGuides] = useState<{ v: number[]; h: number[] }>({ v: [], h: [] });
  const [gapGuides, setGapGuides] = useState<
    { axis: 'h' | 'v'; from: number; to: number; crossPos: number; pxGap: number }[]
  >([]);
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
  const [saveAssetModalOpen, setSaveAssetModalOpen] = useState<boolean>(false);
  const [saveAssetName, setSaveAssetName] = useState<string>('');
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
  const [contextMenu, setContextMenu] = useState<ContextMenuState | null>(null);

  const stageRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const dragRef = useRef<DragSession | null>(null);
  const fileUploadRef = useRef<HTMLInputElement | null>(null);
  const fileUploadModeRef = useRef<'el' | 'bg' | 'replace-el'>('el');
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

  // Sync persisted cross-theme clipboard & style clipboard across tabs/sessions
  useEffect(() => {
    const syncClipboard = () => {
      setClipboard(loadPersistedClipboard());
      setStyleClipboard(loadPersistedStyleClipboard());
      setCustomAssets(loadCustomAssets());
    };
    window.addEventListener('storage', syncClipboard);
    window.addEventListener('focus', syncClipboard);
    return () => {
      window.removeEventListener('storage', syncClipboard);
      window.removeEventListener('focus', syncClipboard);
    };
  }, []);

  // Extract recommended color palette whenever background changes
  useEffect(() => {
    let cancelled = false;
    setExtractedPalette(getFallbackPaletteFromBg(project.bg));
    void extractPaletteFromBg(project.bg).then((palette) => {
      if (!cancelled && palette.length > 0) {
        setExtractedPalette(palette);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [project.bg]);

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

    if (selectedIds.length === 1) {
      const id = selectedIds[0];
      const node = canvasEl.querySelector(`[data-el-id="${id}"]`) as HTMLElement | null;
      if (!node) {
        setSelBox(null);
        return;
      }
      const r = node.getBoundingClientRect();
      const elObj = layoutElements.find((e) => e.id === id);
      const rot = elObj?.rot || 0;
      const cx = r.left + r.width / 2 - cRect.left;
      const cy = r.top + r.height / 2 - cRect.top;
      const width = Math.max(14, node.offsetWidth || r.width);
      const height = Math.max(14, node.offsetHeight || r.height);
      setSelBox({
        left: cx - width / 2,
        top: cy - height / 2,
        width,
        height,
        cx,
        cy,
        rot,
        aabbTop: r.top - cRect.top,
        aabbBottom: r.bottom - cRect.top,
      });
      return;
    }

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
      rot: 0,
      aabbTop: top,
      aabbBottom: top + height,
    });
  }, [layoutElements, selectedIds]);

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

  // Import local image/video file into canvas, bg, or replace selected media layer
  const importMediaFile = useCallback(
    async (file: File, mode: 'el' | 'bg' | 'replace-el') => {
      const isVideo = file.type.startsWith('video/');
      const isImage = file.type.startsWith('image/');
      if (!isVideo && !isImage) {
        onNotify('暂不支持该文件格式，请上传 PNG / JPG / GIF / WebP / MP4 / WebM');
        return;
      }

      const replaceTarget =
        mode === 'replace-el' && selectedElements.length === 1 ? selectedElements[0] : null;

      if (isVideo) {
        const url = desktopMode ? (await importVideo(file, () => {})).videoUrl! : URL.createObjectURL(file);
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
        } else if (replaceTarget?.kind === 'video') {
          const box = computeImportMediaBox(dims.w, dims.h, canvasPxW, canvasPxH);
          mutateElementsInCurrentLayout((els) =>
            els.map((el) =>
              el.id === replaceTarget.id && el.kind === 'video'
                ? ({
                    ...el,
                    src: url,
                    w: box.w,
                    h: box.h,
                    ar: box.ar,
                    size: box.size,
                    fit: box.fit,
                    dur: dims.dur,
                    sourceMeta: {
                      origin: 'local',
                      name: file.name,
                      naturalWidth: dims.w,
                      naturalHeight: dims.h,
                    },
                  } as VideoElement)
                : el
            )
          );
          onNotify(`已替换本地视频「${file.name}」`);
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
      } else if (replaceTarget?.kind === 'image') {
        const box = computeImportMediaBox(dims.w, dims.h, canvasPxW, canvasPxH);
        mutateElementsInCurrentLayout((els) =>
          els.map((el) =>
            el.id === replaceTarget.id && el.kind === 'image'
              ? ({
                  ...el,
                  src: dataUrl,
                  w: box.w,
                  h: box.h,
                  ar: box.ar,
                  size: box.size,
                  fit: box.fit,
                  sourceMeta: {
                    origin: 'local',
                    name: file.name,
                    naturalWidth: dims.w,
                    naturalHeight: dims.h,
                  },
                } as ImageElement)
              : el
          )
        );
        onNotify(`已替换本地图片「${file.name}」(${dims.w}×${dims.h})`);
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
    [
      canvasPxH,
      canvasPxW,
      checkVideoLimit,
      commitProject,
      handleInsertElements,
      mutateElementsInCurrentLayout,
      onNotify,
      selectedElements,
    ]
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
      const idSet = new Set(ids);
      mutateElementsInCurrentLayout((els) => {
        const copy = [...els];
        if (op === 'top') {
          const rest = copy.filter((e) => !idSet.has(e.id));
          const moved = copy.filter((e) => idSet.has(e.id));
          return [...rest, ...moved];
        }
        if (op === 'bottom') {
          const moved = copy.filter((e) => idSet.has(e.id));
          const rest = copy.filter((e) => !idSet.has(e.id));
          return [...moved, ...rest];
        }
        if (op === 'up') {
          for (let i = copy.length - 2; i >= 0; i -= 1) {
            if (idSet.has(copy[i].id) && !idSet.has(copy[i + 1].id)) {
              const tmp = copy[i];
              copy[i] = copy[i + 1];
              copy[i + 1] = tmp;
            }
          }
          return copy;
        }
        for (let i = 1; i < copy.length; i += 1) {
          if (idSet.has(copy[i].id) && !idSet.has(copy[i - 1].id)) {
            const tmp = copy[i];
            copy[i] = copy[i - 1];
            copy[i - 1] = tmp;
          }
        }
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
    const deletableIds = selectedIds.filter((id) => !project.locks[id]);
    if (deletableIds.length === 0) {
      onNotify('选中图层已锁定，请先解锁后再删除');
      return;
    }
    const delSet = new Set(deletableIds);
    mutateElementsInCurrentLayout((els, draft) => {
      deletableIds.forEach((id) => {
        delete draft.locks[id];
        delete draft.names[id];
      });
      return els.filter((e) => !delSet.has(e.id));
    });
    setSelectedIds((prev) => prev.filter((id) => !delSet.has(id)));
    onNotify(`已删除 ${delSet.size} 个图层`);
  }, [mutateElementsInCurrentLayout, onNotify, project.locks, selectedIds]);

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

  // Align & Even Distribution (supports selection-relative alignment + canvas anchor snap)
  const handleAlignSelection = useCallback(
    (mode: InspectorAlignMode, explicitScope?: 'selection' | 'canvas') => {
      if (selectedIds.length === 0) return;
      const scope = selectedIds.length > 1 ? explicitScope ?? alignScope : 'canvas';
      const idSet = new Set(selectedIds);

      if (scope === 'selection' && selectedIds.length > 1 && canvasRef.current) {
        const canvasEl = canvasRef.current;
        const cRect = canvasEl.getBoundingClientRect();
        const cw = Math.max(1, cRect.width);
        const ch = Math.max(1, cRect.height);

        const measured = new Map<
          string,
          { l: number; r: number; cx: number; t: number; b: number; cy: number }
        >();
        let unionL = Infinity;
        let unionR = -Infinity;
        let unionT = Infinity;
        let unionB = -Infinity;

        selectedIds.forEach((id) => {
          const node = canvasEl.querySelector(`[data-el-id="${id}"]`) as HTMLElement | null;
          if (!node) return;
          const r = node.getBoundingClientRect();
          const box = {
            l: ((r.left - cRect.left) / cw) * 100,
            r: ((r.right - cRect.left) / cw) * 100,
            cx: ((r.left + r.width / 2 - cRect.left) / cw) * 100,
            t: ((r.top - cRect.top) / ch) * 100,
            b: ((r.bottom - cRect.top) / ch) * 100,
            cy: ((r.top + r.height / 2 - cRect.top) / ch) * 100,
          };
          measured.set(id, box);
          unionL = Math.min(unionL, box.l);
          unionR = Math.max(unionR, box.r);
          unionT = Math.min(unionT, box.t);
          unionB = Math.max(unionB, box.b);
        });

        if (measured.size > 1) {
          const unionCX = (unionL + unionR) / 2;
          const unionCY = (unionT + unionB) / 2;

          mutateElementsInCurrentLayout((els) =>
            els.map((el) => {
              if (!idSet.has(el.id) || project.locks[el.id]) return el;
              const box = measured.get(el.id);
              if (!box) return el;
              let dxPct = 0;
              let dyPct = 0;
              if (mode === 'l' || mode === 'tl' || mode === 'ml' || mode === 'bl') {
                dxPct = unionL - box.l;
              } else if (mode === 'hc' || mode === 'tc' || mode === 'mc' || mode === 'bc' || mode === 'c') {
                dxPct = unionCX - box.cx;
              } else if (mode === 'r' || mode === 'tr' || mode === 'mr' || mode === 'br') {
                dxPct = unionR - box.r;
              }

              if (mode === 't' || mode === 'tl' || mode === 'tc' || mode === 'tr') {
                dyPct = unionT - box.t;
              } else if (mode === 'vc' || mode === 'ml' || mode === 'mc' || mode === 'mr' || mode === 'c') {
                dyPct = unionCY - box.cy;
              } else if (mode === 'b' || mode === 'bl' || mode === 'bc' || mode === 'br') {
                dyPct = unionB - box.b;
              }

              return {
                ...el,
                x: +(el.x + dxPct).toFixed(2),
                y: +(el.y + dyPct).toFixed(2),
              };
            })
          );
          onNotify('已按选区边界互相对齐');
          return;
        }
      }

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
      if (selectedIds.length > 1) {
        onNotify('已相对画布完成对齐');
      }
    },
    [alignScope, mutateElementsInCurrentLayout, onNotify, project.locks, selectedIds]
  );

  const handleDistributeSelection = useCallback(
    (axis: 'h' | 'v') => {
      if (selectedIds.length < 3) {
        onNotify('至少需要选中 3 个元素才能等距分布');
        return;
      }
      const selected = layoutElements.filter((e) => selectedIds.includes(e.id));
      const canvasEl = canvasRef.current;

      if (canvasEl) {
        const cRect = canvasEl.getBoundingClientRect();
        const cw = Math.max(1, cRect.width);
        const ch = Math.max(1, cRect.height);
        const measured = selected
          .map((el) => {
            const node = canvasEl.querySelector(`[data-el-id="${el.id}"]`) as HTMLElement | null;
            if (!node) return null;
            const r = node.getBoundingClientRect();
            return {
              id: el.id,
              center:
                axis === 'h'
                  ? ((r.left + r.width / 2 - cRect.left) / cw) * 100
                  : ((r.top + r.height / 2 - cRect.top) / ch) * 100,
            };
          })
          .filter((item): item is { id: string; center: number } => item !== null);

        if (measured.length >= 3) {
          measured.sort((a, b) => a.center - b.center);
          const first = measured[0].center;
          const last = measured[measured.length - 1].center;
          const step = (last - first) / (measured.length - 1);
          const deltaMap = new Map(
            measured.map((m, idx) => [m.id, first + step * idx - m.center])
          );
          mutateElementsInCurrentLayout((els) =>
            els.map((el) => {
              const delta = deltaMap.get(el.id);
              if (delta === undefined || project.locks[el.id]) return el;
              return axis === 'h'
                ? { ...el, x: +(el.x + delta).toFixed(2) }
                : { ...el, y: +(el.y + delta).toFixed(2) };
            })
          );
          onNotify(axis === 'h' ? '已完成水平等距分布' : '已完成垂直等距分布');
          return;
        }
      }

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

  const handleCopyElements = useCallback(() => {
    if (selectedElements.length === 0) return;
    const cloned = structuredClone(selectedElements);
    setClipboard(cloned);
    const saved = savePersistedClipboard(cloned);
    onNotify(
      saved
        ? `已复制 ${cloned.length} 个图层（支持跨主题粘贴）`
        : `已复制 ${cloned.length} 个图层（内容较大，仅当前会话可用）`
    );
  }, [onNotify, selectedElements]);

  const handlePasteElements = useCallback(() => {
    const source = clipboard.length > 0 ? clipboard : loadPersistedClipboard();
    if (source.length === 0) return;
    const pasted = source.map((c) => ({
      ...structuredClone(c),
      x: +(c.x + 2).toFixed(2),
      y: +(c.y + 3).toFixed(2),
      g: undefined,
    }));
    handleInsertElements(pasted, `粘贴 (${pasted.length}项)`);
  }, [clipboard, handleInsertElements]);

  const handleCopyStyle = useCallback(() => {
    const target = selectedElements.length === 1 ? selectedElements[0] : null;
    if (!target) return;
    const payload = extractStyleFromElement(target);
    setStyleClipboard(payload);
    savePersistedStyleClipboard(payload);
    onNotify('已复制图层样式参数（支持跨主题粘贴）');
  }, [onNotify, selectedElements]);

  const handlePasteStyle = useCallback(() => {
    const payload = styleClipboard ?? loadPersistedStyleClipboard();
    if (!payload || selectedIds.length === 0) return;
    const idSet = new Set(selectedIds);
    mutateElementsInCurrentLayout((els) =>
      els.map((el) =>
        idSet.has(el.id) && !project.locks[el.id] ? applyStyleToElement(el, payload) : el
      )
    );
    onNotify('已粘贴样式到选中图层');
  }, [mutateElementsInCurrentLayout, onNotify, project.locks, selectedIds, styleClipboard]);

  const handleBatchColor = useCallback(
    (hex: string) => {
      if (selectedIds.length === 0) return;
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          if (el.kind === 'image' || el.kind === 'video') return el;
          if (el.kind === 'widget' || el.kind === 'plugin') {
            return { ...el, accentColor: hex } as EditorElement;
          }
          return { ...el, color: hex };
        })
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  const handleBatchAlpha = useCallback(
    (alpha: number) => {
      if (selectedIds.length === 0) return;
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) =>
          idSet.has(el.id) && !project.locks[el.id] ? { ...el, alpha } : el
        )
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  const handleBatchFont = useCallback(
    (font: string) => {
      if (selectedIds.length === 0) return;
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          if (
            el.kind === 'text' ||
            el.kind === 'date' ||
            (el.kind === 'clock' && (el as ClockElement).style !== 'analog')
          ) {
            return { ...el, font };
          }
          return el;
        })
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  const handleBatchFontSize = useCallback(
    (sizeCqmin: number) => {
      if (selectedIds.length === 0) return;
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          if (
            el.kind === 'text' ||
            el.kind === 'date' ||
            (el.kind === 'clock' && (el as ClockElement).style !== 'analog')
          ) {
            return { ...el, size: sizeCqmin };
          }
          return el;
        })
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  const handleBatchRadius = useCallback(
    (radius: number) => {
      if (selectedIds.length === 0) return;
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          if (
            el.kind === 'image' ||
            el.kind === 'video' ||
            (el.kind === 'shape' && ((el as ShapeElement).sh || 'rect') === 'rect')
          ) {
            return { ...el, radius } as EditorElement;
          }
          return el;
        })
      );
    },
    [mutateElementsInCurrentLayout, project.locks, selectedIds]
  );

  const handleOpenSaveAssetModal = useCallback(() => {
    if (selectedElements.length === 0) return;
    const first = selectedElements[0];
    const defaultName =
      selectedElements.length === 1
        ? project.names[first.id] ||
          (first.kind === 'text' ? first.text.slice(0, 12) : `${first.kind} 预设`)
        : `组合预设 (${selectedElements.length}项)`;
    setSaveAssetName(defaultName);
    setSaveAssetModalOpen(true);
  }, [project.names, selectedElements]);

  const handleConfirmSaveAsset = useCallback(() => {
    if (selectedElements.length === 0) return;
    const name = saveAssetName.trim() || '我的素材预设';
    const res = saveCustomAsset(name, selectedElements);
    setCustomAssets(res.list);
    setSaveAssetModalOpen(false);
    if (res.ok) {
      onNotify(`已存为我的素材「${name}」，可在左侧「素材 → 我的」中复用`);
    } else {
      onNotify('本地存储空间不足，未能保存素材预设');
    }
  }, [onNotify, saveAssetName, selectedElements]);

  const handleDeleteCustomAsset = useCallback(
    (id: string) => {
      const next = deleteCustomAsset(id);
      setCustomAssets(next);
      onNotify('已删除自定义素材预设');
    },
    [onNotify]
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

  const handleFlipSelection = useCallback(
    (axis: 'x' | 'y') => {
      if (selectedIds.length === 0) return;
      const idSet = new Set(selectedIds);
      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (!idSet.has(el.id) || project.locks[el.id]) return el;
          return axis === 'x'
            ? { ...el, sx: (el.sx ?? 1) * -1 }
            : { ...el, sy: (el.sy ?? 1) * -1 };
        })
      );
      onNotify(axis === 'x' ? '已水平翻转选中图层' : '已垂直翻转选中图层');
    },
    [mutateElementsInCurrentLayout, onNotify, project.locks, selectedIds]
  );

  const handleReorderElements = useCallback(
    (orderedIdsBottomToTop: string[]) => {
      mutateElementsInCurrentLayout((els) => {
        const byId = new Map(els.map((el) => [el.id, el]));
        const next: EditorElement[] = [];
        const seen = new Set<string>();
        orderedIdsBottomToTop.forEach((id) => {
          const found = byId.get(id);
          if (found && !seen.has(id)) {
            next.push(found);
            seen.add(id);
          }
        });
        els.forEach((el) => {
          if (!seen.has(el.id)) next.push(el);
        });
        return next;
      });
    },
    [mutateElementsInCurrentLayout]
  );

  const handleCommitLayerName = useCallback(
    (id: string, nextName: string) => {
      const trimmed = nextName.trim();
      commitProject((draft) => {
        if (trimmed) draft.names[id] = trimmed;
        else delete draft.names[id];
        return draft;
      });
    },
    [commitProject]
  );

  const handleSmartReflowLayout = useCallback(
    (targetIdx: number = layoutIndex) => {
      const targetLayout = project.layouts[targetIdx];
      if (targetIdx <= 0 || !targetLayout) return;
      const masterSize = project.layouts[0]?.size ?? [1920, 462];
      const targetSize = targetLayout.size;
      commitProject((draft) => {
        const target = draft.layouts[targetIdx];
        if (!target) return draft;
        const sourceEls = cloneProj(draft).master.map((el) =>
          target.vis?.[el.id] ? { ...el, hide: true } : el
        );
        target.follow = false;
        target.elements = reflowElementsForSize(sourceEls, masterSize, targetSize);
        delete target.vis;
        return draft;
      });
      setLayoutIndex(targetIdx);
      setSelectedIds([]);
      onNotify(
        `已按 ${targetLayout.label} (${targetSize[0]}×${targetSize[1]}) 智能重排，Ctrl+Z 可撤销`
      );
    },
    [commitProject, layoutIndex, onNotify, project.layouts]
  );

  const handleApplyMediaSizeMode = useCallback(
    async (mediaEl: ImageElement | VideoElement, mode: 'natural' | 'fit100') => {
      let nw = mediaEl.sourceMeta?.naturalWidth || 0;
      let nh = mediaEl.sourceMeta?.naturalHeight || 0;
      if (!nw || !nh) {
        if (mediaEl.kind === 'video') {
          const d = await readVideoDimensions(mediaEl.src);
          nw = d.w;
          nh = d.h;
        } else {
          const d = await readImageDimensions(mediaEl.src);
          nw = d.w;
          nh = d.h;
        }
      }
      const box =
        mode === 'natural'
          ? computeNaturalBoxPct(nw, nh, canvasPxW, canvasPxH)
          : computeFit100BoxPct(nw, nh, canvasPxW, canvasPxH);

      mutateElementsInCurrentLayout((els) =>
        els.map((el) => {
          if (el.id !== mediaEl.id) return el;
          const nextSourceMeta = {
            ...(el.kind === 'image' || el.kind === 'video' ? el.sourceMeta : {}),
            naturalWidth: nw,
            naturalHeight: nh,
          };
          if (mode === 'fit100') {
            return {
              ...el,
              a: 'mc',
              x: 0,
              y: 0,
              w: box.w,
              h: box.h,
              ar: box.ar,
              size: box.size,
              fit: 'contain',
              sourceMeta: nextSourceMeta,
            } as EditorElement;
          }
          return {
            ...el,
            w: box.w,
            h: box.h,
            ar: box.ar,
            size: box.size,
            sourceMeta: nextSourceMeta,
          } as EditorElement;
        })
      );
      onNotify(
        mode === 'natural'
          ? `已恢复原始像素比例 (${nw}×${nh})`
          : `已 100% 比例适配居中 (${nw}×${nh})`
      );
    },
    [canvasPxH, canvasPxW, mutateElementsInCurrentLayout, onNotify]
  );

  const handleResetWidgetOrPluginDefaultSize = useCallback(
    (targetEl: EditorElement) => {
      if (targetEl.kind === 'widget') {
        const wEl = targetEl as WidgetElement;
        const spec = WIDGET_CATALOG.find((item) => item.type === wEl.widgetType);
        const defW = spec?.defaultW ?? 28;
        const defH = spec?.defaultH ?? 36;
        mutateElementsInCurrentLayout((els) =>
          els.map((el) => (el.id === wEl.id ? { ...el, w: defW, h: defH } : el))
        );
        onNotify(`已恢复组件默认尺寸 (${defW}% × ${defH}%)`);
      } else if (targetEl.kind === 'plugin') {
        const pEl = targetEl as PluginElement;
        const spec = PLUGIN_CAPABILITY_REGISTRY.find((item) => item.key === pEl.capabilityKey);
        const defW = spec?.defaultW ?? 30;
        const defH = spec?.defaultH ?? 36;
        mutateElementsInCurrentLayout((els) =>
          els.map((el) => (el.id === pEl.id ? { ...el, w: defW, h: defH } : el))
        );
        onNotify(`已恢复插件默认尺寸 (${defW}% × ${defH}%)`);
      }
    },
    [mutateElementsInCurrentLayout, onNotify]
  );

  const handleQuickAddKind = useCallback(
    (kind: 'text' | 'clock' | 'date' | 'shape' | 'widget') => {
      if (kind === 'text') {
        handleInsertElements([makeElement('text', { text: '新建文本' })], '文字');
      } else if (kind === 'clock') {
        handleInsertElements([makeElement('clock', { size: 32 })], '数字时钟');
      } else if (kind === 'date') {
        handleInsertElements([makeElement('date', { size: 7 })], '日期星期');
      } else if (kind === 'shape') {
        handleInsertElements([makeElement('shape', { w: 20, h: 20, radius: 12 })], '几何形状');
      } else {
        handleInsertElements(
          [makeElement('widget', { widgetType: 'sysmon', w: 30, h: 52 })],
          '硬件监控组件'
        );
      }
    },
    [handleInsertElements]
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
        handleCopyElements();
        return;
      }
      if (meta && e.key.toLowerCase() === 'v') {
        const hasPersisted = clipboard.length > 0 || loadPersistedClipboard().length > 0;
        if (hasPersisted) {
          e.preventDefault();
          handlePasteElements();
          return;
        }
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
    handleCopyElements,
    handleDeleteSelection,
    handleDuplicateSelection,
    handleGroupSelection,
    handlePasteElements,
    handleRedo,
    handleUndo,
    handleUngroupSelection,
    layoutElements,
    layoutIndex,
    mutateElementsInCurrentLayout,
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
        const rawClientDx = e.clientX - drag.startX;
        const rawClientDy = e.clientY - drag.startY;
        const lockH = e.shiftKey && Math.abs(rawClientDx) >= Math.abs(rawClientDy);
        const lockV = e.shiftKey && Math.abs(rawClientDy) > Math.abs(rawClientDx);
        const dxPct = lockV ? 0 : (rawClientDx / drag.canvasW) * 100;
        const dyPct = lockH ? 0 : (rawClientDy / drag.canvasH) * 100;
        if (Math.abs(dxPct) > 0.1 || Math.abs(dyPct) > 0.1) drag.moved = true;

        let snapDx = 0;
        let snapDy = 0;
        const activeV: number[] = [];
        const activeH: number[] = [];
        const nextGapGuides: {
          axis: 'h' | 'v';
          from: number;
          to: number;
          crossPos: number;
          pxGap: number;
        }[] = [];

        if (!e.altKey) {
          if (drag.origBoxPct && drag.siblingTargets) {
            const box = drag.origBoxPct;
            if (!lockV) {
              const curV = [box.l + dxPct, box.cx + dxPct, box.r + dxPct];
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
            }

            if (!lockH) {
              const curH = [box.t + dyPct, box.cy + dyPct, box.b + dyPct];
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
            }

            // Equal Spacing (gap) detection against non-selected siblings
            if (drag.siblingBoxes && drag.siblingBoxes.length > 0) {
              const curBox = {
                l: box.l + dxPct + snapDx,
                r: box.r + dxPct + snapDx,
                cx: box.cx + dxPct + snapDx,
                t: box.t + dyPct + snapDy,
                b: box.b + dyPct + snapDy,
                cy: box.cy + dyPct + snapDy,
              };

              // Horizontal equal spacing
              if (!lockV) {
                const rowSibs = drag.siblingBoxes.filter(
                  (s) => s.b >= curBox.t - 6 && s.t <= curBox.b + 6
                );
                const leftSibs = rowSibs
                  .filter((s) => s.r <= curBox.l + 0.35)
                  .sort((a, b) => b.r - a.r);
                const rightSibs = rowSibs
                  .filter((s) => s.l >= curBox.r - 0.35)
                  .sort((a, b) => a.l - b.l);

                let matchedH = false;
                if (leftSibs.length > 0 && rightSibs.length > 0) {
                  const ls = leftSibs[0];
                  const rs = rightSibs[0];
                  const gapL = curBox.l - ls.r;
                  const gapR = rs.l - curBox.r;
                  if (gapL >= 0.4 && gapR >= 0.4 && Math.abs(gapL - gapR) < 1.2) {
                    const adjust = activeV.length === 0 ? (gapR - gapL) / 2 : 0;
                    snapDx += adjust;
                    const finalL = curBox.l + adjust;
                    const finalR = curBox.r + adjust;
                    const finalGap = finalL - ls.r;
                    const pxGap = Math.round((finalGap / 100) * canvasPxW);
                    const crossY = (Math.max(curBox.t, Math.min(ls.t, rs.t)) + Math.min(curBox.b, Math.max(ls.b, rs.b))) / 2;
                    nextGapGuides.push(
                      { axis: 'h', from: ls.r, to: finalL, crossPos: crossY, pxGap },
                      { axis: 'h', from: finalR, to: rs.l, crossPos: crossY, pxGap }
                    );
                    matchedH = true;
                  }
                }
                if (!matchedH && rowSibs.length >= 2) {
                  const sortedRow = [...rowSibs].sort((a, b) => a.l - b.l);
                  for (let i = 0; i < sortedRow.length - 1; i += 1) {
                    const sA = sortedRow[i];
                    const sB = sortedRow[i + 1];
                    const gAB = sB.l - sA.r;
                    if (gAB < 0.5) continue;
                    const crossY = (sA.cy + sB.cy + curBox.cy) / 3;
                    if (curBox.l >= sB.r - 0.35 && Math.abs(curBox.l - sB.r - gAB) < 1.15) {
                      const adjust = activeV.length === 0 ? gAB - (curBox.l - sB.r) : 0;
                      snapDx += adjust;
                      const pxGap = Math.round((gAB / 100) * canvasPxW);
                      nextGapGuides.push(
                        { axis: 'h', from: sA.r, to: sB.l, crossPos: crossY, pxGap },
                        { axis: 'h', from: sB.r, to: curBox.l + adjust, crossPos: crossY, pxGap }
                      );
                      break;
                    }
                    if (curBox.r <= sA.l + 0.35 && Math.abs(sA.l - curBox.r - gAB) < 1.15) {
                      const adjust = activeV.length === 0 ? (sA.l - curBox.r) - gAB : 0;
                      snapDx += adjust;
                      const pxGap = Math.round((gAB / 100) * canvasPxW);
                      nextGapGuides.push(
                        { axis: 'h', from: curBox.r + adjust, to: sA.l, crossPos: crossY, pxGap },
                        { axis: 'h', from: sA.r, to: sB.l, crossPos: crossY, pxGap }
                      );
                      break;
                    }
                  }
                }
              }

              // Vertical equal spacing
              if (!lockH) {
                const colSibs = drag.siblingBoxes.filter(
                  (s) => s.r >= curBox.l - 6 && s.l <= curBox.r + 6
                );
                const topSibs = colSibs
                  .filter((s) => s.b <= curBox.t + 0.35)
                  .sort((a, b) => b.b - a.b);
                const botSibs = colSibs
                  .filter((s) => s.t >= curBox.b - 0.35)
                  .sort((a, b) => a.t - b.t);

                let matchedV = false;
                if (topSibs.length > 0 && botSibs.length > 0) {
                  const ts = topSibs[0];
                  const bs = botSibs[0];
                  const gapT = curBox.t - ts.b;
                  const gapB = bs.t - curBox.b;
                  if (gapT >= 0.4 && gapB >= 0.4 && Math.abs(gapT - gapB) < 1.2) {
                    const adjust = activeH.length === 0 ? (gapB - gapT) / 2 : 0;
                    snapDy += adjust;
                    const finalT = curBox.t + adjust;
                    const finalB = curBox.b + adjust;
                    const finalGap = finalT - ts.b;
                    const pxGap = Math.round((finalGap / 100) * canvasPxH);
                    const crossX = (Math.max(curBox.l, Math.min(ts.l, bs.l)) + Math.min(curBox.r, Math.max(ts.r, bs.r))) / 2;
                    nextGapGuides.push(
                      { axis: 'v', from: ts.b, to: finalT, crossPos: crossX, pxGap },
                      { axis: 'v', from: finalB, to: bs.t, crossPos: crossX, pxGap }
                    );
                    matchedV = true;
                  }
                }
                if (!matchedV && colSibs.length >= 2) {
                  const sortedCol = [...colSibs].sort((a, b) => a.t - b.t);
                  for (let i = 0; i < sortedCol.length - 1; i += 1) {
                    const sA = sortedCol[i];
                    const sB = sortedCol[i + 1];
                    const gAB = sB.t - sA.b;
                    if (gAB < 0.5) continue;
                    const crossX = (sA.cx + sB.cx + curBox.cx) / 3;
                    if (curBox.t >= sB.b - 0.35 && Math.abs(curBox.t - sB.b - gAB) < 1.15) {
                      const adjust = activeH.length === 0 ? gAB - (curBox.t - sB.b) : 0;
                      snapDy += adjust;
                      const pxGap = Math.round((gAB / 100) * canvasPxH);
                      nextGapGuides.push(
                        { axis: 'v', from: sA.b, to: sB.t, crossPos: crossX, pxGap },
                        { axis: 'v', from: sB.b, to: curBox.t + adjust, crossPos: crossX, pxGap }
                      );
                      break;
                    }
                    if (curBox.b <= sA.t + 0.35 && Math.abs(sA.t - curBox.b - gAB) < 1.15) {
                      const adjust = activeH.length === 0 ? (sA.t - curBox.b) - gAB : 0;
                      snapDy += adjust;
                      const pxGap = Math.round((gAB / 100) * canvasPxH);
                      nextGapGuides.push(
                        { axis: 'v', from: curBox.b + adjust, to: sA.t, crossPos: crossX, pxGap },
                        { axis: 'v', from: sA.b, to: sB.t, crossPos: crossX, pxGap }
                      );
                      break;
                    }
                  }
                }
              }
            }
          } else if (drag.ids.length === 1) {
            const lead = layoutElements.find((el) => el.id === drag.ids[0]);
            const orig = drag.origins[drag.ids[0]];
            if (lead && orig) {
              const fx = FX_ANCHOR[lead.a[1] as 'l' | 'c' | 'r'] ?? 50;
              const fy = FY_ANCHOR[lead.a[0] as 't' | 'm' | 'b'] ?? 50;
              const curX = fx + orig.x + dxPct;
              const curY = fy + orig.y + dyPct;
              [4, 50, 96].forEach((target) => {
                if (!lockV && Math.abs(curX - target) < 1.3) {
                  snapDx = target - curX;
                  activeV.push(target);
                }
                if (!lockH && Math.abs(curY - target) < 1.3) {
                  snapDy = target - curY;
                  activeH.push(target);
                }
              });
            }
          }
        }
        setSnapGuides({ v: activeV, h: activeH });
        setGapGuides(nextGapGuides);

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

      if (drag.type === 'multi-resize') {
        const dx = e.clientX - drag.startX;
        const dy = e.clientY - drag.startY;
        drag.moved = true;
        const dw = drag.handle.includes('r') ? dx : -dx;
        const dh = drag.handle.includes('b') ? dy : -dy;
        const normW = dw / Math.max(24, drag.origBox.width);
        const normH = dh / Math.max(24, drag.origBox.height);
        const deltaNorm = Math.abs(normW) >= Math.abs(normH) ? normW : normH;
        const factor = Math.max(0.2, Math.min(5, 1 + deltaNorm));

        const itemMap = new Map(drag.items.map((it) => [it.origEl.id, it.origEl]));
        mutateElementsInCurrentLayout(
          (els) =>
            els.map((el) => {
              const orig = itemMap.get(el.id);
              if (!orig) return el;
              const [fx, fy] = resolveAnchorPercentages(orig.a);
              const nextX = +(drag.anchorPctX + factor * (fx + orig.x - drag.anchorPctX) - fx).toFixed(2);
              const nextY = +(drag.anchorPctY + factor * (fy + orig.y - drag.anchorPctY) - fy).toFixed(2);
              const nextSize = +Math.max(2, Math.min(140, orig.size * factor)).toFixed(1);
              const patched: EditorElement & { w?: number; h?: number } = {
                ...el,
                x: nextX,
                y: nextY,
                size: nextSize,
              };
              if ('w' in orig && typeof orig.w === 'number') {
                patched.w = +Math.max(0.2, Math.min(1000, orig.w * factor)).toFixed(2);
              }
              if ('h' in orig && typeof orig.h === 'number') {
                patched.h = +Math.max(0.2, Math.min(1000, orig.h * factor)).toFixed(2);
              }
              return patched as EditorElement;
            }),
          false
        );
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
        return;
      }

      if (drag.type === 'multi-rotate') {
        drag.moved = true;
        const curAngle = (Math.atan2(e.clientY - drag.cy, e.clientX - drag.cx) * 180) / Math.PI;
        let deltaDeg = curAngle - drag.startAngle;
        if (e.shiftKey) {
          deltaDeg = Math.round(deltaDeg / 15) * 15;
        } else {
          const norm = ((deltaDeg % 360) + 360) % 360;
          const snapTargets = [0, 90, 180, 270, 360];
          const near = snapTargets.find((s) => Math.abs(norm - s) < 3.5);
          if (near !== undefined) deltaDeg = near % 360;
        }
        const rad = (deltaDeg * Math.PI) / 180;
        const cos = Math.cos(rad);
        const sin = Math.sin(rad);
        const displayDeg = ((Math.round(deltaDeg) % 360) + 360) % 360;
        setRotateHud({ deg: displayDeg, x: e.clientX + 14, y: e.clientY - 12 });

        const itemMap = new Map(drag.items.map((it) => [it.origEl.id, it]));
        mutateElementsInCurrentLayout(
          (els) =>
            els.map((el) => {
              const info = itemMap.get(el.id);
              if (!info) return el;
              const vx = info.elCxPx - drag.selLocalCx;
              const vy = info.elCyPx - drag.selLocalCy;
              const newVx = vx * cos - vy * sin;
              const newVy = vx * sin + vy * cos;
              const dCxPx = newVx - vx;
              const dCyPx = newVy - vy;
              const nextRot = (((info.origEl.rot || 0) + deltaDeg) % 360 + 360) % 360;
              return {
                ...el,
                x: +(info.origEl.x + (dCxPx / drag.canvasW) * 100).toFixed(2),
                y: +(info.origEl.y + (dCyPx / drag.canvasH) * 100).toFixed(2),
                rot: Math.round(nextRot),
              };
            }),
          false
        );
      }
    };

    const onPointerUp = () => {
      const drag = dragRef.current;
      if (!drag) return;
      dragRef.current = null;
      setSnapGuides({ v: [], h: [] });
      setGapGuides([]);
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
  }, [aspectLinked, canvasPxH, canvasPxW, commitProject, layoutElements, mutateElementsInCurrentLayout, project.locks]);

  const handlePointerDownElement = useCallback(
    (e: React.PointerEvent, id: string) => {
      if (e.button === 2) {
        e.stopPropagation();
        setMoreMenuOpen(false);
        setAdjBg(false);
        if (!selectedIds.includes(id)) {
          setSelectedIds(resolveGroupIds(id));
        }
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

      const selectedSet = new Set(nextSelection);
      let minL = Infinity;
      let maxR = -Infinity;
      let minT = Infinity;
      let maxB = -Infinity;
      let measuredCount = 0;

      const vSet = new Set<number>([4, 50, 96]);
      const hSet = new Set<number>([4, 50, 96]);
      const siblingBoxes: SiblingBoxPct[] = [];

      canvasEl.querySelectorAll('[data-el-id]').forEach((node) => {
        const nid = node.getAttribute('data-el-id');
        if (!nid) return;
        const r = node.getBoundingClientRect();
        const boxPct: SiblingBoxPct = {
          l: ((r.left - cRect.left) / cw) * 100,
          cx: ((r.left + r.width / 2 - cRect.left) / cw) * 100,
          r: ((r.right - cRect.left) / cw) * 100,
          t: ((r.top - cRect.top) / ch) * 100,
          cy: ((r.top + r.height / 2 - cRect.top) / ch) * 100,
          b: ((r.bottom - cRect.top) / ch) * 100,
        };
        if (selectedSet.has(nid)) {
          minL = Math.min(minL, boxPct.l);
          maxR = Math.max(maxR, boxPct.r);
          minT = Math.min(minT, boxPct.t);
          maxB = Math.max(maxB, boxPct.b);
          measuredCount += 1;
        } else {
          vSet.add(boxPct.l);
          vSet.add(boxPct.cx);
          vSet.add(boxPct.r);
          hSet.add(boxPct.t);
          hSet.add(boxPct.cy);
          hSet.add(boxPct.b);
          siblingBoxes.push(boxPct);
        }
      });

      const origBoxPct: SiblingBoxPct | undefined =
        measuredCount > 0
          ? {
              l: minL,
              r: maxR,
              cx: (minL + maxR) / 2,
              t: minT,
              b: maxB,
              cy: (minT + maxB) / 2,
            }
          : undefined;

      dragRef.current = {
        type: 'move',
        startX: e.clientX,
        startY: e.clientY,
        canvasW: cw,
        canvasH: ch,
        ids: nextSelection,
        origins,
        origBoxPct,
        siblingTargets: { v: Array.from(vSet), h: Array.from(hSet) },
        siblingBoxes,
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

  const handleSave = async (applyNow: boolean) => {
    if (saving.current) return;
    saving.current = true;
    const submitted = project;
    const out = buildThemeItem();
    try {
      const result = await (applyNow ? onSaveAndApply(out, out.editorProject!) : onSaveOnly(out, out.editorProject!));
      if (result === false || latestProject.current !== submitted) return;
    try {
      localStorage.removeItem(draftStorageKey);
    } catch {
      // ignore
    }
    setIsDirty(false);
    setDraftSavedAt(null);
    } catch (error) {
      onNotify('保存失败：' + (error as Error).message);
    } finally { saving.current = false; }
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
  const anySelectedLocked = selectedIds.some((id) => Boolean(project.locks[id]));

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
        {/* Left: Sidebar Mode Switcher, Return, Title, Category, Dirty Status */}
        <div className="flex min-w-0 items-center gap-2">
          {onChangeSidebarMode && (
            <div
              role="group"
              aria-label="主导航栏显示方式"
              className="hidden lg:flex h-8 shrink-0 items-center gap-0.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-0.5"
            >
              {(
                [
                  { id: 'hidden', title: '隐藏最左侧导航栏', icon: EyeOff },
                  { id: 'icons', title: '最左侧导航栏仅显示图标', icon: PanelLeft },
                  { id: 'full', title: '展开完整最左侧导航栏', icon: PanelLeftOpen },
                ] as const
              ).map((opt) => {
                const Icon = opt.icon;
                const active = sidebarMode === opt.id;
                return (
                  <button
                    key={opt.id}
                    type="button"
                    onClick={() => onChangeSidebarMode(opt.id)}
                    title={opt.title}
                    aria-label={opt.title}
                    aria-pressed={active}
                    className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-lg transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] ${
                      active
                        ? 'bg-white text-[#5E45F8] shadow-xs'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    <Icon className="h-3.5 w-3.5" />
                  </button>
                );
              })}
            </div>
          )}

          <button
            type="button"
            onClick={() => {
              if (isDirty) setExitConfirmOpen(true);
              else onExitEditor();
            }}
            title="返回创作中心"
            className="flex h-8 shrink-0 items-center gap-1 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8] whitespace-nowrap"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            <span>返回</span>
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
            className="hidden md:block h-8 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2 text-[11.5px] font-semibold text-[#56526B] focus:border-[#5E45F8] focus:outline-none"
          >
            <option value="主题">主题</option>
            <option value="动态时钟">时钟</option>
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
            <span className="hidden sm:inline">设备预览</span>
          </button>

          <button
            type="button"
            onClick={() => handleSave(false)}
            title="仅保存主题到我的主题库"
            className="flex h-8 items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white px-2.5 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8] whitespace-nowrap"
          >
            <Save className="h-3.5 w-3.5" />
            <span>保存</span>
          </button>

          <button
            type="button"
            onClick={() => handleSave(true)}
            title="保存并应用到当前设备"
            className="flex h-8 items-center gap-1 rounded-xl bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-3 text-[12px] font-bold text-white shadow-[0_4px_14px_rgba(94,69,248,0.28)] hover:brightness-105 whitespace-nowrap"
          >
            <Check className="h-3.5 w-3.5" />
            <span>应用</span>
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

      {/* Compact Responsive Secondary Toolbar Strip (<1024px) */}
      <div className="flex lg:hidden shrink-0 items-center justify-between gap-2 overflow-x-auto border-b border-[#E6E4F2] bg-white/88 px-3 py-1.5 backdrop-blur-md studio-scroll">
        <div className="flex items-center gap-1.5">
          <div className="flex items-center rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-0.5">
            <button
              type="button"
              disabled={historyIdx <= 0}
              onClick={handleUndo}
              title="撤销 (Ctrl+Z)"
              aria-label="撤销"
              className="rounded-lg p-1 text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
            >
              <Undo2 className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              disabled={historyIdx >= history.length - 1}
              onClick={handleRedo}
              title="重做 (Ctrl+Y)"
              aria-label="重做"
              className="rounded-lg p-1 text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
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
            aria-label="切换设备画布规格（移动端工具栏）"
            className="rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11px] font-semibold text-[#1A172B] focus:border-[#5E45F8] focus:outline-none"
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
        </div>

        <div className="flex items-center gap-1.5">
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
              className={`rounded-lg p-1 ${
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
              className={`rounded-lg p-1 ${
                showGridRulers ? 'bg-[#EFEBFD] text-[#5E45F8]' : 'text-[#6B6880] hover:bg-white'
              }`}
              aria-label="标尺与网格"
            >
              <Ruler className="h-3.5 w-3.5" />
            </button>
          </div>

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
              className="px-1 font-mono-tabular text-[11px] font-bold text-[#1A172B] hover:text-[#5E45F8] whitespace-nowrap"
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
      </div>

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
            customAssets={customAssets}
            extractedPalette={extractedPalette}
            onClose={() => setLeftCollapsed(true)}
            onSwitchTab={(t) => setActiveRailTab(t)}
            onInsertElements={handleInsertElements}
            onDeleteCustomAsset={handleDeleteCustomAsset}
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
            onSelectLayers={(ids) => {
              setAdjBg(false);
              setSelectedIds(ids);
            }}
            onSelectBgLayer={() => {
              setSelectedIds([]);
              setActiveRailTab('background');
            }}
            onToggleLock={handleToggleLock}
            onToggleHide={handleToggleHide}
            onReorderLayer={handleReorderLayer}
            onReorderElements={handleReorderElements}
            onRenameLayer={(id) => {
              setRenameTargetId(id);
              setRenameInput(project.names[id] || '');
            }}
            onCommitLayerName={handleCommitLayerName}
            onDuplicateSelection={handleDuplicateSelection}
            onDeleteSelection={handleDeleteSelection}
            onToggleBgAdj={() => setAdjBg((v) => !v)}
            onContextMenuLayer={(e, ids) => {
              setMoreMenuOpen(false);
              setAdjBg(false);
              const allAlreadySelected =
                ids.length > 0 && ids.every((id) => selectedIds.includes(id));
              if (!allAlreadySelected) {
                setSelectedIds(ids);
              }
              setContextMenu({ x: e.clientX, y: e.clientY, target: 'element' });
            }}
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
              setMoreMenuOpen(false);
              const elNode = (e.target as HTMLElement | null)?.closest(
                '[data-el-id]'
              ) as HTMLElement | null;
              const elId = elNode?.getAttribute('data-el-id');
              if (elId) {
                setAdjBg(false);
                if (!selectedIds.includes(elId)) {
                  setSelectedIds(resolveGroupIds(elId));
                }
                setContextMenu({ x: e.clientX, y: e.clientY, target: 'element' });
                return;
              }
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

                {/* Equal-Spacing Gap Indicators */}
                {gapGuides.map((g, idx) => {
                  const len = Math.max(0.2, g.to - g.from);
                  return g.axis === 'h' ? (
                    <div
                      key={`gh-${idx}`}
                      className="pointer-events-none absolute flex items-center justify-center"
                      style={{
                        left: `${g.from}%`,
                        width: `${len}%`,
                        top: `${g.crossPos}%`,
                        transform: 'translateY(-50%)',
                      }}
                    >
                      <div className="relative h-px w-full bg-[#EC4899] shadow-[0_0_4px_#EC4899]">
                        <span className="absolute -top-1 left-0 h-2.5 w-px bg-[#EC4899]" />
                        <span className="absolute -top-1 right-0 h-2.5 w-px bg-[#EC4899]" />
                      </div>
                      <span className="absolute -top-4 rounded bg-[#EC4899] px-1 py-0 font-mono-tabular text-[9px] font-bold text-white shadow-xs whitespace-nowrap">
                        {g.pxGap}px
                      </span>
                    </div>
                  ) : (
                    <div
                      key={`gv-${idx}`}
                      className="pointer-events-none absolute flex items-center justify-center"
                      style={{
                        top: `${g.from}%`,
                        height: `${len}%`,
                        left: `${g.crossPos}%`,
                        transform: 'translateX(-50%)',
                      }}
                    >
                      <div className="relative h-full w-px bg-[#EC4899] shadow-[0_0_4px_#EC4899]">
                        <span className="absolute -left-1 top-0 h-px w-2.5 bg-[#EC4899]" />
                        <span className="absolute -left-1 bottom-0 h-px w-2.5 bg-[#EC4899]" />
                      </div>
                      <span className="absolute left-2 rounded bg-[#EC4899] px-1 py-0 font-mono-tabular text-[9px] font-bold text-white shadow-xs whitespace-nowrap">
                        {g.pxGap}px
                      </span>
                    </div>
                  );
                })}
              </div>

              {/* Unrotated Floating Quick Actions Toolbar (Auto-flips below when near top edge) */}
              {selBox && !editingTextId && (
                <div
                  className="pointer-events-auto absolute z-30 flex -translate-x-1/2 items-center gap-0.5 rounded-xl border border-[#E6E4F2] bg-white/95 px-1.5 py-0.5 shadow-[0_8px_24px_rgba(24,10,68,0.16)] backdrop-blur-md"
                  style={{
                    left: `${selBox.cx}px`,
                    top: selBox.aabbTop < 48 ? `${selBox.aabbBottom + 10}px` : `${selBox.aabbTop - 38}px`,
                  }}
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
                        title={alignScope === 'selection' ? '左对齐（对齐选区）' : '左对齐（对齐画布）'}
                        className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        <AlignStartVertical className="h-3.5 w-3.5" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleAlignSelection('hc')}
                        title={alignScope === 'selection' ? '水平居中（对齐选区）' : '水平居中（对齐画布）'}
                        className="rounded-lg p-1 text-[#56526B] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                      >
                        <AlignCenterVertical className="h-3.5 w-3.5" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleAlignSelection('r')}
                        title={alignScope === 'selection' ? '右对齐（对齐选区）' : '右对齐（对齐画布）'}
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
                    {singleSelectedLocked || anySelectedLocked ? (
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
              )}

              {/* Selection Transform Overlay (Follows element rotation when single-selected) */}
              {selBox && !editingTextId && (
                <div
                  className="pointer-events-none absolute"
                  style={{
                    left: `${selBox.left}px`,
                    top: `${selBox.top}px`,
                    width: `${selBox.width}px`,
                    height: `${selBox.height}px`,
                    transform: selBox.rot ? `rotate(${selBox.rot}deg)` : undefined,
                    transformOrigin: 'center center',
                    outline:
                      singleSelectedLocked || anySelectedLocked
                        ? '1.5px dashed #F59E0B'
                        : '1.5px solid #5E45F8',
                  }}
                >
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
                  )}

                  {/* Multi-Selection Proportional Corner Resize & Group Rotate Handles */}
                  {selectedIds.length > 1 && !anySelectedLocked && (
                    <>
                      {(
                        [
                          { h: 'tl' as const, cls: 'z-10 -left-1.5 -top-1.5 cursor-nwse-resize' },
                          { h: 'tr' as const, cls: 'z-10 -right-1.5 -top-1.5 cursor-nesw-resize' },
                          { h: 'bl' as const, cls: 'z-10 -left-1.5 -bottom-1.5 cursor-nesw-resize' },
                          { h: 'br' as const, cls: 'z-10 -right-1.5 -bottom-1.5 cursor-nwse-resize' },
                        ] as const
                      ).map((corner) => (
                        <div
                          key={corner.h}
                          title="拖拽四角等比缩放整个选区"
                          onPointerDown={(e) => {
                            e.stopPropagation();
                            const cRect = canvasRef.current?.getBoundingClientRect();
                            if (!cRect) return;
                            const cw = Math.max(1, cRect.width);
                            const ch = Math.max(1, cRect.height);
                            const anchorPxX = corner.h.includes('l')
                              ? selBox.left + selBox.width
                              : selBox.left;
                            const anchorPxY = corner.h.includes('t')
                              ? selBox.top + selBox.height
                              : selBox.top;
                            dragRef.current = {
                              type: 'multi-resize',
                              handle: corner.h,
                              startX: e.clientX,
                              startY: e.clientY,
                              canvasW: cw,
                              canvasH: ch,
                              origBox: selBox,
                              anchorPctX: (anchorPxX / cw) * 100,
                              anchorPctY: (anchorPxY / ch) * 100,
                              items: selectedElements.map((el) => ({ origEl: structuredClone(el) })),
                              moved: false,
                            };
                          }}
                          className={`pointer-events-auto absolute h-2.5 w-2.5 rounded-xs border border-[#5E45F8] bg-white shadow ${corner.cls}`}
                        />
                      ))}

                      <div
                        onPointerDown={(e) => {
                          e.stopPropagation();
                          const canvasEl = canvasRef.current;
                          const cRect = canvasEl?.getBoundingClientRect();
                          if (!canvasEl || !cRect) return;
                          const cw = Math.max(1, cRect.width);
                          const ch = Math.max(1, cRect.height);
                          const cx = cRect.left + selBox.cx;
                          const cy = cRect.top + selBox.cy;
                          const items = selectedElements.map((el) => {
                            const node = canvasEl.querySelector(`[data-el-id="${el.id}"]`) as HTMLElement | null;
                            const r = node?.getBoundingClientRect();
                            const elCxPx = r ? r.left + r.width / 2 - cRect.left : selBox.cx;
                            const elCyPx = r ? r.top + r.height / 2 - cRect.top : selBox.cy;
                            return { origEl: structuredClone(el), elCxPx, elCyPx };
                          });
                          dragRef.current = {
                            type: 'multi-rotate',
                            cx,
                            cy,
                            selLocalCx: selBox.cx,
                            selLocalCy: selBox.cy,
                            canvasW: cw,
                            canvasH: ch,
                            startAngle: (Math.atan2(e.clientY - cy, e.clientX - cx) * 180) / Math.PI,
                            items,
                            moved: false,
                          };
                        }}
                        title="拖拽绕选区中心整体旋转 (Shift 每 15° 吸附)"
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
                <button
                  type="button"
                  onClick={() => handleSmartReflowLayout(layoutIndex)}
                  title="基于主布局自动按横/竖屏比例重排所有元素"
                  className="flex items-center gap-1 rounded-lg border border-[#D9D2FF] bg-[#F5F2FF] px-2 py-1 text-[11px] font-semibold text-[#5E45F8] hover:bg-[#5E45F8] hover:text-white"
                >
                  <Wand2 className="h-3 w-3" />
                  <span>智能重排</span>
                </button>
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
          extractedPalette={extractedPalette}
          alignScope={alignScope}
          hasStyleClipboard={Boolean(styleClipboard)}
          onChangeAlignScope={setAlignScope}
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
          onBatchColor={handleBatchColor}
          onBatchAlpha={handleBatchAlpha}
          onBatchFont={handleBatchFont}
          onBatchFontSize={handleBatchFontSize}
          onBatchRadius={handleBatchRadius}
          onCopyStyle={handleCopyStyle}
          onPasteStyle={handlePasteStyle}
          onSaveAsAsset={handleOpenSaveAssetModal}
          onReplaceBgImage={() => setLibraryPickerMode('bg')}
          onUploadBgLocal={() => {
            fileUploadModeRef.current = 'bg';
            fileUploadRef.current?.click();
          }}
          onReplaceImageForSelection={() => {
            if (singleSelected?.kind === 'video') {
              fileUploadModeRef.current = 'replace-el';
              fileUploadRef.current?.click();
            } else {
              setLibraryPickerMode('replace-el');
            }
          }}
          onReplaceImageLocal={() => {
            fileUploadModeRef.current = 'replace-el';
            fileUploadRef.current?.click();
          }}
          onFlipSelection={handleFlipSelection}
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

      {/* ==================== CONTEXT MENU ==================== */}
      {contextMenu && (
        <EditorContextMenu
          menu={contextMenu}
          project={project}
          layoutIndex={layoutIndex}
          layoutElements={layoutElements}
          selectedIds={selectedIds}
          selectedElements={selectedElements}
          clipboardCount={clipboard.length}
          hasStyleClipboard={Boolean(styleClipboard)}
          playMotion={playMotion}
          showSafeZone={showSafeZone}
          showGridRulers={showGridRulers}
          onClose={() => setContextMenu(null)}
          onCopy={handleCopyElements}
          onPaste={handlePasteElements}
          onDuplicate={handleDuplicateSelection}
          onDelete={handleDeleteSelection}
          onReorderLayer={(op) => handleReorderLayer(op)}
          onAlignSelection={handleAlignSelection}
          onDistributeSelection={handleDistributeSelection}
          onToggleLock={() => handleToggleLock()}
          onToggleHide={() => handleToggleHide()}
          onGroupSelection={handleGroupSelection}
          onUngroupSelection={handleUngroupSelection}
          onBulkScale={handleBulkScale}
          onBulkRotate={handleBulkRotate}
          onResetRotate={() => {
            const idSet = new Set(selectedIds);
            mutateElementsInCurrentLayout((els) =>
              els.map((el) =>
                idSet.has(el.id) && !project.locks[el.id] ? { ...el, rot: 0 } : el
              )
            );
            onNotify('已重置旋转角度 (0°)');
          }}
          onFlipSelection={handleFlipSelection}
          onCopyStyle={handleCopyStyle}
          onPasteStyle={handlePasteStyle}
          onSaveAsAsset={handleOpenSaveAssetModal}
          onSelectSameKind={() => {
            if (!singleSelected) return;
            const sameKindIds = layoutElements
              .filter(
                (el) =>
                  el.kind === singleSelected.kind &&
                  !isElementHidden(project, layoutIndex, el)
              )
              .map((el) => el.id);
            setSelectedIds(sameKindIds);
            onNotify(`已选中全部 ${sameKindIds.length} 个同类图层`);
          }}
          onRenameLayer={(id) => {
            setRenameTargetId(id);
            setRenameInput(project.names[id] || '');
          }}
          onEditText={(id) => {
            setEditingTextId(id);
          }}
          onMutateSingle={(fn, msg) => {
            if (!singleSelected) return;
            mutateElementsInCurrentLayout((els) =>
              els.map((el) => (el.id === singleSelected.id ? fn(el) : el))
            );
            if (msg) onNotify(msg);
          }}
          onReplaceImageLocal={() => {
            fileUploadModeRef.current = 'replace-el';
            fileUploadRef.current?.click();
          }}
          onReplaceImageLibrary={() => {
            setLibraryPickerMode('replace-el');
          }}
          onApplyMediaNaturalSize={(el) => {
            void handleApplyMediaSizeMode(el, 'natural');
          }}
          onApplyMediaFit100={(el) => {
            void handleApplyMediaSizeMode(el, 'fit100');
          }}
          onApplyMotionFullBleed={() => {
            if (!singleSelected) return;
            mutateElementsInCurrentLayout((els) =>
              els.map((el) =>
                el.id === singleSelected.id
                  ? { ...el, a: 'mc', x: 0, y: 0, w: 100, h: 100 }
                  : el
              )
            );
            onNotify('已设为 100% 铺满画布并居中');
          }}
          onTogglePlayMotion={() => setPlayMotion((v) => !v)}
          onResetWidgetOrPluginDefaultSize={handleResetWidgetOrPluginDefaultSize}
          onSelectAllVisible={() => {
            setSelectedIds(
              layoutElements
                .filter((el) => !isElementHidden(project, layoutIndex, el))
                .map((el) => el.id)
            );
          }}
          onQuickAdd={handleQuickAddKind}
          onTriggerLocalUploadEl={() => {
            fileUploadModeRef.current = 'el';
            fileUploadRef.current?.click();
          }}
          onEnterBgAdj={() => {
            setSelectedIds([]);
            setAdjBg(true);
          }}
          onToggleSafeZone={() => setShowSafeZone((v) => !v)}
          onToggleGridRulers={() => setShowGridRulers((v) => !v)}
          onSwitchLayout={(idx) => {
            setLayoutIndex(idx);
            setSelectedIds([]);
            setAdjBg(false);
          }}
          onToggleLayoutFollow={(idx) => {
            commitProject((draft) => {
              const lay = draft.layouts[idx];
              if (lay) {
                lay.follow = !lay.follow;
                if (!lay.follow && !lay.elements) {
                  lay.elements = cloneProj(draft).master;
                }
              }
              return draft;
            });
            onNotify(
              project.layouts[idx]?.follow
                ? '已切换为独立定制布局'
                : '已恢复跟随主布局'
            );
          }}
          onSmartReflowLayout={handleSmartReflowLayout}
          onDeleteLayout={(idx) => {
            if (idx <= 0 || project.layouts.length <= 1) return;
            commitProject((draft) => {
              draft.layouts.splice(idx, 1);
              return draft;
            });
            setLayoutIndex((prev) => (prev === idx ? 0 : prev > idx ? prev - 1 : prev));
            setSelectedIds([]);
            onNotify('已删除该规格画布');
          }}
        />
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

      {/* Save As My Asset Modal */}
      {saveAssetModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/50 p-4 backdrop-blur-xs">
          <div className="w-full max-w-sm rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-2xl backdrop-blur-xl">
            <h3 className="text-[15px] font-bold text-[#1A172B]">存为我的素材预设</h3>
            <p className="mt-1 text-[11.5px] text-[#6B6880]">
              将当前选中的 {selectedElements.length} 个图层居中归一化，保存至「素材 → 我的」以便跨主题复用。
            </p>
            <input
              type="text"
              autoFocus
              value={saveAssetName}
              onChange={(e) => setSaveAssetName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') handleConfirmSaveAsset();
                if (e.key === 'Escape') setSaveAssetModalOpen(false);
              }}
              placeholder="输入素材预设名称"
              className="mt-3 w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 text-[13px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
            />
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setSaveAssetModalOpen(false)}
                className="rounded-xl border border-[#E6E4F2] px-3 py-1.5 text-[12px] text-[#6B6880]"
              >
                取消
              </button>
              <button
                type="button"
                onClick={handleConfirmSaveAsset}
                className="rounded-xl bg-[#5E45F8] px-4 py-1.5 text-[12px] font-bold text-white hover:bg-[#4D34E6]"
              >
                保存到我的素材
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

