import React, { useEffect, useRef, useState } from 'react';
import {
  ArrowDownRight,
  ArrowUpRight,
  Disc3,
  Pause,
  Play,
  SkipForward,
} from 'lucide-react';

// ============================================================================
// 1. 水族箱生态 (craft-aquarium)
// ============================================================================

interface Fish {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  color: string;
  finColor: string;
  tailPhase: number;
  speed: number;
  direction: 1 | -1;
  species: 'tang' | 'clown' | 'tetra' | 'guppy';
}

interface Bubble {
  x: number;
  y: number;
  radius: number;
  vy: number;
  wobbleSpeed: number;
  wobbleAmp: number;
  phase: number;
}

interface FoodPellet {
  x: number;
  y: number;
  vy: number;
}

export interface CraftAquariumWidgetProps {
  fishCount?: number;
  causticsIntensity?: number;
  bubbleSpeed?: number;
  waterTint?: string;
  showClock?: boolean;
  compact?: boolean;
  interactive?: boolean;
  className?: string;
}

export const CraftAquariumWidget: React.FC<CraftAquariumWidgetProps> = ({
  fishCount = 7,
  causticsIntensity = 0.6,
  bubbleSpeed = 1.0,
  waterTint = '#041728',
  showClock = true,
  compact = false,
  interactive = true,
  className = '',
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const [timeStr, setTimeStr] = useState<string>('');
  const [dateStr, setDateStr] = useState<string>('');

  useEffect(() => {
    if (!showClock) return;
    const updateTime = () => {
      const now = new Date();
      setTimeStr(
        now.toLocaleTimeString('en-US', {
          hour12: false,
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
        })
      );
      setDateStr(
        now.toLocaleDateString('en-US', {
          weekday: 'short',
          month: 'short',
          day: 'numeric',
        })
      );
    };
    updateTime();
    const interval = window.setInterval(updateTime, 1000);
    return () => window.clearInterval(interval);
  }, [showClock]);

  useEffect(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let animationFrameId = 0;
    let width = Math.max(120, (canvas.width = container.clientWidth || 320));
    let height = Math.max(70, (canvas.height = container.clientHeight || 140));

    const updateSize = () => {
      if (!container) return;
      const nextW = Math.max(120, container.clientWidth);
      const nextH = Math.max(70, container.clientHeight);
      if (nextW !== width || nextH !== height) {
        width = canvas.width = nextW;
        height = canvas.height = nextH;
      }
    };

    const resizeObserver =
      typeof ResizeObserver !== 'undefined' ? new ResizeObserver(updateSize) : null;
    resizeObserver?.observe(container);
    window.addEventListener('resize', updateSize);

    const scaleFactor = compact ? Math.max(0.45, Math.min(0.85, height / 210)) : Math.max(0.65, Math.min(1.15, height / 240));

    const speciesList: Array<{
      species: Fish['species'];
      color: string;
      finColor: string;
      size: number;
      speed: number;
    }> = [
      { species: 'clown', color: '#F97316', finColor: '#FFFFFF', size: 22 * scaleFactor, speed: 1.15 },
      { species: 'tang', color: '#3B82F6', finColor: '#FACC15', size: 25 * scaleFactor, speed: 0.95 },
      { species: 'tetra', color: '#06B6D4', finColor: '#EF4444', size: 16 * scaleFactor, speed: 1.4 },
      { species: 'guppy', color: '#EC4899', finColor: '#A855F7', size: 18 * scaleFactor, speed: 1.2 },
    ];

    const safeFishCount = Math.max(1, Math.min(16, fishCount));
    const fishes: Fish[] = [];
    for (let i = 0; i < safeFishCount; i++) {
      const spec = speciesList[i % speciesList.length];
      const dir: 1 | -1 = Math.random() > 0.5 ? 1 : -1;
      fishes.push({
        x: Math.random() * width,
        y: Math.max(18, Math.min(height - 18, 20 + Math.random() * Math.max(20, height - 40))),
        vx: dir * (spec.speed * (0.75 + Math.random() * 0.4)),
        vy: (Math.random() - 0.5) * 0.35,
        size: spec.size,
        color: spec.color,
        finColor: spec.finColor,
        tailPhase: Math.random() * Math.PI * 2,
        speed: spec.speed,
        direction: dir,
        species: spec.species,
      });
    }

    const bubbleTotal = compact ? 16 : 26;
    const bubbles: Bubble[] = [];
    for (let i = 0; i < bubbleTotal; i++) {
      bubbles.push({
        x: Math.random() * width,
        y: Math.random() * height,
        radius: (1.2 + Math.random() * 2.8) * Math.max(0.65, scaleFactor),
        vy: (0.45 + Math.random() * 1.1) * bubbleSpeed,
        wobbleSpeed: 0.03 + Math.random() * 0.04,
        wobbleAmp: 0.7 + Math.random() * 1.3,
        phase: Math.random() * Math.PI * 2,
      });
    }

    const foodPellets: FoodPellet[] = [];

    const handleCanvasClick = (e: MouseEvent) => {
      if (!interactive) return;
      const rect = canvas.getBoundingClientRect();
      const clickX = e.clientX - rect.left;
      const clickY = e.clientY - rect.top;
      for (let i = 0; i < 4; i++) {
        foodPellets.push({
          x: clickX + (Math.random() - 0.5) * 18,
          y: clickY + (Math.random() - 0.5) * 8,
          vy: 0.45 + Math.random() * 0.35,
        });
      }
    };
    if (interactive) {
      canvas.addEventListener('click', handleCanvasClick);
    }

    let time = 0;
    const render = () => {
      time += 0.016;
      ctx.clearRect(0, 0, width, height);

      // 1. Deep Ocean Gradient
      const bgGrad = ctx.createLinearGradient(0, 0, 0, height);
      bgGrad.addColorStop(0, '#072a4a');
      bgGrad.addColorStop(0.45, waterTint);
      bgGrad.addColorStop(1, '#020b14');
      ctx.fillStyle = bgGrad;
      ctx.fillRect(0, 0, width, height);

      // 2. Light Rays / Water Caustics
      if (causticsIntensity > 0.05) {
        ctx.save();
        ctx.globalCompositeOperation = 'screen';
        const numRays = 6;
        for (let i = 0; i < numRays; i++) {
          const rayX = (width / (numRays + 1)) * (i + 1) + Math.sin(time * 0.6 + i * 1.2) * (width * 0.05);
          const rayGrad = ctx.createLinearGradient(rayX, 0, rayX + (i % 2 === 0 ? 40 : -40), height);
          rayGrad.addColorStop(0, `rgba(56, 189, 248, ${0.14 * causticsIntensity})`);
          rayGrad.addColorStop(0.55, `rgba(14, 165, 233, ${0.05 * causticsIntensity})`);
          rayGrad.addColorStop(1, 'rgba(14, 165, 233, 0)');
          ctx.fillStyle = rayGrad;
          ctx.beginPath();
          ctx.moveTo(rayX - width * 0.04, 0);
          ctx.lineTo(rayX + width * 0.04, 0);
          ctx.lineTo(rayX + (i % 2 === 0 ? width * 0.14 : -width * 0.14), height);
          ctx.lineTo(rayX + (i % 2 === 0 ? width * 0.03 : -width * 0.22), height);
          ctx.closePath();
          ctx.fill();
        }
        ctx.restore();
      }

      // 3. Food Pellets
      for (let i = foodPellets.length - 1; i >= 0; i--) {
        const p = foodPellets[i];
        p.y += p.vy;
        p.x += Math.sin(p.y * 0.08) * 0.3;
        ctx.fillStyle = '#F59E0B';
        ctx.shadowColor = 'rgba(245, 158, 11, 0.65)';
        ctx.shadowBlur = 5;
        ctx.beginPath();
        ctx.arc(p.x, p.y, 2.2, 0, Math.PI * 2);
        ctx.fill();
        ctx.shadowBlur = 0;
        if (p.y > height) foodPellets.splice(i, 1);
      }

      // 4. Bubbles
      ctx.save();
      for (const b of bubbles) {
        b.phase += b.wobbleSpeed;
        b.y -= b.vy;
        const currentX = b.x + Math.sin(b.phase) * b.wobbleAmp;
        if (b.y < -8) {
          b.y = height + 8;
          b.x = Math.random() * width;
        }
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.45)';
        ctx.lineWidth = 1;
        ctx.fillStyle = 'rgba(186, 230, 253, 0.15)';
        ctx.beginPath();
        ctx.arc(currentX, b.y, b.radius, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.beginPath();
        ctx.arc(currentX - b.radius * 0.3, b.y - b.radius * 0.3, b.radius * 0.25, 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();

      // 5. Fishes
      for (const fish of fishes) {
        if (foodPellets.length > 0) {
          let closestDist = Infinity;
          let targetFood: FoodPellet | null = null;
          for (const f of foodPellets) {
            const dist = Math.hypot(f.x - fish.x, f.y - fish.y);
            if (dist < closestDist && dist < 180) {
              closestDist = dist;
              targetFood = f;
            }
          }
          if (targetFood) {
            const angle = Math.atan2(targetFood.y - fish.y, targetFood.x - fish.x);
            fish.vx += Math.cos(angle) * 0.05;
            fish.vy += Math.sin(angle) * 0.05;
            fish.direction = fish.vx >= 0 ? 1 : -1;
            if (closestDist < 10) {
              const idx = foodPellets.indexOf(targetFood);
              if (idx !== -1) foodPellets.splice(idx, 1);
            }
          }
        }

        const spd = Math.hypot(fish.vx, fish.vy);
        const maxSpeed = fish.speed * 1.35;
        if (spd > maxSpeed && spd > 0) {
          fish.vx = (fish.vx / spd) * maxSpeed;
          fish.vy = (fish.vy / spd) * maxSpeed;
        }

        fish.x += fish.vx;
        fish.y += fish.vy;
        fish.tailPhase += 0.18 * (spd + 0.5);
        fish.vy += Math.sin(time + fish.tailPhase * 0.5) * 0.015;

        if (fish.x > width + 30 && fish.direction === 1) {
          fish.x = -30;
          fish.y = Math.max(16, Math.min(height - 16, 18 + Math.random() * Math.max(20, height - 36)));
        } else if (fish.x < -30 && fish.direction === -1) {
          fish.x = width + 30;
          fish.y = Math.max(16, Math.min(height - 16, 18 + Math.random() * Math.max(20, height - 36)));
        }
        const padY = Math.max(14, height * 0.16);
        if (fish.y < padY) fish.vy += 0.07;
        if (fish.y > height - padY) fish.vy -= 0.07;

        ctx.save();
        ctx.translate(fish.x, fish.y);
        ctx.scale(fish.direction, 1);
        const tailAngle = Math.sin(fish.tailPhase) * 0.35;

        // Tail fin
        ctx.save();
        ctx.translate(-fish.size * 0.8, 0);
        ctx.rotate(tailAngle);
        ctx.fillStyle = fish.finColor;
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.lineTo(-fish.size * 0.6, -fish.size * 0.45);
        ctx.quadraticCurveTo(-fish.size * 0.4, 0, -fish.size * 0.6, fish.size * 0.45);
        ctx.closePath();
        ctx.fill();
        ctx.restore();

        // Body
        ctx.fillStyle = fish.color;
        ctx.beginPath();
        ctx.moveTo(fish.size, 0);
        ctx.quadraticCurveTo(0, -fish.size * 0.55, -fish.size * 0.8, 0);
        ctx.quadraticCurveTo(0, fish.size * 0.55, fish.size, 0);
        ctx.closePath();
        ctx.fill();

        if (fish.species === 'clown') {
          ctx.fillStyle = '#FFFFFF';
          ctx.beginPath();
          ctx.ellipse(0, 0, fish.size * 0.18, fish.size * 0.48, 0, 0, Math.PI * 2);
          ctx.fill();
          ctx.beginPath();
          ctx.ellipse(-fish.size * 0.45, 0, fish.size * 0.12, fish.size * 0.32, 0, 0, Math.PI * 2);
          ctx.fill();
        }

        // Dorsal fin
        ctx.fillStyle = fish.finColor;
        ctx.beginPath();
        ctx.moveTo(-fish.size * 0.2, -fish.size * 0.42);
        ctx.quadraticCurveTo(fish.size * 0.1, -fish.size * 0.75, fish.size * 0.3, -fish.size * 0.25);
        ctx.closePath();
        ctx.fill();

        // Eye
        ctx.fillStyle = '#FFFFFF';
        ctx.beginPath();
        ctx.arc(fish.size * 0.6, -fish.size * 0.12, fish.size * 0.14, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = '#0F172A';
        ctx.beginPath();
        ctx.arc(fish.size * 0.64, -fish.size * 0.12, fish.size * 0.08, 0, Math.PI * 2);
        ctx.fill();

        ctx.restore();
      }

      animationFrameId = window.requestAnimationFrame(render);
    };

    render();
    return () => {
      window.cancelAnimationFrame(animationFrameId);
      resizeObserver?.disconnect();
      window.removeEventListener('resize', updateSize);
      if (interactive) {
        canvas.removeEventListener('click', handleCanvasClick);
      }
    };
  }, [fishCount, causticsIntensity, bubbleSpeed, waterTint, compact, interactive]);

  return (
    <div
      ref={containerRef}
      className={`relative h-full w-full overflow-hidden select-none ${
        interactive ? 'cursor-pointer' : ''
      } ${className}`}
    >
      <canvas ref={canvasRef} className="block h-full w-full" />
      <div className="pointer-events-none absolute inset-x-0 top-0 h-6 bg-gradient-to-b from-sky-400/20 via-sky-300/5 to-transparent" />
      {!compact && (
        <div className="pointer-events-none absolute inset-x-0 bottom-0 flex items-end bg-gradient-to-t from-slate-950/80 via-slate-900/35 to-transparent px-3 pb-1 pt-3">
          <div className="font-mono text-[9.5px] tracking-wider text-sky-300/75">
            {interactive ? '点击水面投喂鱼食 · CRAFT 水族箱生态' : 'CRAFT 水族箱生态'}
          </div>
        </div>
      )}
      {showClock && (
        <div className="pointer-events-none absolute right-2.5 top-2 text-right drop-shadow-[0_2px_8px_rgba(0,0,0,0.85)]">
          <div
            className={`font-mono-tabular font-bold leading-none text-white/95 ${
              compact ? 'text-[12px]' : 'text-[17px]'
            }`}
          >
            {timeStr || '12:00:00'}
          </div>
          <div
            className={`mt-0.5 font-medium uppercase tracking-wider text-sky-200/75 ${
              compact ? 'text-[8px]' : 'text-[10px]'
            }`}
          >
            {dateStr || 'Thu, Nov 27'}
          </div>
        </div>
      )}
    </div>
  );
};

// ============================================================================
// 2. 金融脉动 (craft-stocks)
// ============================================================================

interface StockData {
  symbol: string;
  name: string;
  price: number;
  change: number;
  changePercent: number;
  high: number;
  low: number;
  volume: string;
  history: number[];
}

const INITIAL_STOCKS: Record<string, StockData> = {
  NVDA: {
    symbol: 'NVDA',
    name: 'NVIDIA Corp',
    price: 138.25,
    change: 4.82,
    changePercent: 3.61,
    high: 139.5,
    low: 133.1,
    volume: '48.2M',
    history: [132, 133.5, 131.8, 134.2, 135.0, 134.6, 136.2, 137.4, 136.9, 138.25],
  },
  AAPL: {
    symbol: 'AAPL',
    name: 'Apple Inc',
    price: 232.14,
    change: -1.22,
    changePercent: -0.52,
    high: 234.8,
    low: 231.4,
    volume: '34.7M',
    history: [235, 234.2, 234.8, 233.1, 232.8, 233.5, 232.0, 231.8, 232.5, 232.14],
  },
  BTC: {
    symbol: 'BTC/USD',
    name: 'Bitcoin Core',
    price: 68420.0,
    change: 1850.5,
    changePercent: 2.78,
    high: 69120.0,
    low: 66400.0,
    volume: '$28.4B',
    history: [66200, 66500, 66100, 67200, 66900, 67800, 68100, 67900, 68300, 68420],
  },
  ETH: {
    symbol: 'ETH/USD',
    name: 'Ethereum',
    price: 3512.4,
    change: 94.2,
    changePercent: 2.75,
    high: 3580.0,
    low: 3390.0,
    volume: '$14.2B',
    history: [3400, 3420, 3390, 3440, 3460, 3450, 3480, 3500, 3490, 3512.4],
  },
};

export interface CraftStocksWidgetProps {
  selectedSymbol?: string;
  accentColor?: string;
  compact?: boolean;
  interactive?: boolean;
  className?: string;
}

export const CraftStocksWidget: React.FC<CraftStocksWidgetProps> = ({
  selectedSymbol = 'NVDA',
  accentColor = '#38BDF8',
  compact = false,
  interactive = true,
  className = '',
}) => {
  const [stocks, setStocks] = useState<Record<string, StockData>>(INITIAL_STOCKS);
  const [activeKey, setActiveKey] = useState<string>(
    INITIAL_STOCKS[selectedSymbol] ? selectedSymbol : 'NVDA'
  );
  const [timeframe, setTimeframe] = useState<'1D' | '1W' | '1M'>('1D');
  const [isTickUp, setIsTickUp] = useState<boolean>(true);

  useEffect(() => {
    if (INITIAL_STOCKS[selectedSymbol]) {
      setActiveKey(selectedSymbol);
    }
  }, [selectedSymbol]);

  useEffect(() => {
    const interval = window.setInterval(() => {
      setStocks((prev) => {
        const current = prev[activeKey];
        if (!current) return prev;
        const delta = (Math.random() - 0.48) * (current.price * 0.002);
        const newPrice = Number((current.price + delta).toFixed(2));
        const newChange = Number((current.change + delta).toFixed(2));
        const basePrice = Math.max(1, newPrice - newChange);
        const newPercent = Number(((newChange / basePrice) * 100).toFixed(2));
        const updatedHistory = [...current.history.slice(1), newPrice];
        setIsTickUp(delta >= 0);
        return {
          ...prev,
          [activeKey]: {
            ...current,
            price: newPrice,
            change: newChange,
            changePercent: newPercent,
            history: updatedHistory,
          },
        };
      });
    }, 2400);
    return () => window.clearInterval(interval);
  }, [activeKey]);

  const stock = stocks[activeKey] || stocks.NVDA;
  const isPositive = stock.change >= 0;

  const minVal = Math.min(...stock.history) * 0.998;
  const maxVal = Math.max(...stock.history) * 1.002;
  const range = maxVal - minVal || 1;
  const chartWidth = 300;
  const chartHeight = 82;
  const points = stock.history.map((val, idx) => {
    const x = (idx / (stock.history.length - 1)) * chartWidth;
    const y = chartHeight - ((val - minVal) / range) * (chartHeight - 14) - 7;
    return `${x},${y}`;
  });
  const pathD = `M ${points.join(' L ')}`;
  const areaD = `${pathD} L ${chartWidth},${chartHeight} L 0,${chartHeight} Z`;
  const gradId = `craft-stocks-grad-${activeKey}-${compact ? 'sm' : 'lg'}`;

  return (
    <div
      className={`flex h-full w-full flex-col justify-between bg-[#0B0F17] text-slate-100 select-none ${
        compact ? 'p-2' : 'p-3'
      } ${className}`}
    >
      {/* Top Bar */}
      <div className="flex items-center justify-between gap-1 border-b border-slate-800/80 pb-1.5">
        <div className="flex items-center gap-1 overflow-hidden">
          {Object.keys(stocks).map((key) => {
            const cls = `rounded px-1.5 py-0.5 font-mono-tabular font-semibold transition-colors ${
              compact ? 'text-[9px]' : 'text-[10.5px]'
            } ${
              activeKey === key
                ? 'border border-sky-500/40 bg-sky-500/20 text-sky-300'
                : 'text-slate-400 hover:bg-slate-800/60 hover:text-slate-200'
            }`;
            const st =
              activeKey === key
                ? { borderColor: `${accentColor}66`, backgroundColor: `${accentColor}26`, color: accentColor }
                : undefined;
            return interactive ? (
              <button
                key={key}
                type="button"
                onClick={() => setActiveKey(key)}
                className={cls}
                style={st}
              >
                {key}
              </button>
            ) : (
              <span key={key} className={cls} style={st}>
                {key}
              </span>
            );
          })}
        </div>

        {!compact && (
          <div className="flex items-center gap-0.5 rounded border border-slate-800 bg-slate-900/90 p-0.5 font-mono text-[9.5px] text-slate-400">
            {(['1D', '1W', '1M'] as const).map((tf) => {
              const cls = `rounded px-1.5 py-0.5 ${
                timeframe === tf ? 'bg-slate-800 font-bold text-slate-100' : 'hover:text-slate-200'
              }`;
              return interactive ? (
                <button
                  key={tf}
                  type="button"
                  onClick={() => setTimeframe(tf)}
                  className={cls}
                >
                  {tf}
                </button>
              ) : (
                <span key={tf} className={cls}>
                  {tf}
                </span>
              );
            })}
          </div>
        )}
      </div>

      {/* Price Row */}
      <div className="my-1 flex items-baseline justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-1.5">
            <span className={`truncate font-semibold text-slate-200 ${compact ? 'text-[10px]' : 'text-[12px]'}`}>
              {stock.name}
            </span>
            {!compact && (
              <span className="hidden font-mono text-[9.5px] text-slate-500 sm:inline">实时行情</span>
            )}
          </div>
          <div className="mt-0.5 flex items-baseline gap-1.5">
            <span
              className={`font-mono-tabular font-bold tracking-tight transition-colors ${
                compact ? 'text-[14px]' : 'text-[18px]'
              } ${isTickUp ? 'text-emerald-400' : 'text-rose-400'}`}
            >
              ${stock.price.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span
              className={`flex items-center font-mono-tabular font-semibold ${
                compact ? 'text-[9.5px]' : 'text-[11px]'
              } ${isPositive ? 'text-emerald-400' : 'text-rose-400'}`}
            >
              {isPositive ? (
                <ArrowUpRight className="mr-0.5 h-3 w-3" />
              ) : (
                <ArrowDownRight className="mr-0.5 h-3 w-3" />
              )}
              {isPositive ? '+' : ''}
              {stock.changePercent}%
            </span>
          </div>
        </div>

        {!compact && (
          <div className="flex shrink-0 flex-col items-end font-mono-tabular text-[9.5px] text-slate-400">
            <div>
              <span className="text-slate-500">高 </span>
              <span className="text-slate-200">${stock.high}</span>
            </div>
            <div>
              <span className="text-slate-500">低 </span>
              <span className="text-slate-200">${stock.low}</span>
            </div>
          </div>
        )}
      </div>

      {/* SVG Area Chart */}
      <div className="relative min-h-[34px] flex-1 overflow-hidden rounded border border-slate-800/60 bg-slate-950/65 p-1">
        <svg
          viewBox={`0 0 ${chartWidth} ${chartHeight}`}
          preserveAspectRatio="none"
          className="h-full w-full overflow-visible"
        >
          <defs>
            <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={isPositive ? '#10B981' : '#F43F5E'} stopOpacity="0.38" />
              <stop offset="100%" stopColor={isPositive ? '#10B981' : '#F43F5E'} stopOpacity="0.0" />
            </linearGradient>
          </defs>
          <line x1="0" y1={chartHeight * 0.3} x2={chartWidth} y2={chartHeight * 0.3} stroke="rgba(255,255,255,0.06)" strokeDasharray="3 3" />
          <line x1="0" y1={chartHeight * 0.65} x2={chartWidth} y2={chartHeight * 0.65} stroke="rgba(255,255,255,0.06)" strokeDasharray="3 3" />
          <path d={areaD} fill={`url(#${gradId})`} />
          <path
            d={pathD}
            fill="none"
            stroke={isPositive ? '#10B981' : '#F43F5E'}
            strokeWidth="2.2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          {points.length > 0 && (
            <circle
              cx={points[points.length - 1].split(',')[0]}
              cy={points[points.length - 1].split(',')[1]}
              r="3.8"
              fill={isPositive ? '#10B981' : '#F43F5E'}
            />
          )}
        </svg>
        {!compact && (
          <div className="pointer-events-none absolute bottom-1 left-2 flex items-center gap-1 font-mono text-[9px] text-slate-400">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
            <span>24H 成交量: {stock.volume}</span>
          </div>
        )}
      </div>
    </div>
  );
};

// ============================================================================
// 3. 霓虹歌词 (craft-lyrics)
// ============================================================================

interface LyricLine {
  time: number;
  text: string;
  translation?: string;
}

const SAMPLE_LYRICS: LyricLine[] = [
  { time: 0, text: 'City is my church, wrapped in twilight', translation: '城市是我的殿堂，将我裹入暮色流光' },
  { time: 5, text: 'Looking out at neon street lights', translation: '静候车中，遥望街头斑斓霓虹' },
  { time: 10, text: 'Waiting for a ride in the midnight dark', translation: '在夜幕中启程，迎向破晓前飞驰' },
  { time: 16, text: 'Whisper in the synthwave midnight sound', translation: '午夜合成器的回响在耳畔低语' },
  { time: 22, text: 'Electric dreams glowing in the sky', translation: '凝视漫天灯火，点亮电子梦境' },
];

export interface CraftLyricsWidgetProps {
  songTitle?: string;
  artist?: string;
  spectrumBars?: number;
  glowColor?: string;
  compact?: boolean;
  interactive?: boolean;
  className?: string;
}

export const CraftLyricsWidget: React.FC<CraftLyricsWidgetProps> = ({
  songTitle = 'Midnight City',
  artist = 'M83',
  spectrumBars = 20,
  glowColor = '#EC4899',
  compact = false,
  interactive = true,
  className = '',
}) => {
  const [isPlaying, setIsPlaying] = useState<boolean>(true);
  const [playbackTime, setPlaybackTime] = useState<number>(10);
  const [activeLyricIndex, setActiveLyricIndex] = useState<number>(2);
  const barCount = compact ? Math.min(16, spectrumBars) : Math.max(8, Math.min(32, spectrumBars));
  const [barHeights, setBarHeights] = useState<number[]>(() =>
    Array.from({ length: barCount }, (_, i) => 28 + ((i * 19) % 55))
  );

  useEffect(() => {
    if (!isPlaying) {
      setBarHeights(Array.from({ length: barCount }, () => 12));
      return;
    }
    const interval = window.setInterval(() => {
      setPlaybackTime((prev) => {
        const next = (prev + 0.5) % 28;
        let found = 0;
        for (let i = 0; i < SAMPLE_LYRICS.length; i++) {
          if (next >= SAMPLE_LYRICS[i].time) found = i;
        }
        setActiveLyricIndex(found);
        return next;
      });
      setBarHeights(
        Array.from({ length: barCount }, (_, i) => {
          const centerBias = Math.sin(((i + 0.5) / barCount) * Math.PI);
          return Math.floor(18 + Math.random() * 76 * Math.max(0.35, centerBias));
        })
      );
    }, 180);
    return () => window.clearInterval(interval);
  }, [isPlaying, barCount]);

  const currentLyric = SAMPLE_LYRICS[activeLyricIndex] || SAMPLE_LYRICS[0];

  return (
    <div
      className={`flex h-full w-full flex-col justify-between bg-[#0D0B14] text-slate-100 select-none ${
        compact ? 'p-2' : 'p-3'
      } ${className}`}
    >
      {/* Header */}
      <div className="flex items-center justify-between gap-2 border-b border-purple-900/40 pb-1.5">
        <div className="flex min-w-0 items-center gap-2">
          <div
            className={`relative flex shrink-0 items-center justify-center rounded-full bg-gradient-to-tr from-purple-800 to-pink-600 shadow-lg ${
              compact ? 'h-6 w-6' : 'h-8 w-8'
            }`}
          >
            <Disc3
              className={`${compact ? 'h-3.5 w-3.5' : 'h-5 w-5'} text-white ${
                isPlaying ? 'animate-spin' : ''
              }`}
              style={{ animationDuration: '4s' }}
            />
            <div className="absolute h-1.5 w-1.5 rounded-full border border-slate-700 bg-slate-950" />
          </div>
          <div className="min-w-0">
            <div className={`truncate font-semibold text-white ${compact ? 'text-[10.5px]' : 'text-[12.5px]'}`}>
              {songTitle}
            </div>
            <div className={`truncate font-mono text-pink-300/80 ${compact ? 'text-[8.5px]' : 'text-[10px]'}`}>
              {artist} · Synthwave
            </div>
          </div>
        </div>

        {interactive && !compact && (
          <div className="flex shrink-0 items-center gap-1.5">
            <button
              type="button"
              onClick={() => setIsPlaying((p) => !p)}
              aria-label={isPlaying ? '暂停播放' : '继续播放'}
              className="flex h-6 w-6 items-center justify-center rounded-full border border-pink-500/35 bg-pink-500/20 text-pink-200 hover:bg-pink-500/30"
            >
              {isPlaying ? <Pause className="h-3 w-3" /> : <Play className="ml-0.5 h-3 w-3" />}
            </button>
            <button
              type="button"
              onClick={() => setPlaybackTime((p) => (p + 6) % 28)}
              aria-label="切换歌词段落"
              className="flex h-6 w-6 items-center justify-center rounded-full bg-slate-800/70 text-slate-300 hover:text-white"
            >
              <SkipForward className="h-3 w-3" />
            </button>
          </div>
        )}
      </div>

      {/* Neon Lyric Center */}
      <div className="my-1 flex flex-1 flex-col items-center justify-center overflow-hidden text-center">
        <div
          className={`line-clamp-1 bg-gradient-to-r from-pink-400 via-rose-300 to-purple-400 bg-clip-text font-bold text-transparent ${
            compact ? 'text-[11px]' : 'text-[13.5px]'
          }`}
        >
          {currentLyric.text}
        </div>
        {currentLyric.translation && (
          <div className={`mt-0.5 line-clamp-1 text-pink-200/75 ${compact ? 'text-[9px]' : 'text-[10.5px]'}`}>
            {currentLyric.translation}
          </div>
        )}
      </div>

      {/* Spectrum Bars */}
      <div className="flex flex-col gap-1 border-t border-purple-900/30 pt-1.5">
        <div className={`flex items-end justify-between gap-0.5 ${compact ? 'h-3.5' : 'h-5'}`}>
          {barHeights.map((h, i) => (
            <div
              key={i}
              className="flex-1 rounded-t transition-all duration-150"
              style={{
                height: `${h}%`,
                backgroundColor: glowColor,
                opacity: 0.4 + (h / 100) * 0.6,
                boxShadow: isPlaying ? `0 0 6px ${glowColor}66` : 'none',
              }}
            />
          ))}
        </div>
        <div className="flex items-center justify-between font-mono-tabular text-[8.5px] text-slate-500">
          <span>0:{String(Math.floor(playbackTime)).padStart(2, '0')}</span>
          <div className="mx-2 h-1 flex-1 overflow-hidden rounded-full bg-slate-800">
            <div
              className="h-full bg-gradient-to-r from-pink-500 to-purple-500 transition-all duration-300"
              style={{ width: `${(playbackTime / 28) * 100}%` }}
            />
          </div>
          <span>0:28</span>
        </div>
      </div>
    </div>
  );
};

// ============================================================================
// 4. IN-14 辉光管时钟 (Nixie Vacuum Chronometer)
// ============================================================================

export type NixieColorHue = 'amber' | 'cyan' | 'green' | 'white';

const NIXIE_HUE_MAP: Record<
  NixieColorHue,
  { filament: string; glow: string; subGlow: string; baseGlow: string; label: string }
> = {
  amber: {
    filament: '#FED7AA',
    glow: '#F59E0B',
    subGlow: '#D97706',
    baseGlow: 'rgba(245, 158, 11, 0.55)',
    label: 'text-amber-400/75',
  },
  cyan: {
    filament: '#E0F2FE',
    glow: '#38BDF8',
    subGlow: '#0284C7',
    baseGlow: 'rgba(56, 189, 248, 0.55)',
    label: 'text-sky-400/75',
  },
  green: {
    filament: '#D1FAE5',
    glow: '#10B981',
    subGlow: '#059669',
    baseGlow: 'rgba(16, 185, 129, 0.55)',
    label: 'text-emerald-400/75',
  },
  white: {
    filament: '#FFFFFF',
    glow: '#E2E8F0',
    subGlow: '#94A3B8',
    baseGlow: 'rgba(226, 232, 240, 0.45)',
    label: 'text-slate-300/75',
  },
};

export interface CraftNixieClockWidgetProps {
  showSeconds?: boolean;
  showDate?: boolean;
  use24Hour?: boolean;
  colorHue?: NixieColorHue;
  compact?: boolean;
  now?: Date;
  className?: string;
}

export const CraftNixieClockWidget: React.FC<CraftNixieClockWidgetProps> = ({
  showSeconds = true,
  showDate = true,
  use24Hour = true,
  colorHue = 'amber',
  compact = false,
  now: externalNow,
  className = '',
}) => {
  const [internalNow, setInternalNow] = useState<Date>(() => new Date());

  useEffect(() => {
    if (externalNow) return;
    const timer = window.setInterval(() => setInternalNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, [externalNow]);

  const time = externalNow ?? internalNow;
  const rawHours = time.getHours();
  const displayHours = use24Hour ? rawHours : rawHours % 12 || 12;
  const hours = String(displayHours).padStart(2, '0');
  const minutes = String(time.getMinutes()).padStart(2, '0');
  const seconds = String(time.getSeconds()).padStart(2, '0');
  const dateFormatted = time.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  });

  const palette = NIXIE_HUE_MAP[colorHue] || NIXIE_HUE_MAP.amber;

  const renderNixieDigit = (d: string, index: number) => (
    <div
      key={index}
      className={`relative flex items-center justify-center overflow-hidden rounded-lg border bg-gradient-to-b from-slate-900/95 via-slate-950 to-black shadow-[inset_0_0_10px_rgba(0,0,0,0.85)] ${
        compact ? 'h-10 w-6' : 'h-14 w-8 sm:h-16 sm:w-9'
      }`}
      style={{ borderColor: `${palette.glow}38` }}
    >
      {/* Glass reflection */}
      <div className="absolute inset-x-0.5 top-0 h-3 rounded-t bg-gradient-to-b from-white/15 to-transparent" />
      {/* Wire mesh texture */}
      <div
        className="absolute inset-0 opacity-20"
        style={{
          backgroundImage: `radial-gradient(circle, ${palette.glow} 1px, transparent 1px)`,
          backgroundSize: '4px 4px',
        }}
      />
      {/* Filament digit */}
      <span
        className={`relative font-mono-tabular font-black transition-all duration-300 ${
          compact ? 'text-[17px]' : 'text-[23px] sm:text-[26px]'
        }`}
        style={{
          color: palette.filament,
          textShadow: `0 0 6px ${palette.glow}, 0 0 14px ${palette.subGlow}, 0 0 22px ${palette.baseGlow}`,
        }}
      >
        {d}
      </span>
      {/* Subtle base glow */}
      <div
        className="absolute inset-x-1.5 bottom-0 h-1 rounded-full blur-[2px]"
        style={{ backgroundColor: palette.baseGlow }}
      />
    </div>
  );

  const renderColon = (keyPrefix: string) => (
    <div key={keyPrefix} className="flex flex-col gap-1.5 px-0.5">
      <span
        className="h-1 w-1 rounded-full animate-pulse"
        style={{ backgroundColor: palette.glow, boxShadow: `0 0 6px ${palette.glow}` }}
      />
      <span
        className="h-1 w-1 rounded-full animate-pulse"
        style={{ backgroundColor: palette.glow, boxShadow: `0 0 6px ${palette.glow}` }}
      />
    </div>
  );

  return (
    <div
      className={`flex h-full w-full flex-col items-center justify-center bg-[#080705] select-none ${
        compact ? 'p-2' : 'p-3'
      } ${className}`}
    >
      <div
        className={`mb-1 font-mono uppercase tracking-widest ${
          compact ? 'text-[7.5px]' : 'text-[9px]'
        } ${palette.label}`}
      >
        IN-14 NIXIE VACUUM CHRONOMETER
      </div>
      <div
        className={`flex items-center rounded-xl border border-neutral-800 bg-neutral-950/90 shadow-xl ${
          compact ? 'gap-1 p-1.5' : 'gap-1.5 p-2'
        }`}
      >
        {renderNixieDigit(hours[0], 0)}
        {renderNixieDigit(hours[1], 1)}
        {renderColon('c1')}
        {renderNixieDigit(minutes[0], 2)}
        {renderNixieDigit(minutes[1], 3)}
        {showSeconds && (
          <>
            {renderColon('c2')}
            {renderNixieDigit(seconds[0], 4)}
            {renderNixieDigit(seconds[1], 5)}
          </>
        )}
      </div>
      {showDate && (
        <div
          className={`mt-1.5 font-mono tracking-wider ${
            compact ? 'text-[8.5px]' : 'text-[10.5px]'
          } ${palette.label}`}
        >
          {dateFormatted}
          {!use24Hour && ` · ${rawHours >= 12 ? 'PM' : 'AM'}`}
        </div>
      )}
    </div>
  );
};

export const FittedNixieClockWidget: React.FC<CraftNixieClockWidgetProps> = (props) => (
  <div className="relative h-full w-full overflow-hidden bg-[#080705] [container-type:size]">
    <div
      className="absolute left-1/2 top-1/2 h-[86px] w-[216px]"
      style={{ transform: 'translate(-50%, -50%) scale(min(tan(atan2(100cqw, 216px)), tan(atan2(100cqh, 86px))))' }}
    >
      <CraftNixieClockWidget {...props} compact />
    </div>
  </div>
);
