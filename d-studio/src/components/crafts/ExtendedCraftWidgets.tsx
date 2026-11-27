import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  Flame,
  Package,
  Radio,
  Sparkles,
  Sprout,
  Truck,
  Wifi,
} from 'lucide-react';

// ============================================================================
// 1. 赛博电子萌宠 (Cyber Companion Pet)
// ============================================================================

export type CyberPetSpecies = 'mochi' | 'pixel-bot' | 'shiba';
export type CyberPetMood = 'happy' | 'chill' | 'focus';

export interface CraftCyberPetWidgetProps {
  petName?: string;
  species?: CyberPetSpecies;
  mood?: CyberPetMood;
  accentColor?: string;
  compact?: boolean;
  className?: string;
}

const PET_MOOD_COPY: Record<CyberPetMood, { label: string; bubble: string }> = {
  happy: { label: '活力满格', bubble: '今天也要元气满满呀！' },
  chill: { label: '慵懒打盹', bubble: 'Zzz… 休息片刻再出发' },
  focus: { label: '专注陪伴', bubble: '已开启静音护航模式' },
};

export const CraftCyberPetWidget: React.FC<CraftCyberPetWidgetProps> = ({
  petName = 'Mochi',
  species = 'mochi',
  mood = 'happy',
  accentColor = '#F472B6',
  compact = false,
  className = '',
}) => {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const timer = window.setInterval(() => setTick((t) => (t + 1) % 60), 750);
    return () => window.clearInterval(timer);
  }, []);

  const isBlink = tick % 7 === 0;
  const bobOffset = mood === 'happy' ? (tick % 2 === 0 ? -2 : 1) : mood === 'chill' ? (tick % 2 === 0 ? 0 : 1) : -1;
  const energy = mood === 'happy' ? 92 : mood === 'focus' ? 84 : 68;
  const moodInfo = PET_MOOD_COPY[mood] || PET_MOOD_COPY.happy;

  return (
    <div
      className={`flex h-full w-full items-center justify-between gap-2.5 bg-[#0C0B16] text-slate-100 select-none ${
        compact ? 'px-2.5 py-2' : 'px-3.5 py-2.5'
      } ${className}`}
    >
      {/* Animated Pet Avatar Stage */}
      <div
        className={`relative flex shrink-0 items-center justify-center rounded-xl border border-white/10 bg-slate-900/80 ${
          compact ? 'h-12 w-12' : 'h-16 w-16'
        }`}
        style={{ boxShadow: `inset 0 0 18px ${accentColor}22` }}
      >
        <svg
          viewBox="0 0 64 64"
          className="h-[82%] w-[82%] transition-transform duration-300"
          style={{ transform: `translateY(${bobOffset}px)` }}
        >
          {species === 'pixel-bot' ? (
            <>
              <line x1="32" y1="8" x2="32" y2="16" stroke={accentColor} strokeWidth="3" strokeLinecap="round" />
              <circle cx="32" cy="7" r="3.5" fill={accentColor} />
              <rect x="14" y="16" width="36" height="30" rx="8" fill="#1E293B" stroke={accentColor} strokeWidth="2.5" />
              <rect x="20" y="24" width="24" height="14" rx="4" fill="#0F172A" />
              {isBlink || mood === 'chill' ? (
                <>
                  <line x1="24" y1="31" x2="29" y2="31" stroke="#38BDF8" strokeWidth="2.5" strokeLinecap="round" />
                  <line x1="35" y1="31" x2="40" y2="31" stroke="#38BDF8" strokeWidth="2.5" strokeLinecap="round" />
                </>
              ) : (
                <>
                  <circle cx="26.5" cy="31" r="2.6" fill="#38BDF8" />
                  <circle cx="37.5" cy="31" r="2.6" fill="#38BDF8" />
                </>
              )}
              <rect x="22" y="48" width="20" height="6" rx="3" fill="#334155" />
            </>
          ) : species === 'shiba' ? (
            <>
              <polygon points="15,24 20,10 28,20" fill="#F59E0B" />
              <polygon points="49,24 44,10 36,20" fill="#F59E0B" />
              <ellipse cx="32" cy="34" rx="18" ry="15" fill="#FBBF24" />
              <ellipse cx="32" cy="38" rx="13" ry="10" fill="#FEF3C7" />
              {isBlink || mood === 'chill' ? (
                <>
                  <path d="M22 31 Q25 33 28 31" stroke="#1E293B" strokeWidth="2.2" fill="none" strokeLinecap="round" />
                  <path d="M36 31 Q39 33 42 31" stroke="#1E293B" strokeWidth="2.2" fill="none" strokeLinecap="round" />
                </>
              ) : (
                <>
                  <circle cx="25" cy="31" r="2.4" fill="#1E293B" />
                  <circle cx="39" cy="31" r="2.4" fill="#1E293B" />
                </>
              )}
              <ellipse cx="32" cy="36" rx="3" ry="2" fill="#1E293B" />
              <path d="M29 40 Q32 43 35 40" stroke="#1E293B" strokeWidth="1.8" fill="none" strokeLinecap="round" />
            </>
          ) : (
            <>
              <polygon points="16,24 20,10 27,19" fill={accentColor} />
              <polygon points="48,24 44,10 37,19" fill={accentColor} />
              <ellipse cx="32" cy="35" rx="18" ry="15" fill="#1F2937" stroke={accentColor} strokeWidth="2.2" />
              <ellipse cx="22" cy="37" rx="3" ry="1.8" fill="#FB7185" opacity="0.55" />
              <ellipse cx="42" cy="37" rx="3" ry="1.8" fill="#FB7185" opacity="0.55" />
              {isBlink || mood === 'chill' ? (
                <>
                  <line x1="22" y1="32" x2="27" y2="32" stroke="#F8FAFC" strokeWidth="2.2" strokeLinecap="round" />
                  <line x1="37" y1="32" x2="42" y2="32" stroke="#F8FAFC" strokeWidth="2.2" strokeLinecap="round" />
                </>
              ) : (
                <>
                  <circle cx="24.5" cy="32" r="2.5" fill="#F8FAFC" />
                  <circle cx="39.5" cy="32" r="2.5" fill="#F8FAFC" />
                </>
              )}
              <path d="M29 37 Q32 40 35 37" stroke="#F8FAFC" strokeWidth="1.8" fill="none" strokeLinecap="round" />
            </>
          )}
        </svg>
      </div>

      {/* Pet Telemetry */}
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-1.5">
          <span className={`truncate font-bold text-white ${compact ? 'text-[11.5px]' : 'text-[13.5px]'}`}>
            {petName || 'Mochi'}
          </span>
          <span className="shrink-0 font-mono-tabular text-[9.5px] font-semibold" style={{ color: accentColor }}>
            {moodInfo.label}
          </span>
        </div>
        <div className={`mt-0.5 truncate text-slate-400 ${compact ? 'text-[9.5px]' : 'text-[10.5px]'}`}>
          {moodInfo.bubble}
        </div>
        <div className="mt-1.5 flex items-center gap-2">
          <span className="font-mono-tabular text-[9px] text-slate-400">元气值</span>
          <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/10">
            <div
              className="h-full rounded-full transition-all duration-300"
              style={{ width: `${energy}%`, backgroundColor: accentColor }}
            />
          </div>
          <span className="font-mono-tabular text-[9.5px] font-bold text-slate-200">{energy}%</span>
        </div>
      </div>
    </div>
  );
};

