import React, { useMemo, useRef, useState } from 'react';
import {
  CalendarPlus,
  Check,
  CheckSquare,
  Edit3,
  FolderOpen,
  History,
  ImagePlus,
  ListPlus,
  Sparkles,
  Star,
  Trash2,
  TrendingUp,
  Upload,
  X,
} from 'lucide-react';
import {
  CarouselPlaylist,
  ContentCategory,
  DeviceInfo,
  ThemeItem,
  ThemeSource,
} from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';
import { LandscapePosterCard } from './LandscapePosterCard';
import { PortraitPosterCard } from './PortraitPosterCard';

interface LibraryManagerViewProps {
  themes: ThemeItem[];
  activeTheme: ThemeItem;
  activeDevice: DeviceInfo;
  categoryFilter: ContentCategory | '全部';
  onCategoryFilterChange: (cat: ContentCategory | '全部') => void;
  scopedPlaylists: CarouselPlaylist[];
  activePlaylistId: string;
  onApplyTheme: (theme: ThemeItem) => void;
  onToggleFavorite: (id: string) => void;
  onTogglePlaylist: (id: string) => void;
  onImportLocalThemes: (items: ThemeItem[]) => void;
  onRenameTheme: (id: string, nextName: string) => void;
  onRemoveFromLibrary: (ids: string[]) => void;
  onBatchFavorite: (ids: string[]) => void;
  onBatchAddToTargetPlaylist: (playlistId: string | null, themeIds: string[]) => void;
  onEditInStudio?: (theme: ThemeItem) => void;
  onNavigateOnline: () => void;
  onNotify: (msg: string) => void;
}

const SOURCE_LABELS: Record<ThemeSource, string> = {
  official: '在线已下载',
  custom: '创作自制',
  local: '本地导入',
};

function readFileAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : '');
    reader.onerror = () => reject(new Error('文件读取失败'));
    reader.readAsDataURL(file);
  });
}

