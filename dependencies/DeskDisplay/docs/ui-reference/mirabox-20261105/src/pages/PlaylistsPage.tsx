import React, { useState, useEffect } from 'react';
import { Device, Resource, Playlist, PlaylistItem } from '../types';
import { fit, dims, sizeText, MOODS } from '../data';
import { ScreenBox } from '../components/ScreenBox';
import {
  Play,
  Pause,
  ChevronLeft,
  ChevronRight,
  Plus,
  Trash2,
  Shuffle,
  Clock,
  Sparkles,
  ArrowUp,
  ArrowDown,
  GripVertical,
} from 'lucide-react';

interface PlaylistsPageProps {
  playlists: Playlist[];
  devices: Device[];
  resources: Resource[];
  selectedPlaylistIndex: number;
  onSelectPlaylist: (index: number) => void;
  onNewPlaylist: () => void;
  onOpenAddPicker: () => void;
  onTogglePlaylist: (index: number) => void;
  onStepPlaylist: (deviceId: string, dir: number) => void;
  onChangePlaylistDevice: (playlistIndex: number, deviceId: string) => void;
  onChangePlaylistMood: (playlistIndex: number, mood: string) => void;
  onToggleShuffle: (playlistIndex: number, shuffle: boolean) => void;
  onApplyUniformSeconds: (playlistIndex: number, seconds: number) => void;
  onChangeItemSeconds: (
    playlistIndex: number,
    itemIndex: number,
    seconds: number
  ) => void;
  onMoveItem: (playlistIndex: number, itemIndex: number, dir: number) => void;
  onRemoveItem: (playlistIndex: number, itemIndex: number) => void;
}