// ============================================================================
// 2. 深夜暖炉篝火 (Ambient Campfire)
// ============================================================================

export interface CraftCampfireWidgetProps {
  flameIntensity?: 'low' | 'medium' | 'high';
  emberDensity?: 'low' | 'medium' | 'high';
  showSoundHint?: boolean;
  compact?: boolean;
  className?: string;
}

export const CraftCampfireWidget: React.FC<CraftCampfireWidgetProps> = ({
  flameIntensity = 'medium',
  emberDensity = 'medium',
  showSoundHint = true,
  compact = false,
  className = '',
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let animId = 0;
    let width = Math.max(120, (canvas.width = container.clientWidth || 260));
    let height = Math.max(70, (canvas.height = container.clientHeight || 120));

    const updateSize = () => {
      if (!container) return;
      const nextW = Math.max(120, container.clientWidth);
      const nextH = Math.max(70, container.clientHeight);
      if (nextW !== width || nextH !== height) {
        width = canvas.width = nextW;
        height = canvas.height = nextH;
      }
    };
    const ro = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(updateSize) : null;
    ro?.observe(container);

    const flameScale = flameIntensity === 'high' ? 1.25 : flameIntensity === 'low' ? 0.78 : 1.0;
    const sparkCount = emberDensity === 'high' ? 28 : emberDensity === 'low' ? 10 : 18;
    const sparks = Array.from({ length: sparkCount }, () => ({
      x: width * (0.4 + Math.random() * 0.2),
      y: height * (0.55 + Math.random() * 0.25),
      vy: 0.4 + Math.random() * 1.1,
      vx: (Math.random() - 0.5) * 0.5,
      r: 1 + Math.random() * 1.8,
      life: Math.random(),
    }));

    let time = 0;
    const draw = () => {
      time += 0.04;
      ctx.clearRect(0, 0, width, height);

      // Warm dark backdrop
      const bg = ctx.createRadialGradient(width / 2, height * 0.72, 8, width / 2, height * 0.7, Math.max(width, height) * 0.7);
      bg.addColorStop(0, 'rgba(249, 115, 22, 0.28)');
      bg.addColorStop(0.45, 'rgba(124, 45, 18, 0.16)');
      bg.addColorStop(1, '#09070B');
      ctx.fillStyle = '#09070B';
      ctx.fillRect(0, 0, width, height);
      ctx.fillStyle = bg;
      ctx.fillRect(0, 0, width, height);

      const cx = width / 2;
      const baseY = height * 0.82;
      const fSize = Math.min(width, height) * 0.36 * flameScale;

      // Wood logs
      ctx.save();
      ctx.strokeStyle = '#451A03';
      ctx.lineWidth = Math.max(4, fSize * 0.18);
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(cx - fSize * 0.9, baseY + 4);
      ctx.lineTo(cx + fSize * 0.85, baseY - 4);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(cx + fSize * 0.9, baseY + 4);
      ctx.lineTo(cx - fSize * 0.85, baseY - 4);
      ctx.stroke();
      ctx.restore();

      // Flickering flame tongues
      const layers = [
        { color: 'rgba(234, 88, 12, 0.85)', w: 0.78, h: 1.45, phase: 0 },
        { color: 'rgba(249, 115, 22, 0.92)', w: 0.56, h: 1.18, phase: 1.7 },
        { color: 'rgba(253, 224, 71, 0.95)', w: 0.34, h: 0.82, phase: 3.1 },
      ];
      for (const l of layers) {
        const sway = Math.sin(time * 2.1 + l.phase) * (fSize * 0.14);
        const hBob = Math.cos(time * 2.6 + l.phase) * (fSize * 0.1);
        ctx.fillStyle = l.color;
        ctx.beginPath();
        ctx.moveTo(cx - fSize * l.w, baseY);
        ctx.quadraticCurveTo(
          cx - fSize * l.w * 0.8,
          baseY - fSize * l.h * 0.55,
          cx + sway,
          baseY - fSize * l.h + hBob
        );
        ctx.quadraticCurveTo(
          cx + fSize * l.w * 0.8,
          baseY - fSize * l.h * 0.55,
          cx + fSize * l.w,
          baseY
        );
        ctx.closePath();
        ctx.fill();
      }

      // Embers
      for (const s of sparks) {
        s.y -= s.vy * flameScale;
        s.x += s.vx + Math.sin(time * 2 + s.y * 0.05) * 0.28;
        s.life -= 0.012;
        if (s.y < height * 0.08 || s.life <= 0) {
          s.x = cx + (Math.random() - 0.5) * fSize * 0.9;
          s.y = baseY - Math.random() * 10;
          s.life = 0.7 + Math.random() * 0.3;
        }
        ctx.fillStyle = `rgba(253, 186, 116, ${Math.max(0, s.life)})`;
        ctx.beginPath();
        ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
        ctx.fill();
      }

      animId = window.requestAnimationFrame(draw);
    };

    draw();
    return () => {
      window.cancelAnimationFrame(animId);
      ro?.disconnect();
    };
  }, [flameIntensity, emberDensity]);

  return (
    <div ref={containerRef} className={`relative h-full w-full overflow-hidden select-none ${className}`}>
      <canvas ref={canvasRef} className="block h-full w-full" />
      <div className="pointer-events-none absolute left-2.5 top-2 flex items-center gap-1 text-amber-200/85">
        <Flame className="h-3 w-3 text-orange-400" />
        <span className={`font-semibold ${compact ? 'text-[9.5px]' : 'text-[11px]'}`}>深夜篝火</span>
      </div>
      {showSoundHint && !compact && (
        <div className="pointer-events-none absolute bottom-1.5 right-2.5 font-mono text-[9px] text-amber-300/65">
          壁炉白噪音 · 伴随氛围
        </div>
      )}
    </div>
  );
};

