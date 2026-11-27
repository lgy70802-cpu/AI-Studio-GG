import { UpdatesPanel } from './UpdatesPanel';
import type { ScheduleRuntime } from '../services/desktopBridge';
import { setCloseToTray, renameDevice, desktopMode, scanDevices, setRestoreOutput, devicePower, desktopPreferences, setAutoStartNative, cleanTemporaryFiles, exportPackage, inspectPackage, importPackage, cancelPackage, PackageSummary } from '../services/desktopBridge';
import React, { useState, useEffect } from 'react';
import {
  Monitor,
  Sun,
  Clock,
  Sliders,
  Sparkles,
  Trash2,
  Power,
  User,
  ShieldCheck,
  HardDrive,
  FlipHorizontal2,
  Flame,
} from 'lucide-react';
import {
  INITIAL_THEMES,
  CarouselPlaylist,
  ContentCategory,
  DeviceInfo,
  MoodTag,
  NavSection,
  PluginConfig,
  PluginItem,
  ScheduleRule,
  ScreenOrientation,
  ThemeItem,
} from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';
import { PortraitPosterCard } from './PortraitPosterCard';
import { LandscapePosterCard } from './LandscapePosterCard';
import { PlaylistManagerView } from './PlaylistManagerView';
import { ScheduleManagerView } from './ScheduleManagerView';
import { LibraryManagerView } from './LibraryManagerView';
import { PluginCenterView } from './PluginCenterView';
import { PomodoroRuntimeState } from './PluginWidgets';
import { CreatorStudioHub } from './editor/CreatorStudioHub';
import { ThemeEditorV2 } from './editor/ThemeEditorV2';
import type { SidebarMode } from './Sidebar';
import { EditorThemeProject } from '../types/editor';

interface SecondaryViewsProps {
  onFlushLibrary: () => Promise<void>;
  scheduleRuntime: ScheduleRuntime | null;
  searchQuery: string;
  onClearSearch: () => void;
  section: Exclude<NavSection, 'home'>;
  themes: ThemeItem[];
  activeTheme: ThemeItem;
  devices: DeviceInfo[];
  activeDevice: DeviceInfo;
  categoryFilter: ContentCategory | '全部';
  onCategoryFilterChange: (cat: ContentCategory | '全部') => void;
  onApplyTheme: (theme: ThemeItem) => void;
  onToggleFavorite: (id: string) => void;
  onTogglePlaylist: (id: string) => void;
  onSelectDevice: (deviceId: string) => void;
  onUpdateDevice: (deviceId: string, patch: Partial<DeviceInfo>) => void;
  editorSession: { project: EditorThemeProject; sourceThemeId?: string; sessionId?: string } | null;
  editorSidebarMode?: SidebarMode;
  onChangeEditorSidebarMode?: (mode: SidebarMode) => void;
  onEnterEditor: (project: EditorThemeProject, sourceThemeId?: string) => void;
  onExitEditor: () => void;
  onSaveEditorTheme: (theme: ThemeItem, applyNow: boolean) => void | Promise<boolean>;
  onStartTempPreview: (theme: ThemeItem) => void;
  onDeleteCustomTheme: (id: string) => void;
  onDuplicateCustomTheme: (theme: ThemeItem) => void;
  onEditInStudio: (theme: ThemeItem) => void;
  schedules: ScheduleRule[];
  schedulesEnabled: boolean;
  onToggleGlobalSchedules: () => void;
  onToggleSchedule: (id: string) => void;
  onAddSchedule: (rule: ScheduleRule) => void;
  onUpdateSchedule: (id: string, patch: Partial<ScheduleRule>, notifyMsg?: string) => void;
  onDeleteSchedule: (id: string) => void;
  onRunScheduleNow: (rule: ScheduleRule) => void;
  plugins: PluginItem[];
  pureMode: boolean;
  pomodoroState: PomodoroRuntimeState;
  onTogglePureMode: () => void;
  onInstallPlugin: (id: string, enableAfterInstall: boolean) => void;
  onTogglePlugin: (id: string) => void;
  onUninstallPlugin: (id: string) => void;
  onUpdatePluginConfig: (id: string, nextConfig: PluginConfig, enableIfNeeded?: boolean) => void;
  onResetPluginConfig: (id: string) => void;
  onPomodoroAction: (action: 'toggle' | 'reset') => void;
  playlists: CarouselPlaylist[];
  activePlaylistId: string;
  playingPlaylistId: string | null;
  countdownSeconds: number | null;
  onOrientationChange: (orientation: ScreenOrientation) => void;
  onSelectPlaylist: (playlistId: string) => void;
  onCreatePlaylist: (payload: {
    name: string;
    mood: MoodTag;
    deviceId: string;
    orientation: ScreenOrientation;
  }) => void;
  onUpdatePlaylist: (
    playlistId: string,
    patch: Partial<CarouselPlaylist>,
    notifyMsg?: string
  ) => void;
  onDeletePlaylist: (playlistId: string) => void;
  onTogglePlayPlaylist: (playlistId: string) => void;
  onAddItemsToPlaylist: (playlistId: string, themeIds: string[]) => void;
  onRemoveItemFromPlaylist: (playlistId: string, themeId: string) => void;
  onReorderPlaylistItems: (playlistId: string, fromIndex: number, toIndex: number) => void;
  onApplyAllDuration: (playlistId: string, seconds: number) => void;
  onApplyItemDuration: (playlistId: string, themeId: string, seconds: number) => void;
  onImportLocalThemes: (items: ThemeItem[]) => void;
  onRenameTheme: (id: string, nextName: string) => void;
  onRemoveFromLibrary: (ids: string[]) => void;
  onBatchFavorite: (ids: string[]) => void;
  onBatchAddToTargetPlaylist: (playlistId: string | null, themeIds: string[]) => void;
  onAcquireToLibrary: (item: ThemeItem) => void;
  onSelectNav: (nav: NavSection) => void;
  onNotify: (msg: string) => void;
}

