import { ContentCategory, ScreenOrientation } from '../data/themes';

export const THEME_SCHEMA_VERSION = '2.0.0';
export const CLIENT_ENGINE_VERSION = '2.4.1';

export type AnchorPoint = 'tl' | 'tc' | 'tr' | 'ml' | 'mc' | 'mr' | 'bl' | 'bc' | 'br';

export type ClockStyleKind = 'digital' | 'flip' | 'minimal' | 'analog';
export type ClockPartKind = 'hms' | 'hm' | 'hour' | 'minute' | 'second';
export type ClockDialKind = 'tick' | 'dot' | 'num' | 'roman' | 'bare';
export type ClockHandsKind = 'line' | 'round' | 'taper';

export type DatePartKind = 'full' | 'ymd' | 'weekday' | 'ampm';

export type TextEffectKind = '' | 'glow' | 'shadow' | 'outline' | 'gradient';

export type ShapeStyleKind =
  | 'rect'
  | 'circle'
  | 'dash'
  | 'dots'
  | 'double'
  | 'ring'
  | 'brackets'
  | 'dotline';

export type MotionEffectKind =
  | 'particles'
  | 'snow'
  | 'gradient-flow'
  | 'breathe'
  | 'float'
  | 'rotate'
  | 'fade'
  | 'gif-sticker'
  | 'meteor'
  | 'matrix-rain'
  | 'scanline'
  | 'ripple'
  | 'aurora-ribbon'
  | 'starfield'
  | 'firefly'
  | 'prism-beams';

export type WidgetTypeKind =
  | 'progress'
  | 'ring-progress'
  | 'countdown'
  | 'date-card'
  | 'weather'
  | 'sysmon'
  | 'audio-spectrum'
  | 'mini-chart'
  | 'clock-widget'
  | 'telemetry-bar'
  | 'audio-rhythm'
  | 'pomodoro-widget'
  | 'weather-capsule'
  | 'sticky-note';

export type PluginCapabilityKey =
  | 'music-info'
  | 'lyrics-sync'
  | 'quick-notes'
  | 'photo-album'
  | 'pomodoro'
  | 'stock-ticker'
  | 'system-monitor'
  | 'countdown-timer'
  | 'todo-list'
  | 'desktop-aquarium'
  | 'desktop-pet'
  | 'nixie-clock'
  | 'cyber-pet'
  | 'campfire'
  | 'zen-bonsai'
  | 'flip-clock'
  | 'vu-meter'
  | 'cassette-deck'
  | 'zodiac-oracle'
  | 'parcel-tracker'
  | 'milestone-days'
  | 'net-radar';

export interface EditorElementBase {
  id: string;
  a: AnchorPoint;
  x: number; // relative offset in cqw
  y: number; // relative offset in cqh
  size: number; // primary scale in cqmin
  rot?: number;
  sx?: number;
  sy?: number;
  color: string;
  alpha: number;
  weight: number;
  font?: string;
  ls?: number; // letter-spacing * 100 em
  fx?: TextEffectKind;
  vert?: boolean;
  shadow?: {
    color: string;
    alpha?: number; // 0 - 100
    blur: number; // cqmin
    x: number; // cqmin
    y: number; // cqmin
  };
  g?: string; // group id
  hide?: boolean;
}

export interface ClockElement extends EditorElementBase {
  kind: 'clock';
  style: ClockStyleKind;
  part?: ClockPartKind;
  dial?: ClockDialKind;
  hands?: ClockHandsKind;
  ring?: boolean;
  accent?: string;
  h24: boolean;
  sec: boolean;
  tz: '' | 'JP' | 'GB' | 'US';
}

export interface DateElement extends EditorElementBase {
  kind: 'date';
  fmt: 0 | 1 | 2 | 3;
  part?: DatePartKind;
}

export interface TextElement extends EditorElementBase {
  kind: 'text';
  text: string;
  w?: number; // optional text wrap width in cqw
  h?: number; // optional vertical-rl wrap height in cqh
  align?: '' | 'left' | 'center' | 'right';
  lh?: number;
  g1?: string;
  g2?: string;
  gAngle?: number;
}

export type ImageFeatherMode =
  | 'all'
  | 'left'
  | 'right'
  | 'top'
  | 'bottom'
  | 'horizontal'
  | 'vertical';

