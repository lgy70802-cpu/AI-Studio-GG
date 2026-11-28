import type { EditorThemeProject } from '../types/editor';

export type NavSection =
  | 'home'
  | 'online'
  | 'library'
  | 'creator'
  | 'playlist'
  | 'schedule'
  | 'plugins'
  | 'devices'
  | 'settings'
  | 'profile';

export type ScreenOrientation = 'landscape' | 'portrait';
export type ContentCategory = '壁纸' | '主题' | '动态时钟' | '视频';

export type ThemeSource = 'official' | 'custom' | 'local';

export interface ThemeItem {
  deletedAt?: number;
  contentHash?: string;
  thumbnailUrl?: string;
  videoUrl?: string;
  durationSeconds?: number;
  sourceWidth?: number;
  sourceHeight?: number;
  desktopStatic?: boolean;
  desktopFlipped?: boolean;
  desktopBrightness?: number;
  id: string;
  name: string;
  displayName: string;
  category: ContentCategory;
  resolution: string;
  orientation: ScreenOrientation;
  imageUrl?: string;
  customArt?: 'wealth-calligraphy' | 'aurora-prism';
  clockStyle?: 'crystal' | 'sakura' | 'flip-wood' | 'pastel-ice' | 'minimal-warm' | 'none';
  author: string;
  source?: ThemeSource;
  inLibrary?: boolean;
  downloads: string;
  downloadCount: number;
  publishedAt: number;
  releasedLabel: string;
  addedAt: number;
  addedLabel: string;
  lastUsedAt: number;
  useCount: number;
  durationMinutes: number;
  inPlaylist: boolean;
  isFavorite: boolean;
  isTrending?: boolean;
  isRecent?: boolean;
  accentHex: string;
  editorProject?: EditorThemeProject;
}

export interface DeviceInfo {
  standby?: boolean;
  renderFps?: number;
  usbFps?: number;
  hardwareBrightness?: boolean;
  hardwarePower?: boolean;
  id: string;
  name: string;
  serial: string;
  model: string;
  status: '已连接' | '待机中' | '离线';
  nativeWidth: number;
  nativeHeight: number;
  resolution: string;
  refreshRate: string;
  brightness: number;
  orientation: ScreenOrientation;
  flipped: boolean;
  sleepMinutes: number;
  firmware: string;
  themeId?: string;
  themeName?: string;
  preview?: string;
  playing?: boolean;
  wantsPlaying?: boolean;
  outputStatus?: string;
  videoPosition?: number;
  videoDuration?: number;
  videoFps?: number;
  playlistId?: string;
  playlistRunning?: boolean;
  playlistRemaining?: number;
  playlistMessage?: string;
}

export type MoodTag = '专注' | '清晨' | '夜晚' | '放松' | '灵感' | '喜爱';

export interface PlaylistItemEntry {
  themeId: string;
  durationSeconds: number;
}

export interface CarouselPlaylist {
  id: string;
  name: string;
  deviceId: string;
  orientation: ScreenOrientation;
  mood: MoodTag;
  shuffle: boolean;
  defaultDurationSeconds: number;
  items: PlaylistItemEntry[];
}

export type ScheduleRepeatMode = 'daily' | 'weekdays' | 'weekends' | 'once' | 'custom';
export type ScheduleActionType = 'playlist' | 'theme' | 'sleep' | 'wake';

export interface ScheduleRule {
  id: string;
  title: string;
  time: string;
  deviceId: string;
  orientation: ScreenOrientation;
  repeatMode: ScheduleRepeatMode;
  customDays?: number[];
  days: string;
  actionType: ScheduleActionType;
  themeId: string;
  playlistId?: string;
  playlistNameSnapshot?: string;
  playlistDeleted?: boolean;
  targetBrightness?: number;
  enabled: boolean;
}

export type PluginCategory = '时间组件' | '系统监控' | '媒体联动' | '效率工具' | '生活资讯' | '互动娱乐';
export type PluginPosition = 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | 'center' | 'full';
export type PluginScale = 'compact' | 'standard' | 'large';