export const SecondaryViews: React.FC<SecondaryViewsProps> = ({
  onFlushLibrary,
  scheduleRuntime, searchQuery, onClearSearch,
  section,
  themes,
  activeTheme,
  devices,
  activeDevice,
  categoryFilter,
  onCategoryFilterChange,
  onApplyTheme,
  onToggleFavorite,
  onTogglePlaylist,
  onSelectDevice,
  onUpdateDevice,
  editorSession,
  editorSidebarMode,
  onChangeEditorSidebarMode,
  onEnterEditor,
  onExitEditor,
  onSaveEditorTheme,
  onStartTempPreview,
  onDeleteCustomTheme,
  onDuplicateCustomTheme,
  onEditInStudio,
  schedules,
  schedulesEnabled,
  onToggleGlobalSchedules,
  onToggleSchedule,
  onAddSchedule,
  onUpdateSchedule,
  onDeleteSchedule,
  onRunScheduleNow,
  plugins,
  pureMode,
  pomodoroState,
  onTogglePureMode,
  onInstallPlugin,
  onTogglePlugin,
  onUninstallPlugin,
  onUpdatePluginConfig,
  onResetPluginConfig,
  onPomodoroAction,
  playlists,
  activePlaylistId,
  playingPlaylistId,
  countdownSeconds,
  onOrientationChange,
  onSelectPlaylist,
  onCreatePlaylist,
  onUpdatePlaylist,
  onDeletePlaylist,
  onTogglePlayPlaylist,
  onAddItemsToPlaylist,
  onRemoveItemFromPlaylist,
  onReorderPlaylistItems,
  onApplyAllDuration,
  onApplyItemDuration,
  onImportLocalThemes,
  onRenameTheme,
  onRemoveFromLibrary,
  onBatchFavorite,
  onBatchAddToTargetPlaylist,
  onAcquireToLibrary,
  onSelectNav,
  onNotify,
}) => {
  const [onlineSort, setOnlineSort] = useState<'popular' | 'newest'>('popular');

  // Settings local state
  const [closeToTray, setCloseToTrayState] = useState(false);
  const [autoStart, setAutoStart] = useState(!desktopMode);
  const [packageProgress, setPackageProgress] = useState('');
  useEffect(() => {
    const update = (event: Event) => setPackageProgress((event as CustomEvent<string>).detail);
    window.addEventListener('studio-package-progress', update);
    return () => window.removeEventListener('studio-package-progress', update);
  }, []);
  const [packageBusy, setPackageBusy] = useState(false);
  const [packagePreview, setPackagePreview] = useState<PackageSummary | null>(null);
  const [packageThemeId, setPackageThemeId] = useState('');
  const [restoreOutput, setRestoreOutputState] = useState(true);
  useEffect(() => {
    if (desktopMode) void scanDevices().then(state => setRestoreOutputState(state.restore)).catch(console.error);
    if (desktopMode) void desktopPreferences().then(state => { setAutoStart(state.autoStart); setCloseToTrayState(state.closeToTray); }).catch(console.error);
  }, []);
  const [hardwareAccel, setHardwareAccel] = useState(true);
  const [smoothFade, setSmoothFade] = useState(true);
  const packageAction = async (action: () => Promise<void>) => {
    if (packageBusy) return;
    setPackageProgress('等待选择文件或处理备份…');
    setPackageBusy(true);
    try { await onFlushLibrary(); await action(); }
    catch (error) { onNotify((error as Error).message); }
    finally { setPackageBusy(false); setPackageProgress(''); }
  };

  const isPortrait = activeDevice.orientation === 'portrait';
  const previewAspectRatio = isPortrait
    ? `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`
    : `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`;

  if (section === 'library') {
    const scopedPlaylists = playlists.filter(
      (p) => p.deviceId === activeDevice.id && p.orientation === activeDevice.orientation
    );
    return (
      <LibraryManagerView
        searchQuery={searchQuery}
        onClearSearch={onClearSearch}
        themes={themes}
        activeTheme={activeTheme}
        activeDevice={activeDevice}
        categoryFilter={categoryFilter}
        onCategoryFilterChange={onCategoryFilterChange}
        scopedPlaylists={scopedPlaylists}
        activePlaylistId={activePlaylistId}
        onApplyTheme={onApplyTheme}
        onToggleFavorite={onToggleFavorite}
        onTogglePlaylist={onTogglePlaylist}
        onImportLocalThemes={onImportLocalThemes}
        onRenameTheme={onRenameTheme}
        onRemoveFromLibrary={onRemoveFromLibrary}
        onBatchFavorite={onBatchFavorite}
        onBatchAddToTargetPlaylist={onBatchAddToTargetPlaylist}
        onEditInStudio={onEditInStudio}
        onNavigateOnline={() => onSelectNav('online')}
        onNotify={onNotify}
      />
    );
  }

  if (section === 'online') {
    const filtered = themes
      .filter((item) => {
        if (item.deletedAt) return false;
        const official = item.source === 'official' || (!item.source && INITIAL_THEMES.some(theme =>
          theme.id === item.id && (!theme.source || theme.source === 'official')));
        if (!official) return false;
        if (categoryFilter !== '全部' && item.category !== categoryFilter) return false;
        return true;
      })
      .sort((a, b) => {
        if (onlineSort === 'newest') {
          return (b.publishedAt ?? 0) - (a.publishedAt ?? 0);
        }
        return (b.downloadCount ?? 0) - (a.downloadCount ?? 0);
      });

    const getOnlineSubtitleExtra = (item: ThemeItem) => {
      const base = onlineSort === 'newest' ? item.releasedLabel : item.downloads;
      return item.inLibrary !== false ? `${base} · 已入库` : base;
    };

    return (
      <div className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4 rounded-[22px] bg-white p-5 shadow-[0_10px_28px_rgba(30,20,80,0.04)]">
          <div>
            <h2 className="text-[20px] font-bold text-[#1A172B]">在线资源广场</h2>
            <p className="mt-0.5 text-[13px] text-[#7A7690]">
              精选适配 {activeDevice.name}（{activeDevice.resolution}）的时钟与全景壁纸
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <div
              className="flex items-center rounded-xl bg-[#F4F3FA] p-1"
              role="group"
              aria-label="在线资源排序筛选"
            >
              {(
                [
                  { id: 'popular', label: '热门', icon: Flame },
                  { id: 'newest', label: '新上架', icon: Sparkles },
                ] as const
              ).map((opt) => {
                const Icon = opt.icon;
                const active = onlineSort === opt.id;
                return (
                  <button
                    key={opt.id}
                    type="button"
                    onClick={() => setOnlineSort(opt.id)}
                    className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                      active
                        ? 'bg-white text-[#5E45F8] shadow-sm'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    <Icon className="h-3.5 w-3.5" />
                    <span>{opt.label}</span>
                  </button>
                );
              })}
            </div>

            <div className="flex items-center rounded-xl bg-[#F4F3FA] p-1">
              {(['全部', '动态时钟', '壁纸', '主题'] as const).map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => onCategoryFilterChange(cat)}
                  className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                    categoryFilter === cat
                      ? 'bg-white text-[#5E45F8] shadow-sm'
                      : 'text-[#6B6880] hover:text-[#1A172B]'
                  }`}
                >
                  {cat === '动态时钟' ? '时钟' : cat}
                </button>
              ))}
            </div>
          </div>
        </div>

        {filtered.length === 0 ? (
          <div className="rounded-[24px] border border-dashed border-[#DAD6F0] bg-white/85 py-14 text-center">
            <p className="text-[14px] font-bold text-[#1A172B]">当前分类或搜索下暂无匹配的在线资源</p>
            <button
              type="button"
              onClick={() => onCategoryFilterChange('全部')}
              className="mt-3 rounded-xl bg-[#F3F0FF] px-4 py-2 text-[12.5px] font-semibold text-[#5E45F8]"
            >
              查看全部分类
            </button>
          </div>
        ) : isPortrait ? (
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-5">
            {filtered.map((item) => (
              <PortraitPosterCard
                key={item.id}
                item={item}
                activeDevice={activeDevice}
                isCurrent={item.id === activeTheme.id}
                onApply={onApplyTheme}
                onToggleFavorite={onToggleFavorite}
                onTogglePlaylist={onTogglePlaylist}
                onAcquire={onAcquireToLibrary}
                subtitleExtra={getOnlineSubtitleExtra(item)}
              />
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {filtered.map((item) => (
              <LandscapePosterCard
                key={item.id}
                item={item}
                activeDevice={activeDevice}
                isCurrent={item.id === activeTheme.id}
                onApply={onApplyTheme}
                onToggleFavorite={onToggleFavorite}
                onTogglePlaylist={onTogglePlaylist}
                onAcquire={onAcquireToLibrary}
                subtitleExtra={getOnlineSubtitleExtra(item)}
              />
            ))}
          </div>
        )}
      </div>
    );
  }

  if (section === 'creator') {
    if (editorSession) {
      return (
        <ThemeEditorV2
          key={editorSession.sessionId || editorSession.sourceThemeId || editorSession.project.template_id}
          initialProject={editorSession.project}
          sourceThemeId={editorSession.sourceThemeId}
          activeDevice={activeDevice}
          devices={devices}
          themes={themes}
          onSaveOnly={(theme) => onSaveEditorTheme(theme, false)}
          onSaveAndApply={(theme) => onSaveEditorTheme(theme, true)}
          onStartTempPreview={onStartTempPreview}
          onExitEditor={onExitEditor}
          onNotify={onNotify}
          sidebarMode={editorSidebarMode}
          onChangeSidebarMode={onChangeEditorSidebarMode}
        />
      );
    }

    return (
      <CreatorStudioHub
        themes={themes}
        activeTheme={activeTheme}
        activeDevice={activeDevice}
        devices={devices}
        onEnterEditor={onEnterEditor}
        onApplyTheme={onApplyTheme}
        onDeleteCustomTheme={onDeleteCustomTheme}
        onDuplicateCustomTheme={onDuplicateCustomTheme}
        onNotify={onNotify}
      />
    );
  }

  if (section === 'playlist') {
    return (
      <PlaylistManagerView
        pausedPlaylistId={desktopMode && !activeDevice.playlistRunning ? activeDevice.playlistId : null}
        playlists={playlists}
        activePlaylistId={activePlaylistId}
        playingPlaylistId={playingPlaylistId}
        activeThemeId={activeTheme.id}
        countdownSeconds={countdownSeconds}
        themes={themes}
        activeDevice={activeDevice}
        onOrientationChange={onOrientationChange}
        onSelectPlaylist={onSelectPlaylist}
        onCreatePlaylist={onCreatePlaylist}
        onUpdatePlaylist={onUpdatePlaylist}
        onDeletePlaylist={onDeletePlaylist}
        onTogglePlayPlaylist={onTogglePlayPlaylist}
        onAddItemsToPlaylist={onAddItemsToPlaylist}
        onRemoveItemFromPlaylist={onRemoveItemFromPlaylist}
        onReorderPlaylistItems={onReorderPlaylistItems}
        onApplyAllDuration={onApplyAllDuration}
        onApplyItemDuration={onApplyItemDuration}
        onApplyTheme={onApplyTheme}
        onNotify={onNotify}
      />
    );
  }

  if (section === 'schedule') {
    return (
      <ScheduleManagerView
        runtime={scheduleRuntime}
        schedules={schedules}
        schedulesEnabled={schedulesEnabled}
        themes={themes}
        devices={devices}
        activeDevice={activeDevice}
        playlists={playlists}
        onOrientationChange={onOrientationChange}
        onToggleGlobalSchedules={onToggleGlobalSchedules}
        onToggleSchedule={onToggleSchedule}
        onAddSchedule={onAddSchedule}
        onUpdateSchedule={onUpdateSchedule}
        onDeleteSchedule={onDeleteSchedule}
        onRunScheduleNow={onRunScheduleNow}
      />
    );
  }

  if (section === 'plugins') {
    return (
      <PluginCenterView
        plugins={plugins}
        pureMode={pureMode}
        activeTheme={activeTheme}
        activeDevice={activeDevice}
        pomodoroState={pomodoroState}
        onOrientationChange={onOrientationChange}
        onTogglePureMode={onTogglePureMode}
        onInstallPlugin={onInstallPlugin}
        onTogglePlugin={onTogglePlugin}
        onUninstallPlugin={onUninstallPlugin}
        onUpdatePluginConfig={onUpdatePluginConfig}
        onResetPluginConfig={onResetPluginConfig}
        onPomodoroAction={onPomodoroAction}
      />
    );
  }

  if (section === 'devices') {
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
          {devices.map((dev) => {
            const isActive = dev.id === activeDevice.id;
            return (
              <div
                key={dev.id}
                className={`rounded-[24px] bg-white p-6 shadow-[0_12px_32px_rgba(30,20,80,0.04)] transition-all ${
                  isActive ? 'ring-2 ring-[#5E45F8]' : ''
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-3.5">
                    <div
                      className={`flex h-12 w-12 items-center justify-center rounded-2xl ${
                        isActive
                          ? 'bg-[#5E45F8] text-white shadow-[0_8px_18px_rgba(94,69,248,0.3)]'
                          : 'bg-[#F3F1FC] text-[#5E45F8]'
                      }`}
                    >
                      <Monitor className="h-6 w-6" />
                    </div>
                    <div>
                      <h3 className="text-[16px] font-bold text-[#1A172B]">{dev.name}</h3>
                      {desktopMode && <button className="text-xs text-[#5E45F8]" onClick={() => { const name = window.prompt('设备名称', dev.name); if (name !== null) void renameDevice(dev.serial, name).then(() => onUpdateDevice(dev.id, { name: name.trim() || dev.model })).catch(e => onNotify(e.message)); }}>修改名称</button>}
                      <div className="mt-0.5 font-mono-tabular text-[12px] text-[#7A7690]">
                        {dev.serial} · {dev.resolution} · {dev.firmware}
                      </div>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={() => onSelectDevice(dev.id)}
                    className={`rounded-full px-3.5 py-1.5 text-[12px] font-semibold whitespace-nowrap ${
                      isActive
                        ? 'bg-[#EFEBFD] text-[#5E45F8]'
                        : 'bg-[#5E45F8] text-white hover:bg-[#4D34E6]'
                    }`}
                  >
                    {isActive ? '当前控制中' : '设为活动设备'}
                  </button>
                </div>

                {/* Live Proportional Device Screen Silhouette Preview */}
                <div className="mt-4 flex items-center justify-center rounded-2xl bg-[#F7F6FC] p-4">
                  <div
                    style={{
                      aspectRatio:
                        dev.orientation === 'landscape'
                          ? `${dev.nativeWidth} / ${dev.nativeHeight}`
                          : `${dev.nativeHeight} / ${dev.nativeWidth}`,
                    }}
                    className={`overflow-hidden rounded-2xl border border-[#DDD8F5] shadow-md transition-all duration-200 ${
                      dev.orientation === 'landscape' ? 'h-[110px] max-w-full' : 'h-[260px] max-w-full'
                    }`}
                  >
                    <ThemeArtwork
                      theme={desktopMode ? { ...activeTheme, id: dev.themeId ?? '', displayName: dev.themeName ?? '', imageUrl: dev.preview,
                        editorProject: undefined, desktopStatic: true, desktopFlipped: dev.flipped, desktopBrightness: dev.standby ? 0 : dev.brightness } : activeTheme}
                      className="h-full w-full"
                      showClock={true}
                      compact={true}
                      portrait={dev.orientation === 'portrait'}
                    />
                  </div>
                </div>

                <div className="mt-4 space-y-4 border-t border-[#F1EFF8] pt-4">
                  <div>
                    <div className="mb-1.5 flex items-center justify-between text-[12.5px]">
                      <span className="flex items-center gap-1.5 font-semibold text-[#4A4660]">
                        <Sun className="h-4 w-4 text-amber-500" />
                        {desktopMode ? '画面亮度（软件调暗）' : '背光亮度'}
                      </span>
                      <span className="font-mono-tabular font-bold text-[#5E45F8]">
                        {dev.brightness}%
                      </span>
                    </div>
                    <input
                      type="range"
                      min={10}
                      max={100}
                      value={dev.brightness}
                      onChange={(e) =>
                        onUpdateDevice(dev.id, { brightness: Number(e.target.value) })
                      }
                      className="h-2 w-full cursor-pointer accent-[#5E45F8]"
                    />
                  </div>

                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-2 text-[12.5px] font-semibold text-[#4A4660]">
                      <Sliders className="h-4 w-4 text-[#5E45F8]" />
                      <span>显示方向</span>
                    </div>
                    <div className="flex rounded-xl bg-[#F4F3FA] p-1">
                      {(['landscape', 'portrait'] as ScreenOrientation[]).map((ori) => (
                        <button
                          key={ori}
                          type="button"
                          onClick={() => onUpdateDevice(dev.id, { orientation: ori })}
                          className={`rounded-lg px-3 py-1 text-[12px] font-semibold transition-colors ${
                            dev.orientation === ori
                              ? 'bg-white text-[#5E45F8] shadow-sm'
                              : 'text-[#6B6880]'
                          }`}
                        >
                          {ori === 'landscape'
                            ? `横屏 (${dev.nativeWidth}×${dev.nativeHeight})`
                            : `竖屏 (${dev.nativeHeight}×${dev.nativeWidth})`}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="flex items-center justify-between">
                    <span className="flex items-center gap-2 text-[12.5px] font-semibold text-[#4A4660]">
                      <FlipHorizontal2 className="h-4 w-4 text-[#5E45F8]" />
                      <span>左右翻转</span>
                      <span className="text-[11px] font-normal text-[#8B87A1]">（设备画面旋转 180°）</span>
                    </span>
                    <button
                      type="button"
                      onClick={() => onUpdateDevice(dev.id, { flipped: !dev.flipped })}
                      className={`relative h-7 w-12 shrink-0 rounded-full transition-colors ${
                        dev.flipped ? 'bg-[#5E45F8]' : 'bg-[#D6D3E6]'
                      }`}
                      aria-label={`切换 ${dev.name} 左右翻转`}
                    >
                      <span
                        className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-transform ${
                          dev.flipped ? 'left-6' : 'left-1'
                        }`}
                      />
                    </button>
                  </div>

                  <div className="flex items-center justify-between">
                    <span className="flex items-center gap-2 text-[12.5px] font-semibold text-[#4A4660]">
                      <Clock className="h-4 w-4 text-[#5E45F8]" />
                      {desktopMode ? '无操作黑屏待机' : '无操作息屏'}
                    </span>
                    <select
                      value={dev.sleepMinutes}
                      onChange={(e) =>
                        onUpdateDevice(dev.id, { sleepMinutes: Number(e.target.value) })
                      }
                      className="rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-1.5 text-[12.5px] font-semibold text-[#1A172B]"
                    >
                      <option value={15}>15 分钟</option>
                      <option value={30}>30 分钟</option>
                      <option value={60}>60 分钟</option>
                      <option value={0}>永不息屏</option>
                    </select>
                  </div>
                </div>
                {desktopMode && <div className="mt-4 space-y-2 text-[12px] text-[#7A7690]">
                  <p>当前协议未确认背光调节和断电息屏；软件调暗与黑帧待机不改变背光。</p>
                  <div className="flex gap-2">
                    <button className="rounded-xl border border-[#E6E4F2] bg-white px-3 py-1.5" onClick={() => void devicePower(dev.serial, { action: 'standby' }).catch(e => onNotify(e.message))}>黑屏待机</button>
                    <button className="rounded-xl border border-[#E6E4F2] bg-white px-3 py-1.5" onClick={() => void devicePower(dev.serial, { action: 'wake' }).catch(e => onNotify(e.message))}>恢复画面</button>
                  </div>
                </div>}
              </div>
            );
          })}
        </div>
      </div>
    );
  }

  if (section === 'settings') {
    return (
      <div className="max-w-3xl space-y-5 rounded-[24px] bg-white p-6 shadow-[0_12px_32px_rgba(30,20,80,0.04)]">
        <h2 className="text-[20px] font-bold text-[#1A172B]">系统与偏好设置</h2>
        <UpdatesPanel flush={onFlushLibrary} />
        <div className="divide-y divide-[#F1EFF8]">
          {[
            ...(desktopMode ? [{
              icon: Power, title: '关闭窗口时最小化到系统托盘',
              desc: '保留设备输出，可从托盘菜单退出软件', checked: closeToTray,
              toggle: () => { void setCloseToTray(!closeToTray).then(v => setCloseToTrayState(v.closeToTray)).catch(e => onNotify(e.message)); },
            }, {
              icon: Power,
              title: '启动时恢复上次输出',
              desc: '按设备序列号恢复壁纸与方向，已暂停的设备保持暂停',
              checked: restoreOutput,
              toggle: () => { void setRestoreOutput(!restoreOutput).then(value => setRestoreOutputState(Boolean(value))).catch(console.error); },
            }] : []),
            {
              icon: Power,
              title: '开机自动启动 小屏宝PixBuddy',
              desc: desktopMode ? '当前 Windows 用户登录后最小化启动，按恢复设置处理输出' : '系统登录后静默驻留托盘并立即恢复上次副屏内容',
              checked: autoStart,
              toggle: () => { if (desktopMode) void setAutoStartNative(!autoStart).then(v => setAutoStart(v.autoStart)).catch(e => onNotify(e.message)); else setAutoStart(v => !v); },
            },
            {
              icon: Sparkles,
              title: 'GPU 硬件渲染加速',
              desc: desktopMode ? '由 WebView2 按驱动能力自动选择，暂不支持手动切换' : '使用显卡加速高帧率时钟与玻璃拟态过渡效果',
              checked: desktopMode ? false : hardwareAccel,
              disabled: desktopMode,
              toggle: () => setHardwareAccel((v) => !v),
            },
            {
              icon: ShieldCheck,
              title: '丝滑淡入切换动效',
              desc: desktopMode ? '设备切换淡入尚未接入，当前直接切换内容' : '轮播或手动更换主题时启用 300ms 柔光渐变过渡',
              checked: desktopMode ? false : smoothFade,
              disabled: desktopMode,
              toggle: () => setSmoothFade((v) => !v),
            },
          ].map((row) => {
            const Icon = row.icon;
            return (
              <div key={row.title} className="flex items-center justify-between gap-4 py-4">
                <div className="flex items-center gap-3.5">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[#F3F0FF] text-[#5E45F8]">
                    <Icon className="h-5 w-5" />
                  </div>
                  <div>
                    <div className="text-[14px] font-bold text-[#1A172B]">{row.title}</div>
                    <div className="text-[12.5px] text-[#7A7690]">{row.desc}</div>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={row.toggle}
                  disabled={'disabled' in row && row.disabled}
                  aria-label={row.title}
                  role="switch"
                  aria-checked={row.checked}
                  className={`relative h-7 w-12 shrink-0 rounded-full transition-colors ${
                    row.checked ? 'bg-[#5E45F8]' : 'bg-[#D6D3E6]'
                  }`}
                >
                  <span
                    className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-transform ${
                      row.checked ? 'left-6' : 'left-1'
                    }`}
                  />
                </button>
              </div>
            );
          })}
        </div>

        <div className="flex items-center justify-between rounded-2xl bg-[#F8F7FC] p-4">
          <div className="flex items-center gap-3">
            <HardDrive className="h-5 w-5 text-[#5E45F8]" />
            <div>
              <div className="text-[13.5px] font-bold text-[#1A172B]">临时导入文件</div>
              <div className="font-mono-tabular text-[12px] text-[#7A7690]">仅清理未使用的导入残留，保留素材与工程</div>
            </div>
          </div>
          <button
            type="button"
            onClick={() => { if (desktopMode) void cleanTemporaryFiles().then(v => onNotify(`已清理 ${(v.bytes / 1048576).toFixed(2)} MiB 临时文件`)).catch(e => onNotify(e.message)); }}
            className="rounded-xl bg-white px-4 py-2 text-[12.5px] font-semibold text-[#5E45F8] shadow-sm hover:bg-[#F3F0FF]"
          >
            立即清理
          </button>
        </div>
        {desktopMode && <section className="space-y-3 rounded-2xl border border-[#E6E4F2] bg-[#F8F7FC] p-4">
          <h3 className="font-bold text-[#1A172B]">备份、迁移与工程包</h3>
          <p className="text-[12px] text-[#7A7690]">完整备份包含资源库、回收素材、图片和视频依赖、列表、定时及设备状态。工程包可独立导入另一台电脑。</p>
          <div className="flex flex-wrap gap-2">
            {packageBusy && <p role="status" className="w-full text-xs text-[#7A7690]">{packageProgress}</p>}
            <button disabled={packageBusy} className="rounded-xl bg-[#5E45F8] px-3 py-2 text-[12px] font-semibold text-white disabled:opacity-50" onClick={() => void packageAction(async () => { const result = await exportPackage(); if (!result.cancelled) onNotify('完整备份已保存：' + result.path); })}>导出完整备份</button>
            <button disabled={packageBusy} className="rounded-xl border bg-white px-3 py-2 text-[12px]" onClick={() => void packageAction(async () => { const result = await inspectPackage(); if (!result.cancelled) setPackagePreview(result); })}>导入或恢复备份包</button>
          </div>
          <div className="flex flex-wrap gap-2">
            <select aria-label="选择导出工程" value={packageThemeId} onChange={e => setPackageThemeId(e.target.value)} className="max-w-full rounded-xl border bg-white p-2 text-[12px]">
              <option value="">选择一个工程或素材</option>{themes.map(t => <option key={t.id} value={t.id}>{t.displayName}</option>)}
            </select>
            <button disabled={packageBusy || !packageThemeId} className="rounded-xl border bg-white px-3 py-2 text-[12px] disabled:opacity-50" onClick={() => void packageAction(async () => { const result = await exportPackage(packageThemeId); if (!result.cancelled) onNotify('工程包已保存：' + result.path); })}>导出工程包</button>
          </div>
          {packageBusy && <p role="status" className="text-[12px]">正在校验或传输，请勿关闭软件…</p>}
          {packagePreview && <div role="dialog" aria-label="备份包导入预览" className="space-y-3 rounded-xl border bg-white p-4 text-[12px]">
            <p>已通过完整性校验：{packagePreview.themes} 项素材、{packagePreview.playlists} 个列表、{packagePreview.schedules} 条定时。</p><p>{packagePreview.note}</p>
            <div className="flex flex-wrap gap-2">{(['merge', ...(packagePreview.scope === 'workspace' ? ['restore'] : [])] as ('merge' | 'restore')[]).map(mode => <button key={mode} disabled={packageBusy} className="rounded-lg border px-3 py-2" onClick={() => void packageAction(async () => {
              const result = await importPackage(packagePreview.token, mode);
              Object.entries(result.preferences ?? {}).forEach(([key, value]) => {
                  if (typeof value !== 'string') return;
                  if (key === 'd92-te-custom-assets-v1') {
                    const incoming = JSON.parse(value);
                    if (!Array.isArray(incoming)) throw new Error('自定义素材预设格式无效。');
                    const existing = mode === 'merge' ? JSON.parse(localStorage.getItem(key) || '[]') : [];
                    const merged = [...existing];
                    for (const preset of incoming) {
                      if (merged.some(item => JSON.stringify(item.elements) === JSON.stringify(preset.elements) && item.name === preset.name)) continue;
                      merged.push({ ...preset, id: merged.some(item => item.id === preset.id) ? crypto.randomUUID() : preset.id });
                    }
                    localStorage.setItem(key, JSON.stringify(merged));
                  } else if (mode === 'restore' && key.startsWith('studio-')) localStorage.setItem(key, value);
                });
              location.reload();
            })}>{mode === 'merge' ? '合并导入，保留原内容' : '恢复整份备份并暂停输出'}</button>)}<button disabled={packageBusy} onClick={() => { setPackagePreview(null); void cancelPackage().catch(e => onNotify(e.message)); }}>取消</button></div>
          </div>}
        </section>}
      </div>
    );
  }

  // Profile view
  return (
    <div className="max-w-3xl space-y-6">
      <div className="aurora-dark-card rounded-[26px] p-6 text-white shadow-[0_22px_50px_rgba(24,10,68,0.24)]">
        <div className="flex flex-wrap items-center gap-4">
          <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 backdrop-blur-md">
            <User className="h-8 w-8 text-white" />
          </div>
          <div>
            <h2 className="text-[20px] font-bold">Studio 创作者账号</h2>
            <p className="mt-0.5 text-[13px] text-white/75">
              已绑定 {devices.length} 台 D92 硬件设备 · 云端配置自动同步
            </p>
          </div>
        </div>
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="rounded-[20px] bg-white p-5 shadow-[0_10px_28px_rgba(30,20,80,0.04)]">
          <div className="text-[12px] text-[#7A7690]">已收藏主题</div>
          <div className="mt-1 font-mono-tabular text-[24px] font-extrabold text-[#1A172B]">
            {themes.filter((t) => t.isFavorite).length}
          </div>
        </div>
        <div className="rounded-[20px] bg-white p-5 shadow-[0_10px_28px_rgba(30,20,80,0.04)]">
          <div className="text-[12px] text-[#7A7690]">轮播队列数</div>
          <div className="mt-1 font-mono-tabular text-[24px] font-extrabold text-[#1A172B]">
            {themes.filter((t) => t.inPlaylist).length}
          </div>
        </div>
        <div className="rounded-[20px] bg-white p-5 shadow-[0_10px_28px_rgba(30,20,80,0.04)]">
          <div className="text-[12px] text-[#7A7690]">已启用插件</div>
          <div className="mt-1 font-mono-tabular text-[24px] font-extrabold text-[#1A172B]">
            {plugins.filter((p) => p.enabled).length}
          </div>
        </div>
      </div>
    </div>
  );
};







