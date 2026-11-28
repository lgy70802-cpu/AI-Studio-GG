import React, { useEffect, useState } from 'react';
import { ThemeArtwork } from './components/ThemeArtwork';
import type { ThemeItem } from './data/themes';

function videoDecodeError(video: HTMLVideoElement) {
  const duration = Number.isFinite(video.duration) ? video.duration.toFixed(2) : '未知';
  const code = video.error?.code ?? 0;
  const detail = video.error?.message;
  const quality = video.getVideoPlaybackQuality();
  return `主题视频解码中断（错误码 ${code}，位置 ${video.currentTime.toFixed(2)}/${duration} 秒，解码掉帧 ${quality.droppedVideoFrames}/${quality.totalVideoFrames}）${detail ? '：' + detail : '。'}`;
}

// Reuse the editor artwork itself: typography, effects, anchors and layout inheritance
// must have the same implementation on the editing canvas and the USB output.
export function DeviceRenderer() {
  const [state, setState] = useState<{ token: string; theme: ThemeItem; portrait: boolean; layoutIndex?: number; rotation?: number }>();
  const [paused, setPaused] = useState(false);
  useEffect(() => {
    const bridge = (window as any).chrome.webview;
    const receive = (event: MessageEvent) => {
      if (event.data.command === 'render-theme') setState(event.data);
      if (event.data.command === 'render-paused') setPaused(event.data.paused);
    };
    bridge.addEventListener('message', receive);
    bridge.postMessage({ event: 'renderer-loaded' });
    return () => bridge.removeEventListener('message', receive);
  }, []);
  useEffect(() => {
    if (!state) return;
    let cancelled = false;
    void (async () => {
      await document.fonts.ready;
      const images = [...document.images];
      await Promise.all(images.map(img => img.decode().catch(() => {})));
      await Promise.all([...document.querySelectorAll('video')].map(video => new Promise<void>(resolve => {
        if (video.readyState >= 2 || video.error) return resolve();
        video.addEventListener('loadeddata', () => resolve(), { once: true });
        video.addEventListener('error', () => resolve(), { once: true });
        setTimeout(resolve, 8000);
      })));
      const videos = [...document.querySelectorAll('video')];
      videos.forEach(video => { video.pause(); if (!video.error) video.currentTime = 0; });
      await Promise.all(videos.map(video => video.play().catch(() => {})));
      await new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
      const failedVideo = videos.find(video => video.error || video.readyState < 2);
      if (!cancelled) (window as any).chrome.webview.postMessage({ event: 'render-ready', token: state.token,
        error: failedVideo ? videoDecodeError(failedVideo) : images.some(img => !img.naturalWidth) ? '主题图片无法解码，请恢复素材。' : null });
    })();
    return () => { cancelled = true; };
  }, [state]);
  useEffect(() => {
    document.documentElement.classList.toggle('output-paused', paused);
    document.querySelectorAll('video').forEach(video => { if (paused) video.pause(); else void video.play().catch(() => {}); });
  }, [paused, state]);
  useEffect(() => {
    if (!state) return;
    const videos = [...document.querySelectorAll('video')];
    const fail = (event: Event) => (window as any).chrome.webview.postMessage({
      event: 'render-failed',
      token: state.token,
      error: videoDecodeError(event.currentTarget as HTMLVideoElement),
    });
    videos.forEach(video => video.addEventListener('error', fail));
    return () => videos.forEach(video => video.removeEventListener('error', fail));
  }, [state]);
  useEffect(() => {
    if (!state) return;
    const timer = setInterval(() => (window as any).chrome.webview.postMessage({ event: 'render-heartbeat', token: state.token }), 1000);
    return () => clearInterval(timer);
  }, [state]);
  const rotation = state?.rotation ?? 0;
  const sideways = rotation === 90 || rotation === 270;
  return <div style={{ width: '100vw', height: '100vh', overflow: 'hidden', background: 'black' }}>
    <style>{'.output-paused * { animation-play-state: paused !important; }'}</style>
    {state && <div style={{ position: "absolute", left: "50%", top: "50%", width: sideways ? "100vh" : "100vw",
      height: sideways ? "100vw" : "100vh", transform: `translate(-50%, -50%) rotate(${rotation}deg)` }}>
      <ThemeArtwork theme={{ ...state.theme, desktopStatic: false, desktopFlipped: false }}
      layoutIndex={state.layoutIndex} paused={paused} className="h-full w-full" showClock liveTick portrait={state.portrait} />
    </div>}
  </div>;
}
