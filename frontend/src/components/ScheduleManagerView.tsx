import React, { useEffect, useMemo, useState } from 'react';
import {
  CalendarClock,
  Clock,
  Edit3,
  ListVideo,
  Moon,
  PauseCircle,
  Play,
  Plus,
  RefreshCw,
  SlidersHorizontal,
  Sun,
  Trash2,
  AlertTriangle,
  X,
  Image as ImageIcon,
  Power,
  Search,
  Check,
  Star,
} from 'lucide-react';
import {
  CarouselPlaylist,
  ContentCategory,
  DeviceInfo,
  ScheduleActionType,
  ScheduleRepeatMode,
  ScheduleRule,
  ScreenOrientation,
  ThemeItem,
} from '../data/themes';
import { ThemeArtwork } from './ThemeArtwork';

interface ScheduleManagerViewProps {
  schedules: ScheduleRule[];
  schedulesEnabled: boolean;
  themes: ThemeItem[];
  devices: DeviceInfo[];
  activeDevice: DeviceInfo;
  playlists: CarouselPlaylist[];
  onOrientationChange: (orientation: ScreenOrientation) => void;
  onToggleGlobalSchedules: () => void;
  onToggleSchedule: (id: string) => void;
  onAddSchedule: (rule: ScheduleRule) => void;
  onUpdateSchedule: (id: string, patch: Partial<ScheduleRule>, notifyMsg?: string) => void;
  onDeleteSchedule: (id: string) => void;
  onRunScheduleNow: (rule: ScheduleRule) => void;
}

const WEEKDAY_LABELS = [
  { day: 1, short: '一' },
  { day: 2, short: '二' },
  { day: 3, short: '三' },
  { day: 4, short: '四' },
  { day: 5, short: '五' },
  { day: 6, short: '六' },
  { day: 0, short: '日' },
];

function getDaysSet(rule: Pick<ScheduleRule, 'repeatMode' | 'customDays'>): Set<number> {
  if (rule.repeatMode === 'weekdays') return new Set([1, 2, 3, 4, 5]);
  if (rule.repeatMode === 'weekends') return new Set([0, 6]);
  if (rule.repeatMode === 'custom') return new Set(rule.customDays?.length ? rule.customDays : [1]);
  return new Set([0, 1, 2, 3, 4, 5, 6]);
}

function formatRepeatLabel(mode: ScheduleRepeatMode, customDays?: number[]): string {
  if (mode === 'daily') return '每天';
  if (mode === 'weekdays') return '周一至周五';
  if (mode === 'weekends') return '周末';
  if (mode === 'once') return '仅一次';
  const sorted = WEEKDAY_LABELS.filter((w) => (customDays ?? []).includes(w.day)).map(
    (w) => `周${w.short}`
  );
  return sorted.length > 0 ? sorted.join('、') : '自定义';
}

function doesScheduleMatchDay(rule: ScheduleRule, date: Date): boolean {
  const day = date.getDay();
  if (rule.repeatMode === 'daily' || rule.repeatMode === 'once') return true;
  if (rule.repeatMode === 'weekdays') return day >= 1 && day <= 5;
  if (rule.repeatMode === 'weekends') return day === 0 || day === 6;
  return (rule.customDays ?? []).includes(day);
}

function getNextTriggerInfo(
  rules: ScheduleRule[],
  now: Date
): { rule: ScheduleRule; diffMinutes: number; countdownText: string } | null {
  const enabledRules = rules.filter((r) => r.enabled);
  if (enabledRules.length === 0) return null;

  let best: { rule: ScheduleRule; diffMinutes: number; targetDate: Date } | null = null;

  for (const rule of enabledRules) {
    const [hhStr, mmStr] = rule.time.split(':');
    const hh = Number(hhStr);
    const mm = Number(mmStr);
    if (!Number.isFinite(hh) || !Number.isFinite(mm)) continue;

    for (let offset = 0; offset <= 7; offset += 1) {
      const candidate = new Date(now);
      candidate.setDate(now.getDate() + offset);
      candidate.setHours(hh, mm, 0, 0);
      if (candidate.getTime() <= now.getTime()) continue;
      if (!doesScheduleMatchDay(rule, candidate)) continue;

      const diffMinutes = Math.max(1, Math.round((candidate.getTime() - now.getTime()) / 60000));
      if (!best || diffMinutes < best.diffMinutes) {
        best = { rule, diffMinutes, targetDate: candidate };
      }
      break;
    }
  }

  if (!best) return null;

  const hours = Math.floor(best.diffMinutes / 60);
  const mins = best.diffMinutes % 60;
  let countdownText = '';
  if (hours >= 24) {
    const days = Math.floor(hours / 24);
    const remHours = hours % 24;
    countdownText = remHours > 0 ? `${days} 天 ${remHours} 小时后` : `${days} 天后`;
  } else if (hours > 0) {
    countdownText = mins > 0 ? `${hours} 小时 ${mins} 分钟后` : `${hours} 小时后`;
  } else {
    countdownText = `${mins} 分钟后`;
  }

  return { rule: best.rule, diffMinutes: best.diffMinutes, countdownText };
}

const QUICK_PRESETS: {
  label: string;
  title: string;
  time: string;
  repeatMode: ScheduleRepeatMode;
  actionType: ScheduleActionType;
  targetBrightness?: number;
}[] = [
  {
    label: '08:30 早安唤醒',
    title: '工作日清晨唤醒',
    time: '08:30',
    repeatMode: 'weekdays',
    actionType: 'playlist',
    targetBrightness: 85,
  },
  {
    label: '13:30 午后专注',
    title: '午后极光专注',
    time: '13:30',
    repeatMode: 'daily',
    actionType: 'theme',
  },
  {
    label: '20:00 夜间护眼',
    title: '夜间低亮护眼',
    time: '20:00',
    repeatMode: 'daily',
    actionType: 'playlist',
    targetBrightness: 40,
  },
  {
    label: '23:30 定时息屏',
    title: '深夜自动息屏',
    time: '23:30',
    repeatMode: 'daily',
    actionType: 'sleep',
  },
];

