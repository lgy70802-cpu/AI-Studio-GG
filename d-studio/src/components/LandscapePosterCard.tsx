import React from 'react';
import { Check, Download, Edit3, Eye, Plus, Sliders, Star, Trash2 } from 'lucide-react';
import { DeviceInfo, ThemeItem } from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';

interface LandscapePosterCardProps {
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
  subtitleExtra?: string;
}

export const LandscapePosterCard: React.FC<LandscapePosterCardProps> = ({
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
  subtitleExtra,
}) => {
  const aspectRatio = `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`;
  const deviceShortName = activeDevice.name.split('·')[0].trim();

  return (
    <div
      style={{ aspectRatio }}
      className={`group relative w-full overflow-hidden rounded-[18px] bg-transparent shadow-[0_8px_24px_rgba(30,20,80,0.08)] transition-all duration-200 hover:-translate-y-0.5 hover:shadow-[0_14px_32px_rgba(94,69,248,0.16)] ${
        isCurrent ? 'ring-[2.5px] ring-[#5E45F8] ring-offset-2 ring-offset-[#F7F7FC]' : ''
      }`}
    >
      {/* Full-Bleed Clickable Artwork */}
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
        />
      </button>

      {/* Hover / Focus Overlay: Title, Metadata, Menu Controls & Apply/Current Status */}
      <div className="pointer-events-none absolute inset-0 flex flex-col justify-between bg-gradient-to-t from-[#12092E]/88 via-[#12092E]/55 to-[#12092E]/75 p-2.5 opacity-0 backdrop-blur-[2px] transition-opacity duration-150 group-hover:opacity-100 group-focus-within:opacity-100">
        {/* Top Row: Title + Action Menu */}
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0 flex-1">
            <div className="truncate text-[13px] font-bold leading-tight text-white drop-shadow-sm">
              {item.displayName}
            </div>
          </div>

          {(onPreview || onRename || onEditInStudio || onDelete || onAcquire || onTogglePlaylist || onToggleFavorite) && (
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
                  className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white backdrop-blur-md transition-colors hover:bg-[#5E45F8]"
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
                  className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white backdrop-blur-md transition-colors hover:bg-white/30"
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
                  className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white backdrop-blur-md transition-colors hover:bg-white/30"
                >
                  <Edit3 className="h-3 w-3" />
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
                  className={`flex h-6 items-center gap-1 rounded-lg px-2 text-[11px] font-semibold backdrop-blur-md transition-colors whitespace-nowrap ${
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

              {onTogglePlaylist && (
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    onTogglePlaylist(item.id);
                  }}
                  title={item.inPlaylist ? '移出轮播' : '加入轮播'}
                  className={`flex h-6 items-center gap-1 rounded-lg px-2 text-[11px] font-semibold backdrop-blur-md transition-colors whitespace-nowrap ${
                    item.inPlaylist
                      ? 'bg-[#5E45F8] text-white'
                      : 'bg-white/20 text-white hover:bg-white/30'
                  }`}
                >
                  {item.inPlaylist ? <Check className="h-3 w-3" /> : <Plus className="h-3 w-3" />}
                  <span>轮播</span>
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
                  className={`flex h-6 w-6 items-center justify-center rounded-lg backdrop-blur-md transition-colors ${
                    item.isFavorite
                      ? 'bg-amber-400/25 text-amber-300'
                      : 'bg-white/20 text-white/90 hover:text-amber-300'
                  }`}
                >
                  <Star className="h-3 w-3" fill={item.isFavorite ? 'currentColor' : 'none'} />
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
                  className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/20 text-white/90 backdrop-blur-md transition-colors hover:bg-red-500/80 hover:text-white"
                >
                  <Trash2 className="h-3 w-3" />
                </button>
              )}
            </div>
          )}
        </div>

        {/* Bottom Row: Unboxed Metadata + "当前正在输出" / "应用到 D92 实机" */}
        <div className="flex items-center justify-between gap-2">
          <div className="min-w-0 truncate text-[11px] font-medium text-white/80">
            {item.category} · <span className="font-mono-tabular">{activeDevice.resolution}</span>
            {subtitleExtra ? ` · ${subtitleExtra}` : ''}
          </div>

          <button
            type="button"
            onClick={() => onApply(item)}
            className={`pointer-events-auto shrink-0 rounded-full px-3 py-1 text-[11px] font-bold shadow-md transition-transform duration-150 hover:scale-[1.03] whitespace-nowrap ${
              isCurrent
                ? 'bg-[#5E45F8] text-white'
                : 'bg-white/95 text-[#1A172B] hover:bg-white'
            }`}
          >
            {isCurrent ? '当前正在输出' : `应用到 ${deviceShortName}`}
          </button>
        </div>
      </div>
    </div>
  );
};
