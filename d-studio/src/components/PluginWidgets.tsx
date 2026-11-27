import { CloudSun, Cpu, Music2, StickyNote, Timer } from 'lucide-react';
import { FlipClock } from './widgets/FlipClock';
import React, { useEffect, useMemo, useState } from 'react';
import {
  PluginItem,
  PluginPosition,
  PluginScale,
  ThemeItem,
} from '../data/themes';
import {
  CraftAquariumWidget,
  CraftLyricsWidget,
  CraftNixieClockWidget,
  CraftStocksWidget,
} from './crafts/CraftWidgets';
import {
  BonsaiSpecies,
  BonsaiSunlight,
  CassetteShellColor,
  CraftBonsaiWidget,
  CraftCampfireWidget,
  CraftCassetteWidget,
  CraftCountdownWidget,
  CraftCyberPetWidget,
  CraftFlipClockWidget,
  CraftNetSpeedWidget,
  CraftParcelWidget,
  CraftVuMeterWidget,
  CraftZodiacWidget,
  CyberPetMood,
  CyberPetSpecies,
  FlipShellStyle,
  MilestoneMode,
  NetUnitMode,
  ParcelStage,
  VuBacklightHue,
  VuSensitivity,
  ZodiacSignKey,
} from './crafts/ExtendedCraftWidgets';

export interface PomodoroRuntimeState {
  running: boolean;
  remainingSec: number;
  mode: 'focus' | 'break';
}

interface PluginWidgetCardProps {
  plugin: PluginItem;
  portrait?: boolean;
  thumbnailMode?: boolean;
  interactive?: boolean;
  now: Date;
  tickPhase: number;
  pomodoroState?: PomodoroRuntimeState;
}

const POSITION_CLASSES: Record<PluginPosition, string> = {
  'top-left': 'top-2.5 left-2.5 items-start justify-start',
  'top-right': 'top-2.5 right-2.5 items-start justify-end',
  'bottom-left': 'bottom-2.5 left-2.5 items-end justify-start',
  'bottom-right': 'bottom-2.5 right-2.5 items-end justify-end',
  center: 'inset-0 items-center justify-center',
  full: 'inset-0 items-stretch justify-stretch',
};

const PORTRAIT_POSITION_CLASSES: Record<PluginPosition, string> = {
  'top-left': 'top-2 left-2 right-2 items-start justify-start',
  'top-right': 'top-2 left-2 right-2 items-start justify-end',
  'bottom-left': 'bottom-2 left-2 right-2 items-end justify-start',
  'bottom-right': 'bottom-2 left-2 right-2 items-end justify-end',
  center: 'inset-0 px-2 items-center justify-center',
  full: 'inset-0 items-stretch justify-stretch',
};

const SCALE_CLASS: Record<PluginScale, string> = {
  compact: 'scale-[0.88]',
  standard: 'scale-100',
  large: 'scale-[1.1]',
};

const ORIGIN_BY_POS: Record<PluginPosition, string> = {
  'top-left': 'origin-top-left',
  'top-right': 'origin-top-right',
  'bottom-left': 'origin-bottom-left',
  'bottom-right': 'origin-bottom-right',
  center: 'origin-center',
  full: 'origin-center',
};

function formatPad(n: number): string {
  return String(n).padStart(2, '0');
}

function formatCountdown(totalSec: number): string {
  const safe = Math.max(0, Math.floor(totalSec));
  const m = Math.floor(safe / 60);
  const s = safe % 60;
  return `${formatPad(m)}:${formatPad(s)}`;
}

