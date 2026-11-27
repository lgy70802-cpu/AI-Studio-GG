import type { DeviceInfo } from '../data/themes';

type Bridge = {
  postMessage(message: unknown): void;
  addEventListener(type: string, callback: (event: MessageEvent) => void): void;
};
const bridge = (window as unknown as { chrome?: { webview?: Bridge } }).chrome?.webview;
export const desktopMode = Boolean(bridge);
const session = crypto.randomUUID();
let sequence = 0;
const pending = new Map<string, { resolve(value: unknown): void; reject(error: Error): void }>();
bridge?.addEventListener('message', event => {
  const { id, result, error, progress, updateProgress } = event.data;
  if (updateProgress) { window.dispatchEvent(new CustomEvent('studio-update-progress', { detail: updateProgress })); return; }
  if (progress) { window.dispatchEvent(new CustomEvent('studio-package-progress', { detail: progress })); return; }
  const request = pending.get(id);
  if (!request) return;
  pending.delete(id);
  if (error) request.reject(new Error(error));
  else request.resolve(result);
});
function request<T>(command: string, payload: object = {}): Promise<T> {
  return new Promise((resolve, reject) => {
    const id = session + String(++sequence);
    pending.set(id, { resolve: value => resolve(value as T), reject });
    bridge!.postMessage({ id, command, ...payload });
    if (!command.startsWith('capture-') && !command.startsWith('package-') && !command.startsWith('updates-')) window.setTimeout(() => {
      if (pending.delete(id)) reject(new Error('设备操作超时，请检查连接后重试。'));
    }, 60000);
  });
}
export interface OrientationUse { Serial: string; Portrait: boolean; ThemeId: string; UsedAt: number }
export const scanDevices = () => request<{ orientationHistory?: OrientationUse[]; selected?: string; restore: boolean; scheduling: ScheduleRuntime; devices: DeviceInfo[] }>('devices');
export async function outputImage(device: DeviceInfo, source: string, themeId: string, themeName: string) {
  const response = await fetch(source);
  if (!response.ok) throw new Error('无法读取所选图片。');
  const blob = await response.blob();
  const image = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(new Error('读取图片失败。'));
    reader.readAsDataURL(blob);
  });
  return request('apply', { serial: device.serial, portrait: device.orientation === 'portrait', flipped: device.flipped, themeId, themeName, image });
}

export const configureDevice = (serial: string, patch: { portrait?: boolean; flipped?: boolean; playing?: boolean }) =>
  request('configure', { serial, ...patch });
export const devicePower = (serial: string, patch: { brightness?: number; sleepMinutes?: number; action?: 'standby' | 'wake' }) => request('device-power', { serial, ...patch });
export const desktopPreferences = () => request<{ autoStart: boolean; closeToTray: boolean }>('desktop-preferences');
export const setAutoStartNative = (enabled: boolean) => request<{ autoStart: boolean; closeToTray: boolean }>('desktop-autostart', { enabled });
export const cleanTemporaryFiles = () => request<{ bytes: number }>('cache-clean');
export interface PackageSummary { cancelled: boolean; token: string; scope: string; themes: number; playlists: number; schedules: number; note: string }
export const inspectPackage = () => request<PackageSummary>('package-inspect');
export const cancelPackage = () => request('package-cancel');
export const importPackage = (token: string, mode: 'merge' | 'restore') => request<{ preferences: Record<string, string> }>('package-import', { token, mode });
export const exportPackage = (themeId?: string) => request<{ cancelled: boolean; path: string }>('package-export', {
  themeId, preferences: Object.fromEntries(Object.entries(localStorage).filter(([key]) => key.startsWith('studio-') || key === 'd92-te-custom-assets-v1')),
});
export const selectDesktopDevice = (serial: string) => request('select', { serial });
export const setRestoreOutput = (enabled: boolean) => request('restore', { enabled });