export interface PluginConfig {
  clockStyle?: 'digital' | 'flip';
  position: PluginPosition;
  accentColor: string;
  opacity: number;
  scale: PluginScale;
  showSeconds?: boolean;
  showDate?: boolean;
  use24Hour?: boolean;
  showTemp?: boolean;
  showMemory?: boolean;
  refreshSeconds?: number;
  barCount?: number;
  showTrackName?: boolean;
  focusMinutes?: number;
  breakMinutes?: number;
  city?: string;
  showAqi?: boolean;
  noteText?: string;
  fishCount?: number;
  bubbleSpeed?: number;
  causticsIntensity?: number;
  showAquariumClock?: boolean;
  stockSymbol?: 'NVDA' | 'AAPL' | 'BTC' | 'ETH';
  songTitle?: string;
  artist?: string;
  nixieHue?: 'amber' | 'cyan' | 'green' | 'white';
  petName?: string;
  petSpecies?: 'mochi' | 'pixel-bot' | 'shiba';
  petMood?: 'happy' | 'chill' | 'focus';
  flameIntensity?: 'low' | 'medium' | 'high';
  emberDensity?: 'low' | 'medium' | 'high';
  showSoundHint?: boolean;
  bonsaiSpecies?: 'pine' | 'succulent' | 'sakura';
  growthDays?: number;
  sunlightTone?: 'warm' | 'cool' | 'mist';
  flipShellStyle?: 'obsidian' | 'warmwood' | 'titanium';
  vuBacklight?: 'warm-amber' | 'emerald' | 'ice-blue';
  vuSensitivity?: 'smooth' | 'balanced' | 'punchy';
  deckLabel?: string;
  cassetteShell?: 'smoke' | 'amber' | 'indigo';
  tapeSpeed?: number;
  zodiacSign?: 'aries' | 'taurus' | 'gemini' | 'cancer' | 'leo' | 'virgo' | 'libra' | 'scorpio' | 'sagittarius' | 'capricorn' | 'aquarius' | 'pisces';
  showConstellationGlow?: boolean;
  parcelName?: string;
  carrier?: string;
  parcelStage?: 'collected' | 'transit' | 'delivering' | 'delivered';
  milestoneTitle?: string;
  targetDays?: number;
  countMode?: 'countdown' | 'countup';
  targetDateLabel?: string;
  unitMode?: 'Mbps' | 'MB/s';
  showPingChart?: boolean;
}

export interface PluginItem {
  id: string;
  name: string;
  description: string;
  category: PluginCategory;
  version: string;
  author: string;
  supportedOrientations: ScreenOrientation[];
  installed: boolean;
  enabled: boolean;
  core?: boolean;
  config: PluginConfig;
}

export const INITIAL_DEVICES: DeviceInfo[] = [
  {
    id: 'dev-782b05',
    name: 'D92 实机 · 782B05',
    serial: 'C511D3782B05',
    model: 'D92 UltraWide 副屏',
    status: '已连接',
    nativeWidth: 1920,
    nativeHeight: 462,
    resolution: '1920 × 462',
    refreshRate: '60Hz',
    brightness: 82,
    orientation: 'landscape',
    flipped: false,
    sleepMinutes: 30,
    firmware: 'v2.4.1',
  },
  {
    id: 'dev-904a18',
    name: 'D69 桌面副屏 · 904A18',
    serial: 'C511D3904A18',
    model: 'D69 Studio 副屏',
    status: '待机中',
    nativeWidth: 1280,
    nativeHeight: 480,
    resolution: '1280 × 480',
    refreshRate: '60Hz',
    brightness: 65,
    orientation: 'landscape',
    flipped: false,
    sleepMinutes: 15,
    firmware: 'v2.4.0',
  },
];

