import React, { useState, useEffect, useRef } from 'react';
import {
  Device,
  Resource,
  Playlist,
  RecycleEntry,
  AppSettings,
  PreviewSession,
} from './types';
import {
  BASE_DEVICES,
  INITIAL_RESOURCES,
  INITIAL_PLAYLISTS,
  MOODS,
  dims,
  fit,
  sizeText,
} from './data';
import { ScreenBox } from './components/ScreenBox';
import { OverviewPage } from './pages/OverviewPage';
import { DiscoverPage } from './pages/DiscoverPage';
import { LibraryPage } from './pages/LibraryPage';
import { CreatorPage, EditorPage } from './pages/CreatorAndEditorPage';
import { PlaylistsPage } from './pages/PlaylistsPage';
import { DevicesPage } from './pages/DevicesPage';
import {
  PluginsPage,
  AccountPage,
  SettingsPage,
  GuidePage,
} from './pages/OtherPages';
import {
  ResourceDetailModal,
  ApplyModal,
  OrientationConfirmModal,
  RecycleBinModal,
  ExportPlanModal,
} from './components/Modals';
import {
  AddDeviceWizardModal,
  PlaylistAddPickerModal,
  PickPlaylistModal,
} from './components/DeviceWizardAndPickers';
import {
  NewProjectModal,
  AddLayoutModal,
} from './components/EditorModals';
import {
  LayoutDashboard,
  Compass,
  Grid,
  Palette,
  ListVideo,
  Layers,
  Monitor,
  User,
  Settings,
  X,
  Play,
  Square,
  ChevronLeft,
  ChevronRight,
  Sparkles,
} from 'lucide-react';

