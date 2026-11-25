import React, { useState } from 'react';
import { Resource, Device, Playlist, RecycleEntry } from '../types';
import { fit, dims, sizeText, MOODS } from '../data';
import { ScreenBox } from './ScreenBox';
import {
  X,
  Star,
  ListPlus,
  Trash2,
  Sparkles,
  Layers,
  Edit3,
  Eye,
  Check,
  AlertTriangle,
  RotateCcw,
  Copy,
  Plus,
} from 'lucide-react';

// --- Resource Detail Modal ---
export const ResourceDetailModal: React.FC<{
  resource: Resource;
  devices: Device[];
  isFavorite: boolean;
  onClose: () => void;
  onToggleFavorite: (id: number) => void;
  onAddToPlaylist: (id: number) => void;
  onDelete?: (id: number) => void;
  onUseAsBackground?: (id: number) => void;
  onOpenEditor?: (id: number) => void;
  onPreview: (id: number) => void;
  onApply: (id: number) => void;
  onApplyOther?: (id: number) => void;
}> = ({
  resource,
  devices,
  isFavorite,
  onClose,
  onToggleFavorite,
  onAddToPlaylist,
  onDelete,
  onUseAsBackground,
  onOpenEditor,
  onPreview,
  onApply,
  onApplyOther,
}) => {
  const activeDevice = devices[0];
  const activeFit = activeDevice ? fit(resource, activeDevice) : null;
  const isThemeOrClock = resource.type === '主题' || resource.type === '时钟';

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto p-6 shadow-2xl border border-slate-200">
        <div className="flex items-start justify-between gap-4 mb-4">
          <div>
            <h2 className="text-xl font-bold text-slate-900">{resource.name}</h2>
            <p className="text-xs text-slate-500 mt-1">
              {resource.type} ·{' '}
              {resource.sizes.map((s) => sizeText(s)).join(' / ') || '尺寸未知'}
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Large Aspect Ratio Preview */}
        <div className="bg-slate-100 rounded-xl p-4 flex items-center justify-center min-h-[180px] mb-6">
          <ScreenBox
            resource={resource}
            device={activeDevice}
            mode="contain"
            className="max-h-[220px] max-w-full"
          />
        </div>

        {/* Device Compatibility Status */}
        <div className="mb-6">
          <h3 className="text-sm font-semibold text-slate-800 mb-3">
            在已连接设备上的适配表现
          </h3>
          {devices.length === 0 ? (
            <p className="text-xs text-slate-500 bg-slate-50 p-3 rounded-lg border border-slate-200">
              暂未检测到已连接的 MiraBox 设备，连接后可实时预览该屏幕效果。
            </p>
          ) : (
            <div className="divide-y divide-slate-100 border border-slate-200 rounded-xl overflow-hidden">
              {devices.map((d) => {
                const f = fit(resource, d);
                return (
                  <div
                    key={d.id}
                    className="p-3 flex items-center justify-between text-xs hover:bg-slate-50/80 transition-colors"
                  >
                    <div>
                      <span className="font-semibold text-slate-900">{d.name}</span>
                      <span className="ml-2 text-slate-400">
                        {d.model} · {sizeText(dims(d))} ·{' '}
                        {d.rot % 180 !== 0 ? '竖屏' : '横屏'}
                      </span>
                    </div>
                    <span
                      className={`px-2 py-0.5 rounded text-[11px] font-medium ${
                        f.kind === 'ok'
                          ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                          : f.kind === 'blue'
                          ? 'bg-blue-50 text-blue-700 border border-blue-200'
                          : 'bg-amber-50 text-amber-700 border border-amber-200'
                      }`}
                    >
                      {f.text}
                    </span>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Actions Footer */}
        <div className="flex flex-wrap items-center gap-2 pt-4 border-t border-slate-100">
          <button
            onClick={() => onToggleFavorite(resource.id)}
            className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 flex items-center gap-1.5 transition-colors"
          >
            <Star
              className={`w-3.5 h-3.5 ${
                isFavorite ? 'fill-amber-400 text-amber-500' : 'text-slate-400'
              }`}
            />
            {isFavorite ? '已收藏' : '收藏'}
          </button>

          <button
            onClick={() => {
              onClose();
              onAddToPlaylist(resource.id);
            }}
            className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 flex items-center gap-1.5 transition-colors"
          >
            <ListPlus className="w-3.5 h-3.5" />
            加入轮播列表
          </button>

          {resource.local && onDelete && (
            <button
              onClick={() => {
                onClose();
                onDelete(resource.id);
              }}
              className="px-3 py-1.5 text-xs font-medium rounded-lg border border-rose-200 hover:bg-rose-50 text-rose-600 flex items-center gap-1.5 transition-colors"
            >
              <Trash2 className="w-3.5 h-3.5" />
              删除
            </button>
          )}

          {resource.local && (resource.type === '壁纸' || resource.type === '视频') && onUseAsBackground && (
            <button
              onClick={() => {
                onClose();
                onUseAsBackground(resource.id);
              }}
              className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 flex items-center gap-1.5 transition-colors"
            >
              <Layers className="w-3.5 h-3.5" />
              用作主题背景
            </button>
          )}

          {isThemeOrClock && resource.local && onOpenEditor && (
            <button
              onClick={() => {
                onClose();
                onOpenEditor(resource.id);
              }}
              className="px-3 py-1.5 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 flex items-center gap-1.5 transition-colors"
            >
              <Edit3 className="w-3.5 h-3.5" />
              编辑布局
            </button>
          )}

          <div className="grow" />

          {resource.local ? (
            <>
              <button
                onClick={() => onPreview(resource.id)}
                className="px-3.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-800 transition-colors"
              >
                临时预览 (15s)
              </button>
              <button
                onClick={() => {
                  onClose();
                  onApply(resource.id);
                }}
                className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white transition-colors flex items-center gap-1.5 shadow-sm"
              >
                <Sparkles className="w-3.5 h-3.5" />
                {devices.length === 1 ? '应用到设备' : '选择设备并使用'}
              </button>
            </>
          ) : (
            <button
              onClick={() => {
                onClose();
                onApply(resource.id);
              }}
              className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white transition-colors shadow-sm"
            >
              下载并使用
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

// --- Apply Modal (Batch / Multi-Device) ---
export const ApplyModal: React.FC<{
  resource: Resource;
  devices: Device[];
  presetDeviceId?: string;
  onClose: () => void;
  onConfirm: (deviceIds: string[], modes: Record<string, 'contain' | 'cover'>) => void;
}> = ({ resource, devices, presetDeviceId, onClose, onConfirm }) => {
  const [selectedIds, setSelectedIds] = useState<string[]>(() => {
    if (presetDeviceId && devices.some((d) => d.id === presetDeviceId && d.online)) {
      return [presetDeviceId];
    }
    const onlineFirst = devices.filter((d) => d.online).map((d) => d.id);
    return onlineFirst.length ? [onlineFirst[0]] : [];
  });

  const [modes, setModes] = useState<Record<string, 'contain' | 'cover'>>(() => {
    const init: Record<string, 'contain' | 'cover'> = {};
    devices.forEach((d) => {
      init[d.id] = 'contain';
    });
    return init;
  });

  const [hasConfirmedWarning, setHasConfirmedWarning] = useState(false);

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const handleApply = () => {
    if (selectedIds.length === 0) return;
    const heavyLossDevices = selectedIds.filter((id) => {
      const dev = devices.find((d) => d.id === id);
      if (!dev) return false;
      const f = fit(resource, dev);
      return f.loss !== null && f.loss > 0.05;
    });

    if (heavyLossDevices.length > 0 && !hasConfirmedWarning) {
      setHasConfirmedWarning(true);
      return;
    }

    onConfirm(selectedIds, modes);
  };

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-xl w-full max-h-[90vh] overflow-y-auto p-6 shadow-2xl border border-slate-200">
        <div className="flex items-start justify-between gap-4 mb-4">
          <div>
            <h2 className="text-xl font-bold text-slate-900">
              应用「{resource.name}」
            </h2>
            <p className="text-xs text-slate-500 mt-1">
              每台设备分别选择适配方式，未勾选的设备将保持原有内容不变。
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="space-y-3 mb-6">
          {devices.map((d) => {
            const isChecked = selectedIds.includes(d.id);
            const f = fit(resource, d);
            const mode = modes[d.id] || 'contain';

            return (
              <div
                key={d.id}
                className={`p-3.5 rounded-xl border transition-all ${
                  isChecked
                    ? 'border-[#4268ef] bg-[#edf2ff]/30 ring-1 ring-[#4268ef]'
                    : 'border-slate-200 bg-white opacity-85'
                }`}
              >
                <div className="flex items-center gap-3">
                  <input
                    type="checkbox"
                    checked={isChecked}
                    disabled={!d.online}
                    onChange={() => toggleSelect(d.id)}
                    className="w-4 h-4 rounded text-[#4268ef] focus:ring-[#4268ef] cursor-pointer"
                  />
                  <div className="grow min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-sm text-slate-900">
                        {d.name}
                      </span>
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 font-medium">
                        {d.model}
                      </span>
                      <span
                        className={`text-[10px] px-1.5 py-0.5 rounded font-medium ${
                          f.kind === 'ok'
                            ? 'bg-emerald-50 text-emerald-700'
                            : f.kind === 'blue'
                            ? 'bg-blue-50 text-blue-700'
                            : 'bg-amber-50 text-amber-700'
                        }`}
                      >
                        {f.text}
                      </span>
                    </div>
                    <div className="text-xs text-slate-400 mt-0.5">
                      {sizeText(dims(d))} · {d.online ? '已连接' : '离线，不可应用'}
                    </div>
                  </div>

                  {/* Thumbnail Preview */}
                  <div className="w-24 h-12 bg-slate-100 rounded border border-slate-200 overflow-hidden flex items-center justify-center p-0.5 shrink-0">
                    <ScreenBox
                      resource={resource}
                      device={d}
                      mode={mode}
                      className="w-full h-full"
                    />
                  </div>
                </div>

                {isChecked && (
                  <div className="mt-3 pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
                    <span className="text-slate-500">适配画面方式：</span>
                    <select
                      value={mode}
                      onChange={(e) =>
                        setModes((prev) => ({
                          ...prev,
                          [d.id]: e.target.value as 'contain' | 'cover',
                        }))
                      }
                      className="px-2 py-1 text-xs border border-slate-200 rounded-lg bg-white text-slate-800 focus:outline-[#4268ef]"
                    >
                      <option value="contain">完整显示 · 保留全部内容 (黑边填充)</option>
                      {(resource.type === '壁纸' || resource.type === '视频') && (
                        <option value="cover">裁切铺满 · 居中裁剪 (充满整个屏幕)</option>
                      )}
                    </select>
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {hasConfirmedWarning && (
          <div className="mb-4 p-3 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0 text-amber-600" />
            <span>
              所选设备与该资源宽高比存在明显留边差异，再次点击下方按钮即可确认使用。
            </span>
          </div>
        )}

        <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-medium rounded-lg text-slate-600 hover:bg-slate-100 transition-colors"
          >
            取消
          </button>
          <button
            disabled={selectedIds.length === 0}
            onClick={handleApply}
            className={`px-4 py-2 text-xs font-semibold rounded-lg text-white transition-colors shadow-sm ${
              selectedIds.length === 0
                ? 'bg-slate-300 cursor-not-allowed'
                : hasConfirmedWarning
                ? 'bg-amber-600 hover:bg-amber-700'
                : 'bg-[#4268ef] hover:bg-[#3458db]'
            }`}
          >
            {hasConfirmedWarning ? '仍然使用' : '应用到所选设备'}
          </button>
        </div>
      </div>
    </div>
  );
};

// --- Orientation Confirm Modal ---
export const OrientationConfirmModal: React.FC<{
  device: Device;
  targetRot: number;
  currentResource: Resource;
  suggestedResource?: Resource;
  onClose: () => void;
  onConfirmOnly: () => void;
  onConfirmWithReplacement: (replacementId: number) => void;
}> = ({
  device,
  targetRot,
  currentResource,
  suggestedResource,
  onClose,
  onConfirmOnly,
  onConfirmWithReplacement,
}) => {
  const isTargetVertical = targetRot % 180 !== 0;
  const targetLabel = isTargetVertical ? '竖屏' : '横屏';

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
        <h2 className="text-lg font-bold text-slate-900">
          切换「{device.name}」为{targetLabel}？
        </h2>
        <p className="text-xs text-slate-500 mt-2 leading-relaxed">
          当前显示的内容「{currentResource.name}」与{targetLabel}
          尺寸不匹配，直接切换将停止画面发送以保护视觉效果。
        </p>

        {suggestedResource && (
          <div className="mt-4 p-3.5 rounded-xl border border-blue-100 bg-[#edf2ff]/40 flex items-center gap-3">
            <div className="w-20 h-14 bg-slate-200 rounded-lg overflow-hidden shrink-0 flex items-center justify-center">
              <ScreenBox
                resource={suggestedResource}
                device={{ ...device, rot: targetRot }}
                mode="contain"
                className="w-full h-full"
              />
            </div>
            <div>
              <span className="text-[11px] text-[#4268ef] font-medium block">
                系统推荐自动替换为：
              </span>
              <strong className="text-xs text-slate-900 block mt-0.5">
                {suggestedResource.name}
              </strong>
              <span className="text-[10px] text-slate-500">
                {suggestedResource.type} · 完美适配{targetLabel}规格
              </span>
            </div>
          </div>
        )}

        <div className="flex flex-col gap-2 mt-6 pt-3 border-t border-slate-100">
          {suggestedResource && (
            <button
              onClick={() => onConfirmWithReplacement(suggestedResource.id)}
              className="w-full py-2.5 px-3 rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white text-xs font-semibold shadow-sm transition-colors"
            >
              切换并自动换成「{suggestedResource.name}」
            </button>
          )}
          <div className="flex items-center gap-2">
            <button
              onClick={onClose}
              className="flex-1 py-2 px-3 rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 text-xs font-medium transition-colors"
            >
              取消
            </button>
            <button
              onClick={onConfirmOnly}
              className="flex-1 py-2 px-3 rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 text-xs font-medium transition-colors"
            >
              仅切换方向，停止输出
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

// --- Recycle Bin Modal ---
export const RecycleBinModal: React.FC<{
  recycleBin: RecycleEntry[];
  onClose: () => void;
  onRestore: (index: number) => void;
}> = ({ recycleBin, onClose, onRestore }) => {
  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-lg w-full max-h-[85vh] overflow-y-auto p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900">
              回收站 ({recycleBin.length})
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              可随时恢复已删除的资源与原有轮播引用关系。
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {recycleBin.length === 0 ? (
          <div className="py-12 text-center text-slate-400 text-xs border border-dashed border-slate-200 rounded-xl">
            回收站为空，没有任何已删除的项目。
          </div>
        ) : (
          <div className="divide-y divide-slate-100">
            {recycleBin.map((entry, idx) => (
              <div
                key={idx}
                className="py-3 flex items-center justify-between gap-3 text-xs"
              >
                <div>
                  <strong className="text-slate-900 block font-medium">
                    {entry.resource.name}
                  </strong>
                  <span className="text-slate-400 text-[11px]">
                    {entry.resource.type} · 曾关联 {entry.refs.length} 个轮播列表
                  </span>
                </div>
                <button
                  onClick={() => onRestore(idx)}
                  className="px-3 py-1.5 rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white text-xs font-medium flex items-center gap-1 transition-colors shadow-xs"
                >
                  <RotateCcw className="w-3.5 h-3.5" />
                  恢复
                </button>
              </div>
            ))}
          </div>
        )}

        <div className="mt-6 pt-3 border-t border-slate-100 flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-medium transition-colors"
          >
            关闭
          </button>
        </div>
      </div>
    </div>
  );
};

// --- Export Plan Modal ---
export const ExportPlanModal: React.FC<{
  data: any;
  onClose: () => void;
  onCopySuccess: () => void;
}> = ({ data, onClose, onCopySuccess }) => {
  const jsonStr = JSON.stringify(data, null, 2);

  const handleCopy = () => {
    navigator.clipboard.writeText(jsonStr).then(() => {
      onCopySuccess();
    });
  };

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-lg font-bold text-slate-900">导出规划与配置数据</h2>
            <p className="text-xs text-slate-500 mt-0.5">
              导出所有设备、资源元数据及轮播配置的完整 JSON 快照。
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <textarea
          readOnly
          value={jsonStr}
          className="w-full h-72 font-mono text-xs bg-slate-50 border border-slate-200 rounded-xl p-3 text-slate-800 focus:outline-none resize-none"
        />

        <div className="flex items-center justify-end gap-3 mt-4 pt-3 border-t border-slate-100">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-medium rounded-lg text-slate-600 hover:bg-slate-100 transition-colors"
          >
            关闭
          </button>
          <button
            onClick={handleCopy}
            className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white flex items-center gap-1.5 shadow-sm transition-colors"
          >
            <Copy className="w-3.5 h-3.5" />
            复制 JSON 到剪贴板
          </button>
        </div>
      </div>
    </div>
  );
};
