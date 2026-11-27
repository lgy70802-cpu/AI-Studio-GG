import React from 'react';
import {
  Home,
  Cloud,
  FolderOpen,
  PenTool,
  ListVideo,
  Clock,
  Puzzle,
  Monitor,
  Settings,
  ArrowRight,
  PanelLeftClose,
  PanelLeftOpen,
  EyeOff,
} from 'lucide-react';
import { DeviceInfo, NavSection } from '../data/themes';

export type SidebarMode = 'full' | 'icons' | 'hidden';

interface SidebarProps {
  activeNav: NavSection;
  onSelectNav: (nav: NavSection) => void;
  activeDevice: DeviceInfo;
  isPlaying: boolean;
  mode?: SidebarMode;
  onChangeMode?: (mode: SidebarMode) => void;
}

interface NavItemConfig {
  id: NavSection;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

const NAV_GROUPS: { title?: string; items: NavItemConfig[] }[] = [
  {
    items: [{ id: 'home', label: '首页', icon: Home }],
  },
  {
    title: '发现',
    items: [{ id: 'online', label: '在线资源', icon: Cloud }],
  },
  {
    title: '我的',
    items: [
      { id: 'library', label: '资源库', icon: FolderOpen },
      { id: 'creator', label: '创作中心', icon: PenTool },
      { id: 'playlist', label: '轮播列表', icon: ListVideo },
      { id: 'schedule', label: '定时切换', icon: Clock },
      { id: 'plugins', label: '插件中心', icon: Puzzle },
    ],
  },
  {
    title: '设备与偏好',
    items: [
      { id: 'devices', label: '设备管理', icon: Monitor },
      { id: 'settings', label: '设置', icon: Settings },
    ],
  },
];

export const Sidebar: React.FC<SidebarProps> = ({
  activeNav,
  onSelectNav,
  activeDevice,
  isPlaying,
  mode = 'full',
  onChangeMode,
}) => {
  if (mode === 'hidden') {
    return null;
  }

  const isIconsOnly = mode === 'icons';

  return (
    <aside
      className={`flex h-full shrink-0 flex-col justify-between overflow-y-auto border-r border-[#ECEAF6] bg-white/88 backdrop-blur-xl transition-all duration-200 studio-scroll max-lg:hidden ${
        isIconsOnly ? 'w-[64px] items-center px-2 py-4' : 'w-[180px] px-3.5 py-5'
      }`}
    >
      <div className={isIconsOnly ? 'flex w-full flex-col items-center' : ''}>
        {/* Brand Lockup */}
        <div
          className={`group relative mb-5 flex items-center ${
            isIconsOnly ? 'flex-col gap-2' : 'justify-between gap-1'
          }`}
        >
          <button
            type="button"
            onClick={() => onSelectNav('home')}
            title="小屏宝PixBuddy · 返回首页"
            className="flex w-full items-center justify-center rounded-xl py-1 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
          >
            <img
              src={isIconsOnly ? '/pixbuddy-logo.png' : '/pixbuddy-wordmark.png'}
              alt="PIXBUDDY"
              className={isIconsOnly ? 'h-8 w-8 object-contain' : 'h-10 w-full object-contain'}
            />
          </button>

          {onChangeMode && !isIconsOnly && (
            <button
              type="button"
              onClick={() => onChangeMode('icons')}
              title="收起为仅显示图标"
              aria-label="收起为仅显示图标"
              className="absolute -right-1 -top-3 rounded-lg bg-white p-1 text-[#7A7690] opacity-0 transition-opacity group-hover:opacity-100 focus-visible:opacity-100 hover:bg-[#EFEBFD] hover:text-[#5E45F8]"
            >
              <PanelLeftClose className="h-4 w-4" />
            </button>
          )}
        </div>

        {/* Navigation Groups */}
        <nav className={isIconsOnly ? 'w-full space-y-2.5' : 'space-y-3.5'} aria-label="主导航">
          {NAV_GROUPS.map((group, gIdx) => (
            <div key={group.title ?? `group-${gIdx}`}>
              {group.title &&
                (isIconsOnly ? (
                  <div className="mx-auto my-2 w-7 border-t border-[#ECEAF6]" />
                ) : (
                  <div className="mb-1.5 mt-3.5 border-t border-[#F1EFF8] pt-3 text-[11px] font-semibold tracking-wide text-[#9A96AE]">
                    {group.title}
                  </div>
                ))}
              <ul className="space-y-1">
                {group.items.map((item) => {
                  const Icon = item.icon;
                  const isActive = activeNav === item.id;
                  return (
                    <li
                      key={item.id}
                      className={`relative ${isIconsOnly ? 'flex justify-center' : ''}`}
                    >
                      <button
                        type="button"
                        onClick={() => onSelectNav(item.id)}
                        title={item.label}
                        aria-label={item.label}
                        className={`group relative flex items-center rounded-xl transition-all duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8] ${
                          isIconsOnly
                            ? 'h-9 w-9 justify-center p-0'
                            : `w-full gap-2.5 py-1.5 pr-1 text-left ${
                                isActive
                                  ? 'font-semibold text-[#1A172B]'
                                  : 'font-medium text-[#6B6880] hover:text-[#1A172B]'
                              }`
                        }`}
                      >
                        <span
                          className={`flex shrink-0 items-center justify-center rounded-xl transition-all duration-150 ${
                            isIconsOnly ? 'h-9 w-9' : 'h-8 w-8'
                          } ${
                            isActive
                              ? 'bg-gradient-to-br from-[#6B53FF] to-[#563CEB] text-white shadow-[0_8px_18px_rgba(94,69,248,0.32)]'
                              : 'border border-[#EBE9F5] bg-white text-[#7A7690] group-hover:border-[#D6CFF9] group-hover:text-[#5E45F8]'
                          }`}
                        >
                          <Icon className="h-[15px] w-[15px]" />
                        </span>
                        {!isIconsOnly && (
                          <span className="truncate whitespace-nowrap text-[13px]">
                            {item.label}
                          </span>
                        )}
                      </button>
                      {isActive && (
                        <span
                          aria-hidden="true"
                          className={`absolute top-1/2 h-6 w-[3.5px] -translate-y-1/2 rounded-l-full bg-[#5E45F8] ${
                            isIconsOnly ? '-right-2' : '-right-3.5'
                          }`}
                        />
                      )}
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </nav>
      </div>

      {/* Bottom Cosmic Aurora Hardware Card / Compact Icon Mode Controls */}
      {isIconsOnly ? (
        <div className="mt-4 flex w-full flex-col items-center gap-2 border-t border-[#F1EFF8] pt-3">
          <button
            type="button"
            onClick={() => onSelectNav('devices')}
            title={`${activeDevice.name} · ${activeDevice.resolution} (${activeDevice.status === '离线' ? '设备已离线' : isPlaying ? '输出中' : '已暂停'})`}
            aria-label="管理设备"
            className="aurora-dark-card flex h-10 w-10 items-center justify-center rounded-2xl text-white shadow-[0_10px_22px_rgba(24,9,66,0.28)] transition-transform duration-150 hover:scale-105 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#5E45F8]"
          >
            <Monitor className={`h-4 w-4 ${isPlaying ? 'text-white' : 'text-white/60'}`} />
          </button>
          {onChangeMode && (
            <div className="flex w-full flex-col items-center gap-1">
              <button
                type="button"
                onClick={() => onChangeMode('full')}
                title="展开完整导航栏"
                aria-label="展开完整导航栏"
                className="flex h-8 w-8 items-center justify-center rounded-xl border border-[#EBE9F5] bg-white text-[#6B6880] hover:border-[#5E45F8] hover:text-[#5E45F8]"
              >
                <PanelLeftOpen className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                onClick={() => onChangeMode('hidden')}
                title="隐藏最左侧栏"
                aria-label="隐藏最左侧栏"
                className="flex h-8 w-8 items-center justify-center rounded-xl border border-[#EBE9F5] bg-white text-[#6B6880] hover:border-[#5E45F8] hover:text-[#5E45F8]"
              >
                <EyeOff className="h-3.5 w-3.5" />
              </button>
            </div>
          )}
        </div>
      ) : (
      <div className="mt-5">
        <div className="aurora-dark-card relative overflow-hidden rounded-[20px] p-3.5 text-white shadow-[0_18px_36px_rgba(24,9,66,0.28)]">
          <div className="relative z-10">
            <div className="truncate text-[13px] font-bold tracking-tight text-white">
              {activeDevice.name}
            </div>
            <div className="mt-0.5 truncate font-mono-tabular text-[10.5px] text-white/70">
              {activeDevice.resolution} · {activeDevice.status === '离线' ? '设备已离线' : isPlaying ? '输出中' : '已暂停'}
            </div>

            {/* Isometric 3D Prism Graphic matching Figure 2 */}
            <div className="my-2.5 flex items-center justify-center">
              <svg
                viewBox="0 0 160 86"
                className="h-[58px] w-full"
                aria-hidden="true"
              >
                <defs>
                  <linearGradient id="sideBarCubeTop" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stopColor="#F5D0FE" />
                    <stop offset="100%" stopColor="#8B5CF6" />
                  </linearGradient>
                  <linearGradient id="sideBarCubeLeft" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#7C5CFF" />
                    <stop offset="100%" stopColor="#3B1D8E" />
                  </linearGradient>
                  <linearGradient id="sideBarCubeRight" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stopColor="#C084FC" />
                    <stop offset="100%" stopColor="#5E45F8" />
                  </linearGradient>
                </defs>
                <path
                  d="M20 46 L42 32 L54 38 L78 20"
                  fill="none"
                  stroke="#A78BFA"
                  strokeWidth="1.6"
                  strokeLinecap="round"
                  opacity="0.7"
                />
                <path
                  d="M72 20 L78 20 L77 26"
                  fill="none"
                  stroke="#A78BFA"
                  strokeWidth="1.6"
                  strokeLinecap="round"
                  opacity="0.7"
                />
                {/* Small left block */}
                <polygon points="56,52 74,42 92,52 74,62" fill="url(#sideBarCubeTop)" />
                <polygon points="56,52 74,62 74,82 56,72" fill="url(#sideBarCubeLeft)" />
                <polygon points="92,52 74,62 74,82 92,72" fill="url(#sideBarCubeRight)" />
                {/* Tall right block */}
                <polygon points="90,34 114,21 138,34 114,47" fill="url(#sideBarCubeTop)" />
                <polygon points="90,34 114,47 114,82 90,69" fill="url(#sideBarCubeLeft)" />
                <polygon points="138,34 114,47 114,82 138,69" fill="url(#sideBarCubeRight)" />
              </svg>
            </div>

            <button
              type="button"
              onClick={() => onSelectNav('devices')}
              className="flex w-full items-center justify-between rounded-full border border-white/15 bg-black/35 px-3 py-1.5 text-[11.5px] font-semibold text-white backdrop-blur-md transition-all duration-150 hover:bg-white/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/60 whitespace-nowrap"
            >
              <span className="flex items-center gap-1.5">
                <span className="flex h-4 w-4 items-center justify-center rounded-full bg-[#5E45F8] text-white">
                  <Monitor className="h-2.5 w-2.5" />
                </span>
                管理设备
              </span>
              <ArrowRight className="h-3 w-3 text-white/80" />
            </button>
          </div>
        </div>
      </div>
      )}
    </aside>
  );
};
