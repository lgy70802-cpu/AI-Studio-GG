import {
  BUILTIN_ASSET_PRESETS,
  BUILTIN_WALLPAPERS,
  PLUGIN_CAPABILITY_REGISTRY,
  WIDGET_CATALOG,
  makeElement,
} from '../data/editorCatalog';
import {
  BgConfig,
  CLIENT_ENGINE_VERSION,
  CloudCatalogItem,
  CloudCategoryNode,
  CloudDownloadState,
  CloudQueryParams,
  CloudQueryResult,
  CloudResourceKind,
  EditorElement,
  EditorThemeProject,
  THEME_SCHEMA_VERSION,
} from '../types/editor';

export interface ICloudResourceAdapter {
  readonly adapterName: string;
  readonly isMock: boolean;
  isOfflineSimulated(): boolean;
  setSimulateOffline(offline: boolean): void;
  getCategories(kind: CloudResourceKind): Promise<CloudCategoryNode[]>;
  queryCatalog(params: CloudQueryParams): Promise<CloudQueryResult>;
  queryFeatured(): Promise<CloudCatalogItem[]>;
  getDetail(id: string): Promise<CloudCatalogItem | null>;
  getManifest(id: string): Promise<EditorThemeProject | null>;
  downloadPackage(
    id: string,
    onProgress?: (state: CloudDownloadState) => void
  ): Promise<EditorThemeProject | CloudCatalogItem>;
  getCachedState(id: string): CloudDownloadState;
  getAllCachedIds(): string[];
  checkUpdates(localCloudRefs: { cloudTemplateId: string; cloudVersion: string }[]): Promise<
    { cloudTemplateId: string; latestVersion: string; changelog: string }[]
  >;
  getEntitlement(id: string): Promise<{ allowed: boolean; tier: 'free' | 'pro' | 'vip'; reason?: string }>;
  // Mock WEB Admin Simulation APIs (only active in MockCloudAdapter)
  publishMockTemplate(payload: {
    name: string;
    categoryId: string;
    subCategoryId: string;
    description: string;
    accentHex: string;
    bg: BgConfig;
    elements: EditorElement[];
  }): CloudCatalogItem;
  bumpMockVersion(id: string): CloudCatalogItem | null;
  unpublishMockTemplate(id: string): boolean;
}

const CACHE_STORAGE_KEY = 'd92-cloud-pkg-cache-v2';

const TEMPLATE_CATEGORIES: CloudCategoryNode[] = [
  {
    id: 'all',
    label: '全部分类',
  },
  {
    id: 'clock',
    label: '数字与机械时钟',
    children: [
      { id: 'minimal', label: '极简时钟' },
      { id: 'flip', label: '翻页与拟物' },
      { id: 'cyber', label: '霓虹数字' },
      { id: 'analog', label: '经典表盘' },
    ],
  },
  {
    id: 'sysmon',
    label: '性能与效率面板',
    children: [
      { id: 'telemetry', label: '硬件监控' },
      { id: 'focus', label: '专注番茄' },
      { id: 'dual-tz', label: '多时区协同' },
    ],
  },
  {
    id: 'ambient',
    label: '动态氛围壁纸',
    children: [
      { id: 'particles', label: '粒子与雪景' },
      { id: 'scenic', label: '桌面插画' },
      { id: 'gradient', label: '极光渐变' },
    ],
  },
  {
    id: 'editorial',
    label: '创意排版主题',
    children: [
      { id: 'quote', label: '语录与诗句' },
      { id: 'media', label: '音乐与相册' },
    ],
  },
];

const ASSET_CATEGORIES: CloudCategoryNode[] = [
  { id: 'all', label: '全部素材' },
  {
    id: 'shapes',
    label: '矢量与装饰线',
    children: [
      { id: 'line', label: '分隔线/虚线' },
      { id: 'frame', label: '边框与角标' },
    ],
  },
  {
    id: 'stickers',
    label: '图标与贴纸',
    children: [
      { id: 'badge', label: '科技徽章' },
      { id: 'gif', label: '动态图标' },
    ],
  },
  {
    id: 'clocks',
    label: '云端时钟套件',
    children: [
      { id: 'combo', label: '时钟组合' },
      { id: 'dial', label: '表盘预设' },
    ],
  },
];