export const PluginWidgetCard: React.FC<PluginWidgetCardProps> = ({
  plugin,
  portrait = false,
  thumbnailMode = false,
  interactive = false,
  now,
  tickPhase,
  pomodoroState,
}) => {
  const { config } = plugin;
  const accent = config.accentColor || '#5E45F8';
  const bgAlpha = Math.min(0.95, Math.max(0.35, (config.opacity ?? 76) / 100));
  const compact = portrait || thumbnailMode;

  const surfaceStyle: React.CSSProperties = {
    backgroundColor: `rgba(15, 11, 34, ${bgAlpha})`,
    borderColor: `${accent}55`,
  };

  const isFull = config.position === 'full';
  const frameShapeCls = isFull ? 'rounded-none border-0' : 'rounded-xl border';

  const scaleTransform =
    thumbnailMode || isFull
      ? ''
      : `${SCALE_CLASS[config.scale ?? 'standard']} ${ORIGIN_BY_POS[config.position]}`;

  if (plugin.id === 'plg-clock') {
    const rawHours = now.getHours();
    const use24 = config.use24Hour !== false;
    const displayHours = use24 ? rawHours : rawHours % 12 || 12;
    const ampm = rawHours >= 12 ? 'PM' : 'AM';
    const timeMain = `${formatPad(displayHours)}:${formatPad(now.getMinutes())}`;
    const secondsStr = formatPad(now.getSeconds());
    const weekDays = ['周日', '周一', '周二', '周三', '周四', '周五', '周六'];
    const dateLine = `${formatPad(now.getMonth() + 1)}月${formatPad(now.getDate())}日 · ${weekDays[now.getDay()]}`;

    return (
      <div
        style={surfaceStyle}
        className={`rounded-xl border text-white backdrop-blur-md shadow-[0_8px_22px_rgba(8,4,24,0.42)] ${scaleTransform} ${
          compact ? 'px-2.5 py-1.5' : 'px-3.5 py-2'
        }`}
      >
        {config.clockStyle === 'flip' ? (
          <div style={{ fontSize: compact ? 15 : 20, fontWeight: 700 }}>
            <FlipClock hours={formatPad(displayHours)} minutes={formatPad(now.getMinutes())} seconds={secondsStr}
              showSeconds={config.showSeconds !== false} period={use24 ? undefined : ampm} portrait={portrait} animate={false} />
          </div>
        ) : (
        <div className="flex items-baseline gap-1.5">
          <span
            className={`font-mono-tabular font-bold tracking-tight ${
              compact ? 'text-[15px]' : 'text-[20px]'
            }`}
          >
            {timeMain}
          </span>
          {config.showSeconds !== false && (
            <span
              style={{ color: accent }}
              className={`font-mono-tabular font-bold ${
                compact ? 'text-[11px]' : 'text-[13px]'
              }`}
            >
              :{secondsStr}
            </span>
          )}
          {!use24 && (
            <span className="text-[10px] font-semibold text-white/70">{ampm}</span>
          )}
        </div>
        )}
        {config.showDate !== false && (
          <div className="mt-0.5 font-mono-tabular text-[10px] font-medium text-white/75">
            {dateLine}
          </div>
        )}
      </div>
    );
  }

  if (plugin.id === 'plg-telemetry') {
    const cpuLoad = 28 + ((tickPhase * 7) % 19);
    const gpuLoad = 41 + ((tickPhase * 11) % 23);
    const cpuTemp = 48 + (tickPhase % 5);
    const memPct = 62;

    return (
      <div
        style={surfaceStyle}
        className={`rounded-xl border text-white backdrop-blur-md shadow-[0_8px_22px_rgba(8,4,24,0.42)] ${scaleTransform} ${
          compact ? 'min-w-[138px] px-2.5 py-1.5' : 'min-w-[186px] px-3 py-2'
        }`}
      >
        <div className="flex items-center justify-between gap-2 text-[10px] text-white/75">
          <span className="flex items-center gap-1 font-semibold text-white/90">
            <Cpu className="h-3 w-3" style={{ color: accent }} />
            <span>性能监控</span>
          </span>
          {config.showTemp !== false && (
            <span className="font-mono-tabular">{cpuTemp}°C</span>
          )}
        </div>

        <div className="mt-1.5 space-y-1">
          <div className="flex items-center gap-2 text-[10px]">
            <span className="w-6 shrink-0 font-mono-tabular text-white/70">CPU</span>
            <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/15">
              <div
                className="h-full rounded-full transition-all duration-300"
                style={{ width: `${cpuLoad}%`, backgroundColor: accent }}
              />
            </div>
            <span className="w-7 text-right font-mono-tabular font-semibold">{cpuLoad}%</span>
          </div>

          <div className="flex items-center gap-2 text-[10px]">
            <span className="w-6 shrink-0 font-mono-tabular text-white/70">GPU</span>
            <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/15">
              <div
                className="h-full rounded-full transition-all duration-300"
                style={{ width: `${gpuLoad}%`, backgroundColor: accent }}
              />
            </div>
            <span className="w-7 text-right font-mono-tabular font-semibold">{gpuLoad}%</span>
          </div>

          {config.showMemory !== false && (
            <div className="flex items-center justify-between pt-0.5 text-[9.5px] font-mono-tabular text-white/70">
              <span>内存占用</span>
              <span>{memPct}% · 19.8/32G</span>
            </div>
          )}
        </div>
      </div>
    );
  }

  if (plugin.id === 'plg-audio') {
    const barTotal = Math.min(28, Math.max(8, config.barCount ?? 20));
    const displayBars = compact ? Math.min(barTotal, 14) : barTotal;
    const bars = Array.from({ length: displayBars }, (_, idx) => {
      const wave = Math.abs(Math.sin((idx + 1) * 0.68 + tickPhase * 0.85));
      return Math.max(22, Math.round(wave * 100));
    });

    return (
      <div
        style={surfaceStyle}
        className={`rounded-xl border text-white backdrop-blur-md shadow-[0_8px_22px_rgba(8,4,24,0.42)] ${scaleTransform} ${
          compact ? 'px-2.5 py-1.5' : 'px-3.5 py-2'
        }`}
      >
        {config.showTrackName !== false && (
          <div className="mb-1.5 flex items-center gap-1.5 text-[10px] font-medium text-white/85">
            <Music2 className="h-3 w-3 shrink-0" style={{ color: accent }} />
            <span className="max-w-[130px] truncate">Midnight Lo-Fi · 小屏宝PixBuddy</span>
          </div>
        )}
        <div className={`flex items-end gap-[2.5px] ${compact ? 'h-4' : 'h-6'}`}>
          {bars.map((pct, i) => (
            <span
              key={i}
              className="w-[3px] rounded-full transition-all duration-200"
              style={{
                height: `${pct}%`,
                backgroundColor: accent,
                opacity: 0.65 + (pct / 100) * 0.35,
              }}
            />
          ))}
        </div>
      </div>
    );
  }

  if (plugin.id === 'plg-pomodoro') {
    const defaultSec = (config.focusMinutes ?? 25) * 60;
    const remaining = pomodoroState ? pomodoroState.remainingSec : defaultSec;
    const isRunning = pomodoroState?.running ?? false;
    const modeLabel = pomodoroState?.mode === 'break' ? '休息中' : '专注计时';

    return (
      <div
        style={surfaceStyle}
        className={`flex items-center gap-2.5 rounded-xl border text-white backdrop-blur-md shadow-[0_8px_22px_rgba(8,4,24,0.42)] ${scaleTransform} ${
          compact ? 'px-2.5 py-1.5' : 'px-3.5 py-2'
        }`}
      >
        <div
          className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg"
          style={{ backgroundColor: `${accent}28`, color: accent }}
        >
          <Timer className="h-3.5 w-3.5" />
        </div>
        <div>
          <div className="flex items-center gap-1.5 text-[9.5px] text-white/75">
            <span>{modeLabel}</span>
            <span aria-hidden="true">·</span>
            <span style={{ color: isRunning ? accent : undefined }}>
              {isRunning ? '进行中' : '就绪'}
            </span>
          </div>
          <div
            className={`font-mono-tabular font-bold leading-tight ${
              compact ? 'text-[14px]' : 'text-[17px]'
            }`}
          >
            {formatCountdown(remaining)}
          </div>
        </div>
      </div>
    );
  }

  if (plugin.id === 'plg-weather') {
    const city = (config.city ?? '上海').trim() || '上海';
    return (
      <div
        style={surfaceStyle}
        className={`flex items-center gap-2.5 rounded-xl border text-white backdrop-blur-md shadow-[0_8px_22px_rgba(8,4,24,0.42)] ${scaleTransform} ${
          compact ? 'px-2.5 py-1.5' : 'px-3.5 py-2'
        }`}
      >
        <CloudSun className="h-5 w-5 shrink-0" style={{ color: accent }} />
        <div>
          <div className="flex items-baseline gap-1.5">
            <span className="text-[11.5px] font-bold">{city}</span>
            <span className="font-mono-tabular text-[13px] font-bold" style={{ color: accent }}>
              22°C
            </span>
          </div>
          <div className="text-[10px] text-white/75">
            多云转晴{config.showAqi !== false ? ' · 空气优 32' : ''}
          </div>
        </div>
      </div>
    );
  }

  // plg-sticky
  const note = (config.noteText ?? '保持桌面整洁，专注当前工作').trim() || '保持专注';
  if (plugin.id === 'plg-craft-aquarium') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? config.position === 'center'
          ? 'w-[86%] h-[86%]'
          : 'w-[76%] h-[80%]'
        : portrait
          ? 'w-[176px] h-[94px]'
          : 'w-[280px] h-[134px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 90) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(4,12,28,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftAquariumWidget
          fishCount={config.fishCount ?? 7}
          causticsIntensity={config.causticsIntensity ?? 0.6}
          bubbleSpeed={config.bubbleSpeed ?? 1.0}
          showClock={config.showAquariumClock !== false}
          compact={compact}
          interactive={interactive && !thumbnailMode}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-stocks') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? config.position === 'center'
          ? 'w-[84%] h-[86%]'
          : 'w-[76%] h-[82%]'
        : portrait
          ? 'w-[176px] h-[108px]'
          : 'w-[270px] h-[142px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 88) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(4,12,28,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftStocksWidget
          selectedSymbol={config.stockSymbol ?? 'NVDA'}
          accentColor={accent}
          compact={compact}
          interactive={interactive && !thumbnailMode}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-lyrics') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? config.position === 'center'
          ? 'w-[86%] h-[86%]'
          : 'w-[78%] h-[82%]'
        : portrait
          ? 'w-[178px] h-[106px]'
          : 'w-[290px] h-[138px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 88) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(12,6,28,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftLyricsWidget
          songTitle={config.songTitle ?? 'Midnight City'}
          artist={config.artist ?? 'M83'}
          spectrumBars={config.barCount ?? 20}
          glowColor={accent}
          compact={compact}
          interactive={interactive && !thumbnailMode}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-nixie') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'max-w-[92%] max-h-[88%] rounded-xl'
        : portrait
          ? 'max-w-[96%] rounded-xl'
          : 'rounded-xl';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.55, (config.opacity ?? 92) / 100) }}
        className={`overflow-hidden ${isFull ? 'border-0 rounded-none' : 'border'} shadow-[0_10px_28px_rgba(12,8,4,0.62)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftNixieClockWidget
          showSeconds={config.showSeconds !== false}
          showDate={config.showDate !== false}
          use24Hour={config.use24Hour !== false}
          colorHue={config.nixieHue ?? 'amber'}
          compact={compact}
          now={now}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-cyber-pet') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[84%] h-[84%]'
        : portrait
          ? 'w-[176px] h-[82px]'
          : 'w-[248px] h-[94px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 90) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(8,6,22,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftCyberPetWidget
          petName={config.petName ?? 'Mochi'}
          species={(config.petSpecies as CyberPetSpecies) ?? 'mochi'}
          mood={(config.petMood as CyberPetMood) ?? 'happy'}
          accentColor={accent}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-campfire') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[86%] h-[86%]'
        : portrait
          ? 'w-[174px] h-[94px]'
          : 'w-[250px] h-[126px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 92) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(24,8,4,0.58)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftCampfireWidget
          flameIntensity={config.flameIntensity ?? 'medium'}
          emberDensity={config.emberDensity ?? 'medium'}
          showSoundHint={config.showSoundHint !== false}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-bonsai') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[84%] h-[86%]'
        : portrait
          ? 'w-[174px] h-[86px]'
          : 'w-[240px] h-[106px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 90) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(4,16,12,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftBonsaiWidget
          species={(config.bonsaiSpecies as BonsaiSpecies) ?? 'pine'}
          growthDays={config.growthDays ?? 28}
          sunlightTone={(config.sunlightTone as BonsaiSunlight) ?? 'warm'}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-flip-clock') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'max-w-[92%] max-h-[88%] rounded-xl'
        : portrait
          ? 'max-w-[96%] rounded-xl'
          : 'rounded-xl';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.55, (config.opacity ?? 92) / 100) }}
        className={`overflow-hidden ${isFull ? 'border-0 rounded-none' : 'border'} shadow-[0_10px_28px_rgba(6,8,18,0.62)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftFlipClockWidget
          showSeconds={config.showSeconds !== false}
          use24Hour={config.use24Hour !== false}
          showDate={config.showDate !== false}
          shellStyle={(config.flipShellStyle as FlipShellStyle) ?? 'obsidian'}
          compact={compact}
          now={now}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-vu-meter') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[86%] h-[86%]'
        : portrait
          ? 'w-[178px] h-[94px]'
          : 'w-[264px] h-[116px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.55, (config.opacity ?? 92) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(12,10,4,0.6)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftVuMeterWidget
          backlightHue={(config.vuBacklight as VuBacklightHue) ?? 'warm-amber'}
          sensitivity={(config.vuSensitivity as VuSensitivity) ?? 'balanced'}
          deckLabel={config.deckLabel ?? 'MASTER STEREO'}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-cassette') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[84%] h-[86%]'
        : portrait
          ? 'w-[176px] h-[96px]'
          : 'w-[254px] h-[122px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 90) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(8,10,20,0.58)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftCassetteWidget
          trackTitle={config.songTitle ?? 'City Pop Tape #04'}
          artistLabel={config.artist ?? 'SIDE A · 90MIN'}
          shellColor={(config.cassetteShell as CassetteShellColor) ?? 'smoke'}
          tapeSpeed={config.tapeSpeed ?? 1.0}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-zodiac') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[86%] h-[84%]'
        : portrait
          ? 'w-[178px] h-[86px]'
          : 'w-[258px] h-[98px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 88) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(16,10,32,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftZodiacWidget
          sign={(config.zodiacSign as ZodiacSignKey) ?? 'aquarius'}
          showConstellationGlow={config.showConstellationGlow !== false}
          accentColor={accent}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-parcel') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[86%] h-[84%]'
        : portrait
          ? 'w-[178px] h-[92px]'
          : 'w-[264px] h-[106px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 88) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(6,16,28,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftParcelWidget
          parcelName={config.parcelName ?? '客制化机械键盘套件'}
          carrier={config.carrier ?? '顺丰速运'}
          stage={(config.parcelStage as ParcelStage) ?? 'delivering'}
          accentColor={accent}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-milestone') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[84%] h-[84%]'
        : portrait
          ? 'w-[172px] h-[82px]'
          : 'w-[228px] h-[96px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 90) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(14,10,24,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftCountdownWidget
          milestoneTitle={config.milestoneTitle ?? '年度版本发布'}
          targetDays={config.targetDays ?? 18}
          countMode={(config.countMode as MilestoneMode) ?? 'countdown'}
          targetDateLabel={config.targetDateLabel ?? '2026.12.15'}
          accentColor={accent}
          compact={compact}
        />
      </div>
    );
  }

  if (plugin.id === 'plg-craft-netradar') {
    const sizeCls = isFull
      ? 'w-full h-full'
      : thumbnailMode
        ? 'w-[86%] h-[86%]'
        : portrait
          ? 'w-[176px] h-[96px]'
          : 'w-[250px] h-[112px]';
    return (
      <div
        style={{ ...surfaceStyle, opacity: Math.max(0.5, (config.opacity ?? 88) / 100) }}
        className={`overflow-hidden ${frameShapeCls} shadow-[0_10px_28px_rgba(4,16,22,0.55)] ${scaleTransform} ${sizeCls}`}
      >
        <CraftNetSpeedWidget
          unitMode={(config.unitMode as NetUnitMode) ?? 'Mbps'}
          refreshSeconds={config.refreshSeconds ?? 2}
          showPingChart={config.showPingChart !== false}
          accentColor={accent}
          compact={compact}
        />
      </div>
    );
  }

  return (
    <div
      style={surfaceStyle}
      className={`rounded-xl border text-white backdrop-blur-md shadow-[0_8px_22px_rgba(8,4,24,0.42)] ${scaleTransform} ${
        compact ? 'max-w-[168px] px-2.5 py-1.5' : 'max-w-[230px] px-3.5 py-2'
      }`}
    >
      <div className="flex items-center gap-1.5 text-[10px] font-semibold text-white/80">
        <StickyNote className="h-3 w-3 shrink-0" style={{ color: accent }} />
        <span>桌面便签</span>
      </div>
      <div className="mt-0.5 line-clamp-2 text-[11px] font-medium leading-snug text-white/95">
        {note}
      </div>
    </div>
  );
};

