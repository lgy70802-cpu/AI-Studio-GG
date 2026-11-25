import { desktopMode, scanDevices, outputImage, configureDevice, selectDesktopDevice, loadLibrary, saveLibrary, playlistCommand, readViewedPlaylists, applyMedia, devicePower, previewTheme as previewDesktopTheme, endThemePreview, enableSchedules, runSchedule, resetSchedule, ScheduleRuntime } from './services/desktopBridge';
import React, { useState, useMemo, useCallback, useRef, useEffect } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Square,
  Play,
  Image as ImageIcon,
  MoreHorizontal,
  X,
  CheckCircle2,
  Layers,
  RotateCcw,
} from 'lucide-react';
import {
  CarouselPlaylist,
  ContentCategory,
  DeviceInfo,
  INITIAL_DEVICES,
  INITIAL_PLAYLISTS,
  INITIAL_PLUGINS,
  INITIAL_SCHEDULES,
  INITIAL_THEMES,
  MoodTag,
  NavSection,
  PluginConfig,
  PluginItem,
  ScheduleRule,
  ScreenOrientation,
  ThemeItem,
} from './data/themes';
import { Sidebar, SidebarMode } from './components/Sidebar';
import { TopBar } from './components/TopBar';
import { HomeDashboard } from './components/HomeDashboard';
import { SecondaryViews } from './components/SecondaryViews';
import { ContentPickerModal } from './components/ContentPickerModal';
import { ThemeArtwork } from './components/ThemeArtwork';
import { PomodoroRuntimeState } from './components/PluginWidgets';
import { PortraitPosterCard } from './components/PortraitPosterCard';
import { LandscapePosterCard } from './components/LandscapePosterCard';
import { EditorThemeProject } from './types/editor';
import { ensureEditorProjectFromTheme, genEditorId } from './data/editorCatalog';