// ============================================================================
// 3. 微景观桌面盆栽 (Zen Bonsai Oasis)
// ============================================================================

export type BonsaiSpecies = 'pine' | 'succulent' | 'sakura';
export type BonsaiSunlight = 'warm' | 'cool' | 'mist';

export interface CraftBonsaiWidgetProps {
  species?: BonsaiSpecies;
  growthDays?: number;
  sunlightTone?: BonsaiSunlight;
  compact?: boolean;
  className?: string;
}

export const CraftBonsaiWidget: React.FC<CraftBonsaiWidgetProps> = ({
  species = 'pine',
  growthDays = 28,
  sunlightTone = 'warm',
  compact = false,
  className = '',
}) => {
  const bgTone =
    sunlightTone === 'warm'
      ? 'from-amber-950/50 via-[#0E1614] to-[#080D0C]'
      : sunlightTone === 'cool'
        ? 'from-sky-950/55 via-[#0B121B] to-[#070A10]'
        : 'from-emerald-950/50 via-[#0B1513] to-[#070B0A]';
  const leafColor = species === 'sakura' ? '#F472B6' : species === 'succulent' ? '#34D399' : '#22C55E';
  const leafSub = species === 'sakura' ? '#FBCFE8' : species === 'succulent' ? '#6EE7B7' : '#86EFAC';
  const speciesLabel = species === 'sakura' ? '春樱盆景' : species === 'succulent' ? '玉露多肉' : '青松微景';

  return (
    <div
      className={`relative flex h-full w-full items-center justify-between gap-2 bg-gradient-to-br ${bgTone} px-3 py-2 text-slate-100 select-none ${className}`}
    >
      <div className="relative flex h-full items-center justify-center">
        <svg viewBox="0 0 80 80" className={compact ? 'h-14 w-14' : 'h-20 w-20'}>
          {/* Pot */}
          <polygon points="22,56 58,56 52,70 28,70" fill="#334155" stroke="#475569" strokeWidth="1.5" />
          <rect x="20" y="53" width="40" height="4" rx="2" fill="#475569" />
          {/* Soil */}
          <ellipse cx="40" cy="53" rx="16" ry="2.2" fill="#292524" />
          {species === 'succulent' ? (
            <g>
              <ellipse cx="40" cy="43" rx="13" ry="9" fill="#059669" />
              <ellipse cx="32" cy="40" rx="8" ry="6" fill={leafColor} />
              <ellipse cx="48" cy="40" rx="8" ry="6" fill={leafColor} />
              <ellipse cx="40" cy="36" rx="7" ry="6" fill={leafSub} />
            </g>
          ) : (
            <g>
              {/* Trunk */}
              <path d="M40 53 Q35 42 42 32 Q45 25 36 22" stroke="#78350F" strokeWidth="4.5" fill="none" strokeLinecap="round" />
              <path d="M40 40 Q48 36 53 33" stroke="#78350F" strokeWidth="2.8" fill="none" strokeLinecap="round" />
              {/* Foliage */}
              <ellipse cx="32" cy="24" rx="14" ry="8" fill={leafColor} opacity="0.92" />
              <ellipse cx="51" cy="31" rx="11" ry="6.5" fill={leafColor} opacity="0.88" />
              <ellipse cx="37" cy="19" rx="10" ry="6" fill={leafSub} opacity="0.92" />
            </g>
          )}
        </svg>
      </div>

      <div className="min-w-0 flex-1 text-right">
        <div className="inline-flex items-center gap-1 text-[10px] font-semibold" style={{ color: leafSub }}>
          <Sprout className="h-3 w-3" />
          <span>{speciesLabel}</span>
        </div>
        <div className={`mt-0.5 font-mono-tabular font-bold text-white ${compact ? 'text-[14px]' : 'text-[18px]'}`}>
          Day {growthDays}
        </div>
        <div className={`mt-0.5 text-slate-400 ${compact ? 'text-[9px]' : 'text-[10px]'}`}>
          {sunlightTone === 'warm' ? '晨光滋养 · 状态良好' : sunlightTone === 'cool' ? '月夜静谧 · 水分充足' : '微雾润叶 · 生长中'}
        </div>
      </div>
    </div>
  );
};

