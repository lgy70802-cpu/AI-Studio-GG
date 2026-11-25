import React, { useEffect, useMemo, useState } from 'react';
import {
  Plus,
  Play,
  Square,
  GripVertical,
  Eye,
  Trash2,
  Target,
  Sunrise,
  Moon,
  Waves,
  Sparkles,
  Heart,
  ChevronUp,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Search,
  Check,
  Edit3,
  X,
  AlertTriangle,
  Shuffle,
  Clock,
} from 'lucide-react';
import {
  CarouselPlaylist,
  ContentCategory,
  DeviceInfo,
  MoodTag,
  PlaylistItemEntry,
  ScreenOrientation,
  ThemeItem,
} from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';

interface PlaylistManagerViewProps {
  playlists: CarouselPlaylist[];
  activePlaylistId: string;
  playingPlaylistId: string | null;
  activeThemeId: string;
  countdownSeconds: number | null;
  themes: ThemeItem[];
  activeDevice: DeviceInfo;
  onOrientationChange: (orientation: ScreenOrientation) => void;
  onSelectPlaylist: (playlistId: string) => void;
  onCreatePlaylist: (payload: {
    name: string;
    mood: MoodTag;
    deviceId: string;
    orientation: ScreenOrientation;
  }) => void;
  onUpdatePlaylist: (
    playlistId: string,
    patch: Partial<CarouselPlaylist>,
    notifyMsg?: string
  ) => void;
  onDeletePlaylist: (playlistId: string) => void;
  onTogglePlayPlaylist: (playlistId: string) => void;
  onAddItemsToPlaylist: (playlistId: string, themeIds: string[]) => void;
  onRemoveItemFromPlaylist: (playlistId: string, themeId: string) => void;
  onReorderPlaylistItems: (playlistId: string, fromIndex: number, toIndex: number) => void;
  onApplyAllDuration: (playlistId: string, seconds: number) => void;
  onApplyItemDuration: (playlistId: string, themeId: string, seconds: number) => void;
  onApplyTheme: (theme: ThemeItem) => void;
  onNotify: (msg: string) => void;
}

const MOOD_OPTIONS: { id: MoodTag; icon: React.ComponentType<{ className?: string }> }[] = [
  { id: '专注', icon: Target },
  { id: '清晨', icon: Sunrise },
  { id: '夜晚', icon: Moon },
  { id: '放松', icon: Waves },
  { id: '灵感', icon: Sparkles },
  { id: '喜爱', icon: Heart },
];

type DurationUnit = 's' | 'm' | 'h';

function clampSeconds(secs: number): number {
  if (!Number.isFinite(secs)) return 30;
  return Math.min(86400, Math.max(10, Math.round(secs)));
}

function secondsToUnit(secs: number): { value: number; unit: DurationUnit } {
  const safe = clampSeconds(secs);
  if (safe >= 3600 && safe % 3600 === 0) {
    return { value: safe / 3600, unit: 'h' };
  }
  if (safe >= 60 && safe % 60 === 0) {
    return { value: safe / 60, unit: 'm' };
  }
  return { value: safe, unit: 's' };
}

function unitToSeconds(val: number, unit: DurationUnit): number {
  const num = Number.isFinite(val) ? val : 30;
  const multiplier = unit === 'h' ? 3600 : unit === 'm' ? 60 : 1;
  return clampSeconds(num * multiplier);
}

function formatDurationSummary(totalSeconds: number): string {
  if (totalSeconds <= 0) return '0 秒';
  const hours = Math.floor(totalSeconds / 3600);
  const mins = Math.floor((totalSeconds % 3600) / 60);
  const secs = totalSeconds % 60;
  const parts: string[] = [];
  if (hours > 0) parts.push(`${hours} 小时`);
  if (mins > 0) parts.push(`${mins} 分钟`);
  if (secs > 0 && hours === 0) parts.push(`${secs} 秒`);
  return parts.join(' ');
}

interface DurationEditorProps {
  seconds: number;
  onChange: (nextSeconds: number) => void;
  compact?: boolean;
}

const DurationEditor: React.FC<DurationEditorProps> = ({ seconds, onChange, compact }) => {
  const initial = secondsToUnit(seconds);
  const [draftText, setDraftText] = useState<string>(String(initial.value));
  const [draftUnit, setDraftUnit] = useState<DurationUnit>(initial.unit);
  const [isEditing, setIsEditing] = useState(false);

  useEffect(() => {
    if (!isEditing) {
      const next = secondsToUnit(seconds);
      setDraftText(String(next.value));
      setDraftUnit(next.unit);
    }
  }, [seconds, isEditing]);

  const commitValue = (text: string, unit: DurationUnit) => {
    const parsed = Number(text.trim());
    const safeVal = Number.isFinite(parsed) && text.trim() !== '' ? parsed : 30;
    const clamped = unitToSeconds(safeVal, unit);
    onChange(clamped);
    const formatted = secondsToUnit(clamped);
    setDraftText(String(formatted.value));
    setDraftUnit(formatted.unit);
  };

  const handleUnitChange = (nextUnit: DurationUnit) => {
    const parsed = Number(draftText.trim());
    const baseVal = Number.isFinite(parsed) && draftText.trim() !== '' ? parsed : 1;
    const clamped = unitToSeconds(baseVal, nextUnit);
    setDraftUnit(nextUnit);
    onChange(clamped);
    const multiplier = nextUnit === 'h' ? 3600 : nextUnit === 'm' ? 60 : 1;
    setDraftText(String(Math.max(nextUnit === 's' ? 10 : 1, Math.round(clamped / multiplier))));
  };

  return (
    <div className="inline-flex items-center gap-1 rounded-xl border border-[#E6E4F2] bg-white px-2 py-1 shadow-2xs">
      <input
        type="text"
        inputMode="numeric"
        value={draftText}
        onFocus={() => setIsEditing(true)}
        onChange={(e) => {
          const digitsOnly = e.target.value.replace(/[^\d]/g, '');
          setDraftText(digitsOnly);
        }}
        onBlur={() => {
          setIsEditing(false);
          commitValue(draftText, draftUnit);
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.currentTarget.blur();
          }
        }}
        aria-label="停留时长数值"
        className={`bg-transparent text-center font-mono-tabular font-bold text-[#1A172B] focus:outline-none ${
          compact ? 'w-10 text-[12px]' : 'w-12 text-[12.5px]'
        }`}
      />
      <select
        value={draftUnit}
        onChange={(e) => handleUnitChange(e.target.value as DurationUnit)}
        aria-label="停留时长单位"
        className="cursor-pointer bg-transparent text-[11.5px] font-semibold text-[#6B6880] focus:outline-none"
      >
        <option value="s">秒</option>
        <option value="m">分</option>
        <option value="h">时</option>
      </select>
    </div>
  );
};