export default function App() {
  const [activeNav, setActiveNav] = useState<NavSection>('home');
  const [themes, setThemes] = useState<ThemeItem[]>(INITIAL_THEMES);
  const [devices, setDevices] = useState<DeviceInfo[]>(desktopMode ? [] : INITIAL_DEVICES);
  const [activeDeviceId, setActiveDeviceId] = useState<string>(INITIAL_DEVICES[0].id);
  const [activeThemeByScope, setActiveThemeByScope] = useState<Record<string, string>>({
    'dev-782b05:landscape': INITIAL_THEMES[0].id,
    'dev-782b05:portrait': 'sakura-rose-gold',
    'dev-904a18:landscape': 'cozy-bear-exact',
    'dev-904a18:portrait': 'aurora-prism-3d',
  });
  const [isPlayingByScope, setIsPlayingByScope] = useState<Record<string, boolean>>({
    'dev-782b05:landscape': true,
    'dev-782b05:portrait': true,
    'dev-904a18:landscape': true,
    'dev-904a18:portrait': true,
  });
  const [searchQuery, setSearchQuery] = useState<string>(() => localStorage.getItem('studio-library-query') ?? '');
  const [categoryFilter, setCategoryFilter] = useState<ContentCategory | '全部'>(() => (localStorage.getItem('studio-library-category') as ContentCategory | '全部') ?? '全部');
  const [schedules, setSchedules] = useState<ScheduleRule[]>(INITIAL_SCHEDULES);
  const [schedulesEnabled, setSchedulesEnabled] = useState<boolean>(true);
  useEffect(() => {
    localStorage.setItem('studio-library-query', searchQuery);
    localStorage.setItem('studio-library-category', categoryFilter);
  }, [searchQuery, categoryFilter]);  const triggeredScheduleRef = useRef<Record<string, string>>({});
  const [plugins, setPlugins] = useState<PluginItem[]>(INITIAL_PLUGINS);
  const [pureMode, setPureMode] = useState<boolean>(false);
  const [pomodoroState, setPomodoroState] = useState<PomodoroRuntimeState>({
    running: false,
    remainingSec: 25 * 60,
    mode: 'focus',
  });
  const [playlists, setPlaylists] = useState<CarouselPlaylist[]>(INITIAL_PLAYLISTS);
  const [activePlaylistByScope, setActivePlaylistByScope] = useState<Record<string, string>>(() => desktopMode ? readViewedPlaylists() : {
    'dev-782b05:landscape': 'pl-work-d92',
    'dev-782b05:portrait': 'pl-portrait-d92',
    'dev-904a18:landscape': 'pl-morning-d69',
    'dev-904a18:portrait': 'pl-portrait-d69',
  });
  useEffect(() => {
    if (desktopMode) localStorage.setItem('studio-viewed-playlists', JSON.stringify(activePlaylistByScope));
  }, [activePlaylistByScope]);
  const [playingPlaylistByScope, setPlayingPlaylistByScope] = useState<
    Record<string, string | null>
  >({
    'dev-782b05:landscape': 'pl-work-d92',
    'dev-782b05:portrait': null,
    'dev-904a18:landscape': null,
    'dev-904a18:portrait': null,
  });
  const [simulatedCountdownSeconds, setCountdownSeconds] = useState<number | null>(null);
  const [pickerOpen, setPickerOpen] = useState<boolean>(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState<boolean>(false);
  const [editorSession, setEditorSession] = useState<{
    project: EditorThemeProject;
    sourceThemeId?: string;
  } | null>(null);
  const [editorSidebarMode, setEditorSidebarMode] = useState<SidebarMode>('hidden');
  const [tempPreview, setTempPreview] = useState<{
    scopeKey: string;
    prevThemeId: string;
    prevPlayingPlaylistId: string | null;
    prevIsPlaying: boolean;
    previewTheme: ThemeItem;
    remainingSec: number;
  } | null>(null);
  const [toast, setToast] = useState<{
    id: number;
    message: string;
    onUndo?: () => void;
  } | null>(null);
  const toastTimerRef = useRef<number | null>(null);
  const [recentLogs, setRecentLogs] = useState<string[]>(desktopMode ? [] : [
    '已连接 D92 实机 · C511D3782B05 (1920×462)',
    '当前输出：D92_clock_03_crystal_ice_palace',
  ]);

  const [scheduleRuntime, setScheduleRuntime] = useState<ScheduleRuntime | null>(null);
  const [desktopReady, setDesktopReady] = useState(!desktopMode);
  const desktopSelectionLoaded = useRef(false);
  const refreshDesktop = useCallback(async () => {
    const snapshot = await scanDevices();
    setDevices(snapshot.devices);
    if (snapshot.scheduling) { setScheduleRuntime(snapshot.scheduling); setSchedulesEnabled(snapshot.scheduling.enabled); }
    if (!desktopSelectionLoaded.current) {
      setActiveDeviceId(snapshot.selected ?? snapshot.devices[0]?.id ?? '');
      desktopSelectionLoaded.current = true;
    }
    setDesktopReady(true);
  }, []);
  useEffect(() => {
    if (!desktopMode) return;
    let disposed = false;
    let busy = false;
    const scan = async () => {
      if (busy || disposed) return;
      busy = true;
      try { await refreshDesktop(); }
      catch (error) { console.error(error); }
      finally { busy = false; }
    };
    void scan();
    const timer = window.setInterval(scan, 1000);
    return () => { disposed = true; window.clearInterval(timer); };
  }, [refreshDesktop]);
  const activeDevice = useMemo(
    () => devices.find((d) => d.id === activeDeviceId) ?? { ...INITIAL_DEVICES[0], id: '', serial: '', name: desktopReady ? '未连接 MiraBox' : '正在查找 MiraBox', status: '待机中' as const },
    [devices, activeDeviceId, desktopReady]
  );

  const countdownSeconds = desktopMode ? activeDevice.playlistRemaining ?? null : simulatedCountdownSeconds;
  const currentScopeKey = `${activeDevice.id}:${activeDevice.orientation}`;
  const isPlaying = desktopMode ? Boolean(activeDevice.playing) : isPlayingByScope[currentScopeKey] ?? true;

  const activeTheme = useMemo(() => {
    const scopedId = desktopMode ? activeDevice.themeId : activeThemeByScope[currentScopeKey];
    return themes.find((t) => t.id === scopedId) ?? themes[0] ?? INITIAL_THEMES[0];
  }, [themes, activeThemeByScope, currentScopeKey, activeDevice.themeId]);

  const effectiveActiveTheme = useMemo(() => {
    if (desktopMode) return {
      ...activeTheme,
      id: activeDevice.themeId ?? 'desktop-empty',
      displayName: activeDevice.themeId ? activeDevice.themeName ?? activeTheme.displayName : '请选择壁纸',
      imageUrl: activeDevice.preview,
      desktopStatic: true,
      desktopFlipped: activeDevice.flipped,
      desktopBrightness: activeDevice.standby ? 0 : activeDevice.brightness ?? 100,
      category: activeTheme.category,
      editorProject: undefined,
    };
    if (tempPreview && tempPreview.scopeKey === currentScopeKey) {
      return tempPreview.previewTheme;
    }
    return activeTheme;
  }, [tempPreview, currentScopeKey, activeTheme, activeDevice]);

  const scopedPlaylists = useMemo(
    () =>
      playlists.filter(
        (p) => p.deviceId === activeDevice.id && p.orientation === activeDevice.orientation
      ),
    [playlists, activeDevice.id, activeDevice.orientation]
  );

  const playingPlaylistId = useMemo(() => {
    const candidate = desktopMode ? (activeDevice.playlistRunning ? activeDevice.playlistId ?? null : null) : playingPlaylistByScope[currentScopeKey] ?? null;
    if (!candidate) return null;
    return scopedPlaylists.some((p) => p.id === candidate) ? candidate : null;
  }, [playingPlaylistByScope, currentScopeKey, scopedPlaylists, activeDevice]);

  const activePlaylistId = useMemo(() => {
    const candidate = activePlaylistByScope[currentScopeKey];
    if (candidate && scopedPlaylists.some((p) => p.id === candidate)) {
      return candidate;
    }
    if (desktopMode && scopedPlaylists.some(p => p.id === activeDevice.playlistId)) return activeDevice.playlistId!;
    return scopedPlaylists[0]?.id ?? '';
  }, [activePlaylistByScope, currentScopeKey, scopedPlaylists, activeDevice]);

  // Target playlist strictly scoped to current device + orientation (never leaks across devices)
  const activeScopedPlaylist = useMemo(() => {
    if (playingPlaylistId) {
      const playing = scopedPlaylists.find((p) => p.id === playingPlaylistId);
      if (playing) return playing;
    }
    if (activePlaylistId) {
      const viewing = scopedPlaylists.find((p) => p.id === activePlaylistId);
      if (viewing) return viewing;
    }
    return scopedPlaylists[0] ?? null;
  }, [scopedPlaylists, playingPlaylistId, activePlaylistId]);

  const themesWithPlaylist = useMemo(() => {
    const activeIds = new Set(activeScopedPlaylist?.items.map((i) => i.themeId) ?? []);
    return themes.filter(t => !t.deletedAt).map((t) => ({ ...t, inPlaylist: activeIds.has(t.id) }));
  }, [themes, activeScopedPlaylist]);

  const notify = useCallback(
    (msg: string, options?: { durationMs?: number; onUndo?: () => void }) => {
      const id = Date.now();
      if (toastTimerRef.current !== null) {
        window.clearTimeout(toastTimerRef.current);
      }
      setToast({ id, message: msg, onUndo: options?.onUndo });
      setRecentLogs((prev) => [msg, ...prev].slice(0, 8));
      toastTimerRef.current = window.setTimeout(() => {
        setToast((current) => (current?.id === id ? null : current));
      }, options?.durationMs ?? 2800);
    },
    []
  );

  const [libraryReady, setLibraryReady] = useState(!desktopMode);
  const [libraryError, setLibraryError] = useState('');
  const [librarySaving, setLibrarySaving] = useState(false);
  const [libraryRetry, setLibraryRetry] = useState(0);
  const saveChain = useRef(Promise.resolve());
  const saveVersion = useRef(0);
  const saveFailed = useRef(false);
  useEffect(() => {
    if (!desktopMode) return;
    void loadLibrary().then(snapshot => {
      if (snapshot) {
        setThemes(snapshot.themes);
        setPlaylists(snapshot.playlists);
        setSchedules(snapshot.schedules);
      }
      setLibraryReady(true);
    }).catch(error => setLibraryError('读取资源库失败：' + error.message));
  }, []);
  useEffect(() => {
    if (!desktopMode || !libraryReady) return;
    const version = ++saveVersion.current;
    const snapshot = { themes, playlists, schedules };
    setLibrarySaving(true);
    saveChain.current = saveChain.current.then(async () => {
      try {
        const stored = await saveLibrary(snapshot);
        saveFailed.current = false;
        const mapped = new Map(stored.map(theme => [theme.id, theme]));
        setThemes(current => {
          let changed = false;
          const next = current.map(theme => {
            const original = snapshot.themes.find(item => item.id === theme.id);
            const saved = mapped.get(theme.id);
            if (!saved || !original || theme.imageUrl !== original.imageUrl ||
                saved.imageUrl === theme.imageUrl) return theme;
            changed = true;
            return { ...theme, imageUrl: saved.imageUrl, thumbnailUrl: saved.thumbnailUrl,
              sourceWidth: saved.sourceWidth, sourceHeight: saved.sourceHeight, resolution: saved.resolution };
          });
          return changed ? next : current;
        });
        if (version === saveVersion.current) setLibraryError('');
      } catch (error) {
        saveFailed.current = true;
        setLibraryError('资源库尚未保存：' + (error as Error).message);
      } finally {
        if (version === saveVersion.current) setLibrarySaving(false);
      }
    });
  }, [libraryReady, themes, playlists, schedules, libraryRetry]);
  const handleRestoreThemes = useCallback((ids: string[]) => {
    const selected = new Set(ids);
    setThemes(previous => previous.map(theme => selected.has(theme.id)
      ? { ...theme, deletedAt: undefined, inLibrary: true } : theme));
    notify('已从回收站恢复素材及其原有引用');
  }, [notify]);
  const applyThemeInScope = useCallback(
    (theme: ThemeItem, scopeKey: string, silent?: boolean) => {
      setActiveThemeByScope((prev) => ({ ...prev, [scopeKey]: theme.id }));
      setIsPlayingByScope((prev) => ({ ...prev, [scopeKey]: true }));
      setThemes((prev) =>
        prev.map((t) =>
          t.id === theme.id
            ? {
                ...t,
                inLibrary: true,
                isRecent: true,
                useCount: (t.useCount ?? 0) + 1,
                lastUsedAt: Date.now(),
              }
            : t
        )
      );
      if (!silent) {
        notify(`已将「${theme.displayName}」应用到 ${activeDevice.name}`);
      }
    },
    [activeDevice.name, notify]
  );

  const handleApplyTheme = useCallback(
    (theme: ThemeItem) => {
      if (theme.deletedAt) { notify('请先从回收站恢复这项素材。'); return; }
      if (desktopMode) {
        if (!activeDevice.serial) { notify('请先连接并选择一台 MiraBox。'); return; }
        if (theme.category === '视频' || theme.editorProject || theme.category === '主题' || theme.category === '动态时钟') {
          void (async () => {
            await saveChain.current;
            if (saveFailed.current) throw new Error('素材尚未保存，请先重试保存。');
            await applyMedia(activeDevice.serial, theme.id);
            await refreshDesktop();
            applyThemeInScope(theme, currentScopeKey);
          })().catch(error => notify(error.message));
          return;
        }        if (!theme.imageUrl) { notify('当前实机版本支持带图片的内容，请选择一张壁纸。'); return; }
        notify('正在发送到 ' + activeDevice.name);
        void outputImage(activeDevice, theme.imageUrl, theme.id, theme.displayName).then(async () => {
          await refreshDesktop();
          applyThemeInScope(theme, currentScopeKey);
        }).catch(error => notify(error.message));
        return;
      }
      setTempPreview(null);
      const activePlId = playingPlaylistByScope[currentScopeKey];
      const activePl = activePlId ? playlists.find((p) => p.id === activePlId) : null;
      const isInsidePlayingPlaylist = Boolean(
        activePl?.items.some((item) => item.themeId === theme.id)
      );
      if (activePl && !isInsidePlayingPlaylist) {
        setPlayingPlaylistByScope((prev) => ({ ...prev, [currentScopeKey]: null }));
      }
      applyThemeInScope(theme, currentScopeKey);
    },
    [applyThemeInScope, currentScopeKey, playlists, playingPlaylistByScope, activeDevice, notify, refreshDesktop]
  );

  const handleTogglePlayCurrentScope = useCallback(() => {
    if (desktopMode) {
      if (activeDevice.standby) { void devicePower(activeDevice.serial, { action: 'wake' }).then(refreshDesktop).catch(error => notify(error.message)); return; }
      void configureDevice(activeDevice.serial, { playing: !activeDevice.wantsPlaying })
        .then(refreshDesktop).catch(error => notify(error.message));
      return;
    }    setIsPlayingByScope((prev) => {
      const next = !(prev[currentScopeKey] ?? true);
      notify(next ? '已恢复副屏实时输出' : '已暂停副屏输出');
      return { ...prev, [currentScopeKey]: next };
    });
  }, [currentScopeKey, notify, activeDevice, refreshDesktop]);

  const handleToggleFavorite = useCallback(
    (id: string) => {
      setThemes((prev) =>
        prev.map((t) => {
          if (t.id !== id) return t;
          const nextFav = !t.isFavorite;
          notify(nextFav ? `已收藏「${t.displayName}」` : `已取消收藏「${t.displayName}」`);
          return { ...t, isFavorite: nextFav };
        })
      );
    },
    [notify]
  );

  const handleAddItemsToPlaylist = useCallback(
    (playlistId: string, themeIds: string[]) => {
      const target = playlists.find((p) => p.id === playlistId);
      if (!target || themeIds.length === 0) return;

      const existingIds = new Set(target.items.map((i) => i.themeId));
      const newIds = themeIds.filter((id) => !existingIds.has(id));
      const skippedCount = themeIds.length - newIds.length;

      if (newIds.length === 0) {
        notify(`所选内容均已在「${target.name}」中，已跳过重复项`);
        return;
      }

      const previousItems = target.items;
      setPlaylists((prev) =>
        prev.map((p) =>
          p.id === playlistId
            ? {
                ...p,
                items: [
                  ...p.items,
                  ...newIds.map((themeId) => ({ themeId, durationSeconds: 30 })),
                ],
              }
            : p
        )
      );

      const skippedSuffix = skippedCount > 0 ? `（跳过 ${skippedCount} 项重复）` : '';
      const label =
        newIds.length === 1
          ? `已将「${themes.find((t) => t.id === newIds[0])?.displayName ?? '素材'}」加入「${target.name}」`
          : `已向「${target.name}」批量加入 ${newIds.length} 项内容${skippedSuffix}`;

      notify(label, {
        durationMs: 5000,
        onUndo: () => {
          setPlaylists((prev) =>
            prev.map((p) => (p.id === playlistId ? { ...p, items: previousItems } : p))
          );
          notify(`已撤销向「${target.name}」添加内容`);
        },
      });
    },
    [playlists, themes, notify]
  );

  const createDefaultAndAddItems = useCallback(
    (themeIds: string[]) => {
      const uniqueIds = Array.from(new Set(themeIds));
      if (uniqueIds.length === 0) return;
      const newPlId = `pl-${Date.now()}`;
      const oriLabel = activeDevice.orientation === 'portrait' ? '竖屏' : '横屏';
      const created: CarouselPlaylist = {
        id: newPlId,
        name: `${oriLabel}默认轮播`,
        deviceId: activeDevice.id,
        orientation: activeDevice.orientation,
        mood: '专注',
        shuffle: false,
        defaultDurationSeconds: 30,
        items: uniqueIds.map((themeId) => ({ themeId, durationSeconds: 30 })),
      };
      setPlaylists((prev) => [...prev, created]);
      setActivePlaylistByScope((prev) => ({ ...prev, [currentScopeKey]: newPlId }));

      notify(
        `已为当前设备（${oriLabel}）自动创建「${created.name}」并加入 ${uniqueIds.length} 项内容`,
        {
          durationMs: 5000,
          onUndo: () => {
            setPlaylists((prev) => prev.filter((p) => p.id !== newPlId));
            notify(`已撤销创建「${created.name}」`);
          },
        }
      );
    },
    [activeDevice.id, activeDevice.orientation, currentScopeKey, notify]
  );

  const handleTogglePlaylist = useCallback(
    (id: string) => {
      if (!activeScopedPlaylist) {
        createDefaultAndAddItems([id]);
        return;
      }
      const theme = themes.find((t) => t.id === id);
      const exists = activeScopedPlaylist.items.some((i) => i.themeId === id);

      if (exists) {
        const remainingCount = activeScopedPlaylist.items.filter((i) => i.themeId !== id).length;
        setPlaylists((prev) =>
          prev.map((p) =>
            p.id === activeScopedPlaylist.id
              ? { ...p, items: p.items.filter((i) => i.themeId !== id) }
              : p
          )
        );
        if (remainingCount === 0 && playingPlaylistByScope[currentScopeKey] === activeScopedPlaylist.id) {
          setPlayingPlaylistByScope((prev) => ({ ...prev, [currentScopeKey]: null }));
        }
        notify(`已从「${activeScopedPlaylist.name}」移出「${theme?.displayName ?? '内容'}」`);
      } else {
        handleAddItemsToPlaylist(activeScopedPlaylist.id, [id]);
      }
    },
    [
      activeScopedPlaylist,
      currentScopeKey,
      themes,
      playingPlaylistByScope,
      handleAddItemsToPlaylist,
      createDefaultAndAddItems,
      notify,
    ]
  );

  const handleBatchAddToTargetPlaylist = useCallback(
    (playlistId: string | null, themeIds: string[]) => {
      if (themeIds.length === 0) return;
      if (!playlistId) {
        createDefaultAndAddItems(themeIds);
        return;
      }
      handleAddItemsToPlaylist(playlistId, themeIds);
    },
    [createDefaultAndAddItems, handleAddItemsToPlaylist]
  );

  const handleImportLocalThemes = useCallback((items: ThemeItem[]) => {
    const hashes = new Set(themes.filter(t => !t.deletedAt).map(t => t.contentHash).filter(Boolean));
    const unique = items.filter(item => {
      if (item.contentHash && hashes.has(item.contentHash)) return false;
      if (item.contentHash) hashes.add(item.contentHash);
      return true;
    });
    if (unique.length) setThemes(previous => [...unique, ...previous]);
    notify('已加入 ' + unique.length + ' 项素材，跳过 ' + (items.length - unique.length) + ' 项重复图片；正在保存');
  }, [themes, notify]);
  const handleRenameTheme = useCallback(
    (id: string, nextName: string) => {
      setThemes((prev) =>
        prev.map((t) =>
          t.id === id ? { ...t, displayName: nextName, name: nextName } : t
        )
      );
      notify(`已将素材重命名为「${nextName}」`);
    },
    [notify]
  );

  const handleAcquireToLibrary = useCallback(
    (item: ThemeItem) => {
      if (item.inLibrary !== false) {
        notify(`「${item.displayName}」已在我的资源库中`);
        return;
      }
      const now = Date.now();
      setThemes((prev) =>
        prev.map((t) =>
          t.id === item.id
            ? { ...t, inLibrary: true, addedAt: now, addedLabel: '刚刚下载' }
            : t
        )
      );
      notify(`已将「${item.displayName}」下载到我的资源库`);
    },
    [notify]
  );

  const handleBatchFavorite = useCallback(
    (ids: string[]) => {
      if (ids.length === 0) return;
      const idSet = new Set(ids);
      const allAlreadyFav = themes
        .filter((t) => idSet.has(t.id))
        .every((t) => t.isFavorite);
      const targetFav = !allAlreadyFav;
      setThemes((prev) =>
        prev.map((t) => (idSet.has(t.id) ? { ...t, isFavorite: targetFav } : t))
      );
      notify(
        targetFav
          ? `已将选中的 ${ids.length} 项素材加入收藏`
          : `已取消选中 ${ids.length} 项素材的收藏`
      );
    },
    [themes, notify]
  );

  const handleRemoveFromLibrary = useCallback(
    (ids: string[]) => {
      if (ids.length === 0) return;
      if (desktopMode) {
        const removed = new Set(ids);
        setThemes(previous => previous.map(theme => removed.has(theme.id)
          ? { ...theme, deletedAt: Date.now(), inLibrary: false } : theme));
        notify('已移入回收站。当前屏幕画面保留，恢复后可继续使用原有引用。', {
          onUndo: () => handleRestoreThemes(ids),
        });
        return;
      }      const idSet = new Set(ids);
      const prevThemes = themes;
      const prevPlaylists = playlists;
      const prevActiveByScope = activeThemeByScope;
      const prevPlayingByScope = playingPlaylistByScope;

      const remainingThemes = themes.filter((t) => {
        if (!idSet.has(t.id)) return true;
        // Official themes stay in Online Plaza with inLibrary=false; local/custom items are removed
        return (t.source ?? 'official') === 'official';
      });

      const updatedThemes = remainingThemes.map((t) =>
        idSet.has(t.id) ? { ...t, inLibrary: false } : t
      );
      const fallbackTheme =
        updatedThemes.find((t) => t.inLibrary !== false) ?? updatedThemes[0];

      setThemes(updatedThemes);

      const updatedPlaylists = playlists.map((pl) => ({
        ...pl,
        items: pl.items.filter((entry) => !idSet.has(entry.themeId)),
      }));
      setPlaylists(updatedPlaylists);

      setPlayingPlaylistByScope((prev) => {
        const next = { ...prev };
        for (const [scope, plId] of Object.entries(next)) {
          if (!plId) continue;
          const pl = updatedPlaylists.find((p) => p.id === plId);
          if (!pl || pl.items.length === 0) next[scope] = null;
        }
        return next;
      });

      if (fallbackTheme) {
        setActiveThemeByScope((prev) => {
          const next = { ...prev };
          for (const [scope, themeId] of Object.entries(next)) {
            if (idSet.has(themeId)) {
              next[scope] = fallbackTheme.id;
            }
          }
          return next;
        });
      }

      const firstTitle = themes.find((t) => t.id === ids[0])?.displayName ?? '素材';
      const label =
        ids.length === 1
          ? `已将「${firstTitle}」移出我的资源库`
          : `已将 ${ids.length} 项素材移出我的资源库`;

      notify(label, {
        durationMs: 5000,
        onUndo: () => {
          setThemes(prevThemes);
          setPlaylists(prevPlaylists);
          setActiveThemeByScope(prevActiveByScope);
          setPlayingPlaylistByScope(prevPlayingByScope);
          notify('已撤销移出资源库操作');
        },
      });
    },
    [themes, playlists, activeThemeByScope, playingPlaylistByScope, notify, handleRestoreThemes]
  );

  const handleCreatePlaylist = useCallback(
    (payload: {
      name: string;
      mood: MoodTag;
      deviceId: string;
      orientation: ScreenOrientation;
    }) => {
      const next: CarouselPlaylist = {
        id: `pl-${Date.now()}`,
        name: payload.name,
        deviceId: payload.deviceId,
        orientation: payload.orientation,
        mood: payload.mood,
        shuffle: false,
        defaultDurationSeconds: 30,
        items: [],
      };
      const scope = `${payload.deviceId}:${payload.orientation}`;
      setPlaylists((prev) => [...prev, next]);
      setActivePlaylistByScope((prev) => ({ ...prev, [scope]: next.id }));
      const dev = devices.find((d) => d.id === payload.deviceId);
      const oriLabel = payload.orientation === 'portrait' ? '竖屏' : '横屏';
      notify(`已新建${oriLabel}轮播列表「${next.name}」(${dev?.name.split('·')[0].trim() ?? ''})`);
    },
    [devices, notify]
  );

  const handleUpdatePlaylist = useCallback(
    (playlistId: string, patch: Partial<CarouselPlaylist>, notifyMsg?: string) => {
      const prevTarget = playlists.find((p) => p.id === playlistId);
      setPlaylists((prev) =>
        prev.map((p) => (p.id === playlistId ? { ...p, ...patch } : p))
      );
      // If deviceId or orientation ever changes via patch, clean up stale playing pointer in old scope
      if (
        prevTarget &&
        ((patch.deviceId && patch.deviceId !== prevTarget.deviceId) ||
          (patch.orientation && patch.orientation !== prevTarget.orientation))
      ) {
        const oldScope = `${prevTarget.deviceId}:${prevTarget.orientation}`;
        setPlayingPlaylistByScope((prev) =>
          prev[oldScope] === playlistId ? { ...prev, [oldScope]: null } : prev
        );
      }
      if (notifyMsg) notify(notifyMsg);
    },
    [playlists, notify]
  );

  const handleDeletePlaylist = useCallback(
    (playlistId: string) => {
      const target = playlists.find((p) => p.id === playlistId);
      const remaining = playlists.filter((p) => p.id !== playlistId);
      setPlaylists(remaining);

      setPlayingPlaylistByScope((prev) => {
        const next = { ...prev };
        Object.keys(next).forEach((scope) => {
          if (next[scope] === playlistId) next[scope] = null;
        });
        return next;
      });

      setSchedules((prev) =>
        prev.map((rule) =>
          rule.playlistId === playlistId
            ? {
                ...rule,
                playlistDeleted: true,
                playlistNameSnapshot: target?.name ?? rule.playlistNameSnapshot,
              }
            : rule
        )
      );

      if (target) {
        const scope = `${target.deviceId}:${target.orientation}`;
        const nextInScope = remaining.find(
          (p) => p.deviceId === target.deviceId && p.orientation === target.orientation
        );
        setActivePlaylistByScope((prev) =>
          prev[scope] === playlistId ? { ...prev, [scope]: nextInScope?.id ?? '' } : prev
        );
        notify(`已删除轮播列表「${target.name}」，素材已保留在资源库`);
      }
    },
    [playlists, notify]
  );

  const handleTogglePlayPlaylist = useCallback(
    (playlistId: string) => {
      if (desktopMode) {
        const target = playlists.find(p => p.id === playlistId);
        if (!target) return;
        const device = devices.find(d => d.id === target.deviceId);
        const command = device?.playlistId === playlistId && device.playlistRunning ? 'playlist-pause' : 'playlist-start';
        void (async () => {
          await saveChain.current;
          if (saveFailed.current) throw new Error('请先重试保存列表，再开始轮播。');
          await playlistCommand(command, target.deviceId, playlistId);
          await refreshDesktop();
          notify(command === 'playlist-pause' ? '轮播已暂停，当前画面保留' : '已开始轮播「' + target.name + '」');
        })().catch(error => notify(error.message));
        return;
      }      const pl = playlists.find((p) => p.id === playlistId);
      if (!pl) return;
      if (pl.items.length === 0) {
        notify(`「${pl.name}」暂无内容，请先添加素材后再开始轮播`);
        return;
      }
      const scope = `${pl.deviceId}:${pl.orientation}`;
      const currentlyPlaying = playingPlaylistByScope[scope] === pl.id;
      setPlayingPlaylistByScope((prev) => ({
        ...prev,
        [scope]: currentlyPlaying ? null : pl.id,
      }));
      if (!currentlyPlaying) {
        const firstTheme = themes.find((t) => t.id === pl.items[0].themeId);
        if (firstTheme) {
          applyThemeInScope(firstTheme, scope, true);
        }
        const devName = devices.find((d) => d.id === pl.deviceId)?.name ?? '副屏';
        const oriLabel = pl.orientation === 'portrait' ? '竖屏' : '横屏';
        notify(`已开始在 ${devName}（${oriLabel}）轮播「${pl.name}」`);
      } else {
        notify(`已停止轮播「${pl.name}」`);
      }
    },
    [playlists, playingPlaylistByScope, themes, applyThemeInScope, devices, notify, refreshDesktop]
  );

  const handleRemoveItemFromPlaylist = useCallback(
    (playlistId: string, themeId: string) => {
      const pl = playlists.find((p) => p.id === playlistId);
      const theme = themes.find((t) => t.id === themeId);
      const remainingCount = pl ? pl.items.filter((i) => i.themeId !== themeId).length : 0;
      setPlaylists((prev) =>
        prev.map((p) =>
          p.id === playlistId
            ? { ...p, items: p.items.filter((i) => i.themeId !== themeId) }
            : p
        )
      );
      if (pl && remainingCount === 0) {
        const scope = `${pl.deviceId}:${pl.orientation}`;
        setPlayingPlaylistByScope((prev) =>
          prev[scope] === playlistId ? { ...prev, [scope]: null } : prev
        );
      }
      notify(`已从「${pl?.name ?? '列表'}」移除「${theme?.displayName ?? '条目'}」（保留在资源库）`);
    },
    [playlists, themes, notify]
  );

  const handleReorderPlaylistItems = useCallback(
    (playlistId: string, fromIndex: number, toIndex: number) => {
      setPlaylists((prev) =>
        prev.map((p) => {
          if (p.id !== playlistId) return p;
          if (
            fromIndex < 0 ||
            toIndex < 0 ||
            fromIndex >= p.items.length ||
            toIndex >= p.items.length
          ) {
            return p;
          }
          const copy = [...p.items];
          const [moved] = copy.splice(fromIndex, 1);
          copy.splice(toIndex, 0, moved);
          return { ...p, items: copy };
        })
      );
    },
    []
  );

  const handleApplyAllDuration = useCallback(
    (playlistId: string, seconds: number) => {
      const clamped = Math.min(86400, Math.max(10, Math.round(seconds)));
      setPlaylists((prev) =>
        prev.map((p) =>
          p.id === playlistId
            ? {
                ...p,
                defaultDurationSeconds: clamped,
                items: p.items.map((item) => ({ ...item, durationSeconds: clamped })),
              }
            : p
        )
      );
      notify(`已将所有条目的停留时间统一设为 ${clamped} 秒`);
    },
    [notify]
  );

  const handleApplyItemDuration = useCallback(
    (playlistId: string, themeId: string, seconds: number) => {
      const clamped = Math.min(86400, Math.max(10, Math.round(seconds)));
      setPlaylists((prev) =>
        prev.map((p) =>
          p.id === playlistId
            ? {
                ...p,
                items: p.items.map((item) =>
                  item.themeId === themeId ? { ...item, durationSeconds: clamped } : item
                ),
              }
            : p
        )
      );
    },
    []
  );

  const handleOrientationChange = useCallback(
    (orientation: ScreenOrientation) => {
      if (desktopMode) {
        void configureDevice(activeDevice.serial, { portrait: orientation === 'portrait' })
          .then(refreshDesktop).catch(error => notify(error.message));
        return;
      }      const nextRes =
        orientation === 'landscape'
          ? `${activeDevice.nativeWidth} × ${activeDevice.nativeHeight}`
          : `${activeDevice.nativeHeight} × ${activeDevice.nativeWidth}`;
      setDevices((prev) =>
        prev.map((d) =>
          d.id === activeDevice.id
            ? {
                ...d,
                orientation,
                resolution:
                  orientation === 'landscape'
                    ? `${d.nativeWidth} × ${d.nativeHeight}`
                    : `${d.nativeHeight} × ${d.nativeWidth}`,
              }
            : d
        )
      );
      notify(
        `已切换 ${activeDevice.name} 为${orientation === 'landscape' ? '横屏' : '竖屏'} (${nextRes})`
      );
    },
    [
      activeDevice.id,
      activeDevice.name,
      activeDevice.nativeWidth,
      activeDevice.nativeHeight,
      notify,
    ]
  );

  const handleToggleFlip = useCallback(() => {
    if (desktopMode) {
      void configureDevice(activeDevice.serial, { flipped: !activeDevice.flipped })
        .then(refreshDesktop).catch(error => notify(error.message));
      return;
    }    const nextFlipped = !activeDevice.flipped;
    setDevices((prev) =>
      prev.map((d) =>
        d.id === activeDevice.id ? { ...d, flipped: !d.flipped } : d
      )
    );
    notify(
      nextFlipped
        ? `已开启 ${activeDevice.name} 设备画面 180° 旋转`
        : `已关闭 ${activeDevice.name} 设备画面 180° 旋转`
    );
  }, [activeDevice, refreshDesktop, notify]);

  const stepWithinPlaylistOrThemes = useCallback(
    (direction: 1 | -1, silent?: boolean) => {
      if (desktopMode) {
        if (activeDevice.playlistId) {
          void playlistCommand('playlist-step', activeDevice.serial, activeDevice.playlistId, direction)
            .then(refreshDesktop).catch(error => notify(error.message));
          return;
        }
        const available = themes.filter(theme => !theme.deletedAt && theme.inLibrary !== false && (theme.imageUrl || theme.editorProject || theme.category === '动态时钟' || theme.category === '主题'));
        const index = available.findIndex(theme => theme.id === activeDevice.themeId);
        const target = available[(index + direction + available.length) % available.length];
        if (target) handleApplyTheme(target);
        return;
      }      if (activeScopedPlaylist && activeScopedPlaylist.items.length > 0) {
        const items = activeScopedPlaylist.items;
        if (activeScopedPlaylist.shuffle && items.length > 1) {
          const candidates = items.filter((item) => item.themeId !== activeTheme.id);
          const picked = candidates[Math.floor(Math.random() * candidates.length)];
          const pickedTheme = themes.find((t) => t.id === picked?.themeId);
          if (pickedTheme) {
            applyThemeInScope(pickedTheme, currentScopeKey, silent);
            return;
          }
        }
        const currentIdx = items.findIndex((i) => i.themeId === activeTheme.id);
        let nextIdx = 0;
        if (currentIdx === -1) {
          nextIdx = direction === 1 ? 0 : items.length - 1;
        } else {
          nextIdx = (currentIdx + direction + items.length) % items.length;
        }
        const nextTheme = themes.find((t) => t.id === items[nextIdx].themeId);
        if (nextTheme) {
          applyThemeInScope(nextTheme, currentScopeKey, silent);
          return;
        }
      }

      const idx = themes.findIndex((t) => t.id === activeTheme.id);
      const targetIdx = (idx + direction + themes.length) % themes.length;
      applyThemeInScope(themes[targetIdx], currentScopeKey, silent);
    },
    [activeScopedPlaylist, activeTheme.id, themes, applyThemeInScope, currentScopeKey, activeDevice, handleApplyTheme, refreshDesktop, notify]
  );

  const handlePrevTheme = useCallback(() => {
    stepWithinPlaylistOrThemes(-1);
  }, [stepWithinPlaylistOrThemes]);

  const handleNextTheme = useCallback(() => {
    stepWithinPlaylistOrThemes(1);
  }, [stepWithinPlaylistOrThemes]);

  // Real auto-advance carousel timer with live remaining seconds countdown
  const autoAdvanceRef = useRef<() => void>(() => {});
  autoAdvanceRef.current = () => {
    stepWithinPlaylistOrThemes(1, true);
  };

  const activePlayingPlaylist = useMemo(() => {
    if (!playingPlaylistId) return null;
    return scopedPlaylists.find((p) => p.id === playingPlaylistId) ?? null;
  }, [playingPlaylistId, scopedPlaylists]);

  const currentEntryDuration = useMemo(() => {
    if (!activePlayingPlaylist || activePlayingPlaylist.items.length < 2) return null;
    const entry =
      activePlayingPlaylist.items.find((i) => i.themeId === activeTheme.id) ??
      activePlayingPlaylist.items[0];
    return entry ? Math.min(86400, Math.max(10, entry.durationSeconds)) : null;
  }, [activePlayingPlaylist, activeTheme.id]);

  useEffect(() => {
    if (desktopMode) return;
    if (!isPlaying || !activePlayingPlaylist || currentEntryDuration === null) {
      setCountdownSeconds(null);
      return;
    }

    setCountdownSeconds(currentEntryDuration);
    const timer = window.setInterval(() => {
      setCountdownSeconds((prev) => {
        if (prev === null) return null;
        if (prev <= 1) {
          window.setTimeout(() => autoAdvanceRef.current(), 0);
          return currentEntryDuration;
        }
        return prev - 1;
      });
    }, 1000);

    return () => window.clearInterval(timer);
  }, [
    isPlaying,
    currentScopeKey,
    activePlayingPlaylist?.id,
    activeTheme.id,
    currentEntryDuration,
  ]);

  const handleSelectDevice = useCallback(
    (deviceId: string) => {
      setActiveDeviceId(deviceId);
      if (desktopMode) void selectDesktopDevice(deviceId).catch(error => notify(error.message));
      const dev = devices.find((d) => d.id === deviceId);
      if (dev) {
        notify(`已切换控制设备：${dev.name}`);
      }
    },
    [devices, notify]
  );

  const handleUpdateDevice = useCallback(
    (deviceId: string, patch: Partial<DeviceInfo>) => {
      if (desktopMode) {
        void (async () => {
          if (patch.orientation !== undefined || patch.flipped !== undefined)
            await configureDevice(deviceId, { portrait: patch.orientation === undefined ? undefined : patch.orientation === 'portrait', flipped: patch.flipped });
          if (patch.brightness !== undefined || patch.sleepMinutes !== undefined)
            await devicePower(deviceId, { brightness: patch.brightness, sleepMinutes: patch.sleepMinutes });
          await refreshDesktop();
        })().catch(error => notify(error.message));
        return;
      }
      setDevices((prev) =>
        prev.map((d) => {
          if (d.id !== deviceId) return d;
          const nextOrientation = patch.orientation ?? d.orientation;
          const nextResolution =
            patch.orientation !== undefined
              ? nextOrientation === 'landscape'
                ? `${d.nativeWidth} × ${d.nativeHeight}`
                : `${d.nativeHeight} × ${d.nativeWidth}`
              : d.resolution;
          return { ...d, ...patch, resolution: nextResolution };
        })
      );
    },
    [refreshDesktop, notify]
  );

  const handleSaveEditorTheme = useCallback(
    (savedTheme: ThemeItem, applyNow: boolean) => {
        if (desktopMode) {
          return (async () => {
            await saveChain.current;
            if (saveFailed.current) throw new Error('资源库尚未保存，请先重试保存。');
            const nextThemes = themes.some(t => t.id === savedTheme.id)
              ? themes.map(t => t.id === savedTheme.id ? { ...t, ...savedTheme } : t) : [savedTheme, ...themes];
            const stored = await saveLibrary({ themes: nextThemes, playlists, schedules });
            setThemes(stored);
            const item = stored.find(t => t.id === savedTheme.id)!;
            setEditorSession(curr => curr && item.editorProject ? { project: item.editorProject, sourceThemeId: item.id } : curr);
            if (applyNow) {
              if (!activeDevice.serial) throw new Error('主题已保存，请连接并选择 MiraBox 后再应用。');
              await applyMedia(activeDevice.serial, item.id);
              setTempPreview(null);
              await refreshDesktop();
              notify(`已保存「${item.displayName}」并输出至 ${activeDevice.name}`);
            } else notify(`已保存「${item.displayName}」到我的资源库`);
            return true;
          })().catch(error => { notify(error.message); return false; });
        }
      setThemes((prev) => {
        const idx = prev.findIndex((t) => t.id === savedTheme.id);
        if (idx >= 0) {
          const copy = [...prev];
          copy[idx] = { ...prev[idx], ...savedTheme };
          return copy;
        }
        return [savedTheme, ...prev];
      });
      setEditorSession((curr) =>
        curr && savedTheme.editorProject
          ? { project: savedTheme.editorProject, sourceThemeId: savedTheme.id }
          : curr
      );
      if (applyNow) {
        setTempPreview(null);
        setPlayingPlaylistByScope((prev) => ({ ...prev, [currentScopeKey]: null }));
        applyThemeInScope(savedTheme, currentScopeKey, true);
        notify(`已保存「${savedTheme.displayName}」并输出至 ${activeDevice.name}`);
      } else {
        notify(`已保存「${savedTheme.displayName}」到我的资源库（当前设备输出未改变）`);
      }
    },
      [activeDevice.name, activeDevice.serial, applyThemeInScope, currentScopeKey, notify, themes, playlists, schedules, refreshDesktop]
  );

  const handleStartTempPreview = useCallback(
    (previewTheme: ThemeItem) => {
        if (desktopMode) {
          if (!activeDevice.serial) { notify('请先连接并选择一台 MiraBox。'); return; }
          void previewDesktopTheme(activeDevice.serial, previewTheme).then(async () => {
            setTempPreview({ scopeKey: currentScopeKey, prevThemeId: activeTheme.id, prevPlayingPlaylistId: null,
              prevIsPlaying: Boolean(activeDevice.playing), previewTheme, remainingSec: 15 });
            await refreshDesktop();
            notify('已开始 15 秒实机预览，结束后自动恢复原内容。');
          }).catch(error => notify(error.message));
          return;
        }
      const prevThemeId = activeThemeByScope[currentScopeKey] ?? activeTheme.id;
      const prevPlayingPlaylistId = playingPlaylistByScope[currentScopeKey] ?? null;
      const prevIsPlaying = isPlayingByScope[currentScopeKey] ?? true;
      setPlayingPlaylistByScope((prev) => ({ ...prev, [currentScopeKey]: null }));
      setIsPlayingByScope((prev) => ({ ...prev, [currentScopeKey]: true }));
      setTempPreview({
        scopeKey: currentScopeKey,
        prevThemeId,
        prevPlayingPlaylistId,
        prevIsPlaying,
        previewTheme,
        remainingSec: 15,
      });
      notify(`已在 ${activeDevice.name} 开启 15 秒临时预览（未污染资源库，结束自动恢复原内容）`);
    },
    [
      activeDevice.name,
      activeTheme.id,
      activeThemeByScope,
      currentScopeKey,
      isPlayingByScope,
      notify,
        playingPlaylistByScope, activeDevice.serial, activeDevice.playing, refreshDesktop,
    ]
  );

  const stopTempPreviewAndRestore = useCallback(
    (silent?: boolean) => {
        if (desktopMode) {
          const serial = tempPreview?.scopeKey.split(':')[0] ?? activeDevice.serial;
          setTempPreview(null);
          if (serial) void endThemePreview(serial).then(refreshDesktop).catch(error => notify(error.message));
          if (!silent) notify('临时设备预览已结束，已恢复原内容。');
          return;
        }
      setTempPreview((curr) => {
        if (!curr) return null;
        setActiveThemeByScope((prev) => ({ ...prev, [curr.scopeKey]: curr.prevThemeId }));
        setPlayingPlaylistByScope((prev) => ({
          ...prev,
          [curr.scopeKey]: curr.prevPlayingPlaylistId,
        }));
        setIsPlayingByScope((prev) => ({ ...prev, [curr.scopeKey]: curr.prevIsPlaying }));
        if (!silent) notify('临时设备预览已结束，已自动恢复设备原有主题播放');
        return null;
      });
    },
      [notify, tempPreview?.scopeKey, activeDevice.serial, refreshDesktop]
  );

  useEffect(() => {
    if (!tempPreview) return;
    const t = window.setInterval(() => {
      setTempPreview((curr) => {
        if (!curr) return null;
        if (curr.remainingSec <= 1) {
          window.setTimeout(() => stopTempPreviewAndRestore(false), 0);
          return null;
        }
        return { ...curr, remainingSec: curr.remainingSec - 1 };
      });
    }, 1000);
    return () => window.clearInterval(t);
  }, [Boolean(tempPreview), stopTempPreviewAndRestore]);

  const handleDuplicateCustomTheme = useCallback(
    (item: ThemeItem) => {
      const now = Date.now();
      const copyId = `custom-${genEditorId('dup')}`;
      const clonedProject = item.editorProject
        ? {
            ...structuredClone(item.editorProject),
            template_id: copyId,
            name: `${item.displayName} (副本)`,
            updated_at: now,
          }
        : undefined;
      const duplicated: ThemeItem = {
        ...item,
        id: copyId,
        name: `${item.displayName} (副本)`,
        displayName: `${item.displayName} (副本)`,
        source: 'custom',
        inLibrary: true,
        addedAt: now,
        addedLabel: '刚刚复制',
        lastUsedAt: now,
        editorProject: clonedProject,
      };
      setThemes((prev) => [duplicated, ...prev]);
      notify(`已创建副本「${duplicated.displayName}」`);
    },
    [notify]
  );

  const handleEditInStudio = useCallback(
    (theme: ThemeItem) => {
      const proj = ensureEditorProjectFromTheme(theme, activeDevice);
      setEditorSession({ project: proj, sourceThemeId: theme.id });
      setActiveNav('creator');
    },
    [activeDevice]
  );

  const handleToggleGlobalSchedules = useCallback(() => {
    if (desktopMode) {
      void enableSchedules(!schedulesEnabled).then(refreshDesktop).catch(error => notify(error.message));
      return;
    }
    setSchedulesEnabled((prev) => {
      const next = !prev;
      notify(next ? '已恢复全部定时切换调度' : '已全局暂停定时切换调度（休假模式）');
      return next;
    });
  }, [schedulesEnabled, refreshDesktop, notify]);

  const handleToggleSchedule = useCallback(
    (id: string) => {
      if (desktopMode && scheduleRuntime?.completed.includes(id)) {
        void (async () => {
          await resetSchedule(id);
          setSchedules(previous => previous.map(rule => rule.id === id ? { ...rule, enabled: true } : rule));
          await refreshDesktop();
        })().catch(error => notify(error.message));
        return;
      }
      setSchedules((prev) =>
        prev.map((s) => (s.id === id ? { ...s, enabled: !s.enabled } : s))
      );
    },
    [scheduleRuntime, refreshDesktop, notify]
  );

  const handleAddSchedule = useCallback(
    (rule: ScheduleRule) => {
      setSchedules((prev) => [...prev, rule]);
      notify(`已添加定时任务「${rule.title}」(${rule.time})`);
    },
    [notify]
  );

  const handleUpdateSchedule = useCallback(
    (id: string, patch: Partial<ScheduleRule>, notifyMsg?: string) => {
      if (desktopMode && patch.enabled && scheduleRuntime?.completed.includes(id)) {
        void resetSchedule(id).then(refreshDesktop).catch(error => notify(error.message));
      }      setSchedules((prev) =>
        prev.map((s) => (s.id === id ? { ...s, ...patch } : s))
      );
      if (notifyMsg) notify(notifyMsg);
    },
    [notify, scheduleRuntime, refreshDesktop]
  );

  const handleDeleteSchedule = useCallback(
    (id: string) => {
      const targetIndex = schedules.findIndex((s) => s.id === id);
      const target = schedules[targetIndex];
      if (!target) return;
      setSchedules((prev) => prev.filter((s) => s.id !== id));
      notify(`已删除定时规则「${target.title}」`, {
        durationMs: 5000,
        onUndo: () => {
          setSchedules((prev) => {
            if (prev.some((s) => s.id === target.id)) return prev;
            const copy = [...prev];
            copy.splice(Math.min(targetIndex, copy.length), 0, target);
            return copy;
          });
          notify(`已撤销删除「${target.title}」`);
        },
      });
    },
    [schedules, notify]
  );

  const executeScheduleRule = useCallback(
    (rule: ScheduleRule, opts?: { manual?: boolean }) => {
      const scope = `${rule.deviceId}:${rule.orientation}`;
      const devShort =
        devices.find((d) => d.id === rule.deviceId)?.name.split('·')[0].trim() ?? '副屏';
      const oriLabel = rule.orientation === 'portrait' ? '竖屏' : '横屏';
      const actionVerb = opts?.manual ? '试运行规则' : '定时触发';

      if (rule.actionType === 'sleep') {
        setIsPlayingByScope((prev) => ({ ...prev, [scope]: false }));
        notify(`已${actionVerb}「${rule.title}」：${devShort}（${oriLabel}）已息屏暂停`);
      } else if (rule.actionType === 'wake') {
        setIsPlayingByScope((prev) => ({ ...prev, [scope]: true }));
        if (typeof rule.targetBrightness === 'number') {
          setDevices((prev) =>
            prev.map((d) =>
              d.id === rule.deviceId ? { ...d, brightness: rule.targetBrightness! } : d
            )
          );
        }
        notify(`已${actionVerb}「${rule.title}」：${devShort}（${oriLabel}）已唤醒恢复输出`);
      } else if (rule.actionType === 'playlist' && !rule.playlistDeleted && rule.playlistId) {
        const pl = playlists.find((p) => p.id === rule.playlistId);
        if (pl && pl.items.length > 0) {
          setPlayingPlaylistByScope((prev) => ({ ...prev, [scope]: pl.id }));
          setActivePlaylistByScope((prev) => ({ ...prev, [scope]: pl.id }));
          const firstTheme = themes.find((t) => t.id === pl.items[0].themeId);
          if (firstTheme) {
            applyThemeInScope(firstTheme, scope, true);
          }
          if (typeof rule.targetBrightness === 'number') {
            setDevices((prev) =>
              prev.map((d) =>
                d.id === rule.deviceId ? { ...d, brightness: rule.targetBrightness! } : d
              )
            );
          }
          notify(
            `已${actionVerb}「${rule.title}」：${devShort}（${oriLabel}）开始轮播「${pl.name}」`
          );
        } else {
          const fallbackTheme = themes.find((t) => t.id === rule.themeId) ?? themes[0] ?? INITIAL_THEMES[0];
          setPlayingPlaylistByScope((prev) => ({ ...prev, [scope]: null }));
          if (fallbackTheme) applyThemeInScope(fallbackTheme, scope, true);
          notify(
            `已${actionVerb}「${rule.title}」：${devShort}（${oriLabel}）切换至「${fallbackTheme?.displayName ?? '默认主题'}」`
          );
        }
      } else {
        const targetTheme = themes.find((t) => t.id === rule.themeId) ?? themes[0] ?? INITIAL_THEMES[0];
        setPlayingPlaylistByScope((prev) => ({ ...prev, [scope]: null }));
        if (targetTheme) {
          applyThemeInScope(targetTheme, scope, true);
        }
        if (typeof rule.targetBrightness === 'number') {
          setDevices((prev) =>
            prev.map((d) =>
              d.id === rule.deviceId ? { ...d, brightness: rule.targetBrightness! } : d
            )
          );
        }
        notify(
          `已${actionVerb}「${rule.title}」：${devShort}（${oriLabel}）切换至「${targetTheme?.displayName ?? '默认主题'}」`
        );
      }

      if (!opts?.manual && rule.repeatMode === 'once') {
        setSchedules((prev) =>
          prev.map((s) => (s.id === rule.id ? { ...s, enabled: false } : s))
        );
      }
    },
    [devices, playlists, themes, applyThemeInScope, notify]
  );

  const handleRunScheduleNow = useCallback(
    (rule: ScheduleRule) => {
      if (desktopMode) {
        void (async () => {
          await saveChain.current;
          if (saveFailed.current) throw new Error('规则尚未保存，请先重试保存。');
          const runtime = await runSchedule(rule.id);
          setScheduleRuntime(runtime);
          await refreshDesktop();
          const result = runtime.history[0];
          notify(result ? result.status + '：' + result.reason : '执行记录已更新');
        })().catch(error => notify(error.message));
        return;
      }      executeScheduleRule(rule, { manual: true });
    },
    [executeScheduleRule, refreshDesktop, notify]
  );

  const executeScheduleRef = useRef(executeScheduleRule);
  executeScheduleRef.current = executeScheduleRule;

  useEffect(() => {
    if (desktopMode || !schedulesEnabled) return;

    const checkSchedules = () => {
      const now = new Date();
      const day = now.getDay();
      const dateStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(
        2,
        '0'
      )}-${String(now.getDate()).padStart(2, '0')}`;
      const timeStr = `${String(now.getHours()).padStart(2, '0')}:${String(
        now.getMinutes()
      ).padStart(2, '0')}`;
      const minuteKey = `${dateStr} ${timeStr}`;

      schedules.forEach((rule) => {
        if (!rule.enabled || rule.time !== timeStr) return;
        const matchesDay =
          rule.repeatMode === 'daily' ||
          rule.repeatMode === 'once' ||
          (rule.repeatMode === 'weekdays' && day >= 1 && day <= 5) ||
          (rule.repeatMode === 'weekends' && (day === 0 || day === 6)) ||
          (rule.repeatMode === 'custom' && Boolean(rule.customDays?.includes(day)));

        if (!matchesDay) return;
        if (triggeredScheduleRef.current[rule.id] === minuteKey) return;

        triggeredScheduleRef.current[rule.id] = minuteKey;
        executeScheduleRef.current(rule);
      });
    };

    const timer = window.setInterval(checkSchedules, 5000);
    return () => window.clearInterval(timer);
  }, [schedules, schedulesEnabled]);

  const enabledPlugins = useMemo(
    () => (desktopMode || pureMode ? [] : plugins.filter((p) => p.installed && p.enabled)),
    [plugins, pureMode]
  );

  useEffect(() => {
    if (!pomodoroState.running) return;
    const timer = window.setInterval(() => {
      setPomodoroState((prev) => {
        if (!prev.running) return prev;
        if (prev.remainingSec <= 1) {
          const pomoPlugin = plugins.find((p) => p.id === 'plg-pomodoro');
          const nextMode = prev.mode === 'focus' ? 'break' : 'focus';
          const nextMins =
            nextMode === 'focus'
              ? pomoPlugin?.config.focusMinutes ?? 25
              : pomoPlugin?.config.breakMinutes ?? 5;
          notify(
            nextMode === 'break'
              ? '专注时间结束，已切换至休息倒计时'
              : '休息结束，已准备好下一轮专注'
          );
          return {
            running: false,
            mode: nextMode,
            remainingSec: nextMins * 60,
          };
        }
        return { ...prev, remainingSec: prev.remainingSec - 1 };
      });
    }, 1000);
    return () => window.clearInterval(timer);
  }, [pomodoroState.running, plugins, notify]);

  const handlePomodoroAction = useCallback(
    (action: 'toggle' | 'reset') => {
      const pomoPlugin = plugins.find((p) => p.id === 'plg-pomodoro');
      const focusSec = (pomoPlugin?.config.focusMinutes ?? 25) * 60;
      if (action === 'reset') {
        setPomodoroState({ running: false, remainingSec: focusSec, mode: 'focus' });
        notify('已重置番茄钟倒计时');
        return;
      }
      setPomodoroState((prev) => {
        const nextRunning = !prev.running;
        notify(nextRunning ? '已开始番茄钟专注计时' : '已暂停番茄钟计时');
        return { ...prev, running: nextRunning };
      });
    },
    [plugins, notify]
  );

  const handleTogglePureMode = useCallback(() => {
    setPureMode((prev) => {
      const next = !prev;
      notify(next ? '已开启纯净显示模式，临时隐藏所有桌面挂件' : '已退出纯净模式，恢复挂件显示');
      return next;
    });
  }, [notify]);

  const handleInstallPlugin = useCallback(
    (id: string, enableAfterInstall: boolean) => {
      setPlugins((prev) =>
        prev.map((p) => {
          if (p.id !== id) return p;
          notify(
            enableAfterInstall
              ? `已安装并启用插件「${p.name}」`
              : `已安装插件「${p.name}」（可在配置后随时启用）`
          );
          return { ...p, installed: true, enabled: enableAfterInstall };
        })
      );
    },
    [notify]
  );

  const handleTogglePlugin = useCallback(
    (id: string) => {
      setPlugins((prev) =>
        prev.map((p) => {
          if (p.id !== id) return p;
          const nextEnabled = !p.enabled;
          notify(`${nextEnabled ? '已启用' : '已停用'}插件「${p.name}」`);
          return { ...p, installed: true, enabled: nextEnabled };
        })
      );
    },
    [notify]
  );

  const handleUninstallPlugin = useCallback(
    (id: string) => {
      const target = plugins.find((p) => p.id === id);
      if (!target || target.core) return;
      const prevPlugins = plugins;
      setPlugins((prev) =>
        prev.map((p) => (p.id === id ? { ...p, installed: false, enabled: false } : p))
      );
      notify(`已卸载插件「${target.name}」`, {
        durationMs: 5000,
        onUndo: () => {
          setPlugins(prevPlugins);
          notify(`已撤销卸载「${target.name}」`);
        },
      });
    },
    [plugins, notify]
  );

  const handleUpdatePluginConfig = useCallback(
    (id: string, nextConfig: PluginConfig, enableIfNeeded = false) => {
      setPlugins((prev) =>
        prev.map((p) => {
          if (p.id !== id) return p;
          return {
            ...p,
            installed: true,
            enabled: enableIfNeeded ? true : p.enabled,
            config: { ...nextConfig },
          };
        })
      );
      if (id === 'plg-pomodoro' && typeof nextConfig.focusMinutes === 'number') {
        setPomodoroState((prev) =>
          prev.running
            ? prev
            : { running: false, mode: 'focus', remainingSec: nextConfig.focusMinutes! * 60 }
        );
      }
      const target = plugins.find((p) => p.id === id);
      notify(
        enableIfNeeded
          ? `已保存「${target?.name ?? '插件'}」配置并启用`
          : `已保存「${target?.name ?? '插件'}」参数配置`
      );
    },
    [plugins, notify]
  );

  const handleResetPluginConfig = useCallback(
    (id: string) => {
      const defaults = INITIAL_PLUGINS.find((p) => p.id === id);
      if (!defaults) return;
      setPlugins((prev) =>
        prev.map((p) => (p.id === id ? { ...p, config: { ...defaults.config } } : p))
      );
      if (id === 'plg-pomodoro') {
        setPomodoroState({
          running: false,
          mode: 'focus',
          remainingSec: (defaults.config.focusMinutes ?? 25) * 60,
        });
      }
      notify(`已恢复「${defaults.name}」默认参数`);
    },
    [notify]
  );

  // Filtered themes when user searches or picks category filter from TopBar
  const filteredThemes = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return themesWithPlaylist.filter((t) => {
      if (categoryFilter !== '全部' && t.category !== categoryFilter) return false;
      if (
        q !== '' &&
        !t.displayName.toLowerCase().includes(q) &&
        !t.name.toLowerCase().includes(q) &&
        !t.category.toLowerCase().includes(q)
      ) {
        return false;
      }
      return true;
    });
  }, [themesWithPlaylist, searchQuery, categoryFilter]);

  const hasActiveSearch = searchQuery.trim() !== '' || categoryFilter !== '全部';

  const MOBILE_NAV_ITEMS: { id: NavSection; label: string }[] = [
    { id: 'home', label: '首页' },
    { id: 'online', label: '在线资源' },
    { id: 'library', label: '资源库' },
    { id: 'creator', label: '创作中心' },
    { id: 'playlist', label: '轮播列表' },
    { id: 'schedule', label: '定时切换' },
    { id: 'plugins', label: '插件中心' },
    { id: 'devices', label: '设备管理' },
    { id: 'settings', label: '设置' },
  ];

  const isInEditor = activeNav === 'creator' && Boolean(editorSession);

  return (
    <div
      className={
        isInEditor
          ? 'flex h-screen w-full items-stretch justify-center overflow-hidden bg-[#F7F7FC] p-0'
          : 'studio-ambient-bg flex h-screen w-full items-stretch justify-center overflow-hidden p-2 sm:p-4 lg:p-5'
      }
    >
      {desktopMode && (!libraryReady || librarySaving || libraryError) && (
        <div role="status" className="fixed right-6 top-3 z-[100] rounded-xl bg-white px-4 py-2 text-xs shadow-lg">
          {libraryError || (libraryReady ? '正在保存资源库…' : '正在读取资源库…')}
          {libraryError && libraryReady && <button className="ml-3 text-violet-600" onClick={() => setLibraryRetry(v => v + 1)}>重试保存</button>}
          {!libraryReady && libraryError && <button className="ml-3 text-violet-600" onClick={() => location.reload()}>重新读取</button>}
        </div>
      )}
      {desktopMode && !libraryReady && <div className="fixed inset-0 z-[90] bg-white/50" />}
      {/* Main Frosted Glass Desktop App Shell (Full-viewport edge-to-edge in Editor) */}
      <div
        className={
          isInEditor
            ? 'relative flex h-full w-full overflow-hidden bg-[#F7F7FC]'
            : 'relative flex h-full w-full max-w-[1440px] overflow-hidden rounded-[28px] border border-white/80 bg-[#F7F7FC]/92 shadow-[0_30px_90px_rgba(45,28,110,0.13)] backdrop-blur-2xl'
        }
      >
        {/* Left Navigation Rail */}
        <Sidebar
          activeNav={activeNav}
          onSelectNav={setActiveNav}
          activeDevice={activeDevice}
          isPlaying={isPlaying}
          mode={isInEditor ? editorSidebarMode : 'full'}
          onChangeMode={isInEditor ? setEditorSidebarMode : undefined}
        />

        {/* Mobile Drawer Menu */}
        {mobileMenuOpen && (
          <div className="fixed inset-0 z-50 flex lg:hidden">
            <div
              className="fixed inset-0 bg-[#140B36]/40 backdrop-blur-xs"
              onClick={() => setMobileMenuOpen(false)}
            />
            <div className="relative z-10 flex w-64 flex-col bg-white p-5 shadow-2xl">
              <div className="mb-6 flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-[#5E45F8] text-white">
                    <Layers className="h-4 w-4" />
                  </div>
                  <span className="text-[18px] font-extrabold text-[#1A172B]">D Studio</span>
                </div>
                <button
                  type="button"
                  onClick={() => setMobileMenuOpen(false)}
                  className="rounded-lg p-1.5 text-[#6B6880]"
                  aria-label="关闭菜单"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
              <div className="space-y-1">
                {MOBILE_NAV_ITEMS.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => {
                      setActiveNav(item.id);
                      setMobileMenuOpen(false);
                    }}
                    className={`w-full rounded-xl px-3.5 py-2.5 text-left text-[13.5px] font-semibold transition-colors ${
                      activeNav === item.id
                        ? 'bg-[#5E45F8] text-white'
                        : 'text-[#56526B] hover:bg-[#F5F4FA]'
                    }`}
                  >
                    {item.label}
                  </button>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Right Workspace Shell: TopBar + Scrollable Content + Pinned Bottom Dock */}
        <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <TopBar
            devices={devices}
            activeDevice={activeDevice}
            onSelectDevice={handleSelectDevice}
            searchQuery={searchQuery}
            onSearchChange={setSearchQuery}
            categoryFilter={categoryFilter}
            onCategoryFilterChange={setCategoryFilter}
            onSelectNav={setActiveNav}
            onOpenMobileMenu={() => setMobileMenuOpen(true)}
            recentLogs={recentLogs}
          />

          {/* Temporary Device Preview Banner (15s auto-restore) */}
          {tempPreview && (
            <div className="flex shrink-0 items-center justify-between gap-3 border-b border-[#D9D2FF] bg-[#EFEBFD]/90 px-5 py-2 text-[12px] text-[#1A172B] backdrop-blur-md">
              <div className="flex items-center gap-2 truncate">
                <span className="font-bold text-[#5E45F8]">
                  临时设备预览中（{tempPreview.remainingSec}s 后自动恢复）
                </span>
                <span className="truncate text-[#56526B]">
                  正在 {activeDevice.name} 实时预览「{tempPreview.previewTheme.displayName}」
                </span>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <button
                  type="button"
                  onClick={() => {
                    const item = tempPreview.previewTheme;
                    setTempPreview(null);
                    handleSaveEditorTheme(item, true);
                  }}
                  className="rounded-lg bg-[#5E45F8] px-3 py-1 text-[11.5px] font-bold text-white hover:bg-[#4D34E6] whitespace-nowrap"
                >
                  保留并正式应用
                </button>
                <button
                  type="button"
                  onClick={() => stopTempPreviewAndRestore(false)}
                  className="rounded-lg border border-[#DAD6F0] bg-white px-3 py-1 text-[11.5px] font-semibold text-[#56526B] hover:border-[#5E45F8] whitespace-nowrap"
                >
                  立即结束并恢复
                </button>
              </div>
            </div>
          )}

          {/* Main Scrollable Viewport (Full-bleed non-padded when Theme Editor V2.0 is active) */}
          <main
            className={
              activeNav === 'creator' && editorSession
                ? 'min-h-0 flex-1 overflow-hidden'
                : 'flex-1 overflow-y-auto px-5 py-5 sm:px-7 sm:py-6 studio-scroll'
            }
          >
            {/* Search / Filter Result Strip when user filters from TopBar */}
              {desktopMode && ['online', 'profile', 'plugins'].includes(activeNav) && <div role="status" className="mb-4 rounded-xl border border-[#E6E4F2] bg-white p-3 text-[12px] text-[#7A7690]">
                {activeNav === 'online' ? '当前展示内置示例资源，尚未连接真实在线商店。可用素材可以加入本机资源库。' : activeNav === 'profile' ? '此页为原型账号与会员演示，尚未连接登录、支付或订阅服务。' : '插件页面保留原型演示，插件真实数据接入不属于当前版本功能。'}
              </div>}
            {hasActiveSearch && activeNav === 'home' && (
              <div className="mb-5 rounded-[22px] bg-white p-4 shadow-[0_10px_28px_rgba(30,20,80,0.05)]">
                <div className="mb-3 flex items-center justify-between">
                  <div className="text-[13.5px] font-bold text-[#1A172B]">
                    筛选结果（{filteredThemes.length} 项）
                    {categoryFilter !== '全部' && (
                      <span className="ml-2 text-[#5E45F8]">· {categoryFilter}</span>
                    )}
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      setSearchQuery('');
                      setCategoryFilter('全部');
                    }}
                    className="text-[12px] font-semibold text-[#5E45F8] hover:underline"
                  >
                    重置筛选
                  </button>
                </div>

                {filteredThemes.length === 0 ? (
                  <div className="rounded-xl bg-[#F8F7FC] py-6 text-center text-[13px] text-[#8B87A1]">
                    没有找到匹配的内容，请尝试其他关键词
                  </div>
                ) : activeDevice.orientation === 'portrait' ? (
                  <div className="grid grid-cols-2 gap-3.5 sm:grid-cols-3 md:grid-cols-5">
                    {filteredThemes.map((item) => (
                      <PortraitPosterCard
                        key={item.id}
                        item={item}
                        activeDevice={activeDevice}
                        isCurrent={item.id === effectiveActiveTheme.id}
                        onApply={handleApplyTheme}
                        onToggleFavorite={handleToggleFavorite}
                        onTogglePlaylist={handleTogglePlaylist}
                        subtitleExtra={activeDevice.resolution}
                      />
                    ))}
                  </div>
                ) : (
                  <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-3">
                    {filteredThemes.map((item) => (
                      <LandscapePosterCard
                        key={item.id}
                        item={item}
                        activeDevice={activeDevice}
                        isCurrent={item.id === effectiveActiveTheme.id}
                        onApply={handleApplyTheme}
                        onToggleFavorite={handleToggleFavorite}
                        onTogglePlaylist={handleTogglePlaylist}
                      />
                    ))}
                  </div>
                )}
              </div>
            )}

            {activeNav === 'home' ? (
              <HomeDashboard
                themes={themesWithPlaylist}
                activeTheme={effectiveActiveTheme}
                activeDevice={activeDevice}
                isPlaying={isPlaying}
                overlayPlugins={enabledPlugins}
                pomodoroState={pomodoroState}
                onTogglePlay={handleTogglePlayCurrentScope}
                onApplyTheme={handleApplyTheme}
                onToggleFavorite={handleToggleFavorite}
                onTogglePlaylist={handleTogglePlaylist}
                onOrientationChange={handleOrientationChange}
                onToggleFlip={handleToggleFlip}
                onPrevTheme={handlePrevTheme}
                onNextTheme={handleNextTheme}
                onOpenPicker={() => setPickerOpen(true)}
                onSelectNav={setActiveNav}
              />
            ) : (
              <SecondaryViews
                trashedThemes={themes.filter(t => Boolean(t.deletedAt))}
                onRestoreThemes={handleRestoreThemes}
                searchQuery={searchQuery}
                onClearSearch={() => setSearchQuery('')}
                section={activeNav}
                themes={activeNav === 'playlist' || activeNav === 'schedule' ? themesWithPlaylist : filteredThemes}
                activeTheme={effectiveActiveTheme}
                devices={devices}
                activeDevice={activeDevice}
                categoryFilter={categoryFilter}
                onCategoryFilterChange={setCategoryFilter}
                onApplyTheme={handleApplyTheme}
                onToggleFavorite={handleToggleFavorite}
                onTogglePlaylist={handleTogglePlaylist}
                onSelectDevice={handleSelectDevice}
                onUpdateDevice={handleUpdateDevice}
                editorSession={editorSession}
                editorSidebarMode={editorSidebarMode}
                onChangeEditorSidebarMode={setEditorSidebarMode}
                onEnterEditor={(proj, srcId) =>
                  setEditorSession({ project: proj, sourceThemeId: srcId })
                }
                onExitEditor={() => setEditorSession(null)}
                onSaveEditorTheme={handleSaveEditorTheme}
                onStartTempPreview={handleStartTempPreview}
                onDeleteCustomTheme={(id) => handleRemoveFromLibrary([id])}
                onDuplicateCustomTheme={handleDuplicateCustomTheme}
                onEditInStudio={handleEditInStudio}
                onFlushLibrary={async () => { await saveChain.current; if (saveFailed.current) throw new Error('请先重试保存资源库。'); }}
                  scheduleRuntime={scheduleRuntime}
                schedules={desktopMode ? schedules.map(rule => scheduleRuntime?.completed.includes(rule.id) ? { ...rule, enabled: false } : rule) : schedules}
                schedulesEnabled={schedulesEnabled}
                onToggleGlobalSchedules={handleToggleGlobalSchedules}
                onToggleSchedule={handleToggleSchedule}
                onAddSchedule={handleAddSchedule}
                onUpdateSchedule={handleUpdateSchedule}
                onDeleteSchedule={handleDeleteSchedule}
                onRunScheduleNow={handleRunScheduleNow}
                plugins={plugins}
                pureMode={pureMode}
                pomodoroState={pomodoroState}
                onTogglePureMode={handleTogglePureMode}
                onInstallPlugin={handleInstallPlugin}
                onTogglePlugin={handleTogglePlugin}
                onUninstallPlugin={handleUninstallPlugin}
                onUpdatePluginConfig={handleUpdatePluginConfig}
                onResetPluginConfig={handleResetPluginConfig}
                onPomodoroAction={handlePomodoroAction}
                playlists={playlists}
                activePlaylistId={activePlaylistId}
                playingPlaylistId={playingPlaylistId}
                countdownSeconds={countdownSeconds}
                onOrientationChange={handleOrientationChange}
                onSelectPlaylist={(plId) =>
                  setActivePlaylistByScope((prev) => ({ ...prev, [currentScopeKey]: plId }))
                }
                onCreatePlaylist={handleCreatePlaylist}
                onUpdatePlaylist={handleUpdatePlaylist}
                onDeletePlaylist={handleDeletePlaylist}
                onTogglePlayPlaylist={handleTogglePlayPlaylist}
                onAddItemsToPlaylist={handleAddItemsToPlaylist}
                onRemoveItemFromPlaylist={handleRemoveItemFromPlaylist}
                onReorderPlaylistItems={handleReorderPlaylistItems}
                onApplyAllDuration={handleApplyAllDuration}
                onApplyItemDuration={handleApplyItemDuration}
                onImportLocalThemes={handleImportLocalThemes}
                onRenameTheme={handleRenameTheme}
                onRemoveFromLibrary={handleRemoveFromLibrary}
                onBatchFavorite={handleBatchFavorite}
                onBatchAddToTargetPlaylist={handleBatchAddToTargetPlaylist}
                onAcquireToLibrary={handleAcquireToLibrary}
                onSelectNav={setActiveNav}
                onNotify={notify}
              />
            )}
          </main>

          {/* Bottom Slim Frosted Playback Dock (Pinned at bottom, fixed height across landscape & portrait) */}
          <footer className="relative z-20 flex shrink-0 items-center justify-between gap-4 border-t border-[#ECEAF6] bg-white/88 px-5 py-3 backdrop-blur-xl sm:px-7">
            <div className="flex min-w-0 items-center gap-3.5">
              <div
                style={{
                  aspectRatio:
                    activeDevice.orientation === 'landscape'
                      ? `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`
                      : `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`,
                }}
                className="h-10 shrink-0 overflow-hidden rounded-lg border border-[#E5E2F4] bg-[#160F30] shadow-xs transition-all duration-200"
              >
                <ThemeArtwork
                  theme={effectiveActiveTheme}
                  className="h-full w-full"
                  showClock={false}
                  portrait={activeDevice.orientation === 'portrait'}
                />
              </div>
              <div className="min-w-0">
                <div className="truncate text-[13px] font-bold text-[#1A172B]">
                  {effectiveActiveTheme.displayName}
                </div>
                <div className="hidden items-center gap-1.5 truncate text-[11px] text-[#8B87A1] sm:flex">
                  <span className="truncate">{activeDevice.name}</span>
                  {activePlayingPlaylist ? (
                    <>
                      <span aria-hidden="true">·</span>
                      <button
                        type="button"
                        onClick={() => setActiveNav('playlist')}
                        className="font-semibold text-[#5E45F8] transition-colors hover:text-[#4830DB] hover:underline whitespace-nowrap"
                        title="点击前往轮播列表"
                      >
                        轮播「{activePlayingPlaylist.name}」· {activePlayingPlaylist.mood}
                      </button>
                      <span aria-hidden="true">·</span>
                      <span className="font-mono-tabular whitespace-nowrap">
                        {isPlaying
                          ? countdownSeconds !== null
                            ? `${countdownSeconds}s`
                            : '实时输出中'
                          : '已暂停'}
                      </span>
                    </>
                  ) : (
                    <>
                      <span aria-hidden="true">·</span>
                      <span className="whitespace-nowrap">
                        {isPlaying ? '实时输出中' : '已暂停'}
                      </span>
                    </>
                  )}
                </div>
              </div>

              <div className="ml-2 hidden items-center gap-1.5 sm:flex">
                <button
                  type="button"
                  onClick={handlePrevTheme}
                  aria-label="上一项"
                  className="flex h-8 w-8 items-center justify-center rounded-full border border-[#ECEAF6] bg-white text-[#56526B] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8]"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
                <button
                  type="button"
                  onClick={handleNextTheme}
                  aria-label="下一项"
                  className="flex h-8 w-8 items-center justify-center rounded-full border border-[#ECEAF6] bg-white text-[#56526B] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8]"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
                <button
                  type="button"
                  onClick={handleTogglePlayCurrentScope}
                  aria-label={isPlaying ? '停止输出' : '开始输出'}
                  className="flex h-8 w-8 items-center justify-center rounded-full border border-[#ECEAF6] bg-white text-[#56526B] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8]"
                >
                  {isPlaying ? (
                    <Square className="h-3.5 w-3.5 fill-current" />
                  ) : (
                    <Play className="h-3.5 w-3.5 fill-current" />
                  )}
                </button>
                <button
                  type="button"
                  onClick={() => setActiveNav('playlist')}
                  aria-label="更多播放队列设置"
                  className="flex h-8 w-8 items-center justify-center rounded-full border border-[#ECEAF6] bg-white text-[#56526B] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8]"
                >
                  <MoreHorizontal className="h-4 w-4" />
                </button>
              </div>
            </div>

            <button
              type="button"
              onClick={() => setPickerOpen(true)}
              className="flex items-center gap-2 rounded-full bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-5 py-2 text-[12.5px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.32)] transition-all duration-150 hover:scale-[1.02] hover:shadow-[0_10px_24px_rgba(94,69,248,0.42)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] whitespace-nowrap shrink-0"
            >
              <ImageIcon className="h-3.5 w-3.5" />
              <span>更换内容</span>
            </button>
          </footer>
        </div>
      </div>

      {/* Content Picker Modal */}
      <ContentPickerModal
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        themes={themes}
        activeThemeId={activeTheme.id}
        activeDevice={activeDevice}
        onSelectTheme={handleApplyTheme}
      />

      {/* Non-blocking Toast Feedback (with optional 5-second Undo action) */}
      {toast && (
        <div
          role="status"
          aria-live="polite"
          className="fixed bottom-20 right-6 z-50 flex items-center gap-3 rounded-2xl border border-[#D9D2FF] bg-white/95 px-4 py-2.5 text-[12.5px] font-semibold text-[#1A172B] shadow-[0_16px_36px_rgba(45,28,110,0.16)] backdrop-blur-md"
        >
          <CheckCircle2 className="h-4 w-4 shrink-0 text-[#5E45F8]" />
          <span>{toast.message}</span>
          {toast.onUndo && (
            <button
              type="button"
              onClick={toast.onUndo}
              className="flex items-center gap-1 rounded-full bg-[#EFEBFD] px-2.5 py-1 text-[11.5px] font-bold text-[#5E45F8] transition-colors hover:bg-[#E2DAFC]"
            >
              <RotateCcw className="h-3 w-3" />
              <span>撤销</span>
            </button>
          )}
        </div>
      )}
    </div>
  );
}






















