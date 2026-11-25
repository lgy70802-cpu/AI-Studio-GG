import React from 'react';
import {
  Play,
  Pause,
  SkipBack,
  SkipForward,
  ArrowRight,
  Image as ImageIcon,
  Sun,
  FlipHorizontal2,
  Pencil,
} from 'lucide-react';
import {
  DeviceInfo,
  NavSection,
  PluginItem,
  ScreenOrientation,
  ThemeItem,
} from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';
import { PomodoroRuntimeState } from './PluginWidgets';
import { PortraitPosterCard } from './PortraitPosterCard';
import { LandscapePosterCard } from './LandscapePosterCard';

interface HomeDashboardProps {
  recentItems?: ThemeItem[];
  themes: ThemeItem[];
  activeTheme: ThemeItem;
  activeDevice: DeviceInfo;
  isPlaying: boolean;
  overlayPlugins?: PluginItem[];
  pomodoroState?: PomodoroRuntimeState;
  onTogglePlay: () => void;
  onApplyTheme: (theme: ThemeItem) => void;
  onToggleFavorite: (id: string) => void;
  onTogglePlaylist: (id: string) => void;
  onOrientationChange: (orientation: ScreenOrientation) => void;
  onToggleFlip: () => void;
  onPrevTheme: () => void;
  onNextTheme: () => void;
  onOpenPicker: () => void;
  onEditTheme: (theme: ThemeItem) => void;
  onSelectNav: (nav: NavSection) => void;
}

