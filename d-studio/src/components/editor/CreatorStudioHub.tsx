import { desktopMode, importVideo } from '../../services/desktopBridge';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  Cloud,
  Copy,
  Edit3,
  Eye,
  FilePlus2,
  FolderUp,
  Layers,
  Monitor,
  MoreHorizontal,
  Plus,
  RefreshCw,
  Search,
  Server,
  Sparkles,
  Trash2,
  Upload,
  Wifi,
  WifiOff,
  X,
} from 'lucide-react';
import { DeviceInfo, ThemeItem } from '../../data/themes';
import {
  BUILTIN_TEMPLATES,
  BuiltinTemplateSpec,
  EDITOR_DEVICE_SPECS,
  createBlankEditorProject,
  ensureEditorProjectFromTheme,
  genEditorId,
  makeElement,
} from '../../data/editorCatalog';
import { cloudResourceAdapter } from '../../services/cloudResourceAdapter';
import { EditorThemeProject, THEME_SCHEMA_VERSION } from '../../types/editor';
import { ThemeArtwork } from '../ThemeArtwork';

interface CreatorStudioHubProps {
  themes: ThemeItem[];
  activeTheme: ThemeItem;
  activeDevice: DeviceInfo;
  onEnterEditor: (project: EditorThemeProject, sourceThemeId?: string) => void;
  onApplyTheme: (theme: ThemeItem) => void;
  onDeleteCustomTheme: (id: string) => void;
  onDuplicateCustomTheme: (theme: ThemeItem) => void;
  onNotify: (msg: string) => void;
}

function readFileAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : '');
    reader.onerror = () => reject(new Error('读取失败'));
    reader.readAsDataURL(file);
  });
}