export interface ImageElement extends EditorElementBase {
  kind: 'image';
  src: string;
  w?: number; // optional explicit width in cqw for free resize / crop
  h?: number; // optional explicit height in cqh for free resize / crop
  ar?: number;
  radius?: number;
  blur?: number;
  fit?: 'fill' | 'cover' | 'contain';
  brightness?: number; // 50 - 150, default 100
  contrast?: number; // 50 - 150, default 100
  saturate?: number; // 0 - 200, default 100
  cropScale?: number; // 1 - 2.5, default 1
  cropX?: number; // -50 - 50, default 0
  cropY?: number; // -50 - 50, default 0
  feather?: {
    mode: ImageFeatherMode;
    strength: number; // 0 - 60 (%)
  };
  sourceMeta?: {
    origin: 'local' | 'library' | 'cloud' | 'capture';
    naturalWidth?: number;
    naturalHeight?: number;
    name?: string;
  };
}

export interface VideoElement extends EditorElementBase {
  kind: 'video';
  src: string;
  poster?: string;
  w?: number; // optional explicit width in cqw for free resize / crop
  h?: number; // optional explicit height in cqh for free resize / crop
  ar?: number;
  dur?: number;
  fit?: 'fill' | 'cover' | 'contain';
  radius?: number;
  blur?: number;
  brightness?: number; // 50 - 150, default 100
  contrast?: number; // 50 - 150, default 100
  saturate?: number; // 0 - 200, default 100
  cropScale?: number; // 1 - 2.5, default 1
  cropX?: number; // -50 - 50, default 0
  cropY?: number; // -50 - 50, default 0
  feather?: {
    mode: ImageFeatherMode;
    strength: number; // 0 - 60 (%)
  };
  sourceMeta?: {
    origin: 'local' | 'library' | 'capture';
    naturalWidth?: number;
    naturalHeight?: number;
    name?: string;
  };
}

export interface ShapeElement extends EditorElementBase {
  kind: 'shape';
  sh?: ShapeStyleKind;
  w: number; // % of canvas width (cqw)
  h: number; // % of canvas height (cqh)
  radius: number;
  bw?: number;
  strokeColor?: string;
  fillMode?: 'solid' | 'gradient';
  color2?: string;
  gradAngle?: number;
  backdropBlur?: number; // px
}

export interface MotionElement extends EditorElementBase {
  kind: 'motion';
  effect: MotionEffectKind;
  w: number;
  h: number;
  speed: number; // 0.5 - 2.0
  intensity: number; // 20 - 100
  secondaryColor?: string;
  performanceCost: 'low' | 'medium' | 'high';
  requiresGpu?: boolean;
  label?: string;
}

export interface WidgetElement extends EditorElementBase {
  kind: 'widget';
  widgetType: WidgetTypeKind;
  w: number;
  h: number;
  widgetVersion: string;
  dataSource: string;
  accentColor: string;
  bgOpacity: number;
  label?: string;
  value?: number; // 0-100
  targetLabel?: string;
  city?: string;
  barCount?: number;
  showSeconds?: boolean;
  showDate?: boolean;
  use24Hour?: boolean;
  showTemp?: boolean;
  showMemory?: boolean;
  refreshSeconds?: number;
  showTrackName?: boolean;
  trackName?: string;
  focusMinutes?: number;
  breakMinutes?: number;
  showAqi?: boolean;
  noteText?: string;
}

export interface PluginElement extends EditorElementBase {
  kind: 'plugin';
  pluginId: string;
  pluginName: string;
  pluginVersion: string;
  capabilityKey: PluginCapabilityKey;
  w: number;
  h: number;
  accentColor: string;
  bgOpacity: number;
  config: Record<string, string | number | boolean>;
}

export type EditorElement =
  | ClockElement
  | DateElement
  | TextElement
  | ImageElement
  | VideoElement
  | ShapeElement
  | MotionElement
  | WidgetElement
  | PluginElement;

export type BgStylePreset =
  | 'paper'
  | 'ink'
  | 'night'
  | 'mountain'
  | 'ocean'
  | 'dusk'
  | 'aurora'
  | 'grid-tech';

export interface BgConfig {
  kind: 'style' | 'color' | 'gradient' | 'image' | 'video';
  style?: BgStylePreset;
  url?: string;
  vurl?: string;
  poster?: string;
  dur?: number;
  vname?: string;
  color?: string;
  g1?: string;
  g2?: string;
  angle?: number;
  dim: number; // 0 - 80
  blur: number; // 0 - 12
  brightness?: number; // 50 - 150, default 100
  contrast?: number; // 50 - 150, default 100
  saturate?: number; // 0 - 200, default 100
  fit: 'cover' | 'contain' | 'stretch';
  hide?: boolean;
  tx?: number;
  ty?: number;
  rot?: number;
  sc?: number;
}

export interface EditorLayout {
  size: [number, number];
  label: string;
  follow: boolean;
  elements?: EditorElement[];
  vis?: Record<string, boolean>;
}