export const HomeDashboard: React.FC<HomeDashboardProps> = ({
  themes,
  recentItems,
  activeTheme,
  activeDevice,
  isPlaying,
  overlayPlugins,
  pomodoroState,
  onTogglePlay,
  onApplyTheme,
  onToggleFavorite,
  onTogglePlaylist,
  onOrientationChange,
  onToggleFlip,
  onPrevTheme,
  onNextTheme,
  onOpenPicker,
  onEditTheme,
  onSelectNav,
}) => {
  const isPortrait = activeDevice.orientation === 'portrait';
  const recentThemes = (recentItems ?? themes.filter((t) => t.isRecent).sort((a, b) => (b.lastUsedAt ?? 0) - (a.lastUsedAt ?? 0))).slice(0, isPortrait ? 5 : 6);
  const trendingThemes = themes.filter((t) => t.isTrending).slice(0, isPortrait ? 5 : 3);

  const previewAspectRatio = isPortrait
    ? `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`
    : `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`;

  const liveOutputModule = (
    <section
      aria-label="当前屏幕输出预览"
      className="aurora-dark-card relative w-full overflow-hidden rounded-[26px] p-5 sm:p-6 text-white shadow-[0_22px_50px_rgba(24,10,68,0.26)]"
    >
      {/* Card Header Controls */}
      <div className="relative z-10 flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2 text-[12px] font-medium text-white/75">
            <span className="inline-block h-2 w-2 rounded-full bg-[#34D399]" />
            <span>{activeDevice.outputStatus ?? (isPlaying ? '正在输出' : '已暂停输出')}</span>
            <span aria-hidden="true">·</span>
            <span className="font-mono-tabular">{activeDevice.resolution}</span>
            <span aria-hidden="true">·</span>
            <span className="font-mono-tabular">{activeDevice.serial}</span>
          </div>
          <h2 className="mt-1 truncate text-[18px] sm:text-[20px] font-bold tracking-tight text-white">
            {activeTheme.displayName}
          </h2>
        </div>

        <div className="flex flex-wrap items-center gap-2 shrink-0">
          {/* Segmented Orientation Switch */}
          <div
            className="flex items-center rounded-full border border-white/15 bg-white/10 p-1 backdrop-blur-md"
            role="group"
            aria-label="屏幕方向切换"
          >
            <button
              type="button"
              onClick={() => onOrientationChange('landscape')}
              className={`rounded-full px-3 py-1 text-[12px] font-semibold transition-all duration-150 whitespace-nowrap ${
                activeDevice.orientation === 'landscape'
                  ? 'bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.45)]'
                  : 'text-white/75 hover:text-white'
              }`}
            >
              横屏
            </button>
            <button
              type="button"
              onClick={() => onOrientationChange('portrait')}
              className={`rounded-full px-3 py-1 text-[12px] font-semibold transition-all duration-150 whitespace-nowrap ${
                activeDevice.orientation === 'portrait'
                  ? 'bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.45)]'
                  : 'text-white/75 hover:text-white'
              }`}
            >
              竖屏
            </button>
          </div>

          {/* Horizontal Flip Toggle */}
          <button
            type="button"
            onClick={onToggleFlip}
            aria-pressed={activeDevice.flipped}
            className={`flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-[12px] font-semibold transition-all duration-150 whitespace-nowrap ${
              activeDevice.flipped
                ? 'border-[#8B76FF] bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.45)]'
                : 'border-white/15 bg-white/10 text-white/85 hover:bg-white/20'
            }`}
          >
            <FlipHorizontal2 className="h-3.5 w-3.5" />
            <span>翻转</span>
          </button>

          {isPortrait && <button
            type="button"
            onClick={onOpenPicker}
            className="flex items-center gap-1.5 rounded-full bg-white px-3.5 py-1.5 text-[12px] font-bold text-[#1A172B] shadow-[0_8px_20px_rgba(0,0,0,0.2)] transition-transform duration-150 hover:scale-[1.02] hover:bg-[#F4F2FF] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white whitespace-nowrap shrink-0"
          >
            <ImageIcon className="h-3.5 w-3.5 text-[#5E45F8]" />
            <span>更换</span>
          </button>}
          {isPortrait && (
            <button type="button" onClick={() => onEditTheme(activeTheme)}
              className="flex items-center gap-1.5 rounded-full border border-white/15 bg-white/10 px-3 py-1.5 text-[12px] font-semibold text-white transition-colors hover:bg-white/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white whitespace-nowrap">
              <Pencil className="h-3.5 w-3.5" />编辑
            </button>
          )}
        </div>
      </div>

      {/* Live Screen Preview Frame: Doubled width in portrait (~236px wide) */}
      <div className="relative z-10 mt-5 flex justify-center">
        <div
          style={{
            aspectRatio: previewAspectRatio,
            width: isPortrait ? undefined : `${Math.min(1, (activeDevice.nativeWidth / activeDevice.nativeHeight) / (1920 / 462)) * 100}%`,
          }}
          className={`group relative overflow-hidden rounded-[20px] ring-1 ring-inset ring-white/20 bg-black/35 shadow-[0_22px_48px_rgba(6,2,20,0.6)] transition-all duration-300 ${
            isPortrait ? 'w-[216px] sm:w-[236px] max-w-full' : ''
          }`}
        >
          <ThemeArtwork
            theme={activeTheme}
            className="h-full w-full"
            showClock={true}
            liveTick={isPlaying}
            compact={isPortrait}
            portrait={isPortrait}
            overlayPlugins={overlayPlugins}
            pomodoroState={pomodoroState}
          />
        </div>
      </div>

      {/* Card Footer Quick Playback Strip */}
      <div className="relative z-10 mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-white/10 pt-3.5 text-[12px] text-white/75">
        <div className="flex items-center gap-2.5 font-mono-tabular">
          <span>{activeTheme.category}</span>
          {activeDevice.renderFps != null && <span>合成 {activeDevice.renderFps.toFixed(1)} FPS</span>}
          {activeDevice.videoDuration != null && <span className="font-mono-tabular">
            {Math.floor(activeDevice.videoPosition ?? 0)} / {Math.round(activeDevice.videoDuration)}s · {(activeDevice.videoFps ?? 0).toFixed(1)} FPS
          </span>}
          <span aria-hidden="true">·</span>
          <span>{activeDevice.refreshRate}</span>
          <span aria-hidden="true">·</span>
          <span className="flex items-center gap-1">
            <Sun className="h-3.5 w-3.5 text-amber-300" />
            {activeDevice.brightness == null ? '—' : activeDevice.brightness + '%'}
          </span>
        </div>

        <div className="flex flex-wrap items-center gap-1.5">
          <button
            type="button"
            onClick={onPrevTheme}
            aria-label="上一项内容"
            className="flex h-8 w-8 items-center justify-center rounded-full border border-white/15 bg-white/10 text-white transition-colors hover:bg-white/20"
          >
            <SkipBack className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            onClick={onTogglePlay}
            aria-label={isPlaying ? '暂停输出' : '恢复输出'}
            className="flex h-8 items-center gap-1.5 rounded-full bg-[#5E45F8] px-3.5 text-[12px] font-semibold text-white shadow-[0_6px_16px_rgba(94,69,248,0.45)] transition-colors hover:bg-[#6E57FF] whitespace-nowrap"
          >
            {isPlaying ? (
              <>
                <Pause className="h-3.5 w-3.5" />
                <span>暂停</span>
              </>
            ) : (
              <>
                <Play className="h-3.5 w-3.5" />
                <span>恢复</span>
              </>
            )}
          </button>
          <button
            type="button"
            onClick={onNextTheme}
            aria-label="下一项内容"
            className="flex h-8 w-8 items-center justify-center rounded-full border border-white/15 bg-white/10 text-white transition-colors hover:bg-white/20"
          >
            <SkipForward className="h-3.5 w-3.5" />
          </button>
          {!isPortrait && <>
            <button type="button" onClick={onOpenPicker}
              className="flex h-8 items-center gap-1.5 rounded-full bg-white px-3.5 text-[12px] font-semibold text-[#1A172B] transition-colors hover:bg-[#F4F2FF] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white">
              <ImageIcon className="h-3.5 w-3.5 text-[#5E45F8]" />更换
            </button>
            <button type="button" onClick={() => onEditTheme(activeTheme)}
              className="flex h-8 items-center gap-1.5 rounded-full border border-white/15 bg-white/10 px-3.5 text-[12px] font-semibold text-white transition-colors hover:bg-white/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white">
              <Pencil className="h-3.5 w-3.5" />编辑
            </button>
          </>}
        </div>
      </div>
    </section>
  );

  const recentSection = (
    <section aria-label="最近使用" className="w-full">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="text-[17px] font-bold tracking-tight text-[#1A172B]">
          最近使用
        </h3>
        <button
          type="button"
          onClick={() => onSelectNav('library')}
          className="flex items-center gap-1 text-[13px] font-semibold text-[#5E45F8] transition-colors hover:text-[#4830DB] whitespace-nowrap"
        >
          <span>查看更多记录</span>
          <ArrowRight className="h-3.5 w-3.5" />
        </button>
      </div>

      {isPortrait ? (
        <div className="grid grid-cols-2 gap-3.5 sm:grid-cols-3 xl:grid-cols-5">
          {recentThemes.map((item) => (
            <PortraitPosterCard
              key={item.id}
              item={item}
              activeDevice={activeDevice}
              isCurrent={item.id === activeTheme.id}
              onApply={onApplyTheme}
              onToggleFavorite={onToggleFavorite}
              onTogglePlaylist={onTogglePlaylist}
            />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {recentThemes.map((item) => (
            <LandscapePosterCard
              key={item.id}
              item={item}
              activeDevice={activeDevice}
              isCurrent={item.id === activeTheme.id}
              onApply={onApplyTheme}
              onToggleFavorite={onToggleFavorite}
              onTogglePlaylist={onTogglePlaylist}
            />
          ))}
        </div>
      )}
    </section>
  );

  const trendingSection = (
    <section aria-label="热门内容" className="w-full">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="text-[17px] font-bold tracking-tight text-[#1A172B]">
          {isPortrait ? '适合竖屏的热门内容' : '适合横屏的热门内容'}
        </h3>
        <button
          type="button"
          onClick={() => onSelectNav('online')}
          className="flex items-center gap-1 text-[13px] font-semibold text-[#5E45F8] transition-colors hover:text-[#4830DB] whitespace-nowrap"
        >
          <span>查看资源库</span>
          <ArrowRight className="h-3.5 w-3.5" />
        </button>
      </div>

      {isPortrait ? (
        <div className="grid grid-cols-2 gap-3.5 sm:grid-cols-3 xl:grid-cols-5">
          {trendingThemes.map((item) => (
            <PortraitPosterCard
              key={item.id}
              item={item}
              activeDevice={activeDevice}
              isCurrent={item.id === activeTheme.id}
              onApply={onApplyTheme}
              onToggleFavorite={onToggleFavorite}
              onTogglePlaylist={onTogglePlaylist}
              subtitleExtra={item.downloads}
            />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {trendingThemes.map((item) => (
            <LandscapePosterCard
              key={item.id}
              item={item}
              activeDevice={activeDevice}
              isCurrent={item.id === activeTheme.id}
              onApply={onApplyTheme}
              onToggleFavorite={onToggleFavorite}
              onTogglePlaylist={onTogglePlaylist}
              subtitleExtra={item.downloads}
            />
          ))}
        </div>
      )}
    </section>
  );

  if (isPortrait) {
    return (
      <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-12">
        <div className="lg:col-span-4">{liveOutputModule}</div>
        <div className="space-y-7 lg:col-span-8">
          {recentSection}
          {trendingSection}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-7">
      {liveOutputModule}
      {recentSection}
      {trendingSection}
    </div>
  );
};


