import React, { useState } from 'react';
import { Resource, Device, Playlist } from '../types';
import { fit, dims, sizeText, MOODS } from '../data';
import { ScreenBox } from './ScreenBox';
import {
  X,
  Check,
  Search,
  Sparkles,
  Monitor,
  Zap,
  ArrowRight,
  ArrowLeft,
  RotateCw,
} from 'lucide-react';

// --- Add Device Wizard (4 Steps) ---
export const AddDeviceWizardModal: React.FC<{
  existingDevices: Device[];
  resources: Resource[];
  onClose: () => void;
  onFinish: (newDevice: Device) => void;
}> = ({ existingDevices, resources, onClose, onFinish }) => {
  const [step, setStep] = useState(0);
  const [model, setModel] = useState<'D92' | 'D69'>('D92');
  const [serial] = useState(
    () => `D92-${Math.random().toString(16).slice(2, 6).toUpperCase()}`
  );
  const [name, setName] = useState('我的 D92');
  const [rot, setRot] = useState(0);
  const [selectedContent, setSelectedContent] = useState<number>(1);
  const [isFlashing, setIsFlashing] = useState(false);

  const deviceSpec: Device = {
    id: `dev_${Date.now()}`,
    name,
    model,
    w: model === 'D69' ? 1280 : 1920,
    h: model === 'D92' ? 462 : 480,
    rot,
    online: true,
    running: true,
    content: selectedContent,
    serial,
    fit: 'contain',
  };

  const currentDims = dims(deviceSpec);

  const matchingPicks = resources
    .filter(
      (r) =>
        r.local &&
        r.sizes.length > 0 &&
        fit(r, deviceSpec).loss !== null &&
        fit(r, deviceSpec).loss! <= 0.05
    )
    .slice(0, 4);

  const handleFlash = () => {
    setIsFlashing(true);
    setTimeout(() => setIsFlashing(false), 1600);
  };

  const stepsLabels = ['发现设备', '屏幕识别', '命名与方向', '初始内容'];

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between mb-3">
          <div>
            <h2 className="text-lg font-bold text-slate-900">连接新设备向导</h2>
            <p className="text-xs text-slate-500">
              第 {step + 1} 步，共 4 步 · 模拟即插即用硬件检测流程
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Step Indicator */}
        <div className="flex items-center gap-2 mb-6">
          {stepsLabels.map((lbl, idx) => (
            <div
              key={lbl}
              className={`flex-1 py-1.5 px-2 rounded-lg text-xs text-center font-medium transition-colors ${
                idx === step
                  ? 'bg-[#edf2ff] text-[#4268ef] font-semibold ring-1 ring-[#cbd6ff]'
                  : idx < step
                  ? 'bg-emerald-50 text-emerald-700'
                  : 'bg-slate-100 text-slate-400'
              }`}
            >
              {idx < step ? '✓ ' : `${idx + 1}. `}
              {lbl}
            </div>
          ))}
        </div>

        {/* Step 1: Discover */}
        {step === 0 && (
          <div className="space-y-4">
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200">
              <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2.5">
                  <Monitor className="w-5 h-5 text-[#4268ef]" />
                  <div>
                    <h3 className="text-sm font-semibold text-slate-900">
                      成功识别到 MiraBox 超宽副屏
                    </h3>
                    <p className="text-xs text-slate-500">USB 2.0/3.0 高速连接正常</p>
                  </div>
                </div>
                <span className="px-2 py-0.5 rounded text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
                  ● 已就绪
                </span>
              </div>

              <div className="grid grid-cols-2 gap-3 text-xs pt-2 border-t border-slate-200/60">
                <div>
                  <span className="text-slate-500 block mb-1">设备型号</span>
                  <select
                    value={model}
                    onChange={(e) => {
                      const m = e.target.value as 'D92' | 'D69';
                      setModel(m);
                      setName(existingDevices.length > 0 ? `${m} #${existingDevices.length + 1}` : `我的 ${m}`);
                    }}
                    className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 bg-white font-medium"
                  >
                    <option value="D92">MiraBox D92 (1920 × 462)</option>
                    <option value="D69">MiraBox D69 (1280 × 480)</option>
                  </select>
                </div>
                <div>
                  <span className="text-slate-500 block mb-1">物理序列号</span>
                  <input
                    value={serial}
                    disabled
                    className="w-full px-2.5 py-1.5 rounded-lg border border-slate-200 bg-slate-100 text-slate-600 font-mono"
                  />
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Step 2: Screen Identify Flash */}
        {step === 1 && (
          <div className="space-y-4 text-center">
            <p className="text-xs text-slate-500">
              点击下方按钮，新接入的物理屏幕将快速高亮闪烁，方便您在桌面区分多台设备。
            </p>
            <div
              className={`p-6 rounded-xl bg-slate-100 flex items-center justify-center transition-all ${
                isFlashing ? 'ring-4 ring-[#4268ef] brightness-125' : ''
              }`}
            >
              <ScreenBox
                device={{ ...deviceSpec, rot: 0 }}
                resource={resources[0]}
                className="max-h-[140px] max-w-full"
              />
            </div>
            <button
              onClick={handleFlash}
              className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-sm transition-colors inline-flex items-center gap-1.5"
            >
              <Zap className="w-3.5 h-3.5" />
              立即触发屏幕闪烁测试
            </button>
          </div>
        )}

        {/* Step 3: Naming & Orientation */}
        {step === 2 && (
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                设备自定义名称
              </label>
              <input
                value={name}
                maxLength={30}
                onChange={(e) => setName(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-200 rounded-lg focus:outline-[#4268ef]"
                placeholder="例如：桌面左侧副屏"
              />
              <div className="flex items-center gap-1.5 mt-2">
                <span className="text-[11px] text-slate-400">快速命名：</span>
                {['左侧', '右侧', '桌面', '副屏'].map((prefix) => (
                  <button
                    key={prefix}
                    type="button"
                    onClick={() => setName(`${prefix} ${model}`)}
                    className="px-2 py-0.5 rounded text-[11px] bg-slate-100 hover:bg-slate-200 text-slate-700"
                  >
                    {prefix} {model}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                屏幕摆放方向
              </label>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => setRot(0)}
                  className={`p-3 rounded-xl border text-left text-xs transition-all ${
                    rot === 0
                      ? 'border-[#4268ef] bg-[#edf2ff] text-[#4268ef] font-semibold'
                      : 'border-slate-200 hover:bg-slate-50 text-slate-700'
                  }`}
                >
                  <div className="font-medium">▭ 横屏模式</div>
                  <div className="text-[10px] text-slate-400 mt-1">
                    {model === 'D92' ? '1920 × 462' : '1280 × 480'}
                  </div>
                </button>
                <button
                  type="button"
                  onClick={() => setRot(90)}
                  className={`p-3 rounded-xl border text-left text-xs transition-all ${
                    rot === 90
                      ? 'border-[#4268ef] bg-[#edf2ff] text-[#4268ef] font-semibold'
                      : 'border-slate-200 hover:bg-slate-50 text-slate-700'
                  }`}
                >
                  <div className="font-medium">▯ 竖屏模式</div>
                  <div className="text-[10px] text-slate-400 mt-1">
                    {model === 'D92' ? '462 × 1920' : '480 × 1280'}
                  </div>
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Step 4: Pick Initial Content */}
        {step === 3 && (
          <div className="space-y-4">
            <p className="text-xs text-slate-500">
              为该屏幕挑选首屏呈现的画面内容，后续可随时更换。
            </p>
            <div className="grid grid-cols-2 gap-2.5 max-h-56 overflow-y-auto p-1">
              {matchingPicks.map((r) => {
                const isSelected = selectedContent === r.id;
                return (
                  <div
                    key={r.id}
                    onClick={() => setSelectedContent(r.id)}
                    className={`p-2.5 rounded-xl border cursor-pointer transition-all ${
                      isSelected
                        ? 'border-[#4268ef] bg-[#edf2ff] ring-1 ring-[#4268ef]'
                        : 'border-slate-200 hover:bg-slate-50'
                    }`}
                  >
                    <div className="h-14 w-full bg-slate-100 rounded flex items-center justify-center overflow-hidden mb-2">
                      <ScreenBox
                        resource={r}
                        device={deviceSpec}
                        mode="contain"
                        className="w-full h-full"
                      />
                    </div>
                    <div className="flex items-center justify-between text-xs">
                      <span className="font-semibold text-slate-900 truncate">
                        {r.name}
                      </span>
                      <span className="text-[10px] text-slate-400">{r.type}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Wizard Footer Controls */}
        <div className="flex items-center justify-between pt-5 mt-6 border-t border-slate-100">
          <button
            type="button"
            disabled={step === 0}
            onClick={() => setStep((s) => s - 1)}
            className={`px-3 py-1.5 text-xs font-medium rounded-lg text-slate-700 flex items-center gap-1 transition-colors ${
              step === 0
                ? 'opacity-30 cursor-not-allowed'
                : 'hover:bg-slate-100'
            }`}
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            上一步
          </button>

          <div className="flex items-center gap-2">
            {step === 1 && (
              <button
                type="button"
                onClick={() => setStep(2)}
                className="px-3 py-1.5 text-xs text-slate-500 hover:text-slate-800"
              >
                跳过识别
              </button>
            )}
            {step < 3 ? (
              <button
                type="button"
                onClick={() => setStep((s) => s + 1)}
                className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white flex items-center gap-1.5 shadow-sm transition-colors"
              >
                下一步
                <ArrowRight className="w-3.5 h-3.5" />
              </button>
            ) : (
              <button
                type="button"
                onClick={() => onFinish(deviceSpec)}
                className="px-4 py-2 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white flex items-center gap-1.5 shadow-sm transition-colors"
              >
                <Check className="w-4 h-4" />
                完成配置并连接
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

// --- Add Content to Playlist Picker Grid Modal ---
export const PlaylistAddPickerModal: React.FC<{
  playlist: Playlist;
  targetDevice?: Device;
  resources: Resource[];
  onClose: () => void;
  onAddItems: (resourceIds: number[]) => void;
}> = ({ playlist, targetDevice, resources, onClose, onAddItems }) => {
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [query, setQuery] = useState('');
  const [type, setType] = useState('全部');
  const [showAllSizes, setShowAllSizes] = useState(false);

  const d = targetDevice || { w: 1920, h: 462, rot: 0 };
  const ds = dims(d);

  const existingIds = playlist.items.map((it) => it.id);

  const filteredResources = resources.filter((r) => {
    if (!r.local || !r.sizes.length) return false;
    if (type !== '全部' && r.type !== type) return false;
    if (query && !r.name.toLowerCase().includes(query.toLowerCase())) return false;
    if (!showAllSizes) {
      const f = fit(r, d);
      if (f.loss === null || f.loss > 0.05) return false;
    }
    return true;
  });

  const togglePick = (id: number) => {
    if (existingIds.includes(id)) return;
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const handleConfirm = () => {
    if (selectedIds.length === 0) return;
    onAddItems(selectedIds);
  };

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-3xl w-full max-h-[90vh] overflow-hidden flex flex-col p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between mb-3 shrink-0">
          <div>
            <h2 className="text-lg font-bold text-slate-900">
              添加内容到「{playlist.name}」
            </h2>
            <p className="text-xs text-slate-500">
              按输出设备 {'name' in d ? d.name : '副屏'} ({sizeText(ds)}) 比例精准筛选，可一次多选
            </p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Filter Controls Bar */}
        <div className="flex flex-wrap items-center gap-2 mb-4 shrink-0">
          <div className="relative flex-1 min-w-[160px]">
            <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2" />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="搜索素材名称..."
              className="w-full pl-8 pr-3 py-1.5 text-xs rounded-lg border border-slate-200 bg-slate-50 focus:bg-white focus:outline-[#4268ef]"
            />
          </div>

          <div className="flex items-center gap-1 bg-slate-100 p-1 rounded-lg">
            {['全部', '壁纸', '主题', '时钟', '视频'].map((t) => (
              <button
                key={t}
                onClick={() => setType(t)}
                className={`px-2.5 py-1 text-xs rounded-md font-medium transition-colors ${
                  type === t
                    ? 'bg-white text-slate-900 shadow-xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                {t}
              </button>
            ))}
          </div>

          <label className="flex items-center gap-1.5 text-xs text-slate-600 cursor-pointer ml-1">
            <input
              type="checkbox"
              checked={showAllSizes}
              onChange={(e) => setShowAllSizes(e.target.checked)}
              className="rounded text-[#4268ef]"
            />
            显示其他尺寸
          </label>
        </div>

        {/* Grid of Choices */}
        <div className="flex-1 overflow-y-auto grid grid-cols-3 sm:grid-cols-4 gap-3 p-1">
          {filteredResources.map((r) => {
            const isAlreadyIn = existingIds.includes(r.id);
            const isPicked = selectedIds.includes(r.id);
            const f = fit(r, d);

            return (
              <div
                key={r.id}
                onClick={() => togglePick(r.id)}
                className={`relative rounded-xl border overflow-hidden cursor-pointer transition-all ${
                  isAlreadyIn
                    ? 'opacity-40 cursor-not-allowed border-slate-200'
                    : isPicked
                    ? 'border-[#4268ef] ring-2 ring-[#4268ef] shadow-sm'
                    : 'border-slate-200 hover:border-slate-400'
                }`}
              >
                <div
                  className="w-full bg-[#131c2d]"
                  style={{ aspectRatio: `${ds[0]} / ${ds[1]}` }}
                >
                  <ScreenBox
                    resource={r}
                    device={d}
                    mode="contain"
                    className="w-full h-full rounded-none"
                  />
                </div>

                <div className="p-2 bg-white flex items-center justify-between text-xs">
                  <span className="font-semibold text-slate-900 truncate">
                    {r.name}
                  </span>
                  <span className="text-[10px] text-slate-400 shrink-0">
                    {r.type}
                  </span>
                </div>

                {/* Status Badge */}
                <div className="absolute top-1.5 right-1.5">
                  {isAlreadyIn ? (
                    <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-900/80 text-white font-medium">
                      已在列表中
                    </span>
                  ) : isPicked ? (
                    <div className="w-5 h-5 rounded-full bg-[#4268ef] text-white flex items-center justify-center">
                      <Check className="w-3.5 h-3.5 stroke-[3]" />
                    </div>
                  ) : null}
                </div>
              </div>
            );
          })}
        </div>

        {/* Footer Bar */}
        <div className="flex items-center justify-between pt-4 mt-4 border-t border-slate-100 shrink-0">
          <span className="text-xs text-slate-500">
            {selectedIds.length > 0
              ? `已选 ${selectedIds.length} 项内容`
              : '点击卡片勾选，支持批量添加'}
          </span>
          <div className="flex items-center gap-2">
            <button
              onClick={onClose}
              className="px-4 py-2 text-xs font-medium rounded-lg text-slate-600 hover:bg-slate-100 transition-colors"
            >
              取消
            </button>
            <button
              disabled={selectedIds.length === 0}
              onClick={handleConfirm}
              className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] disabled:bg-slate-300 disabled:cursor-not-allowed text-white shadow-sm transition-colors"
            >
              加入所选 ({selectedIds.length})
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

// --- Fast Pick Playlist Modal (When user clicks "＋轮播" on any card) ---
export const PickPlaylistModal: React.FC<{
  resourceIds: number[];
  playlists: Playlist[];
  devices: Device[];
  resources: Resource[];
  onClose: () => void;
  onAddMany: (playlistIndex: number, ids: number[]) => void;
  onCreateAndAdd: (name: string, ids: number[]) => void;
}> = ({
  resourceIds,
  playlists,
  devices,
  resources,
  onClose,
  onAddMany,
  onCreateAndAdd,
}) => {
  const [newPlaylistName, setNewPlaylistName] = useState('');

  const targetResourceNames = resourceIds
    .map((id) => resources.find((r) => r.id === id)?.name)
    .filter(Boolean);

  const titleText =
    targetResourceNames.length === 1
      ? `将「${targetResourceNames[0]}」加入轮播列表`
      : `将 ${resourceIds.length} 项内容加入轮播列表`;

  const handleCreateNew = () => {
    if (!newPlaylistName.trim()) return;
    onCreateAndAdd(newPlaylistName.trim(), resourceIds);
  };

  return (
    <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
        <div className="flex items-center justify-between mb-3">
          <div>
            <h2 className="text-base font-bold text-slate-900">{titleText}</h2>
            <p className="text-xs text-slate-500 mt-0.5">请选择目标轮播列表：</p>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="space-y-2 max-h-60 overflow-y-auto my-4">
          {playlists.map((p, idx) => {
            const dev = devices.find((d) => d.id === p.device);
            const mood = MOODS.find((m) => m.k === p.mood) || MOODS[0];

            return (
              <button
                key={p.name + idx}
                onClick={() => onAddMany(idx, resourceIds)}
                className="w-full p-3 rounded-xl border border-slate-200 hover:border-[#4268ef] hover:bg-[#edf2ff]/30 text-left flex items-center justify-between transition-all group"
              >
                <div className="flex items-center gap-2.5">
                  <span
                    className="w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold"
                    style={{
                      backgroundColor: `${mood.c}20`,
                      color: mood.c,
                    }}
                  >
                    {mood.g}
                  </span>
                  <div>
                    <strong className="text-xs text-slate-900 block group-hover:text-[#4268ef]">
                      {p.name}
                    </strong>
                    <span className="text-[10px] text-slate-400">
                      {p.items.length} 项内容 · 输出至 {dev?.name || '未关联设备'}
                    </span>
                  </div>
                </div>
                <span className="text-xs font-semibold text-[#4268ef]">加入</span>
              </button>
            );
          })}
        </div>

        <div className="pt-3 border-t border-slate-100">
          <label className="text-xs font-medium text-slate-700 block mb-1.5">
            或快速新建一个轮播列表：
          </label>
          <div className="flex items-center gap-2">
            <input
              value={newPlaylistName}
              onChange={(e) => setNewPlaylistName(e.target.value)}
              placeholder="例如：灵感桌面、晨间时钟..."
              className="flex-1 px-3 py-2 text-xs border border-slate-200 rounded-lg focus:outline-[#4268ef]"
              onKeyDown={(e) => {
                if (e.key === 'Enter') handleCreateNew();
              }}
            />
            <button
              disabled={!newPlaylistName.trim()}
              onClick={handleCreateNew}
              className="px-3 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] disabled:bg-slate-300 disabled:cursor-not-allowed text-white shadow-xs"
            >
              新建并加入
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