export const CreatorStudioHub: React.FC<CreatorStudioHubProps> = ({
  themes,
  activeTheme,
  activeDevice,
  onEnterEditor,
  onApplyTheme,
  onDeleteCustomTheme,
  onDuplicateCustomTheme,
  onNotify,
}) => {
  const [selectedSpecId, setSelectedSpecId] = useState<string>(
    activeDevice.orientation === 'portrait'
      ? activeDevice.nativeWidth === 1280
        ? 'd69-port'
        : 'd92-port'
      : activeDevice.nativeWidth === 1280
        ? 'd69-land'
        : 'd92-land'
  );
  const [adminDrawerOpen, setAdminDrawerOpen] = useState(false);
  const [heroMenuOpen, setHeroMenuOpen] = useState(false);
  const [previewBuiltin, setPreviewBuiltin] = useState<BuiltinTemplateSpec | null>(null);
  const [offlineSim, setOfflineSim] = useState(() => cloudResourceAdapter.isOfflineSimulated());
  const [mockTitle, setMockTitle] = useState('云端限定 · 极光效能桌搭');
  const [mockAccent, setMockAccent] = useState('#5E45F8');
  const [mockG1, setMockG1] = useState('#120733');
  const [mockG2, setMockG2] = useState('#241059');
  const [publishedMockIds, setPublishedMockIds] = useState<string[]>([]);
  const [updatesMap, setUpdatesMap] = useState<
    Record<string, { latestVersion: string; changelog: string }>
  >({});

  // Template & My Works search/filter state
  const [tplSearch, setTplSearch] = useState('');
  const [tplCat, setTplCat] = useState<'全部' | '动态时钟' | '主题'>('全部');
  const [workSearch, setWorkSearch] = useState('');
  const [workOri, setWorkOri] = useState<'all' | 'landscape' | 'portrait'>('all');

  const localImportRef = useRef<HTMLInputElement | null>(null);
  const jsonImportRef = useRef<HTMLInputElement | null>(null);
  const heroMenuRef = useRef<HTMLDivElement | null>(null);

  const selectedSpec =
    EDITOR_DEVICE_SPECS.find((s) => s.id === selectedSpecId) ?? EDITOR_DEVICE_SPECS[0];

  const customThemes = useMemo(
    () => themes.filter((t) => t.source === 'custom' || Boolean(t.editorProject)),
    [themes]
  );

  const filteredTemplates = useMemo(() => {
    const q = tplSearch.trim().toLowerCase();
    return BUILTIN_TEMPLATES.filter((tpl) => {
      if (tplCat !== '全部' && tpl.category !== tplCat) return false;
      if (!q) return true;
      return (
        tpl.name.toLowerCase().includes(q) ||
        tpl.subCategory.toLowerCase().includes(q) ||
        tpl.tags.some((tag) => tag.toLowerCase().includes(q))
      );
    });
  }, [tplCat, tplSearch]);

  const filteredCustomThemes = useMemo(() => {
    const q = workSearch.trim().toLowerCase();
    return customThemes.filter((item) => {
      if (workOri !== 'all' && item.orientation !== workOri) return false;
      if (!q) return true;
      return (
        item.displayName.toLowerCase().includes(q) ||
        item.category.toLowerCase().includes(q) ||
        item.resolution.toLowerCase().includes(q)
      );
    });
  }, [customThemes, workOri, workSearch]);

  useEffect(() => {
    if (!heroMenuOpen) return;
    const onOutside = (e: MouseEvent) => {
      if (heroMenuRef.current && !heroMenuRef.current.contains(e.target as Node)) {
        setHeroMenuOpen(false);
      }
    };
    window.addEventListener('mousedown', onOutside);
    return () => window.removeEventListener('mousedown', onOutside);
  }, [heroMenuOpen]);

  // Check cloud template updates for user's saved cloud-derived projects
  useEffect(() => {
    const refs = customThemes
      .map((t) => t.editorProject)
      .filter((p): p is EditorThemeProject => Boolean(p?.cloudTemplateId && p?.cloudVersion))
      .map((p) => ({ cloudTemplateId: p.cloudTemplateId!, cloudVersion: p.cloudVersion! }));
    if (refs.length === 0) {
      setUpdatesMap({});
      return;
    }
    cloudResourceAdapter.checkUpdates(refs).then((list) => {
      const next: Record<string, { latestVersion: string; changelog: string }> = {};
      list.forEach((u) => {
        next[u.cloudTemplateId] = { latestVersion: u.latestVersion, changelog: u.changelog };
      });
      setUpdatesMap(next);
    });
  }, [customThemes, publishedMockIds]);

  const makeDeviceWithSpec = (): DeviceInfo => {
    const isPort = selectedSpec.size[0] < selectedSpec.size[1];
    return {
      ...activeDevice,
      orientation: isPort ? 'portrait' : 'landscape',
      nativeWidth: Math.max(selectedSpec.size[0], selectedSpec.size[1]),
      nativeHeight: Math.min(selectedSpec.size[0], selectedSpec.size[1]),
      resolution: `${selectedSpec.size[0]} × ${selectedSpec.size[1]}`,
    };
  };

  const handleStartBlank = () => {
    const proj = createBlankEditorProject(makeDeviceWithSpec(), {
      name: `${selectedSpec.label.split(' ')[0]}_新建主题`,
    });
    onEnterEditor(proj);
  };

  const handleStartFromBuiltin = (tpl: BuiltinTemplateSpec) => {
    const proj = createBlankEditorProject(makeDeviceWithSpec(), {
      name: `${tpl.name} · 创作副本`,
      category: tpl.category,
      template: tpl,
    });
    onEnterEditor(proj);
  };

  const handleQuickImportLocal = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    e.target.value = '';
    if (!f) return;
    const proj = createBlankEditorProject(makeDeviceWithSpec(), {
      name: f.name.replace(/\.[^.]+$/, ''),
    });
    if (f.type.startsWith('video/')) {
      const url = desktopMode ? (await importVideo(f, () => {})).videoUrl! : URL.createObjectURL(f);
      proj.bg = {
        kind: 'video',
        vurl: url,
        vname: f.name,
        dim: 15,
        blur: 0,
        fit: 'cover',
      };
    } else {
      const dataUrl = await readFileAsDataUrl(f);
      proj.bg = {
        kind: 'image',
        url: dataUrl,
        dim: 15,
        blur: 0,
        fit: 'cover',
      };
    }
    onEnterEditor(proj);
  };

  const handleImportProjectJson = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    e.target.value = '';
    if (!f) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const parsed = JSON.parse(String(reader.result || '{}'));
        if (!parsed.bg || !Array.isArray(parsed.master)) {
          onNotify('工程文件格式校验失败：缺少有效的背景或图层结构');
          return;
        }
        const imported: EditorThemeProject = {
          ...parsed,
          template_id: parsed.template_id || genEditorId('imp'),
          schema_version: parsed.schema_version || THEME_SCHEMA_VERSION,
          updated_at: Date.now(),
        };
        onNotify(`已导入工程「${imported.name || f.name}」，正在打开编辑器`);
        onEnterEditor(imported);
      } catch {
        onNotify('工程 JSON 解析失败，请确认文件格式有效');
      }
    };
    reader.readAsText(f);
  };

  const handlePublishMockCloudTemplate = () => {
    const item = cloudResourceAdapter.publishMockTemplate({
      name: mockTitle.trim() || '云端新发布模板',
      categoryId: 'sysmon',
      subCategoryId: 'telemetry',
      description: '通过模拟 WEB 后台实时发布至客户端目录的新模板包',
      accentHex: mockAccent,
      bg: { kind: 'gradient', g1: mockG1, g2: mockG2, angle: 135, dim: 10, blur: 0, fit: 'cover' },
      elements: [
        makeElement('clock', { a: 'ml', x: 6, size: 36, font: 'tech' }),
        makeElement('date', { a: 'ml', x: 6, y: 24, size: 7, fmt: 1, alpha: 0.85 }),
        makeElement('widget', {
          widgetType: 'sysmon',
          a: 'mr',
          x: -6,
          w: 32,
          h: 64,
          accentColor: mockAccent,
          bgOpacity: 78,
        }),
      ],
    });
    setPublishedMockIds((prev) => [item.id, ...prev]);
    onNotify(`模拟 WEB 后台已发布「${item.name}」，无需重启或更新软件即可在编辑器云端模板库使用！`);
  };

  return (
    <div className="space-y-6">
      <input
        ref={localImportRef}
        type="file"
        accept="image/*,video/mp4,video/webm"
        className="hidden"
        onChange={(e) => void handleQuickImportLocal(e)}
      />
      <input
        ref={jsonImportRef}
        type="file"
        accept=".json,.d92theme.json"
        className="hidden"
        onChange={handleImportProjectJson}
      />

      {/* Hero Entry Card: Cosmic Violet Studio Showcase */}
      <div className="aurora-dark-card rounded-[26px] p-5 sm:p-6 text-white shadow-[0_22px_50px_rgba(24,10,68,0.26)]">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-[22px] font-extrabold tracking-tight text-white">
            创作中心
          </h2>

          {/* Primary Import + Tucked Developer Menu */}
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => jsonImportRef.current?.click()}
              className="flex items-center gap-1.5 rounded-xl border border-white/18 bg-white/10 px-3 py-1.5 text-[12px] font-semibold text-white/95 backdrop-blur-md transition-colors hover:border-[#A78BFA]/70 hover:bg-white/18 whitespace-nowrap"
            >
              <Upload className="h-3.5 w-3.5 text-[#C4B5FD]" />
              <span>导入工程包</span>
            </button>

            <div ref={heroMenuRef} className="relative">
              <button
                type="button"
                onClick={() => setHeroMenuOpen((v) => !v)}
                title="更多工具与联调设置"
                aria-label="更多工具与联调设置"
                className="flex h-8 w-8 items-center justify-center rounded-xl border border-white/15 bg-white/10 text-white/85 backdrop-blur-md transition-colors hover:border-white/35 hover:bg-white/18 hover:text-white"
              >
                <MoreHorizontal className="h-4 w-4" />
              </button>
              {heroMenuOpen && (
                <div className="absolute right-0 top-10 z-40 w-52 rounded-2xl border border-[#E6E4F2] bg-white/96 p-1.5 text-[12px] text-[#1A172B] shadow-[0_18px_46px_rgba(24,10,68,0.24)] backdrop-blur-xl">
                  <button
                    type="button"
                    onClick={() => {
                      setHeroMenuOpen(false);
                      const next = !offlineSim;
                      cloudResourceAdapter.setSimulateOffline(next);
                      setOfflineSim(next);
                      onNotify(next ? '已切换至离线模拟模式' : desktopMode ? '已恢复本机示例目录' : '已恢复云端目录连接');
                    }}
                    className="flex w-full items-center justify-between rounded-xl px-2.5 py-2 text-left font-medium hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                  >
                    <span className="flex items-center gap-2">
                      {offlineSim ? (
                        <WifiOff className="h-3.5 w-3.5 text-amber-600" />
                      ) : (
                        <Wifi className="h-3.5 w-3.5 text-emerald-600" />
                      )}
                      <span>{offlineSim ? '切回云端在线' : '模拟离线模式'}</span>
                    </span>
                    <span className="text-[10.5px] text-[#8B87A1]">
                      {offlineSim ? '离线' : '在线'}
                    </span>
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setHeroMenuOpen(false);
                      setAdminDrawerOpen(true);
                    }}
                    className="flex w-full items-center gap-2 rounded-xl px-2.5 py-2 text-left font-medium hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
                  >
                    <Server className="h-3.5 w-3.5 text-[#5E45F8]" />
                    <span>云端模板发布台（本机演示）</span>
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Target Device Resolution Strip */}
        <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-white/12 pt-3.5">
          <span className="mr-1 flex items-center gap-1.5 text-[12px] font-bold text-[#DDD6FE]">
            <Monitor className="h-3.5 w-3.5 text-[#A78BFA]" />
            画布规格
          </span>
          {EDITOR_DEVICE_SPECS.map((spec) => {
            const active = spec.id === selectedSpecId;
            return (
              <button
                key={spec.id}
                type="button"
                onClick={() => setSelectedSpecId(spec.id)}
                className={`rounded-xl border px-3 py-1.5 text-[12px] font-semibold transition-all whitespace-nowrap ${
                  active
                    ? 'border-[#8B76FF] bg-[#5E45F8] text-white shadow-[0_6px_16px_rgba(94,69,248,0.45)]'
                    : 'border-white/15 bg-white/10 text-[#E5E0FA] hover:border-white/35 hover:bg-white/15'
                }`}
              >
                {spec.label}{' '}
                <span className={`font-mono-tabular text-[11px] ${active ? 'text-[#E0D9FF]' : 'text-white/60'}`}>
                  {spec.size[0]}×{spec.size[1]}
                </span>
              </button>
            );
          })}
        </div>

        {/* 3 Primary Creation Actions */}
        <div className="mt-4 grid grid-cols-1 gap-3.5 sm:grid-cols-3">
          <button
            type="button"
            onClick={handleStartBlank}
            className="group flex items-center gap-3.5 rounded-2xl border border-[#8D78FF]/60 bg-gradient-to-br from-[#6A52FF] to-[#563CEB] p-4 text-left text-white shadow-[0_12px_28px_rgba(94,69,248,0.4)] transition-all hover:-translate-y-0.5 hover:shadow-[0_16px_34px_rgba(94,69,248,0.52)]"
          >
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-white/18 backdrop-blur-sm">
              <FilePlus2 className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <div className="text-[14.5px] font-bold">新建空白画布</div>
              <div className="mt-0.5 truncate text-[11.5px] text-[#E3DCFF]">
                {selectedSpec.label}
              </div>
            </div>
          </button>

          <button
            type="button"
            onClick={() => {
              const starter = BUILTIN_TEMPLATES[5] ?? BUILTIN_TEMPLATES[0];
              if (starter) handleStartFromBuiltin(starter);
            }}
            className="group flex items-center gap-3.5 rounded-2xl border border-white/15 bg-white/10 p-4 text-left backdrop-blur-md transition-all hover:-translate-y-0.5 hover:border-[#A78BFA]/70 hover:bg-white/16"
          >
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#5E45F8]/35 text-[#DCD2FF]">
              <Layers className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <div className="text-[14.5px] font-bold text-white">组件监控模板</div>
              <div className="mt-0.5 truncate text-[11.5px] text-[#CEC7EB]">
                时钟 · 硬件监控
              </div>
            </div>
          </button>

          <button
            type="button"
            onClick={() => localImportRef.current?.click()}
            className="group flex items-center gap-3.5 rounded-2xl border border-white/15 bg-white/10 p-4 text-left backdrop-blur-md transition-all hover:-translate-y-0.5 hover:border-[#A78BFA]/70 hover:bg-white/16"
          >
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-white/15 text-white">
              <FolderUp className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <div className="text-[14.5px] font-bold text-white">导入本地素材</div>
              <div className="mt-0.5 truncate text-[11.5px] text-[#CEC7EB]">
                图片 / MP4 / WebM
              </div>
            </div>
          </button>
        </div>
      </div>

      {/* Built-in Offline Starters */}
      <div className="rounded-[24px] border border-white/80 bg-white/92 p-5 shadow-[0_12px_36px_rgba(30,20,80,0.06)] backdrop-blur-xl">
        <div className="mb-3.5 flex flex-wrap items-center justify-between gap-2.5">
          <h3 className="text-[15.5px] font-bold text-[#1A172B]">
            内置模板 ({filteredTemplates.length})
          </h3>

          <div className="flex flex-wrap items-center gap-2">
            <div className="flex items-center rounded-xl bg-[#F4F3FA] p-1 text-[11.5px] font-semibold">
              {(['全部', '动态时钟', '主题'] as const).map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => setTplCat(cat)}
                  className={`rounded-lg px-2.5 py-1 transition-colors whitespace-nowrap ${
                    tplCat === cat
                      ? 'bg-white text-[#5E45F8] shadow-xs'
                      : 'text-[#6B6880] hover:text-[#1A172B]'
                  }`}
                >
                  {cat}
                </button>
              ))}
            </div>

            <div className="relative">
              <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-[#8E89A6]" />
              <input
                type="text"
                value={tplSearch}
                onChange={(e) => setTplSearch(e.target.value)}
                placeholder="搜索模板…"
                aria-label="搜索内置模板"
                className="w-40 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 pl-8 pr-6 text-[12px] text-[#1A172B] placeholder:text-[#9A96AE] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
              />
              {tplSearch && (
                <button
                  type="button"
                  onClick={() => setTplSearch('')}
                  aria-label="清除搜索"
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-[#8E89A6] hover:text-[#1A172B]"
                >
                  <X className="h-3 w-3" />
                </button>
              )}
            </div>
          </div>
        </div>

        {filteredTemplates.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-[#DAD6F0] bg-[#F8F7FC] py-8 text-center">
            <p className="text-[12.5px] font-medium text-[#6B6880]">未找到匹配的内置模板</p>
            <button
              type="button"
              onClick={() => {
                setTplSearch('');
                setTplCat('全部');
              }}
              className="mt-2 rounded-lg bg-[#EFEBFD] px-3 py-1 text-[11.5px] font-semibold text-[#5E45F8] hover:bg-[#E4DCFC]"
            >
              重置筛选
            </button>
          </div>
        ) : (
        <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-4">
          {filteredTemplates.map((tpl) => {
            const previewProj = createBlankEditorProject(makeDeviceWithSpec(), {
              name: tpl.name,
              template: tpl,
            });
            const isPort = selectedSpec.size[0] < selectedSpec.size[1];
            return (
              <div
                key={tpl.id}
                className="group relative flex flex-col overflow-hidden rounded-2xl border border-[#E6E4F2] bg-[#F8F7FC] p-2.5 transition-all hover:-translate-y-0.5 hover:border-[#5E45F8] hover:bg-white hover:shadow-[0_12px_28px_rgba(94,69,248,0.12)]"
              >
                <div
                  onClick={() => handleStartFromBuiltin(tpl)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      handleStartFromBuiltin(tpl);
                    }
                  }}
                  className="relative mx-auto w-full cursor-pointer overflow-hidden rounded-xl bg-[#120733]"
                  style={{
                    aspectRatio: `${selectedSpec.size[0]} / ${selectedSpec.size[1]}`,
                    maxHeight: isPort ? '150px' : undefined,
                  }}
                >
                  <ThemeArtwork
                    theme={{
                      id: tpl.id,
                      name: tpl.name,
                      displayName: tpl.name,
                      category: tpl.category,
                      resolution: `${selectedSpec.size[0]}×${selectedSpec.size[1]}`,
                      orientation: isPort ? 'portrait' : 'landscape',
                      author: '官方内置',
                      downloads: '内置',
                      downloadCount: 0,
                      publishedAt: 0,
                      releasedLabel: '内置',
                      addedAt: 0,
                      addedLabel: '内置',
                      lastUsedAt: 0,
                      useCount: 0,
                      durationMinutes: 15,
                      inPlaylist: false,
                      isFavorite: false,
                      accentHex: tpl.accentHex,
                      editorProject: previewProj,
                    }}
                    className="h-full w-full"
                    showClock={true}
                    liveTick={false}
                    portrait={isPort}
                  />
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      setPreviewBuiltin(tpl);
                    }}
                    title="多规格设备预览"
                    aria-label={`预览 ${tpl.name}`}
                    className="absolute right-2 top-2 flex h-7 w-7 items-center justify-center rounded-lg bg-[#120733]/70 text-white opacity-0 backdrop-blur-md transition-opacity group-hover:opacity-100 hover:bg-[#5E45F8]"
                  >
                    <Eye className="h-3.5 w-3.5" />
                  </button>
                </div>
                <div className="mt-2.5 flex items-center justify-between gap-2">
                  <div className="min-w-0 truncate text-[13px] font-bold text-[#1A172B] group-hover:text-[#5E45F8]">
                    {tpl.name}
                  </div>
                  <button
                    type="button"
                    onClick={() => handleStartFromBuiltin(tpl)}
                    className="shrink-0 rounded-lg bg-[#EFEBFD] px-2.5 py-1 text-[11px] font-bold text-[#5E45F8] transition-colors group-hover:bg-[#5E45F8] group-hover:text-white whitespace-nowrap"
                  >
                    使用
                  </button>
                </div>
              </div>
            );
          })}
        </div>
        )}
      </div>

      {/* User's Saved Custom Projects */}
      <div className="rounded-[24px] border border-white/80 bg-white/92 p-5 shadow-[0_12px_36px_rgba(30,20,80,0.06)] backdrop-blur-xl">
        <div className="mb-3.5 flex flex-wrap items-center justify-between gap-2.5">
          <h3 className="text-[15.5px] font-bold text-[#1A172B]">
            我的作品 ({filteredCustomThemes.length})
          </h3>
          <div className="flex flex-wrap items-center gap-2">
            {customThemes.length > 0 && (
              <>
                <div className="flex items-center rounded-xl bg-[#F4F3FA] p-1 text-[11.5px] font-semibold">
                  {(
                    [
                      { id: 'all', label: '全部' },
                      { id: 'landscape', label: '横屏' },
                      { id: 'portrait', label: '竖屏' },
                    ] as const
                  ).map((opt) => (
                    <button
                      key={opt.id}
                      type="button"
                      onClick={() => setWorkOri(opt.id)}
                      className={`rounded-lg px-2.5 py-1 transition-colors whitespace-nowrap ${
                        workOri === opt.id
                          ? 'bg-white text-[#5E45F8] shadow-xs'
                          : 'text-[#6B6880] hover:text-[#1A172B]'
                      }`}
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
                <div className="relative">
                  <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-[#8E89A6]" />
                  <input
                    type="text"
                    value={workSearch}
                    onChange={(e) => setWorkSearch(e.target.value)}
                    placeholder="搜索作品…"
                    aria-label="搜索我的作品"
                    className="w-36 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] py-1.5 pl-8 pr-6 text-[12px] text-[#1A172B] placeholder:text-[#9A96AE] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                  />
                  {workSearch && (
                    <button
                      type="button"
                      onClick={() => setWorkSearch('')}
                      aria-label="清除搜索"
                      className="absolute right-2 top-1/2 -translate-y-1/2 text-[#8E89A6] hover:text-[#1A172B]"
                    >
                      <X className="h-3 w-3" />
                    </button>
                  )}
                </div>
              </>
            )}
            <button
              type="button"
              onClick={() => jsonImportRef.current?.click()}
              className="flex items-center gap-1 rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-1.5 text-[12px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8] whitespace-nowrap"
            >
              <Upload className="h-3.5 w-3.5" />
              <span>导入工程</span>
            </button>
            <button
              type="button"
              onClick={handleStartBlank}
              className="flex items-center gap-1 rounded-xl bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-3.5 py-1.5 text-[12px] font-bold text-white shadow-[0_6px_16px_rgba(94,69,248,0.28)] hover:opacity-95 whitespace-nowrap"
            >
              <Plus className="h-3.5 w-3.5" />
              <span>新建工程</span>
            </button>
          </div>
        </div>

        {customThemes.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-[#DAD6F0] bg-[#F8F7FC] py-8 text-center">
            <p className="text-[12.5px] font-medium text-[#6B6880]">
              暂无自制主题，点击新建、导入工程或选用模板开始创作
            </p>
          </div>
        ) : filteredCustomThemes.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-[#DAD6F0] bg-[#F8F7FC] py-8 text-center">
            <p className="text-[12.5px] font-medium text-[#6B6880]">未找到匹配的自定义作品</p>
            <button
              type="button"
              onClick={() => {
                setWorkSearch('');
                setWorkOri('all');
              }}
              className="mt-2 rounded-lg bg-[#EFEBFD] px-3 py-1 text-[11.5px] font-semibold text-[#5E45F8] hover:bg-[#E4DCFC]"
            >
              重置筛选
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {filteredCustomThemes.map((item) => {
              const isCurrent = item.id === activeTheme.id;
              const isPort = item.orientation === 'portrait';
              const cloudUpd = item.editorProject?.cloudTemplateId
                ? updatesMap[item.editorProject.cloudTemplateId]
                : undefined;

              return (
                <div
                  key={item.id}
                  className={`flex flex-col overflow-hidden rounded-2xl border bg-[#F8F7FC] p-3 transition-all ${
                    isCurrent
                      ? 'border-[#5E45F8] bg-white ring-2 ring-[#5E45F8]/20 shadow-[0_10px_28px_rgba(94,69,248,0.12)]'
                      : 'border-[#E6E4F2] hover:border-[#5E45F8]/60 hover:bg-white'
                  }`}
                >
                  <div
                    className="overflow-hidden rounded-xl bg-[#120733]"
                    style={{
                      aspectRatio: isPort ? '462 / 1920' : '1920 / 462',
                      maxHeight: isPort ? '170px' : undefined,
                    }}
                  >
                    <ThemeArtwork
                      theme={item}
                      className="h-full w-full"
                      showClock={true}
                      liveTick={true}
                      portrait={isPort}
                    />
                  </div>

                  <div className="mt-2.5 flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5">
                        <span className="truncate text-[13.5px] font-bold text-[#1A172B]">
                          {item.displayName}
                        </span>
                        {isCurrent && (
                          <span className="shrink-0 rounded bg-[#5E45F8] px-1.5 py-0.5 text-[10px] font-bold text-white">
                            正在输出
                          </span>
                        )}
                      </div>
                      <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-[11px] text-[#6B6880]">
                        <span>{item.category}</span>
                        <span>·</span>
                        <span className="font-mono-tabular">{item.resolution}</span>
                        {item.editorProject && (
                          <>
                            <span>·</span>
                            <span>{item.editorProject.layouts.length} 规格布局</span>
                          </>
                        )}
                      </div>
                    </div>
                  </div>

                  {cloudUpd && (
                    <div className="mt-2 flex items-center gap-1.5 rounded-lg border border-amber-200 bg-amber-50/90 px-2 py-1 text-[11px] font-semibold text-amber-800">
                      <RefreshCw className="h-3 w-3 shrink-0" />
                      <span className="truncate">
                        云端母版已更新 v{cloudUpd.latestVersion}
                      </span>
                    </div>
                  )}

                  <div className="mt-3 flex flex-wrap items-center gap-1.5 border-t border-[#E6E4F2] pt-2.5">
                    <button
                      type="button"
                      onClick={() =>
                        onEnterEditor(
                          ensureEditorProjectFromTheme(item, activeDevice),
                          item.id
                        )
                      }
                      className="flex flex-1 items-center justify-center gap-1 rounded-xl bg-[#5E45F8] px-2.5 py-1.5 text-[11.5px] font-bold text-white shadow-xs hover:bg-[#4D34E6] whitespace-nowrap"
                    >
                      <Edit3 className="h-3 w-3" />
                      <span>编辑</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => onApplyTheme(item)}
                      className="rounded-xl border border-[#E6E4F2] bg-white px-2.5 py-1.5 text-[11.5px] font-semibold text-[#1A172B] hover:border-[#5E45F8] hover:text-[#5E45F8] whitespace-nowrap"
                    >
                      应用
                    </button>
                    <button
                      type="button"
                      onClick={() => onDuplicateCustomTheme(item)}
                      title="复制副本"
                      aria-label="复制副本"
                      className="rounded-xl border border-[#E6E4F2] bg-white p-1.5 text-[#6B6880] hover:border-[#5E45F8] hover:text-[#5E45F8]"
                    >
                      <Copy className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={() => onDeleteCustomTheme(item.id)}
                      title="删除自制主题"
                      aria-label="删除自制主题"
                      className="rounded-xl border border-[#E6E4F2] bg-white p-1.5 text-[#6B6880] hover:border-red-300 hover:text-red-600"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Multi-Device Built-in Starter Preview Modal */}
      {previewBuiltin && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#100926]/55 p-4 backdrop-blur-sm"
          onClick={() => setPreviewBuiltin(null)}
        >
          <div
            className="w-full max-w-3xl overflow-hidden rounded-[24px] border border-white/80 bg-white/95 shadow-[0_24px_60px_rgba(24,10,68,0.28)] backdrop-blur-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-[#E6E4F2] px-5 py-3.5">
              <div className="flex items-center gap-2">
                <span className="rounded-md bg-[#EFEBFD] px-2 py-0.5 text-[10.5px] font-bold text-[#5E45F8]">
                  双规格预览
                </span>
                <h4 className="text-[15px] font-extrabold text-[#1A172B]">
                  {previewBuiltin.name}
                </h4>
              </div>
              <button
                type="button"
                onClick={() => setPreviewBuiltin(null)}
                aria-label="关闭模板预览"
                className="rounded-lg p-1.5 text-[#6B6880] hover:bg-[#F5F4FA]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="aurora-dark-card grid grid-cols-1 gap-4 p-5 md:grid-cols-[1fr_140px]">
              <div>
                <div className="mb-1.5 text-[11px] font-semibold text-[#C9C1EA]">
                  横屏带鱼副屏 · <span className="font-mono-tabular">1920 × 462</span>
                </div>
                <div className="aspect-[1920/462] overflow-hidden rounded-xl border border-white/15 bg-black/30 shadow-lg">
                  <ThemeArtwork
                    theme={{
                      id: `${previewBuiltin.id}-land`,
                      name: previewBuiltin.name,
                      displayName: previewBuiltin.name,
                      category: previewBuiltin.category,
                      resolution: '1920×462',
                      orientation: 'landscape',
                      author: '官方内置',
                      downloads: '内置',
                      downloadCount: 0,
                      publishedAt: 0,
                      releasedLabel: '内置',
                      addedAt: 0,
                      addedLabel: '内置',
                      lastUsedAt: 0,
                      useCount: 0,
                      durationMinutes: 15,
                      inPlaylist: false,
                      isFavorite: false,
                      accentHex: previewBuiltin.accentHex,
                      editorProject: createBlankEditorProject(
                        { ...activeDevice, orientation: 'landscape', nativeWidth: 1920, nativeHeight: 462 },
                        { name: previewBuiltin.name, template: previewBuiltin }
                      ),
                    }}
                    className="h-full w-full"
                    showClock={true}
                    liveTick={true}
                    portrait={false}
                  />
                </div>
              </div>
              <div>
                <div className="mb-1.5 text-[11px] font-semibold text-[#C9C1EA]">
                  竖屏副屏 · <span className="font-mono-tabular">462 × 1920</span>
                </div>
                <div className="mx-auto aspect-[462/1920] h-44 overflow-hidden rounded-xl border border-white/15 bg-black/30 shadow-lg">
                  <ThemeArtwork
                    theme={{
                      id: `${previewBuiltin.id}-port`,
                      name: previewBuiltin.name,
                      displayName: previewBuiltin.name,
                      category: previewBuiltin.category,
                      resolution: '462×1920',
                      orientation: 'portrait',
                      author: '官方内置',
                      downloads: '内置',
                      downloadCount: 0,
                      publishedAt: 0,
                      releasedLabel: '内置',
                      addedAt: 0,
                      addedLabel: '内置',
                      lastUsedAt: 0,
                      useCount: 0,
                      durationMinutes: 15,
                      inPlaylist: false,
                      isFavorite: false,
                      accentHex: previewBuiltin.accentHex,
                      editorProject: createBlankEditorProject(
                        { ...activeDevice, orientation: 'portrait', nativeWidth: 1920, nativeHeight: 462 },
                        { name: previewBuiltin.name, template: previewBuiltin }
                      ),
                    }}
                    className="h-full w-full"
                    showClock={true}
                    liveTick={true}
                    portrait={true}
                  />
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-2 bg-[#F8F7FC] px-5 py-3.5">
              <button
                type="button"
                onClick={() => setPreviewBuiltin(null)}
                className="rounded-xl border border-[#E6E4F2] bg-white px-4 py-1.5 text-[12px] font-semibold text-[#56526B] hover:border-[#5E45F8]"
              >
                关闭
              </button>
              <button
                type="button"
                onClick={() => {
                  const target = previewBuiltin;
                  setPreviewBuiltin(null);
                  handleStartFromBuiltin(target);
                }}
                className="flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-4 py-1.5 text-[12px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] hover:opacity-95 whitespace-nowrap"
              >
                <Sparkles className="h-3.5 w-3.5" />
                <span>以此模板进入编辑器 V2.0</span>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Mock WEB Admin & Cloud Architecture Simulator Drawer */}
      {adminDrawerOpen && (
        <div className="fixed inset-0 z-50 flex justify-end bg-[#100926]/50 backdrop-blur-xs">
          <div className="flex h-full w-full max-w-lg flex-col border-l border-[#E6E4F2] bg-white/95 shadow-2xl backdrop-blur-xl">
            <div className="flex items-center justify-between border-b border-[#E6E4F2] px-5 py-4">
              <div className="flex items-center gap-2">
                <Server className="h-4 w-4 text-[#5E45F8]" />
                <h3 className="text-[15.5px] font-bold text-[#1A172B]">
                  云端模板发布台
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setAdminDrawerOpen(false)}
                className="rounded-lg p-1.5 text-[#6B6880] hover:bg-[#F5F4FA]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="flex-1 space-y-5 overflow-y-auto p-5 studio-scroll">
              {/* Publish New Cloud Template Form */}
              <div className="rounded-xl border border-[#E6E4F2] bg-white p-4">
                <div className="flex items-center gap-1.5 text-[13px] font-bold text-[#1A172B]">
                  <Upload className="h-4 w-4 text-[#5E45F8]" />
                  <span>发布新模板到云端目录</span>
                </div>

                <div className="mt-3 space-y-3">
                  <div>
                    <label className="text-[11px] font-semibold text-[#6B6880]">模板标题</label>
                    <input
                      type="text"
                      value={mockTitle}
                      onChange={(e) => setMockTitle(e.target.value)}
                      className="mt-1 w-full rounded-lg border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-1.5 text-[12.5px] text-[#1A172B] outline-none focus:border-[#5E45F8] focus:bg-white"
                    />
                  </div>
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label className="text-[11px] font-semibold text-[#6B6880]">起始色</label>
                      <input
                        type="color"
                        value={mockG1}
                        onChange={(e) => setMockG1(e.target.value)}
                        className="mt-1 h-8 w-full cursor-pointer rounded border border-[#E6E4F2]"
                      />
                    </div>
                    <div>
                      <label className="text-[11px] font-semibold text-[#6B6880]">结束色</label>
                      <input
                        type="color"
                        value={mockG2}
                        onChange={(e) => setMockG2(e.target.value)}
                        className="mt-1 h-8 w-full cursor-pointer rounded border border-[#E6E4F2]"
                      />
                    </div>
                    <div>
                      <label className="text-[11px] font-semibold text-[#6B6880]">强调色</label>
                      <input
                        type="color"
                        value={mockAccent}
                        onChange={(e) => setMockAccent(e.target.value)}
                        className="mt-1 h-8 w-full cursor-pointer rounded border border-[#E6E4F2]"
                      />
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={handlePublishMockCloudTemplate}
                    className="flex w-full items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-[#6A52FF] to-[#563CEB] py-2 text-[12.5px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] hover:opacity-95"
                  >
                    <Cloud className="h-4 w-4" />
                    <span>立即发布</span>
                  </button>
                </div>

                {publishedMockIds.length > 0 && (
                  <div className="mt-4 space-y-2 border-t border-[#E6E4F2] pt-3">
                    <div className="text-[11px] font-bold text-[#6B6880]">
                      已发布列表
                    </div>
                    {publishedMockIds.map((pid) => (
                      <div
                        key={pid}
                        className="flex items-center justify-between rounded-lg bg-[#F8F7FC] px-2.5 py-2 text-[11.5px]"
                      >
                        <div className="flex items-center gap-1.5 truncate">
                          <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
                          <span className="font-mono-tabular font-semibold text-[#1A172B]">{pid}</span>
                        </div>
                        <div className="flex items-center gap-1">
                          <button
                            type="button"
                            onClick={() => {
                              const bumped = cloudResourceAdapter.bumpMockVersion(pid);
                              if (bumped) {
                                setPublishedMockIds((p) => [...p]);
                                onNotify(`已将 ${pid} 云端版本号升级至 v${bumped.version}`);
                              }
                            }}
                            className="rounded border border-[#E6E4F2] bg-white px-2 py-0.5 text-[10.5px] font-semibold text-[#5E45F8] hover:border-[#5E45F8]"
                          >
                            升级版本
                          </button>
                          <button
                            type="button"
                            onClick={() => {
                              cloudResourceAdapter.unpublishMockTemplate(pid);
                              setPublishedMockIds((prev) => prev.filter((x) => x !== pid));
                              onNotify(`已从云端目录热下架 ${pid}（本地已保存工程不受影响）`);
                            }}
                            className="rounded border border-red-200 bg-white px-2 py-0.5 text-[10.5px] font-semibold text-red-600"
                          >
                            下架
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};