export interface LibrarySnapshot {
  themes: import('../data/themes').ThemeItem[];
  playlists: import('../data/themes').CarouselPlaylist[];
  schedules: import('../data/themes').ScheduleRule[];
}
export const loadLibrary = () => request<LibrarySnapshot | null>('library-load');
export const saveLibrary = (snapshot: LibrarySnapshot) => request<import('../data/themes').ThemeItem[]>('library-save', { snapshot });



export const playlistCommand = (command: 'playlist-start' | 'playlist-pause' | 'playlist-step', serial: string, playlistId: string, direction = 1) =>
  request(command, { serial, playlistId, direction });

export function readViewedPlaylists(): Record<string, string> {
  try {
    const saved = JSON.parse(localStorage.getItem('studio-viewed-playlists') || '{}');
    return Object.fromEntries(Object.entries(saved ?? {}).filter((entry): entry is [string, string] => typeof entry[1] === 'string'));
  } catch { return {}; }
}

export interface ScheduleRuntime {
  enabled: boolean;
  completed: string[];
  history: { id: string; title: string; ruleId: string; serial: string; at: string; status: string; reason: string; manual: boolean }[];
  rules: { id: string; nextAt: string | null; warning: string | null; conflict: boolean }[];
}
export const enableSchedules = (enabled: boolean) => request('schedule-enable', { enabled });
export const runSchedule = (ruleId: string) => request<ScheduleRuntime>('schedule-run', { ruleId });
export const resetSchedule = (ruleId: string) => request<ScheduleRuntime>('schedule-reset', { ruleId });


export const applyMedia = (serial: string, themeId: string) => request('apply-media', { serial, themeId });
export const previewTheme = (serial: string, theme: import('../data/themes').ThemeItem) => request('theme-preview', { serial, theme });
export const endThemePreview = (serial: string) => request('theme-preview-end', { serial });
export async function importVideo(file: File, progress: (percent: number) => void) {
  if (file.size > 500 * 1024 * 1024) throw new Error('视频超过 500 MiB');
  const uploadId = await request<string>('video-begin', { name: file.name, size: file.size });
  try {
    const chunkSize = 512 * 1024;
    for (let offset = 0; offset < file.size; offset += chunkSize) {
      const data = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result).split(',')[1]);
        reader.onerror = () => reject(new Error('读取视频失败'));
        reader.readAsDataURL(file.slice(offset, offset + chunkSize));
      });
      await request('video-chunk', { uploadId, data });
      progress(Math.min(100, Math.round((offset + chunkSize) / file.size * 100)));
    }
    return await request<import('../data/themes').ThemeItem>('video-finish', { uploadId });
  } catch (error) {
    await request('video-cancel', { uploadId }).catch(() => {});
    throw error;
  }
}

export const controlWindow = (action: 'minimize' | 'maximize' | 'close') => bridge?.postMessage({ command: 'window-control', action });

export interface UpdateManifest { Version: string; Notes: string; Url: string; Sha256: string; Bytes: number }
export const updateStatus = () => request<{version: string; source: string}>('updates-status');
export const checkUpdates = (source: string) => request<{current: string; available: UpdateManifest | null}>('updates-check', { source });
export const installUpdate = () => request<{backup: string}>('updates-install', {
  preferences: Object.fromEntries(Object.entries(localStorage).filter(([key]) => key.startsWith('studio-') || key.startsWith('d92-'))),
});
export const cancelUpdate = () => request('updates-cancel');

export const captureDesktopImage = (width: number, height: number) => request<{ cancelled: boolean; src: string; width: number; height: number; name: string }>('capture-shot', { width, height });


export const captureDesktopVideo = (width: number, height: number) => request<{ cancelled: boolean; src: string; poster: string; width: number; height: number; name: string; dur: number }>('capture-record', { width, height });

export const refreshCurrentMedia = (serial: string, themeId: string, portrait: boolean, sourceThemeId: string) => request<boolean>('refresh-current-media', { serial, themeId, portrait, sourceThemeId });

export const setCloseToTray = (enabled: boolean) => request<{ closeToTray: boolean }>("window-preferences", { enabled });
export const renameDevice = (serial: string, name: string) => request<boolean>("device-rename", { serial, name });