export const INITIAL_THEMES: ThemeItem[] = [
  {
    id: 'crystal-ice-palace',
    name: 'D92_clock_03_crystal_ice_palace',
    displayName: 'D92_clock_03_crystal_ice_palace',
    category: '动态时钟',
    resolution: '1920 × 462',
    orientation: 'landscape',
    imageUrl: '/src/assets/images/theme_crystal_ice_palace_1795333384131.jpg',
    clockStyle: 'crystal',
    author: 'Studio 官方',
    downloads: '12.8k',
    downloadCount: 12800,
    publishedAt: 1731542400000,
    releasedLabel: '11-14 上架',
    addedAt: 1731801600000,
    addedLabel: '11-17 添加',
    lastUsedAt: 1732233600000,
    useCount: 42,
    durationMinutes: 30,
    inPlaylist: true,
    isFavorite: true,
    isRecent: true,
    isTrending: true,
    accentHex: '#5E45F8',
  },
  {
    id: 'cozy-bear-exact',
    name: 'D92_2557x615_exact',
    displayName: 'D92_2557x615_exact',
    category: '壁纸',
    resolution: '2557 × 615',
    orientation: 'landscape',
    imageUrl: '/src/assets/images/theme_cozy_bear_desk_1795333385881.jpg',
    clockStyle: 'flip-wood',
    author: '暖木工坊',
    downloads: '8.4k',
    downloadCount: 8400,
    publishedAt: 1731283200000,
    releasedLabel: '11-11 上架',
    addedAt: 1732060800000,
    addedLabel: '11-20 添加',
    lastUsedAt: 1732147200000,
    useCount: 28,
    durationMinutes: 20,
    inPlaylist: true,
    isFavorite: false,
    isRecent: true,
    accentHex: '#B47B48',
  },
  {
    id: 'sakura-rose-gold',
    name: 'D92_clock_01_sakura_rose_gold',
    displayName: 'D92_clock_01_sakura_rose_gold',
    category: '动态时钟',
    resolution: '2557 × 615',
    orientation: 'landscape',
    imageUrl: '/src/assets/images/theme_sakura_rose_gold_1795333385651.jpg',
    clockStyle: 'sakura',
    author: '绯樱绘社',
    downloads: '15.2k',
    downloadCount: 15200,
    publishedAt: 1731369600000,
    releasedLabel: '11-12 上架',
    addedAt: 1731628800000,
    addedLabel: '11-15 添加',
    lastUsedAt: 1732190400000,
    useCount: 56,
    durationMinutes: 30,
    inPlaylist: true,
    isFavorite: true,
    isRecent: true,
    isTrending: true,
    accentHex: '#E06C9F',
  },
  {
    id: 'wealth-theme',
    name: 'D92_财源广进_2557x615',
    displayName: 'D92_财源广进_2557x615 · 主题',
    category: '主题',
    resolution: '2557 × 615',
    orientation: 'landscape',
    customArt: 'wealth-calligraphy',
    clockStyle: 'none',
    author: '国风馆',
    downloads: '19.6k',
    downloadCount: 19600,
    publishedAt: 1731110400000,
    releasedLabel: '11-09 上架',
    addedAt: 1731456000000,
    addedLabel: '11-13 添加',
    lastUsedAt: 1732017600000,
    useCount: 34,
    durationMinutes: 15,
    inPlaylist: false,
    isFavorite: false,
    isRecent: true,
    accentHex: '#D93829',
  },
  {
    id: 'ice-cat-pastel',
    name: 'D92_clock_02_ice_cat_pastel',
    displayName: 'D92_clock_02_ice_cat_pastel',
    category: '动态时钟',
    resolution: '2557 × 615',
    orientation: 'landscape',
    imageUrl: '/src/assets/images/theme_ice_cat_pastel_1795333386016.jpg',
    clockStyle: 'pastel-ice',
    author: 'Studio 官方',
    downloads: '11.3k',
    downloadCount: 11300,
    publishedAt: 1731628800000,
    releasedLabel: '11-15 上架',
    addedAt: 1731974400000,
    addedLabel: '11-19 添加',
    lastUsedAt: 1732212000000,
    useCount: 39,
    durationMinutes: 30,
    inPlaylist: true,
    isFavorite: true,
    isRecent: true,
    accentHex: '#7B61FF',
  },
  {
    id: 'miniature-bedroom',
    name: 'D92_clock_04_miniature_bedroom_2557x615',
    displayName: 'D92_clock_04_miniature_bedroom_2557x615',
    category: '壁纸',
    resolution: '2557 × 615',
    orientation: 'landscape',
    imageUrl: '/src/assets/images/theme_miniature_bedroom_1795333386709.jpg',
    clockStyle: 'minimal-warm',
    author: '微缩空间',
    source: 'official',
    inLibrary: false,
    downloads: '6.9k',
    downloadCount: 6900,
    publishedAt: 1731888000000,
    releasedLabel: '11-18 上架',
    addedAt: 1732104000000,
    addedLabel: '11-20 添加',
    lastUsedAt: 1731931200000,
    useCount: 15,
    durationMinutes: 25,
    inPlaylist: false,
    isFavorite: false,
    isRecent: true,
    accentHex: '#D49A56',
  },
  {
    id: 'sunset-skyline',
    name: 'D92_sunset_flight_1920x462',
    displayName: '日落航线',
    category: '壁纸',
    resolution: '1920 × 462',
    orientation: 'landscape',
    imageUrl: '/src/assets/images/theme_sunset_skyline_1795333384132.jpg',
    clockStyle: 'minimal-warm',
    author: '云端航迹',
    downloads: '9.1k',
    downloadCount: 9100,
    publishedAt: 1731974400000,
    releasedLabel: '11-19 上架',
    addedAt: 1731888000000,
    addedLabel: '11-18 添加',
    lastUsedAt: 1731844800000,
    useCount: 22,
    durationMinutes: 20,
    inPlaylist: false,
    isFavorite: true,
    isTrending: true,
    accentHex: '#9D6BFF',
  },
  {
    id: 'aurora-prism-3d',
    name: 'D92_aurora_prism_studio',
    displayName: '紫韵极光立方 · 主题',
    category: '主题',
    resolution: '1920 × 462',
    orientation: 'landscape',
    customArt: 'aurora-prism',
    clockStyle: 'pastel-ice',
    author: 'Studio 官方',
    downloads: '14.5k',
    downloadCount: 14500,
    publishedAt: 1732060800000,
    releasedLabel: '11-20 上架',
    addedAt: 1732147200000,
    addedLabel: '11-21 添加',
    lastUsedAt: 1732104000000,
    useCount: 47,
    durationMinutes: 30,
    inPlaylist: true,
    isFavorite: true,
    isTrending: true,
    accentHex: '#5E45F8',
  },
  {
    id: 'ice-cat-trending',
    name: 'D92_cloud_dream_02',
    displayName: '雪夜星猫物语',
    category: '壁纸',
    resolution: '1920 × 462',
    orientation: 'landscape',
    imageUrl: '/src/assets/images/theme_ice_cat_pastel_1795333386016.jpg',
    clockStyle: 'none',
    author: '星云绘',
    source: 'official',
    inLibrary: false,
    downloads: '7.8k',
    downloadCount: 7800,
    publishedAt: 1732147200000,
    releasedLabel: '11-21 上架',
    addedAt: 1732190400000,
    addedLabel: '11-21 添加',
    lastUsedAt: 1731758400000,
    useCount: 11,
    durationMinutes: 15,
    inPlaylist: false,
    isFavorite: false,
    isTrending: true,
    accentHex: '#6D8CFF',
  },
];

