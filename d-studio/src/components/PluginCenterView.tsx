import {
  desktopMode,
  previewCraftPlugin,
  craftSettings,
  configureCraft,
  applyCraft,
  craftCover,
  removeCraft,
  importCraftPlugin,
  listCraftPlugins,
  type CraftSettings,
  type CraftPluginEntry,
} from "../services/desktopBridge";
import React, { useEffect, useMemo, useState } from "react";
import { Check, Pause, Play, RotateCcw, Trash2, Puzzle, X } from "lucide-react";
import {
  DeviceInfo,
  PluginConfig,
  PluginItem,
  ScreenOrientation,
  ThemeItem,
} from "../data/themes";
import { COMPONENT_PLUGIN_IDS, canOutputPlugin } from "../data/pluginCatalog";
import { PluginThumbnailPreview, PomodoroRuntimeState } from "./PluginWidgets";

interface PluginCenterViewProps {
  plugins: PluginItem[];
  onApplyBuiltin: (
    plugin: PluginItem,
    config: PluginConfig,
  ) => Promise<boolean>;
  pureMode: boolean;
  activeTheme: ThemeItem;
  activeDevice: DeviceInfo;
  devices: DeviceInfo[];
  pomodoroState: PomodoroRuntimeState;
  onOrientationChange: (orientation: ScreenOrientation) => void;
  onTogglePureMode: () => void;
  onInstallPlugin: (id: string, enableAfterInstall: boolean) => void;
  onTogglePlugin: (id: string) => void;
  onUninstallPlugin: (id: string) => void;
  onUpdatePluginConfig: (
    id: string,
    nextConfig: PluginConfig,
    enableIfNeeded?: boolean,
  ) => void;
  onResetPluginConfig: (id: string) => void;
  onPomodoroAction: (action: "toggle" | "reset") => void;
}

const categories = ['全部', '时间组件', '系统监控', '媒体联动', '效率工具', '生活资讯', '互动娱乐', '其他'];
const statuses = ['全部', '已启用', '已安装', '未安装'];
const builtinThemeId = (pluginId: string, device: DeviceInfo) =>
  'plugin-scene-' + pluginId + '-' + device.id + '-' + device.orientation;

function importedCategory(plugin: CraftPluginEntry) {
  const text = plugin.Name + ' ' + (plugin.Description ?? '');
  if (/时钟|clock/i.test(text)) return '时间组件';
  if (/性能|monitor|telemetry/i.test(text)) return '系统监控';
  if (/音频|歌词|audio|music/i.test(text)) return '媒体联动';
  if (/便签|计时|timer|notes/i.test(text)) return '效率工具';
  if (/天气|股票|weather|stock/i.test(text)) return '生活资讯';
  if (/鱼缸|水族|aquarium|game/i.test(text)) return '互动娱乐';
  return '其他';
}

const ACCENT_SWATCHES = [
  { hex: "#5E45F8", label: "极光紫" },
  { hex: "#38BDF8", label: "冰晶蓝" },
  { hex: "#10B981", label: "翡翠绿" },
  { hex: "#F59E0B", label: "琥珀金" },
  { hex: "#EC4899", label: "绯樱粉" },
];

