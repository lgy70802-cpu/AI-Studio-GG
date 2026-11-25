import React from 'react';
import { Device, Resource, Playlist } from '../types';
import { dims, fit, sizeText, MOODS } from '../data';
import { ScreenBox } from '../components/ScreenBox';
import { ResourceCard } from '../components/ResourceCard';
import {
  Monitor,
  Play,
  Square,
  ChevronLeft,
  ChevronRight,
  Settings,
  Sparkles,
  Layers,
  ArrowRight,
  Clock,
  RotateCw,
} from 'lucide-react';

interface OverviewPageProps {
  devices: Device[];
  resources: Resource[];
  playlists: Playlist[];
  favorites: number[];
  recentHistory: { id: number; t: number }[];
  onGoPage: (page: string) => void;
  onRequestOrientation: (deviceId: string, rot: number) => void;
  onToggleOutput: (deviceId: string) => void;
  onStepPlaylist: (deviceId: string, dir: number) => void;
  onSelectTargetDevice: (id: string) => void;
  onOpenDetail: (id: number) => void;
  onOpenEditor: (id: number) => void;
  onApply: (id: number, targetDevice?: string) => void;
  onApplyOther: (id: number) => void;
  onAddToPlaylist: (id: number) => void;
  onToggleFavorite: (id: number) => void;
  onPlayPlaylistNow: (index: number) => void;
  onConnectSingle: () => void;
}

