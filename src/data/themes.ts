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
export type ContentCategory = '壁纸' | '主题' | '动态时钟';

export type ThemeSource = 'official' | 'custom' | 'local';

export interface ThemeItem {
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
  id: string;
  name: string;
  serial: string;
  model: string;
  status: '已连接' | '待机中';
  nativeWidth: number;
  nativeHeight: number;
  resolution: string;
  refreshRate: string;
  brightness: number;
  orientation: ScreenOrientation;
  flipped: boolean;
  sleepMinutes: number;
  firmware: string;
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

export type PluginCategory = '时间组件' | '系统监控' | '媒体联动' | '效率工具' | '生活资讯';
export type PluginPosition = 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' | 'center';
export type PluginScale = 'compact' | 'standard' | 'large';

export interface PluginConfig {
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
];