export const PluginCenterView: React.FC<PluginCenterViewProps> = ({
  plugins,
  onApplyBuiltin,
  activeTheme,
  activeDevice,
  devices,
  onInstallPlugin,
  onUninstallPlugin,
  pomodoroState,
  onUpdatePluginConfig,
  onResetPluginConfig,
  onPomodoroAction,
}) => {
  const [craftPlugins, setCraftPlugins] = useState<CraftPluginEntry[]>([]);
  const [craftMessage, setCraftMessage] = useState("");
  const [craftBusy, setCraftBusy] = useState(false);
  const [craftEdit, setCraftEdit] = useState<{
    id: string;
    values: CraftSettings;
  } | null>(null);
  async function runCraft(action: () => Promise<void>) {
    setCraftBusy(true);
    setCraftMessage("");
    try {
      await action();
    } catch (error) {
      setCraftMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setCraftBusy(false);
    }
  }
  const [craftImporting, setCraftImporting] = useState(false);
  useEffect(() => {
    if (!desktopMode) return;
    let active = true;
    listCraftPlugins()
      .then((entries) => {
        if (active) setCraftPlugins(entries);
      })
      .catch((error) => {
        if (active) setCraftMessage(String(error));
      });
    return () => {
      active = false;
    };
  }, []);
  const handleCraftImport = async () => {
    setCraftImporting(true);
    setCraftMessage("");
    try {
      const result = await importCraftPlugin();
      if (result) {
        setCraftPlugins(await listCraftPlugins());
        setSearchQuery(result.Entry.Name);
        setStatus('全部');
        setCategory('全部');
        setCraftMessage(
          result.AlreadyImported
            ? `${result.Entry.Name} 已在插件列表中`
            : `${result.Entry.Name} 导入成功`,
        );
      }
    } catch (error) {
      setCraftMessage(error instanceof Error ? error.message : "导入失败");
    } finally {
      setCraftImporting(false);
    }
  };
  const [searchQuery, setSearchQuery] = useState("");
  const [status, setStatus] = useState('全部');
  const [category, setCategory] = useState('全部');
  const [covers, setCovers] = useState<Record<string, string>>({});
  const [pendingDelete, setPendingDelete] = useState<{name: string; remove: () => void} | null>(null);
  const coverScope = activeDevice.model + ':' + activeDevice.orientation;
  const targetSerial = activeDevice.serial;
  useEffect(() => {
    let current = true;
    async function loadCovers() {
      for (const plugin of craftPlugins) {
        try {
          const cover = await craftCover(plugin.Id, targetSerial);
          if (current) setCovers(previous => ({ ...previous, [coverScope + ':' + plugin.Id]: cover }));
        } catch {
          // The live preview remains available if the initial frame cannot be cached.
        }
        if (!current) break;
      }
    }
    void loadCovers();
    return () => { current = false; };
  }, [craftPlugins, coverScope, targetSerial]);

  // Config / Preview Modal state
  const [editingPluginId, setEditingPluginId] = useState<string | null>(null);
  const [draftConfig, setDraftConfig] = useState<PluginConfig | null>(null);
  const [modalBackdrop, setModalBackdrop] = useState<"theme" | "dark">("theme");

  const isPortrait = activeDevice.orientation === "portrait";
  const previewAspectRatio = isPortrait
    ? `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`
    : `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`;

  const [applying, setApplying] = useState(false);
  const isPlaying = (themeId: string) =>
    activeDevice.themeId === themeId && activeDevice.status !== '离线' && activeDevice.playing === true;
  const matches = (name: string, author: string, group: string, installed: boolean, playing: boolean) =>
    (category === '全部' || category === group) &&
    (status === '全部' || (status === '已启用' && playing) ||
      (status === '已安装' && installed) || (status === '未安装' && !installed)) &&
    (name + ' ' + author + ' ' + group).toLowerCase().includes(searchQuery.trim().toLowerCase());

  const visibleCraft = craftPlugins.filter(plugin =>
    matches(plugin.Name, plugin.Author, importedCategory(plugin), true, isPlaying('craft-plugin:' + plugin.Id)));
  const filteredPlugins = plugins.filter(plugin => !COMPONENT_PLUGIN_IDS.has(plugin.id) &&
    matches(plugin.name, plugin.author, plugin.category, plugin.installed, isPlaying(builtinThemeId(plugin.id, activeDevice))));

  const enableBuiltin = async (plugin: PluginItem, config = plugin.config) => {
    setApplying(true);
    setCraftMessage('');
    try {
      const applied = await onApplyBuiltin(plugin, config);
      if (applied) onUpdatePluginConfig(plugin.id, config, false);
      return applied;
    } catch (error) {
      setCraftMessage(error instanceof Error ? error.message : String(error));
      return false;
    } finally { setApplying(false); }
  };
  const enableCraft = async (pluginId: string) => {
    if (!await applyCraft(pluginId, targetSerial)) throw new Error('设备未确认输出。');
    setCraftMessage('已应用到 ' + activeDevice.name);
  };
  const unavailable = activeDevice.status === '离线' || (desktopMode && !targetSerial);
  const buttonClass = 'rounded-xl bg-[#F3F0FF] px-3 py-2 text-sm text-[#5E45F8] disabled:opacity-50';
  const primaryClass = 'rounded-xl bg-[#6244ff] px-3 py-2 text-sm text-white disabled:opacity-50';
  const deleteClass = 'rounded-xl p-2 text-[#7A7690] hover:bg-red-50 hover:text-red-600 disabled:opacity-50';
  const currentlyUsed = (themeId: string) => devices.find(device => device.themeId === themeId && device.wantsPlaying);

  const editingPlugin = useMemo(
    () => plugins.find((p) => p.id === editingPluginId) ?? null,
    [plugins, editingPluginId],
  );

  const openConfigModal = (plg: PluginItem) => {
    setEditingPluginId(plg.id);
    setDraftConfig({ ...plg.config });
  };

  const closeConfigModal = () => {
    setEditingPluginId(null);
    setDraftConfig(null);
  };

  const handleSaveConfig = () => {
    if (!editingPlugin || !draftConfig) return;
    onUpdatePluginConfig(editingPlugin.id, draftConfig, false);
    closeConfigModal();
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-[24px] bg-white p-5">
        <div>
          <h2 className="text-xl font-bold text-[#1A172B]">我的插件</h2>
          <p className="mt-1 text-sm text-[#7A7690]">
            {activeDevice.name} · {isPortrait ? "竖屏" : "横屏"} ·{" "}
            {activeDevice.resolution}
          </p>
        </div>
        {desktopMode && (
          <button
            disabled={craftImporting || craftBusy}
            className="rounded-xl bg-[#6244ff] px-4 py-2 text-sm text-white disabled:opacity-50"
            onClick={handleCraftImport}
          >
            {craftImporting ? "正在导入…" : "导入插件"}
          </button>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-3 rounded-[24px] bg-white p-4">
        {[
          { label: '安装状态', items: statuses, value: status, select: setStatus },
          { label: '插件分类', items: categories, value: category, select: setCategory },
        ].map(filter => (
          <div key={filter.label} role="group" aria-label={filter.label} className="flex flex-wrap gap-1 rounded-xl bg-[#F3F0FA] p-1">
            {filter.items.map(item => <button key={item} type="button" aria-pressed={filter.value === item}
              onClick={() => filter.select(item)}
              className={'rounded-lg px-2.5 py-1.5 text-xs ' + (filter.value === item ? 'bg-white font-semibold text-[#5E45F8] shadow-sm' : 'text-[#59546E]')}>
              {item}
            </button>)}
          </div>
        ))}
        <input aria-label="搜索插件" placeholder="搜索插件名称、分类或作者…" value={searchQuery}
          onChange={event => setSearchQuery(event.target.value)}
          className="ml-auto rounded-xl border border-[#E5E2F4] bg-[#FAF9FD] px-4 py-2 text-sm" />
      </div>
      {craftMessage && (
        <p role="status" className="text-sm text-[#6244ff]">
          {craftMessage}
        </p>
      )}
      {filteredPlugins.length + visibleCraft.length === 0 && (
        <p className="py-12 text-center text-[#7A7690]">没有符合条件的插件</p>
      )}
      <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-3">
        {visibleCraft.map(plugin => {
          const themeId = 'craft-plugin:' + plugin.Id;
          const playing = isPlaying(themeId);
          const cover = covers[coverScope + ':' + plugin.Id];
          const open = () => runCraft(async () => setCraftEdit({ id: plugin.Id, values: await craftSettings(plugin.Id) }));
          return (
            <article key={plugin.Id} className="flex flex-col rounded-[24px] border border-[#E5E2F4] bg-white p-4">
              <button type="button" aria-label={'预览并配置 ' + plugin.Name} disabled={craftBusy} onClick={open}
                className="grid aspect-[16/7] w-full place-items-center overflow-hidden rounded-xl bg-[#F3F0FF] text-[#7A7690]">
                {cover ? <img src={cover} alt={plugin.Name + '预览图'} className="h-full w-full object-contain" />
                  : <Puzzle size={28} aria-hidden />}
              </button>
              <p className="mt-3 text-xs text-[#7A7690]">{importedCategory(plugin)} · {plugin.Format ?? 'Craft'} · {plugin.Version} · {plugin.Author} · {playing ? '已启用' : '已安装'}</p>
              <h3 className="mt-1 font-bold text-[#1A172B]">{plugin.Name}</h3>
              <div className="mt-auto flex items-center gap-2 pt-4">
                <button className={buttonClass} disabled={craftBusy} onClick={open}>参数配置</button>
                <button className={deleteClass} aria-label={'删除 ' + plugin.Name} disabled={craftBusy} onClick={() => {
                  const device = currentlyUsed(themeId);
                  if (device) { setCraftMessage('请先在 ' + device.name + ' 上更换内容，再删除此插件。'); return; }
                  setPendingDelete({ name: plugin.Name, remove: () => void runCraft(async () => {
                    await removeCraft(plugin.Id);
                    setCraftPlugins(previous => previous.filter(item => item.Id !== plugin.Id));
                    setCraftMessage('已删除 ' + plugin.Name);
                  }) });
                }}><Trash2 size={15} /></button>
                <button className={primaryClass + ' ml-auto'} disabled={craftBusy || applying || unavailable || playing}
                  title={unavailable ? '设备已离线' : undefined} onClick={() => runCraft(() => enableCraft(plugin.Id))}>
                  {playing ? '已启用' : '启用插件'}
                </button>
              </div>
            </article>
          );
        })}
        {filteredPlugins.map(plugin => {
          const playing = isPlaying(builtinThemeId(plugin.id, activeDevice));
          const canOutput = canOutputPlugin(plugin);
          return (
            <article key={plugin.id} className="flex flex-col overflow-hidden rounded-[24px] border border-[#E5E2F4] bg-white p-4">
              <button className="block aspect-[16/7] w-full overflow-hidden rounded-xl bg-[#10101A]" aria-label={'预览并配置 ' + plugin.name} onClick={() => openConfigModal(plugin)}>
                <PluginThumbnailPreview plugin={{ ...plugin, config: { ...plugin.config, position: 'full' } }}
                  pomodoroState={pomodoroState} aspectRatio={previewAspectRatio} className="h-full w-full" />
              </button>
              <p className="mt-3 text-xs text-[#7A7690]">{plugin.category} · {plugin.version} · {plugin.author} · {playing ? '已启用' : plugin.installed ? '已安装' : '未安装'}</p>
              <h3 className="mt-1 font-bold text-[#1A172B]">{plugin.name}</h3>
              <div className="mt-auto flex items-center gap-2 pt-4">
                <button className={buttonClass} onClick={() => openConfigModal(plugin)}>参数配置</button>
                {plugin.installed && !plugin.core && <button className={deleteClass} aria-label={'卸载 ' + plugin.name} disabled={applying} onClick={() => {
                  const device = devices.find(item => item.themeId === builtinThemeId(plugin.id, item) && item.wantsPlaying);
                  if (device) { setCraftMessage('请先在 ' + device.name + ' 上更换内容，再卸载此插件。'); return; }
                  setPendingDelete({ name: plugin.name, remove: () => onUninstallPlugin(plugin.id) });
                }}><Trash2 size={15} /></button>}
                <button className={primaryClass + ' ml-auto'}
                  disabled={applying || craftBusy || playing || (plugin.installed && (unavailable || !canOutput))}
                  title={plugin.installed && !canOutput ? '目前仅支持预览' : unavailable ? '设备已离线' : undefined}
                  onClick={() => plugin.installed ? void enableBuiltin(plugin) : onInstallPlugin(plugin.id, false)}>
                  {playing ? '已启用' : plugin.installed ? '启用插件' : '安装插件'}
                </button>
              </div>
            </article>
          );
        })}
      </div>
      {pendingDelete && <div className="fixed inset-0 z-50 grid place-items-center bg-[#151027]/50 p-5" role="dialog" aria-modal="true" aria-label="确认移除插件">
        <div className="w-full max-w-sm rounded-[24px] bg-white p-6">
          <h3 className="font-bold">移除「{pendingDelete.name}」？</h3>
          <div className="mt-5 flex justify-end gap-3">
            <button className={buttonClass} onClick={() => setPendingDelete(null)}>取消</button>
            <button className={primaryClass} onClick={() => { pendingDelete.remove(); setPendingDelete(null); }}>确认移除</button>
          </div>
        </div>
      </div>}
      {craftEdit && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#151027]/50 p-5"
          role="dialog"
          aria-modal="true"
          aria-label="插件预览与配置"
        >
          <div className="max-h-[85vh] w-full max-w-xl space-y-5 overflow-y-auto rounded-[24px] bg-white p-6">
            {craftMessage && <p role="status" className="text-sm text-[#6244ff]">{craftMessage}</p>}
            <div className="flex justify-between gap-4">
              <h3 className="font-bold">
                {
                  craftPlugins.find((plugin) => plugin.Id === craftEdit.id)
                    ?.Name
                }
              </h3>
              <button
                aria-label="关闭配置"
                disabled={craftBusy}
                onClick={() => setCraftEdit(null)}
              >
                <X size={18} />
              </button>
            </div>
            <p className="text-sm text-[#7A7690]">
              {activeDevice.name} · {activeDevice.resolution}
            </p>
            <button
              disabled={craftBusy}
              className="rounded-xl bg-[#F3F0FF] px-4 py-2 text-sm text-[#5E45F8]"
              onClick={() =>
                runCraft(async () => {
                  await previewCraftPlugin(
                    craftEdit.id,
                    activeDevice.serial,
                    craftEdit.values,
                  );
                })
              }
            >
              打开实时预览
            </button>
            {Object.entries(craftEdit.values).map(([key, value]) => (
              <label
                key={key}
                className="flex items-center justify-between gap-4 text-sm"
              >
                {key === "hour12" ? "使用12小时制" : key}
                <input
                  className="min-w-0 rounded-lg border border-[#E5E2F4] px-3 py-2"
                  type={
                    typeof value === "boolean"
                      ? "checkbox"
                      : typeof value === "number"
                        ? "number"
                        : "text"
                  }
                  checked={typeof value === "boolean" ? value : undefined}
                  value={typeof value === "boolean" ? undefined : value}
                  onChange={(event) => {
                    const next =
                      typeof value === "boolean"
                        ? event.target.checked
                        : typeof value === "number"
                          ? Number(event.target.value)
                          : event.target.value;
                    setCraftEdit({
                      ...craftEdit,
                      values: { ...craftEdit.values, [key]: next },
                    });
                  }}
                />
              </label>
            ))}
            {Object.keys(craftEdit.values).length === 0 && (
              <p className="text-sm text-[#7A7690]">此插件没有配置项。</p>
            )}
            {activeDevice.status === "离线" && (
              <p className="text-sm text-amber-700">
                设备已离线，连接后可应用。
              </p>
            )}
            <div className="flex flex-wrap gap-3">
              <button
                disabled={craftBusy}
                className="rounded-xl border border-[#E5E2F4] px-4 py-2 text-sm"
                onClick={() =>
                  runCraft(async () => {
                    await configureCraft(craftEdit.id, craftEdit.values);
                    setCraftMessage("插件配置已保存");
                  })
                }
              >
                保存配置
              </button>
              <button
                disabled={craftBusy || activeDevice.status === "离线"}
                className="rounded-xl bg-[#6244ff] px-4 py-2 text-sm text-white disabled:opacity-50"
                onClick={() =>
                  runCraft(async () => {
                    await configureCraft(craftEdit.id, craftEdit.values);
                    await applyCraft(craftEdit.id, activeDevice.serial);
                    setCraftMessage("已应用到 " + activeDevice.name);
                    setCraftEdit(null);
                  })
                }
              >
                应用到 {activeDevice.name}
              </button>
            </div>
          </div>
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
                  <span className="font-semibold text-[#5E45F8]">
                    {editingPlugin.category}
                  </span>
                  <span aria-hidden="true">·</span>
                  <span className="font-mono-tabular">
                    {editingPlugin.version}
                  </span>
                  <span aria-hidden="true">·</span>
                  <span>{editingPlugin.author}</span>
                </div>
                <h3
                  id="plugin-config-title"
                  className="mt-0.5 text-[18px] font-bold text-[#1A172B]"
                >
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
                      onClick={() => setModalBackdrop("theme")}
                      className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold transition-colors ${
                        modalBackdrop === "theme"
                          ? "bg-white text-[#1A172B]"
                          : "text-white/75"
                      }`}
                    >
                      当前壁纸
                    </button>
                    <button
                      type="button"
                      onClick={() => setModalBackdrop("dark")}
                      className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold transition-colors ${
                        modalBackdrop === "dark"
                          ? "bg-white text-[#1A172B]"
                          : "text-white/75"
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
                      isPortrait ? "w-[190px] max-w-full" : "w-full"
                    }`}
                  >
                    <PluginThumbnailPreview
                      plugin={{
                        ...editingPlugin,
                        config: { ...draftConfig, position: "full" },
                      }}
                      backdropTheme={
                        modalBackdrop === "theme" ? activeTheme : undefined
                      }
                      pomodoroState={pomodoroState}
                      aspectRatio={previewAspectRatio}
                      className="h-full w-full"
                    />
                  </div>
                </div>
              </div>

              <div>
                <label className="mb-2 block text-sm font-bold text-[#1A172B]">
                  主题高亮色
                </label>
                <div className="flex items-center gap-3">
                  {ACCENT_SWATCHES.map((swatch) => (
                    <button
                      key={swatch.hex}
                      type="button"
                      aria-label={"选择高亮色 " + swatch.label}
                      style={{ backgroundColor: swatch.hex }}
                      onClick={() =>
                        setDraftConfig({
                          ...draftConfig,
                          accentColor: swatch.hex,
                        })
                      }
                      className="flex h-8 w-8 items-center justify-center rounded-full"
                    >
                      {draftConfig.accentColor === swatch.hex && (
                        <Check className="h-4 w-4 text-white" />
                      )}
                    </button>
                  ))}
                </div>
              </div>

              {/* Opacity Slider */}
              <div>
                <div className="mb-1.5 flex items-center justify-between text-[12.5px]">
                  <span className="font-bold text-[#1A172B]">
                    毛玻璃底框不透明度
                  </span>
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
                    setDraftConfig({
                      ...draftConfig,
                      opacity: Number(e.target.value),
                    })
                  }
                  className="h-2 w-full cursor-pointer accent-[#5E45F8]"
                />
              </div>

              {/* Plugin-Specific Settings */}
              <div className="space-y-3 border-t border-[#F1EFF8] pt-4">
                <div className="text-[12.5px] font-bold text-[#1A172B]">
                  插件专属参数
                </div>

                {editingPlugin.id === "plg-craft-aquarium" && (
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
                                setDraftConfig({
                                  ...draftConfig,
                                  fishCount: count,
                                })
                              }
                              className={`rounded-xl py-2 font-mono-tabular text-[12px] font-semibold transition-colors ${
                                (draftConfig.fishCount ?? 7) === count
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                            { val: 0.6, label: "柔和" },
                            { val: 1.0, label: "标准" },
                            { val: 1.6, label: "活跃" },
                          ].map((sp) => (
                            <button
                              key={sp.val}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  bubbleSpeed: sp.val,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.bubbleSpeed ?? 1.0) === sp.val
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                            { val: 0.3, label: "微弱" },
                            { val: 0.6, label: "适中" },
                            { val: 1.0, label: "通透" },
                          ].map((c) => (
                            <button
                              key={c.val}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  causticsIntensity: c.val,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.causticsIntensity ?? 0.6) === c.val
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                              showAquariumClock: !(
                                draftConfig.showAquariumClock !== false
                              ),
                            })
                          }
                          className={`flex w-full items-center justify-between rounded-xl border px-3.5 py-2 text-[12.5px] font-semibold transition-colors ${
                            draftConfig.showAquariumClock !== false
                              ? "border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]"
                              : "border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]"
                          }`}
                        >
                          <span>显示水面悬浮时钟</span>
                          <span>
                            {draftConfig.showAquariumClock !== false
                              ? "开"
                              : "关"}
                          </span>
                        </button>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-stocks" && (
                  <div>
                    <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                      默认关注金融标的
                    </span>
                    <div className="grid grid-cols-4 gap-2">
                      {(["NVDA", "AAPL", "BTC", "ETH"] as const).map((sym) => (
                        <button
                          key={sym}
                          type="button"
                          onClick={() =>
                            setDraftConfig({ ...draftConfig, stockSymbol: sym })
                          }
                          className={`rounded-xl py-2 font-mono-tabular text-[12px] font-bold transition-colors ${
                            (draftConfig.stockSymbol ?? "NVDA") === sym
                              ? "bg-[#5E45F8] text-white"
                              : "bg-[#F5F4FA] text-[#56526B]"
                          }`}
                        >
                          {sym}
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-lyrics" && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          曲目标题
                        </label>
                        <input
                          type="text"
                          maxLength={40}
                          value={draftConfig.songTitle ?? "Midnight City"}
                          onChange={(e) =>
                            setDraftConfig({
                              ...draftConfig,
                              songTitle: e.target.value,
                            })
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
                          value={draftConfig.artist ?? "M83"}
                          onChange={(e) =>
                            setDraftConfig({
                              ...draftConfig,
                              artist: e.target.value,
                            })
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
                                ? "bg-[#5E45F8] text-white"
                                : "bg-[#F5F4FA] text-[#56526B]"
                            }`}
                          >
                            {bars} 频段
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-nixie" && (
                  <div className="space-y-3">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        辉光管灯丝色温
                      </span>
                      <div className="grid grid-cols-4 gap-2">
                        {(
                          [
                            { id: "amber", label: "经典琥珀" },
                            { id: "cyan", label: "赛博青蓝" },
                            { id: "green", label: "荧光翠绿" },
                            { id: "white", label: "纯净冷白" },
                          ] as const
                        ).map((hue) => (
                          <button
                            key={hue.id}
                            type="button"
                            onClick={() =>
                              setDraftConfig({
                                ...draftConfig,
                                nixieHue: hue.id,
                              })
                            }
                            className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                              (draftConfig.nixieHue ?? "amber") === hue.id
                                ? "bg-[#5E45F8] text-white"
                                : "bg-[#F5F4FA] text-[#56526B]"
                            }`}
                          >
                            {hue.label}
                          </button>
                        ))}
                      </div>
                    </div>
                    <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                      {[
                        { key: "showSeconds" as const, label: "显示秒数管" },
                        { key: "showDate" as const, label: "显示底部日期" },
                        { key: "use24Hour" as const, label: "24 小时制" },
                      ].map((opt) => {
                        const active = draftConfig[opt.key] !== false;
                        return (
                          <button
                            key={opt.key}
                            type="button"
                            onClick={() =>
                              setDraftConfig({
                                ...draftConfig,
                                [opt.key]: !active,
                              })
                            }
                            className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                              active
                                ? "border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]"
                                : "border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]"
                            }`}
                          >
                            <span>{opt.label}</span>
                            <span>{active ? "开" : "关"}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-cyber-pet" && (
                  <div className="space-y-3">
                    <div>
                      <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        萌宠昵称
                      </label>
                      <input
                        type="text"
                        maxLength={20}
                        value={draftConfig.petName ?? "Mochi"}
                        onChange={(e) =>
                          setDraftConfig({
                            ...draftConfig,
                            petName: e.target.value,
                          })
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
                              { id: "mochi", label: "赛博猫耳" },
                              { id: "pixel-bot", label: "像素机甲" },
                              { id: "shiba", label: "柴犬伙伴" },
                            ] as const
                          ).map((sp) => (
                            <button
                              key={sp.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  petSpecies: sp.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.petSpecies ?? "mochi") === sp.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                              { id: "happy", label: "活力" },
                              { id: "chill", label: "打盹" },
                              { id: "focus", label: "专注" },
                            ] as const
                          ).map((m) => (
                            <button
                              key={m.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  petMood: m.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.petMood ?? "happy") === m.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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

                {editingPlugin.id === "plg-craft-campfire" && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          火焰大小强度
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: "low", label: "微火" },
                              { id: "medium", label: "适中" },
                              { id: "high", label: "旺火" },
                            ] as const
                          ).map((f) => (
                            <button
                              key={f.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  flameIntensity: f.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.flameIntensity ?? "medium") ===
                                f.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                              { id: "low", label: "稀疏" },
                              { id: "medium", label: "适中" },
                              { id: "high", label: "密集" },
                            ] as const
                          ).map((e) => (
                            <button
                              key={e.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  emberDensity: e.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.emberDensity ?? "medium") === e.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                          ? "border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]"
                          : "border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]"
                      }`}
                    >
                      <span>显示白噪音氛围标签</span>
                      <span>
                        {draftConfig.showSoundHint !== false ? "开" : "关"}
                      </span>
                    </button>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-bonsai" && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          盆栽植物品种
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: "pine", label: "青松" },
                              { id: "succulent", label: "多肉" },
                              { id: "sakura", label: "春樱" },
                            ] as const
                          ).map((b) => (
                            <button
                              key={b.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  bonsaiSpecies: b.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.bonsaiSpecies ?? "pine") === b.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                              { id: "warm", label: "暖阳" },
                              { id: "cool", label: "月光" },
                              { id: "mist", label: "微雾" },
                            ] as const
                          ).map((t) => (
                            <button
                              key={t.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  sunlightTone: t.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.sunlightTone ?? "warm") === t.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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

                {editingPlugin.id === "plg-craft-flip-clock" && (
                  <div className="space-y-3">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        机械外壳质感
                      </span>
                      <div className="grid grid-cols-3 gap-2">
                        {(
                          [
                            { id: "obsidian", label: "曜石炭黑" },
                            { id: "warmwood", label: "暖木胡桃" },
                            { id: "titanium", label: "钛银拉丝" },
                          ] as const
                        ).map((sh) => (
                          <button
                            key={sh.id}
                            type="button"
                            onClick={() =>
                              setDraftConfig({
                                ...draftConfig,
                                flipShellStyle: sh.id,
                              })
                            }
                            className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                              (draftConfig.flipShellStyle ?? "obsidian") ===
                              sh.id
                                ? "bg-[#5E45F8] text-white"
                                : "bg-[#F5F4FA] text-[#56526B]"
                            }`}
                          >
                            {sh.label}
                          </button>
                        ))}
                      </div>
                    </div>
                    <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                      {[
                        { key: "showSeconds" as const, label: "显示秒数牌" },
                        { key: "showDate" as const, label: "显示底部日期" },
                        { key: "use24Hour" as const, label: "24 小时制" },
                      ].map((opt) => {
                        const active = draftConfig[opt.key] !== false;
                        return (
                          <button
                            key={opt.key}
                            type="button"
                            onClick={() =>
                              setDraftConfig({
                                ...draftConfig,
                                [opt.key]: !active,
                              })
                            }
                            className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-[12.5px] font-semibold transition-colors ${
                              active
                                ? "border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]"
                                : "border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]"
                            }`}
                          >
                            <span>{opt.label}</span>
                            <span>{active ? "开" : "关"}</span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-vu-meter" && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          表盘背光色温
                        </span>
                        <div className="grid grid-cols-3 gap-1.5">
                          {(
                            [
                              { id: "warm-amber", label: "暖黄" },
                              { id: "emerald", label: "翠绿" },
                              { id: "ice-blue", label: "冰蓝" },
                            ] as const
                          ).map((bg) => (
                            <button
                              key={bg.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  vuBacklight: bg.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.vuBacklight ?? "warm-amber") ===
                                bg.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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
                              { id: "smooth", label: "舒缓" },
                              { id: "balanced", label: "均衡" },
                              { id: "punchy", label: "灵敏" },
                            ] as const
                          ).map((sv) => (
                            <button
                              key={sv.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  vuSensitivity: sv.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.vuSensitivity ?? "balanced") ===
                                sv.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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

                {editingPlugin.id === "plg-craft-cassette" && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          磁带手写标签标题
                        </label>
                        <input
                          type="text"
                          maxLength={32}
                          value={draftConfig.songTitle ?? "City Pop Tape #04"}
                          onChange={(e) =>
                            setDraftConfig({
                              ...draftConfig,
                              songTitle: e.target.value,
                            })
                          }
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
                              { id: "smoke", label: "烟熏黑" },
                              { id: "amber", label: "琥珀" },
                              { id: "indigo", label: "靛蓝" },
                            ] as const
                          ).map((sh) => (
                            <button
                              key={sh.id}
                              type="button"
                              onClick={() =>
                                setDraftConfig({
                                  ...draftConfig,
                                  cassetteShell: sh.id,
                                })
                              }
                              className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                                (draftConfig.cassetteShell ?? "smoke") === sh.id
                                  ? "bg-[#5E45F8] text-white"
                                  : "bg-[#F5F4FA] text-[#56526B]"
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

                {editingPlugin.id === "plg-craft-zodiac" && (
                  <div className="space-y-3">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        选择专属星座
                      </span>
                      <div className="grid grid-cols-4 gap-1.5 sm:grid-cols-6">
                        {(
                          [
                            { id: "aries", label: "白羊" },
                            { id: "taurus", label: "金牛" },
                            { id: "gemini", label: "双子" },
                            { id: "cancer", label: "巨蟹" },
                            { id: "leo", label: "狮子" },
                            { id: "virgo", label: "处女" },
                            { id: "libra", label: "天秤" },
                            { id: "scorpio", label: "天蝎" },
                            { id: "sagittarius", label: "射手" },
                            { id: "capricorn", label: "摩羯" },
                            { id: "aquarius", label: "水瓶" },
                            { id: "pisces", label: "双鱼" },
                          ] as const
                        ).map((z) => (
                          <button
                            key={z.id}
                            type="button"
                            onClick={() =>
                              setDraftConfig({
                                ...draftConfig,
                                zodiacSign: z.id,
                              })
                            }
                            className={`rounded-xl py-1.5 text-[12px] font-semibold transition-colors ${
                              (draftConfig.zodiacSign ?? "aquarius") === z.id
                                ? "bg-[#5E45F8] text-white"
                                : "bg-[#F5F4FA] text-[#56526B]"
                            }`}
                          >
                            {z.label}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-parcel" && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          包裹备注名称
                        </label>
                        <input
                          type="text"
                          maxLength={32}
                          value={draftConfig.parcelName ?? "客制化机械键盘套件"}
                          onChange={(e) =>
                            setDraftConfig({
                              ...draftConfig,
                              parcelName: e.target.value,
                            })
                          }
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
                          value={draftConfig.carrier ?? "顺丰速运"}
                          onChange={(e) =>
                            setDraftConfig({
                              ...draftConfig,
                              carrier: e.target.value,
                            })
                          }
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
                            { id: "collected", label: "已揽收" },
                            { id: "transit", label: "干线中" },
                            { id: "delivering", label: "派送中" },
                            { id: "delivered", label: "已签收" },
                          ] as const
                        ).map((st) => (
                          <button
                            key={st.id}
                            type="button"
                            onClick={() =>
                              setDraftConfig({
                                ...draftConfig,
                                parcelStage: st.id,
                              })
                            }
                            className={`rounded-xl py-2 text-[12px] font-semibold transition-colors ${
                              (draftConfig.parcelStage ?? "delivering") ===
                              st.id
                                ? "bg-[#5E45F8] text-white"
                                : "bg-[#F5F4FA] text-[#56526B]"
                            }`}
                          >
                            {st.label}
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-milestone" && (
                  <div className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                          纪念日 / 目标标题
                        </label>
                        <input
                          type="text"
                          maxLength={24}
                          value={draftConfig.milestoneTitle ?? "年度版本发布"}
                          onChange={(e) =>
                            setDraftConfig({
                              ...draftConfig,
                              milestoneTitle: e.target.value,
                            })
                          }
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
                                countMode:
                                  (draftConfig.countMode ?? "countdown") ===
                                  "countdown"
                                    ? "countup"
                                    : "countdown",
                              })
                            }
                            className="rounded-xl bg-[#F5F4FA] px-3 py-2 text-[12px] font-semibold text-[#5E45F8]"
                          >
                            {(draftConfig.countMode ?? "countdown") ===
                            "countdown"
                              ? "倒数模式"
                              : "正数纪念"}
                          </button>
                          <input
                            type="number"
                            min={0}
                            max={9999}
                            value={draftConfig.targetDays ?? 18}
                            onChange={(e) =>
                              setDraftConfig({
                                ...draftConfig,
                                targetDays: Number(e.target.value) || 0,
                              })
                            }
                            className="w-24 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 font-mono-tabular text-[13px] font-bold text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                          />
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {editingPlugin.id === "plg-craft-netradar" && (
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <span className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                        速率显示单位
                      </span>
                      <div className="grid grid-cols-2 gap-1.5">
                        {(["Mbps", "MB/s"] as const).map((u) => (
                          <button
                            key={u}
                            type="button"
                            onClick={() =>
                              setDraftConfig({ ...draftConfig, unitMode: u })
                            }
                            className={`rounded-xl py-2 font-mono-tabular text-[12px] font-semibold transition-colors ${
                              (draftConfig.unitMode ?? "Mbps") === u
                                ? "bg-[#5E45F8] text-white"
                                : "bg-[#F5F4FA] text-[#56526B]"
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
                            showPingChart: !(
                              draftConfig.showPingChart !== false
                            ),
                          })
                        }
                        className={`flex w-full items-center justify-between rounded-xl border px-3.5 py-2 text-[12.5px] font-semibold transition-colors ${
                          draftConfig.showPingChart !== false
                            ? "border-[#C8BDFC] bg-[#F5F2FF] text-[#5E45F8]"
                            : "border-[#E6E4F2] bg-[#F8F7FC] text-[#6B6880]"
                        }`}
                      >
                        <span>显示实时带宽折线图</span>
                        <span>
                          {draftConfig.showPingChart !== false ? "开" : "关"}
                        </span>
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* Plugin Description */}
              {craftMessage && <p role="status" className="text-sm text-[#6244ff]">{craftMessage}</p>}
              <div className="border-t border-[#F1EFF8] pt-4">
                <div className="text-[12.5px] font-bold text-[#1A172B]">
                  插件介绍
                </div>
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
                <button
                  type="button"
                  onClick={() => handleSaveConfig()}
                  className="rounded-xl border border-[#E5E2F4] px-4 py-2 text-sm"
                >
                  保存配置
                </button>
                <button
                  type="button"
                  disabled={
                    applying ||
                    activeDevice.status === "离线" ||
                    !canOutputPlugin(editingPlugin)
                  }
                  onClick={async () => {
                    setApplying(true);
                    try {
                      if (await onApplyBuiltin(editingPlugin, draftConfig)) {
                        onUpdatePluginConfig(
                          editingPlugin.id,
                          draftConfig,
                          false,
                        );
                        closeConfigModal();
                      }
                    } catch (error) {
                      setCraftMessage(String(error));
                    } finally {
                      setApplying(false);
                    }
                  }}
                  className="rounded-xl bg-[#5E45F8] px-4 py-2 text-sm text-white disabled:opacity-50"
                >
                  {applying ? "正在应用…" : "应用到 " + activeDevice.name}
                </button>
                {!canOutputPlugin(editingPlugin) && (
                  <p className="text-xs text-amber-700">
                    此插件尚待接入真实数据，目前可预览。
                  </p>
                )}
                {activeDevice.status === "离线" && (
                  <p className="text-xs text-amber-700">设备已离线</p>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
