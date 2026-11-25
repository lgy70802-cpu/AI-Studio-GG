import React, { useState, useRef } from 'react';
import { Device, Resource } from '../types';
import { fit, dims, sizeText, specName, CATS } from '../data';
import { ResourceCard } from '../components/ResourceCard';
import {
  Search,
  Upload,
  Plus,
  Trash2,
  CheckSquare,
  ListPlus,
  X,
  ChevronDown,
} from 'lucide-react';

interface LibraryPageProps {
  devices: Device[];
  resources: Resource[];
  favorites: number[];
  downloaded: number[];
  recycleBinCount: number;
  activeDeviceId: string;
  onSelectTargetDevice: (id: string) => void;
  onImportImage: (file: File) => void;
  onOpenNewProject: () => void;
  onOpenRecycleBin: () => void;
  onOpenDetail: (id: number) => void;
  onOpenEditor: (id: number) => void;
  onApply: (id: number, targetDevice?: string) => void;
  onApplyOther?: (id: number) => void;
  onAddToPlaylist: (id: number) => void;
  onBatchAddToPlaylist: (ids: number[]) => void;
  onToggleFavorite: (id: number) => void;
  onDeleteResource: (id: number) => void;
}

export const LibraryPage: React.FC<LibraryPageProps> = ({
  devices,
  resources,
  favorites,
  downloaded,
  recycleBinCount,
  activeDeviceId,
  onSelectTargetDevice,
  onImportImage,
  onOpenNewProject,
  onOpenRecycleBin,
  onOpenDetail,
  onOpenEditor,
  onApply,
  onApplyOther,
  onAddToPlaylist,
  onBatchAddToPlaylist,
  onToggleFavorite,
  onDeleteResource,
}) => {
  const [tab, setTab] = useState<'全部资源' | '收藏' | '已下载' | '我的创作'>('全部资源');
  const [browseRot, setBrowseRot] = useState<number>(0);
  const [filterMode, setFilterMode] = useState<'fit' | 'adjust' | 'all'>('fit');
  const [type, setType] = useState('全部');
  const [query, setQuery] = useState('');
  const [selMode, setSelMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const isSingleDevice = devices.length === 1;
  const currentDevice = devices.find((d) => d.id === activeDeviceId) || devices[0];
  const refD = currentDevice
    ? { ...currentDevice, rot: isSingleDevice ? currentDevice.rot : browseRot }
    : { w: 1920, h: 462, rot: browseRot };

  const localPool = resources.filter((r) => r.local);

  const tabFiltered = localPool.filter((r) => {
    if (tab === '收藏' && !favorites.includes(r.id)) return false;
    if (tab === '已下载' && !downloaded.includes(r.id)) return false;
    if (tab === '我的创作' && !r.created) return false;
    if (type !== '全部' && r.type !== type) return false;
    if (query && !r.name.toLowerCase().includes(query.toLowerCase())) return false;
    return true;
  });

  const toggleSelectCard = (id: number) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      onImportImage(file);
      e.target.value = '';
    }
  };

  // Check if we should render grouped by hardware specs (when "全部设备" is chosen and multiple devices exist)
  const isGroupedView = !isSingleDevice && activeDeviceId === 'all' && devices.length > 1;

  // Grouping logic for "All Devices"
  const renderGroupedContent = () => {
    const specsMap: { key: string; ds: [number, number]; devs: Device[] }[] = [];
    devices.forEach((d) => {
      const ds = dims(d);
      const key = `${ds[0]}x${ds[1]}`;
      let g = specsMap.find((x) => x.key === key);
      if (!g) {
        g = { key, ds, devs: [] };
        specsMap.push(g);
      }
      g.devs.push(d);
    });

    const usedIds = new Set<number>();

    return (
      <div className="space-y-8">
        {specsMap.map((g) => {
          const repDev = g.devs[0];
          const matched = tabFiltered.filter((r) => {
            const f = fit(r, repDev);
            return f.loss !== null && f.loss <= 0.05;
          });
          matched.forEach((r) => usedIds.add(r.id));

          return (
            <section key={g.key} className="space-y-3">
              <div className="flex flex-wrap items-center justify-between gap-3 pb-2 border-b border-slate-200">
                <div className="flex items-center gap-2">
                  <h2 className="text-base font-bold text-slate-900">
                    {specName(g.ds)}
                  </h2>
                  <span className="text-xs text-slate-400">({sizeText(g.ds)})</span>
                  <div className="flex items-center gap-1.5 ml-2">
                    {g.devs.map((d) => (
                      <span
                        key={d.id}
                        className="px-2 py-0.5 rounded text-[11px] font-medium bg-[#edf2ff] text-[#4268ef]"
                      >
                        ▱ {d.name}
                      </span>
                    ))}
                  </div>
                </div>

                <div className="flex items-center gap-3 text-xs">
                  <span className="text-slate-400">{matched.length} 项适合</span>
                  <button
                    onClick={() => {
                      onSelectTargetDevice(repDev.id);
                      setFilterMode('fit');
                    }}
                    className="px-2.5 py-1 rounded-md border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-medium"
                  >
                    只看这组
                  </button>
                </div>
              </div>

              {matched.length === 0 ? (
                <div className="py-8 text-center text-xs text-slate-400 bg-white border border-dashed border-slate-200 rounded-xl">
                  此类屏幕暂无专属尺寸内容，可去在线资源下载或创作中心新建。
                </div>
              ) : (
                <div
                  className={`grid gap-4 ${
                    g.ds[0] < g.ds[1]
                      ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-6'
                      : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4'
                  }`}
                >
                  {matched.map((r) => (
                    <ResourceCard
                      key={r.id}
                      resource={r}
                      refDevice={repDev}
                      isFavorite={favorites.includes(r.id)}
                      selMode={selMode}
                      isSelected={selectedIds.includes(r.id)}
                      hasMultipleDevices={devices.length > 1}
                      onSelect={toggleSelectCard}
                      onToggleFavorite={onToggleFavorite}
                      onOpenDetail={onOpenDetail}
                      onOpenEditor={onOpenEditor}
                      onApply={(id) => onApply(id, repDev.id)}
                      onApplyOther={onApplyOther}
                      onAddToPlaylist={onAddToPlaylist}
                      onDelete={onDeleteResource}
                    />
                  ))}
                </div>
              )}
            </section>
          );
        })}

        {/* Collapsible Other Sizes */}
        {(() => {
          const rest = tabFiltered.filter((r) => !usedIds.has(r.id));
          return (
            <details className="p-4 rounded-xl border border-dashed border-slate-300 bg-slate-50/70 group">
              <summary className="font-semibold text-xs text-slate-700 cursor-pointer flex items-center justify-between select-none">
                <span>
                  其他尺寸素材 ({rest.length} 项 · 与现有设备规格差异较大)
                </span>
                <ChevronDown className="w-4 h-4 text-slate-400 group-open:rotate-180 transition-transform" />
              </summary>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 mt-4">
                {rest.map((r) => (
                  <ResourceCard
                    key={r.id}
                    resource={r}
                    refDevice={devices[0]}
                    isFavorite={favorites.includes(r.id)}
                    selMode={selMode}
                    isSelected={selectedIds.includes(r.id)}
                    hasMultipleDevices={devices.length > 1}
                    onSelect={toggleSelectCard}
                    onToggleFavorite={onToggleFavorite}
                    onOpenDetail={onOpenDetail}
                    onOpenEditor={onOpenEditor}
                    onApply={(id) => onApply(id, devices[0]?.id)}
                    onApplyOther={onApplyOther}
                    onAddToPlaylist={onAddToPlaylist}
                    onDelete={onDeleteResource}
                  />
                ))}
              </div>
            </details>
          );
        })()}
      </div>
    );
  };

  // Standard Flat View (Single Device or Selected Device)
  const singleFiltered = tabFiltered.filter((r) => {
    if (filterMode === 'all') return true;
    const f = fit(r, refD);
    if (filterMode === 'fit') return f.loss !== null && f.loss <= 0.05;
    if (filterMode === 'adjust') return f.loss === null || f.loss > 0.05;
    return true;
  });

  return (
    <div className="space-y-6 pb-20">
      <input
        type="file"
        ref={fileInputRef}
        onChange={handleFileInputChange}
        accept="image/*"
        className="hidden"
      />

      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            本地资源库
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            按设备规格浏览已有内容，超宽与竖屏适配度一目了然。
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={onOpenRecycleBin}
            className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors"
          >
            回收站 · {recycleBinCount}
          </button>
          <button
            onClick={() => {
              setSelMode(!selMode);
              setSelectedIds([]);
            }}
            className={`px-3 py-1.5 text-xs font-medium rounded-lg border transition-colors ${
              selMode
                ? 'bg-[#edf2ff] border-[#4268ef] text-[#4268ef] font-semibold'
                : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-700'
            }`}
          >
            {selMode ? '退出多选' : '多选'}
          </button>
          <button
            onClick={() => fileInputRef.current?.click()}
            className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors flex items-center gap-1.5"
          >
            <Upload className="w-3.5 h-3.5 text-slate-400" />
            导入图片
          </button>
          <button
            onClick={onOpenNewProject}
            className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
          >
            <Plus className="w-3.5 h-3.5" />
            新建主题
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-6 border-b border-slate-200 overflow-x-auto">
        {(['全部资源', '收藏', '已下载', '我的创作'] as const).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`pb-3 text-xs font-semibold whitespace-nowrap transition-colors relative ${
              tab === t
                ? 'text-[#4268ef]'
                : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            {t}
            {tab === t && (
              <span className="absolute inset-x-0 bottom-0 h-0.5 bg-[#4268ef] rounded-full" />
            )}
          </button>
        ))}
      </div>

      {/* Filters Bar */}
      <div className="bg-white rounded-2xl border border-slate-200 p-4 space-y-3.5 shadow-2xs">
        <div className="flex flex-wrap items-center gap-3">
          {isSingleDevice ? (
            <div className="flex items-center gap-2 bg-slate-50 px-3 py-1.5 rounded-lg border border-slate-200 text-xs">
              <span className="text-slate-400">当前设备：</span>
              <strong className="text-slate-800">{currentDevice.name}</strong>
              <span className="text-slate-400">({sizeText(dims(currentDevice))})</span>
            </div>
          ) : (
            <select
              value={activeDeviceId}
              onChange={(e) => onSelectTargetDevice(e.target.value)}
              className="px-3 py-1.5 text-xs rounded-lg border border-slate-200 bg-white text-slate-800 font-medium focus:outline-[#4268ef]"
            >
              <option value="all">全部设备 · 按规格自动分组</option>
              {devices.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name} · {d.model} · {sizeText(dims(d))}
                </option>
              ))}
            </select>
          )}

          {!isGroupedView && (
            <select
              value={filterMode}
              onChange={(e) => setFilterMode(e.target.value as any)}
              className="px-3 py-1.5 text-xs rounded-lg border border-slate-200 bg-white text-slate-700 focus:outline-[#4268ef]"
            >
              <option value="fit">适合当前设备 (误差≤5%)</option>
              <option value="adjust">需要裁剪或调整</option>
              <option value="all">全部尺寸</option>
            </select>
          )}

          <div className="relative flex-1 min-w-[180px]">
            <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2" />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="搜索本地素材名称..."
              className="w-full pl-8 pr-3 py-1.5 text-xs rounded-lg border border-slate-200 bg-slate-50 focus:bg-white focus:outline-[#4268ef]"
            />
          </div>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-slate-100 text-xs">
          <div className="flex items-center gap-1.5 flex-wrap">
            <span className="text-slate-400 mr-1">内容类型：</span>
            {['全部', '壁纸', '主题', '时钟', '视频'].map((t) => (
              <button
                key={t}
                onClick={() => setType(t)}
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

          <span className="text-slate-400">
            {isGroupedView ? tabFiltered.length : singleFiltered.length} 项本地素材
          </span>
        </div>
      </div>

      {/* Main Content Area */}
      {isGroupedView ? (
        renderGroupedContent()
      ) : singleFiltered.length === 0 ? (
        <div className="py-16 text-center border border-dashed border-slate-300 rounded-2xl bg-white text-slate-500">
          <h3 className="text-sm font-semibold text-slate-800">
            没有符合当前筛选条件的本地内容
          </h3>
          <p className="text-xs text-slate-400 mt-1">
            您可以尝试勾选“全部尺寸”，或前往在线资源下载丰富内容。
          </p>
        </div>
      ) : (
        <div
          className={`grid gap-4 ${
            dims(refD)[0] < dims(refD)[1]
              ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-6'
              : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4'
          }`}
        >
          {singleFiltered.map((r) => (
            <ResourceCard
              key={r.id}
              resource={r}
              refDevice={refD as Device}
              isFavorite={favorites.includes(r.id)}
              selMode={selMode}
              isSelected={selectedIds.includes(r.id)}
              hasMultipleDevices={devices.length > 1}
              onSelect={toggleSelectCard}
              onToggleFavorite={onToggleFavorite}
              onOpenDetail={onOpenDetail}
              onOpenEditor={onOpenEditor}
              onApply={(id) => onApply(id, currentDevice?.id)}
              onApplyOther={onApplyOther}
              onAddToPlaylist={onAddToPlaylist}
              onDelete={onDeleteResource}
            />
          ))}
        </div>
      )}

      {/* Sticky Bottom Multi-Select Bar */}
      {selMode && (
        <div className="fixed bottom-16 inset-x-0 z-40 flex items-center justify-center p-3 pointer-events-none">
          <div className="bg-slate-900/90 text-white backdrop-blur-md px-5 py-2.5 rounded-full shadow-2xl flex items-center gap-4 pointer-events-auto border border-white/10 text-xs">
            <span className="font-medium">
              已选 <strong className="text-[#9cb4ff]">{selectedIds.length}</strong> 项
            </span>
            <div className="h-4 w-px bg-white/20" />
            <button
              onClick={() => {
                setSelMode(false);
                setSelectedIds([]);
              }}
              className="text-slate-300 hover:text-white"
            >
              取消
            </button>
            <button
              disabled={selectedIds.length === 0}
              onClick={() => onBatchAddToPlaylist(selectedIds)}
              className="px-3.5 py-1.5 rounded-full bg-[#4268ef] hover:bg-[#3458db] disabled:opacity-40 font-semibold text-white transition-colors flex items-center gap-1.5"
            >
              <ListPlus className="w-3.5 h-3.5" />
              加入轮播列表
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
