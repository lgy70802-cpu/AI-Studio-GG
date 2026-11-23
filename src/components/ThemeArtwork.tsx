import React, { useEffect, useState } from 'react';
import { Monitor, Sparkles } from 'lucide-react';
import { PluginItem, ThemeItem } from '../data/themes';
import { PluginOverlayLayer, PomodoroRuntimeState } from './PluginWidgets';
import { EditorArtworkRenderer } from './editor/EditorArtworkRenderer';

interface ThemeArtworkProps {
  theme: ThemeItem;
  className?: string;
  showClock?: boolean;
  compact?: boolean;
  portrait?: boolean;
  liveTick?: boolean;
  overlayPlugins?: PluginItem[];
  pomodoroState?: PomodoroRuntimeState;
}

export const ThemeArtwork: React.FC<ThemeArtworkProps> = ({
  theme,
  className = '',
  showClock = true,
  compact = false,
  portrait = false,
  liveTick = false,
  overlayPlugins,
  pomodoroState,
}) => {
  const [imgError, setImgError] = useState(false);
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    setImgError(false);
  }, [theme.imageUrl]);

  useEffect(() => {
    if (!liveTick || theme.editorProject) return;
    const timer = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(timer);
  }, [liveTick, theme.editorProject]);

  if (theme.editorProject) {
    const matchedIdx = theme.editorProject.layouts.findIndex((l) =>
      portrait ? l.size[0] < l.size[1] : l.size[0] >= l.size[1]
    );
    const layoutIdx = matchedIdx >= 0 ? matchedIdx : 0;
    return (
      <div className={`relative overflow-hidden select-none ${className}`}>
        <EditorArtworkRenderer
          project={theme.editorProject}
          layoutIndex={layoutIdx}
          liveTick={liveTick}
          playMotion={showClock}
          interactive={false}
          className="h-full w-full"
        />
        {overlayPlugins && overlayPlugins.length > 0 && (
          <PluginOverlayLayer
            plugins={overlayPlugins}
            portrait={portrait}
            liveTick={liveTick}
            pomodoroState={pomodoroState}
          />
        )}
      </div>
    );
  }

  const pad = (n: number) => String(n).padStart(2, '0');
  const timeStr = liveTick
    ? `${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}`
    : '20:10:58';
  const shortTimeStr = liveTick
    ? `${pad(now.getHours())}:${pad(now.getMinutes())}`
    : '20:10';
  const dateStr = liveTick
    ? `${pad(now.getMonth() + 1)}/${pad(now.getDate())}`
    : '10/27';
  const weekDays = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'];
  const dayStr = liveTick ? weekDays[now.getDay()] : 'MON';

  // Custom calligraphy art for 财源广进
  if (theme.customArt === 'wealth-calligraphy') {
    return (
      <div
        className={`relative overflow-hidden select-none bg-gradient-to-r from-[#EAE2D6] via-[#F2ECE1] to-[#E6DDD0] ${className}`}
      >
        <div
          className="absolute inset-0 opacity-20"
          style={{
            backgroundImage:
              'radial-gradient(circle at 20% 50%, rgba(201,55,44,0.22), transparent 55%), radial-gradient(circle at 80% 50%, rgba(212,163,68,0.25), transparent 50%)',
          }}
        />
        <div
          className={`relative z-10 flex h-full w-full items-center justify-center ${
            portrait ? 'flex-col gap-1 px-1 py-2' : 'gap-3 px-4'
          }`}
        >
          {portrait ? (
            <>
              <span className="font-extrabold leading-none text-[#C8231E] text-sm sm:text-base">
                财
              </span>
              <span className="font-extrabold leading-none text-[#C8231E] text-sm sm:text-base">
                源
              </span>
              <span className="my-0.5 flex h-4 w-4 items-center justify-center rounded border border-[#C8231E]/60 bg-[#C8231E] text-[9px] font-bold text-[#FBE7B2]">
                發
              </span>
              <span className="font-extrabold leading-none text-[#C8231E] text-sm sm:text-base">
                广
              </span>
              <span className="font-extrabold leading-none text-[#C8231E] text-sm sm:text-base">
                进
              </span>
            </>
          ) : (
            <>
              <span
                className={`font-extrabold tracking-[0.18em] text-[#C8231E] drop-shadow-[0_2px_6px_rgba(200,35,30,0.18)] ${
                  compact ? 'text-lg sm:text-xl' : 'text-3xl sm:text-5xl'
                }`}
              >
                财 源
              </span>
              <span
                className={`flex items-center justify-center rounded-md border border-[#C8231E]/60 bg-[#C8231E] font-bold text-[#FBE7B2] shadow-sm ${
                  compact ? 'h-5 w-5 text-[10px]' : 'h-9 w-9 text-sm'
                }`}
              >
                發
              </span>
              <span
                className={`font-extrabold tracking-[0.18em] text-[#C8231E] drop-shadow-[0_2px_6px_rgba(200,35,30,0.18)] ${
                  compact ? 'text-lg sm:text-xl' : 'text-3xl sm:text-5xl'
                }`}
              >
                广 进
              </span>
            </>
          )}
        </div>
        {overlayPlugins && overlayPlugins.length > 0 && (
          <PluginOverlayLayer
            plugins={overlayPlugins}
            portrait={portrait}
            liveTick={liveTick}
            pomodoroState={pomodoroState}
          />
        )}
      </div>
    );
  }

  // Custom 3D aurora prism art matching Figure 2's aesthetic
  if (theme.customArt === 'aurora-prism') {
    return (
      <div
        className={`relative overflow-hidden select-none aurora-dark-card ${className}`}
      >
        <div className="absolute -right-8 -top-8 h-36 w-36 rounded-full bg-[#8B6DFF]/35 blur-2xl" />
        <div className="absolute -bottom-8 left-6 h-32 w-32 rounded-full bg-[#F472B6]/25 blur-2xl" />
        <svg
          viewBox="0 0 400 120"
          className="absolute inset-0 h-full w-full opacity-80"
          preserveAspectRatio="xMidYMid slice"
        >
          <defs>
            <linearGradient id="prismA" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" stopColor="#9B87FF" stopOpacity="0.85" />
              <stop offset="100%" stopColor="#5E45F8" stopOpacity="0.25" />
            </linearGradient>
            <linearGradient id="prismB" x1="0" y1="1" x2="1" y2="0">
              <stop offset="0%" stopColor="#E0B6FF" stopOpacity="0.7" />
              <stop offset="100%" stopColor="#6D4AFF" stopOpacity="0.15" />
            </linearGradient>
          </defs>
          <polygon points="48,95 88,35 128,95" fill="url(#prismA)" stroke="rgba(255,255,255,0.35)" strokeWidth="1" />
          <rect x="278" y="22" width="54" height="54" rx="10" transform="rotate(14 305 49)" fill="url(#prismB)" stroke="rgba(255,255,255,0.4)" strokeWidth="1" />
          <circle cx="348" cy="84" r="24" fill="url(#prismA)" opacity="0.75" />
        </svg>
        {showClock && (
          <div className="relative z-10 flex h-full w-full items-center justify-center px-1">
            <div className={`text-center text-white ${compact && !portrait ? 'scale-75' : ''}`}>
              <div
                className={`font-mono-tabular font-bold tracking-wider drop-shadow-[0_4px_18px_rgba(94,69,248,0.65)] ${
                  portrait ? 'text-sm sm:text-base' : 'text-2xl sm:text-4xl'
                }`}
              >
                {portrait ? shortTimeStr : timeStr}
              </div>
              {(!compact || portrait) && (
                <div
                  className={`mt-1 font-medium text-white/75 ${
                    portrait ? 'text-[9px] tracking-widest' : 'text-xs tracking-[0.22em]'
                  }`}
                >
                  {dateStr} · {dayStr}
                </div>
              )}
            </div>
          </div>
        )}
        {overlayPlugins && overlayPlugins.length > 0 && (
          <PluginOverlayLayer
            plugins={overlayPlugins}
            portrait={portrait}
            liveTick={liveTick}
            pomodoroState={pomodoroState}
          />
        )}
      </div>
    );
  }

  return (
    <div className={`relative overflow-hidden bg-[#1A1436] ${className}`}>
      {theme.imageUrl && !imgError ? (
        <img
          src={theme.imageUrl}
          alt={theme.displayName}
          referrerPolicy="no-referrer"
          onError={() => setImgError(true)}
          className="h-full w-full object-cover object-center transition-transform duration-500 group-hover:scale-[1.03]"
        />
      ) : (
        <div className="flex h-full w-full flex-col items-center justify-center bg-gradient-to-br from-[#2B1B5A] via-[#462B8A] to-[#6B4EFF] text-white/85">
          <Monitor className="h-6 w-6 opacity-80" />
          <span className="mt-1 text-xs font-medium">{theme.displayName}</span>
        </div>
      )}

      {/* Subtle contrast scrim + optional live clock HUD */}
      {showClock && theme.clockStyle && theme.clockStyle !== 'none' && (
        <>
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-r from-black/15 via-black/30 to-black/25" />
          <div
            className={`pointer-events-none absolute inset-0 flex items-center justify-center ${
              portrait ? 'px-1' : 'justify-end pr-4 sm:justify-center sm:pr-0'
            }`}
          >
            {portrait ? (
              <div className="flex flex-col items-center rounded-xl border border-white/35 bg-[#12163A]/60 px-2 py-2 text-center text-white backdrop-blur-md shadow-lg">
                <span className="font-mono-tabular text-[13px] sm:text-[15px] font-bold tracking-wider">
                  {shortTimeStr}
                </span>
                {!compact && (
                  <span className="mt-0.5 font-mono-tabular text-[9px] text-white/80">
                    {dateStr} · {dayStr}
                  </span>
                )}
              </div>
            ) : (
              <>
                {theme.clockStyle === 'crystal' && (
                  <div
                    className={`flex items-center gap-2 sm:gap-3 rounded-xl border border-white/35 bg-[#10204C]/55 backdrop-blur-md shadow-[0_8px_28px_rgba(12,24,70,0.45)] ${
                      compact ? 'px-2.5 py-1' : 'px-5 py-2.5 sm:px-7 sm:py-3.5'
                    }`}
                  >
                    <span
                      className={`font-mono-tabular font-bold tracking-wider text-white drop-shadow-[0_0_14px_rgba(147,197,253,0.85)] ${
                        compact ? 'text-sm sm:text-base' : 'text-2xl sm:text-4xl lg:text-[42px]'
                      }`}
                    >
                      {timeStr}
                    </span>
                    <div
                      className={`border-l border-white/25 pl-2 sm:pl-3 text-center leading-tight text-blue-100 ${
                        compact ? 'text-[9px]' : 'text-xs sm:text-sm'
                      }`}
                    >
                      <div className="font-mono-tabular font-semibold">{dateStr}</div>
                      <div className="text-[10px] tracking-widest opacity-80">{dayStr}</div>
                    </div>
                  </div>
                )}

                {theme.clockStyle === 'sakura' && (
                  <div
                    className={`flex items-center gap-2.5 rounded-xl border border-[#F8C8DC]/40 bg-[#2D1224]/55 backdrop-blur-md shadow-lg ${
                      compact ? 'px-2.5 py-1' : 'px-5 py-2.5 sm:px-6 sm:py-3'
                    }`}
                  >
                    <span
                      className={`font-mono-tabular font-semibold tracking-wider text-[#FDE4EC] ${
                        compact ? 'text-sm' : 'text-2xl sm:text-4xl'
                      }`}
                    >
                      {timeStr}
                    </span>
                    {!compact && (
                      <div className="border-l border-[#F8C8DC]/30 pl-2.5 text-xs text-[#FAD2E0]">
                        <div className="font-mono-tabular">{dateStr}</div>
                        <div className="tracking-widest opacity-80">{dayStr}</div>
                      </div>
                    )}
                  </div>
                )}

                {theme.clockStyle === 'flip-wood' && (
                  <div
                    className={`flex items-center gap-1.5 rounded-xl border border-[#F5E6D3]/35 bg-[#3D2718]/65 backdrop-blur-sm shadow-md ${
                      compact ? 'px-2 py-1' : 'px-4 py-2 sm:px-5 sm:py-2.5'
                    }`}
                  >
                    <span
                      className={`font-mono-tabular font-bold tracking-widest text-[#FFF7ED] ${
                        compact ? 'text-xs sm:text-sm' : 'text-xl sm:text-3xl'
                      }`}
                    >
                      {timeStr}
                    </span>
                  </div>
                )}

                {(theme.clockStyle === 'pastel-ice' || theme.clockStyle === 'minimal-warm') && (
                  <div
                    className={`flex items-center gap-2 rounded-xl border border-white/40 bg-[#1B153A]/40 backdrop-blur-md ${
                      compact ? 'px-2.5 py-1' : 'px-5 py-2 sm:px-6 sm:py-2.5'
                    }`}
                  >
                    <Sparkles className={`text-white/80 ${compact ? 'h-3 w-3' : 'h-4 w-4'}`} />
                    <span
                      className={`font-mono-tabular font-bold tracking-wider text-white ${
                        compact ? 'text-sm' : 'text-2xl sm:text-3xl'
                      }`}
                    >
                      {timeStr}
                    </span>
                  </div>
                )}
              </>
            )}
          </div>
        </>
      )}
      {overlayPlugins && overlayPlugins.length > 0 && (
        <PluginOverlayLayer
          plugins={overlayPlugins}
          portrait={portrait}
          liveTick={liveTick}
          pomodoroState={pomodoroState}
        />
      )}
    </div>
  );
};