export const INITIAL_PLAYLISTS: CarouselPlaylist[] = [
  {
    id: 'pl-work-d92',
    name: '上班',
    deviceId: 'dev-782b05',
    orientation: 'landscape',
    mood: '喜爱',
    shuffle: false,
    defaultDurationSeconds: 30,
    items: [
      { themeId: 'crystal-ice-palace', durationSeconds: 30 },
      { themeId: 'cozy-bear-exact', durationSeconds: 30 },
      { themeId: 'sakura-rose-gold', durationSeconds: 30 },
      { themeId: 'ice-cat-pastel', durationSeconds: 30 },
      { themeId: 'aurora-prism-3d', durationSeconds: 30 },
    ],
  },
  {
    id: 'pl-night-d92',
    name: '深夜沉浸时钟',
    deviceId: 'dev-782b05',
    orientation: 'landscape',
    mood: '夜晚',
    shuffle: true,
    defaultDurationSeconds: 60,
    items: [
      { themeId: 'crystal-ice-palace', durationSeconds: 60 },
      { themeId: 'sunset-skyline', durationSeconds: 60 },
      { themeId: 'ice-cat-trending', durationSeconds: 60 },
    ],
  },
  {
    id: 'pl-portrait-d92',
    name: '竖屏桌面伴侣',
    deviceId: 'dev-782b05',
    orientation: 'portrait',
    mood: '专注',
    shuffle: false,
    defaultDurationSeconds: 30,
    items: [
      { themeId: 'sakura-rose-gold', durationSeconds: 30 },
      { themeId: 'crystal-ice-palace', durationSeconds: 30 },
      { themeId: 'wealth-theme', durationSeconds: 30 },
      { themeId: 'aurora-prism-3d', durationSeconds: 30 },
    ],
  },
  {
    id: 'pl-portrait-morning-d92',
    name: '晨间治愈竖屏',
    deviceId: 'dev-782b05',
    orientation: 'portrait',
    mood: '清晨',
    shuffle: true,
    defaultDurationSeconds: 45,
    items: [
      { themeId: 'cozy-bear-exact', durationSeconds: 45 },
      { themeId: 'miniature-bedroom', durationSeconds: 45 },
      { themeId: 'ice-cat-pastel', durationSeconds: 45 },
    ],
  },
  {
    id: 'pl-morning-d69',
    name: 'D69 桌面清晨',
    deviceId: 'dev-904a18',
    orientation: 'landscape',
    mood: '清晨',
    shuffle: false,
    defaultDurationSeconds: 45,
    items: [
      { themeId: 'cozy-bear-exact', durationSeconds: 45 },
      { themeId: 'miniature-bedroom', durationSeconds: 45 },
      { themeId: 'sakura-rose-gold', durationSeconds: 45 },
    ],
  },
  {
    id: 'pl-focus-d69',
    name: '极简工作台',
    deviceId: 'dev-904a18',
    orientation: 'landscape',
    mood: '专注',
    shuffle: true,
    defaultDurationSeconds: 30,
    items: [
      { themeId: 'aurora-prism-3d', durationSeconds: 30 },
      { themeId: 'sunset-skyline', durationSeconds: 30 },
      { themeId: 'ice-cat-pastel', durationSeconds: 30 },
    ],
  },
  {
    id: 'pl-portrait-d69',
    name: 'D69 竖屏灵感',
    deviceId: 'dev-904a18',
    orientation: 'portrait',
    mood: '灵感',
    shuffle: false,
    defaultDurationSeconds: 30,
    items: [
      { themeId: 'aurora-prism-3d', durationSeconds: 30 },
      { themeId: 'ice-cat-trending', durationSeconds: 30 },
      { themeId: 'sunset-skyline', durationSeconds: 30 },
    ],
  },
];

