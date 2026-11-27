import React, { useState } from 'react';
import { Device, Resource } from '../types';
import { fit, dims, sizeText, CATS } from '../data';
import { ResourceCard } from '../components/ResourceCard';
import { Search, Sparkles, Download, Clock } from 'lucide-react';

interface DiscoverPageProps {
  devices: Device[];
  resources: Resource[];
  favorites: number[];
  downloaded: number[];
  activeDevice?: Device;
  onDownload: (id: number) => void;
  onOpenDetail: (id: number) => void;
  onToggleFavorite: (id: number) => void;
  onApply: (id: number) => void;
  onApplyOther?: (id: number) => void;
  onAddToPlaylist: (id: number) => void;
  onOpenDownloadsModal: () => void;
}

export const DiscoverPage: React.FC<DiscoverPageProps> = ({
  devices,
  resources,
  favorites,
  downloaded,
  activeDevice,
  onDownload,
  onOpenDetail,
  onToggleFavorite,
  onApply,
  onApplyOther,
  onAddToPlaylist,
  onOpenDownloadsModal,
}) => {
  const [browseRot, setBrowseRot] = useState<number>(0);
  const [filterMode, setFilterMode] = useState<'fit' | 'adjust' | 'all'>('fit');
  const [type, setType] = useState('全部');
  const [cat1, setCat1] = useState('全部');
  const [cat2, setCat2] = useState('全部');
  const [sortMode, setSortMode] = useState<'hot' | 'new'>('hot');
  const [query, setQuery] = useState('');

  const refD = activeDevice
    ? { ...activeDevice, rot: browseRot }
    : { w: 1920, h: 462, rot: browseRot };

  const pool = resources.filter((r) => r.online);

  const filtered = pool
    .filter((r) => {
      if (type !== '全部' && r.type !== type) return false;
      if (cat1 !== '全部' && r.cat && r.cat[0] !== cat1) return false;
      if (cat2 !== '全部' && r.cat && r.cat[1] !== cat2) return false;
      if (query && !r.name.toLowerCase().includes(query.toLowerCase())) return false;

      const f = fit(r, refD);
      if (filterMode === 'fit') {
        return f.loss !== null && f.loss <= 0.05;
      }
      if (filterMode === 'adjust') {
        return f.loss === null || f.loss > 0.05;
      }
      return true;
    })
    .sort((a, b) => {
      if (sortMode === 'hot') {
        return (b.hot ?? 0) - (a.hot ?? 0);
      }
      return (b.added ?? b.id) - (a.added ?? a.id);
    });

  const catTree = type !== '全部' && CATS[type] ? CATS[type] : null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            发现适合你的精彩
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            按设备规格挑选在线内容，下载后永久保留在本地资源库。
          </p>
        </div>
        <button
          onClick={onOpenDownloadsModal}
          className="px-3.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors flex items-center gap-1.5"
        >
          <Download className="w-3.5 h-3.5 text-slate-400" />
          下载记录 · {downloaded.length}
        </button>
      </div>

      {/* Hero Banner */}
      <div className="bg-gradient-to-r from-[#162a48] to-[#315567] rounded-2xl p-6 text-white flex flex-col sm:flex-row items-center justify-between gap-6 shadow-xs">
        <div>
          <span className="px-2.5 py-1 rounded-md text-[11px] font-semibold bg-white/20 text-white backdrop-blur-xs">
            本周精选 · 宽屏与竖屏
          </span>
          <h2 className="text-2xl font-bold mt-2.5 tracking-tight">
            给你的副屏设备，换一种心情
          </h2>
          <p className="text-xs text-slate-200/90 mt-1.5 max-w-lg leading-relaxed">
            极简数字时钟、超宽画卷壁纸与流光视频，专为 MiraBox D92 & D69 调优呈现。
          </p>
        </div>
        <div className="text-5xl font-light tracking-widest text-white/90 select-none font-mono">
          10:24
        </div>
      </div>

      {/* Filters Panel */}
      <div className="bg-white rounded-2xl border border-slate-200 p-4 space-y-3.5 shadow-2xs">
        {/* Row 1: Target Device Direction & Search */}
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2 bg-slate-50 px-3 py-1.5 rounded-lg border border-slate-200 text-xs">
            <span className="text-slate-400">适配设备：</span>
            <strong className="text-slate-800">
              {activeDevice ? activeDevice.name : '标准 D92'}
            </strong>
            <span className="text-slate-400">({sizeText(dims(refD))})</span>
          </div>

          {/* Orientation Filter Switcher */}
          <div className="flex items-center bg-slate-100 p-0.5 rounded-lg border border-slate-200/60">
            <button
              onClick={() => setBrowseRot(0)}
              className={`px-3 py-1 text-xs font-medium rounded-md transition-all ${
                browseRot === 0
                  ? 'bg-white text-[#4268ef] shadow-xs font-semibold'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              横屏素材
            </button>
            <button
              onClick={() => setBrowseRot(90)}
              className={`px-3 py-1 text-xs font-medium rounded-md transition-all ${
                browseRot === 90
                  ? 'bg-white text-[#4268ef] shadow-xs font-semibold'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              竖屏素材
            </button>
          </div>

          <select
            value={filterMode}
            onChange={(e) => setFilterMode(e.target.value as any)}
            className="px-3 py-1.5 text-xs rounded-lg border border-slate-200 bg-white text-slate-700 focus:outline-[#4268ef]"
          >
            <option value="fit">适合当前规格 (误差≤5%)</option>
            <option value="adjust">需要裁剪或调整</option>
            <option value="all">查看全部尺寸</option>
          </select>

          <div className="relative flex-1 min-w-[180px]">
            <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2" />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="搜索在线壁纸、主题或视频..."
              className="w-full pl-8 pr-3 py-1.5 text-xs rounded-lg border border-slate-200 bg-slate-50 focus:bg-white focus:outline-[#4268ef]"
            />
          </div>
        </div>

        {/* Row 2: Type Filters and Sort Mode */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-slate-100 text-xs">
          <div className="flex items-center gap-1.5 flex-wrap">
            <span className="text-slate-400 mr-1">类型：</span>
            {['全部', '壁纸', '主题', '时钟', '视频'].map((t) => (
              <button
                key={t}
                onClick={() => {
                  setType(t);
                  setCat1('全部');
                  setCat2('全部');
                }}
                className={`px-2.5 py-1 rounded-lg font-medium transition-colors ${
                  type === t
                    ? 'bg-[#edf2ff] text-[#4268ef] font-semibold'
                    : 'text-slate-600 hover:bg-slate-100'
                }`}
              >
                {t}
              </button>
            ))}
          </div>

          <div className="flex items-center gap-3">
            <span className="text-slate-400">{filtered.length} 项在线内容</span>
            <select
              value={sortMode}
              onChange={(e) => setSortMode(e.target.value as any)}
              className="px-2.5 py-1 rounded-lg border border-slate-200 bg-white text-slate-700 text-xs focus:outline-[#4268ef]"
            >
              <option value="hot">热门推荐</option>
              <option value="new">新上架</option>
            </select>
          </div>
        </div>

        {/* Row 3: Subcategories if available */}
        {catTree && (
          <div className="pt-2 border-t border-slate-100 space-y-2 text-xs">
            <div className="flex items-center gap-1.5 flex-wrap">
              <span className="text-slate-400 mr-1">一级分类：</span>
              <button
                onClick={() => {
                  setCat1('全部');
                  setCat2('全部');
                }}
                className={`px-2 py-0.5 rounded text-[11px] font-medium ${
                  cat1 === '全部'
                    ? 'bg-[#4268ef] text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                }`}
              >
                全部
              </button>
              {Object.keys(catTree).map((c) => (
                <button
                  key={c}
                  onClick={() => {
                    setCat1(c);
                    setCat2('全部');
                  }}
                  className={`px-2 py-0.5 rounded text-[11px] font-medium ${
                    cat1 === c
                      ? 'bg-[#4268ef] text-white'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                  }`}
                >
                  {c}
                </button>
              ))}
            </div>

            {cat1 !== '全部' && catTree[cat1]?.length > 0 && (
              <div className="flex items-center gap-1.5 flex-wrap pl-6">
                <span className="text-slate-400 mr-1">二级分类：</span>
                <button
                  onClick={() => setCat2('全部')}
                  className={`px-2 py-0.5 rounded text-[11px] font-medium ${
                    cat2 === '全部'
                      ? 'bg-[#4268ef] text-white'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                  }`}
                >
                  全部
                </button>
                {catTree[cat1].map((sc) => (
                  <button
                    key={sc}
                    onClick={() => setCat2(sc)}
                    className={`px-2 py-0.5 rounded text-[11px] font-medium ${
                      cat2 === sc
                        ? 'bg-[#4268ef] text-white'
                        : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                    }`}
                  >
                    {sc}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Resource Grid */}
      {filtered.length === 0 ? (
        <div className="py-16 text-center border border-dashed border-slate-300 rounded-2xl bg-white text-slate-500">
          <h3 className="text-sm font-semibold text-slate-800">
            未找到符合条件的在线内容
          </h3>
          <p className="text-xs text-slate-400 mt-1">
            建议切换为横屏/竖屏，或勾选“查看全部尺寸”。
          </p>
          <button
            onClick={() => {
              setType('全部');
              setCat1('全部');
              setCat2('全部');
              setQuery('');
              setFilterMode('all');
            }}
            className="mt-4 px-3 py-1.5 text-xs rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700"
          >
            重置所有筛选
          </button>
        </div>
      ) : (
        <div
          className={`grid gap-4 ${
            browseRot % 180 !== 0
              ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-6'
              : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4'
          }`}
        >
          {filtered.map((r) => (
            <ResourceCard
              key={r.id}
              resource={r}
              refDevice={refD as Device}
              isFavorite={favorites.includes(r.id)}
              hasMultipleDevices={devices.length > 1}
              onToggleFavorite={onToggleFavorite}
              onOpenDetail={onOpenDetail}
              onApply={onApply}
              onApplyOther={onApplyOther}
              onAddToPlaylist={onAddToPlaylist}
              onDownload={onDownload}
            />
          ))}
        </div>
      )}
    </div>
  );
};