// ============================================================================
// 4. 机械翻页大时钟 (Mechanical Flip Clock)
// ============================================================================

export type FlipShellStyle = 'obsidian' | 'warmwood' | 'titanium';

export interface CraftFlipClockWidgetProps {
  showSeconds?: boolean;
  use24Hour?: boolean;
  showDate?: boolean;
  shellStyle?: FlipShellStyle;
  compact?: boolean;
  now?: Date;
  className?: string;
}

export const CraftFlipClockWidget: React.FC<CraftFlipClockWidgetProps> = ({
  showSeconds = true,
  use24Hour = true,
  showDate = true,
  shellStyle = 'obsidian',
  compact = false,
  now: externalNow,
  className = '',
}) => {
  const [internalNow, setInternalNow] = useState(() => new Date());

  useEffect(() => {
    if (externalNow) return;
    const t = window.setInterval(() => setInternalNow(new Date()), 1000);
    return () => window.clearInterval(t);
  }, [externalNow]);

  const time = externalNow ?? internalNow;
  const rawH = time.getHours();
  const displayH = use24Hour ? rawH : rawH % 12 || 12;
  const hh = String(displayH).padStart(2, '0');
  const mm = String(time.getMinutes()).padStart(2, '0');
  const ss = String(time.getSeconds()).padStart(2, '0');

  const cardTheme =
    shellStyle === 'warmwood'
      ? { bg: 'bg-[#29201B]', border: 'border-[#574236]', num: 'text-[#FEF3C7]', wrapper: 'bg-[#17110E]' }
      : shellStyle === 'titanium'
        ? { bg: 'bg-slate-800', border: 'border-slate-500/50', num: 'text-slate-50', wrapper: 'bg-slate-950' }
        : { bg: 'bg-[#151822]', border: 'border-white/12', num: 'text-white', wrapper: 'bg-[#090B10]' };

  const renderTile = (val: string, unit: string) => (
    <div className="flex flex-col items-center">
      <div
        className={`relative flex items-center justify-center overflow-hidden rounded-lg border shadow-inner ${cardTheme.bg} ${cardTheme.border} ${
          compact ? 'h-10 w-10' : 'h-14 w-14 sm:h-16 sm:w-16'
        }`}
      >
        <div className="absolute inset-x-0 top-0 h-1/2 bg-white/[0.04]" />
        <div className="absolute inset-x-0 top-1/2 z-10 h-[1.5px] bg-black/75 shadow-[0_1px_0_rgba(255,255,255,0.08)]" />
        <div className="absolute left-0 top-1/2 z-10 h-2 w-0.5 -translate-y-1/2 bg-black/80" />
        <div className="absolute right-0 top-1/2 z-10 h-2 w-0.5 -translate-y-1/2 bg-black/80" />
        <span className={`font-mono-tabular font-black tracking-tight ${cardTheme.num} ${compact ? 'text-[19px]' : 'text-[28px]'}`}>
          {val}
        </span>
      </div>
      {!compact && <span className="mt-1 font-mono text-[8.5px] tracking-widest text-slate-500">{unit}</span>}
    </div>
  );

  return (
    <div className={`flex h-full w-full flex-col items-center justify-center ${cardTheme.wrapper} p-2.5 select-none ${className}`}>
      <div className={`flex items-center ${compact ? 'gap-1.5' : 'gap-2'}`}>
        {renderTile(hh, 'HR')}
        <span className={`font-mono font-bold text-slate-500 ${compact ? 'text-sm' : 'text-xl -mt-3'}`}>:</span>
        {renderTile(mm, 'MIN')}
        {showSeconds && (
          <>
            <span className={`font-mono font-bold text-slate-500 ${compact ? 'text-sm' : 'text-xl -mt-3'}`}>:</span>
            {renderTile(ss, 'SEC')}
          </>
        )}
      </div>
      {showDate && (
        <div className={`mt-1 font-mono text-slate-400 ${compact ? 'text-[8.5px]' : 'text-[10px]'}`}>
          {time.toLocaleDateString('zh-CN', { month: '2-digit', day: '2-digit', weekday: 'short' })}
          {!use24Hour && ` · ${rawH >= 12 ? 'PM' : 'AM'}`}
        </div>
      )}
    </div>
  );
};

// ============================================================================
// 5. 双通道模拟 VU 电平表 (Analog VU Meter)
// ============================================================================

export type VuBacklightHue = 'warm-amber' | 'emerald' | 'ice-blue';
export type VuSensitivity = 'smooth' | 'balanced' | 'punchy';

export interface CraftVuMeterWidgetProps {
  backlightHue?: VuBacklightHue;
  sensitivity?: VuSensitivity;
  deckLabel?: string;
  compact?: boolean;
  className?: string;
}

