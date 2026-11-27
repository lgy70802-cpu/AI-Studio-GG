import React, { useState } from 'react';
import { Device, Resource } from '../types';
import { dims, sizeText } from '../data';
import { ScreenBox } from '../components/ScreenBox';
import {
  Monitor,
  RefreshCw,
  Plus,
  Zap,
  RotateCw,
  Sliders,
  ChevronDown,
  Sparkles,
  ListPlus,
  Square,
  Play,
  Save,
} from 'lucide-react';

interface DevicesPageProps {
  devices: Device[];
  resources: Resource[];
  detailDeviceId: string;
  onSelectDetailDevice: (id: string) => void;
  onOpenAddDeviceWizard: () => void;
  onSaveDevice: (id: string, newName: string, newRot: number) => void;
  onIdentifyDevice: (id: string) => void;
  onToggleOnline: (id: string) => void;
  onToggleOutput: (id: string) => void;
  onGoLibraryWithTarget: (deviceId: string) => void;
  onGoPlaylists: () => void;
  onShowToast: (msg: string) => void;
}

export const DevicesPage: React.FC<DevicesPageProps> = ({
  devices,
  resources,
  detailDeviceId,
  onSelectDetailDevice,
  onOpenAddDeviceWizard,
  onSaveDevice,
  onIdentifyDevice,
  onToggleOnline,
  onToggleOutput,
  onGoLibraryWithTarget,
  onGoPlaylists,
  onShowToast,
}) => {
  const currentDev =
    devices.find((d) => d.id === detailDeviceId) || devices[0];

  const [devName, setDevName] = useState(currentDev?.name || '');
  const [rotation, setRotation] = useState<number>(currentDev?.rot ?? 0);

  // Sync state when detailDeviceId changes
  React.useEffect(() => {
    if (currentDev) {
      setDevName(currentDev.name);
      setRotation(currentDev.rot);
    }
  }, [currentDev?.id, currentDev?.name, currentDev?.rot]);

  if (!currentDev) {
    return (
      <div className="py-16 text-center text-slate-500 bg-white rounded-2xl border border-slate-200">
        <h2 className="text-base font-bold text-slate-900 mb-2">尚未连接任何设备</h2>
        <p className="text-xs text-slate-400 mb-4">
          通过 USB 连接 MiraBox D92 / D69 后，系统将在此自动枚举。
        </p>
        <button
          onClick={onOpenAddDeviceWizard}
          className="px-4 py-2 text-xs font-semibold rounded-lg bg-[#4268ef] text-white shadow-xs"
        >
          ＋ 连接新设备
        </button>
      </div>
    );
  }

  const currentRes =
    resources.find((r) => r.id === currentDev.content) || {
      id: 0,
      name: '未选择内容',
      type: '壁纸' as const,
      style: 'ink',
      sizes: [],
      local: false,
    };

  const handleSave = () => {
    onSaveDevice(currentDev.id, devName, rotation);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            设备管理与设置
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            每台设备拥有独立稳定的物理身份、名称与显示方向，互不干扰。
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => onShowToast('演示设备列表已刷新')}
            className="px-3.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors flex items-center gap-1.5"
          >
            <RefreshCw className="w-3.5 h-3.5 text-slate-400" />
            刷新状态
          </button>
          <button
            onClick={onOpenAddDeviceWizard}
            className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
          >
            <Plus className="w-3.5 h-3.5" />
            连接新设备
          </button>
        </div>
      </div>

      {/* Main Split Grid */}
      <div className="grid grid-cols-1 md:grid-cols-12 gap-6 items-start">
        {/* Left Devices List */}
        <div className="md:col-span-4 space-y-2">
          {devices.map((d) => {
            const isSelected = d.id === currentDev.id;
            const ds = dims(d);

            return (
              <div
                key={d.id}
                onClick={() => onSelectDetailDevice(d.id)}
                className={`p-3.5 rounded-xl border cursor-pointer transition-all flex items-center justify-between ${
                  isSelected
                    ? 'border-[#4268ef] bg-[#edf2ff]/40 ring-1 ring-[#4268ef]'
                    : 'border-slate-200 bg-white hover:border-slate-300'
                }`}
              >
                <div>
                  <strong className="text-xs font-bold text-slate-900 block">
                    {d.name}
                  </strong>
                  <span className="text-[11px] text-slate-400 mt-0.5 block">
                    {d.model} · {sizeText(ds)} · {d.rot % 180 !== 0 ? '竖屏' : '横屏'}
                  </span>
                  <span className="text-[10px] text-slate-400 font-mono mt-0.5 block">
                    SN: {d.serial}
                  </span>
                </div>

                <span
                  className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${
                    d.online
                      ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                      : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  {d.online ? '已连接' : '未连接'}
                </span>
              </div>
            );
          })}
        </div>

        {/* Right Detail Panel */}
        <div className="md:col-span-8 space-y-4">
          <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-5 shadow-2xs">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div>
                <h2 className="text-lg font-bold text-slate-900">
                  {currentDev.name}
                </h2>
                <span className="text-xs text-slate-400 font-mono">
                  硬件序列号：{currentDev.serial}
                </span>
              </div>

              <span
                className={`text-xs px-2.5 py-1 rounded-full font-medium ${
                  currentDev.online
                    ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                    : 'bg-amber-50 text-amber-700 border border-amber-200'
                }`}
              >
                ● {currentDev.online ? 'USB 连接正常' : '设备处于离线状态'}
              </span>
            </div>

            {/* Screen Bezel Preview */}
            <div
              className={`bg-slate-100 rounded-xl p-4 flex items-center justify-center min-h-[160px] ${
                currentDev.flash ? 'device-flashing' : ''
              }`}
            >
              <ScreenBox
                resource={currentRes}
                device={currentDev}
                mode={currentDev.fit || 'contain'}
                className="max-h-[160px] max-w-full flash-target shadow-lg"
              />
            </div>

            {/* Form Grid */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
              <div>
                <label className="block text-slate-600 font-medium mb-1">
                  设备名称
                </label>
                <input
                  value={devName}
                  maxLength={30}
                  onChange={(e) => setDevName(e.target.value)}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg focus:outline-[#4268ef]"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">
                  显示旋转方向
                </label>
                <select
                  value={rotation}
                  onChange={(e) => setRotation(Number(e.target.value))}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg bg-white focus:outline-[#4268ef]"
                >
                  <option value={0}>横屏 · 0° (标准超宽)</option>
                  <option value={90}>竖屏 · 90° (立式副屏)</option>
                  <option value={180}>横屏翻转 · 180° (倒装吊顶)</option>
                  <option value={270}>竖屏翻转 · 270° (反向立式)</option>
                </select>
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">
                  物理输出分辨率
                </label>
                <input
                  disabled
                  value={`${currentDev.w} × ${currentDev.h} 像素`}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg bg-slate-50 text-slate-500 font-mono"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">
                  当前传输连接通道
                </label>
                <input
                  disabled
                  value={`USB 2.0/3.0 · ${currentDev.model} 专属协议`}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg bg-slate-50 text-slate-500"
                />
              </div>
            </div>

            {/* Actions Bar */}
            <div className="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-100 text-xs">
              <div className="flex items-center gap-2">
                <button
                  disabled={!currentDev.online}
                  onClick={() => onIdentifyDevice(currentDev.id)}
                  className="px-3 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-medium flex items-center gap-1.5"
                >
                  <Zap className="w-3.5 h-3.5 text-amber-500" />
                  让此屏幕闪烁识别
                </button>

                <button
                  onClick={() => onToggleOnline(currentDev.id)}
                  className="px-3 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 font-medium"
                >
                  模拟{currentDev.online ? '拔出断开' : '插回重连'}
                </button>
              </div>

              <button
                onClick={handleSave}
                className="px-4 py-1.5 rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white font-semibold flex items-center gap-1.5 shadow-xs transition-colors"
              >
                <Save className="w-3.5 h-3.5" />
                保存设备参数
              </button>
            </div>
          </div>

          {/* Independent Output Management Panel */}
          <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-3 shadow-2xs">
            <h3 className="text-sm font-bold text-slate-900">独立播放控制</h3>
            <p className="text-xs text-slate-500">
              当前输出画面为：<strong>{currentRes.name}</strong>
              。针对这台屏幕的修改与切换操作绝不影响其他副屏。
            </p>

            <div className="flex items-center gap-2 pt-2">
              <button
                onClick={() => onGoLibraryWithTarget(currentDev.id)}
                className="px-3 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 text-xs font-medium flex items-center gap-1.5"
              >
                <Sparkles className="w-3.5 h-3.5 text-[#4268ef]" />
                更换内容
              </button>

              <button
                onClick={onGoPlaylists}
                className="px-3 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 text-xs font-medium flex items-center gap-1.5"
              >
                <ListPlus className="w-3.5 h-3.5" />
                设置轮播列表
              </button>

              <button
                disabled={!currentDev.online}
                onClick={() => onToggleOutput(currentDev.id)}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors ${
                  currentDev.running
                    ? 'border border-rose-200 bg-rose-50 hover:bg-rose-100 text-rose-700'
                    : 'bg-[#4268ef] hover:bg-[#3458db] text-white'
                }`}
              >
                {currentDev.running ? (
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

          {/* Advanced Hardware Performance Settings */}
          <details className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs group">
            <summary className="font-bold text-xs text-slate-900 cursor-pointer flex items-center justify-between select-none">
              <span>高级设置 · 输出性能与物理屏关联组</span>
              <ChevronDown className="w-4 h-4 text-slate-400 group-open:rotate-180 transition-transform" />
            </summary>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4 pt-4 border-t border-slate-100 text-xs">
              <div>
                <label className="block text-slate-600 font-medium mb-1">
                  视频编码输出策略
                </label>
                <select className="w-full px-3 py-2 border border-slate-200 rounded-lg bg-white">
                  <option>画质优先 · 4:4:4 色彩高保真</option>
                  <option>流畅优先 · 低延迟 60FPS 运动渲染</option>
                </select>
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">
                  多物理屏联动分组标识
                </label>
                <input
                  placeholder="例如：主工作台副屏组"
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg"
                />
              </div>
            </div>

            <p className="text-[11px] text-slate-400 mt-3">
              实际帧率由目标硬件 GPU 解码性能与 USB 总线带宽动态协商，此处预设调度策略。
            </p>
          </details>
        </div>
      </div>
    </div>
  );
};