export const INITIAL_SCHEDULES: ScheduleRule[] = [
  {
    id: 'sch-1',
    title: '工作日清晨桌面唤醒',
    time: '08:30',
    deviceId: 'dev-782b05',
    orientation: 'landscape',
    repeatMode: 'weekdays',
    days: '周一至周五',
    actionType: 'playlist',
    themeId: 'cozy-bear-exact',
    playlistId: 'pl-work-d92',
    playlistNameSnapshot: '上班',
    targetBrightness: 85,
    enabled: true,
  },
  {
    id: 'sch-2',
    title: '午后极光专注模式',
    time: '13:30',
    deviceId: 'dev-782b05',
    orientation: 'landscape',
    repeatMode: 'daily',
    days: '每天',
    actionType: 'theme',
    themeId: 'aurora-prism-3d',
    enabled: true,
  },
  {
    id: 'sch-3',
    title: '夜间冰晶星夜时钟',
    time: '20:00',
    deviceId: 'dev-782b05',
    orientation: 'landscape',
    repeatMode: 'daily',
    days: '每天',
    actionType: 'playlist',
    themeId: 'crystal-ice-palace',
    playlistId: 'pl-night-d92',
    playlistNameSnapshot: '深夜沉浸时钟',
    targetBrightness: 45,
    enabled: true,
  },
  {
    id: 'sch-4',
    title: '深夜自动护眼息屏',
    time: '23:30',
    deviceId: 'dev-782b05',
    orientation: 'landscape',
    repeatMode: 'daily',
    days: '每天',
    actionType: 'sleep',
    themeId: 'crystal-ice-palace',
    enabled: false,
  },
  {
    id: 'sch-5',
    title: '竖屏早间樱花灵感',
    time: '09:00',
    deviceId: 'dev-782b05',
    orientation: 'portrait',
    repeatMode: 'weekdays',
    days: '周一至周五',
    actionType: 'playlist',
    themeId: 'sakura-rose-gold',
    playlistId: 'pl-portrait-morning-d92',
    playlistNameSnapshot: '竖屏早安灵感',
    targetBrightness: 80,
    enabled: true,
  },
  {
    id: 'sch-6',
    title: 'D69 晨间暖风轮播',
    time: '08:45',
    deviceId: 'dev-904a18',
    orientation: 'landscape',
    repeatMode: 'weekdays',
    days: '周一至周五',
    actionType: 'playlist',
    themeId: 'cozy-bear-exact',
    playlistId: 'pl-morning-d69',
    playlistNameSnapshot: 'D69 晨间唤醒',
    targetBrightness: 82,
    enabled: true,
  },
];

