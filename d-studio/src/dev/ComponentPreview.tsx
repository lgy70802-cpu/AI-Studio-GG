import React, { useEffect, useState } from 'react';
import { FlipClock } from '../components/widgets/FlipClock';
import { EDITOR_DEVICE_SPECS } from '../data/editorCatalog';

interface ExampleConfig {
  sec: boolean;
  h24: boolean;
  color: string;
  size: number;
}
const defaults: ExampleConfig = { sec: true, h24: true, color: '#F4F1FF', size: 24 };
const storageKey = 'pixbuddy-component-example-v1';

function readConfig(): ExampleConfig {
  try {
    const stored = JSON.parse(localStorage.getItem(storageKey) || '{}');
    return {
      sec: typeof stored.sec === 'boolean' ? stored.sec : defaults.sec,
      h24: typeof stored.h24 === 'boolean' ? stored.h24 : defaults.h24,
      color: /^#[0-9a-f]{6}$/i.test(stored.color) ? stored.color : defaults.color,
      size: Number.isFinite(stored.size) ? Math.max(8, Math.min(30, stored.size)) : defaults.size,
    };
  } catch { return defaults; }
}

export default function ComponentPreview() {
  const [config, setConfig] = useState(readConfig);
  const [now, setNow] = useState(() => new Date());
  const [playing, setPlaying] = useState(true);
  const [message, setMessage] = useState('');
  useEffect(() => {
    if (!playing) return;
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, [playing]);
  const hours = String(config.h24 ? now.getHours() : now.getHours() % 12 || 12).padStart(2, '0');
  return (
    <main className="min-h-screen bg-[#F6F5FB] p-6 text-[#1A172B]">
      <h1 className="text-xl font-bold">翻页时钟开发预览</h1>
      <div className="my-5 flex flex-wrap items-center gap-4 rounded-2xl bg-white p-4 [&>button]:rounded-xl [&>button]:bg-[#EFEBFF] [&>button]:px-3 [&>button]:py-2 [&>button]:text-[#5E45F8]">
        <label><input type="checkbox" checked={config.sec} onChange={e => setConfig({ ...config, sec: e.target.checked })} /> 显示秒数</label>
        <label><input type="checkbox" checked={config.h24} onChange={e => setConfig({ ...config, h24: e.target.checked })} /> 24小时制</label>
        <label>颜色 <input aria-label="颜色" type="color" value={config.color} onChange={e => setConfig({ ...config, color: e.target.value })} /></label>
        <label>大小 <input aria-label="大小" type="range" min="8" max="30" value={config.size} onChange={e => setConfig({ ...config, size: Number(e.target.value) })} /></label>
        <button onClick={() => setPlaying(!playing)}>{playing ? '暂停' : '恢复'}</button>
        <button onClick={() => { setPlaying(false); setNow(new Date(2026, 10, 27, 11, 11, 11)); }}>示例 11:11:11</button>
        <button onClick={() => { setPlaying(false); setNow(new Date(2026, 10, 27, 20, 59, 59)); }}>示例 20:59:59</button>
        <button onClick={() => {
          try { localStorage.setItem(storageKey, JSON.stringify(config)); setMessage('配置已保存，刷新页面可恢复'); }
          catch { setMessage('配置保存失败，请检查浏览器存储权限'); }
        }}>保存配置</button>
        <button onClick={() => setConfig(defaults)}>恢复默认</button>
        <span role="status">{message}</span>
      </div>
      <div className="grid items-start gap-5 lg:grid-cols-2">
        {EDITOR_DEVICE_SPECS.map(spec => (
          <section key={spec.id} className="rounded-2xl bg-white p-4">
            <h2 className="mb-3 font-semibold">{spec.label} {spec.size.join('×')}</h2>
            <div data-preview-spec={spec.id} className="mx-auto flex items-center justify-center overflow-hidden rounded-xl bg-[#170E32]"
              style={{ aspectRatio: `${spec.size[0]} / ${spec.size[1]}`, width: spec.size[0] < spec.size[1] ? 170 : '100%', containerType: 'size', color: config.color }}>
              <div style={{ fontSize: `${config.size}cqmin`, fontWeight: 700 }}>
                <FlipClock hours={hours} minutes={String(now.getMinutes()).padStart(2, '0')} seconds={String(now.getSeconds()).padStart(2, '0')}
                  showSeconds={config.sec} portrait={spec.size[0] < spec.size[1]} animate={playing}
                  period={config.h24 ? undefined : now.getHours() >= 12 ? 'PM' : 'AM'} />
              </div>
            </div>
          </section>
        ))}
      </div>
    </main>
  );
}