export const PlaylistsPage: React.FC<PlaylistsPageProps> = ({
  playlists,
  devices,
  resources,
  selectedPlaylistIndex,
  onSelectPlaylist,
  onNewPlaylist,
  onOpenAddPicker,
  onTogglePlaylist,
  onStepPlaylist,
  onChangePlaylistDevice,
  onChangePlaylistMood,
  onToggleShuffle,
  onApplyUniformSeconds,
  onChangeItemSeconds,
  onMoveItem,
  onRemoveItem,
}) => {
  const [uniformSec, setUniformSec] = useState(30);
  const [ticker, setTicker] = useState(0);

  // Simulated ticker for progress bar animation
  useEffect(() => {
    const timer = setInterval(() => {
      setTicker((t) => t + 1);
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const p = playlists[selectedPlaylistIndex] || playlists[0];
  if (!p) {
    return (
      <div className="py-16 text-center text-slate-500 bg-white rounded-2xl border border-slate-200">
        <h2 className="text-base font-bold text-slate-900 mb-2">暂无轮播列表</h2>
        <button
          onClick={onNewPlaylist}
          className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] text-white shadow-xs"
        >
          创建第一个轮播列表
        </button>
      </div>
    );
  }

  const targetDev = devices.find((d) => d.id === p.device) || devices[0];
  const currentItem = p.items[p.at];
  const currentResource = resources.find((r) => r.id === currentItem?.id) || {
    id: 0,
    name: '未选择内容',
    type: '壁纸' as const,
    style: 'ink',
    sizes: [],
    local: false,
  };

  const nextIndex =
    p.items.length > 1 ? (p.at + 1) % p.items.length : p.at;
  const nextResource = resources.find((r) => r.id === p.items[nextIndex]?.id);

  const durationSec = currentItem ? currentItem.sec : 30;
  const progressRatio = ((ticker % durationSec) / durationSec) * 100;
  const remainingSec = durationSec - (ticker % durationSec);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            轮播列表管理
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            将超宽壁纸、流光视频与动态时钟组合成队列，每台屏幕独立自动轮播。
          </p>
        </div>
        <button
          onClick={onNewPlaylist}
          className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
        >
          <Plus className="w-3.5 h-3.5" />
          新建轮播列表
        </button>
      </div>

      {/* Main Split Grid */}
      <div className="grid grid-cols-1 md:grid-cols-12 gap-6 items-start">
        {/* Left: Playlists List */}
        <div className="md:col-span-4 space-y-2">
          {playlists.map((item, idx) => {
            const mood = MOODS.find((m) => m.k === item.mood) || MOODS[0];
            const isSelected = idx === selectedPlaylistIndex;
            const dev = devices.find((d) => d.id === item.device);

            return (
              <div
                key={item.name + idx}
                onClick={() => onSelectPlaylist(idx)}
                className={`p-3.5 rounded-xl border cursor-pointer transition-all flex items-center justify-between ${
                  isSelected
                    ? 'border-[#4268ef] bg-[#edf2ff]/40 ring-1 ring-[#4268ef]'
                    : 'border-slate-200 bg-white hover:border-slate-300'
                }`}
              >
                <div className="flex items-center gap-3">
                  <span
                    className="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold shrink-0"
                    style={{ backgroundColor: `${mood.c}20`, color: mood.c }}
                  >
                    {mood.g}
                  </span>
                  <div>
                    <h3 className="font-semibold text-xs text-slate-900">
                      {item.name}
                    </h3>
                    <p className="text-[11px] text-slate-400 mt-0.5">
                      {item.items.length} 项内容 ·{' '}
                      {dev ? dev.name : '未关联设备'}
                    </p>
                  </div>
                </div>

                <span
                  className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${
                    item.playing
                      ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                      : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  {item.playing ? '● 正在轮播' : '已暂停'}
                </span>
              </div>
            );
          })}
        </div>

        {/* Right: Selected Playlist Detail */}
        <div className="md:col-span-8 bg-white rounded-2xl border border-slate-200 p-5 space-y-5 shadow-2xs">
          {/* Top Playlist Header */}
          <div className="flex flex-wrap items-center justify-between gap-3 pb-4 border-b border-slate-100">
            <div>
              <div className="flex items-center gap-2">
                <span
                  className="w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold"
                  style={{
                    backgroundColor: `${
                      MOODS.find((m) => m.k === p.mood)?.c || '#4268ef'
                    }20`,
                    color:
                      MOODS.find((m) => m.k === p.mood)?.c || '#4268ef',
                  }}
                >
                  {MOODS.find((m) => m.k === p.mood)?.g || '◎'}
                </span>
                <h2 className="text-lg font-bold text-slate-900">{p.name}</h2>
              </div>
              <p className="text-xs text-slate-400 mt-1">
                已编排 {p.items.length} 个独立显示项目 · 支持拖拽排序与单项独立停留时间
              </p>
            </div>

            <button
              onClick={onOpenAddPicker}
              className="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
            >
              <Plus className="w-3.5 h-3.5" />
              添加内容到列表
            </button>
          </div>

          {/* Device and Action Controls Row */}
          <div className="flex flex-wrap items-center justify-between gap-3 text-xs bg-slate-50 p-3 rounded-xl border border-slate-200">
            <div className="flex items-center gap-2">
              <span className="text-slate-500">输出目标屏幕：</span>
              <select
                value={p.device}
                onChange={(e) =>
                  onChangePlaylistDevice(selectedPlaylistIndex, e.target.value)
                }
                className="px-2.5 py-1 rounded-md border border-slate-200 bg-white font-medium text-slate-800"
              >
                {devices.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name} · {d.model} ({d.online ? '在线' : '离线'})
                  </option>
                ))}
              </select>
            </div>

            <div className="flex items-center gap-2">
              <button
                disabled={!p.playing || !targetDev?.online}
                onClick={() => onStepPlaylist(p.device, -1)}
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 disabled:opacity-40"
                title="立即切换至上一项"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <button
                disabled={!p.playing || !targetDev?.online}
                onClick={() => onStepPlaylist(p.device, 1)}
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 disabled:opacity-40"
                title="立即切换至下一项"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <button
                onClick={() => onTogglePlaylist(selectedPlaylistIndex)}
                className={`px-4 py-1.5 rounded-lg font-semibold flex items-center gap-1.5 transition-colors shadow-xs ${
                  p.playing
                    ? 'bg-amber-600 hover:bg-amber-700 text-white'
                    : 'bg-[#4268ef] hover:bg-[#3458db] text-white'
                }`}
              >
                {p.playing ? (
                  <>
                    <Pause className="w-3.5 h-3.5 fill-current" />
                    暂停轮播切换
                  </>
                ) : (
                  <>
                    <Play className="w-3.5 h-3.5 fill-current" />
                    开始独立轮播
                  </>
                )}
              </button>
            </div>
          </div>

          {/* Simulated Playback Progress Box */}
          {p.playing && targetDev?.online && (
            <div className="p-4 rounded-xl bg-[#edf2ff] border border-[#cbd6ff] space-y-2 text-xs">
              <div className="flex items-center justify-between font-semibold text-[#25314b]">
                <span>当前屏幕正显示：「{currentResource.name}」</span>
                <span className="text-[#4268ef] tabular-nums font-mono">
                  剩余 {remainingSec} 秒切换
                </span>
              </div>

              <div className="w-full bg-white/70 h-2 rounded-full overflow-hidden">
                <div
                  className="bg-[#4268ef] h-full transition-all duration-300"
                  style={{ width: `${progressRatio}%` }}
                />
              </div>

              <div className="flex items-center justify-between text-[11px] text-slate-500">
                <span>
                  下一项：
                  {p.shuffle
                    ? '随机选择下一个匹配内容'
                    : nextResource
                    ? nextResource.name
                    : '循环结束'}
                </span>
                <span>与设备规格不符的内容将自动智能跳过</span>
              </div>
            </div>
          )}

          {/* Quick Tools: Mood, Shuffle, Uniform Duration */}
          <div className="flex flex-wrap items-center justify-between gap-4 p-3 rounded-xl bg-slate-50 text-xs border border-slate-200">
            <div className="flex items-center gap-1.5">
              <span className="text-slate-400 mr-1">情境标记：</span>
              {MOODS.map((m) => (
                <button
                  key={m.k}
                  onClick={() => onChangePlaylistMood(selectedPlaylistIndex, m.k)}
                  className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs transition-all ${
                    p.mood === m.k
                      ? 'bg-white shadow-xs font-semibold'
                      : 'text-slate-600 hover:bg-slate-200'
                  }`}
                  style={{
                    color: p.mood === m.k ? m.c : undefined,
                    border:
                      p.mood === m.k
                        ? `1px solid ${m.c}40`
                        : '1px solid transparent',
                  }}
                >
                  <span>{m.g}</span>
                  <span>{m.n}</span>
                </button>
              ))}
            </div>

            <div className="flex items-center gap-3">
              <label className="flex items-center gap-1.5 cursor-pointer text-slate-700 font-medium">
                <input
                  type="checkbox"
                  checked={!!p.shuffle}
                  onChange={(e) =>
                    onToggleShuffle(selectedPlaylistIndex, e.target.checked)
                  }
                  className="rounded text-[#4268ef]"
                />
                随机乱序播放
              </label>

              <div className="h-4 w-px bg-slate-200" />

              <div className="flex items-center gap-1.5">
                <span className="text-slate-500">统一设置停留：</span>
                <input
                  type="number"
                  min="10"
                  max="3600"
                  value={uniformSec}
                  onChange={(e) => setUniformSec(Number(e.target.value) || 30)}
                  className="w-16 px-2 py-1 rounded border border-slate-200 bg-white font-mono text-center"
                />
                <span className="text-slate-500">秒</span>
                <button
                  onClick={() =>
                    onApplyUniformSeconds(selectedPlaylistIndex, uniformSec)
                  }
                  className="px-2.5 py-1 rounded border border-slate-200 bg-white hover:bg-slate-100 font-medium text-slate-700"
                >
                  应用到全部
                </button>
              </div>
            </div>
          </div>

          {/* Items Table */}
          {p.items.length === 0 ? (
            <div className="py-12 text-center text-xs text-slate-400 border border-dashed border-slate-200 rounded-xl">
              此轮播列表尚无任何内容，点击上方“添加内容到列表”添加吧。
            </div>
          ) : (
            <div className="border border-slate-200 rounded-xl overflow-hidden">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50 text-slate-500 border-b border-slate-200 font-medium">
                  <tr>
                    <th className="py-2.5 px-3">顺序 / 内容名称</th>
                    <th className="py-2.5 px-3">屏幕适配</th>
                    <th className="py-2.5 px-3">停留时间</th>
                    <th className="py-2.5 px-3 text-right">调序与移除</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {p.items.map((item, itemIdx) => {
                    const r = resources.find((res) => res.id === item.id) || {
                      id: item.id,
                      name: '未知素材',
                      type: '壁纸' as const,
                      style: 'ink',
                      sizes: [],
                      local: false,
                    };
                    const f = fit(r, targetDev);
                    const isCurrentPlaying = p.playing && p.at === itemIdx;

                    return (
                      <tr
                        key={item.id + itemIdx}
                        className={`hover:bg-slate-50/80 transition-colors ${
                          isCurrentPlaying ? 'bg-[#edf2ff]/30' : ''
                        }`}
                      >
                        <td className="py-2.5 px-3">
                          <div className="flex items-center gap-3">
                            <span className="text-slate-400 font-mono text-xs w-4">
                              {itemIdx + 1}.
                            </span>
                            <div className="w-16 h-8 bg-[#131c2d] rounded overflow-hidden flex items-center justify-center shrink-0">
                              <ScreenBox
                                resource={r}
                                device={targetDev}
                                mode="contain"
                                className="w-full h-full"
                              />
                            </div>
                            <div>
                              <strong className="font-semibold text-slate-900 block">
                                {r.name}
                              </strong>
                              <span className="text-[10px] text-slate-400">
                                {r.type}
                              </span>
                            </div>
                          </div>
                        </td>

                        <td className="py-2.5 px-3">
                          <span
                            className={`px-2 py-0.5 rounded text-[10px] font-medium ${
                              f.kind === 'ok'
                                ? 'bg-emerald-50 text-emerald-700'
                                : f.kind === 'blue'
                                ? 'bg-blue-50 text-blue-700'
                                : 'bg-amber-50 text-amber-700'
                            }`}
                          >
                            {f.text}
                          </span>
                        </td>

                        <td className="py-2.5 px-3">
                          <div className="flex items-center gap-1.5">
                            <input
                              type="number"
                              min="10"
                              max="3600"
                              value={item.sec}
                              onChange={(e) =>
                                onChangeItemSeconds(
                                  selectedPlaylistIndex,
                                  itemIdx,
                                  Number(e.target.value) || 30
                                )
                              }
                              className="w-16 px-2 py-1 text-xs border border-slate-200 rounded font-mono text-center bg-white"
                            />
                            <span className="text-slate-400">秒</span>
                          </div>
                        </td>

                        <td className="py-2.5 px-3 text-right">
                          <div className="flex items-center justify-end gap-1">
                            <button
                              disabled={itemIdx === 0}
                              onClick={() =>
                                onMoveItem(selectedPlaylistIndex, itemIdx, -1)
                              }
                              className="p-1 rounded hover:bg-slate-200 disabled:opacity-30 text-slate-600"
                              title="上移"
                            >
                              <ArrowUp className="w-3.5 h-3.5" />
                            </button>
                            <button
                              disabled={itemIdx === p.items.length - 1}
                              onClick={() =>
                                onMoveItem(selectedPlaylistIndex, itemIdx, 1)
                              }
                              className="p-1 rounded hover:bg-slate-200 disabled:opacity-30 text-slate-600"
                              title="下移"
                            >
                              <ArrowDown className="w-3.5 h-3.5" />
                            </button>
                            <button
                              onClick={() =>
                                onRemoveItem(selectedPlaylistIndex, itemIdx)
                              }
                              className="p-1 rounded hover:bg-rose-50 text-rose-600 ml-1"
                              title="从列表中移除"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