const WIDGET_CATEGORIES: CloudCategoryNode[] = [
  { id: 'all', label: '全部组件预设' },
  { id: 'telemetry', label: '硬件与图表' },
  { id: 'productivity', label: '进度与倒计时' },
  { id: 'lifestyle', label: '气象与频谱' },
];

const PLUGIN_CATEGORIES: CloudCategoryNode[] = [
  { id: 'all', label: '全部插件外观' },
  { id: 'media', label: '媒体与歌词' },
  { id: 'efficiency', label: '效率与便签' },
  { id: 'interactive', label: '桌面互动' },
];

const CLOUD_THEMES_PALETTES: {
  title: string;
  cat: string;
  sub: string;
  g1: string;
  g2: string;
  accent: string;
  tags: string[];
  author: string;
  featured?: boolean;
  clockStyle: 'digital' | 'flip' | 'minimal' | 'analog';
  withSysmon?: boolean;
  withMotion?: 'particles' | 'snow' | 'gradient-flow';
  wallpaperIdx?: number;
}[] = [
  {
    title: '深空量子 HUD 监控面板',
    cat: 'sysmon',
    sub: 'telemetry',
    g1: '#091124',
    g2: '#1B3A6B',
    accent: '#38BDF8',
    tags: ['系统监控', 'D92', 'HUD'],
    author: 'Studio Cloud',
    featured: true,
    clockStyle: 'digital',
    withSysmon: true,
    withMotion: 'particles',
  },
  {
    title: '极北冰川雪夜时钟',
    cat: 'ambient',
    sub: 'particles',
    g1: '#0F172A',
    g2: '#1E3A5F',
    accent: '#7DD3FC',
    tags: ['雪景', '动态', 'D92'],
    author: 'Nordic Lab',
    featured: true,
    clockStyle: 'minimal',
    withMotion: 'snow',
    wallpaperIdx: 0,
  },
  {
    title: '原木工坊机械翻页',
    cat: 'clock',
    sub: 'flip',
    g1: '#2D1D14',
    g2: '#543828',
    accent: '#F59E0B',
    tags: ['翻页', '暖色', '桌搭'],
    author: '暖木工坊',
    featured: true,
    clockStyle: 'flip',
    wallpaperIdx: 1,
  },
  {
    title: '绯樱微光桌面伴侣',
    cat: 'ambient',
    sub: 'scenic',
    g1: '#3B162C',
    g2: '#7A2E58',
    accent: '#F472B6',
    tags: ['樱花', '竖屏适配', '治愈'],
    author: '绯樱绘社',
    featured: true,
    clockStyle: 'minimal',
    wallpaperIdx: 2,
  },
  {
    title: '赛博脉冲频谱工作台',
    cat: 'editorial',
    sub: 'media',
    g1: '#100C2A',
    g2: '#2B1F68',
    accent: '#60A5FA',
    tags: ['频谱', '音乐', '动感'],
    author: '声浪实验室',
    featured: true,
    clockStyle: 'digital',
    withMotion: 'gradient-flow',
  },
  {
    title: '日落天际线航班时刻',
    cat: 'sysmon',
    sub: 'dual-tz',
    g1: '#1F1B3A',
    g2: '#734B6D',
    accent: '#FB923C',
    tags: ['双时区', '航线', '商务'],
    author: '云端航迹',
    clockStyle: 'digital',
    wallpaperIdx: 5,
  },
  {
    title: '典雅罗马指针表盘',
    cat: 'clock',
    sub: 'analog',
    g1: '#141B29',
    g2: '#273549',
    accent: '#D4A24C',
    tags: ['表盘', '罗马数字', '商务'],
    author: 'Horology Studio',
    clockStyle: 'analog',
  },
  {
    title: '专注番茄高效工作站',
    cat: 'sysmon',
    sub: 'focus',
    g1: '#0B241E',
    g2: '#144B3F',
    accent: '#10B981',
    tags: ['番茄钟', '高效', '极简'],
    author: 'FocusLab',
    clockStyle: 'minimal',
  },
];

