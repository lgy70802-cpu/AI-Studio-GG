import { captureDesktopImage, captureDesktopVideo, desktopMode } from '../../services/desktopBridge';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Camera, Video, X } from 'lucide-react';
import { EDITOR_DEVICE_SPECS } from '../../data/editorCatalog';

interface CaptureModalProps {
  mode: 'shot' | 'rec' | null;
  defaultSize: [number, number];
  onClose: () => void;
  onCapturedImage: (payload: { src: string; width: number; height: number; name: string }) => void;
  onCapturedVideo: (payload: {
    src: string;
    poster: string;
    width: number;
    height: number;
    dur: number;
    name: string;
  }) => void;
  onNotify: (msg: string) => void;
}

const DESK_W = 1920;
const DESK_H = 1080;

function pad2(n: number) {
  return String(n).padStart(2, '0');
}

function formatStamp() {
  const d = new Date();
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())} ${pad2(d.getHours())}.${pad2(
    d.getMinutes()
  )}.${pad2(d.getSeconds())}`;
}

export const CaptureModal: React.FC<CaptureModalProps> = ({
  mode,
  defaultSize,
  onClose,
  onCapturedImage,
  onCapturedVideo,
  onNotify,
}) => {
  const nativeStarted = useRef(false);
  useEffect(() => {
    if (!mode) { nativeStarted.current = false; return; }
    if (!desktopMode || nativeStarted.current) return;
    nativeStarted.current = true;
    const action = mode === 'shot'
      ? captureDesktopImage(defaultSize[0], defaultSize[1]).then(result => { if (!result.cancelled) onCapturedImage(result); })
      : captureDesktopVideo(defaultSize[0], defaultSize[1]).then(result => { if (!result.cancelled) onCapturedVideo(result); });
    action.then(() => {

      onClose();
    }).catch(error => { onNotify(error.message); onClose(); });
  }, [mode, defaultSize, onCapturedImage, onCapturedVideo, onClose, onNotify]);
  const [specIdx, setSpecIdx] = useState<number>(() => {
    const idx = EDITOR_DEVICE_SPECS.findIndex(
      (s) => s.size[0] === defaultSize[0] && s.size[1] === defaultSize[1]
    );
    return idx >= 0 ? idx : 0;
  });
  const [recordCursor, setRecordCursor] = useState(true);
  const [includeApp, setIncludeApp] = useState(false);
  const [phase, setPhase] = useState<'select' | 'countdown' | 'recording'>('select');
  const [countdownVal, setCountdownVal] = useState(3);
  const [recElapsed, setRecElapsed] = useState(0);

  const [sel, setSel] = useState({ x: DESK_W * 0.15, y: DESK_H * 0.3, w: DESK_W * 0.6, h: DESK_H * 0.3 });
  const [viewport, setViewport] = useState({ scale: 0.5, ox: 0, oy: 0 });

  const deskCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const outCanvasRef = useRef<HTMLCanvasElement | null>(null);
  const mediaRecRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const recStartRef = useRef<number>(0);
  const keepRecRef = useRef<boolean>(false);
  const rafRef = useRef<number>(0);
  const t0Ref = useRef<number>(performance.now());
  const simCursorRef = useRef<{ x: number; y: number }>({ x: 900, y: 430 });

  const dragRef = useRef<{
    type: 'mv' | 'rs';
    p0?: { x: number; y: number };
    s0?: typeof sel;
    anchor?: { x: number; y: number };
  } | null>(null);

  const activeSpec = specIdx >= 0 ? EDITOR_DEVICE_SPECS[specIdx] : null;

  const computeOutSize = useCallback((): [number, number] => {
    if (activeSpec) return [...activeSpec.size] as [number, number];
    const k = Math.min(1, 1920 / Math.max(1, sel.w));
    return [Math.max(2, Math.round(sel.w * k)), Math.max(2, Math.round(sel.h * k))];
  }, [activeSpec, sel.w, sel.h]);

  const fitSelectionToSpec = useCallback(
    (nextSpecIdx: number) => {
      const sp = nextSpecIdx >= 0 ? EDITOR_DEVICE_SPECS[nextSpecIdx] : null;
      if (!sp) return;
      const ratio = sp.size[0] / sp.size[1];
      setSel((prev) => {
        const cx = prev.x + prev.w / 2;
        const cy = prev.y + prev.h / 2;
        let w = Math.min(prev.w, DESK_W * 0.8);
        let h = w / ratio;
        if (h > DESK_H * 0.8) {
          h = DESK_H * 0.8;
          w = h * ratio;
        }
        return {
          x: Math.max(0, Math.min(DESK_W - w, cx - w / 2)),
          y: Math.max(0, Math.min(DESK_H - h, cy - h / 2)),
          w,
          h,
        };
      });
    },
    []
  );

  useEffect(() => {
    if (!mode) return;
    fitSelectionToSpec(specIdx);
    setPhase('select');
    setRecElapsed(0);
    t0Ref.current = performance.now();
  }, [mode, specIdx, fitSelectionToSpec]);

  useEffect(() => {
    if (!mode) return;
    const updateLayout = () => {
      const W = window.innerWidth;
      const H = window.innerHeight;
      const s = Math.min(W / DESK_W, H / DESK_H);
      setViewport({ scale: s, ox: (W - DESK_W * s) / 2, oy: (H - DESK_H * s) / 2 });
    };
    updateLayout();
    window.addEventListener('resize', updateLayout);
    return () => window.removeEventListener('resize', updateLayout);
  }, [mode]);

  const drawDesktop = useCallback(
    (t: number) => {
      const canvas = deskCanvasRef.current;
      if (!canvas) return;
      const c = canvas.getContext('2d');
      if (!c) return;

      const g = c.createLinearGradient(0, 0, DESK_W, DESK_H);
      g.addColorStop(0, '#120733');
      g.addColorStop(0.55, '#241059');
      g.addColorStop(1, '#311B7E');
      c.fillStyle = g;
      c.fillRect(0, 0, DESK_W, DESK_H);

      c.fillStyle = 'rgba(255,255,255,0.06)';
      c.beginPath();
      c.arc(1500, 200, 320, 0, Math.PI * 2);
      c.fill();
      c.beginPath();
      c.arc(300, 900, 260, 0, Math.PI * 2);
      c.fill();

      // Browser window mockup
      c.fillStyle = 'rgba(0,0,0,0.25)';
      c.beginPath();
      c.roundRect(128, 104, 1000, 620, 16);
      c.fill();
      c.fillStyle = '#f6f9fb';
      c.beginPath();
      c.roundRect(120, 90, 1000, 620, 14);
      c.fill();
      c.fillStyle = '#dfe8ee';
      c.fillRect(120, 90, 1000, 48);
      c.fillStyle = '#fff';
      c.beginPath();
      c.roundRect(200, 100, 560, 28, 14);
      c.fill();
      c.fillStyle = '#64748b';
      c.font = '14px sans-serif';
      c.fillText('workspace.local/metrics', 222, 120);
      ['#ff6b5e', '#ffc24b', '#43c463'].forEach((k, i) => {
        c.fillStyle = k;
        c.beginPath();
        c.arc(146 + i * 22, 114, 7, 0, Math.PI * 2);
        c.fill();
      });

      const g2 = c.createLinearGradient(150, 170, 700, 420);
      g2.addColorStop(0, '#8B5CF6');
      g2.addColorStop(1, '#5E45F8');
      c.fillStyle = g2;
      c.beginPath();
      c.roundRect(150, 170, 520, 250, 12);
      c.fill();

      c.fillStyle = '#cbd5e1';
      for (let i = 0; i < 6; i += 1) {
        c.fillRect(700, 176 + i * 40, 380 - ((i * 53) % 140), 14);
      }

      c.fillStyle = '#e2e8f0';
      c.beginPath();
      c.roundRect(150, 450, 930, 16, 8);
      c.fill();
      c.fillStyle = '#5E45F8';
      c.beginPath();
      c.roundRect(150, 450, 930 * ((t * 0.12) % 1), 16, 8);
      c.fill();
      c.fillStyle = '#475569';
      c.font = '600 18px sans-serif';
      c.fillText(`同步桌面素材 · ${Math.floor(((t * 0.12) % 1) * 100)}%`, 150, 500);

      // System monitor window
      c.fillStyle = '#0f172a';
      c.beginPath();
      c.roundRect(1000, 300, 780, 520, 16);
      c.fill();
      c.fillStyle = '#1e293b';
      c.fillRect(1000, 300, 780, 44);
      c.fillStyle = '#94a3b8';
      c.font = '600 16px sans-serif';
      c.fillText('实时桌面监视器 (模拟环境)', 1024, 328);
      for (let i = 0; i < 9; i += 1) {
        const h = 80 + 60 * Math.abs(Math.sin(t * 0.9 + i * 0.7));
        const x = 1040 + i * 78;
        c.fillStyle = i % 2 ? '#A78BFA' : '#5E45F8';
        c.beginPath();
        c.roundRect(x, 780 - h * 2.2, 48, h * 2.2, 8);
        c.fill();
      }

      // Desktop clock widget
      const d = new Date();
      c.fillStyle = '#fff';
      c.font = '700 92px sans-serif';
      c.textAlign = 'right';
      c.fillText(`${pad2(d.getHours())}:${pad2(d.getMinutes())}`, 1860, 150);
      c.font = '600 26px sans-serif';
      c.fillStyle = 'rgba(255,255,255,0.8)';
      c.fillText(`${d.getMonth() + 1}月${d.getDate()}日`, 1860, 196);
      c.textAlign = 'left';

      if (includeApp) {
        c.fillStyle = 'rgba(0,0,0,0.3)';
        c.beginPath();
        c.roundRect(308, 208, 720, 440, 16);
        c.fill();
        c.fillStyle = '#fff';
        c.beginPath();
        c.roundRect(300, 200, 720, 440, 16);
        c.fill();
        c.fillStyle = '#5E45F8';
        c.fillRect(300, 200, 720, 44);
        c.fillStyle = '#fff';
        c.font = '700 18px sans-serif';
        c.fillText('D92 Studio（本软件窗口）', 324, 229);
      }

      // Taskbar
      c.fillStyle = 'rgba(8,18,26,0.92)';
      c.fillRect(0, DESK_H - 52, DESK_W, 52);
      ['#58c4dc', '#ffd166', '#7bd88f', '#ff7a59', '#c19bff'].forEach((k, i) => {
        c.fillStyle = k;
        c.beginPath();
        c.roundRect(24 + i * 56, DESK_H - 42, 34, 32, 8);
        c.fill();
      });

      simCursorRef.current = {
        x: 900 + 520 * Math.cos(t * 0.7),
        y: 430 + 230 * Math.sin(t * 1.05),
      };
    },
    [includeApp]
  );

  useEffect(() => {
    if (!mode) return;
    let mounted = true;
    const loop = (now: number) => {
      if (!mounted) return;
      const t = (now - t0Ref.current) / 1000;
      drawDesktop(t);

      if (phase === 'recording' && outCanvasRef.current && deskCanvasRef.current) {
        const out = outCanvasRef.current;
        const octx = out.getContext('2d');
        if (octx) {
          octx.drawImage(
            deskCanvasRef.current,
            sel.x,
            sel.y,
            sel.w,
            sel.h,
            0,
            0,
            out.width,
            out.height
          );
          if (recordCursor) {
            const p = simCursorRef.current;
            const cx = ((p.x - sel.x) / sel.w) * out.width;
            const cy = ((p.y - sel.y) / sel.h) * out.height;
            if (cx >= 0 && cy >= 0 && cx < out.width && cy < out.height) {
              octx.save();
              octx.translate(cx, cy);
              octx.fillStyle = '#ffffff';
              octx.strokeStyle = '#111827';
              octx.lineWidth = 2;
              octx.beginPath();
              octx.moveTo(0, 0);
              octx.lineTo(18, 10);
              octx.lineTo(10, 13);
              octx.lineTo(6, 20);
              octx.closePath();
              octx.fill();
              octx.stroke();
              octx.restore();
            }
          }
        }
        setRecElapsed(Math.floor((performance.now() - recStartRef.current) / 1000));
      }

      rafRef.current = requestAnimationFrame(loop);
    };
    rafRef.current = requestAnimationFrame(loop);
    return () => {
      mounted = false;
      cancelAnimationFrame(rafRef.current);
    };
  }, [mode, phase, drawDesktop, sel, recordCursor]);

  const stopRecording = useCallback((keep: boolean) => {
    keepRecRef.current = keep;
    const rec = mediaRecRef.current;
    if (rec && rec.state !== 'inactive') {
      rec.stop();
    } else if (!keep) {
      onClose();
    }
  }, [onClose]);

  const startRecording = useCallback(() => {
    if (!deskCanvasRef.current) return;
    if (typeof MediaRecorder === 'undefined' || !HTMLCanvasElement.prototype.captureStream) {
      onNotify('当前浏览器环境不支持 Canvas 录制');
      onClose();
      return;
    }

    const [w, h] = computeOutSize();
    const out = document.createElement('canvas');
    out.width = w;
    out.height = h;
    outCanvasRef.current = out;

    const mime =
      ['video/webm;codecs=vp9', 'video/webm;codecs=vp8', 'video/webm'].find((t) =>
        MediaRecorder.isTypeSupported(t)
      ) || '';
    const rec = new MediaRecorder(
      out.captureStream(30),
      mime ? { mimeType: mime, videoBitsPerSecond: 4000000 } : undefined
    );
    chunksRef.current = [];
    rec.ondataavailable = (e) => {
      if (e.data.size) chunksRef.current.push(e.data);
    };
    rec.onstop = () => {
      const elapsed = (performance.now() - recStartRef.current) / 1000;
      const keep = keepRecRef.current;
      mediaRecRef.current = null;
      if (!keep) {
        onNotify('已取消录制');
        onClose();
        return;
      }
      if (elapsed < 1) {
        onNotify('录制不足 1 秒，未保存');
        onClose();
        return;
      }
      const blob = new Blob(chunksRef.current, { type: 'video/webm' });
      const pc = document.createElement('canvas');
      const k = Math.min(1, 640 / w);
      pc.width = Math.round(w * k);
      pc.height = Math.round(h * k);
      pc.getContext('2d')?.drawImage(out, 0, 0, pc.width, pc.height);
      const poster = pc.toDataURL('image/jpeg', 0.78);
      onCapturedVideo({
        src: URL.createObjectURL(blob),
        poster,
        width: w,
        height: h,
        dur: elapsed,
        name: `录屏 ${formatStamp()}`,
      });
      onClose();
    };

    mediaRecRef.current = rec;
    recStartRef.current = performance.now();
    rec.start(250);
    setPhase('recording');
  }, [computeOutSize, onCapturedVideo, onClose, onNotify]);

  useEffect(() => {
    if (phase !== 'countdown') return;
    if (countdownVal <= 0) {
      startRecording();
      return;
    }
    const timer = window.setTimeout(() => setCountdownVal((v) => v - 1), 1000);
    return () => window.clearTimeout(timer);
  }, [phase, countdownVal, startRecording]);

  const handleConfirm = useCallback(() => {
    if (phase !== 'select') return;
    if (mode === 'shot') {
      const desk = deskCanvasRef.current;
      if (!desk) return;
      const [w, h] = computeOutSize();
      const c = document.createElement('canvas');
      c.width = w;
      c.height = h;
      c.getContext('2d')?.drawImage(desk, sel.x, sel.y, sel.w, sel.h, 0, 0, w, h);
      const url = c.toDataURL('image/png');
      onCapturedImage({ src: url, width: w, height: h, name: `截图 ${formatStamp()}` });
      onClose();
      return;
    }
    setCountdownVal(3);
    setPhase('countdown');
  }, [phase, mode, computeOutSize, sel, onCapturedImage, onClose]);

  useEffect(() => {
    if (!mode) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        if (phase === 'recording') stopRecording(false);
        else onClose();
      } else if (e.key === 'Enter' && phase === 'select') {
        e.preventDefault();
        handleConfirm();
      }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [mode, phase, stopRecording, onClose, handleConfirm]);

  if (!mode || desktopMode) return null;

  const toDeskCoords = (clientX: number, clientY: number) => ({
    x: (clientX - viewport.ox) / viewport.scale,
    y: (clientY - viewport.oy) / viewport.scale,
  });

  const handlePointerDown = (e: React.PointerEvent) => {
    if (phase !== 'select' || e.button !== 0) return;
    if ((e.target as HTMLElement).closest('.cap-bar')) return;
    const p = toDeskCoords(e.clientX, e.clientY);
    const handle = (e.target as HTMLElement).closest<HTMLElement>('[data-ch]');
    if (handle) {
      const k = handle.dataset.ch!;
      dragRef.current = {
        type: 'rs',
        anchor: {
          x: k.includes('l') ? sel.x + sel.w : sel.x,
          y: k[0] === 't' ? sel.y + sel.h : sel.y,
        },
      };
    } else if (p.x >= sel.x && p.x <= sel.x + sel.w && p.y >= sel.y && p.y <= sel.y + sel.h) {
      dragRef.current = { type: 'mv', p0: p, s0: { ...sel } };
    } else {
      dragRef.current = {
        type: 'rs',
        anchor: { x: Math.max(0, Math.min(DESK_W, p.x)), y: Math.max(0, Math.min(DESK_H, p.y)) },
      };
    }
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    const d = dragRef.current;
    if (!d) return;
    const p = toDeskCoords(e.clientX, e.clientY);
    if (d.type === 'mv' && d.p0 && d.s0) {
      setSel({
        ...d.s0,
        x: Math.max(0, Math.min(DESK_W - d.s0.w, d.s0.x + p.x - d.p0.x)),
        y: Math.max(0, Math.min(DESK_H - d.s0.h, d.s0.y + p.y - d.p0.y)),
      });
    } else if (d.type === 'rs' && d.anchor) {
      const a = d.anchor;
      const sx = p.x < a.x ? -1 : 1;
      const sy = p.y < a.y ? -1 : 1;
      const maxW = sx > 0 ? DESK_W - a.x : a.x;
      const maxH = sy > 0 ? DESK_H - a.y : a.y;
      let w = Math.abs(p.x - a.x);
      let h = Math.abs(p.y - a.y);
      if (activeSpec) {
        const r = activeSpec.size[0] / activeSpec.size[1];
        w = Math.min(w, maxW, maxH * r);
        h = w / r;
      } else {
        w = Math.min(w, maxW);
        h = Math.min(h, maxH);
      }
      w = Math.max(40, w);
      h = Math.max(activeSpec ? 40 / (activeSpec.size[0] / activeSpec.size[1]) : 24, h);
      setSel({
        x: sx > 0 ? a.x : a.x - w,
        y: sy > 0 ? a.y : a.y - h,
        w,
        h,
      });
    }
  };

  const outSize = computeOutSize();

  return (
    <div
      className="fixed inset-0 z-[100] bg-[#0D0624] text-white select-none touch-none"
      role="dialog"
      aria-modal="true"
      aria-label={mode === 'shot' ? '区域截图' : '区域录屏'}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={() => {
        dragRef.current = null;
      }}
    >
      <canvas
        ref={deskCanvasRef}
        width={DESK_W}
        height={DESK_H}
        style={{
          position: 'absolute',
          left: viewport.ox,
          top: viewport.oy,
          width: DESK_W * viewport.scale,
          height: DESK_H * viewport.scale,
        }}
      />

      {/* Selection Box */}
      <div
        style={{
          position: 'absolute',
          left: viewport.ox + sel.x * viewport.scale,
          top: viewport.oy + sel.y * viewport.scale,
          width: sel.w * viewport.scale,
          height: sel.h * viewport.scale,
          boxShadow: phase === 'recording' ? 'none' : '0 0 0 9999px rgba(13,6,36,0.62)',
        }}
        className={`border-2 ${phase === 'recording' ? 'border-red-500' : 'border-[#A78BFA] cursor-move'}`}
      >
        {phase === 'select' && (
          <>
            <span className="absolute -top-7 left-0 whitespace-nowrap rounded-md bg-[#5E45F8] px-2.5 py-0.5 text-[11px] font-semibold shadow-md">
              {activeSpec ? `已锁定比例 · ` : ''}
              {Math.round(sel.w)} × {Math.round(sel.h)} → 输出 {outSize[0]}×{outSize[1]}
            </span>
            {(['tl', 'tr', 'bl', 'br'] as const).map((k) => (
              <i
                key={k}
                data-ch={k}
                className={`absolute h-3 w-3 rounded-xs border-2 border-[#5E45F8] bg-white ${
                  k === 'tl'
                    ? '-left-1.5 -top-1.5 cursor-nwse-resize'
                    : k === 'tr'
                      ? '-right-1.5 -top-1.5 cursor-nesw-resize'
                      : k === 'bl'
                        ? '-left-1.5 -bottom-1.5 cursor-nesw-resize'
                        : '-right-1.5 -bottom-1.5 cursor-nwse-resize'
                }`}
              />
            ))}
          </>
        )}

        {phase === 'countdown' && (
          <div className="flex h-full w-full items-center justify-center text-7xl font-extrabold text-white drop-shadow-lg">
            {countdownVal}
          </div>
        )}
      </div>

      {/* Top Bar */}
      {phase === 'select' && (
        <div className="cap-bar fixed left-1/2 top-4 z-10 flex -translate-x-1/2 items-center gap-3 whitespace-nowrap rounded-xl border border-white/15 bg-[#150A38]/92 px-4 py-2 text-[12px] shadow-2xl backdrop-blur-xl">
          <span className="flex items-center gap-1.5 font-bold">
            {mode === 'shot' ? <Camera className="h-4 w-4 text-[#A78BFA]" /> : <Video className="h-4 w-4 text-[#A78BFA]" />}
            {mode === 'shot' ? '区域截图' : '区域录屏'}
          </span>
          <select
            value={specIdx}
            onChange={(e) => setSpecIdx(Number(e.target.value))}
            aria-label="目标设备比例"
            className="rounded-lg border border-white/20 bg-[#241259] px-2 py-1 text-[12px] text-white outline-none focus:border-[#A78BFA]"
          >
            {EDITOR_DEVICE_SPECS.map((sp, i) => (
              <option key={sp.id} value={i}>
                {sp.label} · {sp.size[0]}×{sp.size[1]}
              </option>
            ))}
            <option value={-1}>自由框选</option>
          </select>
          {mode === 'rec' && (
            <label className="flex cursor-pointer items-center gap-1.5 text-[#E5E0FA]">
              <input
                type="checkbox"
                checked={recordCursor}
                onChange={(e) => setRecordCursor(e.target.checked)}
                className="accent-[#5E45F8]"
              />
              录制鼠标
            </label>
          )}
          <label className="flex cursor-pointer items-center gap-1.5 text-[#E5E0FA]">
            <input
              type="checkbox"
              checked={includeApp}
              onChange={(e) => setIncludeApp(e.target.checked)}
              className="accent-[#5E45F8]"
            />
            包含软件窗口
          </label>
          <span className="hidden text-[#B6AFD8] md:inline">拖动框选 · Esc 取消</span>
        </div>
      )}

      {/* Bottom Bar */}
      {phase === 'select' && (
        <div className="cap-bar fixed bottom-5 left-1/2 z-10 flex -translate-x-1/2 items-center gap-3 whitespace-nowrap rounded-xl border border-white/15 bg-[#150A38]/92 px-4 py-2 text-[12px] shadow-2xl backdrop-blur-xl">
          <span className="text-[#B6AFD8]">演示环境：采用模拟桌面画布实时采集</span>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg bg-white/10 px-3.5 py-1.5 font-semibold hover:bg-white/20"
          >
            取消
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            className="rounded-lg bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-4 py-1.5 font-bold text-white shadow-[0_6px_16px_rgba(94,69,248,0.4)] hover:opacity-95"
          >
            {mode === 'shot' ? '确认截图并放入画布' : '开始录制（3s 倒计时）'}
          </button>
        </div>
      )}

      {/* Recording Chip */}
      {phase === 'recording' && (
        <div className="cap-bar fixed left-1/2 top-4 z-10 flex -translate-x-1/2 items-center gap-3 whitespace-nowrap rounded-lg border border-red-500/40 bg-slate-900/95 px-4 py-2 text-[12px] shadow-2xl">
          <span className="h-2.5 w-2.5 animate-pulse rounded-full bg-red-500" />
          <span className="font-mono-tabular font-bold">
            {pad2(Math.floor(recElapsed / 60))}:{pad2(recElapsed % 60)}
          </span>
          <span className="text-slate-300">正在录制 WebM 视频…</span>
          <button
            type="button"
            onClick={() => stopRecording(true)}
            className="rounded-md bg-red-600 px-3 py-1 font-bold text-white hover:bg-red-500"
          >
            停止并放入画布
          </button>
          <button
            type="button"
            onClick={() => stopRecording(false)}
            aria-label="放弃录制"
            className="rounded-md bg-white/10 p-1 hover:bg-white/20"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}
    </div>
  );
};
