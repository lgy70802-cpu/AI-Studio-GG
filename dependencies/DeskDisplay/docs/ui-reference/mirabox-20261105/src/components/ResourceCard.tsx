import React from 'react';
import { Resource, Device } from '../types';
import { fit, dims } from '../data';
import { ScreenBox } from './ScreenBox';
import { Star, Eye, Edit3, Trash2, ListPlus, Download, Check, Sparkles } from 'lucide-react';

interface ResourceCardProps {
  resource: Resource;
  refDevice?: Device;
  isFavorite: boolean;
  selMode?: boolean;
  isSelected?: boolean;
  cardClickMode?: 'apply' | 'detail';
  onToggleFavorite: (id: number) => void;
  onSelect?: (id: number) => void;
  onOpenDetail: (id: number) => void;
  onOpenEditor?: (id: number) => void;
  onApply: (id: number) => void;
  onApplyOther?: (id: number) => void;
  onAddToPlaylist: (id: number) => void;
  onDownload?: (id: number) => void;
  onDelete?: (id: number) => void;
  hasMultipleDevices?: boolean;
}

export const ResourceCard: React.FC<ResourceCardProps> = ({
  resource,
  refDevice,
  isFavorite,
  selMode = false,
  isSelected = false,
  cardClickMode = 'detail',
  onToggleFavorite,
  onSelect,
  onOpenDetail,
  onOpenEditor,
  onApply,
  onApplyOther,
  onAddToPlaylist,
  onDownload,
  onDelete,
  hasMultipleDevices = false,
}) => {
  const d = refDevice || { w: 1920, h: 462, rot: 0 };
  const ds = dims(d);
  const isVertical = ds[0] < ds[1];
  const fitResult = fit(resource, d);

  const isTheme = resource.type === '主题' || resource.type === '时钟';
  const typeText =
    resource.type +
    (isTheme && resource.sizes.length > 1
      ? ` · ${resource.sizes.length} 布局`
      : '');

  const handleClick = (e: React.MouseEvent) => {
    if (selMode) {
      onSelect?.(resource.id);
      return;
    }
    if (cardClickMode === 'apply') {
      onApply(resource.id);
    } else {
      onOpenDetail(resource.id);
    }
  };

  return (
    <article
      className={`group relative rounded-xl overflow-hidden bg-white border border-[#e5eaf2] shadow-sm hover:shadow-md transition-all duration-200 ${
        isSelected ? 'ring-2 ring-[#4268ef] border-[#4268ef]' : ''
      }`}
    >
      <div
        className="relative w-full cursor-pointer bg-[#e9edf3] overflow-hidden"
        style={{ aspectRatio: `${ds[0]} / ${ds[1]}` }}
        onClick={handleClick}
        title={cardClickMode === 'apply' ? '点击直接应用' : '点击查看详情'}
      >
        <ScreenBox
          resource={resource}
          device={d}
          mode="contain"
          className="w-full h-full rounded-none"
        />

        {/* Top Gradient Overlay Info */}
        <div className="absolute inset-x-0 top-0 p-2.5 pb-5 bg-gradient-to-b from-black/75 to-transparent text-white flex items-center justify-between gap-2 pointer-events-none z-10">
          <span className="font-semibold text-xs tracking-tight truncate drop-shadow-sm">
            {resource.name}
          </span>
          <span className="text-[11px] opacity-90 shrink-0 font-medium drop-shadow-sm">
            {typeText}
          </span>
        </div>

        {/* Fit Badge (Only visible when not ideal fit) */}
        {fitResult.kind !== 'ok' && (
          <span
            className={`absolute left-2 bottom-2 z-10 px-1.5 py-0.5 rounded text-[10px] font-medium transition-opacity duration-150 ${
              fitResult.kind === 'blue'
                ? 'bg-[#edf1ff] text-[#4268ef] border border-[#cbd6ff]'
                : fitResult.kind === 'warn'
                ? 'bg-[#fff4df] text-[#a16b16] border border-[#f5deb3]'
                : 'bg-slate-200 text-slate-700'
            } group-hover:opacity-0`}
          >
            {fitResult.text}
          </span>
        )}

        {/* Multi-select indicator */}
        {selMode && (
          <div
            className={`absolute left-2.5 bottom-2.5 z-20 w-5 h-5 rounded-md border flex items-center justify-center transition-colors ${
              isSelected
                ? 'bg-[#4268ef] border-[#4268ef] text-white'
                : 'bg-white/90 border-slate-300 text-transparent'
            }`}
          >
            <Check className="w-3.5 h-3.5 stroke-[3]" />
          </div>
        )}

        {/* Action Bar on Hover */}
        {!selMode && (
          <div
            className="absolute inset-x-0 bottom-0 p-2.5 pt-7 bg-gradient-to-t from-[#0a142bd9] via-[#0a142b99] to-transparent opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity duration-200 flex flex-wrap items-center justify-center gap-1.5 z-20"
            onClick={(e) => e.stopPropagation()}
          >
            {resource.local ? (
              <>
                <button
                  type="button"
                  onClick={() => onApply(resource.id)}
                  className="px-2.5 py-1 text-xs font-semibold rounded bg-[#4268ef] hover:bg-[#3458db] text-white shadow transition-colors flex items-center gap-1"
                >
                  <Sparkles className="w-3 h-3" />
                  应用
                </button>
                {hasMultipleDevices && onApplyOther && (
                  <button
                    type="button"
                    onClick={() => onApplyOther(resource.id)}
                    className="px-2 py-1 text-xs font-medium rounded bg-white hover:bg-slate-100 text-slate-800 transition-colors"
                    title="选择其他设备或批量应用"
                  >
                    其他设备…
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => onAddToPlaylist(resource.id)}
                  className="px-2 py-1 text-xs font-medium rounded bg-white hover:bg-slate-100 text-slate-800 transition-colors flex items-center gap-1"
                >
                  <ListPlus className="w-3 h-3" />
                  ＋轮播
                </button>
                {isTheme && onOpenEditor ? (
                  <button
                    type="button"
                    onClick={() => onOpenEditor(resource.id)}
                    className="px-2 py-1 text-xs font-medium rounded bg-white hover:bg-slate-100 text-slate-800 transition-colors flex items-center gap-1"
                  >
                    <Edit3 className="w-3 h-3" />
                    编辑
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => onOpenDetail(resource.id)}
                    className="px-2 py-1 text-xs font-medium rounded bg-white hover:bg-slate-100 text-slate-800 transition-colors flex items-center gap-1"
                  >
                    <Eye className="w-3 h-3" />
                    详情
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => onToggleFavorite(resource.id)}
                  className="p-1 rounded bg-white hover:bg-slate-100 text-slate-700 transition-colors"
                  title={isFavorite ? '取消收藏' : '收藏'}
                >
                  <Star
                    className={`w-3.5 h-3.5 ${
                      isFavorite ? 'fill-amber-400 text-amber-500' : 'text-slate-600'
                    }`}
                  />
                </button>
                {onDelete && (
                  <button
                    type="button"
                    onClick={() => onDelete(resource.id)}
                    className="p-1 rounded bg-white/90 hover:bg-rose-50 text-rose-600 transition-colors"
                    title="删除或移入回收站"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                )}
              </>
            ) : (
              <>
                <button
                  type="button"
                  onClick={() => onDownload?.(resource.id)}
                  className="px-3 py-1 text-xs font-semibold rounded bg-[#4268ef] hover:bg-[#3458db] text-white shadow transition-colors flex items-center gap-1"
                >
                  <Download className="w-3 h-3" />
                  下载到资源库
                </button>
                <button
                  type="button"
                  onClick={() => onOpenDetail(resource.id)}
                  className="px-2 py-1 text-xs font-medium rounded bg-white hover:bg-slate-100 text-slate-800 transition-colors flex items-center gap-1"
                >
                  <Eye className="w-3 h-3" />
                  查看
                </button>
                <button
                  type="button"
                  onClick={() => onToggleFavorite(resource.id)}
                  className="p-1 rounded bg-white hover:bg-slate-100 text-slate-700 transition-colors"
                >
                  <Star
                    className={`w-3.5 h-3.5 ${
                      isFavorite ? 'fill-amber-400 text-amber-500' : 'text-slate-600'
                    }`}
                  />
                </button>
              </>
            )}
          </div>
        )}
      </div>
    </article>
  );
};