export const INITIAL_PLUGINS: PluginItem[] = [
  {
    id: 'plg-clock',
    name: '高精度桌面时钟挂件',
    description: '支持毫秒/秒级同步与星期日期显示，在任意静态壁纸上轻量化悬浮当前时间。',
    category: '时间组件',
    version: 'v2.4.0',
    author: 'Studio 官方',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: true,
    core: true,
    config: {
      position: 'top-left',
      accentColor: '#5E45F8',
      opacity: 76,
      scale: 'standard',
      showSeconds: true,
      showDate: true,
      use24Hour: true,
    },
  },
  {
    id: 'plg-telemetry',
    name: '硬件性能监测条',
    description: '实时采集主机 CPU / GPU 温度、核心负载与内存占用率并映射至副屏角落。',
    category: '系统监控',
    version: 'v1.8.2',
    author: 'Studio 官方',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: true,
    config: {
      position: 'top-right',
      accentColor: '#38BDF8',
      opacity: 78,
      scale: 'standard',
      showTemp: true,
      showMemory: true,
      refreshSeconds: 2,
    },
  },
  {
    id: 'plg-audio',
    name: '桌面音频频谱律动',
    description: '捕获桌面系统音频输出流，在副屏底部生成细腻的动态光波频谱与曲目胶囊。',
    category: '媒体联动',
    version: 'v1.5.0',
    author: '声浪实验室',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'bottom-left',
      accentColor: '#5E45F8',
      opacity: 74,
      scale: 'standard',
      barCount: 20,
      showTrackName: true,
    },
  },
  {
    id: 'plg-pomodoro',
    name: '沉浸式番茄钟计时器',
    description: '在副屏角落常驻 25 分钟专注倒计时与休息提醒，减少主屏切换干扰。',
    category: '效率工具',
    version: 'v1.3.4',
    author: 'FocusLab',
    supportedOrientations: ['landscape', 'portrait'],
    installed: false,
    enabled: false,
    config: {
      position: 'bottom-right',
      accentColor: '#10B981',
      opacity: 80,
      scale: 'standard',
      focusMinutes: 25,
      breakMinutes: 5,
    },
  },
  {
    id: 'plg-weather',
    name: '实时气象微胶囊',
    description: '同步城市实时气温、天气状况与空气质量指数，为桌面环境提供轻量气象参考。',
    category: '生活资讯',
    version: 'v1.6.1',
    author: 'Studio 官方',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'bottom-left',
      accentColor: '#F59E0B',
      opacity: 75,
      scale: 'standard',
      city: '上海',
      showAqi: true,
    },
  },
  {
    id: 'plg-sticky',
    name: '极简桌面备忘便签',
    description: '在副屏指定区域展示自定义今日待办或桌面座右铭，随时保持重点清晰。',
    category: '效率工具',
    version: 'v1.2.0',
    author: '效率工坊',
    supportedOrientations: ['landscape', 'portrait'],
    installed: false,
    enabled: false,
    config: {
      position: 'bottom-right',
      accentColor: '#EC4899',
      opacity: 78,
      scale: 'standard',
      noteText: '完成 D92 视觉方案评审 · 19:00',
    },
  },

  {
    id: 'plg-craft-aquarium',
    name: '水族箱生态 (craft-aquarium)',
    description: '原生动力学微生态水族箱：实时热带鱼群游弋、水中焦散光斑、上升气泡与点击投喂互动。',
    category: '互动娱乐',
    version: 'v1.2.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#38BDF8',
      opacity: 90,
      scale: 'standard',
      fishCount: 7,
      causticsIntensity: 0.6,
      bubbleSpeed: 1.0,
      showAquariumClock: true,
    },
  },
  {
    id: 'plg-craft-stocks',
    name: '金融脉动 (craft-stocks)',
    description: '实时市场脉搏仪表板：支持 NVDA、AAPL、BTC、ETH 多标的切换、SVG 分时趋势曲线与 24H 极值监测。',
    category: '生活资讯',
    version: 'v1.4.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#10B981',
      opacity: 88,
      scale: 'standard',
      stockSymbol: 'NVDA',
    },
  },
  {
    id: 'plg-craft-lyrics',
    name: '霓虹歌词 (craft-lyrics)',
    description: 'Synthwave 霓虹黑胶视觉卡片：黑胶旋转封面、中英双语逐行高亮歌词与音频律动频谱柱。',
    category: '媒体联动',
    version: 'v1.5.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#EC4899',
      opacity: 88,
      scale: 'standard',
      songTitle: 'Midnight City',
      artist: 'M83',
      barCount: 20,
    },
  },
  {
    id: 'plg-craft-nixie',
    name: 'IN-14 辉光管时钟',
    description: '经典拟物 IN-14 真空辉光管时计：玻璃反光灯罩、金属丝网纹理与多色灯丝辉光脉冲。',
    category: '时间组件',
    version: 'v1.3.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#F59E0B',
      opacity: 92,
      scale: 'standard',
      nixieHue: 'amber',
      showSeconds: true,
      showDate: true,
      use24Hour: true,
    },
  },
  {
    id: 'plg-craft-cyber-pet',
    name: '赛博电子萌宠',
    description: '桌面治愈电子宠物伙伴：动态呼吸眨眼动画、元气能量条及多种萌宠形态与心情状态切换。',
    category: '互动娱乐',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#F472B6',
      opacity: 90,
      scale: 'standard',
      petName: 'Mochi',
      petSpecies: 'mochi',
      petMood: 'happy',
    },
  },
  {
    id: 'plg-craft-campfire',
    name: '深夜暖炉篝火',
    description: '实时 2D Canvas 粒子动态火焰与上升余烬火星，为夜间桌搭营造温暖治愈氛围。',
    category: '互动娱乐',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#F59E0B',
      opacity: 92,
      scale: 'standard',
      flameIntensity: 'medium',
      emberDensity: 'medium',
      showSoundHint: true,
    },
  },
  {
    id: 'plg-craft-bonsai',
    name: '微景观桌面盆栽',
    description: '暗黑静谧系桌面矢量盆景，支持青松/玉露多肉/春樱品种切换、日光色温调节与养护天数记录。',
    category: '互动娱乐',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#10B981',
      opacity: 90,
      scale: 'standard',
      bonsaiSpecies: 'pine',
      growthDays: 28,
      sunlightTone: 'warm',
    },
  },
  {
    id: 'plg-craft-flip-clock',
    name: '机械翻页大时钟',
    description: '经典拟物分体机械翻页卡片时钟，配备金属铰链倒角光影、等宽数字与多款材质外壳。',
    category: '时间组件',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#5E45F8',
      opacity: 92,
      scale: 'standard',
      flipShellStyle: 'obsidian',
      showSeconds: true,
      showDate: true,
      use24Hour: true,
    },
  },
  {
    id: 'plg-craft-vu-meter',
    name: '双通道模拟 VU 电平表',
    description: '复古双声道指针式音频电平仪表盘，指针随音频波形灵动摆动并配备峰值红灯警示。',
    category: '媒体联动',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#F59E0B',
      opacity: 92,
      scale: 'standard',
      vuBacklight: 'warm-amber',
      vuSensitivity: 'balanced',
      deckLabel: 'MASTER STEREO',
    },
  },
  {
    id: 'plg-craft-cassette',
    name: '复古磁带卡座',
    description: '透明视窗卡带播放器卡片：双卷轴匀速旋转、手写贴纸风曲目标签与多款透明外壳配色。',
    category: '媒体联动',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#38BDF8',
      opacity: 90,
      scale: 'standard',
      songTitle: 'City Pop Tape #04',
      artist: 'SIDE A · 90MIN',
      cassetteShell: 'smoke',
      tapeSpeed: 1.0,
    },
  },
  {
    id: 'plg-craft-zodiac',
    name: '今日星座能量签',
    description: '暗黑星图毛玻璃罗盘卡片：支持 12 星座实时切换，展示星象连线、幸运色及每日专注/幸运指数。',
    category: '生活资讯',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#A78BFA',
      opacity: 88,
      scale: 'standard',
      zodiacSign: 'aquarius',
      showConstellationGlow: true,
    },
  },
  {
    id: 'plg-craft-parcel',
    name: '快递物流追踪卡',
    description: '四段式物流节点追踪条，清晰展示包裹揽收、干线运输、派送中到签收状态与承运商信息。',
    category: '生活资讯',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#38BDF8',
      opacity: 88,
      scale: 'standard',
      parcelName: '客制化机械键盘套件',
      carrier: '顺丰速运',
      parcelStage: 'delivering',
    },
  },
  {
    id: 'plg-craft-milestone',
    name: '纪念日倒数日历',
    description: '带环形进度徽章的高对比度倒数/正数纪念日卡片，适合项目上线冲刺、考试或重要周年提醒。',
    category: '效率工具',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#F59E0B',
      opacity: 90,
      scale: 'standard',
      milestoneTitle: '年度版本发布',
      targetDays: 18,
      countMode: 'countdown',
      targetDateLabel: '2026.12.15',
    },
  },
  {
    id: 'plg-craft-netradar',
    name: '实时网速雷达',
    description: '实时上下行速率等宽数字监测仪，搭配动态带宽波形折线图与网络节点延迟指示。',
    category: '系统监控',
    version: 'v1.0.0',
    author: 'Craft 生态馆',
    supportedOrientations: ['landscape', 'portrait'],
    installed: true,
    enabled: false,
    config: {
      position: 'full',
      accentColor: '#10B981',
      opacity: 88,
      scale: 'standard',
      unitMode: 'Mbps',
      refreshSeconds: 2,
      showPingChart: true,
    },
  },
];







