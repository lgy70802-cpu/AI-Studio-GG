import React, { useEffect, useState } from 'react';
import { cancelUpdate, checkUpdates, desktopMode, installUpdate, updateStatus, UpdateManifest } from '../services/desktopBridge';

export function UpdatesPanel({ flush }: { flush: () => Promise<void> }) {
  const [version, setVersion] = useState('');
  const [source, setSource] = useState('');
  const [available, setAvailable] = useState<UpdateManifest | null>(null);
  const [busy, setBusy] = useState(false);
  const [installing, setInstalling] = useState(false);
  const [message, setMessage] = useState('');
  const [percent, setPercent] = useState(-1);
  useEffect(() => {
    if (!desktopMode) return;
    void updateStatus().then(status => { setVersion(status.version); setSource(status.source); }).catch(error => setMessage(error.message));
    const update = (event: Event) => {
      const value = (event as CustomEvent<{phase: string; percent: number}>).detail;
      setMessage(value.phase); setPercent(value.percent);
    };
    window.addEventListener('studio-update-progress', update);
    return () => window.removeEventListener('studio-update-progress', update);
  }, []);
  async function check() {
    setBusy(true); setMessage('正在检查更新…'); setAvailable(null); setPercent(-1);
    try {
      const result = await checkUpdates(source);
      setVersion(result.current); setAvailable(result.available);
      setMessage(result.available ? `发现新版本 ${result.available.Version}` : '当前已是最新版本');
    } catch (error) { setMessage('检查失败：' + (error as Error).message); }
    finally { setBusy(false); }
  }
  async function install() {
    setBusy(true); setInstalling(true); setPercent(-1); setMessage('正在准备更新…');
    try { await flush(); await installUpdate(); }
    catch (error) { setMessage('更新未完成：' + (error as Error).message); }
    finally { setBusy(false); setInstalling(false); }
  }
  return (
    <section className="space-y-3 rounded-[20px] border border-[#E6E4F2] bg-white p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div><h3 className="font-bold text-[#1A172B]">软件更新</h3><p className="mt-1 text-xs text-[#7A7690]">当前版本 {version || '—'}</p></div>
        <button disabled={!desktopMode || busy || !source.trim()} onClick={() => void check()}
          className="rounded-xl bg-[#5E45F8] px-4 py-2 text-xs font-semibold text-white disabled:opacity-50">检查更新</button>
      </div>
      <label className="block text-xs text-[#6B6880]">HTTPS 更新清单地址
        <input aria-label="HTTPS 更新清单地址" value={source} disabled={busy} onChange={event => { setSource(event.target.value); setAvailable(null); setMessage(''); }}
          placeholder="https://example.com/mirabox/update.json" className="mt-2 w-full rounded-xl border border-[#E6E4F2] px-3 py-2 text-xs focus:border-[#5E45F8] focus:outline-none" />
      </label>
      {message && <p role="status" className="break-words text-xs text-[#6B6880]">{message}</p>}
      {available && <div className="space-y-3 rounded-xl bg-[#F7F5FF] p-4 text-xs text-[#56526B]">
        <p className="font-semibold">版本 {available.Version} · {(available.Bytes / 1024 / 1024).toFixed(1)} MiB</p>
        <p className="whitespace-pre-wrap">{available.Notes || '发行方未提供更新说明。'}</p>
        <p>请先保存编辑器中的修改。软件会校验安装包，备份配置和旧程序，然后退出并打开安装器。安装完成后启动新版。</p>
        <button disabled={busy} onClick={() => void install()} className="rounded-xl bg-[#5E45F8] px-4 py-2 font-semibold text-white disabled:opacity-50">备份并安装更新</button>
      </div>}
      {installing && <div className="fixed inset-0 z-[120] flex items-center justify-center bg-[#140B36]/45 p-5 backdrop-blur-sm">
        <div className="w-full max-w-sm space-y-4 rounded-2xl bg-white p-6 shadow-xl">
          <p role="status" className="text-sm font-semibold text-[#1A172B]">{message || '正在准备更新…'}</p>
          {percent >= 0 && <progress className="w-full accent-[#5E45F8]" max={100} value={percent} />}
          <button onClick={() => void cancelUpdate()} className="rounded-xl border border-[#E6E4F2] px-4 py-2 text-xs">取消更新</button>
        </div>
      </div>}
    </section>
  );
}