export const PlaylistManagerView: React.FC<PlaylistManagerViewProps> = ({
  playlists,
  activePlaylistId,
  playingPlaylistId,
  activeThemeId,
  countdownSeconds,
  themes,
  activeDevice,
  onOrientationChange,
  onSelectPlaylist,
  onCreatePlaylist,
  onUpdatePlaylist,
  onDeletePlaylist,
  onTogglePlayPlaylist,
  onAddItemsToPlaylist,
  onRemoveItemFromPlaylist,
  onReorderPlaylistItems,
  onApplyAllDuration,
  onApplyItemDuration,
  onApplyTheme,
  onNotify,
}) => {
  const isPortrait = activeDevice.orientation === 'portrait';
  const previewAspectRatio = isPortrait
    ? `${activeDevice.nativeHeight} / ${activeDevice.nativeWidth}`
    : `${activeDevice.nativeWidth} / ${activeDevice.nativeHeight}`;
  const deviceShortName = activeDevice.name.split('·')[0].trim();

  // Strictly filter playlists to match activeDevice + activeDevice.orientation
  const scopedPlaylists = useMemo(
    () =>
      playlists.filter(
        (p) => p.deviceId === activeDevice.id && p.orientation === activeDevice.orientation
      ),
    [playlists, activeDevice.id, activeDevice.orientation]
  );

  const activePlaylist = useMemo(
    () => scopedPlaylists.find((p) => p.id === activePlaylistId) ?? scopedPlaylists[0] ?? null,
    [scopedPlaylists, activePlaylistId]
  );

  // Create / Edit Modal state
  const [formModalMode, setFormModalMode] = useState<'create' | 'edit' | null>(null);
  const [formName, setFormName] = useState('');
  const [formMood, setFormMood] = useState<MoodTag>('专注');

  // Add Content Multi-select Modal state
  const [addModalOpen, setAddModalOpen] = useState(false);
  const [addQuery, setAddQuery] = useState('');
  const [addCategory, setAddCategory] = useState<ContentCategory | '全部'>('全部');
  const [selectedToAdd, setSelectedToAdd] = useState<string[]>([]);

  // Preview Modal state
  const [previewTheme, setPreviewTheme] = useState<ThemeItem | null>(null);

  // Delete Confirm Modal state
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);

  // Drag & Drop state
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  // Master duration draft for "应用到全部"
  const [masterSecondsDraft, setMasterSecondsDraft] = useState<number>(
    activePlaylist?.defaultDurationSeconds ?? 30
  );

  useEffect(() => {
    if (activePlaylist) {
      setMasterSecondsDraft(activePlaylist.defaultDurationSeconds);
    }
  }, [activePlaylist?.id, activePlaylist?.defaultDurationSeconds]);

  const openCreateModal = () => {
    setFormName('');
    setFormMood('专注');
    setFormModalMode('create');
  };

  const openEditModal = () => {
    if (!activePlaylist) return;
    setFormName(activePlaylist.name);
    setFormMood(activePlaylist.mood);
    setFormModalMode('edit');
  };

  const handleFormSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = formName.trim().slice(0, 30);
    if (!trimmed) {
      onNotify('请输入轮播列表名称');
      return;
    }
    if (formModalMode === 'create') {
      onCreatePlaylist({
        name: trimmed,
        mood: formMood,
        deviceId: activeDevice.id,
        orientation: activeDevice.orientation,
      });
    } else if (formModalMode === 'edit' && activePlaylist) {
      onUpdatePlaylist(
        activePlaylist.id,
        { name: trimmed, mood: formMood },
        `已更新轮播列表「${trimmed}」`
      );
    }
    setFormModalMode(null);
  };

  const openAddModal = () => {
    setAddQuery('');
    setAddCategory('全部');
    setSelectedToAdd([]);
    setAddModalOpen(true);
  };

  const activeEntrySet = useMemo(
    () => new Set(activePlaylist?.items.map((i) => i.themeId) ?? []),
    [activePlaylist]
  );

  const modalThemes = useMemo(() => {
    const q = addQuery.trim().toLowerCase();
    return themes.filter((t) => {
      if (addCategory !== '全部' && t.category !== addCategory) return false;
      if (
        q !== '' &&
        !t.displayName.toLowerCase().includes(q) &&
        !t.name.toLowerCase().includes(q)
      ) {
        return false;
      }
      return true;
    });
  }, [themes, addCategory, addQuery]);

  const toggleSelectAdd = (theme: ThemeItem) => {
    if (activeEntrySet.has(theme.id)) {
      onNotify(`「${theme.displayName}」已在当前列表中，重复内容将自动跳过`);
      return;
    }
    setSelectedToAdd((prev) =>
      prev.includes(theme.id) ? prev.filter((id) => id !== theme.id) : [...prev, theme.id]
    );
  };

  const resolvedItems: { entry: PlaylistItemEntry; theme: ThemeItem }[] = useMemo(() => {
    if (!activePlaylist) return [];
    return activePlaylist.items
      .map((entry) => {
        const theme = themes.find((t) => t.id === entry.themeId);
        return theme ? { entry, theme } : null;
      })
      .filter((x): x is { entry: PlaylistItemEntry; theme: ThemeItem } => x !== null);
  }, [activePlaylist, themes]);

  const totalDurationSeconds = useMemo(
    () => resolvedItems.reduce((sum, item) => sum + item.entry.durationSeconds, 0),
    [resolvedItems]
  );

  const isPlayingActivePlaylist = Boolean(
    activePlaylist && playingPlaylistId === activePlaylist.id
  );

  return (
    <div className="space-y-5">
      {/* Studio Header Bar with Device/Orientation Context Switcher */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-[22px] font-bold tracking-tight text-[#1A172B]">轮播列表</h2>
          <p className="mt-0.5 text-[13px] text-[#7A7690]">
            当前归属：{activeDevice.name} · {isPortrait ? '竖屏' : '横屏'}（
            <span className="font-mono-tabular">{activeDevice.resolution}</span>）·
            切换顶部设备或方向将自动同步对应列表
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          {/* Quick Orientation Scope Toggle */}
          <div
            className="flex items-center rounded-full border border-[#E6E4F2] bg-white p-1 shadow-2xs"
            role="group"
            aria-label="切换轮播方向"
          >
            {(
              [
                { id: 'landscape', label: '横屏列表' },
                { id: 'portrait', label: '竖屏列表' },
              ] as const
            ).map((opt) => (
              <button
                key={opt.id}
                type="button"
                onClick={() => onOrientationChange(opt.id)}
                className={`rounded-full px-3.5 py-1 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  activeDevice.orientation === opt.id
                    ? 'bg-[#5E45F8] text-white shadow-xs'
                    : 'text-[#6B6880] hover:text-[#1A172B]'
                }`}
              >
                {opt.label}
              </button>
            ))}
          </div>

          <button
            type="button"
            onClick={openCreateModal}
            className="flex items-center gap-1.5 rounded-full bg-gradient-to-r from-[#6A52FF] to-[#563CEB] px-4 py-2 text-[12.5px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] transition-transform duration-150 hover:scale-[1.02] whitespace-nowrap"
          >
            <Plus className="h-4 w-4" />
            <span>新建列表</span>
          </button>
        </div>
      </div>

      {/* Horizontal Playlist Deck Strip (Replaces Old Left Sidebar Column) */}
      <div className="flex flex-wrap items-stretch gap-3">
        {scopedPlaylists.map((pl) => {
          const isSelected = activePlaylist?.id === pl.id;
          const isPlayingThis = playingPlaylistId === pl.id;
          const plTotalSeconds = pl.items.reduce((acc, i) => acc + i.durationSeconds, 0);
          const moodMeta = MOOD_OPTIONS.find((m) => m.id === pl.mood);
          const MoodIcon = moodMeta?.icon ?? Heart;

          return (
            <button
              key={pl.id}
              type="button"
              onClick={() => onSelectPlaylist(pl.id)}
              className={`group flex min-w-[210px] max-w-[280px] flex-1 flex-col justify-between rounded-[20px] border px-4 py-3.5 text-left transition-all duration-150 ${
                isSelected
                  ? 'border-[#5E45F8] bg-white shadow-[0_12px_28px_rgba(94,69,248,0.14)] ring-1 ring-[#5E45F8]/25'
                  : 'border-white/80 bg-white/85 shadow-[0_6px_18px_rgba(30,20,80,0.04)] hover:border-[#CFC7F8] hover:bg-white'
              }`}
            >
              <div className="flex items-center justify-between gap-2">
                <span className="truncate text-[14.5px] font-bold text-[#1A172B]">
                  {pl.name}
                </span>
                {isPlayingThis && (
                  <span className="shrink-0 rounded-full bg-[#5E45F8] px-2 py-0.5 text-[10px] font-bold text-white">
                    轮播中
                  </span>
                )}
              </div>

              <div className="mt-2 flex items-center justify-between gap-2 text-[11.5px] text-[#7A7690]">
                <span className="inline-flex items-center gap-1 font-semibold text-[#5E45F8]">
                  <MoodIcon className="h-3 w-3" />
                  <span>{pl.mood}</span>
                </span>
                <span className="font-mono-tabular">
                  {pl.items.length} 项 · {formatDurationSummary(plTotalSeconds)}
                </span>
              </div>
            </button>
          );
        })}

        <button
          type="button"
          onClick={openCreateModal}
          className="flex min-w-[140px] items-center justify-center gap-1.5 rounded-[20px] border border-dashed border-[#C7BFF4] bg-white/60 px-4 py-3.5 text-[12.5px] font-semibold text-[#5E45F8] transition-colors hover:border-[#5E45F8] hover:bg-[#F5F2FF]"
        >
          <Plus className="h-4 w-4" />
          <span>新建{isPortrait ? '竖屏' : '横屏'}列表</span>
        </button>
      </div>

      {/* Active Playlist Stage */}
      {activePlaylist ? (
        <div className="space-y-4">
          {/* Cosmic Aurora Control Hero */}
          <section className="aurora-dark-card relative overflow-hidden rounded-[26px] p-5 text-white shadow-[0_20px_48px_rgba(24,10,68,0.24)] sm:p-6">
            <div className="relative z-10 flex flex-wrap items-start justify-between gap-4">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2 text-[12px] font-medium text-white/75">
                  <span>{deviceShortName}</span>
                  <span aria-hidden="true">·</span>
                  <span>{isPortrait ? '竖屏模式' : '横屏模式'}</span>
                  <span aria-hidden="true">·</span>
                  <span className="font-mono-tabular">{resolvedItems.length} 项</span>
                  <span aria-hidden="true">·</span>
                  <span className="font-mono-tabular">
                    总时长 {formatDurationSummary(totalDurationSeconds)}
                  </span>
                  {isPlayingActivePlaylist && countdownSeconds !== null && (
                    <>
                      <span aria-hidden="true">·</span>
                      <span className="inline-flex items-center gap-1 font-mono-tabular text-[#6EE7B7]">
                        <Clock className="h-3 w-3" />
                        {countdownSeconds}s 后切换下一项
                      </span>
                    </>
                  )}
                </div>

                <div className="mt-1.5 flex items-center gap-2.5">
                  <h3 className="truncate text-[22px] font-bold tracking-tight text-white">
                    {activePlaylist.name}
                  </h3>
                  <button
                    type="button"
                    onClick={openEditModal}
                    title="修改名称与心情"
                    className="flex h-7 w-7 items-center justify-center rounded-lg bg-white/10 text-white/80 transition-colors hover:bg-white/20 hover:text-white"
                  >
                    <Edit3 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>

              {/* Hero Right Actions */}
              <div className="flex flex-wrap items-center gap-2 shrink-0">
                <button
                  type="button"
                  onClick={() => onTogglePlayPlaylist(activePlaylist.id)}
                  className={`flex items-center gap-1.5 rounded-full px-4 py-2 text-[12.5px] font-bold transition-all whitespace-nowrap ${
                    isPlayingActivePlaylist
                      ? 'bg-[#5E45F8] text-white shadow-[0_6px_18px_rgba(94,69,248,0.45)] ring-1 ring-white/25'
                      : 'bg-white text-[#1A172B] shadow-md hover:bg-[#F3F0FF]'
                  }`}
                >
                  {isPlayingActivePlaylist ? (
                    <>
                      <Square className="h-3.5 w-3.5 fill-current" />
                      <span>停止轮播</span>
                    </>
                  ) : (
                    <>
                      <Play className="h-3.5 w-3.5 fill-current" />
                      <span>开始轮播</span>
                    </>
                  )}
                </button>

                <button
                  type="button"
                  onClick={openAddModal}
                  className="flex items-center gap-1.5 rounded-full border border-white/20 bg-white/12 px-4 py-2 text-[12.5px] font-semibold text-white backdrop-blur-md transition-colors hover:bg-white/20 whitespace-nowrap"
                >
                  <Plus className="h-3.5 w-3.5" />
                  <span>添加内容</span>
                </button>

                <button
                  type="button"
                  onClick={() => setDeleteConfirmOpen(true)}
                  title="删除当前轮播列表"
                  aria-label="删除当前轮播列表"
                  className="flex h-9 w-9 items-center justify-center rounded-full border border-white/15 bg-white/10 text-white/80 transition-colors hover:border-red-300/60 hover:bg-red-500/80 hover:text-white"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </div>

            {/* Mood Selector Strip inside Aurora Hero */}
            <div className="relative z-10 mt-5 flex flex-wrap items-center gap-2 border-t border-white/12 pt-4">
              <span className="mr-1 text-[12px] font-semibold text-white/70">心情标签</span>
              {MOOD_OPTIONS.map((opt) => {
                const Icon = opt.icon;
                const active = activePlaylist.mood === opt.id;
                return (
                  <button
                    key={opt.id}
                    type="button"
                    onClick={() =>
                      onUpdatePlaylist(
                        activePlaylist.id,
                        { mood: opt.id },
                        `已将列表心情标签设为「${opt.id}」`
                      )
                    }
                    className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-[12px] font-semibold transition-all whitespace-nowrap ${
                      active
                        ? 'bg-[#5E45F8] text-white shadow-[0_4px_12px_rgba(94,69,248,0.45)]'
                        : 'border border-white/15 bg-white/8 text-white/80 hover:bg-white/18 hover:text-white'
                    }`}
                  >
                    <Icon className="h-3.5 w-3.5" />
                    <span>{opt.id}</span>
                  </button>
                );
              })}
            </div>
          </section>

          {/* Slim Frosted Playback Settings Strip */}
          <div className="flex flex-wrap items-center justify-between gap-4 rounded-[20px] border border-white/80 bg-white/92 px-5 py-3.5 shadow-[0_8px_24px_rgba(30,20,80,0.04)]">
            <div className="flex items-center gap-3">
              <button
                type="button"
                role="switch"
                aria-checked={activePlaylist.shuffle}
                onClick={() =>
                  onUpdatePlaylist(
                    activePlaylist.id,
                    { shuffle: !activePlaylist.shuffle },
                    !activePlaylist.shuffle
                      ? '已开启随机播放（自动切换与“下一项”均会避开当前项）'
                      : '已切换为按列表顺序播放'
                  )
                }
                className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
                  activePlaylist.shuffle ? 'bg-[#5E45F8]' : 'bg-[#D6D3E6]'
                }`}
              >
                <span
                  className={`absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform ${
                    activePlaylist.shuffle ? 'left-5.5' : 'left-0.5'
                  }`}
                />
              </button>
              <div className="flex items-center gap-1.5 text-[13px] font-semibold text-[#1A172B]">
                <Shuffle className="h-3.5 w-3.5 text-[#5E45F8]" />
                <span>随机播放</span>
                <span className="hidden text-[11.5px] font-normal text-[#8B87A1] sm:inline">
                  （多条目时自动避开当前画面）
                </span>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2.5">
              <span className="text-[12.5px] font-semibold text-[#6B6880]">统一每项停留</span>
              <DurationEditor
                seconds={masterSecondsDraft}
                onChange={setMasterSecondsDraft}
              />
              <button
                type="button"
                onClick={() => onApplyAllDuration(activePlaylist.id, masterSecondsDraft)}
                className="rounded-full bg-[#EFEBFD] px-3.5 py-1.5 text-[12px] font-bold text-[#5E45F8] transition-colors hover:bg-[#E2DAFC] whitespace-nowrap"
              >
                应用到全部
              </button>
              <span className="text-[11px] text-[#9A96AE]">10 秒 ~ 24 小时</span>
            </div>
          </div>

          {/* Full-Width Visual Queue Grid */}
          {resolvedItems.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-[24px] border border-dashed border-[#D3CCF6] bg-white/80 py-14 text-center">
              <p className="text-[15px] font-bold text-[#1A172B]">
                当前{isPortrait ? '竖屏' : '横屏'}轮播列表暂无素材
              </p>
              <p className="mt-1 text-[12.5px] text-[#8B87A1]">
                点击上方「+ 添加内容」批量加入，或从首页 / 资源库卡片点击「+ 轮播」
              </p>
              <button
                type="button"
                onClick={openAddModal}
                className="mt-4 flex items-center gap-1.5 rounded-full bg-[#5E45F8] px-5 py-2 text-[12.5px] font-bold text-white shadow-sm hover:bg-[#4D34E6]"
              >
                <Plus className="h-4 w-4" />
                <span>添加内容</span>
              </button>
            </div>
          ) : isPortrait ? (
            /* Portrait Mode: Full-Width Multi-Column Vertical Poster Queue */
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
              {resolvedItems.map(({ entry, theme }, idx) => {
                const isCurrentOutput = theme.id === activeThemeId;
                return (
                  <div
                    key={theme.id}
                    draggable
                    onDragStart={() => setDragIndex(idx)}
                    onDragOver={(e) => {
                      e.preventDefault();
                      setDragOverIndex(idx);
                    }}
                    onDrop={() => {
                      if (dragIndex !== null && dragIndex !== idx) {
                        onReorderPlaylistItems(activePlaylist.id, dragIndex, idx);
                      }
                      setDragIndex(null);
                      setDragOverIndex(null);
                    }}
                    onDragEnd={() => {
                      setDragIndex(null);
                      setDragOverIndex(null);
                    }}
                    className={`group flex flex-col rounded-[22px] border bg-white p-2.5 shadow-[0_10px_26px_rgba(30,20,80,0.05)] transition-all ${
                      dragOverIndex === idx
                        ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/30'
                        : isCurrentOutput
                          ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/20'
                          : 'border-white/80 hover:border-[#CFC7F8]'
                    }`}
                  >
                    <div
                      style={{ aspectRatio: previewAspectRatio }}
                      className="relative w-full overflow-hidden rounded-[16px] bg-[#160F30]"
                    >
                      <button
                        type="button"
                        onClick={() => onApplyTheme(theme)}
                        className="block h-full w-full text-left"
                        title="点击立即输出到设备"
                      >
                        <ThemeArtwork
                          theme={theme}
                          className="h-full w-full"
                          showClock={true}
                          compact={true}
                          portrait={true}
                        />
                      </button>

                      <span className="pointer-events-none absolute left-2 top-2 flex h-6 min-w-6 items-center justify-center rounded-full bg-[#12092E]/75 px-1.5 font-mono-tabular text-[11px] font-bold text-white backdrop-blur-md">
                        {idx + 1}
                      </span>

                      {isCurrentOutput && (
                        <span className="pointer-events-none absolute bottom-2 left-2 rounded-full bg-[#5E45F8] px-2 py-0.5 text-[10px] font-bold text-white shadow">
                          输出中
                        </span>
                      )}

                      <div className="absolute right-2 top-2 flex items-center gap-1 opacity-0 transition-opacity group-hover:opacity-100">
                        <button
                          type="button"
                          onClick={() => setPreviewTheme(theme)}
                          title="临时预览"
                          className="flex h-7 w-7 items-center justify-center rounded-full bg-[#12092E]/75 text-white backdrop-blur-md hover:bg-[#5E45F8]"
                        >
                          <Eye className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => onRemoveItemFromPlaylist(activePlaylist.id, theme.id)}
                          title="从当前列表移除"
                          className="flex h-7 w-7 items-center justify-center rounded-full bg-[#12092E]/75 text-white backdrop-blur-md hover:bg-red-500"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>

                    <div className="mt-2.5 flex items-center justify-between gap-1 px-0.5">
                      <div className="min-w-0">
                        <div className="truncate text-[13px] font-bold text-[#1A172B]">
                          {theme.displayName}
                        </div>
                        <div className="text-[11px] text-[#8B87A1]">{theme.category}</div>
                      </div>
                      <span
                        className="cursor-grab text-[#B3AEC6] hover:text-[#5E45F8]"
                        title="拖动卡片调整顺序"
                      >
                        <GripVertical className="h-4 w-4" />
                      </span>
                    </div>

                    <div className="mt-2 flex items-center justify-between gap-1 border-t border-[#F1EFF8] pt-2">
                      <DurationEditor
                        seconds={entry.durationSeconds}
                        compact
                        onChange={(secs) =>
                          onApplyItemDuration(activePlaylist.id, theme.id, secs)
                        }
                      />
                      <div className="flex items-center gap-0.5">
                        <button
                          type="button"
                          disabled={idx === 0}
                          onClick={() => onReorderPlaylistItems(activePlaylist.id, idx, idx - 1)}
                          aria-label="前移"
                          className="flex h-7 w-6 items-center justify-center rounded-md text-[#7A7690] hover:bg-[#F4F3FA] hover:text-[#5E45F8] disabled:opacity-30"
                        >
                          <ChevronLeft className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          disabled={idx === resolvedItems.length - 1}
                          onClick={() => onReorderPlaylistItems(activePlaylist.id, idx, idx + 1)}
                          aria-label="后移"
                          className="flex h-7 w-6 items-center justify-center rounded-md text-[#7A7690] hover:bg-[#F4F3FA] hover:text-[#5E45F8] disabled:opacity-30"
                        >
                          <ChevronRight className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            /* Landscape Mode: 2-Column Visual Queue Cards with Full-Bleed Artwork & Sleek Control Bar */
            <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
              {resolvedItems.map(({ entry, theme }, idx) => {
                const isCurrentOutput = theme.id === activeThemeId;
                return (
                  <div
                    key={theme.id}
                    draggable
                    onDragStart={() => setDragIndex(idx)}
                    onDragOver={(e) => {
                      e.preventDefault();
                      setDragOverIndex(idx);
                    }}
                    onDrop={() => {
                      if (dragIndex !== null && dragIndex !== idx) {
                        onReorderPlaylistItems(activePlaylist.id, dragIndex, idx);
                      }
                      setDragIndex(null);
                      setDragOverIndex(null);
                    }}
                    onDragEnd={() => {
                      setDragIndex(null);
                      setDragOverIndex(null);
                    }}
                    className={`group flex flex-col rounded-[22px] border bg-white p-3 shadow-[0_10px_26px_rgba(30,20,80,0.04)] transition-all ${
                      dragOverIndex === idx
                        ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/25'
                        : isCurrentOutput
                          ? 'border-[#5E45F8] ring-2 ring-[#5E45F8]/20'
                          : 'border-white/80 hover:border-[#CFC7F8]'
                    }`}
                  >
                    {/* Full-Proportional Landscape Thumbnail */}
                    <div
                      style={{ aspectRatio: previewAspectRatio }}
                      className="relative w-full overflow-hidden rounded-[15px] bg-[#160F30]"
                    >
                      <button
                        type="button"
                        onClick={() => onApplyTheme(theme)}
                        title="点击立即输出到当前设备"
                        className="block h-full w-full text-left"
                      >
                        <ThemeArtwork
                          theme={theme}
                          className="h-full w-full"
                          showClock={true}
                          compact={true}
                        />
                      </button>

                      <span className="pointer-events-none absolute left-2.5 top-2.5 flex h-6 min-w-6 items-center justify-center rounded-full bg-[#12092E]/78 px-1.5 font-mono-tabular text-[11px] font-bold text-white backdrop-blur-md">
                        {idx + 1}
                      </span>

                      {isCurrentOutput && (
                        <span className="pointer-events-none absolute bottom-2.5 left-2.5 rounded-full bg-[#5E45F8] px-2.5 py-0.5 text-[10.5px] font-bold text-white shadow">
                          当前正在输出
                        </span>
                      )}

                      <div className="absolute right-2.5 top-2.5 flex items-center gap-1.5 opacity-0 transition-opacity group-hover:opacity-100">
                        <button
                          type="button"
                          onClick={() => setPreviewTheme(theme)}
                          title="临时预览"
                          aria-label="临时预览"
                          className="flex h-8 w-8 items-center justify-center rounded-full bg-[#12092E]/75 text-white backdrop-blur-md transition-colors hover:bg-[#5E45F8]"
                        >
                          <Eye className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => onRemoveItemFromPlaylist(activePlaylist.id, theme.id)}
                          title="从当前列表移除（保留在资源库）"
                          aria-label="移除条目"
                          className="flex h-8 w-8 items-center justify-center rounded-full bg-[#12092E]/75 text-white backdrop-blur-md transition-colors hover:bg-red-500"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>

                    {/* Sleek Card Footer Controls */}
                    <div className="mt-3 flex flex-wrap items-center justify-between gap-2 px-1">
                      <div className="flex min-w-0 flex-1 items-center gap-2">
                        <span
                          className="cursor-grab text-[#B3AEC6] hover:text-[#5E45F8]"
                          title="拖动卡片调整顺序"
                        >
                          <GripVertical className="h-4 w-4" />
                        </span>
                        <div className="min-w-0">
                          <div className="truncate text-[13.5px] font-bold text-[#1A172B]">
                            {theme.displayName}
                          </div>
                          <div className="text-[11px] text-[#8B87A1]">
                            {theme.category} · <span className="font-mono-tabular">{activeDevice.resolution}</span>
                          </div>
                        </div>
                      </div>

                      <div className="flex items-center gap-1.5 shrink-0">
                        <DurationEditor
                          seconds={entry.durationSeconds}
                          compact
                          onChange={(secs) =>
                            onApplyItemDuration(activePlaylist.id, theme.id, secs)
                          }
                        />
                        <div className="flex items-center gap-0.5">
                          <button
                            type="button"
                            disabled={idx === 0}
                            onClick={() => onReorderPlaylistItems(activePlaylist.id, idx, idx - 1)}
                            aria-label="上移条目"
                            title="上移"
                            className="flex h-7 w-7 items-center justify-center rounded-lg text-[#7A7690] transition-colors hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:opacity-30"
                          >
                            <ChevronUp className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            disabled={idx === resolvedItems.length - 1}
                            onClick={() => onReorderPlaylistItems(activePlaylist.id, idx, idx + 1)}
                            aria-label="下移条目"
                            title="下移"
                            className="flex h-7 w-7 items-center justify-center rounded-lg text-[#7A7690] transition-colors hover:bg-[#EFEBFD] hover:text-[#5E45F8] disabled:opacity-30"
                          >
                            <ChevronDown className="h-4 w-4" />
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center rounded-[24px] bg-white py-16 text-center shadow-[0_12px_32px_rgba(30,20,80,0.04)]">
          <p className="text-[15.5px] font-bold text-[#1A172B]">
            {deviceShortName}（{isPortrait ? '竖屏' : '横屏'}）暂无轮播列表
          </p>
          <p className="mt-1 text-[13px] text-[#8B87A1]">
            为当前设备与显示方向创建一个专属轮播列表吧
          </p>
          <button
            type="button"
            onClick={openCreateModal}
            className="mt-4 flex items-center gap-1.5 rounded-full bg-[#5E45F8] px-5 py-2 text-[12.5px] font-bold text-white"
          >
            <Plus className="h-4 w-4" />
            <span>新建轮播列表</span>
          </button>
        </div>
      )}

      {/* Create / Edit Playlist Modal */}
      {formModalMode && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#140B36]/45 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
        >
          <form
            onSubmit={handleFormSubmit}
            className="w-full max-w-md rounded-[26px] border border-white/70 bg-white p-6 shadow-[0_30px_80px_rgba(20,10,60,0.3)]"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-[18px] font-bold text-[#1A172B]">
                {formModalMode === 'create' ? '新建轮播列表' : '修改轮播列表'}
              </h3>
              <button
                type="button"
                onClick={() => setFormModalMode(null)}
                className="flex h-8 w-8 items-center justify-center rounded-full bg-[#F4F3FA] text-[#56526B] hover:bg-[#E9E6F7]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-5 space-y-4">
              <div className="rounded-xl bg-[#F5F3FF] px-3.5 py-2 text-[12px] font-semibold text-[#5E45F8]">
                归属设备与方向：{activeDevice.name} · {isPortrait ? '竖屏' : '横屏'}（{activeDevice.resolution}）
              </div>

              <div>
                <div className="mb-1.5 flex items-center justify-between text-[12px] font-semibold text-[#6B6880]">
                  <label htmlFor="playlist-name-input">列表名称</label>
                  <span className="font-mono-tabular">{formName.length}/30</span>
                </div>
                <input
                  id="playlist-name-input"
                  type="text"
                  maxLength={30}
                  value={formName}
                  onChange={(e) => setFormName(e.target.value.slice(0, 30))}
                  placeholder="例如：上班专注、夜间氛围..."
                  className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3.5 py-2.5 text-[13.5px] font-medium text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                  autoFocus
                />
              </div>

              <div>
                <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                  选择心情标签
                </label>
                <div className="flex flex-wrap gap-2">
                  {MOOD_OPTIONS.map((opt) => {
                    const Icon = opt.icon;
                    const active = formMood === opt.id;
                    return (
                      <button
                        key={opt.id}
                        type="button"
                        onClick={() => setFormMood(opt.id)}
                        className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[12px] font-semibold transition-colors ${
                          active
                            ? 'bg-[#5E45F8] text-white'
                            : 'bg-[#F5F4FA] text-[#56526B] hover:bg-[#ECE8FC]'
                        }`}
                      >
                        <Icon className="h-3.5 w-3.5" />
                        <span>{opt.id}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            </div>

            <div className="mt-6 flex justify-end gap-2.5">
              <button
                type="button"
                onClick={() => setFormModalMode(null)}
                className="rounded-full bg-[#F4F3FA] px-4 py-2 text-[12.5px] font-semibold text-[#56526B] hover:bg-[#EAE7F6]"
              >
                取消
              </button>
              <button
                type="submit"
                className="rounded-full bg-[#5E45F8] px-5 py-2 text-[12.5px] font-bold text-white shadow-[0_8px_18px_rgba(94,69,248,0.3)] hover:bg-[#4D34E6]"
              >
                {formModalMode === 'create' ? '创建列表' : '保存修改'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Batch Add Content Modal */}
      {addModalOpen && activePlaylist && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#140B36]/45 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
        >
          <div className="w-full max-w-4xl overflow-hidden rounded-[26px] border border-white/70 bg-[#F7F7FC] shadow-[0_30px_80px_rgba(20,10,60,0.3)]">
            <div className="flex items-center justify-between border-b border-[#ECEAF6] bg-white px-6 py-4">
              <div>
                <h3 className="text-[17px] font-bold text-[#1A172B]">
                  向「{activePlaylist.name}」添加内容
                </h3>
                <p className="text-[12px] text-[#7A7690]">
                  支持多选批量加入；同一素材在当前列表中仅保留一项，重复项将自动跳过
                </p>
              </div>
              <button
                type="button"
                onClick={() => setAddModalOpen(false)}
                className="flex h-8 w-8 items-center justify-center rounded-full bg-[#F4F3FA] text-[#56526B] hover:bg-[#E9E6F7]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 px-6 pt-4">
              <div className="flex items-center rounded-xl bg-white p-1 shadow-sm">
                {(['全部', '动态时钟', '壁纸', '主题'] as const).map((cat) => (
                  <button
                    key={cat}
                    type="button"
                    onClick={() => setAddCategory(cat)}
                    className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                      addCategory === cat
                        ? 'bg-[#5E45F8] text-white'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    {cat}
                  </button>
                ))}
              </div>

              <div className="flex items-center rounded-xl border border-[#ECEAF6] bg-white px-3 py-1.5">
                <Search className="h-3.5 w-3.5 text-[#9A96AE]" />
                <input
                  type="text"
                  value={addQuery}
                  onChange={(e) => setAddQuery(e.target.value)}
                  placeholder="搜索壁纸、时钟或主题..."
                  className="w-48 bg-transparent pl-2 text-[12.5px] text-[#1A172B] focus:outline-none"
                />
              </div>
            </div>

            <div
              className={`grid max-h-[440px] gap-4 overflow-y-auto p-6 studio-scroll ${
                isPortrait
                  ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-4'
                  : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3'
              }`}
            >
              {modalThemes.map((item) => {
                const alreadyIn = activeEntrySet.has(item.id);
                const checked = selectedToAdd.includes(item.id);
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => toggleSelectAdd(item)}
                    style={{ aspectRatio: previewAspectRatio }}
                    className={`group relative w-full overflow-hidden rounded-[18px] bg-[#160F30] text-left transition-all ${
                      checked
                        ? 'ring-[2.5px] ring-[#5E45F8] ring-offset-2 ring-offset-[#F7F7FC]'
                        : alreadyIn
                          ? 'opacity-70 ring-1 ring-white/40'
                          : 'hover:-translate-y-0.5 hover:shadow-lg'
                    }`}
                  >
                    <ThemeArtwork
                      theme={item}
                      className="h-full w-full"
                      showClock={true}
                      compact={true}
                      portrait={isPortrait}
                    />
                    <div className="absolute inset-0 flex flex-col justify-between bg-gradient-to-t from-[#12092E]/85 via-[#12092E]/35 to-[#12092E]/65 p-2.5">
                      <div className="flex items-start justify-between gap-2">
                        <span className="truncate text-[12.5px] font-bold text-white drop-shadow">
                          {item.displayName}
                        </span>
                        {alreadyIn ? (
                          <span className="shrink-0 rounded-full bg-white/25 px-2 py-0.5 text-[10.5px] font-semibold text-white backdrop-blur-md">
                            已在列表中
                          </span>
                        ) : (
                          <span
                            className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border ${
                              checked
                                ? 'border-[#5E45F8] bg-[#5E45F8] text-white'
                                : 'border-white/70 bg-black/30 text-transparent'
                            }`}
                          >
                            <Check className="h-3 w-3" />
                          </span>
                        )}
                      </div>

                      <div className="text-[11px] font-medium text-white/80">
                        {item.category} · <span className="font-mono-tabular">{activeDevice.resolution}</span>
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>

            <div className="flex items-center justify-between border-t border-[#ECEAF6] bg-white px-6 py-3.5">
              <span className="text-[12.5px] font-medium text-[#6B6880]">
                已选择 <strong className="text-[#5E45F8]">{selectedToAdd.length}</strong> 项新内容（加入后默认停留 30 秒）
              </span>
              <div className="flex items-center gap-2.5">
                <button
                  type="button"
                  onClick={() => setAddModalOpen(false)}
                  className="rounded-full bg-[#F4F3FA] px-4 py-2 text-[12.5px] font-semibold text-[#56526B]"
                >
                  取消
                </button>
                <button
                  type="button"
                  disabled={selectedToAdd.length === 0}
                  onClick={() => {
                    onAddItemsToPlaylist(activePlaylist.id, selectedToAdd);
                    setAddModalOpen(false);
                  }}
                  className="rounded-full bg-[#5E45F8] px-5 py-2 text-[12.5px] font-bold text-white shadow-[0_6px_16px_rgba(94,69,248,0.28)] hover:bg-[#4D34E6] disabled:opacity-40"
                >
                  加入列表 ({selectedToAdd.length})
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Item Temporary Preview Modal */}
      {previewTheme && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#140B36]/60 p-4 backdrop-blur-md"
          role="dialog"
          aria-modal="true"
        >
          <div className="aurora-dark-card w-full max-w-3xl rounded-[26px] border border-white/20 p-6 text-white shadow-[0_30px_80px_rgba(6,2,20,0.6)]">
            <div className="mb-4 flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="text-[12px] text-white/70">
                  条目临时预览 · {activeDevice.name} · {activeDevice.resolution}
                </div>
                <h3 className="truncate text-[18px] font-bold">{previewTheme.displayName}</h3>
              </div>
              <button
                type="button"
                onClick={() => setPreviewTheme(null)}
                className="flex h-8 w-8 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="flex justify-center">
              <div
                style={{ aspectRatio: previewAspectRatio }}
                className={`overflow-hidden rounded-[20px] border border-white/20 shadow-2xl ${
                  isPortrait ? 'w-[216px] sm:w-[240px]' : 'w-full'
                }`}
              >
                <ThemeArtwork
                  theme={previewTheme}
                  className="h-full w-full"
                  showClock={true}
                  liveTick={true}
                  compact={isPortrait}
                  portrait={isPortrait}
                />
              </div>
            </div>

            <div className="mt-5 flex justify-end gap-2.5">
              <button
                type="button"
                onClick={() => setPreviewTheme(null)}
                className="rounded-full bg-white/15 px-4 py-2 text-[12.5px] font-semibold text-white hover:bg-white/25"
              >
                关闭预览
              </button>
              <button
                type="button"
                onClick={() => {
                  onApplyTheme(previewTheme);
                  setPreviewTheme(null);
                }}
                className="rounded-full bg-white px-5 py-2 text-[12.5px] font-bold text-[#1A172B] hover:bg-[#F4F2FF]"
              >
                立即输出到 {deviceShortName}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {deleteConfirmOpen && activePlaylist && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[#140B36]/45 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
        >
          <div className="w-full max-w-md rounded-[24px] border border-white/70 bg-white p-6 shadow-[0_30px_80px_rgba(20,10,60,0.3)]">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-red-50 text-red-500">
                <AlertTriangle className="h-5 w-5" />
              </div>
              <div>
                <h3 className="text-[17px] font-bold text-[#1A172B]">
                  确认删除轮播列表「{activePlaylist.name}」？
                </h3>
                <p className="mt-1.5 text-[13px] leading-relaxed text-[#6B6880]">
                  删除后列表中的 {activePlaylist.items.length} 项素材仍会保留在资源库中；引用该列表的定时规则也会保留，并标记为“目标列表已删除”。
                </p>
              </div>
            </div>
            <div className="mt-6 flex justify-end gap-2.5">
              <button
                type="button"
                onClick={() => setDeleteConfirmOpen(false)}
                className="rounded-full bg-[#F4F3FA] px-4 py-2 text-[12.5px] font-semibold text-[#56526B]"
              >
                取消
              </button>
              <button
                type="button"
                onClick={() => {
                  onDeletePlaylist(activePlaylist.id);
                  setDeleteConfirmOpen(false);
                }}
                className="rounded-full bg-red-500 px-5 py-2 text-[12.5px] font-bold text-white hover:bg-red-600"
              >
                确认删除
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
