import React, { useEffect, useState } from 'react';
import { ThemeArtwork } from './components/ThemeArtwork';
import type { ThemeItem } from './data/themes';

// Reuse the editor artwork itself: typography, effects, anchors and layout inheritance
// must have the same implementation on the editing canvas and the USB output.
export function DeviceRenderer() {
  const [state, setState] = useState<{ token: string; theme: ThemeItem; portrait: boolean; layoutIndex?: number }>();
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
      if (!cancelled) (window as any).chrome.webview.postMessage({ event: 'render-ready', token: state.token,
        error: images.some(img => !img.naturalWidth) || [...document.querySelectorAll('video')].some(v => v.error || v.readyState < 2) ? '主题图片或视频无法解码，请恢复素材或更换编码。' : null });
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
    const fail = () => (window as any).chrome.webview.postMessage({ event: 'render-failed', token: state.token, error: '主题视频解码中断，请检查文件或更换编码。' });
    videos.forEach(video => video.addEventListener('error', fail));
    return () => videos.forEach(video => video.removeEventListener('error', fail));
  }, [state]);
  useEffect(() => {
    if (!state) return;
    const timer = setInterval(() => (window as any).chrome.webview.postMessage({ event: 'render-heartbeat', token: state.token }), 1000);
    return () => clearInterval(timer);
  }, [state]);
  return <div style={{ width: '100vw', height: '100vh', overflow: 'hidden', background: 'black' }}>
    <style>{'.output-paused * { animation-play-state: paused !important; }'}</style>
    {state && <ThemeArtwork theme={{ ...state.theme, desktopStatic: false, desktopFlipped: false }}
      layoutIndex={state.layoutIndex} paused={paused} className="h-full w-full" showClock liveTick portrait={state.portrait} />}
  </div>;
}
