import React from 'react';
import { Device, Resource, AppSettings } from '../types';
import {
  Clock,
  TrendingUp,
  Cpu,
  Shield,
  Download,
  RotateCcw,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  BookOpen,
} from 'lucide-react';

// --- Plugins Page ---
export const PluginsPage: React.FC<{
  onOpenNewProject: () => void;
  onShowToast: (msg: string) => void;
}> = ({ onOpenNewProject, onShowToast }) => {
  const pluginList = [
    {
      id: 'flip_clock',
      title: '复古相机翻页时钟',
      desc: '支持本地时间同步、拟物翻页动画与复古机械相机质感外框。',
      icon: Clock,
      bg: '#f2e9df',
      color: '#916b49',
      features: ['支持 12 / 24 小时制自由切换', '平滑 60 帧翻页物理动效'],
    },
    {
      id: 'stock_ticker',
      title: 'A 股与美股实时自选行情',
      desc: '专为副屏设计的超宽股票与外汇滚动走势组件，红绿分明。',
      icon: TrendingUp,
      bg: '#eaf3ef',
      color: '#397e65',
      features: ['显示实时涨跌幅与分时极简走势线', '代码自选 (默认 600519, 300750)'],
    },
    {
      id: 'hw_monitor',
      title: '系统硬件脉搏监视器',
      desc: '实时渲染 CPU / GPU 温度、利用率及显存占用，科技感终端风格。',
      icon: Cpu,
      bg: '#eff2fe',
      color: '#4268ef',
      features: ['超低系统资源开销', '深色赛博终端风格'],
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
          插件中心
        </h1>
        <p className="text-xs text-slate-500 mt-1">
          统一管理桌面小组件，可在主题创作中心自由组合或作为专属卡片输出。
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {pluginList.map((p) => {
          const Icon = p.icon;
          return (
            <div
              key={p.id}
              className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-4"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div
                    className="w-10 h-10 rounded-xl flex items-center justify-center"
                    style={{ backgroundColor: p.bg, color: p.color }}
                  >
                    <Icon className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="font-bold text-sm text-slate-900">
                      {p.title}
                    </h3>
                    <span className="text-[10px] text-slate-400">
                      系统官方内置扩展
                    </span>
                  </div>
                </div>

                <label className="flex items-center gap-1.5 text-xs text-slate-600 font-medium cursor-pointer">
                  <input
                    type="checkbox"
                    defaultChecked
                    onChange={(e) =>
                      onShowToast(
                        e.target.checked
                          ? `${p.title} 已在演示中启用`
                          : `${p.title} 已停用`
                      )
                    }
                    className="rounded text-[#4268ef]"
                  />
                  启用插件
                </label>
              </div>

              <p className="text-xs text-slate-500 leading-relaxed">{p.desc}</p>

              <div className="p-3 rounded-xl bg-slate-50 border border-slate-200/80 space-y-2 text-xs">
                {p.features.map((f, i) => (
                  <div
                    key={i}
                    className="flex items-center justify-between text-slate-700"
                  >
                    <span>{f}</span>
                    <input
                      type="checkbox"
                      defaultChecked={i === 0}
                      className="rounded text-[#4268ef]"
                    />
                  </div>
                ))}
              </div>

              <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-100">
                <button
                  onClick={() => onShowToast('插件配置已成功应用')}
                  className="px-3 py-1.5 text-xs rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 font-medium"
                >
                  保存配置
                </button>
                <button
                  onClick={onOpenNewProject}
                  className="px-3.5 py-1.5 text-xs rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white font-semibold shadow-xs"
                >
                  在新主题中使用此组件
                </button>
              </div>
            </div>
          );
        })}
      </div>

      <div className="p-4 rounded-xl bg-blue-50/60 border border-blue-200/60 text-xs text-blue-900">
        <strong>提示：</strong>
        插件适配能力由各组件内部声明。当目标屏幕规格或硬件带宽不足时，系统将智能提示并降级显示。
      </div>
    </div>
  );
};

// --- Account Page ---
export const AccountPage: React.FC<{
  resourcesCount: number;
  favoritesCount: number;
  devicesCount: number;
  onGoSettings: () => void;
  onShowToast: (msg: string) => void;
}> = ({
  resourcesCount,
  favoritesCount,
  devicesCount,
  onGoSettings,
  onShowToast,
}) => {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
          个人中心
        </h1>
        <p className="text-xs text-slate-500 mt-1">
          本地内容无需登录即可完全离线使用；云端权益与跨设备同步功能持续规划中。
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* User Card */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-4">
          <div className="flex items-center gap-3.5 pb-4 border-b border-slate-100">
            <div className="w-12 h-12 rounded-2xl bg-gradient-to-tr from-[#4268ef] to-[#6d8bf8] text-white flex items-center justify-center text-lg font-bold shadow-md">
              M
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900">
                本地创作者模式 (访客)
              </h2>
              <p className="text-xs text-slate-400 mt-0.5">
                你的设备配置、自定义排版与创作均安全存储于本机
              </p>
            </div>
          </div>

          <div>
            <h3 className="text-xs font-bold text-slate-800 mb-1">
              MiraBox 云端账号连接
            </h3>
            <p className="text-xs text-slate-500 leading-relaxed mb-3">
              正式版将提供账号快速登录、多电脑桌面无感漫游同步及专属设计师社区工坊。
            </p>
            <button
              onClick={() => onShowToast('登录模块已在正式版本规划中')}
              className="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs"
            >
              了解账号同步特权 →
            </button>
          </div>
        </div>

        {/* Local Summary */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs flex flex-col justify-between">
          <div>
            <h2 className="text-base font-bold text-slate-900 mb-4">
              本机数据概况
            </h2>
            <div className="divide-y divide-slate-100 text-xs">
              <div className="py-2.5 flex items-center justify-between">
                <span className="text-slate-500">已收录的壁纸与主题</span>
                <strong className="text-slate-900 font-semibold">
                  {resourcesCount} 项
                </strong>
              </div>
              <div className="py-2.5 flex items-center justify-between">
                <span className="text-slate-500">标记收藏的项目</span>
                <strong className="text-slate-900 font-semibold">
                  {favoritesCount} 项
                </strong>
              </div>
              <div className="py-2.5 flex items-center justify-between">
                <span className="text-slate-500">受管理的物理屏幕</span>
                <strong className="text-slate-900 font-semibold">
                  {devicesCount} 台
                </strong>
              </div>
            </div>
          </div>

          <div className="pt-4 border-t border-slate-100">
            <button
              onClick={onGoSettings}
              className="text-xs font-semibold text-[#4268ef] hover:underline flex items-center gap-1"
            >
              前往数据备份与还原设置 →
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

// --- Settings Page ---
export const SettingsPage: React.FC<{
  settings: AppSettings;
  devices: Device[];
  onToggleSetting: (key: keyof AppSettings) => void;
  onOpenExportModal: () => void;
  onResetDemo: () => void;
  onGoGuide: () => void;
}> = ({
  settings,
  devices,
  onToggleSetting,
  onOpenExportModal,
  onResetDemo,
  onGoGuide,
}) => {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
          系统设置与维护
        </h1>
        <p className="text-xs text-slate-500 mt-1">
          管理软件启动行为、硬件输出偏好与规划数据导出。
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* General Startup */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-4">
          <h2 className="text-base font-bold text-slate-900">常规偏好</h2>

          <div className="space-y-3 text-xs">
            <label className="flex items-center justify-between p-2.5 rounded-lg border border-slate-100 bg-slate-50/50 cursor-pointer">
              <span className="font-medium text-slate-700">关闭主窗口后最小化到任务栏托盘</span>
              <input
                type="checkbox"
                checked={settings.tray}
                onChange={() => onToggleSetting('tray')}
                className="rounded text-[#4268ef]"
              />
            </label>

            <label className="flex items-center justify-between p-2.5 rounded-lg border border-slate-100 bg-slate-50/50 cursor-pointer">
              <span className="font-medium text-slate-700">开机自动启动后台副屏渲染引擎</span>
              <input
                type="checkbox"
                checked={settings.start}
                onChange={() => onToggleSetting('start')}
                className="rounded text-[#4268ef]"
              />
            </label>

            <label className="flex items-center justify-between p-2.5 rounded-lg border border-slate-100 bg-slate-50/50 cursor-pointer">
              <span className="font-medium text-slate-700">重新连入设备时自动恢复上次输出</span>
              <input
                type="checkbox"
                checked={settings.restore}
                onChange={() => onToggleSetting('restore')}
                className="rounded text-[#4268ef]"
              />
            </label>
          </div>

          <p className="text-[11px] text-slate-400">
            正式版在恢复时将严格匹配物理序列号，离线或内容缺失时不强制输出。
          </p>
        </div>

        {/* Data & Backup */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-4">
          <h2 className="text-base font-bold text-slate-900">数据与备份</h2>
          <p className="text-xs text-slate-500 leading-relaxed">
            将当前工作区内的全部设备排版、轮播队列及主题元数据一键导出为标准 JSON 格式。
          </p>

          <button
            onClick={onOpenExportModal}
            className="w-full py-2.5 px-4 rounded-xl bg-[#4268ef] hover:bg-[#3458db] text-white font-semibold text-xs shadow-xs transition-colors flex items-center justify-center gap-2"
          >
            <Download className="w-3.5 h-3.5" />
            导出完整配置快照 JSON
          </button>

          <div className="pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
            <span className="text-slate-400">重置所有演示数据至出厂状态</span>
            <button
              onClick={onResetDemo}
              className="px-3 py-1.5 rounded-lg border border-rose-200 hover:bg-rose-50 text-rose-600 font-medium"
            >
              恢复出厂预设
            </button>
          </div>
        </div>

        {/* Hardware Status Diagnostics */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-3">
          <h2 className="text-base font-bold text-slate-900">硬件通道诊断</h2>
          <div className="divide-y divide-slate-100 text-xs">
            {devices.map((d) => (
              <div
                key={d.id}
                className="py-2.5 flex items-center justify-between"
              >
                <div>
                  <span className="font-semibold text-slate-800">{d.name}</span>
                  <span className="text-slate-400 ml-2 font-mono">
                    [{d.serial}]
                  </span>
                </div>
                <span
                  className={`px-2 py-0.5 rounded text-[10px] font-medium ${
                    d.online
                      ? 'bg-emerald-50 text-emerald-700'
                      : 'bg-amber-50 text-amber-700'
                  }`}
                >
                  {d.online ? 'USB 握手就绪' : '等待重连同一设备'}
                </span>
              </div>
            ))}
          </div>

          <button
            onClick={onGoGuide}
            className="text-xs text-[#4268ef] hover:underline font-medium block pt-2"
          >
            查看系统完整架构与验收规范 →
          </button>
        </div>

        {/* Version Information */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-2">
          <h2 className="text-base font-bold text-slate-900">版本信息</h2>
          <p className="text-xs text-slate-700 font-medium">
            D92 Studio · MiraBox 多设备智能副屏控制中心 V6.3
          </p>
          <p className="text-xs text-slate-400 leading-relaxed">
            支持超宽屏硬件像素级直驱、多设备多画布自适应排版及高保真轮播引擎。
          </p>
        </div>
      </div>
    </div>
  );
};

// --- Guide Page ---
export const GuidePage: React.FC<{
  onSetScenario: (sc: string, page: string) => void;
  onGoPage: (page: string) => void;
}> = ({ onSetScenario, onGoPage }) => {
  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            完整页面规划与多设备核心架构
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            以设备为操作目标，以资源为共享内容，以多布局解决超宽屏物理规格差异。
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => onSetScenario('dual', 'overview')}
            className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-[#edf2ff] text-[#4268ef] hover:bg-blue-100 transition-colors"
          >
            试试：双台 D92 (一横一竖)
          </button>
          <button
            onClick={() => onSetScenario('single69', 'overview')}
            className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-[#edf2ff] text-[#4268ef] hover:bg-blue-100 transition-colors"
          >
            试试：单台 D69
          </button>
        </div>
      </div>

      {/* Core Objects Cards */}
      <div className="bg-white rounded-2xl border border-slate-200 p-6 space-y-4 shadow-2xs">
        <h2 className="text-base font-bold text-slate-900">三个核心对象</h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
          <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 space-y-2">
            <h3 className="font-bold text-slate-900 text-sm">1. 物理设备 (Device)</h3>
            <p className="text-slate-500 leading-relaxed">
              具有唯一的稳定硬件序列号、自定义名称、物理分辨率与旋转方向。多台同型号设备独立管理。
            </p>
          </div>

          <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 space-y-2">
            <h3 className="font-bold text-slate-900 text-sm">2. 共享资源 (Resource)</h3>
            <p className="text-slate-500 leading-relaxed">
              超宽壁纸、流光视频、自适应主题及扩展组件。资源与设备规格解耦，统一存储并跨设备按需分发。
            </p>
          </div>

          <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 space-y-2">
            <h3 className="font-bold text-slate-900 text-sm">3. 多布局会话 (Session)</h3>
            <p className="text-slate-500 leading-relaxed">
              一个主题内可保存多个专属分辨率布局；每台屏幕拥有独立渲染会话与轮播队列，互不阻塞。
            </p>
          </div>
        </div>
      </div>

      {/* 4 Fit Levels */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-3">
          <h2 className="text-base font-bold text-slate-900">
            智能适配度四档判定
          </h2>
          <div className="space-y-2.5 text-xs">
            <div className="p-2.5 rounded-lg bg-emerald-50 border border-emerald-200 flex items-center justify-between">
              <div>
                <strong className="text-emerald-900 block font-semibold">
                  适合 / 专属布局
                </strong>
                <span className="text-emerald-700 text-[11px]">
                  宽高比差异 ≤ 1%，或主题拥有对应硬件规格的原生排版
                </span>
              </div>
              <span className="text-emerald-800 font-mono font-bold">100% 完整</span>
            </div>

            <div className="p-2.5 rounded-lg bg-blue-50 border border-blue-200 flex items-center justify-between">
              <div>
                <strong className="text-blue-900 block font-semibold">
                  轻微留边
                </strong>
                <span className="text-blue-700 text-[11px]">
                  误差 1% ~ 5%，完整显示无裁切，边缘微量纯黑填充
                </span>
              </div>
              <span className="text-blue-800 font-mono font-bold">≤ 5%</span>
            </div>

            <div className="p-2.5 rounded-lg bg-amber-50 border border-amber-200 flex items-center justify-between">
              <div>
                <strong className="text-amber-900 block font-semibold">
                  明显留边
                </strong>
                <span className="text-amber-700 text-[11px]">
                  误差 &gt; 5%，使用前需要二次点击“仍然使用”确认防误触
                </span>
              </div>
              <span className="text-amber-800 font-mono font-bold">&gt; 5%</span>
            </div>

            <div className="p-2.5 rounded-lg bg-slate-100 border border-slate-200 flex items-center justify-between">
              <div>
                <strong className="text-slate-800 block font-semibold">
                  尺寸待检查
                </strong>
                <span className="text-slate-600 text-[11px]">
                  动态抓取或未解码资源，需进入详情核实后应用
                </span>
              </div>
              <span className="text-slate-700 font-mono font-bold">待验</span>
            </div>
          </div>
        </div>

        {/* Roadmap Batches */}
        <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-2xs space-y-3">
          <h2 className="text-base font-bold text-slate-900">工程迭代批次</h2>
          <div className="space-y-2 text-xs">
            <div className="p-2.5 rounded-lg border border-slate-200 bg-slate-50">
              <strong className="text-slate-900 block">批次 1 · 设计底座</strong>
              <p className="text-slate-500 mt-0.5">
                顶栏、侧栏导航、底部独立输出常驻栏、超宽副屏预览容器。
              </p>
            </div>
            <div className="p-2.5 rounded-lg border border-slate-200 bg-slate-50">
              <strong className="text-slate-900 block">批次 2 · 多设备底座</strong>
              <p className="text-slate-500 mt-0.5">
                硬件注册表、SN 唯一绑定、横竖屏几何缩放及误差计算公式。
              </p>
            </div>
            <div className="p-2.5 rounded-lg border border-slate-200 bg-slate-50">
              <strong className="text-slate-900 block">批次 3 · 单设备精修优先</strong>
              <p className="text-slate-500 mt-0.5">
                80% 用户仅拥有单台 D92/D69，默认以单设备体验呈现，连接第二台时自动升级多设备管理。
              </p>
            </div>
            <div className="p-2.5 rounded-lg border border-slate-200 bg-slate-50">
              <strong className="text-slate-900 block">批次 4 · 创作与轮播扩展</strong>
              <p className="text-slate-500 mt-0.5">
                画布设计中心、多布局克隆、多选批量加入及回收站安全恢复。
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