export const ScheduleManagerView: React.FC<ScheduleManagerViewProps> = ({
  schedules,
  schedulesEnabled,
  themes,
  devices,
  activeDevice,
  playlists,
  onOrientationChange,
  onToggleGlobalSchedules,
  onToggleSchedule,
  onAddSchedule,
  onUpdateSchedule,
  onDeleteSchedule,
  onRunScheduleNow,
}) => {
  const [scopeFilter, setScopeFilter] = useState<'current' | 'all'>('current');
  const [now, setNow] = useState<Date>(() => new Date());

  // Modal state for creating / editing a rule
  const [modalOpen, setModalOpen] = useState(false);
  const [editingRuleId, setEditingRuleId] = useState<string | null>(null);
  const [formTitle, setFormTitle] = useState('');
  const [formTime, setFormTime] = useState('18:00');
  const [formDeviceId, setFormDeviceId] = useState(activeDevice.id);
  const [formOrientation, setFormOrientation] = useState<ScreenOrientation>(
    activeDevice.orientation
  );
  const [formRepeatMode, setFormRepeatMode] = useState<ScheduleRepeatMode>('daily');
  const [formCustomDays, setFormCustomDays] = useState<number[]>([1, 2, 3, 4, 5]);
  const [formActionType, setFormActionType] = useState<ScheduleActionType>('playlist');
  const [formPlaylistId, setFormPlaylistId] = useState('');
  const [formThemeId, setFormThemeId] = useState(themes[0]?.id ?? 'crystal-ice-palace');
  const [formAdjustBrightness, setFormAdjustBrightness] = useState(false);
  const [formBrightness, setFormBrightness] = useState(75);
  const [formEnabled, setFormEnabled] = useState(true);

  // Visual theme picker modal state (Option A)
  const [themePickerOpen, setThemePickerOpen] = useState(false);
  const [themePickerQuery, setThemePickerQuery] = useState('');
  const [themePickerFilter, setThemePickerFilter] = useState<ContentCategory | '全部' | '收藏'>('全部');

  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 15000);
    return () => window.clearInterval(id);
  }, []);

  const deviceShortName = activeDevice.name.split('·')[0].trim();
  const oriLabel = activeDevice.orientation === 'portrait' ? '竖屏' : '横屏';

  const scopedRules = useMemo(() => {
    const list =
      scopeFilter === 'current'
        ? schedules.filter(
            (r) => r.deviceId === activeDevice.id && r.orientation === activeDevice.orientation
          )
        : schedules;
    return [...list].sort((a, b) => a.time.localeCompare(b.time));
  }, [schedules, scopeFilter, activeDevice.id, activeDevice.orientation]);

  const enabledCount = useMemo(
    () => scopedRules.filter((r) => r.enabled).length,
    [scopedRules]
  );

  const nextTrigger = useMemo(
    () => (schedulesEnabled ? getNextTriggerInfo(scopedRules, now) : null),
    [scopedRules, schedulesEnabled, now]
  );

  // Playlists strictly filtered by the form's chosen device + orientation
  const formScopedPlaylists = useMemo(
    () =>
      playlists.filter(
        (p) => p.deviceId === formDeviceId && p.orientation === formOrientation
      ),
    [playlists, formDeviceId, formOrientation]
  );

  useEffect(() => {
    if (formActionType === 'playlist' && formScopedPlaylists.length > 0) {
      if (!formScopedPlaylists.some((p) => p.id === formPlaylistId)) {
        setFormPlaylistId(formScopedPlaylists[0].id);
      }
    }
  }, [formActionType, formScopedPlaylists, formPlaylistId]);

  // Target device & aspect info for form preview & picker
  const formTargetDevice = useMemo(
    () => devices.find((d) => d.id === formDeviceId) ?? activeDevice,
    [devices, formDeviceId, activeDevice]
  );

  const formIsPortrait = formOrientation === 'portrait';
  const formAspectRatio = formIsPortrait
    ? `${formTargetDevice.nativeHeight} / ${formTargetDevice.nativeWidth}`
    : `${formTargetDevice.nativeWidth} / ${formTargetDevice.nativeHeight}`;

  const selectedFormTheme = useMemo(
    () => themes.find((t) => t.id === formThemeId) ?? themes[0],
    [themes, formThemeId]
  );

  const pickerFilteredThemes = useMemo(() => {
    const q = themePickerQuery.trim().toLowerCase();
    return [...themes]
      .filter((t) => {
        if (themePickerFilter === '收藏') {
          if (!t.isFavorite) return false;
        } else if (themePickerFilter !== '全部' && t.category !== themePickerFilter) {
          return false;
        }
        if (
          q !== '' &&
          !t.displayName.toLowerCase().includes(q) &&
          !t.name.toLowerCase().includes(q) &&
          !t.category.toLowerCase().includes(q)
        ) {
          return false;
        }
        return true;
      })
      .sort((a, b) => {
        const favDiff = Number(Boolean(b.isFavorite)) - Number(Boolean(a.isFavorite));
        if (favDiff !== 0) return favDiff;
        return (b.useCount ?? 0) - (a.useCount ?? 0);
      });
  }, [themes, themePickerFilter, themePickerQuery]);

  // Conflict detection in modal
  const conflictingRule = useMemo(() => {
    if (!modalOpen || !formEnabled) return null;
    const draftDays = getDaysSet({ repeatMode: formRepeatMode, customDays: formCustomDays });
    return (
      schedules.find((r) => {
        if (r.id === editingRuleId) return false;
        if (!r.enabled) return false;
        if (r.deviceId !== formDeviceId || r.orientation !== formOrientation) return false;
        if (r.time !== formTime) return false;
        const existingDays = getDaysSet(r);
        for (const d of draftDays) {
          if (existingDays.has(d)) return true;
        }
        return false;
      }) ?? null
    );
  }, [
    modalOpen,
    formEnabled,
    formRepeatMode,
    formCustomDays,
    schedules,
    editingRuleId,
    formDeviceId,
    formOrientation,
    formTime,
  ]);

  const openCreateModal = (preset?: (typeof QUICK_PRESETS)[number]) => {
    setEditingRuleId(null);
    setFormDeviceId(activeDevice.id);
    setFormOrientation(activeDevice.orientation);

    const matchingPls = playlists.filter(
      (p) => p.deviceId === activeDevice.id && p.orientation === activeDevice.orientation
    );

    if (preset) {
      setFormTitle(preset.title);
      setFormTime(preset.time);
      setFormRepeatMode(preset.repeatMode);
      const nextAction =
        preset.actionType === 'playlist' && matchingPls.length === 0 ? 'theme' : preset.actionType;
      setFormActionType(nextAction);
      setFormAdjustBrightness(typeof preset.targetBrightness === 'number');
      setFormBrightness(preset.targetBrightness ?? 75);
    } else {
      setFormTitle('');
      setFormTime('18:00');
      setFormRepeatMode('daily');
      setFormActionType(matchingPls.length > 0 ? 'playlist' : 'theme');
      setFormAdjustBrightness(false);
      setFormBrightness(75);
    }

    setFormCustomDays([1, 2, 3, 4, 5]);
    setFormPlaylistId(matchingPls[0]?.id ?? '');
    setFormThemeId(themes[0]?.id ?? 'crystal-ice-palace');
    setFormEnabled(true);
    setModalOpen(true);
  };

  const openEditModal = (rule: ScheduleRule, forcePlaylistRepair?: boolean) => {
    setEditingRuleId(rule.id);
    setFormTitle(rule.title);
    setFormTime(rule.time);
    setFormDeviceId(rule.deviceId);
    setFormOrientation(rule.orientation);
    setFormRepeatMode(rule.repeatMode);
    setFormCustomDays(rule.customDays?.length ? rule.customDays : [1, 2, 3, 4, 5]);
    setFormActionType(forcePlaylistRepair ? 'playlist' : rule.actionType);
    const matchingPls = playlists.filter(
      (p) => p.deviceId === rule.deviceId && p.orientation === rule.orientation
    );
    setFormPlaylistId(
      matchingPls.some((p) => p.id === rule.playlistId)
        ? (rule.playlistId as string)
        : (matchingPls[0]?.id ?? '')
    );
    setFormThemeId(rule.themeId || themes[0]?.id || 'crystal-ice-palace');
    setFormAdjustBrightness(typeof rule.targetBrightness === 'number');
    setFormBrightness(rule.targetBrightness ?? 75);
    setFormEnabled(rule.enabled);
    setModalOpen(true);
  };

  const handleSaveRule = (e: React.FormEvent) => {
    e.preventDefault();
    const selectedPl =
      formActionType === 'playlist'
        ? formScopedPlaylists.find((p) => p.id === formPlaylistId) ?? formScopedPlaylists[0]
        : undefined;

    if (formActionType === 'playlist' && !selectedPl) {
      return;
    }

    const daysSummary = formatRepeatLabel(formRepeatMode, formCustomDays);
    const fallbackThemeId =
      selectedPl?.items[0]?.themeId ?? formThemeId ?? themes[0]?.id ?? 'crystal-ice-palace';

    const payload: Omit<ScheduleRule, 'id'> = {
      title:
        formTitle.trim() ||
        (formActionType === 'sleep'
          ? '定时息屏'
          : formActionType === 'wake'
            ? '定时唤醒'
            : selectedPl
              ? `定时轮播「${selectedPl.name}」`
              : '自定义定时切换'),
      time: formTime,
      deviceId: formDeviceId,
      orientation: formOrientation,
      repeatMode: formRepeatMode,
      customDays: formRepeatMode === 'custom' ? [...formCustomDays].sort((a, b) => a - b) : undefined,
      days: daysSummary,
      actionType: formActionType,
      themeId: fallbackThemeId,
      playlistId: selectedPl?.id,
      playlistNameSnapshot: selectedPl?.name,
      playlistDeleted: false,
      targetBrightness:
        formActionType !== 'sleep' && formAdjustBrightness ? formBrightness : undefined,
      enabled: formEnabled,
    };

    if (editingRuleId) {
      onUpdateSchedule(editingRuleId, payload, `已更新定时规则「${payload.title}」`);
    } else {
      onAddSchedule({
        id: `sch-${Date.now()}`,
        ...payload,
      });
    }
    setModalOpen(false);
  };

  const currentClockStr = `${String(now.getHours()).padStart(2, '0')}:${String(
    now.getMinutes()
  ).padStart(2, '0')}`;

  return (
    <div className="space-y-5">
      {/* Studio Header Toolbar */}
      <div className="flex flex-wrap items-center justify-between gap-4 rounded-[24px] border border-white/80 bg-white/88 px-5 py-4 shadow-[0_12px_32px_rgba(30,20,80,0.05)] backdrop-blur-xl">
        <div className="min-w-0">
          <h2 className="text-[20px] font-bold tracking-tight text-[#1A172B]">定时切换</h2>
          <div className="mt-0.5 flex flex-wrap items-center gap-2 text-[12px] font-medium text-[#7A7690]">
            <span>{deviceShortName}</span>
            <span aria-hidden="true">·</span>
            <span>{oriLabel} ({activeDevice.resolution})</span>
            <span aria-hidden="true">·</span>
            <span className="font-mono-tabular">系统时间 {currentClockStr}</span>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          {/* Scope view toggle: Current device+orientation vs All */}
          <div
            className="flex items-center rounded-xl bg-[#F4F3FA] p-1"
            role="group"
            aria-label="规则显示范围"
          >
            <button
              type="button"
              onClick={() => setScopeFilter('current')}
              className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                scopeFilter === 'current'
                  ? 'bg-white text-[#5E45F8] shadow-xs'
                  : 'text-[#6B6880] hover:text-[#1A172B]'
              }`}
            >
              当前画面 ({deviceShortName}·{oriLabel})
            </button>
            <button
              type="button"
              onClick={() => setScopeFilter('all')}
              className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                scopeFilter === 'all'
                  ? 'bg-white text-[#5E45F8] shadow-xs'
                  : 'text-[#6B6880] hover:text-[#1A172B]'
              }`}
            >
              全部规则 ({schedules.length})
            </button>
          </div>

          {scopeFilter === 'current' && (
            <div
              className="flex items-center rounded-xl bg-[#F4F3FA] p-1"
              role="group"
              aria-label="切换横竖屏规则"
            >
              <button
                type="button"
                onClick={() => onOrientationChange('landscape')}
                className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  activeDevice.orientation === 'landscape'
                    ? 'bg-white text-[#5E45F8] shadow-xs'
                    : 'text-[#6B6880] hover:text-[#1A172B]'
                }`}
              >
                横屏
              </button>
              <button
                type="button"
                onClick={() => onOrientationChange('portrait')}
                className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                  activeDevice.orientation === 'portrait'
                    ? 'bg-white text-[#5E45F8] shadow-xs'
                    : 'text-[#6B6880] hover:text-[#1A172B]'
                }`}
              >
                竖屏
              </button>
            </div>
          )}

          {/* Global Master Pause Toggle */}
          <button
            type="button"
            onClick={onToggleGlobalSchedules}
            aria-pressed={schedulesEnabled}
            className={`flex items-center gap-1.5 rounded-xl border px-3 py-2 text-[12px] font-semibold transition-colors whitespace-nowrap ${
              schedulesEnabled
                ? 'border-[#E6E4F2] bg-[#FAF9FE] text-[#4C4763] hover:border-[#C8BDFC] hover:text-[#5E45F8]'
                : 'border-amber-200 bg-amber-50 text-amber-700'
            }`}
          >
            <PauseCircle className="h-3.5 w-3.5" />
            <span>{schedulesEnabled ? '定时调度运行中' : '已全局暂停'}</span>
          </button>

          <button
            type="button"
            onClick={() => openCreateModal()}
            className="flex items-center gap-1.5 rounded-xl bg-[#5E45F8] px-4 py-2 text-[12.5px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] transition-colors hover:bg-[#4D34E6] whitespace-nowrap shrink-0"
          >
            <Plus className="h-4 w-4" />
            <span>新建定时规则</span>
          </button>
        </div>
      </div>

      {/* Aurora Hero Stage: Next Trigger Countdown & Quick Presets */}
      <section className="aurora-dark-card relative overflow-hidden rounded-[26px] p-5 sm:p-6 text-white shadow-[0_22px_50px_rgba(24,10,68,0.24)]">
        <div className="relative z-10 flex flex-wrap items-center justify-between gap-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2 text-[12px] font-medium text-white/75">
              <span>{schedulesEnabled ? '自动定时调度已开启' : '全部定时任务已全局暂停'}</span>
              <span aria-hidden="true">·</span>
              <span className="font-mono-tabular">已启用 {enabledCount} / {scopedRules.length} 条</span>
            </div>

            {nextTrigger ? (
              <div className="mt-1.5 flex flex-wrap items-baseline gap-2.5">
                <span className="font-mono-tabular text-[26px] font-bold tracking-tight text-white">
                  {nextTrigger.rule.time}
                </span>
                <span className="text-[17px] font-bold text-white/95">
                  {nextTrigger.rule.title}
                </span>
                <span className="text-[13px] font-medium text-[#C4B8FF]">
                  · {nextTrigger.countdownText}触发
                </span>
              </div>
            ) : (
              <h3 className="mt-1.5 text-[18px] font-bold text-white/90">
                {schedulesEnabled ? '当前筛选范围内暂无启用的定时任务' : '已进入休假/暂停模式，所有定时任务暂不触发'}
              </h3>
            )}
          </div>

          {!schedulesEnabled ? (
            <button
              type="button"
              onClick={onToggleGlobalSchedules}
              className="flex items-center gap-1.5 rounded-full bg-white px-4 py-2 text-[12.5px] font-bold text-[#1A172B] shadow-md transition-colors hover:bg-[#F3F1FF] whitespace-nowrap"
            >
              <Play className="h-3.5 w-3.5 fill-current text-[#5E45F8]" />
              <span>恢复定时调度</span>
            </button>
          ) : (
            <div className="flex flex-wrap items-center gap-2">
              {QUICK_PRESETS.map((preset) => (
                <button
                  key={preset.label}
                  type="button"
                  onClick={() => openCreateModal(preset)}
                  className="rounded-full border border-white/15 bg-white/10 px-3 py-1.5 text-[12px] font-medium text-white/90 transition-colors hover:bg-white/20 whitespace-nowrap"
                >
                  + {preset.label}
                </button>
              ))}
            </div>
          )}
        </div>
      </section>

      {/* Chronological Rules List */}
      {scopedRules.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-[24px] border border-dashed border-[#DAD6F0] bg-white/85 py-14 text-center">
          <CalendarClock className="h-9 w-9 text-[#9C94C9]" />
          <p className="mt-3 text-[15px] font-bold text-[#1A172B]">
            当前画面暂无定时切换规则
          </p>
          <p className="mt-1 text-[13px] text-[#7A7690]">
            为 {deviceShortName}（{oriLabel}）添加作息时段规则，到点自动切换壁纸、启动轮播或调节亮度。
          </p>
          <button
            type="button"
            onClick={() => openCreateModal()}
            className="mt-4 flex items-center gap-1.5 rounded-xl bg-[#5E45F8] px-4 py-2 text-[13px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] hover:bg-[#4D34E6]"
          >
            <Plus className="h-4 w-4" />
            <span>新建定时规则</span>
          </button>
        </div>
      ) : (
        <div className="space-y-3">
          {scopedRules.map((rule) => {
            const ruleDevice = devices.find((d) => d.id === rule.deviceId);
            const ruleDevShort = ruleDevice?.name.split('·')[0].trim() ?? '设备';
            const ruleOriLabel = rule.orientation === 'portrait' ? '竖屏' : '横屏';
            const targetPlaylist =
              rule.actionType === 'playlist' && rule.playlistId
                ? playlists.find((p) => p.id === rule.playlistId)
                : undefined;
            const previewTheme =
              (targetPlaylist?.items[0]
                ? themes.find((t) => t.id === targetPlaylist.items[0].themeId)
                : undefined) ?? themes.find((t) => t.id === rule.themeId);

            const isNext = nextTrigger?.rule.id === rule.id;

            return (
              <div
                key={rule.id}
                className={`flex flex-wrap items-center justify-between gap-4 rounded-[22px] border bg-white p-4 transition-all sm:flex-nowrap ${
                  isNext && rule.enabled && schedulesEnabled
                    ? 'border-[#C5B9FF] shadow-[0_12px_30px_rgba(94,69,248,0.08)]'
                    : 'border-[#ECEAF6] shadow-[0_8px_24px_rgba(30,20,80,0.03)]'
                } ${!rule.enabled || !schedulesEnabled ? 'opacity-75' : ''}`}
              >
                {/* Time + Preview + Metadata */}
                <div className="flex min-w-0 flex-1 items-center gap-4">
                  <div
                    className={`flex h-13 w-20 shrink-0 flex-col items-center justify-center rounded-2xl font-mono-tabular ${
                      rule.enabled && schedulesEnabled
                        ? 'bg-[#F3F0FF] text-[#5E45F8]'
                        : 'bg-[#F4F3F8] text-[#7A7690]'
                    }`}
                  >
                    <span className="text-[17px] font-bold leading-none">{rule.time}</span>
                    {isNext && rule.enabled && schedulesEnabled && (
                      <span className="mt-1 text-[10px] font-semibold text-[#5E45F8]">
                        即将触发
                      </span>
                    )}
                  </div>

                  {/* Artwork or System Action Thumbnail */}
                  <div className="hidden h-12 w-20 shrink-0 overflow-hidden rounded-xl border border-[#EAE8F5] bg-[#160F30] sm:flex items-center justify-center">
                    {rule.actionType === 'sleep' ? (
                      <div className="flex h-full w-full items-center justify-center bg-[#181234] text-[#C4B8FF]">
                        <Moon className="h-5 w-5" />
                      </div>
                    ) : rule.actionType === 'wake' ? (
                      <div className="flex h-full w-full items-center justify-center bg-[#2E2268] text-amber-300">
                        <Power className="h-5 w-5" />
                      </div>
                    ) : previewTheme ? (
                      <ThemeArtwork
                        theme={previewTheme}
                        className="h-full w-full"
                        showClock={false}
                        portrait={rule.orientation === 'portrait'}
                      />
                    ) : (
                      <ImageIcon className="h-4 w-4 text-white/50" />
                    )}
                  </div>

                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="truncate text-[14.5px] font-bold text-[#1A172B]">
                        {rule.title}
                      </span>
                      {rule.playlistDeleted && (
                        <span className="inline-flex items-center gap-1 text-[11.5px] font-semibold text-amber-600">
                          <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
                          <span>目标列表「{rule.playlistNameSnapshot ?? '未知'}」已删除</span>
                        </span>
                      )}
                    </div>

                    <div className="mt-1 flex flex-wrap items-center gap-1.5 text-[12px] text-[#7A7690]">
                      {scopeFilter === 'all' && (
                        <>
                          <span className="font-semibold text-[#4C4763]">
                            {ruleDevShort} · {ruleOriLabel}
                          </span>
                          <span aria-hidden="true">·</span>
                        </>
                      )}
                      <span>{rule.days}</span>
                      <span aria-hidden="true">·</span>
                      {rule.actionType === 'sleep' ? (
                        <span>副屏休眠息屏</span>
                      ) : rule.actionType === 'wake' ? (
                        <span>唤醒并恢复输出</span>
                      ) : rule.playlistDeleted ? (
                        <span>回退主题：{previewTheme?.displayName ?? '默认主题'}</span>
                      ) : targetPlaylist ? (
                        <span className="text-[#5E45F8] font-medium">
                          轮播「{targetPlaylist.name}」（{targetPlaylist.items.length} 项）
                        </span>
                      ) : (
                        <span>主题：{previewTheme?.displayName ?? '默认主题'}</span>
                      )}

                      {typeof rule.targetBrightness === 'number' && (
                        <>
                          <span aria-hidden="true">·</span>
                          <span className="inline-flex items-center gap-1 font-mono-tabular">
                            <Sun className="h-3 w-3 text-amber-500" />
                            亮度 {rule.targetBrightness}%
                          </span>
                        </>
                      )}
                    </div>

                    {rule.playlistDeleted && (
                      <div className="mt-2 flex flex-wrap items-center gap-2">
                        <button
                          type="button"
                          onClick={() => openEditModal(rule, true)}
                          className="inline-flex items-center gap-1 rounded-lg bg-[#F3F0FF] px-2.5 py-1 text-[11.5px] font-semibold text-[#5E45F8] hover:bg-[#E7E1FF] whitespace-nowrap"
                        >
                          <RefreshCw className="h-3 w-3" />
                          <span>重新绑定列表</span>
                        </button>
                        <button
                          type="button"
                          onClick={() =>
                            onUpdateSchedule(
                              rule.id,
                              {
                                actionType: 'theme',
                                playlistId: undefined,
                                playlistNameSnapshot: undefined,
                                playlistDeleted: false,
                              },
                              `已将「${rule.title}」转为使用回退主题`
                            )
                          }
                          className="inline-flex items-center gap-1 rounded-lg bg-[#F5F4FA] px-2.5 py-1 text-[11.5px] font-semibold text-[#56526B] hover:bg-[#EBE9F5] whitespace-nowrap"
                        >
                          <span>改用回退主题</span>
                        </button>
                      </div>
                    )}
                  </div>
                </div>

                {/* Actions */}
                <div className="flex shrink-0 items-center gap-2">
                  <button
                    type="button"
                    onClick={() => onRunScheduleNow(rule)}
                    className="flex items-center gap-1 rounded-xl border border-[#ECEAF6] bg-[#FAF9FE] px-2.5 py-1.5 text-[12px] font-semibold text-[#4C4763] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8] whitespace-nowrap"
                    title="立即试运行此规则"
                  >
                    <Play className="h-3.5 w-3.5" />
                    <span>试运行</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => openEditModal(rule)}
                    aria-label={`编辑 ${rule.title}`}
                    className="flex h-8 w-8 items-center justify-center rounded-xl border border-[#ECEAF6] bg-white text-[#6B6880] transition-colors hover:border-[#C8BDFC] hover:text-[#5E45F8]"
                    title="编辑规则"
                  >
                    <Edit3 className="h-3.5 w-3.5" />
                  </button>

                  <button
                    type="button"
                    onClick={() => onDeleteSchedule(rule.id)}
                    aria-label={`删除 ${rule.title}`}
                    className="flex h-8 w-8 items-center justify-center rounded-xl border border-[#ECEAF6] bg-white text-[#6B6880] transition-colors hover:border-red-200 hover:bg-red-50 hover:text-red-500"
                    title="删除规则"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>

                  <button
                    type="button"
                    onClick={() => onToggleSchedule(rule.id)}
                    className={`relative ml-1 h-7 w-12 shrink-0 rounded-full transition-colors ${
                      rule.enabled ? 'bg-[#5E45F8]' : 'bg-[#D6D3E6]'
                    }`}
                    aria-label={`切换 ${rule.title}`}
                    aria-pressed={rule.enabled}
                  >
                    <span
                      className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-transform ${
                        rule.enabled ? 'left-6' : 'left-1'
                      }`}
                    />
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Create / Edit Modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div
            className="fixed inset-0 bg-[#140B36]/45 backdrop-blur-xs"
            onClick={() => setModalOpen(false)}
          />
          <form
            onSubmit={handleSaveRule}
            className="relative z-10 max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-[26px] border border-white/80 bg-white p-6 shadow-[0_28px_70px_rgba(20,10,60,0.28)] studio-scroll"
          >
            <div className="flex items-center justify-between border-b border-[#F1EFF8] pb-3.5">
              <div className="flex items-center gap-2.5">
                <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-[#F3F0FF] text-[#5E45F8]">
                  <Clock className="h-4.5 w-4.5" />
                </div>
                <div>
                  <h3 className="text-[16px] font-bold text-[#1A172B]">
                    {editingRuleId ? '编辑定时规则' : '新建定时切换规则'}
                  </h3>
                  <p className="text-[11.5px] text-[#7A7690]">
                    到达设定时间时自动应用对应内容或执行硬件控制
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setModalOpen(false)}
                className="rounded-lg p-1.5 text-[#7A7690] hover:bg-[#F5F4FA]"
                aria-label="关闭弹窗"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-4 space-y-4">
              {/* Name & Time */}
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-12">
                <div className="sm:col-span-7">
                  <label className="mb-1 block text-[12px] font-semibold text-[#6B6880]">
                    任务名称
                  </label>
                  <input
                    type="text"
                    value={formTitle}
                    onChange={(e) => setFormTitle(e.target.value)}
                    placeholder="例如：深夜护眼模式"
                    className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 text-[13px] text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                  />
                </div>
                <div className="sm:col-span-5">
                  <label className="mb-1 block text-[12px] font-semibold text-[#6B6880]">
                    触发时间
                  </label>
                  <input
                    type="time"
                    value={formTime}
                    onChange={(e) => setFormTime(e.target.value)}
                    required
                    className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 font-mono-tabular text-[13px] font-bold text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                  />
                </div>
              </div>

              {/* Device & Orientation Scope */}
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <label className="mb-1 block text-[12px] font-semibold text-[#6B6880]">
                    目标设备
                  </label>
                  <select
                    value={formDeviceId}
                    onChange={(e) => setFormDeviceId(e.target.value)}
                    className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 text-[13px] text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                  >
                    {devices.map((dev) => (
                      <option key={dev.id} value={dev.id}>
                        {dev.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-[12px] font-semibold text-[#6B6880]">
                    显示方向
                  </label>
                  <div className="grid grid-cols-2 gap-1.5 rounded-xl bg-[#F4F3FA] p-1">
                    {(
                      [
                        { id: 'landscape', label: '横屏' },
                        { id: 'portrait', label: '竖屏' },
                      ] as const
                    ).map((ori) => (
                      <button
                        key={ori.id}
                        type="button"
                        onClick={() => setFormOrientation(ori.id)}
                        className={`rounded-lg py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                          formOrientation === ori.id
                            ? 'bg-white text-[#5E45F8] shadow-xs'
                            : 'text-[#6B6880] hover:text-[#1A172B]'
                        }`}
                      >
                        {ori.label}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Repeat Mode */}
              <div>
                <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                  重复周期
                </label>
                <div className="flex flex-wrap gap-1.5">
                  {(
                    [
                      { id: 'daily', label: '每天' },
                      { id: 'weekdays', label: '工作日' },
                      { id: 'weekends', label: '周末' },
                      { id: 'once', label: '仅一次' },
                      { id: 'custom', label: '自定义星期' },
                    ] as const
                  ).map((mode) => (
                    <button
                      key={mode.id}
                      type="button"
                      onClick={() => setFormRepeatMode(mode.id)}
                      className={`rounded-xl px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                        formRepeatMode === mode.id
                          ? 'bg-[#5E45F8] text-white'
                          : 'bg-[#F5F4FA] text-[#56526B] hover:bg-[#EBE8F7]'
                      }`}
                    >
                      {mode.label}
                    </button>
                  ))}
                </div>

                {formRepeatMode === 'custom' && (
                  <div className="mt-2.5 flex items-center gap-1.5">
                    {WEEKDAY_LABELS.map((w) => {
                      const active = formCustomDays.includes(w.day);
                      return (
                        <button
                          key={w.day}
                          type="button"
                          onClick={() =>
                            setFormCustomDays((prev) =>
                              prev.includes(w.day)
                                ? prev.length > 1
                                  ? prev.filter((d) => d !== w.day)
                                  : prev
                                : [...prev, w.day]
                            )
                          }
                          className={`flex h-8 w-8 items-center justify-center rounded-full text-[12px] font-bold transition-colors ${
                            active
                              ? 'bg-[#5E45F8] text-white'
                              : 'bg-[#F4F3FA] text-[#6B6880] hover:text-[#1A172B]'
                          }`}
                        >
                          {w.short}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Action Type */}
              <div>
                <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                  执行动作
                </label>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                  {(
                    [
                      { id: 'playlist', label: '启动轮播', icon: ListVideo },
                      { id: 'theme', label: '单个主题', icon: ImageIcon },
                      { id: 'sleep', label: '定时息屏', icon: Moon },
                      { id: 'wake', label: '唤醒输出', icon: Power },
                    ] as const
                  ).map((act) => {
                    const Icon = act.icon;
                    return (
                      <button
                        key={act.id}
                        type="button"
                        onClick={() => setFormActionType(act.id)}
                        className={`flex items-center justify-center gap-1.5 rounded-xl py-2 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                          formActionType === act.id
                            ? 'bg-[#5E45F8] text-white'
                            : 'bg-[#F5F4FA] text-[#56526B] hover:bg-[#EBE8F7]'
                        }`}
                      >
                        <Icon className="h-3.5 w-3.5" />
                        <span>{act.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Target Selector */}
              {formActionType === 'playlist' && (
                <div>
                  <label className="mb-1 block text-[12px] font-semibold text-[#6B6880]">
                    选择轮播列表（仅显示当前目标设备与方向下的列表）
                  </label>
                  {formScopedPlaylists.length === 0 ? (
                    <div className="flex items-center justify-between rounded-xl border border-amber-200 bg-amber-50 px-3.5 py-2.5 text-[12px] text-amber-700">
                      <span>该设备与方向下暂无轮播列表</span>
                      <button
                        type="button"
                        onClick={() => setFormActionType('theme')}
                        className="font-bold underline"
                      >
                        改用单个主题
                      </button>
                    </div>
                  ) : (
                    <select
                      value={formPlaylistId}
                      onChange={(e) => setFormPlaylistId(e.target.value)}
                      className="w-full rounded-xl border border-[#E6E4F2] bg-[#F8F7FC] px-3 py-2 text-[13px] text-[#1A172B] focus:border-[#5E45F8] focus:bg-white focus:outline-none"
                    >
                      {formScopedPlaylists.map((pl) => (
                        <option key={pl.id} value={pl.id}>
                          {pl.name}（{pl.mood} · {pl.items.length} 项）
                        </option>
                      ))}
                    </select>
                  )}
                </div>
              )}

              {formActionType === 'theme' && selectedFormTheme && (
                <div>
                  <label className="mb-1.5 block text-[12px] font-semibold text-[#6B6880]">
                    切换目标主题
                  </label>
                  <div className="flex items-center justify-between gap-3.5 rounded-2xl border border-[#E6E4F2] bg-[#FAF9FE] p-3">
                    <div className="flex min-w-0 items-center gap-3.5">
                      <div
                        style={{ aspectRatio: formAspectRatio }}
                        className={`shrink-0 overflow-hidden rounded-xl border border-[#DDD8F5] bg-[#160F30] shadow-xs ${
                          formIsPortrait ? 'h-16' : 'h-11 max-w-[140px]'
                        }`}
                      >
                        <ThemeArtwork
                          theme={selectedFormTheme}
                          className="h-full w-full"
                          showClock={true}
                          compact={true}
                          portrait={formIsPortrait}
                        />
                      </div>
                      <div className="min-w-0">
                        <div className="truncate text-[13.5px] font-bold text-[#1A172B]">
                          {selectedFormTheme.displayName}
                        </div>
                        <div className="mt-0.5 flex items-center gap-1.5 text-[11.5px] text-[#7A7690]">
                          <span>{selectedFormTheme.category}</span>
                          {selectedFormTheme.isFavorite && (
                            <>
                              <span aria-hidden="true">·</span>
                              <span className="inline-flex items-center gap-0.5 text-amber-500 font-semibold">
                                <Star className="h-3 w-3 fill-current" />
                                已收藏
                              </span>
                            </>
                          )}
                        </div>
                      </div>
                    </div>

                    <button
                      type="button"
                      onClick={() => {
                        setThemePickerQuery('');
                        setThemePickerFilter('全部');
                        setThemePickerOpen(true);
                      }}
                      className="shrink-0 rounded-xl bg-[#5E45F8] px-3.5 py-2 text-[12px] font-bold text-white shadow-[0_6px_16px_rgba(94,69,248,0.25)] transition-colors hover:bg-[#4D34E6] whitespace-nowrap"
                    >
                      更换主题
                    </button>
                  </div>
                </div>
              )}

              {/* Optional Brightness Linkage */}
              {formActionType !== 'sleep' && (
                <div className="rounded-2xl border border-[#EFEFF6] bg-[#FAF9FE] p-3.5">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2 text-[12.5px] font-semibold text-[#1A172B]">
                      <SlidersHorizontal className="h-3.5 w-3.5 text-[#5E45F8]" />
                      <span>联动调节屏幕亮度</span>
                    </div>
                    <input
                      type="checkbox"
                      checked={formAdjustBrightness}
                      onChange={(e) => setFormAdjustBrightness(e.target.checked)}
                      className="h-4 w-4 accent-[#5E45F8]"
                      aria-label="是否联动调节屏幕亮度"
                    />
                  </div>
                  {formAdjustBrightness && (
                    <div className="mt-3 flex items-center gap-3">
                      <input
                        type="range"
                        min={10}
                        max={100}
                        value={formBrightness}
                        onChange={(e) => setFormBrightness(Number(e.target.value))}
                        className="flex-1 accent-[#5E45F8]"
                        aria-label="目标亮度百分比"
                      />
                      <span className="w-11 text-right font-mono-tabular text-[12.5px] font-bold text-[#5E45F8]">
                        {formBrightness}%
                      </span>
                    </div>
                  )}
                </div>
              )}

              {/* Conflict Warning */}
              {conflictingRule && (
                <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 px-3.5 py-2.5 text-[12px] text-amber-800">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
                  <div className="flex-1">
                    同设备与方向在 <span className="font-mono-tabular font-bold">{formTime}</span>{' '}
                    已有启用规则「{conflictingRule.title}」。建议修改时间或先停用旧规则。
                  </div>
                </div>
              )}
            </div>

            <div className="mt-6 flex items-center justify-end gap-2.5 border-t border-[#F1EFF8] pt-4">
              <button
                type="button"
                onClick={() => setModalOpen(false)}
                className="rounded-xl bg-[#F4F3FA] px-4 py-2 text-[13px] font-semibold text-[#56526B] hover:bg-[#EAE8F5]"
              >
                取消
              </button>
              <button
                type="submit"
                disabled={formActionType === 'playlist' && formScopedPlaylists.length === 0}
                className="rounded-xl bg-[#5E45F8] px-5 py-2 text-[13px] font-bold text-white shadow-[0_8px_20px_rgba(94,69,248,0.28)] hover:bg-[#4D34E6] disabled:cursor-not-allowed disabled:opacity-50"
              >
                {editingRuleId ? '保存修改' : '添加定时规则'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Visual Theme Picker Modal (Option A) */}
      {themePickerOpen && (
        <div
          className="fixed inset-0 z-[60] flex items-center justify-center bg-[#140B36]/50 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-label="可视化选择定时主题"
        >
          <div
            className={`w-full overflow-hidden rounded-[26px] border border-white/60 bg-[#F7F7FC] shadow-[0_32px_88px_rgba(20,10,60,0.34)] ${
              formIsPortrait ? 'max-w-4xl' : 'max-w-3xl'
            }`}
          >
            <div className="flex items-center justify-between border-b border-[#ECEAF6] bg-white px-6 py-4">
              <div>
                <h3 className="text-[17px] font-bold text-[#1A172B]">选择切换主题</h3>
                <p className="text-[12px] text-[#7A7690]">
                  按 {formTargetDevice.name.split('·')[0].trim()} ·{' '}
                  {formIsPortrait
                    ? `竖屏 (${formTargetDevice.nativeHeight} × ${formTargetDevice.nativeWidth})`
                    : `横屏 (${formTargetDevice.nativeWidth} × ${formTargetDevice.nativeHeight})`}{' '}
                  比例预览，点击即可选定
                </p>
              </div>
              <button
                type="button"
                onClick={() => setThemePickerOpen(false)}
                aria-label="关闭主题选择器"
                className="flex h-8 w-8 items-center justify-center rounded-full bg-[#F4F3FA] text-[#56526B] hover:bg-[#E9E6F7]"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 px-6 pt-4">
              <div className="flex flex-wrap items-center rounded-xl bg-white p-1 shadow-2xs">
                {(['全部', '动态时钟', '壁纸', '主题', '收藏'] as const).map((cat) => (
                  <button
                    key={cat}
                    type="button"
                    onClick={() => setThemePickerFilter(cat)}
                    className={`rounded-lg px-3 py-1.5 text-[12px] font-semibold transition-colors whitespace-nowrap ${
                      themePickerFilter === cat
                        ? 'bg-[#5E45F8] text-white'
                        : 'text-[#6B6880] hover:text-[#1A172B]'
                    }`}
                  >
                    {cat === '收藏' ? '仅收藏' : cat}
                  </button>
                ))}
              </div>

              <div className="flex items-center rounded-xl border border-[#ECEAF6] bg-white px-3 py-1.5">
                <Search className="h-3.5 w-3.5 text-[#9A96AE]" />
                <input
                  type="text"
                  value={themePickerQuery}
                  onChange={(e) => setThemePickerQuery(e.target.value)}
                  placeholder="搜索主题名称..."
                  className="w-44 bg-transparent pl-2 text-[12.5px] text-[#1A172B] focus:outline-none"
                />
              </div>
            </div>

            {pickerFilteredThemes.length === 0 ? (
              <div className="mx-6 my-8 rounded-2xl bg-white py-12 text-center text-[13px] text-[#8B87A1]">
                没有找到匹配的主题内容，请更换筛选条件
              </div>
            ) : (
              <div
                className={`grid max-h-[500px] gap-4 overflow-y-auto p-6 studio-scroll ${
                  formIsPortrait
                    ? 'grid-cols-2 sm:grid-cols-3 md:grid-cols-4'
                    : 'grid-cols-1 sm:grid-cols-2'
                }`}
              >
                {pickerFilteredThemes.map((item) => {
                  const isSelected = item.id === formThemeId;
                  return (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => {
                        setFormThemeId(item.id);
                        setThemePickerOpen(false);
                      }}
                      className={`group flex flex-col overflow-hidden rounded-[20px] border bg-white p-2.5 text-left transition-all duration-150 hover:-translate-y-0.5 ${
                        isSelected
                          ? 'border-[#5E45F8] bg-[#F7F5FF] ring-2 ring-[#5E45F8]/25 shadow-[0_12px_28px_rgba(94,69,248,0.16)]'
                          : 'border-[#ECEAF6] hover:border-[#C8BDFC] hover:shadow-[0_10px_24px_rgba(30,20,80,0.08)]'
                      }`}
                    >
                      <div
                        style={{ aspectRatio: formAspectRatio }}
                        className="relative w-full overflow-hidden rounded-[14px] bg-[#160F30]"
                      >
                        <ThemeArtwork
                          theme={item}
                          className="h-full w-full"
                          showClock={true}
                          compact={formIsPortrait}
                          portrait={formIsPortrait}
                        />
                        {isSelected && (
                          <span className="absolute top-2 right-2 flex h-6 w-6 items-center justify-center rounded-full bg-[#5E45F8] text-white shadow-md">
                            <Check className="h-3.5 w-3.5" />
                          </span>
                        )}
                      </div>

                      <div className="mt-2.5 flex items-center justify-between gap-2 px-1">
                        <div className="min-w-0">
                          <div className="truncate text-[13px] font-bold text-[#1A172B]">
                            {item.displayName}
                          </div>
                          <div className="mt-0.5 flex items-center gap-1.5 text-[11px] text-[#8B87A1]">
                            <span>{item.category}</span>
                            {item.isFavorite && (
                              <>
                                <span aria-hidden="true">·</span>
                                <span className="text-amber-500 font-semibold">已收藏</span>
                              </>
                            )}
                          </div>
                        </div>
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
