import { desktopMode, previewCraftPlugin, craftSettings, configureCraft, applyCraft, importCraftPlugin, listCraftPlugins, type CraftSettings, type CraftPluginEntry } from '../services/desktopBridge';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Check,
  Pause,
  Play,
  Download,
  Eye,
  EyeOff,
  Maximize2,
  RotateCcw,
  Search,
  SlidersHorizontal,
  Trash2,
  X,
} from 'lucide-react';
import {
  DeviceInfo,
  PluginCategory,
  PluginConfig,
  PluginItem,
  PluginPosition,
  PluginScale,
  ScreenOrientation,
  ThemeItem,
} from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';
import { PluginThumbnailPreview, PomodoroRuntimeState } from './PluginWidgets';

interface PluginCenterViewProps {
  plugins: PluginItem[];
  pureMode: boolean;
  activeTheme: ThemeItem;
  activeDevice: DeviceInfo;
  pomodoroState: PomodoroRuntimeState;
  onOrientationChange: (orientation: ScreenOrientation) => void;
  onTogglePureMode: () => void;
  onInstallPlugin: (id: string, enableAfterInstall: boolean) => void;
  onTogglePlugin: (id: string) => void;
  onUninstallPlugin: (id: string) => void;
  onUpdatePluginConfig: (id: string, nextConfig: PluginConfig, enableIfNeeded?: boolean) => void;
  onResetPluginConfig: (id: string) => void;
  onPomodoroAction: (action: 'toggle' | 'reset') => void;
}

const POSITION_LABELS: Record<PluginPosition, string> = {
  'top-left': '左上角',
  'top-right': '右上角',
  center: '画面居中',
  'bottom-left': '左下角',
  'bottom-right': '右下角',
  full: '100%显示',
};

const SCALE_LABELS: Record<PluginScale, string> = {
  compact: '紧凑',
  standard: '标准',
  large: '醒目',
};

const ACCENT_SWATCHES = [
  { hex: '#5E45F8', label: '极光紫' },
  { hex: '#38BDF8', label: '冰晶蓝' },
  { hex: '#10B981', label: '翡翠绿' },
  { hex: '#F59E0B', label: '琥珀金' },
  { hex: '#EC4899', label: '绯樱粉' },
];

const CATEGORIES: ('全部' | PluginCategory)[] = [
  '全部',
  '时间组件',
  '系统监控',
  '媒体联动',
  '效率工具',
  '生活资讯',
  '互动娱乐',
];

type StatusFilter = 'all' | 'enabled' | 'installed' | 'available';