interface PluginOverlayLayerProps {
  plugins: PluginItem[];
  portrait?: boolean;
  liveTick?: boolean;
  pomodoroState?: PomodoroRuntimeState;
}

export const PluginOverlayLayer: React.FC<PluginOverlayLayerProps> = ({
  plugins,
  portrait = false,
  liveTick = true,
  pomodoroState,
}) => {
  const [now, setNow] = useState(() => new Date());
  const [tickPhase, setTickPhase] = useState(1);

  const hasLiveWidgets = plugins.length > 0;

  useEffect(() => {
    if (!liveTick || !hasLiveWidgets) return;
    const timer = window.setInterval(() => {
      setNow(new Date());
      setTickPhase((p) => (p + 1) % 600);
    }, 1000);
    return () => window.clearInterval(timer);
  }, [liveTick, hasLiveWidgets]);

  if (!hasLiveWidgets) return null;

  return (
    <div className="pointer-events-none absolute inset-0 z-20 overflow-hidden">
      {plugins.map((plg) => {
        const posClass = portrait
          ? PORTRAIT_POSITION_CLASSES[plg.config.position]
          : POSITION_CLASSES[plg.config.position];
        return (
          <div key={plg.id} className={`absolute flex ${posClass}`}>
            <PluginWidgetCard
              plugin={plg}
              portrait={portrait}
              now={now}
              tickPhase={tickPhase}
              pomodoroState={pomodoroState}
            />
          </div>
        );
      })}
    </div>
  );
};