export const CraftVuMeterWidget: React.FC<CraftVuMeterWidgetProps> = ({
  backlightHue = 'warm-amber',
  sensitivity = 'balanced',
  deckLabel = 'MASTER STEREO',
  compact = false,
  className = '',
}) => {
  const [levels, setLevels] = useState<[number, number]>([0.45, 0.52]);

  useEffect(() => {
    const intervalMs = sensitivity === 'punchy' ? 140 : sensitivity === 'smooth' ? 280 : 200;
    let phase = 0;
    const timer = window.setInterval(() => {
      phase += 0.45;
      const baseL = 0.42 + Math.sin(phase * 1.1) * 0.25 + (Math.random() - 0.35) * 0.28;
      const baseR = 0.44 + Math.cos(phase * 0.9) * 0.25 + (Math.random() - 0.35) * 0.28;
      setLevels([Math.max(0.05, Math.min(0.98, baseL)), Math.max(0.05, Math.min(0.98, baseR))]);
    }, intervalMs);
    return () => window.clearInterval(timer);
  }, [sensitivity]);

  const dialColors =
    backlightHue === 'emerald'
      ? { face: 'from-[#062C22] to-[#041712]', glow: '#10B981', needle: '#34D399' }
      : backlightHue === 'ice-blue'
        ? { face: 'from-[#0B253A] to-[#061320]', glow: '#38BDF8', needle: '#7DD3FC' }
        : { face: 'from-[#32220B] to-[#1A1105]', glow: '#F59E0B', needle: '#FDE68A' };

  const renderDial = (ch: 'L' | 'R', level: number) => {
    const angleDeg = -42 + level * 84;
    const isPeak = level > 0.78;
    return (
      <div
        className={`relative flex flex-1 flex-col items-center justify-end overflow-hidden rounded-lg border border-white/15 bg-gradient-to-b ${dialColors.face} p-1.5 shadow-inner`}
      >
        <svg viewBox="0 0 120 68" className="h-full w-full">
          {/* Scale arc */}
          <path d="M 18 54 A 46 46 0 0 1 84 22" fill="none" stroke={dialColors.glow} strokeWidth="2" opacity="0.55" />
          <path d="M 84 22 A 46 46 0 0 1 102 54" fill="none" stroke="#EF4444" strokeWidth="2.5" opacity="0.85" />
          {/* Tick marks */}
          {[-40, -25, -10, 5, 20, 36].map((deg, idx) => (
            <line
              key={idx}
              x1="60"
              y1="14"
              x2="60"
              y2="20"
              stroke={deg > 10 ? '#F87171' : dialColors.glow}
              strokeWidth="1.5"
              transform={`rotate(${deg} 60 62)`}
            />
          ))}
          <text x="60" y="39" textAnchor="middle" fill={dialColors.glow} fontSize="9" fontWeight="700" opacity="0.75">
            VU · {ch}
          </text>
          {/* Needle */}
          <line
            x1="60"
            y1="62"
            x2="60"
            y2="14"
            stroke={dialColors.needle}
            strokeWidth="2"
            strokeLinecap="round"
            style={{
              transformOrigin: '60px 62px',
              transform: `rotate(${angleDeg}deg)`,
              transition: 'transform 140ms cubic-bezier(0.16, 1, 0.3, 1)',
            }}
          />
          <circle cx="60" cy="62" r="4" fill="#0F172A" stroke={dialColors.glow} strokeWidth="1.5" />
        </svg>
        <span
          className={`absolute right-1.5 top-1.5 h-1.5 w-1.5 rounded-full transition-opacity ${
            isPeak ? 'bg-red-500 opacity-100 shadow-[0_0_6px_#EF4444]' : 'bg-red-950 opacity-40'
          }`}
        />
      </div>
    );
  };

  return (
    <div className={`flex h-full w-full flex-col justify-between bg-[#0E0F14] text-slate-100 select-none ${compact ? 'p-2' : 'p-2.5'} ${className}`}>
      <div className="flex items-center justify-between text-[9px] font-mono text-slate-400">
        <span className="truncate font-semibold text-slate-200">{deckLabel}</span>
        <span>-20 ~ +3 dB</span>
      </div>
      <div className="mt-1 flex flex-1 gap-2 min-h-0">
        {renderDial('L', levels[0])}
        {renderDial('R', levels[1])}
      </div>
    </div>
  );
};

// ============================================================================
// 6. 复古磁带卡座 (Retro Cassette Deck)
// ============================================================================

export type CassetteShellColor = 'smoke' | 'amber' | 'indigo';

export interface CraftCassetteWidgetProps {
  trackTitle?: string;
  artistLabel?: string;
  shellColor?: CassetteShellColor;
  tapeSpeed?: number;
  compact?: boolean;
  className?: string;
}

