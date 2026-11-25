export interface Device {
  id: string;
  name: string;
  model: 'D92' | 'D69';
  w: number;
  h: number;
  rot: number; // 0, 90, 180, 270
  online: boolean;
  content: number; // resource id
  running: boolean;
  serial: string;
  fit?: 'contain' | 'cover';
  flash?: boolean;
}

export interface Resource {
  id: number;
  name: string;
  type: '壁纸' | '主题' | '时钟' | '视频';
  style: string;
  sizes: [number, number][];
  local: boolean;
  online?: boolean;
  cat?: [string, string];
  hot?: number;
  added?: number;
  text?: string;
  color?: string;
  url?: string;
  created?: boolean;
  bgCopy?: { from: number; name: string };
  layoutData?: Record<number, { text?: string; color?: string; style?: string }>;
}

export interface PlaylistItem {
  id: number;
  sec: number;
}

export interface Playlist {
  name: string;
  mood?: string;
  items: PlaylistItem[];
  device: string;
  playing: boolean;
  at: number;
  shuffle?: boolean;
  dev0?: string;
}

export interface RecycleEntry {
  resource: Resource;
  fav: boolean;
  downloaded: boolean;
  refs: { playlistName: string; index: number; item: PlaylistItem }[];
}

export interface FitResult {
  kind: 'ok' | 'blue' | 'warn' | '';
  text: string;
  loss: number | null;
  index: number;
}

export interface AppSettings {
  tray: boolean;
  start: boolean;
  restore: boolean;
}

export interface PreviewSession {
  snap: {
    id: string;
    content: number;
    running: boolean;
    fit?: 'contain' | 'cover';
    lists: { p: Playlist; playing: boolean; at: number }[];
  };
  id: number;
  remaining: number;
}