export const PluginCenterView: React.FC<PluginCenterViewProps> = ({
  plugins,
  pureMode,
  activeTheme,
  activeDevice,
  pomodoroState,
  onOrientationChange,
  onTogglePureMode,
  onInstallPlugin,
  onTogglePlugin,
  onUninstallPlugin,
  onUpdatePluginConfig,
  onResetPluginConfig,
  onPomodoroAction,
}) => {
  const [craftPlugins, setCraftPlugins] = useState<CraftPluginEntry[]>([]);
  const [craftMessage, setCraftMessage] = useState('');
  const [craftBusy, setCraftBusy] = useState(false);
  const [craftEdit, setCraftEdit] = useState<{ id: string; values: CraftSettings } | null>(null);
  async function runCraft(action: () => Promise<void>) {
    setCraftBusy(true);
    setCraftMessage('');
    try { await action(); }
    catch (error) { setCraftMessage(error instanceof Error ? error.message : String(error)); }
    finally { setCraftBusy(false); }
  }
  const [craftImporting, setCraftImporting] = useState(false);
  useEffect(() => {
    if (!desktopMode) return;
    let active = true;
    listCraftPlugins().then(entries => { if (active) setCraftPlugins(entries); })
      .catch(error => { if (active) setCraftMessage(String(error)); });
    return () => { active = false; };
  }, []);
  const handleCraftImport = async () => {
    setCraftImporting(true);
    setCraftMessage('');
    try {
      const result = await importCraftPlugin();
      if (result) {
        setCraftPlugins(await listCraftPlugins());
        setStatusFilter('installed');
        setCategoryFilter('全部');
        setSearchQuery(result.Entry.Name);
        setCraftMessage(result.AlreadyImported ? `${result.Entry.Name} 已在插件列表中` : `${result.Entry.Name} 导入成功`);
      }
    } catch (error) { setCraftMessage(error instanceof Error ? error.message : '导入失败'); }
    finally { setCraftImporting(false); }
  };
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [categoryFilter, setCategoryFilter] = useState<'全部' | PluginCategory>('全部');
  const [searchQuery, setSearchQuery] = useState('');
  const [stageBackdrop, setStageBackdrop] = useState<'theme' | 'dark'>('theme');

  // Config / Preview Modal state
  const [editingPluginId, setEditingPluginId] = useState<string | null>(null);
  const [draftConfig, setDraftConfig] = useState<PluginConfig | null>(null);
  const [modalBackdrop, setModalBackdrop] = useState<'theme' | 'dark'>('theme');

  const isPortrait = activeDevice.orientation === 'portrait';
  const previewAspectRatio = isPortrait
    ? `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`
    : `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`;

  const installedCount = useMemo(
    () => plugins.filter((p) => p.installed).length,
    [plugins]
  );
  const enabledPlugins = useMemo(
    () => plugins.filter((p) => p.installed && p.enabled),
    [plugins]
  );

  const visibleCraft = craftPlugins.filter(plugin =>
    statusFilter !== 'available' && (statusFilter !== 'enabled' || activeDevice.themeId === `craft-plugin:${plugin.Id}`) &&
    (categoryFilter === '全部' || categoryFilter === '时间组件') &&
    `${plugin.Name} ${plugin.Author}`.toLowerCase().includes(searchQuery.trim().toLowerCase()));
  const filteredPlugins = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return plugins.filter((plg) => {
      if (statusFilter === 'enabled' && !(plg.installed && plg.enabled)) return false;
      if (statusFilter === 'installed' && !plg.installed) return false;
      if (statusFilter === 'available' && plg.installed) return false;
      if (categoryFilter !== '全部' && plg.category !== categoryFilter) return false;
      if (
        q !== '' &&
        !plg.name.toLowerCase().includes(q) &&
        !plg.description.toLowerCase().includes(q) &&
        !plg.category.toLowerCase().includes(q) &&
        !plg.author.toLowerCase().includes(q)
      ) {
        return false;
      }
      return true;
    });
  }, [plugins, statusFilter, categoryFilter, searchQuery]);

  const editingPlugin = useMemo(
    () => plugins.find((p) => p.id === editingPluginId) ?? null,
    [plugins, editingPluginId]
  );

  const openConfigModal = (plg: PluginItem) => {
    setEditingPluginId(plg.id);
    setDraftConfig({ ...plg.config });
  };

  const closeConfigModal = () => {
    setEditingPluginId(null);
    setDraftConfig(null);
  };

  const handleSaveConfig = (enableToo = false) => {
    if (!editingPlugin || !draftConfig) return;
    onUpdatePluginConfig(editingPlugin.id, draftConfig, enableToo);
    closeConfigModal();
  };

  const darkStageTheme: ThemeItem = useMemo(
    () => ({
      ...activeTheme,
      id: 'plugin-stage-dark',
      displayName: '深空纯黑画布',
      customArt: 'aurora-prism',
      clockStyle: 'none',
    }),
    [activeTheme]
  );

  return (
    <div className="space-y-6">
      {/* Studio Header Card */}
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-[24px] bg-white p-5 shadow-[0_10px_28px_rgba(30,20,80,0.04)]">
        <div>
          <h2 className="text-[20px] font-bold tracking-tight text-[#1A172B]">插件中心</h2>
          {desktopMode && <button disabled={craftImporting} className="mt-2 rounded-xl bg-[#6244ff] px-4 py-2 text-sm text-white disabled:opacity-50" onClick={handleCraftImport}>{craftImporting ? '正在导入…' : '导入插件'}</button>}
          <div className="mt-1 flex flex-wrap items-center gap-2 text-[13px] text-[#7A7690]">
            <span>{activeDevice.name}</span>
            <span aria-hidden="true">·</span>
            <span className="font-mono-tabular">
              {isPortrait ? '竖屏' : '横屏'} ({activeDevice.resolution})
            </span>
            <span aria-hidden="true">·</span>
            <span className="font-mono-tabular">
              已安装 {installedCount + craftPlugins.length}/{plugins.length + craftPlugins.length}
            </span>
            <span aria-hidden="true">·</span>
            <span className="font-mono-tabular text-[#5E45F8] font-semibold">
              已启用 {enabledPlugins.length}
            </span>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          <button
            type="button"
            onClick={onTogglePureMode}
            aria-pressed={pureMode}
            className={`flex items-center gap-2 rounded-xl px-4 py-2 text-[12.5px] font-semibold transition-colors whitespace-nowrap ${
              pureMode
                ? 'bg-[#5E45F8] text-white shadow-[0_6px_16px_rgba(94,69,248,0.3)]'
                : 'border border-[#E5E2F4] bg-[#F8F7FC] text-[#4A4660] hover:border-[#C8BDFC] hover:text-[#5E45F8]'
            }`}
          >
            {pureMode ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            <span>{pureMode ? '纯净模式已开启（已隐藏挂件）' : '开启纯净显示模式'}</span>
          </button>
        </div>
      </div>

      {/* Aurora Live Composite Stage */}
      <section
        aria-label="实时插件叠加预览台"
        className="aurora-dark-card relative overflow-hidden rounded-[26px] p-5 sm:p-6 text-white shadow-[0_22px_50px_rgba(24,10,68,0.24)]"
      >
        <div className="relative z-10 flex flex-wrap items-center justify-between gap-3">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2 text-[12px] font-medium text-white/75">
              <span>实时挂件叠加预览台</span>
              <span aria-hidden="true">·</span>
              <span className="font-mono-tabular">{activeDevice.resolution}</span>
              <span aria-hidden="true">·</span>
              <span>
                {pureMode
                  ? '纯净模式：所有桌面挂件已临时静默'
                  : `已叠加 ${enabledPlugins.length} 个启用中的挂件`}
              </span>
            </div>
            <h3 className="mt-1 truncate text-[17px] sm:text-[19px] font-bold text-white">
              {stageBackdrop === 'theme' ? activeTheme.displayName : '深空极光测试底图'}
            </h3>
          </div>

          <div className="flex flex-wrap items-center gap-2 shrink-0">

            <div
              className="flex items-center rounded-full border border-white/15 bg-white/10 p-1 backdrop-blur-md"
              role="group"
              aria-label="预览底图切换"
            >
              <button
                type="button"
                onClick={() => setStageBackdrop('theme')}
                className={`rounded-full px-3 py-1 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  stageBackdrop === 'theme'
                    ? 'bg-white text-[#1A172B]'
                    : 'text-white/75 hover:text-white'
                }`}
              >
                当前壁纸
              </button>
              <button
                type="button"
                onClick={() => setStageBackdrop('dark')}
                className={`rounded-full px-3 py-1 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  stageBackdrop === 'dark'
                    ? 'bg-white text-[#1A172B]'
                    : 'text-white/75 hover:text-white'
                }`}
              >
                深空底图
              </button>
            </div>

            <div
              className="flex items-center rounded-full border border-white/15 bg-white/10 p-1 backdrop-blur-md"
              role="group"
              aria-label="屏幕方向切换"
            >
              <button
                type="button"
                onClick={() => onOrientationChange('landscape')}
                className={`rounded-full px-3 py-1 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  !isPortrait
                    ? 'bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.45)]'
                    : 'text-white/75 hover:text-white'
                }`}
              >
                横屏
              </button>
              <button
                type="button"
                onClick={() => onOrientationChange('portrait')}
                className={`rounded-full px-3 py-1 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  isPortrait
                    ? 'bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.45)]'
                    : 'text-white/75 hover:text-white'
                }`}
              >
                竖屏
              </button>
            </div>
          </div>
        </div>

        {/* Live Proportional Output Preview */}
        <div className="relative z-10 mt-5 flex justify-center">
          <div
            style={{ aspectRatio: previewAspectRatio, width: isPortrait ? undefined : `${Math.min(1, (activeDevice.nativeWidth / activeDevice.nativeHeight) / (1920 / 462)) * 100}%` }}
            className={`relative overflow-hidden rounded-[20px] border border-white/20 bg-black/40 shadow-[0_22px_48px_rgba(6,2,20,0.55)] transition-all duration-300 ${
              isPortrait ? 'w-[216px] sm:w-[236px] max-w-full' : ''
            }`}
          >
            <ThemeArtwork
              theme={stageBackdrop === 'theme' ? activeTheme : darkStageTheme}
              className="h-full w-full"
              showClock={stageBackdrop === 'theme'}
              liveTick={true}
              compact={isPortrait}
              portrait={isPortrait}
              overlayPlugins={pureMode ? [] : enabledPlugins}
              pomodoroState={pomodoroState}
            />
          </div>
        </div>

        {/* Active Plugin Quick Config Strip */}
        <div className="relative z-10 mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-white/10 pt-3.5 text-[12px] text-white/75">
          {pureMode ? (
            <div className="flex items-center gap-2">
              <span>纯净模式生效中，所有插件悬浮层已在预览和主副屏隐藏</span>
              <button
                type="button"
                onClick={onTogglePureMode}
                className="font-semibold text-[#C4B5FD] hover:text-white hover:underline whitespace-nowrap"
              >
                恢复挂件显示
              </button>
            </div>
          ) : enabledPlugins.length === 0 ? (
            <span>当前未启用任何挂件，可在下方插件卡片中一键安装或开启</span>
          ) : (
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5">
              <span className="text-white/55">当前运行挂件（点击调参）：</span>
              {enabledPlugins.map((plg) => (
                <button
                  key={plg.id}
                  type="button"
                  onClick={() => openConfigModal(plg)}
                  className="flex items-center gap-1.5 font-medium text-white/90 transition-colors hover:text-[#C4B5FD] whitespace-nowrap"
                >
                  <span
                    className="h-2 w-2 rounded-full"
                    style={{ backgroundColor: plg.config.accentColor }}
                  />
                  <span>{plg.name}</span>
                  <span className="text-white/50">· {POSITION_LABELS[plg.config.position]}</span>
                </button>
              ))}
            </div>
          )}
        </div>
      </section>

      {/* Search & Filter Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-[20px] bg-white p-4 shadow-[0_8px_24px_rgba(30,20,80,0.04)]">
        <div className="flex flex-wrap items-center gap-2.5">
          <div
            className="flex items-center rounded-xl bg-[#F4F3FA] p-1"
            role="group"
            aria-label="插件安装状态筛选"
          >
            {(
              [
                { id: 'all', label: '全部' },
                { id: 'enabled', label: '已启用' },
                { id: 'installed', label: '已安装' },
                { id: 'available', label: '未安装' },
              ] as const
            ).map((tab) => (
              <button
                key={tab.id}
                type="button"
                onClick={() => setStatusFilter(tab.id)}
                className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  statusFilter === tab.id
                    ? 'bg-white text-[#5E45F8] shadow-sm'
                    : 'text-[#6B6880] hover:text-[#1A172B]'
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>

          <div
            className="flex flex-wrap items-center rounded-xl bg-[#F4F3FA] p-1"
            role="group"
            aria-label="插件分类筛选"
          >
            {CATEGORIES.map((cat) => (
              <button
                key={cat}
                type="button"
                onClick={() => setCategoryFilter(cat)}
                className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  categoryFilter === cat
                    ? 'bg-white text-[#5E45F8] shadow-sm'
                    : 'text-[#6B6880] hover:text-[#1A172B]'
                }`}
              >
                {cat}
              </button>
            ))}
          </div>
        </div>

        <div className="relative w-full sm:w-64">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8B87A1]" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="搜索插件名称、分类或作者..."
            className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-2 pl-9 pr-8 text-[12.5px] font-medium text-[#1A172B] placeholder:text-[#9A96AE] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
          />
          {searchQuery && (
            <button
              type="button"
              onClick={() => setSearchQuery('')}
              aria-label="清除搜索"
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-[#8B87A1] hover:text-[#1A172B]"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      </div>

      {/* Plugin Cards Grid — Each Card Includes a Live Thumbnail Preview */}
      {craftMessage && <p role="status" className="text-sm text-[#6244ff]">{craftMessage}</p>}
      {filteredPlugins.length + visibleCraft.length === 0 ? (
        <div className="rounded-[24px] border border-dashed border-[#DAD6F0] bg-white/85 py-14 text-center">
          <p className="text-[14px] font-bold text-[#1A172B]">没有找到符合条件的插件</p>
          <p className="mt-1 text-[12.5px] text-[#7A7690]">
            可以重置状态与分类筛选条件，浏览全部 小屏宝PixBuddy 扩展挂件
          </p>
          <button
            type="button"
            onClick={() => {
              setStatusFilter('all');
              setCategoryFilter('全部');
              setSearchQuery('');
            }}
            className="mt-4 rounded-xl bg-[#F3F0FF] px-4 py-2 text-[12.5px] font-semibold text-[#5E45F8] transition-colors hover:bg-[#E7E0FE]"
          >
            重置筛选条件
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-3">
          {visibleCraft.map(plugin => (
            <article key={plugin.Id} className="rounded-[24px] border border-[#E5E2F4] bg-white p-5">
              <h3 className="font-bold text-[#1A172B]">{plugin.Name}</h3>
              <p className="mt-1 text-sm text-[#7A7690]">{plugin.Format ?? 'Craft'} · {plugin.Version} · {plugin.Author}</p>
              <div className="mt-4 flex flex-wrap gap-2">
                <button disabled={craftBusy} className="rounded-xl bg-[#F3F0FF] px-3 py-2 text-sm text-[#5E45F8]" onClick={() => runCraft(async () => { await previewCraftPlugin(plugin.Id, activeDevice.serial); })}>实时预览</button>
                <button disabled={craftBusy} className="rounded-xl bg-[#F3F0FF] px-3 py-2 text-sm text-[#5E45F8]" onClick={() => runCraft(async () => setCraftEdit({ id: plugin.Id, values: await craftSettings(plugin.Id) }))}>配置</button>
                <button disabled={craftBusy || activeDevice.status === '离线'} className="rounded-xl bg-[#5E45F8] px-3 py-2 text-sm text-white disabled:opacity-50" onClick={() => runCraft(async () => {
                  await applyCraft(plugin.Id, activeDevice.serial);
                  setCraftMessage(`已应用 ${plugin.Name} 到 ${activeDevice.name}`);
                })}>应用到当前设备</button>
              </div>
              {craftEdit?.id === plugin.Id && <div className="mt-4 flex flex-wrap items-center gap-3">
                {Object.keys(craftEdit.values).length === 0 && <p className="text-sm text-[#7A7690]">此插件没有可编辑的配置项。</p>}
                {Object.entries(craftEdit.values).map(([key, value]) => (
                  <label key={key} className="flex items-center gap-2 text-sm">
                    {key === 'hour12' ? '使用 12 小时制' : key}
                    <input className="rounded border border-[#E5E2F4] px-2 py-1"
                      type={typeof value === 'boolean' ? 'checkbox' : typeof value === 'number' ? 'number' : 'text'}
                      checked={typeof value === 'boolean' ? value : undefined}
                      value={typeof value === 'boolean' ? undefined : value}
                      onChange={event => {
                        const input = event.target;
                        const next = typeof value === 'boolean' ? input.checked : typeof value === 'number' ? Number(input.value) : input.value;
                        setCraftEdit({ ...craftEdit, values: { ...craftEdit.values, [key]: next } });
                      }} />
                  </label>
                ))}
                <button disabled={craftBusy || Object.keys(craftEdit.values).length === 0} className="text-sm font-semibold text-[#5E45F8] disabled:opacity-50" onClick={() => runCraft(async () => {
                  await configureCraft(plugin.Id, craftEdit.values);
                  setCraftEdit(null);
                  setCraftMessage('插件配置已保存');
                })}>保存配置</button>
                <button className="text-sm text-[#7A7690]" onClick={() => setCraftEdit(null)}>取消</button>
              </div>}
            </article>
          ))}
          {filteredPlugins.map((plg) => {
            const isActive = plg.installed && plg.enabled;
            const statusText = !plg.installed
              ? '未安装'
              : isActive
              ? pureMode
                ? '已启用（纯净模式暂隐）'
                : '输出中'
              : '已安装 · 已停用';
            const statusClass = !plg.installed
              ? 'text-[#8B87A1]'
              : isActive
              ? pureMode
                ? 'text-[#D97706]'
                : 'text-[#16A34A]'
              : 'text-[#5E45F8]';

            return (
              <article
                key={plg.id}
                className={`group flex flex-col justify-between rounded-[24px] bg-white p-4 shadow-[0_10px_28px_rgba(30,20,80,0.04)] transition-shadow hover:shadow-[0_14px_34px_rgba(45,28,110,0.08)] ${
                  isActive && !pureMode ? 'ring-1 ring-[#D9D2FF]' : ''
                }`}
              >
                <div>
                  {/* Visual Plugin Thumbnail Preview */}
                  <button
                    type="button"
                    onClick={() => openConfigModal(plg)}
                    className="relative block w-full overflow-hidden rounded-[14px] text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
                    aria-label={`预览并配置 ${plg.name}`}
                  >
                    <PluginThumbnailPreview
                      plugin={plg}
                      backdropTheme={stageBackdrop === 'theme' ? activeTheme : undefined}
                      pomodoroState={pomodoroState}
                      aspectRatio="16 / 7"
                    />
                    <span className="pointer-events-none absolute right-2.5 top-2.5 flex items-center gap-1 rounded-lg bg-black/55 px-2 py-1 text-[11px] font-semibold text-white opacity-0 backdrop-blur-sm transition-opacity group-hover:opacity-100">
                      <Maximize2 className="h-3 w-3" />
                      <span>预览调参</span>
                    </span>
                  </button>

                  {/* Clean Unboxed Metadata Row (Zero-Pill) */}
                  <div className="mt-3.5 flex flex-wrap items-center gap-1.5 text-[12px] text-[#7A7690]">
                    <span className="font-semibold text-[#5E45F8]">{plg.category}</span>
                    <span aria-hidden="true">·</span>
                    <span className="font-mono-tabular">{plg.version}</span>
                    <span aria-hidden="true">·</span>
                    <span>{plg.author}</span>
                    <span aria-hidden="true">·</span>
                    <span className={`font-semibold ${statusClass}`}>{statusText}</span>
                  </div>

                  {/* Title */}
                  <h3 className="mt-1.5 text-[16px] font-bold tracking-tight text-[#1A172B]">
                    {plg.name}
                  </h3>
                </div>

                {/* Action Footer */}
                <div className="mt-4 flex items-center justify-between gap-2 border-t border-[#F1EFF8] pt-3.5">
                  {!plg.installed ? (
                    <>
                      <button
                        type="button"
                        onClick={() => openConfigModal(plg)}
                        className="flex items-center gap-1.5 rounded-xl bg-[#F5F4FA] px-3 py-2 text-[12px] font-semibold text-[#56526B] transition-colors hover:bg-[#ECE9F8] whitespace-nowrap"
                      >
                        <Eye className="h-3.5 w-3.5" />
                        <span>效果预览</span>
                      </button>

                      <div className="flex items-center gap-2">
                        <button
                          type="button"
                          onClick={() => onInstallPlugin(plg.id, false)}
                          className="rounded-xl border border-[#E5E2F4] bg-white px-3 py-2 text-[12px] font-semibold text-[#4A4660] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8] whitespace-nowrap"
                        >
                          仅安装
                        </button>
                        <button
                          type="button"
                          onClick={() => onInstallPlugin(plg.id, true)}
                          className="flex items-center gap-1.5 rounded-xl bg-[#5E45F8] px-3.5 py-2 text-[12px] font-semibold text-white transition-colors hover:bg-[#4D34E6] whitespace-nowrap"
                        >
                          <Download className="h-3.5 w-3.5" />
                          <span>安装并启用</span>
                        </button>
                      </div>
                    </>
                  ) : (
                    <>
                      <div className="flex items-center gap-1.5">
                        <button
                          type="button"
                          onClick={() => openConfigModal(plg)}
                          className="flex items-center gap-1.5 rounded-xl bg-[#F5F4FA] px-3 py-2 text-[12px] font-semibold text-[#4A4660] transition-colors hover:bg-[#ECE9F8] hover:text-[#5E45F8] whitespace-nowrap"
                        >
                          <SlidersHorizontal className="h-3.5 w-3.5" />
                          <span>参数配置</span>
                        </button>

                        {!plg.core && (
                          <button
                            type="button"
                            onClick={() => onUninstallPlugin(plg.id)}
                            aria-label={`卸载 ${plg.name}`}
                            title="卸载插件"
                            className="flex h-8 w-8 items-center justify-center rounded-xl text-[#8B87A1] transition-colors hover:bg-[#FEE2E2] hover:text-[#DC2626]"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        )}
                      </div>

                      <button
                        type="button"
                        onClick={() => onTogglePlugin(plg.id)}
                        className={`rounded-xl px-4 py-2 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                          plg.enabled
                            ? 'bg-[#EFEBFD] text-[#5E45F8] hover:bg-[#E4DCFC]'
                            : 'bg-[#5E45F8] text-white hover:bg-[#4D34E6]'
                        }`}
                      >
                        {plg.enabled ? '停用插件' : '启用插件'}
                      </button>
                    </>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      )}

      {/* Live Preview & Config Modal */}
      {editingPlugin && draftConfig && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="plugin-config-title"
        >
          <div
            className="fixed inset-0 bg-[#140B36]/45 backdrop-blur-xs"
            onClick={closeConfigModal}
          />
          <div className="relative z-10 flex max-h-[92vh] w-full max-w-3xl flex-col overflow-hidden rounded-[26px] border border-white/80 bg-white shadow-[0_32px_90px_rgba(20,10,60,0.3)]">
            {/* Modal Header */}
            <div className="flex items-center justify-between border-b border-[#F1EFF8] px-6 py-4">
              <div>
                <div className="flex items-center gap-2 text-[12px] text-[#7A7690]">
                  <span className="font-semibold text-[#5E45F8]">{editingPlugin.category}</span>
                  <span aria-hidden="true">·</span>
                  <span className="font-mono-tabular">{editingPlugin.version}</span>
                  <span aria-hidden="true">·</span>
                  <span>{editingPlugin.author}</span>
                </div>
                <h3 id="plugin-config-title" className="mt-0.5 text-[18px] font-bold text-[#1A172B]">
                  {editingPlugin.name} · 预览与参数配置
                </h3>
              </div>

              <button
                type="button"
                onClick={closeConfigModal}
                aria-label="关闭配置弹窗"
                className="flex h-9 w-9 items-center justify-center rounded-xl bg-[#F5F4FA] text-[#56526B] transition-colors hover:bg-[#ECE9F8]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Modal Scrollable Body */}
            <div className="flex-1 space-y-5 overflow-y-auto p-6 studio-scroll">
              {/* Live Proportional Preview Box */}
              <div className="aurora-dark-card rounded-[20px] p-4 text-white">
                <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <span className="text-[12px] font-medium text-white/75">
                    当前设备比例实时效果（{activeDevice.resolution}）
                  </span>
                  <div className="flex items-center rounded-full border border-white/15 bg-white/10 p-0.5">
                    <button
                      type="button"
                      onClick={() => setModalBackdrop('theme')}
                      className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold transition-colors ${
                        modalBackdrop === 'theme' ? 'bg-white text-[#1A172B]' : 'text-white/75'
                      }`}
                    >
                      当前壁纸
                    </button>
                    <button
                      type="button"
                      onClick={() => setModalBackdrop('dark')}
                      className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold transition-colors ${
                        modalBackdrop === 'dark' ? 'bg-white text-[#1A172B]' : 'text-white/75'
                      }`}
                    >
                      深空底图
                    </button>
                  </div>
                </div>

                <div className="flex justify-center">
                  <div
                    style={{ aspectRatio: previewAspectRatio }}
                    className={`overflow-hidden rounded-[16px] ${
                      isPortrait ? 'w-[190px] max-w-full' : 'w-full'
                    }`}
                  >
                    <PluginThumbnailPreview
                      plugin={{ ...editingPlugin, config: draftConfig }}
                      backdropTheme={modalBackdrop === 'theme' ? activeTheme : undefined}
                      pomodoroState={pomodoroState}
                      aspectRatio={previewAspectRatio}
                      className="h-full w-full"
                    />
                  </div>
                </div>
              </div>

              {/* Layout & Appearance Controls */}
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                <div>
                  <label className="mb-2 block text-[12.5px] font-bold text-[#1A172B]">
                    屏幕挂载位置
                  </label>
                  <div className="grid grid-cols-3 gap-1.5">
                    {(
                      [
                        { id: 'top-left', label: '左上角' },
                        { id: 'top-right', label: '右上角' },
                        { id: 'center', label: '居中' },
                        { id: 'bottom-left', label: '左下角' },
                        { id: 'bottom-right', label: '右下角' },
                        { id: 'full', label: '100%显示' },
                      ] as const
                    ).map((pos) => (
                      <button
                        key={pos.id}
                        type="button"
                        onClick={() => setDraftConfig({ ...draftConfig, position: pos.id })}
                        className={`rounded-xl px-2.5 py-2 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                          draftConfig.position === pos.id
                            ? 'bg-[#5E45F8] text-white'
                            : 'bg-[#F5F4FA] text-[#56526B] hover:bg-[#ECE9F8]'
                        }`}
                      >
                        {pos.label}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="space-y-4">
                  <div>
                    <label className="mb-2 block text-[12.5px] font-bold text-[#1A172B]">
                      主题高亮色
                    </label>
                    <div className="flex items-center gap-2.5">
                      {ACCENT_SWATCHES.map((swatch) => {
                        const selected =
                          draftConfig.accentColor.toLowerCase() === swatch.hex.toLowerCase();
                        return (
                          <button
                            key={swatch.hex}
                            type="button"
                            onClick={() =>
                              setDraftConfig({ ...draftConfig, accentColor: swatch.hex })
                            }
                            aria-label={`选择高亮色 ${swatch.label}`}
                            title={swatch.label}
                            style={{ backgroundColor: swatch.hex }}
                            className={`flex h-8 w-8 items-center justify-center rounded-full transition-transform ${
                              selected
                                ? 'scale-110 ring-2 ring-[#5E45F8] ring-offset-2'
                                : 'opacity-85 hover:opacity-100'
                            }`}
                          >
                            {selected && <Check className="h-3.5 w-3.5 text-white" />}
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <div>
                    <label className="mb-1.5 block text-[12.5px] font-bold text-[#1A172B]">
                      挂件尺寸比例
                    </label>
                    <div className="grid grid-cols-3 gap-1.5 rounded-xl bg-[#F4F3FA] p-1">
                      {(['compact', 'standard', 'large'] as const).map((s) => (
                        <button
                          key={s}
                          type="button"
                          onClick={() => setDraftConfig({ ...draftConfig, scale: s })}
                          className={`rounded-lg py-1.5 text-[12px] font-semibold transition-colors ${
                            draftConfig.scale === s
                              ? 'bg-white text-[#5E45F8] shadow-sm'
                              : 'text-[#6B6880]'
                          }`}
                        >
                          {SCALE_LABELS[s]}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {/* Opacity Slider */}
              <div>
                <div className="mb-1.5 flex items-center justify-between text-[12.5px]">
                  <span className="font-bold text-[#1A172B]">毛玻璃底框不透明度</span>
                  <span className="font-mono-tabular font-bold text-[#5E45F8]">
                    {draftConfig.opacity}%
                  </span>
                </div>
                <input
                  type="range"
                  min={40}
                  max={95}
                  value={draftConfig.opacity}
                  onChange={(e) =>
                    setDraftConfig({ ...draftConfig, opacity: Number(e.target.value) })
                  }
                  className="h-2 w-full cursor-pointer accent-[#5E45F8]"
                />
              </div>

              {editingPlugin.id === 'plg-clock' && <label className="flex items-center justify-between text-[12.5px] font-semibold">
                时钟样式
                <select aria-label="时钟样式" className="rounded-xl border border-[#E6E4F2] bg-white px-3 py-2"
                  value={draftConfig.clockStyle ?? 'digital'}
                  onChange={event => setDraftConfig({ ...draftConfig, clockStyle: event.target.value as 'digital' | 'flip' })}>
                  <option value="digital">数字时钟</option><option value="flip">翻页时钟</option>
                </select>
              </label>}
              {/* Plugin-Specific Settings */}
              <div className="space-y-3 border-t border-[#F1EFF8] pt-4">
                <div className="text-[12.5px] font-bold text-[#1A172B]">插件专属参数</div>

                {editingPlugin.id === 'plg-clock' && (
                  <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                    {[
                      { key: 'showSeconds' as const, label: '显示秒数' },
                      { key: 'showDate' as const, label: '显示日期星期' },
                      { key: 'use24Hour' as const, label: '24 小时制' },
                    ].map((opt) => {
                      const active = draftConfig[opt.key] !== false;
                      return (
                        <button
                          key={opt.key}
                          type="button"
                          onClick={() =>
                            setDraftConfig({ ...draftConfig, [opt.key]: !active })
                          }
                          className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                            active
                              ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                              : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                          }`}
                        >
                          <span>{opt.label}</span>
                          <span>{active ? '开' : '关'}</span>
                        </button>
                      );
                    })}
                  </div>
                )}

                {editingPlugin.id === 'plg-telemetry' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                      {[
                        { key: 'showTemp' as const, label: '显示核心温度 (°C)' },
                        { key: 'showMemory' as const, label: '显示系统内存占用' },
                      ].map((opt) => {
                        const active = draftConfig[opt.key] !== false;
                        return (
                          <button
                            key={opt.key}
                            type="button"
                            onClick={() =>
                              setDraftConfig({ ...draftConfig, [opt.key]: !active })
                            }
                            className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                              active
                                ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                                : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                            }`}
                          >
                            <span>{opt.label}</span>
                            <span>{active ? '开' : '关'}</span>
                          </button>
                        );
                      })}
                    </div>
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        硬件采集刷新频率
                      </span>
                      <div className="grid grid-cols-3 gap-2">
                        {[1, 2, 5].map((sec) => (
                          <button
                            key={sec}
                            type="button"
                            onClick={() =>
                              setDraftConfig({ ...draftConfig, refreshSeconds: sec })
                            }
                            className={`rounded-xl py-2 font-mono-tabular text-[12px] font-semibold transition-colors ${
                              (draftConfig.refreshSeconds ?? 2) === sec
                                ? 'bg-[#5E45F8] text-white'
                                : 'bg-[#F5F4FA] text-[#56526B]'
                            }`}
                          >
                            每 {sec} 秒
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-audio' && (
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <button
                      type="button"
                      onClick={() =>
                        setDraftConfig({
                          ...draftConfig,
                          showTrackName: !(draftConfig.showTrackName !== false),
                        })
                      }
                      className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                        draftConfig.showTrackName !== false
                          ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                          : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                      }`}
                    >
                      <span>显示正在播放曲目</span>
                      <span>{draftConfig.showTrackName !== false ? '开' : '关'}</span>
                    </button>

                    <div className="flex items-center justify-between rounded-xl bg-[#F5F4FA] p-1">
                      {[12, 20, 28].map((count) => (
                        <button
                          key={count}
                          type="button"
                          onClick={() => setDraftConfig({ ...draftConfig, barCount: count })}
                          className={`flex-1 rounded-lg py-1.5 font-mono-tabular text-[12px] font-semibold transition-colors ${
                            (draftConfig.barCount ?? 20) === count
                              ? 'bg-white text-[#5E45F8] shadow-sm'
                              : 'text-[#6B6880]'
                          }`}
                        >
                          {count} 频段
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-pomodoro' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          专注时长
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {[15, 25, 45].map((mins) => (
                            <button
                              key={mins}
                              type="button"
                              onClick={() =>
                                setDraftConfig({ ...draftConfig, focusMinutes: mins })
                              }
                              className={`rounded-xl py-2 font-mono-tabular text-[12px] font-semibold transition-colors ${
                                (draftConfig.focusMinutes ?? 25) === mins
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {mins} 分钟
                            </button>
                          ))}
                        </div>
                      </div>

                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          计时器实时控制
                        </span>
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() => onPomodoroAction('toggle')}
                            className="flex flex-1 items-center justify-center gap-1.5 rounded-xl bg-[#5E45F8] py-2 text-[12px] font-semibold text-white hover:bg-[#4D34E6]"
                          >
                            {pomodoroState.running ? (
                              <>
                                <Pause className="h-3.5 w-3.5" />
                                <span>暂停</span>
                              </>
                            ) : (
                              <>
                                <Play className="h-3.5 w-3.5" />
                                <span>开始专注</span>
                              </>
                            )}
                          </button>
                          <button
                            type="button"
                            onClick={() => onPomodoroAction('reset')}
                            className="rounded-xl bg-[#F5F4FA] px-3 py-2 text-[12px] font-semibold text-[#56526B] hover:bg-[#ECE9F8]"
                          >
                            重置
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-weather' && (
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        监测城市
                      </label>
                      <input
                        type="text"
                        maxLength={20}
                        value={draftConfig.city ?? '上海'}
                        onChange={(e) =>
                          setDraftConfig({ ...draftConfig, city: e.target.value })
                        }
                        placeholder="输入城市名称，如 上海"
                        className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                      />
                    </div>
                    <div className="flex items-end">
                      <button
                        type="button"
                        onClick={() =>
                          setDraftConfig({
                            ...draftConfig,
                            showAqi: !(draftConfig.showAqi !== false),
                          })
                        }
                        className={`flex w-full items-center justify-between rounded-xl border px-3.5 py-2 text-[12.5px] font-semibold transition-colors ${
                          draftConfig.showAqi !== false
                            ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                            : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                        }`}
                      >
                        <span>显示空气质量 (AQI)</span>
                        <span>{draftConfig.showAqi !== false ? '开' : '关'}</span>
                      </button>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-sticky' && (
                  <div>
                    <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                      副屏常驻便签文案
                    </label>
                    <input
                      type="text"
                      maxLength={60}
                      value={draftConfig.noteText ?? ''}
                      onChange={(e) =>
                        setDraftConfig({ ...draftConfig, noteText: e.target.value })
                      }
                      placeholder="输入今日重点或桌面座右铭..."
                      className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2.5 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                    />
                  </div>
                )}
                {editingPlugin.id === 'plg-craft-aquarium' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          热带鱼群数量
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {[4, 7, 12].map((count) => (
                            <button
                              key={count}
                              type="button"
                              onClick={() =>
                                setDraftConfig({ ...draftConfig, fishCount: count })
                              }
                              className={`rounded-xl py-2 font-mono-tabular text-[12px] font-semibold transition-colors ${
                                (draftConfig.fishCount ?? 7) === count
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {count} 条
                            </button>
                          ))}
                        </div>
                      </div>

                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          气泡上浮速率
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {[
                            { val: 0.6, label: '柔和' },
                            { val: 1.0, label: '标准' },
                            { val: 1.6, label: '活跃' },
                          ].map((sp) => (
                            <button
                              key={sp.val}
                              type="button"
                              onClick={() =>
                                setDraftConfig({ ...draftConfig, bubbleSpeed: sp.val })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.bubbleSpeed ?? 1.0) === sp.val
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {sp.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>

                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          水体焦散光效强度
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {[
                            { val: 0.3, label: '微弱' },
                            { val: 0.6, label: '适中' },
                            { val: 1.0, label: '通透' },
                          ].map((c) => (
                            <button
                              key={c.val}
                              type="button"
                              onClick={() =>
                                setDraftConfig({ ...draftConfig, causticsIntensity: c.val })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.causticsIntensity ?? 0.6) === c.val
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {c.label}
                            </button>
                          ))}
                        </div>
                      </div>

                      <div className="flex items-end">
                        <button
                          type="button"
                          onClick={() =>
                            setDraftConfig({
                              ...draftConfig,
                              showAquariumClock: !(draftConfig.showAquariumClock !== false),
                            })
                          }
                          className={`flex w-full items-center justify-between rounded-xl border px-3.5 py-2 text-[12.5px] font-semibold transition-colors ${
                            draftConfig.showAquariumClock !== false
                              ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                              : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                          }`}
                        >
                          <span>显示水面悬浮时钟</span>
                          <span>{draftConfig.showAquariumClock !== false ? '开' : '关'}</span>
                        </button>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-stocks' && (
                  <div>
                    <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                      默认关注金融标的
                    </span>
                    <div className="grid grid-cols-4 gap-2">
                      {(['NVDA', 'AAPL', 'BTC', 'ETH'] as const).map((sym) => (
                        <button
                          key={sym}
                          type="button"
                          onClick={() =>
                            setDraftConfig({ ...draftConfig, stockSymbol: sym })
                          }
                          className={`rounded-xl py-2 font-mono-tabular text-[12px] font-bold transition-colors ${
                            (draftConfig.stockSymbol ?? 'NVDA') === sym
                              ? 'bg-[#5E45F8] text-white'
                              : 'bg-[#F5F4FA] text-[#56526B]'
                          }`}
                        >
                          {sym}
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-lyrics' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          曲目标题
                        </label>
                        <input
                          type="text"
                          maxLength={40}
                          value={draftConfig.songTitle ?? 'Midnight City'}
                          onChange={(e) =>
                            setDraftConfig({ ...draftConfig, songTitle: e.target.value })
                          }
                          className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          歌手 / 艺人
                        </label>
                        <input
                          type="text"
                          maxLength={40}
                          value={draftConfig.artist ?? 'M83'}
                          onChange={(e) =>
                            setDraftConfig({ ...draftConfig, artist: e.target.value })
                          }
                          className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                    </div>
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        霓虹频谱柱密度
                      </span>
                      <div className="grid grid-cols-3 gap-2">
                        {[16, 20, 28].map((bars) => (
                          <button
                            key={bars}
                            type="button"
                            onClick={() =>
                              setDraftConfig({ ...draftConfig, barCount: bars })
                            }
                            className={`rounded-xl py-2 font-mono-tabular text-[12px] font-semibold transition-colors ${
                              (draftConfig.barCount ?? 20) === bars
                                ? 'bg-[#5E45F8] text-white'
                                : 'bg-[#F5F4FA] text-[#56526B]'
                            }`}
                          >
                            {bars} 频段
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-nixie' && (
                  <div className="space-y-3">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        辉光管灯丝色温
                      </span>
                      <div className="grid grid-cols-4 gap-2">
                        {(
                          [
                            { id: 'amber', label: '经典琥珀' },
                            { id: 'cyan', label: '赛博青蓝' },
                            { id: 'green', label: '荧光翠绿' },
                            { id: 'white', label: '纯净冷白' },
                          ] as const
                        ).map((hue) => (
                          <button
                            key={hue.id}
                            type="button"
                            onClick={() =>
                              setDraftConfig({ ...draftConfig, nixieHue: hue.id })
                            }
                            className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                              (draftConfig.nixieHue ?? 'amber') === hue.id
                                ? 'bg-[#5E45F8] text-white'
                                : 'bg-[#F5F4FA] text-[#56526B]'
                            }`}
                          >
                            {hue.label}
                          </button>
                        ))}
                      </div>
                    </div>
                    <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                      {[
                        { key: 'showSeconds' as const, label: '显示秒数管' },
                        { key: 'showDate' as const, label: '显示底部日期' },
                        { key: 'use24Hour' as const, label: '24 小时制' },
                      ].map((opt) => {
                        const active = draftConfig[opt.key] !== false;
                        return (
                          <button
                            key={opt.key}
                            type="button"
                            onClick={() =>
                              setDraftConfig({ ...draftConfig, [opt.key]: !active })
                            }
                            className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                              active
                                ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                                : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                            }`}
                          >
                            <span>{opt.label}</span>
                            <span>{active ? '开' : '关'}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-cyber-pet' && (
                  <div className="space-y-3">
                    <div>
                      <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        萌宠昵称
                      </label>
                      <input
                        type="text"
                        maxLength={20}
                        value={draftConfig.petName ?? 'Mochi'}
                        onChange={(e) =>
                          setDraftConfig({ ...draftConfig, petName: e.target.value })
                        }
                        className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                      />
                    </div>
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          宠物形态
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'mochi', label: '赛博猫耳' },
                              { id: 'pixel-bot', label: '像素机甲' },
                              { id: 'shiba', label: '柴犬伙伴' },
                            ] as const
                          ).map((sp) => (
                            <button
                              key={sp.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, petSpecies: sp.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.petSpecies ?? 'mochi') === sp.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {sp.label}
                            </button>
                          ))}
                        </div>
                      </div>
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          当前心情状态
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'happy', label: '活力' },
                              { id: 'chill', label: '打盹' },
                              { id: 'focus', label: '专注' },
                            ] as const
                          ).map((m) => (
                            <button
                              key={m.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, petMood: m.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.petMood ?? 'happy') === m.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {m.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-campfire' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          火焰大小强度
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'low', label: '微火' },
                              { id: 'medium', label: '适中' },
                              { id: 'high', label: '旺火' },
                            ] as const
                          ).map((f) => (
                            <button
                              key={f.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, flameIntensity: f.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.flameIntensity ?? 'medium') === f.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {f.label}
                            </button>
                          ))}
                        </div>
                      </div>
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          余烬火星密度
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'low', label: '稀疏' },
                              { id: 'medium', label: '适中' },
                              { id: 'high', label: '密集' },
                            ] as const
                          ).map((e) => (
                            <button
                              key={e.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, emberDensity: e.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.emberDensity ?? 'medium') === e.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {e.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() =>
                        setDraftConfig({
                          ...draftConfig,
                          showSoundHint: !(draftConfig.showSoundHint !== false),
                        })
                      }
                      className={`flex w-full items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                        draftConfig.showSoundHint !== false
                          ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                          : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                      }`}
                    >
                      <span>显示白噪音氛围标签</span>
                      <span>{draftConfig.showSoundHint !== false ? '开' : '关'}</span>
                    </button>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-bonsai' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          盆栽植物品种
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'pine', label: '青松' },
                              { id: 'succulent', label: '多肉' },
                              { id: 'sakura', label: '春樱' },
                            ] as const
                          ).map((b) => (
                            <button
                              key={b.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, bonsaiSpecies: b.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.bonsaiSpecies ?? 'pine') === b.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {b.label}
                            </button>
                          ))}
                        </div>
                      </div>
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          光照氛围色调
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'warm', label: '暖阳' },
                              { id: 'cool', label: '月光' },
                              { id: 'mist', label: '微雾' },
                            ] as const
                          ).map((t) => (
                            <button
                              key={t.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, sunlightTone: t.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.sunlightTone ?? 'warm') === t.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {t.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-flip-clock' && (
                  <div className="space-y-3">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        机械外壳质感
                      </span>
                      <div className="grid grid-cols-3 gap-2">
                        {(
                          [
                            { id: 'obsidian', label: '曜石炭黑' },
                            { id: 'warmwood', label: '暖木胡桃' },
                            { id: 'titanium', label: '钛银拉丝' },
                          ] as const
                        ).map((sh) => (
                          <button
                            key={sh.id}
                            type="button"
                            onClick={() => setDraftConfig({ ...draftConfig, flipShellStyle: sh.id })}
                            className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                              (draftConfig.flipShellStyle ?? 'obsidian') === sh.id
                                ? 'bg-[#5E45F8] text-white'
                                : 'bg-[#F5F4FA] text-[#56526B]'
                            }`}
                          >
                            {sh.label}
                          </button>
                        ))}
                      </div>
                    </div>
                    <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                      {[
                        { key: 'showSeconds' as const, label: '显示秒数牌' },
                        { key: 'showDate' as const, label: '显示底部日期' },
                        { key: 'use24Hour' as const, label: '24 小时制' },
                      ].map((opt) => {
                        const active = draftConfig[opt.key] !== false;
                        return (
                          <button
                            key={opt.key}
                            type="button"
                            onClick={() => setDraftConfig({ ...draftConfig, [opt.key]: !active })}
                            className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                              active
                                ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                                : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                            }`}
                          >
                            <span>{opt.label}</span>
                            <span>{active ? '开' : '关'}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-vu-meter' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          表盘背光色温
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'warm-amber', label: '暖黄' },
                              { id: 'emerald', label: '翠绿' },
                              { id: 'ice-blue', label: '冰蓝' },
                            ] as const
                          ).map((bg) => (
                            <button
                              key={bg.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, vuBacklight: bg.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.vuBacklight ?? 'warm-amber') === bg.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {bg.label}
                            </button>
                          ))}
                        </div>
                      </div>
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          指针阻尼灵敏度
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'smooth', label: '舒缓' },
                              { id: 'balanced', label: '均衡' },
                              { id: 'punchy', label: '灵敏' },
                            ] as const
                          ).map((sv) => (
                            <button
                              key={sv.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, vuSensitivity: sv.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.vuSensitivity ?? 'balanced') === sv.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {sv.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-cassette' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          磁带手写标签标题
                        </label>
                        <input
                          type="text"
                          maxLength={32}
                          value={draftConfig.songTitle ?? 'City Pop Tape #04'}
                          onChange={(e) => setDraftConfig({ ...draftConfig, songTitle: e.target.value })}
                          className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          卡带透明外壳
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: 'smoke', label: '烟熏黑' },
                              { id: 'amber', label: '琥珀' },
                              { id: 'indigo', label: '靛蓝' },
                            ] as const
                          ).map((sh) => (
                            <button
                              key={sh.id}
                              type="button"
                              onClick={() => setDraftConfig({ ...draftConfig, cassetteShell: sh.id })}
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.cassetteShell ?? 'smoke') === sh.id
                                  ? 'bg-[#5E45F8] text-white'
                                  : 'bg-[#F5F4FA] text-[#56526B]'
                              }`}
                            >
                              {sh.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-zodiac' && (
                  <div className="space-y-3">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        选择专属星座
                      </span>
                      <div className="grid grid-cols-4 gap-1.5 sm:grid-cols-6">
                        {(
                          [
                            { id: 'aries', label: '白羊' },
                            { id: 'taurus', label: '金牛' },
                            { id: 'gemini', label: '双子' },
                            { id: 'cancer', label: '巨蟹' },
                            { id: 'leo', label: '狮子' },
                            { id: 'virgo', label: '处女' },
                            { id: 'libra', label: '天秤' },
                            { id: 'scorpio', label: '天蝎' },
                            { id: 'sagittarius', label: '射手' },
                            { id: 'capricorn', label: '摩羯' },
                            { id: 'aquarius', label: '水瓶' },
                            { id: 'pisces', label: '双鱼' },
                          ] as const
                        ).map((z) => (
                          <button
                            key={z.id}
                            type="button"
                            onClick={() => setDraftConfig({ ...draftConfig, zodiacSign: z.id })}
                            className={`rounded-xl py-1.5 text-[12px] font-semibold transition-colors ${
                              (draftConfig.zodiacSign ?? 'aquarius') === z.id
                                ? 'bg-[#5E45F8] text-white'
                                : 'bg-[#F5F4FA] text-[#56526B]'
                            }`}
                          >
                            {z.label}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-parcel' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          包裹备注名称
                        </label>
                        <input
                          type="text"
                          maxLength={32}
                          value={draftConfig.parcelName ?? '客制化机械键盘套件'}
                          onChange={(e) => setDraftConfig({ ...draftConfig, parcelName: e.target.value })}
                          className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          承运物流商
                        </label>
                        <input
                          type="text"
                          maxLength={20}
                          value={draftConfig.carrier ?? '顺丰速运'}
                          onChange={(e) => setDraftConfig({ ...draftConfig, carrier: e.target.value })}
                          className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                    </div>
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        当前物流进度节点
                      </span>
                      <div className="grid grid-cols-4 gap-1.5">
                        {(
                          [
                            { id: 'collected', label: '已揽收' },
                            { id: 'transit', label: '干线中' },
                            { id: 'delivering', label: '派送中' },
                            { id: 'delivered', label: '已签收' },
                          ] as const
                        ).map((st) => (
                          <button
                            key={st.id}
                            type="button"
                            onClick={() => setDraftConfig({ ...draftConfig, parcelStage: st.id })}
                            className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                              (draftConfig.parcelStage ?? 'delivering') === st.id
                                ? 'bg-[#5E45F8] text-white'
                                : 'bg-[#F5F4FA] text-[#56526B]'
                            }`}
                          >
                            {st.label}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-milestone' && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          纪念日 / 目标标题
                        </label>
                        <input
                          type="text"
                          maxLength={24}
                          value={draftConfig.milestoneTitle ?? '年度版本发布'}
                          onChange={(e) => setDraftConfig({ ...draftConfig, milestoneTitle: e.target.value })}
                          className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2 text-[13px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                        />
                      </div>
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          计数方向与天数
                        </span>
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() =>
                              setDraftConfig({
                                ...draftConfig,
                                countMode: (draftConfig.countMode ?? 'countdown') === 'countdown' ? 'countup' : 'countdown',
                              })
                            }
                            className="rounded-xl bg-[#F5F4FA] px-3 py-2 text-[12px] font-semibold text-[#5E45F8]"
                          >
                            {(draftConfig.countMode ?? 'countdown') === 'countdown' ? '倒数模式' : '正数纪念'}
                          </button>
                          <input
                            type="number"
                            min={0}
                            max={9999}
                            value={draftConfig.targetDays ?? 18}
                            onChange={(e) =>
                              setDraftConfig({ ...draftConfig, targetDays: Number(e.target.value) || 0 })
                            }
                            className="w-24 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 font-mono-tabular text-[13px] font-bold text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                          />
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === 'plg-craft-netradar' && (
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        速率显示单位
                      </span>
                      <div className="grid grid-cols-2 gap-1.5">
                        {(['Mbps', 'MB/s'] as const).map((u) => (
                          <button
                            key={u}
                            type="button"
                            onClick={() => setDraftConfig({ ...draftConfig, unitMode: u })}
                            className={`rounded-xl py-2 font-mono-tabular text-[12px] font-semibold transition-colors ${
                              (draftConfig.unitMode ?? 'Mbps') === u
                                ? 'bg-[#5E45F8] text-white'
                                : 'bg-[#F5F4FA] text-[#56526B]'
                            }`}
                          >
                            {u}
                          </button>
                        ))}
                      </div>
                    </div>
                    <div className="flex items-end">
                      <button
                        type="button"
                        onClick={() =>
                          setDraftConfig({
                            ...draftConfig,
                            showPingChart: !(draftConfig.showPingChart !== false),
                          })
                        }
                        className={`flex w-full items-center justify-between rounded-xl border px-3.5 py-2 text-[12.5px] font-semibold transition-colors ${
                          draftConfig.showPingChart !== false
                            ? 'border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]'
                            : 'border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]'
                        }`}
                      >
                        <span>显示实时带宽折线图</span>
                        <span>{draftConfig.showPingChart !== false ? '开' : '关'}</span>
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* Plugin Description */}
              <div className="border-t border-[#F1EFF8] pt-4">
                <div className="text-[12.5px] font-bold text-[#1A172B]">插件介绍</div>
                <p className="mt-1.5 text-[12.5px] leading-relaxed text-[#6B6880]">
                  {editingPlugin.description}
                </p>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-[#F1EFF8] bg-[#FAF9FD] px-6 py-3.5">
              <button
                type="button"
                onClick={() => {
                  onResetPluginConfig(editingPlugin.id);
                  closeConfigModal();
                }}
                className="flex items-center gap-1.5 text-[12.5px] font-semibold text-[#6B6880] transition-colors hover:text-[#1A172B]"
              >
                <RotateCcw className="h-3.5 w-3.5" />
                <span>恢复默认参数</span>
              </button>

              <div className="flex items-center gap-2.5">
                <button
                  type="button"
                  onClick={closeConfigModal}
                  className="rounded-xl border border-[#E5E2F4] bg-white px-4 py-2 text-[12.5px] font-semibold text-[#56526B] hover:bg-[#F5F4FA]"
                >
                  取消
                </button>
                {!editingPlugin.installed || !editingPlugin.enabled ? (
                  <button
                    type="button"
                    onClick={() => handleSaveConfig(true)}
                    className="rounded-xl bg-[#5E45F8] px-4 py-2 text-[12.5px] font-semibold text-white hover:bg-[#4D34E6]"
                  >
                    {editingPlugin.installed ? '保存并启用' : '安装并应用配置'}
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => handleSaveConfig(false)}
                    className="rounded-xl bg-[#5E45F8] px-4 py-2 text-[12.5px] font-semibold text-white hover:bg-[#4D34E6]"
                  >
                    保存配置
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