export const OverviewPage: React.FC<OverviewPageProps> = ({
  devices,
  resources,
  playlists,
  favorites,
  recentHistory,
  onGoPage,
  onRequestOrientation,
  onToggleOutput,
  onStepPlaylist,
  onSelectTargetDevice,
  onOpenDetail,
  onOpenEditor,
  onApply,
  onApplyOther,
  onAddToPlaylist,
  onToggleFavorite,
  onPlayPlaylistNow,
  onConnectSingle,
}) => {
  const getResource = (id: number): Resource => {
    return (
      resources.find((r) => r.id === id) || {
        id: 0,
        name: '未选择内容',
        type: '壁纸',
        style: 'ink',
        sizes: [],
        local: false,
      }
    );
  };

  const getRelativeTime = (time: number) => {
    const diffMin = Math.round((Date.now() - time) / 60000);
    if (diffMin < 1) return '刚刚';
    if (diffMin < 60) return `${diffMin} 分钟前`;
    const diffHours = Math.round(diffMin / 60);
    if (diffHours < 24) return `${diffHours} 小时前`;
    return `${Math.round(diffHours / 24)} 天前`;
  };

  // --- Single Device Mode ---
  if (devices.length === 1) {
    const d = devices[0];
    const ds = dims(d);
    const isVertical = ds[0] < ds[1];
    const currentRes = getResource(d.content);
    const activePlaylist = playlists.find(
      (p) => p.playing && p.device === d.id && p.items.length > 0
    );
    const fitRes = fit(currentRes, d);
    const isMismatch =
      d.online && d.content > 0 && (fitRes.loss === null || fitRes.loss > 0.05);

    // Recommended items matching current device aspect ratio
    const recommended = resources
      .filter(
        (r) =>
          r.local &&
          r.sizes.length > 0 &&
          fit(r, d).loss !== null &&
          fit(r, d).loss! <= 0.05 &&
          r.id !== d.content
      )
      .slice(0, isVertical ? 6 : 4);

    // Recent history matching current orientation
    const recentItems = recentHistory
      .map((item) => ({ ...item, res: getResource(item.id) }))
      .filter(
        ({ res }) =>
          res.id > 0 &&
          res.local &&
          res.sizes.length > 0 &&
          fit(res, d).loss !== null &&
          fit(res, d).loss! <= 0.05 &&
          res.id !== d.content
      )
      .slice(0, 3);

    const devicePlaylists = playlists
      .map((p, idx) => ({ p, idx }))
      .filter(({ p }) => p.device === d.id || !devices.some((x) => x.id === p.device));

    return (
      <div className="space-y-6">
        {/* Page Heading */}
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
              {d.name}
            </h1>
            <p className="text-xs text-slate-500 mt-1">
              桌面专属超宽副屏 · 实时输出监控与场景快速切换
            </p>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => onGoPage('devices')}
              className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors"
            >
              设备设置
            </button>
            <button
              onClick={() => {
                onSelectTargetDevice(d.id);
                onGoPage('library');
              }}
              className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
            >
              <Sparkles className="w-3.5 h-3.5" />
              更换内容
            </button>
          </div>
        </div>

        {/* Device Alert Warning if Disconnected */}
        {!d.online && (
          <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200 flex items-center justify-between text-xs text-amber-800">
            <span>当前设备处于断开状态，之前的内容配置与历史记录已完整保留在本地。</span>
            <button
              onClick={onConnectSingle}
              className="px-3 py-1 rounded-md bg-white border border-amber-300 hover:bg-amber-100 text-amber-900 font-medium"
            >
              模拟重新连接
            </button>
          </div>
        )}

        {/* Orientation Mismatch Alert */}
        {isMismatch && (
          <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200 flex items-center justify-between text-xs text-amber-800">
            <span>
              当前显示的内容「{currentRes.name}」与{isVertical ? '竖屏' : '横屏'}
              规格差异明显，建议选择专为该方向设计的内容。
            </span>
            <button
              onClick={() => {
                onSelectTargetDevice(d.id);
                onGoPage('library');
              }}
              className="px-3 py-1 rounded-md bg-white border border-amber-300 hover:bg-amber-100 text-amber-900 font-medium"
            >
              前往挑选适合内容 →
            </button>
          </div>
        )}

        {/* Screen Hero Showcase Section */}
        <section
          className={`bg-white rounded-2xl border border-slate-200 p-5 shadow-xs ${
            d.flash ? 'device-flashing' : ''
          }`}
        >
          {/* Header Controls */}
          <div className="flex flex-wrap items-center justify-between gap-3 pb-4 border-b border-slate-100">
            <div className="flex items-center gap-2">
              <span className="font-semibold text-sm text-slate-900">
                {isVertical ? '▯ 竖屏实时呈现' : '▭ 横屏实时呈现'}
              </span>
              <span className="text-xs text-slate-400">
                · {d.model} ({sizeText(ds)})
              </span>
            </div>

            <div className="flex items-center gap-3">
              {/* Orientation Switcher Segments */}
              <div className="flex items-center bg-slate-100 p-0.5 rounded-lg border border-slate-200/60">
                <button
                  type="button"
                  onClick={() => onRequestOrientation(d.id, 0)}
                  className={`px-3 py-1 text-xs font-medium rounded-md transition-all ${
                    !isVertical
                      ? 'bg-white text-[#4268ef] shadow-xs font-semibold'
                      : 'text-slate-600 hover:text-slate-900'
                  }`}
                >
                  ▭ 横屏
                </button>
                <button
                  type="button"
                  onClick={() => onRequestOrientation(d.id, 90)}
                  className={`px-3 py-1 text-xs font-medium rounded-md transition-all ${
                    isVertical
                      ? 'bg-white text-[#4268ef] shadow-xs font-semibold'
                      : 'text-slate-600 hover:text-slate-900'
                  }`}
                >
                  ▯ 竖屏
                </button>
              </div>

              {/* Status Badge */}
              <span
                className={`px-2.5 py-1 rounded-full text-[11px] font-medium flex items-center gap-1.5 ${
                  !d.online
                    ? 'bg-amber-50 text-amber-700 border border-amber-200'
                    : d.running
                    ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                    : 'bg-slate-100 text-slate-600'
                }`}
              >
                <span
                  className={`w-1.5 h-1.5 rounded-full ${
                    !d.online
                      ? 'bg-amber-500'
                      : d.running
                      ? 'bg-emerald-500'
                      : 'bg-slate-400'
                  }`}
                />
                {!d.online ? '设备未连接' : d.running ? '正在显示' : '输出已停止'}
              </span>
            </div>
          </div>

          {/* Screen Bezel & Canvas Preview */}
          <div className="my-5 bg-gradient-to-b from-[#eef2f8] to-[#f8f9fc] rounded-xl p-4 sm:p-6 flex items-center justify-center min-h-[220px]">
            <ScreenBox
              resource={currentRes}
              device={d}
              mode={d.fit || 'contain'}
              className="max-h-[300px] max-w-full flash-target ring-4 ring-slate-800/10"
            />
          </div>

          {/* Footer Playing Information & Controls */}
          <div className="flex flex-wrap items-center justify-between gap-4 pt-3 border-t border-slate-100">
            <div>
              <h3 className="text-base font-bold text-slate-900">
                {currentRes.name}
              </h3>
              <p className="text-xs text-slate-500 mt-0.5">
                {activePlaylist
                  ? `正在轮播「${activePlaylist.name}」· 第 ${
                      activePlaylist.at + 1
                    }/${activePlaylist.items.length} 项`
                  : currentRes.type +
                    (d.fit === 'cover' ? ' · 裁切铺满' : ' · 完整显示')}
                {!d.online && ' · 重新连接后自动恢复输出'}
              </p>
            </div>

            <div className="flex items-center gap-2">
              {(currentRes.type === '主题' || currentRes.type === '时钟') && (
                <button
                  onClick={() => onOpenEditor(currentRes.id)}
                  className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors"
                >
                  编辑此主题
                </button>
              )}

              {activePlaylist && (
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => onStepPlaylist(d.id, -1)}
                    className="p-1.5 rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700"
                    title="轮播上一项"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => onStepPlaylist(d.id, 1)}
                    className="p-1.5 rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700"
                    title="轮播下一项"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              )}

              <button
                disabled={!d.online}
                onClick={() => onToggleOutput(d.id)}
                className={`px-3.5 py-1.5 text-xs font-semibold rounded-lg flex items-center gap-1.5 transition-colors ${
                  d.running
                    ? 'border border-slate-200 bg-white hover:bg-rose-50 text-rose-600'
                    : 'bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs'
                }`}
              >
                {d.running ? (
                  <>
                    <Square className="w-3.5 h-3.5 fill-current" />
                    停止输出
                  </>
                ) : (
                  <>
                    <Play className="w-3.5 h-3.5 fill-current" />
                    恢复显示
                  </>
                )}
              </button>
            </div>
          </div>
        </section>

        {/* Quick Playlist Mood Chips */}
        {devicePlaylists.length > 0 && (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs text-slate-400 font-medium mr-1">
              快速启动轮播：
            </span>
            {devicePlaylists.map(({ p, idx }) => {
              const mood = MOODS.find((m) => m.k === p.mood) || MOODS[0];
              const validCount = p.items.filter((it) => {
                const r = getResource(it.id);
                const f = fit(r, d);
                return f.loss !== null && f.loss <= 0.05;
              }).length;

              return (
                <button
                  key={p.name + idx}
                  disabled={validCount === 0}
                  onClick={() => onPlayPlaylistNow(idx)}
                  className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border transition-all ${
                    p.playing
                      ? 'bg-blue-50 border-[#4268ef] text-[#4268ef] font-semibold ring-1 ring-[#cbd6ff]'
                      : 'bg-white border-slate-200 hover:border-slate-300 text-slate-700'
                  }`}
                >
                  <span
                    className="w-4 h-4 rounded-full flex items-center justify-center text-[10px] font-bold"
                    style={{ backgroundColor: `${mood.c}20`, color: mood.c }}
                  >
                    {mood.g}
                  </span>
                  <span>{p.name}</span>
                  <span className="text-[10px] text-slate-400">
                    ({validCount} 项{p.playing ? ' · 播放中' : ''})
                  </span>
                </button>
              );
            })}
            <button
              onClick={() => onGoPage('playlists')}
              className="text-xs text-[#4268ef] hover:underline font-medium ml-1"
            >
              管理列表 →
            </button>
          </div>
        )}

        {/* Recent History Section */}
        {recentItems.length > 0 && (
          <section className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
                <Clock className="w-4 h-4 text-slate-400" />
                最近使用过的画面
              </h2>
              <span className="text-xs text-slate-400">
                点击卡片即可直接再次应用
              </span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              {recentItems.map(({ res, t }) => (
                <div key={res.id + t} className="relative group">
                  <ResourceCard
                    resource={res}
                    refDevice={d}
                    isFavorite={favorites.includes(res.id)}
                    cardClickMode="apply"
                    onToggleFavorite={onToggleFavorite}
                    onOpenDetail={onOpenDetail}
                    onApply={(id) => onApply(id, d.id)}
                    onAddToPlaylist={onAddToPlaylist}
                  />
                  <div className="text-[11px] text-slate-400 mt-1.5 text-center">
                    上次使用于 {getRelativeTime(t)}
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

        {/* Recommended for current orientation */}
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900">
              适合{isVertical ? '竖屏' : '横屏'}的热门内容
            </h2>
            <button
              onClick={() => {
                onSelectTargetDevice(d.id);
                onGoPage('library');
              }}
              className="text-xs text-[#4268ef] hover:underline font-medium flex items-center gap-1"
            >
              浏览全部适合素材
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div
            className={`grid gap-3 ${
              isVertical
                ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-6'
                : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-4'
            }`}
          >
            {recommended.map((r) => (
              <ResourceCard
                key={r.id}
                resource={r}
                refDevice={d}
                isFavorite={favorites.includes(r.id)}
                onToggleFavorite={onToggleFavorite}
                onOpenDetail={onOpenDetail}
                onOpenEditor={onOpenEditor}
                onApply={(id) => onApply(id, d.id)}
                onAddToPlaylist={onAddToPlaylist}
              />
            ))}
          </div>
        </section>
      </div>
    );
  }

  // --- Multi-Device Mode ---
  const onlineCount = devices.filter((d) => d.online).length;
  const runningCount = devices.filter((d) => d.online && d.running).length;
  const localCount = resources.filter((r) => r.local).length;
  const favoriteResources = resources.filter((r) => favorites.includes(r.id));

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            你的设备，各有精彩
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            独立管理每台 MiraBox 屏幕，让内容在最合适的位置呈现。
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => onGoPage('devices')}
            className="px-3.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors"
          >
            管理设备
          </button>
          <button
            onClick={() => onGoPage('library')}
            className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
          >
            <Sparkles className="w-3.5 h-3.5" />
            挑选内容
          </button>
        </div>
      </div>

      {/* Metrics Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-2xs">
          <span className="text-xs text-slate-400 block mb-1">已连接设备</span>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900 tabular-nums">
              {onlineCount}
            </span>
            <span className="text-xs text-slate-500">/ {devices.length} 台物理屏</span>
          </div>
        </div>

        <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-2xs">
          <span className="text-xs text-slate-400 block mb-1">正在独立输出</span>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900 tabular-nums">
              {runningCount}
            </span>
            <span className="text-xs text-slate-500">台屏幕独立渲染中</span>
          </div>
        </div>

        <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-2xs">
          <span className="text-xs text-slate-400 block mb-1">本地资源总计</span>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900 tabular-nums">
              {localCount}
            </span>
            <span className="text-xs text-slate-500">项 · 跨设备共享存储</span>
          </div>
        </div>
      </div>

      {/* Multi-Device List */}
      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-bold text-slate-900">所有设备状态</h2>
          <span className="text-xs text-slate-400">
            切换查看每台屏幕，操作不会打断其他设备输出
          </span>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {devices.map((d) => {
            const currentRes = getResource(d.content);
            const ds = dims(d);
            const isVertical = ds[0] < ds[1];

            return (
              <div
                key={d.id}
                className="rounded-2xl border border-slate-200 bg-white p-4 shadow-2xs flex flex-col justify-between"
              >
                <div>
                  <div className="flex items-center justify-between mb-3">
                    <div>
                      <h3 className="font-bold text-sm text-slate-900">{d.name}</h3>
                      <p className="text-[11px] text-slate-400 mt-0.5">
                        {d.model} · {sizeText(ds)} · {isVertical ? '竖屏' : '横屏'}
                      </p>
                    </div>
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-medium flex items-center gap-1 ${
                        !d.online
                          ? 'bg-slate-100 text-slate-500'
                          : d.running
                          ? 'bg-emerald-50 text-emerald-700'
                          : 'bg-amber-50 text-amber-700'
                      }`}
                    >
                      <span
                        className={`w-1.5 h-1.5 rounded-full ${
                          !d.online
                            ? 'bg-slate-400'
                            : d.running
                            ? 'bg-emerald-500'
                            : 'bg-amber-500'
                        }`}
                      />
                      {!d.online ? '未连接' : d.running ? '正在显示' : '待机中'}
                    </span>
                  </div>

                  <div className="bg-slate-100 rounded-xl p-3 flex items-center justify-center min-h-[140px] mb-3">
                    <ScreenBox
                      resource={currentRes}
                      device={d}
                      mode={d.fit || 'contain'}
                      className="max-h-[120px] max-w-full"
                    />
                  </div>

                  <div className="text-xs mb-4">
                    <strong className="text-slate-900 block font-semibold truncate">
                      {currentRes.name}
                    </strong>
                    <span className="text-[11px] text-slate-400">
                      {d.online ? 'USB 直连 · 独立会话' : '重新连接后自动恢复'}
                    </span>
                  </div>
                </div>

                <div className="flex items-center gap-2 pt-3 border-t border-slate-100">
                  <button
                    onClick={() => {
                      onSelectTargetDevice(d.id);
                      onGoPage('devices');
                    }}
                    className="flex-1 py-1.5 px-2 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 text-center transition-colors"
                  >
                    设置
                  </button>
                  <button
                    disabled={!d.online}
                    onClick={() => onToggleOutput(d.id)}
                    className="flex-1 py-1.5 px-2 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 text-center transition-colors"
                  >
                    {d.running ? '停止输出' : '恢复显示'}
                  </button>
                  <button
                    onClick={() => {
                      onSelectTargetDevice(d.id);
                      onGoPage('library');
                    }}
                    className="flex-1 py-1.5 px-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white text-center transition-colors shadow-xs"
                  >
                    更换内容
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* Favorite Content Grid */}
      {favoriteResources.length > 0 && (
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900">我收藏的常用内容</h2>
            <button
              onClick={() => onGoPage('library')}
              className="text-xs text-[#4268ef] hover:underline font-medium"
            >
              查看全部内容 →
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {favoriteResources.map((r) => (
              <ResourceCard
                key={r.id}
                resource={r}
                refDevice={devices[0]}
                isFavorite={true}
                hasMultipleDevices={devices.length > 1}
                onToggleFavorite={onToggleFavorite}
                onOpenDetail={onOpenDetail}
                onOpenEditor={onOpenEditor}
                onApply={(id) => onApply(id, devices[0]?.id)}
                onApplyOther={onApplyOther}
                onAddToPlaylist={onAddToPlaylist}
              />
            ))}
          </div>
        </section>
      )}
    </div>
  );
};
