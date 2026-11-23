import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Activity,
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  Camera,
  ChevronRight,
  Clock,
  Cloud,
  Copy,
  Download,
  Edit3,
  Eye,
  EyeOff,
  FolderOpen,
  Image as ImageIcon,
  Layers,
  Lock,
  Monitor,
  Move,
  Palette,
  Plus,
  Puzzle,
  RefreshCw,
  Search,
  Shapes,
  Smartphone,
  Sparkles,
  Star,
  Trash2,
  Type,
  Unlock,
  Upload,
  Video,
  WifiOff,
  X,
} from 'lucide-react';
import { ThemeItem } from '../../data/themes';
import {
  AssetCategoryFilter,
  BUILTIN_ASSET_PRESETS,
  BUILTIN_BG_STYLES,
  BUILTIN_TEMPLATES,
  BUILTIN_WALLPAPERS,
  BuiltinTemplateSpec,
  MOTION_PRESETS,
  PLUGIN_CAPABILITY_REGISTRY,
  TEXT_STYLE_PRESETS,
  WIDGET_CATALOG,
  computeImportMediaBox,
  makeElement,
  readImageDimensions,
} from '../../data/editorCatalog';
import { cloudResourceAdapter } from '../../services/cloudResourceAdapter';
import {
  BgConfig,
  CLIENT_ENGINE_VERSION,
  CloudCatalogItem,
  CloudCategoryNode,
  CloudDownloadState,
  EditorElement,
  EditorRailTab,
  EditorThemeProject,
} from '../../types/editor';
import { EditorArtworkRenderer, getLayoutElements, isElementHidden } from './EditorArtworkRenderer';

const ASSET_CATS: AssetCategoryFilter[] = [
  '全部',
  '收藏',
  '最近',
  '数字',
  '表盘',
  '日期',
  '组合',
  '文字',
  '装饰',
];
const TINTS = ['', '#ffffff', '#ffd166', '#38bdf8', '#5E45F8', '#ff7a59', '#34d399', '#1A172B'];
const ASSET_STORE_KEY = 'd92.editor.assets.v2';

function loadAssetPrefs(): { fav: string[]; rec: string[] } {
  try {
    const o = JSON.parse(localStorage.getItem(ASSET_STORE_KEY) || '{}');
    return {
      fav: Array.isArray(o.fav) ? o.fav : [],
      rec: Array.isArray(o.rec) ? o.rec : [],
    };
  } catch {
    return { fav: [], rec: [] };
  }
}

interface TemplatePreviewTarget {
  title: string;
  subtitle: string;
  author: string;
  version?: string;
  minClientVersion?: string;
  capabilities?: string[];
  tags?: string[];
  bg: BgConfig;
  landscapeEls: EditorElement[];
  portraitEls: EditorElement[];
  applyLabel: string;
  disabled?: boolean;
  onApply: () => void;
}

interface EditorLeftPanelProps {
  activeTab: EditorRailTab;
  project: EditorThemeProject;
  layoutIndex: number;
  selectedIds: string[];
  themes: ThemeItem[];
  recentImports: { id: string; name: string; kind: 'image' | 'video'; src: string; w: number; h: number }[];
  onClose: () => void;
  onSwitchTab: (tab: EditorRailTab) => void;
  onInsertElements: (els: EditorElement[], label?: string) => void;
  onApplyTemplateProject: (nextProject: EditorThemeProject, asCopy: boolean, msg: string) => void;
  onMutateBg: (fn: (bg: BgConfig) => BgConfig, label?: string) => void;
  onTriggerLocalUpload: (mode: 'el' | 'bg') => void;
  onOpenLibraryPicker: (mode: 'el' | 'bg') => void;
  onStartCapture: (mode: 'shot' | 'rec') => void;
  onSelectLayer: (id: string, multi: boolean) => void;
  onSelectBgLayer: () => void;
  onToggleLock: (ids?: string[]) => void;
  onToggleHide: (ids?: string[]) => void;
  onReorderLayer: (op: 'top' | 'up' | 'down' | 'bottom', targetId?: string) => void;
  onRenameLayer: (id: string) => void;
  onDuplicateSelection: () => void;
  onDeleteSelection: () => void;
  onToggleBgAdj: () => void;
  onNotify: (msg: string) => void;
}

