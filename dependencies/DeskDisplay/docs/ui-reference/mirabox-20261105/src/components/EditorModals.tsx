import React, { useState } from 'react';
import { SPECS, specName, sizeText } from '../data';
import { X, Plus } from 'lucide-react';

export const NewProjectModal: React.FC<{
  onClose: () => void;
  onCreate: (name: string, size: [number, number]) => void;
}> = ({ onClose, onCreate }) => {
  const [name, setName] = useState('我的新主题');
  const [sizeStr, setSizeStr] = useState('1920,462');

  const handleCreate = () => {
    if (!name.trim()) return;
    const [w, h] = sizeStr.split(',').map(Number);
    onCreate(name.trim(), [w, h]);
  };

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold text-slate-900">新建屏幕主题</h2>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              主题名称
            </label>
            <input
              value={name}
              maxLength={40}
              onChange={(e) => setName(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-lg focus:outline-[#4268ef]"
              placeholder="例如：极简数字日历"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              初始设计画布规格
            </label>
            <select
              value={sizeStr}
              onChange={(e) => setSizeStr(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-lg bg-white focus:outline-[#4268ef]"
            >
              {SPECS.map((s) => (
                <option key={`${s[0]}_${s[1]}`} value={`${s[0]},${s[1]}`}>
                  {specName(s)} · {sizeText(s)}
                </option>
              ))}
            </select>
            <p className="text-[11px] text-slate-400 mt-1">
              创建后可随时增加横竖屏其他尺寸的关联布局。
            </p>
          </div>
        </div>

        <div className="flex items-center justify-end gap-2 mt-6 pt-3 border-t border-slate-100">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-medium rounded-lg text-slate-600 hover:bg-slate-100 transition-colors"
          >
            取消
          </button>
          <button
            onClick={handleCreate}
            disabled={!name.trim()}
            className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] disabled:bg-slate-300 text-white shadow-sm transition-colors"
          >
            创建并进入设计
          </button>
        </div>
      </div>
    </div>
  );
};

export const AddLayoutModal: React.FC<{
  existingSizes: [number, number][];
  onClose: () => void;
  onAdd: (size: [number, number]) => void;
}> = ({ existingSizes, onClose, onAdd }) => {
  const availableSpecs = SPECS.filter(
    (s) => !existingSizes.some((e) => e[0] === s[0] && e[1] === s[1])
  );

  const [selectedSizeStr, setSelectedSizeStr] = useState<string>(() =>
    availableSpecs.length > 0 ? `${availableSpecs[0][0]},${availableSpecs[0][1]}` : ''
  );

  const handleAdd = () => {
    if (!selectedSizeStr) return;
    const [w, h] = selectedSizeStr.split(',').map(Number);
    onAdd([w, h]);
  };

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold text-slate-900">新增显示画布规格</h2>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {availableSpecs.length === 0 ? (
          <p className="text-xs text-slate-500 py-4 text-center">
            当前主题已支持全部预设尺寸（横竖屏 D92 与 D69）。
          </p>
        ) : (
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                选择要补充的目标画布尺寸
              </label>
              <select
                value={selectedSizeStr}
                onChange={(e) => setSelectedSizeStr(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-200 rounded-lg bg-white focus:outline-[#4268ef]"
              >
                {availableSpecs.map((s) => (
                  <option key={`${s[0]}_${s[1]}`} value={`${s[0]},${s[1]}`}>
                    {specName(s)} · {sizeText(s)}
                  </option>
                ))}
              </select>
              <p className="text-[11px] text-slate-400 mt-1.5">
                系统将自动复制已有布局并等比缩放元素，方便您快速微调位置。
              </p>
            </div>
          </div>
        )}

        <div className="flex items-center justify-end gap-2 mt-6 pt-3 border-t border-slate-100">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-medium rounded-lg text-slate-600 hover:bg-slate-100 transition-colors"
          >
            取消
          </button>
          {availableSpecs.length > 0 && (
            <button
              onClick={handleAdd}
              className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-sm transition-colors"
            >
              新增此布局
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
