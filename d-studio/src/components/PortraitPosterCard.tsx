import React from 'react';
import { Check, Download, Edit3, Eye, Plus, Sliders, Star, Trash2 } from 'lucide-react';
import { DeviceInfo, ThemeItem } from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';

interface PortraitPosterCardProps {
  item: ThemeItem;
  activeDevice: DeviceInfo;
  isCurrent: boolean;
  onApply: (item: ThemeItem) => void;
  onToggleFavorite?: (id: string) => void;
  onTogglePlaylist?: (id: string) => void;
  onPreview?: (item: ThemeItem) => void;
  onRename?: (item: ThemeItem) => void;
  onEditInStudio?: (item: ThemeItem) => void;
  onDelete?: (item: ThemeItem) => void;
  onAcquire?: (item: ThemeItem) => void;
  queueIndex?: number;
  onRemoveFromPlaylist?: (id: string) => void;
  subtitleExtra?: string;
}

export const PortraitPosterCard: React.FC<PortraitPosterCardProps> = ({
  item,
  activeDevice,
  isCurrent,
  onApply,
  onToggleFavorite,
  onTogglePlaylist,
  onPreview,
  onRename,
  onEditInStudio,
  onDelete,
  onAcquire,
  queueIndex,
  onRemoveFromPlaylist,
  subtitleExtra,
}) => {
  const aspectRatio = `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`;
  const deviceShortName = activeDevice.name.split('·')[0].trim();

  return (
    <div
      style={{ aspectRatio }}
      className={`group relative w-full overflow-hidden rounded-[20px] bg-transparent shadow-[0_12px_28px_rgba(30,20,80,0.09)] transition-all duration-200 hover:-translate-y-1 hover:shadow-[0_18px_36px_rgba(94,69,248,0.18)] ${
        isCurrent ? 'ring-[2.5px] ring-[#5E45F8] ring-offset-2 ring-offset-[#F7F7FC]' : ''
      }`}
    >
      {/* Clickable Full-Bleed Vertical Screen Artwork */}
      <button
        type="button"
        onClick={() => onApply(item)}
        title={`点击应用「${item.displayName}」到 ${activeDevice.name}`}
        className="relative block h-full w-full text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
      >
        <ThemeArtwork
          theme={item}
          className="h-full w-full"
          showClock={true}
          compact={true}
          portrait={true}
        />
      </button>

      {/* Queue Index Badge (visible when in Playlist view) */}
      {queueIndex !== undefined && (
        <span className="pointer-events-none absolute right-2.5 top-2.5 z-10 flex h-5 min-w-5 items-center justify-center rounded-full bg-[#5E45F8] px-1.5 font-mono-tabular text-[10px] font-bold text-white shadow">
          {queueIndex}
        </span>
      )}

      {/* Hover / Focus Overlay: Title, Metadata, Apply/Active Status & Menu Controls */}
      <div className="pointer-events-none absolute inset-0 flex flex-col justify-between bg-gradient-to-b from-[#12092E]/85 via-[#12092E]/35 to-[#12092E]/88 p-2.5 opacity-0 backdrop-blur-[1.5px] transition-opacity duration-150 group-hover:opacity-100 group-focus-within:opacity-100">
        {/* Top Title + Metadata + Quick Preview/Rename */}
        <div>
          <div className="flex items-start justify-between gap-1.5">
            <div className="line-clamp-2 min-w-0 flex-1 text-[13px] font-bold leading-snug text-white drop-shadow-sm">
              {item.displayName}
            </div>
            {(onPreview || onRename || onEditInStudio || onDelete) && (
              <div className="pointer-events-auto flex items-center gap-1 shrink-0">
                {onEditInStudio && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onEditInStudio(item);
                    }}
                    title="在主题编辑器打开"
                    aria-label="在主题编辑器打开"
                    className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white backdrop-blur-md hover:bg-[#5E45F8]"
                  >
                    <Sliders className="h-3 w-3" />
                  </button>
                )}
                {onPreview && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onPreview(item);
                    }}
                    title="大图预览"
                    aria-label="大图预览"
                    className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white backdrop-blur-md hover:bg-white/30"
                  >
                    <Eye className="h-3 w-3" />
                  </button>
                )}
                {onRename && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onRename(item);
                    }}
                    title="重命名"
                    aria-label="重命名"
                    className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white backdrop-blur-md hover:bg-white/30"
                  >
                    <Edit3 className="h-3 w-3" />
                  </button>
                )}
                {onDelete && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onDelete(item);
                    }}
                    title="移出资源库"
                    aria-label="移出资源库"
                    className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white backdrop-blur-md hover:bg-red-500/80"
                  >
                    <Trash2 className="h-3 w-3" />
                  </button>
                )}
              </div>
            )}
          </div>
          <div className="mt-0.5 truncate text-[11px] font-medium text-white/80">
            {item.category === '动态时钟' ? '时钟' : item.category}
            {subtitleExtra ? ` · ${subtitleExtra}` : ''}
          </div>
        </div>

        {/* Center Status / Apply Pill */}
        <div className="flex justify-center">
          <button
            type="button"
            onClick={() => onApply(item)}
            className={`pointer-events-auto rounded-full px-3 py-1 text-[11px] font-bold shadow-md transition-transform duration-150 hover:scale-[1.03] whitespace-nowrap ${
              isCurrent
                ? 'bg-[#5E45F8] text-white'
                : 'bg-white/95 text-[#1A172B] hover:bg-white'
            }`}
          >
            {isCurrent ? '当前内容' : `应用到 ${deviceShortName}`}
          </button>
        </div>

        {/* Bottom Menu Actions */}
        <div className="pointer-events-auto flex items-center justify-between gap-1">
          {onTogglePlaylist && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                onTogglePlaylist(item.id);
              }}
              title={item.inPlaylist ? '移出轮播' : '加入轮播'}
              className={`flex h-7 items-center gap-1 rounded-lg px-2 text-[11px] font-semibold backdrop-blur-md transition-colors whitespace-nowrap ${
                item.inPlaylist
                  ? 'bg-[#5E45F8] text-white'
                  : 'bg-white/20 text-white hover:bg-white/30'
              }`}
            >
              {item.inPlaylist ? <Check className="h-3 w-3" /> : <Plus className="h-3 w-3" />}
              <span>轮播</span>
            </button>
          )}

          {onAcquire && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                onAcquire(item);
              }}
              title={item.inLibrary !== false ? '已在资源库' : '下载到资源库'}
              className={`flex h-7 items-center gap-1 rounded-lg px-2 text-[11px] font-semibold backdrop-blur-md transition-colors whitespace-nowrap ${
                item.inLibrary !== false
                  ? 'bg-emerald-500/25 text-emerald-200'
                  : 'bg-white/20 text-white hover:bg-white/30'
              }`}
            >
              {item.inLibrary !== false ? (
                <Check className="h-3 w-3" />
              ) : (
                <Download className="h-3 w-3" />
              )}
              <span>{item.inLibrary !== false ? '已入库' : '获取'}</span>
            </button>
          )}

          {onRemoveFromPlaylist && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                onRemoveFromPlaylist(item.id);
              }}
              className="flex h-7 items-center gap-1 rounded-lg bg-white/20 px-2 text-[11px] font-semibold text-white backdrop-blur-md transition-colors hover:bg-red-500/80 whitespace-nowrap"
            >
              <Trash2 className="h-3 w-3" />
              <span>移出</span>
            </button>
          )}

          {onToggleFavorite && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                onToggleFavorite(item.id);
              }}
              aria-label={item.isFavorite ? '取消收藏' : '收藏'}
              className={`ml-auto flex h-7 w-7 items-center justify-center rounded-lg backdrop-blur-md transition-colors ${
                item.isFavorite
                  ? 'bg-amber-400/25 text-amber-300'
                  : 'bg-white/20 text-white/90 hover:text-amber-300'
              }`}
            >
              <Star className="h-3.5 w-3.5" fill={item.isFavorite ? 'currentColor' : 'none'} />
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