function simpleChecksum(input: string): string {
  let hash = 2166136261;
  for (let i = 0; i < input.length; i += 1) {
    hash ^= input.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return `sha256-mock-${(hash >>> 0).toString(16).padStart(8, '0')}`;
}

function compareSemver(a: string, b: string): number {
  const pa = a.split('.').map(Number);
  const pb = b.split('.').map(Number);
  for (let i = 0; i < 3; i += 1) {
    const diff = (pa[i] || 0) - (pb[i] || 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

function buildGeneratedCatalog(): CloudCatalogItem[] {
  const items: CloudCatalogItem[] = [];
  const baseTime = 1732200000000;

  // 1) Seed 1,080 Cloud Templates (8 signature + 1,072 deterministic variants for scale testing)
  const totalTemplates = 1080;
  for (let i = 0; i < totalTemplates; i += 1) {
    const seed = CLOUD_THEMES_PALETTES[i % CLOUD_THEMES_PALETTES.length];
    const seriesNum = Math.floor(i / CLOUD_THEMES_PALETTES.length) + 1;
    const isPrimary = i < CLOUD_THEMES_PALETTES.length;
    const id = `cloud-tpl-${String(i + 1).padStart(4, '0')}`;
    const name = isPrimary ? seed.title : `${seed.title} · 系列 #${seriesNum}`;
    const bg: BgConfig =
      typeof seed.wallpaperIdx === 'number' && i % 2 === 0
        ? {
            kind: 'image',
            url: BUILTIN_WALLPAPERS[seed.wallpaperIdx].url,
            dim: 22,
            blur: 0,
            fit: 'cover',
          }
        : {
            kind: 'gradient',
            g1: seed.g1,
            g2: seed.g2,
            angle: 120 + ((i * 17) % 180),
            dim: 0,
            blur: 0,
            fit: 'cover',
          };

    const previewElements: EditorElement[] = [
      ...(seed.withMotion
        ? [
            makeElement('motion', {
              id: `${id}-mo`,
              effect: seed.withMotion,
              w: 100,
              h: 100,
              color: seed.accent,
              alpha: 0.45,
              speed: 1,
              intensity: 60,
              performanceCost: 'medium',
            }),
          ]
        : []),
      makeElement('clock', {
        id: `${id}-clk`,
        style: seed.clockStyle,
        a: seed.withSysmon ? 'ml' : 'mc',
        x: seed.withSysmon ? 7 : 0,
        y: seed.withSysmon ? -6 : -4,
        size: seed.withSysmon ? 30 : 34,
        color: '#ffffff',
        accent: seed.accent,
        font: seed.cat === 'cyber' ? 'tech' : seed.sub === 'analog' ? 'serif' : '',
        sec: i % 3 === 0,
      }),
      makeElement('date', {
        id: `${id}-dt`,
        a: seed.withSysmon ? 'ml' : 'mc',
        x: seed.withSysmon ? 7 : 0,
        y: seed.withSysmon ? 20 : 18,
        size: 7.5,
        fmt: (i % 4) as 0 | 1 | 2 | 3,
        color: '#ffffff',
        alpha: 0.82,
      }),
      ...(seed.withSysmon
        ? [
            makeElement('widget', {
              id: `${id}-wg`,
              widgetType: 'sysmon',
              a: 'mr',
              x: -6,
              y: 0,
              w: 32,
              h: 64,
              accentColor: seed.accent,
              bgOpacity: 74,
            }),
          ]
        : []),
    ];

    const version = isPrimary ? '2.1.0' : `1.${(i % 5) + 1}.0`;
    const isFutureClient = i === 11; // 1 sample requiring newer engine to test compatibility warning cleanly!

    items.push({
      id,
      kind: 'template',
      name,
      description: `${seed.author} 出品的云端可编辑主题模板，支持 D92/D69 横竖屏独立布局与二次微调。`,
      categoryId: seed.cat,
      subCategoryId: seed.sub,
      tags: seed.tags,
      version,
      schemaVersion: THEME_SCHEMA_VERSION,
      minClientVersion: isFutureClient ? '3.0.0' : '2.0.0',
      requiredCapabilities: seed.withSysmon ? ['widget.sysmon'] : [],
      author: seed.author,
      entitlement: i % 15 === 14 ? 'pro' : 'free',
      supportedDevices: ['D92', 'D69'],
      supportedResolutions: ['1920x462', '462x1920', '1280x480', '480x1280'],
      downloads: Math.max(120, 24800 - i * 19),
      rating: 4.9 - ((i % 7) * 0.05),
      updatedAt: baseTime - i * 3600000,
      sizeBytes: 184000 + (i % 9) * 32000,
      checksum: simpleChecksum(`${id}:${version}`),
      featured: Boolean(seed.featured && isPrimary),
      accentHex: seed.accent,
      previewBg: bg,
      previewElements,
      compatibilityNote: isFutureClient
        ? `此云端模板需要客户端 v3.0.0 以上引擎（当前 v${CLIENT_ENGINE_VERSION}），已阻止不安全应用`
        : undefined,
    });
  }

  // 2) Seed 120 Cloud Assets (SVG / Stickers / Clock Packs / Wallpapers)
  for (let i = 0; i < 120; i += 1) {
    const basePreset = BUILTIN_ASSET_PRESETS[i % BUILTIN_ASSET_PRESETS.length];
    const id = `cloud-ast-${String(i + 1).padStart(3, '0')}`;
    const tierNum = Math.floor(i / BUILTIN_ASSET_PRESETS.length) + 1;
    const isLine = basePreset.category === '装饰';
    const isDial = basePreset.category === '表盘';
    const colors = ['#38BDF8', '#60A5FA', '#F472B6', '#FBBF24', '#34D399', '#FFFFFF'];
    const tint = colors[i % colors.length];
    const builtEls = basePreset.build(false).map((el, idx) => ({
      ...el,
      id: `${id}-el-${idx}`,
      color: tint,
    }));

    items.push({
      id,
      kind: 'asset',
      name: tierNum === 1 ? `云端扩展 · ${basePreset.name}` : `${basePreset.name} · 配色版 #${tierNum}`,
      description: `独立可编辑矢量/组件素材资产，支持换色、缩放与图层组合。`,
      categoryId: isLine ? 'shapes' : isDial ? 'clocks' : 'stickers',
      subCategoryId: isLine ? 'line' : isDial ? 'dial' : 'badge',
      tags: [...basePreset.keywords, basePreset.category, '云端素材'],
      version: '1.2.0',
      schemaVersion: THEME_SCHEMA_VERSION,
      minClientVersion: '2.0.0',
      requiredCapabilities: [],
      author: 'Studio 素材库',
      entitlement: 'free',
      supportedDevices: ['D92', 'D69'],
      supportedResolutions: ['1920x462', '462x1920', '1280x480'],
      downloads: 6400 - i * 32,
      rating: 4.8,
      updatedAt: baseTime - i * 7200000,
      sizeBytes: 24500 + i * 1200,
      checksum: simpleChecksum(`${id}:1.2.0`),
      accentHex: tint,
      previewBg: { kind: 'style', style: 'ink', dim: 0, blur: 0, fit: 'cover' },
      previewElements: builtEls,
      assetCategory: basePreset.category,
      assetPresetId: basePreset.id,
    });
  }

  // 3) Seed Widget Presets (24 configurable presets using client-supported widget types)
  WIDGET_CATALOG.forEach((w, wIdx) => {
    ['极夜蓝', '翡翠专注', '暖琥珀'].forEach((themeLabel, tIdx) => {
      const accents = ['#2563EB', '#10B981', '#F59E0B'];
      const accent = accents[tIdx];
      const id = `cloud-wgt-${w.type}-${tIdx}`;
      items.push({
        id,
        kind: 'widget_preset',
        name: `${w.name} · ${themeLabel}`,
        description: `${w.description}（基于客户端内置组件协议 v${w.version}）`,
        categoryId:
          w.type === 'sysmon' || w.type === 'mini-chart' || w.type === 'telemetry-bar'
            ? 'telemetry'
            : w.type === 'weather' ||
                w.type === 'weather-capsule' ||
                w.type === 'audio-spectrum' ||
                w.type === 'audio-rhythm'
              ? 'lifestyle'
              : 'productivity',
        subCategoryId: w.type,
        tags: ['组件预设', w.name, themeLabel],
        version: w.version,
        schemaVersion: THEME_SCHEMA_VERSION,
        minClientVersion: '2.0.0',
        requiredCapabilities: [`widget.${w.type}`],
        author: 'Studio 组件团队',
        entitlement: 'free',
        supportedDevices: w.supportedDevices,
        supportedResolutions: ['1920x462', '1280x480'],
        downloads: 5200 - (wIdx * 3 + tIdx) * 180,
        rating: 4.9,
        updatedAt: baseTime - (wIdx * 3 + tIdx) * 86400000,
        sizeBytes: 12400,
        checksum: simpleChecksum(`${id}:${w.version}`),
        accentHex: accent,
        previewBg: { kind: 'style', style: 'night', dim: 0, blur: 0, fit: 'cover' },
        previewElements: [
          makeElement('widget', {
            id: `${id}-el`,
            widgetType: w.type,
            w: 62,
            h: 64,
            accentColor: accent,
            bgOpacity: 82,
            label: w.name,
            value: 74,
            ...(w.defaultProps ?? {}),
          }),
        ],
        widgetType: w.type,
        widgetVersion: w.version,
      });
    });
  });

  // 4) Seed Declarative Plugin Appearance Presets (no arbitrary JS!)
  PLUGIN_CAPABILITY_REGISTRY.forEach((p, idx) => {
    const id = `cloud-plg-${p.key}`;
    items.push({
      id,
      kind: 'plugin_capability',
      name: `${p.name} · 官方外观包`,
      description: `${p.description}。仅下发声明式外观配置与兼容版本检查，不执行远程 JS。`,
      categoryId:
        p.category === '媒体联动'
          ? 'media'
          : p.category === '互动娱乐'
            ? 'interactive'
            : 'efficiency',
      subCategoryId: p.key,
      tags: ['插件能力', p.category, `v${p.version}`],
      version: p.version,
      schemaVersion: THEME_SCHEMA_VERSION,
      minClientVersion: p.minClientVersion,
      requiredCapabilities: p.requiredPermissions,
      author: 'Studio 插件生态',
      entitlement: 'free',
      supportedDevices: p.supportedDevices,
      supportedResolutions: ['1920x462', '462x1920', '1280x480'],
      downloads: 4100 - idx * 210,
      rating: 4.85,
      updatedAt: baseTime - idx * 43200000,
      sizeBytes: 16800,
      checksum: simpleChecksum(`${id}:${p.version}`),
      accentHex: p.defaultAccent,
      previewBg: { kind: 'style', style: 'aurora', dim: 0, blur: 0, fit: 'cover' },
      previewElements: [
        makeElement('plugin', {
          id: `${id}-el`,
          pluginId: p.id,
          pluginName: p.name,
          pluginVersion: p.version,
          capabilityKey: p.key,
          w: 68,
          h: 68,
          accentColor: p.defaultAccent,
          bgOpacity: 82,
          config: { ...p.defaultConfig },
        }),
      ],
      pluginKey: p.key,
      compatibilityNote: p.unsupportedReason,
    });
  });

  return items;
}

class MockCloudAdapter implements ICloudResourceAdapter {
  readonly adapterName = 'MockCloudAdapter (本地模拟云端目录 · 未连接真实 WEB 后台)';
  readonly isMock = true;

  private catalog: CloudCatalogItem[] = buildGeneratedCatalog();
  private offlineSimulated = false;
  private cacheMap: Record<string, { version: string; cachedAt: number; project?: EditorThemeProject }> = {};

  constructor() {
    this.loadCacheFromStorage();
  }

  private loadCacheFromStorage() {
    try {
      const raw = localStorage.getItem(CACHE_STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (parsed && typeof parsed === 'object') {
          this.cacheMap = parsed;
        }
      }
    } catch {
      this.cacheMap = {};
    }
  }

  private persistCacheToStorage() {
    try {
      localStorage.setItem(CACHE_STORAGE_KEY, JSON.stringify(this.cacheMap));
    } catch {
      // Ignore storage quota warnings in browser sandbox
    }
  }

  isOfflineSimulated(): boolean {
    return this.offlineSimulated;
  }

  setSimulateOffline(offline: boolean): void {
    this.offlineSimulated = offline;
  }

  async getCategories(kind: CloudResourceKind): Promise<CloudCategoryNode[]> {
    if (kind === 'template') return TEMPLATE_CATEGORIES;
    if (kind === 'asset') return ASSET_CATEGORIES;
    if (kind === 'widget_preset') return WIDGET_CATEGORIES;
    return PLUGIN_CATEGORIES;
  }

  async queryCatalog(params: CloudQueryParams): Promise<CloudQueryResult> {
    const q = (params.search || '').trim().toLowerCase();
    const limit = params.limit ?? 24;
    const cursor = params.cursor ?? 0;

    // When offline, still allow browsing cached catalog items gracefully
    let list = this.catalog.filter((item) => item.kind === params.kind);

    if (this.offlineSimulated) {
      list = list.filter((item) => Boolean(this.cacheMap[item.id]));
    }

    if (params.categoryId && params.categoryId !== 'all') {
      list = list.filter((item) => item.categoryId === params.categoryId);
    }
    if (params.subCategoryId && params.subCategoryId !== 'all') {
      list = list.filter((item) => item.subCategoryId === params.subCategoryId);
    }
    if (params.tag) {
      const tLower = params.tag.toLowerCase();
      list = list.filter((item) => item.tags.some((t) => t.toLowerCase() === tLower));
    }
    if (params.resolution && params.resolution !== 'all') {
      list = list.filter((item) => item.supportedResolutions.includes(params.resolution!));
    }
    if (q) {
      list = list.filter(
        (item) =>
          item.name.toLowerCase().includes(q) ||
          item.description.toLowerCase().includes(q) ||
          item.author.toLowerCase().includes(q) ||
          item.tags.some((t) => t.toLowerCase().includes(q))
      );
    }

    if (params.sort === 'newest') {
      list = [...list].sort((a, b) => b.updatedAt - a.updatedAt);
    } else if (params.sort === 'featured') {
      list = [...list].sort(
        (a, b) => Number(Boolean(b.featured)) - Number(Boolean(a.featured)) || b.downloads - a.downloads
      );
    } else {
      list = [...list].sort((a, b) => b.downloads - a.downloads);
    }

    const total = list.length;
    const sliced = list.slice(cursor, cursor + limit);
    const nextCursor = cursor + sliced.length < total ? cursor + sliced.length : null;

    return {
      items: sliced,
      total,
      nextCursor,
      fromOfflineCache: this.offlineSimulated,
    };
  }

  async queryFeatured(): Promise<CloudCatalogItem[]> {
    return this.catalog.filter((item) => item.kind === 'template' && item.featured);
  }

  async getDetail(id: string): Promise<CloudCatalogItem | null> {
    return this.catalog.find((item) => item.id === id) ?? null;
  }

  private buildProjectFromCatalogItem(item: CloudCatalogItem): EditorThemeProject {
    if (item.templateProject) {
      return {
        ...structuredClone(item.templateProject),
        template_id: `user-copy-${item.id}-${Date.now().toString(36)}`,
        cloudTemplateId: item.id,
        cloudVersion: item.version,
        sourceOrigin: 'cloud',
        updated_at: Date.now(),
      };
    }

    return {
      template_id: `user-copy-${item.id}-${Date.now().toString(36)}`,
      name: item.name,
      description: item.description,
      category: item.categoryId === 'clock' ? '动态时钟' : '主题',
      category_id: item.subCategoryId || item.categoryId,
      tags: [...item.tags],
      version: '1.0.0',
      schema_version: item.schemaVersion,
      min_client_version: item.minClientVersion,
      required_capabilities: [...item.requiredCapabilities],
      author: item.author,
      entitlement: item.entitlement,
      supported_devices: [...item.supportedDevices],
      supported_resolutions: [...item.supportedResolutions],
      thumbnail_variants: {},
      bg: structuredClone(item.previewBg),
      master: structuredClone(item.previewElements ?? []),
      layouts: [
        { size: [1920, 462], label: 'D92 横屏', follow: false },
        { size: [462, 1920], label: 'D92 竖屏', follow: true },
      ],
      locks: {},
      names: {},
      asset_manifest: [],
      updated_at: Date.now(),
      sourceOrigin: 'cloud',
      cloudTemplateId: item.id,
      cloudVersion: item.version,
    };
  }

  async getManifest(id: string): Promise<EditorThemeProject | null> {
    const item = await this.getDetail(id);
    if (!item) return null;
    return this.buildProjectFromCatalogItem(item);
  }

  getCachedState(id: string): CloudDownloadState {
    const cached = this.cacheMap[id];
    if (!cached) return { status: 'idle', progress: 0 };
    return {
      status: 'cached',
      progress: 100,
      cachedVersion: cached.version,
      cachedAt: cached.cachedAt,
    };
  }

  getAllCachedIds(): string[] {
    return Object.keys(this.cacheMap);
  }

  async downloadPackage(
    id: string,
    onProgress?: (state: CloudDownloadState) => void
  ): Promise<EditorThemeProject | CloudCatalogItem> {
    const item = await this.getDetail(id);
    if (!item) {
      throw new Error('云端资源不存在或已被运营下架（Mock）');
    }

    // Security & Compatibility validation (No unsafe path / incompatible schema / no arbitrary JS)
    if (compareSemver(item.minClientVersion, CLIENT_ENGINE_VERSION) > 0) {
      const msg =
        item.compatibilityNote ||
        `版本不兼容：需客户端 v${item.minClientVersion}，当前为 v${CLIENT_ENGINE_VERSION}`;
      onProgress?.({ status: 'error', progress: 0, errorMsg: msg });
      throw new Error(msg);
    }

    // Check if offline & cached
    if (this.offlineSimulated) {
      const cached = this.cacheMap[id];
      if (cached?.project) {
        onProgress?.({
          status: 'cached',
          progress: 100,
          cachedVersion: cached.version,
          cachedAt: cached.cachedAt,
        });
        return structuredClone(cached.project);
      }
      const errMsg = '当前处于离线模拟模式，且该资源尚未缓存到本地';
      onProgress?.({ status: 'error', progress: 0, errorMsg: errMsg });
      throw new Error(errMsg);
    }

    onProgress?.({ status: 'downloading', progress: 25 });
    await new Promise((r) => setTimeout(r, 120));
    onProgress?.({ status: 'downloading', progress: 68 });
    await new Promise((r) => setTimeout(r, 120));
    onProgress?.({ status: 'verifying', progress: 92 });

    // Integrity check simulation
    if (!item.checksum.startsWith('sha256-')) {
      const msg = '安装包完整性校验失败（Checksum Mismatch）';
      onProgress?.({ status: 'error', progress: 0, errorMsg: msg });
      throw new Error(msg);
    }

    await new Promise((r) => setTimeout(r, 80));
    const project = this.buildProjectFromCatalogItem(item);
    const cachedAt = Date.now();
    this.cacheMap[id] = { version: item.version, cachedAt, project };
    this.persistCacheToStorage();

    onProgress?.({
      status: 'cached',
      progress: 100,
      cachedVersion: item.version,
      cachedAt,
    });

    return item.kind === 'template' ? structuredClone(project) : structuredClone(item);
  }

  async checkUpdates(localCloudRefs: { cloudTemplateId: string; cloudVersion: string }[]) {
    const updates: { cloudTemplateId: string; latestVersion: string; changelog: string }[] = [];
    for (const ref of localCloudRefs) {
      const item = this.catalog.find((c) => c.id === ref.cloudTemplateId);
      if (item && compareSemver(item.version, ref.cloudVersion) > 0) {
        updates.push({
          cloudTemplateId: item.id,
          latestVersion: item.version,
          changelog: `WEB 后台已发布 ${item.name} v${item.version}，您的独立副本保持不变`,
        });
      }
    }
    return updates;
  }

  async getEntitlement(id: string) {
    const item = await this.getDetail(id);
    if (!item) return { allowed: false, tier: 'free' as const, reason: '资源不存在' };
    return { allowed: true, tier: item.entitlement };
  }

  publishMockTemplate(payload: {
    name: string;
    categoryId: string;
    subCategoryId: string;
    description: string;
    accentHex: string;
    bg: BgConfig;
    elements: EditorElement[];
  }): CloudCatalogItem {
    const id = `cloud-web-pub-${Date.now().toString(36)}`;
    const newItem: CloudCatalogItem = {
      id,
      kind: 'template',
      name: payload.name.trim() || 'WEB 后台新发布主题',
      description: payload.description.trim() || '通过 Mock WEB 后台热发布的新模板，无需升级客户端即可拉取。',
      categoryId: payload.categoryId || 'clock',
      subCategoryId: payload.subCategoryId || 'minimal',
      tags: ['WEB热发布', '新品', 'D92'],
      version: '1.0.0',
      schemaVersion: THEME_SCHEMA_VERSION,
      minClientVersion: '2.0.0',
      requiredCapabilities: [],
      author: 'WEB 运营后台 (Mock)',
      entitlement: 'free',
      supportedDevices: ['D92', 'D69'],
      supportedResolutions: ['1920x462', '462x1920', '1280x480', '480x1280'],
      downloads: 1,
      rating: 5.0,
      updatedAt: Date.now(),
      sizeBytes: 142000,
      checksum: simpleChecksum(`${id}:1.0.0`),
      featured: true,
      accentHex: payload.accentHex || '#2563EB',
      previewBg: structuredClone(payload.bg),
      previewElements: structuredClone(payload.elements),
    };
    this.catalog = [newItem, ...this.catalog];
    return newItem;
  }

  bumpMockVersion(id: string): CloudCatalogItem | null {
    const target = this.catalog.find((c) => c.id === id);
    if (!target) return null;
    const parts = target.version.split('.').map(Number);
    parts[1] = (parts[1] || 0) + 1;
    parts[2] = 0;
    target.version = parts.join('.');
    target.updatedAt = Date.now();
    target.checksum = simpleChecksum(`${target.id}:${target.version}`);
    return target;
  }

  unpublishMockTemplate(id: string): boolean {
    const before = this.catalog.length;
    this.catalog = this.catalog.filter((c) => c.id !== id);
    return this.catalog.length < before;
  }
}

export const cloudResourceAdapter: ICloudResourceAdapter = new MockCloudAdapter();