export const CraftCassetteWidget: React.FC<CraftCassetteWidgetProps> = ({
  trackTitle = 'City Pop Tape #04',
  artistLabel = 'SIDE A · 90MIN',
  shellColor = 'smoke',
  tapeSpeed = 1.0,
  compact = false,
  className = '',
}) => {
  const shellStyle =
    shellColor === 'amber'
      ? 'bg-[#28180A] border-amber-500/35'
      : shellColor === 'indigo'
        ? 'bg-[#12162F] border-indigo-400/35'
        : 'bg-[#15171E] border-white/15';
  const spinSec = (3.4 / Math.max(0.4, tapeSpeed)).toFixed(2);

  return (
    <div className={`flex h-full w-full items-center justify-center bg-[#09090D] p-2 select-none ${className}`}>
      <div className={`flex h-full w-full flex-col justify-between rounded-xl border p-2 ${shellStyle}`}>
        {/* Paper label strip */}
        <div className="flex items-center justify-between rounded-md bg-amber-100/90 px-2 py-0.5 text-slate-900">
          <span className={`truncate font-bold ${compact ? 'text-[9.5px]' : 'text-[11px]'}`}>{trackTitle}</span>
          <span className="shrink-0 font-mono text-[8.5px] font-semibold text-slate-600">{artistLabel}</span>
        </div>

        {/* Window with two spinning spools */}
        <div className="my-1 flex flex-1 items-center justify-around rounded-lg border border-white/10 bg-black/65 px-3 py-1">
          {[0, 1].map((idx) => (
            <div
              key={idx}
              className={`relative flex items-center justify-center rounded-full border-2 border-dashed border-slate-300/70 ${
                compact ? 'h-6 w-6' : 'h-8 w-8'
              }`}
              style={{ animation: `teOrbitSpin ${spinSec}s linear infinite` }}
            >
              <span className="h-2 w-2 rounded-full bg-amber-400/90" />
            </div>
          ))}
        </div>

        {/* Bottom tape trapezoid hint */}
        <div className="mx-auto flex w-[68%] items-center justify-between rounded-t border border-b-0 border-white/15 bg-black/50 px-3 py-0.5 text-[8px] font-mono text-slate-400">
          <span>DOLBY NR</span>
          <span>CH-II</span>
        </div>
      </div>
    </div>
  );
};

// ============================================================================
// 7. 今日星座能量签 (Daily Zodiac Oracle)
// ============================================================================

export type ZodiacSignKey =
  | 'aries'
  | 'taurus'
  | 'gemini'
  | 'cancer'
  | 'leo'
  | 'virgo'
  | 'libra'
  | 'scorpio'
  | 'sagittarius'
  | 'capricorn'
  | 'aquarius'
  | 'pisces';

const ZODIAC_DATA: Record<
  ZodiacSignKey,
  { name: string; focus: number; luck: number; luckyColor: string; motto: string }
> = {
  aries: { name: '白羊座', focus: 88, luck: 91, luckyColor: '焰橙', motto: '果断推进新灵感，行动力拉满' },
  taurus: { name: '金牛座', focus: 92, luck: 85, luckyColor: '琥珀', motto: '稳步收网关键任务，收获踏实成果' },
  gemini: { name: '双子座', focus: 84, luck: 89, luckyColor: '天青', motto: '沟通协作高效顺畅，创意频出' },
  cancer: { name: '巨蟹座', focus: 86, luck: 90, luckyColor: '月银', motto: '守护专注节奏，适合深度创作' },
  leo: { name: '狮子座', focus: 90, luck: 94, luckyColor: '耀金', motto: '舞台聚光聚焦于你，自信表达' },
  virgo: { name: '处女座', focus: 95, luck: 86, luckyColor: '松绿', motto: '细节打磨臻于完美，效率拔群' },
  libra: { name: '天秤座', focus: 87, luck: 92, luckyColor: '暮紫', motto: '平衡工作与松弛感，美感在线' },
  scorpio: { name: '天蝎座', focus: 94, luck: 88, luckyColor: '深绯', motto: '洞察力敏锐，适合攻克难点' },
  sagittarius: { name: '射手座', focus: 83, luck: 93, luckyColor: '湖蓝', motto: '跨界探索带来意外惊喜' },
  capricorn: { name: '摩羯座', focus: 96, luck: 87, luckyColor: '墨岩', motto: '长期主义稳步兑现里程碑' },
  aquarius: { name: '水瓶座', focus: 89, luck: 91, luckyColor: '霓虹青', motto: '跳出定式思维，解锁奇思妙想' },
  pisces: { name: '双鱼座', focus: 85, luck: 95, luckyColor: '星粉', motto: '直觉敏锐温柔，艺术感饱满' },
};

export interface CraftZodiacWidgetProps {
  sign?: ZodiacSignKey;
  showConstellationGlow?: boolean;
  accentColor?: string;
  compact?: boolean;
  className?: string;
}

export const CraftZodiacWidget: React.FC<CraftZodiacWidgetProps> = ({
  sign = 'aquarius',
  showConstellationGlow = true,
  accentColor = '#A78BFA',
  compact = false,
  className = '',
}) => {
  const info = ZODIAC_DATA[sign] || ZODIAC_DATA.aquarius;

  return (
    <div
      className={`relative flex h-full w-full items-center justify-between gap-2.5 overflow-hidden bg-[#100D1E] text-slate-100 select-none ${
        compact ? 'px-2.5 py-2' : 'px-3.5 py-2.5'
      } ${className}`}
    >
      {/* Constellation Star Diagram */}
      <div className={`relative flex shrink-0 items-center justify-center rounded-full border border-white/10 bg-white/[0.03] ${compact ? 'h-12 w-12' : 'h-16 w-16'}`}>
        {showConstellationGlow && (
          <div
            className="absolute inset-1 rounded-full blur-md"
            style={{ backgroundColor: `${accentColor}30` }}
          />
        )}
        <svg viewBox="0 0 60 60" className="relative h-[80%] w-[80%]">
          <polyline
            points="12,38 24,22 38,26 48,14"
            fill="none"
            stroke={accentColor}
            strokeWidth="1.5"
            strokeDasharray="2 2"
          />
          <polyline points="24,22 29,44 46,42" fill="none" stroke={accentColor} strokeWidth="1.3" opacity="0.7" />
          {[
            [12, 38],
            [24, 22],
            [38, 26],
            [48, 14],
            [29, 44],
            [46, 42],
          ].map(([cx, cy], idx) => (
            <circle key={idx} cx={cx} cy={cy} r={idx % 2 === 0 ? 2.4 : 1.8} fill="#F8FAFC" />
          ))}
        </svg>
      </div>

      {/* Horoscope Details */}
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-1.5">
          <div className="flex items-center gap-1 font-bold text-white">
            <Sparkles className="h-3 w-3 shrink-0" style={{ color: accentColor }} />
            <span className={compact ? 'text-[11.5px]' : 'text-[13px]'}>{info.name}</span>
          </div>
          <span className="font-mono-tabular text-[9.5px] font-semibold" style={{ color: accentColor }}>
            幸运色 · {info.luckyColor}
          </span>
        </div>
        <div className={`mt-0.5 line-clamp-1 text-slate-300 ${compact ? 'text-[9.5px]' : 'text-[10.5px]'}`}>
          {info.motto}
        </div>
        <div className="mt-1.5 flex items-center gap-3 font-mono-tabular text-[9.5px] text-slate-400">
          <span>
            专注 <strong className="text-white">{info.focus}%</strong>
          </span>
          <span>
            幸运 <strong className="text-white">{info.luck}%</strong>
          </span>
        </div>
      </div>
    </div>
  );
};