export interface AssetManifestEntry {
  assetId: string;
  kind: 'image' | 'video' | 'gif' | 'font' | 'icon';
  name: string;
  source: 'builtin' | 'local' | 'cloud' | 'library';
  width?: number;
  height?: number;
  checksum?: string;
  sizeBytes?: number;
}

export interface EditorThemeProject {
  template_id: string;
  name: string;
  description: string;
  category: ContentCategory;
  category_id: string;
  tags: string[];
  version: string;
  schema_version: string;
  min_client_version: string;
  required_capabilities: string[];
  author: string;
  entitlement: 'free' | 'pro' | 'vip';
  supported_devices: string[];
  supported_resolutions: string[];
  thumbnail_variants: Partial<Record<ScreenOrientation, string>>;
  bg: BgConfig;
  master: EditorElement[];
  layouts: EditorLayout[];
  locks: Record<string, boolean>;
  names: Record<string, string>;
  asset_manifest: AssetManifestEntry[];
  updated_at: number;
  sourceOrigin: 'builtin' | 'cloud' | 'custom';
  cloudTemplateId?: string;
  cloudVersion?: string;
}

// ============================================================
// Cloud Resource System (4-tier architecture)
// ============================================================

export type CloudResourceKind = 'template' | 'asset' | 'widget_preset' | 'plugin_capability';

export interface CloudCategoryNode {
  id: string;
  label: string;
  children?: { id: string; label: string }[];
}

export interface CloudCatalogItem {
  id: string;
  kind: CloudResourceKind;
  name: string;
  description: string;
  categoryId: string;
  subCategoryId: string;
  tags: string[];
  version: string;
  schemaVersion: string;
  minClientVersion: string;
  requiredCapabilities: string[];
  author: string;
  entitlement: 'free' | 'pro' | 'vip';
  supportedDevices: string[];
  supportedResolutions: string[];
  downloads: number;
  rating: number;
  updatedAt: number;
  sizeBytes: number;
  checksum: string;
  featured?: boolean;
  accentHex: string;
  previewBg: BgConfig;
  previewElements?: EditorElement[];
  /** For template kind: full blueprint generator or embedded manifest */
  templateProject?: Omit<EditorThemeProject, 'template_id' | 'updated_at'>;
  /** For asset kind */
  assetCategory?: string;
  assetPresetId?: string;
  /** For widget preset kind */
  widgetType?: WidgetTypeKind;
  widgetVersion?: string;
  /** For plugin capability kind */
  pluginKey?: PluginCapabilityKey;
  compatibilityNote?: string;
}

export type CloudDownloadStatus = 'idle' | 'downloading' | 'verifying' | 'cached' | 'error';

export interface CloudDownloadState {
  status: CloudDownloadStatus;
  progress: number; // 0..100
  cachedVersion?: string;
  cachedAt?: number;
  errorMsg?: string;
}

export interface CloudQueryParams {
  kind: CloudResourceKind;
  search?: string;
  categoryId?: string;
  subCategoryId?: string;
  tag?: string;
  resolution?: string;
  sort?: 'popular' | 'newest' | 'featured';
  cursor?: number;
  limit?: number;
}

export interface CloudQueryResult {
  items: CloudCatalogItem[];
  total: number;
  nextCursor: number | null;
  fromOfflineCache: boolean;
}

export interface PluginCapabilityDescriptor {
  id: string;
  key: PluginCapabilityKey;
  name: string;
  version: string;
  description: string;
  category: string;
  requiredPermissions: string[];
  minClientVersion: string;
  supportedDevices: string[];
  performanceTier: '轻量' | '标准' | '高性能';
  supportedInRuntime: boolean;
  unsupportedReason?: string;
  defaultW: number;
  defaultH: number;
  defaultAccent: string;
  defaultConfig: Record<string, string | number | boolean>;
  declarativeOnlyNote: string;
}

export interface WidgetCapabilityDescriptor {
  type: WidgetTypeKind;
  name: string;
  version: string;
  description: string;
  dataSource: string;
  supportedDevices: string[];
  performanceCost: 'low' | 'medium';
  editableParams: string[];
  defaultW: number;
  defaultH: number;
  defaultAccent: string;
  defaultProps?: Partial<WidgetElement>;
}

export type EditorRailTab =
  | 'add'
  | 'templates'
  | 'assets'
  | 'time'
  | 'background'
  | 'motion'
  | 'widgets'
  | 'plugins'
  | 'layers';

export interface EditorToolMetadata {
  id: EditorRailTab;
  label: string;
}