export default function App() {
  // Scenario & Core datasets
  const [scenario, setScenario] = useState<string>('single92');
  const [devices, setDevices] = useState<Device[]>(() => {
    const d = structuredClone(BASE_DEVICES).filter((x) => x.id === 'a');
    d[0].name = '我的 D92';
    return d;
  });
  const [resources, setResources] = useState<Resource[]>(() =>
    structuredClone(INITIAL_RESOURCES)
  );
  const [playlists, setPlaylists] = useState<Playlist[]>(() =>
    structuredClone(INITIAL_PLAYLISTS)
  );
  const [recycleBin, setRecycleBin] = useState<RecycleEntry[]>([]);
  const [favorites, setFavorites] = useState<number[]>([1, 3, 7, 17, 24]);
  const [downloaded, setDownloaded] = useState<number[]>([1, 2, 3, 4, 5]);
  const [recentHistory, setRecentHistory] = useState<{ id: number; t: number }[]>([
    { id: 3, t: Date.now() - 12 * 60000 },
    { id: 17, t: Date.now() - 95 * 60000 },
    { id: 16, t: Date.now() - 300 * 60000 },
    { id: 24, t: Date.now() - 1600 * 60000 },
    { id: 21, t: Date.now() - 2900 * 60000 },
  ]);

  // Page navigation & selection
  const [page, setPage] = useState<string>('overview');
  const [targetDeviceId, setTargetDeviceId] = useState<string>('a');
  const [detailDeviceId, setDetailDeviceId] = useState<string>('a');
  const [selectedPlaylistIndex, setSelectedPlaylistIndex] = useState<number>(0);
  const [editingResourceId, setEditingResourceId] = useState<number>(1);

  // Settings
  const [settings, setSettings] = useState<AppSettings>({
    tray: true,
    start: false,
    restore: true,
  });

  // Floating temporary preview session
  const [previewSession, setPreviewSession] = useState<PreviewSession | null>(null);

  // Toast message
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const toastTimerRef = useRef<NodeJS.Timeout | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    if (toastTimerRef.current) clearTimeout(toastTimerRef.current);
    toastTimerRef.current = setTimeout(() => setToastMessage(null), 3200);
  };

  // Modals state
  const [detailResourceId, setDetailResourceId] = useState<number | null>(null);
  const [applyResourceId, setApplyResourceId] = useState<number | null>(null);
  const [applyPresetDeviceId, setApplyPresetDeviceId] = useState<string | undefined>(
    undefined
  );
  const [orientationConfirm, setOrientationConfirm] = useState<{
    deviceId: string;
    targetRot: number;
  } | null>(null);
  const [showRecycleBin, setShowRecycleBin] = useState<boolean>(false);
  const [showExportPlan, setShowExportPlan] = useState<boolean>(false);
  const [showAddDeviceWizard, setShowAddDeviceWizard] = useState<boolean>(false);
  const [showPlaylistAddPicker, setShowPlaylistAddPicker] = useState<boolean>(false);
  const [pickPlaylistForIds, setPickPlaylistForIds] = useState<number[] | null>(null);
  const [showNewProjectModal, setShowNewProjectModal] = useState<boolean>(false);
  const [showAddLayoutModal, setShowAddLayoutModal] = useState<boolean>(false);
  const [showDownloadsModal, setShowDownloadsModal] = useState<boolean>(false);

  // Note recent usage
  const noteRecent = (resId: number) => {
    setRecentHistory((prev) => [
      { id: resId, t: Date.now() },
      ...prev.filter((x) => x.id !== resId),
    ]);
  };

  // Scenario Switcher
  const handleSwitchScenario = (sc: string) => {
    setScenario(sc);
    let newDevs: Device[] = [];
    if (sc === 'single92') {
      newDevs = structuredClone(BASE_DEVICES).filter((d) => d.id === 'a');
      newDevs[0].name = '我的 D92';
    } else if (sc === 'single69') {
      newDevs = structuredClone(BASE_DEVICES).filter((d) => d.id === 'c');
      newDevs[0].name = '我的 D69';
    } else if (sc === 'dual') {
      newDevs = structuredClone(BASE_DEVICES).filter((d) => ['a', 'b'].includes(d.id));
    } else if (sc === 'mixed') {
      newDevs = structuredClone(BASE_DEVICES);
    } else if (sc === 'empty') {
      newDevs = [];
    }

    setDevices(newDevs);
    const firstId = newDevs[0]?.id || '';
    setTargetDeviceId(newDevs.length > 1 ? 'all' : firstId);
    setDetailDeviceId(firstId);

    const labels: Record<string, string> = {
      single92: '切换场景：单台 D92（极简横竖切换）',
      single69: '切换场景：单台 D69（桌面小副屏）',
      dual: '切换场景：双台 D92（一横一竖协同）',
      mixed: '切换场景：三台屏幕混合工作台',
      empty: '切换场景：未连接任何硬件屏幕',
    };
    showToast(labels[sc] || '场景已切换');
  };

  // Preview Countdown Timer
  useEffect(() => {
    if (!previewSession) return;
    const interval = setInterval(() => {
      setPreviewSession((prev) => {
        if (!prev) return null;
        if (prev.remaining <= 1) {
          // Revert preview
          endPreview(prev);
          return null;
        }
        return { ...prev, remaining: prev.remaining - 1 };
      });
    }, 1000);

    return () => clearInterval(interval);
  }, [previewSession]);

  const endPreview = (session: PreviewSession, silent = false) => {
    setDevices((prev) =>
      prev.map((d) => {
        if (d.id === session.snap.id) {
          return {
            ...d,
            content: session.snap.content,
            running: d.online && session.snap.running,
            fit: session.snap.fit,
          };
        }
        return d;
      })
    );

    // Restore playlists playing states
    setPlaylists((prev) =>
      prev.map((pl) => {
        const snapPl = session.snap.lists.find((x) => x.p.name === pl.name);
        if (snapPl) {
          return { ...pl, playing: snapPl.playing, at: snapPl.at };
        }
        return pl;
      })
    );

    setPreviewSession(null);
    if (!silent) showToast('预览结束，已自动恢复原屏幕画面');
  };

  const handleKeepPreview = () => {
    if (!previewSession) return;
    const { id, snap } = previewSession;
    setPreviewSession(null);
    const d = devices.find((dev) => dev.id === snap.id);
    if (d && d.online) {
      applyDirect(id, d.id);
      showToast(`已确认将「${getResource(id).name}」保留使用`);
    }
  };

  const startPreview = (resId: number) => {
    const targetDev =
      devices.find((d) => d.id === targetDeviceId && d.online) ||
      devices.find((d) => d.online);
    if (!targetDev) {
      showToast('未检测到已连接的在线设备，无法触发预览');
      return;
    }

    const snap = {
      id: targetDev.id,
      content: targetDev.content,
      running: targetDev.running,
      fit: targetDev.fit,
      lists: playlists
        .filter((p) => p.device === targetDev.id)
        .map((p) => ({ p, playing: p.playing, at: p.at })),
    };

    // Pause playlists on this device during preview
    setPlaylists((prev) =>
      prev.map((p) =>
        p.device === targetDev.id ? { ...p, playing: false } : p
      )
    );

    setDevices((prev) =>
      prev.map((d) =>
        d.id === targetDev.id
          ? { ...d, content: resId, running: true, fit: 'contain' }
          : d
      )
    );

    setPreviewSession({
      snap,
      id: resId,
      remaining: 15,
    });

    showToast(`正在「${targetDev.name}」上触发临时预览 15 秒`);
  };

  // Helper get resource
  const getResource = (id: number): Resource => {
    return (
      resources.find((r) => r.id === id) || {
        id: 0,
        name: '未选择内容',
        type: '壁纸',
        style: 'ink',
        sizes: [],
        local: false,
      }
    );
  };

  // Direct Apply
  const applyDirect = (resId: number, deviceId: string) => {
    const targetDev = devices.find((d) => d.id === deviceId);
    if (!targetDev) return;

    setDevices((prev) =>
      prev.map((d) =>
        d.id === deviceId
          ? { ...d, content: resId, running: true, fit: 'contain' }
          : d
      )
    );

    // Pause playlists assigned to this device
    setPlaylists((prev) =>
      prev.map((p) =>
        p.device === deviceId ? { ...p, playing: false } : p
      )
    );

    noteRecent(resId);
    showToast(`已应用「${getResource(resId).name}」至「${targetDev.name}」`);
  };

  // Handle Apply click from card or detail
  const handleApplyResource = (resId: number, preferDeviceId?: string) => {
    const r = getResource(resId);
    if (!r.local) {
      // Download it first
      handleDownloadResource(resId);
    }

    if (devices.length === 1) {
      const d = devices[0];
      const f = fit(r, d);
      if (d.online && r.sizes.length && f.loss !== null && f.loss <= 0.05) {
        applyDirect(resId, d.id);
        return;
      }
    }

    setApplyResourceId(resId);
    setApplyPresetDeviceId(preferDeviceId || targetDeviceId);
  };

  // Apply Modal Confirm
  const handleConfirmApply = (
    deviceIds: string[],
    modes: Record<string, 'contain' | 'cover'>
  ) => {
    if (!applyResourceId) return;
    const resId = applyResourceId;

    setDevices((prev) =>
      prev.map((d) => {
        if (deviceIds.includes(d.id)) {
          return {
            ...d,
            content: resId,
            running: true,
            fit: modes[d.id] || 'contain',
          };
        }
        return d;
      })
    );

    setPlaylists((prev) =>
      prev.map((p) =>
        deviceIds.includes(p.device) ? { ...p, playing: false } : p
      )
    );

    noteRecent(resId);
    setApplyResourceId(null);

    const devNames = deviceIds
      .map((id) => devices.find((d) => d.id === id)?.name)
      .filter(Boolean)
      .join('、');
    showToast(`已应用「${getResource(resId).name}」至 ${devNames}`);
  };

  // Toggle favorite
  const handleToggleFavorite = (id: number) => {
    setFavorites((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
    showToast('收藏状态已更新');
  };

  // Download online resource
  const handleDownloadResource = (id: number) => {
    setResources((prev) =>
      prev.map((r) => (r.id === id ? { ...r, local: true } : r))
    );
    if (!downloaded.includes(id)) {
      setDownloaded((prev) => [...prev, id]);
    }
    showToast(`「${getResource(id).name}」已下载并加入本地资源库`);
  };

  // Delete Resource & Safety Impacts
  const handleDeleteResource = (id: number) => {
    const r = getResource(id);
    const affectedDevices = devices.filter((d) => d.content === id);
    const affectedPlaylists = playlists.filter((p) =>
      p.items.some((it) => it.id === id)
    );

    // Stop output on affected devices
    setDevices((prev) =>
      prev.map((d) =>
        d.content === id ? { ...d, content: 0, running: false } : d
      )
    );

    // Store in recycle bin for recovery
    const refs = playlists.flatMap((p) =>
      p.items
        .map((item, index) => ({ item, index }))
        .filter((x) => x.item.id === id)
        .map((x) => ({ playlistName: p.name, index: x.index, item: x.item }))
    );

    setRecycleBin((prev) => [
      {
        resource: structuredClone(r),
        fav: favorites.includes(id),
        downloaded: downloaded.includes(id),
        refs,
      },
      ...prev,
    ]);

    // Remove from playlists
    setPlaylists((prev) =>
      prev.map((p) => {
        const has = p.items.some((it) => it.id === id);
        if (!has) return p;
        const newItems = p.items.filter((it) => it.id !== id);
        return {
          ...p,
          items: newItems,
          playing: p.playing && newItems.length > 0,
          at: 0,
        };
      })
    );

    // Remove from local list
    if (r.online) {
      setResources((prev) =>
        prev.map((res) => (res.id === id ? { ...res, local: false } : res))
      );
    } else {
      setResources((prev) => prev.filter((res) => res.id !== id));
    }

    setFavorites((prev) => prev.filter((x) => x !== id));
    showToast(`已删除「${r.name}」并安全移入回收站`);
  };

  // Restore from recycle bin
  const handleRestoreRecycle = (index: number) => {
    const entry = recycleBin[index];
    if (!entry) return;

    const r = entry.resource;
    setResources((prev) => {
      const exists = prev.find((x) => x.id === r.id);
      if (exists) {
        return prev.map((x) => (x.id === r.id ? { ...x, local: true } : x));
      }
      return [r, ...prev];
    });

    if (entry.fav) setFavorites((prev) => [...prev, r.id]);
    if (entry.downloaded) setDownloaded((prev) => [...prev, r.id]);

    // Restore into playlists
    setPlaylists((prev) =>
      prev.map((p) => {
        const ref = entry.refs.find((ref) => ref.playlistName === p.name);
        if (ref && !p.items.some((it) => it.id === r.id)) {
          const newItems = [...p.items];
          newItems.splice(Math.min(ref.index, newItems.length), 0, ref.item);
          return { ...p, items: newItems };
        }
        return p;
      })
    );

    setRecycleBin((prev) => prev.filter((_, i) => i !== index));
    showToast(`已成功恢复「${r.name}」与关联列表位置`);
  };

  // Toggle Output
  const handleToggleOutput = (deviceId: string) => {
    const d = devices.find((dev) => dev.id === deviceId);
    if (!d) return;
    if (!d.running && !d.content) {
      showToast('该屏幕尚未选择内容，请先选择要呈现的画面');
      return;
    }

    setDevices((prev) =>
      prev.map((dev) =>
        dev.id === deviceId ? { ...dev, running: !dev.running } : dev
      )
    );

    // If stopping, pause related playlist
    if (d.running) {
      setPlaylists((prev) =>
        prev.map((p) =>
          p.device === deviceId ? { ...p, playing: false } : p
        )
      );
    }

    showToast(
      `${d.name}：${!d.running ? '已恢复实时画面输出' : '已暂停输出，设备保留最后一帧'}`
    );
  };

  // Request Orientation Switch (with smart replacement detection)
  const handleRequestOrientation = (deviceId: string, targetRot: number) => {
    const d = devices.find((dev) => dev.id === deviceId);
    if (!d) return;

    const isCurrentVertical = d.rot % 180 !== 0;
    const isTargetVertical = targetRot % 180 !== 0;
    if (isCurrentVertical === isTargetVertical) return;

    const curRes = getResource(d.content);
    const probe = { ...d, rot: targetRot };
    const f = fit(curRes, probe);
    const isFitOk = !d.content || (f.loss !== null && f.loss <= 0.05);

    if (isFitOk || !d.running) {
      // Direct orientation change
      executeOrientationChange(deviceId, targetRot);
      return;
    }

    // Find smart replacement
    const matchingReplacement = resources.find(
      (r) =>
        r.local &&
        r.sizes.length > 0 &&
        fit(r, probe).loss !== null &&
        fit(r, probe).loss! <= 0.05 &&
        r.id !== curRes.id
    );

    setOrientationConfirm({
      deviceId,
      targetRot,
    });
  };

  const executeOrientationChange = (
    deviceId: string,
    targetRot: number,
    replacementId?: number
  ) => {
    setDevices((prev) =>
      prev.map((d) => {
        if (d.id === deviceId) {
          const newContent = replacementId ?? d.content;
          return {
            ...d,
            rot: targetRot,
            content: newContent,
            running: replacementId ? true : d.running,
          };
        }
        return d;
      })
    );

    setOrientationConfirm(null);
    const isVert = targetRot % 180 !== 0;
    showToast(`屏幕已切换为${isVert ? '竖屏' : '横屏'}模式`);
  };

  // Playlist Navigation
  const handleStepPlaylist = (deviceId: string, dir: number) => {
    const pl = playlists.find(
      (p) => p.playing && p.device === deviceId && p.items.length > 0
    );
    if (!pl) return;

    const nextAt = (pl.at + dir + pl.items.length) % pl.items.length;
    setPlaylists((prev) =>
      prev.map((p) => (p === pl ? { ...p, at: nextAt } : p))
    );

    const nextResId = pl.items[nextAt]?.id;
    if (nextResId) {
      setDevices((prev) =>
        prev.map((d) =>
          d.id === deviceId ? { ...d, content: nextResId, running: true } : d
        )
      );
    }
  };

  const handleTogglePlaylist = (index: number) => {
    const p = playlists[index];
    if (!p) return;
    const targetDev = devices.find((d) => d.id === p.device);
    if (!targetDev?.online) {
      showToast('请先连接关联的输出屏幕设备');
      return;
    }
    if (p.items.length === 0) {
      showToast('请先添加至少一项内容到此轮播队列');
      return;
    }

    const nextPlaying = !p.playing;
    setPlaylists((prev) =>
      prev.map((item, idx) =>
        idx === index
          ? { ...item, playing: nextPlaying }
          : item.device === p.device
          ? { ...item, playing: false }
          : item
      )
    );

    if (nextPlaying) {
      const activeResId = p.items[p.at]?.id || p.items[0]?.id;
      setDevices((prev) =>
        prev.map((d) =>
          d.id === p.device ? { ...d, content: activeResId, running: true } : d
        )
      );
      showToast(`已开始轮播「${p.name}」`);
    } else {
      showToast(`已暂停「${p.name}」切换，保留当前画面`);
    }
  };

  const handlePlayPlaylistNow = (index: number) => {
    const p = playlists[index];
    if (!p) return;
    const targetDev = devices.find((d) => d.id === p.device);
    if (!targetDev?.online) {
      showToast('请先连接关联的输出屏幕设备');
      return;
    }

    setPlaylists((prev) =>
      prev.map((item, idx) =>
        idx === index
          ? { ...item, playing: true }
          : item.device === p.device
          ? { ...item, playing: false }
          : item
      )
    );

    const activeResId = p.items[p.at]?.id || p.items[0]?.id;
    setDevices((prev) =>
      prev.map((d) =>
        d.id === p.device ? { ...d, content: activeResId, running: true } : d
      )
    );
    showToast(`已启动「${p.name}」轮播`);
  };

  // Import Image
  const handleImportImage = (file: File) => {
    if (!file.type.startsWith('image/')) {
      showToast('请选择有效的图片素材文件');
      return;
    }
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      const newId = Math.max(...resources.map((r) => r.id), 0) + 1;
      const newResource: Resource = {
        id: newId,
        name: file.name.replace(/\.[^/.]+$/, ''),
        type: '壁纸',
        style: '',
        sizes: [[img.width, img.height]],
        local: true,
        url,
        added: Date.now(),
      };
      setResources((prev) => [newResource, ...prev]);
      showToast(`已导入「${newResource.name}」至本地资源库`);
    };
    img.src = url;
  };

  // Use as background for new theme
  const handleUseAsBackground = (resId: number) => {
    const r = getResource(resId);
    const newId = Math.max(...resources.map((x) => x.id), 0) + 1;
    const newTheme: Resource = {
      id: newId,
      name: `${r.name} · 自定义主题`,
      type: '主题',
      style: r.style || 'ink',
      sizes: [[1920, 462]],
      local: true,
      created: true,
      url: r.url,
      bgCopy: { from: resId, name: r.name },
      text: '10:24',
      added: Date.now(),
    };
    setResources((prev) => [newTheme, ...prev]);
    setEditingResourceId(newId);
    setPage('editor');
    showToast(`已基于「${r.name}」创建新主题，进入编辑模式`);
  };

  // Device Flash Identification
  const handleIdentifyDevice = (id: string) => {
    setDevices((prev) =>
      prev.map((d) => (d.id === id ? { ...d, flash: true } : d))
    );
    showToast(`正在令物理副屏快速高亮闪烁，方便桌面识别`);
    setTimeout(() => {
      setDevices((prev) =>
        prev.map((d) => (d.id === id ? { ...d, flash: false } : d))
      );
    }, 1800);
  };

  // Nav configuration
  const navItems = [
    { id: 'overview', icon: LayoutDashboard, label: '首页' },
    { id: 'discover', icon: Compass, label: '在线资源' },
    { id: 'library', icon: Grid, label: '资源库' },
    { id: 'creator', icon: Palette, label: '创作中心' },
    { id: 'playlists', icon: ListVideo, label: '轮播列表' },
    { id: 'plugins', icon: Layers, label: '插件中心' },
    { id: 'devices', icon: Monitor, label: '设备管理' },
  ];

  return (
    <div className="min-h-screen flex bg-[#f5f7fb] text-[#25314b]">
      {/* Sidebar Navigation */}
      <aside className="w-52 shrink-0 bg-white border-r border-[#e5eaf2] flex flex-col fixed top-0 bottom-0 z-30">
        {/* Brand */}
        <div className="p-5 pb-4 flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-xl bg-[#4268ef] text-white flex items-center justify-center font-bold text-base shadow-sm">
            D
          </div>
          <div>
            <strong className="text-sm font-bold text-slate-900 tracking-tight block">
              D92 Studio
            </strong>
            <span className="text-[10px] text-slate-400 font-medium">
              MiraBox 控制中心
            </span>
          </div>
        </div>

        {/* Nav Links */}
        <nav className="flex-1 px-3 py-2 space-y-1 overflow-y-auto">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive =
              page === item.id ||
              (page === 'editor' && item.id === 'creator');

            return (
              <button
                key={item.id}
                onClick={() => setPage(item.id)}
                className={`w-full flex items-center gap-3 px-3 py-2 rounded-xl text-xs font-semibold transition-all ${
                  isActive
                    ? 'bg-[#edf2ff] text-[#4268ef]'
                    : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
                }`}
              >
                <Icon className="w-4 h-4 shrink-0" />
                <span>{item.label}</span>
              </button>
            );
          })}
        </nav>

        {/* Sidebar Bottom */}
        <div className="p-3 border-t border-[#e5eaf2] space-y-1">
          <button
            onClick={() => setPage('account')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-xl text-xs font-medium transition-colors ${
              page === 'account'
                ? 'bg-[#edf2ff] text-[#4268ef] font-semibold'
                : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <User className="w-4 h-4" />
            <span>个人中心</span>
          </button>
          <button
            onClick={() => setPage('settings')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-xl text-xs font-medium transition-colors ${
              page === 'settings'
                ? 'bg-[#edf2ff] text-[#4268ef] font-semibold'
                : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            <Settings className="w-4 h-4" />
            <span>系统设置</span>
          </button>

          <div className="px-3 pt-3 text-[10px] text-slate-400 leading-tight">
            我的 MiraBox 工作空间
            <br />
            多设备控制架构 · V6.3
          </div>
        </div>
      </aside>

      {/* Main Container */}
      <div className="flex-1 ml-52 flex flex-col min-h-screen">
        {/* TopBar */}
        <header className="h-16 bg-white/80 backdrop-blur-md border-b border-[#e5eaf2] px-8 flex items-center justify-between sticky top-0 z-20">
          <div className="flex items-center gap-2 text-xs">
            <span className="text-slate-400">工作空间 /</span>
            <strong className="text-slate-800 font-semibold">
              {navItems.find((n) => n.id === page)?.label ||
                (page === 'editor'
                  ? '主题设计编辑器'
                  : page === 'account'
                  ? '个人中心'
                  : page === 'settings'
                  ? '系统设置'
                  : '架构规划')}
            </strong>
            <span className="ml-2 px-2 py-0.5 rounded text-[10px] font-medium bg-[#fff5d9] text-[#8a6a26] border border-[#f5e4b8]">
              交互原型 V6.3 · 大图自适应
            </span>
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <label className="text-xs text-slate-400">演示场景：</label>
              <select
                value={scenario}
                onChange={(e) => handleSwitchScenario(e.target.value)}
                className="px-2.5 py-1 text-xs border border-slate-200 rounded-lg bg-white text-slate-800 font-medium focus:outline-[#4268ef]"
              >
                <option value="single92">只有一台 D92（默认推荐）</option>
                <option value="single69">只有一台 D69</option>
                <option value="dual">两台 D92（一横一竖桌面协同）</option>
                <option value="mixed">三台设备（D92 × 2 + D69）</option>
                <option value="empty">没有连接设备</option>
              </select>
            </div>

            <span
              className={`px-2.5 py-1 rounded-full text-[11px] font-medium flex items-center gap-1.5 ${
                devices.length === 0
                  ? 'bg-slate-100 text-slate-500'
                  : devices.some((d) => d.online)
                  ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                  : 'bg-amber-50 text-amber-700 border border-amber-200'
              }`}
            >
              <span
                className={`w-1.5 h-1.5 rounded-full ${
                  devices.length === 0
                    ? 'bg-slate-400'
                    : devices.some((d) => d.online)
                    ? 'bg-emerald-500'
                    : 'bg-amber-500'
                }`}
              />
              {devices.length === 0
                ? '○ 未检测到设备'
                : devices.length === 1
                ? `${devices[0].name} · ${devices[0].online ? '在线' : '离线'}`
                : `${devices.filter((d) => d.online).length} 台在线 · ${
                    devices.filter((d) => d.online && d.running).length
                  } 台输出`}
            </span>

            <button
              onClick={() => setPage('devices')}
              className="px-3 py-1 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700"
            >
              设备管理
            </button>
            <button
              onClick={() => setPage('guide')}
              className="px-3 py-1 text-xs font-medium rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700"
            >
              规划说明
            </button>
          </div>
        </header>

        {/* Page Body */}
        <main className="flex-1 p-8 max-w-7xl w-full mx-auto pb-32">
          {page === 'overview' && (
            <OverviewPage
              devices={devices}
              resources={resources}
              playlists={playlists}
              favorites={favorites}
              recentHistory={recentHistory}
              onGoPage={setPage}
              onRequestOrientation={handleRequestOrientation}
              onToggleOutput={handleToggleOutput}
              onStepPlaylist={handleStepPlaylist}
              onSelectTargetDevice={setTargetDeviceId}
              onOpenDetail={(id) => setDetailResourceId(id)}
              onOpenEditor={(id) => {
                setEditingResourceId(id);
                setPage('editor');
              }}
              onApply={(id, devId) => handleApplyResource(id, devId)}
              onApplyOther={(id) => {
                setApplyResourceId(id);
                setApplyPresetDeviceId(devices[0]?.id);
              }}
              onAddToPlaylist={(id) => setPickPlaylistForIds([id])}
              onToggleFavorite={handleToggleFavorite}
              onPlayPlaylistNow={handlePlayPlaylistNow}
              onConnectSingle={() => {
                setDevices((prev) =>
                  prev.map((d, i) => (i === 0 ? { ...d, online: true } : d))
                );
                showToast('设备已重新连接');
              }}
            />
          )}

          {page === 'discover' && (
            <DiscoverPage
              devices={devices}
              resources={resources}
              favorites={favorites}
              downloaded={downloaded}
              activeDevice={
                devices.find((d) => d.id === targetDeviceId) || devices[0]
              }
              onDownload={handleDownloadResource}
              onOpenDetail={(id) => setDetailResourceId(id)}
              onToggleFavorite={handleToggleFavorite}
              onApply={(id) => handleApplyResource(id)}
              onApplyOther={(id) => {
                setApplyResourceId(id);
                setApplyPresetDeviceId(devices[0]?.id);
              }}
              onAddToPlaylist={(id) => setPickPlaylistForIds([id])}
              onOpenDownloadsModal={() => setShowDownloadsModal(true)}
            />
          )}

          {page === 'library' && (
            <LibraryPage
              devices={devices}
              resources={resources}
              favorites={favorites}
              downloaded={downloaded}
              recycleBinCount={recycleBin.length}
              activeDeviceId={targetDeviceId}
              onSelectTargetDevice={setTargetDeviceId}
              onImportImage={handleImportImage}
              onOpenNewProject={() => setShowNewProjectModal(true)}
              onOpenRecycleBin={() => setShowRecycleBin(true)}
              onOpenDetail={(id) => setDetailResourceId(id)}
              onOpenEditor={(id) => {
                setEditingResourceId(id);
                setPage('editor');
              }}
              onApply={(id, devId) => handleApplyResource(id, devId)}
              onApplyOther={(id) => {
                setApplyResourceId(id);
                setApplyPresetDeviceId(devices[0]?.id);
              }}
              onAddToPlaylist={(id) => setPickPlaylistForIds([id])}
              onBatchAddToPlaylist={(ids) => setPickPlaylistForIds(ids)}
              onToggleFavorite={handleToggleFavorite}
              onDeleteResource={handleDeleteResource}
            />
          )}

          {page === 'creator' && (
            <CreatorPage
              resources={resources}
              onOpenNewProject={() => setShowNewProjectModal(true)}
              onOpenEditor={(id) => {
                setEditingResourceId(id);
                setPage('editor');
              }}
              onApplyModal={(id) => handleApplyResource(id)}
            />
          )}

          {page === 'editor' && (
            <EditorPage
              resource={getResource(editingResourceId)}
              devices={devices}
              onBack={() => setPage('creator')}
              onSave={(updated) => {
                setResources((prev) =>
                  prev.map((r) => (r.id === updated.id ? updated : r))
                );
                showToast('主题排版与设计已保存');
              }}
              onFinishAndApply={(updated) => {
                setResources((prev) =>
                  prev.map((r) => (r.id === updated.id ? updated : r))
                );
                handleApplyResource(updated.id);
              }}
              onOpenAddLayoutModal={() => setShowAddLayoutModal(true)}
            />
          )}

          {page === 'playlists' && (
            <PlaylistsPage
              playlists={playlists}
              devices={devices}
              resources={resources}
              selectedPlaylistIndex={selectedPlaylistIndex}
              onSelectPlaylist={setSelectedPlaylistIndex}
              onNewPlaylist={() => {
                const name = `轮播列表 #${playlists.length + 1}`;
                setPlaylists((prev) => [
                  ...prev,
                  {
                    name,
                    mood: 'idea',
                    items: [],
                    device: devices[0]?.id || 'a',
                    playing: false,
                    at: 0,
                  },
                ]);
                setSelectedPlaylistIndex(playlists.length);
                showToast(`已创建「${name}」`);
              }}
              onOpenAddPicker={() => setShowPlaylistAddPicker(true)}
              onTogglePlaylist={handleTogglePlaylist}
              onStepPlaylist={handleStepPlaylist}
              onChangePlaylistDevice={(plIdx, devId) => {
                setPlaylists((prev) =>
                  prev.map((p, i) =>
                    i === plIdx ? { ...p, device: devId, playing: false } : p
                  )
                );
              }}
              onChangePlaylistMood={(plIdx, mood) => {
                setPlaylists((prev) =>
                  prev.map((p, i) => (i === plIdx ? { ...p, mood } : p))
                );
              }}
              onToggleShuffle={(plIdx, shuffle) => {
                setPlaylists((prev) =>
                  prev.map((p, i) => (i === plIdx ? { ...p, shuffle } : p))
                );
                showToast(shuffle ? '已开启随机乱序切换' : '已恢复顺序切换');
              }}
              onApplyUniformSeconds={(plIdx, sec) => {
                setPlaylists((prev) =>
                  prev.map((p, i) =>
                    i === plIdx
                      ? {
                          ...p,
                          items: p.items.map((it) => ({ ...it, sec })),
                        }
                      : p
                  )
                );
                showToast(`已将所有素材停留时间统一设为 ${sec} 秒`);
              }}
              onChangeItemSeconds={(plIdx, itIdx, sec) => {
                setPlaylists((prev) =>
                  prev.map((p, i) =>
                    i === plIdx
                      ? {
                          ...p,
                          items: p.items.map((it, j) =>
                            j === itIdx ? { ...it, sec } : it
                          ),
                        }
                      : p
                  )
                );
              }}
              onMoveItem={(plIdx, itIdx, dir) => {
                setPlaylists((prev) =>
                  prev.map((p, i) => {
                    if (i !== plIdx) return p;
                    const items = [...p.items];
                    const targetIdx = itIdx + dir;
                    if (targetIdx < 0 || targetIdx >= items.length) return p;
                    const temp = items[itIdx];
                    items[itIdx] = items[targetIdx];
                    items[targetIdx] = temp;
                    return { ...p, items };
                  })
                );
              }}
              onRemoveItem={(plIdx, itIdx) => {
                setPlaylists((prev) =>
                  prev.map((p, i) =>
                    i === plIdx
                      ? {
                          ...p,
                          items: p.items.filter((_, j) => j !== itIdx),
                          at: 0,
                          playing: p.playing && p.items.length > 1,
                        }
                      : p
                  )
                );
              }}
            />
          )}

          {page === 'devices' && (
            <DevicesPage
              devices={devices}
              resources={resources}
              detailDeviceId={detailDeviceId}
              onSelectDetailDevice={setDetailDeviceId}
              onOpenAddDeviceWizard={() => setShowAddDeviceWizard(true)}
              onSaveDevice={(id, newName, newRot) => {
                setDevices((prev) =>
                  prev.map((d) =>
                    d.id === id ? { ...d, name: newName, rot: newRot } : d
                  )
                );
                showToast('设备名称与旋转方向已保存');
              }}
              onIdentifyDevice={handleIdentifyDevice}
              onToggleOnline={(id) => {
                setDevices((prev) =>
                  prev.map((d) =>
                    d.id === id ? { ...d, online: !d.online } : d
                  )
                );
                showToast('已切换该设备的连接状态');
              }}
              onToggleOutput={handleToggleOutput}
              onGoLibraryWithTarget={(id) => {
                setTargetDeviceId(id);
                setPage('library');
              }}
              onGoPlaylists={() => setPage('playlists')}
              onShowToast={showToast}
            />
          )}

          {page === 'plugins' && (
            <PluginsPage
              onOpenNewProject={() => {
                setPage('creator');
                setShowNewProjectModal(true);
              }}
              onShowToast={showToast}
            />
          )}

          {page === 'account' && (
            <AccountPage
              resourcesCount={resources.filter((r) => r.local).length}
              favoritesCount={favorites.length}
              devicesCount={devices.length}
              onGoSettings={() => setPage('settings')}
              onShowToast={showToast}
            />
          )}

          {page === 'settings' && (
            <SettingsPage
              settings={settings}
              devices={devices}
              onToggleSetting={(key) => {
                setSettings((prev) => ({ ...prev, [key]: !prev[key] }));
                showToast('偏好设置已更新');
              }}
              onOpenExportModal={() => setShowExportPlan(true)}
              onResetDemo={() => {
                setDevices(
                  structuredClone(BASE_DEVICES).filter((d) => d.id === 'a')
                );
                setResources(structuredClone(INITIAL_RESOURCES));
                setPlaylists(structuredClone(INITIAL_PLAYLISTS));
                setFavorites([1, 3, 7, 17, 24]);
                showToast('所有演示数据已重置为初始状态');
              }}
              onGoGuide={() => setPage('guide')}
            />
          )}

          {page === 'guide' && (
            <GuidePage
              onSetScenario={(sc, p) => {
                handleSwitchScenario(sc);
                setPage(p);
              }}
              onGoPage={setPage}
            />
          )}
        </main>

        {/* Floating Preview Countdown Bar (V5/V6 Feature) */}
        {previewSession && (
          <div className="fixed bottom-20 left-1/2 -translate-x-1/2 z-50 bg-white border border-[#cbd6ff] shadow-2xl px-5 py-3 rounded-2xl flex items-center gap-4 text-xs animate-bounce-subtle">
            <span className="font-semibold text-slate-800 flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-[#4268ef]" />
              {devices.find((d) => d.id === previewSession.snap.id)?.name || '屏幕'}
              · 临时预览中：
              <b className="text-[#4268ef] text-sm tabular-nums">
                {previewSession.remaining}
              </b>
              秒
            </span>
            <div className="h-4 w-px bg-slate-200" />
            <button
              onClick={() => endPreview(previewSession)}
              className="px-3 py-1.5 rounded-lg border border-slate-200 hover:bg-slate-50 text-slate-700 font-medium"
            >
              结束并恢复原内容
            </button>
            <button
              onClick={handleKeepPreview}
              className="px-3.5 py-1.5 rounded-lg bg-[#4268ef] hover:bg-[#3458db] text-white font-semibold shadow-xs"
            >
              保留并使用此画面
            </button>
          </div>
        )}

        {/* Permanent Bottom Output Bar */}
        <footer className="fixed bottom-0 left-52 right-0 bg-white/95 backdrop-blur-md border-t border-[#e5eaf2] px-6 py-2 z-20 flex items-center justify-between text-xs min-h-[58px]">
          <div className="flex items-center gap-3 overflow-x-auto flex-1 mr-4 py-0.5">
            {devices.length === 0 ? (
              <span className="text-slate-400">
                未检测到已连接屏幕 · 连接后在此显示各屏幕的实时画面
              </span>
            ) : (
              devices.map((d) => {
                const curRes = getResource(d.content);
                const pl = playlists.find(
                  (p) => p.playing && p.device === d.id && p.items.length > 0
                );

                return (
                  <div
                    key={d.id}
                    className={`flex items-center gap-2.5 p-1.5 pr-2 rounded-xl border shrink-0 transition-colors ${
                      d.online
                        ? 'border-slate-200 bg-slate-50/70'
                        : 'border-slate-200 bg-slate-100 opacity-60'
                    }`}
                  >
                    {/* Miniature Screen Thumbnail */}
                    <div className="w-20 h-10 bg-[#131c2d] rounded-md overflow-hidden flex items-center justify-center shrink-0 border border-slate-300">
                      <ScreenBox
                        resource={curRes}
                        device={d}
                        mode={d.fit || 'contain'}
                        className="w-full h-full"
                      />
                    </div>

                    <div className="leading-tight min-w-[70px] max-w-[130px]">
                      <strong className="text-slate-900 block truncate text-[11px]">
                        {devices.length === 1 ? '正在显示' : d.name}
                      </strong>
                      <span className="text-slate-400 text-[10px] block truncate">
                        {pl && d.running
                          ? `${curRes.name} (${pl.at + 1}/${pl.items.length})`
                          : !d.online
                          ? '未连接'
                          : d.running
                          ? curRes.name
                          : '已暂停输出'}
                      </span>
                    </div>

                    {/* Quick controls per screen */}
                    <div className="flex items-center gap-1">
                      {pl && (
                        <>
                          <button
                            disabled={!d.online}
                            onClick={() => handleStepPlaylist(d.id, -1)}
                            className="p-1 rounded hover:bg-slate-200 text-slate-600 disabled:opacity-40"
                            title="上一项"
                          >
                            <ChevronLeft className="w-3.5 h-3.5" />
                          </button>
                          <button
                            disabled={!d.online}
                            onClick={() => handleStepPlaylist(d.id, 1)}
                            className="p-1 rounded hover:bg-slate-200 text-slate-600 disabled:opacity-40"
                            title="下一项"
                          >
                            <ChevronRight className="w-3.5 h-3.5" />
                          </button>
                        </>
                      )}
                      <button
                        disabled={!d.online}
                        onClick={() => handleToggleOutput(d.id)}
                        className={`px-2 py-1 rounded text-[10px] font-semibold flex items-center gap-1 ${
                          d.running
                            ? 'text-slate-700 hover:bg-slate-200'
                            : 'bg-[#4268ef] text-white hover:bg-[#3458db]'
                        }`}
                      >
                        {d.running ? '■ 停止' : '▶ 恢复'}
                      </button>
                    </div>
                  </div>
                );
              })
            )}
          </div>

          <div className="text-[11px] text-slate-400 shrink-0">
            MiraBox 硬件会话输出 · 独立控制
          </div>
        </footer>
      </div>

      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed bottom-16 left-1/2 -translate-x-1/2 z-50 bg-slate-900 text-white text-xs font-medium px-4 py-2.5 rounded-xl shadow-xl flex items-center gap-2">
          <span>{toastMessage}</span>
        </div>
      )}

      {/* --- ALL POPUP MODALS --- */}
      {detailResourceId !== null && (
        <ResourceDetailModal
          resource={getResource(detailResourceId)}
          devices={devices}
          isFavorite={favorites.includes(detailResourceId)}
          onClose={() => setDetailResourceId(null)}
          onToggleFavorite={handleToggleFavorite}
          onAddToPlaylist={(id) => setPickPlaylistForIds([id])}
          onDelete={handleDeleteResource}
          onUseAsBackground={handleUseAsBackground}
          onOpenEditor={(id) => {
            setDetailResourceId(null);
            setEditingResourceId(id);
            setPage('editor');
          }}
          onPreview={(id) => startPreview(id)}
          onApply={(id) => handleApplyResource(id)}
          onApplyOther={(id) => {
            setDetailResourceId(null);
            setApplyResourceId(id);
            setApplyPresetDeviceId(devices[0]?.id);
          }}
        />
      )}

      {applyResourceId !== null && (
        <ApplyModal
          resource={getResource(applyResourceId)}
          devices={devices}
          presetDeviceId={applyPresetDeviceId}
          onClose={() => setApplyResourceId(null)}
          onConfirm={handleConfirmApply}
        />
      )}

      {orientationConfirm !== null && (
        <OrientationConfirmModal
          device={
            devices.find((d) => d.id === orientationConfirm.deviceId) ||
            devices[0]
          }
          targetRot={orientationConfirm.targetRot}
          currentResource={getResource(
            devices.find((d) => d.id === orientationConfirm.deviceId)?.content ||
              0
          )}
          suggestedResource={resources.find(
            (r) =>
              r.local &&
              r.sizes.length > 0 &&
              fit(r, {
                ...devices.find((d) => d.id === orientationConfirm.deviceId)!,
                rot: orientationConfirm.targetRot,
              }).loss! <= 0.05
          )}
          onClose={() => setOrientationConfirm(null)}
          onConfirmOnly={() =>
            executeOrientationChange(
              orientationConfirm.deviceId,
              orientationConfirm.targetRot
            )
          }
          onConfirmWithReplacement={(replacementId) =>
            executeOrientationChange(
              orientationConfirm.deviceId,
              orientationConfirm.targetRot,
              replacementId
            )
          }
        />
      )}

      {showRecycleBin && (
        <RecycleBinModal
          recycleBin={recycleBin}
          onClose={() => setShowRecycleBin(false)}
          onRestore={handleRestoreRecycle}
        />
      )}

      {showExportPlan && (
        <ExportPlanModal
          data={{
            version: '6.3-production-ready',
            devices,
            resources: resources.map(({ url, ...r }) => r),
            playlists,
            settings,
          }}
          onClose={() => setShowExportPlan(false)}
          onCopySuccess={() => showToast('已成功复制完整规划 JSON 到剪贴板')}
        />
      )}

      {showAddDeviceWizard && (
        <AddDeviceWizardModal
          existingDevices={devices}
          resources={resources}
          onClose={() => setShowAddDeviceWizard(false)}
          onFinish={(newDev) => {
            setDevices((prev) => [...prev, newDev]);
            setTargetDeviceId(newDev.id);
            setDetailDeviceId(newDev.id);
            setShowAddDeviceWizard(false);
            showToast(`已成功连接新设备「${newDev.name}」`);
          }}
        />
      )}

      {showPlaylistAddPicker && (
        <PlaylistAddPickerModal
          playlist={playlists[selectedPlaylistIndex] || playlists[0]}
          targetDevice={devices.find(
            (d) =>
              d.id ===
              (playlists[selectedPlaylistIndex] || playlists[0]).device
          )}
          resources={resources}
          onClose={() => setShowPlaylistAddPicker(false)}
          onAddItems={(ids) => {
            setPlaylists((prev) =>
              prev.map((p, idx) =>
                idx === selectedPlaylistIndex
                  ? {
                      ...p,
                      items: [
                        ...p.items,
                        ...ids
                          .filter((id) => !p.items.some((it) => it.id === id))
                          .map((id) => ({ id, sec: 30 })),
                      ],
                    }
                  : p
              )
            );
            setShowPlaylistAddPicker(false);
            showToast(`已加入 ${ids.length} 项新内容至轮播队列`);
          }}
        />
      )}

      {pickPlaylistForIds !== null && (
        <PickPlaylistModal
          resourceIds={pickPlaylistForIds}
          playlists={playlists}
          devices={devices}
          resources={resources}
          onClose={() => setPickPlaylistForIds(null)}
          onAddMany={(plIdx, ids) => {
            setPlaylists((prev) =>
              prev.map((p, idx) =>
                idx === plIdx
                  ? {
                      ...p,
                      items: [
                        ...p.items,
                        ...ids
                          .filter((id) => !p.items.some((it) => it.id === id))
                          .map((id) => ({ id, sec: 30 })),
                      ],
                    }
                  : p
              )
            );
            setPickPlaylistForIds(null);
            showToast(
              `已成功加入 ${ids.length} 项内容到「${playlists[plIdx].name}」`
            );
          }}
          onCreateAndAdd={(name, ids) => {
            const newPl: Playlist = {
              name,
              mood: 'idea',
              items: ids.map((id) => ({ id, sec: 30 })),
              device: devices[0]?.id || 'a',
              playing: false,
              at: 0,
            };
            setPlaylists((prev) => [...prev, newPl]);
            setPickPlaylistForIds(null);
            showToast(`已新建「${name}」并加入 ${ids.length} 项内容`);
          }}
        />
      )}

      {showNewProjectModal && (
        <NewProjectModal
          onClose={() => setShowNewProjectModal(false)}
          onCreate={(name, size) => {
            const newId = Math.max(...resources.map((r) => r.id), 0) + 1;
            const newTheme: Resource = {
              id: newId,
              name,
              type: '主题',
              style: 'paper',
              sizes: [size],
              local: true,
              created: true,
              text: '10:24',
              added: Date.now(),
            };
            setResources((prev) => [newTheme, ...prev]);
            setEditingResourceId(newId);
            setShowNewProjectModal(false);
            setPage('editor');
            showToast(`已创建主题「${name}」，进入画布编辑器`);
          }}
        />
      )}

      {showAddLayoutModal && (
        <AddLayoutModal
          existingSizes={getResource(editingResourceId).sizes}
          onClose={() => setShowAddLayoutModal(false)}
          onAdd={(newSize) => {
            setResources((prev) =>
              prev.map((r) =>
                r.id === editingResourceId
                  ? { ...r, sizes: [...r.sizes, newSize] }
                  : r
              )
            );
            setShowAddLayoutModal(false);
            showToast(`已为当前主题追加 ${sizeText(newSize)} 画布布局`);
          }}
        />
      )}

      {showDownloadsModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-base font-bold text-slate-900">
                在线素材下载记录 ({downloaded.length})
              </h2>
              <button
                onClick={() => setShowDownloadsModal(false)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="space-y-2 max-h-60 overflow-y-auto">
              {downloaded.map((id) => {
                const r = getResource(id);
                return (
                  <div
                    key={id}
                    className="p-2.5 rounded-xl border border-slate-100 bg-slate-50 flex items-center justify-between text-xs"
                  >
                    <span className="font-medium text-slate-800">{r.name}</span>
                    <span className="px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 text-[10px] font-medium">
                      已在资源库
                    </span>
                  </div>
                );
              })}
            </div>

            <div className="mt-4 pt-3 border-t border-slate-100 flex justify-end">
              <button
                onClick={() => setShowDownloadsModal(false)}
                className="px-4 py-1.5 text-xs font-medium rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700"
              >
                关闭
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