interface PluginThumbnailPreviewProps {
  plugin: PluginItem;
  backdropTheme?: ThemeItem;
  pomodoroState?: PomodoroRuntimeState;
  aspectRatio?: string;
  className?: string;
  liveTick?: boolean;
}

export const PluginThumbnailPreview: React.FC<PluginThumbnailPreviewProps> = ({
  plugin,
  backdropTheme,
  pomodoroState,
  aspectRatio = '16 / 7',
  className = '',
  liveTick = true,
}) => {
  const [now, setNow] = useState(() => new Date());
  const [tickPhase, setTickPhase] = useState(2);

  useEffect(() => {
    if (!liveTick) return;
    const timer = window.setInterval(() => {
      setNow(new Date());
      setTickPhase((p) => (p + 1) % 600);
    }, 1200);
    return () => window.clearInterval(timer);
  }, [liveTick]);

  const isFull = plugin.config.position === 'full';
  const posClass = useMemo(
    () => POSITION_CLASSES[plugin.config.position] ?? POSITION_CLASSES.full,
    [plugin.config.position]
  );

  return (
    <div
      style={{ aspectRatio }}
      className={`relative w-full overflow-hidden rounded-[14px] border border-white/15 bg-[#120A2A] select-none ${className}`}
    >
      {/* Background: either current theme image or crisp dark studio aurora */}
      {backdropTheme?.imageUrl ? (
        <>
          <img
            src={backdropTheme.imageUrl}
            alt={plugin.name}
            referrerPolicy="no-referrer"
            className="h-full w-full object-cover object-center opacity-75"
          />
          <div className="absolute inset-0 bg-gradient-to-t from-[#0D0820]/75 via-[#140B33]/35 to-[#0D0820]/45" />
        </>
      ) : (
        <div className="aurora-dark-card relative h-full w-full">
          <div
            className="absolute inset-0 opacity-25"
            style={{
              backgroundImage:
                'linear-gradient(rgba(255,255,255,0.08) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.08) 1px, transparent 1px)',
              backgroundSize: '20px 20px',
            }}
          />
        </div>
      )}

      {/* Hardware bezel subtle inner outline */}
      {!isFull && (
        <div className="pointer-events-none absolute inset-1.5 rounded-[10px] border border-white/10" />
      )}

      {/* Live Widget Rendered at Real Anchor Position */}
      <div
        className={`pointer-events-none absolute inset-0 flex ${
          isFull
            ? 'p-0'
            : `p-2 ${posClass.replace(/top-2\.5|left-2\.5|right-2\.5|bottom-2\.5|inset-0/g, '').trim()}`
        }`}
      >
        <PluginWidgetCard
          plugin={plugin}
          thumbnailMode={true}
          now={now}
          tickPhase={tickPhase}
          pomodoroState={pomodoroState}
        />
      </div>
    </div>
  );
};
