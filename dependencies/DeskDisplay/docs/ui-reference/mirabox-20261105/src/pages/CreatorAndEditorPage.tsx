import React, { useState } from 'react';
import { Resource, Device } from '../types';
import { sizeText, specName, SPECS, fit, dims } from '../data';
import { ScreenBox } from '../components/ScreenBox';
import { ArtDisplay } from '../components/ArtDisplay';
import {
  Plus,
  Sparkles,
  Edit3,
  Layers,
  ArrowLeft,
  Check,
  Type,
  Clock,
  Palette,
  Layout,
  Maximize2,
} from 'lucide-react';

// --- Creator Page ---
export const CreatorPage: React.FC<{
  resources: Resource[];
  onOpenNewProject: () => void;
  onOpenEditor: (id: number) => void;
  onApplyModal: (id: number) => void;
}> = ({ resources, onOpenNewProject, onOpenEditor, onApplyModal }) => {
  const themeList = resources.filter(
    (r) => r.local && (r.type === '主题' || r.type === '时钟' || r.created)
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">
            主题创作中心
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            为不同设备与横竖方向设计专属排版，一个主题可包含多个自适应布局。
          </p>
        </div>
        <button
          onClick={onOpenNewProject}
          className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
        >
          <Plus className="w-3.5 h-3.5" />
          新建主题项目
        </button>
      </div>

      {/* Hero Banner */}
      <div className="bg-gradient-to-r from-[#1e293b] via-[#334155] to-[#475569] rounded-2xl p-6 text-white flex flex-col sm:flex-row items-center justify-between gap-6 shadow-xs">
        <div>
          <span className="text-[10px] tracking-widest uppercase font-semibold text-slate-300">
            CREATE YOUR SCREEN
          </span>
          <h2 className="text-2xl font-bold mt-2 tracking-tight">
            从灵感到屏幕，尽在掌控
          </h2>
          <p className="text-xs text-slate-300 mt-1.5 max-w-lg leading-relaxed">
            支持同时为 D92 和 D69
            维护横屏与竖屏布局。素材统一引用，图层独立保存。
          </p>
          <button
            onClick={onOpenNewProject}
            className="mt-4 px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-white text-slate-900 hover:bg-slate-100 transition-colors shadow-xs"
          >
            从空白画布开始设计 →
          </button>
        </div>
        <div className="text-5xl font-light text-white/80 select-none font-serif tracking-wider">
          Aa
        </div>
      </div>

      {/* Themes Grid */}
      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-bold text-slate-900">
            我的主题与设计项目 ({themeList.length})
          </h2>
          <span className="text-xs text-slate-400">
            点击“编辑主题”可调整排版与色彩
          </span>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {themeList.map((r) => (
            <div
              key={r.id}
              className="rounded-2xl border border-slate-200 bg-white p-4 shadow-2xs flex flex-col justify-between"
            >
              <div>
                <div className="bg-slate-100 rounded-xl p-3 flex items-center justify-center min-h-[140px] mb-3">
                  <ScreenBox
                    resource={r}
                    mode="contain"
                    className="max-h-[120px] max-w-full"
                  />
                </div>

                <div className="mb-4">
                  <h3 className="font-bold text-sm text-slate-900">{r.name}</h3>
                  <p className="text-xs text-slate-400 mt-1">
                    {r.sizes.length} 个独立布局 ·{' '}
                    {r.sizes.map((s) => sizeText(s)).join(' / ')}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2 pt-3 border-t border-slate-100">
                <button
                  onClick={() => onOpenEditor(r.id)}
                  className="flex-1 py-1.5 px-2 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white text-center transition-colors shadow-xs flex items-center justify-center gap-1"
                >
                  <Edit3 className="w-3.5 h-3.5" />
                  编辑主题
                </button>
                <button
                  onClick={() => onApplyModal(r.id)}
                  className="flex-1 py-1.5 px-2 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 text-center transition-colors"
                >
                  使用主题
                </button>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};

// --- Editor Page ---
export const EditorPage: React.FC<{
  resource: Resource;
  devices: Device[];
  onBack: () => void;
  onSave: (updated: Resource) => void;
  onFinishAndApply: (updated: Resource) => void;
  onOpenAddLayoutModal: () => void;
}> = ({
  resource,
  devices,
  onBack,
  onSave,
  onFinishAndApply,
  onOpenAddLayoutModal,
}) => {
  const [layoutIndex, setLayoutIndex] = useState(0);
  const [name, setName] = useState(resource.name);
  const [type, setType] = useState(resource.type);
  const [text, setText] = useState(
    resource.layoutData?.[0]?.text || resource.text || '10:24'
  );
  const [color, setColor] = useState(
    resource.layoutData?.[0]?.color || resource.color || '#ffffff'
  );
  const [style, setStyle] = useState(
    resource.layoutData?.[0]?.style || resource.style || 'paper'
  );
  const [isDirty, setIsDirty] = useState(false);

  const currentSize = resource.sizes[layoutIndex] || [1920, 462];
  const isVertical = currentSize[0] < currentSize[1];
  const ratio = currentSize[0] / currentSize[1];

  const buildUpdatedResource = (): Resource => {
    const layoutData = { ...(resource.layoutData || {}) };
    layoutData[layoutIndex] = { text, color, style };

    return {
      ...resource,
      name: name.trim() || resource.name,
      type,
      text,
      color,
      style,
      layoutData,
      created: true,
    };
  };

  const handleSave = () => {
    const updated = buildUpdatedResource();
    onSave(updated);
    setIsDirty(false);
  };

  const handleFinish = () => {
    const updated = buildUpdatedResource();
    onFinishAndApply(updated);
  };

  const handleSwitchLayout = (newIndex: number) => {
    // Save current layout data in memory
    const existing = resource.layoutData?.[newIndex] || {};
    setLayoutIndex(newIndex);
    setText(existing.text || '10:24');
    setColor(existing.color || '#ffffff');
    if (existing.style) setStyle(existing.style);
  };

  // Find which device specs are missing a dedicated layout
  const missingSpecs: [number, number][] = [];
  devices.forEach((d) => {
    const ds = dims(d);
    if (
      !resource.sizes.some((s) => s[0] === ds[0] && s[1] === ds[1]) &&
      !missingSpecs.some((s) => s[0] === ds[0] && s[1] === ds[1])
    ) {
      missingSpecs.push(ds);
    }
  });

  const styleOptions = [
    { id: 'paper', label: '暖纸风格' },
    { id: 'ink', label: '深蓝深邃' },
    { id: 'aurora', label: '极光幻彩' },
    { id: 'pink', label: '柔粉翻页' },
    { id: 'mountain', label: '薄雾山谷' },
    { id: 'sun', label: '日落航线' },
    { id: 'neon', label: '霓虹夜景' },
    { id: 'ember', label: '熔岩流光' },
    { id: 'mint', label: '薄荷几何' },
    { id: 'ocean', label: '深海晨雾' },
    { id: 'night', label: '星河长夜' },
    { id: 'forest', label: '森林瀑布' },
  ];

  return (
    <div className="space-y-4">
      {/* Top Bar for Editor */}
      <div className="bg-white rounded-2xl border border-slate-200 p-4 flex flex-wrap items-center justify-between gap-4 shadow-2xs">
        <div className="flex items-center gap-3">
          <button
            onClick={onBack}
            className="p-1.5 rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 transition-colors"
            title="返回项目列表"
          >
            <ArrowLeft className="w-4 h-4" />
          </button>
          <div>
            <div className="flex items-center gap-2">
              <input
                value={name}
                maxLength={40}
                onChange={(e) => {
                  setName(e.target.value);
                  setIsDirty(true);
                }}
                className="font-bold text-base text-slate-900 border-b border-transparent hover:border-slate-300 focus:border-[#4268ef] focus:outline-none px-1 py-0.5"
              />
              <span
                className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${
                  isDirty
                    ? 'bg-amber-50 text-amber-700 border border-amber-200'
                    : 'bg-emerald-50 text-emerald-700'
                }`}
              >
                {isDirty ? '● 未保存修改' : '✓ 已保存'}
              </span>
            </div>
            <div className="flex items-center gap-2 text-xs text-slate-400 mt-0.5 px-1">
              <span>正在编辑 {specName(currentSize)} 画布</span>
              <span>·</span>
              <span>{sizeText(currentSize)}</span>
            </div>
          </div>
        </div>

        {/* Layout Switcher & Action Buttons */}
        <div className="flex items-center gap-2.5">
          <div className="flex items-center gap-1.5 text-xs bg-slate-50 px-2.5 py-1 rounded-lg border border-slate-200">
            <Layout className="w-3.5 h-3.5 text-slate-400" />
            <span className="text-slate-500">当前布局：</span>
            <select
              value={layoutIndex}
              onChange={(e) => handleSwitchLayout(Number(e.target.value))}
              className="bg-transparent font-semibold text-slate-800 focus:outline-none"
            >
              {resource.sizes.map((s, idx) => (
                <option key={idx} value={idx}>
                  {specName(s)} ({sizeText(s)})
                </option>
              ))}
            </select>
          </div>

          <button
            onClick={onOpenAddLayoutModal}
            className="px-2.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 flex items-center gap-1"
          >
            <Plus className="w-3.5 h-3.5" />
            增加布局
          </button>

          <button
            onClick={handleSave}
            className="px-3.5 py-1.5 text-xs font-medium rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 transition-colors"
          >
            保存
          </button>

          <button
            onClick={handleFinish}
            className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white shadow-xs transition-colors flex items-center gap-1.5"
          >
            <Sparkles className="w-3.5 h-3.5" />
            完成并应用到屏幕
          </button>
        </div>
      </div>

      {/* Editor Main Grid: Left Tools, Center Stage, Right Properties */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">
        {/* Left Column: Layers and Missing Layout Hints */}
        <div className="lg:col-span-3 space-y-4">
          <div className="bg-white rounded-2xl border border-slate-200 p-4 shadow-2xs space-y-4">
            <h3 className="text-xs font-bold text-slate-900 uppercase tracking-wider">
              内容与图层
            </h3>

            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={() => {
                  setText('你好，世界');
                  setIsDirty(true);
                }}
                className="p-2 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 flex items-center justify-center gap-1.5"
              >
                <Type className="w-3.5 h-3.5" />
                文字内容
              </button>
              <button
                onClick={() => {
                  setText('10:24');
                  setIsDirty(true);
                }}
                className="p-2 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 flex items-center justify-center gap-1.5"
              >
                <Clock className="w-3.5 h-3.5" />
                数字时钟
              </button>
            </div>

            <div className="pt-2 border-t border-slate-100 space-y-1.5 text-xs">
              <span className="text-[11px] text-slate-400 block font-medium">
                图层列表：
              </span>
              <div className="p-2 rounded-lg bg-[#edf2ff] border border-[#cbd6ff] text-[#4268ef] font-medium flex items-center gap-2">
                <Type className="w-3.5 h-3.5" />
                <span>{text === '10:24' ? '数字时钟图层' : '自定义文字图层'}</span>
              </div>
              <div className="p-2 rounded-lg bg-slate-50 border border-slate-200 text-slate-600 font-medium flex items-center gap-2">
                <Layers className="w-3.5 h-3.5" />
                <span>背景画卷图层 ({style})</span>
              </div>
            </div>

            {/* Layout Hints for Missing Specs */}
            {missingSpecs.length > 0 && (
              <div className="p-3 rounded-xl bg-blue-50/70 border border-blue-200/60 text-xs text-blue-900 space-y-2">
                <span className="font-semibold block">检测到已连接设备缺少专属排版：</span>
                <div className="flex flex-wrap gap-1.5">
                  {missingSpecs.map((s) => (
                    <button
                      key={`${s[0]}_${s[1]}`}
                      onClick={() => {
                        resource.sizes.push(s);
                        handleSwitchLayout(resource.sizes.length - 1);
                        setIsDirty(true);
                      }}
                      className="px-2 py-1 rounded bg-white hover:bg-blue-100 text-blue-800 text-[11px] font-semibold border border-blue-200 shadow-2xs"
                    >
                      ＋ 一键生成 {specName(s)}
                    </button>
                  ))}
                </div>
                <p className="text-[10px] text-blue-700">
                  自动复制当前元素并按屏幕宽高比智能适配。
                </p>
              </div>
            )}
          </div>
        </div>

        {/* Center Column: Stage Canvas */}
        <div className="lg:col-span-6 bg-white rounded-2xl border border-slate-200 p-4 shadow-2xs">
          <div className="flex items-center justify-between text-xs text-slate-500 mb-3">
            <span className="font-medium">设计画布实时渲染</span>
            <span>
              {isVertical ? '竖屏' : '横屏'} · {sizeText(currentSize)} (
              {ratio.toFixed(2)}:1)
            </span>
          </div>

          <div className="canvas-stage rounded-xl p-8 flex items-center justify-center min-h-[460px] overflow-hidden">
            <div
              className="bg-[#131c2d] rounded-lg shadow-2xl p-1 border-4 border-slate-800 flex items-center justify-center transition-all duration-300"
              style={{
                aspectRatio: `${currentSize[0]} / ${currentSize[1]}`,
                maxHeight: '400px',
                maxWidth: '100%',
              }}
            >
              <div className="w-full h-full overflow-hidden rounded-[4px] relative">
                <ArtDisplay
                  resource={{
                    ...resource,
                    style,
                    color,
                    text,
                  }}
                  text={text}
                  color={color}
                  vertical={isVertical}
                />
              </div>
            </div>
          </div>
        </div>

        {/* Right Column: Properties */}
        <div className="lg:col-span-3 space-y-4">
          <div className="bg-white rounded-2xl border border-slate-200 p-4 shadow-2xs space-y-4">
            <h3 className="text-xs font-bold text-slate-900 uppercase tracking-wider">
              图层属性调节
            </h3>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">
                展示文字 / 时间文本
              </label>
              <input
                value={text}
                maxLength={24}
                onChange={(e) => {
                  setText(e.target.value);
                  setIsDirty(true);
                }}
                className="w-full px-3 py-1.5 text-xs border border-slate-200 rounded-lg focus:outline-[#4268ef]"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1">
                文字渲染颜色
              </label>
              <div className="flex items-center gap-2">
                <input
                  type="color"
                  value={color}
                  onChange={(e) => {
                    setColor(e.target.value);
                    setIsDirty(true);
                  }}
                  className="w-9 h-9 rounded-lg border border-slate-200 cursor-pointer p-0.5"
                />
                <input
                  value={color}
                  onChange={(e) => {
                    setColor(e.target.value);
                    setIsDirty(true);
                  }}
                  className="w-24 px-2 py-1 text-xs border border-slate-200 rounded font-mono uppercase"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                背景艺术风格
              </label>
              <select
                value={style}
                onChange={(e) => {
                  setStyle(e.target.value);
                  setIsDirty(true);
                }}
                className="w-full px-3 py-1.5 text-xs border border-slate-200 rounded-lg bg-white text-slate-800 focus:outline-[#4268ef]"
              >
                {styleOptions.map((opt) => (
                  <option key={opt.id} value={opt.id}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="pt-3 border-t border-slate-100 text-[11px] text-slate-400 space-y-1">
              <div>物理输出设备：MiraBox 超宽屏</div>
              <div>画布抗锯齿与高帧率硬件加速已就绪</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