// ============================================================================
// 8. 快递物流追踪卡 (Parcel Delivery Radar)
// ============================================================================

export type ParcelStage = 'collected' | 'transit' | 'delivering' | 'delivered';

const PARCEL_STEPS: { id: ParcelStage; label: string }[] = [
  { id: 'collected', label: '已揽收' },
  { id: 'transit', label: '干线中' },
  { id: 'delivering', label: '派送中' },
  { id: 'delivered', label: '已签收' },
];

export interface CraftParcelWidgetProps {
  parcelName?: string;
  carrier?: string;
  stage?: ParcelStage;
  accentColor?: string;
  compact?: boolean;
  className?: string;
}

export const CraftParcelWidget: React.FC<CraftParcelWidgetProps> = ({
  parcelName = '客制化机械键盘套件',
  carrier = '顺丰速运',
  stage = 'delivering',
  accentColor = '#38BDF8',
  compact = false,
  className = '',
}) => {
  const activeIdx = Math.max(0, PARCEL_STEPS.findIndex((s) => s.id === stage));

  return (
    <div
      className={`flex h-full w-full flex-col justify-between bg-[#0B121D] text-slate-100 select-none ${
        compact ? 'p-2.5' : 'p-3'
      } ${className}`}
    >
      <div className="flex items-center justify-between gap-2">
        <div className="flex min-w-0 items-center gap-1.5">
          {stage === 'delivered' ? (
            <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-emerald-400" />
          ) : stage === 'delivering' ? (
            <Truck className="h-3.5 w-3.5 shrink-0" style={{ color: accentColor }} />
          ) : (
            <Package className="h-3.5 w-3.5 shrink-0" style={{ color: accentColor }} />
          )}
          <span className={`truncate font-bold text-white ${compact ? 'text-[11px]' : 'text-[12.5px]'}`}>
            {parcelName}
          </span>
        </div>
        <span className="shrink-0 font-mono text-[9.5px] text-slate-400">{carrier}</span>
      </div>

      {/* 4-stage timeline */}
      <div className="my-1 flex items-center justify-between gap-1">
        {PARCEL_STEPS.map((st, idx) => {
          const done = idx <= activeIdx;
          const current = idx === activeIdx;
          return (
            <React.Fragment key={st.id}>
              <div className="flex flex-col items-center">
                <span
                  className={`h-2 w-2 rounded-full transition-all ${
                    current ? 'scale-125 ring-2 ring-white/25' : ''
                  }`}
                  style={{
                    backgroundColor: done ? accentColor : 'rgba(255,255,255,0.16)',
                  }}
                />
                <span
                  className={`mt-1 font-medium ${compact ? 'text-[8.5px]' : 'text-[9.5px]'} ${
                    done ? 'text-white' : 'text-slate-500'
                  }`}
                >
                  {st.label}
                </span>
              </div>
              {idx < PARCEL_STEPS.length - 1 && (
                <div
                  className="-mt-3 h-0.5 flex-1 rounded-full"
                  style={{
                    backgroundColor: idx < activeIdx ? accentColor : 'rgba(255,255,255,0.12)',
                  }}
                />
              )}
            </React.Fragment>
          );
        })}
      </div>

      <div className="flex items-center justify-between font-mono text-[9px] text-slate-400">
        <span>状态：{PARCEL_STEPS[activeIdx]?.label}</span>
        <span>{stage === 'delivered' ? '已完成派送' : '预计今日送达'}</span>
      </div>
    </div>
  );
};

// ============================================================================
// 9. 纪念日倒数日历 (Milestone Countdown)
// ============================================================================

export type MilestoneMode = 'countdown' | 'countup';

export interface CraftCountdownWidgetProps {
  milestoneTitle?: string;
  targetDays?: number;
  countMode?: MilestoneMode;
  targetDateLabel?: string;
  accentColor?: string;
  compact?: boolean;
  className?: string;
}