export const LibraryManagerView: React.FC<LibraryManagerViewProps> = ({
  themes,
  activeTheme,
  activeDevice,
  categoryFilter,
  onCategoryFilterChange,
  scopedPlaylists,
  activePlaylistId,
  onApplyTheme,
  onToggleFavorite,
  onTogglePlaylist,
  onImportLocalThemes,
  onRenameTheme,
  onRemoveFromLibrary,
  onBatchFavorite,
  onBatchAddToTargetPlaylist,
  onEditInStudio,
  onNavigateOnline,
  onNotify,
}) => {
  const [sourceFilter, setSourceFilter] = useState<'all' | ThemeSource>('all');
  const [onlyFavs, setOnlyFavs] = useState(false);
  const [librarySort, setLibrarySort] = useState<'frequent' | 'newest-added' | 'most-used'>('frequent');

  const [batchMode, setBatchMode] = useState(false);
  const [batchSelectedIds, setBatchSelectedIds] = useState<string[]>([]);
  const [targetPlaylistId, setTargetPlaylistId] = useState<string>(activePlaylistId || '');

  const [isDraggingFiles, setIsDraggingFiles] = useState(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const [previewItem, setPreviewItem] = useState<ThemeItem | null>(null);
  const [renameTarget, setRenameTarget] = useState<ThemeItem | null>(null);
  const [renameDraft, setRenameDraft] = useState('');

  const isPortrait = activeDevice.orientation === 'portrait';
  const previewAspectRatio = isPortrait
    ? `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`
    : `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`;
  const deviceShortName = activeDevice.name.split('·')[0].trim();

  const libraryItems = useMemo(
    () => themes.filter((t) => t.inLibrary !== false),
    [themes]
  );

  const stats = useMemo(() => {
    let favCount = 0;
    let localCount = 0;
    let customCount = 0;
    for (const item of libraryItems) {
      if (item.isFavorite) favCount += 1;
      if (item.source === 'local') localCount += 1;
      if (item.source === 'custom') customCount += 1;
    }
    return { total: libraryItems.length, favCount, localCount, customCount };
  }, [libraryItems]);

  const filtered = useMemo(() => {
    return libraryItems
      .filter((item) => {
        if (categoryFilter !== '全部' && item.category !== categoryFilter) return false;
        if (sourceFilter !== 'all' && (item.source ?? 'official') !== sourceFilter) return false;
        if (onlyFavs && !item.isFavorite) return false;
        return true;
      })
      .sort((a, b) => {
        if (librarySort === 'newest-added') {
          return (b.addedAt ?? 0) - (a.addedAt ?? 0);
        }
        if (librarySort === 'most-used') {
          return (b.useCount ?? 0) - (a.useCount ?? 0);
        }
        const recentDiff = Number(Boolean(b.isRecent)) - Number(Boolean(a.isRecent));
        if (recentDiff !== 0) return recentDiff;
        return (b.lastUsedAt ?? 0) - (a.lastUsedAt ?? 0);
      });
  }, [libraryItems, categoryFilter, sourceFilter, onlyFavs, librarySort]);

  const getCardSubtitleExtra = (item: ThemeItem) => {
    const srcTag = SOURCE_LABELS[item.source ?? 'official'];
    if (librarySort === 'most-used') {
      return `${srcTag} · 已用 ${item.useCount} 次`;
    }
    if (librarySort === 'newest-added') {
      return `${srcTag} · ${item.addedLabel}`;
    }
    return `${srcTag} · ${item.useCount} 次`;
  };

  const handleFilesSelected = async (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;
    const imageFiles = Array.from(fileList).filter((f) => f.type.startsWith('image/'));
    if (imageFiles.length === 0) {
      onNotify('请选择 PNG、JPG、WebP 等图片格式文件');
      return;
    }

    try {
      const now = Date.now();
      const created: ThemeItem[] = [];
      for (let i = 0; i < imageFiles.length; i += 1) {
        const file = imageFiles[i];
        const dataUrl = await readFileAsDataUrl(file);
        if (!dataUrl) continue;
        const cleanTitle = file.name.replace(/\.[^/.]+$/, '') || `本地壁纸_${i + 1}`;
        created.push({
          id: `local-${now}-${i}`,
          name: cleanTitle,
          displayName: cleanTitle,
          category: '壁纸',
          resolution: activeDevice.resolution,
          orientation: activeDevice.orientation,
          imageUrl: dataUrl,
          clockStyle: 'none',
          author: '本地导入',
          source: 'local',
          inLibrary: true,
          downloads: '本地导入',
          downloadCount: 0,
          publishedAt: now + i,
          releasedLabel: '刚刚导入',
          addedAt: now + i,
          addedLabel: '刚刚导入',
          lastUsedAt: now + i,
          useCount: 0,
          durationMinutes: 30,
          inPlaylist: false,
          isFavorite: false,
          isRecent: true,
          accentHex: '#5E45F8',
        });
      }
      if (created.length > 0) {
        onImportLocalThemes(created);
      }
    } catch {
      onNotify('导入本地图片失败，请重试');
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const resolvedBatchTargetPlaylistId = useMemo(() => {
    if (scopedPlaylists.length === 0) return null;
    if (targetPlaylistId && scopedPlaylists.some((p) => p.id === targetPlaylistId)) {
      return targetPlaylistId;
    }
    if (activePlaylistId && scopedPlaylists.some((p) => p.id === activePlaylistId)) {
      return activePlaylistId;
    }
    return scopedPlaylists[0].id;
  }, [scopedPlaylists, targetPlaylistId, activePlaylistId]);

  const hasFilters =
    sourceFilter !== 'all' || categoryFilter !== '全部' || onlyFavs;

  const livePreviewItem = previewItem
    ? themes.find((t) => t.id === previewItem.id) ?? previewItem
    : null;

  return (
    <div
      className="relative space-y-5"
      onDragOver={(e) => {
        if (e.dataTransfer.types.includes('Files')) {
          e.preventDefault();
          setIsDraggingFiles(true);
        }
      }}
      onDragLeave={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget as Node)) {
          setIsDraggingFiles(false);
        }
      }}
      onDrop={(e) => {
        if (e.dataTransfer.files?.length) {
          e.preventDefault();
          setIsDraggingFiles(false);
          void handleFilesSelected(e.dataTransfer.files);
        }
      }}
    >
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        multiple
        className="hidden"
        onChange={(e) => void handleFilesSelected(e.target.files)}
      />

      {/* Drag & Drop File Overlay */}
      {isDraggingFiles && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#140B36]/45 p-6 backdrop-blur-xs pointer-events-none">
          <div className="flex flex-col items-center rounded-[28px] border-2 border-dashed border-[#8B76FF] bg-white/95 px-10 py-8 text-center shadow-2xl">
            <Upload className="h-10 w-10 text-[#5E45F8]" />
            <div className="mt-3 text-[16px] font-bold text-[#1A172B]">
              松开即可导入本地图片到我的资源库
            </div>
            <div className="mt-1 text-[12.5px] text-[#7A7690]">
              支持 PNG、JPG、WebP，将自动适配 {deviceShortName}（{activeDevice.resolution}）
            </div>
          </div>
        </div>
      )}

      {/* Two-Tier Studio Header */}
      <div className="rounded-[24px] border border-white/80 bg-white/90 p-5 shadow-[0_12px_32px_rgba(30,20,80,0.05)] backdrop-blur-xl">
        {/* Tier 1: Title, Stats & Primary Actions */}
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="min-w-0">
            <h2 className="text-[20px] font-bold tracking-tight text-[#1A172B]">我的资源库</h2>
            <div className="mt-0.5 flex flex-wrap items-center gap-2 text-[12.5px] text-[#7A7690]">
              <span>
                共 <strong className="font-mono-tabular text-[#1A172B]">{stats.total}</strong> 项个人素材
              </span>
              <span aria-hidden="true">·</span>
              <span>已收藏 {stats.favCount} 项</span>
              {stats.localCount > 0 && (
                <>
                  <span aria-hidden="true">·</span>
                  <span>本地导入 {stats.localCount} 项</span>
                </>
              )}
              <span aria-hidden="true">·</span>
              <span className="font-mono-tabular">
                适配 {deviceShortName} ({activeDevice.resolution})
              </span>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              className="flex items-center gap-1.5 rounded-xl bg-[#5E45F8] px-4 py-2 text-[12.5px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] transition-colors hover:bg-[#4D34E6] whitespace-nowrap shrink-0"
            >
              <ImagePlus className="h-4 w-4" />
              <span>导入本地素材</span>
            </button>

            <button
              type="button"
              onClick={() => {
                setBatchMode((v) => !v);
                setBatchSelectedIds([]);
              }}
              className={`flex items-center gap-1.5 rounded-xl px-3.5 py-2 text-[12.5px] font-semibold transition-colors whitespace-nowrap shrink-0 ${
                batchMode
                  ? 'bg-[#1A172B] text-white'
                  : 'border border-[#E6E4F2] bg-[#FAF9FE] text-[#4C4763] hover:border-[#C8BDFC] hover:text-[#5E45F8]'
              }`}
            >
              <CheckSquare className="h-3.5 w-3.5" />
              <span>{batchMode ? '退出批量管理' : '批量管理'}</span>
            </button>
          </div>
        </div>

        {/* Tier 2: Source, Category, Sort & Favorites Filters */}
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-[#F1EFF8] pt-3.5">
          <div className="flex flex-wrap items-center gap-2.5">
            {/* Source Filter */}
            <div
              className="flex items-center rounded-xl bg-[#F4F3FA] p-1"
              role="group"
              aria-label="资源来源筛选"
            >
              {(
                [
                  { id: 'all', label: '全部来源' },
                  { id: 'official', label: '在线已下载' },
                  { id: 'custom', label: '创作自制' },
                  { id: 'local', label: '本地导入' },
                ] as const
              ).map((src) => (
                <button
                  key={src.id}
                  type="button"
                  onClick={() => setSourceFilter(src.id)}
                  className={`rounded-lg px-2.5 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                    sourceFilter === src.id
                      ? 'bg-white text-[#5E45F8] shadow-xs'
                      : 'text-[#6B6880] hover:text-[#1A172B]'
                  }`}
                >
                  {src.label}
                </button>
              ))}
            </div>

            {/* Category Filter */}
            <div
              className="flex items-center rounded-xl bg-[#F4F3FA] p-1"
              role="group"
              aria-label="资源分类筛选"
            >
              {(['全部', '动态时钟', '壁纸', '主题'] as const).map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => onCategoryFilterChange(cat)}
                  className={`rounded-lg px-2.5 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                    categoryFilter === cat
                      ? 'bg-white text-[#5E45F8] shadow-xs'
                      : 'text-[#6B6880] hover:text-[#1A172B]'
                  }`}
                >
                  {cat}
                </button>
              ))}
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            {/* Sort Tabs */}
            <div
              className="flex items-center rounded-xl bg-[#F4F3FA] p-1"
              role="group"
              aria-label="资源库排序"
            >
              {(
                [
                  { id: 'frequent', label: '经常', icon: History },
                  { id: 'newest-added', label: '新添加', icon: CalendarPlus },
                  { id: 'most-used', label: '最常用', icon: TrendingUp },
                ] as const
              ).map((opt) => {
                const Icon = opt.icon;
                const active = librarySort === opt.id;
                return (
                  <button
                    key={opt.id}
                    type="button"
                    onClick={() => setLibrarySort(opt.id)}
                    className={`flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                      active
                        ? 'bg-white text-[#5E45F8] shadow-xs'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    <Icon className="h-3.5 w-3.5" />
                    <span>{opt.label}</span>
                  </button>
                );
              })}
            </div>

            <button
              type="button"
              onClick={() => setOnlyFavs((v) => !v)}
              aria-pressed={onlyFavs}
              className={`flex items-center gap-1.5 rounded-xl px-3.5 py-2.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                onlyFavs
                  ? 'bg-amber-50 text-amber-600 ring-1 ring-amber-200'
                  : 'bg-[#F4F3FA] text-[#6B6880] hover:text-[#1A172B]'
              }`}
            >
              <Star className="h-3.5 w-3.5" fill={onlyFavs ? 'currentColor' : 'none'} />
              <span>仅看收藏</span>
            </button>
          </div>
        </div>
      </div>

      {/* Enhanced Batch Management Bar */}
      {batchMode && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-[20px] border border-[#D8D0FF] bg-[#F3F0FF] px-4 py-3">
          <div className="flex flex-wrap items-center gap-2.5 text-[13px] font-semibold text-[#1A172B]">
            <span>
              已选 <span className="font-mono-tabular text-[#5E45F8]">{batchSelectedIds.length}</span> 项
            </span>
            <span className="text-[#8B87A1]">· 目标轮播列表：</span>
            {scopedPlaylists.length > 0 ? (
              <select
                value={resolvedBatchTargetPlaylistId ?? ''}
                onChange={(e) => setTargetPlaylistId(e.target.value)}
                aria-label="选择目标轮播列表"
                className="rounded-xl border border-[#D4CBFC] bg-white px-2.5 py-1 text-[12px] font-semibold text-[#1A172B] focus:border-[#5E45F8] focus:outline-none"
              >
                {scopedPlaylists.map((pl) => (
                  <option key={pl.id} value={pl.id}>
                    {pl.name}（{pl.mood} · {pl.items.length} 项）
                  </option>
                ))}
              </select>
            ) : (
              <span className="text-[12px] font-medium text-[#5E45F8]">
                将自动新建「{isPortrait ? '竖屏' : '横屏'}默认轮播」
              </span>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() =>
                setBatchSelectedIds(
                  batchSelectedIds.length === filtered.length && filtered.length > 0
                    ? []
                    : filtered.map((item) => item.id)
                )
              }
              className="rounded-xl bg-white px-3 py-1.5 text-[12px] font-semibold text-[#5E45F8] hover:bg-[#FAF9FE] whitespace-nowrap"
            >
              {batchSelectedIds.length === filtered.length && filtered.length > 0
                ? '取消全选'
                : '全选当前'}
            </button>

            <button
              type="button"
              disabled={batchSelectedIds.length === 0}
              onClick={() => {
                onBatchAddToTargetPlaylist(resolvedBatchTargetPlaylistId, batchSelectedIds);
                setBatchSelectedIds([]);
                setBatchMode(false);
              }}
              className="flex items-center gap-1.5 rounded-xl bg-[#5E45F8] px-3.5 py-1.5 text-[12px] font-bold text-white shadow-xs hover:bg-[#4D34E6] disabled:opacity-40 whitespace-nowrap"
            >
              <ListPlus className="h-3.5 w-3.5" />
              <span>加入轮播 ({batchSelectedIds.length})</span>
            </button>

            <button
              type="button"
              disabled={batchSelectedIds.length === 0}
              onClick={() => {
                onBatchFavorite(batchSelectedIds);
              }}
              className="flex items-center gap-1 rounded-xl bg-white px-3 py-1.5 text-[12px] font-semibold text-amber-600 hover:bg-amber-50 disabled:opacity-40 whitespace-nowrap"
            >
              <Star className="h-3.5 w-3.5" />
              <span>批量收藏</span>
            </button>

            <button
              type="button"
              disabled={batchSelectedIds.length === 0}
              onClick={() => {
                onRemoveFromLibrary(batchSelectedIds);
                setBatchSelectedIds([]);
                setBatchMode(false);
              }}
              className="flex items-center gap-1 rounded-xl bg-white px-3 py-1.5 text-[12px] font-semibold text-red-500 hover:bg-red-50 disabled:opacity-40 whitespace-nowrap"
            >
              <Trash2 className="h-3.5 w-3.5" />
              <span>移出资源库</span>
            </button>

            <button
              type="button"
              onClick={() => {
                setBatchSelectedIds([]);
                setBatchMode(false);
              }}
              aria-label="退出批量管理"
              className="flex h-7 w-7 items-center justify-center rounded-lg text-[#6B6880] hover:bg-white"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}

      {/* Grid or Empty State */}
      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-[24px] border border-dashed border-[#DAD6F0] bg-white/85 px-6 py-16 text-center">
          <FolderOpen className="h-10 w-10 text-[#9C94C9]" />
          <h3 className="mt-3 text-[16px] font-bold text-[#1A172B]">
            {hasFilters ? '当前筛选条件下暂无匹配素材' : '我的资源库暂无内容'}
          </h3>
          <p className="mt-1 max-w-md text-[13px] text-[#7A7690]">
            {hasFilters
              ? '你可以重置筛选条件查看全部已入库内容，或者导入新的本地图片素材。'
              : '支持拖拽或点击导入电脑里的壁纸图片，也可以前往在线资源广场下载官方动态时钟与主题。'}
          </p>
          <div className="mt-5 flex flex-wrap items-center justify-center gap-2.5">
            {hasFilters && (
              <button
                type="button"
                onClick={() => {
                  setSourceFilter('all');
                  onCategoryFilterChange('全部');
                  setOnlyFavs(false);
                }}
                className="rounded-xl bg-[#F3F0FF] px-4 py-2 text-[12.5px] font-bold text-[#5E45F8] hover:bg-[#E7E1FF]"
              >
                重置全部筛选
              </button>
            )}
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              className="flex items-center gap-1.5 rounded-xl bg-[#5E45F8] px-4 py-2 text-[12.5px] font-bold text-white shadow-sm hover:bg-[#4D34E6]"
            >
              <ImagePlus className="h-4 w-4" />
              <span>导入本地图片</span>
            </button>
            <button
              type="button"
              onClick={onNavigateOnline}
              className="flex items-center gap-1.5 rounded-xl border border-[#E6E4F2] bg-white px-4 py-2 text-[12.5px] font-semibold text-[#4C4763] hover:border-[#C8BDFC] hover:text-[#5E45F8]"
            >
              <Sparkles className="h-4 w-4 text-[#5E45F8]" />
              <span>前往在线资源广场</span>
            </button>
          </div>
        </div>
      ) : batchMode ? (
        <div
          className={`grid gap-4 ${
            isPortrait
              ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-5'
              : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3'
          }`}
        >
          {filtered.map((item) => {
            const selected = batchSelectedIds.includes(item.id);
            return (
              <button
                key={item.id}
                type="button"
                onClick={() =>
                  setBatchSelectedIds((prev) =>
                    prev.includes(item.id)
                      ? prev.filter((id) => id !== item.id)
                      : [...prev, item.id]
                  )
                }
                style={{ aspectRatio: previewAspectRatio }}
                className={`group relative w-full overflow-hidden rounded-[18px] bg-[#160F30] text-left transition-all ${
                  selected
                    ? 'ring-[2.5px] ring-[#5E45F8] ring-offset-2 ring-offset-[#F7F7FC]'
                    : 'opacity-85 hover:opacity-100'
                }`}
              >
                <ThemeArtwork
                  theme={item}
                  className="h-full w-full"
                  showClock={true}
                  compact={true}
                  portrait={isPortrait}
                />
                <div className="absolute inset-0 flex flex-col justify-between bg-gradient-to-t from-[#12092E]/85 via-[#12092E]/30 to-[#12092E]/65 p-2.5">
                  <div className="flex items-start justify-between gap-2">
                    <span className="truncate text-[12.5px] font-bold text-white">
                      {item.displayName}
                    </span>
                    <span
                      className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border ${
                        selected
                          ? 'border-[#5E45F8] bg-[#5E45F8] text-white'
                          : 'border-white/75 bg-black/30 text-transparent'
                      }`}
                    >
                      <Check className="h-3 w-3" />
                    </span>
                  </div>
                  <div className="text-[11px] text-white/85">
                    {SOURCE_LABELS[item.source ?? 'official']} · {item.category}
                  </div>
                </div>
              </button>
            );
          })}
        </div>
      ) : isPortrait ? (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-5">
          {filtered.map((item) => (
            <PortraitPosterCard
              key={item.id}
              item={item}
              activeDevice={activeDevice}
              isCurrent={item.id === activeTheme.id}
              onApply={onApplyTheme}
              onToggleFavorite={onToggleFavorite}
              onTogglePlaylist={onTogglePlaylist}
              onPreview={(t) => setPreviewItem(t)}
              onEditInStudio={onEditInStudio}
              onRename={(t) => {
                setRenameTarget(t);
                setRenameDraft(t.displayName);
              }}
              onDelete={(t) => onRemoveFromLibrary([t.id])}
              subtitleExtra={getCardSubtitleExtra(item)}
            />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map((item) => (
            <LandscapePosterCard
              key={item.id}
              item={item}
              activeDevice={activeDevice}
              isCurrent={item.id === activeTheme.id}
              onApply={onApplyTheme}
              onToggleFavorite={onToggleFavorite}
              onTogglePlaylist={onTogglePlaylist}
              onPreview={(t) => setPreviewItem(t)}
              onEditInStudio={onEditInStudio}
              onRename={(t) => {
                setRenameTarget(t);
                setRenameDraft(t.displayName);
              }}
              onDelete={(t) => onRemoveFromLibrary([t.id])}
              subtitleExtra={getCardSubtitleExtra(item)}
            />
          ))}
        </div>
      )}

      {/* Full-Aspect Quick Preview Modal */}
      {livePreviewItem && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#140B36]/55 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-label="资源详情与预览"
        >
          <div className="w-full max-w-3xl overflow-hidden rounded-[26px] border border-white/60 bg-white shadow-[0_32px_90px_rgba(16,8,50,0.36)]">
            <div className="flex items-center justify-between border-b border-[#F1EFF8] px-6 py-4">
              <div className="min-w-0">
                <h3 className="truncate text-[17px] font-bold text-[#1A172B]">
                  {livePreviewItem.displayName}
                </h3>
                <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-[12px] text-[#7A7690]">
                  <span>{SOURCE_LABELS[livePreviewItem.source ?? 'official']}</span>
                  <span aria-hidden="true">·</span>
                  <span>{livePreviewItem.category}</span>
                  <span aria-hidden="true">·</span>
                  <span>作者：{livePreviewItem.author}</span>
                  <span aria-hidden="true">·</span>
                  <span className="font-mono-tabular">已用 {livePreviewItem.useCount} 次</span>
                  <span aria-hidden="true">·</span>
                  <span>{livePreviewItem.addedLabel}</span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setPreviewItem(null)}
                aria-label="关闭预览"
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[#F4F3FA] text-[#56526B] hover:bg-[#EAE8F5]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="aurora-dark-card flex justify-center p-6">
              <div
                style={{ aspectRatio: previewAspectRatio }}
                className={`overflow-hidden rounded-[18px] border border-white/20 shadow-2xl ${
                  isPortrait ? 'w-[220px] max-w-full' : 'w-full'
                }`}
              >
                <ThemeArtwork
                  theme={livePreviewItem}
                  className="h-full w-full"
                  showClock={true}
                  liveTick={true}
                  compact={isPortrait}
                  portrait={isPortrait}
                />
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 px-6 py-4">
              <div className="flex items-center gap-2">
                {onEditInStudio && (
                  <button
                    type="button"
                    onClick={() => {
                      const target = livePreviewItem;
                      setPreviewItem(null);
                      onEditInStudio(target);
                    }}
                    className="flex items-center gap-1.5 rounded-xl bg-[#EFEBFD] px-3.5 py-2 text-[12.5px] font-bold text-[#5E45F8] hover:bg-[#E5DDFD] whitespace-nowrap"
                  >
                    <Sparkles className="h-3.5 w-3.5" />
                    <span>在编辑器 V2.0 修改</span>
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => {
                    setRenameTarget(livePreviewItem);
                    setRenameDraft(livePreviewItem.displayName);
                  }}
                  className="flex items-center gap-1.5 rounded-xl bg-[#F4F3FA] px-3.5 py-2 text-[12.5px] font-semibold text-[#4C4763] hover:bg-[#EAE8F5]"
                >
                  <Edit3 className="h-3.5 w-3.5" />
                  <span>重命名</span>
                </button>
                <button
                  type="button"
                  onClick={() => onToggleFavorite(livePreviewItem.id)}
                  className={`flex items-center gap-1.5 rounded-xl px-3.5 py-2 text-[12.5px] font-semibold transition-colors ${
                    livePreviewItem.isFavorite
                      ? 'bg-amber-50 text-amber-600'
                      : 'bg-[#F4F3FA] text-[#4C4763] hover:bg-[#EAE8F5]'
                  }`}
                >
                  <Star
                    className="h-3.5 w-3.5"
                    fill={livePreviewItem.isFavorite ? 'currentColor' : 'none'}
                  />
                  <span>{livePreviewItem.isFavorite ? '已收藏' : '收藏'}</span>
                </button>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => onTogglePlaylist(livePreviewItem.id)}
                  className={`flex items-center gap-1.5 rounded-xl px-4 py-2 text-[12.5px] font-semibold transition-colors ${
                    livePreviewItem.inPlaylist
                      ? 'bg-[#EFEBFD] text-[#5E45F8]'
                      : 'bg-[#F4F3FA] text-[#4C4763] hover:bg-[#EAE8F5]'
                  }`}
                >
                  <ListPlus className="h-3.5 w-3.5" />
                  <span>{livePreviewItem.inPlaylist ? '已在轮播列表中' : '加入轮播'}</span>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    onApplyTheme(livePreviewItem);
                    setPreviewItem(null);
                  }}
                  className="rounded-xl bg-[#5E45F8] px-5 py-2 text-[12.5px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] hover:bg-[#4D34E6]"
                >
                  应用到 {deviceShortName}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Rename Modal */}
      {renameTarget && (
        <div
          className="fixed inset-0 z-[60] flex items-center justify-center bg-[#140B36]/45 p-4 backdrop-blur-xs"
          role="dialog"
          aria-modal="true"
          aria-label="重命名素材"
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const trimmed = renameDraft.trim();
              if (!trimmed) {
                onNotify('请输入有效的素材名称');
                return;
              }
              onRenameTheme(renameTarget.id, trimmed);
              setRenameTarget(null);
            }}
            className="w-full max-w-md rounded-[24px] border border-white/80 bg-white p-6 shadow-[0_24px_60px_rgba(20,10,60,0.28)]"
          >
            <h3 className="text-[16px] font-bold text-[#1A172B]">重命名资源</h3>
            <p className="mt-0.5 text-[12px] text-[#7A7690]">
              修改后将同步更新轮播列表、定时规则与当前显示名称
            </p>
            <input
              type="text"
              value={renameDraft}
              onChange={(e) => setRenameDraft(e.target.value)}
              maxLength={48}
              autoFocus
              className="mt-4 w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2.5 text-[13.5px] font-semibold text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
            />
            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setRenameTarget(null)}
                className="rounded-xl bg-[#F4F3FA] px-4 py-2 text-[12.5px] font-semibold text-[#56526B] hover:bg-[#EAE8F5]"
              >
                取消
              </button>
              <button
                type="submit"
                className="rounded-xl bg-[#5E45F8] px-5 py-2 text-[12.5px] font-bold text-white hover:bg-[#4D34E6]"
              >
                保存名称
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