export const EditorLeftPanel: React.FC<EditorLeftPanelProps> = ({
  activeTab,
  project,
  layoutIndex,
  selectedIds,
  themes,
  recentImports,
  onClose,
  onSwitchTab,
  onInsertElements,
  onApplyTemplateProject,
  onMutateBg,
  onTriggerLocalUpload,
  onOpenLibraryPicker,
  onStartCapture,
  onSelectLayer,
  onSelectBgLayer,
  onToggleLock,
  onToggleHide,
  onReorderLayer,
  onRenameLayer,
  onDuplicateSelection,
  onDeleteSelection,
  onToggleBgAdj,
  onNotify,
}) => {
  const layoutSize = project.layouts[layoutIndex]?.size ?? [1920, 462];
  const isPortrait = layoutSize[0] < layoutSize[1];
  const aspectRatio = `${layoutSize[0]} / ${layoutSize[1]}`;

  // Templates state
  const [tplScope, setTplScope] = useState<'featured' | 'builtin' | 'cloud' | 'mine'>('featured');
  const [tplQuery, setTplQuery] = useState('');
  const [tplCat, setTplCat] = useState('all');
  const [tplSubCat, setTplSubCat] = useState('all');
  const [tplSort, setTplSort] = useState<'popular' | 'newest'>('popular');
  const [tplCategories, setTplCategories] = useState<CloudCategoryNode[]>([]);
  const [cloudTemplates, setCloudTemplates] = useState<CloudCatalogItem[]>([]);
  const [cloudTotal, setCloudTotal] = useState(0);
  const [cloudLimit, setCloudLimit] = useState(16);
  const [cloudOffline, setCloudOffline] = useState(false);
  const [downloadStates, setDownloadStates] = useState<Record<string, CloudDownloadState>>({});
  const [cloudTick, setCloudTick] = useState(0);
  const [previewTarget, setPreviewTarget] = useState<TemplatePreviewTarget | null>(null);

  // Assets state
  const [assetSource, setAssetSource] = useState<'builtin' | 'cloud'>('builtin');
  const [assetCat, setAssetCat] = useState<AssetCategoryFilter>('全部');
  const [assetQuery, setAssetQuery] = useState('');
  const [assetTint, setAssetTint] = useState('');
  const [assetFavs, setAssetFavs] = useState<string[]>(() => loadAssetPrefs().fav);
  const [assetRecents, setAssetRecents] = useState<string[]>(() => loadAssetPrefs().rec);
  const [cloudAssets, setCloudAssets] = useState<CloudCatalogItem[]>([]);

  // Images tab state
  const [imgSource, setImgSource] = useState<'builtin' | 'library' | 'local'>('builtin');

  // Time tab state
  const [timeUse24, setTimeUse24] = useState(true);
  const [timeShowSec, setTimeShowSec] = useState(false);

  // Widgets & Plugins search
  const [widgetQuery, setWidgetQuery] = useState('');
  const [pluginQuery, setPluginQuery] = useState('');

  useEffect(() => {
    void cloudResourceAdapter.getCategories('template').then(setTplCategories);
  }, []);

  const refreshCloudTemplates = useCallback(async () => {
    setCloudOffline(cloudResourceAdapter.isOfflineSimulated());
    const res = await cloudResourceAdapter.queryCatalog({
      kind: 'template',
      search: tplQuery,
      categoryId: tplCat,
      subCategoryId: tplSubCat,
      sort: tplScope === 'featured' ? 'featured' : tplSort,
      limit: cloudLimit,
    });
    setCloudTemplates(res.items);
    setCloudTotal(res.total);
  }, [tplQuery, tplCat, tplSubCat, tplScope, tplSort, cloudLimit]);

  useEffect(() => {
    if (activeTab === 'templates') {
      void refreshCloudTemplates();
    }
  }, [activeTab, refreshCloudTemplates, cloudTick]);

  useEffect(() => {
    if (activeTab === 'assets' && assetSource === 'cloud') {
      void cloudResourceAdapter
        .queryCatalog({ kind: 'asset', search: assetQuery, limit: 36 })
        .then((res) => setCloudAssets(res.items));
    }
  }, [activeTab, assetSource, assetQuery]);

  const saveAssetPrefs = (fav: string[], rec: string[]) => {
    try {
      localStorage.setItem(ASSET_STORE_KEY, JSON.stringify({ fav, rec }));
    } catch {
      // ignore
    }
  };

  const handleToggleAssetFav = (name: string) => {
    const exists = assetFavs.includes(name);
    const nextFav = exists ? assetFavs.filter((f) => f !== name) : [...assetFavs, name];
    setAssetFavs(nextFav);
    saveAssetPrefs(nextFav, assetRecents);
    onNotify(exists ? `已取消收藏素材「${name}」` : `已收藏素材「${name}」`);
  };

  const handleInsertBuiltinAsset = (presetId: string) => {
    const preset = BUILTIN_ASSET_PRESETS.find((a) => a.id === presetId);
    if (!preset) return;
    let els = preset.build(isPortrait);
    if (assetTint) {
      els = els.map((e) => ({ ...e, color: assetTint }));
    }
    const nextRec = [preset.name, ...assetRecents.filter((x) => x !== preset.name)].slice(0, 8);
    setAssetRecents(nextRec);
    saveAssetPrefs(assetFavs, nextRec);
    onInsertElements(els, `素材「${preset.name}」`);
  };

  const handleApplyBuiltinTemplate = (tpl: BuiltinTemplateSpec, asCopy: boolean) => {
    const next: EditorThemeProject = {
      ...structuredClone(project),
      name: asCopy ? `${tpl.name} · 副本` : tpl.name,
      category: tpl.category,
      category_id: tpl.subCategory,
      tags: [...tpl.tags],
      bg: {
        kind: 'style',
        style: 'ink',
        dim: 0,
        blur: 0,
        fit: 'cover',
        ...tpl.bg,
      },
      master: tpl.buildElements(isPortrait),
      locks: {},
      names: {},
      layouts: project.layouts.map((l, idx) =>
        idx === 0 ? { ...l, follow: false } : { size: l.size, label: l.label, follow: true }
      ),
      sourceOrigin: 'builtin',
      updated_at: Date.now(),
    };
    onApplyTemplateProject(next, asCopy, `已应用内置模板「${tpl.name}」`);
  };

  const handleDownloadAndApplyCloudTemplate = async (item: CloudCatalogItem, asCopy: boolean) => {
    try {
      const downloaded = await cloudResourceAdapter.downloadPackage(item.id, (st) => {
        setDownloadStates((prev) => ({ ...prev, [item.id]: st }));
      });
      const proj = downloaded as EditorThemeProject;
      const merged: EditorThemeProject = {
        ...proj,
        template_id: asCopy ? `custom-${Date.now().toString(36)}` : project.template_id,
        layouts: project.layouts.map((l, idx) =>
          idx === 0 ? { ...l, follow: false } : { size: l.size, label: l.label, follow: true }
        ),
        updated_at: Date.now(),
      };
      onApplyTemplateProject(
        merged,
        asCopy,
        `已按需下载并应用云端模板「${item.name}」(v${item.version}) 的独立副本`
      );
      setCloudTick((v) => v + 1);
    } catch (err) {
      const message = err instanceof Error ? err.message : '云端模板下载失败';
      onNotify(message);
    }
  };

  const activeCategoryObj = tplCategories.find((c) => c.id === tplCat);

  const filteredBuiltinTemplates = useMemo(() => {
    const q = tplQuery.trim().toLowerCase();
    return BUILTIN_TEMPLATES.filter(
      (t) =>
        !q ||
        t.name.toLowerCase().includes(q) ||
        t.description.toLowerCase().includes(q) ||
        t.tags.some((tg) => tg.toLowerCase().includes(q))
    );
  }, [tplQuery]);

  const myCustomThemes = useMemo(
    () =>
      themes.filter(
        (t) =>
          (t.source === 'custom' || Boolean(t.editorProject)) &&
          (!tplQuery.trim() || t.displayName.toLowerCase().includes(tplQuery.trim().toLowerCase()))
      ),
    [themes, tplQuery]
  );

  const filteredBuiltinAssets = useMemo(() => {
    const q = assetQuery.trim().toLowerCase();
    let list = BUILTIN_ASSET_PRESETS;
    if (assetCat === '收藏') {
      list = list.filter((a) => assetFavs.includes(a.name));
    } else if (assetCat === '最近') {
      list = assetRecents
        .map((n) => list.find((a) => a.name === n))
        .filter((x): x is NonNullable<typeof x> => Boolean(x));
    } else if (assetCat !== '全部') {
      list = list.filter((a) => a.category === assetCat);
    }
    if (q) {
      list = list.filter((a) =>
        `${a.name} ${a.category} ${a.keywords.join(' ')}`.toLowerCase().includes(q)
      );
    }
    return list;
  }, [assetCat, assetQuery, assetFavs, assetRecents]);

  const currentElements = getLayoutElements(project, layoutIndex);

  const renderElementPreviewThumb = (
    els: EditorElement[],
    bg: BgConfig = { kind: 'style', style: 'ink', dim: 0, blur: 0, fit: 'cover' },
    targetSize: [number, number] = layoutSize
  ) => {
    const dummy: EditorThemeProject = {
      ...project,
      bg,
      master: els,
      layouts: [{ size: targetSize, label: '', follow: false }],
    };
    return (
      <EditorArtworkRenderer
        project={dummy}
        layoutIndex={0}
        liveTick={false}
        playMotion={false}
      />
    );
  };

  const tabTitles: Record<EditorRailTab, string> = {
    add: '快捷添加',
    templates: '模板库',
    assets: '素材中心',
    text: '文字样式',
    images: '图片资源',
    time: '时间日期模块',
    motion: '动态特效',
    widgets: '内置组件',
    plugins: '插件能力',
    background: '画布背景',
    layers: '图层结构',
  };

  return (
    <aside
      aria-label="二级资源面板"
      className="flex h-full w-[300px] shrink-0 flex-col border-r border-[#E6E4F2] bg-white/95 text-[#1A172B] backdrop-blur-xl"
    >
      {/* Header */}
      <div className="flex h-11 shrink-0 items-center justify-between border-b border-[#E6E4F2] px-3.5">
        <span className="text-[13px] font-bold text-[#1A172B]">{tabTitles[activeTab]}</span>
        <button
          type="button"
          onClick={onClose}
          title="折叠面板"
          aria-label="折叠面板"
          className="rounded-lg p-1 text-[#6B6880] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      {/* Scrollable Body */}
      <div className="flex-1 overflow-y-auto p-3 studio-scroll">
        {/* 1. ADD PANEL */}
        {activeTab === 'add' && (
          <div className="space-y-4">
            <div>
              <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                导入图片与视频
              </div>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => onTriggerLocalUpload('el')}
                  className="flex flex-col items-center gap-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-3 text-[12px] font-semibold text-[#1A172B] transition-all hover:border-[#5E45F8] hover:bg-[#EFEBFD]/55"
                >
                  <Upload className="h-4 w-4 text-[#5E45F8]" />
                  <span>本地图片/视频</span>
                </button>
                <button
                  type="button"
                  onClick={() => onOpenLibraryPicker('el')}
                  className="flex flex-col items-center gap-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-3 text-[12px] font-semibold text-[#1A172B] transition-all hover:border-[#5E45F8] hover:bg-[#EFEBFD]/55"
                >
                  <FolderOpen className="h-4 w-4 text-[#5E45F8]" />
                  <span>从资源库添加</span>
                </button>
                <button
                  type="button"
                  onClick={() => onStartCapture('shot')}
                  className="flex flex-col items-center gap-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-3 text-[12px] font-semibold text-[#1A172B] transition-all hover:border-[#5E45F8] hover:bg-[#EFEBFD]/55"
                >
                  <Camera className="h-4 w-4 text-[#5E45F8]" />
                  <span>区域截图</span>
                </button>
                <button
                  type="button"
                  onClick={() => onStartCapture('rec')}
                  className="flex flex-col items-center gap-1.5 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-3 text-[12px] font-semibold text-[#1A172B] transition-all hover:border-[#5E45F8] hover:bg-[#EFEBFD]/55"
                >
                  <Video className="h-4 w-4 text-[#5E45F8]" />
                  <span>区域录屏</span>
                </button>
              </div>
            </div>

            <div>
              <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                快速插入元素
              </div>
              <div className="grid grid-cols-3 gap-2">
                <button
                  type="button"
                  onClick={() =>
                    onInsertElements([makeElement('clock', { size: isPortrait ? 28 : 38 })], '数字时钟')
                  }
                  className="flex flex-col items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white py-2.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Clock className="h-4 w-4" />
                  <span>数字时钟</span>
                </button>
                <button
                  type="button"
                  onClick={() => onInsertElements([makeElement('date', {})], '日期模块')}
                  className="flex flex-col items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white py-2.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Clock className="h-4 w-4" />
                  <span>动态日期</span>
                </button>
                <button
                  type="button"
                  onClick={() =>
                    onInsertElements(
                      [makeElement('text', { text: '主标题文本', size: 14, weight: 700 })],
                      '主标题'
                    )
                  }
                  className="flex flex-col items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white py-2.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Type className="h-4 w-4" />
                  <span>标题文字</span>
                </button>
                <button
                  type="button"
                  onClick={() =>
                    onInsertElements(
                      [makeElement('shape', { sh: 'rect', w: isPortrait ? 60 : 26, h: isPortrait ? 12 : 44, radius: 4, alpha: 0.85 })],
                      '矩形块'
                    )
                  }
                  className="flex flex-col items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white py-2.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Shapes className="h-4 w-4" />
                  <span>矩形块</span>
                </button>
                <button
                  type="button"
                  onClick={() =>
                    onInsertElements(
                      [
                        makeElement('shape', {
                          sh: 'circle',
                          w: isPortrait ? 48 : 14,
                          h: isPortrait ? 11.5 : 58,
                          radius: 50,
                          alpha: 0.9,
                        }),
                      ],
                      '实心圆'
                    )
                  }
                  className="flex flex-col items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white py-2.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Shapes className="h-4 w-4" />
                  <span>实心圆形</span>
                </button>
                <button
                  type="button"
                  onClick={() =>
                    onInsertElements(
                      [
                        makeElement('shape', {
                          sh: 'ring',
                          w: isPortrait ? 52 : 16,
                          h: isPortrait ? 12.5 : 66,
                          bw: 1,
                        }),
                      ],
                      '圆环'
                    )
                  }
                  className="flex flex-col items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white py-2.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Shapes className="h-4 w-4" />
                  <span>空心圆环</span>
                </button>
                <button
                  type="button"
                  onClick={() =>
                    onInsertElements(
                      [
                        makeElement('shape', {
                          sh: 'rect',
                          w: isPortrait ? 72 : 42,
                          h: isPortrait ? 0.6 : 1.2,
                          radius: 50,
                          alpha: 0.85,
                        }),
                      ],
                      '细分割条'
                    )
                  }
                  className="flex flex-col items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white py-2.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Shapes className="h-4 w-4" />
                  <span>细分割条</span>
                </button>
                <button
                  type="button"
                  onClick={() => onSwitchTab('assets')}
                  className="flex flex-col items-center gap-1 rounded-xl border border-dashed border-[#DAD6F0] bg-[#EFEBFD]/45 py-2.5 text-[11.5px] font-semibold text-[#5E45F8] hover:bg-[#EFEBFD]"
                >
                  <ChevronRight className="h-4 w-4" />
                  <span>更多素材</span>
                </button>
              </div>
            </div>

            {recentImports.length > 0 && (
              <div>
                <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                  最近添加 ({recentImports.length})
                </div>
                <div className="grid grid-cols-2 gap-2">
                  {recentImports.map((imp) => (
                    <button
                      key={imp.id}
                      type="button"
                      onClick={() => {
                        const box = computeImportMediaBox(imp.w, imp.h, layoutSize[0], layoutSize[1]);
                        if (imp.kind === 'image') {
                          onInsertElements(
                            [
                              makeElement('image', {
                                src: imp.src,
                                w: box.w,
                                h: box.h,
                                ar: box.ar,
                                size: box.size,
                                fit: box.fit,
                                sourceMeta: { origin: 'local', naturalWidth: imp.w, naturalHeight: imp.h, name: imp.name },
                              }),
                            ],
                            imp.name
                          );
                        } else {
                          onInsertElements(
                            [
                              makeElement('video', {
                                src: imp.src,
                                w: box.w,
                                h: box.h,
                                ar: box.ar,
                                size: box.size,
                                fit: box.fit,
                                sourceMeta: { origin: 'local', naturalWidth: imp.w, naturalHeight: imp.h, name: imp.name },
                              }),
                            ],
                            imp.name
                          );
                        }
                      }}
                      className="overflow-hidden rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-1 text-left hover:border-[#5E45F8]"
                    >
                      {imp.kind === 'image' ? (
                        <img src={imp.src} alt={imp.name} className="h-14 w-full rounded-lg object-cover" />
                      ) : (
                        <div className="flex h-14 w-full items-center justify-center rounded-lg bg-[#120733] text-[11px] font-semibold text-white">
                          ▶ 视频
                        </div>
                      )}
                      <div className="mt-1 truncate text-[10.5px] font-semibold">{imp.name}</div>
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        {/* 2. TEMPLATES PANEL */}
        {activeTab === 'templates' && (
          <div className="space-y-3">
            {/* 4 Required Template Sections */}
            <div className="grid grid-cols-4 rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] p-1 text-[11.5px] font-semibold">
              {(
                [
                  { id: 'featured', label: '推荐' },
                  { id: 'builtin', label: '内置' },
                  { id: 'cloud', label: '云端' },
                  { id: 'mine', label: '我的' },
                ] as const
              ).map((tab) => (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setTplScope(tab.id)}
                  className={`rounded-lg py-1 transition-all ${
                    tplScope === tab.id
                      ? 'bg-white text-[#5E45F8] shadow-xs'
                      : 'text-[#6B6880] hover:text-[#1A172B]'
                  }`}
                >
                  {tab.label}
                </button>
              ))}
            </div>

            {/* Search */}
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-2 h-3.5 w-3.5 text-[#6B6880]" />
              <input
                type="text"
                value={tplQuery}
                onChange={(e) => setTplQuery(e.target.value)}
                placeholder="搜索模板名称、标签或作者…"
                className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 pl-8 pr-3 text-[12px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
              />
            </div>

            {(tplScope === 'featured' || tplScope === 'cloud') && (
              <>
                <div className="flex flex-wrap items-center gap-1.5">
                  <select
                    value={tplCat}
                    onChange={(e) => {
                      setTplCat(e.target.value);
                      setTplSubCat('all');
                    }}
                    aria-label="模板一级分类"
                    className="flex-1 rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11.5px] font-medium text-[#1A172B]"
                  >
                    {tplCategories.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                  <select
                    value={tplSort}
                    onChange={(e) => setTplSort(e.target.value as 'popular' | 'newest')}
                    aria-label="模板排序"
                    className="rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-2 py-1 text-[11.5px] font-medium text-[#1A172B]"
                  >
                    <option value="popular">按热度</option>
                    <option value="newest">按更新</option>
                  </select>
                </div>

                {activeCategoryObj?.children && activeCategoryObj.children.length > 0 && (
                  <div className="flex flex-wrap gap-1">
                    <button
                      type="button"
                      onClick={() => setTplSubCat('all')}
                      className={`rounded-lg border px-2 py-0.5 text-[11px] font-medium transition-colors ${
                        tplSubCat === 'all'
                          ? 'border-[#D9D2FF] bg-[#EFEBFD] text-[#5E45F8]'
                          : 'border-[#E6E4F2] text-[#6B6880] hover:border-[#DAD6F0]'
                      }`}
                    >
                      全部子类
                    </button>
                    {activeCategoryObj.children.map((sc) => (
                      <button
                        key={sc.id}
                        type="button"
                        onClick={() => setTplSubCat(sc.id)}
                        className={`rounded-lg border px-2 py-0.5 text-[11px] font-medium transition-colors ${
                          tplSubCat === sc.id
                            ? 'border-[#D9D2FF] bg-[#EFEBFD] text-[#5E45F8]'
                            : 'border-[#E6E4F2] text-[#6B6880] hover:border-[#DAD6F0]'
                        }`}
                      >
                        {sc.label}
                      </button>
                    ))}
                  </div>
                )}

                <div className="flex items-center justify-between rounded-xl bg-[#F5F4FA] px-2.5 py-1.5 text-[10.5px] text-[#6B6880]">
                  <span className="flex items-center gap-1">
                    {cloudOffline ? (
                      <>
                        <WifiOff className="h-3 w-3 text-amber-600" />
                        离线模式
                      </>
                    ) : (
                      <>
                        <Cloud className="h-3 w-3 text-[#5E45F8]" />
                        云端 · <span className="font-mono-tabular">{cloudTotal}</span> 款
                      </>
                    )}
                  </span>
                  <button
                    type="button"
                    onClick={() => setCloudTick((v) => v + 1)}
                    title="刷新云端目录"
                    className="hover:text-[#5E45F8]"
                  >
                    <RefreshCw className="h-3 w-3" />
                  </button>
                </div>

                {cloudTemplates.length === 0 ? (
                  <div className="rounded-xl border border-dashed border-[#DAD6F0] py-8 text-center text-[12px] text-[#6B6880]">
                    {cloudOffline ? '离线状态下暂无匹配已缓存模板' : '没有匹配的云端模板'}
                  </div>
                ) : (
                  <div className="space-y-2.5">
                    {cloudTemplates.map((item) => {
                      const cached = cloudResourceAdapter.getCachedState(item.id);
                      const dl = downloadStates[item.id] ?? cached;
                      const isIncompatible = item.minClientVersion > CLIENT_ENGINE_VERSION;

                      return (
                        <div
                          key={item.id}
                          className="group overflow-hidden rounded-xl border border-[#E6E4F2] bg-white transition-all hover:border-[#5E45F8] hover:shadow-[0_6px_18px_rgba(94,69,248,0.12)]"
                        >
                          <div style={{ aspectRatio }} className="relative w-full overflow-hidden bg-[#120733]">
                            {renderElementPreviewThumb(item.previewElements ?? [], item.previewBg)}
                            <div className="absolute right-1.5 top-1.5 flex items-center gap-1">
                              {dl.status === 'cached' && (
                                <span className="rounded-md bg-emerald-600/90 px-1.5 py-0.5 text-[10px] font-bold text-white">
                                  已缓存 v{dl.cachedVersion || item.version}
                                </span>
                              )}
                              {isIncompatible && (
                                <span className="rounded-md bg-amber-600/95 px-1.5 py-0.5 text-[10px] font-bold text-white">
                                  需 v{item.minClientVersion}
                                </span>
                              )}
                              <button
                                type="button"
                                onClick={() =>
                                  setPreviewTarget({
                                    title: item.name,
                                    subtitle: item.description,
                                    author: item.author,
                                    version: item.version,
                                    minClientVersion: item.minClientVersion,
                                    capabilities: item.requiredCapabilities,
                                    tags: item.tags,
                                    bg: item.previewBg,
                                    landscapeEls: item.previewElements ?? [],
                                    portraitEls: item.previewElements ?? [],
                                    applyLabel: dl.status === 'cached' ? '以独立副本应用' : '按需下载并应用副本',
                                    disabled: isIncompatible,
                                    onApply: () => void handleDownloadAndApplyCloudTemplate(item, true),
                                  })
                                }
                                title="预览多设备效果"
                                aria-label="预览多设备效果"
                                className="flex h-6 w-6 items-center justify-center rounded-md bg-black/55 text-white opacity-0 transition-opacity group-hover:opacity-100 hover:bg-[#5E45F8]"
                              >
                                <Eye className="h-3.5 w-3.5" />
                              </button>
                            </div>
                          </div>
                          <div className="p-2.5">
                            <div className="flex items-start justify-between gap-1.5">
                              <div className="min-w-0">
                                <div className="truncate text-[12px] font-bold text-[#1A172B]">
                                  {item.name}
                                </div>
                                <div className="font-mono-tabular text-[10.5px] text-[#6B6880]">
                                  {item.author} · v{item.version} · {(item.sizeBytes / 1024).toFixed(0)}KB
                                </div>
                              </div>
                            </div>
                            {dl.status === 'downloading' || dl.status === 'verifying' ? (
                              <div className="mt-2">
                                <div className="h-1.5 w-full overflow-hidden rounded-full bg-[#EFEBFD]">
                                  <div
                                    className="h-full bg-[#5E45F8] transition-all"
                                    style={{ width: `${dl.progress}%` }}
                                  />
                                </div>
                                <div className="mt-1 text-[10px] text-[#5E45F8]">
                                  {dl.status === 'verifying' ? '校验完整性 SHA-256…' : `下载中 ${dl.progress}%`}
                                </div>
                              </div>
                            ) : (
                              <div className="mt-2 flex items-center gap-1.5">
                                <button
                                  type="button"
                                  disabled={isIncompatible}
                                  onClick={() => void handleDownloadAndApplyCloudTemplate(item, false)}
                                  className="flex flex-1 items-center justify-center gap-1 rounded-lg bg-[#5E45F8] py-1 text-[11px] font-semibold text-white hover:bg-[#4D34E6] disabled:bg-[#DAD6F0]"
                                >
                                  <Download className="h-3 w-3" />
                                  <span>{dl.status === 'cached' ? '应用模板' : '下载并使用'}</span>
                                </button>
                                <button
                                  type="button"
                                  disabled={isIncompatible}
                                  onClick={() => void handleDownloadAndApplyCloudTemplate(item, true)}
                                  title="创建独立用户副本"
                                  className="rounded-lg border border-[#E6E4F2] px-2 py-1 text-[11px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8] disabled:opacity-40"
                                >
                                  副本
                                </button>
                              </div>
                            )}
                          </div>
                        </div>
                      );
                    })}

                    {cloudTemplates.length < cloudTotal && (
                      <button
                        type="button"
                        onClick={() => setCloudLimit((l) => l + 16)}
                        className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-2 font-mono-tabular text-[11.5px] font-semibold text-[#5E45F8] hover:bg-[#EFEBFD]"
                      >
                        按需加载更多 ({cloudTemplates.length} / {cloudTotal})
                      </button>
                    )}
                  </div>
                )}
              </>
            )}

            {tplScope === 'builtin' && (
              <div className="space-y-2.5">
                {filteredBuiltinTemplates.map((tpl) => {
                  const els = tpl.buildElements(isPortrait);
                  const bg: BgConfig = { kind: 'style', style: 'ink', dim: 0, blur: 0, fit: 'cover', ...tpl.bg };
                  return (
                    <div
                      key={tpl.id}
                      className="group overflow-hidden rounded-xl border border-[#E6E4F2] bg-white transition-all hover:border-[#5E45F8]"
                    >
                      <div style={{ aspectRatio }} className="relative w-full overflow-hidden bg-[#120733]">
                        {renderElementPreviewThumb(els, bg)}
                        <button
                          type="button"
                          onClick={() =>
                            setPreviewTarget({
                              title: tpl.name,
                              subtitle: tpl.description,
                              author: 'D92 官方内置',
                              version: '2.0.0',
                              minClientVersion: '2.0.0',
                              tags: tpl.tags,
                              bg,
                              landscapeEls: tpl.buildElements(false),
                              portraitEls: tpl.buildElements(true),
                              applyLabel: '以独立副本应用到画布',
                              onApply: () => handleApplyBuiltinTemplate(tpl, true),
                            })
                          }
                          title="预览横/竖屏双规格效果"
                          aria-label="预览多设备效果"
                          className="absolute right-1.5 top-1.5 flex h-6 w-6 items-center justify-center rounded-md bg-black/55 text-white opacity-0 transition-opacity group-hover:opacity-100 hover:bg-[#5E45F8]"
                        >
                          <Eye className="h-3.5 w-3.5" />
                        </button>
                      </div>
                      <div className="flex items-center justify-between gap-2 p-2.5">
                        <div className="min-w-0">
                          <div className="truncate text-[12px] font-bold text-[#1A172B]">{tpl.name}</div>
                          <div className="truncate text-[10.5px] text-[#6B6880]">{tpl.description}</div>
                        </div>
                        <button
                          type="button"
                          onClick={() => handleApplyBuiltinTemplate(tpl, false)}
                          className="shrink-0 rounded-lg bg-[#5E45F8] px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-[#4D34E6]"
                        >
                          使用
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            {tplScope === 'mine' && (
              <div className="space-y-2.5">
                {myCustomThemes.length === 0 ? (
                  <div className="rounded-xl border border-dashed border-[#DAD6F0] py-8 text-center text-[12px] text-[#6B6880]">
                    暂未保存自定义主题，创作保存后可在此复用。
                  </div>
                ) : (
                  myCustomThemes.map((t) => (
                    <div
                      key={t.id}
                      className="overflow-hidden rounded-xl border border-[#E6E4F2] bg-white hover:border-[#5E45F8]"
                    >
                      {t.editorProject ? (
                        <div style={{ aspectRatio }} className="w-full overflow-hidden">
                          <EditorArtworkRenderer project={t.editorProject} layoutIndex={0} liveTick={false} playMotion={false} />
                        </div>
                      ) : (
                        t.imageUrl && (
                          <div style={{ aspectRatio }} className="w-full overflow-hidden">
                            <img src={t.imageUrl} alt={t.displayName} className="h-full w-full object-cover" />
                          </div>
                        )
                      )}
                      <div className="flex items-center justify-between gap-2 p-2.5">
                        <div className="min-w-0">
                          <div className="truncate text-[12px] font-bold text-[#1A172B]">{t.displayName}</div>
                          <div className="text-[10.5px] text-[#6B6880]">{t.category}</div>
                        </div>
                        {t.editorProject && (
                          <button
                            type="button"
                            onClick={() =>
                              onApplyTemplateProject(
                                {
                                  ...structuredClone(t.editorProject!),
                                  template_id: `custom-${Date.now().toString(36)}`,
                                  name: `${t.displayName} · 副本`,
                                },
                                true,
                                `已基于「${t.displayName}」创建新副本`
                              )
                            }
                            className="shrink-0 rounded-lg bg-[#5E45F8] px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-[#4D34E6]"
                          >
                            创建副本
                          </button>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </div>
            )}
          </div>
        )}

        {/* 3. ASSETS PANEL */}
        {activeTab === 'assets' && (
          <div className="space-y-3">
            <div className="grid grid-cols-2 rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] p-1 text-[11.5px] font-semibold">
              <button
                type="button"
                onClick={() => setAssetSource('builtin')}
                className={`rounded-lg py-1 transition-all ${
                  assetSource === 'builtin' ? 'bg-white text-[#5E45F8] shadow-xs' : 'text-[#6B6880]'
                }`}
              >
                内置素材 ({BUILTIN_ASSET_PRESETS.length})
              </button>
              <button
                type="button"
                onClick={() => setAssetSource('cloud')}
                className={`rounded-lg py-1 transition-all ${
                  assetSource === 'cloud' ? 'bg-white text-[#5E45F8] shadow-xs' : 'text-[#6B6880]'
                }`}
              >
                云端素材库
              </button>
            </div>

            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-2 h-3.5 w-3.5 text-[#6B6880]" />
              <input
                type="text"
                value={assetQuery}
                onChange={(e) => setAssetQuery(e.target.value)}
                placeholder="搜索形状、表盘、线条、装饰…"
                className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 pl-8 pr-3 text-[12px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
              />
            </div>

            {/* Color tint row */}
            <div className="flex items-center gap-1.5">
              <span className="text-[11px] font-semibold text-[#6B6880]">配色:</span>
              {TINTS.map((c) => (
                <button
                  key={c || 'orig'}
                  type="button"
                  onClick={() => setAssetTint(c)}
                  title={c ? `着色 ${c}` : '原色'}
                  className={`flex h-5 w-5 items-center justify-center rounded-full border text-[9px] font-bold ${
                    assetTint === c ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/30' : 'border-[#DAD6F0]'
                  }`}
                  style={{ backgroundColor: c || '#fff' }}
                >
                  {!c && '原'}
                </button>
              ))}
            </div>

            {assetSource === 'builtin' ? (
              <>
                <div className="flex flex-wrap gap-1">
                  {ASSET_CATS.map((cat) => (
                    <button
                      key={cat}
                      type="button"
                      onClick={() => setAssetCat(cat)}
                      className={`rounded-lg border px-2 py-0.5 text-[11px] font-medium transition-colors ${
                        assetCat === cat
                          ? 'border-[#D9D2FF] bg-[#EFEBFD] text-[#5E45F8]'
                          : 'border-[#E6E4F2] text-[#6B6880] hover:border-[#DAD6F0]'
                      }`}
                    >
                      {cat}
                      {cat === '收藏' && assetFavs.length > 0 ? ` ${assetFavs.length}` : ''}
                      {cat === '最近' && assetRecents.length > 0 ? ` ${assetRecents.length}` : ''}
                    </button>
                  ))}
                </div>

                {filteredBuiltinAssets.length === 0 ? (
                  <div className="rounded-xl border border-dashed border-[#DAD6F0] py-8 text-center text-[11.5px] text-[#6B6880]">
                    {assetCat === '收藏' ? '暂无收藏素材，点击星标即可收藏' : '没有匹配的素材'}
                  </div>
                ) : (
                  <div className={`grid gap-2 ${isPortrait ? 'grid-cols-2' : 'grid-cols-1'}`}>
                    {filteredBuiltinAssets.map((ap) => {
                      let previewEls = ap.build(isPortrait);
                      if (assetTint) previewEls = previewEls.map((e) => ({ ...e, color: assetTint }));
                      const isFav = assetFavs.includes(ap.name);
                      const isDarkText = assetTint === '#1A172B';
                      return (
                        <div
                          key={ap.id}
                          className="group relative overflow-hidden rounded-xl border border-[#E6E4F2] bg-white hover:border-[#5E45F8]"
                        >
                          <button
                            type="button"
                            onClick={() => handleInsertBuiltinAsset(ap.id)}
                            className="block w-full text-left"
                          >
                            <div style={{ aspectRatio }} className="w-full overflow-hidden">
                              {renderElementPreviewThumb(previewEls, {
                                kind: 'style',
                                style: isDarkText ? 'paper' : 'ink',
                                dim: 0,
                                blur: 0,
                                fit: 'cover',
                              })}
                            </div>
                            <div className="flex items-center justify-between px-2.5 py-1.5">
                              <span className="truncate text-[11.5px] font-semibold text-[#1A172B]">
                                {ap.name}
                              </span>
                              <span className="text-[10px] font-semibold text-[#5E45F8]">+ 插入</span>
                            </div>
                          </button>
                          <button
                            type="button"
                            onClick={() => handleToggleAssetFav(ap.name)}
                            aria-label={isFav ? '取消收藏' : '收藏'}
                            className={`absolute right-1.5 top-1.5 flex h-6 w-6 items-center justify-center rounded-md bg-black/55 ${
                              isFav ? 'text-amber-400' : 'text-white/80 hover:text-amber-300'
                            }`}
                          >
                            <Star className="h-3.5 w-3.5" fill={isFav ? 'currentColor' : 'none'} />
                          </button>
                        </div>
                      );
                    })}
                  </div>
                )}
              </>
            ) : (
              <div className="space-y-2">
                {cloudAssets.map((item) => (
                  <div
                    key={item.id}
                    className="overflow-hidden rounded-xl border border-[#E6E4F2] bg-white hover:border-[#5E45F8]"
                  >
                    <div style={{ aspectRatio }} className="w-full overflow-hidden">
                      {renderElementPreviewThumb(item.previewElements ?? [], item.previewBg)}
                    </div>
                    <div className="flex items-center justify-between gap-2 p-2.5">
                      <div className="min-w-0">
                        <div className="truncate text-[11.5px] font-bold text-[#1A172B]">{item.name}</div>
                        <div className="text-[10px] text-[#6B6880]">{item.tags.slice(0, 2).join(' · ')}</div>
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          if (item.previewElements?.length) {
                            const cloned = structuredClone(item.previewElements).map((el) => ({
                              ...el,
                              id: makeElement('text').id,
                              color: assetTint || el.color,
                            }));
                            onInsertElements(cloned, item.name);
                          }
                        }}
                        className="shrink-0 rounded-lg bg-[#5E45F8] px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-[#4D34E6]"
                      >
                        下载插入
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* 4. TEXT PANEL */}
        {activeTab === 'text' && (
          <div className="space-y-2">
            {TEXT_STYLE_PRESETS.map((preset) => (
                <button
                  key={preset.id}
                  type="button"
                  onClick={() => onInsertElements([makeElement('text', preset.overrides)], preset.label)}
                  className="flex w-full items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2.5 text-left transition-all hover:border-[#5E45F8] hover:bg-white"
                >
                  <div className="min-w-0">
                    <div className="text-[12.5px] font-bold text-[#1A172B]">{preset.label}</div>
                    <div className="text-[11px] text-[#6B6880]">{preset.desc}</div>
                  </div>
                  <Plus className="h-4 w-4 shrink-0 text-[#5E45F8]" />
                </button>
              ))}
          </div>
        )}

        {/* 5. IMAGES PANEL */}
        {activeTab === 'images' && (
          <div className="space-y-3">
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => onTriggerLocalUpload('el')}
                className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-[#5E45F8] py-2 text-[12px] font-semibold text-white hover:bg-[#4D34E6]"
              >
                <Upload className="h-3.5 w-3.5" />
                <span>上传本地图片</span>
              </button>
              <button
                type="button"
                onClick={() => onOpenLibraryPicker('el')}
                className="flex items-center justify-center gap-1 rounded-xl border border-[#E6E4F2] px-3 py-2 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
              >
                <FolderOpen className="h-3.5 w-3.5" />
                <span>资源库</span>
              </button>
            </div>

            <div className="grid grid-cols-3 rounded-xl border border-[#E6E4F2] bg-[#F5F4FA] p-1 text-[11px] font-semibold">
              {[
                { id: 'builtin', label: '内置壁纸' },
                { id: 'library', label: '我的图库' },
                { id: 'local', label: '本地导入' },
              ].map((s) => (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => setImgSource(s.id as 'builtin' | 'library' | 'local')}
                  className={`rounded-lg py-1 transition-all ${
                    imgSource === s.id ? 'bg-white text-[#5E45F8] shadow-xs' : 'text-[#6B6880]'
                  }`}
                >
                  {s.label}
                </button>
              ))}
            </div>

            {imgSource === 'builtin' && (
              <div className="grid grid-cols-2 gap-2">
                {BUILTIN_WALLPAPERS.map((wp) => (
                  <div key={wp.id} className="overflow-hidden rounded-xl border border-[#E6E4F2] bg-white">
                    <img src={wp.url} alt={wp.name} className="h-16 w-full object-cover" />
                    <div className="p-1.5">
                      <div className="truncate text-[11px] font-bold text-[#1A172B]">{wp.name}</div>
                      <div className="mt-1 flex gap-1">
                        <button
                          type="button"
                          onClick={async () => {
                            const dims = await readImageDimensions(wp.url);
                            const box = computeImportMediaBox(dims.w, dims.h, layoutSize[0], layoutSize[1]);
                            onInsertElements(
                              [
                                makeElement('image', {
                                  src: wp.url,
                                  w: box.w,
                                  h: box.h,
                                  ar: box.ar,
                                  size: box.size,
                                  fit: box.fit,
                                  sourceMeta: {
                                    origin: 'library',
                                    name: wp.name,
                                    naturalWidth: dims.w,
                                    naturalHeight: dims.h,
                                  },
                                }),
                              ],
                              wp.name
                            );
                          }}
                          className="flex-1 rounded-md bg-[#F5F4FA] py-0.5 text-[10.5px] font-semibold text-[#56526B] hover:bg-[#5E45F8] hover:text-white"
                        >
                          插为图层
                        </button>
                        <button
                          type="button"
                          onClick={() =>
                            onMutateBg((b) => ({ ...b, kind: 'image', url: wp.url, fit: 'cover' }), `背景「${wp.name}」`)
                          }
                          className="flex-1 rounded-md bg-[#F5F4FA] py-0.5 text-[10.5px] font-semibold text-[#56526B] hover:bg-[#5E45F8] hover:text-white"
                        >
                          设为背景
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {imgSource === 'library' && (
              <div className="grid grid-cols-2 gap-2">
                {themes
                  .filter((t) => t.imageUrl)
                  .map((t) => (
                    <div key={t.id} className="overflow-hidden rounded-xl border border-[#E6E4F2] bg-white">
                      <img src={t.imageUrl} alt={t.displayName} className="h-16 w-full object-cover" />
                      <div className="p-1.5">
                        <div className="truncate text-[11px] font-bold text-[#1A172B]">{t.displayName}</div>
                        <button
                          type="button"
                          onClick={async () => {
                            const dims = await readImageDimensions(t.imageUrl!);
                            const box = computeImportMediaBox(dims.w, dims.h, layoutSize[0], layoutSize[1]);
                            onInsertElements(
                              [
                                makeElement('image', {
                                  src: t.imageUrl!,
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
                                }),
                              ],
                              t.displayName
                            );
                          }}
                          className="mt-1 w-full rounded-md bg-[#5E45F8] py-0.5 text-[10.5px] font-semibold text-white hover:bg-[#4D34E6]"
                        >
                          添加图片图层
                        </button>
                      </div>
                    </div>
                  ))}
              </div>
            )}

            {imgSource === 'local' && (
              <div className="rounded-xl border border-dashed border-[#DAD6F0] bg-[#F8F7FC] p-5 text-center text-[11.5px] text-[#6B6880]">
                <ImageIcon className="mx-auto mb-2 h-6 w-6 text-[#5E45F8]" />
                <div>点击上传或拖拽图片至画布</div>
              </div>
            )}
          </div>
        )}

        {/* 6. TIME PANEL (Split Time Modules + Clock Presets) */}
        {activeTab === 'time' && (
          <div className="space-y-4">

            <div className="flex items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 text-[11.5px]">
              <label className="flex cursor-pointer items-center gap-1.5 font-medium text-[#1A172B]">
                <input
                  type="checkbox"
                  className="accent-[#5E45F8]"
                  checked={timeUse24}
                  onChange={(e) => setTimeUse24(e.target.checked)}
                />
                24 小时制
              </label>
              <label className="flex cursor-pointer items-center gap-1.5 font-medium text-[#1A172B]">
                <input
                  type="checkbox"
                  className="accent-[#5E45F8]"
                  checked={timeShowSec}
                  onChange={(e) => setTimeShowSec(e.target.checked)}
                />
                显示秒钟
              </label>
            </div>

            <div>
              <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                独立拆分时间元素
              </div>
              <div className="grid grid-cols-2 gap-2">
                {[
                  { label: '小时 (HH)', build: () => makeElement('clock', { part: 'hour', h24: timeUse24, size: 34 }) },
                  { label: '分钟 (MM)', build: () => makeElement('clock', { part: 'minute', h24: timeUse24, size: 34 }) },
                  { label: '秒钟 (SS)', build: () => makeElement('clock', { part: 'second', h24: timeUse24, size: 22 }) },
                  { label: '时分组合', build: () => makeElement('clock', { part: 'hm', h24: timeUse24, sec: false, size: 36 }) },
                  { label: '时分秒组合', build: () => makeElement('clock', { part: 'hms', h24: timeUse24, sec: true, size: 32 }) },
                  { label: '年月日', build: () => makeElement('date', { part: 'ymd', fmt: 2, size: 10 }) },
                  { label: '星期', build: () => makeElement('date', { part: 'weekday', fmt: 0, size: 9 }) },
                  { label: 'AM / PM', build: () => makeElement('date', { part: 'ampm', size: 9 }) },
                ].map((item) => (
                  <button
                    key={item.label}
                    type="button"
                    onClick={() => onInsertElements([item.build()], item.label)}
                    className="flex items-center justify-between rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-2.5 py-2 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                  >
                    <span>{item.label}</span>
                    <Plus className="h-3.5 w-3.5" />
                  </button>
                ))}
              </div>
            </div>

            <div>
              <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                时钟风格预设
              </div>
              <div className="grid grid-cols-2 gap-2">
                {[
                  { label: '电子数字钟', style: 'digital' as const, font: 'tech' },
                  { label: '机械翻页钟', style: 'flip' as const, font: '' },
                  { label: '极简细体钟', style: 'minimal' as const, font: '' },
                  { label: '模拟指针表盘', style: 'analog' as const, font: '' },
                ].map((preset) => (
                  <button
                    key={preset.label}
                    type="button"
                    onClick={() =>
                      onInsertElements(
                        [
                          makeElement('clock', {
                            style: preset.style,
                            font: preset.font,
                            h24: timeUse24,
                            sec: timeShowSec || preset.style === 'analog',
                            size: isPortrait ? 28 : 34,
                          }),
                        ],
                        preset.label
                      )
                    }
                    className="rounded-xl border border-[#E6E4F2] bg-white p-2 text-left hover:border-[#5E45F8]"
                  >
                    <div className="h-12 w-full overflow-hidden rounded-lg">
                      {renderElementPreviewThumb([
                        makeElement('clock', {
                          style: preset.style,
                          font: preset.font,
                          size: 36,
                        }),
                      ])}
                    </div>
                    <div className="mt-1.5 text-[11.5px] font-bold text-[#1A172B]">{preset.label}</div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* 7. MOTION PANEL */}
        {activeTab === 'motion' && (
          <div className="space-y-3">
            <div className="space-y-2">
              {MOTION_PRESETS.map((mo) => (
                <div
                  key={mo.id}
                  className="flex items-center justify-between gap-2 rounded-xl border border-[#E6E4F2] bg-white p-2.5 hover:border-[#5E45F8]"
                >
                  <div className="min-w-0">
                    <div className="flex items-center gap-1.5">
                      <span className="text-[12px] font-bold text-[#1A172B]">{mo.name}</span>
                      <span
                        className={`rounded-md px-1.5 py-0.5 text-[10px] font-semibold ${
                          mo.cost === 'low'
                            ? 'bg-emerald-50 text-emerald-700'
                            : 'bg-amber-50 text-amber-700'
                        }`}
                      >
                        {mo.cost === 'low' ? '低开销' : '中开销'}
                      </span>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() =>
                      onInsertElements(
                        [
                          makeElement('motion', {
                            effect: mo.effect,
                            color: mo.color,
                            secondaryColor: mo.secondaryColor,
                            performanceCost: mo.cost,
                            requiresGpu: mo.requiresGpu,
                            label: mo.name,
                            w: 100,
                            h: 100,
                          }),
                        ],
                        mo.name
                      )
                    }
                    className="shrink-0 rounded-lg bg-[#5E45F8] px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-[#4D34E6]"
                  >
                    添加
                  </button>
                </div>
              ))}
            </div>
            <button
              type="button"
              onClick={() => onTriggerLocalUpload('bg')}
              className="flex w-full items-center justify-center gap-1.5 rounded-xl border border-dashed border-[#5E45F8] bg-[#EFEBFD]/55 py-2 text-[12px] font-semibold text-[#5E45F8] hover:bg-[#EFEBFD]"
            >
              <Video className="h-3.5 w-3.5" />
              <span>导入视频背景</span>
            </button>
          </div>
        )}

        {/* 8. WIDGETS PANEL (Structured Components) */}
        {activeTab === 'widgets' && (
          <div className="space-y-3">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-2 h-3.5 w-3.5 text-[#6B6880]" />
              <input
                type="text"
                value={widgetQuery}
                onChange={(e) => setWidgetQuery(e.target.value)}
                placeholder="搜索进度条、系统监控、天气、频谱…"
                className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 pl-8 pr-3 text-[12px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
              />
            </div>

            <div className="space-y-2">
              {WIDGET_CATALOG.filter(
                (w) =>
                  !widgetQuery.trim() ||
                  `${w.name} ${w.description}`.toLowerCase().includes(widgetQuery.trim().toLowerCase())
              ).map((w) => (
                <div
                  key={w.type}
                  className="rounded-xl border border-[#E6E4F2] bg-white p-2.5 hover:border-[#5E45F8]"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5">
                        <Activity className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span className="text-[12px] font-bold text-[#1A172B]">{w.name}</span>
                        <span className="rounded bg-[#F5F4FA] px-1 py-0.5 font-mono-tabular text-[10px] text-[#6B6880]">
                          v{w.version}
                        </span>
                      </div>
                      <div className="mt-1 text-[10.5px] text-[#6B6880]">{w.description}</div>
                    </div>
                    <button
                      type="button"
                      onClick={() =>
                        onInsertElements(
                          [
                            makeElement('widget', {
                              widgetType: w.type,
                              widgetVersion: w.version,
                              dataSource: w.dataSource,
                              w: isPortrait ? Math.min(88, w.defaultW * 2.2) : w.defaultW,
                              h: isPortrait ? Math.round(w.defaultH * 0.45) : w.defaultH,
                              accentColor: w.defaultAccent,
                              label: w.name,
                            }),
                          ],
                          `组件「${w.name}」`
                        )
                      }
                      className="shrink-0 rounded-lg bg-[#5E45F8] px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-[#4D34E6]"
                    >
                      插入
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* 9. PLUGINS PANEL (Declarative Registry) */}
        {activeTab === 'plugins' && (
          <div className="space-y-3">
            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-2 h-3.5 w-3.5 text-[#6B6880]" />
              <input
                type="text"
                value={pluginQuery}
                onChange={(e) => setPluginQuery(e.target.value)}
                placeholder="搜索音乐、歌词、番茄钟、鱼缸、备忘…"
                className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 pl-8 pr-3 text-[12px] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
              />
            </div>

            <div className="space-y-2">
              {PLUGIN_CAPABILITY_REGISTRY.filter(
                (p) =>
                  !pluginQuery.trim() ||
                  `${p.name} ${p.category} ${p.description}`
                    .toLowerCase()
                    .includes(pluginQuery.trim().toLowerCase())
              ).map((p) => (
                <div
                  key={p.id}
                  className={`rounded-xl border p-2.5 ${
                    p.supportedInRuntime
                      ? 'border-[#E6E4F2] bg-white hover:border-[#5E45F8]'
                      : 'border-amber-200 bg-amber-50/50'
                  }`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-1.5">
                        <Puzzle className="h-3.5 w-3.5 text-[#5E45F8]" />
                        <span className="text-[12px] font-bold text-[#1A172B]">{p.name}</span>
                        <span className="rounded bg-[#F5F4FA] px-1 font-mono-tabular text-[10px] text-[#6B6880]">
                          v{p.version}
                        </span>
                      </div>
                      <div className="mt-1 text-[10.5px] text-[#6B6880]">{p.description}</div>
                      <div className="mt-1 flex flex-wrap gap-1.5 text-[10px] text-[#56526B]">
                        <span>{p.category}</span>
                        <span aria-hidden="true">·</span>
                        <span>{p.performanceTier}</span>
                        {!p.supportedInRuntime && (
                          <span className="flex items-center gap-0.5 font-semibold text-amber-800">
                            <AlertTriangle className="h-3 w-3" />
                            需 v{p.minClientVersion}
                          </span>
                        )}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        if (!p.supportedInRuntime) {
                          onNotify(p.unsupportedReason || '当前客户端引擎版本暂不支持此原生插件能力');
                        }
                        onInsertElements(
                          [
                            makeElement('plugin', {
                              pluginId: p.id,
                              pluginName: p.name,
                              pluginVersion: p.version,
                              capabilityKey: p.key,
                              w: isPortrait ? Math.min(88, p.defaultW * 2.2) : p.defaultW,
                              h: isPortrait ? Math.round(p.defaultH * 0.45) : p.defaultH,
                              accentColor: p.defaultAccent,
                              config: { ...p.defaultConfig },
                            }),
                          ],
                          `插件「${p.name}」`
                        );
                      }}
                      className={`shrink-0 rounded-lg px-2.5 py-1 text-[11px] font-semibold ${
                        p.supportedInRuntime
                          ? 'bg-[#5E45F8] text-white hover:bg-[#4D34E6]'
                          : 'border border-amber-400 bg-amber-100 text-amber-900'
                      }`}
                    >
                      {p.supportedInRuntime ? '插入' : '兼容性占位'}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* 10. BACKGROUND PANEL */}
        {activeTab === 'background' && (
          <div className="space-y-4">
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => onOpenLibraryPicker('bg')}
                className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-[#5E45F8] py-2 text-[12px] font-semibold text-white hover:bg-[#4D34E6]"
              >
                <ImageIcon className="h-3.5 w-3.5" />
                <span>选择背景图/视频</span>
              </button>
              <button
                type="button"
                onClick={onToggleBgAdj}
                className="flex items-center gap-1 rounded-xl border border-[#E6E4F2] px-3 py-2 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
              >
                <Move className="h-3.5 w-3.5" />
                <span>画布调整</span>
              </button>
            </div>

            <div>
              <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                内置材质底色
              </div>
              <div className="grid grid-cols-2 gap-2">
                {BUILTIN_BG_STYLES.map((st) => (
                  <button
                    key={st.id}
                    type="button"
                    onClick={() =>
                      onMutateBg(
                        (b) => ({ ...b, kind: 'style', style: st.id, url: '', vurl: '', hide: false }),
                        `底色「${st.label}」`
                      )
                    }
                    className={`overflow-hidden rounded-xl border text-left transition-all ${
                      project.bg.kind === 'style' && project.bg.style === st.id
                        ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/25'
                        : 'border-[#E6E4F2] hover:border-[#5E45F8]'
                    }`}
                  >
                    <div className="h-10 w-full" style={{ background: st.css }} />
                    <div className="px-2 py-1 text-[11px] font-semibold text-[#1A172B]">{st.label}</div>
                  </button>
                ))}
              </div>
            </div>

            <div>
              <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                纯色与渐变快捷
              </div>
              <div className="flex items-center gap-2">
                <input
                  type="color"
                  value={project.bg.color || '#141026'}
                  onChange={(e) =>
                    onMutateBg((b) => ({ ...b, kind: 'color', color: e.target.value, hide: false }))
                  }
                  aria-label="纯色背景选择器"
                  className="h-8 w-10 cursor-pointer rounded-lg border border-[#E6E4F2]"
                />
                <button
                  type="button"
                  onClick={() =>
                    onMutateBg(
                      (b) => ({
                        ...b,
                        kind: 'gradient',
                        g1: b.g1 || '#180942',
                        g2: b.g2 || '#5E45F8',
                        angle: b.angle ?? 135,
                        hide: false,
                      }),
                      '渐变背景'
                    )
                  }
                  className="flex flex-1 items-center justify-center gap-1.5 rounded-xl border border-[#E6E4F2] py-1.5 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                >
                  <Palette className="h-3.5 w-3.5 text-[#5E45F8]" />
                  <span>切换线性渐变</span>
                </button>
              </div>
            </div>

            <div>
              <div className="mb-2 text-[11px] font-bold tracking-wider text-[#6B6880]">
                官方高清背景壁纸
              </div>
              <div className="grid grid-cols-2 gap-2">
                {BUILTIN_WALLPAPERS.map((wp) => (
                  <button
                    key={wp.id}
                    type="button"
                    onClick={() =>
                      onMutateBg(
                        (b) => ({ ...b, kind: 'image', url: wp.url, vurl: '', fit: 'cover', hide: false }),
                        `背景「${wp.name}」`
                      )
                    }
                    className="overflow-hidden rounded-xl border border-[#E6E4F2] text-left hover:border-[#5E45F8]"
                  >
                    <img src={wp.url} alt={wp.name} className="h-12 w-full object-cover" />
                    <div className="truncate px-1.5 py-1 text-[10.5px] font-semibold text-[#1A172B]">
                      {wp.name}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* 11. LAYERS PANEL */}
        {activeTab === 'layers' && (
          <div className="space-y-3">
            {/* Layer Bulk Quick Tools */}
            <div className="flex items-center justify-between gap-1 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] p-1.5">
              <div className="flex items-center gap-1">
                <button
                  type="button"
                  disabled={selectedIds.length === 0}
                  onClick={() => onReorderLayer('up')}
                  title="上移一层"
                  className="rounded-lg p-1 text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
                >
                  <ArrowUp className="h-3.5 w-3.5" />
                </button>
                <button
                  type="button"
                  disabled={selectedIds.length === 0}
                  onClick={() => onReorderLayer('down')}
                  title="下移一层"
                  className="rounded-lg p-1 text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
                >
                  <ArrowDown className="h-3.5 w-3.5" />
                </button>
                <button
                  type="button"
                  disabled={selectedIds.length === 0}
                  onClick={onDuplicateSelection}
                  title="复制图层 (Ctrl+D)"
                  className="rounded-lg p-1 text-[#56526B] hover:bg-white hover:text-[#5E45F8] disabled:opacity-35"
                >
                  <Copy className="h-3.5 w-3.5" />
                </button>
              </div>
              <button
                type="button"
                disabled={selectedIds.length === 0}
                onClick={onDeleteSelection}
                title="删除选中图层 (Delete)"
                className="rounded-lg p-1 text-red-500 hover:bg-red-50 disabled:opacity-35"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>

            {currentElements.length === 0 ? (
              <div className="rounded-xl border border-dashed border-[#DAD6F0] py-8 text-center text-[11.5px] text-[#6B6880]">
                画布暂无前景图层，可从左侧「添加」或「素材」插入元素。
              </div>
            ) : (
              <div className="space-y-1">
                {[...currentElements].reverse().map((el) => {
                  const isSel = selectedIds.includes(el.id);
                  const locked = Boolean(project.locks[el.id]);
                  const hidden = isElementHidden(project, layoutIndex, el);
                  const customName = project.names[el.id];
                  const defaultLabel =
                    el.kind === 'clock'
                      ? el.style === 'analog'
                        ? '指针表盘'
                        : '数字时钟'
                      : el.kind === 'date'
                        ? '日期模块'
                        : el.kind === 'text'
                          ? el.text.slice(0, 12) || '文字'
                          : el.kind === 'image'
                            ? el.sourceMeta?.name || '图片图层'
                            : el.kind === 'video'
                              ? '视频图层'
                              : el.kind === 'motion'
                                ? el.label || '动态层'
                                : el.kind === 'widget'
                                  ? el.label || '组件'
                                  : el.kind === 'plugin'
                                    ? el.pluginName
                                    : '矢量形状';

                  const TypeIcon =
                    el.kind === 'clock' || el.kind === 'date'
                      ? Clock
                      : el.kind === 'text'
                        ? Type
                        : el.kind === 'image'
                          ? ImageIcon
                          : el.kind === 'video'
                            ? Video
                            : el.kind === 'motion'
                              ? Sparkles
                              : el.kind === 'widget'
                                ? Activity
                                : el.kind === 'plugin'
                                  ? Puzzle
                                  : Shapes;

                  return (
                    <div
                      key={el.id}
                      onClick={(e) => onSelectLayer(el.id, e.shiftKey || e.ctrlKey || e.metaKey)}
                      className={`group flex cursor-pointer items-center gap-2 rounded-xl border px-2.5 py-1.5 text-[12px] transition-colors ${
                        isSel
                          ? 'border-[#5E45F8] bg-[#EFEBFD]/75 text-[#1A172B]'
                          : 'border-transparent hover:border-[#E6E4F2] hover:bg-[#F8F7FC]'
                      } ${hidden ? 'opacity-50' : ''}`}
                    >
                      <TypeIcon className={`h-3.5 w-3.5 shrink-0 ${isSel ? 'text-[#5E45F8]' : 'text-[#6B6880]'}`} />
                      <div className="min-w-0 flex-1">
                        <div className="truncate font-medium">
                          {customName || defaultLabel}
                        </div>
                        {el.g && (
                          <div className="text-[10px] text-[#5E45F8]">组合 · {el.g.slice(-4)}</div>
                        )}
                      </div>
                      <div className="flex items-center gap-0.5 opacity-80 group-hover:opacity-100">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            onRenameLayer(el.id);
                          }}
                          title="重命名"
                          className="rounded p-1 text-[#6B6880] hover:bg-white hover:text-[#1A172B]"
                        >
                          <Edit3 className="h-3 w-3" />
                        </button>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            onToggleHide([el.id]);
                          }}
                          title={hidden ? '显示图层' : '隐藏图层'}
                          className="rounded p-1 text-[#6B6880] hover:bg-white hover:text-[#1A172B]"
                        >
                          {hidden ? <EyeOff className="h-3 w-3" /> : <Eye className="h-3 w-3" />}
                        </button>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            onToggleLock([el.id]);
                          }}
                          title={locked ? '解锁图层' : '锁定图层'}
                          className={`rounded p-1 hover:bg-white ${locked ? 'text-amber-600' : 'text-[#6B6880]'}`}
                        >
                          {locked ? <Lock className="h-3 w-3" /> : <Unlock className="h-3 w-3" />}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            {/* Background Layer Row */}
            <div className="border-t border-[#E6E4F2] pt-2">
              <div
                onClick={onSelectBgLayer}
                className={`flex cursor-pointer items-center gap-2 rounded-xl border px-2.5 py-2 text-[12px] ${
                  selectedIds.length === 0
                    ? 'border-[#5E45F8] bg-[#EFEBFD]/75 font-semibold text-[#1A172B]'
                    : 'border-transparent hover:border-[#E6E4F2] hover:bg-[#F8F7FC]'
                }`}
              >
                <Layers className="h-3.5 w-3.5 text-[#6B6880]" />
                <span className="flex-1 truncate">背景层 ({project.bg.kind})</span>
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    onMutateBg((b) => ({ ...b, hide: !b.hide }));
                  }}
                  title={project.bg.hide ? '显示背景' : '隐藏背景'}
                  className="rounded p-1 text-[#6B6880] hover:bg-white"
                >
                  {project.bg.hide ? <EyeOff className="h-3 w-3" /> : <Eye className="h-3 w-3" />}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Multi-Device Template Preview Modal */}
      {previewTarget && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#120733]/55 p-4 backdrop-blur-xs"
          onClick={() => setPreviewTarget(null)}
        >
          <div
            className="w-full max-w-3xl overflow-hidden rounded-[24px] border border-white/80 bg-white/95 p-5 shadow-[0_24px_60px_rgba(24,10,68,0.28)] backdrop-blur-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-3 border-b border-[#E6E4F2] pb-3.5">
              <div className="min-w-0">
                <h3 className="truncate text-[16px] font-bold text-[#1A172B]">
                  {previewTarget.title}
                </h3>
                <p className="mt-0.5 text-[12px] text-[#6B6880]">
                  {previewTarget.subtitle}
                </p>
                <div className="mt-1.5 flex flex-wrap items-center gap-2 text-[11px] text-[#6B6880]">
                  <span>作者：{previewTarget.author}</span>
                  {previewTarget.version && (
                    <>
                      <span aria-hidden="true">·</span>
                      <span className="font-mono-tabular">v{previewTarget.version}</span>
                    </>
                  )}
                  {previewTarget.minClientVersion && (
                    <>
                      <span aria-hidden="true">·</span>
                      <span className="font-mono-tabular">最低客户端 v{previewTarget.minClientVersion}</span>
                    </>
                  )}
                  {previewTarget.tags && previewTarget.tags.length > 0 && (
                    <>
                      <span aria-hidden="true">·</span>
                      <span>{previewTarget.tags.join(' / ')}</span>
                    </>
                  )}
                </div>
              </div>
              <button
                type="button"
                onClick={() => setPreviewTarget(null)}
                aria-label="关闭模板预览"
                className="rounded-xl p-1.5 text-[#6B6880] hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Dual Aspect Previews (D92 Landscape + D92 Portrait) */}
            <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-[1fr_140px]">
              <div className="aurora-dark-card flex flex-col justify-between rounded-2xl p-4 text-white">
                <div className="mb-2 flex items-center justify-between text-[11px] text-white/75">
                  <span className="flex items-center gap-1.5 font-semibold">
                    <Monitor className="h-3.5 w-3.5" />
                    D92 横屏效果预览
                  </span>
                  <span className="font-mono-tabular">1920 × 462</span>
                </div>
                <div
                  className="w-full overflow-hidden rounded-xl border border-white/15 bg-[#0B061D]"
                  style={{ aspectRatio: '1920 / 462' }}
                >
                  {renderElementPreviewThumb(
                    previewTarget.landscapeEls,
                    previewTarget.bg,
                    [1920, 462]
                  )}
                </div>
              </div>

              <div className="aurora-dark-card flex flex-col items-center rounded-2xl p-3 text-white">
                <div className="mb-2 flex w-full items-center justify-between text-[10.5px] text-white/75">
                  <span className="flex items-center gap-1 font-semibold">
                    <Smartphone className="h-3 w-3" />
                    竖屏
                  </span>
                  <span className="font-mono-tabular">462×1920</span>
                </div>
                <div
                  className="h-[190px] overflow-hidden rounded-xl border border-white/15 bg-[#0B061D]"
                  style={{ aspectRatio: '462 / 1920' }}
                >
                  {renderElementPreviewThumb(
                    previewTarget.portraitEls,
                    previewTarget.bg,
                    [462, 1920]
                  )}
                </div>
              </div>
            </div>

            <div className="mt-4 flex items-center justify-end gap-2 border-t border-[#E6E4F2] pt-3.5">
              <button
                type="button"
                onClick={() => setPreviewTarget(null)}
                className="rounded-xl border border-[#E6E4F2] px-4 py-2 text-[12px] font-semibold text-[#56526B] hover:border-[#DAD6F0]"
              >
                关闭
              </button>
              <button
                type="button"
                disabled={previewTarget.disabled}
                onClick={() => {
                  const fn = previewTarget.onApply;
                  setPreviewTarget(null);
                  fn();
                }}
                className="rounded-xl bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-5 py-2 text-[12px] font-bold text-white shadow-[0_6px_16px_rgba(94,69,248,0.28)] hover:brightness-105 disabled:opacity-45"
              >
                {previewTarget.applyLabel}
              </button>
            </div>
          </div>
        </div>
      )}
    </aside>
  );
};