export const CraftCountdownWidget: React.FC<CraftCountdownWidgetProps> = ({
  milestoneTitle = '年度版本发布',
  targetDays = 18,
  countMode = 'countdown',
  targetDateLabel = '2026.12.15',
  accentColor = '#F59E0B',
  compact = false,
  className = '',
}) => {
  const safeDays = Math.max(0, Math.min(9999, Math.round(targetDays)));
  const pct = countMode === 'countdown' ? Math.max(12, Math.min(95, 100 - (safeDays % 100))) : 78;
  const r = 20;
  const c = 2 * Math.PI * r;
  const dashOffset = c * (1 - pct / 100);

  return (
    <div
      className={`flex h-full w-full items-center justify-between gap-2.5 bg-[#12101B] text-slate-100 select-none ${
        compact ? 'px-2.5 py-2' : 'px-3.5 py-2.5'
      } ${className}`}
    >
      <div className="relative flex shrink-0 items-center justify-center">
        <svg viewBox="0 0 52 52" className={compact ? 'h-11 w-11' : 'h-14 w-14'}>
          <circle cx="26" cy="26" r={r} fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="4" />
          <circle
            cx="26"
            cy="26"
            r={r}
            fill="none"
            stroke={accentColor}
            strokeWidth="4"
            strokeLinecap="round"
            strokeDasharray={c}
            strokeDashoffset={dashOffset}
            transform="rotate(-90 26 26)"
          />
        </svg>
        <span className="absolute font-mono-tabular text-[10px] font-bold text-white">{pct}%</span>
      </div>

      <div className="min-w-0 flex-1 text-right">
        <div className="truncate text-[10px] font-medium text-slate-400">
          {countMode === 'countdown' ? '距离' : '已走过'} · {milestoneTitle}
        </div>
        <div className="mt-0.5 flex items-baseline justify-end gap-1">
          <span
            className={`font-mono-tabular font-black leading-none ${compact ? 'text-[18px]' : 'text-[24px]'}`}
            style={{ color: accentColor }}
          >
            {safeDays}
          </span>
          <span className="text-[10.5px] font-semibold text-slate-300">天</span>
        </div>
        <div className="mt-0.5 font-mono-tabular text-[9px] text-slate-500">{targetDateLabel}</div>
      </div>
    </div>
  );
};

// ============================================================================
// 10. 实时网速雷达 (NetSpeed & Ping Radar)
// ============================================================================

export type NetUnitMode = 'Mbps' | 'MB/s';

export interface CraftNetSpeedWidgetProps {
  unitMode?: NetUnitMode;
  refreshSeconds?: number;
  showPingChart?: boolean;
  accentColor?: string;
  compact?: boolean;
  className?: string;
}

export const CraftNetSpeedWidget: React.FC<CraftNetSpeedWidgetProps> = ({
  unitMode = 'Mbps',
  refreshSeconds = 2,
  showPingChart = true,
  accentColor = '#10B981',
  compact = false,
  className = '',
}) => {
  const [samples, setSamples] = useState<number[]>([210, 265, 240, 312, 288, 345, 326, 378]);
  const [upload, setUpload] = useState<number>(64.2);
  const [pingMs, setPingMs] = useState<number>(14);

  useEffect(() => {
    const ms = Math.max(800, Math.min(5000, refreshSeconds * 1000));
    const timer = window.setInterval(() => {
      setSamples((prev) => {
        const nextDl = Math.round(180 + Math.random() * 240);
        return [...prev.slice(1), nextDl];
      });
      setUpload(Number((42 + Math.random() * 48).toFixed(1)));
      setPingMs(Math.round(11 + Math.random() * 9));
    }, ms);
    return () => window.clearInterval(timer);
  }, [refreshSeconds]);

  const rawDl = samples[samples.length - 1] || 320;
  const dlDisplay = unitMode === 'MB/s' ? (rawDl / 8).toFixed(1) : String(rawDl);
  const ulDisplay = unitMode === 'MB/s' ? (upload / 8).toFixed(1) : upload.toFixed(1);

  const chartPoints = useMemo(() => {
    const w = 160;
    const h = 34;
    const max = Math.max(400, ...samples);
    return samples
      .map((val, idx) => {
        const x = (idx / Math.max(1, samples.length - 1)) * w;
        const y = h - (val / max) * (h - 6) - 3;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }, [samples]);

  return (
    <div
      className={`flex h-full w-full flex-col justify-between bg-[#081018] text-slate-100 select-none ${
        compact ? 'p-2.5' : 'p-3'
      } ${className}`}
    >
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-1.5 text-[10px] font-semibold text-slate-300">
          <Wifi className="h-3 w-3 shrink-0" style={{ color: accentColor }} />
          <span>实时网速雷达</span>
        </div>
        <div className="flex items-center gap-1 font-mono-tabular text-[9.5px] text-slate-400">
          <Radio className="h-2.5 w-2.5" style={{ color: accentColor }} />
          <span>{pingMs} ms</span>
        </div>
      </div>

      <div className="my-1 flex items-baseline justify-between gap-2 font-mono-tabular">
        <div>
          <span className="text-[9px] text-slate-400">↓ 下行 </span>
          <span className={`font-bold text-white ${compact ? 'text-[14px]' : 'text-[18px]'}`}>{dlDisplay}</span>
          <span className="ml-0.5 text-[9px] text-slate-400">{unitMode}</span>
        </div>
        <div className="text-right">
          <span className="text-[9px] text-slate-400">↑ 上行 </span>
          <span className={`font-bold ${compact ? 'text-[11.5px]' : 'text-[14px]'}`} style={{ color: accentColor }}>
            {ulDisplay}
          </span>
          <span className="ml-0.5 text-[9px] text-slate-400">{unitMode}</span>
        </div>
      </div>

      {showPingChart && (
        <div className="h-6 w-full overflow-hidden rounded border border-white/10 bg-slate-950/65 px-1 py-0.5">
          <svg viewBox="0 0 160 34" preserveAspectRatio="none" className="h-full w-full">
            <polyline
              points={chartPoints}
              fill="none"
              stroke={accentColor}
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </div>
      )}
    </div>
  );
};
